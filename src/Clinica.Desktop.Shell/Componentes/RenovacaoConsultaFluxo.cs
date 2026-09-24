using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
namespace Clinica.Desktop.Shell.Componentes;
public static class RenovacaoConsultaFluxo
{
    public static async Task<bool> ExecutarAsync(IServiceScopeFactory escopos, IDialogoService dialogo, int pacienteId, string nome)
    {
        SessaoUsuario.Atual.Exigir(Permissao.LancarAtendimento, "renovar consulta do convênio");
        if (!dialogo.Confirmar("Renovar validade da consulta do convênio", $"Registrar a renovação do convênio de {nome} para hoje? Esta ação não registra uma consulta clínica.")) return false;
        using var scope = escopos.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ConsultaService>().RenovarAsync(pacienteId, DateOnly.FromDateTime(DateTime.Today));
        return true;
    }
}
