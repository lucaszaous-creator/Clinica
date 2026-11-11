using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A guia de consulta no padrão ANS conferida na BAIXA (nov/2026, pedido da direção): toda baixa
/// de consulta da Saúde Petrobras passa pelos campos que a operadora pede. Aqui: os domínios
/// contra o XSD oficial, a regra de quem exige, a validação, a proposta pré-preenchida, a recusa
/// nas portas sem formulário (sem trancar a rodada bloqueante) e o XML válido no XSD.
/// </summary>
/// <remarks>
/// A baixa em lote lê o <see cref="SessaoUsuario.Atual"/> GLOBAL: a classe entra na coleção de
/// quem troca a sessão e parte da sessão anônima, como <see cref="DecisoesDaRodadaTests"/>.
/// </remarks>
[Collection(ColecaoDiagnostico.Nome)]
public class GuiaAnsNaBaixaTests : IDisposable
{
    private static readonly XNamespace Ans = "http://www.ans.gov.br/padroes/tiss/schemas";
    private static readonly string PastaXsd = Path.Combine(AppContext.BaseDirectory, "Tiss", "xsd-4.01.00");

    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly FaturamentoService _faturamento;

    public GuiaAnsNaBaixaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _faturamento = new FaturamentoService(_repo);
        new SessaoUsuario().Entrar(new UsuarioSistema());
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private static readonly DateOnly Dia = new(2026, 11, 10);

    private static DadosGuiaConsultaAns GuiaCompleta() => new()
    {
        RegistroAns = "422631",
        NumeroCarteira = "000123456789",
        ValidadeCarteira = new DateOnly(2027, 1, 31),
        NomeBeneficiario = "Paciente de Teste",
        CodigoPrestadorNaOperadora = "12345678000190",
        NomeContratado = "Clínica de Teste Ltda",
        Cnes = "9999999",
        NomeProfissional = "Dra. Ana de Teste",
        ConselhoProfissional = "06",
        NumeroConselho = "123456",
        UfConselho = "33",
        Cbos = "225133",
        IndicacaoAcidente = "9",
        RegimeAtendimento = "01",
        DataAtendimento = Dia,
        CodigoTabela = "22",
        TipoConsulta = "1",
        CodigoProcedimento = "10101012",
        ValorProcedimento = 139.00m
    };

    private async Task<(Paciente Paciente, CodigoFaturamento Codigo)> LancarAsync(
        Convenio convenio, ModalidadeAtendimento modalidade = ModalidadeAtendimento.Consulta, string? carteirinha = "000123456789")
    {
        var p = new Paciente { Nome = "Paciente de Teste", Convenio = convenio, Sexo = Sexo.Feminino, Carteirinha = carteirinha };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        var r = await new AtendimentoService(_repo).LancarAsync(p.Id, Dia, modalidade);
        return (p, r.Atendimento.Codigos.OrderBy(c => c.Ordem).First());
    }

    // ==================== domínios × XSD ====================

    public static IEnumerable<object[]> DominiosIguaisAoXsd() =>
    [
        ["dm_indicadorAcidente", DominiosTiss.IndicacaoAcidente],
        ["dm_regimeAtendimento", DominiosTiss.RegimeAtendimento],
        ["dm_tipoConsulta", DominiosTiss.TipoConsulta],
        ["dm_conselhoProfissional", DominiosTiss.ConselhoProfissional],
        ["dm_UF", DominiosTiss.Uf],
        ["dm_cobEsp", DominiosTiss.CoberturaEspecial],
        ["dm_saudeOcupacional", DominiosTiss.SaudeOcupacional],
        ["dm_CBOS", DominiosTiss.Cbos]
    ];

    [Theory]
    [MemberData(nameof(DominiosIguaisAoXsd))]
    public void Dominio_tem_exatamente_os_codigos_da_enumeracao_do_XSD(string tipo, IReadOnlyList<ValorTiss> dominio)
    {
        var doXsd = EnumeracaoDoXsd(tipo);

        doXsd.Should().NotBeEmpty();
        dominio.Select(v => v.Codigo).Should().BeEquivalentTo(doXsd);
        dominio.Select(v => v.Codigo).Should().OnlyHaveUniqueItems();
        dominio.Should().OnlyContain(v => !string.IsNullOrWhiteSpace(v.Termo));
    }

    [Fact]
    public void Tabela_so_oferece_codigos_que_o_XSD_aceita_e_inclui_a_22()
    {
        var doXsd = EnumeracaoDoXsd("dm_tabela");

        DominiosTiss.Tabela.Select(v => v.Codigo).Should().BeSubsetOf(doXsd).And.Contain("22");
    }

    [Fact]
    public void As_armadilhas_do_padrao_estao_nos_codigos()
    {
        DominiosTiss.UfPelaSigla("rj").Should().Be("33");           // código IBGE, não a sigla
        DominiosTiss.ConselhoPelaSigla("CRM").Should().Be("06");    // com zero à esquerda
        DominiosTiss.Contem(DominiosTiss.ConselhoProfissional, "6").Should().BeFalse();
        DominiosTiss.Contem(DominiosTiss.Uf, "RJ").Should().BeFalse();
        DominiosTiss.Termo(DominiosTiss.Cbos, "225133").Should().Be("Médico psiquiatra");
        DominiosTiss.Termo(DominiosTiss.Cbos, "225105").Should().Be("Médico acupunturista");
    }

    private static HashSet<string> EnumeracaoDoXsd(string tipo)
    {
        XNamespace xs = "http://www.w3.org/2001/XMLSchema";
        var doc = XDocument.Load(Path.Combine(PastaXsd, "tissSimpleTypesV4_01_00.xsd"));
        return doc.Descendants(xs + "simpleType").Single(t => (string?)t.Attribute("name") == tipo)
            .Descendants(xs + "enumeration").Select(e => (string)e.Attribute("value")!).ToHashSet();
    }

    // ==================== quem exige ====================

    [Theory]
    [InlineData(Convenio.Petrobras, TipoCodigo.Consulta, true)]
    [InlineData(Convenio.Petrobras, TipoCodigo.ConsultaEspecialidade, true)]
    [InlineData(Convenio.Petrobras, TipoCodigo.Bsv, false)]            // procedimento: guia SP/SADT, outro formulário
    [InlineData(Convenio.Amil, TipoCodigo.Consulta, false)]
    [InlineData(Convenio.UnimedPadrao, TipoCodigo.Consulta, false)]
    [InlineData(Convenio.UnimedIntercambio, TipoCodigo.Acupuntura, false)]
    public void Exige_so_na_consulta_da_familia_Petrobras(Convenio convenio, TipoCodigo tipo, bool esperado)
        => GuiaAnsNaBaixa.Exige(tipo, null, convenio).Should().Be(esperado);

    [Fact]
    public void Sessao_particular_de_paciente_Petrobras_nao_exige()
    {
        var paciente = new Paciente { Nome = "P", Convenio = Convenio.Petrobras };
        var doPlano = new CodigoFaturamento { Tipo = TipoCodigo.Consulta, Atendimento = new Atendimento { Paciente = paciente } };
        var particular = new CodigoFaturamento { Tipo = TipoCodigo.Consulta, Atendimento = new Atendimento { Paciente = paciente, Particular = true } };
        var semPaciente = new CodigoFaturamento { Tipo = TipoCodigo.Consulta };

        GuiaAnsNaBaixa.Exige(doPlano).Should().BeTrue();
        GuiaAnsNaBaixa.Exige(particular).Should().BeFalse();
        GuiaAnsNaBaixa.Exige(semPaciente).Should().BeFalse();
    }

    // ==================== validação ====================

    [Fact]
    public void Guia_completa_nao_tem_erro()
        => GuiaAnsNaBaixa.Validar(GuiaCompleta()).Should().BeEmpty();

    [Fact]
    public void Guia_em_branco_acusa_cada_campo_obrigatorio_do_padrao_e_nenhum_opcional()
    {
        var erros = GuiaAnsNaBaixa.Validar(new DadosGuiaConsultaAns());

        erros.Keys.Should().BeEquivalentTo(
            nameof(DadosGuiaConsultaAns.RegistroAns), nameof(DadosGuiaConsultaAns.NumeroCarteira),
            nameof(DadosGuiaConsultaAns.CodigoPrestadorNaOperadora), nameof(DadosGuiaConsultaAns.Cnes),
            nameof(DadosGuiaConsultaAns.NomeProfissional), nameof(DadosGuiaConsultaAns.ConselhoProfissional),
            nameof(DadosGuiaConsultaAns.NumeroConselho), nameof(DadosGuiaConsultaAns.UfConselho),
            nameof(DadosGuiaConsultaAns.Cbos), nameof(DadosGuiaConsultaAns.IndicacaoAcidente),
            nameof(DadosGuiaConsultaAns.RegimeAtendimento), nameof(DadosGuiaConsultaAns.TipoConsulta),
            nameof(DadosGuiaConsultaAns.CodigoTabela), nameof(DadosGuiaConsultaAns.DataAtendimento),
            nameof(DadosGuiaConsultaAns.CodigoProcedimento), nameof(DadosGuiaConsultaAns.ValorProcedimento));
    }

    [Fact]
    public void Valor_fora_do_dominio_tamanho_estourado_e_valor_invalido_sao_acusados()
    {
        var erros = GuiaAnsNaBaixa.Validar(GuiaCompleta() with
        {
            UfConselho = "RJ", ConselhoProfissional = "6", Cbos = "123456", NumeroCarteira = new string('1', 21),
            RegistroAns = "42263", ValorProcedimento = 0m, CoberturaEspecial = "09", Observacao = new string('x', 501),
            DataAtendimento = DateOnly.FromDateTime(DateTime.Today).AddDays(1)
        });

        erros.Keys.Should().BeEquivalentTo(
            nameof(DadosGuiaConsultaAns.UfConselho), nameof(DadosGuiaConsultaAns.ConselhoProfissional),
            nameof(DadosGuiaConsultaAns.Cbos), nameof(DadosGuiaConsultaAns.NumeroCarteira),
            nameof(DadosGuiaConsultaAns.RegistroAns), nameof(DadosGuiaConsultaAns.ValorProcedimento),
            nameof(DadosGuiaConsultaAns.CoberturaEspecial), nameof(DadosGuiaConsultaAns.Observacao),
            nameof(DadosGuiaConsultaAns.DataAtendimento));
        GuiaAnsNaBaixa.Validar(GuiaCompleta() with { ValorProcedimento = 1_000_000m }).Should().ContainKey(nameof(DadosGuiaConsultaAns.ValorProcedimento));
        GuiaAnsNaBaixa.Validar(GuiaCompleta() with { ValorProcedimento = 139.005m }).Should().ContainKey(nameof(DadosGuiaConsultaAns.ValorProcedimento));
    }

    [Theory]
    [InlineData("139,00", 139.00)]
    [InlineData("139.00", 139.00)]     // como o portal mostra: NÃO pode virar 13.900
    [InlineData("139.5", 139.5)]
    [InlineData("1.250,00", 1250.00)]
    [InlineData("R$ 139", 139)]
    [InlineData(" 80 ", 80)]
    public void Valor_digitado_e_lido_como_a_clinica_escreve(string texto, double esperado)
        => GuiaAnsEmEdicao.LerValor(texto).Should().Be((decimal)esperado);

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-10")]
    [InlineData("12,3,4")]
    public void Valor_ilegivel_nao_vira_numero(string texto)
        => GuiaAnsEmEdicao.LerValor(texto).Should().BeNull();

    [Fact]
    public void Edicao_devolve_os_dados_e_acusa_o_valor_que_nao_entendeu()
    {
        var edicao = GuiaAnsEmEdicao.De(GuiaCompleta());

        edicao.ValorTexto.Should().Be("139,00");
        edicao.Erros().Should().BeEmpty();
        edicao.ParaDados().Should().BeEquivalentTo(GuiaCompleta());

        edicao.ValorTexto = "cento e trinta";
        edicao.Erros().Should().ContainKey(nameof(DadosGuiaConsultaAns.ValorProcedimento))
            .WhoseValue.Should().Contain("139,00");
        edicao.ValorTexto = "";
        edicao.Erros()[nameof(DadosGuiaConsultaAns.ValorProcedimento)].Should().Be("Informe o valor do procedimento.");
    }

    [Theory]
    [InlineData("CRM-RJ 123456", "06", "123456", "33")]
    [InlineData("CRM/RJ 52.12345-0", "06", "52123450", "33")]
    [InlineData("crm sp 123456", "06", "123456", "35")]
    [InlineData("CREFITO-2 12345-F", "05", "2", null)]
    [InlineData("123456", null, "123456", null)]
    [InlineData("Dra. sem registro", null, null, null)]
    [InlineData(null, null, null, null)]
    public void Registro_em_texto_livre_e_lido_no_que_da_para_reconhecer(string? texto, string? conselho, string? numero, string? uf)
    {
        var lido = GuiaAnsNaBaixa.LerRegistroConselho(texto);

        lido.Conselho.Should().Be(conselho);
        lido.Uf.Should().Be(uf);
        // O número é PROPOSTA: nos casos ambíguos (CREFITO-2) ele pode vir errado, e por isso o
        // formulário o mostra para conferência em vez de gravá-lo sozinho.
        lido.Numero.Should().Be(numero);
    }

    // ==================== a baixa pelo serviço ====================

    [Fact]
    public async Task Consulta_Petrobras_sem_a_guia_ANS_e_recusada_e_nada_e_gravado()
    {
        var (_, codigo) = await LancarAsync(Convenio.Petrobras);

        var acao = () => _faturamento.DarBaixaAsync(codigo.Id, Dia.AddDays(1), "PB123", "secretaria", null);

        (await acao.Should().ThrowAsync<InvalidOperationException>()).WithMessage(GuiaAnsNaBaixa.RecusaSemGuia);
        _db.ChangeTracker.Clear();
        (await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == codigo.Id)).Status.Should().Be(StatusCodigo.Aberto);
        (await _db.GuiasTissConsulta.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Consulta_Petrobras_com_guia_incompleta_e_recusada_dizendo_o_que_falta()
    {
        var (_, codigo) = await LancarAsync(Convenio.Petrobras);

        var acao = () => _faturamento.DarBaixaAsync(codigo.Id, Dia.AddDays(1), "PB123", "secretaria", null,
            guiaAns: GuiaCompleta() with { Cbos = "" });

        (await acao.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*CBO*");
        _db.ChangeTracker.Clear();
        (await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == codigo.Id)).Baixado.Should().BeFalse();
    }

    [Fact]
    public async Task Consulta_Petrobras_com_a_guia_conferida_baixa_e_grava_a_guia_com_quem_conferiu()
    {
        var (paciente, codigo) = await LancarAsync(Convenio.Petrobras);

        await _faturamento.DarBaixaAsync(codigo.Id, Dia.AddDays(1), "PB123", "secretaria", "ok", guiaAns: GuiaCompleta());

        _db.ChangeTracker.Clear();
        var salvo = await _repo.ObterCodigoAsync(codigo.Id);
        salvo!.Status.Should().Be(StatusCodigo.Baixado);
        salvo.NumeroGuiaReal.Should().Be("PB123");
        var guia = salvo.GuiaAns;
        Assert.NotNull(guia);
        guia.Should().BeEquivalentTo(GuiaCompleta(), o => o.ExcludingMissingMembers());
        guia.ValorProcedimento.Should().Be(139.00m);
        guia.ConferidaPor.Should().Be("secretaria");
        guia.ConferidaEm.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1));
        var auditoria = await _db.Auditoria.AsNoTracking().SingleAsync(a => a.Acao == "BaixaGuia" && a.CodigoId == codigo.Id);
        auditoria.Detalhe.Should().Contain("guia ANS de consulta conferida");
        auditoria.PacienteId.Should().Be(paciente.Id);
    }

    [Fact]
    public async Task Baixa_refeita_depois_do_estorno_substitui_a_guia_e_nao_cria_outra()
    {
        var (_, codigo) = await LancarAsync(Convenio.Petrobras);
        await _faturamento.DarBaixaAsync(codigo.Id, Dia.AddDays(1), "PB123", "secretaria", null, guiaAns: GuiaCompleta());
        _db.ChangeTracker.Clear();
        await _faturamento.EstornarBaixaAsync(codigo.Id, "número errado", "gerente");
        _db.ChangeTracker.Clear();

        // O estorno PRESERVA a guia conferida: ela é o ponto de partida da baixa seguinte.
        (await _db.GuiasTissConsulta.CountAsync()).Should().Be(1);

        await _faturamento.DarBaixaAsync(codigo.Id, Dia.AddDays(2), "PB999", "outra", null,
            guiaAns: GuiaCompleta() with { TipoConsulta = "2", ValorProcedimento = 150m });

        _db.ChangeTracker.Clear();
        var guia = await _db.GuiasTissConsulta.AsNoTracking().SingleAsync();
        guia.TipoConsulta.Should().Be("2");
        guia.ValorProcedimento.Should().Be(150m);
        guia.ConferidaPor.Should().Be("outra");
    }

    [Theory]
    [InlineData(Convenio.Amil, ModalidadeAtendimento.Consulta)]
    [InlineData(Convenio.UnimedIntercambio, ModalidadeAtendimento.AcupunturaComEletro)]
    public async Task Os_outros_convenios_continuam_baixando_so_com_o_numero(Convenio convenio, ModalidadeAtendimento modalidade)
    {
        var (_, codigo) = await LancarAsync(convenio, modalidade);

        await _faturamento.DarBaixaAsync(codigo.Id, Dia.AddDays(1), "7654321", "secretaria", null);

        _db.ChangeTracker.Clear();
        (await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == codigo.Id)).Status.Should().Be(StatusCodigo.Baixado);
        (await _db.GuiasTissConsulta.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Guia_ANS_enviada_para_guia_que_nao_a_exige_e_ignorada()
    {
        var (_, codigo) = await LancarAsync(Convenio.Amil);

        await _faturamento.DarBaixaAsync(codigo.Id, Dia.AddDays(1), "7654321", "secretaria", null, guiaAns: GuiaCompleta());

        (await _db.GuiasTissConsulta.CountAsync()).Should().Be(0);
    }

    // ==================== as portas sem formulário ====================

    private static PendenciaCodigo Pendencia(int id, Convenio convenio, TipoCodigo tipo)
        => new(id, 1, $"Paciente {id}", convenio, tipo, OrdemCodigo.Primeiro, Dia, FormaObtencao.NaoAplica, 30, NivelUrgencia.Vermelho, null);

    [Fact]
    public void Linha_de_consulta_Petrobras_diz_onde_resolver_e_as_outras_ficam_iguais()
    {
        var linhas = DecisoesDaRodada.Montar([
            Pendencia(1, Convenio.Petrobras, TipoCodigo.Consulta),
            Pendencia(2, Convenio.Petrobras, TipoCodigo.Bsv),
            Pendencia(3, Convenio.Amil, TipoCodigo.Consulta)
        ]);

        linhas[0].PedeGuiaAns.Should().BeTrue();
        linhas[0].Situacao.Should().Contain("Dar baixa").And.Contain("guia ANS").And.Contain("justifique");
        linhas[1].PedeGuiaAns.Should().BeFalse();
        linhas[2].PedeGuiaAns.Should().BeFalse();
        linhas[2].Situacao.Should().NotContain("guia ANS");
    }

    [Fact]
    public void Rodada_bloqueante_cobra_a_JUSTIFICATIVA_da_consulta_Petrobras_e_diz_onde_dar_a_baixa()
    {
        var linhas = DecisoesDaRodada.Montar([
            Pendencia(1, Convenio.Petrobras, TipoCodigo.Consulta),
            Pendencia(3, Convenio.Amil, TipoCodigo.Consulta)
        ]);
        linhas[1].NumeroGuia = "7654321";

        // A da Petrobras em branco trava, e a frase diz o que escrever e onde a baixa é dada.
        DecisoesDaRodada.ImpedimentoParaConcluir(linhas, bloqueante: true)
            .Should().Contain("decida todas as guias").And.Contain("Saúde Petrobras").And.Contain("justificativa").And.Contain("Dar baixa");
        // Justificada, a rodada conclui — sempre há saída, sem número e sem não conformidade falsa.
        linhas[0].Justificativa = "aguardando o portal da operadora";
        DecisoesDaRodada.ImpedimentoParaConcluir(linhas, bloqueante: true).Should().BeNull();
        // Fora do modo bloqueante, linha em branco continua sendo "ainda não decidi".
        linhas[0].Justificativa = null;
        DecisoesDaRodada.ImpedimentoParaConcluir(linhas, bloqueante: false).Should().BeNull();
    }

    [Fact]
    public void A_justificativa_ja_anotada_volta_preenchida_so_na_consulta_Petrobras()
    {
        var linhas = DecisoesDaRodada.Montar([
            Pendencia(1, Convenio.Petrobras, TipoCodigo.Consulta) with { ObservacaoPendencia = "aguardando o portal" },
            Pendencia(3, Convenio.Amil, TipoCodigo.Consulta) with { ObservacaoPendencia = "paciente vai mandar o QR" }
        ]);

        linhas[0].Justificativa.Should().Be("aguardando o portal");
        linhas[0].Situacao.Should().Contain("justifique");
        // Nas outras, justificativa na rodada É não conformidade: anotação antiga não pode virar uma.
        linhas[1].Justificativa.Should().BeNull();
        DecisoesDaRodada.ImpedimentoParaConcluir(linhas, bloqueante: true).Should().Contain("decida todas as guias");
    }

    [Fact]
    public async Task Justificativa_da_consulta_Petrobras_vira_ANOTACAO_e_a_guia_continua_pendente_no_painel()
    {
        var (_, petrobras) = await LancarAsync(Convenio.Petrobras);
        var (_, amil) = await LancarAsync(Convenio.Amil);
        var rodada = new RodadaPendenciasService(_repo, new PendenciaService(_repo), new ParametrosService(_repo));
        var linhas = new List<LinhaDaRodada>
        {
            new() { CodigoId = petrobras.Id, Descricao = "petrobras", Situacao = "", PedeGuiaAns = true, Justificativa = " aguardando o portal " },
            new() { CodigoId = amil.Id, Descricao = "amil", Situacao = "", Justificativa = "paciente não autorizou" }
        };

        var falhas = await DecisoesDaRodada.AplicarAsync(linhas, Dia.AddDays(1), _faturamento, rodada);

        falhas.Should().BeEmpty();
        _db.ChangeTracker.Clear();
        var daPetrobras = await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == petrobras.Id);
        daPetrobras.Status.Should().Be(StatusCodigo.Aberto);                  // continua no painel
        daPetrobras.ObservacaoPendencia.Should().Be("aguardando o portal");
        daPetrobras.NaoConformidadeJustificativa.Should().BeNull();
        (await _db.Auditoria.AsNoTracking().AnyAsync(a => a.Acao == "ObservacaoPendencia" && a.CodigoId == petrobras.Id)).Should().BeTrue();
        // A dos outros convênios não mudou: justificativa na rodada é não conformidade.
        (await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == amil.Id)).Status.Should().Be(StatusCodigo.NaoConformidade);

        // Confirmar a MESMA justificativa na rodada seguinte não regrava a anotação nem a data dela.
        var anotadoEm = daPetrobras.ObservacaoPendenciaEm;
        var deNovo = new List<LinhaDaRodada>
        {
            new() { CodigoId = petrobras.Id, Descricao = "petrobras", Situacao = "", PedeGuiaAns = true,
                    Justificativa = "aguardando o portal", JustificativaJaAnotada = "aguardando o portal" }
        };
        (await DecisoesDaRodada.AplicarAsync(deNovo, Dia.AddDays(2), _faturamento, rodada)).Should().BeEmpty();
        _db.ChangeTracker.Clear();
        (await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == petrobras.Id)).ObservacaoPendenciaEm.Should().Be(anotadoEm);
        (await _db.Auditoria.AsNoTracking().CountAsync(a => a.Acao == "ObservacaoPendencia" && a.CodigoId == petrobras.Id)).Should().Be(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Numero_digitado_numa_consulta_Petrobras_e_recusado_antes_de_gravar_na_rodada_e_no_lote(bool bloqueante)
    {
        var linhas = DecisoesDaRodada.Montar([
            Pendencia(1, Convenio.Petrobras, TipoCodigo.Consulta),
            Pendencia(3, Convenio.Amil, TipoCodigo.Consulta)
        ]);
        linhas[0].NumeroGuia = "PB123";
        linhas[1].NumeroGuia = "7654321";

        var rodada = DecisoesDaRodada.ImpedimentoParaConcluir(linhas, bloqueante);
        var lote = PainelPendenciasFaturamento.ImpedimentoDoLote(linhas);

        rodada.Should().Contain("guia ANS").And.Contain("Paciente 1").And.Contain("nada foi gravado").And.NotContain("Paciente 3");
        lote.Should().Contain("guia ANS").And.Contain("Paciente 1").And.Contain("nenhuma baixa foi feita");
    }

    [Fact]
    public async Task Se_a_conferencia_for_pulada_o_servico_recusa_a_linha_e_as_demais_seguem()
    {
        var (_, petrobras) = await LancarAsync(Convenio.Petrobras);
        var (_, amil) = await LancarAsync(Convenio.Amil);
        var linhas = new List<LinhaDaRodada>
        {
            new() { CodigoId = petrobras.Id, Descricao = "petrobras", Situacao = "", NumeroGuia = "PB123" },
            new() { CodigoId = amil.Id, Descricao = "amil", Situacao = "", NumeroGuia = "7654321" }
        };

        var (feitas, falhas) = await PainelPendenciasFaturamento.BaixarLoteAsync(linhas, Dia.AddDays(1), _faturamento);

        feitas.Should().Be(1);
        falhas.Should().ContainSingle().Which.Should().Contain("petrobras").And.Contain("Dar baixa");
    }

    // ==================== a proposta que o formulário recebe ====================

    private GuiaAnsNaBaixaService Proposta() => new(_repo, new ParametrosService(_repo), new PrecoConvenioService(_repo));

    [Fact]
    public async Task Guia_que_nao_exige_recebe_proposta_vazia()
    {
        var (_, codigo) = await LancarAsync(Convenio.Amil);

        var proposta = await Proposta().PrepararAsync(codigo.Id);

        proposta.Exige.Should().BeFalse();
        proposta.Dados.Should().BeNull();
        (await Proposta().PrepararAsync(987654)).Exige.Should().BeFalse();
    }

    [Fact]
    public async Task Proposta_sem_cadastro_nenhum_traz_os_padroes_e_DIZ_o_que_nao_soube_preencher()
    {
        var (_, codigo) = await LancarAsync(Convenio.Petrobras, carteirinha: null);
        _db.ChangeTracker.Clear();

        var proposta = await Proposta().PrepararAsync(codigo.Id);

        proposta.Exige.Should().BeTrue();
        var d = proposta.Dados!;
        d.IndicacaoAcidente.Should().Be("9");
        d.RegimeAtendimento.Should().Be("01");
        d.CodigoTabela.Should().Be("22");
        d.TipoConsulta.Should().Be("1");
        d.Cnes.Should().Be("9999999");
        d.DataAtendimento.Should().Be(Dia);
        d.NomeBeneficiario.Should().Be("Paciente de Teste");
        d.AtendimentoRn.Should().BeFalse();
        // O que falta fica EM BRANCO — nunca um palpite — e cada falta tem a sua frase.
        d.NumeroCarteira.Should().BeEmpty();
        d.CodigoPrestadorNaOperadora.Should().BeEmpty();
        d.NomeProfissional.Should().BeEmpty();
        d.CodigoProcedimento.Should().BeEmpty();
        d.ValorProcedimento.Should().BeNull();
        proposta.Avisos.Should().Contain(a => a.Contains("profissional"))
            .And.Contain(a => a.Contains("CNPJ"))
            .And.Contain(a => a.Contains("registro ANS"))
            .And.Contain(a => a.Contains("código do procedimento"))
            .And.Contain(a => a.Contains("preço"))
            .And.Contain(a => a.Contains("carteira"));
        GuiaAnsNaBaixa.Validar(d).Should().NotBeEmpty();
    }

    [Fact]
    public async Task Proposta_com_os_cadastros_vem_pronta_para_conferir()
    {
        var (paciente, codigo) = await LancarAsync(Convenio.Petrobras);
        await new ParametrosService(_repo).SalvarPrestadorAsync(new DadosPrestador
        {
            Cnpj = "12.345.678/0001-90", RazaoSocial = "Clínica de Teste Ltda", RegistroAnsOperadora = "422631",
            CodigosTuss = { [TipoCodigo.Consulta] = "10101012" }
        });
        _db.PrecosConvenio.Add(new PrecoConvenio { ConvenioCodigo = "Petrobras", Tipo = TipoCodigo.Consulta, Valor = 139m });
        var medica = new Profissional { Nome = "Dra. Ana de Teste", RegistroConselho = "CRM-RJ 123456" };
        _db.Profissionais.Add(medica);
        await _db.SaveChangesAsync();
        _db.Agendamentos.Add(new Agendamento
        {
            PacienteId = paciente.Id, DataHora = Dia.ToDateTime(new TimeOnly(9, 0)), ModalidadePrevista = ModalidadeAtendimento.Consulta,
            ProfissionalId = medica.Id, AtendimentoId = codigo.AtendimentoId, Status = StatusAgendamento.Realizado
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var proposta = await Proposta().PrepararAsync(codigo.Id);

        var d = proposta.Dados!;
        d.RegistroAns.Should().Be("422631");
        d.NumeroCarteira.Should().Be("000123456789");
        d.CodigoPrestadorNaOperadora.Should().Be("12345678000190");
        d.NomeContratado.Should().Be("Clínica de Teste Ltda");
        d.NomeProfissional.Should().Be("Dra. Ana de Teste");
        d.ConselhoProfissional.Should().Be("06");
        d.NumeroConselho.Should().Be("123456");
        d.UfConselho.Should().Be("33");
        d.Cbos.Should().BeEmpty();                       // o cadastro da equipe não tem CBO
        d.CodigoProcedimento.Should().Be("10101012");
        d.ValorProcedimento.Should().Be(139m);
        d.TipoConsulta.Should().Be("1");
        // Só falta o CBO — e é a única coisa que a validação acusa.
        GuiaAnsNaBaixa.Validar(d).Keys.Should().BeEquivalentTo(nameof(DadosGuiaConsultaAns.Cbos));
        proposta.Avisos.Should().ContainSingle().Which.Should().Contain("Primeira guia deste profissional");
    }

    [Fact]
    public async Task Segunda_guia_do_mesmo_profissional_herda_conselho_UF_e_CBO_da_anterior_e_propoe_seguimento()
    {
        var medica = new Profissional { Nome = "Dra. Ana de Teste", RegistroConselho = "texto que não se lê" };
        _db.Profissionais.Add(medica);
        var (paciente, primeira) = await LancarAsync(Convenio.Petrobras);
        _db.Agendamentos.Add(new Agendamento
        {
            PacienteId = paciente.Id, DataHora = Dia.ToDateTime(new TimeOnly(9, 0)), ModalidadePrevista = ModalidadeAtendimento.Consulta,
            ProfissionalId = medica.Id, AtendimentoId = primeira.AtendimentoId, Status = StatusAgendamento.Realizado
        });
        await _db.SaveChangesAsync();
        await _faturamento.DarBaixaAsync(primeira.Id, Dia.AddDays(1), "PB123", "secretaria", null, guiaAns: GuiaCompleta());

        var depois = Dia.AddDays(7);
        var segunda = (await new AtendimentoService(_repo).LancarAsync(paciente.Id, depois, ModalidadeAtendimento.Consulta))
            .Atendimento.Codigos.OrderBy(c => c.Ordem).First();
        _db.Agendamentos.Add(new Agendamento
        {
            PacienteId = paciente.Id, DataHora = depois.ToDateTime(new TimeOnly(9, 0)), ModalidadePrevista = ModalidadeAtendimento.Consulta,
            ProfissionalId = medica.Id, AtendimentoId = segunda.AtendimentoId, Status = StatusAgendamento.Realizado
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var proposta = await Proposta().PrepararAsync(segunda.Id);

        var d = proposta.Dados!;
        (d.ConselhoProfissional, d.NumeroConselho, d.UfConselho, d.Cbos).Should().Be(("06", "123456", "33", "225133"));
        d.TipoConsulta.Should().Be("2");
        d.DataAtendimento.Should().Be(depois);
        proposta.Avisos.Should().Contain(a => a.Contains("seguimento") && a.Contains(Dia.ToString("dd/MM/yyyy")));
        proposta.Avisos.Should().NotContain(a => a.Contains("Primeira guia"));
    }

    [Fact]
    public async Task Proposta_da_baixa_refeita_parte_da_guia_ja_conferida()
    {
        var (_, codigo) = await LancarAsync(Convenio.Petrobras);
        await _faturamento.DarBaixaAsync(codigo.Id, Dia.AddDays(1), "PB123", "secretaria", null,
            guiaAns: GuiaCompleta() with { Observacao = "retorno em 30 dias" });
        _db.ChangeTracker.Clear();
        await _faturamento.EstornarBaixaAsync(codigo.Id, "engano", "gerente");
        _db.ChangeTracker.Clear();

        var proposta = await Proposta().PrepararAsync(codigo.Id);

        proposta.Dados.Should().BeEquivalentTo(GuiaCompleta() with { Observacao = "retorno em 30 dias" });
        proposta.Avisos.Should().ContainSingle().Which.Should().Contain("já tinha sido conferida por secretaria");
    }

    // ==================== o XML do lote ====================

    [Fact]
    public async Task Lote_com_a_guia_conferida_e_VALIDO_no_XSD_oficial_e_leva_os_campos_no_formato_do_padrao()
    {
        var (_, codigo) = await LancarAsync(Convenio.Petrobras);
        await _faturamento.DarBaixaAsync(codigo.Id, Dia.AddDays(1), "PB123", "secretaria", null,
            guiaAns: GuiaCompleta() with { Observacao = "conferida", CoberturaEspecial = "01" });
        _db.ChangeTracker.Clear();
        var salvo = await _repo.ObterCodigoAsync(codigo.Id);
        var prestador = new DadosPrestador { CodigoNaOperadora = "12345678000190", RegistroAnsOperadora = "422631", Cnpj = "12345678000190" };

        var xml = new TissExportService().GerarLoteXml([salvo!], prestador, "7");

        var doc = XDocument.Parse(xml);
        var guia = doc.Descendants(Ans + "guiaConsulta").Single();
        guia.Descendants(Ans + "UF").Single().Value.Should().Be("33");
        guia.Descendants(Ans + "conselhoProfissional").Single().Value.Should().Be("06");
        guia.Descendants(Ans + "CBOS").Single().Value.Should().Be("225133");
        guia.Descendants(Ans + "valorProcedimento").Single().Value.Should().Be("139.00");
        guia.Descendants(Ans + "tipoConsulta").Single().Value.Should().Be("1");
        guia.Descendants(Ans + "coberturaEspecial").Single().Value.Should().Be("01");
        guia.Descendants(Ans + "saudeOcupacional").Should().BeEmpty();          // opcional vazia é OMITIDA
        guia.Descendants(Ans + "cnpjContratado").Should().BeEmpty();            // uma identificação só
        guia.ToString().Should().NotContain("Paciente de Teste");               // o TISS 4 não leva o nome
        ValidarNoXsd(doc).Should().BeEmpty();
    }

    [Fact]
    public async Task Guia_sem_conferencia_continua_saindo_como_sempre_saiu()
    {
        var (_, codigo) = await LancarAsync(Convenio.Amil);
        await _faturamento.DarBaixaAsync(codigo.Id, Dia.AddDays(1), "7654321", "secretaria", null);
        _db.ChangeTracker.Clear();
        var salvo = await _repo.ObterCodigoAsync(codigo.Id);

        var xml = new TissExportService().GerarLoteXml([salvo!],
            new DadosPrestador { CodigoNaOperadora = "P1", RegistroAnsOperadora = "326305", Cnpj = "12345678000190" }, "7");

        var guia = XDocument.Parse(xml).Descendants(Ans + "guiaConsulta").Single();
        guia.Descendants(Ans + "profissionalExecutante").Should().BeEmpty();
        guia.Descendants(Ans + "cnpjContratado").Should().ContainSingle();
    }

    private static List<string> ValidarNoXsd(XDocument doc)
    {
        var schemas = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        schemas.Add(Ans.NamespaceName, Path.Combine(PastaXsd, "tissV4_01_00.xsd"));
        schemas.Compile();
        var problemas = new List<string>();
        doc.Validate(schemas, (_, e) => problemas.Add(e.Message));
        return problemas;
    }
}
