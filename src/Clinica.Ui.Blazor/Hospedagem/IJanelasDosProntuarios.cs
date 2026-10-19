using Clinica.Application.Modelos;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// As janelas que a tela de Prontuários abre e que continuam WPF (out/2026): o quick-view do
/// prontuário (evoluções, anamnese e anexos), a assinatura da anamnese com o certificado
/// ICP-Brasil (o MESMO fluxo das Prescrições — o arquivo assinado é salvo e aberto) e a tela de
/// atendimento para escrever. A prévia responde com <see cref="JanelasDosProntuariosIndisponiveis"/>.
/// </summary>
public interface IJanelasDosProntuarios
{
    /// <summary>O quick-view da linha; linha de anamnese abre já na aba dela.</summary>
    Task AbrirResumoAsync(LinhaProntuario linha);

    /// <summary>Assina a anamnese da linha. Devolve a frase do desfecho, ou nulo quando a pessoa desistiu.</summary>
    Task<ResultadoDaAssinatura?> AssinarAnamneseAsync(LinhaProntuario linha);

    /// <summary>"Novo prontuário" = a tela de atendimento de quem clicou; falso quando o posto não alcança.</summary>
    Task<bool> AbrirAtendimentoAsync();
}

/// <summary>O desfecho da assinatura: a frase e se ela é de erro (o arquivo pode não ter sido salvo).</summary>
public sealed record ResultadoDaAssinatura(string Frase, bool Erro);

/// <summary>Sem hospedeiro WPF (prévia, testes): nada abre, e a tela diz isso com uma frase.</summary>
public sealed class JanelasDosProntuariosIndisponiveis : IJanelasDosProntuarios
{
    public static readonly JanelasDosProntuariosIndisponiveis Instancia = new();

    public const string Frase = "Esta ação abre no aplicativo do Consultório.";

    public Task AbrirResumoAsync(LinhaProntuario linha) => Task.CompletedTask;
    public Task<ResultadoDaAssinatura?> AssinarAnamneseAsync(LinhaProntuario linha) => Task.FromResult<ResultadoDaAssinatura?>(null);
    public Task<bool> AbrirAtendimentoAsync() => Task.FromResult(false);
}
