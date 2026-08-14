namespace Clinica.Domain.Entities;

/// <summary>
/// Qual figura do mapa recebeu a marcação. São duas porque é o que a sessão de
/// acupuntura precisa: o profissional marca de frente e de costas. Vista lateral só
/// entraria se houvesse desenho para ela — campo sem tela é campo que ninguém preenche.
/// </summary>
public enum FaceCorpo
{
    Frente,
    Costas
}

/// <summary>O que foi aplicado no ponto. A conduta em texto continua existindo; isto é
/// o que dá para contar depois (quantas ventosas, quanta moxa) sem ler prontuário.</summary>
public enum TecnicaPonto
{
    Agulha,
    Eletroacupuntura,
    Moxa,
    Ventosa,
    Auriculoterapia,
    Laser,
    Outra
}

/// <summary>
/// Um ponto marcado no mapa corporal de uma sessão.
///
/// As coordenadas são NORMALIZADAS (0 a 1) sobre a figura, nunca pixels: a tela pode
/// crescer, mudar de resolução ou ser redesenhada que a marcação continua no mesmo
/// lugar do corpo. Guardar pixel aqui seria amarrar o prontuário ao tamanho da janela
/// do dia em que ele foi escrito.
/// </summary>
public class PontoMapa
{
    public int Id { get; set; }

    public int MapaCorporalId { get; set; }
    public MapaCorporal? Mapa { get; set; }

    public FaceCorpo Face { get; set; }

    /// <summary>Posição horizontal na figura, de 0 (esquerda) a 1 (direita).</summary>
    public double X { get; set; }

    /// <summary>Posição vertical na figura, de 0 (topo) a 1 (base).</summary>
    public double Y { get; set; }

    /// <summary>Nome do ponto/meridiano, como o profissional escreve (ex.: "IG4", "VB20").</summary>
    public string? Nome { get; set; }

    public TecnicaPonto Tecnica { get; set; } = TecnicaPonto.Agulha;

    public string? Observacao { get; set; }

    /// <summary>Ordem de marcação — é ela que numera as bolinhas na tela e no PDF.</summary>
    public int Ordem { get; set; }
}

/// <summary>
/// O mapa corporal de UMA sessão: onde as agulhas (ou ventosas, ou moxa) foram
/// aplicadas. Feature 06 da proposta.
///
/// Existe UM mapa por evolução — o mapa não é um documento à parte, é a parte
/// desenhada da mesma sessão. Por isso o vínculo é 1:1 e apagar a evolução leva o
/// mapa junto: mapa órfão não diz de quem nem de quando.
/// </summary>
public class MapaCorporal
{
    public int Id { get; set; }

    public int EvolucaoId { get; set; }
    public Evolucao? Evolucao { get; set; }

    /// <summary>
    /// Protocolo de onde os pontos vieram, quando o mapa nasceu de um. Guardado para a
    /// tela poder dizer "aplicado do protocolo X" — depois de aplicado, os pontos são
    /// do paciente e podem ser editados sem mexer no protocolo.
    /// </summary>
    public int? ProtocoloOrigemId { get; set; }

    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    public DateTime? AtualizadoEm { get; set; }

    public List<PontoMapa> Pontos { get; set; } = new();

    /// <summary>
    /// Teto de marcações num mapa. Não é limitação técnica: é o sinal de que alguém
    /// está clicando à toa — uma sessão de acupuntura não passa disto.
    /// </summary>
    public const int MaximoPontos = 80;

    /// <summary>Coordenada dentro da figura (0 a 1).</summary>
    public static bool CoordenadaValida(double valor) => valor >= 0 && valor <= 1;
}

/// <summary>
/// Um conjunto de pontos guardado para ser reaplicado. É o "protocolo reutilizável"
/// que a proposta vendeu com o mapa corporal.
///
/// Serve a dois usos que parecem um só: o protocolo DA CLÍNICA
/// (<see cref="PacienteId"/> nulo — "Lombalgia — padrão", vale para qualquer paciente)
/// e o protocolo DE UM PACIENTE ("o esquema da dona Maria"). É o mesmo objeto porque
/// é o mesmo gesto: marcar uma vez e repetir nas próximas sessões.
/// </summary>
public class ProtocoloCorporal
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string? Descricao { get; set; }

    /// <summary>Dono do protocolo. Null = protocolo da clínica, vale para todo mundo.</summary>
    public int? PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Aposentado sai das listas sem sumir dos mapas que já o aplicaram.</summary>
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    public DateTime? AtualizadoEm { get; set; }

    public List<PontoProtocolo> Pontos { get; set; } = new();

    /// <summary>Protocolo da clínica (sem dono), que aparece para qualquer paciente.</summary>
    public bool EhDaClinica => PacienteId is null;
}

/// <summary>
/// Um ponto de um protocolo. Tem os mesmos campos de <see cref="PontoMapa"/> de
/// propósito: aplicar um protocolo é copiar ponto a ponto para a sessão, e a partir daí
/// os dois seguem vidas separadas — editar o mapa do paciente não pode reescrever o
/// protocolo da clínica.
/// </summary>
public class PontoProtocolo
{
    public int Id { get; set; }

    public int ProtocoloCorporalId { get; set; }
    public ProtocoloCorporal? Protocolo { get; set; }

    public FaceCorpo Face { get; set; }

    public double X { get; set; }

    public double Y { get; set; }

    public string? Nome { get; set; }

    public TecnicaPonto Tecnica { get; set; } = TecnicaPonto.Agulha;

    public string? Observacao { get; set; }

    public int Ordem { get; set; }
}

/// <summary>
/// Os documentos clínicos da página 21 da proposta que saem da Recepção.
///
/// Os quatro primeiros são ESCRITOS pelo profissional; os três últimos são MONTADOS a
/// partir do que o sistema já tem (prontuário, cadastro, consentimentos). A diferença
/// importa na emissão, não na impressão: todos viram o mesmo registro numerado.
/// </summary>
public enum TipoDocumentoClinico
{
    Receita,
    Atestado,
    Comparecimento,
    PedidoExame,
    RelatorioEvolucao,
    Consentimento,
    Anamnese,

    /// <summary>
    /// Termo de consentimento de PROCEDIMENTO, assinado pelo paciente (parcela 66) — o
    /// caso que trouxe a feature é o BSV, com a declaração de jejum junto.
    ///
    /// ⚠️ <b>Não é o <see cref="Consentimento"/>.</b> Aquele é o termo <b>LGPD</b>, montado
    /// de <c>ConsentimentoService.Finalidades</c>, e responde "posso tratar seus dados?".
    /// Este responde "você foi informado do risco e está preparado para o procedimento?".
    /// Juntar os dois num tipo só seria o bit sobrecarregado da parcela 49 num papel: não
    /// haveria como conceder um sem o outro, e a segunda via de um sairia com o texto do
    /// outro.
    ///
    /// O TEXTO e as DECLARAÇÕES são <see cref="ModeloDocumento"/>, não código: cada
    /// procedimento tem o seu, quem o redige é a responsabilidade técnica da clínica, e
    /// cada emissão COPIA o modelo — corrigir uma palavra hoje não pode reescrever o que
    /// um paciente assinou no mês passado.
    /// </summary>
    TermoProcedimento,

    /// <summary>
    /// O tipo que ESTE build não conhece — um documento gravado por uma versão mais nova.
    ///
    /// ⚠️ <b>Não é um tipo de documento; é a ausência de um.</b> Ele nunca é escrito, nunca
    /// aparece em <see cref="TipoDocumentoInfo.Todos"/> e nunca sai impresso: o
    /// <c>ConversorEnumTolerante</c> o produz na LEITURA e RECUSA gravá-lo.
    ///
    /// Por que ele precisa existir
    /// ---------------------------
    /// Os cinco apps se auto-atualizam por Velopack, <b>um canal por app</b>, e dividem UM
    /// banco. Isso não é hipótese: é o desenho. Então há sempre uma janela em que o
    /// Consultório já atualizou, gravou um termo de procedimento, e a Recepção ainda não —
    /// e o nome do valor novo chega a quem não o tem no enum.
    ///
    /// Sem este valor, quem lê é <c>Enum.TryParse</c> dentro do conversor do EF, que ESTOURA
    /// e derruba a consulta inteira: a clínica perde a tela de Prescrições por causa de UMA
    /// linha, e a mensagem que sobra ("Cannot convert string value…") não diz a ninguém que
    /// o que falta é atualizar. Foi o que aconteceu em 14/08/2026.
    ///
    /// A alternativa — cair no primeiro valor do enum — seria pior: o termo apareceria como
    /// "Receita", que é mentir sobre um registro de prontuário.
    /// </summary>
    Desconhecido
}

/// <summary>Rótulos e natureza de cada tipo de documento, para a tela e o PDF.</summary>
public static class TipoDocumentoInfo
{
    public static string Rotular(TipoDocumentoClinico tipo) => tipo switch
    {
        TipoDocumentoClinico.Receita => "Receita",
        TipoDocumentoClinico.Atestado => "Atestado",
        TipoDocumentoClinico.Comparecimento => "Declaração de comparecimento",
        TipoDocumentoClinico.PedidoExame => "Pedido de exame",
        TipoDocumentoClinico.RelatorioEvolucao => "Relatório de evolução",
        TipoDocumentoClinico.Consentimento => "Termo de consentimento",
        TipoDocumentoClinico.Anamnese => "Anamnese",
        TipoDocumentoClinico.TermoProcedimento => "Termo de procedimento",

        // Diz o que fazer, não só o que houve: "Desconhecido" sozinho mandaria a clínica
        // procurar defeito no documento, e o que falta é atualizar o programa.
        TipoDocumentoClinico.Desconhecido => "Tipo não reconhecido — atualize o sistema",

        _ => tipo.ToString()
    };

    /// <summary>
    /// O documento é assinado pelo PACIENTE, e não (só) pelo profissional.
    ///
    /// Hoje é um tipo só, e mesmo assim a pergunta mora aqui em vez de espalhada como
    /// <c>== TermoProcedimento</c> pelas telas: a coleta, o alerta da fila, o catálogo de
    /// folhas e o PDF precisam da mesma resposta, e quatro cópias divergem na primeira
    /// vez que um segundo tipo entrar.
    /// </summary>
    public static bool AssinadoPeloPaciente(TipoDocumentoClinico tipo)
        => tipo is TipoDocumentoClinico.TermoProcedimento;

    /// <summary>
    /// O documento é montado pelo sistema a partir do prontuário/cadastro (em vez de
    /// digitado). São os três que a parcela 2 deixou prontos no domínio e sem papel.
    /// </summary>
    public static bool EhMontadoDoProntuario(TipoDocumentoClinico tipo)
        => tipo is TipoDocumentoClinico.RelatorioEvolucao
               or TipoDocumentoClinico.Consentimento
               or TipoDocumentoClinico.Anamnese;

    /// <summary>O documento é uma lista de itens (medicamentos, exames) e exige ao menos um.</summary>
    public static bool ExigeItens(TipoDocumentoClinico tipo)
        => tipo is TipoDocumentoClinico.Receita or TipoDocumentoClinico.PedidoExame;

    /// <summary>
    /// Os tipos que a clínica EMITE. <see cref="TipoDocumentoClinico.Desconhecido"/> fica de
    /// fora de propósito: ele não é papel nenhum, é o nome que este build não soube ler.
    /// Escrita à mão em vez de <c>Enum.GetValues</c> justamente por isso — e é o que faz o
    /// teste "os N documentos geram PDF" continuar cobrando cobertura de cada tipo REAL.
    /// </summary>
    public static IReadOnlyList<TipoDocumentoClinico> Todos { get; } =
    [
        TipoDocumentoClinico.Receita,
        TipoDocumentoClinico.Atestado,
        TipoDocumentoClinico.Comparecimento,
        TipoDocumentoClinico.PedidoExame,
        TipoDocumentoClinico.RelatorioEvolucao,
        TipoDocumentoClinico.Consentimento,
        TipoDocumentoClinico.Anamnese,
        TipoDocumentoClinico.TermoProcedimento
    ];
}

/// <summary>
/// Um documento clínico EMITIDO: receita, atestado, declaração, pedido de exame,
/// relatório, termo ou anamnese. Feature 07 e a página 21 da proposta.
///
/// Documento emitido é FATO, como o consentimento da parcela 2: uma vez impresso e
/// entregue, ele existe no mundo. Por isso não se apaga nem se reescreve — corrige-se
/// CANCELANDO com motivo e emitindo outro. Por isso também o conteúdo fica gravado
/// aqui em vez de ser remontado na hora de reimprimir: a segunda via tem de sair
/// idêntica à que o paciente levou, mesmo que o prontuário tenha andado desde então.
/// </summary>
public class DocumentoClinico
{
    public int Id { get; set; }

    /// <summary>Número sequencial por ano, no formato <c>2026/0001</c>.</summary>
    public string Numero { get; set; } = string.Empty;

    /// <summary>
    /// Código curto impresso no rodapé para conferir a autenticidade da via em papel
    /// (a clínica procura por ele na lista de documentos do paciente).
    /// </summary>
    public string CodigoVerificacao { get; set; } = string.Empty;

    public TipoDocumentoClinico Tipo { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Quem assina. Null quando a clínica ainda não cadastrou a equipe.</summary>
    public int? ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }

    /// <summary>Sessão de origem, quando o documento nasceu de uma evolução.</summary>
    public int? EvolucaoId { get; set; }
    public Evolucao? Evolucao { get; set; }

    /// <summary>
    /// De qual <see cref="ModeloDocumento"/> este documento foi COPIADO (parcela 66).
    ///
    /// É PROCEDÊNCIA, nunca referência viva: o conteúdo continua gravado nas colunas deste
    /// registro, e corrigir o modelo amanhã não muda uma vírgula do que o paciente assinou
    /// — a mesma regra do protocolo do mapa corporal e do modelo de evolução.
    ///
    /// Serve para responder "este paciente já assinou O TERMO DO BSV hoje?": casar por
    /// TIPO não bastaria, porque dois procedimentos no mesmo dia exigem dois termos
    /// diferentes e o primeiro passaria a cobrir o segundo.
    ///
    /// <c>SetNull</c>: apagar um modelo não pode apagar o termo assinado com ele.
    /// </summary>
    public int? ModeloOrigemId { get; set; }
    public ModeloDocumento? ModeloOrigem { get; set; }

    public DateOnly Data { get; set; }

    /// <summary>Título impresso. Vazio usa o rótulo do tipo.</summary>
    public string? Titulo { get; set; }

    /// <summary>Corpo do documento: o texto que vai impresso acima da lista de itens.</summary>
    public string? Corpo { get; set; }

    public string? Observacoes { get; set; }

    // ---- Atestado ----

    /// <summary>Dias de afastamento do atestado.</summary>
    public int? DiasAfastamento { get; set; }

    /// <summary>CID informado no atestado.</summary>
    public string? Cid { get; set; }

    /// <summary>
    /// O paciente autorizou imprimir o CID. Sem autorização expressa o diagnóstico NÃO
    /// vai no atestado — é sigilo do paciente, e o PDF respeita isto mesmo com o campo
    /// preenchido.
    /// </summary>
    public bool CidAutorizado { get; set; }

    // ---- Comparecimento e relatório ----

    public DateOnly? PeriodoInicio { get; set; }
    public DateOnly? PeriodoFim { get; set; }

    public TimeOnly? HoraChegada { get; set; }
    public TimeOnly? HoraSaida { get; set; }

    // ---- Assinatura eletrônica (parcela 43) ----
    //
    // Por que os campos moram AQUI e não em `AssinaturaDocumento`, que já existe:
    // aquela tabela nasceu para a folha de infusão, que tem DOIS papéis (prescritor e
    // enfermagem) e por isso precisa de uma linha por assinatura. O documento clínico
    // tem UMA e só uma — quem emite assina, e corrigir é cancelar e emitir outro —, e
    // um-para-um mora na própria linha. Reaproveitar a outra exigiria tornar
    // `PrescricaoInternaId` anulável, que é `AlterColumn`: migration não aditiva, o que
    // este repositório não faz enquanto houver versões diferentes em campo.

    /// <summary>
    /// Como o documento foi assinado. Null = não foi assinado eletronicamente — a via
    /// vale pela assinatura à caneta na folha impressa, como sempre valeu.
    /// </summary>
    public TipoAssinatura? AssinaturaTipo { get; set; }

    /// <summary>SHA-256 do PDF assinado, em hexadecimal minúsculo.</summary>
    public string? AssinaturaHash { get; set; }

    public string? AssinaturaAlgoritmo { get; set; }

    /// <summary>
    /// Quando foi assinado, pelo relógio da máquina. Sem carimbo do tempo de uma ACT
    /// credenciada esta data é DECLARADA, não provada — e o rodapé do PDF diz isso.
    /// </summary>
    public DateTime? AssinadoEm { get; set; }

    /// <summary>O login que assinou.</summary>
    public int? AssinadoPorUsuarioId { get; set; }

    /// <summary>Nome e conselho COPIADOS no ato — o cadastro muda, o documento não.</summary>
    public string? AssinanteNome { get; set; }

    public string? AssinanteRegistroConselho { get; set; }

    /// <summary>
    /// Só dígitos, e vem de DENTRO do certificado (OID 2.16.76.1.3.1), não do cadastro.
    /// É o que prova que quem assinou é quem o documento diz que assinou.
    /// </summary>
    public string? AssinanteCpf { get; set; }

    public string? CertificadoTitular { get; set; }

    public string? CertificadoEmissor { get; set; }

    public string? CertificadoSerie { get; set; }

    public DateTime? CertificadoValidoDe { get; set; }

    public DateTime? CertificadoValidoAte { get; set; }

    public DateTime? CarimboTempoEm { get; set; }

    public string? CarimboTempoAutoridade { get; set; }

    // ---- Publicação do arquivo assinado (parcela 53) ----

    /// <summary>
    /// Token do link público. Nasce ANTES de o PDF ser gerado, porque a URL vai no QR e o
    /// QR é selado pela assinatura — descobrir o endereço depois obrigaria a mexer no
    /// arquivo assinado, o que quebraria a assinatura que ele carrega.
    ///
    /// É ESTÁVEL: renovar a publicação reusa o mesmo token, para o QR do PDF que o paciente
    /// já tem continuar funcionando. Null = nunca publicado, ou tipo que não se publica
    /// (ver <see cref="PublicacaoDocumento.PodePublicar"/>).
    /// </summary>
    public string? TokenPublicacao { get; set; }

    /// <summary>Quando o arquivo foi enviado ao armazenamento pela última vez.</summary>
    public DateTime? PublicadoEm { get; set; }

    /// <summary>
    /// Até quando o link responde. Vencido, o objeto sai do ar e a clínica republica —
    /// deixá-lo no ar para sempre seria manter um acervo público de dado de saúde.
    /// </summary>
    public DateOnly? PublicadoAte { get; set; }

    /// <summary>O link está no ar nesta data.</summary>
    public bool LinkNoAr(DateOnly hoje)
        => TokenPublicacao is not null && PublicadoAte is { } ate && hoje <= ate;

    /// <summary>
    /// O PDF assinado, guardado byte a byte.
    ///
    /// Tabela à parte para a listagem não arrastar megabytes — e, mais importante, é a
    /// razão de a reimpressão devolver os bytes GUARDADOS em vez de gerar de novo: a
    /// assinatura cobre uma faixa de bytes do arquivo, e um PDF "igual" regerado agora
    /// abriria como inválido no leitor do farmacêutico.
    /// </summary>
    public int? ArquivoAssinadoId { get; set; }
    public ArquivoAssinado? ArquivoAssinado { get; set; }

    // ---- Assinatura do PACIENTE (parcela 66) ----
    //
    // Um-para-um com o documento (um termo, um paciente, uma assinatura), então mora na
    // própria linha — o mesmo argumento que o bloco de cima já escreveu para a assinatura
    // do profissional. O que NÃO cabe aqui é o traço: são bytes, e bytes na linha fariam
    // a listagem de documentos arrastar imagem a cada abertura de ficha.
    //
    // ⚠️ E o paciente NÃO assina com certificado. Termo de consentimento é documento
    // ENTRE AS PARTES, e a MP 2.200-2/2001 (art. 10, §2º) admite outro meio quando as
    // partes o aceitam — a Lei 14.063/2020 chama isso de assinatura SIMPLES. Exigir e-CPF
    // do paciente seria inviável e desnecessário. O que dá valor a ela é a EVIDÊNCIA
    // gravada abaixo, e é só isso que o rodapé do PDF pode afirmar.

    /// <summary>Quando o paciente assinou. Null = ainda não assinou.</summary>
    public DateTime? PacienteAssinadoEm { get; set; }

    /// <summary>Como o traço foi colhido — hoje só há um meio, e a coluna prepara o link.</summary>
    public MeioAssinaturaPaciente? PacienteAssinaturaMeio { get; set; }

    /// <summary>
    /// SHA-256 do conteúdo que o paciente TINHA NA FRENTE, em hexadecimal minúsculo.
    ///
    /// É a metade que responde "assinou o quê?" — sem ela a assinatura prova que alguém
    /// riscou a tela, e não o que ele leu antes de riscar. Cobre título, corpo e as
    /// declarações com as respostas dadas.
    /// </summary>
    public string? PacienteAssinaturaHash { get; set; }

    /// <summary>
    /// O documento de identidade apresentado no ato ("CPF 123.456.789-00", "RG 12.345.678").
    ///
    /// É o que substitui o certificado: quem confere a identidade é a pessoa da clínica que
    /// estava na frente do paciente, e o registro diz o que ela viu.
    /// </summary>
    public string? PacienteDocumentoConferido { get; set; }

    /// <summary>
    /// Quem da clínica colheu a assinatura — <c>SessaoUsuario.Atual.Operador</c>, nunca o
    /// login do Windows: no balcão duas pessoas dividem a máquina, e a testemunha é
    /// justamente o que se pergunta quando o termo é contestado.
    /// </summary>
    public string? PacienteAssinaturaTestemunha { get; set; }

    /// <summary>O traço em si (PNG), em tabela à parte.</summary>
    public int? TracoAssinaturaId { get; set; }
    public TracoAssinatura? TracoAssinatura { get; set; }

    /// <summary>
    /// O paciente RECUSOU assinar, e por quê.
    ///
    /// Recusa é fato: sem onde escrevê-la, o termo fica eternamente "pendente" e ninguém
    /// distingue quem ainda não foi chamado de quem disse não. Recusado, o documento
    /// continua emitido — ele é a prova de que o termo foi apresentado.
    /// </summary>
    public DateTime? PacienteRecusouEm { get; set; }

    public string? MotivoRecusaPaciente { get; set; }

    // ---- Rastro ----

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    /// <summary>Quando foi cancelado. Cancelar NÃO apaga: a via em papel continua no mundo.</summary>
    public DateTime? CanceladoEm { get; set; }

    public string? MotivoCancelamento { get; set; }

    public List<ItemDocumento> Itens { get; set; } = new();

    public bool Cancelado => CanceladoEm is not null;

    /// <summary>Título que vai impresso: o informado ou, na falta, o rótulo do tipo.</summary>
    public string TituloImpresso
        => string.IsNullOrWhiteSpace(Titulo) ? TipoDocumentoInfo.Rotular(Tipo) : Titulo!;

    /// <summary>O CID só sai impresso quando o paciente autorizou.</summary>
    public string? CidImpresso
        => CidAutorizado && !string.IsNullOrWhiteSpace(Cid) ? Cid : null;

    /// <summary>O documento foi assinado eletronicamente.</summary>
    public bool AssinadoEletronicamente => AssinaturaTipo is not null;

    /// <summary>
    /// Assinatura que a lei presume autêntica sem a outra parte precisar concordar — a
    /// única que serve para atestado em meio eletrônico (art. 13 da Lei 14.063/2020).
    /// </summary>
    public bool AssinaturaQualificada => AssinaturaTipo == TipoAssinatura.IcpBrasil;

    /// <summary>
    /// O que o rodapé do PDF e a tela escrevem sobre a assinatura.
    ///
    /// A frase muda com o carimbo do tempo de propósito: sem ACT contratada a data é a
    /// do relógio de quem assinou, e chamá-la de comprovada seria prometer mais do que
    /// a via garante — o mesmo cuidado que a folha de infusão já toma.
    /// </summary>
    public string FraseAssinatura => (AssinaturaTipo, CarimboTempoEm) switch
    {
        (null, _) => "Documento sem assinatura eletrônica: vale pela assinatura à caneta na via impressa.",
        (TipoAssinatura.IcpBrasil, { } quando) =>
            $"Assinado digitalmente por {AssinanteNome} com certificado ICP-Brasil, "
            + $"com carimbo do tempo de {quando:dd/MM/yyyy HH:mm}.",
        (TipoAssinatura.IcpBrasil, null) =>
            $"Assinado digitalmente por {AssinanteNome} com certificado ICP-Brasil em "
            + $"{AssinadoEm:dd/MM/yyyy HH:mm} (data declarada pelo relógio de quem assinou).",
        _ => $"Assinado eletronicamente por {AssinanteNome} em {AssinadoEm:dd/MM/yyyy HH:mm}."
    };

    /// <summary>O paciente assinou este documento.</summary>
    public bool PacienteAssinou => PacienteAssinadoEm is not null;

    /// <summary>O paciente recusou assinar — e isso está registrado.</summary>
    public bool PacienteRecusou => PacienteRecusouEm is not null;

    /// <summary>
    /// O documento espera a assinatura do paciente: é do tipo que ele assina, não foi
    /// cancelado, e ele nem assinou nem recusou.
    /// </summary>
    public bool AguardaAssinaturaDoPaciente
        => TipoDocumentoInfo.AssinadoPeloPaciente(Tipo)
           && !Cancelado && !PacienteAssinou && !PacienteRecusou;

    /// <summary>
    /// O que o rodapé do PDF escreve sobre a assinatura do PACIENTE.
    ///
    /// ⚠️ Ela diz "assinatura eletrônica simples" e jamais "assinatura digital" ou
    /// "validade jurídica ICP-Brasil" — nada disso é verdade sobre o traço do paciente, e
    /// garantia aparente é pior que ausência de garantia (a regra do carimbo escaneado da
    /// parcela 3). O que a frase entrega é a EVIDÊNCIA, que é o que de fato sustenta o
    /// termo se ele for contestado: quem, quando, diante de quem, com que documento, e o
    /// selo do conteúdo que estava na tela.
    /// </summary>
    public string FraseAssinaturaPaciente
    {
        get
        {
            if (PacienteRecusou)
                return $"O paciente recusou assinar em {PacienteRecusouEm:dd/MM/yyyy HH:mm}"
                       + (string.IsNullOrWhiteSpace(MotivoRecusaPaciente)
                           ? "." : $": {MotivoRecusaPaciente}");

            if (!PacienteAssinou)
                return "Aguardando a assinatura do paciente.";

            var frase = "Assinatura eletrônica simples, colhida presencialmente em "
                        + $"{PacienteAssinadoEm:dd/MM/yyyy 'às' HH:mm}";

            if (!string.IsNullOrWhiteSpace(PacienteAssinaturaTestemunha))
                frase += $", diante de {PacienteAssinaturaTestemunha}";

            if (!string.IsNullOrWhiteSpace(PacienteDocumentoConferido))
                frase += $", com {PacienteDocumentoConferido} conferido";

            if (!string.IsNullOrWhiteSpace(PacienteAssinaturaHash))
                frase += $". Conteúdo selado por SHA-256 {PacienteAssinaturaHash[..12]}…";

            return frase + $" Código de conferência {CodigoVerificacao}.";
        }
    }

    /// <summary>
    /// O que o rodapé escreve quando o conteúdo NÃO bate com o selo (parcela 66, 2ª rodada).
    ///
    /// Vazio = nada a dizer (não há selo, ou ele confere). Guardar um hash que ninguém
    /// recalcula é guardar um número — a conferência só vira garantia quando ela tem
    /// consequência visível, e a consequência é esta frase saindo impressa na segunda via.
    /// **Falha exibida como sucesso é o desfecho que este projeto recusa**: um termo
    /// alterado depois da assinatura não pode sair com um rodapé afirmando integridade.
    /// </summary>
    public string AvisoDeSeloQuebrado
    {
        get
        {
            if (!PacienteAssinou || string.IsNullOrWhiteSpace(PacienteAssinaturaHash))
                return string.Empty;

            return SeloDoConteudo() == PacienteAssinaturaHash
                ? string.Empty
                : "⚠ ATENÇÃO: o conteúdo deste termo NÃO confere com o que foi selado no "
                  + "momento da assinatura. Esta via não prova o que o paciente assinou.";
        }
    }

    /// <summary>
    /// SHA-256 do que o paciente tinha na frente — a mesma montagem que
    /// <c>AssinaturaDoPacienteService</c> grava na coleta.
    ///
    /// Mora na ENTIDADE porque quem precisa dela são dois: o serviço, que a grava, e o PDF,
    /// que a recalcula ao imprimir. Duas montagens divergiriam na primeira correção, e a
    /// divergência apareceria como "selo quebrado" em todo termo válido — que é como se
    /// ensina alguém a ignorar o aviso.
    ///
    /// Cultura INVARIANTE de propósito: o hash é gravado e recalculado meses depois,
    /// possivelmente noutra máquina.
    /// </summary>
    public string SeloDoConteudo()
    {
        var texto = new System.Text.StringBuilder();

        texto.Append(Numero).Append('\n');
        texto.Append(Data.ToString("yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        texto.Append(TituloImpresso).Append('\n');
        texto.Append(Corpo ?? string.Empty).Append('\n');

        foreach (var item in Itens.OrderBy(i => i.Ordem))
            texto.Append(item.Ordem).Append('|')
                 .Append(item.Descricao).Append('|')
                 .Append(item.Detalhe ?? string.Empty).Append('|')
                 .Append(item.Quantidade ?? string.Empty).Append('\n');

        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(texto.ToString()))).ToLowerInvariant();
    }
}

/// <summary>Por onde o traço do paciente entrou no sistema.</summary>
public enum MeioAssinaturaPaciente
{
    /// <summary>
    /// Na tela da própria clínica — tablet, monitor sensível ao toque, mesa de assinatura
    /// ou mouse. É o único meio da parcela 66, e cobre o caso que a motivou: o paciente do
    /// BSV está de corpo presente minutos antes do procedimento.
    /// </summary>
    NaClinica,

    /// <summary>
    /// Assinou remotamente, por link. ⚠️ NÃO IMPLEMENTADO — o valor existe para a coluna
    /// não precisar mudar depois, e porque é ele que torna a leitura honesta: sem ele, o
    /// dia em que o link existir todo termo antigo passaria a parecer remoto.
    ///
    /// Ele exige um componente que hoje não temos: <c>IArmazenamentoPublico</c> publica e
    /// remove — ele não RECEBE, e um S3 estático não aceita POST.
    /// </summary>
    LinkRemoto
}

/// <summary>
/// O traço da assinatura do paciente, em PNG.
///
/// Tabela à parte pela mesma razão do <see cref="ArquivoAssinado"/> e do
/// <see cref="AnexoProntuario"/>: a listagem de documentos de um paciente com quarenta
/// termos não pode arrastar quarenta imagens do banco para desenhar quarenta linhas.
///
/// Não se apaga junto do documento (<c>SetNull</c>, como o arquivo assinado): cancelar um
/// termo não pode destruir a prova de que alguém o assinou.
/// </summary>
public class TracoAssinatura
{
    public int Id { get; set; }

    /// <summary>PNG com fundo transparente, do tamanho da área de coleta.</summary>
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();

    /// <summary>Largura e altura em que foi colhido — o PDF precisa manter a proporção.</summary>
    public int Largura { get; set; }

    public int Altura { get; set; }

    public DateTime ColhidoEm { get; set; } = DateTime.Now;

    public int Tamanho => Conteudo.Length;
}

/// <summary>
/// Uma linha do documento. É genérica de propósito, porque as sete impressões são a
/// mesma forma com nomes diferentes: medicamento + posologia na receita, exame +
/// indicação no pedido, sessão + evolução no relatório, pergunta + resposta na
/// anamnese. Uma tabela por tipo daria sete tabelas com as mesmas três colunas.
/// </summary>
public class ItemDocumento
{
    public int Id { get; set; }

    public int DocumentoClinicoId { get; set; }
    public DocumentoClinico? Documento { get; set; }

    public int Ordem { get; set; }

    /// <summary>A linha em si: o medicamento, o exame, a data da sessão, a pergunta.</summary>
    public string Descricao { get; set; } = string.Empty;

    /// <summary>O que a acompanha: posologia, indicação clínica, resposta, evolução.</summary>
    public string? Detalhe { get; set; }

    /// <summary>Quantidade, quando faz sentido ("1 caixa", "2 frascos").</summary>
    public string? Quantidade { get; set; }
}

/// <summary>
/// Modelo reutilizável de documento — os "modelos de receita e orientação" da feature 07.
///
/// Nasce de um documento que o profissional acabou de escrever ("salvar como modelo"),
/// que é o gesto real: ninguém senta para cadastrar modelos antes de precisar deles.
/// Por isso não há tela própria de cadastro; o modelo se cria e se aplica de dentro da
/// janela do documento.
/// </summary>
public class ModeloDocumento
{
    public int Id { get; set; }

    public TipoDocumentoClinico Tipo { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string? Titulo { get; set; }

    public string? Corpo { get; set; }

    public bool Ativo { get; set; } = true;

    public int Ordem { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    public DateTime? AtualizadoEm { get; set; }

    public List<ItemModelo> Itens { get; set; } = new();
}

/// <summary>Uma linha de um modelo, com os mesmos campos de <see cref="ItemDocumento"/>.</summary>
public class ItemModelo
{
    public int Id { get; set; }

    public int ModeloDocumentoId { get; set; }
    public ModeloDocumento? Modelo { get; set; }

    public int Ordem { get; set; }

    public string Descricao { get; set; } = string.Empty;

    public string? Detalhe { get; set; }

    public string? Quantidade { get; set; }
}

/// <summary>
/// "Esta modalidade exige termo assinado pelo paciente, e é ESTE termo" (parcela 66).
///
/// É DADO e não código, pela mesma razão do formato do número da guia (parcela 45): a
/// clínica faz BSV hoje e pode fazer outro procedimento amanhã, e amarrar a exigência a um
/// <c>switch</c> sobre <see cref="ModalidadeAtendimento"/> obrigaria a publicar versão
/// nova a cada procedimento novo. Aqui é uma linha em Configurações.
///
/// A validade é escolha da clínica por PROCEDIMENTO — ver
/// <see cref="SoValeNoDiaDoProcedimento"/>.
/// </summary>
public class ExigenciaTermoProcedimento
{
    public int Id { get; set; }

    /// <summary>A família da modalidade — <c>BsvApenas</c>, <c>BsvComAcupuntura</c>…</summary>
    public ModalidadeAtendimento Modalidade { get; set; }

    /// <summary>
    /// Código da variante no catálogo, quando a exigência é só dela. Null = vale para toda
    /// a família, que é o caso normal: quem faz BSV assina o termo do BSV, seja qual for o
    /// nome que a clínica deu à variante.
    /// </summary>
    public string? ModalidadeCodigo { get; set; }

    /// <summary>O modelo de termo que será COPIADO na emissão.</summary>
    public int ModeloDocumentoId { get; set; }
    public ModeloDocumento? Modelo { get; set; }

    /// <summary>
    /// Desligar em vez de apagar: a clínica que suspende a exigência por um mês não perde
    /// o texto nem a amarração, e o histórico de quem já assinou continua fazendo sentido.
    /// </summary>
    public bool Ativa { get; set; } = true;

    /// <summary>
    /// O termo só vale para a sessão do DIA — assinado ontem, é pedido de novo hoje.
    ///
    /// ⚠️ Nasce <b>FALSO</b>, e isso é decisão da clínica (ago/2026, 3ª rodada): o
    /// consentimento do procedimento é assinado **quando o paciente estiver por perto** —
    /// inclusive na consulta em que ele vem tirar dúvidas, semanas antes —, e obrigar a
    /// esperar o dia jogaria fora justamente o momento em que ele lê o texto com calma.
    /// Assinado uma vez, está cumprido.
    ///
    /// O campo existe porque as DECLARAÇÕES moram dentro do termo, e nem toda declaração
    /// sobrevive à antecedência: "estou em jejum" assinado na semana passada é uma
    /// afirmação sobre o futuro. A clínica que quiser perguntar o jejum no dia cria um
    /// termo curto só com essa declaração e liga esta caixa — os dois convivem, porque a
    /// exigência é por MODELO e não por tipo.
    ///
    /// A primeira versão desta parcela não tinha o campo, com o argumento de que "regra com
    /// exceção que ninguém exerce é código a mais". A cliente exerceu a exceção antes de a
    /// feature chegar à clínica — e é ela quem sabe quando o paciente aparece.
    /// </summary>
    public bool SoValeNoDiaDoProcedimento { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }
}
