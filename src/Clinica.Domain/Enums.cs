namespace Clinica.Domain;

/// <summary>Convênios atendidos pela clínica. Cada um possui um fluxograma próprio de faturamento.</summary>
public enum Convenio
{
    UnimedPadrao,        // Unimed Costa do Sol (Padrão)
    UnimedIntercambio,   // Unimed Costa do Sol Intercâmbio
    Amil,
    Petrobras,
    Personalizado        // Regra configurável pela clínica (RegraGenerica), para convênios novos
}

public enum Sexo
{
    Masculino,
    Feminino
}

/// <summary>O que foi realizado no atendimento do dia. Base para o motor de regras gerar os códigos.</summary>
public enum ModalidadeAtendimento
{
    AcupunturaSimples,   // apenas acupuntura
    AcupunturaComEletro, // acupuntura + eletroacupuntura (gera 2º código quando o convênio permite)
    BsvComAcupuntura,    // Bloqueio Simpático Venoso + acupuntura no mesmo dia
    BsvApenas,           // apenas BSV
    Consulta             // consulta médica avulsa (a especialidade é informada no lançamento)
}

/// <summary>Natureza de cada código/guia faturável.</summary>
public enum TipoCodigo
{
    Consulta,
    Acupuntura,
    Eletroacupuntura,
    Bsv,
    ConsultaEspecialidade // usado pela Petrobras (acupuntura faturada como consulta de especialidade)
}

/// <summary>
/// Especialidades de consulta atendidas pela clínica. Usadas na consulta avulsa (discriminam a
/// especialidade para os relatórios) e na rotação da Petrobras (acupuntura faturada como consulta).
/// </summary>
public enum Especialidade
{
    Psiquiatria,
    Geriatria,
    Ginecologia,
    Acupuntura,
    ClinicaDaDor,
    Endocrinologia
}

/// <summary>Ordem do código no dia. O 2º código é o historicamente esquecido.</summary>
public enum OrdemCodigo
{
    Primeiro,
    Segundo
}

/// <summary>Como o 2º código deve ser obtido, conforme o fluxograma do convênio.</summary>
public enum FormaObtencao
{
    NaoAplica, // código já é faturado no ato, não há 2ª obtenção
    App,       // paciente gera pelo aplicativo (QR Code)
    Sistema,   // secretária solicita diretamente no sistema do convênio
    Ligacao    // é preciso ligar para o paciente e pedir a autorização
}

/// <summary>Ciclo de vida do código quanto à baixa (registro de faturamento).</summary>
public enum StatusCodigo
{
    Aberto,        // gerado, aguardando baixa
    Baixado,       // secretária registrou que a guia foi efetivada
    NaoAplicavel,  // documentado que não pôde ser faturado (ex.: sem app, sem especialidade disponível)
    NaoConformidade // pendência não resolvida numa rodada e justificada; silenciada até ser reaberta
}

/// <summary>Categoria/semáforo do paciente conforme os fluxogramas.</summary>
public enum Categoria
{
    Verde,    // faz acu + eletro e o 2º código será obtido
    Amarela,  // faz apenas acupuntura; não haverá 2º código
    Vermelha  // Petrobras (código de prioridade vermelho)
}

/// <summary>Situação da guia quanto a glosa (recusa do convênio).</summary>
public enum StatusGlosa
{
    SemGlosa,      // não glosada
    Glosada,       // recusada pelo convênio
    Reapresentada, // reenviada após a glosa
    Recuperada     // aceita após reapresentação
}

/// <summary>
/// Como o paciente chegou à clínica (CRM). Serve à pergunta que a direção faz no fim do
/// mês: "de onde vem gente nova?" — sem ela, campanha e indicação disputam o mesmo
/// crédito e ninguém sabe o que financiar.
///
/// Não há "Não informado" no enum de propósito: o campo é anulável, e null significa
/// exatamente "ninguém perguntou". Um valor de enum para isso empataria "não perguntamos"
/// com "perguntamos e ele não soube dizer", que são fatos diferentes.
/// </summary>
public enum OrigemPaciente
{
    /// <summary>Indicação de outro paciente, de um profissional ou de terceiro.</summary>
    Indicacao,

    /// <summary>Encaminhado por médico ou outro serviço de saúde.</summary>
    Encaminhamento,

    /// <summary>Achou a clínica na internet (busca, site, mapa).</summary>
    Internet,

    /// <summary>Veio das redes sociais da clínica.</summary>
    RedesSociais,

    /// <summary>Passou em frente e entrou; fachada, placa, vizinhança.</summary>
    Fachada,

    /// <summary>Veio pelo convênio (lista de credenciados da operadora).</summary>
    Convenio,

    /// <summary>Campanha da própria clínica (recall, NPS, promoção).</summary>
    Campanha,

    /// <summary>Perguntado, e a resposta não cabe nas anteriores.</summary>
    Outro
}
