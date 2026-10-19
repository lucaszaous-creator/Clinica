using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Clinico.ViewModels;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.Modulo;

/// <summary>
/// A ponte da tela Blazor de Prontuários (out/2026) até o que continua WPF: o quick-view do
/// prontuário, a assinatura da anamnese e a tela de atendimento. Faz EXATAMENTE o que o
/// <c>ProntuariosViewModel</c> fazia — a barreira de permissão é da tela que chama. A
/// assinatura usa o MESMO serviço e o MESMO seletor de certificado de todas as portas (o motor
/// da assinatura não muda — ver docs/safeid-congelado.md). Tudo no despachante do WPF.
/// </summary>
public sealed class JanelasDosProntuariosWpf(IServiceScopeFactory escopos, PacienteEmFoco foco) : IJanelasDosProntuarios
{
    public Task AbrirResumoAsync(LinhaProntuario linha) => NaInterface(() =>
    {
        // Linha de anamnese abre já na aba dela, com a 2ª via da folha emitida no rodapé.
        var vm = new ResumoProntuarioViewModel(escopos, foco, linha.PacienteId, linha.Paciente,
            linha.Natureza == NaturezaLinhaProntuario.Anamnese ? linha.DocumentoId : null);
        new Clinica.Clinico.Janelas.ResumoProntuarioWindow(vm) { Owner = JanelaDona.Atual() }.ShowDialog();
        return Task.FromResult(true);
    });

    public Task<ResultadoDaAssinatura?> AssinarAnamneseAsync(LinhaProntuario linha) => NaInterface(async () =>
    {
        if (!linha.MostraAssinar || linha.DocumentoId is not { } docId) return (ResultadoDaAssinatura?)null;

        using var certificado = EscolherCertificadoWindow.Perguntar(
            $"Assinar anamnese {linha.Numero}", JanelaDona.Atual(), escopos);
        if (certificado is null) return null;

        DocumentoAssinado assinado;
        using (var scope = escopos.CreateScope())
        {
            var assinaturas = scope.ServiceProvider.GetRequiredService<AssinaturaDeDocumentoClinicoService>();
            assinado = await assinaturas.AssinarAsync(
                docId, certificado,
                SessaoUsuario.Atual.Autenticado ? SessaoUsuario.Atual.UsuarioId : null,
                SessaoUsuario.Atual.Operador);
        }

        // É o ARQUIVO que vale: salva e abre; a frase diz se o salvamento falhou.
        var erro = await ImpressaoPdf.SalvarEAbrirAsync(assinado.Pdf, ImpressaoPdf.NomeSeguro(assinado.NomeArquivo));
        return new ResultadoDaAssinatura(erro ?? $"Anamnese {linha.Numero} assinada.", erro is not null);
    });

    public Task<bool> AbrirAtendimentoAsync() => NaInterface(() =>
        Task.FromResult(NavegacaoSuite.Ir(PostoClinico.ChaveDoAtendimento())));

    private static Task<T> NaInterface<T>(Func<Task<T>> acao)
    {
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (despachante is null || despachante.CheckAccess()) return acao();
        return despachante.InvokeAsync(acao).Task.Unwrap();
    }
}
