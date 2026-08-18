using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

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
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    /// <summary>
    /// TODAS as guias do mês. `Linhas` é o recorte que a tela mostra.
    ///
    /// A separação existe por uma razão que não é de leiaute: a linha guarda o
    /// <see cref="LinhaConciliacao.Valor"/> DIGITADO. Se filtrar recriasse as linhas, o
    /// valor que a pessoa acabou de teclar em cinco guias sumiria ao ela restringir a
    /// lista para achar a sexta. Por isso o filtro reaproveita as MESMAS instâncias — ele
    /// escolhe quais aparecem, nunca as reconstrói.
    /// </summary>
    private readonly List<LinhaConciliacao> _todas = [];

    public ObservableCollection<LinhaConciliacao> Linhas { get; } = [];

    // ===== Filtros (parcela 56) =====
    //
    // A tela abria com 53 guias numa lista corrida e nada para estreitá-la. Quem concilia
    // não lê a lista: ela tem o DEMONSTRATIVO de uma operadora na mão e precisa achar,
    // uma a uma, as guias que estão nele. Sem filtro, isso é rolar 53 linhas por guia.

    /// <summary>Rótulo da opção "não filtrar por convênio". Fica sempre no topo da lista.</summary>
    public const string TodosConvenios = "Todos os convênios";

    /// <summary>
    /// As operadoras presentes NO MÊS carregado — não o catálogo inteiro.
    ///
    /// Oferecer convênio que não tem guia no mês daria uma lista de opções que só levam a
    /// resultado vazio, e resultado vazio se lê como "não achei", não como "você escolheu
    /// um filtro impossível".
    /// </summary>
    public ObservableCollection<string> Convenios { get; } = [TodosConvenios];

    /// <summary>
    /// Filtra pela OPERADORA (o nome resolvido), e não pela família do enum.
    ///
    /// É diferente de propósito da consulta de guias do faturamento, que filtra por
    /// família porque a pergunta lá é "o que vem sendo feito". Aqui a pergunta é outra:
    /// "onde estão as guias deste demonstrativo?", e demonstrativo é de UMA operadora.
    /// Filtrar por família juntaria todas as personalizadas — "Sul América" e "Unimed
    /// Costa do Sol" respondem ao mesmo `Convenio.Personalizado` — e devolveria as guias
    /// de quem não está no papel.
    /// </summary>
    [ObservableProperty]
    private string _filtroConvenio = TodosConvenios;

    [ObservableProperty]
    private string _filtroPaciente = string.Empty;

    /// <summary>O número da guia é a chave do demonstrativo — casa por TRECHO, não exato.</summary>
    [ObservableProperty]
    private string _filtroGuia = string.Empty;

    /// <summary>
    /// Só as que a tabela de preço não soube propor — as que exigem digitar o valor.
    /// É a fila de trabalho de quem está conferindo: as com valor proposto só precisam
    /// ser confirmadas.
    /// </summary>
    [ObservableProperty]
    private bool _somenteSemValor;

    /// <summary>
    /// O que o estado vazio DIZ. Muda com o filtro, e isso não é detalhe de texto.
    ///
    /// "Nenhuma guia baixada esperando receita" e "nenhuma guia bate com o filtro" pedem
    /// ações opostas: a primeira encerra o trabalho do mês, a segunda manda limpar o
    /// filtro. Um filtro esquecido que responda a primeira faz a clínica dar o mês por
    /// conciliado com 53 guias pendentes — é a lição da lista de espera da parcela 25.
    /// </summary>
    public string TituloVazio => FiltroAtivo
        ? "Nenhuma guia bate com o filtro"
        : "Nenhuma guia baixada esperando receita";

    public string DescricaoVazio => FiltroAtivo
        ? $"O mês tem {_todas.Count} guia(s) pendente(s) — elas estão fora do filtro atual."
        : "A guia sai desta lista quando passa a TER receita, não quando alguém a marca.";

    /// <summary>Algum filtro está valendo — acende o "Limpar" e muda o texto do vazio.</summary>
    public bool FiltroAtivo =>
        FiltroConvenio != TodosConvenios
        || !string.IsNullOrWhiteSpace(FiltroPaciente)
        || !string.IsNullOrWhiteSpace(FiltroGuia)
        || SomenteSemValor;

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

    /// <summary>
    /// ⚠️ Nada de serviço SCOPED no construtor — o shell resolve esta tela do provedor
    /// RAIZ, e Scoped pedido à raiz vive pela vida inteira do app, com o `DbContext`
    /// junto (parcela 69). Escopo por operação. Ver a checagem 37 do verificar-suite.
    /// </summary>
    public ConciliacaoViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    partial void OnMesChanged(DateTime value) => _ = CarregarAsync();

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): trocar o mês duas vezes rápido
    /// deixa duas cargas no ar, e a resposta velha chegando por último deixaria as guias
    /// de um mês sob o título do outro — na tela em que se lança dinheiro.
    /// </summary>
    private int _geracaoCarga;

    /// <summary>
    /// Ligado enquanto a lista de convênios é remontada.
    ///
    /// ⚠️ `Convenios.Clear()` faz o `ComboBox` zerar a seleção e devolver <c>null</c> pelo
    /// binding — é a mesma armadilha que a parcela 50 pegou na prévia do Novo atendimento.
    /// Sem a guarda, trocar de mês dispara um refiltro com o convênio nulo, que não casa
    /// com nada: a tela pisca "nenhuma guia bate com o filtro" antes de mostrar o mês novo.
    /// </summary>
    private bool _montandoConvenios;

    // Todo filtro refiltra na hora. É lista já carregada — não vai ao banco, então não há
    // resposta fora de ordem para descartar nem motivo para agrupar as teclas.
    partial void OnFiltroConvenioChanged(string value)
    {
        // O binding pode devolver nulo ao esvaziar a lista; "todos" é o estado honesto.
        if (value is null)
        {
            FiltroConvenio = TodosConvenios;
            return;
        }

        if (!_montandoConvenios) Refiltrar();
    }
    partial void OnFiltroPacienteChanged(string value) => Refiltrar();
    partial void OnFiltroGuiaChanged(string value) => Refiltrar();
    partial void OnSomenteSemValorChanged(bool value) => Refiltrar();

    /// <summary>
    /// Recorta <see cref="_todas"/> em <see cref="Linhas"/>.
    ///
    /// Reaproveita as instâncias: o valor digitado numa guia sobrevive a estreitar e
    /// alargar o filtro.
    /// </summary>
    private void Refiltrar()
    {
        var escolhidas = _todas.Where(l =>
            (FiltroConvenio == TodosConvenios || l.Convenio == FiltroConvenio)
            && Busca.Casa(l.Paciente, FiltroPaciente)
            && Busca.Casa(l.NumeroGuia, FiltroGuia)
            && (!SomenteSemValor || l.Procedencia is null));

        Linhas.Clear();
        foreach (var l in escolhidas) Linhas.Add(l);

        OnPropertyChanged(nameof(FiltroAtivo));
        OnPropertyChanged(nameof(TituloVazio));
        OnPropertyChanged(nameof(DescricaoVazio));
        AtualizarResumo();
    }

    /// <summary>
    /// O resumo DIZ que a lista está filtrada.
    ///
    /// "12 guia(s)" e "12 de 53 guia(s)" respondem perguntas diferentes, e quem volta à
    /// tela depois do café não lembra o que deixou marcado no combo. Sem isso, o filtro
    /// esquecido faz a clínica concluir que o mês teve pouca guia pendente.
    /// </summary>
    private void AtualizarResumo()
    {
        if (_todas.Count == 0)
        {
            Resumo = "Nenhuma guia pendente de lançamento neste mês.";
            return;
        }

        if (Linhas.Count == 0)
        {
            // Distinguir as duas ausências é o que impede a pessoa de concluir errado:
            // "não há guias" e "o filtro não achou" pedem ações opostas.
            Resumo = $"Nenhuma das {_todas.Count} guia(s) do mês bate com o filtro atual.";
            return;
        }

        var semTabela = Linhas.Count(l => l.Procedencia is null);
        var quantas = FiltroAtivo
            ? $"{Linhas.Count} de {_todas.Count} guia(s)"
            : $"{Linhas.Count} guia(s)";

        Resumo = semTabela == 0
            ? $"{quantas} efetivada(s) sem receita lançada — valor proposto pela tabela."
            : semTabela == Linhas.Count
                ? $"{quantas} efetivada(s) sem receita lançada. Sem tabela de preço cadastrada: informe o valor."
                : $"{quantas} efetivada(s) sem receita lançada · {semTabela} sem preço na tabela.";
    }

    [RelayCommand]
    private void LimparFiltro()
    {
        FiltroConvenio = TodosConvenios;
        FiltroPaciente = string.Empty;
        FiltroGuia = string.Empty;
        SomenteSemValor = false;
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        try
        {
            Carregando = true;
            NaoVerificado = false;
            var inicio = new DateOnly(Mes.Year, Mes.Month, 1);
            var fim = inicio.AddMonths(1).AddDays(-1);

            using var escopo = _escopos.CreateScope();
            var financeiro = escopo.ServiceProvider.GetRequiredService<FinanceiroService>();
            var precos = escopo.ServiceProvider.GetRequiredService<PrecoConvenioService>();

            var guias = await financeiro.GuiasSemLancamentoAsync(inicio, fim);
            var novas = new List<LinhaConciliacao>();
            foreach (var g in guias)
            {
                // A TABELA DE PREÇO cadastrada no Gerente (parcela 20) preenche o valor.
                // É proposta, não imposição: quem confirma é quem está conferindo o
                // demonstrativo, porque a operadora pode ter pago menos (glosa parcial) ou
                // um valor negociado fora da tabela. Sem tabela, o campo fica vazio para
                // ser digitado — como sempre foi; o sistema não inventa valor de mercado.
                var proposto = await precos.ProporAsync(g);

                novas.Add(new LinhaConciliacao
                {
                    Guia = g,
                    DataBaixa = g.DataBaixa.ToString("dd/MM/yyyy"),
                    Paciente = g.Paciente,
                    Convenio = CatalogoConvenios.Nome(g.ConvenioCodigo, g.Convenio),
                    NumeroGuia = g.NumeroGuiaReal ?? "—",
                    // Rótulo, nunca o identificador: "ConsultaEspecialidade" na coluna
                    // Tipo é o defeito da parcela 41 — o Gerente já resolve pelo mesmo caminho.
                    Tipo = RotulosEnum.De(g.Tipo),
                    Valor = proposto.Houve ? proposto.Valor.ToString("0.##") : string.Empty,
                    Procedencia = proposto.Houve ? proposto.Procedencia : null
                });
            }

            // Chegou tarde: outro mês já pediu uma carga mais nova — os valores que a
            // pessoa digitou nas linhas da carga vigente não podem ser apagados por esta.
            if (geracao != _geracaoCarga) return;

            _todas.Clear();
            _todas.AddRange(novas);

            // As operadoras do mês, em ordem. A escolha anterior é preservada quando ela
            // ainda existe — trocar de mês não pode desfazer o filtro de quem está
            // conferindo o demonstrativo da mesma operadora mês a mês.
            var escolhido = FiltroConvenio;
            _montandoConvenios = true;
            try
            {
                Convenios.Clear();
                Convenios.Add(TodosConvenios);
                foreach (var nome in _todas.Select(l => l.Convenio).Distinct().OrderBy(n => n))
                    Convenios.Add(nome);

                FiltroConvenio = Convenios.Contains(escolhido) ? escolhido : TodosConvenios;
            }
            finally
            {
                _montandoConvenios = false;
            }

            Refiltrar();
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            _snackbar.Erro($"Não foi possível carregar a conciliação: {ex.Message}");
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }

        // Cada lado carrega sozinho: a aba das glosadas quebrar não pode levar junto a
        // lista de guias a lançar, que é o trabalho do dia.
        await CarregarGlosadasAsync(geracao);
    }

    private async Task CarregarGlosadasAsync(int geracao)
    {
        var inicio = new DateOnly(Mes.Year, Mes.Month, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        try
        {
            GlosadasNaoVerificadas = false;
            using var escopo = _escopos.CreateScope();
            var receitas = await escopo.ServiceProvider
                .GetRequiredService<ReceitaGlosadaService>().PendentesAsync(inicio, fim);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Glosadas.Clear();
            foreach (var r in receitas)
            {
                var dias = r.DiasParaFimRecurso(hoje);
                Glosadas.Add(new LinhaReceitaGlosada
                {
                    Receita = r,
                    Paciente = r.Paciente,
                    Convenio = CatalogoConvenios.Nome(r.ConvenioCodigo, r.Convenio),
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

            if (geracao != _geracaoCarga) return;

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
            using var escopo = _escopos.CreateScope();
            await escopo.ServiceProvider.GetRequiredService<ReceitaGlosadaService>()
                .CancelarReceitaAsync(
                linha.Receita.CodigoId, motivo, SessaoUsuario.Atual.Operador);

            // A guia volta ao mês do ATENDIMENTO, não ao carregado: a aba "A lançar"
            // filtra pela data da sessão, e a glosa pode ter chegado meses depois. Sem
            // dizer o mês, quem cancela em setembro procura a guia de junho em setembro
            // — e conclui que ela sumiu.
            var voltouPara = linha.Receita.DataAtendimento is { } atendimento
                             && (atendimento.Year != Mes.Year || atendimento.Month != Mes.Month)
                ? $" de {atendimento:MMMM/yyyy} (mês do atendimento)"
                : string.Empty;
            _snackbar.Info($"Receita de {linha.Paciente} cancelada — a guia voltou para a conciliação{voltouPara}.");
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

        // O mesmo bit do irmão CancelarReceita: lançar é entrar dinheiro no caixa —
        // barrar o desfazer e deixar o fazer aberto era a assimetria errada.
        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "lançar receita no caixa");

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
            using var escopo = _escopos.CreateScope();
            var deducoes = await escopo.ServiceProvider
                .GetRequiredService<TaxaService>().CalcularAsync(
                valor, linha.Guia.DataBaixa, FormaPagamento.Convenio,
                reterImposto: true, convenioCodigo: linha.Guia.ConvenioCodigo);

            await escopo.ServiceProvider.GetRequiredService<FinanceiroService>()
                .LancarReceitaDaGuiaAsync(linha.Guia, valor, deducoes: deducoes);

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

            using var escopo = _escopos.CreateScope();
            var d = await escopo.ServiceProvider.GetRequiredService<TaxaService>().CalcularAsync(
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
