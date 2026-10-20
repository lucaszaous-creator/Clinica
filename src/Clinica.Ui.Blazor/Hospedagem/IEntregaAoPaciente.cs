namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// A metade do "Enviar ao paciente" que só o aplicativo instalado faz (out/2026, quando a janela de
/// envio virou o <c>EnvioAoPacienteForm</c> Blazor): o WhatsApp DESTE computador — gravar o PDF numa
/// pasta estável, pôr o ARQUIVO na área de transferência (o Ctrl+V do WhatsApp vira anexo) e abrir a
/// conversa com a legenda pronta. É o <c>EntregaAoPaciente.Entregar</c> do shell, chamado tal e qual;
/// o e-mail e a fila do CRM o formulário faz sozinho, pelo <c>EnvioAoPacienteService</c>.
///
/// Onde não há hospedeiro WPF (a prévia, os testes) vale <see cref="EntregaAoPacienteIndisponivel"/>,
/// e o formulário DIZ que esse caminho é do aplicativo — nunca o silêncio.
/// </summary>
public interface IEntregaAoPaciente
{
    /// <summary>Grava, copia e abre a conversa. Nunca lança: toda falha vira a frase do resultado.</summary>
    Task<ResultadoDoAto> EntregarNoWhatsAppAsync(byte[] pdf, string nomeArquivo, string? telefone, string paciente, string legenda);
}

/// <summary>Sem hospedeiro WPF: nada é aberto, e a frase diz onde isso acontece.</summary>
public sealed class EntregaAoPacienteIndisponivel : IEntregaAoPaciente
{
    public static readonly EntregaAoPacienteIndisponivel Instancia = new();

    public const string Frase = "O WhatsApp deste computador abre no aplicativo instalado — envie por e-mail ou pelo CRM.";

    public Task<ResultadoDoAto> EntregarNoWhatsAppAsync(byte[] pdf, string nomeArquivo, string? telefone, string paciente, string legenda)
        => Task.FromResult(new ResultadoDoAto(Frase: Frase, EhErro: true));
}
