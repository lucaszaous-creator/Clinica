using Clinica.Application.Servicos;

namespace Clinica.Application.Modelos;

/// <summary>
/// O PASSO 3 da emissão — "o que vai sair" — escrito antes de o papel existir (set/2026,
/// mockup 5 aprovado, <c>docs/mockups/documentos-depois-de-escolher.html</c>).
///
/// A tela de Documentos passou a ser três passos (QUEM · QUAL PAPEL · O QUE VAI SAIR), o
/// mesmo desenho que a clínica já aprovou no Novo atendimento. Este é o conteúdo do
/// terceiro: para quem o papel sai, o que ele é, o que FALTA e — o que nenhuma tela dizia —
/// **o que o clique vai fazer**.
///
/// ⚠️ ELA NÃO PROMETE O QUE NÃO ENTREGA, e é por isso que ela é curta.
/// O mockup desenhava o número da folha ("2026/0189") no passo 3; ele é atribuído na
/// EMISSÃO, por ano e por tipo, e adivinhá-lo aqui seria escrever na tela um número que a
/// próxima emissão concorrente torna falso. É a mesma decisão da prévia da guia, que não
/// tem número porque ele nasce na baixa — e a tarja diz isso. O desvio do desenho aprovado
/// fica DECLARADO, como manda a regra da casa; o que se promete é <see cref="Garantias"/>,
/// que é verdade de toda folha.
///
/// ⚠️ **Mora na Application, e não na ViewModel**: é o que a tela AFIRMA, e afirmação que
/// só existe dentro de um projeto WPF é afirmação que o <c>dotnet test</c> não alcança
/// (a regra da <c>GradeSemana</c> e do <c>ResumoSessaoAnterior</c>).
/// </summary>
/// <param name="Rotulo">O nome da folha, como o catálogo o escreve.</param>
/// <param name="Grupo">Do atendimento · do dinheiro · da gestão.</param>
/// <param name="Descricao">
/// O que a folha É. Hoje ela vive só na DICA do cartão, e dica é o que ninguém lê: quem não
/// conhece a folha descobre o que ela é errando — e documento numerado não se apaga.
/// </param>
/// <param name="ParaQuem">
/// O destinatário por extenso. É a metade que impede o erro mais caro desta tela: emitir no
/// nome de quem estava escolhido antes.
/// </param>
/// <param name="OQueAcontece">
/// O que o clique FAZ. Quatro das folhas não imprimem nada ao serem clicadas — uma navega
/// para o Caixa, duas abrem a coleta de uma assinatura, uma abre a janela onde se escreve —
/// e a leitura natural de um botão chamado "Emitir" é que o papel saiu.
/// </param>
/// <param name="OQueFalta">
/// A pendência, já escrita. Vazio quando não falta nada.
/// </param>
/// <param name="PodeEmitir">Se o botão do passo 3 está aceso.</param>
/// <param name="AcaoRotulo">O rótulo do botão, o MESMO do cartão.</param>
public sealed record PreviaDaFolha(
    string Rotulo,
    string Grupo,
    string Descricao,
    string ParaQuem,
    string OQueAcontece,
    string OQueFalta,
    bool PodeEmitir,
    string AcaoRotulo)
{
    /// <summary>Tem pendência escrita — o que a tela pinta como buraco.</summary>
    public bool TemPendencia => OQueFalta.Length > 0;

    /// <summary>
    /// O que vale para TODA folha, e por isso é constante em vez de campo: numeração por
    /// ano, código de conferência e a imutabilidade. Não promete assinatura digital — quem
    /// a tem é o Consultório, com e-CPF, e dizer isso aqui seria a garantia aparente que
    /// este projeto recusa desde a parcela 3.
    /// </summary>
    public const string GarantiasDaFolha =
        "O número sai na emissão, por ano. A folha leva um código de conferência no rodapé, "
        + "não se apaga — cancela-se com motivo — e a segunda via sai idêntica à que o "
        + "paciente levou.";

    /// <summary>
    /// A mesma frase, alcançável por binding. ⚠️ <c>{Binding Garantias}</c> NÃO chegaria à
    /// constante: binding do WPF não alcança membro ESTÁTICO, e o texto sairia vazio — sem
    /// erro, sem aviso, e verde em todas as redes (a lição do combo de formas de pagamento
    /// da conciliação).
    /// </summary>
    public string Garantias => GarantiasDaFolha;

    /// <summary>
    /// Monta o passo 3 a partir do que o cartão já sabe. Os três últimos parâmetros vêm da
    /// tela porque é ela que conhece a escolha do momento; a REGRA de o que cada folha faz
    /// no clique vem do catálogo, que é o mesmo que roteia a emissão de verdade.
    /// </summary>
    /// <param name="folha">A folha escolhida no passo 2.</param>
    /// <param name="acaoRotulo">O rótulo do botão, montado pela tela junto do cartão.</param>
    /// <param name="podeEmitir">Se dá para emitir agora.</param>
    /// <param name="pendencia">O que falta, já escrito pela tela. Vazio = nada falta.</param>
    /// <param name="pacienteNome">Quem foi escolhido no passo 1, quando houve.</param>
    /// <param name="pacienteDocumento">O documento dele, já formatado. Opcional.</param>
    /// <param name="periodo">
    /// O período por extenso, para a única folha que não é de uma pessoa. Ela não passa pelo
    /// passo 1, e exigir paciente a tornaria INALCANÇÁVEL — a regra 3 do faturamento
    /// ("não tire capacidade de quem já a usava"), que a parcela 88 já invocou para deixar
    /// esta tela de fora da busca obrigatória.
    /// </param>
    public static PreviaDaFolha Montar(
        FolhaCatalogo folha,
        string acaoRotulo,
        bool podeEmitir,
        string pendencia,
        string? pacienteNome,
        string? pacienteDocumento,
        string periodo)
    {
        var paraQuem = folha.Exigencia == ExigenciaFolha.Periodo
            ? (string.IsNullOrWhiteSpace(periodo) ? "O período escolhido" : periodo)
            : Destinatario(pacienteNome, pacienteDocumento);

        return new PreviaDaFolha(
            folha.Rotulo,
            GrupoDe(folha),
            folha.Descricao,
            paraQuem,
            OQueOCliqueFaz(folha),
            pendencia ?? string.Empty,
            podeEmitir,
            acaoRotulo);
    }

    /// <summary>
    /// Nome e documento na mesma linha. Sem paciente escolhido a frase DIZ isso, em vez de
    /// sair vazia: linha em branco no lugar do destinatário se lê como dado que não
    /// carregou, e aqui ela é a resposta certa — ainda não escolheram ninguém.
    /// </summary>
    private static string Destinatario(string? nome, string? documento)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "Ninguém escolhido ainda";
        return string.IsNullOrWhiteSpace(documento) ? nome : $"{nome} · {documento}";
    }

    private static string GrupoDe(FolhaCatalogo folha) => folha.Natureza switch
    {
        NaturezaFolha.Clinico => "DO ATENDIMENTO",
        NaturezaFolha.Financeiro => "DO DINHEIRO",
        _ => "DA GESTÃO"
    };

    /// <summary>
    /// A frase que o produto nunca teve: o que acontece ao clicar. Ela sai da MESMA
    /// <see cref="ExigenciaFolha"/> que roteia a emissão de verdade — duas definições de
    /// "o que este botão faz" divergiriam na primeira correção, e a que ficasse para trás
    /// seria a da TELA, isto é, a que a pessoa lê.
    /// </summary>
    private static string OQueOCliqueFaz(FolhaCatalogo folha) => folha.Exigencia switch
    {
        ExigenciaFolha.LancamentoNoCaixa =>
            "Leva você ao Caixa. O recibo nasce SOBRE o lançamento que comprova o pagamento — "
            + "emiti-lo daqui deixaria sair dois recibos do mesmo dinheiro.",

        ExigenciaFolha.TermoParaAssinar =>
            "Abre a coleta da assinatura do paciente — na tela do balcão, no monitor dele ou "
            + "pelo link do WhatsApp. O papel só fica cumprido quando alguém confere e conclui.",

        ExigenciaFolha.Periodo =>
            "Monta a conferência do período e abre o PDF. Não é de uma pessoa e não fica "
            + "guardada: é montada na hora, então não há segunda via.",

        ExigenciaFolha.PacienteComProntuario =>
            "É MONTADA do que já está no prontuário e sai direto em PDF — não se digita nada "
            + "aqui. Sem registro no prontuário ela sairia só com cabeçalho e rodapé.",

        _ when folha.Chave == "orcamento" =>
            "Abre a janela do orçamento, onde você escreve as linhas e o valor. Orçamento "
            + "PROPÕE um valor; quem comprova pagamento é o recibo.",

        _ =>
            "Abre a janela onde você escreve o conteúdo, escolhe quem assina e emite."
    };
}
