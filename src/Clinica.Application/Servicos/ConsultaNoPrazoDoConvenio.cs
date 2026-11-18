using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// A outra consulta pelo convênio que cai dentro do prazo da operadora (nov/2026). É a CONTA da
/// trava de consulta, num lugar só, para as duas perguntas que a fazem:
/// <list type="bullet">
/// <item>a <b>trava</b> (<see cref="AgendaService.TravaDeConsultaAsync"/>): todas as portas que
/// marcam, remarcam ou lançam consulta pelo convênio — ela soma a chave de Configurações, a
/// cobertura do convênio e a liberação da gerência;</item>
/// <item>o <b>aviso de glosa na baixa</b> (<see cref="GuiaAnsNaBaixaService"/>): o que já entrou
/// (marcado antes da chave, liberado pela gerência ou passado por uma porta antiga) ainda é
/// glosado pela operadora, e quem dá a baixa precisa saber antes de mandar a guia.</item>
/// </list>
/// O que disputa o prazo é dado do convênio (<see cref="Janela"/>): a mesma especialidade com o
/// mesmo médico (o critério da direção, nov/2026) ou, na Saúde Petrobras, a mesma especialidade com
/// qualquer médico.
/// </summary>
public static class ConsultaNoPrazoDoConvenio
{
    /// <param name="Dias">O prazo do convênio em dias.</param>
    /// <param name="Consulta">A mais próxima do dia pretendido (a que a frase cita).</param>
    /// <param name="NoPrazo">Todas as que estão no prazo (a trava solta depois da ÚLTIMA).</param>
    public sealed record Achada(int Dias, ConsultaParaTrava Consulta, IReadOnlyList<ConsultaParaTrava> NoPrazo);

    /// <summary>
    /// O prazo entre duas consultas pelo convênio deste paciente: o convênio personalizado pelo
    /// catálogo; o embutido pela linha da família, e linha sem número cai no padrão do código
    /// (<see cref="ParametrosSnapshot.PrazoDaTravaDeConsulta"/>). Nulo ou zero = sem prazo.
    /// </summary>
    public static int? PrazoDias(Paciente paciente, ParametrosSnapshot parametros)
        => paciente.Convenio == Convenio.Personalizado
            ? CatalogoConvenios.ValidadeConsultaDias(paciente.ConvenioCodigo)
            : parametros.PrazoDaTravaDeConsulta(paciente.Convenio);

    /// <summary>
    /// O critério da janela do convênio deste paciente: a família no catálogo (a variante cadastrada
    /// de uma família embutida herda o dela); convênio personalizado fica com o critério original.
    /// </summary>
    public static JanelaDaTravaDeConsulta Janela(Paciente paciente)
        => ConvenioInfo.JanelaDaTravaDeConsulta(CatalogoConvenios.Familia(paciente.ConvenioCodigo ?? paciente.Convenio.ToString()));

    /// <summary>A janela precisa do médico para comparar? (Sem ele, não há com quem comparar.)</summary>
    public static bool ComparaProfissional(Paciente paciente) => Janela(paciente) == JanelaDaTravaDeConsulta.PorEspecialidadeEProfissional;

    /// <summary>
    /// A consulta pelo convênio do paciente que disputa o prazo com esta (mesma especialidade e, quando o
    /// convênio compara, o mesmo médico), a menos de
    /// <see cref="PrazoDias"/> dias de <paramref name="dataHora"/>; nulo quando não há (ou quando o
    /// convênio não tem prazo). <paramref name="soAnteriores"/>: só as que vêm ANTES (o aviso da
    /// baixa fala da consulta anterior); falso olha os dois lados (a trava protege também a próxima).
    /// </summary>
    public static async Task<Achada?> ProcurarAsync(
        IClinicaRepositorio repo, ParametrosSnapshot parametros, Paciente paciente, DateTime dataHora,
        string especialidadeCodigo, int? profissionalId, int? ignorarAgendamentoId, bool soAnteriores,
        CancellationToken ct = default)
    {
        if (PrazoDias(paciente, parametros) is not { } dias || dias <= 0) return null;
        var comparaProfissional = ComparaProfissional(paciente);
        if (comparaProfissional && profissionalId is null) return null;

        var dia = DateOnly.FromDateTime(dataHora);
        var outras = (await repo.ConsultasPeloConvenioDoPacienteAsync(
                paciente.Id, dia.AddDays(-dias).ToDateTime(TimeOnly.MinValue),
                dia.AddDays(soAnteriores ? 1 : dias + 1).ToDateTime(TimeOnly.MinValue), ct))
            .Where(c => c.AgendamentoId != ignorarAgendamentoId)
            .Where(c => !soAnteriores || c.DataHora < dataHora)
            .Where(c => (!comparaProfissional || c.ProfissionalId == profissionalId)
                        && string.Equals(c.EspecialidadeCodigo, especialidadeCodigo, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return TravaDeConsulta.Conferir(dia, dias, outras) is { } consulta ? new Achada(dias, consulta, outras) : null;
    }

    /// <summary>
    /// A frase do aviso na baixa: "Consulta Saúde Petrobras a menos de 30 dias da anterior
    /// (atendimento 2026-000123 em 20/10) — risco de glosa." Sem número de atendimento (horário
    /// importado ou só marcado), cita o dia.
    /// </summary>
    public static string AvisoDeGlosa(string convenio, Achada achada)
    {
        var anterior = achada.Consulta;
        var qual = anterior.AtendimentoNumero is { } n
            ? $"atendimento {n} em {anterior.DataHora:dd/MM}"
            : $"consulta {(anterior.Atendida ? "atendida" : "marcada")} em {anterior.DataHora:dd/MM}";
        return $"Consulta {convenio} a menos de {achada.Dias} dias da anterior ({qual}) — risco de glosa. "
               + "Confira se a operadora aceita antes de enviar.";
    }
}
