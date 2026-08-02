using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Um ponto marcado, do jeito que a tela precisa: as coordenadas normalizadas do
/// domínio (0 a 1) mais a posição em PIXEL dentro da figura.
///
/// A conversão mora aqui, e não num conversor de binding, porque o desenho tem tamanho
/// fixo: a figura é a mesma para todo mundo, e calcular no item deixa o XAML sem
/// MultiBinding para posicionar cada bolinha.
/// </summary>
public sealed partial class PontoMapaItem : ObservableObject
{
    /// <summary>Tamanho da figura na tela, em pixels. O XAML usa os mesmos números.</summary>
    public const double LarguraFigura = 220;
    public const double AlturaFigura = 460;

    /// <summary>Raio da bolinha — a marcação fica centrada no clique, não ao lado dele.</summary>
    public const double RaioMarcador = 10;

    public required FaceCorpo Face { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }

    [ObservableProperty] private string? _nome;
    [ObservableProperty] private TecnicaPonto _tecnica;
    [ObservableProperty] private string? _observacao;
    [ObservableProperty] private int _numero;

    public double Esquerda => X * LarguraFigura - RaioMarcador;
    public double Topo => Y * AlturaFigura - RaioMarcador;

    /// <summary>Rótulo da lista: "3 · IG4 (Agulha)".</summary>
    public string Rotulo => string.IsNullOrWhiteSpace(Nome)
        ? $"{Numero} · {FaceRotulo} · {Tecnica}"
        : $"{Numero} · {Nome} · {FaceRotulo} · {Tecnica}";

    public string FaceRotulo => Face == FaceCorpo.Frente ? "frente" : "costas";

    partial void OnNomeChanged(string? value) => OnPropertyChanged(nameof(Rotulo));
    partial void OnTecnicaChanged(TecnicaPonto value) => OnPropertyChanged(nameof(Rotulo));
    partial void OnNumeroChanged(int value) => OnPropertyChanged(nameof(Rotulo));
}

/// <summary>
/// O mapa corporal da sessão (feature 06): onde as agulhas foram aplicadas, marcado
/// clicando na figura.
///
/// Mora no SHELL desde a parcela 36, e não no módulo da Recepção que o criou. A razão é a
/// mesma do <see cref="SeletorPacienteViewModel"/>: o mapa é da acupuntura, que é a
/// especialidade da casa, e quem mais o usa é quem atende — só que nenhum módulo conhece
/// os outros, então o Consultório não teria como reaproveitá-lo de lá. As alternativas
/// eram copiar (duas versões da mesma figura, divergindo na primeira correção) ou deixar
/// o app do médico sem a ferramenta central dele.
///
/// Tela nova da suíte que marca ponto no corpo usa ESTE componente.
///
/// Ele NÃO grava sozinho. Os pontos vivem na tela até a sessão ser salva — inclusive os
/// que vieram de "repetir a sessão anterior" ou de um protocolo. Gravar no clique do
/// "repetir" deixaria no prontuário um mapa que ninguém confirmou, e prontuário não é
/// rascunho.
/// </summary>
public sealed partial class MapaCorporalViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _pacienteId;
    private int? _evolucaoId;

    public ObservableCollection<PontoMapaItem> Pontos { get; } = [];
    public ObservableCollection<PontoMapaItem> PontosFrente { get; } = [];
    public ObservableCollection<PontoMapaItem> PontosCostas { get; } = [];
    public ObservableCollection<ProtocoloCorporal> Protocolos { get; } = [];

    /// <summary>Técnicas oferecidas ao marcar — a escolhida vale para o próximo clique.</summary>
    public IReadOnlyList<TecnicaPonto> Tecnicas { get; } = Enum.GetValues<TecnicaPonto>();

    [ObservableProperty] private TecnicaPonto _tecnicaSelecionada = TecnicaPonto.Agulha;
    [ObservableProperty] private string? _nomeProximoPonto;
    [ObservableProperty] private string? _observacoes;
    [ObservableProperty] private ProtocoloCorporal? _protocoloSelecionado;
    [ObservableProperty] private bool _protocoloDaClinica;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;

    public string Resumo => Pontos.Count == 0
        ? "Nenhum ponto marcado. Clique na figura para marcar."
        : $"{Pontos.Count} ponto(s) — {PontosFrente.Count} de frente, {PontosCostas.Count} de costas.";

    /// <summary>O aviso do mapa aparece tanto para erro quanto para confirmação.</summary>
    public bool TemMensagem => !string.IsNullOrWhiteSpace(Mensagem);

    partial void OnMensagemChanged(string value) => OnPropertyChanged(nameof(TemMensagem));

    public MapaCorporalViewModel(IServiceScopeFactory escopos, int pacienteId, int? evolucaoId)
    {
        _escopos = escopos;
        _pacienteId = pacienteId;
        _evolucaoId = evolucaoId;
    }

    /// <summary>Carrega o mapa já gravado (se houver) e os protocolos disponíveis.</summary>
    public async Task CarregarAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var mapas = scope.ServiceProvider.GetRequiredService<MapaCorporalService>();

            Protocolos.Clear();
            foreach (var p in await mapas.ProtocolosAsync(_pacienteId))
                Protocolos.Add(p);

            if (_evolucaoId is null) return;

            var mapa = await mapas.DaEvolucaoAsync(_evolucaoId.Value);
            if (mapa is null) return;

            Observacoes = mapa.Observacoes;
            Substituir(mapa.Pontos.OrderBy(p => p.Ordem).ThenBy(p => p.Id));
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Mapa corporal não pôde ser carregado", ex);
            Erro($"Não foi possível carregar o mapa: {ex.Message}");
        }
    }

    /// <summary>
    /// Marca um ponto onde o profissional clicou. As coordenadas chegam normalizadas
    /// (0 a 1) — quem converte o pixel do clique é a tela, que é quem conhece o tamanho
    /// do desenho.
    /// </summary>
    public void Marcar(FaceCorpo face, double x, double y)
    {
        if (!MapaCorporal.CoordenadaValida(x) || !MapaCorporal.CoordenadaValida(y)) return;

        if (Pontos.Count >= MapaCorporal.MaximoPontos)
        {
            Erro($"O mapa aceita até {MapaCorporal.MaximoPontos} pontos.");
            return;
        }

        Pontos.Add(new PontoMapaItem
        {
            Face = face,
            X = x,
            Y = y,
            Nome = string.IsNullOrWhiteSpace(NomeProximoPonto) ? null : NomeProximoPonto!.Trim(),
            Tecnica = TecnicaSelecionada
        });

        // O nome é do ponto que acabou de ser marcado: deixá-lo no campo repetiria
        // "IG4" no próximo clique sem ninguém pedir.
        NomeProximoPonto = null;
        Reindexar();
    }

    [RelayCommand]
    private void RemoverPonto(PontoMapaItem? ponto)
    {
        if (ponto is null) return;
        Pontos.Remove(ponto);
        Reindexar();
    }

    [RelayCommand]
    private void Limpar()
    {
        Pontos.Clear();
        Reindexar();
    }

    /// <summary>Traz os pontos da última sessão que tinha mapa — sem gravar nada.</summary>
    [RelayCommand]
    private async Task RepetirAnteriorAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var mapas = scope.ServiceProvider.GetRequiredService<MapaCorporalService>();
            var pontos = await mapas.PontosDaSessaoAnteriorAsync(_pacienteId, _evolucaoId);

            if (pontos.Count == 0)
            {
                Erro("Nenhuma sessão anterior deste paciente tem mapa para repetir.");
                return;
            }

            Substituir(pontos);
            Informar($"{pontos.Count} ponto(s) trazidos da sessão anterior. Ajuste e salve a sessão.");
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Mapa da sessão anterior não pôde ser lido", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>Aplica o protocolo escolhido, substituindo o que estiver marcado.</summary>
    [RelayCommand]
    private void AplicarProtocolo()
    {
        if (ProtocoloSelecionado is not { } protocolo) return;

        var pontos = MapaCorporalService.PontosDoProtocolo(protocolo);
        if (pontos.Count == 0)
        {
            Erro("Este protocolo não tem nenhum ponto marcado.");
            return;
        }

        Substituir(pontos);
        Informar($"Protocolo \"{protocolo.Nome}\" aplicado. Ajuste e salve a sessão.");
    }

    /// <summary>
    /// Apaga o protocolo escolhido.
    ///
    /// Protocolo não é prontuário: é um atalho para marcar pontos, e por isso se apaga
    /// mesmo. As SESSÕES já salvas com ele não mudam uma vírgula — aplicar um protocolo
    /// COPIA os pontos, nunca aponta para ele. Se fosse referência, apagar aqui esvaziaria
    /// o mapa de sessões passadas, que é registro do que aconteceu.
    ///
    /// Sem esta porta a lista só crescia: protocolo criado com o nome errado, ou a
    /// combinação que a clínica deixou de usar, ficava no combo para sempre.
    /// </summary>
    [RelayCommand]
    private async Task ExcluirProtocoloAsync()
    {
        if (ProtocoloSelecionado is not { } protocolo) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var dialogo = scope.ServiceProvider.GetRequiredService<IDialogoService>();

            // Apagar é destrutivo e o botão fica ao lado do "Aplicar": sem a pergunta,
            // um clique errado leva embora o protocolo da clínica inteira.
            if (!dialogo.ConfirmarPerigo("Apagar protocolo",
                    $"Apagar o protocolo \"{protocolo.Nome}\"? "
                    + "As sessões já salvas com ele NÃO mudam — os pontos foram copiados para cada uma."))
                return;

            var mapas = scope.ServiceProvider.GetRequiredService<MapaCorporalService>();
            await mapas.ExcluirProtocoloAsync(protocolo.Id, SessaoUsuario.Atual.Operador);

            Protocolos.Remove(protocolo);
            ProtocoloSelecionado = null;
            Informar($"Protocolo \"{protocolo.Nome}\" apagado. As sessões já salvas com ele não mudam.");
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Protocolo corporal não pôde ser apagado", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>Guarda o que está desenhado como protocolo reutilizável.</summary>
    [RelayCommand]
    private async Task SalvarComoProtocoloAsync(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            Erro("Dê um nome ao protocolo antes de guardá-lo.");
            return;
        }

        if (Pontos.Count == 0)
        {
            Erro("Marque ao menos um ponto antes de guardar o protocolo.");
            return;
        }

        try
        {
            using var scope = _escopos.CreateScope();
            var mapas = scope.ServiceProvider.GetRequiredService<MapaCorporalService>();

            var protocolo = await mapas.SalvarComoProtocoloAsync(
                _pacienteId, nome!, ParaDominio(), ProtocoloDaClinica,
                descricao: null, operador: SessaoUsuario.Atual.Operador);

            Protocolos.Add(protocolo);
            ProtocoloSelecionado = protocolo;
            Informar(ProtocoloDaClinica
                ? $"Protocolo \"{protocolo.Nome}\" guardado para toda a clínica."
                : $"Protocolo \"{protocolo.Nome}\" guardado para este paciente.");
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Protocolo corporal não pôde ser guardado", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>
    /// Grava o mapa da sessão. Chamado pelo salvamento da evolução, que é quem sabe o
    /// Id — o mapa não existe sem a sessão a que pertence.
    /// </summary>
    public async Task SalvarAsync(int evolucaoId)
    {
        _evolucaoId = evolucaoId;

        using var scope = _escopos.CreateScope();
        var mapas = scope.ServiceProvider.GetRequiredService<MapaCorporalService>();

        // Mapa vazio em sessão que nunca teve mapa não vira registro; sessão que tinha
        // e foi esvaziada precisa gravar o vazio, senão o desenho antigo ressuscita.
        if (Pontos.Count == 0 && await mapas.DaEvolucaoAsync(evolucaoId) is null) return;

        await mapas.SalvarAsync(
            evolucaoId, ParaDominio(), Observacoes, SessaoUsuario.Atual.Operador);
    }

    private IReadOnlyList<PontoMapa> ParaDominio()
        => Pontos.Select(p => new PontoMapa
        {
            Face = p.Face,
            X = p.X,
            Y = p.Y,
            Nome = p.Nome,
            Tecnica = p.Tecnica,
            Observacao = p.Observacao
        }).ToList();

    private void Substituir(IEnumerable<PontoMapa> pontos)
    {
        Pontos.Clear();
        foreach (var p in pontos)
            Pontos.Add(new PontoMapaItem
            {
                Face = p.Face,
                X = p.X,
                Y = p.Y,
                Nome = p.Nome,
                Tecnica = p.Tecnica,
                Observacao = p.Observacao
            });

        Reindexar();
    }

    /// <summary>Renumera as bolinhas e reparte por face — a ordem é a de marcação.</summary>
    private void Reindexar()
    {
        var numero = 1;
        foreach (var p in Pontos) p.Numero = numero++;

        PontosFrente.Clear();
        PontosCostas.Clear();
        foreach (var p in Pontos)
            (p.Face == FaceCorpo.Frente ? PontosFrente : PontosCostas).Add(p);

        OnPropertyChanged(nameof(Resumo));
    }

    private void Informar(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = false;
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}
