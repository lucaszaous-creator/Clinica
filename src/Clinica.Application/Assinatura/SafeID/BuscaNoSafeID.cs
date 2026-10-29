using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Application.Assinatura.SafeID;

/// <summary>
/// Como o cliente do PSC nasce. O <see cref="HttpClient"/> vem da fábrica do host (registrada em
/// <c>DependencyInjection.RegistrarSafeID</c>, com o tempo-limite curto) e sobrevive ao escopo de
/// propósito: o assinador guardado no certificado vai usá-lo DEPOIS que o seletor fechar.
/// </summary>
public sealed class FabricaDoClienteSafeID(Func<HttpClient> http)
{
    public ClienteSafeID Criar(OpcoesSafeID opcoes) => new(http(), opcoes);
}

/// <summary>
/// BUSCAR O CERTIFICADO EM NUVEM (out/2026 — o SafeID de volta para receitas e documentos, a
/// pedido da clínica; a infusão continua só com o A1). É o <c>BuscarNaNuvemAsync</c> do antigo
/// <c>EscolherCertificadoViewModel</c> WPF, movido sem mudar a sequência: credenciais → cliente →
/// autorização (QR Code no celular) → um certificado por item autorizado, cada um levando o seu
/// <see cref="AssinadorSafeID"/>. Quem confere se o CPF do certificado é o de quem assina continua
/// sendo o serviço da assinatura (<c>TitularDoCertificado.Exigir</c>), igual ao A1.
/// </summary>
public static class BuscaNoSafeID
{
    /// <summary>Nome do <c>HttpClient</c> configurado para falar com o PSC.</summary>
    public const string NomeHttp = "SafeID";

    /// <summary>O que a tela diz enquanto espera a confirmação no celular.</summary>
    public const string Aguardando =
        "Abrimos a página do SafeID no navegador. Leia o QR Code com o aplicativo e confirme no celular — esta janela espera.";

    public const string NaoConfigurado =
        "O SafeID não está configurado nesta clínica. A direção cadastra o client_id e o client_secret em Configurações.";

    public const string NenhumCertificado = "O SafeID autorizou, mas não devolveu nenhum certificado.";

    /// <summary>
    /// Se a clínica cadastrou o SafeID — a metade visível da regra: sem isto o botão nem aparece.
    /// Falhar ao ler não derruba o seletor: o A1 continua servindo, e o log diz por quê.
    /// </summary>
    public static async Task<bool> ConfiguradoAsync(IServiceScopeFactory? escopos, CancellationToken ct = default)
    {
        if (escopos is null) return false;
        try
        {
            using var escopo = escopos.CreateScope();
            return escopo.ServiceProvider.GetService<ProvedorOpcoesSafeID>() is { } provedor
                   && await provedor.ObterAsync(ct) is not null;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("SafeID — a configuração não pôde ser lida", ex);
            return false;
        }
    }

    /// <summary>
    /// Manda a médica ao QR Code e devolve os certificados que ela autorizar, prontos para assinar
    /// (cada um com o seu <see cref="CertificadoAssinatura.AssinadorRemoto"/>). Recusa com
    /// <see cref="InvalidOperationException"/>, cuja frase vai para a tela tal e qual.
    /// </summary>
    /// <param name="assunto">O que se assina — o PSC o registra e ele aparece na confirmação do celular.</param>
    /// <param name="assinaturasDoAto">
    /// Quantos hashes este ato sela. O escopo do token sai daqui (<see cref="EscopoSafeID.ParaAto"/>):
    /// o de assinatura única MORRE NO USO, e um ato de duas assinaturas teria a segunda recusada.
    /// </param>
    public static async Task<IReadOnlyList<CertificadoAssinatura>> BuscarAsync(
        IServiceScopeFactory? escopos, string assunto, Action<Uri> abrirNavegador,
        int assinaturasDoAto = 1, CancellationToken ct = default)
    {
        if (escopos is null || !SessaoUsuario.Atual.Autenticado)
            throw new InvalidOperationException("Entre com seu usuário profissional para assinar.");

        OpcoesSafeID? opcoes;
        FabricaDoClienteSafeID fabrica;
        string? cpf;
        using (var escopo = escopos.CreateScope())
        {
            var sp = escopo.ServiceProvider;
            opcoes = await sp.GetRequiredService<ProvedorOpcoesSafeID>().ObterAsync(ct);
            fabrica = sp.GetRequiredService<FabricaDoClienteSafeID>();
            // O CPF vai como login_hint: não é segurança, é a página do PSC já abrir no CPF certo.
            cpf = (await sp.GetRequiredService<IClinicaRepositorio>().ObterUsuarioAsync(SessaoUsuario.Atual.UsuarioId))
                ?.Profissional?.Cpf;
        }
        if (opcoes is null) throw new InvalidOperationException(NaoConfigurado);

        var cliente = fabrica.Criar(opcoes);
        var autorizacao = new AutorizacaoSafeIDService(
            _ => Task.FromResult<OpcoesSafeID?>(opcoes), _ => cliente, abrirNavegador);
        var (escopoDoAto, duracao) = EscopoSafeID.ParaAto(assinaturasDoAto);
        var sessao = await autorizacao.AutorizarAsync(
            cpf: cpf, escopo: escopoDoAto, duracaoSegundos: duracao, ct: ct);

        return sessao.Certificados
            .Select(daNuvem => daNuvem.Certificado with
            {
                AssinadorRemoto = new AssinadorSafeID(cliente, sessao.Token.AccessToken, daNuvem, assunto)
            })
            .ToList();
    }
}
