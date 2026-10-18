using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;

namespace Clinica.Application.Servicos;

/// <summary>
/// As respostas dos pacientes à confirmação, já decididas (out/2026): UMA leitura do banco
/// (<see cref="IClinicaRepositorio.ContatosParaAvisosDeConfirmacaoAsync"/>) e UMA decisão
/// (<see cref="AvisosDeConfirmacao.Decidir"/>). O sino do shell e o painel da direção chamam
/// este mesmo método — é o que garante que "3 não vêm" no Gerente seja o "3" do sino.
/// </summary>
public sealed class AvisosDeConfirmacaoService(IClinicaRepositorio repo)
{
    /// <param name="agora">O momento da pergunta (as regras de horizonte e de espera partem dele).</param>
    /// <param name="confirmacoesDesde">
    /// Desde quando uma confirmação ainda é notícia. O sino passa o começo do dia; o painel
    /// não conta confirmações e pode passar <paramref name="agora"/>.
    /// </param>
    public async Task<IReadOnlyList<AvisoDeConfirmacao>> ListarAsync(
        DateTime agora, DateTime confirmacoesDesde, CancellationToken ct = default)
    {
        var contatos = await repo.ContatosParaAvisosDeConfirmacaoAsync(
            agora, AvisosDeConfirmacao.FimDoHorizonte(agora), ct);
        return AvisosDeConfirmacao.Decidir(contatos, agora, confirmacoesDesde);
    }
}
