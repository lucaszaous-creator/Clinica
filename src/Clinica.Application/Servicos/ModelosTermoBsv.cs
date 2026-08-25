using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Os dois termos do BSV com o TEXTO DA CLÍNICA — o conteúdo veio por escrito do advogado
/// dela (ago/2026) e substituiu o rascunho da parcela 67.
///
/// ⚠️ Isto continua NÃO sendo termo de fábrica, e a regra do projeto ("o texto é da
/// clínica, não nosso") continua valendo — só que agora ela é atendida pela via direta:
/// o texto abaixo É o da clínica, reproduzido do documento que o advogado dela redigiu.
/// Por isso a marca "(rascunho — revisar)" morreu: mantê-la num texto que a cliente
/// aprovou mandaria o responsável técnico revisar o que ele já assinou embaixo.
/// Nada mudou no mecanismo: ninguém aplica sozinho (o botão de Configurações continua
/// sendo um clique de quem responde por ele), criar não amarra nada por si, e aplicar
/// COPIA (Lei 13.787/2018) — editar o modelo amanhã não reescreve o que já foi assinado.
///
/// Por que DOIS termos, e o que cada um vale (decisão da cliente, 24/08/2026)
/// --------------------------------------------------------------------------
/// - **TCLE** — o consentimento institucional, longo, lido com calma. O paciente assina
///   UMA vez e ele fica registrado na ficha: vale a partir da assinatura
///   (`SoValeNoDiaDoProcedimento = false`).
/// - **Termo da sessão** — a reavaliação de cada dia. O paciente assina a CADA sessão
///   (`SoValeNoDiaDoProcedimento = true`), porque o que ele afirma ("fui reavaliado NESTA
///   data", "meu estado não mudou desde a última sessão") é sobre o dia, não sobre o ano.
///
/// O que foi ADAPTADO do papel para o meio eletrônico — e é adaptação de FORMA, não de
/// conteúdo:
///
/// 1. Os blocos de IDENTIFICAÇÃO e ASSINATURAS com linhas em branco saíram: a folha
///    emitida já imprime paciente, data e profissional, e a assinatura é colhida na tela
///    (traço + evidência) no lugar da linha de caneta.
/// 2. As lacunas da INDICAÇÃO ("____", Diagnóstico, CID) viraram remissão ao prontuário:
///    um modelo é um texto FIXO copiado na emissão, e lacuna impressa sairia em branco
///    para sempre. A indicação individual mora onde sempre morou — no registro clínico.
/// 3. As listas de "☐" do termo da sessão viraram as DECLARAÇÕES Sim/Não do sistema —
///    que é exatamente o que elas são no papel.
/// 4. As seções 4 a 7 do termo da sessão (avaliação médica, dados da sessão,
///    intercorrências, alta) são atos da EQUIPE, não afirmações do paciente: no sistema
///    eles já têm registro próprio (prescrição, checagem de enfermagem, intercorrências
///    da execução, evolução), cada um com autoria e trilha. O corpo do termo diz onde
///    eles ficam, em vez de duplicá-los num papel que o paciente assina.
/// </summary>
public static class ModelosTermoBsv
{
    /// <summary>
    /// O TCLE institucional — assinado UMA vez, fica na ficha do paciente e vale a partir
    /// da assinatura. Texto do advogado da clínica, seções 1 a 12.
    /// </summary>
    public static ModeloDocumento Consentimento() => new()
    {
        Tipo = TipoDocumentoClinico.TermoProcedimento,
        Nome = "TCLE — Bloqueio Simpático Venoso (BSV)",
        Titulo = "Termo de Consentimento Livre e Esclarecido (TCLE) — Bloqueio Simpático Venoso (BSV)",
        Corpo = string.Join("\n\n",
            "1. FINALIDADE DESTE DOCUMENTO\n"
            + "Este Termo de Consentimento Livre e Esclarecido tem como objetivo registrar "
            + "que fui informado(a), em linguagem clara e adequada, sobre o procedimento "
            + "denominado Bloqueio Simpático Venoso (BSV), incluindo sua finalidade, "
            + "possíveis benefícios, limitações, riscos previsíveis, alternativas "
            + "terapêuticas e cuidados relacionados. Declaro que tive oportunidade de fazer "
            + "perguntas e que recebi esclarecimentos suficientes para decidir, de forma "
            + "livre e consciente, sobre a realização do procedimento.",

            "2. O QUE É O BSV\n"
            + "Fui informado(a) de que o Bloqueio Simpático Venoso (BSV) é um procedimento "
            + "utilizado em determinadas condições dolorosas, com o objetivo de auxiliar no "
            + "controle da dor e na melhora funcional. O procedimento envolverá a "
            + "administração intravenosa de medicamentos considerados necessários pelo "
            + "médico responsável, conforme avaliação clínica e prescrição médica "
            + "individualizada, devidamente registrada no prontuário do paciente. "
            + "Compreendo que a escolha e a indicação dos medicamentos, bem como a eventual "
            + "necessidade de repetição do tratamento, dependerão da minha condição clínica "
            + "e da avaliação do médico responsável.",

            "3. INDICAÇÃO\n"
            + "Fui informado(a) de que o procedimento foi indicado em razão da minha "
            + "condição clínica, conforme avaliação médica registrada no meu prontuário — "
            + "incluindo, quando aplicável, o diagnóstico e o CID correspondentes.",

            "4. BENEFÍCIOS ESPERADOS\n"
            + "Os benefícios podem incluir, entre outros:\n"
            + "• redução da intensidade da dor;\n"
            + "• melhora funcional;\n"
            + "• melhora da qualidade de vida;\n"
            + "• melhora do sono;\n"
            + "• redução da necessidade de outros analgésicos;\n"
            + "• auxílio na reabilitação.\n"
            + "Estou ciente de que os resultados variam entre os pacientes e de que não há "
            + "garantia de melhora completa, nem de cura.",

            "5. ALTERNATIVAS TERAPÊUTICAS\n"
            + "Fui informado(a) de que existem outras opções de tratamento, cuja indicação "
            + "depende da avaliação médica, podendo incluir tratamento medicamentoso, "
            + "fisioterapia, psicoterapia, terapia ocupacional, bloqueios por outras "
            + "técnicas, procedimentos intervencionistas e outras abordagens. Recebi "
            + "explicações sobre essas possibilidades e tive oportunidade de esclarecê-las.",

            "6. RISCOS E POSSÍVEIS EFEITOS ADVERSOS\n"
            + "Compreendo que todo procedimento médico envolve riscos, ainda que realizado "
            + "com técnica adequada e monitorização. Fui informado(a) de que podem ocorrer "
            + "efeitos adversos ou complicações, tais como:\n"
            + "• dor, hematoma ou desconforto no local da punção venosa;\n"
            + "• náuseas e vômitos;\n"
            + "• tontura;\n"
            + "• sonolência;\n"
            + "• alterações transitórias da pressão arterial ou da frequência cardíaca;\n"
            + "• visão borrada ou dupla;\n"
            + "• sensação de flutuação ou dissociação;\n"
            + "• alterações temporárias da percepção;\n"
            + "• ansiedade ou agitação;\n"
            + "• reações alérgicas aos medicamentos.\n"
            + "Fui informado(a) de que, embora raros, eventos graves podem ocorrer e podem "
            + "exigir atendimento médico imediato.",

            "7. CONTRAINDICAÇÕES E INFORMAÇÕES PRESTADAS PELO PACIENTE\n"
            + "Declaro que informei ao médico, de forma completa e verdadeira, sobre: "
            + "alergias conhecidas; doenças pré-existentes; cirurgias anteriores; gravidez "
            + "ou suspeita de gravidez; amamentação; medicamentos em uso; uso de álcool ou "
            + "outras substâncias que possam interferir na segurança do procedimento; e a "
            + "realização recente de procedimentos ou tratamentos que envolvam "
            + "administração de medicamentos por qualquer via, incluindo imunobiológicos, "
            + "infiltrações, infusões, injeções, vacinas ou outros tratamentos "
            + "medicamentosos. Comprometo-me a informar ao médico responsável qualquer "
            + "procedimento ou tratamento medicamentoso realizado antes da sessão de BSV, "
            + "independentemente da via de administração, para avaliação da segurança e da "
            + "possibilidade de realização do procedimento. Compreendo que a omissão de "
            + "informações relevantes pode aumentar os riscos do procedimento.",

            "8. ORIENTAÇÕES\n"
            + "Recebi orientações sobre os cuidados antes e após o procedimento, incluindo, "
            + "quando aplicável:\n"
            + "• necessidade de acompanhante;\n"
            + "• restrições para dirigir ou operar máquinas após o procedimento;\n"
            + "• uso de medicamentos;\n"
            + "• sinais de alerta que justificam contato com a equipe médica ou procura por "
            + "atendimento;\n"
            + "• realizar tricotomia/depilação da região do tórax, conforme orientação da "
            + "equipe assistencial;\n"
            + "• manter jejum de 6 (seis) horas para alimentos sólidos ou, no caso de "
            + "ingestão de líquidos, observar o intervalo de 2 (duas) a 3 (três) horas "
            + "antes do horário previsto para o BSV, conforme orientação da equipe "
            + "assistencial;\n"
            + "• comparecer utilizando roupas leves, folgadas e de fácil abertura, a fim de "
            + "facilitar o posicionamento adequado do paciente e dos dispositivos "
            + "necessários à realização do procedimento.",

            "9. INTERCORRÊNCIAS\n"
            + "Estou ciente de que, caso ocorra alguma intercorrência durante ou após o "
            + "procedimento, serei avaliado(a) pela equipe assistencial, e as medidas "
            + "necessárias serão adotadas e devidamente registradas. Havendo necessidade, "
            + "serei encaminhado(a) para um serviço de maior complexidade, visando à "
            + "continuidade e à segurança da assistência.",

            "10. TRATAMENTO DE DADOS PESSOAIS\n"
            + "Fui informado(a) de que meus dados pessoais e informações de saúde serão "
            + "tratados pela clínica para finalidades assistenciais, administrativas, "
            + "regulatórias e legais relacionadas ao meu atendimento, observada a "
            + "legislação aplicável de proteção de dados.",

            "11. DECLARAÇÃO DO PACIENTE\n"
            + "As declarações que faço constam abaixo, respondidas uma a uma no momento da "
            + "assinatura.",

            "12. REVOGAÇÃO DO CONSENTIMENTO\n"
            + "Estou ciente de que posso retirar meu consentimento antes da realização do "
            + "procedimento, devendo comunicar minha decisão à equipe médica."),
        // As declarações reproduzem a seção 11 do documento do advogado, agrupadas em três
        // respostas — e TODAS continuam redigidas para que "Não" seja um SINAL (a regra da
        // parcela 67): afirmativas, incondicionais, e com o Detalhe falando com o PACIENTE.
        Itens =
        [
            new ItemModelo
            {
                Ordem = 1,
                Descricao = "Li este documento, ou ele me foi integralmente lido, e "
                            + "compreendi o seu conteúdo",
                Detalhe = "Tive oportunidade de fazer perguntas, recebi esclarecimentos "
                          + "suficientes e minhas dúvidas foram respondidas de forma "
                          + "satisfatória."
            },
            new ItemModelo
            {
                Ordem = 2,
                Descricao = "Informei ao médico, de forma completa e verdadeira, minhas "
                            + "alergias, doenças, cirurgias anteriores e medicamentos em uso",
                Detalhe = "Incluindo gravidez ou suspeita, amamentação, uso de álcool ou "
                          + "outras substâncias, e tratamentos recentes com medicamentos "
                          + "por qualquer via — imunobiológicos, infiltrações, infusões, "
                          + "injeções e vacinas."
            },
            new ItemModelo
            {
                Ordem = 3,
                Descricao = "Recebi tempo suficiente para decidir e concordo "
                            + "voluntariamente com a realização do procedimento proposto"
            }
        ]
    };

    /// <summary>
    /// O termo da sessão — a reavaliação de cada dia, assinada a CADA sessão. Reproduz as
    /// seções do paciente do documento do advogado (1 a 3 e a declaração final); as seções
    /// da equipe (4 a 7) têm registro próprio no sistema e o corpo diz onde.
    /// </summary>
    public static ModeloDocumento TermoDaSessao() => new()
    {
        Tipo = TipoDocumentoClinico.TermoProcedimento,
        Nome = "Termo da sessão — BSV",
        Titulo = "Termo de Consentimento Específico para Sessão de Bloqueio Simpático Venoso (BSV)",
        Corpo = string.Join("\n\n",
            "1. REAVALIAÇÃO CLÍNICA\n"
            + "Declaro que fui reavaliado(a) pelo médico responsável nesta data e que tive "
            + "a oportunidade de relatar qualquer alteração ocorrida desde a última "
            + "consulta ou sessão de tratamento — novos sintomas, internações recentes, "
            + "procedimentos realizados desde a última sessão, uso de novos medicamentos, "
            + "reações alérgicas, gravidez ou suspeita e outros fatos relevantes. As "
            + "alterações que relatei foram registradas no meu prontuário.",

            "2. CONFIRMAÇÃO DAS INFORMAÇÕES\n"
            + "Confirmo que li e compreendi o TCLE do Bloqueio Simpático Venoso "
            + "anteriormente assinado e que não houve alteração relevante que exija a sua "
            + "substituição, salvo as informações atualizadas registradas nesta "
            + "reavaliação. Compreendo que os resultados podem variar entre os pacientes e "
            + "estou ciente de que poderão ocorrer efeitos adversos ou complicações "
            + "inerentes ao procedimento.",

            "3. AVALIAÇÃO MÉDICA E REGISTROS DA SESSÃO\n"
            + "A avaliação médica desta data — indicação, contraindicações identificadas e "
            + "conduta —, os dados da sessão, as eventuais intercorrências e as condutas "
            + "adotadas, bem como a alta, são registrados pela equipe assistencial no meu "
            + "prontuário, com a respectiva autoria."),
        // As declarações reproduzem as listas de "☐" das seções 1 a 3 e a declaração final
        // (seção 8) do documento do advogado — e TODAS continuam redigidas para que "Não"
        // seja um SINAL (parcela 67): afirmativas e incondicionais, porque o "Não" acende
        // alerta VERMELHO no balcão e no consultório. Na 2ª, "Não" = "meu estado mudou",
        // que é exatamente o que a equipe precisa ouvir ANTES de começar.
        Itens =
        [
            new ItemModelo
            {
                Ordem = 1,
                Descricao = "Fui reavaliado(a) pelo médico responsável nesta data e "
                            + "relatei tudo o que mudou desde a última sessão",
                Detalhe = "Novos sintomas, internações, procedimentos ou tratamentos "
                          + "realizados, novos medicamentos, reações alérgicas, gravidez "
                          + "ou suspeita e outros fatos relevantes."
            },
            new ItemModelo
            {
                Ordem = 2,
                Descricao = "Meu estado de saúde está sem alterações importantes desde a "
                            + "última sessão",
                Detalhe = "Alterações que existirem devem ser descritas à equipe antes do "
                          + "início do procedimento."
            },
            new ItemModelo
            {
                Ordem = 3,
                Descricao = "Li e compreendi o TCLE do BSV que assinei anteriormente, e "
                            + "as informações dele continuam valendo"
            },
            new ItemModelo
            {
                Ordem = 4,
                Descricao = "Permaneço com indicação clínica, minhas dúvidas sobre esta "
                            + "sessão foram esclarecidas e concordo voluntariamente com a "
                            + "realização desta sessão de BSV"
            }
        ]
    };

    /// <summary>
    /// As duas modalidades de BSV do motor de regras. Ambas exigem os dois termos: quem faz
    /// BSV com acupuntura no mesmo dia fez BSV, e o consentimento é do BSV.
    /// </summary>
    public static IReadOnlyList<ModalidadeAtendimento> ModalidadesDoBsv { get; } =
    [
        ModalidadeAtendimento.BsvApenas,
        ModalidadeAtendimento.BsvComAcupuntura
    ];
}
