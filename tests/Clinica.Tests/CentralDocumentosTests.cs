using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// A central de documentos (parcela 24).
///
/// O mockup mostrava NOVE folhas como um conjunto. No sistema as nove existiam e nenhuma
/// estava no mesmo lugar: quatro saíam de uma janela dentro da ficha do paciente, três só
/// do botão certo na aba certa dessa ficha, o recibo do Caixa, o orçamento só de dentro de
/// um pacote vendido — e o fechamento do período **só do app de faturamento**, que a suíte
/// não abre. Não faltava capacidade, faltava porta.
///
/// O que estes testes protegem:
/// 1. **O catálogo tem as nove**, e cada uma declara o que EXIGE para ser gerada.
/// 2. **Clínico e financeiro na MESMA lista** — a pergunta "que papéis saíram este mês?"
///    não tinha resposta porque as duas moram em tabelas diferentes.
/// 3. **Cancelado aparece marcado, nunca sumindo.** Documento não se apaga neste sistema.
/// 4. **Filtro de folha inexistente devolve NADA**, não tudo.
/// </summary>
public class CentralDocumentosTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly DocumentoClinicoService _clinicos;
    private readonly DocumentoFinanceiroService _financeiros;
    private readonly CentralDocumentosService _central;

    private static readonly DateOnly Dia = new(2026, 9, 10);
    private static readonly DateOnly Inicio = new(2026, 9, 1);
    private static readonly DateOnly Fim = new(2026, 9, 30);

    public CentralDocumentosTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);

        var prontuario = new ProntuarioService(_repo);
        var consentimentos = new ConsentimentoService(_repo);
        _clinicos = new DocumentoClinicoService(_repo, prontuario, consentimentos);
        _financeiros = new DocumentoFinanceiroService(_repo);

        var parametros = new ParametrosService(_repo);
        _central = new CentralDocumentosService(
            _repo,
            new FechamentoPdfService(_repo, new RelatorioService(_repo)),
            parametros);
    }

    private async Task<int> PacienteAsync(string nome = "Maria Souza")
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<int> ProfissionalAsync()
    {
        var p = new Profissional { Nome = "Dra. Ana", RegistroConselho = "CRM-SP 123456" };
        _db.Profissionais.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private Task<DocumentoClinico> ReceitaAsync(int pacienteId, int profissionalId, DateOnly? dia = null)
        => _clinicos.EmitirAsync(new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.Receita,
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            Data = dia ?? Dia,
            Itens = [new ItemDocumento { Descricao = "Fitoterápico", Quantidade = "1 frasco" }]
        }, operador: "ana");

    private Task<DocumentoClinico> AtestadoAsync(int pacienteId, int profissionalId)
        => _clinicos.EmitirAsync(new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.Atestado,
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            Data = Dia,
            DiasAfastamento = 2
        }, operador: "ana");

    private Task<DocumentoFinanceiro> ReciboAsync(int? pacienteId, decimal valor)
        => _financeiros.EmitirAsync(new DocumentoFinanceiro
        {
            Tipo = TipoDocumentoFinanceiro.Recibo,
            PacienteId = pacienteId,
            Destinatario = "Maria Souza",
            Data = Dia,
            Itens = [new ItemDocumentoFinanceiro
            {
                Descricao = "Sessão de acupuntura", Quantidade = 1, ValorUnitario = valor
            }]
        }, operador: "ana");

    // ---------------- O catálogo ----------------

    [Fact]
    public void Catalogo_TemAsNoveFolhasDoMockup()
    {
        var rotulos = CentralDocumentosService.Catalogo.Select(f => f.Rotulo).ToList();

        // As nove exatas que a cliente viu no mockup. Se uma sair daqui, ela procura e não
        // acha — que foi o problema que esta parcela resolve.
        rotulos.Should().Contain("Receituário");
        rotulos.Should().Contain("Atestado");
        rotulos.Should().Contain("Declaração de comparecimento");
        rotulos.Should().Contain("Solicitação de exames");
        rotulos.Should().Contain("Relatório de evolução clínica");
        rotulos.Should().Contain("Ficha de anamnese");
        rotulos.Should().Contain("Recibo de pagamento");
        rotulos.Should().Contain("Orçamento");
        rotulos.Should().Contain("Fechamento do período");
    }

    [Fact]
    public void Catalogo_CadaFolhaDizOQueEXIGE()
    {
        // A tela habilita ou explica a partir daqui. Botão que abre e só então diz "escolha
        // um paciente" faz a pessoa descobrir o requisito errando.
        CentralDocumentosService.Folha("receita")!.Exigencia
            .Should().Be(ExigenciaFolha.Paciente);
        CentralDocumentosService.Folha("anamnese")!.Exigencia
            .Should().Be(ExigenciaFolha.PacienteComProntuario);
        CentralDocumentosService.Folha("recibo")!.Exigencia
            .Should().Be(ExigenciaFolha.LancamentoNoCaixa);
        CentralDocumentosService.Folha(CentralDocumentosService.ChaveFechamentoPeriodo)!.Exigencia
            .Should().Be(ExigenciaFolha.Periodo);
    }

    [Fact]
    public void Catalogo_AsTresMontadasDoProntuarioEstaoMarcadas()
    {
        var montadas = CentralDocumentosService.Catalogo
            .Where(f => f.MontadaDoProntuario)
            .Select(f => f.Chave)
            .ToList();

        // Estas três não se digitam — o sistema monta do prontuário. A tela precisa saber
        // para não abrir um formulário em branco que ninguém deveria preencher.
        montadas.Should().BeEquivalentTo(["relatorio-evolucao", "anamnese", "consentimento"]);
    }

    [Fact]
    public void Catalogo_ChaveDesconhecida_NaoResolve()
    {
        CentralDocumentosService.Folha("nao-existe").Should().BeNull();
    }

    [Fact]
    public void Rotulo_SegueOCatalogo_NaoOEnum()
    {
        // A cliente chama de "Receituário" e "Solicitação de exames"; o enum chama de
        // "Receita" e "Pedido de exame". Quem manda na tela é o nome do mockup.
        CentralDocumentosService.RotularClinico(TipoDocumentoClinico.Receita)
            .Should().Be("Receituário");
        CentralDocumentosService.RotularClinico(TipoDocumentoClinico.PedidoExame)
            .Should().Be("Solicitação de exames");
        CentralDocumentosService.RotularFinanceiro(TipoDocumentoFinanceiro.Recibo)
            .Should().Be("Recibo de pagamento");
    }

    // ---------------- A lista unificada ----------------

    [Fact]
    public async Task Emitidas_TrazClinicoEFinanceiroNaMESMALista()
    {
        var paciente = await PacienteAsync();
        var profissional = await ProfissionalAsync();

        await ReceitaAsync(paciente, profissional);
        await ReciboAsync(paciente, 150m);

        var folhas = await _central.EmitidasAsync(Inicio, Fim);

        // Era esta a pergunta sem resposta: as duas moram em tabelas diferentes, e ninguém
        // conseguia dizer que papéis saíram no mês.
        folhas.Should().HaveCount(2);
        folhas.Should().Contain(f => f.Natureza == NaturezaFolha.Clinico);
        folhas.Should().Contain(f => f.Natureza == NaturezaFolha.Financeiro);
    }

    [Fact]
    public async Task Emitidas_ValorSoNasFinanceiras()
    {
        var paciente = await PacienteAsync();
        var profissional = await ProfissionalAsync();

        await ReceitaAsync(paciente, profissional);
        await ReciboAsync(paciente, 150m);

        var folhas = await _central.EmitidasAsync(Inicio, Fim);

        // Receita não tem preço, e mostrar "R$ 0,00" ao lado dela sugeriria uma cobrança
        // que não existe.
        folhas.Single(f => f.Natureza == NaturezaFolha.Clinico).Valor.Should().BeNull();
        folhas.Single(f => f.Natureza == NaturezaFolha.Financeiro).Valor.Should().Be(150m);
    }

    [Fact]
    public async Task Emitidas_CanceladoAPARECEMarcado()
    {
        var paciente = await PacienteAsync();
        var profissional = await ProfissionalAsync();
        var receita = await ReceitaAsync(paciente, profissional);

        await _clinicos.CancelarAsync(receita.Id, "Dose errada", "ana");

        var folhas = await _central.EmitidasAsync(Inicio, Fim);

        // Documento não se apaga: cancela-se com motivo e emite-se outro. Esconder o
        // cancelado faria a lista mentir sobre o que o paciente levou para casa.
        var f = folhas.Should().ContainSingle().Subject;
        f.Cancelado.Should().BeTrue();
        f.MotivoCancelamento.Should().Be("Dose errada");
    }

    [Fact]
    public async Task Emitidas_ForaDoPeriodo_NaoEntra()
    {
        var paciente = await PacienteAsync();
        var profissional = await ProfissionalAsync();

        await ReceitaAsync(paciente, profissional, Dia);
        await ReceitaAsync(paciente, profissional, Inicio.AddMonths(-2));

        (await _central.EmitidasAsync(Inicio, Fim)).Should().ContainSingle();
    }

    [Fact]
    public async Task Emitidas_FiltraPorFolha()
    {
        var paciente = await PacienteAsync();
        var profissional = await ProfissionalAsync();

        await ReceitaAsync(paciente, profissional);
        await AtestadoAsync(paciente, profissional);
        await ReciboAsync(paciente, 150m);

        (await _central.EmitidasAsync(Inicio, Fim, "receita"))
            .Should().ContainSingle().Which.FolhaRotulo.Should().Be("Receituário");

        (await _central.EmitidasAsync(Inicio, Fim, "recibo"))
            .Should().ContainSingle().Which.Natureza.Should().Be(NaturezaFolha.Financeiro);
    }

    [Fact]
    public async Task Emitidas_FolhaDesconhecida_DevolveNADA_NaoTudo()
    {
        var paciente = await PacienteAsync();
        var profissional = await ProfissionalAsync();
        await ReceitaAsync(paciente, profissional);

        // Filtro que silenciosamente vira "sem filtro" faz a pessoa concluir que há mais
        // papel emitido do que há.
        (await _central.EmitidasAsync(Inicio, Fim, "nao-existe")).Should().BeEmpty();
    }

    [Fact]
    public async Task Emitidas_FechamentoDoPeriodo_NaoTemSegundaVia()
    {
        var paciente = await PacienteAsync();
        var profissional = await ProfissionalAsync();
        await ReceitaAsync(paciente, profissional);

        // O fechamento não é gravado: é conferência, montada na hora a partir das guias.
        // Lista vazia aqui é a verdade — não há segunda via de um relatório que se refaz
        // igual sempre que se pede.
        (await _central.EmitidasAsync(
                Inicio, Fim, CentralDocumentosService.ChaveFechamentoPeriodo))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Emitidas_FiltraPorPaciente()
    {
        var maria = await PacienteAsync("Maria");
        var joao = await PacienteAsync("João");
        var profissional = await ProfissionalAsync();

        await ReceitaAsync(maria, profissional);
        await ReceitaAsync(joao, profissional);
        await ReciboAsync(joao, 200m);

        (await _central.EmitidasAsync(Inicio, Fim, pacienteId: maria))
            .Should().ContainSingle();
        (await _central.EmitidasAsync(Inicio, Fim, pacienteId: joao)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Emitidas_DoMaisRecenteAoMaisAntigo()
    {
        var paciente = await PacienteAsync();
        var profissional = await ProfissionalAsync();

        await ReceitaAsync(paciente, profissional, Dia.AddDays(-5));
        await AtestadoAsync(paciente, profissional);

        var folhas = await _central.EmitidasAsync(Inicio, Fim);

        folhas[0].FolhaRotulo.Should().Be("Atestado");
    }

    // ---------------- O resumo ----------------

    [Fact]
    public async Task Resumo_ContaEMostraOCancelado()
    {
        var paciente = await PacienteAsync();
        var profissional = await ProfissionalAsync();

        await ReceitaAsync(paciente, profissional);
        await ReceitaAsync(paciente, profissional);
        var atestado = await AtestadoAsync(paciente, profissional);
        await _clinicos.CancelarAsync(atestado.Id, "Data errada", "ana");

        var r = await _central.ResumoAsync(Inicio, Fim);

        r.Emitidas.Should().Be(3);
        r.Canceladas.Should().Be(1);
        r.PorFolha[0].Should().Be(("Receituário", 2));
        r.Vazio.Should().BeFalse();
    }

    [Fact]
    public async Task Resumo_PeriodoSemPapel_NaoQuebra()
    {
        var r = await _central.ResumoAsync(Inicio, Fim);

        r.Vazio.Should().BeTrue();
        r.PorFolha.Should().BeEmpty();
        await Task.CompletedTask;
    }

    // ---------------- Fechamento do período ----------------

    [Fact]
    public async Task FechamentoDoPeriodo_GeraPdfPelaSUITE()
    {
        // Até esta parcela o FechamentoPdfService só era chamado pelo app de faturamento,
        // que está congelado e que a suíte não abre: a folha existia e ninguém da suíte
        // conseguia tirá-la.
        var pdf = await _central.GerarFechamentoPeriodoAsync(Inicio, Fim);

        pdf.Should().NotBeNull();
        pdf.Length.Should().BeGreaterThan(1000);
    }

    [Fact]
    public async Task FechamentoDoPeriodo_DatasInvertidas_Recusa()
    {
        var acao = () => _central.GerarFechamentoPeriodoAsync(Fim, Inicio);

        // Gerar um PDF de um período negativo produziria um relatório vazio com cara de
        // "não houve movimento".
        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*anterior ao início*");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
