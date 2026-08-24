using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A FICHA DO ATENDIMENTO (parcela 78) — o papel que o médico e a enfermeira entregam ao
/// paciente na hora.
///
/// O motor já existia: o relatório de evolução é um <see cref="DocumentoClinico"/> com a
/// logo, numeração por ano e código de conferência. O que faltava era a PORTA no
/// Consultório, o RECORTE (ele saía com o histórico inteiro) e o papel da ENFERMAGEM,
/// que só tinha impressão quando havia folha de infusão.
///
/// Estes testes fixam o que mudou no documento, porque as três coisas se provam aqui — a
/// tela é WPF e não compila no projeto de teste.
/// </summary>
public class FichaDoAtendimentoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ProntuarioService _prontuario;
    private readonly DocumentoClinicoService _documentos;
    private readonly EvolucaoEnfermagemService _enfermagem;

    private static readonly DateOnly Dia = new(2026, 8, 12);
    private static DateTime MeioDia => Dia.ToDateTime(new TimeOnly(12, 0));

    private static readonly IdentificacaoExecutante Tecnica =
        new(UsuarioId: null, Nome: "Joana Técnica", Conselho: "COREN-SP 999999");

    public FichaDoAtendimentoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _prontuario = new ProntuarioService(_repo);
        _documentos = new DocumentoClinicoService(_repo, _prontuario, new ConsentimentoService(_repo));
        _enfermagem = new EvolucaoEnfermagemService(_repo, () => MeioDia);
    }

    private async Task<int> PacienteAsync()
    {
        var p = new Paciente { Nome = "Maria de Teste", Convenio = Convenio.UnimedIntercambio };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private Task<Evolucao> SessaoAsync(int pacienteId, DateOnly data, int? antes = 8, int? depois = 4)
        => _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            Data = data,
            EvaAntes = antes,
            EvaDepois = depois,
            QueixaPrincipal = "dor lombar",
            Conduta = "acupuntura sistêmica",
            TextoEvolucao = "paciente refere melhora"
        }, "dra. ana");

    // ==================== A enfermagem entra no papel ====================

    [Fact]
    public async Task A_passagem_de_enfermagem_sai_no_relatorio_com_o_COREN_de_quem_a_fez()
    {
        var pacienteId = await PacienteAsync();

        await _enfermagem.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 20),
            "Curativo em MSD, sem sinais flogísticos.", Tecnica,
            sinais: new SinaisVitais(Temperatura: 36.5m),
            acesso: new AcessoVenoso("MSD", "20G", Dia));

        var ficha = await _documentos.EmitirRelatorioEvolucaoAsync(
            pacienteId, inicio: Dia, fim: Dia, operador: "joana");

        var item = ficha.Itens.Should().ContainSingle().Subject;
        item.Descricao.Should().Contain("enfermagem").And.Contain("09:20");
        item.Detalhe.Should().Contain("Curativo em MSD");
        item.Detalhe.Should().Contain("acesso venoso");
        item.Quantidade.Should().Be("Joana Técnica (COREN-SP 999999)",
            "quem assina a passagem é quem a fez, e é o COREN que dá valor ao registro "
            + "num papel que circula fora da clínica");
    }

    [Fact]
    public async Task Um_dia_so_de_enfermagem_produz_ficha_em_vez_de_estourar()
    {
        var pacienteId = await PacienteAsync();

        // Nenhuma evolução MÉDICA: até a parcela 78 o período saía de `evolucoes[0]`, e
        // esta emissão estourava com IndexOutOfRange na frente de quem clicou em imprimir.
        await _enfermagem.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(11, 0), "Aferição de PA.", Tecnica);

        var ficha = await _documentos.EmitirRelatorioEvolucaoAsync(pacienteId);

        ficha.PeriodoInicio.Should().Be(Dia);
        ficha.PeriodoFim.Should().Be(Dia);
        ficha.Corpo.Should().Contain("1 registro(s) de enfermagem");
        ficha.Corpo.Should().NotContain("0 sessão(ões)",
            "'0 sessão(ões) registrada(s)' é verdade e se lê como FALTA, na primeira linha "
            + "do papel que o paciente leva embora");
        ficha.Corpo.Should().NotContain("EVA",
            "numa ficha só de enfermagem, 'nenhuma sessão tem a EVA medida' seria uma "
            + "afirmação sobre um registro que não se propôs a medi-la");
    }

    [Fact]
    public async Task Registro_de_enfermagem_cancelado_nao_sai_no_papel()
    {
        var pacienteId = await PacienteAsync();

        var boa = await _enfermagem.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 0), "Passagem correta.", Tecnica);
        var errada = await _enfermagem.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(10, 0), "Lançada no paciente errado.", Tecnica);

        await _enfermagem.CancelarAsync(errada.Id, "paciente errado", "joana");

        var ficha = await _documentos.EmitirRelatorioEvolucaoAsync(pacienteId, inicio: Dia, fim: Dia);

        ficha.Itens.Should().ContainSingle()
            .Which.Detalhe.Should().Contain("Passagem correta");
        ficha.Corpo.Should().Contain("1 registro(s) de enfermagem",
            "registro cancelado continua no prontuário e NÃO no papel que sai da clínica");
    }

    [Fact]
    public async Task A_consulta_de_enfermagem_leva_o_DIAGNOSTICO_e_os_CUIDADOS_ao_papel()
    {
        var pacienteId = await PacienteAsync();

        await _enfermagem.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 0),
            "Consulta de enfermagem.", Tecnica,
            processo: new ProcessoDeEnfermagem(
                Historico: "Refere dor lombar há duas semanas.",
                ExameFisico: "Acesso em MSD pérvio.",
                Avaliacao: "Compreendeu as orientações.",
                Diagnosticos:
                [
                    new DiagnosticoEnfermagem
                    {
                        Titulo = "Dor aguda",
                        RelacionadoA = "espasmo muscular",
                        EvidenciadoPor = "relato de 7/10"
                    }
                ],
                Cuidados:
                [
                    new CuidadoEnfermagem
                    {
                        Descricao = "Avaliar a dor pela escala numérica",
                        Frequencia = "a cada 2h"
                    }
                ]));

        var ficha = await _documentos.EmitirRelatorioEvolucaoAsync(pacienteId, inicio: Dia, fim: Dia);

        var detalhe = ficha.Itens.Should().ContainSingle().Subject.Detalhe!;

        // As CINCO etapas da COFEN 358/2009. Sem as etapas 2, 3 e 4 o papel diz "consulta
        // de enfermagem" e mostra texto livre — que é o que a clínica já tinha antes de a
        // consulta existir.
        detalhe.Should().Contain("Refere dor lombar");
        detalhe.Should().Contain("Acesso em MSD");
        detalhe.Should().Contain("diagnóstico(s) de enfermagem: Dor aguda, relacionado a espasmo muscular");
        detalhe.Should().Contain("cuidados prescritos: Avaliar a dor pela escala numérica — a cada 2h");
        detalhe.Should().Contain("Compreendeu as orientações");
    }

    [Fact]
    public async Task Passagem_sem_processo_nao_imprime_ROTULO_de_etapa_vazia()
    {
        var pacienteId = await PacienteAsync();

        // Troca de curativo: não há consulta de enfermagem, e rótulo sem conteúdo faria o
        // papel parecer um formulário preenchido pela metade.
        await _enfermagem.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 0), "Curativo trocado.", Tecnica);

        var ficha = await _documentos.EmitirRelatorioEvolucaoAsync(pacienteId, inicio: Dia, fim: Dia);

        var detalhe = ficha.Itens.Should().ContainSingle().Subject.Detalhe!;
        detalhe.Should().Be("Curativo trocado.");
        detalhe.Should().NotContain("diagnóstico").And.NotContain("cuidados prescritos");
    }

    // ==================== A linha do tempo ====================

    [Fact]
    public async Task Os_itens_saem_em_ORDEM_DE_DATA_e_nao_em_blocos_por_origem()
    {
        var pacienteId = await PacienteAsync();

        // Uma sessão em junho, uma passagem de enfermagem em julho, outra sessão em
        // agosto. Até a parcela 78 a enfermagem era ANEXADA depois de todas as sessões, e
        // o papel saía junho → agosto → julho: quem lê compara o julho com o agosto acima.
        await SessaoAsync(pacienteId, new DateOnly(2026, 6, 10));
        await _enfermagem.RegistrarAsync(
            pacienteId, new DateOnly(2026, 7, 15), new TimeOnly(9, 0), "Curativo.", Tecnica);
        await SessaoAsync(pacienteId, new DateOnly(2026, 8, 20));

        var ficha = await _documentos.EmitirRelatorioEvolucaoAsync(pacienteId);

        ficha.Itens.Select(i => i.Descricao[..10])
            .Should().ContainInOrder("10/06/2026", "15/07/2026", "20/08/2026");
        ficha.PeriodoInicio.Should().Be(new DateOnly(2026, 6, 10));
        ficha.PeriodoFim.Should().Be(new DateOnly(2026, 8, 20));
    }

    [Fact]
    public async Task A_ficha_do_dia_leva_SO_o_dia_pedido()
    {
        var pacienteId = await PacienteAsync();

        await SessaoAsync(pacienteId, Dia.AddDays(-30));
        await _enfermagem.RegistrarAsync(
            pacienteId, Dia.AddDays(-30), new TimeOnly(9, 0), "Passagem antiga.", Tecnica);
        await SessaoAsync(pacienteId, Dia);
        await _enfermagem.RegistrarAsync(
            pacienteId, Dia, new TimeOnly(9, 0), "Passagem de hoje.", Tecnica);

        // É o que o botão do Consultório e o da enfermagem pedem: a ficha DESTE
        // atendimento, para entregar ao paciente na hora. As duas portas que já existiam
        // (ficha do paciente e central de documentos) omitem o recorte e emitem o
        // histórico inteiro, que é o relatório para o convênio — outro papel.
        var ficha = await _documentos.EmitirRelatorioEvolucaoAsync(pacienteId, inicio: Dia, fim: Dia);

        ficha.Itens.Should().HaveCount(2);
        ficha.Itens.Should().OnlyContain(i => i.Descricao.StartsWith("12/08/2026"));
        ficha.PeriodoInicio.Should().Be(Dia);
        ficha.PeriodoFim.Should().Be(Dia);
    }

    [Fact]
    public async Task Dia_sem_registro_nenhum_e_recusado_e_a_recusa_fala_dos_DOIS()
    {
        var pacienteId = await PacienteAsync();
        await SessaoAsync(pacienteId, Dia.AddDays(-30));

        var emitir = () => _documentos.EmitirRelatorioEvolucaoAsync(
            pacienteId, inicio: Dia, fim: Dia);

        // A frase deixou de dizer "sessão" porque a enfermagem passou a contar: a técnica
        // que registrou a passagem e não vê a ficha sair precisa saber que o que falta é
        // registro, não sessão médica.
        await emitir.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Não há registro no prontuário*");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
