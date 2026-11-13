using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>O que a secretária informou no bloco "Pagamento" do formulário de marcar.</summary>
public sealed record PagamentoNaMarcacao(
    decimal Valor, FormaPagamento Forma, string? Adquirente = null, string? Bandeira = null, int Parcelas = 1);

/// <summary>
/// RECEBER AO MARCAR (nov/2026). A direção: <i>"o paciente chega no balcão, marca a consulta, a
/// secretária passa o valor e é cobrado ali na hora"</i>. O sistema mostrava o preço no
/// formulário de marcar e não tinha onde registrar que foi pago — o dinheiro só entrava dias
/// depois, redigitado na aba Particulares da Conciliação, com "recebido ou a receber" perguntado
/// sobre um pagamento que já tinha acontecido.
///
/// O pagamento entra no caixa COMO RECEBIDO, hoje, preso ao HORÁRIO
/// (<see cref="LancamentoFinanceiro.AgendamentoId"/>): o atendimento ainda não existe. A
/// competência é o dia da sessão; a data do pagamento é hoje. Quando o atendimento nasce,
/// <c>AgendaService</c> amarra os dois no mesmo commit, e a sessão não aparece na Conciliação.
///
/// A FORMA é informada, nunca presumida: sem ela o cartão não gera taxa nem recebível e o
/// extrato não concilia. É a única coisa que o sistema não tem como saber sozinho.
///
/// Só sessão PARTICULAR: o dinheiro do convênio entra pela guia.
/// </summary>
public sealed class PagamentoNaMarcacaoService
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly IClinicaRepositorio _repo;
    private readonly FinanceiroService _financeiro;

    public PagamentoNaMarcacaoService(IClinicaRepositorio repo, FinanceiroService financeiro)
    {
        _repo = repo;
        _financeiro = financeiro;
    }

    /// <summary>Quanto já foi pago por este horário (0 = nada). É o que a edição mostra e o que impede a segunda cobrança.</summary>
    public async Task<decimal> PagoPeloHorarioAsync(int agendamentoId, CancellationToken ct = default)
        => (await _repo.EntradasDoHorarioAsync(agendamentoId, ct)).Sum(l => l.Valor);

    public async Task<LancamentoFinanceiro> ReceberAsync(
        int agendamentoId, PagamentoNaMarcacao pagamento, string? operador = null,
        DateOnly? hoje = null, CancellationToken ct = default)
    {
        var ag = await _repo.ObterAgendamentoAsync(agendamentoId, ct)
            ?? throw new InvalidOperationException($"Agendamento {agendamentoId} não encontrado.");
        if (!SessaoParticular.Eh(ag))
            throw new InvalidOperationException(
                "Só a sessão particular é paga no balcão ao marcar — o dinheiro do convênio entra pela guia.");
        if (ag.Status is StatusAgendamento.Cancelado or StatusAgendamento.Faltou)
            throw new InvalidOperationException("Este horário não está mais marcado — reabra-o antes de receber.");
        if (pagamento.Valor <= 0m)
            throw new InvalidOperationException("Informe o valor cobrado do paciente.");
        // Dois cliques, ou duas máquinas: um horário se paga uma vez.
        if ((await _repo.EntradasDoHorarioAsync(agendamentoId, ct)).Count > 0
            || (ag.AtendimentoId is { } jaTem && await _repo.ExisteEntradaVivaAsync(null, jaTem, ct)))
            throw new InvalidOperationException("Este horário já tem pagamento registrado.");

        var dia = hoje ?? DateOnly.FromDateTime(DateTime.Today);
        var deducoes = await new TaxaService(_repo).CalcularPagamentoPacienteAsync(
            pagamento.Valor, dia, pagamento.Forma, pagamento.Adquirente, pagamento.Bandeira, pagamento.Parcelas, ct);

        var sessao = DateOnly.FromDateTime(ag.DataHora);
        return await _financeiro.LancarAsync(
            // Competência = o dia da SESSÃO; o caixa recebe HOJE (pode ser antes dela).
            sessao,
            TipoLancamento.Entrada,
            FinanceiroService.DescricaoDaSessao(sessao,
                CatalogoModalidades.NomeComEspecialidade(ag.ModalidadeCodigo, ag.ModalidadePrevista, ag.EspecialidadeConsultaCodigo),
                ag.Paciente?.Nome ?? "(paciente removido)"),
            pagamento.Valor,
            formaPagamento: pagamento.Forma,
            pacienteId: ag.PacienteId,
            // Com a chave "guia no agendamento" o atendimento já nasceu junto do horário.
            atendimentoId: ag.AtendimentoId,
            dataPagamento: dia,
            operador: operador,
            deducoes: deducoes,
            adquirente: pagamento.Adquirente,
            bandeira: pagamento.Bandeira,
            modalidadeCartao: TaxaService.ModalidadeDe(pagamento.Forma, pagamento.Parcelas),
            parcelas: TaxaService.ModalidadeDe(pagamento.Forma) is null ? null : pagamento.Parcelas,
            ct: ct,
            agendamentoId: ag.Id);
    }

    /// <summary>O recado de quem cancela (ou marca falta em) um horário já pago. O sistema avisa; devolver é de gente.</summary>
    public static string AvisoDeHorarioPago(decimal valor, StatusAgendamento novo)
        => $"Este horário foi PAGO ao marcar ({valor.ToString("C2", PtBr)}). "
           + (novo == StatusAgendamento.Faltou ? "A falta" : "O cancelamento")
           + " não devolve o dinheiro: se for remarcar, reabra este mesmo horário e o pagamento o acompanha; "
           + "se for devolver, o Financeiro cancela o lançamento.";

    // ---------------------------------------------------------------- a tela

    /// <summary>As formas que o paciente usa no balcão (Convênio não é forma de pagamento dele).</summary>
    public static IReadOnlyList<FormaPagamento> Formas { get; } =
        Enum.GetValues<FormaPagamento>().Where(f => f != FormaPagamento.Convenio).ToList();

    public static bool EhCartao(FormaPagamento? forma) => forma is FormaPagamento.CartaoCredito or FormaPagamento.CartaoDebito;

    public static string ParaCampo(decimal? valor) => valor?.ToString("0.00", PtBr) ?? string.Empty;

    /// <summary>
    /// Confere o que foi digitado ANTES de o horário ser gravado — recusa depois de marcar
    /// deixaria o horário marcado e a secretária sem saber se cobrou.
    /// </summary>
    public static PagamentoNaMarcacao? Conferir(
        string? valor, FormaPagamento? forma, string? adquirente, string? bandeira, string? parcelas, out string? erro)
    {
        erro = null;
        if (!decimal.TryParse(valor?.Trim(), NumberStyles.Number, PtBr, out var v) || v <= 0m || decimal.Round(v, 2) != v)
        {
            erro = "Informe o valor cobrado do paciente, em reais e centavos — ou desmarque \"Recebido agora\".";
            return null;
        }
        if (forma is not { } f || f == FormaPagamento.Convenio)
        {
            erro = "Escolha como o paciente pagou (PIX, dinheiro, cartão) — ou desmarque \"Recebido agora\" se ele não pagou neste momento.";
            return null;
        }
        var n = 1;
        if (EhCartao(f))
        {
            if (string.IsNullOrWhiteSpace(adquirente) || string.IsNullOrWhiteSpace(bandeira))
            {
                erro = "Informe a maquininha e a bandeira do cartão.";
                return null;
            }
            if (f == FormaPagamento.CartaoCredito
                && (!int.TryParse(string.IsNullOrWhiteSpace(parcelas) ? "1" : parcelas.Trim(), out n) || n is < 1 or > 36))
            {
                erro = "Informe de 1 a 36 parcelas.";
                return null;
            }
        }
        return new PagamentoNaMarcacao(v, f,
            EhCartao(f) ? adquirente!.Trim() : null, EhCartao(f) ? bandeira!.Trim() : null, n);
    }

    public static string Recebido(decimal valor, FormaPagamento forma)
        => $"Pagamento de {valor.ToString("C2", PtBr)} ({RotulosEnum.De(forma)}) registrado no caixa.";

    public static string NaoRecebido(string motivo)
        => $"O horário foi MARCADO, mas o pagamento NÃO entrou no caixa: {motivo} Registre-o em Pagamentos ou na Conciliação.";

    public static string JaPago(decimal valor) => $"Pago ao marcar: {valor.ToString("C2", PtBr)}.";
}
