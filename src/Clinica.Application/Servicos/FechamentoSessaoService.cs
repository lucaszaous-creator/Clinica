using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Um insumo sugerido para a baixa do fechamento, com o saldo que existe hoje.</summary>
public sealed record InsumoSugerido(
    int ItemId,
    string Nome,
    string Unidade,
    decimal Quantidade,
    decimal Saldo)
{
    /// <summary>Sugestão maior que o saldo — a tela avisa antes de a baixa ser recusada.</summary>
    public bool SemSaldo => Quantidade > Saldo;
}

/// <summary>
/// O que vai acontecer quando a sessão for concluída. É PROPOSTA: nada aqui foi gravado.
///
/// A recepção confirma na tela, e é de propósito que ela veja tudo de uma vez — pacote,
/// insumo e dinheiro saem do mesmo ato (o paciente que acabou de ser atendido), e pedir
/// três telas diferentes para o mesmo ato é como o débito do pacote deixava de acontecer.
/// </summary>
public sealed record PropostaFechamento(
    int AgendamentoId,
    int PacienteId,
    string Paciente,
    DateOnly Data,
    SaldoPacote? PacoteADebitar,
    decimal? ValorSugerido,
    string? ProcedenciaDoValor,
    FormaPagamento? FormaSugerida,
    bool SugereLancamento,
    IReadOnlyList<InsumoSugerido> Insumos)
{
    /// <summary>Há pacote com saldo utilizável hoje.</summary>
    public bool TemPacote => PacoteADebitar is not null;
}

/// <summary>Quantidade decidida para um insumo (o que a recepção confirmou na tela).</summary>
public sealed record InsumoAConsumir(int ItemId, decimal Quantidade);

/// <summary>
/// O que a recepção confirmou. Tudo é opt-in: o que ela não marcar não acontece.
/// </summary>
public sealed record DecisaoFechamento(
    int AgendamentoId,
    bool DebitarPacote = true,
    bool GerarLancamento = false,
    decimal? Valor = null,
    FormaPagamento? Forma = null,
    int? CategoriaId = null,
    IReadOnlyList<InsumoAConsumir>? Insumos = null);

/// <summary>
/// O que de fato aconteceu. Os <see cref="Avisos"/> são a parte que não pode ser
/// escondida: o atendimento pode ter sido concluído e o pacote não ter sido debitado, e
/// mostrar isso como sucesso mandaria a secretária embora achando que o saldo baixou.
/// </summary>
public sealed record ResultadoFechamento(
    Atendimento Atendimento,
    ConsumoPacote? Consumo,
    LancamentoFinanceiro? Lancamento,
    IReadOnlyList<MovimentoEstoque> Movimentos,
    IReadOnlyList<string> Avisos)
{
    /// <summary>
    /// O que o LANÇAMENTO tem a dizer — e que não é falha (parcela 62).
    ///
    /// Vem de <c>ResultadoLancamento.Avisos</c>: a NÃO CONFORMIDADE reaberta porque o
    /// paciente voltou ("cobre a guia agora, com ele na frente") e os recados das regras
    /// do convênio. O <see cref="ConcluirAsync"/> recebia esses avisos e <b>abria uma
    /// lista nova vazia</b>, então eles morriam aqui: só chegavam à tela pelo caminho do
    /// atendimento avulso, que monta a própria prévia. Pelo Finalizar da Fila — o caminho
    /// que a clínica usa todo dia — nunca chegaram.
    ///
    /// Ficam SEPARADOS de <see cref="Avisos"/> de propósito: aquele é o que FALHOU (e
    /// segura a janela aberta), este é o que ACONTECEU e a pessoa precisa saber. Somá-los
    /// faria um fechamento perfeito com uma NC reaberta aparecer como fechamento com erro,
    /// e três dias depois ninguém mais lê o aviso.
    /// </summary>
    public IReadOnlyList<string> RecadosDoLancamento { get; init; } = [];

    public bool TudoCerto => Avisos.Count == 0;

    /// <summary>Há recado do lançamento para mostrar depois de concluir.</summary>
    public bool TemRecados => RecadosDoLancamento.Count > 0;
}

/// <summary>
/// O atendimento acabou de ser REGISTRADO: ele existe, as guias existem e já estão no
/// faturamento. O que vem junto é a proposta do que ainda pode ser decidido.
///
/// Por que este record existe (parcela 65)
/// ---------------------------------------
/// Até aqui a guia só nascia quando a janela de fechamento era CONFIRMADA. Quem fechasse
/// a janela ficava com um horário na agenda, o paciente marcado como presente e
/// <b>nenhuma guia</b> — e como o encaixe tinha sido criado, a tela parecia ter
/// funcionado pela metade. No dia 12/08/2026 a mesma sessão foi lançada três vezes em 71
/// segundos por causa disso: a recepcionista não via guia nenhuma e tentava de novo.
///
/// A regra agora é uma só, e vale para as DUAS portas (avulso e Finalizar da fila):
/// <b>registrou o atendimento, a guia nasce e vai para o faturamento</b>. Pacote, insumo
/// e caixa são o passo seguinte e não podem condicionar nem desfazer a guia — que é a
/// mesma hierarquia que o <see cref="FechamentoSessaoService.ConcluirAsync"/> já aplicava
/// entre os quatro fatos, agora estendida ao momento em que cada um acontece.
/// </summary>
public sealed record RegistroAtendimento(
    Atendimento Atendimento,
    PropostaFechamento Proposta,
    IReadOnlyList<string> RecadosDoLancamento,
    bool JaExistia)
{
    /// <summary>
    /// Há algo para a recepção decidir — pacote a debitar, dinheiro a lançar ou insumo a
    /// baixar. Quando não há, abrir a janela seria pedir confirmação de uma tela vazia, e
    /// o lançamento termina em um clique.
    /// </summary>
    public bool TemDecisao =>
        Proposta.TemPacote || Proposta.SugereLancamento || Proposta.Insumos.Count > 0;

    /// <summary>Quantas guias faturáveis nasceram (o Particular gera código não aplicável).</summary>
    public int GuiasGeradas =>
        Atendimento.Codigos.Count(c => c.Status != StatusCodigo.NaoAplicavel);
}

/// <summary>
/// Fecha a sessão na RECEPÇÃO: conclui o atendimento e, no mesmo ato, debita o pacote,
/// baixa o insumo e lança o dinheiro no caixa.
///
/// Este serviço existe porque cada uma dessas quatro coisas já estava construída e
/// TESTADA, e nenhuma delas era chamada: <c>PacoteService.ConsumirPorAtendimentoAsync</c>
/// e <c>EstoqueService.BaixarAsync</c> não tinham um único chamador em produção, e a
/// Recepção não conhecia o <c>FinanceiroService</c>. Na prática o balcão concluía a
/// sessão e o pacote do paciente não baixava — o saldo só ficava certo se alguém
/// lembrasse de abrir o módulo Financeiro e debitar à mão.
///
/// Ele NÃO entra no <c>AtendimentoService</c> de propósito: aquele é compartilhado com o
/// app de faturamento, que está congelado e nada tem com pacote, insumo ou caixa. Dar-lhe
/// efeito colateral novo mudaria o comportamento de um app em produção. A ponte fica
/// aqui, e quem a atravessa é a Recepção.
///
/// A proposta é confirmada, nunca automática: no balcão o valor errado gravado sozinho
/// vira um caixa que não bate, e o erro só aparece no fechamento do mês.
/// </summary>
public sealed class FechamentoSessaoService
{
    private readonly IClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly PacoteService _pacotes;
    private readonly EstoqueService _estoque;
    private readonly FinanceiroService _financeiro;

    public FechamentoSessaoService(
        IClinicaRepositorio repo,
        AgendaService agenda,
        PacoteService pacotes,
        EstoqueService estoque,
        FinanceiroService financeiro)
    {
        _repo = repo;
        _agenda = agenda;
        _pacotes = pacotes;
        _estoque = estoque;
        _financeiro = financeiro;
    }

    /// <summary>
    /// Monta a proposta sem gravar nada. Falha de uma das partes não derruba a proposta
    /// inteira — a conclusão do atendimento é o que importa, e o resto é sugestão.
    /// </summary>
    public async Task<PropostaFechamento> PrepararAsync(
        int agendamentoId, DateOnly? hoje = null, CancellationToken ct = default)
    {
        var ag = await _repo.ObterAgendamentoAsync(agendamentoId, ct)
            ?? throw new InvalidOperationException($"Agendamento {agendamentoId} não encontrado.");

        // ⚠️ Agendamento JÁ REALIZADO não é mais erro aqui (parcela 65).
        //
        // A guia passou a nascer no instante em que o atendimento é registrado, e a
        // proposta de pacote/insumo/caixa é o passo SEGUINTE — então quando esta tela
        // abre, o atendimento já existe e o agendamento já está `Realizado`. Recusar
        // aqui derrubaria justamente a janela que veio depois da guia.
        //
        // O que impede a duplicidade não é esta recusa: é o <see cref="ConcluirAsync"/>,
        // que reaproveita o atendimento existente em vez de confirmar a presença de novo,
        // e o `AtendimentoJaConsumiuPacoteAsync` do PacoteService.
        var dia = hoje ?? DateOnly.FromDateTime(ag.DataHora);

        var pacote = await PacoteADebitarAsync(ag.PacienteId, dia, ct);
        var (valor, procedencia, forma) = await ValorSugeridoAsync(ag.PacienteId, ct);
        var insumos = await InsumosSugeridosAsync(ct);

        return new PropostaFechamento(
            AgendamentoId: ag.Id,
            PacienteId: ag.PacienteId,
            Paciente: ag.Paciente?.Nome ?? "(paciente removido)",
            Data: dia,
            PacoteADebitar: pacote,
            ValorSugerido: valor,
            ProcedenciaDoValor: procedencia,
            FormaSugerida: forma,
            // Sugere cobrar só quando há evidência de que este paciente paga no balcão
            // (já pagou antes) e não há pacote para debitar — pacote é sessão comprada,
            // e cobrar de novo seria cobrar duas vezes. Paciente de convênio nunca cai
            // aqui na primeira vez: o dinheiro dele entra pela conciliação da guia, e um
            // lançamento marcado por padrão viraria receita fantasma no caixa.
            SugereLancamento: pacote is null && valor is not null,
            Insumos: insumos);
    }

    /// <summary>
    /// REGISTRA o atendimento: a guia nasce aqui e vai para o faturamento na hora.
    ///
    /// Este é o ponto que a direção fixou na parcela 65: <b>atendimento que entrou no
    /// sistema já gera guia</b>, agendado ou avulso, sem depender de mais nenhum clique.
    /// Antes, o único caminho para gerar a guia era confirmar a janela de fechamento — e
    /// fechar aquela janela deixava a sessão registrada na agenda e invisível para quem
    /// fatura.
    ///
    /// A proposta de pacote/insumo/caixa é montada ANTES de confirmar a presença, porque
    /// ela descreve o que ainda pode ser decidido e a tela precisa dela para saber se vale
    /// a pena abrir (<see cref="RegistroAtendimento.TemDecisao"/>).
    ///
    /// ⚠️ É IDEMPOTENTE por agendamento. Chamar duas vezes devolve o MESMO atendimento em
    /// vez de estourar — dois cliques no Finalizar não podem virar duas guias para a mesma
    /// sessão. A duplicidade que sobra é a de agendamentos distintos para a mesma sessão, e
    /// essa se impede na porta (a tela pergunta antes de criar o segundo encaixe).
    /// </summary>
    public async Task<RegistroAtendimento> RegistrarAtendimentoAsync(
        int agendamentoId, string? operador = null, DateOnly? hoje = null,
        CancellationToken ct = default)
    {
        var proposta = await PrepararAsync(agendamentoId, hoje, ct);
        var (atendimento, recados, jaExistia) =
            await GarantirAtendimentoAsync(agendamentoId, operador, ct);

        return new RegistroAtendimento(atendimento, proposta, recados, jaExistia);
    }

    /// <summary>
    /// Devolve o atendimento do agendamento, confirmando a presença se ainda não foi
    /// confirmada. É o ponto único que impede a segunda guia: presença JÁ CONFIRMADA é
    /// reaproveitada, nunca reconfirmada.
    ///
    /// ⚠️ O reaproveitamento olha o STATUS, não só o <c>AtendimentoId</c> (parcela 70):
    /// no regime "guia no agendamento", o horário marcado já tem atendimento e guias —
    /// mas a presença ainda não aconteceu. Reaproveitar por ter AtendimentoId pularia a
    /// confirmação para sempre, e o Concluir da Fila deixaria de carimbar a sessão.
    /// </summary>
    private async Task<(Atendimento Atendimento, IReadOnlyList<string> Recados, bool JaExistia)>
        GarantirAtendimentoAsync(int agendamentoId, string? operador, CancellationToken ct)
    {
        var ag = await _repo.ObterAgendamentoAsync(agendamentoId, ct)
            ?? throw new InvalidOperationException($"Agendamento {agendamentoId} não encontrado.");

        if (ag.Status == StatusAgendamento.Realizado && ag.AtendimentoId is { } id)
        {
            var existente = await _repo.ObterAtendimentoAsync(id, ct)
                ?? throw new InvalidOperationException(
                    $"O agendamento {agendamentoId} aponta para o atendimento {id}, que não existe mais.");

            return (existente, [], true);
        }

        var resultado = await _agenda.ConfirmarPresencaAsync(agendamentoId, ct, operador);
        return (resultado.Atendimento, resultado.Avisos, false);
    }

    /// <summary>
    /// Executa o fechamento. A ordem importa: o atendimento nasce primeiro porque pacote,
    /// insumo e lançamento se penduram nele.
    ///
    /// Só a conclusão do atendimento derruba a operação; as outras três são registradas
    /// como aviso quando falham. A alternativa seria desfazer o atendimento porque o
    /// estoque de uma agulha não bateu — e o atendimento aconteceu de verdade, o paciente
    /// foi embora, a guia precisa existir.
    ///
    /// Desde a parcela 65 o atendimento normalmente JÁ EXISTE quando este método é
    /// chamado (a guia nasce no registro, não aqui). O <see cref="GarantirAtendimentoAsync"/>
    /// reaproveita o que existe e mantém o método funcionando também para quem o chama
    /// direto, sem ter passado pelo registro.
    /// </summary>
    public async Task<ResultadoFechamento> ConcluirAsync(
        DecisaoFechamento decisao, string? operador = null, CancellationToken ct = default)
    {
        var (atendimento, recadosDoLancamento, _) =
            await GarantirAtendimentoAsync(decisao.AgendamentoId, operador, ct);

        var avisos = new List<string>();
        ConsumoPacote? consumo = null;
        LancamentoFinanceiro? lancamento = null;
        var movimentos = new List<MovimentoEstoque>();

        if (decisao.DebitarPacote)
        {
            try
            {
                consumo = await _pacotes.ConsumirPorAtendimentoAsync(
                    atendimento.PacienteId, atendimento.Id, atendimento.Data,
                    decisao.AgendamentoId, operador, ct);
            }
            catch (Exception ex)
            {
                Diagnostico.Registrar("Fechamento da sessão — pacote não pôde ser debitado", ex);
                avisos.Add($"O pacote NÃO foi debitado: {ex.Message}");
            }
        }

        foreach (var insumo in decisao.Insumos ?? [])
        {
            if (insumo.Quantidade <= 0) continue;

            try
            {
                movimentos.Add(await _estoque.BaixarAsync(
                    insumo.ItemId, insumo.Quantidade, atendimento.Id, atendimento.PacienteId,
                    atendimento.Data, observacao: "Consumo da sessão", operador: operador, ct: ct));
            }
            catch (Exception ex)
            {
                Diagnostico.Registrar("Fechamento da sessão — insumo não pôde ser baixado", ex);
                avisos.Add($"O insumo do item {insumo.ItemId} NÃO baixou do estoque: {ex.Message}");
            }
        }

        if (decisao.GerarLancamento)
        {
            try
            {
                if (decisao.Valor is not { } valor || valor <= 0)
                    throw new InvalidOperationException(
                        "Sem valor não há o que lançar no caixa.");

                lancamento = await _financeiro.LancarAsync(
                    atendimento.Data,
                    TipoLancamento.Entrada,
                    $"Sessão de {atendimento.Data:dd/MM/yyyy}",
                    valor,
                    formaPagamento: decisao.Forma,
                    categoriaId: decisao.CategoriaId,
                    pacienteId: atendimento.PacienteId,
                    atendimentoId: atendimento.Id,
                    operador: operador,
                    ct: ct);
            }
            catch (Exception ex)
            {
                Diagnostico.Registrar("Fechamento da sessão — lançamento não pôde ser gravado", ex);
                avisos.Add($"O dinheiro NÃO entrou no caixa: {ex.Message}");
            }
        }

        // Os avisos do LANÇAMENTO viajam junto (parcela 62) — inclusive a NC reaberta
        // pelo retorno do paciente, que é o recado que a secretária tem de ver enquanto
        // ele ainda está no balcão.
        return new ResultadoFechamento(atendimento, consumo, lancamento, movimentos, avisos)
        {
            RecadosDoLancamento = recadosDoLancamento
        };
    }

    /// <summary>
    /// Qual pacote seria debitado — a MESMA escolha do
    /// <see cref="PacoteService.ConsumirPorAtendimentoAsync"/> (o que vence primeiro),
    /// para a tela não prometer um pacote e o serviço debitar outro.
    /// </summary>
    private async Task<SaldoPacote?> PacoteADebitarAsync(
        int pacienteId, DateOnly dia, CancellationToken ct)
    {
        try
        {
            var pacotes = await _repo.PacotesDoPacienteAsync(pacienteId, ct);
            var escolhido = pacotes
                .Where(p => p.PodeConsumir(dia))
                .OrderBy(p => p.ValidoAte ?? DateOnly.MaxValue)
                .ThenBy(p => p.DataCompra)
                .ThenBy(p => p.Id)
                .Select(p => p.Id)
                .FirstOrDefault();

            if (escolhido == 0) return null;

            var saldos = await _pacotes.DoPacienteAsync(pacienteId, dia, ct);
            return saldos.FirstOrDefault(s => s.PacoteId == escolhido);
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Fechamento da sessão — pacotes do paciente não puderam ser lidos", ex);
            return null;
        }
    }

    /// <summary>
    /// Valor sugerido: o da última entrada realizada do paciente, com a data de onde
    /// saiu. Sem histórico, o campo vai vazio e a recepção digita — inventar um preço
    /// seria pior do que perguntar.
    /// </summary>
    private async Task<(decimal?, string?, FormaPagamento?)> ValorSugeridoAsync(
        int pacienteId, CancellationToken ct)
    {
        try
        {
            var ultimo = await _repo.UltimoRecebimentoDoPacienteAsync(pacienteId, ct);
            return ultimo is null
                ? (null, null, null)
                : (ultimo.Valor, $"igual à sessão de {ultimo.Data:dd/MM/yyyy}", ultimo.FormaPagamento);
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Fechamento da sessão — valor sugerido não pôde ser lido", ex);
            return (null, null, null);
        }
    }

    /// <summary>
    /// Insumos sugeridos: o que a última sessão gastou, com o saldo de hoje ao lado.
    /// Item inativo ou já apagado sai da lista — sugerir o que não se pode baixar só
    /// gera erro na confirmação.
    /// </summary>
    private async Task<IReadOnlyList<InsumoSugerido>> InsumosSugeridosAsync(CancellationToken ct)
    {
        try
        {
            var ultimos = await _repo.UltimoConsumoDeSessaoAsync(ct);
            if (ultimos.Count == 0) return [];

            var saldos = await _estoque.SaldosAsync(somenteAtivos: true, ct: ct);
            var porId = saldos.ToDictionary(s => s.ItemId);

            return ultimos
                .Where(m => porId.ContainsKey(m.ItemEstoqueId))
                // O mesmo item pode ter saído em dois movimentos na mesma sessão.
                .GroupBy(m => m.ItemEstoqueId)
                .Select(g =>
                {
                    var saldo = porId[g.Key];
                    return new InsumoSugerido(
                        g.Key, saldo.Nome, saldo.Unidade, g.Sum(m => m.Quantidade), saldo.Saldo);
                })
                .OrderBy(i => i.Nome)
                .ToList();
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Fechamento da sessão — sugestão de insumo não pôde ser montada", ex);
            return [];
        }
    }
}
