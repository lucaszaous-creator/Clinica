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
    /// Os CARTÕES de "Novo documento" (out/2026 — a tela de produção da clínica): a mesma
    /// régua de <see cref="ParaEmitir"/>, cada folha com o ícone dela. Lida pela tela de
    /// Receitas e documentos do Consultório e pela escolha de documento da ficha do
    /// paciente — duas listas escritas à mão divergiriam na primeira folha nova.
    /// </summary>
    public static IReadOnlyList<CartaoDeFolha> Cartoes(Permissao acessos)
        => ParaEmitir(acessos).Select(f => new CartaoDeFolha(f, Icone(f))).ToList();

    /// <summary>
    /// O ícone de cada papel, pelo TIPO (nome do traço do ícone da tela). O tipo, e
    /// não a chave: é o tipo que decide qual folha a janela abre, e o desenho segue o que
    /// vai sair. Vale para TODAS as folhas do catálogo — a central de Documentos da
    /// Recepção e do Gerente desenha os mesmos cartões (a mesma tela em todos os módulos).
    /// </summary>
    public static string Icone(FolhaCatalogo folha) => folha.TipoClinico switch
    {
        TipoDocumentoClinico.Receita => "rx",
        TipoDocumentoClinico.Atestado => "document",
        TipoDocumentoClinico.LaudoMedico => "record",
        TipoDocumentoClinico.Comparecimento => "check",
        TipoDocumentoClinico.PedidoExame => "exam",
        TipoDocumentoClinico.RelatorioEvolucao => "chart",
        TipoDocumentoClinico.Anamnese => "model",
        TipoDocumentoClinico.TermoProcedimento or TipoDocumentoClinico.Consentimento => "record",
        // As folhas que não são do atendimento — a central de Documentos as mostra no MESMO
        // cartão: o dinheiro com a pasta, a gestão com o gráfico.
        _ when folha.Natureza == NaturezaFolha.Financeiro => "folder",
        _ when folha.Natureza == NaturezaFolha.Gerencial => "chart",
        _ => "document"
    };

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

/// <summary>
/// Um cartão de "Novo documento": a folha do catálogo e o ícone dela. O clique entrega a
/// FOLHA a quem emite — é ela que diz qual papel a janela abre (<see cref="FolhaCatalogo.TipoClinico"/>).
/// </summary>
public sealed record CartaoDeFolha(FolhaCatalogo Folha, string Icone)
{
    public string Rotulo => Folha.Rotulo;
    public string Descricao => Folha.Descricao;
}
