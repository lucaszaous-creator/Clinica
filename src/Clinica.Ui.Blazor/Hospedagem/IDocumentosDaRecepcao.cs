using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// O que a central de Documentos (Blazor, out/2026) pede ao aplicativo e que continua WPF: os
/// atos do documento clínico já emitido (a coleta do termo virou formulário Blazor em out/2026 —
/// <see cref="ColetaDeTermoNaPagina"/>; e o "Assinar" das telas Blazor passa por
/// <see cref="IAssinaturaNaTela"/>)
/// (<c>AcoesDoDocumento</c> — 2ª via, assinar com e-CPF, enviar, renovar o link, tirar do ar,
/// cancelar).
///
/// O papel ESCRITO (receita, atestado, laudo, declaração, pedido de exame), a NOVA VERSÃO do
/// laudo e o orçamento livre deixaram de ser janela WPF (out/2026): são formulários Blazor que a
/// tela abre dentro dela mesma — e por isso não passam mais por aqui.
///
/// Reescrever qualquer um deles aqui seria a segunda definição — e assinar é a área
/// congelada do SafeID. A implementação WPF (<c>DocumentosNoAplicativoWpf</c>) chama EXATAMENTE
/// o que o <c>DocumentosViewModel</c> chamava; onde ela não existe (a prévia, os testes) vale
/// <see cref="DocumentosDaRecepcaoIndisponiveis"/>, e a tela DIZ que a ação é do aplicativo.
///
/// A primeira barreira de permissão é da tela; janela e ato conferem de novo, como sempre.
/// </summary>
public interface IDocumentosDaRecepcao
{
    /// <summary>Falso onde as janelas não existem: a tela avisa em vez de abrir nada.</summary>
    bool Disponivel { get; }

    /// <summary>O Caixa está aberto neste aplicativo — o recibo nasce de um lançamento dele.</summary>
    bool CaixaNesteAplicativo { get; }


    /// <summary>Os atos que a linha oferece — a mesma resolução das outras portas (estado E bit do tipo).</summary>
    AtosDoDocumento Atos(DocumentoEmitido doc);

    /// <summary>
    /// Executa um dos atos sobre o documento clínico. A <see cref="AtoDoDocumento.NovaVersao"/> não
    /// chega aqui: a tela a abre no <c>DocumentoForm</c>.
    /// </summary>
    Task<ResultadoDoAto> ExecutarAsync(AtoDoDocumento ato, DocumentoEmitido doc);
}

/// <summary>Um documento CLÍNICO já emitido, como os atos precisam vê-lo.</summary>
public sealed record DocumentoEmitido(
    int DocumentoId, int? PacienteId, string Numero, TipoDocumentoClinico Tipo, string Rotulo,
    bool Cancelado, bool Assinado, bool LinkNoAr, bool JaTeveLink)
{
    /// <summary>A linha da lista, quando ela é de um documento clínico do catálogo.</summary>
    public static DocumentoEmitido? De(FolhaEmitida e, DateOnly hoje)
    {
        if (e.Natureza != NaturezaFolha.Clinico) return null;
        if (CentralDocumentosService.Folha(e.Chave)?.TipoClinico is not { } tipo) return null;
        return new DocumentoEmitido(e.DocumentoId, e.PacienteId, e.Numero, tipo, e.FolhaRotulo,
            e.Cancelado, e.Assinado, e.PublicadoAte is { } ate && ate >= hoje, e.JaTeveLink);
    }

    /// <summary>O documento recém-emitido (montada do prontuário), para a 2ª via sem esperar a lista.</summary>
    public static DocumentoEmitido De(DocumentoClinico d, string rotulo, DateOnly hoje) => new(
        d.Id, d.PacienteId, d.Numero, d.Tipo, rotulo, d.Cancelado, d.AssinadoEletronicamente,
        d.LinkNoAr(hoje), !string.IsNullOrWhiteSpace(d.TokenPublicacao));
}

public enum AtoDoDocumento { SegundaVia, NovaVersao, Assinar, Enviar, RenovarLink, TirarDoAr, Cancelar }

/// <summary>Os atos que a linha oferece, na ordem do menu "⋯".</summary>
public sealed record AtosDoDocumento(
    bool NovaVersao, bool Assinar, bool Enviar, bool RenovarLink, bool TirarDoAr, bool Cancelar)
{
    /// <summary>A 2ª via está SEMPRE no menu — "⋯" que abre vazio é o botão que não faz nada.</summary>
    public IEnumerable<AtoDoDocumento> NoMenu()
    {
        yield return AtoDoDocumento.SegundaVia;
        if (NovaVersao) yield return AtoDoDocumento.NovaVersao;
        if (Assinar) yield return AtoDoDocumento.Assinar;
        if (Enviar) yield return AtoDoDocumento.Enviar;
        if (RenovarLink) yield return AtoDoDocumento.RenovarLink;
        if (TirarDoAr) yield return AtoDoDocumento.TirarDoAr;
        if (Cancelar) yield return AtoDoDocumento.Cancelar;
    }

    /// <summary>Os rótulos do menu — os MESMOS do <c>MenuDoDocumento</c> do shell.</summary>
    public static string Rotulo(AtoDoDocumento a) => a switch
    {
        AtoDoDocumento.SegundaVia => "Imprimir a 2ª via",
        AtoDoDocumento.NovaVersao => "Alterar (emitir nova versão)…",
        AtoDoDocumento.Assinar => "Assinar com o e-CPF…",
        AtoDoDocumento.Enviar => "Enviar ao paciente…",
        AtoDoDocumento.RenovarLink => "Renovar o link",
        AtoDoDocumento.TirarDoAr => "Tirar do ar",
        _ => "Cancelar o documento…"
    };
}

/// <summary>
/// O que o ato fez. <c>Mudou</c> = a lista precisa ser relida; <c>Silencioso</c> = a pessoa
/// desistiu num diálogo e a tela não mexe em mensagem nenhuma.
/// </summary>
public sealed record ResultadoDoAto(bool Mudou = false, string? Frase = null, bool EhErro = false, bool Silencioso = false)
{
    public static readonly ResultadoDoAto Indisponivel = new(Frase:
        "Esta ação é do aplicativo da Recepção — abra o Documentos no aplicativo instalado.", EhErro: true);
}

/// <summary>Onde não há janelas WPF (a prévia, os testes): tudo responde "não disponível", e a tela diz.</summary>
public sealed class DocumentosDaRecepcaoIndisponiveis : IDocumentosDaRecepcao
{
    public static readonly DocumentosDaRecepcaoIndisponiveis Instancia = new();

    public bool Disponivel => false;

    /// <summary>Verdadeiro: quem responde se o Caixa existe é o <c>Host.NavegarAsync</c>, que a prévia já simula.</summary>
    public bool CaixaNesteAplicativo => true;


    /// <summary>
    /// A nova versão do laudo (Blazor, a tela a abre) e o cancelamento são oferecidos — pelo
    /// estado E pelo bit do tipo, a mesma resolução do <c>DocumentoNaTela</c> do shell.
    /// </summary>
    public AtosDoDocumento Atos(DocumentoEmitido doc)
    {
        var pode = SessaoUsuario.Atual.Pode(CentralDocumentosService.AcessoParaEmitir(doc.Tipo));
        return new(TipoDocumentoInfo.PermiteNovaVersao(doc.Tipo) && !doc.Cancelado && doc.PacienteId is not null && pode,
            false, false, false, false, !doc.Cancelado && pode);
    }

    public Task<ResultadoDoAto> ExecutarAsync(AtoDoDocumento ato, DocumentoEmitido doc) => Task.FromResult(ResultadoDoAto.Indisponivel);
}
