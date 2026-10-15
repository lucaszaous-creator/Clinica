using Clinica.Application.Servicos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Em que pé está um documento já emitido, como o histórico o pinta.</summary>
public enum EstadoDoDocumento
{
    /// <summary>Emitido e válido; a assinatura, se houver, é a da caneta no papel.</summary>
    Emitido,

    /// <summary>Selado com certificado (e-CPF) — o arquivo vale sozinho.</summary>
    Assinado,

    /// <summary>Cancelado com motivo. Fica na lista, marcado: a via impressa não some.</summary>
    Cancelado
}

/// <summary>A situação de um documento: o estado (que decide a cor) e a frase.</summary>
public sealed record SituacaoDoDocumento(EstadoDoDocumento Estado, string Texto);

/// <summary>
/// Os DOCUMENTOS do paciente no Consultório — a régua de emitir e o HISTÓRICO (out/2026 — a clínica: "os laudos
/// precisam ser fáceis de LOCALIZAR e REIMPRIMIR").
///
/// A lista do Consultório era uma pilha corrida, sem busca nem filtro: quem tinha o número
/// "2026/0014" do laudo na mão rolava a vida inteira do paciente procurando a linha. A
/// central da Recepção já resolvia isto com <see cref="BuscaNasFolhas"/>, e este histórico
/// usa a MESMA regra — duas buscas de "papel que já saiu" divergiriam na primeira correção,
/// e a que ficasse para trás é a que a clínica usaria para concluir que o laudo não existe.
///
/// ⚠️ Mora na Application, e não na ViewModel do Consultório: o que decide a frase, a cor e
/// o vazio é o que a tela AFIRMA, e afirmação dentro de um projeto WPF é afirmação que o
/// <c>dotnet test</c> não alcança (a regra da <c>GradeSemana</c>).
/// </summary>
public static class DocumentosDoConsultorio
{
    /// <summary>
    /// A régua "Novo documento" — as folhas CLÍNICAS de paciente que este acesso pode
    /// EMITIR, na ordem do catálogo: receituário, atestado, LAUDO MÉDICO, solicitação de
    /// exames, comparecimento, relatório de evolução e anamnese.
    ///
    /// ⚠️ Pelo catálogo, e não por botões escritos à mão: folha nova (o laudo, out/2026)
    /// aparece sozinha, e cada uma traz o BIT dela — o comparecimento pede
    /// <c>EditarPaciente</c>, a receita e o laudo pedem <c>Prescrever</c>.
    ///
    /// Os dois TERMOS assinados pelo paciente ficam de fora: não se emitem por janela
    /// nenhuma — colhem-se com o traço na tela —, e a pendência deles tem porta própria na
    /// tela do paciente.
    /// </summary>
    public static IReadOnlyList<FolhaCatalogo> ParaEmitir(Permissao acessos)
        => CentralDocumentosService.Catalogo
            .Where(f => f.Natureza == NaturezaFolha.Clinico
                        && f.Exigencia is ExigenciaFolha.Paciente or ExigenciaFolha.PacienteComProntuario
                        && acessos.HasFlag(f.PermissaoEmitir))
            .ToList();

    /// <summary>
    /// As opções do combo "Tipo": "Todas as folhas", depois as folhas CLÍNICAS do catálogo
    /// que este acesso pode LER (o laudo médico entre elas), e por fim os tipos que só a
    /// lista trouxe.
    ///
    /// ⚠️ Pelo catálogo, e não só pelo que o paciente já tem: o tipo sumir do combo
    /// justamente quando não há laudo nenhum faria "este paciente não tem laudo" virar
    /// "o sistema não sabe filtrar laudo" (a regra de <see cref="BuscaNasFolhas.Tipos"/>).
    /// </summary>
    public static IReadOnlyList<string> Tipos(Permissao acessos, IEnumerable<FolhaEmitida> lidas)
        => BuscaNasFolhas.Tipos(
            CentralDocumentosService.Catalogo
                .Where(f => f.TipoClinico is not null && CentralDocumentosService.PodeVer(acessos, f))
                .Select(f => f.Rotulo),
            lidas);

    /// <summary>
    /// A situação do documento. A ordem é a da gravidade: cancelado vence assinado — um
    /// laudo selado e depois cancelado NÃO vale, e pintá-lo de verde mandaria reimprimir o
    /// papel que foi substituído.
    /// </summary>
    public static SituacaoDoDocumento Situacao(DocumentoClinico d)
    {
        if (d.Cancelado)
            return new(EstadoDoDocumento.Cancelado, $"Cancelado · {d.CanceladoEm:dd/MM/yyyy}");
        if (d.AssinadoEletronicamente)
            return new(EstadoDoDocumento.Assinado, $"Assinado e-CPF · {d.AssinadoEm:dd/MM/yyyy}");
        return new(EstadoDoDocumento.Emitido, "Emitido");
    }

    /// <summary>
    /// O resumo da lista inteira, antes da busca: "12 documentos deste paciente · 2
    /// cancelados". Contado sobre o que foi LIDO — o recorte da busca é dito à parte
    /// (<see cref="BuscaNasFolhas.Recorte"/>), senão "12" acima de uma lista de três faria
    /// procurar os nove que faltam.
    /// </summary>
    public static string Resumo(IReadOnlyCollection<FolhaEmitida> lidas)
    {
        if (lidas.Count == 0) return string.Empty;

        var frase = lidas.Count == 1
            ? "1 documento deste paciente"
            : $"{lidas.Count} documentos deste paciente";

        var cancelados = lidas.Count(f => f.Cancelado);
        return cancelados switch
        {
            0 => frase,
            1 => $"{frase} · 1 cancelado",
            _ => $"{frase} · {cancelados} cancelados"
        };
    }

    /// <summary>
    /// O vazio, e são DUAS perguntas: "este paciente não tem documento" e "nenhum bate com a
    /// busca" mandam fazer coisas diferentes. Responder a primeira quando a verdade é a
    /// segunda faria o médico emitir de novo um laudo que já existe — com número novo, e
    /// dois papéis do mesmo ato no prontuário.
    /// </summary>
    public static (string Titulo, string Descricao) Vazio(bool haDocumentos)
        => haDocumentos
            ? ("Nenhum documento bate com a busca",
               "Confira o número, o código de conferência ou o tipo escolhido, ou use Limpar busca.")
            : ("Nenhum documento emitido para este paciente",
               "Receita, atestado, laudo e pedido de exame aparecem aqui assim que forem emitidos.");
}
