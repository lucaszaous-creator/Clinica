using System.IO;
using System.Windows.Media.Imaging;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Desktop.Shell.Web;

/// <summary>Foto local e transitória. Confirmar somente devolve a imagem ao cadastro; salvar o cadastro persiste.</summary>
public sealed partial class CapturaFotoWebViewModel(string paciente) : ObservableObject
{
    public string Paciente { get; } = string.IsNullOrWhiteSpace(paciente) ? "Novo cadastro" : paciente;
    public byte[]? Conteudo { get; private set; }
    public byte[]? Miniatura { get; private set; }
    public bool TemFoto => Conteudo is { Length: > 0 };
    public event Action? Confirmado;
    [ObservableProperty] private string _mensagem = "Escolha uma imagem ou capture pela câmera. O recorte será quadrado e centralizado.";
    [ObservableProperty] private bool _mensagemEhErro;
    private string _fotoDataUrl = "";
    public string FotoDataUrl
    {
        get => _fotoDataUrl;
        set
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarPaciente, "capturar foto do paciente");
            if (string.IsNullOrWhiteSpace(value)) { Limpar(); return; }
            const int limite = 8 * 1024 * 1024;
            if(value.Length > (limite + 2) / 3 * 4 + 64) throw new InvalidOperationException("Escolha uma imagem de até 8 MB.");
            var separador = value.IndexOf(',');
            if(separador < 0 || value[..separador] is not ("data:image/jpeg;base64" or "data:image/png;base64" or "data:image/bmp;base64"))
                throw new InvalidOperationException("Escolha uma imagem JPEG, PNG ou BMP.");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(value[(separador+1)..]); }
            catch(FormatException) { throw new InvalidOperationException("A imagem recebida está incompleta. Tente novamente."); }
            if(bytes.Length > limite) throw new InvalidOperationException("Escolha uma imagem de até 8 MB.");
            using(var memoria = new MemoryStream(bytes))
            {
                var decoder = BitmapDecoder.Create(memoria, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                var quadro = decoder.Frames.FirstOrDefault() ?? throw new InvalidOperationException("A imagem está vazia.");
                if(quadro.PixelWidth <= 0 || quadro.PixelHeight <= 0 || (long)quadro.PixelWidth * quadro.PixelHeight > 16_000_000)
                    throw new InvalidOperationException("Escolha uma foto de até 16 megapixels.");
            }
            var preparada = Retrato.Preparar(bytes);
            Conteudo = preparada.Cheia; Miniatura = preparada.Miniatura;
            _fotoDataUrl = "data:image/jpeg;base64," + Convert.ToBase64String(Conteudo);
            Mensagem = "Confira o retrato e clique em Usar foto. A gravação ocorre ao salvar o cadastro.";
            MensagemEhErro = false;
            OnPropertyChanged(); OnPropertyChanged(nameof(TemFoto)); ConfirmarCommand.NotifyCanExecuteChanged();
        }
    }
    [RelayCommand(CanExecute = nameof(TemFoto))]
    private void Confirmar() { SessaoUsuario.Atual.Exigir(Permissao.EditarPaciente,"usar foto do paciente"); if(TemFoto) Confirmado?.Invoke(); }
    [RelayCommand]
    private void Limpar()
    {
        Conteudo = Miniatura = null; _fotoDataUrl = "";
        Mensagem = "Escolha outra imagem ou tire uma nova foto."; MensagemEhErro = false;
        OnPropertyChanged(nameof(FotoDataUrl)); OnPropertyChanged(nameof(TemFoto)); ConfirmarCommand.NotifyCanExecuteChanged();
    }
}
