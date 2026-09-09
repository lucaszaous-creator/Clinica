using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>
/// "De qual sessão é este documento?" — a frase que as listas de documento mostram
/// (set/2026).
///
/// Mora aqui, e não nas ViewModels, pela regra da <c>GradeSemana</c> e do
/// <c>ResumoSessaoAnterior</c>: o que a tela AFIRMA precisa morar onde o
/// <c>dotnet test</c> alcança. E são TRÊS listas de documento na suíte — a ficha da
/// Recepção, as prescrições do Consultório e a central —; três montagens da mesma frase
/// divergiriam na primeira correção, e a que ficasse para trás mostraria a sessão errada
/// sem quebrar nada.
/// </summary>
public static class ProcedenciaDaSessao
{
    /// <summary>
    /// "Sessão de 08/09/2026, 09h00 · Acupuntura + eletroestimulação".
    ///
    /// Nulo quando o documento não pertence a sessão nenhuma — o termo avulso, a receita
    /// emitida fora de consulta, e toda linha anterior à versão que passou a guardar o
    /// vínculo. Nulo é resposta honesta, e a linha SOME em vez de escrever um traço.
    ///
    /// A modalidade sai do CATÁLOGO, nunca do enum: quem lê a ficha precisa ver
    /// "Acupuntura + eletro", e não "AcupunturaEletro" (o defeito da parcela 41).
    /// </summary>
    public static string? Descrever(Agendamento? horario)
    {
        if (horario is null) return null;

        var modalidade = CatalogoModalidades.Nome(
            horario.ModalidadeCodigo, horario.ModalidadePrevista);

        var quando = $"Sessão de {horario.DataHora:dd/MM/yyyy}, {horario.DataHora:HH'h'mm}";

        return string.IsNullOrWhiteSpace(modalidade)
            ? quando
            : $"{quando}  ·  {modalidade}";
    }
}
