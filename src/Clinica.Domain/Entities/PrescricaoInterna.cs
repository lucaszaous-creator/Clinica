namespace Clinica.Domain.Entities;

/// <summary>Em que pé está a prescrição. É o eixo do fluxo: só assinada se executa.</summary>
public enum SituacaoPrescricao
{
    /// <summary>Sendo escrita. Não aparece para a enfermagem e não se checa.</summary>
    Rascunho,

    /// <summary>Assinada pelo prescritor. É o único estado em que a execução é permitida.</summary>
    Assinada,

    /// <summary>
    /// A enfermagem terminou a execução e fechou a folha. Não há assinatura eletrônica
    /// aqui: quem assina a execução é a enfermeira, na via impressa.
    /// </summary>
    Encerrada,

    /// <summary>Desfeita antes de executar. Não some da base: pode ter sido impressa.</summary>
    Cancelada
}

/// <summary>
/// Por onde o medicamento entra. A clínica é de infusão, então a endovenosa vem primeiro —
/// e a via não é enfeite: ela muda o que a técnica prepara e é o campo que a checagem
/// confere contra o que foi feito.
/// </summary>
public enum ViaAdministracao
{
    Endovenosa,
    Intramuscular,
    Subcutanea,
    Intradermica,
    Oral,
    Inalatoria,
    Topica,
    Outra
}

/// <summary>
/// O que a técnica disse sobre o item. São só dois, e é de propósito: "pendente" não é
/// uma checagem, é a AUSÊNCIA de uma — gravar pendente como fato criaria a possibilidade
/// de uma folha em que alguém afirmou que não fez nada, que é diferente de não ter dito.
/// </summary>
public enum SituacaoChecagem
{
    /// <summary>Feito. No papel é o "chequezinho" em cima do horário.</summary>
    Realizado,

    /// <summary>
    /// Prescrito e NÃO feito. No papel é a "rodela": a técnica circula o horário para dizer
    /// que aquilo não aconteceu. Exige justificativa escrita.
    /// </summary>
    NaoRealizado
}

/// <summary>
/// A situação de um item, DERIVADA das checagens (nunca gravada). Mesma razão da situação
/// do pacote: um item gravado como "pendente" continuaria pendente depois de checado, e a
/// folha passaria a mentir sobre o que a equipe fez.
/// </summary>
public enum SituacaoItemPrescricao
{
    /// <summary>Ninguém disse nada ainda. É a fila de trabalho da sala.</summary>
    Pendente,

    Realizado,

    NaoRealizado,

    /// <summary>O prescritor tirou o item antes de ele ser executado.</summary>
    Suspenso
}

/// <summary>
/// Quem assinou eletronicamente.
///
/// Hoje só o <see cref="Prescritor"/> é gravado: a clínica decidiu que quem confere e
/// assina a EXECUÇÃO é a enfermeira, na via impressa, depois que a folha sai da impressora.
/// O valor <see cref="Executante"/> permanece porque a coluna é gravada como texto e tirá-lo
/// exigiria uma migration destrutiva para não ganhar nada — mas nada o escreve.
/// </summary>
public enum PapelAssinatura
{
    Prescritor,

    /// <summary>
    /// A enfermagem que executou. Quando a folha nasce com
    /// <see cref="PrescricaoInterna.ExigeAssinaturaEletronicaDaExecucao"/> marcado, a
    /// enfermeira assina <b>a MESMA prescrição</b> no encerramento, com o certificado
    /// DELA — por revisão incremental, sem tocar num byte do que o prescritor selou
    /// (<c>RevisaoIncrementalPdf</c>, 16/08/2026). Até essa data o desenho era outro
    /// arquivo, por uma premissa sobre PDF que foi medida e derrubada.
    /// </summary>
    Executante
}

/// <summary>
/// Qual das duas folhas se está pedindo — a impressão ou a reimpressão.
///
/// Enum próprio, e não <see cref="PapelAssinatura"/> reaproveitado: desde que a enfermagem
/// passou a assinar no papel, "Executante" não nomeia mais um signatário, e usá-lo para
/// dizer "a outra folha" faria o parâmetro mentir sobre o que ele escolhe.
/// </summary>
public enum FolhaPrescricao
{
    /// <summary>O que foi mandado fazer. É esta que leva a assinatura ICP-Brasil.</summary>
    Prescricao,

    /// <summary>O que foi feito — registro do sistema, conferido e assinado no papel.</summary>
    RegistroExecucao
}

/// <summary>
/// O nível da assinatura — e o sistema IMPRIME qual é, sempre.
///
/// Esta é a decisão de projeto mais importante do assunto. O pedido original da clínica
/// foi escanear o carimbo e colar no PDF, e isso é pior do que não assinar nada: uma
/// imagem de assinatura não prova autoria (qualquer um copia e cola em outro documento),
/// e a partir do dia em que ela está no banco existe uma assinatura reutilizável do
/// profissional por quem tiver acesso ao sistema. O pior de tudo é que ela PARECE uma
/// garantia — o mesmo motivo pelo qual <c>DocumentosClinicosPdfService</c> se recusa a
/// chamar o carimbo impresso de assinatura digital.
///
/// A saída não é fingir um nível: é declarar o que cada folha tem.
/// </summary>
public enum TipoAssinatura
{
    /// <summary>
    /// Imprimiu, assinou e carimbou à mão. É o que a clínica faz hoje e continua valendo
    /// — enquanto o papel existir, ele é o documento; o eletrônico é a evidência e o fluxo.
    /// </summary>
    ManuscritaEmPapel,

    /// <summary>
    /// Login + senha reconfirmada no ato + hash do conteúdo + trilha de auditoria. Atende
    /// os três requisitos de assinatura AVANÇADA (associada unicamente ao signatário, feita
    /// com dado sob controle exclusivo dele, e capaz de detectar alteração posterior), mas
    /// não é qualificada: não há terceiro de confiança atestando quem é a pessoa.
    /// </summary>
    EletronicaAvancada,

    /// <summary>
    /// Assinatura QUALIFICADA com certificado ICP-Brasil (e-CPF A1 ou A3), PKCS#7 destacado
    /// dentro do PDF. É o que a lei presume autêntico sem a outra parte precisar concordar.
    /// </summary>
    IcpBrasil
}

/// <summary>
/// A PRESCRIÇÃO DE EXECUÇÃO INTERNA — a folha de infusão da clínica (parcela 42).
///
/// Por que não é a <see cref="TipoDocumentoClinico.Receita"/> que já existe
/// ---------------------------------------------------------------------
/// A receita é o papel que o paciente leva para a farmácia; esta folha "é destinada ao
/// próprio consultório, o paciente não vai apresentar lá fora" (palavras da clínica). A
/// diferença não é de rótulo, é de natureza:
///
/// - <see cref="DocumentoClinico"/> é FATO IMUTÁVEL emitido: nasce pronto, não se
///   reescreve, e é essa regra que garante que a segunda via saia idêntica à primeira.
///   Esta aqui tem CICLO DE VIDA (rascunho → assinada → executada → encerrada) e muda
///   depois de nascer, porque a execução acontece ao longo do dia. Enfiar estado vivo em
///   <c>DocumentoClinico</c> quebraria a regra que sustenta as outras sete impressões.
/// - <see cref="ItemDocumento"/> tem descrição, detalhe e quantidade. Uma infusão precisa
///   de dose, diluente, volume, via e tempo — e cada um deles é campo que a técnica CONFERE
///   antes de administrar. Empilhá-los em texto livre num "detalhe" transformaria a
///   conferência num exercício de leitura.
///
/// O que ela acrescenta ao sistema
/// -------------------------------
/// A checagem de enfermagem. Quando a técnica checa um item ela não está preenchendo um
/// campo: está AFIRMANDO que foi prescrito assim e realizado assim. A assinatura que
/// responde por essa afirmação é MANUSCRITA, na folha impressa — o sistema guarda o
/// registro, o papel guarda a autoria.
///
/// As regras que o serviço cobra
/// -----------------------------
/// - <b>Só se checa prescrição ASSINADA.</b> Administrar sobre rascunho é o buraco clássico.
/// - <b>Item já checado não se edita</b> — suspende-se e prescreve-se outro. Editar depois
///   faria a checagem passar a atestar coisa diferente da que foi feita, que é exatamente
///   o que a assinatura da técnica existe para impedir.
/// - <b>A hora é INFORMADA, nunca a do relógio.</b> Ver <see cref="ChecagemPrescricao"/>.
/// - <b>Não realizado exige justificativa escrita.</b>
/// - <b>Só a prescrição é assinada eletronicamente</b>, por quem prescreve. A execução é
///   conferida e assinada à caneta na via impressa. Ver <see cref="AssinaturaDocumento"/>.
/// </summary>
public class PrescricaoInterna
{
    public int Id { get; set; }

    /// <summary>Número sequencial por ano, no formato <c>PRE 2026/0001</c>.</summary>
    public string Numero { get; set; } = string.Empty;

    /// <summary>
    /// Código curto impresso no rodapé, para conferir a via em papel contra o sistema —
    /// mesma convenção do <see cref="DocumentoClinico"/>.
    /// </summary>
    public string CodigoVerificacao { get; set; } = string.Empty;

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Quem prescreve e assina. Null só enquanto a clínica não cadastrou a equipe.</summary>
    public int? ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }

    /// <summary>
    /// A sessão do dia, quando há uma. É por ela que a sala de infusão sabe que esta folha
    /// é de hoje e de quem está na cadeira — sem isso a técnica teria de procurar pelo nome.
    /// </summary>
    public int? AgendamentoId { get; set; }
    public Agendamento? Agendamento { get; set; }

    /// <summary>Evolução de origem, quando a prescrição nasceu de dentro do atendimento.</summary>
    public int? EvolucaoId { get; set; }
    public Evolucao? Evolucao { get; set; }

    public DateOnly Data { get; set; }

    /// <summary>Hora em que foi prescrita. Vai impressa: é o começo da linha do tempo da folha.</summary>
    public TimeOnly Hora { get; set; }

    public SituacaoPrescricao Situacao { get; set; } = SituacaoPrescricao.Rascunho;

    /// <summary>Indicação/motivo — o que se está tratando com esta infusão.</summary>
    public string? Indicacao { get; set; }

    /// <summary>Orientações gerais que valem para a folha inteira (jejum, acesso, monitorização).</summary>
    public string? Observacoes { get; set; }

    /// <summary>
    /// O "campo de 2ª assinatura" (decisão da direção, 14/08/2026): marcado, a enfermagem
    /// também assina eletronicamente — o REGISTRO DE EXECUÇÃO, no encerramento, com o
    /// certificado DELA.
    ///
    /// Por que é uma escolha por folha, e não regra da clínica: nem toda técnica tem
    /// e-CPF/SafeID, e uma exigência global travaria o encerramento da sala inteira no dia
    /// em que o certificado de alguém vencesse. Quem prescreve decide, folha a folha — e
    /// desmarcado, vale o regime de sempre: a enfermeira assina à caneta, na via impressa.
    ///
    /// Por que a assinatura é no ENCERRAMENTO, e nunca antes: o registro de execução MUDA
    /// a cada item checado. Assinar antes selaria um arquivo que ainda ia mudar — a mesma
    /// razão pela qual a prescritora não assina rascunho.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Nasce MARCADO</b> (decisão da clínica, 20/08/2026). Ele nasceu desmarcado, e a
    /// consequência apareceu em produção: a folha PRE 2026/0009 foi encerrada sem a
    /// assinatura da enfermagem porque ninguém marcou a caixinha, e a clínica leu isso como
    /// "o sistema não pede quando tem item não realizado" — que não é verdade, mas era o
    /// que ela via. <b>Garantia que depende de alguém lembrar não é garantia.</b>
    ///
    /// Desmarcar continua existindo, e continua sendo de quem prescreve: é a folha que vai
    /// ser assinada à caneta. O que mudou é o lado para o qual o esquecimento cai.
    /// </remarks>
    public bool ExigeAssinaturaEletronicaDaExecucao { get; set; } = true;

    public DateTime? AssinadaEm { get; set; }

    public DateTime? EncerradaEm { get; set; }

    public DateTime? CanceladaEm { get; set; }

    public string? MotivoCancelamento { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    public DateTime? AtualizadoEm { get; set; }

    public string? AtualizadoPor { get; set; }

    public List<ItemPrescricaoInterna> Itens { get; set; } = new();

    public List<AssinaturaDocumento> Assinaturas { get; set; } = new();

    // ---- Leituras derivadas ----

    public bool EstaAssinada
        => Situacao is SituacaoPrescricao.Assinada or SituacaoPrescricao.Encerrada;

    public bool Cancelada => Situacao == SituacaoPrescricao.Cancelada;

    /// <summary>
    /// A enfermagem pode mexer nesta folha. Encerrada sai da sala: o registro de execução
    /// já foi assinado, e checar depois disso alteraria um documento assinado.
    /// </summary>
    public bool PodeChecar => Situacao == SituacaoPrescricao.Assinada;

    /// <summary>Só rascunho se edita. Depois de assinada, corrige-se suspendendo e prescrevendo.</summary>
    public bool PodeEditar => Situacao == SituacaoPrescricao.Rascunho;

    /// <summary>
    /// Itens que ainda cobram uma palavra da técnica.
    ///
    /// O <b>se necessário</b> fica de fora, e é a única sutileza da conta: um SOS não
    /// administrado não é trabalho atrasado, é a condição que não aconteceu. Contá-lo
    /// deixaria toda folha com SOS eternamente "pendente", e o contador da sala — que
    /// existe para dizer o que falta fazer — passaria a apontar para nada. Na LINHA o
    /// item continua aparecendo como aguardando (<see cref="ItemPrescricaoInterna.Situacao"/>),
    /// porque ali a informação é sobre aquele item, não sobre o que resta do dia.
    /// </summary>
    public int Pendentes
        => Itens.Count(i => !i.SeNecessario && i.Situacao == SituacaoItemPrescricao.Pendente);

    public int Realizados
        => Itens.Count(i => i.Situacao == SituacaoItemPrescricao.Realizado);

    public int NaoRealizados
        => Itens.Count(i => i.Situacao == SituacaoItemPrescricao.NaoRealizado);

    /// <summary>
    /// Todo item já teve destino (feito, não feito ou suspenso). É o que habilita encerrar:
    /// encerrar com item pendente deixaria no prontuário uma folha que não diz se a droga
    /// entrou no paciente — que é a única pergunta que ela existe para responder.
    /// </summary>
    public bool ExecucaoCompleta => Itens.Count > 0 && Pendentes == 0;

    /// <summary>
    /// A assinatura eletrônica da PRESCRIÇÃO — sempre a de quem prescreveu. A da enfermagem,
    /// quando existe, é OUTRO documento (<see cref="AssinaturaDaExecucao"/>).
    /// </summary>
    public AssinaturaDocumento? AssinaturaDoPrescritor
        => Assinaturas.FirstOrDefault(a => a.Papel == PapelAssinatura.Prescritor);

    /// <summary>
    /// A assinatura eletrônica do REGISTRO DE EXECUÇÃO — a da enfermagem, colhida no
    /// encerramento quando <see cref="ExigeAssinaturaEletronicaDaExecucao"/> está marcado.
    /// Nula nas folhas do regime de sempre (assinatura à caneta na via impressa).
    /// </summary>
    public AssinaturaDocumento? AssinaturaDaExecucao
        => Assinaturas.FirstOrDefault(a => a.Papel == PapelAssinatura.Executante);

    /// <summary>
    /// A 2ª assinatura está pedida e ainda não foi colhida. Só existe depois de ENCERRADA:
    /// o registro de execução muda a cada checagem, e assinar antes selaria um arquivo que
    /// ainda ia mudar.
    /// </summary>
    public bool AguardaAssinaturaDaExecucao
        => ExigeAssinaturaEletronicaDaExecucao
           && Situacao == SituacaoPrescricao.Encerrada
           && AssinaturaDaExecucao is null;
}

/// <summary>
/// Um item da prescrição: a droga, como diluir, por onde e em quanto tempo.
///
/// Os campos são separados em vez de uma linha de texto porque cada um deles é conferido
/// isoladamente por quem prepara. "Dipirona 1g + SF 0,9% 100mL EV em 30 min" lido de um
/// campo só obriga a técnica a fazer a separação de cabeça, toda vez, com o paciente na
/// cadeira — e é aí que se troca o diluente.
/// </summary>
public class ItemPrescricaoInterna
{
    public int Id { get; set; }

    public int PrescricaoInternaId { get; set; }
    public PrescricaoInterna? Prescricao { get; set; }

    /// <summary>Ordem de administração na folha.</summary>
    public int Ordem { get; set; }

    /// <summary>O fármaco, como o prescritor escreve ("Dipirona sódica 500mg/mL").</summary>
    public string Descricao { get; set; } = string.Empty;

    /// <summary>A dose ("1 g", "2 ampolas", "40 mg").</summary>
    public string? Dose { get; set; }

    /// <summary>Em que diluir ("SF 0,9%", "SG 5%"). Vazio quando é em bolus puro.</summary>
    public string? Diluente { get; set; }

    /// <summary>Volume final ("100 mL").</summary>
    public string? Volume { get; set; }

    public ViaAdministracao Via { get; set; } = ViaAdministracao.Endovenosa;

    /// <summary>Tempo ou velocidade ("30 min", "60 gts/min", "bolus lento").</summary>
    public string? TempoInfusao { get; set; }

    /// <summary>
    /// Horário previsto (o "aprazamento"). Opcional: numa sessão de infusão a ordem
    /// costuma bastar, e exigir horário faria o prescritor inventar um.
    /// </summary>
    public TimeOnly? HoraPrevista { get; set; }

    /// <summary>
    /// "Se necessário" (SOS) — só se administra se a condição acontecer. Item SOS não
    /// checado NÃO é pendência: a condição pode simplesmente não ter ocorrido.
    /// </summary>
    public bool SeNecessario { get; set; }

    /// <summary>Cuidado específico ("infundir em acesso exclusivo", "monitorar PA").</summary>
    public string? Observacoes { get; set; }

    /// <summary>
    /// Quando o prescritor tirou o item. Suspender é o caminho de correção de uma folha já
    /// assinada — e é diferente de "não realizado": um é decisão de quem prescreve, o outro
    /// é o que aconteceu na sala. Somar os dois esconderia qual dos dois foi.
    /// </summary>
    public DateTime? SuspensoEm { get; set; }

    public string? MotivoSuspensao { get; set; }

    public string? SuspensoPor { get; set; }

    public List<ChecagemPrescricao> Checagens { get; set; } = new();

    // ---- Leituras derivadas ----

    public bool Suspenso => SuspensoEm is not null;

    /// <summary>
    /// A checagem que vale. Retificar não apaga: grava-se outra apontando a anterior, e a
    /// vigente é a que ninguém retificou. Assim a folha impressa mostra o que vale e a
    /// trilha continua mostrando o que foi corrigido e por quê.
    /// </summary>
    public ChecagemPrescricao? ChecagemVigente
    {
        get
        {
            var retificadas = Checagens
                .Where(c => c.RetificaChecagemId is not null)
                .Select(c => c.RetificaChecagemId!.Value)
                .ToHashSet();

            return Checagens
                .Where(c => !retificadas.Contains(c.Id))
                .OrderByDescending(c => c.RegistradoEm)
                .FirstOrDefault();
        }
    }

    public SituacaoItemPrescricao Situacao
    {
        get
        {
            if (Suspenso) return SituacaoItemPrescricao.Suspenso;

            return ChecagemVigente?.Situacao switch
            {
                SituacaoChecagem.Realizado => SituacaoItemPrescricao.Realizado,
                SituacaoChecagem.NaoRealizado => SituacaoItemPrescricao.NaoRealizado,
                _ => SituacaoItemPrescricao.Pendente
            };
        }
    }

    /// <summary>
    /// A linha como sai impressa e como a conferência de alergia a lê.
    ///
    /// Dose e diluente entram no texto de propósito: o alérgeno pode estar no diluente
    /// (látex do equipo não, mas soro glicosado num diabético sim), e a conferência do
    /// <c>PrescricaoService</c> compara palavra a palavra — o que não estiver no texto
    /// não é comparado com nada.
    /// </summary>
    public string TextoCompleto
    {
        get
        {
            var partes = new List<string> { Descricao.Trim() };
            if (!string.IsNullOrWhiteSpace(Dose)) partes.Add(Dose!.Trim());
            if (!string.IsNullOrWhiteSpace(Diluente)) partes.Add($"em {Diluente!.Trim()}");
            if (!string.IsNullOrWhiteSpace(Volume)) partes.Add(Volume!.Trim());
            return string.Join(" ", partes);
        }
    }
}

/// <summary>
/// A CHECAGEM DA TÉCNICA DE ENFERMAGEM — o coração desta parcela.
///
/// No hospital, checar não é marcar uma caixinha: é dizer <i>"foi prescrito assim e foi
/// realizado assim"</i>, com o horário em que aconteceu, e responder por isso. Quando não
/// foi realizado, a técnica "rodela" — circula o horário — e escreve o porquê.
///
/// As regras, e por que cada uma
/// -----------------------------
/// - <b>A hora é INFORMADA, nunca <c>DateTime.Now</c>.</b> A técnica administra às 14h e
///   consegue registrar às 14h20, entre um paciente e outro; carimbar o relógio mentiria
///   sobre o horário da administração, que é justamente o dado clínico. O relógio vai em
///   <see cref="RegistradoEm"/>, AO LADO — e a diferença entre os dois é o que uma
///   auditoria de enfermagem procura (checagem retroativa). Esconder um dos dois seria
///   escolher entre mentir sobre o cuidado ou perder a trilha.
/// - <b>Não realizado EXIGE justificativa.</b> É a terceira recusa do projeto, junto da
///   divergência do fechamento de caixa e do descarte de problema. Um item que não entrou
///   no paciente sem uma linha dizendo por quê é a pior linha possível de um prontuário.
/// - <b>Não se apaga: retifica-se.</b> Grava-se outra checagem apontando a anterior, com
///   motivo. Checagem que some leva junto a prova de que a conduta da época era razoável —
///   mesma regra da NC do faturamento e do documento clínico cancelado.
/// - <b>Quem checa é quem fez LOGIN.</b> Nunca um campo de texto com o nome: é o vínculo
///   com a pessoa que dá valor à checagem, e um nome digitado não vincula ninguém. O nome
///   e o conselho ficam COPIADOS aqui porque o usuário pode ser renomeado ou desativado, e
///   a folha tem de continuar dizendo quem fez — mesma razão do valor da taxa copiado na
///   venda.
/// </summary>
public class ChecagemPrescricao
{
    public int Id { get; set; }

    public int ItemPrescricaoInternaId { get; set; }
    public ItemPrescricaoInterna? Item { get; set; }

    public SituacaoChecagem Situacao { get; set; }

    /// <summary>
    /// Hora em que foi (ou seria) administrado, digitada por quem executou. Nos itens não
    /// realizados é o horário que, no papel, aparece circulado.
    /// </summary>
    public TimeOnly HoraRealizacao { get; set; }

    /// <summary>
    /// Por que não foi feito. Obrigatória quando <see cref="Situacao"/> é
    /// <see cref="SituacaoChecagem.NaoRealizado"/>; opcional no realizado (cabe a intercorrência).
    /// </summary>
    public string? Justificativa { get; set; }

    /// <summary>O login que executou. É o vínculo forte; o resto é cópia para a impressão.</summary>
    public int? ExecutanteUsuarioId { get; set; }
    public UsuarioSistema? ExecutanteUsuario { get; set; }

    /// <summary>Nome copiado no ato — a folha continua legível depois de o usuário sair da clínica.</summary>
    public string ExecutanteNome { get; set; } = string.Empty;

    /// <summary>COREN/registro copiado no ato.</summary>
    public string? ExecutanteConselho { get; set; }

    /// <summary>O relógio do sistema no momento do registro. Ver o comentário da classe.</summary>
    public DateTime RegistradoEm { get; set; } = DateTime.Now;

    /// <summary>
    /// A checagem que esta corrige. Quando preenchida, a apontada deixa de ser a vigente
    /// mas continua na base — ver <see cref="ItemPrescricaoInterna.ChecagemVigente"/>.
    /// </summary>
    public int? RetificaChecagemId { get; set; }
    public ChecagemPrescricao? RetificaChecagem { get; set; }

    /// <summary>Por que a checagem anterior estava errada. Obrigatório ao retificar.</summary>
    public string? MotivoRetificacao { get; set; }

    // ---- Leituras derivadas ----

    public bool EhRetificacao => RetificaChecagemId is not null;

    /// <summary>
    /// Quanto tempo depois da administração a checagem foi digitada. É informativo, não
    /// acusação: a técnica está com o paciente, não com o teclado. Só vira leitura de
    /// auditoria quando é grande.
    /// </summary>
    public TimeSpan AtrasoDoRegistro
    {
        get
        {
            var administrado = DateOnly.FromDateTime(RegistradoEm).ToDateTime(HoraRealizacao);
            var diferenca = RegistradoEm - administrado;
            return diferenca < TimeSpan.Zero ? TimeSpan.Zero : diferenca;
        }
    }
}

/// <summary>
/// A ASSINATURA ELETRÔNICA de uma das duas folhas — e o registro honesto de que nível ela tem.
///
/// DUAS assinaturas na MESMA folha
/// -------------------------------
/// A prescritora assina a PRESCRIÇÃO (<see cref="PapelAssinatura.Prescritor"/>) e, quando
/// a folha nasce com <see cref="PrescricaoInterna.ExigeAssinaturaEletronicaDaExecucao"/>
/// marcado, a enfermagem assina <b>a mesma prescrição</b> no encerramento
/// (<see cref="PapelAssinatura.Executante"/>) — que é o fluxo que a clínica descreveu e o
/// que a legalidade pede: uma folha com as duas, não duas folhas com uma cada.
///
/// ⚠️ <b>Isto mudou em 16/08/2026, e o motivo vale mais que a mudança.</b> Da parcela 42
/// até aqui o desenho era "dois documentos encadeados, um por signatário", justificado por
/// "em PDF não se assina incrementalmente". A primeira metade da premissa foi MEDIDA e
/// confirmada — o PDFsharp reescreve o arquivo ao salvar —, e a conclusão estava errada: a
/// limitação é da BIBLIOTECA, não do formato. O PDF prevê múltiplas assinaturas por
/// atualização incremental, e é o que <c>RevisaoIncrementalPdf</c> faz. A premissa ficou
/// seis parcelas de pé sem ninguém tentar o caminho que o formato já oferecia.
///
/// A prescritora continua assinando um PDF com as colunas de checagem em branco, e isso
/// continua correto: elas são pré-impressas do formulário. Ela atesta o que MANDOU fazer;
/// a enfermagem anexa a dela depois de executar, sem tocar num byte do que a médica selou.
///
/// A prescritora assinar um PDF com as colunas EM BRANCO é correto, e vale entender por
/// quê: elas são campos pré-impressos do formulário, como no talão de papel. Ela atesta o
/// que mandou fazer; o que foi feito é escrito à mão em cima da folha, depois.
///
/// O hash é o que faz a assinatura valer alguma coisa
/// --------------------------------------------------
/// Guardamos o SHA-256 do PDF assinado. Ao reabrir, o sistema recalcula e compara, e a
/// tela diz uma de TRÊS coisas — íntegra, alterada, ou "não foi possível conferir". O
/// terceiro estado é a regra da casa: falha de conferência nunca pode ser exibida como
/// sucesso.
/// </summary>
public class AssinaturaDocumento
{
    public int Id { get; set; }

    public int PrescricaoInternaId { get; set; }
    public PrescricaoInterna? Prescricao { get; set; }

    public PapelAssinatura Papel { get; set; }

    public TipoAssinatura Tipo { get; set; }

    /// <summary>SHA-256 do PDF assinado, em hexadecimal minúsculo.</summary>
    public string HashConteudo { get; set; } = string.Empty;

    public string AlgoritmoHash { get; set; } = "SHA-256";

    /// <summary>
    /// Quando foi assinado, pelo relógio da máquina. Sem carimbo do tempo de uma ACT
    /// credenciada (ver <see cref="CarimboTempoEm"/>) esta data é declarada, não provada —
    /// e o PDF diz isso.
    /// </summary>
    public DateTime AssinadoEm { get; set; } = DateTime.Now;

    /// <summary>O login que assinou.</summary>
    public int? UsuarioId { get; set; }
    public UsuarioSistema? Usuario { get; set; }

    /// <summary>Nome e conselho copiados no ato, como na checagem.</summary>
    public string NomeAssinante { get; set; } = string.Empty;

    public string? RegistroConselho { get; set; }

    /// <summary>Só dígitos. No ICP-Brasil vem do próprio certificado; ver o serviço.</summary>
    public string? CpfAssinante { get; set; }

    // ---- Só preenchidos na assinatura qualificada (ICP-Brasil) ----

    public string? CertificadoTitular { get; set; }

    public string? CertificadoEmissor { get; set; }

    /// <summary>Número de série do certificado, em hexadecimal. Identifica a via usada.</summary>
    public string? CertificadoSerie { get; set; }

    public DateTime? CertificadoValidoDe { get; set; }

    public DateTime? CertificadoValidoAte { get; set; }

    /// <summary>
    /// Data do carimbo do tempo RFC 3161, quando a clínica configurou uma ACT. Sem ele a
    /// assinatura é PAdES-B: válida, mas com a data por conta do relógio de quem assinou.
    /// </summary>
    public DateTime? CarimboTempoEm { get; set; }

    public string? CarimboTempoAutoridade { get; set; }

    /// <summary>O PDF assinado. Tabela à parte para a listagem não arrastar megabytes.</summary>
    public int? ArquivoId { get; set; }
    public ArquivoAssinado? Arquivo { get; set; }

    /// <summary>
    /// O <b>REGISTRO DE EXECUÇÃO</b> selado no mesmo ato (decisão da direção, 20/08/2026).
    ///
    /// A folha da prescrição é selada pela médica ANTES da execução, então ela nunca poderá
    /// mostrar o que foi feito — e acrescentar-lhe uma página faz o validador de fora
    /// acusar modificação ilegal na assinatura DELA (medido). Quem mostra o ✓, a rodela e o
    /// suspenso é o registro; para ele valer como prova, ele precisa ser selado também.
    ///
    /// São DOIS arquivos de UM ato: a enfermeira escolhe o certificado uma vez e o sistema
    /// sela a prescrição (revisão incremental, o carimbo dela ao lado do da médica) e o
    /// registro (assinatura própria, um carimbo). Nulo quando a selagem do registro falhou
    /// — e aí o registro é montado na hora e DIZ que não é assinado, apontando a folha que
    /// é. Falha na segunda não desfaz a primeira: o ato irreversível não depende do passo
    /// que veio depois dele.
    /// </summary>
    public int? ArquivoRegistroId { get; set; }
    public ArquivoAssinado? ArquivoRegistro { get; set; }

    // ---- Leituras derivadas ----

    /// <summary>Assinatura que a lei presume autêntica sem a outra parte precisar concordar.</summary>
    public bool EhQualificada => Tipo == TipoAssinatura.IcpBrasil;

    /// <summary>Como o nível aparece escrito no rodapé do PDF e na tela.</summary>
    public string RotuloDoNivel => Tipo switch
    {
        TipoAssinatura.IcpBrasil =>
            "Assinado digitalmente com certificado ICP-Brasil (assinatura qualificada)",
        TipoAssinatura.EletronicaAvancada =>
            "Assinado eletronicamente no sistema (assinatura avançada: login, senha e registro de integridade)",
        _ =>
            "Assinado à mão na via impressa"
    };

    /// <summary>Os oito primeiros dígitos do hash — o que se confere a olho contra a tela.</summary>
    public string HashCurto
        => HashConteudo.Length >= 8 ? HashConteudo[..8].ToUpperInvariant() : HashConteudo.ToUpperInvariant();
}

/// <summary>
/// Os bytes de um PDF assinado.
///
/// Tabela separada pela mesma razão de <c>PacientesFotos</c>: a lista da sala de infusão
/// carrega dezenas de prescrições para desenhar linhas de texto, e arrastar junto algumas
/// centenas de KB por linha faria a tela do balcão engasgar num banco remoto.
///
/// E os bytes precisam ser guardados EXATAMENTE como saíram: a assinatura PKCS#7 cobre uma
/// faixa de bytes do arquivo (o <c>/ByteRange</c>), então regenerar o PDF "igual" na hora
/// de reimprimir produziria um arquivo cuja assinatura não confere. Aqui não é cache — é o
/// documento.
/// </summary>
public class ArquivoAssinado
{
    public int Id { get; set; }

    public byte[] Conteudo { get; set; } = Array.Empty<byte>();

    public string NomeArquivo { get; set; } = string.Empty;

    public DateTime GeradoEm { get; set; } = DateTime.Now;

    public int Tamanho => Conteudo.Length;
}
