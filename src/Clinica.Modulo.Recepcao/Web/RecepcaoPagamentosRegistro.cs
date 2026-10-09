using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using P = Clinica.Desktop.Shell.Web.PaginasWebController;
using D = Clinica.Desktop.Shell.Web.DialogosWebController;
namespace Clinica.Recepcao.Web;
public static partial class RecepcaoWebRegistro
{
    private static P.Pagina PaginaPrecosParticular() => new("precos-particular","Preços da sessão particular",typeof(PrecosParticularViewModel),
        [new("SoValendoHoje","Só o que vale hoje","booleano")],[],[
            new("precos","Preços por modalidade e especialidade",null,[],[],[
                PT("Precos",[new("Modalidade","Modalidade"),new("Especialidade","Especialidade"),new("Valor","Valor"),new("Vigencia","Vigência"),new("ValendoAgora","Vigente hoje")],[],
                    [new("Editar","Editar preço",Guarda:"vm:PodeEditar"),new("Excluir","Excluir preço",Guarda:"vm:PodeEditar")])
            ],[])
        ],[new("NovoPreco","Novo preço",Guarda:"PodeEditar"),new("Carregar","Atualizar")],Autorizado:()=>SessaoUsuario.Atual.PodeAlgum(Permissao.VenderPacote|Permissao.EditarFinanceiro));
    private static D.RegistroDialogo DialogoRecebimento() => new("Recebimento",typeof(ReceberPagamentoViewModel),new("Receber pagamento do paciente",null,[
        DF("Descricao","Cobrança","leitura"),DF("Valor","Saldo da cobrança","leitura"),DF("ValorInformado","Valor recebido agora",maximo:15),
        DF("VencimentoSaldo","Vencimento do saldo restante","data"),DF("Data","Data do pagamento","data"),DF("Forma","Forma de pagamento","selecao","Formas"),
        DF("Adquirente","Maquininha / adquirente",visivel:"EhCartao",maximo:60),DF("Bandeira","Bandeira",visivel:"EhCartao",maximo:40),DF("Parcelas","Parcelas",visivel:"EhCartao",maximo:2)
    ],[DA("ConfirmarCommand","Confirmar recebimento",habilitado:"PodeConfirmar")],[]),Autorizado:()=>SessaoUsuario.Atual.PodeAlgum(CobrancaDoPacienteViewModel.QuemRecebe));
}
