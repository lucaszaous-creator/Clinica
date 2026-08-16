using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Clinica.Desktop.Shell.Componentes;

namespace Clinica.Gerente.ViewModels;

/// <summary>Uma meta do ano, com o que aconteceu ao lado.</summary>
public sealed class LinhaMeta
{
    public required int MetaId { get; init; }
    public required int Mes { get; init; }
    public required IndicadorMeta Indicador { get; init; }
    public required string Periodo { get; init; }
    public required string Rotulo { get; init; }
    public required string Alvo { get; init; }
    public required string Dono { get; init; }
    public required string Realizado { get; init; }
    public required string Situacao { get; init; }
    public required decimal Valor { get; init; }
    public int? ProfissionalId { get; init; }

    /// <summary>Fração da barra, de 0 a 1. Nula quando não há realizado medido.</summary>
    public double? Fracao { get; init; }

    public bool Atingida { get; init; }
    public bool EmRisco { get; init; }
}

/// <summary>
/// Metas da direção (parcela 28) — a tela onde ela declara aonde quer chegar.
///
/// O painel comparava o mês com o **mês anterior**, e isso responde "melhorou?" e nunca
/// "chegamos onde a gente disse que ia chegar?". Sem alvo declarado, um mês 10% melhor que
/// um mês ruim aparece como vitória, e a direção só descobre o problema no fechamento —
/// quando não há mais mês para reagir.
///
/// A tela é a LISTA do ano; definir é botão que abre janela, como no resto da suíte.
/// </summary>
public sealed partial class MetasViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaMeta> Metas { get; } = [];

    [ObservableProperty] private int _ano = DateTime.Today.Year;
    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, tela vazia por erro fica idêntica
    /// a tela vazia por não haver nada.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;

    /// <summary>
    /// A apuração do mês corrente falhou. Terceiro estado obrigatório: lista sem
    /// realizado por consulta quebrada se lê como "não fizemos nada este mês".
    /// </summary>
    [ObservableProperty] private bool _apuracaoNaoVerificada;

    /// <summary>
    /// Metade VISÍVEL da permissão. <b>Definir meta é bit próprio desde a parcela 64</b>:
    /// até aqui a tela pedia <see cref="Permissao.VerIndicadores"/>, que é o bit de LER o
    /// BI — quem recebesse acesso de leitura aos números ganhava junto o poder de criar e
    /// APAGAR o alvo do ano, sem que houvesse como separar os dois. Era o bit
    /// sobrecarregado da parcela 49 outra vez, agora entre ver o fato e decidir sobre ele.
    /// </summary>
    public bool PodeEditar => SessaoUsuario.Atual.Pode(Permissao.DefinirMetas);

    public MetasViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    partial void OnAnoChanged(int value) => _ = CarregarAsync();

    /// <summary>
    /// Número da carga mais recente pedida — descarte de resposta fora de ordem (parcela 50).
    /// Navegar de ano em ano dispara uma leitura por clique; a resposta velha chegando por
    /// último mostraria as metas de um ano que não é o escrito no seletor.
    /// </summary>
    private int _geracaoCarga;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;
            ApuracaoNaoVerificada = false;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<MetaService>();

            var doAno = await servico.DoAnoAsync(Ano);

            // O realizado só é apurado para o MÊS CORRENTE: apurar doze meses seria uma
            // dúzia de consultas pesadas para uma tela de cadastro, e o acompanhamento
            // mês a mês é justamente o que o painel da direção faz.
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            IReadOnlyList<MetaApurada> apuradas = [];
            if (Ano == hoje.Year)
            {
                try
                {
                    apuradas = await servico.ApurarMesAsync(hoje);
                }
                catch (Exception ex)
                {
                    // Chegou tarde: outra carga mais nova já foi pedida.
                    if (geracao != _geracaoCarga) return;

                    NaoVerificado = true;
                    Clinica.Application.Diagnostico.Registrar(
                        "Gerente — apuração das metas do mês falhou", ex);
                    ApuracaoNaoVerificada = true;
                }
            }

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Metas.Clear();
            foreach (var m in doAno)
                Metas.Add(Montar(m, apuradas, hoje));

            var doMes = Metas.Count(m => m.Mes == hoje.Month && Ano == hoje.Year);
            Resumo = Metas.Count == 0
                ? $"Nenhuma meta definida para {Ano}. Sem alvo, o painel mostra variação — "
                  + "não desempenho."
                : $"{Metas.Count} meta(s) em {Ano} · {doMes} para o mês corrente.";
        }
        catch (Exception ex)
        {
            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Clinica.Application.Diagnostico.Registrar("Gerente — metas não puderam ser lidas", ex);
            Erro($"Não foi possível ler as metas: {ex.Message}");
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    private static LinhaMeta Montar(
        MetaMensal m, IReadOnlyList<MetaApurada> apuradas, DateOnly hoje)
    {
        var apurada = apuradas.FirstOrDefault(a =>
            a.Indicador == m.Indicador && a.Mes == m.Mes && a.ProfissionalId == m.ProfissionalId);

        return new LinhaMeta
        {
            MetaId = m.Id,
            Mes = m.Mes,
            Indicador = m.Indicador,
            Periodo = $"{m.Mes:00}/{m.Ano}",
            Rotulo = MetaMensal.Rotular(m.Indicador),
            Alvo = Formatar(m.Indicador, m.Valor),
            Valor = m.Valor,
            Dono = m.Profissional?.Rotulo ?? "Clínica",
            ProfissionalId = m.ProfissionalId,
            // "—" e não "0": meta de mês que ainda não chegou não tem realizado nenhum, e
            // zero faria todo mês futuro parecer fracasso.
            Realizado = apurada?.Realizado is { } r ? Formatar(m.Indicador, r) : "—",
            Situacao = Descrever(apurada, m, hoje),
            Fracao = apurada?.PercentualAtingido is { } p ? Math.Min(p / 100.0, 1.0) : null,
            Atingida = apurada?.Atingida ?? false,
            EmRisco = apurada?.EmRisco ?? false
        };
    }

    private static string Descrever(MetaApurada? apurada, MetaMensal m, DateOnly hoje)
    {
        if (apurada is null)
            return m.Ano > hoje.Year || (m.Ano == hoje.Year && m.Mes > hoje.Month)
                ? "ainda não começou"
                : "não apurado nesta tela";

        if (apurada.Atingida) return "meta batida";

        if (apurada.EmRisco && apurada.DesvioProjetado is { } desvio)
            return $"em risco: no ritmo atual fecha {desvio:0.#}% abaixo";

        return apurada.PercentualAtingido is { } p
            ? $"{p:0.#}% do alvo"
            : "sem base de cálculo";
    }

    private static string Formatar(IndicadorMeta indicador, decimal valor)
        => MetaMensal.Formatar(indicador, valor);

    [RelayCommand]
    private async Task NovaMetaAsync() => await AbrirAsync(null);

    [RelayCommand]
    private async Task EditarAsync(LinhaMeta? linha)
    {
        if (linha is null) return;
        await AbrirAsync(linha);
    }

    private async Task AbrirAsync(LinhaMeta? linha)
    {
        SessaoUsuario.Atual.Exigir(Permissao.DefinirMetas, "definir metas");

        var vm = new MetaEdicaoViewModel(_escopos, Ano, linha);
        var janela = new Janelas.MetaWindow(vm)
        {
            Owner = JanelaDona.Atual()
        };

        if (janela.ShowDialog() != true) return;

        _snackbar.Sucesso("Meta definida — o painel da direção já compara com ela.");
        await CarregarAsync();
    }

    /// <summary>
    /// Apagar a meta é diferente de zerá-la: **meta ausente significa "não decidimos"**,
    /// meta zero significa "decidimos que é zero". Guardar uma meta zerada faria todo mês
    /// não planejado aparecer como alvo batido no painel.
    /// </summary>
    [RelayCommand]
    private async Task ExcluirAsync(LinhaMeta? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.DefinirMetas, "excluir metas");

            if (!_dialogo.ConfirmarPerigo(
                    "Excluir meta",
                    $"Apagar a meta de {linha.Rotulo} de {linha.Periodo}?\n\n"
                    + "O mês passa a não ter alvo — o painel volta a mostrar só a variação "
                    + "contra o mês anterior. Se a intenção é declarar que o alvo é zero, "
                    + "edite o valor em vez de apagar: ausência e zero dizem coisas "
                    + "diferentes.")) return;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<MetaService>();
            await servico.ExcluirAsync(linha.MetaId, SessaoUsuario.Atual.Operador);

            _snackbar.Info("Meta excluída.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — meta não pôde ser excluída", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private void AnoAnterior() => Ano--;

    [RelayCommand]
    private void ProximoAno() => Ano++;

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}

/// <summary>
/// Definir a meta de um mês, na janela.
///
/// A unidade muda com o indicador e a tela DIZ qual é: R$ para faturamento, contagem para
/// sessões, percentual para ocupação. Sem isso, "80" numa caixa vazia tanto pode ser
/// oitenta mil reais quanto oitenta por cento — e o erro só apareceria no painel, com o
/// mês já andando.
/// </summary>
public sealed partial class MetaEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    public sealed record OpcaoIndicador(IndicadorMeta Valor, string Nome);
    public sealed record OpcaoMes(int Valor, string Nome);
    public sealed record OpcaoProfissional(int? Id, string Nome);

    public IReadOnlyList<OpcaoIndicador> Indicadores { get; } =
        Enum.GetValues<IndicadorMeta>()
            .Select(i => new OpcaoIndicador(i, MetaMensal.Rotular(i)))
            .ToList();

    public IReadOnlyList<OpcaoMes> Meses { get; } =
        Enumerable.Range(1, 12)
            .Select(m => new OpcaoMes(m, new DateOnly(2000, m, 1)
                .ToString("MMMM", new System.Globalization.CultureInfo("pt-BR"))))
            .ToList();

    public ObservableCollection<OpcaoProfissional> Profissionais { get; } = [];

    [ObservableProperty] private int _ano;
    [ObservableProperty] private OpcaoMes? _mes;
    [ObservableProperty] private OpcaoIndicador? _indicador;
    [ObservableProperty] private OpcaoProfissional? _profissional;
    [ObservableProperty] private string _valor = string.Empty;
    [ObservableProperty] private string _observacoes = string.Empty;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    public string Titulo { get; }

    /// <summary>A unidade do indicador escolhido, escrita ao lado do campo.</summary>
    public string UnidadeAtual => Indicador is null
        ? string.Empty
        : MetaMensal.Unidade(Indicador.Valor);

    public event Action? Concluido;

    public MetaEdicaoViewModel(IServiceScopeFactory escopos, int ano, LinhaMeta? existente)
    {
        _escopos = escopos;
        Ano = ano;
        Titulo = existente is null ? "Nova meta" : "Editar meta";

        Mes = Meses.FirstOrDefault(m => m.Valor == (existente?.Mes ?? DateTime.Today.Month));
        Indicador = Indicadores.FirstOrDefault(i =>
            i.Valor == (existente?.Indicador ?? IndicadorMeta.Faturamento));
        if (existente is not null)
            Valor = existente.Valor.ToString("0.##");

        _ = CarregarProfissionaisAsync(existente?.ProfissionalId);
    }

    partial void OnIndicadorChanged(OpcaoIndicador? value) => OnPropertyChanged(nameof(UnidadeAtual));

    private async Task CarregarProfissionaisAsync(int? selecionado)
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var equipe = scope.ServiceProvider.GetRequiredService<EquipeService>();

            Profissionais.Clear();
            // A meta da clínica vem primeiro porque é a que a direção define sempre; a do
            // profissional é a exceção que revela quem carrega quem.
            Profissionais.Add(new OpcaoProfissional(null, "Clínica (todos)"));
            foreach (var p in await equipe.ProfissionaisAsync())
                Profissionais.Add(new OpcaoProfissional(p.Id, p.Rotulo));

            Profissional = Profissionais.FirstOrDefault(p => p.Id == selecionado)
                           ?? Profissionais[0];
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Gerente — equipe não pôde ser lida para a meta", ex);
            Profissionais.Clear();
            Profissionais.Add(new OpcaoProfissional(null, "Clínica (todos)"));
            Profissional = Profissionais[0];
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (Mes is null || Indicador is null)
        {
            Erro("Escolha o mês e o indicador.");
            return;
        }

        // Valores.TentarLerDecimal, e não decimal.TryParse: é o leitor do projeto, que
        // aceita "1.250,00" e "1250.00" sem depender da cultura da máquina.
        if (!Valores.TentarLerDecimal(Valor, out var valor))
        {
            Erro("Informe o valor da meta.");
            return;
        }

        try
        {
            Salvando = true;
            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<MetaService>();

            await servico.DefinirAsync(
                Ano, Mes.Valor, Indicador.Valor, valor,
                Profissional?.Id,
                string.IsNullOrWhiteSpace(Observacoes) ? null : Observacoes.Trim(),
                SessaoUsuario.Atual.Operador);

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — meta não pôde ser salva", ex);
            Erro(ex.Message);
        }
        finally
        {
            Salvando = false;
        }
    }

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}
