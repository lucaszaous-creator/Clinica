namespace Clinica.Domain.Regras;

/// <summary>Um valor de domínio do padrão TISS: o código que vai no XML e o termo que a pessoa lê.</summary>
public sealed record ValorTiss(string Codigo, string Termo)
{
    /// <summary>Como aparece numa lista de escolha: "06 - CRM".</summary>
    public string Rotulo => $"{Codigo} - {Termo}";
}

/// <summary>
/// Os domínios do padrão TISS 4.01.00 usados na GUIA DE CONSULTA, copiados do XSD oficial
/// (<c>tissSimpleTypesV4_01_00.xsd</c>). O que vai no XML é o CÓDIGO, e ele tem forma: o conselho
/// leva zero à esquerda ("06"), a UF é o código do IBGE ("33", nunca "RJ") e o CBO é uma lista
/// fechada — valor fora dela torna a guia inválida.
///
/// ⚠️ Não acrescente valor aqui de memória: <c>DominiosTissTests</c> confere cada lista contra a
/// enumeração do XSD que está em <c>tests/Clinica.Tests/Tiss/xsd-4.01.00</c>, nos dois sentidos.
/// </summary>
public static class DominiosTiss
{
    /// <summary>Versão do padrão de onde estas listas saíram.</summary>
    public const string Versao = "4.01.00";

    /// <summary><c>dm_indicadorAcidente</c> — campo 17 da guia de consulta.</summary>
    public static readonly IReadOnlyList<ValorTiss> IndicacaoAcidente =
    [
        new("0", "Trabalho"), new("1", "Trânsito"), new("2", "Outros acidentes"), new("9", "Não acidentes"),
    ];

    /// <summary><c>dm_regimeAtendimento</c> — campo 28.</summary>
    public static readonly IReadOnlyList<ValorTiss> RegimeAtendimento =
    [
        new("01", "Ambulatorial"), new("02", "Domiciliar"), new("03", "Internação"),
        new("04", "Pronto socorro"), new("05", "Telessaúde"),
    ];

    /// <summary><c>dm_tipoConsulta</c> — campo 20.</summary>
    public static readonly IReadOnlyList<ValorTiss> TipoConsulta =
    [
        new("1", "Primeira"), new("2", "Seguimento"), new("3", "Pré-natal"), new("4", "Por encaminhamento"),
    ];

    /// <summary><c>dm_conselhoProfissional</c> — campo 13.</summary>
    public static readonly IReadOnlyList<ValorTiss> ConselhoProfissional =
    [
        new("01", "CRESS"), new("02", "COREN"), new("03", "CRF"), new("04", "CRFA"), new("05", "CREFITO"),
        new("06", "CRM"), new("07", "CRN"), new("08", "CRO"), new("09", "CRP"), new("10", "Outros conselhos"),
        new("11", "CRBio"), new("12", "CRBM"), new("13", "CREF"), new("14", "CRMV"), new("15", "CRTR"),
    ];

    /// <summary><c>dm_UF</c> — campo 15. O código é o do IBGE; o termo é a sigla que a pessoa conhece.</summary>
    public static readonly IReadOnlyList<ValorTiss> Uf =
    [
        new("12", "AC"), new("27", "AL"), new("13", "AM"), new("16", "AP"), new("29", "BA"), new("23", "CE"),
        new("53", "DF"), new("32", "ES"), new("52", "GO"), new("21", "MA"), new("31", "MG"), new("50", "MS"),
        new("51", "MT"), new("15", "PA"), new("25", "PB"), new("26", "PE"), new("22", "PI"), new("41", "PR"),
        new("33", "RJ"), new("24", "RN"), new("11", "RO"), new("14", "RR"), new("43", "RS"), new("42", "SC"),
        new("28", "SE"), new("35", "SP"), new("17", "TO"), new("98", "EX (países estrangeiros)"),
    ];

    /// <summary><c>dm_cobEsp</c> — campo 27, opcional.</summary>
    public static readonly IReadOnlyList<ValorTiss> CoberturaEspecial =
    [
        new("01", "Gestante"), new("02", "Pré-operatório"), new("03", "Pós-operatório"),
    ];

    /// <summary><c>dm_saudeOcupacional</c> — campo 29, opcional.</summary>
    public static readonly IReadOnlyList<ValorTiss> SaudeOcupacional =
    [
        new("01", "Admissional"), new("02", "Demissional"), new("03", "Periódico"),
        new("04", "Retorno ao trabalho"), new("05", "Mudança de função"), new("06", "Promoção à saúde"),
    ];

    /// <summary>
    /// <c>dm_tabela</c> — campo 19. Só as tabelas com significado escrito no XSD; a guia de consulta
    /// usa a 22. (O XSD também aceita 90 e 98, sem dizer o que são: ficam de fora da lista de escolha.)
    /// </summary>
    public static readonly IReadOnlyList<ValorTiss> Tabela =
    [
        // Termos curtos de propósito: na lista de escolha, o nome completo da 22 era cortado em 1366.
        new("22", "TUSS procedimentos"), new("00", "Tabela própria da operadora"),
        new("18", "TUSS taxas, diárias e gases"), new("19", "TUSS materiais"), new("20", "TUSS medicamentos"),
    ];

    /// <summary>
    /// <c>dm_CBOS</c> — campo 16. Lista FECHADA do XSD (169 códigos), em ordem alfabética do termo,
    /// com o "não informado" por último. Os termos são os do comentário do próprio XSD.
    /// </summary>
    public static readonly IReadOnlyList<ValorTiss> Cbos =
    [
        new("226310", "- arteterapeuta"),
        new("224105", "- Avaliador físico"),
        new("221205", "- Biomédico"),
        new("322135", "- doula"),
        new("226315", "- equoterapeuta"),
        new("131220", "- gerontologo"),
        new("224110", "- Ludomotricista"),
        new("322120", "- massoterapeuta"),
        new("225355", "- medico radiologista intervencionista"),
        new("226305", "- Musicoterapeuta"),
        new("226320", "- naturologo"),
        new("239440", "- neuropsicopedagogo clinico"),
        new("239445", "- neuropsicopedagogo institucional"),
        new("226110", "- osteopata"),
        new("224115", "- Preparador de atleta"),
        new("224120", "- Preparador físico"),
        new("224140", "- profissional de educação fisica na saude"),
        new("251555", "- psicologo acunpunturista"),
        new("223915", "- psicomotricista"),
        new("226105", "- quiropraxista"),
        new("322125", "- terapeuta holistico"),
        new("224135", "- Treinador profissional de futebol"),
        new("224125", "- Técnico de desporto individual e coletivo (exceto futebol)"),
        new("224130", "- Técnico de laboratório e fiscalização desportiva"),
        new("251605", "Assistente social"),
        new("322230", "Auxiliar de enfermagem"),
        new("221105", "Biólogo"),
        new("223204", "Cirurgião dentista - auditor"),
        new("223208", "Cirurgião dentista - clínico geral"),
        new("223280", "Cirurgião dentista - dentística"),
        new("223284", "Cirurgião dentista - disfunção temporomandibular e dor orofacial"),
        new("223212", "Cirurgião dentista - endodontista"),
        new("223216", "Cirurgião dentista - epidemiologista"),
        new("223220", "Cirurgião dentista - estomatologista"),
        new("223224", "Cirurgião dentista - implantodontista"),
        new("223228", "Cirurgião dentista - odontogeriatra"),
        new("223276", "Cirurgião dentista - odontologia do trabalho"),
        new("223288", "Cirurgião dentista - odontologia para pacientes com necessidades especiais"),
        new("223232", "Cirurgião dentista - odontologista legal"),
        new("223236", "Cirurgião dentista - odontopediatra"),
        new("223240", "Cirurgião dentista - ortopedista e ortodontista"),
        new("223244", "Cirurgião dentista - patologista bucal"),
        new("223248", "Cirurgião dentista - periodontista"),
        new("223256", "Cirurgião dentista - protesista"),
        new("223252", "Cirurgião dentista - protesiólogo bucomaxilofacial"),
        new("223260", "Cirurgião dentista - radiologista"),
        new("223264", "Cirurgião dentista - reabilitador oral"),
        new("223268", "Cirurgião dentista - traumatologista bucomaxilofacial"),
        new("223272", "Cirurgião dentista de saúde coletiva"),
        new("223293", "Cirurgião-dentista da estratégia de saúde da família"),
        new("516210", "Cuidador de idosos"),
        new("223705", "Dietista"),
        new("223505", "Enfermeiro"),
        new("223510", "Enfermeiro auditor"),
        new("223565", "Enfermeiro da estratégia de saúde da família"),
        new("223515", "Enfermeiro de bordo"),
        new("223520", "Enfermeiro de centro cirúrgico"),
        new("223525", "Enfermeiro de terapia intensiva"),
        new("223530", "Enfermeiro do trabalho"),
        new("223535", "Enfermeiro nefrologista"),
        new("223540", "Enfermeiro neonatologista"),
        new("223545", "Enfermeiro obstétrico"),
        new("223550", "Enfermeiro psiquiátrico"),
        new("223555", "Enfermeiro puericultor e pediátrico"),
        new("223560", "Enfermeiro sanitarista"),
        new("223405", "Farmacêutico"),
        new("223415", "Farmacêutico analista clínico"),
        new("223420", "Farmacêutico de alimentos"),
        new("223430", "Farmacêutico em saúde pública"),
        new("223445", "Farmacêutico hospitalar e clínico"),
        new("223435", "Farmacêutico industrial"),
        new("223425", "Farmacêutico práticas integrativas e complementares"),
        new("223440", "Farmacêutico toxicologista"),
        new("223660", "Fisioterapeuta  do trabalho"),
        new("223650", "Fisioterapeuta acupunturista"),
        new("223655", "Fisioterapeuta esportivo"),
        new("223605", "Fisioterapeuta geral"),
        new("223630", "Fisioterapeuta neurofuncional"),
        new("223640", "Fisioterapeuta osteopata"),
        new("223645", "Fisioterapeuta quiropraxista"),
        new("223625", "Fisioterapeuta respiratória"),
        new("223635", "Fisioterapeuta traumato-ortopédica funcional"),
        new("223810", "Fonoaudiólogo"),
        new("223815", "Fonoaudiólogo educacional"),
        new("223820", "Fonoaudiólogo em audiologia"),
        new("223825", "Fonoaudiólogo em disfagia"),
        new("223830", "Fonoaudiólogo em linguagem"),
        new("223835", "Fonoaudiólogo em motricidade orofacial"),
        new("223840", "Fonoaudiólogo em saúde coletiva"),
        new("223845", "Fonoaudiólogo em voz"),
        new("213150", "Físico médico"),
        new("201115", "Geneticista"),
        new("322225", "Instrumentador cirúrgico"),
        new("225260", "Médico  neurocirurgião"),
        new("225105", "Médico acupunturista"),
        new("225110", "Médico alergista e imunologista"),
        new("225148", "Médico anatomopatologista"),
        new("225151", "Médico anestesiologista"),
        new("225115", "Médico angiologista"),
        new("225154", "Médico antroposófico"),
        new("225290", "Médico cancerologista cirúrgico"),
        new("225122", "Médico cancerologista pediátrico"),
        new("225120", "Médico cardiologista"),
        new("225210", "Médico cirurgião cardiovascular"),
        new("225295", "Médico cirurgião da mão"),
        new("225215", "Médico cirurgião de cabeça e pescoço"),
        new("225220", "Médico cirurgião do aparelho digestivo"),
        new("225225", "Médico cirurgião geral"),
        new("225230", "Médico cirurgião pediátrico"),
        new("225235", "Médico cirurgião plástico"),
        new("225240", "Médico cirurgião torácico"),
        new("225305", "Médico citopatologista"),
        new("225125", "Médico clínico"),
        new("225142", "Médico da estratégia de saúde da família"),
        new("225130", "Médico de família e comunidade"),
        new("225135", "Médico dermatologista"),
        new("225140", "Médico do trabalho"),
        new("225203", "Médico em cirurgia vascular"),
        new("225310", "Médico em endoscopia"),
        new("225145", "Médico em medicina de tráfego"),
        new("225150", "Médico em medicina intensiva"),
        new("225315", "Médico em medicina nuclear"),
        new("225320", "Médico em radiologia e diagnóstico por imagem"),
        new("225155", "Médico endocrinologista e metabologista"),
        new("225160", "Médico fisiatra"),
        new("225245", "Médico foniatra"),
        new("225165", "Médico gastroenterologista"),
        new("225170", "Médico generalista"),
        new("225175", "Médico geneticista"),
        new("225180", "Médico geriatra"),
        new("225250", "Médico ginecologista e obstetra"),
        new("225185", "Médico Hematologista"),
        new("225190", "Médico Hemoterapeuta"),
        new("225340", "Médico hemoterapeuta"),
        new("225345", "Médico hiperbarista"),
        new("225195", "Médico Homeopata"),
        new("225103", "Médico infectologista"),
        new("225106", "Médico legista"),
        new("225255", "Médico Mastologista"),
        new("225109", "Médico Nefrologista"),
        new("225350", "Médico neurofisiologista"),
        new("225112", "Médico neurologista"),
        new("225118", "Médico nutrologista"),
        new("225265", "Médico oftalmologista"),
        new("225121", "Médico oncologista"),
        new("225270", "Médico ortopedista e traumatologista"),
        new("225275", "Médico otorrinolaringologista"),
        new("225325", "Médico patologista clínico"),
        new("225335", "Médico patologista clínico / medicina laboratorial"),
        new("225124", "Médico pediatra"),
        new("225127", "Médico pneumologista"),
        new("225280", "Médico proctologista"),
        new("225133", "Médico psiquiatra"),
        new("225330", "Médico radioterapeuta"),
        new("225136", "Médico reumatologista"),
        new("225139", "Médico sanitarista"),
        new("225285", "Médico urologista"),
        new("251545", "Neuropsicólogo"),
        new("223710", "Nutricionista"),
        new("223910", "Ortoptista"),
        new("223570", "Perfusionista"),
        new("203015", "Pesquisador em biologia de microorganismos e parasitas"),
        new("251550", "Psicanalista"),
        new("239425", "Psicopedagogo"),
        new("251510", "Psicólogo clínico"),
        new("223905", "Terapeuta ocupacional"),
        new("322205", "Técnico de enfermagem"),
        new("322220", "Técnico de enfermagem psiquiátrica"),
        new("999999", "CBO-S desconhecido ou não informado pelo solicitante"),
    ];

    /// <summary>O valor está na lista? (Código exato: "6" não é "06".)</summary>
    public static bool Contem(IReadOnlyList<ValorTiss> dominio, string? codigo)
        => codigo is not null && dominio.Any(v => v.Codigo == codigo);

    /// <summary>O termo de um código, ou o próprio código quando ele não está na lista.</summary>
    public static string Termo(IReadOnlyList<ValorTiss> dominio, string? codigo)
        => dominio.FirstOrDefault(v => v.Codigo == codigo)?.Termo ?? codigo ?? string.Empty;

    /// <summary>O código IBGE de uma sigla de UF ("RJ" → "33"); nulo quando a sigla não existe.</summary>
    public static string? UfPelaSigla(string? sigla)
        => string.IsNullOrWhiteSpace(sigla) ? null
            : Uf.FirstOrDefault(v => v.Termo.Equals(sigla.Trim(), StringComparison.OrdinalIgnoreCase))?.Codigo;

    /// <summary>O código de um conselho pela sigla ("CRM" → "06"); nulo quando a sigla não existe.</summary>
    public static string? ConselhoPelaSigla(string? sigla)
        => string.IsNullOrWhiteSpace(sigla) ? null
            : ConselhoProfissional.FirstOrDefault(v => v.Termo.Equals(sigla.Trim(), StringComparison.OrdinalIgnoreCase))?.Codigo;
}
