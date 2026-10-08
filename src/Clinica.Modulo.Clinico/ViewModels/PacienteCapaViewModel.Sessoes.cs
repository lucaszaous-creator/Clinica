using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Clinico.Janelas;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

public sealed partial class PacienteCapaViewModel
{
    private bool PodeAbrirSessao(SessaoNaFichaPaciente? linha)
        => PodeVerProntuario && linha?.EvolucaoId is not null && Sessoes.Contains(linha);

    [RelayCommand(CanExecute = nameof(PodeAbrirSessao))]
    private async Task AbrirSessaoAsync(SessaoNaFichaPaciente? linha)
    {
        if (linha?.EvolucaoId is not { } evolucaoId) return;
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "abrir a sessão da ficha");
            if (!Sessoes.Contains(linha)) throw new InvalidOperationException("Atualize as sessões antes de abrir o registro.");
            var pacienteId = PacienteId;
            using (var scope = _escopos.CreateScope())
            {
                var evolucao = await scope.ServiceProvider.GetRequiredService<IClinicaRepositorio>().ObterEvolucaoAsync(evolucaoId);
                if (pacienteId != PacienteId || evolucao is null || evolucao.PacienteId != pacienteId
                    || evolucao.AgendamentoId != linha.AgendamentoId || evolucao.CanceladaEm is not null)
                    throw new InvalidOperationException("A evolução desta sessão mudou. Atualize a ficha antes de abrir.");
            }
            var sessao = new SessaoDoProntuarioViewModel(_escopos, evolucaoId, Paciente, ofereceAnexos: true);
            await DialogosDaSessao.AbrirAsync("SessaoDoProntuario", sessao,
                () => new SessaoDoProntuarioWindow(sessao) { Owner = JanelaDona.Atual() }.ShowDialog());
            if (sessao.PediuAnexos && pacienteId == PacienteId)
            {
                var anexos = new AnexosSessaoViewModel(_escopos, evolucaoId, $"Sessão de {linha.DataTexto} — {Paciente}", pacienteId);
                await DialogosDaSessao.AbrirAsync("AnexosSessao", anexos,
                    () => new AnexosSessaoWindow(anexos) { Owner = JanelaDona.Atual() }.ShowDialog());
            }
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Ficha — sessão não pôde ser aberta", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
