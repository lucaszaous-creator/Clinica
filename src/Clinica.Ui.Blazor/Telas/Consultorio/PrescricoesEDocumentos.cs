namespace Clinica.Ui.Blazor.Telas;

/// <summary>
/// Clínico → Prescrições (nov/2026): a MESMA central de Documentos, com a aba "Para assinar" ligada — pedido do dono
/// ("Melhor, coloca em Clínico > Prescrições"). Uma porta por pergunta: não é tela nova nem item novo de menu; é a
/// central de sempre, e a Recepção → Documentos continua sem a aba.
/// </summary>
public sealed class PrescricoesEDocumentos : DocumentosDaRecepcao
{
    public PrescricoesEDocumentos() => ComParaAssinar = true;
}
