using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>O que este atendimento produziu, e que o estorno pode desfazer.</summary>
public sealed record PreviaDoEstorno(
    int AtendimentoId,
    string Numero,
    string Paciente,
    DateOnly Data,
    string Modalidade,
    int GuiasAnulaveis,
    int? AgendamentoId,
    decimal? EntradaNoCaixa,
    int? ConsumoPacoteId,
    int InsumosBaixados,
    DateOnly? ConsultaRenovadaEm,
    string? Impedimento)
{
    /// <summary>Pode estornar? Impedimento preenchido = o fato já saiu da clínica.</summary>
    public bool Pode => Impedimento is null;

    public bool TemCaixa => EntradaNoCaixa is not null;

    public bool TemPacote => ConsumoPacoteId is not null;

    public bool TemInsumo => InsumosBaixados > 0;

    /// <summary>
    /// A consulta do convênio renovada por esta sessão. É LISTADA e não desfeita — ver a
    /// razão em <see cref="EstornoAtendimentoService"/>.
    /// </summary>
    public bool TemConsultaRenovada => ConsultaRenovadaEm is not null;
}

/// <summary>O que a pessoa marcou na janela do estorno. As guias saem sempre.</summary>
public sealed record DecisaoDeEstorno(
    string Motivo,
    bool DesfazerCaixa = false,
    bool DevolverSessaoDoPacote = false,
    bool DevolverInsumoAoEstoque = false);

/// <summary>O que o estorno de fato desfez — para a tela contar de volta.</summary>
public sealed record ResultadoDoEstorno(
    int AtendimentoId,
    int GuiasAnuladas,
    bool CaixaDesfeito,
    bool SessaoDevolvida,
    int InsumosDevolvidos,
    int? AgendamentoLiberado,
    IReadOnlyList<string> Avisos);

/// <summary>
/// ESTORNAR UM ATENDIMENTO (parcela 94) — desfazer a sessão lançada por engano.
///
/// Por que não existia
/// -------------------
/// O <c>RemarcarAsync</c> recusa horário já realizado com a frase <i>"Estorne o atendimento
/// antes"</i> — e não havia estorno de atendimento nenhum no sistema. A instrução mandava
/// fazer uma coisa que não existe, e a saída que a recepção encontrava era o CANCELAR, que
/// não tem trava: apagava o carimbo de realizado e suspendia as guias abertas, avisando
/// (só avisando) sobre as já baixadas.
///
/// O motivo de não existir fica claro quando se lista o que o lançamento FAZ. São cinco
/// efeitos em três serviços, e só um é a guia:
/// <list type="number">
/// <item><c>Atendimento</c> + <c>CodigoFaturamento</c> (<c>MontarAsync</c>);</item>
/// <item>não conformidades do paciente REABERTAS (<c>PrepararPresencaAsync</c>);</item>
/// <item>consulta do convênio RENOVADA (<c>ConcluirPresencaAsync</c>);</item>
/// <item><c>Status = Realizado</c>, <c>RealizadoEm</c>, <c>ChegadaEm</c> (<c>ConfirmarNucleoAsync</c>);</item>
/// <item>pacote debitado, insumo baixado e entrada no caixa (<c>FechamentoSessaoService.ConcluirAsync</c>).</item>
/// </list>
/// Um estorno que desfizesse só a guia seria um meio-estorno com cara de completo.
///
/// As decisões da direção (set/2026)
/// ---------------------------------
/// <b>Pergunta item a item.</b> A janela lista o que aquele atendimento produziu e desfaz
/// só o que for marcado — porque o caso varia: às vezes o caixa do dia já foi conferido,
/// às vezes não. As GUIAS saem sempre: são a razão do estorno.
///
/// <b>Recusa quando o fato já saiu da clínica</b> — guia baixada, em lote TISS ou em não
/// conformidade. É a MESMA trava que o <c>AjustarAoRemarcarAsync</c> aplica à troca de
/// modalidade, e pela mesma razão: desfazer aqui deixaria o portal do convênio e o sistema
/// dizendo coisas diferentes. O caminho é estornar a baixa primeiro.
///
/// <b>Bit <c>LancarAtendimento</c></b>, e não um bit de chefia: com a recusa acima, o que
/// se pode desfazer é uma guia que ainda NÃO saiu da clínica — "corrigir o próprio erro na
/// hora", que é trabalho de balcão.
///
/// ⚠️ O que ele NÃO desfaz, e é decisão escrita
/// --------------------------------------------
/// <b>A consulta do convênio renovada.</b> Ela é LISTADA na prévia e não é revertida:
/// <see cref="StatusConsulta"/> não tem "cancelada", desfazer exigiria ressuscitar a
/// consulta anterior, e se uma receita já foi emitida sob a nova, invalidá-la
/// retroativamente quebra um documento clínico. O estrago de deixá-la é pequeno e
/// reversível à mão (a aba Consultas): o paciente fica com cobertura um pouco antes do
/// devido, sem guia e sem dinheiro envolvidos.
///
/// <b>As não conformidades reabertas.</b> Elas voltaram a ser pendência porque o paciente
/// APARECEU — e ele apareceu, mesmo que a sessão tenha sido lançada errado. Fechá-las de
/// volta esconderia uma cobrança legítima.
///
/// ⚠️ E ele NUNCA APAGA
/// --------------------
/// Nem o atendimento, nem os códigos. <c>OnDelete(SetNull)</c> transformaria um
/// <c>Remove</c> em horário ÓRFÃO — <c>Status = Realizado</c> com <c>AtendimentoId</c>
/// nulo, o estado dos três encaixes de 12/08/2026, que o kanban mostra como "Finalizado"
/// e o repasse exclui em silêncio. O atendimento fica, marcado
/// (<see cref="Atendimento.EstornadoEm"/>), e os códigos vão para
/// <see cref="StatusCodigo.NaoAplicavel"/> com a <see cref="MarcaEstorno"/>.
/// </summary>
public sealed class EstornoAtendimentoService
{
    /// <summary>
    /// Prefixo da observação das guias anuladas pelo estorno. Mesma forma da
    /// <c>AtendimentoService.MarcaSuspensao</c>, e pela mesma razão: é por ele que se
    /// reconhece, depois, POR QUE aquele código está inaplicável.
    /// </summary>
    public const string MarcaEstorno = "Atendimento estornado";

    private readonly IClinicaRepositorio _repo;
    private readonly PacoteService? _pacotes;
    private readonly FinanceiroService? _financeiro;
    private readonly EstoqueService? _estoque;

    public EstornoAtendimentoService(
        IClinicaRepositorio repo, PacoteService? pacotes = null,
        FinanceiroService? financeiro = null, EstoqueService? estoque = null)
    {
        _repo = repo;
        // Opcionais pela mesma escolha do ParametrosService no GlosaService: este serviço
        // tem de continuar construível sem o Financeiro montado. Sem eles, a prévia não
        // OFERECE o que não sabe desfazer — em vez de oferecer e falhar no clique.
        _pacotes = pacotes;
        _financeiro = financeiro;
        _estoque = estoque;
    }

    /// <summary>
    /// O que este atendimento produziu — a lista que a janela transforma em caixinhas.
    /// Leitura pura: não muta nada.
    /// </summary>
    public async Task<PreviaDoEstorno> PreverAsync(int atendimentoId, CancellationToken ct = default)
    {
        var atendimento = await _repo.ObterAtendimentoAsync(atendimentoId, ct)
            ?? throw new InvalidOperationException($"Atendimento {atendimentoId} não encontrado.");

        var impedimento = Impedimento(atendimento);

        // ⚠️ SEQUENCIAL, nunca Task.WhenAll: é o mesmo DbContext.
        var horario = await _repo.AgendamentoDoAtendimentoAsync(atendimentoId, ct);

        var lancamentos = await _repo.LancamentosDosAtendimentosAsync([atendimentoId], ct);
        var entradas = lancamentos
            .Where(l => l.Tipo == TipoLancamento.Entrada && l.Status != StatusLancamento.Cancelado)
            .ToList();
        // ⚠️ NÃO use `Sum(l => (decimal?)l.Valor)`: sobre sequência vazia ele devolve 0, e
        // não nulo — "sem entrada no caixa" viraria "entrada de R$ 0,00", e a janela
        // ofereceria a caixinha de desfazer um lançamento que não existe. Botão que não
        // faz nada é o defeito da parcela 41, aqui em forma de caixa de seleção.
        decimal? entrada = entradas.Count > 0 ? entradas.Sum(l => l.Valor) : null;

        var consumo = await _repo.ConsumoDoAtendimentoAsync(atendimentoId, ct);
        var movimentos = await _repo.MovimentosDoAtendimentoAsync(atendimentoId, ct);

        var consultas = await _repo.ConsultasDoPacienteAsync(atendimento.PacienteId, ct);
        var consultaRenovada = consultas
            .Where(c => c.DataEmissao == atendimento.Data)
            .Select(c => (DateOnly?)c.DataEmissao)
            .FirstOrDefault();

        return new PreviaDoEstorno(
            atendimento.Id,
            atendimento.Numero ?? $"#{atendimento.Id}",
            atendimento.Paciente?.Nome ?? $"Paciente {atendimento.PacienteId}",
            atendimento.Data,
            atendimento.ModalidadeCodigo is { } cod
                ? CatalogoModalidades.Nome(cod)
                : ModalidadeInfo.NomeExibicao(atendimento.Modalidade),
            GuiasAnulaveis: atendimento.Codigos.Count(c => c.Status == StatusCodigo.Aberto),
            horario?.Id,
            entrada,
            consumo?.Id,
            movimentos.Count,
            consultaRenovada,
            impedimento);
    }

    /// <summary>
    /// Estorna. As guias saem sempre; caixa, pacote e insumo só se a decisão pedir.
    ///
    /// Ordem: as mutações do NÚCLEO (guias, carimbo, horário) primeiro e num
    /// <c>SaveChanges</c> só — ou o atendimento está estornado e o horário livre, ou nada
    /// mudou. As reversões de fora (caixa, pacote, estoque) têm gravação própria e viram
    /// AVISO quando falham: uma agulha que não voltou ao estoque não pode desfazer um
    /// estorno já commitado, que é a mesma hierarquia do <c>ConcluirAsync</c>.
    /// </summary>
    public async Task<ResultadoDoEstorno> EstornarAsync(
        int atendimentoId, DecisaoDeEstorno decisao, string? operador = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(decisao);
        if (string.IsNullOrWhiteSpace(decisao.Motivo))
            throw new ArgumentException("Diga por que este atendimento está sendo estornado.");

        var atendimento = await _repo.ObterAtendimentoAsync(atendimentoId, ct)
            ?? throw new InvalidOperationException($"Atendimento {atendimentoId} não encontrado.");

        if (atendimento.Estornado)
            throw new InvalidOperationException(
                $"O atendimento nº {atendimento.Numero} já foi estornado em "
                + $"{atendimento.EstornadoEm:dd/MM/yyyy HH:mm}.");

        if (Impedimento(atendimento) is { } impedimento)
            throw new InvalidOperationException(impedimento);

        var motivo = decisao.Motivo.Trim();
        var quem = string.IsNullOrWhiteSpace(operador) ? "?" : operador.Trim();
        var avisos = new List<string>();

        // ===== O NÚCLEO, num commit só =====
        var anuladas = 0;
        foreach (var c in atendimento.Codigos.Where(c => c.Status == StatusCodigo.Aberto))
        {
            c.Status = StatusCodigo.NaoAplicavel;
            c.RegistrarObservacaoPendencia(
                $"{MarcaEstorno} em {DateTime.Now:dd/MM/yyyy} — {motivo}");
            anuladas++;
        }

        atendimento.EstornadoEm = DateTime.Now;
        atendimento.EstornadoPor = quem;
        atendimento.MotivoEstorno = motivo;
        // A sessão deixa de contar como realizada para BI, retenção e origem.
        atendimento.RealizadoEm = null;

        // O horário é SOLTO para poder ser relançado limpo. Sem soltar, o
        // `ConfirmarNucleoAsync` reaproveitaria este atendimento anulado e a sessão nova
        // nasceria sem guia faturável — em silêncio.
        var horario = await _repo.AgendamentoDoAtendimentoAsync(atendimentoId, ct);
        int? liberado = null;
        if (horario is not null)
        {
            horario.AtendimentoId = null;
            horario.Status = StatusAgendamento.Agendado;
            // Os carimbos da fila vão junto: um horário reaberto que guardasse a chegada
            // nasceria na raia "Na recepção" com o paciente em casa. É a lição das
            // parcelas 69 e 74 — todo carimbo novo entra NESTE bloco.
            horario.ChegadaEm = null;
            horario.ChamadoEm = null;
            horario.InicioAtendimentoEm = null;
            horario.FimAtendimentoEm = null;
            liberado = horario.Id;
        }

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = quem,
            Acao = "AtendimentoEstornado",
            Detalhe = $"Atendimento nº {atendimento.Numero} ({atendimento.Data:dd/MM/yyyy}) — "
                      + $"{anuladas} guia(s) anulada(s). Motivo: {motivo}",
            PacienteId = atendimento.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);

        // ===== AS REVERSÕES DE FORA — gravação própria, falha vira aviso =====
        var caixa = await DesfazerCaixaAsync(atendimento, decisao, quem, motivo, avisos, ct);
        var pacote = await DevolverSessaoAsync(atendimento, decisao, quem, motivo, avisos, ct);
        var insumos = await DevolverInsumosAsync(atendimento, decisao, quem, motivo, avisos, ct);

        return new ResultadoDoEstorno(
            atendimento.Id, anuladas, caixa, pacote, insumos, liberado, avisos);
    }

    /// <summary>
    /// A recusa: o fato já saiu da clínica. Mesma trava do <c>AjustarAoRemarcarAsync</c>.
    /// </summary>
    private static string? Impedimento(Atendimento atendimento)
    {
        var baixadas = atendimento.Codigos.Count(c => c.Baixado);
        var emLote = atendimento.Codigos.Count(c => c.LoteTissId is not null);
        var nc = atendimento.Codigos.Count(c => c.Status == StatusCodigo.NaoConformidade);

        if (baixadas == 0 && emLote == 0 && nc == 0) return null;

        var partes = new List<string>();
        if (baixadas > 0) partes.Add($"{baixadas} já baixada(s) no portal");
        if (emLote > 0) partes.Add($"{emLote} em lote TISS");
        if (nc > 0) partes.Add($"{nc} em não conformidade");

        return $"Este atendimento não pode ser estornado: há guia dele {string.Join(", ", partes)}. "
               + "O fato já saiu da clínica — estorne a baixa (ou resolva a não conformidade) "
               + "antes, senão o portal do convênio e o sistema passam a dizer coisas diferentes.";
    }

    private async Task<bool> DesfazerCaixaAsync(
        Atendimento atendimento, DecisaoDeEstorno decisao, string quem, string motivo,
        List<string> avisos, CancellationToken ct)
    {
        if (!decisao.DesfazerCaixa) return false;
        if (_financeiro is null)
        {
            avisos.Add("A entrada no caixa NÃO foi desfeita — o módulo Financeiro não está "
                       + "disponível nesta tela. Cancele o lançamento pelo Financeiro.");
            return false;
        }

        try
        {
            var lancamentos = await _repo.LancamentosDosAtendimentosAsync([atendimento.Id], ct);
            var alvos = lancamentos
                .Where(l => l.Tipo == TipoLancamento.Entrada && l.Status != StatusLancamento.Cancelado)
                .ToList();

            foreach (var l in alvos)
                await _financeiro.CancelarAsync(
                    l.Id, $"Atendimento nº {atendimento.Numero} estornado — {motivo}", quem, ct);

            if (alvos.Count == 0)
            {
                avisos.Add("Não havia entrada no caixa para desfazer.");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Estorno do atendimento — caixa não pôde ser desfeito", ex);
            avisos.Add("A entrada no caixa NÃO pôde ser desfeita: " + ex.Message
                       + " O estorno das guias está gravado; resolva o caixa pelo Financeiro.");
            return false;
        }
    }

    private async Task<bool> DevolverSessaoAsync(
        Atendimento atendimento, DecisaoDeEstorno decisao, string quem, string motivo,
        List<string> avisos, CancellationToken ct)
    {
        if (!decisao.DevolverSessaoDoPacote) return false;
        if (_pacotes is null)
        {
            avisos.Add("A sessão do pacote NÃO foi devolvida — o módulo de pacotes não está "
                       + "disponível nesta tela.");
            return false;
        }

        try
        {
            var consumo = await _repo.ConsumoDoAtendimentoAsync(atendimento.Id, ct);
            if (consumo is null)
            {
                avisos.Add("Não havia sessão de pacote debitada para devolver.");
                return false;
            }

            await _pacotes.CancelarConsumoAsync(
                consumo.Id, $"Atendimento nº {atendimento.Numero} estornado — {motivo}", quem, ct);
            return true;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Estorno do atendimento — sessão do pacote não pôde voltar", ex);
            avisos.Add("A sessão do pacote NÃO pôde ser devolvida: " + ex.Message
                       + " Devolva pela tela de Pacotes.");
            return false;
        }
    }

    /// <summary>
    /// O insumo volta por ENTRADA compensatória, não por exclusão do movimento: extrato de
    /// estoque não se apaga — é ele que explica o saldo, e um movimento sumido deixa a
    /// conta sem resposta.
    /// </summary>
    private async Task<int> DevolverInsumosAsync(
        Atendimento atendimento, DecisaoDeEstorno decisao, string quem, string motivo,
        List<string> avisos, CancellationToken ct)
    {
        if (!decisao.DevolverInsumoAoEstoque) return 0;
        if (_estoque is null)
        {
            avisos.Add("Os insumos NÃO voltaram ao estoque — o módulo de estoque não está "
                       + "disponível nesta tela.");
            return 0;
        }

        try
        {
            var movimentos = await _repo.MovimentosDoAtendimentoAsync(atendimento.Id, ct);
            var saidas = movimentos.Where(m => m.Tipo == TipoMovimentoEstoque.Saida).ToList();
            if (saidas.Count == 0)
            {
                avisos.Add("Não havia insumo baixado para devolver.");
                return 0;
            }

            var devolvidos = 0;
            foreach (var m in saidas)
            {
                await _estoque.EntrarAsync(
                    m.ItemEstoqueId, m.Quantidade,
                    observacao: $"Devolução — atendimento nº {atendimento.Numero} estornado: {motivo}",
                    operador: quem, ct: ct);
                devolvidos++;
            }

            return devolvidos;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Estorno do atendimento — insumo não pôde voltar ao estoque", ex);
            avisos.Add("Os insumos NÃO puderam voltar ao estoque: " + ex.Message
                       + " Registre a entrada pela tela de Estoque.");
            return 0;
        }
    }
}
