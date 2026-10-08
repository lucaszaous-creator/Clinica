using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Web;

/// <summary>Troca voluntária da própria senha, com as mesmas regras do shell nativo.</summary>
public sealed partial class TrocaSenhaWebViewModel(IServiceScopeFactory escopos) : ObservableObject, IDisposable
{
    private readonly int _usuarioId = SessaoUsuario.Atual.UsuarioId;
    private bool _descartado;
    [ObservableProperty] private string _atual = string.Empty;
    [ObservableProperty] private string _nova = string.Empty;
    [ObservableProperty] private string _repetida = string.Empty;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _salvando;
    public bool MensagemEhErro => !string.IsNullOrEmpty(Mensagem);
    public event Action? Concluido;

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (_descartado || Salvando) return;
        Mensagem = null;
        Salvando = true;
        try
        {
            if (!SessaoUsuario.Atual.Autenticado || SessaoUsuario.Atual.UsuarioId != _usuarioId)
                throw new OperationCanceledException("A sessão mudou. Abra novamente o formulário.");
            if (Nova != Repetida) throw new InvalidOperationException("A nova senha e a repetição não conferem — digite as duas de novo.");
            using var escopo = escopos.CreateScope();
            await escopo.ServiceProvider.GetRequiredService<AcessoService>().TrocarSenhaAsync(_usuarioId, Atual, Nova);
            Limpar();
            Concluido?.Invoke();
        }
        catch (Exception ex) { Mensagem = ex.Message; }
        finally { Limpar(); Salvando = false; }
    }
    private void Limpar() { Atual = Nova = Repetida = string.Empty; }
    public void Dispose() { _descartado = true; Limpar(); }
}
