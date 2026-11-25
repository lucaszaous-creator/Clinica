using Clinica.Application.Servicos;
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
// AddClinica — no Postgres (CLINICA_TESTES_POSTGRES), onde a permissão é de verdade.
//
// ⚠️ E no CI, que roda SÓ SQLite (nov/2026): o SQLite não tem permissão, então os MESMOS fluxos
// rodam com um interceptador que anota cada INSERT/UPDATE/DELETE da ponte e, ao fim do teste,
// confere contra a lista do papel-ponte-crm.sql. Foi assim que a fila de envios parou em
// produção (42501 no claim): a regra estava no arquivo, mas nada rodava este teste no CI. Fluxo
// novo que grave em tabela fora da lista agora reprova no PR.
public sealed class PontePapelRestritoTests : IDisposable
{
    const string Fone = "5511999990001";
    const string Senha = "senha-de-teste-da-ponte";
    readonly SqliteConnection conexao = new("Data Source=:memory:");
    readonly ClinicaDbContext admin;
    readonly ClinicaDbContext? ponte;
    readonly GravacoesDaPonte? gravacoes;

    public PontePapelRestritoTests()
    {
        conexao.Open();
        admin = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);
        admin.Database.EnsureCreated();
        if (!BancoDosTestes.NoPostgres)
        {
            // SQLite (o CI): a mesma base, e cada gravação da ponte anotada para a conferência do fim.
            gravacoes = new GravacoesDaPonte();
            ponte = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).AddInterceptors(gravacoes).Options);
            return;
        }

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

    /// <summary>
    /// O que a ponte NÃO pode fazer. No Postgres, recusado com 42501; no SQLite não há permissão para
    /// provar, e rodar o comando anotaria uma gravação que não existe — a conferência é a do Postgres.
    /// </summary>
    async Task Negado(Func<Task> acao)
    {
        if (!BancoDosTestes.NoPostgres) return;
        var e = await Assert.ThrowsAnyAsync<Exception>(acao);
        Assert.Contains("42501", e.ToString());
    }

    /// <summary>{tabela → privilégios} que o papel-ponte-crm.sql concede (a lista do FOREACH e o GRANT por coluna).</summary>
    internal static IReadOnlyDictionary<string, HashSet<string>> Concessoes(string sql)
    {
        var mapa = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        void Add(string tabela, string priv) { if (!mapa.TryGetValue(tabela, out var c)) mapa[tabela] = c = new(StringComparer.Ordinal); c.Add(priv); }
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(sql, @"\['(\w+)','([^']+)'\]"))
            foreach (var priv in m.Groups[2].Value.Split(','))
                Add(m.Groups[1].Value, System.Text.RegularExpressions.Regex.Replace(priv, @"\(.*\)", "").Trim());
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(sql, @"GRANT (\w+) \([^)]*\) ON public\.""(\w+)"""))
            Add(m.Groups[2].Value, m.Groups[1].Value);
        return mapa;
    }

    [Fact] public void A_lista_do_arquivo_e_lida_pela_conferencia()
    {
        var c = Concessoes(File.ReadAllText(Arquivo()));
        Assert.Contains("UPDATE", c["EnviosPacienteCrm"]);
        Assert.DoesNotContain("INSERT", c["EnviosPacienteCrm"]);
        Assert.Contains("INSERT", c["AvisosDeLiberacao"]);
        Assert.Contains("UPDATE", c["LiberacoesDeTrava"]);
        Assert.Contains("UPDATE", c["Usuarios"]);
        Assert.Equal(["INSERT"], c["Auditoria"]);
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
        await Horario();
        await Negado(() => ponte!.Database.ExecuteSqlRawAsync("UPDATE \"Usuarios\" SET \"Nome\" = \"Nome\""));
        await Negado(() => ponte!.Database.ExecuteSqlRawAsync("DELETE FROM \"Agendamentos\""));
    }

    [Fact] public async Task Confirmar_pelo_lembrete()
    {
        var a = await Horario(); var (_, agenda) = Servicos();
        var r = await new ClinicAgendaBridge(ponte!, agenda).Decide(new(Guid.NewGuid().ToString("N"), a.Id, ClinicAgendaBridge.Revision(a), Fone, "confirm", "msg-1"), default);
        Assert.Equal("confirmed", r.Status);
    }

    [Fact] public async Task Marcar_com_guia_no_agendamento_e_cancelar()
    {
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
        var a = await Horario();
        await new ClinicRecallBridge(ponte!).Notice(new(Guid.NewGuid().ToString("N"), a.PacienteId, null, "sms:1", DateTimeOffset.UtcNow, "sms"), default);
        Assert.Single(await admin.Contatos.ToListAsync());
    }

    [Fact] public async Task Gravar_o_resumo_do_dia()
    {
        var bridge = new ClinicSummaryBridge(ponte!);
        var dia = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        await bridge.Daily(new(dia, DateTimeOffset.UtcNow.AddMinutes(-1), 3, 2, 1, 10, 9, 90, 4, 2, 2, 0, 3, 2, 1, 1, 0), default);
        await bridge.Daily(new(dia, DateTimeOffset.UtcNow, 5, 2, 1, 10, 9, 90, 4, 2, 2, 0, 3, 2, 1, 1, 0), default);
        Assert.Equal(5, (await admin.ResumosCrmDiarios.SingleAsync()).ConversasIniciadas);
    }

    // Um PARAR só (fase 3 Recepção×CRM, out/2026): a ponte revoga a autorização de mensagens —
    // UPDATE na linha vigente e INSERT da recusa em Consentimentos, mais a trilha. (O lembrete de
    // SMS 24h que este arquivo testava saiu: quem manda SMS é só o CRM.)
    [Fact] public async Task O_PARAR_revoga_a_autorizacao_de_mensagens()
    {
        var a = await Horario();
        admin.Consentimentos.Add(new() { PacienteId = a.PacienteId, Finalidade = FinalidadeConsentimento.ComunicacaoEMarketing, Concedido = true });
        await admin.SaveChangesAsync();
        await new ClinicRecallBridge(ponte!).OptOut(new(Guid.NewGuid().ToString("N"), Fone, "wamid.1", DateTimeOffset.UtcNow), default);
        Assert.Equal(2, await admin.Consentimentos.CountAsync());
        Assert.NotNull((await admin.Consentimentos.AsNoTracking().OrderBy(c => c.Id).FirstAsync()).RevogadoEm);
    }

    // A fila de envios ao paciente (out/2026): o desktop PÕE o pedido (aqui, o admin); a ponte
    // lista, reserva, autoriza e registra o resultado como ponte_crm — UPDATE em
    // EnviosPacienteCrm e mais nada. Sem a linha no papel-ponte-crm.sql, reprova com 42501.
    [Fact] public async Task Fila_de_envios_ao_paciente()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var a = await Horario();
        var repoAdmin = new ClinicaRepositorio(admin);
        var doc = await new DocumentoClinicoService(repoAdmin, new ProntuarioService(repoAdmin), new ConsentimentoService(repoAdmin))
            .EmitirAsync(new DocumentoClinico { Tipo = TipoDocumentoClinico.Comparecimento, PacienteId = a.PacienteId, ProfissionalId = a.ProfissionalId, Data = DateOnly.FromDateTime(DateTime.Today) }, "ana");
        var envio = new EnvioAoPacienteService(repoAdmin, new DocumentosClinicosPdfService(repoAdmin), new ParametrosService(repoAdmin));
        Assert.True((await envio.EnfileirarWhatsAppAsync(doc.Id, "ana")).Ok);

        var fila = new FilaEnviosCrmService(new ClinicaRepositorio(ponte!));
        var item = Assert.Single(await fila.ListarAsync("crm-1"));
        Assert.NotNull((await fila.ReservarAsync(item.Id, "crm-1", 2)).Arquivo);
        Assert.True((await fila.AutorizarAsync(item.Id, "crm-1", 2)).Concedida);
        Assert.Equal("accepted", (await fila.RegistrarAsync(item.Id, "crm-1", "accepted", "ok")).Estado);
        Assert.Equal("accepted", (await admin.EnviosPacienteCrm.AsNoTracking().SingleAsync()).Estado);

        await Negado(() => ponte!.Database.ExecuteSqlRawAsync("DELETE FROM \"EnviosPacienteCrm\""));
    }

    // O documento pedido pelo WhatsApp (@SemDor fase 4): a ponte grava a conversa e o RASCUNHO,
    // e nada em DocumentosClinicos — quem emite é o portal.
    [Fact] public async Task Documento_pelo_WhatsApp_grava_so_o_rascunho()
    {
        var a = await Horario();
        admin.Usuarios.Add(new UsuarioSistema { Login = "medica", Nome = "Médica fictícia", Perfil = PerfilAcesso.Profissional,
            ProfissionalId = a.ProfissionalId, TelefoneWhatsApp = "5522999422253", WhatsAppLigadoEm = DateTime.Today.AddDays(-1) });
        await admin.SaveChangesAsync();

        var r = await new AssistenteWhatsAppService(ponte!, new OpcoesDocumentoPeloWhatsApp(true, "https://portal.exemplo"))
            .ResponderAsync("5522999422253", "comparecimento Pessoa fictícia", "wamid.ponte-doc");
        Assert.Contains("assinar.html#", r.Texto);
        var p = await admin.PedidosDocumentoWhatsApp.AsNoTracking().SingleAsync();
        Assert.Equal((a.PacienteId, "comparecimento"), (p.PacienteId, p.Tipo));

        await Negado(() => ponte!.Database.ExecuteSqlRawAsync("UPDATE \"PedidosDocumentoWhatsApp\" SET \"AbertoEm\" = now()"));
    }

    // O CRM dentro da Recepção: o app grava o bilhete; a ponte só o gasta (UsadoEm), e não cria
    // bilhete nem mexe em outra coluna — com esta credencial ninguém fabrica entrada no CRM.
    [Fact] public async Task Bilhete_da_Recepcao_a_ponte_so_gasta()
    {
        var ana = new UsuarioSistema { Login = "ana-bilhete", Nome = "Ana", Perfil = PerfilAcesso.Recepcao };
        admin.Usuarios.Add(ana); await admin.SaveChangesAsync();
        var bilhete = await new EntradaNoCrmService(admin).EmitirAsync(ana.Id);

        Assert.Equal(ana.Id, await new EntradaNoCrmService(ponte!).ConferirAsync(bilhete));
        Assert.Null(await new EntradaNoCrmService(ponte!).ConferirAsync(bilhete));

        await Negado(() => new EntradaNoCrmService(ponte!).EmitirAsync(ana.Id));
        await Negado(() => ponte!.Database.ExecuteSqlRawAsync("UPDATE \"BilhetesEntradaCrm\" SET \"VenceEm\" = now()"));
    }

    // O @SemDor com IA nos apps (Clinica#307): ler a fila (pulso), responder a releitura no chat e
    // substituir a lista de avisos — e nada além: a pergunta não se apaga pela ponte.
    [Fact] public async Task SemDor_nos_apps_fila_e_avisos()
    {
        await Horario(0, DateTime.Today.AddHours(10));
        var ana = new UsuarioSistema { Login = "ana-semdor", Nome = "Ana", Perfil = PerfilAcesso.Recepcao };
        admin.Usuarios.Add(ana);
        await admin.SaveChangesAsync();
        admin.PerguntasAoSemDor.Add(new PerguntaAoSemDor { UsuarioId = ana.Id, Conversa = ChatEquipeRegras.Sistema(ana.Id),
            Texto = "qual a senha do wifi", CriadaEm = DateTime.Now, Situacao = SemDorNosAppsRegras.Aguardando });
        await admin.SaveChangesAsync();

        var bridge = new ClinicSemDorBridge(ponte!, new PerguntasAoSemDorService(ponte!));
        var fila = await bridge.Questions(default);
        Assert.Single(fila);
        Assert.Equal("answered", (await bridge.Answer(new(fila[0].Id, "agenda de hoje"), default)).Status);
        Assert.Contains(await admin.MensagensEquipe.AsNoTracking().Select(m => m.Texto).ToListAsync(), t => t.StartsWith("Entendi como"));

        var agora = DateTimeOffset.UtcNow;
        await bridge.Alerts(new(agora.AddSeconds(-1), [new("semdor:a1", "Maria", "Sintoma grave", agora, "https://crm.exemplo/#inbox/c1")]), default);
        Assert.Equal("recorded", (await bridge.Alerts(new(agora, []), default)).Status);
        Assert.Equal(0, await admin.AvisosDoSemDor.CountAsync());

        await Negado(() => ponte!.Database.ExecuteSqlRawAsync("DELETE FROM \"PerguntasAoSemDor\""));
    }

    // O login do CRM conta tentativas, trava e marca o último acesso — e só essas colunas: a
    // senha e as permissões continuam fora do alcance do usuário da ponte.
    [Fact] public async Task Login_do_CRM_com_o_usuario_do_sistema()
    {
        var (hash, sal) = HashSenha.Gerar("senha-boa-do-balcao");
        admin.Usuarios.Add(new UsuarioSistema { Login = "ana", Nome = "Ana", Perfil = PerfilAcesso.Recepcao, SenhaHash = hash, SenhaSalt = sal });
        await admin.SaveChangesAsync();
        var repo = new ClinicaRepositorio(ponte!);
        var bridge = new ClinicTeamBridge(repo, new AcessoService(repo), new EntradaNoCrmService(ponte!));

        for (var i = 0; i < AcessoService.TentativasAteTravar; i++)
            Assert.Equal(401, ((Microsoft.AspNetCore.Http.IStatusCodeHttpResult)await bridge.Enter(new("ana", "errada"), default)).StatusCode);
        // A migration da fase 1 do @SemDor semeia o usuário técnico: procurar pelo login.
        Assert.NotNull((await admin.Usuarios.AsNoTracking().SingleAsync(u => u.Login == "ana")).BloqueadoAte);

        await Negado(() => ponte!.Database.ExecuteSqlRawAsync("UPDATE \"Usuarios\" SET \"SenhaHash\" = 'x'"));
    }

    // A liberação da trava de consulta (nov/2026): o pedido nasce no app (aqui, o admin); pelo
    // WhatsApp a gerência decide — a ponte grava a decisão, marca o horário pela agenda, conta à
    // secretária e registra o aviso que o CRM entrega. Sem LiberacoesDeTrava/AvisosDeLiberacao no
    // papel, isto parava com 42501 na clínica (foi aplicado à mão em nov/2026).
    [Fact] public async Task Liberacao_da_trava_pela_gerencia_no_WhatsApp()
    {
        var a = await Horario();
        var secretaria = new UsuarioSistema { Login = "rayssa", Nome = "Rayssa", Perfil = PerfilAcesso.Recepcao, Ativo = true,
            TelefoneWhatsApp = "5522999990002", WhatsAppLigadoEm = DateTime.Today.AddDays(-1) };
        var gerente = new UsuarioSistema { Login = "ana-gerente", Nome = "Ana Gerente", Perfil = PerfilAcesso.Gerente, Ativo = true,
            AprovaLiberacaoDeTrava = true, TelefoneWhatsApp = "5522999990001", WhatsAppLigadoEm = DateTime.Today.AddDays(-1) };
        admin.Usuarios.AddRange(secretaria, gerente);
        await admin.SaveChangesAsync();
        var quando = DateTime.Today.AddDays(6).AddHours(15);
        admin.LiberacoesDeTrava.Add(new LiberacaoDeTrava
        {
            PacienteId = a.PacienteId, PacienteNome = "Pessoa fictícia", ConvenioNome = "Unimed", DataHora = quando,
            ProfissionalId = a.ProfissionalId, ProfissionalNome = "Profissional fictício", DuracaoMinutos = 30,
            ModalidadeCodigo = "Consulta", PrazoDias = 15, ConsultaAnteriorEm = a.DataHora, Motivo = "Retorno com exames (fictício)",
            PedidoPorId = secretaria.Id, PedidoPorNome = secretaria.Nome, PedidoEm = DateTime.Now, ExpiraEm = DateTime.Now.AddHours(12)
        });
        await admin.SaveChangesAsync();
        var eu = await ponte!.Usuarios.AsNoTracking().SingleAsync(u => u.Id == gerente.Id);

        var resposta = await LiberacaoDeTravaService.Sobre(ponte).DecidirPorTextoAsync(eu, "sim", "whatsapp");
        Assert.Contains("LIBERADO", resposta);
        Assert.Equal(LiberacaoDeTrava.Liberada, (await admin.LiberacoesDeTrava.AsNoTracking().SingleAsync()).Estado);

        var avisos = await LiberacaoDeTravaService.Sobre(ponte).AvisosParaEquipeAsync();
        Assert.NotEmpty(avisos);
        foreach (var aviso in avisos) await LiberacaoDeTravaService.Sobre(ponte).ConfirmarAvisoAsync(aviso.Id, true, null);

        await Negado(() => ponte.Database.ExecuteSqlRawAsync("DELETE FROM \"LiberacoesDeTrava\""));
    }

    public void Dispose()
    {
        // SQLite (o CI): toda gravação que a ponte fez neste teste precisa estar no papel-ponte-crm.sql.
        if (gravacoes is not null)
        {
            var concedido = Concessoes(File.ReadAllText(Arquivo()));
            var fora = gravacoes.Feitas.Where(g => !concedido.TryGetValue(g.Tabela, out var p) || !p.Contains(g.Privilegio))
                .Select(g => $"{g.Privilegio} em \"{g.Tabela}\"").Distinct().OrderBy(x => x).ToList();
            ponte?.Dispose(); admin.Dispose(); conexao.Dispose();
            Assert.True(fora.Count == 0,
                "A ponte grava fora do papel-ponte-crm.sql (em produção, 42501): " + string.Join(", ", fora)
                + ". Acrescente a linha no arquivo — o deploy do portal a reaplica.");
            return;
        }
        if (ponte is not null) { NpgsqlConnection.ClearPool(new NpgsqlConnection(ponte.Database.GetConnectionString())); ponte.Dispose(); }
        admin.Dispose(); conexao.Dispose();
    }
}

/// <summary>Anota as gravações (INSERT/UPDATE/DELETE, por tabela) que um contexto manda ao banco.</summary>
internal sealed class GravacoesDaPonte : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
{
    static readonly System.Text.RegularExpressions.Regex Comando = new(
        @"\b(INSERT\s+INTO|UPDATE|DELETE\s+FROM)\s+""(\w+)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    readonly System.Collections.Concurrent.ConcurrentBag<(string Privilegio, string Tabela)> feitas = [];
    public IReadOnlyCollection<(string Privilegio, string Tabela)> Feitas => feitas;

    void Anotar(System.Data.Common.DbCommand c)
    {
        foreach (System.Text.RegularExpressions.Match m in Comando.Matches(c.CommandText))
            feitas.Add((m.Groups[1].Value.Split(' ', '\t', '\n', '\r')[0].ToUpperInvariant(), m.Groups[2].Value));
    }

    public override Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> NonQueryExecuting(System.Data.Common.DbCommand c,
        Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData e, Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> r)
    { Anotar(c); return r; }
    public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> NonQueryExecutingAsync(System.Data.Common.DbCommand c,
        Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData e, Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> r, CancellationToken ct = default)
    { Anotar(c); return ValueTask.FromResult(r); }
    public override Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> ReaderExecuting(System.Data.Common.DbCommand c,
        Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData e, Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> r)
    { Anotar(c); return r; }
    public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(System.Data.Common.DbCommand c,
        Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData e, Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> r, CancellationToken ct = default)
    { Anotar(c); return ValueTask.FromResult(r); }
    public override Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<object> ScalarExecuting(System.Data.Common.DbCommand c,
        Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData e, Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<object> r)
    { Anotar(c); return r; }
    public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<object>> ScalarExecutingAsync(System.Data.Common.DbCommand c,
        Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData e, Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<object> r, CancellationToken ct = default)
    { Anotar(c); return ValueTask.FromResult(r); }
}
