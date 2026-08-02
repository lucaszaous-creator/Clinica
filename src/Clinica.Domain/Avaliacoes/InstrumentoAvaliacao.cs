namespace Clinica.Domain.Avaliacoes;

/// <summary>
/// Quanto a faixa de pontuação pede atenção. NÃO é diagnóstico: é a leitura da própria
/// escala, e quem interpreta é quem assina o prontuário.
///
/// Existe em <c>Clinica.Domain</c>, e não reaproveita o <c>NivelUrgencia</c> da camada de
/// aplicação, porque o domínio não referencia a aplicação — e porque as duas escalas
/// respondem coisas diferentes: uma diz "esta guia vence hoje", a outra diz "este escore
/// caiu numa faixa que merece conversa".
/// </summary>
public enum GravidadeFaixa
{
    /// <summary>Faixa sem sinal de alerta.</summary>
    Normal,

    /// <summary>Faixa intermediária — acompanhar.</summary>
    Atencao,

    /// <summary>Faixa alta da escala.</summary>
    Alerta
}

/// <summary>Uma alternativa de resposta de um item, com o peso que ela vale no escore.</summary>
public sealed record OpcaoItem(int Valor, string Rotulo);

/// <summary>
/// Uma pergunta do instrumento.
///
/// O <see cref="Enunciado"/> é COPIADO para a avaliação no momento em que ela é aplicada
/// — nunca referenciado. É a mesma regra do protocolo do mapa corporal e do preço por
/// convênio: se a avaliação apontasse para o item, corrigir a redação de uma pergunta
/// hoje reescreveria o que o paciente respondeu no mês passado, e o prontuário deixaria
/// de descrever o que de fato foi perguntado.
/// </summary>
public sealed record ItemInstrumento(
    string Codigo,
    string Enunciado,
    IReadOnlyList<OpcaoItem> Opcoes)
{
    /// <summary>Maior peso que este item pode contribuir ao escore.</summary>
    public int ValorMaximo => Opcoes.Count == 0 ? 0 : Opcoes.Max(o => o.Valor);

    /// <summary>O rótulo da alternativa de peso <paramref name="valor"/>, se existir.</summary>
    public string? RotuloDe(int valor)
        => Opcoes.FirstOrDefault(o => o.Valor == valor)?.Rotulo;
}

/// <summary>
/// Uma faixa de resultado: de <see cref="De"/> a <see cref="Ate"/> pontos, inclusive nas
/// duas pontas. <see cref="Interpretacao"/> é o que a escala publica sobre a faixa, e é
/// COPIADA para a avaliação junto com o nome — pelo mesmo motivo do enunciado.
/// </summary>
public sealed record FaixaInstrumento(
    int De,
    int Ate,
    string Nome,
    string Interpretacao,
    GravidadeFaixa Gravidade)
{
    public bool Contem(int pontuacao) => pontuacao >= De && pontuacao <= Ate;
}

/// <summary>
/// Um instrumento de avaliação clínica: um questionário pontuado, com faixas de leitura.
///
/// Mora no DOMÍNIO e em CÓDIGO, não numa tabela, pelo mesmo desenho do motor de regras de
/// convênio (<c>IRegraConvenio</c> + <c>RegistroRegras</c>): a pontuação de uma escala não
/// é configuração da clínica, é a definição publicada dela. Deixar a clínica editar o peso
/// de um item pelo sistema produziria um escore que continua se chamando PHQ-9 sem ser um.
///
/// O que a clínica de fato precisa trocar é a REDAÇÃO em português, quando ela usa uma
/// tradução validada específica. Trocar o texto aqui é seguro por construção: as avaliações
/// já aplicadas guardam o enunciado que estava valendo no dia, e não mudam.
///
/// O sistema PONTUA e REGISTRA — ele não diagnostica. A faixa é a leitura da escala; a
/// conduta é de quem assina o prontuário.
/// </summary>
public interface IInstrumentoAvaliacao
{
    /// <summary>Código estável, gravado na avaliação (ex.: <c>PHQ9</c>). Nunca renomear.</summary>
    string Codigo { get; }

    /// <summary>Nome exibido, copiado para a avaliação aplicada.</summary>
    string Nome { get; }

    /// <summary>Uma linha sobre o que a escala mede — é o que a tela mostra na escolha.</summary>
    string Descricao { get; }

    /// <summary>De onde a escala vem, para constar no prontuário e no papel.</summary>
    string Fonte { get; }

    /// <summary>
    /// Período que o paciente deve considerar ao responder ("nas últimas duas semanas").
    /// Fica separado do enunciado porque vale para o questionário inteiro, e repeti-lo
    /// item a item é o que faz as pessoas pararem de ler.
    /// </summary>
    string Janela { get; }

    /// <summary>
    /// Especialidades (códigos do catálogo) em que este instrumento é oferecido. Vazio =
    /// oferecido a todas.
    /// </summary>
    IReadOnlyList<string> Especialidades { get; }

    IReadOnlyList<ItemInstrumento> Itens { get; }

    IReadOnlyList<FaixaInstrumento> Faixas { get; }

    /// <summary>
    /// Unidade do escore, quando não é "pontos" (o Oswestry sai em %). Só muda o rótulo:
    /// o valor gravado continua sendo o resultado da <see cref="Pontuar"/>.
    /// </summary>
    string Unidade => "pontos";

    /// <summary>Maior escore possível — a tela mostra "12 de 27", não "12".</summary>
    int PontuacaoMaxima => Itens.Sum(i => i.ValorMaximo);

    /// <summary>
    /// Cair é melhorar?
    ///
    /// Vale para quase todas as escalas — menos dor, menos sintoma, menos risco. O Katz
    /// inverte (6 é o melhor resultado possível), e sem esta propriedade a curva de
    /// evolução chamaria a recuperação funcional de piora. É informação do INSTRUMENTO,
    /// nunca um palpite da tela: quem lê o gráfico não tem como saber o sentido da escala.
    /// </summary>
    bool MelhorQuandoMenor => true;

    /// <summary>
    /// O instrumento sabe pontuar com item em branco?
    ///
    /// O padrão é NÃO, e o serviço recusa a aplicação incompleta — porque numa escala de
    /// soma o item omitido vira zero, e zero significa "não tenho esse sintoma": a
    /// pergunta que o paciente não quis responder viraria uma resposta que ele não deu.
    ///
    /// O Oswestry é a exceção, e não por conveniência: a regra publicada dele manda
    /// calcular o percentual sobre as seções respondidas.
    /// </summary>
    bool AceitaItemOmitido => false;

    /// <summary>
    /// Escore a partir das respostas (código do item → peso escolhido). O padrão é a
    /// soma simples, que é como a maioria das escalas funciona; quem converte para outra
    /// unidade (o Oswestry, que vira porcentagem) sobrescreve.
    /// </summary>
    int Pontuar(IReadOnlyDictionary<string, int> respostas)
        => Itens.Sum(i => respostas.TryGetValue(i.Codigo, out var v) ? v : 0);

    /// <summary>
    /// A faixa em que o escore cai. Null quando o escore está fora de todas as faixas —
    /// que é defeito de definição do instrumento, e a tela prefere dizer "sem faixa" a
    /// inventar uma leitura.
    /// </summary>
    FaixaInstrumento? FaixaDe(int pontuacao)
        => Faixas.FirstOrDefault(f => f.Contem(pontuacao));

    /// <summary>
    /// Alerta que vem de UM item, independentemente do escore total. Null quando não há.
    ///
    /// Existe por causa do item 9 do PHQ-9 (ideação suicida): somado, ele vale 3 dos 27
    /// pontos e um paciente pode marcá-lo respondendo o resto com zeros — escore 3, faixa
    /// "sintomas mínimos", e a única resposta que exigia conduta imediata desaparece
    /// dentro do total. Escala que esconde o item mais grave na média é pior do que
    /// escala nenhuma, porque tem cara de leitura completa.
    /// </summary>
    string? AlertaDeItem(IReadOnlyDictionary<string, int> respostas) => null;
}
