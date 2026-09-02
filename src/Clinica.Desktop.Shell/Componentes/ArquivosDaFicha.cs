using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// As DUAS ações sobre um ARQUIVO DA FICHA (set/2026), num ponto único: abrir e cancelar.
///
/// São três portas — a aba Prontuário da ficha (Recepção), a tela da Enfermagem e a seção
/// "Exames e anexos" do Consultório —, e três cópias de "abrir o PDF com a trilha de
/// acesso" divergiriam na primeira correção; a que ficasse para trás é a que abriria o
/// arquivo SEM registrar quem leu (a lição das parcelas 60 e 75).
/// </summary>
public static class ArquivosDaFicha
{
    /// <summary>
    /// Abre o arquivo: a linha pelo id (nome e paciente), os bytes sob demanda, a trilha de
    /// LEITURA — dado de saúde saindo para o disco é o que uma investigação procura — e o
    /// visualizador da máquina. Devolve a frase de erro, ou nulo quando abriu.
    /// </summary>
    public static async Task<string?> AbrirAsync(IServiceScopeFactory escopos, int anexoId)
    {
        SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "abrir arquivo da ficha");

        byte[]? bytes;
        string nome;
        using (var escopo = escopos.CreateScope())
        {
            var servico = escopo.ServiceProvider.GetRequiredService<AnexoPacienteService>();
            var anexo = await servico.ObterAsync(anexoId);
            if (anexo is null) return "O arquivo não foi encontrado.";
            nome = anexo.NomeArquivo;

            // SEQUENCIAL, nunca WhenAll: mesmo escopo, mesmo DbContext.
            bytes = await servico.ConteudoAsync(anexoId);
            await escopo.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                .RegistrarAsync(anexo.PacienteId, SessaoUsuario.Atual.Operador,
                    OrigemAcessoProntuario.ExportacaoClinica);
        }

        if (bytes is null || bytes.Length == 0) return "O arquivo não foi encontrado no banco.";
        return await ImpressaoPdf.SalvarEAbrirAsync(bytes, ImpressaoPdf.NomeSeguro(nome));
    }

    /// <summary>
    /// Cancela com motivo — nunca "excluir" (parcela 52). Devolve false quando a pessoa
    /// DESISTIU na pergunta: Cancelar não é resposta em branco (checagem 39).
    /// </summary>
    public static async Task<bool> CancelarAsync(
        IServiceScopeFactory escopos, IDialogoService dialogo, int anexoId, string titulo)
    {
        SessaoUsuario.Atual.ExigirAlgum(
            Permissao.EditarProntuario | Permissao.RegistrarEvolucaoEnfermagem,
            "cancelar arquivo da ficha");

        var motivo = dialogo.PerguntarTexto(
            "Cancelar arquivo da ficha",
            $"Por que \"{titulo}\" está sendo cancelado? Ele sai da lista e fica guardado, com este motivo.");
        if (string.IsNullOrWhiteSpace(motivo)) return false;

        using var escopo = escopos.CreateScope();
        await escopo.ServiceProvider.GetRequiredService<AnexoPacienteService>()
            .CancelarAsync(anexoId, motivo, SessaoUsuario.Atual.Operador);
        return true;
    }
}
