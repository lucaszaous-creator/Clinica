using Clinica.Application.Servicos;

namespace Clinica.Application.Assinatura.SafeID;

/// <summary>
/// De onde as opções do SafeID saem em tempo de execução: <b>do banco</b>, com o ambiente
/// podendo sobrepor (parcela 44).
///
/// Por que não é mais um singleton resolvido no arranque
/// -----------------------------------------------------
/// Era, e estava errado. Ler a configuração dentro do <c>AddClinica</c> obrigava a resposta
/// a existir ANTES de haver banco, o que só sobra variável de ambiente — e variável de
/// ambiente é ritual de instalação: instalar numa máquina nova passaria por abrir o Prompt
/// de Comando e digitar quatro <c>setx</c>. Uma clínica não faz isso, e o sintoma da falha
/// seria a médica sem assinar num consultório só, sem ninguém entender por quê.
///
/// A ordem de precedência, e por que ela é essa
/// --------------------------------------------
/// <b>Ambiente vence banco.</b> É a mesma regra de <c>ConnectionStrings__Clinica</c>
/// (ver <c>docs/testar-sem-publicar.md</c>): quem está testando um build precisa apontar
/// para a homologação sem gravar nada na base da clínica — e, principalmente, sem que o
/// teste reescreva a configuração de produção pelo caminho.
/// </summary>
public sealed class ProvedorOpcoesSafeID
{
    private readonly ParametrosService _parametros;

    public ProvedorOpcoesSafeID(ParametrosService parametros) => _parametros = parametros;

    /// <summary>
    /// As opções em vigor, ou <c>null</c> quando a clínica ainda não cadastrou o SafeID.
    ///
    /// <b>Null é resposta, não falha.</b> Uma clínica que assina só com token nunca vai
    /// preencher isto, e quem chama usa a ausência para não oferecer a opção de nuvem — em
    /// vez de oferecer e falhar no clique.
    /// </summary>
    public async Task<OpcoesSafeID?> ObterAsync(CancellationToken ct = default)
    {
        // O ambiente responde inteiro ou não responde: misturar client_id do ambiente com
        // secret do banco produziria um par que não existe em lugar nenhum.
        if (ConfiguracaoSafeID.DoAmbiente() is { } doAmbiente) return doAmbiente;

        var (clientId, clientSecret, ambiente) =
            await _parametros.ObterCredenciaisSafeIDAsync(ct);

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return null;

        return new OpcoesSafeID(
            clientId.Trim(),
            clientSecret.Trim(),
            RedirectUris: null,     // o cadastro padrão, que é o que a Safeweb aceita
            BaseUrl: ConfiguracaoSafeID.EhHomologacao(ambiente)
                ? OpcoesSafeID.BaseHomologacao
                : OpcoesSafeID.BasePadrao);
    }
}
