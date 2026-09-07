namespace Clinica.Domain.Entities;

/// <summary>Que forma o campo tem — decide o controle da tela e como o valor é lido.</summary>
public enum TipoCampoPersonalizado
{
    /// <summary>Uma linha de texto livre.</summary>
    Texto,

    /// <summary>Várias linhas — a observação que não cabe numa linha.</summary>
    TextoLongo,

    /// <summary>Número com casas decimais (a carga do exercício, o ângulo medido).</summary>
    Numero,

    /// <summary>Sim/Não.</summary>
    SimNao,

    /// <summary>Uma data.</summary>
    Data,

    /// <summary>Uma escolha entre as opções que a clínica cadastrou.</summary>
    Lista
}

/// <summary>
/// Um CAMPO PERSONALIZADO da sessão (set/2026): o que ESTA clínica anota e o sistema não
/// tem.
///
/// O problema
/// ----------
/// A evolução tem doze campos, e eles cobrem o que é comum a toda clínica. O que é
/// específico — "número de agulhas", "aparelho usado", "resposta ao De Qi", "carga do
/// exercício" — era escrito no meio do texto livre, quando era escrito. Dado dentro de
/// prosa não se compara entre sessões, não vira coluna de relatório e não se acha por
/// busca confiável: é a mesma perda que as escalas clínicas resolveram para o escore, um
/// nível abaixo.
///
/// O que ele NÃO é
/// ---------------
/// ⚠️ <b>Não é um construtor de prontuário.</b> Os doze campos da sessão continuam em
/// CÓDIGO, com as regras deles (a validação de "registro vazio", o versionamento, o
/// relatório do convênio, o modelo de evolução). Campo personalizado é acréscimo: ele
/// nunca substitui um campo do sistema, e não pode ser marcado como obrigatório a ponto
/// de impedir o registro — registro clínico que não se consegue salvar é registro que não
/// acontece (a lição da consulta de enfermagem, parcela 73).
///
/// O que é copiado, e por quê
/// --------------------------
/// <see cref="ValorCampoPersonalizado"/> copia o RÓTULO e o TIPO no momento em que o valor
/// é gravado — a mesma regra das escalas (parcela 36), das medidas (37) e do protocolo do
/// mapa corporal (3). Sem a cópia, renomear "Agulhas" para "Nº de agulhas" reescreveria a
/// sessão do mês passado, e desativar o campo deixaria valores gravados sem rótulo — um
/// número solto no prontuário, que é pior do que não ter registrado nada.
///
/// É por copiar que a DEFINIÇÃO pode ser desativada (e só desativada — ver
/// <see cref="Ativo"/>).
/// </summary>
public class CampoPersonalizadoProntuario
{
    public int Id { get; set; }

    /// <summary>Como o profissional o lê na tela: "Nº de agulhas", "Aparelho".</summary>
    public string Rotulo { get; set; } = string.Empty;

    public TipoCampoPersonalizado Tipo { get; set; } = TipoCampoPersonalizado.Texto;

    /// <summary>
    /// As opções da <see cref="TipoCampoPersonalizado.Lista"/>, uma por linha. Vazio nos
    /// outros tipos.
    /// </summary>
    public string? Opcoes { get; set; }

    /// <summary>
    /// A frase de ajuda sob o campo — onde a clínica escreve o que ela entende por aquilo.
    /// Sem isso, dois profissionais preenchem o mesmo campo com coisas diferentes e a
    /// coluna deixa de comparar.
    /// </summary>
    public string? Ajuda { get; set; }

    /// <summary>
    /// A qual MODALIDADE o campo pertence (código do catálogo). Nulo = vale para todas.
    ///
    /// É o que impede a tela de encher: "número de agulhas" não faz sentido na consulta de
    /// psiquiatria, e um campo que aparece onde não serve é o campo que ninguém preenche
    /// — e que faz parar de preencher os outros.
    /// </summary>
    public string? ModalidadeCodigo { get; set; }

    public int Ordem { get; set; }

    /// <summary>
    /// Desativado SOME da tela de escrita e continua aparecendo no que já foi gravado.
    ///
    /// ⚠️ Não há exclusão, e a razão é diferente da do modelo de evolução (que se apaga
    /// mesmo): o modelo é rascunho de apoio e nada aponta para ele; a definição do campo é
    /// a procedência do valor. Apagá-la não apagaria os valores — eles copiam o rótulo —,
    /// mas quebraria o vínculo pelo qual a clínica sabe que a coluna "Agulhas" de 2026 e a
    /// de 2027 são a MESMA pergunta.
    /// </summary>
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; } = DateTime.Now;
    public string? CriadoPor { get; set; }

    /// <summary>As opções da lista, já quebradas e sem linha em branco.</summary>
    public IReadOnlyList<string> OpcoesDaLista =>
        string.IsNullOrWhiteSpace(Opcoes)
            ? []
            : Opcoes.Split('\n')
                .Select(o => o.Trim())
                .Where(o => o.Length > 0)
                .ToList();

    /// <summary>O campo vale para esta modalidade? Sem modalidade declarada, vale para todas.</summary>
    public bool ValePara(string? modalidadeCodigo)
        => string.IsNullOrWhiteSpace(ModalidadeCodigo)
           || string.Equals(ModalidadeCodigo, modalidadeCodigo, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// O VALOR de um campo personalizado numa sessão.
///
/// ⚠️ <b>Rótulo e tipo são COPIADOS aqui</b> — ver <see cref="CampoPersonalizadoProntuario"/>.
/// O <see cref="CampoId"/> é PROCEDÊNCIA (é ele que faz duas sessões responderem à mesma
/// pergunta), nunca a fonte do que a tela escreve.
///
/// O valor é guardado como TEXTO, sempre. Uma coluna por tipo daria seis colunas com cinco
/// nulas em toda linha, e o que se ganha — comparar número com número — se ganha na
/// LEITURA, com o tipo copiado ao lado dizendo como interpretar. Número é gravado em
/// cultura INVARIANTE: dois postos com culturas diferentes escreveriam "2,5" e "2.5" na
/// mesma coluna (a lição do <c>ParametrosService</c>).
/// </summary>
public class ValorCampoPersonalizado
{
    public int Id { get; set; }

    public int EvolucaoId { get; set; }
    public Evolucao? Evolucao { get; set; }

    /// <summary>Procedência: qual campo do catálogo respondeu a isto.</summary>
    public int CampoId { get; set; }
    public CampoPersonalizadoProntuario? Campo { get; set; }

    /// <summary>O rótulo COMO ELE ERA quando o valor foi gravado.</summary>
    public string Rotulo { get; set; } = string.Empty;

    /// <summary>O tipo COMO ELE ERA — é ele que diz como ler o <see cref="Valor"/>.</summary>
    public TipoCampoPersonalizado Tipo { get; set; }

    /// <summary>O que foi escrito. Vazio nunca é gravado — o campo em branco não vira linha.</summary>
    public string Valor { get; set; } = string.Empty;
}
