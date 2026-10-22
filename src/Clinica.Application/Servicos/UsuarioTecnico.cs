namespace Clinica.Application.Servicos;

/// <summary>
/// O usuário técnico do @SemDor (out/2026): o autor do pedido à recepção que vem do PACIENTE (o
/// "Remarcar" do lembrete pelo WhatsApp) e do cartão dele no # Recepção. O pedido e o chat pedem um
/// usuário como autor, e o paciente não é um. Nasce na migration <c>PedidoDoPacientePeloWhatsApp</c>,
/// INATIVO e com senha impossível — não entra no sistema — e fica fora das listas de usuários.
/// </summary>
public static class UsuarioTecnico
{
    public const string Login = "semdor.sistema";
    public const string Nome = "SemDor";
}
