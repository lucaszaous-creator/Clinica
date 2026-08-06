namespace Clinica.Application.Assinatura;

/// <summary>
/// A regra que faz a assinatura qualificada valer: <b>o certificado tem de ser DA PESSOA
/// que está assinando</b>.
///
/// Sem ela, "assinatura ICP-Brasil" provaria apenas que ALGUÉM com ALGUM token assinou —
/// e o e-CPF da recepcionista assinaria a receita da médica sem um único alerta. O CPF
/// sai de DENTRO do certificado (OID 2.16.76.1.3.1, no <i>subjectAltName</i>), não de um
/// campo digitado, e é comparado com o do cadastro do profissional.
///
/// Mora aqui, e não dentro de um dos serviços que assinam, porque desde a parcela 43 são
/// DOIS (a folha de infusão e os documentos da feature 07) e a regra tinha de valer nos
/// dois iguais. Checagem de segurança que existe numa porta só é o defeito recorrente do
/// projeto com a agravante de dar a impressão de estar coberto — foi o argumento que já
/// levou a conferência de alergia para o shell.
/// </summary>
public static class TitularDoCertificado
{
    /// <summary>
    /// Recusa o certificado que não é de quem assina, com a frase que a tela mostra.
    ///
    /// São três situações, e as três precisam de tratamento diferente:
    /// <list type="bullet">
    /// <item><b>Os dois CPFs existem e diferem</b> → recusa. É o caso que a regra existe
    /// para pegar: o token de outra pessoa.</item>
    /// <item><b>O certificado não traz CPF</b> (não é e-CPF ICP-Brasil) → recusa, porque
    /// então não há como saber de quem ele é, e assinatura qualificada anônima não é
    /// qualificada para nada.</item>
    /// <item><b>O profissional não tem CPF cadastrado</b> → recusa, e diz onde cadastrar.
    /// É chato, e a alternativa é pior: aceitar em silêncio faria a conferência existir
    /// só para quem já tinha o campo preenchido, e o buraco ficaria exatamente em quem
    /// esqueceu de preenchê-lo.</item>
    /// </list>
    /// </summary>
    /// <exception cref="InvalidOperationException">O certificado não é de quem assina.</exception>
    public static void Exigir(
        CertificadoAssinatura certificado, string? cpfEsperado, string? nome)
    {
        ArgumentNullException.ThrowIfNull(certificado);

        if (certificado.Cpf is null)
            throw new InvalidOperationException(
                $"O certificado de {certificado.Titular} não é um e-CPF ICP-Brasil (não traz "
                + "o CPF do titular). Sem isso não há como confirmar de quem ele é.");

        var esperado = Domain.Cpf.Normalizar(cpfEsperado);

        if (esperado.Length == 0)
            throw new InvalidOperationException(
                $"{nome ?? "O profissional"} não tem CPF cadastrado, e sem ele o sistema não "
                + "consegue confirmar que o certificado é mesmo desta pessoa. Cadastre o CPF "
                + "em Equipe e assine de novo.");

        if (esperado != certificado.Cpf)
            throw new InvalidOperationException(
                $"O certificado escolhido é de {certificado.Titular} "
                + $"(CPF {Domain.Cpf.Formatar(certificado.Cpf)}), e o documento está sendo "
                + $"assinado como {nome}. Cada um assina com o próprio certificado.");
    }
}
