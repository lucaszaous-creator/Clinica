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
/// Prontidão da Recepção para produção (parcela 62).
///
/// A auditoria do módulo antes de liberá-lo achou dez grupos de defeito, e o que os une é
/// que <b>nenhum deles quebra alguma coisa</b>: o build fica verde, os 1441 testes ficam
/// verdes, e quem descobre é a recepcionista no balcão com o paciente na frente. São as
/// variantes mais caras do defeito recorrente do projeto — o aviso que não chega, o botão
/// que a pessoa certa não alcança, a leitura que responde por último e sobrescreve a nova.
///
/// Estes testes fixam as três correções que o compilador não é capaz de proteger sozinho.
/// </summary>
public class RecepcaoProntidaoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly FechamentoSessaoService _fechamento;

    private static readonly DateTime Sessao = new(2026, 8, 20, 14, 0, 0);


    /// <summary>
    /// Todo horário MARCADO precisa de dono desde a parcela 95 — o fixture cria um para os
    /// cenários que não se importam com QUEM atende.
    /// </summary>
    private readonly int _profPadrao;

    public RecepcaoProntidaoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        var profPadrao = new Profissional { Nome = "Dra. Padrão" };
        _db.Profissionais.Add(profPadrao);
        _db.SaveChanges();
        _profPadrao = profPadrao.Id;
        _repo = new ClinicaRepositorio(_db);

        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
        _fechamento = new FechamentoSessaoService(
            _repo, _agenda, new PacoteService(_repo), new EstoqueService(_repo),
            new FinanceiroService(_repo));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    // ====================================================================
    // 1. O recado do lançamento chega ao balcão pelo Finalizar da Fila
    // ====================================================================

    /// <summary>
    /// A NC reaberta é o recado mais caro do sistema: ela diz "o paciente voltou, cobre a
    /// guia AGORA, com ele na frente". Ela existe desde que a não conformidade existe, e
    /// pelo <b>Finalizar da Fila</b> — o caminho que a clínica usa todo dia — ela nunca
    /// chegou: <c>ConcluirAsync</c> recebia os avisos de <c>LancarAsync</c> e abria uma
    /// lista nova vazia por cima.
    ///
    /// O defeito não falhava em lugar nenhum. A guia nascia certa, o pacote debitava, o
    /// caixa entrava — e a única frase que pedia uma ação sumia no caminho. A pessoa
    /// descobriria dias depois, na rodada de pendências, com o paciente longe.
    /// </summary>
    [Fact]
    public async Task Finalizar_leva_ao_balcao_a_NC_reaberta_porque_o_paciente_voltou()
    {
        var pacienteId = await CriarPacienteAsync();

        // Uma sessão anterior cuja guia foi marcada como NÃO CONFORMIDADE: ela saiu das
        // pendências ativas e some do painel. Só a volta do paciente a traz de volta.
        await MarcarNaoConformidadeAntigaAsync(pacienteId);

        var ag = await _agenda.AgendarAsync(
            pacienteId, Sessao, ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _profPadrao);

        var resultado = await _fechamento.ConcluirAsync(
            new DecisaoFechamento(ag.Id), operador: "recepcao");

        // O fechamento em si deu certo — e é justamente por isso que o recado precisa de
        // canal PRÓPRIO: somá-lo a `Avisos` faria um dia perfeito parecer um dia com erro.
        resultado.TudoCerto.Should().BeTrue();

        resultado.TemRecados.Should().BeTrue(
            "a NC reaberta é o único aviso do sistema que pede uma ação com o paciente ainda no balcão");
        resultado.RecadosDoLancamento.Should()
            .ContainSingle(r => r.Contains("não conformidade", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// O outro recado que morria no mesmo lugar, e que é o ASSUNTO DO PRODUTO: a guia que
    /// só libera +24h depois. A regra do convênio a anuncia desde sempre ("2º código
    /// previsto para +24h — solicitar pelo sistema"), e pelo Finalizar da Fila a frase
    /// nunca saía da camada de serviço.
    ///
    /// Anunciá-la no instante em que o paciente vai embora é o mais barato que o sistema
    /// faz: dali para a frente ela vira pendência no painel, prazo correndo e, no décimo
    /// dia, rodada bloqueante.
    ///
    /// E o recado da NC <b>não</b> aparece para quem não tem NC — janela que avisa alguma
    /// coisa em toda sessão é janela que a recepcionista fecha sem ler, e é assim que a NC
    /// reaberta voltaria a passar batida, agora por excesso.
    /// </summary>
    [Fact]
    public async Task Finalizar_anuncia_o_segundo_codigo_e_nao_inventa_NC()
    {
        var pacienteId = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            pacienteId, Sessao, ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _profPadrao);

        var resultado = await _fechamento.ConcluirAsync(new DecisaoFechamento(ag.Id));

        resultado.TudoCerto.Should().BeTrue();
        resultado.RecadosDoLancamento.Should()
            .Contain(r => r.Contains("2º código", StringComparison.Ordinal));
        resultado.RecadosDoLancamento.Should()
            .NotContain(r => r.Contains("não conformidade", StringComparison.OrdinalIgnoreCase));
    }

    // ====================================================================
    // 2. O balcão alcança o que o balcão faz
    // ====================================================================

    /// <summary>
    /// As três permissões que fazem a Recepção funcionar, e que a tela precisa consultar
    /// com o sentido certo.
    ///
    /// <c>VenderPacote</c> é bit PRÓPRIO desde a parcela 60 e não <c>VerFinanceiro</c>:
    /// vender dez sessões ao paciente é combinar um preço no balcão, e dar o financeiro
    /// junto abriria o caixa, a conciliação e as contas a pagar de uma vez.
    ///
    /// <c>MovimentarFila</c> é o corte estreito da parcela 61 — carimbar chegada e chamada
    /// sem poder marcar horário de terceiros.
    ///
    /// E a Recepção <b>não tem</b> <c>EditarFinanceiro</c>: é essa ausência que obriga a
    /// tela de fechamento a perguntar por OU (<c>PodeAlgum</c>) em vez de exigir o bit do
    /// Financeiro. Trocar de volta por um <c>Pode(EditarFinanceiro)</c> compila, passa em
    /// tudo, e apaga o campo de dinheiro do balcão — que é o passo do Finalizar em que a
    /// clínica registra o que o paciente pagou.
    /// </summary>
    [Fact]
    public void Perfil_da_recepcao_alcanca_pacote_fila_e_lancamento()
    {
        var recepcao = PerfisAcesso.Padrao(PerfilAcesso.Recepcao);

        recepcao.HasFlag(Permissao.VenderPacote).Should().BeTrue();
        recepcao.HasFlag(Permissao.MovimentarFila).Should().BeTrue();
        recepcao.HasFlag(Permissao.LancarAtendimento).Should().BeTrue();

        recepcao.HasFlag(Permissao.EditarFinanceiro).Should().BeFalse(
            "quem atende no balcão não administra o financeiro da clínica — e é por isso "
            + "que as telas dele perguntam por OU, nunca só pelo bit do Financeiro");
    }

    /// <summary>
    /// <c>Pode</c> com bits combinados é um E, e foi assim que a fila do médico e a do
    /// balcão passaram a exigir coisas diferentes para a mesma escrita (parcela 61). Este
    /// teste fixa a distinção que as telas usam: quem tem <b>um</b> dos dois passa.
    /// </summary>
    [Fact]
    public void PodeAlgum_e_um_OU_e_Pode_continua_sendo_um_E()
    {
        var sessao = new SessaoUsuario();
        sessao.Entrar(new UsuarioSistema
        {
            Id = 12,
            Login = "recepcao",
            Nome = "Balcão",
            Perfil = PerfilAcesso.Recepcao
        });

        sessao.PodeAlgum(Permissao.LancarAtendimento | Permissao.EditarFinanceiro)
              .Should().BeTrue("tem LancarAtendimento, e basta um dos dois");

        sessao.Pode(Permissao.LancarAtendimento | Permissao.EditarFinanceiro)
              .Should().BeFalse("Pode exige os DOIS — é o E que apagaria o campo de dinheiro");
    }

    // ====================================================================
    // 3. O identificador do enum não sai na tela nem no relatório
    // ====================================================================

    /// <summary>
    /// De onde o paciente veio é um dos poucos campos que a direção lê AGRUPADO num
    /// relatório — o identificador cru vira a categoria do relatório, e sai sem acento.
    /// A ficha do paciente escrevia esta lista à mão desde que o campo existe; a janela
    /// de cadastro não escrevia nenhuma, e o WPF chamava <c>ToString()</c>.
    /// </summary>
    [Theory]
    [InlineData(OrigemPaciente.Indicacao, "Indicação")]
    [InlineData(OrigemPaciente.Convenio, "Lista do convênio")]
    [InlineData(OrigemPaciente.RedesSociais, "Redes sociais")]
    [InlineData(OrigemPaciente.Fachada, "Passou em frente")]
    [InlineData(OrigemPaciente.Campanha, "Campanha da clínica")]
    public void Origem_do_paciente_tem_rotulo_em_portugues(OrigemPaciente origem, string esperado)
        => RotulosEnum.De(origem).Should().Be(esperado);

    /// <summary>
    /// Nenhum valor pode sair com cara de identificador. O que denuncia o PascalCase é a
    /// maiúscula NO MEIO da palavra ("RedesSociais"), e é ela que este teste proíbe — não
    /// a igualdade com o <c>ToString()</c>, porque "Outro" é ao mesmo tempo o
    /// identificador e o português correto.
    ///
    /// Vale para a origem que alguém acrescentar depois: o dia em que o enum ganhar
    /// "IndicacaoMedica" sem rótulo, este teste falha antes de a palavra chegar ao
    /// relatório da direção.
    /// </summary>
    [Fact]
    public void Nenhuma_origem_sai_com_cara_de_identificador()
    {
        foreach (var origem in Enum.GetValues<OrigemPaciente>())
        {
            var rotulo = RotulosEnum.De(origem);

            rotulo.Should().NotBeNullOrWhiteSpace();
            rotulo.Skip(1).Any(char.IsUpper).Should().BeFalse(
                $"'{origem}' sairia em PascalCase na janela de cadastro e no relatório da direção");
        }
    }

    // ==================== Cenário ====================

    private async Task<int> CriarPacienteAsync()
    {
        var p = new Paciente
        {
            Nome = "Maria",
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>
    /// Uma guia de sessão antiga marcada como não conformidade — fora das pendências
    /// ativas, esperando o paciente voltar.
    /// </summary>
    private async Task MarcarNaoConformidadeAntigaAsync(int pacienteId)
    {
        var anterior = new Atendimento
        {
            PacienteId = pacienteId,
            Data = DateOnly.FromDateTime(Sessao).AddDays(-30),
            Modalidade = ModalidadeAtendimento.AcupunturaComEletro,
            Numero = "ATD-ANTIGO"
        };
        _db.Atendimentos.Add(anterior);
        await _db.SaveChangesAsync();

        _db.Codigos.Add(new CodigoFaturamento
        {
            AtendimentoId = anterior.Id,
            Descricao = "Sessão de acupuntura",
            Tipo = TipoCodigo.Acupuntura,
            DataPrevistaFaturamento = anterior.Data,
            Status = StatusCodigo.NaoConformidade,
            NaoConformidadeJustificativa = "Guia não obtida no prazo.",
            NaoConformidadeEm = Sessao.AddDays(-20)
        });
        await _db.SaveChangesAsync();
    }
}
