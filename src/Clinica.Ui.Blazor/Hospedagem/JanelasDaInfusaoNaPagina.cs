using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Formularios.Infusao;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// A SALA DE INFUSÃO EM FORMULÁRIOS BLAZOR (out/2026: substitui a <c>JanelasDaSalaDeInfusaoWpf</c>
/// — a folha de execução, a evolução de enfermagem da folha e o registro da infusão externa
/// deixaram de ser janelas WPF; autorização da direção de 19/10/2026, só a camada de tela). A
/// interface ficou: as telas continuam pedindo a MESMA coisa (`AbrirFolhaAsync`), e quem atende é
/// a camada de formulários da página, sem janela por cima. Um por navegador (Scoped).
/// </summary>
public sealed class JanelasDaSalaDeInfusaoNaPagina(JanelasNaTela janelas) : IJanelasDaSalaDeInfusao
{
    public bool Disponivel => true;

    public Task AbrirFolhaAsync(int prescricaoId)
        => janelas.AbrirAsync<FolhaDeExecucaoForm, bool>(new Dictionary<string, object?> { ["PrescricaoId"] = prescricaoId });

    public Task AnotarAsync(LinhaSalaInfusao linha)
        => janelas.AbrirAsync<EvolucaoDaFolhaForm, bool>(new Dictionary<string, object?>
        {
            ["PacienteId"] = linha.PacienteId,
            ["Paciente"] = linha.Paciente,
            ["PrescricaoId"] = linha.PrescricaoId,
            ["Folha"] = linha.Numero
        });

    public async Task<bool> RegistrarInfusaoExternaAsync()
    {
        var paciente = await janelas.AbrirAsync<EscolherPacienteForm, Paciente>(
            new Dictionary<string, object?> { ["Assunto"] = "Registrar infusão realizada" });
        if (paciente is null) return false;
        var id = await janelas.AbrirAsync<InfusaoExternaForm, int?>(new Dictionary<string, object?>
        {
            ["PacienteId"] = paciente.Id,
            ["Paciente"] = paciente.Nome
        });
        if (id is not { } prescricaoId) return false;
        await AbrirFolhaAsync(prescricaoId);
        return true;
    }
}

/// <summary>
/// A ENFERMAGEM EM FORMULÁRIOS BLAZOR (out/2026: substitui a <c>JanelasDaEnfermagemWpf</c>) — a
/// coleta do termo do dia (pelo ponto único da coleta) e a infusão realizada → a folha criada.
/// </summary>
public sealed class JanelasDaEnfermagemNaPagina(JanelasNaTela janelas, ColetaDeTermoNaPagina coleta,
    IJanelasDaSalaDeInfusao sala) : IJanelasDaEnfermagem
{
    public bool Disponivel => true;

    public Task<bool> ColherTermoAsync(int pacienteId, string paciente, int? modeloId, int? documentoId, int? agendamentoId)
        => coleta.AbrirAsync(pacienteId, paciente, modeloId, documentoId, agendamentoId: agendamentoId);

    public async Task<bool> RegistrarInfusaoExternaAsync(int pacienteId, string paciente, int? agendamentoId)
    {
        var id = await janelas.AbrirAsync<InfusaoExternaForm, int?>(new Dictionary<string, object?>
        {
            ["PacienteId"] = pacienteId,
            ["Paciente"] = paciente,
            ["AgendamentoId"] = agendamentoId
        });
        if (id is not { } prescricaoId) return false;
        await sala.AbrirFolhaAsync(prescricaoId);
        return true;
    }
}

/// <summary>
/// O EDITOR DA PRESCRIÇÃO DE INFUSÃO EM FORMULÁRIO BLAZOR (out/2026: substitui a
/// <c>JanelasDaPrescricaoDeInfusaoWpf</c> e o ramo de infusão da <c>JanelasDoAtendimentoClinicoWpf</c>).
/// Quem prescreve é o profissional do LOGIN, como no WPF.
/// </summary>
public sealed class JanelasDaPrescricaoDeInfusaoNaPagina(JanelasNaTela janelas) : IJanelasDaPrescricaoDeInfusao
{
    public bool Disponivel => true;

    public async Task<bool> NovaAsync(int pacienteId, string paciente, ModeloInfusao? modelo)
        => await janelas.AbrirAsync<PrescricaoDeInfusaoForm, bool>(new Dictionary<string, object?>
        {
            ["PacienteId"] = pacienteId,
            ["Paciente"] = paciente,
            ["ProfissionalId"] = SessaoUsuario.Atual.ProfissionalId,
            ["Modelo"] = modelo
        });

    public async Task<bool> EditarAsync(int pacienteId, string paciente, int prescricaoId)
        => await janelas.AbrirAsync<PrescricaoDeInfusaoForm, bool>(new Dictionary<string, object?>
        {
            ["PacienteId"] = pacienteId,
            ["Paciente"] = paciente,
            ["ProfissionalId"] = SessaoUsuario.Atual.ProfissionalId,
            ["PrescricaoId"] = prescricaoId
        });

    public async Task<bool> PrescreverNaSessaoAsync(int pacienteId, string paciente, int? agendamentoId, int? evolucaoId)
        => await janelas.AbrirAsync<PrescricaoDeInfusaoForm, bool>(new Dictionary<string, object?>
        {
            ["PacienteId"] = pacienteId,
            ["Paciente"] = paciente,
            ["ProfissionalId"] = SessaoUsuario.Atual.ProfissionalId,
            ["AgendamentoId"] = agendamentoId,
            ["EvolucaoId"] = evolucaoId
        });
}
