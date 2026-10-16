using System.Globalization;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// Um cartão do passo 2 ("Qual papel?") da central de Documentos da Recepção, já com o que
/// falta e o que o clique faz.
/// </summary>
/// <param name="Folha">A folha do catálogo.</param>
/// <param name="Grupo">"Do atendimento", "Do dinheiro" ou "Da gestão" — a ordem do catálogo.</param>
/// <param name="AcaoRotulo">"Emitir", "Gerar PDF", "Colher assinatura…" ou "Ir para o Caixa".</param>
/// <param name="PodeGerar">O botão do passo 3 aceso.</param>
/// <param name="Pendencia">O que falta, já escrito. Vazio = nada falta.</param>
/// <param name="ExigePaciente">A folha é de uma pessoa (sete das nove).</param>
public sealed record CartaoDaCentral(
    FolhaCatalogo Folha,
    string Grupo,
    string AcaoRotulo,
    bool PodeGerar,
    string Pendencia,
    bool ExigePaciente);

/// <summary>Uma linha de "O que já saiu", já escrita.</summary>
public sealed record LinhaDaCentral(
    FolhaEmitida Folha,
    string Data,
    string Para,
    string Detalhe,
    string Situacao,
    string Link,
    bool LinkNoAr,
    bool PodeMexer);

/// <summary>
/// O que a central de Documentos da Recepção AFIRMA (out/2026 — a tela virou Blazor). Era
/// decidido dentro do <c>DocumentosViewModel</c> WPF, onde o <c>dotnet test</c> não alcança;
/// as frases e a disponibilidade são as MESMAS, palavra por palavra.
/// </summary>
public static class CentralDeDocumentosNaTela
{
    private static readonly CultureInfo Brasil = new("pt-BR");

    /// <summary>A frase de quem sabe ver e não emitir.</summary>
    private static string SoVer(Permissao emitir, string verbo)
        => $"Seu acesso permite ver, não {verbo} ({PerfisAcesso.Rotular(emitir)}).";

    private static bool Tem(Permissao acessos, Permissao bit)
        => bit == Permissao.Nenhuma || (acessos & bit) == bit;

    /// <summary>
    /// Os cartões que ESTE acesso alcança (parcela 59 — o cartão que a pessoa não alcança SOME,
    /// não fica apagado: apagado anunciaria que o papel existe), com a disponibilidade de agora.
    /// </summary>
    /// <param name="acessos">As permissões EFETIVAS (sem login, todas).</param>
    /// <param name="temPaciente">Há paciente escolhido no passo 1.</param>
    /// <param name="caixaNesteAplicativo">O Caixa existe neste executável (o recibo navega até lá).</param>
    public static IReadOnlyList<CartaoDaCentral> Cartoes(
        Permissao acessos, bool temPaciente, bool caixaNesteAplicativo)
        => CentralDocumentosService.CatalogoPara(acessos)
            .Select(f => Cartao(f, acessos, temPaciente, caixaNesteAplicativo))
            .ToList();

    public static CartaoDaCentral Cartao(
        FolhaCatalogo f, Permissao acessos, bool temPaciente, bool caixaNesteAplicativo)
    {
        var podeEmitir = Tem(acessos, f.PermissaoEmitir);
        var grupo = f.Natureza switch
        {
            NaturezaFolha.Clinico => "Do atendimento",
            NaturezaFolha.Financeiro => "Do dinheiro",
            _ => "Da gestão"
        };
        var acao = f.Exigencia switch
        {
            ExigenciaFolha.Periodo => "Gerar PDF",
            ExigenciaFolha.LancamentoNoCaixa => "Ir para o Caixa",
            // O clique NAVEGA para a coleta, e o rótulo diz isso: "Emitir" prometeria que o
            // termo saiu.
            ExigenciaFolha.TermoParaAssinar => "Colher assinatura…",
            _ => "Emitir"
        };

        bool pode;
        string pendencia;
        switch (f.Exigencia)
        {
            case ExigenciaFolha.Paciente:
            case ExigenciaFolha.PacienteComProntuario:
                pode = temPaciente && podeEmitir;
                pendencia = !temPaciente ? "Escolha o paciente para liberar."
                    : podeEmitir ? string.Empty : SoVer(f.PermissaoEmitir, "emitir");
                break;

            case ExigenciaFolha.LancamentoNoCaixa:
                // O recibo comprova dinheiro que JÁ entrou e aponta para o lançamento — nasce
                // no Caixa, e daqui sairiam dois recibos do mesmo pagamento.
                pode = caixaNesteAplicativo;
                pendencia = caixaNesteAplicativo
                    ? "Nasce do lançamento no caixa, para não sair recibo em duplicidade."
                    : "O módulo Financeiro não está aberto neste aplicativo.";
                break;

            case ExigenciaFolha.TermoParaAssinar:
                pode = temPaciente && podeEmitir;
                pendencia = !temPaciente ? "Escolha o paciente para liberar."
                    : podeEmitir ? string.Empty : SoVer(f.PermissaoEmitir, "colher");
                break;

            default: // Periodo
                pode = podeEmitir;
                pendencia = podeEmitir
                    ? "Usa o período da aba “O que já saiu”."
                    : SoVer(f.PermissaoEmitir, "emitir");
                break;
        }

        return new CartaoDaCentral(f, grupo, acao, pode, pendencia,
            f.Exigencia is ExigenciaFolha.Paciente or ExigenciaFolha.PacienteComProntuario
                or ExigenciaFolha.TermoParaAssinar);
    }

    /// <summary>
    /// "Tem horário hoje às 14:00" — vazio quando não tem, e vazio é a resposta certa: inventar
    /// uma presença que a agenda não registra seria pior do que calar.
    /// </summary>
    public static string ContextoDeHoje(TimeOnly? horaDeHoje)
        => horaDeHoje is { } h ? $"tem horário hoje às {h.ToString("HH':'mm", CultureInfo.InvariantCulture)}" : string.Empty;

    /// <summary>
    /// O link está no ar HOJE. O dia do vencimento conta inteiro — quem publicou até 09/10
    /// espera que a receita abra no dia 09.
    /// </summary>
    public static bool NoAr(FolhaEmitida e, DateOnly hoje)
        => e.PublicadoAte is { } ate && ate >= hoje;

    /// <summary>
    /// Dá para mexer (cancelar, republicar) nesta linha: o acesso de EMITIR a folha dela. Folha
    /// fora do catálogo responde NÃO — papel cujo acesso ninguém declarou não vira botão aceso.
    /// </summary>
    public static bool PodeMexer(FolhaEmitida e, Permissao acessos)
        => CentralDocumentosService.Folha(e.Chave) is { } f && Tem(acessos, f.PermissaoEmitir);

    /// <summary>A linha de "O que já saiu", com as mesmas frases da tela WPF.</summary>
    public static LinhaDaCentral Linha(FolhaEmitida e, DateOnly hoje, Permissao acessos)
    {
        var noAr = NoAr(e, hoje);
        var detalhe = string.Join(" · ", new[]
            {
                e.Valor is { } v ? v.ToString("C2", Brasil) : null,
                e.Profissional,
                string.IsNullOrWhiteSpace(e.CriadoPor) ? null : $"por {e.CriadoPor}",
                // O código à vista: é por ele que a busca acha o papel que alguém trouxe na mão.
                string.IsNullOrWhiteSpace(e.CodigoVerificacao) ? null : $"conferência {e.CodigoVerificacao}"
            }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return new LinhaDaCentral(
            e,
            e.Data.ToString("dd/MM/yyyy", Brasil),
            e.Paciente ?? "—",
            detalhe,
            // Cancelada aparece MARCADA, nunca sumindo.
            e.Cancelado ? $"CANCELADA — {e.MotivoCancelamento}" : $"código {e.CodigoVerificacao}",
            !e.JaTeveLink ? string.Empty
                : noAr ? $"link no ar até {e.PublicadoAte!.Value.ToString("dd/MM/yyyy", Brasil)}"
                : "link fora do ar",
            noAr,
            PodeMexer(e, acessos));
    }
}
