using Clinica.Application.Abstracoes;

namespace Clinica.Application.Servicos;

/// <summary>
/// De onde as credenciais do armazenamento saem em tempo de execução (parcela 53).
///
/// Gêmeo do <c>ProvedorOpcoesSafeID</c>, e de propósito: são o mesmo problema — segredo de
/// serviço externo que precisa alcançar várias máquinas sem ritual de instalação em cada
/// uma. Ambiente vence banco; ausência é resposta, não falha.
/// </summary>
public sealed class ProvedorOpcoesArmazenamento
{
    private readonly ParametrosService _parametros;

    public ProvedorOpcoesArmazenamento(ParametrosService parametros) => _parametros = parametros;

    /// <summary>
    /// As opções em vigor, ou <c>null</c> quando a clínica ainda não contratou armazenamento.
    ///
    /// <b>Null é resposta.</b> Sem provedor, a publicação simplesmente não acontece e o
    /// sistema funciona como sempre funcionou — o QR do PDF aponta para o validador do ITI.
    /// </summary>
    public async Task<OpcoesArmazenamento?> ObterAsync(CancellationToken ct = default)
    {
        if (OpcoesArmazenamento.DoAmbiente() is { } doAmbiente) return doAmbiente;

        var (endpoint, regiao, bucket, chave, segredo) =
            await _parametros.ObterCredenciaisArmazenamentoAsync(ct);

        return OpcoesArmazenamento.De(endpoint, regiao, bucket, chave, segredo);
    }

    /// <summary>
    /// As credenciais estão vindo do ambiente. A tela usa isto para mostrar os campos como
    /// só leitura e DIZER por quê — deixar editar o que não terá efeito, e ainda confirmar
    /// "salvo", é pior do que não oferecer o campo.
    /// </summary>
    public static bool VemDoAmbiente() => OpcoesArmazenamento.DoAmbiente() is not null;
}
