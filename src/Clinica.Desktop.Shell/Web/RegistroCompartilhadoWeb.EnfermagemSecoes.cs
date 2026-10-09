using System.Linq;
using P=Clinica.Desktop.Shell.Web.PaginasWebController;
namespace Clinica.Desktop.Shell.Web;
public static partial class RegistroCompartilhadoWeb {
    private static P.Secao EscreverSessaoCompleta() => new("EscreverSessaoCompleta", "Escrever Sessao Window", null,
        [new("Profissional", "Profissional", "selecao", Opcoes: "Profissionais", RotuloOpcao: "Rotulo"), new("SessaoParaReutilizar", "Sessão anterior para reutilizar", "selecao", Opcoes: "Anteriores", RotuloOpcao: "Data"), new("Data", "Data", "data"), new("EvaAntes", "Eva Antes", "selecao", Opcoes: "EscalaEva"), new("EvaDepois", "Eva Depois", "selecao", Opcoes: "EscalaEva"), new("RetornoSugeridoEm", "Retorno Sugerido Em", "data"), new("TextoEvolucao", "Evolução da sessão", "texto-rico", Guarda: "PodeEditarProntuario", Formato: "TextoEvolucaoFormatado")],
        ["DicaRodape|Dica Rodape", "Titulo|Titulo", "Paciente|Paciente", "Contexto|Contexto", "SeloDetalhe|Selo Detalhe"],
        [
            new("Anexos", "Anexos", ["Anexos"], vm => ((Clinica.Desktop.Shell.Componentes.EscreverSessaoViewModel)vm).Anexos.Cast<object>(), [new("NomeArquivo", "Nome Arquivo")], [], [new("RemoverAnexo", "Retirar…"), new("BaixarAnexo", "Salvar como…")]),
            new("CamposPersonalizados", "Campos Personalizados", ["CamposPersonalizados"], vm => ((Clinica.Desktop.Shell.Componentes.EscreverSessaoViewModel)vm).CamposPersonalizados.Cast<object>(), [new("Rotulo", "Rotulo"), new("Ajuda", "Ajuda")], [new("Resposta", "Resposta", "texto", Visivel: "EhCaixaDeTexto"), new("Resposta", "Resposta", "selecao", Opcoes: "Opcoes", Visivel: "EhLista"), new("Resposta", "Resposta", "selecao", ValorOpcao: "Content", Visivel: "EhSimNao", Fixas: ["Sim", "Não"])], []),
        ], [new("Salvar", "Salvar sessão", Guarda: "PodeEditarProntuario"), new("Anexar", "Anexar arquivo…", Guarda: "PodeAnexar"), new("ReutilizarSessao", "Usar texto anterior", Guarda: "TemPaciente"), new("AbrirMapa", "Mapa corporal", Guarda: "TemPaciente"), new("AbrirModelos", "Modelos…", Guarda: "TemPaciente"), new("AbrirDetalhe", "Abrir Detalhe", Guarda: "TemPaciente")]);

    private static P.Secao PassagemCompleta() => new("PassagemCompleta", "Evolucao Enfermagem Window", null,
        [new("DataDoAtendimento", "Data Do Atendimento", "data"), new("Hora", "Hora", "texto"), new("Sistolica", "Sistolica", "texto"), new("Diastolica", "Diastolica", "texto"), new("Cardiaca", "Cardiaca", "texto"), new("Respiratoria", "Respiratoria", "texto"), new("Saturacao", "Saturacao", "texto"), new("ConsultaCompleta", "Consulta Completa", "booleano"), new("Intercorrencia", "Intercorrencia", "booleano"), new("AlergiaObservada", "Alergia Observada", "texto"), new("Texto", "Texto", "textarea")],
        ["AvisoModalidadeEnfermagem|Aviso Modalidade Enfermagem", "Paciente|Paciente", "Contexto|Contexto", "EtapasEmFalta|Etapas Em Falta"],
        [
            new("Registros", "Registros", ["Registros"], vm => ((Clinica.Desktop.Shell.Componentes.EvolucaoEnfermagemViewModel)vm).Registros.Cast<object>(), [new("Hora", "Hora"), new("Data", "Data"), new("Texto", "Texto"), new("SinaisVitais", "Sinais Vitais"), new("Marca", "Marca"), new("Assinatura", "Assinatura")], [], [new("VincularSessao", "Vincular à sessão", Guarda: "vm:PodeRegistrar"), new("Corrigir", "Corrigir", Guarda: "vm:PodeRegistrar"), new("CancelarRegistro", "Cancelar", Guarda: "vm:PodeRegistrar")]),
        ], [new("CancelarCorrecao", "Cancelar correção"), new("Registrar", "Registrar", Guarda: "PodeRegistrarNova")]);

    private static P.Secao ConsultaCompleta() => new("ConsultaCompleta", "Consulta De Enfermagem Window", null,
        [new("Historico", "Historico", "textarea"), new("AcessoLocal", "Acesso Local", "texto"), new("AcessoCalibre", "Acesso Calibre", "texto"), new("AcessoPuncionadoEm", "Acesso Puncionado Em", "data"), new("ExameFisico", "Exame Fisico", "textarea"), new("Avaliacao", "Avaliacao", "textarea")],
        ["Paciente|Paciente", "EtapasEmFalta|Etapas Em Falta"],
        [
            new("Diagnosticos", "Diagnosticos", ["Diagnosticos"], vm => ((Clinica.Desktop.Shell.Componentes.EvolucaoEnfermagemViewModel)vm).Diagnosticos.Cast<object>(), [], [new("Titulo", "Titulo", "texto"), new("RelacionadoA", "Relacionado A", "texto"), new("EvidenciadoPor", "Evidenciado Por", "texto"), new("ResultadoEsperado", "Resultado Esperado", "textarea")], [new("RemoverDiagnostico", "Remover")]),
            new("Cuidados", "Cuidados", ["Cuidados"], vm => ((Clinica.Desktop.Shell.Componentes.EvolucaoEnfermagemViewModel)vm).Cuidados.Cast<object>(), [], [new("Descricao", "Descricao", "texto"), new("Frequencia", "Frequencia", "texto"), new("SeNecessario", "Se Necessario", "booleano")], [new("RemoverCuidado", "Remover")]),
        ], [new("NovoDiagnostico", "Escrever à mão"), new("NovoCuidado", "Escrever à mão")]);
}
