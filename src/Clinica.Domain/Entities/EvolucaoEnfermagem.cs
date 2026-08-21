namespace Clinica.Domain.Entities;

/// <summary>
/// A EVOLUÇÃO DE ENFERMAGEM — o registro de quem executa (parcela 71).
///
/// Por que ela não é a <see cref="Evolucao"/> com outro autor
/// ---------------------------------------------------------
/// São dois registros de naturezas e responsabilidades diferentes. A evolução responde
/// <i>"o que eu concluí e o que decidi fazer"</i> e é de quem ATENDEU; esta responde
/// <i>"o que eu observei no paciente, e a que horas"</i> e é de quem EXECUTOU, com o
/// registro no conselho ao lado. A evolução é UMA por sessão, salva em várias passadas até
/// ficar certa; esta são VÁRIAS por passagem — 14h20, 14h50, 15h10 —, cada uma um fato
/// pontual que não se reescreve.
///
/// ⚠️ Reusar a <see cref="Evolucao"/> com um campo de tipo foi medido e recusado, e o
/// argumento não é conceitual: quatro leitores quebrariam <b>em silêncio</b>.
/// <c>ConsultorioService.EvolucaoDoHorario</c> cai para paciente + data, então a anotação
/// da técnica cobriria a sessão do médico e ela sumiria de "sessões sem evolução" — elo
/// partido não vira erro, vira LISTA VAZIA; <c>EvolucoesNoPeriodoAsync</c> filtra
/// <c>ProfissionalId == null</c>, então a anotação apareceria no "Meu dia" de TODOS os
/// profissionais; <c>ProdutividadeProfissional.CompletudeProntuario</c> passaria a medir o
/// trabalho de outra pessoa; e o relatório que o paciente leva ao CONVÊNIO imprime o autor
/// por sessão. É o mesmo argumento com que a parcela 42 recusou enfiar a folha de infusão
/// em <c>TipoDocumentoClinico</c>.
///
/// O paciente é o dono; a infusão é PROCEDÊNCIA
/// -------------------------------------------
/// A clínica disse que <b>todo paciente passa pela enfermagem</b>, e é isso que decide a
/// forma: o dono é o <see cref="PacienteId"/>, e <see cref="PrescricaoInternaId"/> é
/// opcional. Amarrar a evolução à folha faria a enfermagem só poder registrar quem tem
/// infusão marcada — e deixaria sem lugar o curativo, a sala de observação, a triagem e a
/// reação que aparece meia hora depois de a folha ter sido encerrada (não há reabertura).
///
/// ⚠️ E o vínculo anulável é <b>o ponto 1 do compromisso de conformidade</b>, não
/// conveniência: chave estrangeira obrigatória força <c>Cascade</c>, e apagar uma folha
/// levaria junto registro clínico — que é exatamente a cascata que a parcela 60 achou no
/// botão de excluir paciente.
///
/// As regras que ela herda da checagem, e por quê
/// ---------------------------------------------
/// - <b>A hora é INFORMADA, nunca o relógio.</b> A técnica observa às 14h20 e senta para
///   digitar às 14h50; carimbar <c>DateTime.Now</c> mentiria sobre quando a reação
///   aconteceu, que é o dado clínico. O relógio vai em <see cref="RegistradoEm"/> AO LADO,
///   e a diferença entre os dois é o que uma auditoria de enfermagem procura.
/// - <b>Hora futura é recusada.</b> Registro adiantado é o hábito que faz aparecer como
///   observado um paciente que saiu antes.
/// - <b>Não se apaga: RETIFICA-SE</b> (linha nova apontando a anterior, com motivo), e
///   cancelar é outra coisa — é para a evolução lançada no paciente errado. As duas
///   continuam na base e saem MARCADAS no papel: imprimir só o valor final faria a via
///   esconder o que a trilha guarda.
/// - <b>Quem assina é quem fez LOGIN.</b> Nome e conselho ficam COPIADOS aqui porque o
///   usuário pode ser renomeado ou desativado, e o prontuário tem de continuar dizendo
///   quem escreveu.
/// </summary>
public class EvolucaoEnfermagem
{
    public int Id { get; set; }

    /// <summary>O DONO. É por ele que a exportação, a guarda e a recusa de excluir a ficha
    /// alcançam este registro sem passar pela prescrição.</summary>
    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>
    /// A folha de infusão, quando a passagem foi uma. PROCEDÊNCIA, nunca dona — ver o
    /// comentário da classe sobre por que é anulável.
    /// </summary>
    public int? PrescricaoInternaId { get; set; }
    public PrescricaoInterna? Prescricao { get; set; }

    /// <summary>O horário da agenda, quando a passagem veio de um. Também procedência.</summary>
    public int? AgendamentoId { get; set; }
    public Agendamento? Agendamento { get; set; }

    /// <summary>O dia do FATO observado.</summary>
    public DateOnly Data { get; set; }

    /// <summary>A hora do FATO, informada por quem observou. Ver o comentário da classe.</summary>
    public TimeOnly Hora { get; set; }

    /// <summary>O que foi observado. Obrigatório — linha sem conteúdo não é registro.</summary>
    public string Texto { get; set; } = string.Empty;

    /// <summary>
    /// Marca a evolução que descreve algo fora do esperado (reação, queda de pressão,
    /// extravasamento). É o único campo que a MÁQUINA lê do que aconteceu: ele acende o
    /// selo na fila, viaja para a tela de atendimento do médico e, um dia, responde à
    /// direção quantas infusões tiveram intercorrência e em quais medicamentos.
    ///
    /// ⚠️ É <c>bool</c> e não enum de propósito: a taxonomia da SAE (histórico, diagnóstico,
    /// prescrição e evolução de enfermagem) é grande demais para nascer chutada, e valor
    /// novo de enum lido pelos cinco apps paga o preço do incidente de 14/08/2026. Se a
    /// clínica precisar da distinção, ela entra depois como coluna aditiva.
    /// </summary>
    public bool Intercorrencia { get; set; }

    // ---- Sinais vitais (todos opcionais) ----

    /// <summary>
    /// ⚠️ Os sinais vitais moram AQUI, e não em <see cref="MedidaClinica"/>, por uma razão
    /// que não é preferência: <c>MedidaClinica</c> não tem HORA (só <c>DateOnly</c>), então
    /// três aferições na mesma infusão ficariam indistinguíveis — e é justamente a
    /// sequência dentro da sessão que faz a leitura de enfermagem ("PA 120x80 na admissão,
    /// 90x60 aos vinte minutos"). Faltam-lhe também temperatura, FC, FR e SpO₂.
    ///
    /// A divisão de trabalho fica escrita na tela: <b>aqui é o ponto no tempo</b> — o que
    /// se observou naquela passagem; a <b>CURVA</b> de peso, IMC e cintura continua sendo
    /// a tela de Medidas, que é onde ela se lê ao longo dos meses.
    /// </summary>
    public int? PressaoSistolica { get; set; }

    public int? PressaoDiastolica { get; set; }

    public int? FrequenciaCardiaca { get; set; }

    public int? FrequenciaRespiratoria { get; set; }

    /// <summary>Em graus Celsius.</summary>
    public decimal? Temperatura { get; set; }

    /// <summary>SpO₂ em porcentagem.</summary>
    public int? SaturacaoOxigenio { get; set; }

    /// <summary>
    /// Dor de 0 a 10 — o quinto sinal vital.
    ///
    /// ⚠️ Ela NÃO entra na curva de dor do prontuário (<c>EvolucaoDaDor</c>, alimentada por
    /// <c>Evolucao.EvaAntes/EvaDepois</c>). Aquela mede o efeito do TRATAMENTO entre
    /// sessões e é lida pela direção; esta é a aferição do momento. Misturá-las mudaria em
    /// silêncio um número que a clínica já lê — e a tela diz que são duas leituras.
    /// </summary>
    public int? Dor { get; set; }

    // ---- Autoria ----

    /// <summary>O login que escreveu. É o vínculo forte; o resto é cópia para a impressão.</summary>
    public int? AutorUsuarioId { get; set; }
    public UsuarioSistema? AutorUsuario { get; set; }

    /// <summary>Nome copiado no ato.</summary>
    public string AutorNome { get; set; } = string.Empty;

    /// <summary>
    /// COREN copiado no ato. ⚠️ Evolução de enfermagem sem o registro no conselho não é
    /// evolução de enfermagem — o número é parte da assinatura profissional.
    /// </summary>
    public string? AutorConselho { get; set; }

    /// <summary>O relógio do sistema. Sai IMPRESSO ao lado da hora informada.</summary>
    public DateTime RegistradoEm { get; set; } = DateTime.Now;

    // ---- Correção ----

    /// <summary>A evolução que esta corrige. A apontada deixa de ser a vigente e FICA.</summary>
    public int? RetificaEvolucaoId { get; set; }
    public EvolucaoEnfermagem? RetificaEvolucao { get; set; }

    /// <summary>Por que a anterior estava errada. Obrigatório ao retificar.</summary>
    public string? MotivoRetificacao { get; set; }

    /// <summary>Cancelar é para o registro lançado no paciente ERRADO — não para corrigir texto.</summary>
    public DateTime? CanceladaEm { get; set; }

    public string? MotivoCancelamento { get; set; }

    public string? CanceladaPor { get; set; }

    // ---- Leituras derivadas ----

    public bool Cancelada => CanceladaEm is not null;

    public bool EhRetificacao => RetificaEvolucaoId is not null;

    /// <summary>O momento do fato, para ordenar e para comparar com o relógio.</summary>
    public DateTime Momento => Data.ToDateTime(Hora);

    /// <summary>
    /// Quanto tempo depois do fato o registro foi digitado. Informativo, não acusação: a
    /// técnica está com o paciente, não com o teclado. Só vira leitura de auditoria quando
    /// é grande.
    /// </summary>
    public TimeSpan AtrasoDoRegistro
    {
        get
        {
            var diferenca = RegistradoEm - Momento;
            return diferenca < TimeSpan.Zero ? TimeSpan.Zero : diferenca;
        }
    }

    /// <summary>"120x80", ou nulo quando a pressão não foi aferida.</summary>
    public string? PressaoArterial => PressaoSistolica is { } s && PressaoDiastolica is { } d
        ? $"{s}x{d}"
        : null;

    public bool TemSinaisVitais =>
        PressaoSistolica is not null || FrequenciaCardiaca is not null
        || FrequenciaRespiratoria is not null || Temperatura is not null
        || SaturacaoOxigenio is not null || Dor is not null;

    /// <summary>
    /// Os sinais vitais numa linha, para a lista e para o papel: "PA 120x80 · FC 78 · T
    /// 36,4 °C · SpO₂ 97% · dor 3/10". Devolve nulo quando nada foi aferido — e nulo é
    /// diferente de zero, como em todo indicador deste projeto.
    /// </summary>
    public string? SinaisVitaisResumidos
    {
        get
        {
            if (!TemSinaisVitais) return null;

            var partes = new List<string>(6);
            if (PressaoArterial is { } pa) partes.Add($"PA {pa}");
            if (FrequenciaCardiaca is { } fc) partes.Add($"FC {fc}");
            if (FrequenciaRespiratoria is { } fr) partes.Add($"FR {fr}");
            // pt-BR FIXO, e não a cultura da máquina: este texto é GRAVADO e impresso, e
            // dois postos escreveriam "36,4" e "36.4" na mesma coluna. É a mesma regra do
            // DetalheImposto da parcela 15.
            if (Temperatura is { } t)
                partes.Add($"T {t.ToString("0.0", PtBr)} °C");
            if (SaturacaoOxigenio is { } spo) partes.Add($"SpO₂ {spo}%");
            if (Dor is { } dor) partes.Add($"dor {dor}/10");

            return string.Join(" · ", partes);
        }
    }

    private static readonly System.Globalization.CultureInfo PtBr = new("pt-BR");

    // ---- Plausibilidade ----
    //
    // ⚠️ A única recusa é a IMPLAUSIBILIDADE, nunca a anormalidade — é a regra do
    // CatalogoMedidas (parcela 37): 2500 kg é dedo no teclado; 210 kg é anormal e possível,
    // e recusá-lo esconderia quem precisa de atenção. Aqui vale igual: FC 180 é taquicardia
    // e existe; FC 1800 é digitação.

    public const int SistolicaMinima = 40, SistolicaMaxima = 300;
    public const int DiastolicaMinima = 20, DiastolicaMaxima = 200;
    public const int CardiacaMinima = 20, CardiacaMaxima = 300;
    public const int RespiratoriaMinima = 4, RespiratoriaMaxima = 80;
    public const decimal TemperaturaMinima = 30m, TemperaturaMaxima = 45m;
    public const int SaturacaoMinima = 50, SaturacaoMaxima = 100;
    public const int DorMinima = 0, DorMaxima = 10;

    /// <summary>
    /// A crítica dos sinais vitais, em português e dizendo o intervalo aceito. Devolve nulo
    /// quando está tudo plausível.
    ///
    /// Mora no DOMÍNIO porque há mais de uma porta que grava (a janela da sala, a do
    /// prontuário) — validar na tela cobriria uma e deixaria a outra passando, que é o
    /// defeito recorrente do projeto vestido de validação.
    /// </summary>
    public string? CriticarSinaisVitais()
    {
        if (PressaoSistolica is { } s && (s < SistolicaMinima || s > SistolicaMaxima))
            return $"Pressão sistólica de {s} não é plausível "
                 + $"(esperado entre {SistolicaMinima} e {SistolicaMaxima}).";

        if (PressaoDiastolica is { } d && (d < DiastolicaMinima || d > DiastolicaMaxima))
            return $"Pressão diastólica de {d} não é plausível "
                 + $"(esperado entre {DiastolicaMinima} e {DiastolicaMaxima}).";

        // Meia pressão arterial não existe — a mesma regra do tipo com par no
        // CatalogoMedidas. E diastólica maior que a sistólica é campo trocado.
        if (PressaoSistolica is null != (PressaoDiastolica is null))
            return "A pressão arterial precisa dos dois números — sistólica e diastólica.";

        if (PressaoSistolica is { } sis && PressaoDiastolica is { } dia && dia >= sis)
            return $"A diastólica ({dia}) não pode ser maior ou igual à sistólica ({sis}) — "
                 + "confira se os campos não foram trocados.";

        if (FrequenciaCardiaca is { } fc && (fc < CardiacaMinima || fc > CardiacaMaxima))
            return $"Frequência cardíaca de {fc} não é plausível "
                 + $"(esperado entre {CardiacaMinima} e {CardiacaMaxima}).";

        if (FrequenciaRespiratoria is { } fr && (fr < RespiratoriaMinima || fr > RespiratoriaMaxima))
            return $"Frequência respiratória de {fr} não é plausível "
                 + $"(esperado entre {RespiratoriaMinima} e {RespiratoriaMaxima}).";

        if (Temperatura is { } t && (t < TemperaturaMinima || t > TemperaturaMaxima))
            return $"Temperatura de {t.ToString("0.0", PtBr)} °C não é plausível "
                 + $"(esperado entre {TemperaturaMinima:0} e {TemperaturaMaxima:0} °C).";

        if (SaturacaoOxigenio is { } spo && (spo < SaturacaoMinima || spo > SaturacaoMaxima))
            return $"Saturação de {spo}% não é plausível "
                 + $"(esperado entre {SaturacaoMinima} e {SaturacaoMaxima}).";

        if (Dor is { } dor && (dor < DorMinima || dor > DorMaxima))
            return $"A dor é medida de {DorMinima} a {DorMaxima}.";

        return null;
    }
}
