using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
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

namespace Clinica.Gerente.ViewModels;

/// <summary>Uma guia pendente na visão da direção, com o semáforo já resolvido.</summary>
public sealed class LinhaPendencia
{
    public required int CodigoId { get; init; }
    public required string Paciente { get; init; }
    public required string Convenio { get; init; }
    public required string Guia { get; init; }
    public required string Prevista { get; init; }
    public required string Atraso { get; init; }
    public required string Observacao { get; init; }

    /// <summary>Semáforo: vermelho = atrasada, amarelo = vence hoje/amanhã, verde = em dia.</summary>
    public required NivelUrgencia Urgencia { get; init; }

    public bool EhVermelha => Urgencia == NivelUrgencia.Vermelho;
    public bool EhAmarela => Urgencia == NivelUrgencia.Amarelo;
    public bool EhVerde => Urgencia == NivelUrgencia.Verde;

    /// <summary>
    /// A FORMA que o número da guia deste convênio tem (parcela 45). A linha descartava
    /// o código do convênio ao virar nome de exibição, e a fila do Gerente ficou sendo a
    /// única porta de baixa sem a metade que explica — a gerente digitava "9O123" (letra
    /// O), o serviço recusava, e a recusa chegava genérica com o diálogo já fechado.
    /// </summary>
    public required FormatoNumeroGuia FormatoGuia { get; init; }

    public static LinhaPendencia De(PendenciaCodigo p) => new()
    {
        CodigoId = p.CodigoId,
        Paciente = p.PacienteNome,
        Convenio = CatalogoConvenios.Nome(p.ConvenioCodigo, p.Convenio),
        FormatoGuia = CatalogoConvenios.FormatoDoNumeroDaGuia(
            p.ConvenioCodigo ?? p.Convenio.ToString()),
        Guia = $"{RotulosEnum.De(p.Tipo)} · {RotulosEnum.De(p.Ordem)}",
        Prevista = p.DataPrevista.ToString("dd/MM/yyyy"),
        Urgencia = p.Urgencia,
        Atraso = p.DiasEmAtraso switch
        {
            < 0 => $"vence em {-p.DiasEmAtraso} dia(s)",
            0 => "vence hoje",
            var d => $"{d} dia(s) em atraso"
        },
        Observacao = p.ObservacaoPendencia ?? string.Empty
    };
}

/// <summary>
/// Pendências de guias na visão da DIREÇÃO — o mesmo semáforo do painel do faturamento,
/// dentro do Gerente.
///
/// Por que existe: o Gerente Geral deve ter as features dos quatro módulos somados, e o
/// faturamento é o único cuja tela ele não tinha — só a leitura consolidada. Quem dirige
/// a clínica precisa ver a guia que está vencendo sem ir até o posto do faturamento, que
/// é onde o app congelado roda.
///
/// Ela NÃO é uma segunda via do app de faturamento: trabalha sobre os MESMOS serviços
/// compartilhados (<see cref="PendenciaService"/>, <see cref="FaturamentoService"/>), que
/// já carregam as regras e a auditoria. Nenhuma linha de `Clinica.Desktop` é tocada.
///
/// A BAIXA é permitida daqui porque é o ato que destrava a guia e não cria nada: dois
/// postos dando baixa na mesma guia disputam uma linha, e a concorrência otimista (xmin)
/// resolve mostrando a mensagem em vez de sobrescrever em silêncio.
/// </summary>
public sealed partial class PendenciasGerencialViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaPendencia> Pendencias { get; } = [];

    /// <summary>Tudo o que o banco devolveu; <see cref="Pendencias"/> é o recorte do filtro.</summary>
    private readonly List<LinhaPendencia> _todas = [];

    // ---- Filtro (o espelho do painel do faturamento, que filtra por convênio e
    // urgência desde a parcela 1 — a MESMA leitura no Gerente não tinha como estreitar).
    // Em memória, sobre o que já foi lido.
    public const string TodosConvenios = "Todos os convênios";
    public const string TodasUrgencias = "Todas";
    public const string UrgenciaVermelhas = "Vermelhas";
    public const string UrgenciaAmarelas = "Amarelas";
    public const string UrgenciaVerdes = "Verdes";

    // Strings, e não o enum NivelUrgencia: ComboBox amarrado a enum sem rotulador escreve
    // o identificador na tela (o defeito da parcela 41, vigiado pela checagem 20).
    public string[] OpcoesUrgenciaGuia { get; } =
        [TodasUrgencias, UrgenciaVermelhas, UrgenciaAmarelas, UrgenciaVerdes];

    /// <summary>As operadoras COM pendência — oferecer as sem guia daria filtro que só leva a vazio.</summary>
    public ObservableCollection<string> ConveniosPendencia { get; } = [TodosConvenios];

    [ObservableProperty] private string _filtroConvenioGuia = TodosConvenios;
    [ObservableProperty] private string _filtroUrgenciaGuia = TodasUrgencias;
    [ObservableProperty] private string _filtroPacienteGuia = string.Empty;

    /// <summary>O `Clear()` do combo devolve nulo pelo binding (lição da parcela 56) — remonta sob guarda.</summary>
    private bool _montandoConvenios;

    partial void OnFiltroUrgenciaGuiaChanged(string value) => Refiltrar();
    partial void OnFiltroPacienteGuiaChanged(string value) => Refiltrar();
    partial void OnFiltroConvenioGuiaChanged(string value)
    {
        if (value is null)
        {
            FiltroConvenioGuia = TodosConvenios;
            return;
        }
        if (!_montandoConvenios) Refiltrar();
    }

    public bool FiltroAtivo =>
        FiltroConvenioGuia != TodosConvenios
        || FiltroUrgenciaGuia != TodasUrgencias
        || !string.IsNullOrWhiteSpace(FiltroPacienteGuia);

    [RelayCommand]
    private void LimparFiltro()
    {
        FiltroConvenioGuia = TodosConvenios;
        FiltroUrgenciaGuia = TodasUrgencias;
        FiltroPacienteGuia = string.Empty;
    }

    /// <summary>O estado vazio muda de frase quando há filtro — vazio filtrado não é "tudo em dia".</summary>
    [ObservableProperty] private string _vazioDescricao =
        "O 2º código só vira pendência 24 h depois do atendimento.";

    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, tela vazia por erro fica idêntica
    /// a tela vazia por não haver nada.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    [ObservableProperty] private string _vermelhas = "—";
    [ObservableProperty] private string _amarelas = "—";
    [ObservableProperty] private string _verdes = "—";

    /// <summary>
    /// Metade VISÍVEL da permissão. Ver pendência é VerFaturamento; DAR BAIXA é escrita, e
    /// desde a parcela 45 tem bit próprio (<see cref="Permissao.BaixarGuia"/>).
    ///
    /// A tela do Gerente tinha de mudar junto: enquanto ela pedisse VerFaturamento, a
    /// direção negaria "Dar baixa em guia" a alguém, essa pessoa continuaria baixando por
    /// aqui, e a permissão nova seria só uma caixinha na tela de Acessos.
    /// </summary>
    public bool PodeBaixar => SessaoUsuario.Atual.Pode(Permissao.BaixarGuia);

    public PendenciasGerencialViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var pendencias = scope.ServiceProvider.GetRequiredService<PendenciaService>();

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var lista = await pendencias.CodigosPendentesAsync(hoje);

            _todas.Clear();
            foreach (var p in lista.OrderByDescending(p => p.DiasEmAtraso))
                _todas.Add(LinhaPendencia.De(p));

            // As operadoras com pendência, preservando a escolha quando ela ainda
            // existe — atualizar não pode desfazer o filtro de quem está trabalhando.
            var escolhido = FiltroConvenioGuia;
            _montandoConvenios = true;
            try
            {
                ConveniosPendencia.Clear();
                ConveniosPendencia.Add(TodosConvenios);
                foreach (var nome in _todas.Select(l => l.Convenio).Distinct().OrderBy(n => n))
                    ConveniosPendencia.Add(nome);
                FiltroConvenioGuia = ConveniosPendencia.Contains(escolhido)
                    ? escolhido : TodosConvenios;
            }
            finally
            {
                _montandoConvenios = false;
            }

            Refiltrar();
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar(
                "Gerente — pendências não puderam ser lidas", ex);
            Erro($"Não foi possível ler as pendências: {ex.Message}");
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>
    /// Aplica o filtro sobre o que já foi lido — em memória, sem ida ao banco (o padrão
    /// da tela de Consultas da Recepção).
    /// </summary>
    private void Refiltrar()
    {
        Pendencias.Clear();
        foreach (var p in _todas.Where(p =>
                     (FiltroConvenioGuia == TodosConvenios || p.Convenio == FiltroConvenioGuia)
                     && (FiltroUrgenciaGuia switch
                     {
                         UrgenciaVermelhas => p.EhVermelha,
                         UrgenciaAmarelas => p.EhAmarela,
                         UrgenciaVerdes => p.EhVerde,
                         _ => true
                     })
                     && Busca.Casa(p.Paciente, FiltroPacienteGuia)))
            Pendencias.Add(p);

        OnPropertyChanged(nameof(FiltroAtivo));

        // O semáforo conta o TOTAL, nunca o recorte: o filtro serve para achar uma guia,
        // não para mudar o tamanho do problema que a direção enxerga.
        Vermelhas = _todas.Count(p => p.EhVermelha).ToString();
        Amarelas = _todas.Count(p => p.EhAmarela).ToString();
        Verdes = _todas.Count(p => p.EhVerde).ToString();

        // O resumo DIZ que está filtrado: "5 guias" e "5 de 40 no filtro" respondem
        // perguntas diferentes.
        Resumo = _todas.Count == 0
            ? "Nenhuma guia pendente. É o estado que o produto existe para manter."
            : FiltroAtivo
                ? $"{Pendencias.Count} de {_todas.Count} guia(s) pendente(s) no filtro."
                : $"{Pendencias.Count} guia(s) pendente(s) de baixa.";

        VazioDescricao = FiltroAtivo
            ? "Nenhuma pendência bate com o filtro — limpe-o para ver todas."
            : "O 2º código só vira pendência 24 h depois do atendimento.";
    }

    /// <summary>
    /// Baixa: a secretária efetivou a guia no sistema do convênio. Pede o número real da
    /// guia porque é ele que casa o retorno da operadora com a linha daqui — sem número,
    /// a conciliação do demonstrativo vira trabalho manual.
    /// </summary>
    [RelayCommand]
    private async Task BaixarAsync(LinhaPendencia? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.BaixarGuia, "dar baixa na guia");

            // A dica do formato entra na pergunta e a crítica roda ANTES de chamar o
            // serviço — é a MESMA RegraNumeroGuia, nunca uma cópia. As outras três portas
            // já avisavam; esta recusava só no serviço, com o diálogo já fechado.
            var dica = RegraNumeroGuia.Dica(linha.FormatoGuia);
            var pergunta = $"Número da guia de {linha.Paciente} no sistema do convênio. "
                           + "É por ele que o retorno da operadora casa com esta linha — sem número, "
                           + "conciliar o demonstrativo vira trabalho manual."
                           + (dica is null ? string.Empty : $"\n\n{linha.Convenio}: {dica}");

            string? numero;
            while (true)
            {
                numero = _dialogo.PerguntarTexto("Dar baixa", pergunta);

                // Cancelar o diálogo devolve null; string vazia é "não tenho o número
                // agora", que é diferente e continua permitindo a baixa.
                if (numero is null) return;

                if (RegraNumeroGuia.Criticar(numero, linha.FormatoGuia) is not { } critica) break;

                // O "O" no lugar do zero, apontado com o número ainda na mão — e a
                // pergunta reabre para corrigir, em vez de a recusa chegar depois.
                _dialogo.Aviso("Número da guia", critica);
            }

            using var scope = _escopos.CreateScope();
            var faturamento = scope.ServiceProvider.GetRequiredService<FaturamentoService>();

            await faturamento.DarBaixaAsync(
                linha.CodigoId, DateOnly.FromDateTime(DateTime.Today),
                string.IsNullOrWhiteSpace(numero) ? null : numero.Trim(),
                SessaoUsuario.Atual.Operador, observacao: null);

            _snackbar.Sucesso($"Guia de {linha.Paciente} baixada.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — baixa não pôde ser registrada", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>
    /// Anota por que a guia ainda não baixou. Não dá baixa: registra a situação, para a
    /// próxima pessoa que olhar a lista não recomeçar a investigação do zero.
    /// </summary>
    [RelayCommand]
    private async Task AnotarAsync(LinhaPendencia? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.VerFaturamento, "anotar a pendência");

            var texto = _dialogo.PerguntarTexto(
                "Anotar pendência",
                $"Por que a guia de {linha.Paciente} ainda não foi baixada?",
                linha.Observacao);
            if (texto is null) return;

            using var scope = _escopos.CreateScope();
            var faturamento = scope.ServiceProvider.GetRequiredService<FaturamentoService>();

            await faturamento.RegistrarObservacaoPendenciaAsync(
                linha.CodigoId, texto, SessaoUsuario.Atual.Operador);

            _snackbar.Info("Anotação registrada.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — anotação não pôde ser salva", ex);
            Erro(ex.Message);
        }
    }

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}
