using Clinica.Domain.Entities;
using D = Clinica.Desktop.Shell.Web.DialogosWebController;

namespace Clinica.Recepcao.Web;

public static partial class RecepcaoWebRegistro
{
    private static IEnumerable<D.RegistroDialogo> CriarDialogosDeEscolha()
    {
        yield return new("RecepcaoDetalheHorario", typeof(DetalheHorarioWebViewModel),
            new("Horário da agenda", null,
                [DF("Cartao.Paciente", "Paciente", "leitura"), DF("Cartao.Faixa", "Horário", "leitura"),
                 DF("Cartao.Contexto", "Atendimento", "leitura"), DF("Cartao.Telefone", "Telefone", "leitura"),
                 DF("Cartao.Observacoes", "Observações", "leitura", visivel: "Cartao.TemObservacoes"),
                 DF("Cartao.Lancamento", "Quem marcou", "leitura"), DF("Cartao.StatusRotulo", "Situação", "leitura"),
                 DF("Cartao.EhEncaixe", "Encaixe", "leitura"), DF("Cartao.VeioDaListaEspera", "Da lista de espera", "leitura"),
                 DF("Cartao.EhRetornoDoSegundoCodigo", "Retorno do 2º código", "leitura")],
                [DA("RemarcarCommand", "Remarcar / reabrir"), DA("ConfirmarCommand", "Confirmar pelo WhatsApp"),
                 DA("ComprovanteCommand", "Comprovante"), DA("QuemChamarCommand", "Quem chamar?"),
                 DA("FaltaCommand", "Marcar falta"), DA("CancelarCommand", "Cancelar horário"),
                 DA("CancelarSerieCommand", "Cancelar o resto da série")], []), Permissao.VerAgenda);
        yield return new("RecepcaoProximasVagas", typeof(ProximasVagasWebViewModel),
            new("Próximas vagas", null,
                [DF("Criterio", "Critério da busca", "leitura"), DF("AvisoVazio", "Disponibilidade", "leitura", visivel: "Vazio")], [],
                [new("Vagas", "Vagas disponíveis", "Vagas", [new("Rotulo", "Dia e horário")],
                    [DA("EscolherCommand", "Escolher esta vaga")])]), Permissao.EditarAgenda);
    }
}
