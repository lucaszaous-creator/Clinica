using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Financeiro.ViewModels;

/// <summary>Guia efetivada no convênio esperando virar dinheiro no caixa.</summary>
public sealed partial class LinhaConciliacao : ObservableObject
{
    public required GuiaSemLancamento Guia { get; init; }
    public required string DataBaixa { get; init; }
    public required string Paciente { get; init; }
    public required string Convenio { get; init; }
    public required string NumeroGuia { get; init; }
    public required string Tipo { get; init; }

    /// <summary>Valor a lançar, digitado pela operadora do financeiro.</summary>
    [ObservableProperty]
    private string _valor = string.Empty;

    /// <summary>
    /// O que a operadora vai reter deste valor, calculado ENQUANTO se digita (parcela 18).
    /// Aparece antes de gravar porque é aí que dá para conferir contra o demonstrativo —
    /// descobrir a retenção depois transformaria a conferência num estorno.
    /// </summary>
    [ObservableProperty]
    private string? _retencao;

    /// <summary>
    /// DE ONDE veio o valor proposto (parcela 20): "tabela: Unimed · Acupuntura — R$ 145,00".
    /// Um campo que se preenche sozinho sem explicar é pior que um campo vazio — a pessoa
    /// confirma sem conferir, e o erro entra no caixa com aparência de conferido.
    /// Null quando não há tabela para este convênio: aí o valor é digitado, como sempre foi.
    /// </summary>
    [ObservableProperty]
    private string? _procedencia;

    /// <summary>
    /// A guia foi glosada e o convênio ainda não aceitou de volta (parcela 27). A linha
    /// continua na lista — some não, MARCADA sim, como o documento cancelado na central:
    /// a guia que desaparece sem explicação faz o balcão gastar a tarde procurando o que
    /// ele viu ontem. Aqui ela diz por que não deveria virar receita.
    /// </summary>
    public bool TemGlosa => Guia.GlosaEmAberto;

    /// <summary>O motivo, escrito como se fala, para caber ao lado da linha.</summary>
    public string AvisoGlosa => Guia.GlosaEmAberto
        ? $"Glosada em {Guia.DataGlosa:dd/MM/yyyy}"
          + (string.IsNullOrWhiteSpace(Guia.MotivoGlosa) ? "" : $" — {Guia.MotivoGlosa}")
        : string.Empty;
}

/// <summary>
/// Uma guia glosada que tem receita contada no caixa. É o caminho inverso da conciliação:
/// lá a pergunta é "o que ainda não virou dinheiro?", aqui é "que dinheiro eu contei e o
/// convênio recusou?".
/// </summary>
public sealed partial class LinhaReceitaGlosada : ObservableObject
{
    public required ReceitaGlosada Receita { get; init; }
    public required string Paciente { get; init; }
    public required string Convenio { get; init; }
    public required string NumeroGuia { get; init; }
    public required string DataGlosa { get; init; }
    public required string Valor { get; init; }
    public required string Situacao { get; init; }
    public required string Motivo { get; init; }

    /// <summary>Prazo de recurso, quando ainda corre — é o que decide a pressa.</summary>
    public required string Prazo { get; init; }

    /// <summary>
    /// Só a receita PREVISTA se cancela, e só por quem lança no caixa. O botão apagado é
    /// a metade VISÍVEL das duas regras — a que impede é o <c>Exigir</c> no comando mais
    /// a recusa do serviço, porque atalho de teclado passa por cima de botão desabilitado.
    /// </summary>
    public bool PodeCancelar =>
        Receita.AindaPrevisto && SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro);

    /// <summary>O que fazer quando o dinheiro já entrou — dito na própria linha.</summary>
    public string? Orientacao => Receita.JaRealizado
        ? "O dinheiro desta guia já entrou. Se a operadora estornou, lance a devolução "
          + "como saída, com a data do estorno."
        : null;
}

/// <summary>
/// Conciliação — a tela onde o faturamento e o financeiro se encontram, nos DOIS
/// sentidos (parcela 27).
///
/// A aba "A lançar" é a de sempre: guias que o faturamento efetivou no convênio e que
/// ainda não têm receita lançada. Ao lançar, o vínculo fica gravado e a guia sai da lista.
///
/// A aba "Glosadas" é o caminho de volta, que não existia: guias que já viraram receita
/// e que o convênio recusou depois. Sem ela, o dinheiro recusado continuava no fluxo de
/// caixa e na rentabilidade como se fosse entrar — receita fantasma, o número errado com
/// cara de exato.
///
/// As duas ficam no MESMO item da sidebar, em sub-abas: é o mesmo assunto (guia × caixa)
/// visto pelos dois lados, e a proposta tem um item ali.
/// </summary>
public sealed partial class ConciliacaoViewModel : ObservableObject
{
    private readonly FinanceiroService _financeiro;
    private readonly TaxaService _taxas;
    private readonly PrecoConvenioService _precos;
    private readonly ReceitaGlosadaService _glosadas;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaConciliacao> Linhas { get; } = [];

    /// <summary>Guias glosadas com receita ainda contada — a aba do caminho de volta.</summary>
    public ObservableCollection<LinhaReceitaGlosada> Glosadas { get; } = [];

    [ObservableProperty]
    private DateTime _mes = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, lista vazia por erro fica idêntica
    /// a lista vazia por não haver nada, e o aviso some junto com o snackbar em 4 segundos.
    /// </summary>
    [ObservableProperty]
    private bool _naoVerificado;

    [ObservableProperty]
    private string _resumo = string.Empty;

    [ObservableProperty]
    private string _resumoGlosadas = string.Empty;

    /// <summary>
    /// A leitura das glosas FALHOU. Terceiro estado obrigatório: uma aba vazia porque a
    /// consulta quebrou se lê como "nenhuma receita glosada", que é a mentira mais cara
    /// que esta tela poderia contar.
    /// </summary>
    [ObservableProperty]
    private bool _glosadasNaoVerificadas;

    public ConciliacaoViewModel(
        FinanceiroService financeiro, TaxaService taxas, PrecoConvenioService precos,
        ReceitaGlosadaService glosadas, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _financeiro = financeiro;
        _taxas = taxas;
        _precos = precos;
        _glosadas = glosadas;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    partial void OnMesChanged(DateTime value) => _ = CarregarAsync();

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            NaoVerificado = false;
            var inicio = new DateOnly(Mes.Year, Mes.Month, 1);
            var fim = inicio.AddMonths(1).AddDays(-1);

            var guias = await _financeiro.GuiasSemLancamentoAsync(inicio, fim);
            Linhas.Clear();
            foreach (var g in guias)
            {
                // A TABELA DE PREÇO cadastrada no Gerente (parcela 20) preenche o valor.
                // É proposta, não imposição: quem confirma é quem está conferindo o
                // demonstrativo, porque a operadora pode ter pago menos (glosa parcial) ou
                // um valor negociado fora da tabela. Sem tabela, o campo fica vazio para
                // ser digitado — como sempre foi; o sistema não inventa valor de mercado.
                var proposto = await _precos.ProporAsync(g);

                Linhas.Add(new LinhaConciliacao
                {
                    Guia = g,
                    DataBaixa = g.DataBaixa.ToString("dd/MM/yyyy"),
                    Paciente = g.Paciente,
                    Convenio = g.Convenio.ToString(),
                    NumeroGuia = g.NumeroGuiaReal ?? "—",
                    Tipo = g.Tipo.ToString(),
                    Valor = proposto.Houve ? proposto.Valor.ToString("0.##") : string.Empty,
                    Procedencia = proposto.Houve ? proposto.Procedencia : null
                });
            }

            var comTabela = Linhas.Count(l => l.Procedencia is not null);
            Resumo = Linhas.Count == 0
                ? "Nenhuma guia pendente de lançamento neste mês."
                : comTabela == Linhas.Count
                    ? $"{Linhas.Count} guia(s) efetivada(s) sem receita lançada — valor proposto pela tabela."
                    : comTabela == 0
                        ? $"{Linhas.Count} guia(s) efetivada(s) sem receita lançada. Sem tabela de preço cadastrada: informe o valor."
                        : $"{Linhas.Count} guia(s) efetivada(s) sem receita lançada · {Linhas.Count - comTabela} sem preço na tabela.";
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            _snackbar.Erro($"Não foi possível carregar a conciliação: {ex.Message}");
        }
        finally
        {
            Carregando = false;
        }

        // Cada lado carrega sozinho: a aba das glosadas quebrar não pode levar junto a
        // lista de guias a lançar, que é o trabalho do dia.
        await CarregarGlosadasAsync();
    }

    private async Task CarregarGlosadasAsync()
    {
        var inicio = new DateOnly(Mes.Year, Mes.Month, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        try
        {
            GlosadasNaoVerificadas = false;
            var receitas = await _glosadas.PendentesAsync(inicio, fim);

            Glosadas.Clear();
            foreach (var r in receitas)
            {
                var dias = r.DiasParaFimRecurso(hoje);
                Glosadas.Add(new LinhaReceitaGlosada
                {
                    Receita = r,
                    Paciente = r.Paciente,
                    Convenio = r.Convenio.ToString(),
                    NumeroGuia = r.NumeroGuiaReal ?? "—",
                    DataGlosa = r.DataGlosa.ToString("dd/MM/yyyy"),
                    Valor = r.Valor.ToString("C"),
                    Situacao = r.AindaPrevisto ? "receita prevista" : "receita já realizada",
                    Motivo = string.IsNullOrWhiteSpace(r.MotivoGlosa)
                        ? (r.MotivoGlosaCodigo ?? "sem motivo registrado")
                        : r.MotivoGlosa,
                    Prazo = dias switch
                    {
                        null => "—",
                        < 0 => $"recurso vencido há {-dias.Value} dia(s)",
                        0 => "recurso vence hoje",
                        _ => $"{dias} dia(s) para recorrer"
                    }
                });
            }

            var previstas = Glosadas.Where(l => l.Receita.AindaPrevisto).ToList();
            ResumoGlosadas = Glosadas.Count == 0
                ? "Nenhuma guia glosada com receita lançada neste mês."
                : previstas.Count == 0
                    ? $"{Glosadas.Count} guia(s) glosada(s) — todas com o dinheiro já recebido."
                    : $"{previstas.Count} de {Glosadas.Count} guia(s) glosada(s) ainda contam "
                      + $"{previstas.Sum(l => l.Receita.Valor):C} de receita que o convênio recusou.";
        }
        catch (Exception ex)
        {
            // Falha nunca aparece como sucesso: a aba diz que não conseguiu conferir.
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — receita glosada não pôde ser carregada", ex);
            Glosadas.Clear();
            GlosadasNaoVerificadas = true;
            ResumoGlosadas = $"Não foi possível conferir as glosas deste mês: {ex.Message}";
        }
    }

    /// <summary>
    /// Derruba a receita prevista de uma guia que o convênio recusou.
    ///
    /// Não apaga o lançamento — cancela com motivo, porque lançamento é fato datado e o
    /// histórico precisa dizer que a receita caiu por GLOSA, e não por engano de
    /// digitação. Cancelado, o vínculo deixa de valer e a guia REAPARECE sozinha na aba
    /// "A lançar": se o recurso for aceito, ela está lá esperando ser lançada de novo.
    /// </summary>
    [RelayCommand]
    private async Task CancelarReceitaAsync(LinhaReceitaGlosada? linha)
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "cancelar receita no caixa");

        if (linha is null) return;

        if (linha.Receita.JaRealizado)
        {
            _dialogo.Aviso(
                "O dinheiro já entrou",
                "Esta guia foi glosada DEPOIS de o valor cair na conta. Cancelar a "
                + "entrada faria o caixa parar de bater com o extrato e levaria junto a "
                + "conferência do dia.\n\nSe a operadora estornou o valor, lance a "
                + "devolução como saída no Caixa — ela é outro fato, com a data do estorno.");
            return;
        }

        var motivo = _dialogo.PerguntarTexto(
            "Derrubar a receita glosada",
            $"A guia de {linha.Paciente} ({linha.Valor}) foi glosada em {linha.DataGlosa}. "
            + "Por que a receita está caindo? O lançamento NÃO é apagado — fica cancelado "
            + "com este motivo, e a guia volta para a aba \"A lançar\" caso o recurso seja "
            + "aceito.",
            linha.Motivo);
        if (string.IsNullOrWhiteSpace(motivo)) return;

        try
        {
            await _glosadas.CancelarReceitaAsync(
                linha.Receita.CodigoId, motivo, SessaoUsuario.Atual.Operador);

            _snackbar.Info($"Receita de {linha.Paciente} cancelada — a guia voltou para a conciliação.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            _snackbar.Erro(ex.Message);
        }
    }

    /// <summary>Lança a receita da guia com o valor informado na linha.</summary>
    [RelayCommand]
    private async Task LancarAsync(LinhaConciliacao? linha)
    {
        if (linha is null) return;

        // Valores.TentarLerDecimal, e não decimal.TryParse: é o leitor do projeto, que
        // aceita "1.250,00" e "1250.00" sem depender da cultura da máquina.
        if (!Valores.TentarLerDecimal(linha.Valor, out var valor) || valor <= 0)
        {
            _snackbar.Erro("Informe um valor válido, maior que zero.");
            return;
        }

        // Guia recusada pelo convênio ainda pode virar receita — a clínica pode estar
        // certa de que recupera no recurso, e quem decide é ela. Mas ela decide SABENDO:
        // até a parcela 27 esta linha era idêntica à de uma guia paga.
        if (linha.TemGlosa && !_dialogo.ConfirmarPerigo(
                "Guia glosada",
                $"{linha.AvisoGlosa}.\n\nO convênio recusou esta guia e ainda não a "
                + "aceitou de volta. Lançar a receita agora conta um dinheiro que foi "
                + "negado — se o recurso não for aceito, ele vira receita fantasma no "
                + "fluxo de caixa.\n\nLançar mesmo assim?"))
            return;

        try
        {
            // A RETENÇÃO na fonte da operadora (parcela 18). O valor do lançamento
            // continua sendo o BRUTO da guia; o retido é dedução ao lado, e o líquido é
            // calculado — mesma regra da maquininha.
            var deducoes = await _taxas.CalcularAsync(
                valor, linha.Guia.DataBaixa, FormaPagamento.Convenio,
                reterImposto: true, convenioCodigo: linha.Guia.ConvenioCodigo);

            await _financeiro.LancarReceitaDaGuiaAsync(linha.Guia, valor, deducoes: deducoes);

            _snackbar.Sucesso(deducoes.ValorImposto is { } retido and > 0m
                ? $"Receita de {valor:C} lançada para {linha.Paciente} — {retido:C} retidos na fonte."
                : $"Receita de {valor:C} lançada para {linha.Paciente}.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            _snackbar.Erro(ex.Message);
        }
    }

    /// <summary>
    /// Calcula a retenção da linha sem gravar nada — a mesma conta que o Lançar vai usar,
    /// para não haver duas respostas para a mesma pergunta.
    /// </summary>
    [RelayCommand]
    private async Task PreverAsync(LinhaConciliacao? linha)
    {
        if (linha is null) return;

        try
        {
            linha.Retencao = null;
            if (!Valores.TentarLerDecimal(linha.Valor, out var valor) || valor <= 0) return;

            var d = await _taxas.CalcularAsync(
                valor, linha.Guia.DataBaixa, FormaPagamento.Convenio,
                reterImposto: true, convenioCodigo: linha.Guia.ConvenioCodigo);

            // Sem retenção cadastrada não se inventa desconto — mas a linha DIZ que não
            // achou, senão o líquido igual ao bruto passaria por retenção zero.
            linha.Retencao = d.ValorImposto is { } retido and > 0m
                ? $"retém {retido:C} ({d.DetalheImposto}) · líquido {valor - retido:C}"
                : "sem retenção cadastrada para este convênio";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — prévia da retenção falhou", ex);
            linha.Retencao = null;
        }
    }

    [RelayCommand]
    private void MesAnterior() => Mes = Mes.AddMonths(-1);

    [RelayCommand]
    private void ProximoMes() => Mes = Mes.AddMonths(1);
}
