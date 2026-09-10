using Clinica.Application.Servicos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que a SESSÃO ABERTA sabe e o papel pode herdar (set/2026, tela 1 do mockup aprovado
/// "documentos nas quatro telas").
///
/// Ela existe para o documento não abrir em branco na frente de quem acabou de escrever o
/// que ele precisa: o CID da hipótese, a hora em que o paciente chegou, quantas sessões
/// ele já teve. Nada aqui é adivinhado — cada campo é copiado de um dado GRAVADO, e a
/// tela DIZ de onde ele veio.
/// </summary>
/// <param name="PacienteId">De quem é a sessão.</param>
/// <param name="Cid">
/// O CID da hipótese desta sessão, quando o profissional o escreveu. Vai para o atestado.
/// </param>
/// <param name="Hipotese">
/// A hipótese diagnóstica por extenso. Vai para o pedido de exame como INDICAÇÃO CLÍNICA —
/// que é o que o laboratório pede e é o que ela é.
/// </param>
/// <param name="Chegada">Quando o paciente chegou ao balcão, do carimbo da fila.</param>
/// <param name="Saida">
/// Quando ele saiu da sala. Nulo enquanto a sessão não foi encerrada — e nulo é a resposta
/// certa: escrever "agora" numa declaração seria afirmar um fato que ainda não aconteceu.
/// </param>
/// <param name="SessoesRegistradas">
/// Quantas sessões o prontuário tem. É o que decide se o relatório de evolução tem o que
/// montar: sem sessão nenhuma ele sairia com o cabeçalho e o rodapé.
/// </param>
/// <param name="AgendamentoId">
/// O HORÁRIO a que o papel se refere, e ele é o que faz a coluna "de onde veio" das listas
/// de documento dizer alguma coisa.
///
/// ⚠️ Até aqui a janela de emissão NÃO gravava este vínculo: <c>DocumentoClinico.
/// AgendamentoId</c> existia, <c>ProcedenciaDaSessao</c> sabia escrever a frase, as três
/// listas a mostravam — e o único escritor era o termo assinado pelo paciente. Toda
/// receita emitida na consulta aparecia como "avulsa", ao lado da sessão que a produziu.
/// Dado gravável sem escritor é o defeito recorrente do projeto pelo avesso.
/// </param>
/// <param name="EvolucaoId">
/// A sessão ESCRITA, quando ela já existe. Nulo enquanto o profissional não salvou — e
/// nulo é a resposta certa: apontar para uma evolução que não foi gravada seria um vínculo
/// para lugar nenhum.
/// </param>
/// <param name="DataHoraDaSessao">
/// Quando a sessão é. Só serve para escrever a procedência no cabeçalho da janela.
/// </param>
public sealed record HerancaDaSessao(
    int PacienteId,
    string? Cid = null,
    string? Hipotese = null,
    TimeOnly? Chegada = null,
    TimeOnly? Saida = null,
    int SessoesRegistradas = 0,
    int? AgendamentoId = null,
    int? EvolucaoId = null,
    DateTime? DataHoraDaSessao = null)
{
    /// <summary>
    /// "da sessão de hoje, 14h00" — a frase que a janela de emissão põe no cabeçalho, para
    /// quem abriu saber a qual atendimento aquele papel vai ficar preso.
    ///
    /// Vazia quando não há horário: o documento é AVULSO, e inventar "da sessão de hoje"
    /// para uma receita emitida fora de consulta seria afirmar um vínculo que o banco não
    /// tem. O <c>hoje</c> chega por parâmetro para a frase ser testável.
    /// </summary>
    public string Procedencia(DateOnly hoje) => DataHoraDaSessao switch
    {
        null => string.Empty,
        { } d when DateOnly.FromDateTime(d) == hoje => $"da sessão de hoje, {d:HH'h'mm}",
        { } d => $"da sessão de {d:dd/MM}, {d:HH'h'mm}"
    };
}

/// <summary>Um cartão da coluna ENTREGAR AGORA, já escrito.</summary>
/// <param name="Chave">A chave do catálogo (<see cref="FolhaCatalogo.Chave"/>).</param>
/// <param name="Rotulo">O nome curto, como quem atende o pede.</param>
/// <param name="Heranca">
/// O que ESTE papel já traz preenchido, por extenso. Nunca vazio: "em branco — você
/// escreve" é resposta, e cartão mudo faria a pessoa clicar para descobrir.
/// </param>
/// <param name="VemDaSessao">
/// A frase acima descreve algo COPIADO da sessão (e a tela a pinta de acento). Falso
/// quando o papel abre em branco — e aí a frase é cinza, porque não há o que conferir.
/// </param>
/// <param name="PodeEmitir">O acesso desta pessoa alcança a emissão desta folha.</param>
/// <param name="Pendencia">
/// O que falta para o botão funcionar, já escrito. Vazio quando dá para emitir agora.
/// </param>
public sealed record FolhaParaEntregar(
    string Chave,
    string Rotulo,
    string Heranca,
    bool VemDaSessao,
    bool PodeEmitir,
    string Pendencia);

/// <summary>
/// A coluna ENTREGAR AGORA do atendimento: os papéis que quem está com o paciente na sala
/// entrega na hora, cada um dizendo <b>o que ele já traz preenchido</b>.
///
/// Por que ela mora aqui, e não na ViewModel
/// -----------------------------------------
/// Ela DECIDE o que a tela afirma sobre um documento clínico — "CID M54.5 da sua hipótese"
/// é uma promessa, e a janela tem de cumpri-la. Regra que decide o que a tela AFIRMA mora
/// onde o <c>dotnet test</c> alcança (a lição da <c>GradeSemana</c> e do
/// <c>ResumoSessaoAnterior</c>); dentro de um projeto WPF ela apodreceria sem rede.
///
/// ⚠️ DESVIO DECLARADO DO MOCKUP APROVADO. O desenho mostra a receita dizendo "já com a
/// Passiflora que você escreveu" — e isso não se entrega sem LER o texto livre da conduta
/// à procura de nome de medicamento. Adivinhar ali é pôr no papel que a farmácia avia algo
/// que o profissional não escreveu como prescrição, que é a garantia aparente que este
/// projeto recusa desde a parcela 3. A receita abre EM BRANCO e o cartão diz isso. Desvio
/// de mockup aprovado se escreve, não se comete em silêncio.
///
/// ⚠️ Só entram as folhas que o acesso desta pessoa ALCANÇA (a regra da parcela 59, e o
/// motivo é o mesmo da central: cartão apagado dizendo "sem permissão" ANUNCIA que existe
/// um relatório de evolução daquele paciente, que é o que não se quer contar a quem não
/// pode lê-lo).
/// </summary>
public static class FolhasParaEntregar
{
    /// <summary>
    /// As cinco folhas da coluna, na ordem em que quem atende as pede — e não na ordem do
    /// catálogo, que é a da central. Termo e anamnese ficam de fora: o termo é colhido com
    /// assinatura do paciente (outra janela, outro gesto) e a anamnese é da primeira
    /// avaliação, não do que se entrega ao fim da sessão. Os dois continuam na aba
    /// "Prescrições e documentos", que é a pasta clínica dela.
    /// </summary>
    public static readonly IReadOnlyList<string> Ordem =
        ["receita", "atestado", "pedido-exame", "comparecimento", "relatorio-evolucao"];

    /// <summary>Monta a coluna para esta sessão e este conjunto de acessos.</summary>
    public static IReadOnlyList<FolhaParaEntregar> Montar(HerancaDaSessao heranca, Permissao acessos)
    {
        var cartoes = new List<FolhaParaEntregar>();

        foreach (var chave in Ordem)
        {
            var folha = CentralDocumentosService.Folha(chave);
            if (folha is null) continue;
            if (!acessos.HasFlag(folha.PermissaoVer)) continue;

            var (frase, daSessao) = Descrever(chave, heranca);
            var podeEmitir = acessos.HasFlag(folha.PermissaoEmitir);

            // O relatório é MONTADO do prontuário: sem sessão nenhuma ele sairia com o
            // cabeçalho, o rodapé e nada no meio — papel numerado que não diz nada.
            var semBase = chave == "relatorio-evolucao" && heranca.SessoesRegistradas == 0;

            var pendencia =
                !podeEmitir
                    ? $"Seu acesso permite ver, não emitir ({PerfisAcesso.Rotular(folha.PermissaoEmitir)})."
                    : semBase
                        ? "Sem sessão registrada para montar o relatório."
                        : string.Empty;

            cartoes.Add(new FolhaParaEntregar(
                chave,
                RotuloCurto(chave, folha.Rotulo),
                frase,
                daSessao,
                podeEmitir && !semBase,
                pendencia));
        }

        return cartoes;
    }

    /// <summary>
    /// O nome como quem atende o diz. "Receituário" e "Solicitação de exames" são os
    /// rótulos da central, que fala com a clínica inteira; na coluna do atendimento eles
    /// disputam 340 px com o que o cartão tem a explicar.
    /// </summary>
    private static string RotuloCurto(string chave, string padrao) => chave switch
    {
        "receita" => "Receita",
        "pedido-exame" => "Pedido de exame",
        "comparecimento" => "Comparecimento",
        "relatorio-evolucao" => "Relatório de evolução",
        _ => padrao
    };

    /// <summary>
    /// O que cada papel herda desta sessão — a frase do cartão e se ela descreve algo
    /// COPIADO (que a tela pinta de acento) ou o papel abrindo em branco.
    /// </summary>
    private static (string Frase, bool DaSessao) Descrever(string chave, HerancaDaSessao h) => chave switch
    {
        // Ver o desvio declarado no comentário da classe: não se lê texto livre à procura
        // de medicamento.
        "receita" => ("em branco — você escreve o que prescreve", false),

        "atestado" => string.IsNullOrWhiteSpace(h.Cid)
            ? ("dias e data — o CID você escreve", false)
            : ($"CID {h.Cid.Trim()} da sua hipótese", true),

        "pedido-exame" => string.IsNullOrWhiteSpace(h.Hipotese)
            ? ("em branco — você escreve os exames", false)
            : ($"indicação clínica: {Curto(h.Hipotese)}", true),

        "comparecimento" => h switch
        {
            { Chegada: { } c, Saida: { } s } => ($"esteve aqui {c:HH\\:mm} → {s:HH\\:mm}", true),
            { Chegada: { } c } => ($"está aqui desde {c:HH\\:mm}", true),
            _ => ("sem hora de chegada registrada", false)
        },

        "relatorio-evolucao" => h.SessoesRegistradas == 0
            ? ("nenhuma sessão registrada ainda", false)
            : ($"{h.SessoesRegistradas} sessão(ões) · monta sozinho", true),

        _ => (string.Empty, false)
    };

    /// <summary>
    /// A hipótese cabe no cartão. Corte por PALAVRA e com reticências: cortar no meio de
    /// uma palavra faz o cartão parecer defeito, e sem o sinal ninguém sabe que há mais.
    /// </summary>
    private static string Curto(string texto)
    {
        var limpo = texto.Trim();
        if (limpo.Length <= 34) return limpo;

        var corte = limpo.LastIndexOf(' ', 34);
        return (corte > 12 ? limpo[..corte] : limpo[..34]) + "…";
    }
}
