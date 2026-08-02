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
/// Os quatro módulos, de ponta a ponta.
///
/// Todo o resto da suíte testa TRECHOS: o `FechamentoSessaoService` sozinho, a
/// conciliação sozinha, a glosa sozinha. Cada um passa, e mesmo assim o circuito pode
/// estar partido — porque o que liga um módulo ao outro aqui não é chamada de método, é
/// **chave estrangeira**: eles compartilham o BANCO, não mensagens. Não há fila, evento
/// nem sincronização; o que um grava, o outro lê.
///
/// É por isso que um elo se rompe em silêncio. Se `LancamentoFinanceiro.CodigoFaturamentoId`
/// deixasse de ser preenchido, nenhum teste de unidade falharia — e a guia simplesmente
/// nunca sairia da lista de conciliação, para sempre. Foi exatamente o que aconteceu com
/// a glosa até a parcela 27: o convênio recusava a guia e o dinheiro continuava no fluxo
/// de caixa.
///
/// Estes testes percorrem o caminho inteiro, uma vez por circuito, afirmando o ELO — não
/// a regra de cada ponta, que já tem teste próprio.
/// </summary>
public class CircuitoCompletoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    private readonly AgendaService _agenda;
    private readonly PacoteService _pacotes;
    private readonly EstoqueService _estoque;
    private readonly FinanceiroService _financeiro;
    private readonly FechamentoSessaoService _fechamento;
    private readonly FaturamentoService _faturamento;
    private readonly GlosaService _glosas;
    private readonly ReceitaGlosadaService _receitaGlosada;

    private static readonly DateOnly Dia = new(2026, 8, 12);

    public CircuitoCompletoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);

        var atendimentos = new AtendimentoService(_repo);
        _agenda = new AgendaService(_repo, atendimentos);
        _pacotes = new PacoteService(_repo);
        _estoque = new EstoqueService(_repo);
        _financeiro = new FinanceiroService(_repo);
        _fechamento = new FechamentoSessaoService(_repo, _agenda, _pacotes, _estoque, _financeiro);
        _faturamento = new FaturamentoService(_repo);
        _glosas = new GlosaService(_repo);
        _receitaGlosada = new ReceitaGlosadaService(_repo, _financeiro);
    }

    // ------------------------------------------------------------------ apoio

    private async Task<int> CriarPacienteAsync(string nome = "Maria Souza")
    {
        var p = new Paciente
        {
            Nome = nome,
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino,
            Carteirinha = "0123456789"
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private Task<Agendamento> MarcarAsync(int pacienteId, TimeOnly hora)
        => _agenda.AgendarAsync(pacienteId, Dia.ToDateTime(hora),
            ModalidadeAtendimento.AcupunturaComEletro, null);

    private async Task<int> CriarInsumoAsync(decimal saldoInicial)
    {
        var item = await _estoque.SalvarItemAsync(new ItemEstoque
        {
            Nome = "Agulha 0,25x30",
            Unidade = "un",
            EstoqueMinimo = 10
        });

        await _estoque.MovimentarAsync(new MovimentoEstoque
        {
            ItemEstoqueId = item.Id,
            Tipo = TipoMovimentoEstoque.Entrada,
            Quantidade = saldoInicial,
            Data = Dia.AddDays(-30),
            CustoUnitario = 0.50m
        });

        return item.Id;
    }

    private async Task<int> VenderPacoteAsync(int pacienteId)
    {
        var catalogo = await _pacotes.SalvarCatalogoAsync(new PacoteCatalogo
        {
            Nome = "10 sessões",
            Tipo = TipoPacote.Sessoes,
            SessoesIncluidas = 10,
            Valor = 1200m,
            ValidadeDias = 180,
            Ativo = true
        });

        var venda = await _pacotes.VenderAsync(pacienteId, catalogo.Id, Dia.AddDays(-10));
        return venda.Id;
    }

    // =====================================================================
    // Circuito 1 — o dia da clínica
    // =====================================================================

    /// <summary>
    /// O caminho que o dinheiro faz: a recepção conclui a sessão, a guia nasce pelas
    /// regras do convênio, a secretária dá baixa no sistema da operadora, a guia aparece
    /// na conciliação do Financeiro, a receita é lançada — e a guia SAI da lista.
    ///
    /// O elo que este teste prende é o último: a guia sai da conciliação porque passou a
    /// TER receita (`LancamentoFinanceiro.CodigoFaturamentoId`), não porque alguém a
    /// marcou como resolvida. Se esse campo deixasse de ser preenchido, nada falharia e a
    /// guia ficaria na lista para sempre.
    /// </summary>
    [Fact]
    public async Task Recepcao_conclui_a_sessao_e_a_guia_chega_ate_a_receita_do_financeiro()
    {
        var pacienteId = await CriarPacienteAsync();
        var agendamento = await MarcarAsync(pacienteId, new TimeOnly(9, 0));

        // --- Recepção: quatro fatos do mesmo ato ---
        var itemId = await CriarInsumoAsync(saldoInicial: 100m);

        var resultado = await _fechamento.ConcluirAsync(new DecisaoFechamento(
            agendamento.Id,
            DebitarPacote: false,
            GerarLancamento: false,
            Insumos: [new InsumoAConsumir(itemId, 5m)]), operador: "recepcao");

        resultado.TudoCerto.Should().BeTrue(
            "o fechamento não pode reportar sucesso escondendo aviso: " + string.Join(" · ", resultado.Avisos));
        resultado.Movimentos.Should().ContainSingle("o insumo saiu do estoque no mesmo ato");

        // --- Faturamento: a guia nasceu pelas regras do convênio ---
        var codigos = resultado.Atendimento.Codigos;
        codigos.Should().HaveCountGreaterThan(1, "a eletroacupuntura gera o 1º e o 2º código");

        var primeiro = codigos.OrderBy(c => c.Ordem).First();

        // Antes da baixa a guia NÃO está na conciliação: ela ainda não foi efetivada no
        // sistema do convênio, e cobrar receita do que não foi entregue inventaria caixa.
        (await _financeiro.GuiasSemLancamentoAsync(Dia.AddDays(-30), Dia.AddDays(30)))
            .Should().NotContain(g => g.CodigoId == primeiro.Id);

        await _faturamento.DarBaixaAsync(primeiro.Id, Dia, "GUIA-001", "secretaria", null);
        await _repo.SalvarAsync();

        // --- Financeiro: a guia baixada aparece para conciliar ---
        var pendentes = await _financeiro.GuiasSemLancamentoAsync(Dia.AddDays(-30), Dia.AddDays(30));
        var guia = pendentes.Should().ContainSingle(g => g.CodigoId == primeiro.Id).Subject;

        var lancamento = await _financeiro.LancarReceitaDaGuiaAsync(guia, 150m, operador: "financeiro");

        lancamento.CodigoFaturamentoId.Should().Be(primeiro.Id,
            "é ESTE campo que fecha o elo entre faturamento e financeiro");

        // --- E a guia sai da lista sozinha, por ter receita ---
        (await _financeiro.GuiasSemLancamentoAsync(Dia.AddDays(-30), Dia.AddDays(30)))
            .Should().NotContain(g => g.CodigoId == primeiro.Id,
                "ela sai porque passou a ter receita, não porque alguém a marcou");
    }

    /// <summary>
    /// O mesmo ato, agora com pacote e cobrança. Duas regras do produto se encontram
    /// aqui: o pacote debita UMA sessão por atendimento, e cobrança **não é sugerida**
    /// quando há pacote — sessão comprada já foi paga, e lançar de novo criaria receita
    /// que a clínica nunca recebeu.
    /// </summary>
    [Fact]
    public async Task Sessao_de_pacote_debita_o_saldo_e_nao_cobra_de_novo()
    {
        var pacienteId = await CriarPacienteAsync();
        await VenderPacoteAsync(pacienteId);
        var agendamento = await MarcarAsync(pacienteId, new TimeOnly(10, 0));

        var proposta = await _fechamento.PrepararAsync(agendamento.Id, Dia);

        proposta.PacoteADebitar.Should().NotBeNull("o sistema acha o pacote que vence primeiro");
        proposta.TemPacote.Should().BeTrue();
        proposta.SugereLancamento.Should().BeFalse(
            "sessão de pacote já foi paga — sugerir cobrança criaria receita fantasma");

        var resultado = await _fechamento.ConcluirAsync(
            new DecisaoFechamento(agendamento.Id, DebitarPacote: true), operador: "recepcao");

        resultado.Consumo.Should().NotBeNull();

        var saldos = await _pacotes.DoPacienteAsync(pacienteId, Dia);
        saldos.Should().ContainSingle().Which.SaldoSessoes.Should().Be(9,
            "dez menos a sessão de hoje");
    }

    // =====================================================================
    // Circuito 2 — a glosa que volta
    // =====================================================================

    /// <summary>
    /// O elo de VOLTA, que faltava até a parcela 27: o convênio recusa a guia e o dinheiro
    /// precisa sair do fluxo de caixa.
    ///
    /// Três regras se encontram aqui, e todas são sobre não mentir com número:
    /// 1. a glosa **não apaga** o lançamento — é fato datado, sai do total, fica no histórico;
    /// 2. só o PREVISTO se cancela (o realizado é dinheiro que entrou; estorno é outra saída);
    /// 3. a guia **volta sozinha** para a conciliação, porque deixou de ter receita ativa.
    /// </summary>
    [Fact]
    public async Task Glosa_tira_a_receita_do_caixa_e_devolve_a_guia_para_a_conciliacao()
    {
        var pacienteId = await CriarPacienteAsync();
        var agendamento = await MarcarAsync(pacienteId, new TimeOnly(9, 0));

        var resultado = await _fechamento.ConcluirAsync(
            new DecisaoFechamento(agendamento.Id, DebitarPacote: false));
        var codigo = resultado.Atendimento.Codigos.OrderBy(c => c.Ordem).First();

        await _faturamento.DarBaixaAsync(codigo.Id, Dia, "GUIA-002", "secretaria", null);
        await _repo.SalvarAsync();

        var guia = (await _financeiro.GuiasSemLancamentoAsync(Dia.AddDays(-30), Dia.AddDays(30)))
            .Single(g => g.CodigoId == codigo.Id);

        var lancamento = await _financeiro.LancarReceitaDaGuiaAsync(
            guia, 150m, StatusLancamento.Previsto, operador: "financeiro");

        // --- o convênio recusa ---
        await _glosas.RegistrarAsync(codigo.Id, Dia.AddDays(20), "Código não autorizado",
            operador: "secretaria");
        await _repo.SalvarAsync();

        var glosadas = await _receitaGlosada.PendentesAsync(Dia, Dia.AddDays(30));
        glosadas.Should().ContainSingle(g => g.CodigoId == codigo.Id,
            "a guia glosada com receita ativa é o que o Financeiro precisa ver");

        await _receitaGlosada.CancelarReceitaAsync(
            codigo.Id, "glosa aceita — não vamos recorrer", "financeiro");

        // 1. o lançamento continua existindo, cancelado
        var depois = await _repo.ObterLancamentoAsync(lancamento.Id);
        depois!.Status.Should().Be(StatusLancamento.Cancelado);
        depois.Should().NotBeNull("glosa não apaga o lançamento: ela o cancela");

        // 2. e some do total do caixa
        var resumo = await _financeiro.ResumoAsync(Dia.AddDays(-30), Dia.AddDays(60));
        resumo.EntradasPrevistas.Should().Be(0m, "receita glosada não é receita");
        resumo.EntradasRealizadas.Should().Be(0m);

        // 3. a guia volta à conciliação sem ninguém mandar
        (await _financeiro.GuiasSemLancamentoAsync(Dia.AddDays(-30), Dia.AddDays(30)))
            .Should().Contain(g => g.CodigoId == codigo.Id,
                "cancelado o vínculo, ela deixa de ter receita e reaparece — "
                + "o caminho de volta funcionando sem uma linha de código para isso");
    }

    /// <summary>
    /// A guia glosada volta para a conciliação **marcada**, nunca em branco: lançar
    /// receita dela de novo é permitido (a clínica pode estar certa de que recupera no
    /// recurso), mas quem lança precisa saber o que está lançando.
    /// </summary>
    [Fact]
    public async Task Guia_glosada_reaparece_marcada_na_conciliacao()
    {
        var pacienteId = await CriarPacienteAsync();
        var agendamento = await MarcarAsync(pacienteId, new TimeOnly(9, 0));

        var resultado = await _fechamento.ConcluirAsync(
            new DecisaoFechamento(agendamento.Id, DebitarPacote: false));
        var codigo = resultado.Atendimento.Codigos.OrderBy(c => c.Ordem).First();

        await _faturamento.DarBaixaAsync(codigo.Id, Dia, "GUIA-003", "secretaria", null);
        await _glosas.RegistrarAsync(codigo.Id, Dia.AddDays(5), "Divergência de código");
        await _repo.SalvarAsync();

        var guia = (await _financeiro.GuiasSemLancamentoAsync(Dia.AddDays(-30), Dia.AddDays(30)))
            .Single(g => g.CodigoId == codigo.Id);

        guia.Glosa.Should().Be(StatusGlosa.Glosada);
        guia.GlosaEmAberto.Should().BeTrue();
        guia.MotivoGlosa.Should().Contain("Divergência");
    }

    // =====================================================================
    // Circuito 3 — o que a Recepção manda de volta ao balcão
    // =====================================================================

    /// <summary>
    /// A não conformidade reabre quando o paciente VOLTA.
    ///
    /// É o elo mais fácil de perder de vista, porque não passa por tela nenhuma: a
    /// secretária marcou a guia como não conforme meses atrás, o paciente reaparece, e o
    /// sistema precisa cobrar a guia enquanto ele está ali. Quem reabre é o
    /// `AtendimentoService`, no lançamento — o mesmo que a Recepção chama pelo fechamento.
    /// </summary>
    [Fact]
    public async Task Paciente_que_volta_reabre_a_nao_conformidade_da_guia_antiga()
    {
        var pacienteId = await CriarPacienteAsync();

        var primeira = await MarcarAsync(pacienteId, new TimeOnly(9, 0));
        var r1 = await _fechamento.ConcluirAsync(
            new DecisaoFechamento(primeira.Id, DebitarPacote: false));

        var codigo = r1.Atendimento.Codigos.OrderBy(c => c.Ordem).First();

        var rodada = new RodadaPendenciasService(
            _repo, new PendenciaService(_repo), new ParametrosService(_repo));
        await rodada.MarcarNaoConformidadeAsync(
            codigo.Id, "convênio não localizou a autorização", "secretaria");

        var naoConformes = await rodada.NaoConformidadesAsync();
        naoConformes.Should().ContainSingle(c => c.CodigoId == codigo.Id);

        // --- o paciente volta ---
        var segunda = await _agenda.AgendarAsync(pacienteId, Dia.AddDays(30).ToDateTime(new TimeOnly(9, 0)),
            ModalidadeAtendimento.AcupunturaComEletro, null);
        await _fechamento.ConcluirAsync(new DecisaoFechamento(segunda.Id, DebitarPacote: false));

        (await rodada.NaoConformidadesAsync())
            .Should().NotContain(c => c.CodigoId == codigo.Id,
                "o paciente voltou: a guia sai da NC e volta a ser pendência, "
                + "para a secretária cobrá-la com ele na frente dela");
    }

    // =====================================================================
    // Circuito 4 — a direção enxerga o dia que a recepção fechou
    // =====================================================================

    /// <summary>
    /// Monta o painel da direção com todas as dependências reais.
    ///
    /// São onze serviços; construí-los à mão aqui é de propósito — é o que prova que o
    /// grafo do Gerente fecha sem o contêiner de DI resolver nada por baixo.
    /// </summary>
    private PainelDirecaoService MontarPainel()
    {
        var parametros = new ParametrosService(_repo);
        var contas = new ContasService(_repo);
        var pendencias = new PendenciaService(_repo, parametros);

        return new PainelDirecaoService(
            _repo,
            _financeiro,
            contas,
            new RecebiveisService(_repo),
            new FechamentoCaixaService(_repo),
            new RodadaPendenciasService(_repo, pendencias, parametros),
            pendencias,
            new InadimplenciaService(_repo, _financeiro),
            _receitaGlosada,
            new MetaService(_repo, _financeiro, new IndicadoresService(_repo, parametros)),
            new OrcamentoService(_repo, contas),
            new ConsultorioService(_repo));
    }

    /// <summary>
    /// O circuito inteiro, nas quatro pontas: a Recepção fecha a sessão, o Faturamento
    /// registra a guia, o Financeiro lança a receita — e a DIREÇÃO vê o número, sem que
    /// ninguém tenha avisado ninguém.
    ///
    /// O painel **não calcula nada**: cada número vem do serviço dono dele. É por isso
    /// que ele é o teste certo para o fim do circuito — se um elo estiver partido, o
    /// número não some com erro, ele aparece ZERADO. Painel que mostra zero por defeito é
    /// indistinguível de painel que mostra zero porque o dia foi fraco.
    /// </summary>
    [Fact]
    public async Task O_dia_fechado_na_recepcao_chega_ao_painel_da_direcao()
    {
        var pacienteId = await CriarPacienteAsync();
        var agendamento = await MarcarAsync(pacienteId, new TimeOnly(9, 0));

        var resultado = await _fechamento.ConcluirAsync(
            new DecisaoFechamento(agendamento.Id, DebitarPacote: false));
        var codigo = resultado.Atendimento.Codigos.OrderBy(c => c.Ordem).First();

        await _faturamento.DarBaixaAsync(codigo.Id, Dia, "GUIA-004", "secretaria", null);
        await _repo.SalvarAsync();

        // A guia baixada e ainda sem receita é o que a direção precisa ver primeiro.
        var antes = await MontarPainel().MontarAsync(Dia);
        antes.GuiasSemReceita.Should().BeGreaterThan(0,
            "guia efetivada sem receita lançada é dinheiro que a clínica ainda não contou");

        var guia = (await _financeiro.GuiasSemLancamentoAsync(Dia.AddDays(-30), Dia.AddDays(30)))
            .Single(g => g.CodigoId == codigo.Id);
        await _financeiro.LancarReceitaDaGuiaAsync(
            guia, 150m, StatusLancamento.Realizado, operador: "financeiro");

        var depois = await MontarPainel().MontarAsync(Dia);

        depois.EntradasMes.Should().Be(150m,
            "a receita lançada no Financeiro é a mesma que a direção lê — mesmo banco, sem sincronização");
        depois.GuiasSemReceita.Should().Be(antes.GuiasSemReceita - 1);
    }

    /// <summary>
    /// A glosa também atravessa até a direção: o painel tem um alerta próprio para
    /// "receita que o convênio recusou e ainda está contada". Sem ele, a única pessoa que
    /// pode decidir recorrer descobriria o buraco no fechamento do mês.
    /// </summary>
    [Fact]
    public async Task Receita_glosada_aparece_como_alerta_proprio_no_painel()
    {
        var pacienteId = await CriarPacienteAsync();
        var agendamento = await MarcarAsync(pacienteId, new TimeOnly(9, 0));

        var resultado = await _fechamento.ConcluirAsync(
            new DecisaoFechamento(agendamento.Id, DebitarPacote: false));
        var codigo = resultado.Atendimento.Codigos.OrderBy(c => c.Ordem).First();

        await _faturamento.DarBaixaAsync(codigo.Id, Dia, "GUIA-005", "secretaria", null);
        await _repo.SalvarAsync();

        var guia = (await _financeiro.GuiasSemLancamentoAsync(Dia.AddDays(-30), Dia.AddDays(30)))
            .Single(g => g.CodigoId == codigo.Id);
        await _financeiro.LancarReceitaDaGuiaAsync(guia, 150m, StatusLancamento.Previsto);

        await _glosas.RegistrarAsync(codigo.Id, Dia, "Código não autorizado");
        await _repo.SalvarAsync();

        var painel = await MontarPainel().MontarAsync(Dia);

        painel.GuiasComReceitaGlosada.Should().Be(1);
        painel.ValorReceitaGlosada.Should().Be(150m,
            "receita fantasma é a pior espécie de número errado, porque tem cara de exato");
    }

    /// <summary>
    /// O painel abre numa base VAZIA — clínica que acabou de instalar o sistema.
    ///
    /// Parece trivial e é o caminho mais provável de quebrar: onze serviços consultados de
    /// uma vez, todos sem uma linha para ler. E é a primeira tela que o cliente vê.
    /// </summary>
    [Fact]
    public async Task Painel_abre_em_base_vazia_sem_estourar()
    {
        var painel = await MontarPainel().MontarAsync(Dia);

        painel.EntradasMes.Should().Be(0m);
        painel.GuiasSemReceita.Should().Be(0);
        painel.NaoVerificados.Should().BeEmpty(
            "base vazia não é falha de leitura — nenhum bloco pode se declarar não verificado");
    }

    // =====================================================================
    // Circuito 5 — o Consultório (parcela 36)
    // =====================================================================

    /// <summary>
    /// A sessão atendida sem evolução escrita sobe do Consultório até a DIREÇÃO.
    ///
    /// O elo aqui não é chamada de método: é o par (agendamento realizado, evolução com
    /// <c>AgendamentoId</c>). Se o atendimento parasse de gravar esse vínculo, nenhum
    /// teste de unidade falharia — o Consultório continuaria cobrando um registro já
    /// escrito, para sempre, e o painel da direção acusaria prontuário em aberto numa
    /// clínica que documenta tudo.
    ///
    /// É o mesmo defeito de <c>LancamentoFinanceiro.CodigoFaturamentoId</c>, do outro lado
    /// do sistema.
    /// </summary>
    [Fact]
    public async Task Sessao_sem_evolucao_chega_ao_painel_da_direcao()
    {
        var profissional = new Profissional { Nome = "Dra. Ana" };
        _db.Profissionais.Add(profissional);
        await _db.SaveChangesAsync();

        var paciente = await CriarPacienteAsync();

        // Uma sessão ONTEM, atendida: hoje ela já é dívida de registro.
        var ontem = Dia.AddDays(-1);
        var agendamento = new Agendamento
        {
            PacienteId = paciente,
            ProfissionalId = profissional.Id,
            DataHora = ontem.ToDateTime(new TimeOnly(9, 0)),
            ModalidadePrevista = ModalidadeAtendimento.AcupunturaSimples,
            Status = StatusAgendamento.Realizado
        };
        _db.Agendamentos.Add(agendamento);
        await _db.SaveChangesAsync();

        var painel = await MontarPainel().MontarAsync(Dia);

        painel.SessoesSemEvolucao.Should().Be(1,
            "a direção é a única que enxerga a clínica inteira");
        painel.Alertas.Should().Contain(a => a.Assunto == AssuntoDirecao.ProntuarioEmAberto);

        // ---- O profissional escreve a evolução, ligada ao HORÁRIO ----
        var prontuario = new ProntuarioService(_repo);
        await prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = paciente,
            ProfissionalId = profissional.Id,
            AgendamentoId = agendamento.Id,
            Data = ontem,
            EvaAntes = 8,
            EvaDepois = 4,
            Conduta = "IG4, VB20"
        }, "dra.ana");

        var depois = await MontarPainel().MontarAsync(Dia);

        depois.SessoesSemEvolucao.Should().Be(0,
            "a sessão sai da lista por TER evolução, não porque alguém a marcou");
        depois.Alertas.Should().NotContain(a => a.Assunto == AssuntoDirecao.ProntuarioEmAberto);
    }

    /// <summary>
    /// A guia já faturada sem evolução é OUTRO alerta — mais grave.
    ///
    /// A sessão sem registro é prontuário incompleto; a guia sem registro é uma cobrança
    /// sem o documento que a sustenta, e é ela que o convênio pede quando audita. O painel
    /// precisa distinguir as duas, senão a direção trata as duas do mesmo jeito.
    /// </summary>
    [Fact]
    public async Task Guia_faturada_sem_evolucao_sobe_como_perigo()
    {
        var paciente = await CriarPacienteAsync();
        var ontem = Dia.AddDays(-1);

        // O caminho real: a recepção confirma a presença e a guia nasce pelas regras do
        // convênio. É o mesmo fluxo do circuito 1 — aqui só não se escreve a evolução.
        var agendamento = await _agenda.AgendarAsync(
            paciente, ontem.ToDateTime(new TimeOnly(9, 0)),
            ModalidadeAtendimento.AcupunturaComEletro, null);
        await _agenda.ConfirmarPresencaAsync(agendamento.Id);

        var painel = await MontarPainel().MontarAsync(Dia);

        painel.SessoesSemEvolucaoComGuia.Should().Be(1);
        painel.Alertas.Should().Contain(a =>
            a.Assunto == AssuntoDirecao.ProntuarioEmAberto
            && a.Gravidade == GravidadeDirecao.Perigo);
    }

    /// <summary>
    /// As escalas aplicadas no Consultório saem no relatório que a RECEPÇÃO emite.
    ///
    /// É o elo mais silencioso da parcela 36: `AvaliacaoClinica` é gravada por um módulo
    /// e o papel é montado por outro. Sem esta ligação a escala viraria mais um dado com
    /// um leitor só — e o relatório de evolução, que é o que o paciente leva ao convênio,
    /// descreveria metade do tratamento.
    /// </summary>
    [Fact]
    public async Task Escala_aplicada_no_consultorio_sai_no_relatorio_da_recepcao()
    {
        var paciente = await CriarPacienteAsync();
        var prontuario = new ProntuarioService(_repo);
        var avaliacoes = new AvaliacaoClinicaService(_repo);

        await prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = paciente,
            Data = Dia,
            EvaAntes = 8,
            EvaDepois = 4,
            QueixaPrincipal = "lombalgia"
        }, "dra.ana");

        var gad7 = Clinica.Domain.Avaliacoes.RegistroInstrumentos.Obter("GAD7")!;
        await avaliacoes.AplicarAsync(
            paciente, gad7.Codigo,
            gad7.Itens.ToDictionary(i => i.Codigo, _ => 2),
            Dia, operador: "dra.ana");

        var documentos = new DocumentoClinicoService(
            _repo, prontuario, new ConsentimentoService(_repo));

        var relatorio = await documentos.EmitirRelatorioEvolucaoAsync(
            paciente, operador: "recepcao");

        relatorio.Corpo.Should().Contain("GAD-7", "a escala do consultório entra no papel");
        relatorio.Itens.Should().Contain(i => i.Descricao.Contains("GAD-7"));
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
