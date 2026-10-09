using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace Clinica.Gerente.Web;
public sealed class SenhaProvisoriaWebViewModel : ObservableObject
{
 public string Titulo {get;}
 public string Descricao=>"A pessoa deverá trocar esta senha ao entrar.";
 public string Senha {get;set;}="";
 private string? _mensagem;
 public string? Mensagem {get=>_mensagem;private set=>SetProperty(ref _mensagem,value);}
 public bool MensagemEhErro=>Mensagem is not null;
 public event Action? Concluido;
 public IRelayCommand ConfirmarCommand {get;}
 public SenhaProvisoriaWebViewModel(string nome){Titulo="Redefinir senha — "+nome;ConfirmarCommand=new RelayCommand(()=>{if(string.IsNullOrWhiteSpace(Senha)){Mensagem="Informe a senha provisória.";return;}Concluido?.Invoke();});}
}
