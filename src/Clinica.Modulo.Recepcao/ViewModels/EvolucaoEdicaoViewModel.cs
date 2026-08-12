using System.Collections.ObjectModel;
using System.IO;
using Clinica.Application.Modelos;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// Registro de uma sessão no prontuário: EVA antes/depois, o que o paciente relatou, o
/// que foi feito, a leitura clínica e as orientações — mais os anexos.
///
/// A EVA aparece como uma régua de 0 a 10 em vez de campo de texto: no balcão a
/// pergunta é feita em voz alta ("de 0 a 10, quanto dói?") e a resposta tem de caber num
/// clique. Digitar número aqui é o caminho mais curto para a medida não ser registrada.
/// </summary>
public sealed partial class EvolucaoEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _pacienteId;
    private int? _evolucaoId;

    public ObservableCollection<Profissional> Profissionais { get; } = [];
    public ObservableCollection<AnexoResumo> Anexos { get; } = [];

    /// <summary>
    /// O mapa corporal desta sessão. Vive junto da evolução porque é a MESMA sessão
    /// vista de outro jeito: o texto diz o que foi feito, o desenho diz onde. Ele é
    /// gravado no Salvar daqui, depois de a evolução existir — o mapa aponta para ela.
    /// </summary>
    public MapaCorporalViewModel Mapa { get; }

    /// <summary>Valores da escala — a régua de 0 a 10 da tela sai daqui.</summary>
    public IReadOnlyList<int> EscalaEva { get; } =
        Enumerable.Range(Evolucao.EvaMinima, Evolucao.EvaMaxima - Evolucao.EvaMinima + 1).ToList();

    [ObservableProperty] private DateTime _data = DateTime.Today;
    [ObservableProperty] private Profissional? _profissional;
    [ObservableProperty] private int? _evaAntes;
    [ObservableProperty] private int? _evaDepois;
    [ObservableProperty] private string? _queixaPrincipal;
    [ObservableProperty] private string? _conduta;
    [ObservableProperty] private string? _textoEvolucao;
    [ObservableProperty] private string? _orientacoes;

    /// <summary>
    /// O roteiro da sessão que se repete (parcela 63). A janela mora no SHELL porque a
    /// evolução é escrita aqui e no Consultório.
    ///
    /// Ela devolve o texto e <b>não grava nada</b>: quem efetiva continua sendo o Salvar
    /// desta tela — a mesma regra de "repetir a sessão anterior" do mapa corporal.
    /// </summary>
    [RelayCommand]
    private void AbrirModelos()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "usar modelos de evolução");

        var vm = new ModelosEvolucaoViewModel(
            _escopos,
            Profissional?.Id ?? SessaoUsuario.Atual.ProfissionalId,
            new ModeloAplicado(QueixaPrincipal, Conduta, TextoEvolucao, Orientacoes));

        var janela = new ModelosEvolucaoWindow(vm)
        {
            Owner = System.Windows.Application.Current?.Windows
                        .OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive)
                    ?? System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() != true || janela.Escolhido is not { } m) return;

        // Só sobrescreve o campo que o modelo TEM. Um modelo que traz conduta e
        // orientações não pode apagar a queixa que a pessoa acabou de digitar ouvindo o
        // paciente — aplicar é preencher o que falta, não zerar a tela.
        if (!string.IsNullOrWhiteSpace(m.QueixaPrincipal)) QueixaPrincipal = m.QueixaPrincipal;
        if (!string.IsNullOrWhiteSpace(m.Conduta)) Conduta = m.Conduta;
        if (!string.IsNullOrWhiteSpace(m.TextoEvolucao)) TextoEvolucao = m.TextoEvolucao;
        if (!string.IsNullOrWhiteSpace(m.Orientacoes)) Orientacoes = m.Orientacoes;

        Mensagem = "Modelo aplicado. Confira o texto e salve a sessão — nada foi gravado ainda.";
        MensagemEhErro = false;
    }

    [ObservableProperty] private string _titulo = "Nova sessão no prontuário";
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    /// <summary>
    /// Anexar só é possível depois de a sessão existir: o anexo aponta para a evolução,
    /// e num registro novo o Id ainda não nasceu.
    /// </summary>
    public bool PodeAnexar => _evolucaoId is not null;

    /// <summary>Quanto a dor caiu, calculado enquanto o usuário mexe na régua.</summary>
    public string VariacaoRotulo => (EvaAntes, EvaDepois) switch
    {
        (null, _) or (_, null) => "Meça antes e depois para saber se aliviou.",
        var (a, d) when a > d => $"Aliviou {a - d} ponto(s) nesta sessão.",
        var (a, d) when a < d => $"Piorou {d - a} ponto(s) nesta sessão.",
        _ => "Sem mudança nesta sessão."
    };

    public event Action? Concluido;

    public EvolucaoEdicaoViewModel(IServiceScopeFactory escopos, int pacienteId, int? evolucaoId = null)
    {
        _escopos = escopos;
        _pacienteId = pacienteId;
        _evolucaoId = evolucaoId;
        Mapa = new MapaCorporalViewModel(escopos, pacienteId, evolucaoId);
        _ = CarregarAsync();
    }

    partial void OnEvaAntesChanged(int? value) => OnPropertyChanged(nameof(VariacaoRotulo));
    partial void OnEvaDepoisChanged(int? value) => OnPropertyChanged(nameof(VariacaoRotulo));

    private async Task CarregarAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var equipe = scope.ServiceProvider.GetRequiredService<EquipeService>();

            Profissionais.Clear();
            foreach (var p in await equipe.ProfissionaisAtivosAsync()) Profissionais.Add(p);

            // O mapa carrega sempre: numa sessão nova ele traz os protocolos, que são
            // justamente o que evita começar o desenho do zero.
            await Mapa.CarregarAsync();

            if (_evolucaoId is null) return;

            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();
            var evolucao = await prontuario.ObterAsync(_evolucaoId.Value);
            if (evolucao is null) return;

            Titulo = "Editar sessão do prontuário";
            Data = evolucao.Data.ToDateTime(TimeOnly.MinValue);
            Profissional = Profissionais.FirstOrDefault(p => p.Id == evolucao.ProfissionalId);
            EvaAntes = evolucao.EvaAntes;
            EvaDepois = evolucao.EvaDepois;
            QueixaPrincipal = evolucao.QueixaPrincipal;
            Conduta = evolucao.Conduta;
            TextoEvolucao = evolucao.TextoEvolucao;
            Orientacoes = evolucao.Orientacoes;

            await RecarregarAnexosAsync(prontuario);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — sessão do prontuário não pôde ser aberta", ex);
            Erro($"Não foi possível carregar a sessão: {ex.Message}");
        }
    }

    private async Task RecarregarAnexosAsync(ProntuarioService prontuario)
    {
        Anexos.Clear();
        if (_evolucaoId is null) return;

        foreach (var a in await prontuario.AnexosAsync(_evolucaoId.Value))
            Anexos.Add(a);
    }

    [RelayCommand]
    private void LimparEvaAntes() => EvaAntes = null;

    [RelayCommand]
    private void LimparEvaDepois() => EvaDepois = null;

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = string.Empty;
        MensagemEhErro = false;

        try
        {
            Salvando = true;
            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();

            var salva = await prontuario.SalvarAsync(new Evolucao
            {
                Id = _evolucaoId ?? 0,
                PacienteId = _pacienteId,
                ProfissionalId = Profissional?.Id,
                Data = DateOnly.FromDateTime(Data),
                EvaAntes = EvaAntes,
                EvaDepois = EvaDepois,
                QueixaPrincipal = QueixaPrincipal,
                Conduta = Conduta,
                TextoEvolucao = TextoEvolucao,
                Orientacoes = Orientacoes
            }, SessaoUsuario.Atual.Operador);

            _evolucaoId = salva.Id;

            // O mapa vem depois de propósito: ele aponta para a evolução, e numa sessão
            // nova o Id só nasce agora. A falha DELE tem mensagem própria — a sessão já
            // está gravada, e dizer "não foi possível salvar" faria a secretária digitar
            // tudo de novo por causa do desenho.
            try
            {
                await Mapa.SalvarAsync(salva.Id);
            }
            catch (Exception ex)
            {
                Clinica.Application.Diagnostico.Registrar(
                    "Recepção — mapa corporal não pôde ser salvo", ex);
                Erro($"A sessão foi salva, mas o mapa corporal não: {ex.Message}");
                return;
            }

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — sessão do prontuário não pôde ser salva", ex);
            Erro(ex.Message);
        }
        finally
        {
            Salvando = false;
        }
    }

    /// <summary>Anexa um arquivo (foto da região, laudo, exame) à sessão.</summary>
    [RelayCommand]
    private async Task AnexarAsync()
    {
        if (_evolucaoId is null)
        {
            Erro("Salve a sessão antes de anexar arquivos.");
            return;
        }

        var dialogo = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Anexar ao prontuário",
            Filter = "Imagens e documentos (*.jpg;*.jpeg;*.png;*.pdf)|*.jpg;*.jpeg;*.png;*.pdf"
                     + "|Todos os arquivos (*.*)|*.*"
        };
        if (dialogo.ShowDialog() != true) return;

        try
        {
            var bytes = await File.ReadAllBytesAsync(dialogo.FileName);
            var extensao = Path.GetExtension(dialogo.FileName).ToLowerInvariant();
            var ehImagem = extensao is ".jpg" or ".jpeg" or ".png" or ".bmp";

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();

            await prontuario.AnexarAsync(
                _evolucaoId.Value, Path.GetFileName(dialogo.FileName), bytes,
                ehImagem ? TipoAnexo.Imagem : TipoAnexo.Documento,
                tipoConteudo: ehImagem ? $"image/{extensao.TrimStart('.')}" : null,
                operador: SessaoUsuario.Atual.Operador);

            await RecarregarAnexosAsync(prontuario);
            Mensagem = "Arquivo anexado.";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — anexo não pôde ser gravado", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>Salva o anexo em disco para abrir no programa padrão do Windows.</summary>
    [RelayCommand]
    private async Task BaixarAnexoAsync(AnexoResumo? anexo)
    {
        if (anexo is null) return;

        var dialogo = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Salvar anexo",
            FileName = anexo.NomeArquivo
        };
        if (dialogo.ShowDialog() != true) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();
            var bytes = await prontuario.ConteudoAnexoAsync(anexo.Id);

            // TRILHA DE LEITURA (parcela 62): laudo e imagem de exame saindo para o disco
            // é dado de saúde deixando o sistema — o que uma investigação procura primeiro.
            // A tela irmã do Consultório (AnexosSessaoViewModel) já registrava; esta não.
            await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                .RegistrarAsync(_pacienteId, SessaoUsuario.Atual.Operador,
                    OrigemAcessoProntuario.ExportacaoClinica);

            if (bytes is null)
            {
                Erro("O arquivo não foi encontrado no banco.");
                return;
            }

            await File.WriteAllBytesAsync(dialogo.FileName, bytes);
            Mensagem = $"Anexo salvo em {dialogo.FileName}.";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — anexo não pôde ser salvo em disco", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task RemoverAnexoAsync(AnexoResumo? anexo)
    {
        if (anexo is null) return;

        // Retirar, nunca apagar (parcela 52): o laudo que sustentou uma conduta é parte da
        // prova de que ela era razoável, e a guarda de 20 anos não admite que um clique o
        // destrua.
        using var escopoDialogo = _escopos.CreateScope();
        var motivo = escopoDialogo.ServiceProvider.GetRequiredService<IDialogoService>().PerguntarTexto(
            "Retirar anexo",
            $"Por que \"{anexo.NomeArquivo}\" está saindo do prontuário? O arquivo NÃO é "
            + "apagado — sai da lista e fica guardado, com este motivo.");
        if (string.IsNullOrWhiteSpace(motivo)) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();
            await prontuario.CancelarAnexoAsync(anexo.Id, motivo, SessaoUsuario.Atual.Operador);
            await RecarregarAnexosAsync(prontuario);
            Mensagem = "Anexo retirado (guardado no prontuário).";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — anexo não pôde ser removido", ex);
            Erro(ex.Message);
        }
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}
