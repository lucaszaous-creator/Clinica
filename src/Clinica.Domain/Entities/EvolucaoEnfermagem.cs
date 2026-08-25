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

    // ---- A CONSULTA DE ENFERMAGEM: o Processo de Enfermagem (parcela 73) ----
    //
    // ⚠️ Até aqui isto era UMA caixa de texto com sinais vitais ao lado, e isso não é uma
    // consulta de enfermagem — é uma anotação. A COFEN 358/2009 torna o Processo de
    // Enfermagem OBRIGATÓRIO e o descreve em cinco etapas; a Lei 7.498/1986 (art. 11, I,
    // "i") faz a consulta de enfermagem privativa do Enfermeiro.
    //
    // As cinco etapas, e onde cada uma mora:
    //   1. Histórico (coleta de dados) ......... <see cref="Historico"/>
    //   2. Diagnóstico de enfermagem ........... <see cref="Diagnosticos"/>
    //   3. Planejamento (resultado esperado) ... <see cref="DiagnosticoEnfermagem.ResultadoEsperado"/>
    //   4. Implementação (prescrição) .......... <see cref="Cuidados"/>
    //   5. Avaliação ........................... <see cref="Avaliacao"/>
    //
    // ⚠️ TODOS NULOS, e isso é o desenho, não uma folga. A ANOTAÇÃO curta continua
    // existindo: a técnica que troca um curativo e registra "ferida limpa, sem exsudato"
    // não abre um processo de enfermagem, e obrigá-la faria a clínica escrever "idem" em
    // cinco campos — que é pior do que o campo vazio, porque parece registro. O que
    // distingue os dois é o CONTEÚDO: <see cref="EhConsulta"/>.

    /// <summary>
    /// Etapa 1 — HISTÓRICO DE ENFERMAGEM: a coleta de dados. O que o paciente relata, o que
    /// ele já usa, o que a família informa, hábitos, condições que importam ao cuidado.
    ///
    /// É a anamnese DE ENFERMAGEM, e não a médica: ela pergunta pela capacidade de
    /// autocuidado, pela rede de apoio e pelo que o paciente entende do próprio tratamento
    /// — coisas que a consulta médica não colhe e das quais o cuidado depende.
    /// </summary>
    public string? Historico { get; set; }

    /// <summary>
    /// Etapa 1 (segunda metade) — o EXAME FÍSICO de enfermagem, céfalo-podal na medida do
    /// que a passagem exige: pele, acesso venoso, edema, ausculta, o que for.
    ///
    /// Separado do <see cref="Texto"/> porque achado e conduta são coisas diferentes — a
    /// mesma razão pela qual a sessão médica separou o exame da conduta.
    /// </summary>
    public string? ExameFisico { get; set; }

    /// <summary>
    /// Etapa 5 — AVALIAÇÃO: o que aconteceu com o que foi prescrito. É a etapa que fecha o
    /// processo, e a que mais some dos prontuários — sem ela, o plano de cuidados vira uma
    /// lista de intenções que ninguém confere.
    /// </summary>
    public string? Avaliacao { get; set; }

    /// <summary>Etapa 2 e 3 — os diagnósticos de enfermagem, com o resultado esperado de cada um.</summary>
    public List<DiagnosticoEnfermagem> Diagnosticos { get; set; } = new();

    /// <summary>Etapa 4 — a PRESCRIÇÃO DE ENFERMAGEM: os cuidados, com a frequência de cada um.</summary>
    public List<CuidadoEnfermagem> Cuidados { get; set; } = new();

    /// <summary>
    /// Este registro é uma CONSULTA DE ENFERMAGEM, e não uma anotação de passagem.
    ///
    /// ⚠️ É derivado do conteúdo, e não uma coluna: um sinalizador gravado viraria mentira
    /// no dia em que alguém apagasse os diagnósticos de uma consulta, e um valor novo de
    /// enum quebraria a leitura do app que ainda não atualizou (a mina da parcela 67).
    /// O que define a consulta é ela ter as etapas — que é o que a COFEN 358/2009 cobra.
    /// </summary>
    public bool EhConsulta =>
        Diagnosticos.Count > 0
        || Cuidados.Count > 0
        || !string.IsNullOrWhiteSpace(Historico)
        || !string.IsNullOrWhiteSpace(ExameFisico);

    /// <summary>
    /// As etapas do Processo de Enfermagem que ficaram VAZIAS nesta consulta.
    ///
    /// ⚠️ Ela AVISA, não impede — e essa é a decisão. A enfermeira que colhe o histórico
    /// hoje e fecha a avaliação depois da infusão está fazendo o processo certo; recusar o
    /// registro incompleto faria ela esperar o fim do dia para escrever tudo de memória,
    /// que é exatamente o que o módulo do Consultório existe para combater. O que não pode
    /// é a etapa faltar SEM NINGUÉM PERCEBER.
    /// </summary>
    public IReadOnlyList<string> EtapasEmFalta
    {
        get
        {
            if (!EhConsulta) return [];

            var faltam = new List<string>();
            if (string.IsNullOrWhiteSpace(Historico)) faltam.Add("histórico (coleta de dados)");
            if (Diagnosticos.Count == 0) faltam.Add("diagnóstico de enfermagem");
            if (Diagnosticos.Count > 0
                && Diagnosticos.All(d => string.IsNullOrWhiteSpace(d.ResultadoEsperado)))
                faltam.Add("planejamento (resultado esperado)");
            if (Cuidados.Count == 0) faltam.Add("prescrição de enfermagem");
            if (string.IsNullOrWhiteSpace(Avaliacao)) faltam.Add("avaliação");
            return faltam;
        }
    }

    /// <summary>
    /// Marca a evolução que descreve algo fora do esperado (reação, queda de pressão,
    /// extravasamento). É o único campo que a MÁQUINA lê do que aconteceu: ele acende o
    /// selo na fila e, desde a parcela 72, entra na lista de alertas clínicos da tela de
    /// atendimento do médico — dentro da <see cref="JanelaDeAlerta"/>.
    ///
    /// ⚠️ Até a parcela 72 este comentário afirmava que a marca "viaja para a tela de
    /// atendimento do médico" e ela NÃO viajava: nenhuma linha de
    /// <c>Clinica.Modulo.Clinico</c> lia a evolução de enfermagem. Comentário que promete
    /// o que o código não faz é a armadilha da parcela 67 — e aqui o estrago não era erro,
    /// era AUSÊNCIA, indistinguível de "não houve intercorrência".
    ///
    /// ⚠️ É <c>bool</c> e não enum de propósito: a taxonomia da SAE (histórico, diagnóstico,
    /// prescrição e evolução de enfermagem) é grande demais para nascer chutada, e valor
    /// novo de enum lido pelos cinco apps paga o preço do incidente de 14/08/2026. Se a
    /// clínica precisar da distinção, ela entra depois como coluna aditiva.
    /// </summary>
    public bool Intercorrencia { get; set; }

    /// <summary>
    /// Por quanto tempo uma intercorrência continua sendo ALERTA na tela de quem atende.
    ///
    /// ⚠️ A janela não é detalhe, é o que separa um alerta de um entulho. <b>Alergia é
    /// ESTADO; intercorrência é EVENTO DATADO</b> — e <see cref="Intercorrencia"/> é
    /// <c>bool</c>, então <b>não há como descartá-la</b> (ela não é
    /// <c>ProblemaPaciente</c>, que tem situação e motivo de descarte). Sem janela, seis
    /// meses depois a lista de alerta do paciente crônico teria uma náusea de março, um
    /// extravasamento de abril e a alergia real no meio: isso não acrescenta contexto,
    /// <b>destrói a lista que já funciona</b> — e é assim que se ensina alguém a fechar o
    /// alerta sem ler, matando o de dipirona ao lado.
    ///
    /// Quarenta e oito horas cobrem a reação que aparece no dia seguinte e a sessão de
    /// segunda vista na quarta. Passado isso, a intercorrência continua no prontuário, na
    /// linha do tempo e na folha — ela sai do ALERTA, não do registro.
    ///
    /// É <c>const</c> no domínio, ao lado da regra, e não configuração: é a definição de
    /// "recente" para uma leitura de segurança, e editá-la numa tela faria alguém
    /// esticá-la para um ano sem perceber o que perde.
    /// </summary>
    public const int JanelaDeAlertaHoras = 48;

    /// <summary>
    /// Esta intercorrência ainda é alerta para quem vai atender agora?
    ///
    /// Cancelada não alerta (o registro foi desdito) e substituída por retificação
    /// tampouco — quem decide isso é quem chama, com a lista na mão.
    /// </summary>
    public bool AlertaAgora(DateTime agora)
        => Intercorrencia
           && !Cancelada
           && Momento >= agora.AddHours(-JanelaDeAlertaHoras)
           && Momento <= agora.AddMinutes(5);

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

    // ==================== O ACESSO VENOSO (parcela 77) ====================
    //
    // A clínica tem sala de infusão, e a dica do exame físico desta mesma janela manda
    // avaliar "pele, ACESSO VENOSO, edema, ausculta" — em texto corrido. Estruturado, ele
    // responde a pergunta que a técnica faz antes de puncionar de novo: há quantos dias
    // está esse acesso? A prática de enfermagem troca cateter periférico por tempo, e o
    // tempo não se conta lendo parágrafo.
    //
    // ⚠️ Três campos na EVOLUÇÃO, e não uma entidade: o acesso é um ACHADO da passagem,
    // como a PA — quem o descreve é quem o viu naquele momento. Uma entidade "acesso" com
    // ciclo de vida próprio precisaria de retirada, troca e motivo, e nada disso foi
    // pedido: seria construir a exceção que ninguém vai exercer.

    /// <summary>Onde está o acesso ("MSD, antecubital"). Nulo = não há acesso, ou não foi
    /// avaliado — e a tela diz qual dos dois.</summary>
    public string? AcessoLocal { get; set; }

    /// <summary>O calibre do cateter ("20G", "22G").</summary>
    public string? AcessoCalibre { get; set; }

    /// <summary>
    /// Quando foi puncionado. É a DATA do fato, informada — a técnica que assume o plantão
    /// registra o acesso que outra puncionou anteontem.
    /// </summary>
    public DateOnly? AcessoPuncionadoEm { get; set; }

    /// <summary>Há um acesso descrito nesta passagem.</summary>
    public bool TemAcesso => !string.IsNullOrWhiteSpace(AcessoLocal);

    /// <summary>
    /// Há quantos dias o acesso está no paciente, na data desta passagem. Nulo quando não
    /// se sabe a punção — e nulo é diferente de zero, como em todo indicador do projeto:
    /// zero diria "puncionado hoje", que é uma afirmação que ninguém fez.
    /// </summary>
    public int? DiasDeAcesso => AcessoPuncionadoEm is { } p
        ? Math.Max(0, Data.DayNumber - p.DayNumber)
        : null;

    /// <summary>"MSD, antecubital · 20G · puncionado há 3 dias".</summary>
    public string? AcessoResumo
    {
        get
        {
            if (!TemAcesso) return null;

            var partes = new List<string> { AcessoLocal!.Trim() };
            if (!string.IsNullOrWhiteSpace(AcessoCalibre)) partes.Add(AcessoCalibre!.Trim());
            if (DiasDeAcesso is { } d)
                partes.Add(d == 0 ? "puncionado hoje"
                         : d == 1 ? "puncionado ontem"
                         : $"puncionado há {d} dias");

            return string.Join(" \u00B7 ", partes);
        }
    }

    /// <summary>
    /// As evoluções VIGENTES de uma lista: fora as canceladas, e fora as que já foram
    /// retificadas por outra.
    ///
    /// ⚠️ Pública e estática porque a regra tem DOIS leitores — a curva de pressão
    /// (<c>MedidaClinicaService</c>) e os sinais vitais que a tela de atendimento mostra ao
    /// médico. Duas definições de "qual registro vale" divergem na primeira correção, e a
    /// que ninguém lembra de ajustar é a que passa a responder com um número DESDITO.
    ///
    /// A lista chega da mais recente para a mais antiga e sai na mesma ordem: quem ordena é
    /// o SQL do repositório, e reordenar aqui daria uma segunda resposta para "a última".
    /// </summary>
    public static IEnumerable<EvolucaoEnfermagem> Vigentes(
        IEnumerable<EvolucaoEnfermagem> todas)
    {
        var lista = todas as IReadOnlyCollection<EvolucaoEnfermagem> ?? todas.ToList();

        var substituidas = lista
            .Where(e => e.RetificaEvolucaoId is not null)
            .Select(e => e.RetificaEvolucaoId!.Value)
            .ToHashSet();

        return lista.Where(e => !e.Cancelada && !substituidas.Contains(e.Id));
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
