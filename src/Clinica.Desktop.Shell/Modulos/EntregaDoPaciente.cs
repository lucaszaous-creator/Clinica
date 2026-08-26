using Clinica.Application;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Modulos;

/// <summary>
/// ENTREGAR o paciente ao posto clínico — com o horário de hoje, quando há um (parcela 88).
///
/// Por que existe, e por que num lugar só
/// -------------------------------------
/// Duas listas mandam alguém para a tela do paciente: a da <b>Enfermagem</b> (no shell) e a
/// <b>carteira</b> do Consultório (no módulo). As duas precisam fazer a MESMA coisa antes de
/// navegar — achar o horário de hoje daquele paciente e amarrá-lo ao foco.
///
/// ⚠️ E o vínculo não é detalhe: é ele que faz a evolução nascer ligada ao AGENDAMENTO, que
/// é o que tira a sessão de <i>"Sessões sem evolução"</i> depois de escrita. Sem ele o
/// registro é do paciente e de sessão nenhuma — e o Consultório continua cobrando para
/// sempre um registro que já existe.
///
/// Duas cópias disso divergiriam na primeira correção, e a que ficasse para trás não
/// quebraria nada: ela apenas deixaria de amarrar, em silêncio, na porta que ninguém releu.
/// </summary>
public static class EntregaDoPaciente
{
    /// <summary>
    /// Fixa o paciente no posto, com o horário de hoje quando existe um.
    /// </summary>
    /// <returns>
    /// <c>false</c> quando este executável não tem posto clínico — <see cref="PacienteEmFoco"/>
    /// é registrado pelo módulo do Consultório, e o <c>Clinica.Recepcao.exe</c> não o carrega.
    /// Quem chama usa isso para cair na própria tela em vez de navegar para lugar nenhum.
    /// </returns>
    public static async Task<bool> AoPostoAsync(
        IServiceScopeFactory escopos, int pacienteId, string nome)
    {
        ArgumentNullException.ThrowIfNull(escopos);

        using var escopo = escopos.CreateScope();

        var foco = escopo.ServiceProvider.GetService<PacienteEmFoco>();
        if (foco is null) return false;

        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var agenda = await escopo.ServiceProvider
                .GetRequiredService<AgendaService>().DoDiaAsync(hoje);

            // O horário DESTE paciente hoje. Quem já está na sala VENCE: com duas sessões
            // no mesmo dia (a da manhã e a da tarde), o registro pertence à que está
            // acontecendo — presumir a primeira o amarraria à sessão errada, e a marca
            // "desta sessão" da lista passaria a apontar para a outra.
            var doPaciente = agenda
                .Where(a => a.PacienteId == pacienteId
                            && a.Status != StatusAgendamento.Cancelado)
                .OrderBy(a => a.DataHora)
                .ToList();

            var horario = doPaciente.FirstOrDefault(
                              a => a.InicioAtendimentoEm is not null && a.FimAtendimentoEm is null)
                          ?? doPaciente.FirstOrDefault();

            foco.Definir(
                pacienteId, nome, horario?.Id, horario?.AtendimentoId,
                horario is null ? null : DateOnly.FromDateTime(horario.DataHora));
        }
        catch (Exception ex)
        {
            // ⚠️ Degradar deixa rastro, e o paciente é aberto DE QUALQUER FORMA: não achar o
            // horário de hoje não pode impedir alguém de registrar o que observou. O
            // registro apenas não fica ligado à sessão — e a tela de lá diz isso, na linha
            // de contexto.
            Diagnostico.Registrar(
                "Posto clínico — horário de hoje do paciente não pôde ser lido", ex);
            foco.Definir(pacienteId, nome);
        }

        return true;
    }
}
