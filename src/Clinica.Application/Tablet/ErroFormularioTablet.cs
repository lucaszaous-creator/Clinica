namespace Clinica.Application.Tablet;

/// <summary>
/// Identifica mensagens de validação que podem ser devolvidas pelo portal.
/// Falhas internas continuam como InvalidOperationException, mas sem mensagem pública.
/// O tipo permanece o mesmo para os demais clientes e testes da suíte.
/// </summary>
public static class ErroFormularioTablet
{
    private const string Marcador = "Clinica.Tablet.MensagemPublica";

    public static InvalidOperationException Criar(string mensagem)
    {
        var erro = new InvalidOperationException(mensagem);
        erro.Data[Marcador] = true;
        return erro;
    }

    public static bool EhPublico(InvalidOperationException erro)
        => erro.Data[Marcador] is true;
}
