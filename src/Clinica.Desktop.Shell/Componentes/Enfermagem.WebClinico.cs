using System.Collections.ObjectModel;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace Clinica.Desktop.Shell.Componentes;
public sealed partial class EscolherSessaoEnfermagemWebViewModel : ObservableObject
{
    public sealed record Opcao(int Id,string Rotulo);
    public string Paciente { get; }
    public IReadOnlyList<Opcao> Opcoes { get; }
    [ObservableProperty] private Opcao? _selecionada;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    public event Action? Confirmado;
    public EscolherSessaoEnfermagemWebViewModel(string paciente,IReadOnlyList<Agendamento> sessoes)
    { Paciente=paciente;Opcoes=sessoes.Where(a=>a.Status is StatusAgendamento.Agendado or StatusAgendamento.Realizado).OrderBy(a=>a.DataHora).Select(a=>new Opcao(a.Id,$"{a.DataHora:dd/MM/yyyy HH:mm} · sessão #{a.Id} · {a.Profissional?.Rotulo??"Responsável não informado"} · {(a.FimAtendimentoEm is null?"Em aberto":"Concluída")}")).ToArray(); }
    [RelayCommand] private void Confirmar()
    { if(Selecionada is null || !Opcoes.Contains(Selecionada)){Mensagem="Escolha a sessão original.";MensagemEhErro=true;return;}Confirmado?.Invoke(); }
}
public partial class EvolucaoEnfermagemViewModel
{
    [RelayCommand] private async Task AbrirConsultaWebAsync()
    { SessaoUsuario.Atual.Exigir(Permissao.RegistrarEvolucaoEnfermagem,"preencher consulta de enfermagem");ConsultaCompleta=true;await ConsultaDeEnfermagemWindow.AbrirAsync(this); }
    [RelayCommand] private Task CatalogoDiagnosticosWebAsync()=>AbrirCatalogoWebAsync(CatalogoDeDiagnosticos);
    [RelayCommand] private Task CatalogoCuidadosWebAsync()=>AbrirCatalogoWebAsync(CatalogoDeCuidados);
    private static async Task AbrirCatalogoWebAsync(CatalogoDeEnfermagem catalogo)
    { SessaoUsuario.Atual.Exigir(Permissao.RegistrarEvolucaoEnfermagem,"consultar o catálogo de enfermagem");catalogo.Recarregar();await DialogosDaSessao.AbrirAsync("CatalogoEnfermagem",catalogo,()=>{CatalogoEnfermagemWindow.Abrir(catalogo);return null;}); }
}
