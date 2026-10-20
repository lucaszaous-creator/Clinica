using Clinica.Application.Email;
using Clinica.Application.Modelos;

namespace Clinica.Application.Servicos;

/// <summary>
/// O e-mail da clínica (out/2026): o servidor de saída por onde o DOCUMENTO vai ao paciente.
///
/// ⚠️ <b>Não há mais lembrete de sessão por e-mail</b> (decisão da direção, out/2026: a
/// clínica não colhe e-mail no cadastro, e o e-mail ficou só para o documento). Saíram
/// juntos o envio na abertura da Recepção e do Gerente, o botão "Enviar e-mails" das
/// Confirmações, o relógio das 06:00 na ponte do CRM e a opção "e-mail só para quem não tem
/// celular" do SMS — com a regra da casa: função que sai, sai inteira. A confirmação de
/// sessão continua pelo WhatsApp (CRM) e pelo SMS.
///
/// Sobrou aqui o que serve ao documento: o TESTE do servidor e o nome que assina.
/// </summary>
public sealed class EmailDaClinicaService
{
    private readonly ParametrosService _parametros;
    private readonly IEnviadorDeEmail _enviador;

    public EmailDaClinicaService(ParametrosService parametros, IEnviadorDeEmail enviador)
    {
        _parametros = parametros;
        _enviador = enviador;
    }

    /// <summary>
    /// Manda UM e-mail de teste para o endereço informado, com a configuração GRAVADA e no
    /// MESMO modelo do documento ao paciente (<see cref="EmailDoDocumento"/>) — é assim que a
    /// clínica vê como a mensagem chega antes de mandar a primeira de verdade. Lança dizendo o
    /// que falta quando não há servidor, e deixa a exceção do servidor subir com a frase dele:
    /// "autenticação recusada" é a única pista de que a senha de aplicativo está errada.
    /// </summary>
    public async Task EnviarTesteAsync(string destino, CancellationToken ct = default)
    {
        var email = EnderecoDeEmail.SeValido(destino)
            ?? throw new InvalidOperationException("Informe um e-mail de destino válido para o teste.");

        var opcoes = await _parametros.ObterOpcoesEmailAsync(ct)
            ?? throw new InvalidOperationException(
                "O e-mail da clínica não está configurado: preencha o servidor de saída e o remetente, salve, e teste de novo.");

        var prestador = await _parametros.ObterPrestadorAsync(ct);
        var clinica = NomeDaClinica(prestador.NomeFantasia, prestador.RazaoSocial, opcoes.NomeRemetente) ?? "a clínica";
        var exemplo = EmailDoDocumento.Montar("Paciente de teste", "Documento de exemplo", "0000/0000",
            prestador, MarcaSemDor.Logo);

        await _enviador.EnviarMensagemAsync(opcoes, email, exemplo with
        {
            Assunto = $"Teste do e-mail · {clinica}",
            Texto = $"Este é um e-mail de teste do sistema de {clinica}. Se você o recebeu, os documentos "
                    + "enviados aos pacientes vão sair por este servidor, com esta aparência. Nenhum paciente recebeu nada."
        }, ct);
    }

    /// <summary>
    /// O nome que assina a mensagem: o nome fantasia do prestador, a razão social, e por
    /// último o nome do remetente cadastrado junto do servidor — que é o que o paciente já
    /// vê no "De:", então assinar com ele nunca contradiz o cabeçalho.
    /// </summary>
    public static string? NomeDaClinica(string? nomeFantasia, string? razaoSocial, string? nomeRemetente = null)
        => EnderecoDeEmail.Normalizar(nomeFantasia)
           ?? EnderecoDeEmail.Normalizar(razaoSocial)
           ?? EnderecoDeEmail.Normalizar(nomeRemetente);
}
