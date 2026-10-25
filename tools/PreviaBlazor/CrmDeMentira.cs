// Dados FICTÍCIOS da prévia — nenhuma pessoa real, nenhum telefone real.
using System.IO;
using System.Globalization;
using System.Text;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Crm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PreviaBlazor;

/// <summary>
/// As pessoas das conversas do WhatsApp (o roteiro de <c>docs/mockups/whatsapp-nativo/app.js</c>):
/// UM lugar para o nome e o telefone, que a semente usa para criar as fichas e o
/// <see cref="CrmDeMentira"/> usa para achá-las. Terezinha e Pedro têm o MESMO telefone (o caso
/// "duas fichas — conferir"); o novo contato não tem ficha nenhuma.
/// </summary>
public static class PessoasDoWhatsApp
{
    public sealed record Pessoa(string Nome, string Telefone)
    {
        /// <summary>O número como o CRM guarda: 55 + DDD + número, só dígitos.</summary>
        public string NoCrm => "55" + new string(Telefone.Where(char.IsAsciiDigit).ToArray());
    }

    // As conversas.
    public static readonly Pessoa Maria = new("Maria das Graças Silva", "(22) 99876-5432");
    public static readonly Pessoa NovoContato = new("", "(22) 99712-0045");
    public static readonly Pessoa Terezinha = new("Terezinha Souza", "(22) 98811-2233");
    public static readonly Pessoa Pedro = new("Pedro Henrique Souza", "(22) 98811-2233");
    public static readonly Pessoa Carlos = new("Carlos Eduardo Mota", "(22) 99133-7788");
    public static readonly Pessoa Joao = new("João Batista Ferreira", "(22) 99650-1010");
    public static readonly Pessoa Roberto = new("Roberto Lima", "(22) 99240-3344");
    public static readonly Pessoa Fernanda = new("Fernanda Nogueira", "(22) 99877-4455");
    public static readonly Pessoa Antonio = new("Antônio Carvalho", "(22) 99333-9090");
    public static readonly Pessoa Alzira = new("Alzira Pinto", "(22) 99501-2020");
    /// <summary>Escreveu de um número que não está em ficha nenhuma; a etiqueta do @SemDor pede atenção (reação ao tratamento).</summary>
    public static readonly Pessoa Lucia = new("Lúcia Andrade", "(22) 99588-1414");

    // Só a retenção (sem conversa no CRM: a tela a cria em memória ao abrir — PR 4).
    public static readonly Pessoa Sebastiao = new("Sebastião Ramos", "(22) 99120-4477");
    public static readonly Pessoa Helena = new("Helena Marques", "(22) 99877-0101");
    public static readonly Pessoa Marcos = new("Marcos Vinícius Teles", "(22) 99311-8899");
    public static readonly Pessoa Rita = new("Rita de Cássia Lopes", "(22) 99455-6677");
    public static readonly Pessoa Felipe = new("Felipe Arantes", "(22) 99222-3131");
    public static readonly Pessoa Claudia = new("Cláudia Reis", "(22) 99666-0202");
    public static readonly Pessoa Ines = new("Inês Barreto", "(22) 99700-1234");
    public static readonly Pessoa Geraldo = new("Geraldo Nunes", "(22) 99100-9900");
}

/// <summary>
/// O RELÓGIO da prévia do WhatsApp. O mockup "nasce" às 10:12; aqui tudo é deslocado pelo
/// mesmo tanto (<see cref="Deslocamento"/>, arredondado PARA BAIXO em meia hora) para que a
/// tela pareça viva em qualquer hora de qualquer dia: o que no mockup é "hoje 09:58" fica
/// 14 minutos (ou até 44) antes do momento em que a prévia subiu, e a sessão "hoje 14:00"
/// fica de 3 h 18 a 3 h 48 depois — sempre no futuro, sempre num horário redondo.
/// Capturado UMA vez por processo: a semente (que roda antes) e o CRM de mentira (criado
/// depois) leem o mesmo instante, e dentro de uma execução tudo é determinístico.
/// </summary>
public static class RelogioDaPrevia
{
    private static readonly DateTime inicio = DateTime.Now;
    private static readonly TimeSpan meiaHora = TimeSpan.FromMinutes(30);

    /// <summary>O instante em que a prévia (ou a suíte) começou.</summary>
    public static DateTime Inicio => inicio;

    /// <summary>Quanto o "10:12 de hoje" do mockup foi deslocado: múltiplo de 30 min, nunca para depois do início.</summary>
    public static TimeSpan Deslocamento
    {
        get
        {
            var diferenca = inicio - inicio.Date.AddHours(10).AddMinutes(12);
            return TimeSpan.FromTicks((long)Math.Floor(diferenca.Ticks / (double)meiaHora.Ticks) * meiaHora.Ticks);
        }
    }

    /// <summary>O "dia D às hh:mm" do mockup, já deslocado (hora LOCAL, como a agenda grava).</summary>
    public static DateTime Em(int dias, int hora, int minuto) => inicio.Date.AddDays(dias).AddHours(hora).AddMinutes(minuto) + Deslocamento;

    /// <summary>O mesmo instante, como o CRM carimba (UTC).</summary>
    public static DateTimeOffset EmUtc(int dias, int hora, int minuto, int segundo = 0)
        => new DateTimeOffset(Em(dias, hora, minuto).AddSeconds(segundo)).ToUniversalTime();

    // As frases que citam horários são MONTADAS com eles: o texto escrito à mão diria "14:00"
    // enquanto a agenda, deslocada, marca 14:30.
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Hora(DateTime d) => d.ToString("HH:mm", PtBr);

    /// <summary>"14h" ou "14h30" — como o paciente escreve.</summary>
    public static string HoraFalada(DateTime d) => d.Minute == 0 ? $"{d.Hour}h" : $"{d.Hour}h{d.Minute:00}";

    public static string DiaDaSemana(DateTime d) => d.DayOfWeek switch
    {
        DayOfWeek.Monday => "segunda", DayOfWeek.Tuesday => "terça", DayOfWeek.Wednesday => "quarta",
        DayOfWeek.Thursday => "quinta", DayOfWeek.Friday => "sexta", DayOfWeek.Saturday => "sábado", _ => "domingo"
    };

    /// <summary>"terça 28/10 às 14:00".</summary>
    public static string Quando(DateTime d) => $"{DiaDaSemana(d)} {d.ToString("dd/MM", PtBr)} às {Hora(d)}";

    /// <summary>O texto em GSM-7, como o CRM manda o SMS (sem acento).</summary>
    public static string SemAcento(string texto)
        => new string(texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).Normalize(NormalizationForm.FormC);
}

/// <summary>
/// O CRM DE MENTIRA (out/2026, PR 3 do WhatsApp nativo — docs/whatsapp-nativo-implementacao.md):
/// um <see cref="IClienteDoCrm"/> em memória com as conversas do mockup aprovado, para a prévia
/// fotografar a tela e o <c>TelasBlazorAbremTests</c> clicar cada botão SEM um CRM de verdade.
/// O <see cref="ClienteDoCrmHttp"/> nunca é chamado na prévia nem em teste (<see cref="Registrar"/>).
///
/// O que ele imita do CRM (semdor-crm: <c>Store.cs</c>, <c>ServiceWorkflow.cs</c>,
/// <c>ReceptionHandoffs.cs</c>, <c>PatientIdentity.cs</c>, <c>ListaDesde.cs</c>), com as MESMAS frases:
/// <list type="bullet">
/// <item>toda escrita confere a <c>Version</c> (velha = <see cref="RecusaDoCrm"/> 409 "A conversa
/// mudou…"), incrementa a <c>Version</c> e carimba <c>UpdatedAt</c>;</item>
/// <item>o <c>Id</c> do envio e da nota é Guid "D" (o "N" é 400) e torna o envio idempotente;</item>
/// <item>escrever é assumir (sem dono assume; encerrada reabre; de outra pessoa é 409); nota,
/// transferir e encerrar pedem a conversa com quem escreve; devolver à fila pede o motivo;</item>
/// <item>a janela de 24 h: texto livre fora dela é 409 (o modelo passa); SMS só texto, sem acento;</item>
/// <item>o <c>?desde=</c> devolve o que mudou com 5 s de margem e o <c>Now</c> lido ANTES;</item>
/// <item>"não lidas" por pessoa (<c>/read</c> só sobe, e não carimba <c>UpdatedAt</c>).</item>
/// </list>
///
/// ⚠️ Duas diferenças de propósito:
/// <list type="bullet">
/// <item><b>O envio anda nas LEITURAS</b>: a resposta nasce <c>sending</c> e cada leitura seguinte
/// (lista, desde, abrir) a leva um passo — <c>accepted</c> → <c>delivered</c> → <c>seen</c>, com
/// <c>Version</c> nova como o recibo do provedor faz lá. Sem timer: a foto do "Enviando" é
/// possível e o teste não depende do relógio. No CRM real a resposta do <c>/reply</c> já volta
/// <c>accepted</c>; por isso aqui escrita nenhuma recusa "Aguarde o envio em andamento".</item>
/// <item><b>Todo mundo é a "ana"</b> (Ana Lima, supervisora no CRM): o falso não conhece o mapa
/// usuário do app → agente do CRM. É a recepcionista que entra na prévia.</item>
/// </list>
///
/// <b>Os <c>PatientId</c></b>: as fichas são criadas pela semente
/// (<c>Sementes/ConversasDoWhatsApp.cs</c>) num SQLite cujos ids são gerados, e o banco da prévia
/// e o de cada classe de teste são bancos diferentes. Por isso o falso não guarda número de
/// ficha: na PRIMEIRA chamada ele abre um escopo (<see cref="IServiceScopeFactory"/>), procura
/// cada ficha das conversas conferidas pelo NOME e confere o TELEFONE
/// (<see cref="ConversasNaTela.MesmoTelefone"/>) — os dois vêm de <see cref="PessoasDoWhatsApp"/>,
/// a mesma fonte da semente. Ficha não achada (banco sem a semente) deixa a conversa sem ficha,
/// nunca com o id de outra pessoa. Escolhido em vez de a semente gravar os ids num estático:
/// o estático é do processo, e a suíte semeia vários bancos em paralelo.
///
/// <b>As falhas</b>, para a foto dos estados: a variável de ambiente <see cref="VariavelDoModo"/>
/// = <c>fora-do-ar</c> (toda chamada dá <see cref="CrmIndisponivel"/>) ou <c>nao-configurado</c>
/// (<see cref="IClienteDoCrm.ConfiguradoAsync"/> falso e o resto <see cref="CrmNaoConfigurado"/>);
/// nos testes, a propriedade <see cref="Modo"/>. Sem a variável, o "configurado" é o de verdade:
/// o endereço do CRM nas Configurações (apagá-lo na tela de Configurações mostra o "ainda não foi ligado").
///
/// <b>O @SemDor</b> (24/10): responde a sugestão, a revisão e o resumo com textos fixos plausíveis (como o
/// <c>SemDorSecretariaDemo</c> do CRM) e põe etiquetas nas linhas de quem espera a clínica — uma delas de atenção
/// (a Lúcia). <see cref="VariavelDoModo"/> = <c>semdor-desligado</c> (a IA desligada: sem botão nem etiqueta) ou
/// <c>semdor-falhando</c> (ligada, sem responder: a frase do 502); nos testes, a propriedade <see cref="SemDor"/>.
/// </summary>
public sealed class CrmDeMentira : IClienteDoCrm
{
    /// <summary>A variável de ambiente que simula a falha: <c>fora-do-ar</c> ou <c>nao-configurado</c>.</summary>
    public const string VariavelDoModo = "CLINICA_PREVIA_CRM";

    public enum ModoDoCrm { Normal, ForaDoAr, NaoConfigurado }

    /// <summary>Como o CRM responde agora (começa pela <see cref="VariavelDoModo"/>).</summary>
    public ModoDoCrm Modo { get; set; }

    public enum ModoDoSemDor { Ligado, Desligado, Falhando }

    /// <summary>Como o @SemDor responde agora (começa pela <see cref="VariavelDoModo"/>).</summary>
    public ModoDoSemDor SemDor { get; set; }

    /// <summary>O número oficial do WhatsApp da clínica (fictício) e o remetente do SMS.</summary>
    public const string RemetenteWhatsApp = "5522999990000";
    public const string RemetenteSms = "sms";

    /// <summary>Quem entra no CRM, seja qual for o usuário do app.</summary>
    public const string Eu = "ana";

    /// <summary>A margem do <c>?desde=</c> (a do <c>ListaDesde.Margem</c> do CRM).</summary>
    public static readonly TimeSpan MargemDoDesde = TimeSpan.FromSeconds(5);

    /// <summary>Os ids das conversas (Guid "D" fixos, como o CRM gera — fixos para o teste achá-las).</summary>
    public static class Ids
    {
        public const string Maria = "0f6a1e2b-0001-4c3d-9a10-000000000001";
        public const string NovoContato = "0f6a1e2b-0002-4c3d-9a10-000000000002";
        public const string Terezinha = "0f6a1e2b-0003-4c3d-9a10-000000000003";
        public const string Carlos = "0f6a1e2b-0004-4c3d-9a10-000000000004";
        public const string Joao = "0f6a1e2b-0005-4c3d-9a10-000000000005";
        public const string Roberto = "0f6a1e2b-0006-4c3d-9a10-000000000006";
        public const string Fernanda = "0f6a1e2b-0007-4c3d-9a10-000000000007";
        public const string Antonio = "0f6a1e2b-0008-4c3d-9a10-000000000008";
        public const string Alzira = "0f6a1e2b-0009-4c3d-9a10-000000000009";
        public const string Lucia = "0f6a1e2b-0010-4c3d-9a10-000000000010";
    }

    /// <summary>
    /// A leitura do @SemDor de cada conversa (o que o <c>SemDorTriagemWorker</c> do CRM teria guardado). Como no CRM,
    /// a linha só a mostra enquanto a última mensagem é do paciente e a conversa está aberta: quem responde a apaga.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, AppSemDorTag> Etiquetas = new Dictionary<string, AppSemDorTag>
    {
        [Ids.Maria] = new("remarcar", "Remarcar", false),
        [Ids.NovoContato] = new("valores", "Valores", false),
        [Ids.Terezinha] = new("confirmar", "Confirmou", false),
        [Ids.Alzira] = new("agendar", "Quer marcar", false),
        [Ids.Lucia] = new("urgencia", "Urgência clínica", true),
    };

    public const string SemDorDesligado = "O @SemDor está desligado neste CRM.";
    public const string SemDorSemResposta = "O @SemDor não respondeu agora. Escreva normalmente ou tente de novo em instantes.";

    private static readonly AppAgent[] Agentes =
    [
        new(Eu, "Ana Lima"), new("bruna", "Bruna Reis"), new("carla", "Carla Mendes"), new("paulo", "Paulo (Financeiro)")
    ];

    private static readonly string[] Filas = ["Recepção", "Agendamento", "Financeiro"];

    /// <summary>Os resultados de encerrar, com os rótulos de <c>Store.Outcomes</c> (ServiceWorkflow.cs).</summary>
    public static readonly IReadOnlyDictionary<string, string> Resultados = new Dictionary<string, string>
    {
        ["resolved"] = "Solicitação resolvida", ["scheduled"] = "Agendamento realizado",
        ["information"] = "Orientação prestada", ["followup"] = "Retorno necessário",
        ["no_response"] = "Sem retorno do paciente", ["duplicate"] = "Contato duplicado / engano"
    };

    /// <summary>O lembrete automático das 06:00 e o convite de retorno: os nomes REAIS aprovados na Meta em produção.</summary>
    public const string ModeloLembrete = "semdor_lembrete_sessao";
    public const string ModeloRecall = "semdor_recall";

    /// <summary>
    /// Os modelos aprovados. Primeiro os REAIS de produção (prefixo <c>semdor_</c>, a tabela KNOWN de
    /// <c>semdor-crm/src/Clinica.Crm/wwwroot/template-picker.js</c>: nome e quantidade de variáveis
    /// iguais; os textos são plausíveis, não os da Meta), depois os quatro do mockup e os dois de
    /// demonstração do CRM (<c>RichMessaging.cs</c>). Parameters = quantos <c>{{n}}</c> o texto tem.
    /// </summary>
    public static readonly AppTemplate[] Modelos =
    [
        new(ModeloRecall, "pt_BR", "APPROVED",
            "Olá, {{1}}! Aqui é a Clínica SemDor. Sentimos sua falta: quer que a gente veja um horário para você voltar ao tratamento? "
            + "Para não receber mais convites, responda PARAR.", 1, true, "", [], "MARKETING"),
        new("semdor_iniciar_atendimento", "pt_BR", "APPROVED",
            "Olá, {{1}}! Aqui é a recepção da Clínica SemDor, sobre {{2}}. Toque em \"Continuar atendimento\" para conversarmos por aqui.",
            2, true, "", ["Continuar atendimento"], "UTILITY"),
        new(ModeloLembrete, "pt_BR", "APPROVED",
            "Olá, {{1}}! Lembrando do seu horário na Clínica SemDor {{2}}. Confirme a presença ou escolha uma opção abaixo.",
            2, true, "", ["Confirmar", "Remarcar", "Cancelar"], "UTILITY"),
        new("semdor_horario_remarcado", "pt_BR", "APPROVED",
            "Olá, {{1}}! Seu horário na Clínica SemDor foi remarcado para {{2}}. Qualquer dúvida, é só responder por aqui.",
            2, true, "", [], "UTILITY"),
        new("semdor_agendamento_confirmado", "pt_BR", "APPROVED",
            "Olá, {{1}}! Seu horário na Clínica SemDor está marcado para {{2}}. Até lá!", 2, true, "", [], "UTILITY"),
        new("semdor_retorno_contato", "pt_BR", "APPROVED",
            "Olá, {{1}}! Aqui é a recepção da Clínica SemDor, retornando o seu contato. Como podemos ajudar?", 1, true, "", [], "UTILITY"),
        new("semdor_documento", "pt_BR", "APPROVED",
            "Olá, {{1}}! Segue o documento que você pediu à Clínica SemDor. Qualquer dúvida, é só responder por aqui.", 1, true, "", [], "UTILITY"),
        new(ModeloDoWhatsApp.LembreteSessao, "pt_BR", "APPROVED",
            "Olá, {{1}}! Lembrando que você tem {{2}} na Clínica SemDor {{3}}. Responda SIM para confirmar ou NÃO se precisar remarcar.",
            3, true, "", [], "UTILITY"),
        new(ModeloDoWhatsApp.RetornoTratamento, "pt_BR", "APPROVED",
            "Olá, {{1}}! Sentimos sua falta na {{2}}: já são {{3}} dias desde a última sessão. Quer que a gente veja um horário para você?",
            3, true, "", [], "UTILITY"),
        new(ModeloDoWhatsApp.DocumentoPronto, "pt_BR", "APPROVED",
            "Olá, {{1}}! O seu {{2}} está pronto. Pode retirar na recepção ou responder aqui que enviamos por mensagem.",
            2, true, "", [], "UTILITY"),
        new(ModeloDoWhatsApp.PrimeiroContato, "pt_BR", "APPROVED",
            "Olá, {{1}}! Aqui é a recepção da Clínica SemDor. Você pediu contato sobre {{2}}. Pode falar por aqui?",
            2, true, "", [], "UTILITY"),
        new("recall_semdor", "pt_BR", "APPROVED",
            "Olá, {{1}}! Aqui é da Clínica SemDor. Quer conversar sobre um novo agendamento? Para suspender os convites, responda PARAR.",
            1, true, "", [], "MARKETING"),
        new("semdor_confirmacao_24h", "pt_BR", "APPROVED",
            "Olá, {{1}}! Sua consulta na Clínica SemDor está agendada para {{2}}. Confirme sua presença ou escolha uma das opções abaixo.",
            2, true, "", ["Confirmar", "Remarcar", "Cancelar"], "UTILITY"),
    ];

    /// <summary>As respostas rápidas (as do mockup, assinadas pela Ana da prévia).</summary>
    public static readonly AppQuickReply[] RespostasRapidas =
    [
        new("qr-bom-dia", "Bom dia", "Bom dia! Aqui é a Ana, da Clínica SemDor. Em que posso ajudar?"),
        new("qr-endereco", "Endereço", "Estamos na Av. Atlântica, 1.200, sala 304, Macaé. Tem estacionamento no prédio."),
        new("qr-valor-acupuntura", "Valor da acupuntura",
            "A sessão avulsa de acupuntura é R$ 180,00 e o pacote de 10 sessões sai R$ 1.500,00. Atendemos Unimed e Bradesco Saúde."),
        new("qr-primeira-consulta", "Documentos da 1ª consulta",
            "Para a primeira consulta, traga documento com foto, carteirinha do convênio e os exames recentes, se tiver."),
        new("qr-horario", "Horário de atendimento", "Atendemos de segunda a sexta, das 8h às 18h, e aos sábados das 8h às 12h."),
    ];

    /// <summary>O teto de anexo do CRM (MediaStore.Limit).</summary>
    public const int TetoDoAnexo = ClienteDoCrmHttp.TetoDoAnexo;

    private static readonly TimeSpan ValidadeDoEndereco = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory? escopos;
    private readonly object trava = new();
    private readonly List<Conversa> conversas;
    private readonly Dictionary<(int Usuario, string Conversa), int> lidas = new();
    private readonly Dictionary<string, Anexo> anexos = new();
    private readonly HashSet<string> emVoo = new();
    private readonly SemaphoreSlim leituraDoBanco = new(1, 1);
    private bool fichasProcuradas;
    private (bool Configurado, DateTimeOffset LidoEm)? endereco;

    /// <param name="escopos">Para achar as fichas e ler o endereço do CRM; nulo = sem banco (conversas sem ficha, sempre configurado).</param>
    public CrmDeMentira(IServiceScopeFactory? escopos)
    {
        this.escopos = escopos;
        Modo = (Environment.GetEnvironmentVariable(VariavelDoModo) ?? "").Trim().ToLowerInvariant() switch
        {
            "fora-do-ar" or "fora" => ModoDoCrm.ForaDoAr,
            "nao-configurado" or "sem-configuracao" => ModoDoCrm.NaoConfigurado,
            _ => ModoDoCrm.Normal
        };
        SemDor = (Environment.GetEnvironmentVariable(VariavelDoModo) ?? "").Trim().ToLowerInvariant() switch
        {
            "semdor-desligado" => ModoDoSemDor.Desligado,
            "semdor-falhando" => ModoDoSemDor.Falhando,
            _ => ModoDoSemDor.Ligado
        };
        conversas = Semear();
        // Os anexos que a semente já "enviou" (o PDF das orientações): servidos como os que a tela sobe.
        foreach (var a in conversas.SelectMany(c => c.AnexosDaSemente)) anexos[a.Id] = a;
    }

    /// <summary>Troca o <see cref="IClienteDoCrm"/> do <c>AddClinica</c> por este (prévia e <c>TelasBlazorAbremTests</c>).</summary>
    public static void Registrar(IServiceCollection servicos)
    {
        foreach (var d in servicos.Where(d => d.ServiceType == typeof(IClienteDoCrm)).ToList()) servicos.Remove(d);
        servicos.AddSingleton<IClienteDoCrm>(sp => new CrmDeMentira(sp.GetRequiredService<IServiceScopeFactory>()));
    }

    // ==================================================================== leituras

    public async Task<bool> ConfiguradoAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Modo switch
        {
            ModoDoCrm.NaoConfigurado => false,
            ModoDoCrm.ForaDoAr => true,
            _ => await EnderecoConfiguradoAsync(forcar: true, ct)
        };
    }

    public async Task<AppSession> SessaoAsync(int usuarioId, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        return new AppSession(Eu, "csrf-previa", Agentes, [new AppInbox(RemetenteWhatsApp, "WhatsApp da clínica")], Filas,
            AttachmentsEnabled: true, SendEnabled: true, WhatsappOff: false, Sms: new AppSms(true, "generic"), RecordingEnabled: true);
    }

    public async Task<AppConversationSummary[]> ListarConversasAsync(int usuarioId, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        lock (trava)
        {
            AvancarEnvios(null);
            return conversas.OrderByDescending(c => c.UltimaAtividade).Select(c => Linha(c, usuarioId)).ToArray();
        }
    }

    public async Task<AppConversationsSince> ListarDesdeAsync(int usuarioId, DateTimeOffset desde, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        lock (trava)
        {
            AvancarEnvios(null);
            // Como o CRM: o "agora" é lido ANTES de filtrar — o que mudar depois volta na próxima.
            var agora = DateTimeOffset.UtcNow;
            var mudaram = conversas.Where(c => c.UpdatedAt > desde - MargemDoDesde).OrderByDescending(c => c.UltimaAtividade).ToList();
            return new AppConversationsSince(mudaram.Select(c => Linha(c, usuarioId)).ToArray(),
                mudaram.Where(c => c.Closed).Select(c => c.Id).ToArray(), agora);
        }
    }

    public async Task<AppConversation> AbrirConversaAsync(int usuarioId, string conversaId, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        lock (trava)
        {
            var c = Achar(conversaId);
            AvancarEnvios(c);
            return Foto(c);
        }
    }

    public async Task<AppTemplate[]> ModelosAsync(int usuarioId, string remetente, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        if (remetente == RemetenteSms) return [];
        if (remetente != RemetenteWhatsApp) throw new RecusaDoCrm("Número não configurado.", 400);
        return Modelos.ToArray();
    }

    public async Task<AppWorkspace> AreaDeTrabalhoAsync(int usuarioId, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        return new AppWorkspace(RespostasRapidas.ToArray(), new Dictionary<string, string>(Resultados));
    }

    public void EsquecerSessao(int usuarioId) { }

    /// <summary>Os pulsos de presença que chegaram (25/10): o teste confere que o vigia pulsa e que para.</summary>
    public int Pulsos { get; private set; }

    /// <summary>Como o <c>ReceptionSessions.Touch</c> do CRM: conectado por 120 s.</summary>
    public async Task<AppPresence> PulsoDePresencaAsync(int usuarioId, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        lock (trava) Pulsos++;
        return new AppPresence(true, 120);
    }

    /// <summary>
    /// O paciente escreve (25/10, para os testes da espera e do turno): a mensagem entra como recebida, a
    /// conversa encerrada reabre sem dono — como o CRM faz com a mensagem nova.
    /// </summary>
    public void ChegouDoPaciente(string conversaId, string texto, DateTimeOffset? quando = null)
    {
        lock (trava)
        {
            var c = Achar(conversaId);
            var at = quando ?? DateTimeOffset.UtcNow;
            if (c.Closed) c.Reaberta(at);
            c.Recebida(at, texto);
            Gravar(c, at);
        }
    }

    // ==================================================================== o @SemDor (as frases e os 409/502 do Program.cs do CRM)

    public async Task<AppSemDorState> EstadoDoSemDorAsync(int usuarioId, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        // A "ana" da prévia é supervisora: o motivo da falha vem inteiro (para quem não é da gestão, o CRM manda "Fale com a gestão.").
        return SemDor switch
        {
            ModoDoSemDor.Desligado => new AppSemDorState(false, false, null, null, ""),
            ModoDoSemDor.Falhando => new AppSemDorState(true, false, DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddMinutes(-3),
                "a conta da Anthropic está sem crédito."),
            _ => new AppSemDorState(true, true, DateTimeOffset.UtcNow.AddMinutes(-1), null, ""),
        };
    }

    public async Task<AppSemDorSuggestion> SugerirComSemDorAsync(int usuarioId, string conversaId, AppSemDorRequest pedido, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        ConferirSemDor();
        var rascunho = pedido.Rascunho?.Trim();
        if (rascunho is { Length: > 4096 }) throw new RecusaDoCrm("O texto passou de 4.096 caracteres.", 400);
        lock (trava)
        {
            var c = Achar(conversaId);
            if (string.IsNullOrEmpty(rascunho))
            {
                if (c.Recebidas == 0) throw new RecusaDoCrm("O paciente ainda não escreveu nada para o @SemDor responder.", 409);
                var nome = PrimeiroNome(c.PatientName.Length > 0 ? c.PatientName : c.Name);
                return new AppSemDorSuggestion(c.Id == Ids.Maria
                    ? $"Combinado, {nome}! Vou ver esse horário na agenda com a Dra. Helena e já te confirmo por aqui, tudo bem?"
                    : $"Olá{(nome.Length > 0 ? ", " + nome : "")}! Obrigada pela mensagem. Vou verificar e já te retorno com as opções, tudo bem?", "sugestao");
            }
            return new AppSemDorSuggestion(Revisar(rascunho), "revisao");
        }
    }

    public async Task<AppSemDorSummary> ResumirComSemDorAsync(int usuarioId, string conversaId, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        ConferirSemDor();
        lock (trava)
        {
            var c = Achar(conversaId);
            if (c.Messages.Count == 0) throw new RecusaDoCrm("Ainda não há conversa para resumir.", 409);
            if (c.Id == Ids.Maria)
                return new AppSemDorSummary("Remarcar a sessão de hoje: surgiu um compromisso no trabalho. Prefere à tarde.",
                    "A Ana ofereceu dois horários com a Dra. Helena; a paciente escolheu o primeiro.",
                    "Remarcar na agenda e confirmar o novo horário com ela.");
            var ultima = c.Messages.LastOrDefault(m => m.Direction == "in")?.Text;
            return new AppSemDorSummary(string.IsNullOrWhiteSpace(ultima) ? "Ainda não escreveu." : ultima.Trim(),
                c.Messages.Any(m => m.Direction == "out") ? "A clínica já respondeu." : "Ninguém respondeu ainda.",
                "Responder ao paciente.");
        }
    }

    /// <summary>Desligado é 409 (RecusaDoCrm); sem resposta é o 502 do CRM, que o cliente HTTP entrega como CrmIndisponivel com a frase.</summary>
    private void ConferirSemDor()
    {
        switch (SemDor)
        {
            case ModoDoSemDor.Desligado: throw new RecusaDoCrm(SemDorDesligado, 409);
            case ModoDoSemDor.Falhando: throw new CrmIndisponivel(SemDorSemResposta);
        }
    }

    private static string PrimeiroNome(string nome)
    {
        var primeiro = nome.Trim().Split(' ')[0];
        return primeiro.Length >= 2 && primeiro.All(char.IsLetter) ? primeiro : "";
    }

    /// <summary>A "revisão" de mentira: abreviações por extenso, a primeira letra maiúscula e o ponto final. O sentido não muda.</summary>
    private static string Revisar(string texto)
    {
        var trocas = new (string De, string Para)[] { ("vc", "você"), ("pra", "para"), ("tb", "também"), ("tbm", "também"), ("hj", "hoje"),
            ("amanha", "amanhã"), ("obg", "obrigada"), ("q", "que"), ("blz", "beleza") };
        // "as 14h" é hora (crase); "as consultas" não é.
        texto = System.Text.RegularExpressions.Regex.Replace(texto, @"\bas (?=\d)", "às ");
        var palavras = texto.Split(' ').Select(p =>
        {
            var miolo = p.TrimEnd(',', '.', '?', '!');
            var troca = trocas.FirstOrDefault(t => t.De == miolo.ToLowerInvariant());
            return troca.Para is null ? p : troca.Para + p[miolo.Length..];
        });
        var r = string.Join(' ', palavras).Trim();
        r = char.ToUpper(r[0], CultureInfo.GetCultureInfo("pt-BR")) + r[1..];
        return r.EndsWith('.') || r.EndsWith('?') || r.EndsWith('!') ? r : r + ".";
    }

    // ==================================================================== escritas

    public async Task<AppConversation> NovaConversaAsync(int usuarioId, AppNewConversation pedido, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        var digitos = new string((pedido.Contact ?? "").Where(char.IsAsciiDigit).ToArray());
        if (digitos.Length is 10 or 11) digitos = "55" + digitos;
        var remetenteConhecido = pedido.Sender is RemetenteWhatsApp or RemetenteSms;
        if (!remetenteConhecido || string.IsNullOrWhiteSpace(pedido.Name) || pedido.Name.Length > 120 || digitos.Length is < 10 or > 15)
            throw new RecusaDoCrm("Preencha nome, telefone com DDI e número de origem válido.", 400);
        if (pedido.Sender == RemetenteSms && !(digitos.Length == 13 && digitos[4] == '9'))
            throw new RecusaDoCrm("SMS só chega a celular. Confira o número (DDI, DDD e 9 dígitos).", 400);
        lock (trava)
        {
            var existente = conversas.FirstOrDefault(c => c.Sender == pedido.Sender && c.Contact == digitos);
            if (existente is not null) return Foto(existente);
            var c = new Conversa(Guid.NewGuid().ToString("D"), pedido.Name.Trim(), digitos, pedido.Sender) { AgentId = Eu };
            var agora = DateTimeOffset.UtcNow;
            c.Audit.Add(new(agora, Nome(Eu), pedido.Sender == RemetenteSms ? "Criou um atendimento por SMS."
                : "Criou um novo atendimento. Aguardando envio de modelo ou mensagem do contato."));
            Gravar(c, agora);
            conversas.Add(c);
            return Foto(c);
        }
    }

    public async Task<AppConversation> ResponderAsync(int usuarioId, string conversaId, AppReply resposta, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        lock (trava)
        {
            var c = Achar(conversaId);
            var texto = resposta.Text;
            if (!Guid.TryParseExact(resposta.Id, "D", out _) || texto is null || texto.Length > 4096
                || string.IsNullOrWhiteSpace(texto) && resposta.Template is null && resposta.AttachmentId is null)
                throw new RecusaDoCrm("Mensagem inválida (limite de 4.096 caracteres).", 400);
            var sms = c.Sender == RemetenteSms;
            if (sms && (resposta.Template is not null || resposta.AttachmentId is not null || resposta.ReplyToId is not null))
                throw new RecusaDoCrm("SMS envia somente texto: sem modelos, anexos, botões ou citações.", 400);

            var mensagem = Preparar(c, resposta, sms);
            // O mesmo Id de novo é a mesma resposta (a resposta HTTP se perdeu): devolve, não duplica.
            var anterior = c.Messages.FirstOrDefault(m => m.Direction == "out" && m.Id == resposta.Id);
            if (anterior is not null)
            {
                if (anterior.Text != mensagem.Text || anterior.AttachmentId != mensagem.AttachmentId || anterior.Template?.Name != mensagem.Template?.Name)
                    throw new RecusaDoCrm("Identificador de envio já utilizado.", 409);
                return Foto(c);
            }
            if (conversas.Any(o => o.Id != c.Id && o.Messages.Any(m => m.Id == resposta.Id)))
                throw new RecusaDoCrm("Identificador já utilizado em outra conversa.", 409);
            Conferir(c, resposta.Version);
            var agora = DateTimeOffset.UtcNow;
            // No CRM as recusas abaixo desfazem a transação; aqui elas vêm ANTES de mexer na conversa.
            if (!c.Closed && c.AgentId is not null && c.AgentId != Eu)
                throw new RecusaDoCrm($"Esta conversa está com {Nome(c.AgentId)}. Peça a transferência para escrever.", 409);
            if (resposta.Template is null && !sms && agora - c.LastIncoming >= ConversasNaTela.DuracaoDaJanela)
                throw new RecusaDoCrm("A janela de 24 horas terminou. Use um modelo aprovado do WhatsApp ou continue por SMS.", 409);
            AssumirAoEscrever(c, agora);
            c.Messages.Add(mensagem with { At = agora, AgentId = Eu, Status = "sending" });
            emVoo.Add(mensagem.Id);
            Gravar(c, agora);
            return Foto(c);
        }
    }

    public async Task<AppConversation> AnotarAsync(int usuarioId, string conversaId, AppNoteRequest nota, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        lock (trava)
        {
            var c = Achar(conversaId);
            if (!Guid.TryParseExact(nota.Id, "D", out _) || string.IsNullOrWhiteSpace(nota.Text) || nota.Text.Length > 4000)
                throw new RecusaDoCrm("Preencha uma nota de até 4.000 caracteres.", 400);
            var anterior = c.Notes.FirstOrDefault(n => n.Id == nota.Id);
            if (anterior is not null)
            {
                if (anterior.AgentId != Eu || anterior.Text != nota.Text) throw new RecusaDoCrm("Identificador de nota já utilizado.", 409);
                return Foto(c);
            }
            Conferir(c, nota.Version);
            Dono(c);
            var agora = DateTimeOffset.UtcNow;
            c.Notes.Add(new(nota.Id, nota.Text, Eu, agora));
            c.Audit.Add(new(agora, Nome(Eu), "Adicionou uma nota interna"));
            Gravar(c, agora);
            return Foto(c);
        }
    }

    public async Task MarcarLidaAsync(int usuarioId, string conversaId, AppReadMarker marcador, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        lock (trava)
        {
            var c = Achar(conversaId);
            if (marcador.IncomingCount < 0 || marcador.IncomingCount > c.Recebidas)
                throw new RecusaDoCrm("Contagem inválida.", 400);
            // Como o read_markers do CRM: só sobe (MAX) e não carimba UpdatedAt.
            lidas[(usuarioId, c.Id)] = Math.Max(Lidas(c, usuarioId), marcador.IncomingCount);
        }
    }

    public async Task<AppConversation> AssumirAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        lock (trava)
        {
            var c = Achar(conversaId);
            Conferir(c, mudanca.Version);
            if (c.Closed || c.AgentId is not null) throw new RecusaDoCrm("Esta conversa já tem responsável ou está encerrada.", 409);
            c.AgentId = Eu;
            var agora = DateTimeOffset.UtcNow;
            c.Audit.Add(new(agora, Nome(Eu), "Assumiu o atendimento humano; respostas automáticas suspensas"));
            Gravar(c, agora);
            return Foto(c);
        }
    }

    public async Task<AppConversation> TransferirAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        lock (trava)
        {
            var c = Achar(conversaId);
            Conferir(c, mudanca.Version);
            Dono(c);
            if (mudanca.Queue is null || !Filas.Contains(mudanca.Queue)) throw new RecusaDoCrm("Escolha uma fila válida.", 400);
            if (mudanca.AgentId is not null && Agentes.All(a => a.Id != mudanca.AgentId)) throw new RecusaDoCrm("Atendente inválido.", 400);
            var contexto = (mudanca.Note ?? "").Trim();
            if (contexto.Length > 2000) throw new RecusaDoCrm("O contexto da transferência deve ter até 2.000 caracteres.", 400);
            var agora = DateTimeOffset.UtcNow;
            if (contexto.Length > 0) c.Notes.Add(new(Guid.NewGuid().ToString("D"), "Transferência: " + contexto, Eu, agora));
            var filaAnterior = c.Queue;
            c.AgentId = mudanca.AgentId;
            c.Queue = mudanca.Queue;
            c.Audit.Add(new(agora, Nome(Eu), $"Transferiu de {filaAnterior} / {Nome(Eu)} para {c.Queue} / {Pessoa(c.AgentId)}"));
            Gravar(c, agora);
            return Foto(c);
        }
    }

    public async Task<AppConversation> ReabrirAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        lock (trava)
        {
            var c = Achar(conversaId);
            Conferir(c, mudanca.Version);
            if (!c.Closed) throw new RecusaDoCrm("Esta conversa já está aberta.", 409);
            c.Closed = false;
            c.ClosedAt = null;
            c.AgentId = Eu;
            var agora = DateTimeOffset.UtcNow;
            c.Audit.Add(new(agora, Nome(Eu), "Reabriu e assumiu o atendimento humano"));
            Gravar(c, agora);
            return Foto(c);
        }
    }

    public async Task<AppConversation> DevolverAFilaAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        lock (trava)
        {
            // O Reassign do CRM (ReceptionHandoffs.cs): só a gestão — a "ana" da prévia é supervisora.
            var c = Achar(conversaId);
            Conferir(c, mudanca.Version);
            if (c.Closed || c.AgentId is null) throw new RecusaDoCrm("O atendimento já está encerrado ou sem responsável. Atualize a conversa.", 409);
            if (mudanca.Queue is null || !Filas.Contains(mudanca.Queue)) throw new RecusaDoCrm("Escolha uma fila válida.", 400);
            if (mudanca.AgentId is not null && Agentes.All(a => a.Id != mudanca.AgentId))
                throw new RecusaDoCrm("O atendente não está habilitado na fila de destino.", 400);
            if (mudanca.AgentId == c.AgentId && mudanca.Queue == c.Queue) throw new RecusaDoCrm("Escolha outro responsável ou outra fila.", 409);
            var contexto = (mudanca.Note ?? "").Trim();
            if (contexto.Length is < 5 or > 2000) throw new RecusaDoCrm("Registre o motivo e o próximo passo em 5 a 2.000 caracteres.", 400);
            var agora = DateTimeOffset.UtcNow;
            c.Notes.Add(new(Guid.NewGuid().ToString("D"), "Reorganização: " + contexto, Eu, agora));
            var antes = $"{c.Queue} / {Pessoa(c.AgentId)}";
            c.AgentId = mudanca.AgentId;
            c.Queue = mudanca.Queue;
            c.Audit.Add(new(agora, Nome(Eu), $"Reorganizou atendimento de {antes} para {c.Queue} / {Pessoa(c.AgentId)}"));
            Gravar(c, agora);
            return Foto(c);
        }
    }

    public async Task<AppConversation> EncerrarAsync(int usuarioId, string conversaId, AppClose encerramento, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        lock (trava)
        {
            var c = Achar(conversaId);
            Conferir(c, encerramento.Version);
            Dono(c);
            if (!Resultados.TryGetValue(encerramento.Outcome ?? "", out var rotulo)) throw new RecusaDoCrm("Selecione o resultado do atendimento.", 400);
            var resumo = encerramento.Summary?.Trim() ?? "";
            if (resumo.Length is < 5 or > 1500) throw new RecusaDoCrm("Descreva o resultado em 5 a 1.500 caracteres.", 400);
            var agora = DateTimeOffset.UtcNow;
            var retorno = encerramento.Outcome == "followup";
            if (retorno && ((encerramento.NextStep?.Trim().Length ?? 0) is < 5 or > 200
                            || encerramento.DueAt is not { } prazo || prazo <= agora || prazo > agora.AddYears(1)))
                throw new RecusaDoCrm("Informe o próximo passo e um prazo futuro de até um ano.", 400);
            c.Resolutions.Add(new(encerramento.Outcome!, resumo, Eu, agora));
            c.Closed = true;
            c.ClosedAt = agora;
            c.Audit.Add(new(agora, Nome(Eu), "Concluiu: " + rotulo + (retorno ? " · retorno registrado" : "")));
            Gravar(c, agora);
            return Foto(c);
        }
    }

    public async Task<AppConversation> LigarFichaAsync(int usuarioId, string conversaId, AppPatientLink ficha, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        lock (trava)
        {
            var c = Achar(conversaId);
            if (ficha.Version is { } v) Conferir(c, v);
            if (c.PatientId == ficha.PatientId) return Foto(c);
            var agora = DateTimeOffset.UtcNow;
            var nome = (ficha.Name ?? "").Trim();
            c.Audit.Add(new(agora, Nome(Eu), ficha.PatientId is null
                ? $"Desligou a ficha {c.PatientName} desta conversa."
                : $"Identificou o paciente: ficha {nome} (nº {ficha.PatientId})."));
            c.PatientId = ficha.PatientId;
            c.PatientName = ficha.PatientId is null ? "" : nome;
            Gravar(c, agora);
            return Foto(c);
        }
    }

    /// <summary>
    /// Como o CRM: sobe o arquivo e devolve o anexo. ⚠️ A gravação (<paramref name="gravacao"/>) o CRM de
    /// verdade converte para OGG/Opus com o ffmpeg; aqui ela fica no formato em que chegou (o WebM do
    /// Chromium toca na prévia), com o nome "gravacao" e o tipo AUDIO — e o que não é áudio é recusado
    /// com a mesma frase.
    /// </summary>
    public async Task<AppAttachment> AnexarAsync(int usuarioId, string conversaId, Stream arquivo, string nome, string mime, bool gravacao = false,
        CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        using var memoria = new MemoryStream();
        var bloco = new byte[81920];
        int lidos;
        while ((lidos = await arquivo.ReadAsync(bloco, ct)) > 0)
        {
            if (memoria.Length + lidos > TetoDoAnexo) throw new RecusaDoCrm("Selecione um arquivo de até 16 MB.", 413);
            memoria.Write(bloco, 0, lidos);
        }
        if (memoria.Length == 0) throw new RecusaDoCrm("Selecione um arquivo.", 400);
        lock (trava)
        {
            var c = Achar(conversaId);
            if (c.Sender == RemetenteSms) throw new RecusaDoCrm("SMS envia somente texto: sem modelos, anexos, botões ou citações.", 400);
            if (!c.Closed && c.AgentId is { } dono && dono != Eu)
                throw new RecusaDoCrm("Esta conversa está com outra pessoa. Peça a transferência para anexar.", 409);
            var tipo = string.IsNullOrWhiteSpace(mime) ? "application/octet-stream" : mime.Trim();
            if (gravacao && TipoDoAnexo(tipo) != "AUDIO") throw new RecusaDoCrm("A gravação precisa conter áudio.", 400);
            var nomeFinal = gravacao ? MidiaDaConversa.NomeDaGravacao(tipo) : string.IsNullOrWhiteSpace(nome) ? "arquivo" : nome.Trim();
            var anexo = new Anexo(Guid.NewGuid().ToString("N"), c.Id, nomeFinal, tipo, memoria.ToArray());
            anexos[anexo.Id] = anexo;
            return new AppAttachment(anexo.Id, c.Id, anexo.Nome, anexo.Mime, TipoDoAnexo(anexo.Mime), anexo.Bytes.Length);
        }
    }

    /// <summary>
    /// O arquivo de uma mensagem, pelas mesmas regras do CRM: a ENVIADA pelo anexo (da conversa), a
    /// RECEBIDA pela mensagem com Direction "in". A recebida sem arquivo (o vídeo da semente) volta com
    /// a frase do CRM para mídia que o provedor não entrega — é a foto do "Tentar de novo".
    /// </summary>
    public async Task<ArquivoDaMidia> MidiaAsync(int usuarioId, string conversaId, AppMessage mensagem, CancellationToken ct = default)
    {
        await EntrarAsync(ct);
        lock (trava)
        {
            var c = Achar(conversaId);
            if (MidiaDaConversa.Rota(conversaId, mensagem) is null) throw new RecusaDoCrm(MidiaDaConversa.SemArquivo, 404);
            if (!string.IsNullOrWhiteSpace(mensagem.AttachmentId))
            {
                if (!anexos.TryGetValue(mensagem.AttachmentId, out var a) || a.ConversaId != c.Id) throw new RecusaDoCrm("Arquivo não encontrado.", 404);
                return new ArquivoDaMidia(a.Bytes.ToArray(), a.Mime, a.Nome);
            }
            var m = c.Messages.FirstOrDefault(x => x.Id == mensagem.Id && x.Direction == "in") ?? throw new RecusaDoCrm("Mensagem não encontrada.", 404);
            if (!c.Midias.TryGetValue(m.Id, out var arquivo))
                throw new RecusaDoCrm("Este anexo não possui um endereço de mídia compatível com o provedor do WhatsApp.", 409);
            return arquivo with { Conteudo = arquivo.Conteudo.ToArray() };
        }
    }

    /// <summary>Os bytes de um anexo enviado (para o teste conferir que nada se perdeu).</summary>
    public byte[]? BytesDoAnexo(string id)
    {
        lock (trava) return anexos.TryGetValue(id, out var a) ? a.Bytes.ToArray() : null;
    }

    // ==================================================================== regras do CRM

    private static void Conferir(Conversa c, int versao)
    {
        if (c.Version != versao) throw new RecusaDoCrm("A conversa mudou. Atualize e tente novamente.", 409);
    }

    private static void Dono(Conversa c)
    {
        if (c.Closed || c.AgentId != Eu) throw new RecusaDoCrm("Assuma uma conversa aberta antes de alterar ou responder.", 409);
    }

    /// <summary>Escrever é assumir (TakeForSending do CRM): sem dono assume, encerrada reabre. A de outra pessoa já foi recusada antes.</summary>
    private static void AssumirAoEscrever(Conversa c, DateTimeOffset agora)
    {
        if (!c.Closed && c.AgentId == Eu) return;
        var reabriu = c.Closed;
        c.Closed = false;
        c.ClosedAt = null;
        c.AgentId = Eu;
        c.Audit.Add(new(agora, Nome(Eu), reabriu ? "Reabriu ao escrever para o paciente" : "Assumiu ao escrever para o paciente"));
    }

    /// <summary>A mensagem que vai sair: texto (o SMS sem acento), modelo preenchido ou anexo.</summary>
    private AppMessage Preparar(Conversa c, AppReply r, bool sms)
    {
        if (r.Template is { } escolha)
        {
            var modelo = Modelos.FirstOrDefault(m => m.Name == escolha.Name && m.Language == escolha.Language)
                         ?? throw new RecusaDoCrm("Modelo não aprovado para este número.", 400);
            var valores = escolha.Parameters ?? [];
            if (valores.Length != modelo.Parameters || valores.Any(string.IsNullOrWhiteSpace))
                throw new RecusaDoCrm("Preencha todas as variáveis do modelo.", 400);
            return new AppMessage(r.Id, "out", ModeloDoWhatsApp.Previa(modelo, valores), "TEMPLATE", Eu, default, "sending",
                Template: escolha, ReplyToId: r.ReplyToId);
        }
        if (r.AttachmentId is { } anexoId)
        {
            if (!anexos.TryGetValue(anexoId, out var anexo) || anexo.ConversaId != c.Id) throw new RecusaDoCrm("Anexo não encontrado. Envie o arquivo de novo.", 400);
            return new AppMessage(r.Id, "out", r.Text ?? "", TipoDoAnexo(anexo.Mime), Eu, default, "sending",
                AttachmentId: anexo.Id, FileName: anexo.Nome, ReplyToId: r.ReplyToId);
        }
        return new AppMessage(r.Id, "out", sms ? RelogioDaPrevia.SemAcento(r.Text) : r.Text, "TEXT", Eu, default, "sending", ReplyToId: r.ReplyToId);
    }

    private static string TipoDoAnexo(string mime) => mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? "IMAGE"
        : mime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ? "AUDIO"
        : mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ? "VIDEO"
        : "DOCUMENT";

    /// <summary>
    /// O recibo do provedor, sem timer: cada leitura leva o envio em voo um passo adiante, com
    /// Version e UpdatedAt novos (como o <c>Receipts</c> do CRM grava). <paramref name="so"/>
    /// limita à conversa aberta.
    /// </summary>
    private void AvancarEnvios(Conversa? so)
    {
        if (emVoo.Count == 0) return;
        var agora = DateTimeOffset.UtcNow;
        foreach (var c in so is null ? conversas : new List<Conversa> { so })
        {
            var mudou = false;
            for (var i = 0; i < c.Messages.Count; i++)
            {
                var m = c.Messages[i];
                if (!emVoo.Contains(m.Id)) continue;
                var proximo = m.Status switch { "sending" => "accepted", "accepted" => "delivered", "delivered" => "seen", _ => null };
                if (proximo is null) continue;
                c.Messages[i] = m with { Status = proximo };
                if (proximo == "seen") emVoo.Remove(m.Id);
                mudou = true;
            }
            if (mudou) Gravar(c, agora);
        }
    }

    private static void Gravar(Conversa c, DateTimeOffset agora)
    {
        c.Version++;
        c.UpdatedAt = agora;
    }

    private Conversa Achar(string id)
        => conversas.FirstOrDefault(c => c.Id == id) ?? throw new RecusaDoCrm("Conversa não encontrada.", 404);

    private int Lidas(Conversa c, int usuarioId)
        => lidas.TryGetValue((usuarioId, c.Id), out var n) ? n : Math.Max(0, c.Recebidas - c.NaoLidasIniciais);

    private static string Nome(string? agente) => Agentes.FirstOrDefault(a => a.Id == agente)?.Name ?? agente ?? "";

    private static string Pessoa(string? agente) => agente is null ? "fila sem responsável" : Nome(agente);

    // ==================================================================== entrada, falhas e fichas

    /// <summary>O que toda chamada passa antes de responder: o modo de falha, o endereço e as fichas.</summary>
    private async Task EntrarAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        switch (Modo)
        {
            case ModoDoCrm.ForaDoAr: throw new CrmIndisponivel(ClienteDoCrmHttp.ForaDoAr);
            case ModoDoCrm.NaoConfigurado: throw new CrmNaoConfigurado();
        }
        if (!await EnderecoConfiguradoAsync(forcar: false, ct)) throw new CrmNaoConfigurado();
        await ProcurarFichasAsync(ct);
    }

    /// <summary>O endereço do CRM nas Configurações (relido a cada 30 s, como o cliente de verdade).</summary>
    private async Task<bool> EnderecoConfiguradoAsync(bool forcar, CancellationToken ct)
    {
        if (escopos is null) return true;
        lock (trava)
            if (!forcar && endereco is { } lido && DateTimeOffset.UtcNow - lido.LidoEm < ValidadeDoEndereco) return lido.Configurado;
        await leituraDoBanco.WaitAsync(ct);
        try
        {
            await using var escopo = escopos.CreateAsyncScope();
            var valor = await escopo.ServiceProvider.GetRequiredService<ParametrosService>().ObterEnderecoDoCrmAsync(ct);
            lock (trava) endereco = (valor is not null, DateTimeOffset.UtcNow);
            return valor is not null;
        }
        finally { leituraDoBanco.Release(); }
    }

    /// <summary>
    /// Liga cada conversa conferida à ficha que a semente criou: pelo NOME, conferindo o TELEFONE
    /// (ver o resumo da classe). Uma vez só; sem banco, ou sem a ficha, a conversa fica sem ficha.
    /// </summary>
    private async Task ProcurarFichasAsync(CancellationToken ct)
    {
        if (escopos is null) return;
        lock (trava) if (fichasProcuradas) return;
        await leituraDoBanco.WaitAsync(ct);
        try
        {
            lock (trava) if (fichasProcuradas) return;
            List<Conversa> aLigar;
            lock (trava) aLigar = conversas.Where(c => c.Ficha is not null).ToList();
            var nomes = aLigar.Select(c => c.Ficha!.Nome).Distinct().ToList();
            await using var escopo = escopos.CreateAsyncScope();
            var db = escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            var fichas = await db.Pacientes.AsNoTracking().Where(p => nomes.Contains(p.Nome))
                .Select(p => new { p.Id, p.Nome, p.Telefone }).ToListAsync(ct);
            lock (trava)
            {
                foreach (var c in aLigar)
                {
                    var achada = fichas.Where(p => p.Nome == c.Ficha!.Nome && ConversasNaTela.MesmoTelefone(p.Telefone, c.Ficha.Telefone))
                        .OrderBy(p => p.Id).FirstOrDefault();
                    if (achada is null) continue;
                    c.PatientId = achada.Id;
                    c.PatientName = achada.Nome;
                    // A conversa que a clínica começou mostra quando a ficha foi ligada (com o número de verdade).
                    if (c.FichaLigadaEm is { } quando)
                        c.Audit.Add(new(quando, c.FichaLigadaPor, $"Identificou o paciente: ficha {achada.Nome} (nº {achada.Id})."));
                }
                fichasProcuradas = true;
            }
        }
        finally { leituraDoBanco.Release(); }
    }

    // ==================================================================== as fotos devolvidas

    private AppConversation Foto(Conversa c) => new(c.Id, c.Name, c.Contact, c.Channel, c.Sender, c.Queue, c.AgentId, c.Closed,
        c.Version, c.UpdatedAt, c.LastIncoming, c.PatientId, c.PatientName, c.Messages.ToArray(), c.Notes.ToArray(),
        c.Audit.OrderBy(a => a.At).ToArray(), c.Resolutions.ToArray(), c.ClosedAt);

    private AppConversationSummary Linha(Conversa c, int usuarioId)
    {
        var ultima = c.Messages.LastOrDefault();
        var ultimaSaida = c.Messages.LastOrDefault(m => m.Direction == "out");
        return new AppConversationSummary(c.Id, c.Name, c.Contact, c.Channel, c.Sender, c.Queue, c.AgentId, c.Closed, c.Version,
            c.UpdatedAt, c.LastIncoming, c.PatientId, c.PatientName, Math.Max(0, c.Recebidas - Lidas(c, usuarioId)),
            ultima is null ? "Nova conversa" : ultima.Text.Length > 0 ? ultima.Text : ultima.FileName ?? ultima.Type,
            AwaitingMenu: false, DeliveryAttention: ultimaSaida?.Status is "failed" or "uncertain",
            Resolution: c.Closed ? c.Resolutions.LastOrDefault() : null, c.ClosedAt,
            PreviewDirection: ultima?.Direction ?? "in", PreviewStatus: ultima?.Status ?? "",
            // A regra do CRM (Program.cs): a etiqueta só vale enquanto o paciente espera a clínica.
            Semdor: SemDor != ModoDoSemDor.Desligado && !c.Closed && ultima?.Direction == "in" ? Etiquetas.GetValueOrDefault(c.Id) : null);
    }

    private sealed record Anexo(string Id, string ConversaId, string Nome, string Mime, byte[] Bytes);

    /// <summary>Uma conversa como o CRM a guarda (mutável; só se mexe dentro da trava).</summary>
    private sealed class Conversa(string id, string nome, string contato, string remetente)
    {
        public string Id { get; } = id;
        public string Name { get; } = nome;
        public string Contact { get; } = contato;
        public string Sender { get; } = remetente;
        public string Channel => Sender == RemetenteSms ? "sms" : "whatsapp";
        public string Queue { get; set; } = "Recepção";
        public string? AgentId { get; set; }
        public bool Closed { get; set; }
        public DateTimeOffset? ClosedAt { get; set; }
        public int Version { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset LastIncoming { get; set; }
        public int? PatientId { get; set; }
        public string PatientName { get; set; } = "";
        public List<AppMessage> Messages { get; } = new();
        public List<AppNote> Notes { get; } = new();
        public List<AppAudit> Audit { get; } = new();
        public List<AppResolution> Resolutions { get; } = new();
        public int NaoLidasIniciais { get; set; }

        /// <summary>A ficha que a recepção já conferiu (procurada no banco na primeira chamada).</summary>
        public PessoasDoWhatsApp.Pessoa? Ficha { get; set; }
        public DateTimeOffset? FichaLigadaEm { get; set; }
        public string FichaLigadaPor { get; set; } = "";

        /// <summary>O arquivo de cada mensagem RECEBIDA com mídia (a que não está aqui, o "provedor" não entrega).</summary>
        public Dictionary<string, ArquivoDaMidia> Midias { get; } = new();

        /// <summary>Os anexos das mensagens que a semente "enviou" (vão para o dicionário de anexos do CRM).</summary>
        public List<Anexo> AnexosDaSemente { get; } = new();

        public int Recebidas => Messages.Count(m => m.Direction == "in");
        public DateTimeOffset UltimaAtividade => Messages.Count == 0 ? UpdatedAt : Messages.Max(m => m.At);

        private int seq;

        public Conversa Recebida(DateTimeOffset at, string texto)
        {
            Messages.Add(new AppMessage($"wamid.previa.{Id[^4..]}.{++seq}", "in", texto, "TEXT", null, at, "received"));
            if (at > LastIncoming) LastIncoming = at;
            return Tocar(at);
        }

        /// <summary>Uma recebida com mídia (Type do CRM: IMAGE, AUDIO, VIDEO, DOCUMENT); <paramref name="arquivo"/> nulo = o provedor não entrega.
        /// <paramref name="figurinha"/>: a figurinha do WhatsApp (IMAGE com AppMessage.Sticker).</summary>
        public Conversa RecebidaComMidia(DateTimeOffset at, string tipo, string legenda, ArquivoDaMidia? arquivo, bool figurinha = false)
        {
            var id = $"wamid.previa.{Id[^4..]}.{++seq}";
            Messages.Add(new AppMessage(id, "in", legenda, tipo, null, at, "received", FileName: arquivo?.Nome, Sticker: figurinha));
            if (arquivo is not null) Midias[id] = arquivo;
            if (at > LastIncoming) LastIncoming = at;
            return Tocar(at);
        }

        /// <summary>Uma enviada com anexo (como a tela manda: o anexo sobe antes, o envio leva o AttachmentId).</summary>
        public Conversa EnviadaComAnexo(DateTimeOffset at, string agente, string legenda, string tipo, ArquivoDaMidia arquivo, string status)
        {
            var anexo = new Anexo(Guid.NewGuid().ToString("N"), Id, arquivo.Nome ?? "arquivo", arquivo.Mime, arquivo.Conteudo);
            AnexosDaSemente.Add(anexo);
            Messages.Add(new AppMessage(Guid.NewGuid().ToString("D"), "out", legenda, tipo, agente, at, status, AttachmentId: anexo.Id, FileName: anexo.Nome));
            return Tocar(at);
        }

        public Conversa Enviada(DateTimeOffset at, string? agente, string texto, string status, AppTemplateSelection? modelo = null)
        {
            Messages.Add(new AppMessage(Guid.NewGuid().ToString("D"), "out", texto, modelo is null ? "TEXT" : "TEMPLATE", agente, at, status, Template: modelo));
            return Tocar(at);
        }

        public Conversa Nota(DateTimeOffset at, string agente, string texto)
        {
            Notes.Add(new AppNote(Guid.NewGuid().ToString("D"), texto, agente, at));
            return Tocar(at);
        }

        public Conversa Evento(DateTimeOffset at, string ator, string acao)
        {
            Audit.Add(new AppAudit(at, ator, acao));
            return Tocar(at);
        }

        public Conversa Encerrada(DateTimeOffset at, string agente, string resultado, string resumo)
        {
            Resolutions.Add(new AppResolution(resultado, resumo, agente, at));
            Audit.Add(new AppAudit(at, Nome(agente), "Concluiu: " + Resultados[resultado]));
            Closed = true;
            ClosedAt = at;
            return Tocar(at);
        }

        public Conversa Reaberta(DateTimeOffset at)
        {
            Closed = false;
            ClosedAt = null;
            AgentId = null;
            return Evento(at, "Sistema", "Reaberta após nova mensagem");
        }

        private Conversa Tocar(DateTimeOffset at)
        {
            Version++;
            if (at > UpdatedAt) UpdatedAt = at;
            return this;
        }
    }

    // ==================================================================== as conversas do mockup

    private static AppTemplateSelection Modelo(string nome, params string[] valores) => new(nome, "pt_BR", valores);

    private static string Texto(string nome, params string[] valores)
        => ModeloDoWhatsApp.Previa(Modelos.Single(m => m.Name == nome), valores);

    /// <summary>
    /// As nove conversas de <c>CONVERSAS</c> do mockup, nas horas do <see cref="RelogioDaPrevia"/>.
    /// Os horários que as mensagens citam são OS DA AGENDA que a semente marca (a mesma conta).
    /// Onde o mockup simplifica o que o CRM faz, vale o CRM: quem anotou ou escreveu é o dono da
    /// conversa (escrever é assumir), e a conversa encerrada que recebe mensagem reabre sem dono.
    /// </summary>
    private static List<Conversa> Semear()
    {
        DateTimeOffset T(int dias, int h, int m, int s = 0) => RelogioDaPrevia.EmUtc(dias, h, m, s);
        var R = RelogioDaPrevia.Em;
        var ana = Nome(Eu);
        var bruna = Nome("bruna");
        var lista = new List<Conversa>();

        // ----- Maria: "não vem" hoje 14:00; Ana ofereceu dois horários; a resposta nova não foi lida.
        var sessaoMaria = R(0, 14, 0);
        var opcao1 = R(4, 14, 0);
        var opcao2 = R(6, 9, 30);
        var maria = new Conversa(Ids.Maria, PessoasDoWhatsApp.Maria.Nome, PessoasDoWhatsApp.Maria.NoCrm, RemetenteWhatsApp)
            { Ficha = PessoasDoWhatsApp.Maria, NaoLidasIniciais = 1 };
        maria.Evento(T(-1, 16, 19), bruna, "Assumiu ao escrever para o paciente")
            .Enviada(T(-1, 16, 20), "bruna", $"Boa tarde, Maria! Lembrando que amanhã às {RelogioDaPrevia.Hora(sessaoMaria)} você tem acupuntura com a Dra. Helena. "
                + "Qualquer coisa é só responder por aqui.", "seen")
            .Encerrada(T(-1, 16, 24), "bruna", "information", "Lembrete da sessão de amanhã enviado.")
            .Enviada(T(0, 6, 0), "automation", Texto(ModeloLembrete, "Maria", "hoje às " + RelogioDaPrevia.Hora(sessaoMaria)), "seen",
                Modelo(ModeloLembrete, "Maria", "hoje às " + RelogioDaPrevia.Hora(sessaoMaria)))
            .Recebida(T(0, 9, 58), "Bom dia! Não vou conseguir ir hoje, posso remarcar?")
            .Reaberta(T(0, 9, 58))
            .Recebida(T(0, 9, 58, 20), "Surgiu um compromisso no trabalho 😕")
            .Evento(T(0, 9, 58, 21), "Paciente", "Paciente pediu remarcação; horário original mantido até a equipe combinar o novo")
            .Evento(T(0, 9, 59), "Agenda da clínica", "A agenda da clínica registrou: Não vem — remarcar (o horário continua marcado até a recepção combinar o novo)")
            .Evento(T(0, 10, 1), ana, "Assumiu o atendimento humano; respostas automáticas suspensas");
        maria.AgentId = Eu;
        maria.Nota(T(0, 10, 2), Eu, "Ela prefere o período da tarde. Oferecer terça ou quinta com a Dra. Helena.")
            .Enviada(T(0, 10, 5), Eu, $"Bom dia, Maria! Sem problema. Tenho {RelogioDaPrevia.Quando(opcao1)} ou {RelogioDaPrevia.Quando(opcao2)} "
                + "com a Dra. Helena. Qual prefere?", "seen")
            .Recebida(T(0, 10, 9), $"{char.ToUpper(RelogioDaPrevia.DiaDaSemana(opcao1)[0])}{RelogioDaPrevia.DiaDaSemana(opcao1)[1..]} às {RelogioDaPrevia.HoraFalada(opcao1)} fica ótimo");
        lista.Add(maria);

        // ----- Novo contato: sem ficha, duas mensagens sem resposta.
        var novo = new Conversa(Ids.NovoContato, PessoasDoWhatsApp.NovoContato.NoCrm, PessoasDoWhatsApp.NovoContato.NoCrm, RemetenteWhatsApp)
            { NaoLidasIniciais = 2 };
        novo.Recebida(T(0, 9, 29), "Bom dia, gostaria de saber o valor da acupuntura")
            .Recebida(T(0, 9, 30), "É para a minha mãe, 72 anos, dor no joelho");
        lista.Add(novo);

        // ----- Terezinha: o telefone está em DUAS fichas (ela e o filho Pedro); a conversa não tem ficha.
        var sessaoPedro = R(1, 9, 0);
        var terezinha = new Conversa(Ids.Terezinha, PessoasDoWhatsApp.Terezinha.Nome, PessoasDoWhatsApp.Terezinha.NoCrm, RemetenteWhatsApp)
            { NaoLidasIniciais = 1 };
        terezinha.Enviada(T(0, 6, 0), "automation", Texto(ModeloLembrete, "Pedro", "amanhã às " + RelogioDaPrevia.Hora(sessaoPedro)), "seen",
                Modelo(ModeloLembrete, "Pedro", "amanhã às " + RelogioDaPrevia.Hora(sessaoPedro)))
            .Recebida(T(0, 9, 12), "O Pedro vai sim amanhã, obrigada");
        lista.Add(terezinha);

        // ----- Carlos: janela de 24 h fechada; o lembrete de hoje foi lido e ficou sem resposta.
        var sessaoCarlos = R(0, 16, 0);
        var carlos = new Conversa(Ids.Carlos, PessoasDoWhatsApp.Carlos.Nome, PessoasDoWhatsApp.Carlos.NoCrm, RemetenteWhatsApp)
            { Ficha = PessoasDoWhatsApp.Carlos, AgentId = Eu };
        carlos.Evento(T(-2, 10, 58), ana, "Assumiu ao escrever para o paciente")
            .Enviada(T(-2, 10, 58), Eu, $"Bom dia, Carlos! Sua próxima sessão de BSV ficou para {RelogioDaPrevia.Quando(sessaoCarlos)} com o Dr. Marcos, combinado?", "seen")
            .Recebida(T(-2, 11, 5), "Ok, obrigado")
            .Enviada(T(0, 6, 0), "automation", Texto(ModeloLembrete, "Carlos", "hoje às " + RelogioDaPrevia.Hora(sessaoCarlos)), "seen",
                Modelo(ModeloLembrete, "Carlos", "hoje às " + RelogioDaPrevia.Hora(sessaoCarlos)))
            .Nota(T(0, 10, 10), Eu, $"Lembrete lido às {RelogioDaPrevia.Hora(R(0, 7, 12))} e sem resposta. Se ele não responder, ligo depois do almoço.");
        lista.Add(carlos);

        // ----- João: confirmou pelo WhatsApp.
        var sessaoJoao = R(0, 15, 0);
        var joao = new Conversa(Ids.Joao, PessoasDoWhatsApp.Joao.Nome, PessoasDoWhatsApp.Joao.NoCrm, RemetenteWhatsApp)
            { Ficha = PessoasDoWhatsApp.Joao, AgentId = Eu };
        joao.Enviada(T(0, 6, 0), "automation", Texto(ModeloLembrete, "João", "hoje às " + RelogioDaPrevia.Hora(sessaoJoao)), "seen",
                Modelo(ModeloLembrete, "João", "hoje às " + RelogioDaPrevia.Hora(sessaoJoao)))
            // O pedido médico em PDF e um vídeo que o provedor não entregou (a foto do "Tentar de novo").
            .RecebidaComMidia(T(0, 9, 38), "DOCUMENT", "", MidiaDeExemplo.PedidoMedico)
            .RecebidaComMidia(T(0, 9, 39), "VIDEO", "O exercício que a fisio passou", null)
            .Recebida(T(0, 9, 40), "Confirmado, obrigado!")
            .Evento(T(0, 9, 40, 5), "Paciente", "Confirmação recebida; atualização da agenda pendente")
            .Evento(T(0, 9, 40, 30), "Agenda da clínica", "A agenda da clínica registrou a confirmação do horário")
            .Evento(T(0, 9, 41), ana, "Assumiu ao escrever para o paciente")
            .Enviada(T(0, 9, 41), Eu, $"Perfeito, João! Até as {RelogioDaPrevia.HoraFalada(sessaoJoao)} 🙂", "seen");
        lista.Add(joao);

        // ----- Roberto: remarcou ontem; a última mensagem é da clínica (aguardando o paciente).
        var sessaoRoberto = R(6, 9, 30);
        var roberto = new Conversa(Ids.Roberto, PessoasDoWhatsApp.Roberto.Nome, PessoasDoWhatsApp.Roberto.NoCrm, RemetenteWhatsApp)
            { Ficha = PessoasDoWhatsApp.Roberto, AgentId = Eu };
        roberto.Recebida(T(-1, 16, 48), "Boa tarde, preciso trocar a sessão da semana que vem, dá pra ser de manhã?")
            // A mídia (24/10): a foto do pedido e a nota de voz que o paciente mandou; o PDF que a Ana mandou.
            .RecebidaComMidia(T(-1, 16, 49), "IMAGE", "Esse é o pedido do médico", MidiaDeExemplo.Foto)
            .RecebidaComMidia(T(-1, 16, 50), "AUDIO", "", MidiaDeExemplo.NotaDeVoz)
            .Evento(T(-1, 16, 55), ana, "Assumiu ao escrever para o paciente")
            .Enviada(T(-1, 16, 55), Eu, $"Boa tarde, Roberto! Tenho {RelogioDaPrevia.Quando(sessaoRoberto)} com a Dra. Helena. Serve?", "seen")
            .Recebida(T(-1, 17, 2), "Serve sim")
            .RecebidaComMidia(T(-1, 17, 2, 20), "IMAGE", "", MidiaDeExemplo.Figurinha, figurinha: true)
            .Evento(T(-1, 17, 3), "Agenda da clínica", "Clínica confirmou remarcação do horário #1. Nenhuma mensagem enviada ao paciente.")
            .EnviadaComAnexo(T(-1, 17, 3, 10), Eu, "Seguem as orientações para o dia do BSV.", "DOCUMENT", MidiaDeExemplo.Orientacoes, "seen")
            .Enviada(T(-1, 17, 3, 30), Eu, $"Fica combinado então, Roberto. Até {RelogioDaPrevia.DiaDaSemana(sessaoRoberto)}!", "delivered");
        lista.Add(roberto);

        // ----- Fernanda: retenção — o convite foi ontem, pelo modelo (a janela estava fechada desde a última sessão).
        var fernanda = new Conversa(Ids.Fernanda, PessoasDoWhatsApp.Fernanda.Nome, PessoasDoWhatsApp.Fernanda.NoCrm, RemetenteWhatsApp)
            { Ficha = PessoasDoWhatsApp.Fernanda };
        fernanda.Evento(T(-20, 10, 18), bruna, "Assumiu o atendimento humano; respostas automáticas suspensas");
        fernanda.AgentId = "bruna";
        fernanda.Recebida(T(-20, 10, 20), "Obrigada pela sessão de hoje! Até semana que vem")
            .Encerrada(T(-20, 10, 31), "bruna", "information", "Agradeceu a sessão.")
            .Evento(T(-1, 15, 10), bruna, "Reabriu ao escrever para o paciente");
        fernanda.Closed = false;
        fernanda.ClosedAt = null;
        fernanda.Enviada(T(-1, 15, 10, 5), "bruna", Texto(ModeloRecall, "Fernanda"), "delivered", Modelo(ModeloRecall, "Fernanda"));
        lista.Add(fernanda);

        // ----- Antônio: por SMS (sem acento) e encerrada com "Agendamento realizado".
        var sessaoAntonio = R(3, 10, 0);
        var antonio = new Conversa(Ids.Antonio, PessoasDoWhatsApp.Antonio.Nome, PessoasDoWhatsApp.Antonio.NoCrm, RemetenteSms)
            { Ficha = PessoasDoWhatsApp.Antonio, AgentId = "bruna" };
        antonio.Recebida(T(-1, 11, 2), "Bom dia quero marcar o bloqueio")
            .Evento(T(-1, 11, 20), bruna, "Assumiu ao escrever para o paciente")
            .Enviada(T(-1, 11, 20, 5), "bruna", RelogioDaPrevia.SemAcento(
                $"Bom dia, Sr. Antônio! Marquei o BSV para {RelogioDaPrevia.Quando(sessaoAntonio)} com o Dr. Marcos. Chegue 15 min antes."), "delivered")
            .Recebida(T(-1, 11, 28), "Ta bom obrigado")
            .Encerrada(T(-1, 11, 30), "bruna", "scheduled", $"BSV marcado para {RelogioDaPrevia.Quando(sessaoAntonio)} com o Dr. Marcos.");
        lista.Add(antonio);

        // ----- Alzira: respondeu ao convite de retorno (o caso pede o "Como foi?").
        var alzira = new Conversa(Ids.Alzira, PessoasDoWhatsApp.Alzira.Nome, PessoasDoWhatsApp.Alzira.NoCrm, RemetenteWhatsApp)
            { Ficha = PessoasDoWhatsApp.Alzira, AgentId = "bruna", NaoLidasIniciais = 1, FichaLigadaEm = T(-2, 15, 11, 30), FichaLigadaPor = bruna };
        alzira.Evento(T(-2, 15, 11), bruna, "Criou um novo atendimento. Aguardando envio de modelo ou mensagem do contato.")
            .Enviada(T(-2, 15, 12), "bruna", Texto(ModeloRecall, "Alzira"), "seen", Modelo(ModeloRecall, "Alzira"))
            .Recebida(T(-1, 17, 40), "Oi Bruna, obrigada por lembrar. Eu quero voltar sim, mas só depois da viagem da minha filha, daqui a umas duas semanas")
            .Evento(T(-1, 17, 40, 10), "Recall", "Paciente respondeu ao recall; equipe deve acompanhar o retorno");
        lista.Add(alzira);

        // ----- Lúcia: escreveu de um número sem ficha, com a Bruna; o @SemDor leu como reação ao tratamento (atenção já).
        var lucia = new Conversa(Ids.Lucia, PessoasDoWhatsApp.Lucia.Nome, PessoasDoWhatsApp.Lucia.NoCrm, RemetenteWhatsApp)
            { AgentId = "bruna", NaoLidasIniciais = 1 };
        lucia.Evento(T(-1, 15, 40), bruna, "Assumiu ao escrever para o paciente")
            .Enviada(T(-1, 15, 40, 5), "bruna", "Boa tarde, Lúcia! Qualquer dúvida depois da sessão, é só escrever por aqui.", "seen")
            .Recebida(T(0, 10, 6), "Bom dia. Depois da acupuntura de ontem o braço ficou inchado e vermelho onde colocaram a agulha, e está doendo. É normal?");
        lista.Add(lucia);

        return lista;
    }
}
