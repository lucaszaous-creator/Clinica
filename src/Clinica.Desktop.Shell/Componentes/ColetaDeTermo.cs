using Clinica.Application.Assinatura;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Os formulários Blazor que o código WPF ainda pede (out/2026 — a conversão da área do
/// SafeID/assinatura para Blazor; autorização da direção de 19/10/2026, só a camada de tela). As
/// janelas WPF (<c>EscolherCertificadoWindow</c>, <c>AssinaturaPacienteWindow</c> e as de escolha
/// do termo, <c>EvolucaoEnfermagemWindow</c>) saíram; as telas Blazor abrem os formulários na
/// própria página. O que ainda é WPF — a ficha do balcão, a ponte WPF dos Prontuários e os atos do
/// documento — pede por aqui, e quem atende é a janela de transição do
/// <c>Clinica.Desktop.Blazor</c> (registrada pela hospedagem Blazor de cada módulo).
/// </summary>
public interface IFormulariosBlazorNoWpf
{
    /// <summary>O seletor do A1. O certificado confirmado é de quem chamou (use <c>using</c>). Nulo = desistiu.</summary>
    Task<CertificadoAssinatura?> EscolherCertificadoAsync(string assunto);

    /// <summary>A coleta do termo do procedimento (modelo nulo = pergunta qual). Verdadeiro = assinou ou recusou.</summary>
    Task<bool> ColherTermoAsync(int pacienteId, string paciente, int? modeloId, int? documentoId,
        int? profissionalId, int? agendamentoId);

    /// <summary>A evolução de enfermagem avulsa do paciente.</summary>
    Task AnotarEnfermagemAsync(int pacienteId, string paciente);
}

/// <summary>
/// O PONTO ÚNICO por onde a coleta de termo passa no código WPF que sobrou (a ficha do balcão). Era a
/// fábrica das três janelas WPF; desde out/2026 delega ao formulário Blazor (a MESMA coleta das
/// telas Blazor — <c>ColetaDeTermoNaPagina</c>), pela <see cref="IFormulariosBlazorNoWpf"/>.
///
/// ⚠️ Quem chama deve RECARREGAR de qualquer forma: abrir a coleta já EMITE o termo numerado.
/// </summary>
public static class ColetaDeTermo
{
    public static Task<bool> AbrirAsync(
        IServiceScopeFactory escopos,
        int pacienteId,
        string pacienteNome,
        int? modeloId = null,
        int? documentoId = null,
        int? profissionalId = null,
        int? agendamentoId = null)
        => Formularios(escopos).ColherTermoAsync(pacienteId, pacienteNome, modeloId, documentoId, profissionalId, agendamentoId);

    /// <summary>A evolução de enfermagem avulsa (era o <c>EvolucaoEnfermagemWindow.Abrir</c>).</summary>
    public static Task AnotarEnfermagemAsync(IServiceScopeFactory escopos, int pacienteId, string pacienteNome)
        => Formularios(escopos).AnotarEnfermagemAsync(pacienteId, pacienteNome);

    /// <summary>O seletor do A1 (era o <c>EscolherCertificadoWindow.Perguntar</c>).</summary>
    public static Task<CertificadoAssinatura?> EscolherCertificadoAsync(IServiceScopeFactory escopos, string assunto)
        => Formularios(escopos).EscolherCertificadoAsync(assunto);

    private static IFormulariosBlazorNoWpf Formularios(IServiceScopeFactory escopos)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        using var escopo = escopos.CreateScope();
        return escopo.ServiceProvider.GetService<IFormulariosBlazorNoWpf>()
            ?? throw new InvalidOperationException(
                "Este aplicativo não tem os formulários de assinatura instalados. Avise o suporte.");
    }
}
