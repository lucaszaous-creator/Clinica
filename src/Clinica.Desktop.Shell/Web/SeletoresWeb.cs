using Clinica.Application.Assinatura;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Web;

public sealed partial class EscolherPacienteWebViewModel : ObservableObject
{
    public string Titulo { get; }
    public SeletorPacienteViewModel Seletor { get; }
    public event Action? Concluido;
    public EscolherPacienteWebViewModel(string titulo,IServiceScopeFactory escopos)
    { Titulo=titulo;Seletor=new(escopos); }
    [RelayCommand] private void Confirmar() { if(Seletor.Selecionado is not null)Concluido?.Invoke(); }
}

/// <summary>Adapta somente a apresentação do importador A1. A validação e assinatura existentes são reutilizadas.</summary>
public sealed partial class CertificadoWebViewModel : ObservableObject
{
    public EscolherCertificadoViewModel Certificado { get; }
    public string Titulo => Certificado.Assunto;
    private string? _arquivo;
    [ObservableProperty] private string _nomeArquivo="Nenhum arquivo selecionado";
    [ObservableProperty] private string _senha="";
    public bool PodeFechar => !Certificado.Autorizando;
    public event Action? Concluido;
    public CertificadoWebViewModel(string assunto,IServiceScopeFactory? escopos)
    { Certificado=new(assunto,escopos);Certificado.Fechar=()=>Concluido?.Invoke(); }
    [RelayCommand] private void EscolherArquivo()
    {
        if(!PodeFechar)return;
        var seletor=new Microsoft.Win32.OpenFileDialog{Filter="Certificado A1 (*.pfx;*.p12)|*.pfx;*.p12",CheckFileExists=true};
        if(seletor.ShowDialog()!=true)return;
        _arquivo=seletor.FileName;NomeArquivo=System.IO.Path.GetFileName(_arquivo);Senha="";Certificado.Selecionado=null;
    }
    [RelayCommand] private async Task CarregarAsync()
    {
        if(!PodeFechar)return;byte[]? bytes=null;
        try
        {
            if(_arquivo is null)throw new InvalidOperationException("Escolha o arquivo do certificado A1.");
            using var arquivo=System.IO.File.OpenRead(_arquivo);
            if(arquivo.Length is <=0 or >CertificadoA1.LimiteBytes)throw new InvalidOperationException("Escolha um arquivo A1 de até 128 KB.");
            bytes=new byte[(int)arquivo.Length];arquivo.ReadExactly(bytes);var senha=Senha;Senha="";
            await Certificado.CarregarA1Async(bytes,senha);
        }
        catch(Exception ex){Certificado.Mensagem=ex is InvalidOperationException?ex.Message:"Não foi possível ler o arquivo A1.";Certificado.MensagemEhErro=true;}
        finally{Senha="";if(bytes is not null)System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);}
    }
}
