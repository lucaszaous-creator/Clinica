namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// "Enviar agora ao paciente?" ao fim da emissão (out/2026) — o único pedaço da emissão de
/// documento que continua WPF: a janela de envio é a MESMA de todas as portas
/// (<c>AcoesDoDocumento.EnviarAsync</c> com <c>aposEmitir: true</c> → <c>EnvioAoPacienteWindow</c>:
/// e-mail primeiro, WhatsApp pelo CRM depois). Reescrevê-la aqui seria a segunda definição do
/// envio, e a trilha (quem, por qual canal, para onde) tem de sair de um lugar só.
///
/// O formulário de emissão (<c>DocumentoForm</c>, Blazor) é quem DECIDE se pergunta
/// (<c>EmissaoDeDocumentoNaTela.OferecerEnvio</c>: o papel pode sair como arquivo E quem emite
/// tem o bit do tipo); a ponte só abre a janela. Onde ela não existe (a prévia, o bUnit) vale
/// <see cref="EnvioDoDocumentoAoPacienteIndisponivel"/>, que responde em silêncio: o documento já
/// está emitido, e não perguntar é o desfecho de quem fecha a janela com "Agora não".
/// </summary>
public interface IEnvioDoDocumentoAoPaciente
{
    /// <summary>Abre a pergunta de envio do documento recém-emitido. Silencioso = a pessoa disse "Agora não".</summary>
    Task<ResultadoDoAto> PerguntarAposEmitirAsync(DocumentoEmitido documento);
}

/// <summary>Sem hospedeiro WPF: nada a perguntar, e a emissão segue para o fim.</summary>
public sealed class EnvioDoDocumentoAoPacienteIndisponivel : IEnvioDoDocumentoAoPaciente
{
    public static readonly EnvioDoDocumentoAoPacienteIndisponivel Instancia = new();

    public Task<ResultadoDoAto> PerguntarAposEmitirAsync(DocumentoEmitido documento)
        => Task.FromResult(new ResultadoDoAto(Silencioso: true));
}
