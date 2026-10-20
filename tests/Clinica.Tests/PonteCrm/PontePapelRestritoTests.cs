using Clinica.Application.Servicos;
using Clinica.Application.Sms;
using Clinica.Crm.Bridge;
using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Clinica.Tests;

// O usuário do banco da ponte (deploy/ponte-crm/papel-ponte-crm.sql) só GRAVA nas tabelas
// que a ponte grava — se a credencial vazar, usuários, prontuário e financeiro ficam só para
// leitura. A lista de tabelas é a parte frágil: uma que faltar vira 42501 na clínica, e os
// outros testes rodam como superusuário e nunca veriam. Aqui os fluxos da ponte rodam com
// ESTE arquivo aplicado, conectados como ponte_crm e com a estratégia que repete do
// AddClinica. Só no Postgres (CLINICA_TESTES_POSTGRES): o SQLite não tem permissão nenhuma.
public sealed class PontePapelRestritoTests : IDisposable
{
    const string Fone = "5511999990001";
    const string Senha = "senha-de-teste-da-ponte";
    readonly SqliteConnection conexao = new("Data Source=:memory:");
    readonly ClinicaDbContext admin;
    readonly ClinicaDbContext? ponte;

    public PontePapelRestritoTests()
    {
        conexao.Open();
        admin = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);
        admin.Database.EnsureCreated();
        if (!BancoDosTestes.NoPostgres) return;

        var cadeia = new NpgsqlConnectionStringBuilder(admin.Database.GetConnectionString());
        var dono = cadeia.Username!;
        var sql = File.ReadAllText(Arquivo())
            .Replace(":senha", $"'{Senha}'")
            .Replace("DATABASE clinica", $"DATABASE \"{cadeia.Database}\"")
            .Replace("FOR ROLE clinica", $"FOR ROLE \"{dono}\"");
        admin.Database.ExecuteSqlRaw(sql.Replace("{", "{{").Replace("}", "}}"));

        cadeia.Username = "ponte_crm"; cadeia.Password = Senha;
        ponte = new(new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseNpgsql(cadeia.ConnectionString, o => o.EnableRetryOnFailure(3, TimeSpan.FromSeconds(1), null)).Options);
    }

    static string Arquivo()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "deploy", "ponte-crm", "papel-ponte-crm.sql"))) d = d.Parent;
        return Path.Combine(d!.FullName, "deploy", "ponte-crm", "papel-ponte-crm.sql");
    }

    async Task<Agendamento> Horario(int diasAFrente = 3, DateTime? quando = null)
    {
        var p = new Paciente { Nome = "Pessoa fictícia", Telefone = "(11) 99999-0001", Convenio = Convenio.UnimedIntercambio };
        var m = new Profissional { Nome = "Profissional fictício", DuracaoPadraoMinutos = 30 };
        admin.Pacientes.Add(p); admin.Profissionais.Add(m); await admin.SaveChangesAsync();
        var a = new Agendamento { PacienteId = p.Id, ProfissionalId = m.Id, DataHora = quando ?? DateTime.Today.AddDays(diasAFrente).AddHours(10), DuracaoMinutos = 30,
            ModalidadePrevista = ModalidadeAtendimento.Consulta, ModalidadeCodigo = "Consulta" };
        admin.Agendamentos.Add(a); await admin.SaveChangesAsync();
        return await ponte!.Agendamentos.Include(x => x.Paciente).SingleAsync(x => x.Id == a.Id);
    }

    // Como o DI da ponte monta: com os parâmetros, que é o que liga a guia na marcação.
    (ClinicaRepositorio repo, AgendaService agenda) Servicos() { var r = new ClinicaRepositorio(ponte!); return (r, new(r, new AtendimentoService(r), new ParametrosService(r))); }

    [Fact] public async Task O_papel_nao_grava_fora_da_lista()
    {
        if (!BancoDosTestes.NoPostgres) return;
        await Horario();
        var e = await Assert.ThrowsAnyAsync<Exception>(() => ponte!.Database.ExecuteSqlRawAsync("UPDATE \"Usuarios\" SET \"Nome\" = \"Nome\""));
        Assert.Contains("42501", e.ToString());
        e = await Assert.ThrowsAnyAsync<Exception>(() => ponte!.Database.ExecuteSqlRawAsync("DELETE FROM \"Agendamentos\""));
        Assert.Contains("42501", e.ToString());
    }

    [Fact] public async Task Confirmar_pelo_lembrete()
    {
        if (!BancoDosTestes.NoPostgres) return;
        var a = await Horario(); var (_, agenda) = Servicos();
        var r = await new ClinicAgendaBridge(ponte!, agenda).Decide(new(Guid.NewGuid().ToString("N"), a.Id, ClinicAgendaBridge.Revision(a), Fone, "confirm", "msg-1"), default);
        Assert.Equal("confirmed", r.Status);
    }

    [Fact] public async Task Marcar_com_guia_no_agendamento_e_cancelar()
    {
        if (!BancoDosTestes.NoPostgres) return;
        await new ParametrosService(new ClinicaRepositorio(admin)).DefinirGuiaNoAgendamentoAsync(true);
        var a = await Horario(); var (repo, agenda) = Servicos();
        var bridge = new ClinicBookingBridge(ponte!, agenda, repo);
        var dia = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        var q = new BookingQuery(Fone, a.PacienteId, a.ProfissionalId!.Value, null, "Consulta", null, 30, dia, dia.AddDays(5), new(8, 0), new(18, 0), []);
        var slots = await bridge.Slots(q, default); Assert.NotEmpty(slots.Slots);
        var hold = new BookingHold(Guid.NewGuid().ToString("N"), q, slots.Slots[0], "Ana");
        await bridge.Hold(hold, default);
        Assert.Equal("confirmed", (await bridge.Confirm(new(hold.Id, Fone, "Ana"), default)).State);
        Assert.True(await admin.Codigos.AnyAsync(), "a marcação com a chave ligada precisa gerar a guia");

        var novo = await ponte!.Agendamentos.AsNoTracking().Include(x => x.Paciente).OrderByDescending(x => x.Id).FirstAsync();
        var c = await bridge.Cancel(new(Guid.NewGuid().ToString("N"), novo.Id, novo.PacienteId, ClinicAgendaBridge.Revision(novo), Fone, "Teste", "Ana"), default);
        Assert.Equal("cancelled", c.Status);
    }

    [Fact] public async Task Remarcar()
    {
        if (!BancoDosTestes.NoPostgres) return;
        var a = await Horario(); var (repo, agenda) = Servicos();
        var bridge = new ClinicSchedulingBridge(ponte!, agenda, new BuscaDeVagasService(repo));
        var from = new DateTimeOffset(DateTime.SpecifyKind(DateTime.Today.AddDays(1).AddHours(9), DateTimeKind.Unspecified), TimeSpan.FromHours(-3));
        var q = new ClinicSlotQuery(a.Id, a.PacienteId, ClinicAgendaBridge.Revision(a), Fone, from);
        var slots = await bridge.Slots(q, default); Assert.NotEmpty(slots.StartsAt);
        var r = await bridge.Reschedule(new(Guid.NewGuid().ToString("N"), a.Id, a.PacienteId, q.Revision, Fone, slots.StartsAt[0], "Ana"), default);
        Assert.Equal(slots.StartsAt[0], r.StartsAt);
    }

    [Fact] public async Task Registrar_o_recall()
    {
        if (!BancoDosTestes.NoPostgres) return;
        var a = await Horario();
        await new ClinicRecallBridge(ponte!).Notice(new(Guid.NewGuid().ToString("N"), a.PacienteId, null, "sms:1", DateTimeOffset.UtcNow, "sms"), default);
        Assert.Single(await admin.Contatos.ToListAsync());
    }

    [Fact] public async Task Gravar_o_resumo_do_dia()
    {
        if (!BancoDosTestes.NoPostgres) return;
        var bridge = new ClinicSummaryBridge(ponte!);
        var dia = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        await bridge.Daily(new(dia, DateTimeOffset.UtcNow.AddMinutes(-1), 3, 2, 1, 10, 9, 90, 4, 2, 2, 0, 3, 2, 1, 1, 0), default);
        await bridge.Daily(new(dia, DateTimeOffset.UtcNow, 5, 2, 1, 10, 9, 90, 4, 2, 2, 0, 3, 2, 1, 1, 0), default);
        Assert.Equal(5, (await admin.ResumosCrmDiarios.SingleAsync()).ConversasIniciadas);
    }

    [Fact] public async Task O_lembrete_de_SMS_24h()
    {
        if (!BancoDosTestes.NoPostgres) return;
        var parametrosAdmin = new ParametrosService(new ClinicaRepositorio(admin), new ProtecaoSegredoGlobal(Convert.ToBase64String(new byte[32])));
        await parametrosAdmin.SalvarCamposSmsAsync(new CamposSms(ProvedorSms.Zenvia, null, null, "tok-123", "clinica", null, null, 24, false));
        await parametrosAdmin.SalvarUrlPublicacaoAsync("https://docs.clinica.exemplo");
        var quando = DateTime.Today.AddDays(1).AddHours(9);
        await Horario(quando: quando);

        var repo = new ClinicaRepositorio(ponte!);
        var gateway = new GatewayDoTeste();
        var r = await new LembreteSmsService(repo, new CampanhaService(repo), new ParametrosService(repo, new ProtecaoSegredoGlobal(Convert.ToBase64String(new byte[32]))),
            gateway, new ArmazenamentoFake(), () => quando.AddHours(-20)).RodarAsync();
        Assert.True(r.Enviados == 1, r.ToString()); // Falha de permissão vira Falhas aqui, não exceção
        Assert.Single(await admin.MensagensSms.ToListAsync());
    }

    // A fila de envios ao paciente (out/2026): o desktop PÕE o pedido (aqui, o admin); a ponte
    // lista, reserva, autoriza e registra o resultado como ponte_crm — UPDATE em
    // EnviosPacienteCrm e mais nada. Sem a linha no papel-ponte-crm.sql, reprova com 42501.
    [Fact] public async Task Fila_de_envios_ao_paciente()
    {
        if (!BancoDosTestes.NoPostgres) return;
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var a = await Horario();
        var repoAdmin = new ClinicaRepositorio(admin);
        var doc = await new DocumentoClinicoService(repoAdmin, new ProntuarioService(repoAdmin), new ConsentimentoService(repoAdmin))
            .EmitirAsync(new DocumentoClinico { Tipo = TipoDocumentoClinico.Comparecimento, PacienteId = a.PacienteId, ProfissionalId = a.ProfissionalId, Data = DateOnly.FromDateTime(DateTime.Today) }, "ana");
        var envio = new EnvioAoPacienteService(repoAdmin, new DocumentosClinicosPdfService(repoAdmin), new ParametrosService(repoAdmin), new EnviadorSemUso());
        Assert.True((await envio.EnfileirarWhatsAppAsync(doc.Id, "ana")).Ok);

        var fila = new FilaEnviosCrmService(new ClinicaRepositorio(ponte!));
        var item = Assert.Single(await fila.ListarAsync("crm-1"));
        Assert.NotNull((await fila.ReservarAsync(item.Id, "crm-1", 2)).Arquivo);
        Assert.True((await fila.AutorizarAsync(item.Id, "crm-1", 2)).Concedida);
        Assert.Equal("accepted", (await fila.RegistrarAsync(item.Id, "crm-1", "accepted", "ok")).Estado);
        Assert.Equal("accepted", (await admin.EnviosPacienteCrm.AsNoTracking().SingleAsync()).Estado);

        var e = await Assert.ThrowsAnyAsync<Exception>(() => ponte!.Database.ExecuteSqlRawAsync("DELETE FROM \"EnviosPacienteCrm\""));
        Assert.Contains("42501", e.ToString());
    }

    sealed class EnviadorSemUso : Clinica.Application.Email.IEnviadorDeEmail
    {
        public Task EnviarAsync(Clinica.Application.Email.OpcoesEmail opcoes, string destinatario, string assunto, string corpo, CancellationToken ct = default)
            => throw new InvalidOperationException();
    }

    // O login do CRM conta tentativas, trava e marca o último acesso — e só essas colunas: a
    // senha e as permissões continuam fora do alcance do usuário da ponte.
    [Fact] public async Task Login_do_CRM_com_o_usuario_do_sistema()
    {
        if (!BancoDosTestes.NoPostgres) return;
        var (hash, sal) = HashSenha.Gerar("senha-boa-do-balcao");
        admin.Usuarios.Add(new UsuarioSistema { Login = "ana", Nome = "Ana", Perfil = PerfilAcesso.Recepcao, SenhaHash = hash, SenhaSalt = sal });
        await admin.SaveChangesAsync();
        var repo = new ClinicaRepositorio(ponte!);
        var bridge = new ClinicTeamBridge(repo, new AcessoService(repo));

        for (var i = 0; i < AcessoService.TentativasAteTravar; i++)
            Assert.Equal(401, ((Microsoft.AspNetCore.Http.IStatusCodeHttpResult)await bridge.Enter(new("ana", "errada"), default)).StatusCode);
        Assert.NotNull((await admin.Usuarios.AsNoTracking().SingleAsync()).BloqueadoAte);

        var e = await Assert.ThrowsAnyAsync<Exception>(() => ponte!.Database.ExecuteSqlRawAsync("UPDATE \"Usuarios\" SET \"SenhaHash\" = 'x'"));
        Assert.Contains("42501", e.ToString());
    }

    sealed class GatewayDoTeste : IGatewaySms
    {
        public Task<string?> EnviarAsync(OpcoesSms opcoes, string celularE164, string texto, CancellationToken ct = default)
            => Task.FromResult<string?>("id-1");
    }

    public void Dispose()
    {
        if (ponte is not null) { NpgsqlConnection.ClearPool(new NpgsqlConnection(ponte.Database.GetConnectionString())); ponte.Dispose(); }
        admin.Dispose(); conexao.Dispose();
    }
}
