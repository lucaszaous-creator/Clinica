using System.Globalization;
using Clinica.Application.Formatacao;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Uma linha do CATÁLOGO de pacotes, já escrita para a tela — e com os VALORES que o
/// formulário de edição carrega e devolve. Ordem e observações não têm campo no formulário e
/// viajam para serem PRESERVADOS: o serviço copia campo a campo, e o que chegar nulo é
/// apagado na primeira edição (lugares 3 e 6 da lista de conferência).
/// </summary>
public sealed record LinhaDeCatalogoPacote(
    int Id,
    string Nome,
    string Resumo,
    string Valor,
    bool Ativo,
    TipoPacote Tipo,
    int? Sessoes,
    decimal ValorDecimal,
    int? ValidadeDias,
    int Ordem,
    string? Observacoes);

/// <summary>Um pacote vendido, com o saldo e a situação de pagamento já escritos.</summary>
public sealed record LinhaDePacoteVendido(
    int Id,
    int PacienteId,
    string Paciente,
    string Nome,
    string Saldo,
    string Situacao,
    string Valor,
    string Compra,
    bool Ativo,
    /// <summary>"pago" · "R$ 400,00 a receber (2 parcelas, 1 vencida)" · "sem lançamento no caixa".</summary>
    string Pagamento,
    /// <summary>Há parcela vencida: o selo do pagamento sai em aviso, não em neutro.</summary>
    bool PagamentoAtrasado,
    /// <summary>Sessões ainda no saldo (nulo = plano livre) — o resumo soma as dos ATIVOS.</summary>
    int? SaldoSessoes);

/// <summary>Uma sessão já debitada do pacote, como a lista do formulário de sessões a mostra.</summary>
public sealed record LinhaDeConsumoPacote(
    int ConsumoId,
    string Data,
    string Origem,
    bool Cancelado,
    string Situacao);

/// <summary>O que o formulário de SESSÕES do pacote mostra no topo: o saldo e o resumo.</summary>
public sealed record SessoesDoPacote(
    IReadOnlyList<LinhaDeConsumoPacote> Linhas,
    string Saldo,
    string Resumo);

/// <summary>Um pacote do catálogo como aparece na lista de escolha da venda.</summary>
public sealed record OpcaoDePacote(int Id, string Rotulo, decimal Valor);

/// <summary>
/// O que a tela PACOTES (e os formulários dela) AFIRMA — out/2026, saiu do WPF para o Blazor.
/// Era o <c>PacotesViewModel</c> (com <c>LinhaCatalogo</c>/<c>LinhaPacoteVendido</c>), o
/// <c>PacoteCatalogoEdicaoViewModel</c>, o <c>PacoteVendaViewModel</c> e o
/// <c>ConsumosPacoteViewModel</c>: o filtro e o resumo que DIZ que está filtrado, a frase do
/// vazio, o resumo do catálogo, a leitura dos números digitados, a DECISÃO DE PAGAMENTO da venda
/// (à vista ou entrada + parcelas — desenhada pelo MESMO <see cref="ParcelasDaVenda"/> que o
/// serviço grava, para a prévia nunca prometer parcelas diferentes das gravadas) e a origem de
/// cada sessão debitada. Quem lê e grava é a tela, com o MESMO <see cref="PacoteService"/>.
///
/// As permissões moram aqui também, porque são TRÊS atos com TRÊS regras: vender, debitar
/// sessão, orçar e mexer no catálogo são do BALCÃO (<c>VenderPacote</c> OU
/// <c>EditarFinanceiro</c>); cancelar a venda e devolver sessão ao saldo desfazem o que outra
/// pessoa registrou e ficam no Financeiro (<c>EditarFinanceiro</c>).
/// </summary>
public static class PacotesNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Vender, debitar sessão, orçar e o catálogo: um dos dois bits basta (um OU).</summary>
    public const Permissao QuemVende = Permissao.VenderPacote | Permissao.EditarFinanceiro;

    /// <summary>Cancelar a venda e devolver sessão: só o Financeiro — desfaz dinheiro de outra pessoa.</summary>
    public const Permissao QuemCancela = Permissao.EditarFinanceiro;

    public const string VazioSemFiltro =
        "A situação do pacote é calculada na hora — vencimento não precisa de rotina.";

    public const string VazioComFiltro =
        "Nenhum pacote vendido bate com o filtro — limpe-o para ver todos.";

    public const string Vendido = "Pacote vendido.";
    public const string Acrescentado = "Pacote acrescentado ao catálogo.";
    public const string RemovidoDoCatalogo = "Pacote removido do catálogo.";
    public const string SessaoDebitada = "Sessão debitada.";
    public const string Cancelado = "Pacote cancelado.";
    public const string CadastradoNaVenda = "Pacote cadastrado no catálogo. Agora dá para vendê-lo.";

    public const string CatalogoVazio =
        "Nenhum pacote à venda no catálogo — é o primeiro passo. O catálogo é o que a clínica oferece (\"10 sessões\", \"mensal\") e quanto custa; a venda copia o preço dele, então reajustar depois não muda o que já foi vendido.";

    public static string Atualizado(string nome)
        => $"\"{nome}\" atualizado no catálogo. O que já foi vendido não muda.";

    // ---------------------------------------------------------------- catálogo

    public static LinhaDeCatalogoPacote LinhaDe(PacoteCatalogo p) => new(
        Id: p.Id,
        Nome: p.Nome,
        Resumo: ResumirCatalogo(p),
        // pt-BR fixo: o WPF escrevia na cultura da máquina.
        Valor: p.Valor.ToString("C", PtBr),
        Ativo: p.Ativo,
        Tipo: p.Tipo,
        Sessoes: p.SessoesIncluidas,
        ValorDecimal: p.Valor,
        ValidadeDias: p.ValidadeDias,
        Ordem: p.Ordem,
        Observacoes: p.Observacoes);

    /// <summary>"Pacote de sessões · 10 sessões · vale 90 dias" — o rótulo, nunca o enum (parcela 41).</summary>
    public static string ResumirCatalogo(PacoteCatalogo p)
    {
        var sessoes = p.SessoesIncluidas is { } n ? $"{n} sessões" : "sessões livres";
        var validade = p.ValidadeDias is { } dias ? $" · vale {dias} dias" : string.Empty;
        return $"{RotulosEnum.De(p.Tipo)} · {sessoes}{validade}";
    }

    /// <summary>Rótulo do pacote na lista da venda: "10 sessões — 10 sessões — R$ 1.500,00".</summary>
    public static OpcaoDePacote OpcaoDe(PacoteCatalogo p) => new(
        p.Id,
        p.SessoesIncluidas is { } n
            ? $"{p.Nome} — {n} sessões — {p.Valor.ToString("C", PtBr)}"
            : $"{p.Nome} — {p.Valor.ToString("C", PtBr)}",
        p.Valor);

    /// <summary>
    /// Confere o formulário do catálogo e monta o objeto NOVO que o serviço regrava (com o Id —
    /// regravar mantém a procedência do que já foi vendido). Lê os números como o WPF lia:
    /// sessões e validade são inteiros; o valor aceita "1.250,00" e "1250.00", e em branco é
    /// zero (o voucher presenteado). O que a tela não mostra (ordem, observações, ativo) volta
    /// INTACTO de <paramref name="original"/>. Falso = <paramref name="erro"/> diz o quê.
    /// </summary>
    public static bool MontarCatalogo(
        LinhaDeCatalogoPacote? original, string? nome, TipoPacote tipo, string? sessoes,
        string? valor, string? validadeDias, out PacoteCatalogo? pacote, out string? erro)
    {
        pacote = null;
        if (!LerInteiro(sessoes, "as sessões", out var nSessoes, out erro)) return false;
        if (!LerValor(valor, out var v, out erro)) return false;
        if (!LerInteiro(validadeDias, "a validade", out var dias, out erro)) return false;

        pacote = new PacoteCatalogo
        {
            Id = original?.Id ?? 0,
            Nome = nome ?? string.Empty,
            Tipo = tipo,
            SessoesIncluidas = nSessoes,
            Valor = v,
            ValidadeDias = dias,
            Ativo = original?.Ativo ?? true,
            Ordem = original?.Ordem ?? 0,
            Observacoes = original?.Observacoes
        };
        return true;
    }

    /// <summary>Inteiro digitado (sessões, validade, parcelas). Vazio = nulo, que é "não informado".</summary>
    public static bool LerInteiro(string? texto, string oQue, out int? valor, out string? erro)
    {
        valor = null;
        erro = null;
        if (string.IsNullOrWhiteSpace(texto)) return true;
        if (int.TryParse(texto.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
        {
            valor = n;
            return true;
        }
        erro = $"Não entendi {oQue}: use só números.";
        return false;
    }

    /// <summary>
    /// O valor do pacote no catálogo. Vazio ou zero = zero (a cortesia é legítima); o resto pela
    /// <see cref="LeituraDeValores.TentarLerDecimal"/>, a mesma leitura do resto do sistema.
    /// </summary>
    public static bool LerValor(string? texto, out decimal valor, out string? erro)
    {
        erro = null;
        valor = 0m;
        if (string.IsNullOrWhiteSpace(texto)) return true;
        if (LeituraDeValores.TentarLerDecimal(texto, out valor)) return true;
        if (LeituraDeValores.TentarLerNumeroExato(texto, out var exato) && exato == 0m)
        {
            valor = 0m;
            return true;
        }
        valor = 0m;
        erro = "Não entendi o valor: use um número como 250,00.";
        return false;
    }

    public static string PerguntaDeExcluirDoCatalogo(LinhaDeCatalogoPacote l)
        => $"Tirar \"{l.Nome}\" da lista de venda? Os pacotes JÁ VENDIDOS continuam valendo — "
           + "eles guardam a própria cópia do que foi contratado.";

    // ---------------------------------------------------------------- vendidos

    public static LinhaDePacoteVendido LinhaDe(SaldoPacote v) => new(
        Id: v.PacoteId,
        PacienteId: v.PacienteId,
        Paciente: v.PacienteNome ?? "—",
        Nome: v.Nome,
        Saldo: v.SaldoRotulo,
        Situacao: RotulosEnum.De(v.Situacao),
        Valor: v.Valor.ToString("C", PtBr),
        Compra: v.DataCompra.ToString("dd/MM/yyyy", PtBr),
        Ativo: v.Ativo,
        Pagamento: v.PagamentoRotulo,
        PagamentoAtrasado: v.ParcelasVencidas > 0,
        SaldoSessoes: v.SaldoSessoes);

    public static bool FiltroAtivo(string? paciente, bool soAtivos)
        => soAtivos || !string.IsNullOrWhiteSpace(paciente);

    /// <summary>O filtro em memória, sobre o que já foi lido — o acento não atrapalha (<see cref="Busca"/>).</summary>
    public static IReadOnlyList<LinhaDePacoteVendido> Filtrar(
        IEnumerable<LinhaDePacoteVendido> todos, string? paciente, bool soAtivos)
        => todos.Where(v => (!soAtivos || v.Ativo) && Busca.Casa(v.Paciente, paciente)).ToList();

    /// <summary>
    /// O resumo DIZ que está filtrado: "12 pacotes" e "12 de 90 no filtro" respondem perguntas
    /// diferentes. Os totais são os do que foi CARREGADO — o filtro não muda a carteira.
    /// </summary>
    public static string Resumo(IReadOnlyCollection<LinhaDePacoteVendido> todos, int noFiltro, bool filtroAtivo)
    {
        var ativos = todos.Count(v => v.Ativo);
        var emSaldo = todos.Where(v => v.Ativo).Sum(v => v.SaldoSessoes ?? 0);
        return filtroAtivo
            ? $"{noFiltro} de {todos.Count} pacote(s) no filtro · {ativos} ativo(s) no total"
            : $"{todos.Count} pacote(s) vendidos · {ativos} ativo(s) · {emSaldo} sessão(ões) em saldo";
    }

    /// <summary>"Nenhum pacote vendido" e "nenhum bate com o filtro" são respostas diferentes.</summary>
    public static string Vazio(bool filtroAtivo) => filtroAtivo ? VazioComFiltro : VazioSemFiltro;

    public static string PerguntaDeUsarSessao(LinhaDePacoteVendido l)
        => $"Debitar uma sessão de \"{l.Nome}\" ({l.Paciente})? Hoje o saldo é: {l.Saldo}.";

    public static string PerguntaDeCancelar(LinhaDePacoteVendido l)
        => $"Por que o pacote \"{l.Nome}\" de {l.Paciente} está sendo cancelado? "
           + "Ele continua na lista, com o motivo — as sessões já usadas não somem. "
           + "As parcelas ainda A RECEBER caem junto; o que já foi recebido fica no caixa.";

    // ---------------------------------------------------------------- sessões do pacote

    /// <summary>
    /// As sessões que o pacote já consumiu, a mais recente primeiro, com o saldo e o resumo. A
    /// situação sai pelo RÓTULO (o WPF escrevia o enum cru no resumo: "situação hoje: Ativo").
    /// </summary>
    public static SessoesDoPacote Sessoes(PacotePaciente pacote, DateOnly hoje)
    {
        var linhas = pacote.Consumos
            .OrderByDescending(c => c.Data).ThenByDescending(c => c.Id)
            .Select(c => new LinhaDeConsumoPacote(
                c.Id,
                c.Data.ToString("dd/MM/yyyy", PtBr),
                Origem(c),
                c.Cancelado,
                c.Cancelado ? $"devolvida ao saldo — {c.MotivoCancelamento}" : "usada"))
            .ToList();

        var usadas = pacote.Consumos.Count(c => !c.Cancelado);
        var devolvidas = pacote.Consumos.Count(c => c.Cancelado);

        var saldo = pacote.SessoesContratadas is { } contratadas
            ? $"{Math.Max(contratadas - usadas, 0)} de {contratadas} sessões"
            : pacote.ValidoAte is { } ate
                ? $"livre até {ate.ToString("dd/MM/yyyy", PtBr)}"
                : "livre, sem prazo";

        var resumo = pacote.Consumos.Count == 0
            ? "Este pacote ainda não debitou nenhuma sessão."
            : $"{usadas} sessão(ões) usada(s)"
              + (devolvidas > 0 ? $" · {devolvidas} devolvida(s) ao saldo" : string.Empty)
              + $" · situação hoje: {RotulosEnum.De(pacote.Situacao(hoje)).ToLower(PtBr)}.";

        return new SessoesDoPacote(linhas, saldo, resumo);
    }

    /// <summary>
    /// A baixa automática grava de onde veio; a manual, quem a digitou. Sem isso a lista seria
    /// uma coluna de datas iguais, e quem confere não teria como achar a errada.
    /// </summary>
    public static string Origem(ConsumoPacote c)
    {
        var partes = new List<string>();
        if (c.AtendimentoId is { } atendimento) partes.Add($"atendimento #{atendimento}");
        if (!string.IsNullOrWhiteSpace(c.Observacao)) partes.Add(c.Observacao!);
        if (!string.IsNullOrWhiteSpace(c.CriadoPor)) partes.Add($"por {c.CriadoPor}");
        return partes.Count == 0 ? "—" : string.Join(" · ", partes);
    }

    public static string PerguntaDeDevolver(LinhaDeConsumoPacote l)
        => $"Por que a sessão de {l.Data} está voltando ao saldo? "
           + "O consumo continua no histórico, marcado com o motivo — não some.";

    public static string Devolvida(LinhaDeConsumoPacote l) => $"Sessão de {l.Data} devolvida ao saldo.";

    // ---------------------------------------------------------------- venda: pagamento

    /// <summary>Formas oferecidas na venda: tudo menos "Convênio", que não é como um particular paga.</summary>
    public static IReadOnlyList<FormaPagamento> Formas { get; } =
        Enum.GetValues<FormaPagamento>().Where(f => f != FormaPagamento.Convenio).ToList();

    /// <summary>Cartão pede maquininha e bandeira (é o que acha a taxa da adquirente).</summary>
    public static bool EhCartao(FormaPagamento? forma) => TaxaService.ModalidadeDe(forma) is not null;

    public static bool EhCredito(FormaPagamento? forma) => forma == FormaPagamento.CartaoCredito;

    /// <summary>O que o balcão escolheu no formulário da venda, ainda como texto digitado.</summary>
    public sealed record EscolhaDePagamento(
        bool AVista,
        FormaPagamento? Forma,
        string? Adquirente,
        string? Bandeira,
        string? ParcelasCartao,
        string? Entrada,
        string? Parcelas,
        DateOnly PrimeiroVencimento);

    /// <summary>
    /// A decisão de pagamento, montada do que foi digitado — a MESMA leitura do WPF. Lança
    /// <see cref="InvalidOperationException"/> com a frase quando a decisão não fecha.
    /// </summary>
    public static PagamentoDaVenda Pagamento(EscolhaDePagamento e)
    {
        var parcelasCartao = 1;
        if (EhCredito(e.Forma)
            && (!int.TryParse(e.ParcelasCartao?.Trim(), out parcelasCartao) || parcelasCartao is < 1 or > 36))
            throw new InvalidOperationException("Informe de 1 a 36 parcelas na maquininha.");

        if (e.AVista)
            return new PagamentoDaVenda
            {
                TudoAgora = true, FormaDoPagoAgora = e.Forma,
                Adquirente = e.Adquirente, Bandeira = e.Bandeira, ParcelasCartao = parcelasCartao
            };

        var entrada = 0m;
        if (!string.IsNullOrWhiteSpace(e.Entrada) && !LeituraDeValores.TentarLerDecimal(e.Entrada, out entrada))
            throw new InvalidOperationException("Não entendi a entrada: use algo como 200,00.");

        if (!int.TryParse((e.Parcelas ?? string.Empty).Trim(), out var n))
            throw new InvalidOperationException("Diga em quantas parcelas (um número inteiro).");

        return PagamentoDaVenda.APrazo(
            n, e.PrimeiroVencimento, entrada, formaDaEntrada: entrada > 0m ? e.Forma : null) with
        {
            Adquirente = e.Adquirente, Bandeira = e.Bandeira, ParcelasCartao = parcelasCartao
        };
    }

    /// <summary>
    /// O valor cobrado digitado. Nulo = em branco (vale o de tabela). Lança com a frase quando
    /// não se lê.
    /// </summary>
    public static decimal? ValorCobrado(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        if (!LeituraDeValores.TentarLerDecimal(texto, out var lido))
            throw new InvalidOperationException("Não entendi o valor: use algo como 250,00.");
        return lido;
    }

    /// <summary>
    /// O que vai ser gravado, por extenso, ANTES do clique: "entrada R$ 200,00 + 3 parcelas
    /// (R$ 1.000,00) a partir de 06/10/2026". Quando a decisão não fecha, a frase é o MOTIVO —
    /// o mesmo que o Vender vai recusar. Em branco, o valor é o de TABELA do pacote escolhido — o
    /// mesmo que a venda grava (o WPF desenhava sobre zero e dizia "nada a receber" antes de a
    /// pessoa digitar o valor).
    /// </summary>
    public static string Previa(string? valorCobrado, decimal? valorDeTabela, DateOnly dataCompra, EscolhaDePagamento escolha)
    {
        try
        {
            var valor = ValorCobrado(valorCobrado) ?? valorDeTabela;
            if (valor is null) return "Escolha o pacote: a prévia das parcelas usa o valor dele.";
            var desenho = ParcelasDaVenda.Desenhar(valor.Value, dataCompra, Pagamento(escolha));
            return desenho.Count == 0
                ? "Pacote sem valor: nada a receber."
                : "Vai gravar: " + ParcelasDaVenda.Resumir(desenho) + ".";
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Confere a venda na ORDEM do WPF — paciente, pacote, valor, decisão de pagamento — e
    /// devolve o que o serviço recebe. A decisão é validada pelo MESMO desenho que o serviço
    /// grava: a recusa chega antes de a venda existir. Falso = <paramref name="erro"/>.
    /// </summary>
    public static bool ConferirVenda(
        int? pacienteId, OpcaoDePacote? pacote, bool catalogoVazio, string? valorCobrado,
        DateOnly dataCompra, EscolhaDePagamento escolha,
        out decimal? valor, out PagamentoDaVenda? pagamento, out string? erro)
    {
        valor = null;
        pagamento = null;
        if (pacienteId is null)
        {
            erro = "Escolha o paciente que está comprando.";
            return false;
        }
        if (pacote is null)
        {
            // Catálogo vazio não é "escolha o pacote": é "não há pacote para escolher".
            erro = catalogoVazio
                ? "Não há pacote no catálogo para vender. Cadastre o primeiro — o botão está aí em cima."
                : "Escolha o pacote.";
            return false;
        }
        try
        {
            valor = ValorCobrado(valorCobrado);
            pagamento = Pagamento(escolha);
            ParcelasDaVenda.Desenhar(valor ?? pacote.Valor, dataCompra, pagamento);
        }
        catch (InvalidOperationException ex)
        {
            erro = ex.Message;
            return false;
        }
        erro = null;
        return true;
    }

    /// <summary>O valor de tabela no campo, quando o pacote é escolhido (continua editável).</summary>
    public static string ParaCampo(decimal valor) => valor.ToString("0.00", PtBr);
}
