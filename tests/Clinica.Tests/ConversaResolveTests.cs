using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A CONVERSA RESOLVE (25/10 — docs/whatsapp-nativo-implementacao.md §16): as frases do que a clínica fez
/// (marcar e cancelar), a lista e o relato da passagem do turno, as regras de guardar na ficha e de perguntar
/// ao profissional — e os dois serviços sobre o app montado pelo DI e o SQLite da casa: o arquivo vai aos
/// arquivos da ficha com a trilha e o operador de quem entrou, e a pergunta chega à direta do chat da equipe
/// com o paciente anexado.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class ConversaResolveTests : IDisposable
{
    private static readonly DateOnly Hoje = new(2026, 10, 23);

    private readonly SqliteConnection _conexao = new("Data Source=:memory:");
    private readonly ServiceProvider _sp;
    private readonly UsuarioSistema _ana = new() { Nome = "Ana Paula", Login = "ana", Perfil = PerfilAcesso.Gerente, SenhaHash = "-", SenhaSalt = "-", Ativo = true };
    private readonly UsuarioSistema _recepcao = new() { Nome = "Bia", Login = "bia", Perfil = PerfilAcesso.Recepcao, SenhaHash = "-", SenhaSalt = "-", Ativo = true };
    private readonly Profissional _helena = new() { Nome = "Dra. Helena" };
    private readonly Profissional _marcos = new() { Nome = "Dr. Marcos" };
    private UsuarioSistema _medica = null!, _medico = null!;
    private readonly Paciente _maria = new() { Nome = "Maria das Graças Silva", Telefone = "(22) 99876-5432" };

    public ConversaResolveTests()
    {
        _conexao.Open();
        var servicos = new ServiceCollection();
        servicos.AddClinica("Host=teste;Database=teste");
        foreach (var d in servicos.Where(d => d.ServiceType == typeof(DbContextOptions<ClinicaDbContext>)
                     || d.ServiceType == typeof(DbContextOptions) || d.ServiceType == typeof(ClinicaDbContext)).ToList())
            servicos.Remove(d);
        servicos.AddDbContext<ClinicaDbContext>(o => o.UseSqlite(_conexao));
        _sp = servicos.BuildServiceProvider();
        using var db = Db();
        db.Database.EnsureCreated();
        db.Profissionais.AddRange(_helena, _marcos);
        db.Pacientes.Add(_maria);
        db.SaveChanges();
        _medica = new() { Nome = "Helena Marques", Login = "helena", Perfil = PerfilAcesso.Profissional, ProfissionalId = _helena.Id, SenhaHash = "-", SenhaSalt = "-", Ativo = true };
        _medico = new() { Nome = "Marcos Teles", Login = "marcos", Perfil = PerfilAcesso.Profissional, ProfissionalId = _marcos.Id, SenhaHash = "-", SenhaSalt = "-", Ativo = true };
        var saiu = new UsuarioSistema { Nome = "Antigo", Login = "antigo", Perfil = PerfilAcesso.Profissional, ProfissionalId = _marcos.Id, SenhaHash = "-", SenhaSalt = "-", Ativo = false };
        db.Usuarios.AddRange(_ana, _recepcao, _medica, _medico, saiu);
        db.SaveChanges();
        new SessaoUsuario().Entrar(_ana);
    }

    public void Dispose()
    {
        new SessaoUsuario().Entrar(new UsuarioSistema());
        _sp.Dispose();
        _conexao.Dispose();
    }

    private ClinicaDbContext Db() => new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conexao).Options);

    private static HorarioDoPaciente Horario(int id, DateTime quando, string? profissional = "Dra. Helena",
        ModalidadeAtendimento modalidade = ModalidadeAtendimento.AcupunturaSimples)
        => new(id, quando, "", "", "Marcado", "neutro", false, false, false, 1, modalidade, profissional);

    // ------------------------------------------------------------------ R5: o que a clínica fez

    [Fact]
    public void Marcar_acha_o_horario_que_nao_estava_e_diz_o_que_a_clinica_fez_sem_prometer_lembrete()
    {
        var existente = Horario(10, new DateTime(2026, 10, 27, 9, 0, 0));
        var novo = Horario(11, new DateTime(2026, 10, 27, 14, 0, 0));
        Assert.Equal(novo, RascunhosDaConversa.HorarioNovo([existente], [existente, novo]));
        Assert.Null(RascunhosDaConversa.HorarioNovo([existente], [existente]));
        // Remarcar muda a hora do MESMO número: não é horário novo.
        Assert.Null(RascunhosDaConversa.HorarioNovo([existente], [existente with { Quando = existente.Quando.AddHours(3) }]));

        var texto = RascunhosDaConversa.Marcacao("Maria das Graças Silva", novo, Hoje);
        Assert.Equal("Pronto, Maria! Marquei a sua acupuntura para terça-feira, 27/10 às 14:00 com Dra. Helena.", texto);
        Assert.DoesNotContain("lembrete", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Pronto! Marquei a sua sessão de BSV para amanhã às 09:00.",
            RascunhosDaConversa.Marcacao("5522999990000", Horario(12, new DateTime(2026, 10, 24, 9, 0, 0), null, ModalidadeAtendimento.BsvApenas), Hoje));
    }

    [Fact]
    public void Cancelar_diz_o_que_a_clinica_fez_e_convida_sem_prometer()
    {
        var h = Horario(10, new DateTime(2026, 10, 24, 14, 0, 0));
        Assert.Equal("Maria, cancelei o seu horário de amanhã às 14:00 com Dra. Helena. Se quiser marcar outro dia, é só responder por aqui.",
            RascunhosDaConversa.Cancelamento("Maria das Graças Silva", h, Hoje));
        Assert.StartsWith("Olá! Cancelei o seu horário de", RascunhosDaConversa.Cancelamento(null, h, Hoje));
        Assert.Contains("ficou pronta no campo", RascunhosDaConversa.DepoisDeCancelar(true));
        Assert.Contains("Nenhuma mensagem foi ao paciente", RascunhosDaConversa.DepoisDeCancelar(false));
        Assert.Contains("escolha um modelo", RascunhosDaConversa.DepoisDeMarcar(false, janelaFechada: true));
        Assert.Equal("Horário marcado na agenda.", RascunhosDaConversa.DepoisDeMarcar(false, false));
    }

    // ------------------------------------------------------------------ D12: passar o turno (as regras)

    [Fact]
    public void O_turno_leva_as_abertas_de_quem_sai_e_a_nota_e_obrigatoria()
    {
        AppConversationSummary L(string id, string? agente, bool encerrada = false) => new(id, id, "5522999990000", "whatsapp", "s", "Recepção",
            agente, encerrada, 1, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, "", 0, "", false, false, null, null);

        var doTurno = TurnoNoWhatsApp.DoTurno([L("minha", "ana"), L("da-bruna", "bruna"), L("sem-dono", null), L("encerrada", "ana", true)], "ana");

        Assert.Equal(["minha"], doTurno.Select(c => c.Id));
        Assert.Empty(TurnoNoWhatsApp.DoTurno([L("minha", "ana")], null));
        Assert.NotNull(TurnoNoWhatsApp.Impedimento("ok"));
        Assert.NotNull(TurnoNoWhatsApp.Impedimento(new string('a', WhatsAppDaRecepcaoService.MaximoDoMotivoDeDevolver + 1)));
        Assert.Null(TurnoNoWhatsApp.Impedimento("Fim do turno da manhã"));
        Assert.Equal("Passar o turno (3)", TurnoNoWhatsApp.Rotulo(3));
    }

    [Fact]
    public void O_relato_nao_esconde_a_conversa_que_ficou()
    {
        Assert.Equal("3 conversas voltaram à fila. Quem assumir cada uma lê a sua nota.",
            TurnoNoWhatsApp.Relato(new(["a", "b", "c"], [])));
        Assert.Equal("2 de 3 voltaram à fila. Ficaram com você: Maria (já não estava com você).",
            TurnoNoWhatsApp.Relato(new(["a", "b"], [new("m", "Maria", TurnoNoWhatsApp.JaNaoEraSua)])));
        Assert.Equal("Nenhuma conversa voltou à fila. Ficaram com você: João (Aguarde o envio em andamento).",
            TurnoNoWhatsApp.Relato(new([], [new("j", "João", "Aguarde o envio em andamento.")])));
    }

    // ------------------------------------------------------------------ R9: guardar na ficha

    private static MidiaDaMensagem Midia(TipoDeMidia tipo, string direcao = "in", bool figurinha = false, string? nome = null)
        => new(tipo, "Foto", nome, "c-maria", new AppMessage("m1", direcao, "", tipo == TipoDeMidia.Imagem ? "IMAGE" : "DOCUMENT", null,
            new DateTimeOffset(2026, 10, 23, 13, 0, 0, TimeSpan.Zero), "received", Sticker: figurinha, FileName: nome));

    [Fact]
    public void So_foto_e_documento_que_o_paciente_mandou_cabem_na_ficha()
    {
        Assert.True(GuardarNaFichaService.Cabe(Midia(TipoDeMidia.Imagem)));
        Assert.True(GuardarNaFichaService.Cabe(Midia(TipoDeMidia.Documento, nome: "pedido.pdf")));
        Assert.False(GuardarNaFichaService.Cabe(Midia(TipoDeMidia.Imagem, figurinha: true)));
        Assert.False(GuardarNaFichaService.Cabe(Midia(TipoDeMidia.Imagem, direcao: "out")));
        Assert.False(GuardarNaFichaService.Cabe(Midia(TipoDeMidia.Audio)));
        Assert.True(GuardarNaFichaService.ArquivoServe(new([1], "image/jpeg", null)));
        Assert.True(GuardarNaFichaService.ArquivoServe(new([1], "application/octet-stream", "exame.PDF")));
        Assert.False(GuardarNaFichaService.ArquivoServe(new([1], "application/vnd.ms-excel", "planilha.xls")));

        Assert.Equal("Pedido médico", GuardarNaFichaService.Titulo("Pedido médico", null));
        Assert.Null(GuardarNaFichaService.Titulo(GuardarNaFichaService.Outro, "  "));
        Assert.Equal("Laudo da ressonância", GuardarNaFichaService.Titulo(GuardarNaFichaService.Outro, " Laudo da ressonância "));
        Assert.NotNull(GuardarNaFichaService.Impedimento("Exame", null, Hoje));
        Assert.Equal("A data do documento não pode ser futura.", GuardarNaFichaService.Impedimento("Exame", Hoje.AddDays(1), Hoje));
        Assert.Null(GuardarNaFichaService.Impedimento("Exame", Hoje, Hoje));
        Assert.Equal("Guardado na ficha por Ana Paula: Exame (documento de 20/10/2026).", GuardarNaFichaService.Nota("Ana Paula", "Exame", new(2026, 10, 20)));
    }

    [Fact]
    public async Task Guardar_poe_o_arquivo_na_ficha_conferida_com_a_trilha_e_quem_entrou()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 };
        var dia = DateOnly.FromDateTime(DateTime.Today).AddDays(-3);
        using (var escopo = _sp.CreateScope())
            await escopo.ServiceProvider.GetRequiredService<GuardarNaFichaService>()
                .GuardarAsync(_maria.Id, Midia(TipoDeMidia.Imagem), new ArquivoDaMidia(jpeg, "image/jpeg", null), "Carteirinha", dia);

        using var db = Db();
        var anexo = await db.AnexosPaciente.AsNoTracking().SingleAsync();
        Assert.Equal((_maria.Id, "Carteirinha", dia, "ana", "image/jpeg"), (anexo.PacienteId, anexo.Titulo, anexo.Data, anexo.CriadoPor, anexo.TipoConteudo));
        Assert.Equal("Recebido pelo WhatsApp da clínica.", anexo.Observacoes);
        Assert.EndsWith(".jpg", anexo.NomeArquivo);
        Assert.Equal(jpeg, (await db.ArquivosAnexoPaciente.AsNoTracking().SingleAsync()).Conteudo);
        var trilha = await db.Auditoria.AsNoTracking().SingleAsync(e => e.Acao == "AnexoFichaRegistrado");
        Assert.Equal((_maria.Id, "ana"), (trilha.PacienteId, trilha.Operador));
    }

    [Fact]
    public async Task Guardar_recusa_sem_ficha_sem_permissao_figurinha_e_data_futura_e_nada_grava()
    {
        var foto = new ArquivoDaMidia([1, 2, 3], "image/png", null);
        using var escopo = _sp.CreateScope();
        var s = escopo.ServiceProvider.GetRequiredService<GuardarNaFichaService>();
        var ontem = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);

        Assert.Equal(GuardarNaFichaService.SemFicha, (await Assert.ThrowsAsync<InvalidOperationException>(
            () => s.GuardarAsync(null, Midia(TipoDeMidia.Imagem), foto, "Exame", ontem))).Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.GuardarAsync(_maria.Id, Midia(TipoDeMidia.Imagem, figurinha: true), foto, "Exame", ontem));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.GuardarAsync(_maria.Id, Midia(TipoDeMidia.Imagem), foto, "Exame", ontem.AddDays(5)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.GuardarAsync(_maria.Id, Midia(TipoDeMidia.Documento), new([1], "text/csv", "lista.csv"), "Exame", ontem));
        // A recepção do perfil padrão não anexa dado de saúde (a mesma permissão das outras portas de anexar).
        new SessaoUsuario().Entrar(_recepcao);
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.GuardarAsync(_maria.Id, Midia(TipoDeMidia.Imagem), foto, "Exame", ontem));

        using var db = Db();
        Assert.Empty(db.AnexosPaciente);
    }

    // ------------------------------------------------------------------ R10: perguntar ao profissional

    [Fact]
    public void A_sugestao_e_o_profissional_da_proxima_sessao_e_sem_ela_o_do_caso()
    {
        var quem = new List<ProfissionalDoChat> { new(5, "Helena", 1), new(6, "Marcos", 2) };
        Assert.Equal(6, PerguntaAoProfissionalService.Sugestao(quem, 2, 1));
        Assert.Equal(5, PerguntaAoProfissionalService.Sugestao(quem, null, 1));
        Assert.Equal(5, PerguntaAoProfissionalService.Sugestao(quem, 99, 1)); // o da próxima sessão não tem login
        Assert.Null(PerguntaAoProfissionalService.Sugestao(quem, null, null));
    }

    [Fact]
    public void O_texto_cita_o_paciente_e_tem_teto()
    {
        var texto = PerguntaAoProfissionalService.Texto("Lúcia Andrade", fichaConferida: false, "O braço inchou\nÉ normal?", "Pode orientar?");
        Assert.Equal("Pergunta pelo WhatsApp da clínica — Lúcia Andrade (ficha ainda não conferida):\n\n> O braço inchou\n> É normal?\n\nPode orientar?", texto);
        var longo = PerguntaAoProfissionalService.Texto("Maria", true, new string('x', 5000), null);
        Assert.True(longo.Length < PerguntaAoProfissionalService.TetoDoTrecho + 100);
        Assert.EndsWith("…", longo);
    }

    [Fact]
    public async Task A_pergunta_chega_a_direta_do_profissional_com_o_paciente_anexado()
    {
        using var escopo = _sp.CreateScope();
        var s = escopo.ServiceProvider.GetRequiredService<PerguntaAoProfissionalService>();

        var quem = await s.QuemPodeResponderAsync();
        // Só login ATIVO ligado a profissional, pelo nome; quem saiu da equipe e o balcão ficam de fora.
        Assert.Equal(["Helena Marques", "Marcos Teles"], quem.Select(q => q.Nome));

        var texto = PerguntaAoProfissionalService.Texto(_maria.Nome, true, "Posso tomar banho de mar depois da sessão?", null);
        var conversa = await s.PerguntarAsync(quem[0], _maria.Id, texto);

        Assert.Equal(ChatEquipeRegras.Direta(_ana.Id, _medica.Id), conversa);
        using var db = Db();
        var msg = await db.MensagensEquipe.AsNoTracking().SingleAsync();
        Assert.Equal((conversa, _ana.Id, _maria.Id), (msg.Conversa, msg.AutorId, msg.PacienteId));
        Assert.Contains("> Posso tomar banho de mar", msg.Texto);
        Assert.Equal("Pergunta enviada a Helena Marques pelo chat da equipe. A resposta chega lá.", PerguntaAoProfissionalService.Enviada("Helena Marques"));
    }
}
