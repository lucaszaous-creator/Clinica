using Clinica.Desktop.Shell.Componentes.Cadastro;
using Clinica.Domain.Entities;
using static Clinica.Desktop.Shell.Web.DialogosWebController;
namespace Clinica.Desktop.Shell.Web;
public static partial class RegistroCompartilhadoWeb
{
    public static IEnumerable<RegistroDialogo> DialogosCadastro()
    {
        Campo F(string p,string r,string tipo="texto",int max=4000,string? opcoes=null,string? rotulo=null,string? visivel=null,string? habilitado="PodePreencher") => new(p,p,r,tipo,opcoes,rotulo??"Rotulo",visivel,habilitado,Maximo:max);
        yield return new("CadastroPaciente",typeof(CadastroPacienteViewModel),new("Cadastro do paciente","Identificação, convênio e preferências do paciente.",[
            F("Titulo","Cadastro","leitura"), F("Nome","Nome completo",max:120),F("Documento","CPF",max:14),F("ErroDocumento","Conferência do CPF","leitura"),
            F("DataNascimento","Data de nascimento","data"),F("SexoSelecionado","Sexo","selecao",opcoes:"Sexos"),
            F("Telefone","Telefone / WhatsApp",max:20),F("Email","E-mail",max:120),F("Endereco","Endereço residencial","textarea",300),F("ErroEndereco","Conferência do endereço","leitura"),
            F("Convenio","Convênio ou particular","selecao",opcoes:"Convenios",rotulo:"Nome"),F("ExplicacaoDoConvenio","Sobre o convênio","leitura"),
            F("Carteirinha","Número da carteirinha",max:40),F("ValidadeCarteirinha","Validade da carteirinha","data"),F("PossuiApp","Tem o aplicativo do convênio","booleano"),
            F("ModalidadePreferida","Modalidade preferida","selecao",opcoes:"Modalidades",rotulo:"Nome"),
            F("Categoria","Categoria de faturamento","selecao",opcoes:"Categorias",visivel:"PodeAjustarCategoria",habilitado:"PodePreencher&PodeAjustarCategoria"),
            F("Origem","Como conheceu a clínica?","selecao",opcoes:"Origens"),F("IndicadoPor","Quem indicou?",max:120,habilitado:"PodePreencher&EhIndicacao"),
            F("Observacoes","Observações do cadastro","textarea"),F("FotoPreviaWeb","Foto do paciente","imagem-leitura",habilitado:null)
        ],[
            new("CapturarFotoCommand","Tirar ou escolher foto","CapturarFotoCommand",Habilitado:"PodePreencher"),
            new("RemoverFotoCommand","Remover foto","RemoverFotoCommand",Habilitado:"PodePreencher",Visivel:"TemFoto"),
            new("SalvarCommand","Salvar cadastro","SalvarCommand","primario",Habilitado:"PodePreencher")
        ],[]),Permissao.EditarPaciente);
        yield return new("CapturaFoto",typeof(CapturaFotoWebViewModel),new("Foto do paciente",null,[
            new("Paciente","Paciente","Paciente","leitura"),
            new("FotoDataUrl","FotoDataUrl","Capturar ou escolher imagem","imagem",Maximo:11_184_876,Ajuda:"JPEG, PNG ou BMP; até 8 MB. Confira o recorte antes de usar a foto.")
        ],[new("LimparCommand","Tirar outra foto","LimparCommand"),new("ConfirmarCommand","Usar foto","ConfirmarCommand","primario",Habilitado:"TemFoto")],[]),Permissao.EditarPaciente);
    }
}
