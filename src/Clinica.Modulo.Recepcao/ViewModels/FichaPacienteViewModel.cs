using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Domain.Prontuario;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// Uma sessão do prontuário numa lista. Usada pela ficha do paciente E pela tela de
/// Prontuário — o <see cref="De"/> existe para as duas nunca divergirem no que mostram:
/// duas descrições da mesma sessão em telas diferentes é defeito que ninguém reporta e
/// todo mundo estranha.
/// </summary>
public sealed class LinhaEvolucao
{
    public required int EvolucaoId { get; init; }
    public required string Data { get; init; }
    public required string Profissional { get; init; }
    public required string Eva { get; init; }
    public required string Resumo { get; init; }
    public required bool Melhorou { get; init; }
    public required bool Piorou { get; init; }
    public required string Anexos { get; init; }

    public static LinhaEvolucao De(Evolucao e, int anexos) => new()
    {
        EvolucaoId = e.Id,
        Data = e.Data.ToString("dd/MM/yyyy"),
        Profissional = e.Profissional?.Rotulo ?? "—",
        Eva = e.TemParEva ? $"EVA {e.EvaAntes} → {e.EvaDepois}" : "EVA não medida",
        Resumo = Resumir(e),
        Melhorou = e.VariacaoEva > 0,
        Piorou = e.VariacaoEva < 0,
        Anexos = anexos == 0 ? string.Empty : $"{anexos} anexo(s)"
    };

    /// <summary>
    /// A primeira linha que existir, cortada. A queixa vem antes da conduta porque é o
    /// que identifica a sessão na lista: "lombalgia" acha o dia; "agulhamento" não.
    /// </summary>
    private static string Resumir(Evolucao e)
    {
        var texto = e.QueixaPrincipal ?? e.TextoEvolucao ?? e.Conduta ?? e.Orientacoes ?? string.Empty;
        texto = texto.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return texto.Length <= 120 ? texto : texto[..117] + "…";
    }
}

/// <summary>Situação de uma finalidade de consentimento LGPD.</summary>
public sealed class LinhaConsentimento
{
    /// <summary>
    /// Qual finalidade a linha descreve. Deixou de ter leitor na parcela 89 — quem
    /// concede e recusa agora é o paciente, no termo — e FICA porque é a identidade da
    /// linha: sem ela, duas finalidades com o mesmo rótulo seriam indistinguíveis para
    /// quem depurar a lista, e a próxima leitura por finalidade a reconstruiria igual.
    /// </summary>
    public required FinalidadeConsentimento Finalidade { get; init; }
    public required string Rotulo { get; init; }
    public required string Situacao { get; init; }
    public required bool Vigente { get; init; }
    public required bool PodeRevogar { get; init; }

    /// <summary>Id do registro atual — necessário para revogar.</summary>
    public int? RegistroId { get; init; }
}

/// <summary>Um contato de campanha no histórico de CRM da ficha.</summary>
public sealed class LinhaContato
{
    public required string Tipo { get; init; }
    public required string Data { get; init; }
    public required string Situacao { get; init; }
    public required string Detalhe { get; init; }

    public static LinhaContato De(ContatoCampanha c) => new()
    {
        Tipo = c.Tipo switch
        {
            TipoContato.ConfirmacaoSessao => "Confirmação de sessão",
            TipoContato.Nps => "Pesquisa de satisfação (NPS)",
            TipoContato.Recall => "Recall",
            _ => c.Tipo.ToString()
        },
        Data = c.Referencia.ToString("dd/MM/yyyy"),
        Situacao = c.Status.ToString(),
        // A nota do NPS é o que a direção quer ver de relance; nos outros tipos, quem
        // disparou. Mostrar "—" seria desperdiçar a linha.
        Detalhe = c.Nota is { } nota
            ? $"nota {nota}/10"
            : c.EnviadoEm is { } enviado
                ? $"enviado em {enviado:dd/MM/yyyy} por {c.EnviadoPor ?? "?"}"
                : "ainda não enviado"
    };
}

/// <summary>Um alerta de elegibilidade na ficha.</summary>
public sealed class LinhaAlerta
{
    public required string Descricao { get; init; }
    public required bool EhVermelho { get; init; }
}

/// <summary>
/// Um termo que o procedimento de HOJE exige deste paciente (parcela 66, 2ª rodada).
///
/// ⚠️ A linha existe SÓ para o dia de hoje, e a tela diz isso. A validade do termo é POR
/// SESSÃO — a declaração de jejum afirma "estou em jejum" e não se herda —, então uma
/// porta que deixasse colher hoje um termo "para amanhã" produziria um papel assinado que
/// o balcão não veria como cumprido amanhã: pior do que não ter porta, porque a pessoa
/// acreditaria ter resolvido.
/// </summary>
public sealed class LinhaTermo
{
    public required int ModeloId { get; init; }
    public required int? DocumentoId { get; init; }
    public required string Nome { get; init; }
    public required string Procedimento { get; init; }
    public required bool Pendente { get; init; }
    public required bool Assinado { get; init; }
    public required bool Recusado { get; init; }
    public required string? MotivoRecusa { get; init; }
    public required IReadOnlyList<string> DeclaracoesNegadas { get; init; }

    /// <summary>
    /// Quem atende no horário de hoje. Vai para o termo emitido — sem ele a via que o
    /// paciente assina sai com "Profissional responsável" no lugar do nome e do CRM.
    /// </summary>
    public required int? ProfissionalId { get; init; }

    /// <summary>
    /// O que a linha diz de relance. As três respostas são diferentes de propósito:
    /// "falta assinar" é tarefa, "recusou" é decisão tomada, e "assinado com declaração
    /// negada" é o caso GRAVE — o papel está completo e o procedimento pode não estar
    /// seguro. Fundir os dois últimos faria "resolvido" cobrir um problema clínico.
    /// </summary>
    public string Situacao
    {
        get
        {
            if (Recusado)
                return string.IsNullOrWhiteSpace(MotivoRecusa)
                    ? "Paciente recusou assinar"
                    : $"Paciente recusou: {MotivoRecusa}";

            if (DeclaracoesNegadas.Count > 0)
                return $"Assinado — respondeu NÃO em: {string.Join("; ", DeclaracoesNegadas)}";

            return Assinado ? "Assinado hoje" : "Falta o paciente assinar";
        }
    }

    /// <summary>Vermelho: ou falta assinar, ou o paciente negou uma declaração.</summary>
    public bool EhVermelho => Pendente || DeclaracoesNegadas.Count > 0;

    /// <summary>
    /// A metade VISÍVEL do acesso. A que IMPEDE é o <c>Exigir</c> no comando — só
    /// desabilitar é enfeite, porque o atalho de teclado passa direto.
    /// </summary>
    public bool PodeColher
        => Pendente && SessaoUsuario.Atual.Pode(Permissao.ColherAssinaturaPaciente);

    public static LinhaTermo De(SituacaoTermo s) => new()
    {
        ModeloId = s.ModeloId,
        DocumentoId = s.DocumentoId,
        Nome = s.NomeDoTermo,
        // Nome do CATÁLOGO, nunca o enum: `{s.Modalidade}` escreveria "BsvComAcupuntura"
        // na ficha que a recepcionista lê (o defeito da parcela 41).
        Procedimento = CatalogoModalidades.Nome(s.Modalidade.ToString()),
        Pendente = s.Pendente,
        Assinado = s.Assinado,
        Recusado = s.Recusado,
        MotivoRecusa = s.MotivoRecusa,
        DeclaracoesNegadas = s.DeclaracoesNegadas,
        ProfissionalId = s.ProfissionalId
    };
}

/// <summary>Um documento clínico emitido, na lista da ficha.</summary>
public sealed class LinhaDocumento
{
    public required int DocumentoId { get; init; }

    /// <summary>Dono do documento — a entrega precisa do telefone dele.</summary>
    public required int PacienteId { get; init; }
    public required string Numero { get; init; }
    public required string Tipo { get; init; }
    public required string Data { get; init; }
    public required string Profissional { get; init; }
    public required string Codigo { get; init; }
    public required bool Cancelado { get; init; }
    public required bool Assinado { get; init; }
    public required string Situacao { get; init; }

    /// <summary>Nome sugerido ao salvar o PDF.</summary>
    public string NomeArquivo => $"{Tipo}-{Numero.Replace('/', '-')}.pdf";

    /// <summary>
    /// Nome do arquivo entregue ao paciente — o mesmo sufixo que o serviço de assinatura
    /// grava, para a pasta de entregas não guardar duas versões do mesmo número.
    /// </summary>
    public string NomeArquivoAssinado => $"{Tipo}-{Numero.Replace('/', '-')}-assinado.pdf";

    /// <summary>
    /// O acesso que ESTE papel exige para ser visto (parcela 59).
    ///
    /// Receita, atestado, pedido de exame, relatório de evolução e anamnese pedem
    /// <c>VerProntuario</c>; declaração de comparecimento e termo de consentimento pedem
    /// só a ficha. Quem decide é o CATÁLOGO, para as três telas que listam documento
    /// clínico não terem três respostas para a mesma pergunta.
    /// </summary>
    public required Permissao AcessoParaVer { get; init; }

    /// <summary>O acesso para cancelar ou assinar este papel.</summary>
    public required Permissao AcessoParaMexer { get; init; }

    /// <summary>
    /// Cancelar duas vezes não existe — o botão desliga depois do primeiro. E cancelar
    /// uma receita é ato de quem prescreve: é a metade VISÍVEL do acesso; a que impede é
    /// o <c>Exigir</c> no comando.
    /// </summary>
    public bool PodeCancelar => !Cancelado && SessaoUsuario.Atual.Pode(AcessoParaMexer);

    /// <summary>
    /// Assinar depois existe porque emissão e assinatura nem sempre acontecem no mesmo
    /// minuto. Assinado não se reassina (dois arquivos válidos do mesmo ato, e nada
    /// diria qual o paciente levou) e cancelado não se assina. Compõe com o acesso
    /// (parcela 61), como o <see cref="PodeCancelar"/> logo acima: sem o bit o botão
    /// ficava aceso e o clique estourava no Exigir.
    /// </summary>
    public bool PodeAssinar => !Cancelado && !Assinado
        && SessaoUsuario.Atual.Pode(AcessoParaMexer);

    /// <summary>
    /// Só documento ASSINADO se entrega como arquivo. Mandar um PDF sem assinatura pelo
    /// WhatsApp entrega ao paciente algo que a farmácia não tem como conferir — e ele só
    /// descobre no balcão. Sem assinatura, o que vale é a via impressa, assinada à caneta.
    /// </summary>
    /// <summary>
    /// ⚠️ Compõe com o ACESSO, como <see cref="PodeCancelar"/> e <see cref="PodeAssinar"/>
    /// ao lado. Era estado puro, e a enfermeira — que tem `VerProntuario` e não tem
    /// `Prescrever` — via a receita na lista (o filtro por folha a deixa passar) com o
    /// botão "Enviar" ACESO: um clique mandava a receita assinada pelo WhatsApp.
    /// </summary>
    public bool PodeEnviar => Assinado && !Cancelado
        && SessaoUsuario.Atual.Pode(AcessoParaMexer);

    /// <summary>
    /// Mesma razão do <see cref="LinhaEvolucao.De"/>: a ficha e a tela de Prescrições
    /// listam o mesmo documento, e a situação ("Cancelado em…") não pode ser escrita
    /// duas vezes em lugares diferentes.
    /// </summary>
    public static LinhaDocumento De(DocumentoClinico d) => new()
    {
        DocumentoId = d.Id,
        PacienteId = d.PacienteId,
        Numero = d.Numero,
        Tipo = TipoDocumentoInfo.Rotular(d.Tipo),
        Data = d.Data.ToString("dd/MM/yyyy"),
        Profissional = d.Profissional?.Rotulo ?? "—",
        Codigo = d.CodigoVerificacao,
        Cancelado = d.Cancelado,
        Assinado = d.AssinadoEletronicamente,
        AcessoParaVer = CentralDocumentosService.AcessoParaVer(d.Tipo),
        AcessoParaMexer = CentralDocumentosService.AcessoParaEmitir(d.Tipo),
        Situacao = d.Cancelado
            ? $"Cancelado em {d.CanceladoEm:dd/MM/yyyy}"
            : d.AssinadoEletronicamente
                ? $"Assinado digitalmente em {d.AssinadoEm:dd/MM/yyyy}"
                : "Válido"
    };
}

/// <summary>
/// Ficha 360º do paciente: cadastro, elegibilidade, consentimentos LGPD, prontuário com
/// a evolução da dor e o histórico de guias — tudo o que a recepção precisa saber com o
/// paciente na frente.
///
/// A ELEGIBILIDADE é o que esta tela tem de mais valioso: carteirinha vencida e cota
/// estourada hoje só aparecem na hora de faturar, quando a sessão já aconteceu. Aqui
/// elas aparecem antes, enquanto ainda dá para resolver.
/// </summary>
public sealed partial class FichaPacienteViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaConsentimento> Consentimentos { get; } = [];

    /// <summary>
    /// Os próximos horários deste paciente (set/2026) — a resposta a "quando é a minha
    /// próxima sessão?", que até aqui a recepcionista respondia navegando a agenda dia a
    /// dia. Lista curta, com teto: quem tem dez sessões marcadas vê as cinco mais próximas
    /// e abre a agenda para o resto.
    /// </summary>
    public ObservableCollection<LinhaHorarioFuturo> ProximosHorarios { get; } = [];

    /// <summary>Terceiro estado: a agenda não pôde ser lida — não é "nenhum horário".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SemProximosHorarios))]
    private bool _proximosHorariosNaoVerificados;

    private bool _proximosHorariosLidos;

    /// <summary>
    /// "Nenhum horário marcado" só depois de a leitura ter voltado vazia — antes da
    /// resposta, e depois de uma falha, a frase seria uma afirmação sobre o que não se sabe.
    /// </summary>
    public bool SemProximosHorarios
        => _proximosHorariosLidos && !ProximosHorariosNaoVerificados && ProximosHorarios.Count == 0;

    /// <summary>Horário é dado da agenda: a região segue o bit da agenda, não o da ficha.</summary>
    public bool PodeVerAgenda => SessaoUsuario.Atual.Pode(Permissao.VerAgenda);

    public const int TetoDeProximosHorarios = 5;
    public ObservableCollection<LinhaAlerta> Alertas { get; } = [];
    public ObservableCollection<LinhaDocumento> Documentos { get; } = [];

    /// <summary>
    /// Os termos que o procedimento de HOJE exige deste paciente (parcela 66, 2ª rodada).
    ///
    /// A porta nasceu na fila, no check-in. O balcão pediu a mesma coisa aqui pela razão
    /// que o cliente escreveu: com a ficha aberta na frente do paciente, colher a
    /// assinatura antes de ele subir para a sala é um clique, e não uma volta à fila.
    /// </summary>
    public ObservableCollection<LinhaTermo> Termos { get; } = [];

    /// <summary>
    /// As autorizações de sessões — a "senha" do convênio (parcela 48).
    ///
    /// A recepção já era AVISADA de que a cota ia estourar (<c>ElegibilidadeService</c>,
    /// parcela 26) e a única porta para registrar a senha nova estava no app de
    /// FATURAMENTO. Quem recebe a senha da operadora é o balcão.
    /// </summary>
    public ObservableCollection<SaldoAutorizacao> Autorizacoes { get; } = [];

    /// <summary>Não foi possível LER as autorizações — o terceiro estado, nunca lista vazia.</summary>
    [ObservableProperty] private bool _autorizacoesNaoVerificadas;

    // ---- O termo LGPD assinado pelo paciente (parcela 89) ----

    /// <summary>
    /// O que a aba LGPD diz de relance sobre o termo. As respostas não se fundem: "nunca
    /// colhido" é tarefa que ninguém começou, "aguardando" é papel já emitido esperando a
    /// assinatura (do balcão ou do celular), e a data do assinado é a prova. Uma frase só
    /// faria a segunda parecer a primeira, e o balcão emitiria outro termo em cima do que
    /// o paciente está lendo no telefone.
    ///
    /// ⚠️ Nasce em "Conferindo…" e não em "Nenhum termo": antes de a leitura voltar, o
    /// sistema não SABE, e afirmar que nunca foi colhido é a resposta que faz a
    /// recepcionista começar um termo que já existe.
    /// </summary>
    [ObservableProperty] private string _termoLgpdSituacao = "Conferindo…";

    /// <summary>
    /// O termo já emitido e ainda não assinado, quando existe — para a coleta REAPROVEITAR
    /// em vez de emitir outro. Dois papéis do mesmo ato com números diferentes é o que
    /// faria a auditoria perguntar qual deles vale.
    /// </summary>
    public int? TermoLgpdPendenteId { get; private set; }

    /// <summary>
    /// A metade VISÍVEL do acesso. A que IMPEDE é o <c>Pode</c> no comando, e ela DIZ por
    /// que recusou — só desabilitar é enfeite, porque o atalho de teclado passa direto.
    /// </summary>
    public bool PodeColherTermoLgpd
        => SessaoUsuario.Atual.Pode(Permissao.ColherAssinaturaPaciente);

    /// <summary>Código do convênio do paciente, para a janela de autorização já vir escolhida.</summary>
    private string? _convenioCodigo;

    [ObservableProperty] private int _pacienteId;
    [ObservableProperty] private bool _carregando;

    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private string _documento = "—";
    [ObservableProperty] private string _telefone = "—";
    [ObservableProperty] private string _nascimento = "—";
    [ObservableProperty] private string _convenio = "—";
    [ObservableProperty] private string _carteirinha = "—";
    [ObservableProperty] private string _observacoes = string.Empty;
    [ObservableProperty] private byte[]? _foto;

    [ObservableProperty] private string _totalSessoes = "—";

    // ---- Padrão de falta (parcela 28) ----

    /// <summary>
    /// Quantas vezes o paciente faltou. A agenda registra `Faltou` desde a parcela 1 e os
    /// indicadores calculam a taxa da CLÍNICA — a do paciente nunca foi lida por ninguém.
    /// Quem está no balcão decidindo se dá o horário das 18h de terça (o mais disputado)
    /// para alguém que faltou três vezes não tinha como saber.
    /// </summary>
    [ObservableProperty] private string _faltas = "—";

    /// <summary>Cancelamento avisado, contado SEPARADO: quem desmarcou deu chance de reocupar.</summary>
    [ObservableProperty] private string _cancelamentos = "—";

    /// <summary>Padrão de falta digno de atenção. Aviso, nunca impedimento.</summary>
    [ObservableProperty] private bool _faltaReincidente;

    [ObservableProperty] private string _avisoFaltas = string.Empty;
    [ObservableProperty] private string _guiasEmAberto = "—";
    [ObservableProperty] private string _ultimaSessao = "—";

    // ---- Evolução da dor (EVA) ----
    [ObservableProperty] private string _dorInicial = "—";
    [ObservableProperty] private string _dorAtual = "—";
    [ObservableProperty] private string _ganhoAcumulado = "—";
    [ObservableProperty] private string _alivioMedio = "—";
    [ObservableProperty] private string _resumoEva = string.Empty;
    [ObservableProperty] private bool _semMedidaEva;

    /// <summary>
    /// Terceiro estado da elegibilidade: a checagem NÃO rodou. Sem isto, uma consulta
    /// que falhou apareceria como "tudo certo" — falha exibida como sucesso.
    /// </summary>
    [ObservableProperty] private bool _elegibilidadeNaoVerificada;

    // ===== CRM =====

    /// <summary>De onde o paciente veio. "Não perguntado" é um estado legítimo.</summary>
    [ObservableProperty] private string _origem = "Não perguntado";

    [ObservableProperty] private bool _temPaciente;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Últimas conversas da clínica com este paciente (confirmação, NPS, recall).</summary>
    public ObservableCollection<LinhaContato> Contatos { get; } = [];

    /// <summary>Avisa a tela dona que o cadastro mudou (para recarregar a lista).</summary>
    public event Action? Alterou;

    /// <summary>
    /// Habilita os botões de escrita da tela. É a metade VISÍVEL da permissão: o
    /// botão apagado explica por que não dá; a guarda no comando é que impede.
    /// Só desabilitar seria enfeite — um atalho de teclado passaria direto.
    /// </summary>
    public bool PodeEditarProntuario => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    /// <summary>
    /// Metade VISÍVEL da permissão de escrever na FICHA: cadastro, consentimento,
    /// autorização do convênio e documento. Separada de <c>PodeEditarProntuario</c> na
    /// parcela 49 — digitar o telefone de alguém e escrever a evolução dele são atos de
    /// peso diferente, e até aqui o mesmo bit dava os dois.
    /// </summary>
    public bool PodeEditarCadastro => SessaoUsuario.Atual.Pode(Permissao.EditarPaciente);

    /// <summary>
    /// Metade VISÍVEL do acesso a emitir documento clínico (parcela 59).
    ///
    /// Ela existe porque a guarda mudou: o botão "Novo documento…" abre a janela que
    /// oferece receita, atestado e pedido de exame, e o comando passou a exigir
    /// <see cref="Permissao.Prescrever"/>. Deixá-lo ligado a <c>PodeEditarCadastro</c>
    /// daria um botão ACESO que só diz "seu acesso não permite" depois do clique — o
    /// defeito da parcela 41 com uma etapa a mais.
    /// </summary>
    public bool PodePrescrever => SessaoUsuario.Atual.Pode(Permissao.Prescrever);

    /// <summary>
    /// A seção de termos aparece. Fecha para quem não tem <see cref="Permissao.VerProntuario"/>
    /// — o termo é dado de saúde, e os perfis de balcão financeiro abrem esta ficha.
    /// Some, não fica apagada: "sem permissão" ao lado de "Termo do BSV" anunciaria que
    /// existe um BSV marcado para esta pessoa, que é justamente o que não se quer contar.
    /// </summary>
    /// <remarks>
    /// Quem TEM o acesso vê a seção mesmo sem termo nenhum: é ela que explica que o termo é
    /// colhido no DIA do procedimento, e sumir faria a recepcionista procurar onde assinar
    /// numa aba que decidiu não mostrar nada. O que a some é a falta de permissão.
    /// </remarks>
    public bool TemTermoDoDia => SessaoUsuario.Atual.Pode(Permissao.VerProntuario);

    /// <summary>
    /// A conferência não rodou. Terceiro estado escrito, nunca lista vazia silenciosa:
    /// "não há termo" e "não deu para conferir" são respostas diferentes.
    /// </summary>
    [ObservableProperty]
    private bool _termosNaoVerificados;

    /// <summary>
    /// A frase da seção, e ela muda de resposta de propósito.
    ///
    /// "Nenhum procedimento de hoje exige termo" e "todos assinados" são coisas diferentes,
    /// e quem lê precisa distingui-las: a primeira significa que não há nada a fazer, a
    /// segunda que já foi feito. Seção muda sem frase se lê como defeito.
    /// </summary>
    public string ResumoTermos
    {
        get
        {
            if (TermosNaoVerificados)
                return "Não foi possível conferir os termos deste paciente agora. "
                       + "Recarregue a ficha antes de liberar o procedimento.";

            if (Termos.Count == 0)
                return "Nenhum procedimento marcado para hoje exige termo assinado. "
                       + "Dá para colher agora mesmo assim — o termo vale a partir da "
                       + "assinatura, e o paciente está aqui.";

            var faltam = Termos.Count(t => t.Pendente);

            return faltam switch
            {
                0 => "Os termos de hoje já foram resolvidos.",
                1 => "Falta 1 termo assinado para o procedimento de hoje.",
                _ => $"Faltam {faltam} termos assinados para os procedimentos de hoje."
            };
        }
    }

    /// <summary>
    /// Relatório de evolução e anamnese são IMPRESSOS do prontuário — emitir é tirar uma
    /// via do que já está lá, não escrever. Por isso pedem só a leitura.
    /// </summary>
    public bool PodeEmitirDoProntuario => SessaoUsuario.Atual.Pode(Permissao.VerProntuario);

    /// <summary>
    /// ⚠️ A aba Prontuário SOME para quem não tem o bit. Ela não tinha barreira nenhuma:
    /// Recepção, Financeiro e Faturista têm `VerFichaPaciente` e não têm `VerProntuario`, e
    /// liam a evolução inteira de qualquer paciente — o corte da parcela 49 desfeito por uma
    /// aba. Nem ler nem desenhar, como a seção de termos já fazia.
    /// </summary>
    public bool PodeVerProntuario => SessaoUsuario.Atual.Pode(Permissao.VerProntuario);

    /// <summary>Bit próprio da evolução de enfermagem (parcela 71).</summary>
    public bool PodeRegistrarEnfermagem =>
        SessaoUsuario.Atual.Pode(Permissao.RegistrarEvolucaoEnfermagem);

    /// <summary>
    /// Anonimizar não tem volta, então a barreira é outra: o balcão exporta os dados do
    /// titular, mas quem apaga a identificação é quem responde pela clínica.
    /// </summary>
    public bool PodeAnonimizar => SessaoUsuario.Atual.Pode(Permissao.AnonimizarDados);

    /// <summary>
    /// O PRONTUÁRIO NUMA SUPERFÍCIE SÓ (parcela 72) — o mesmo componente do Consultório e
    /// da tela da Enfermagem.
    ///
    /// ⚠️ As ações são injetadas AQUI, com a natureza junto: o componente não sabe abrir
    /// nada, e é isso que impede o comando da sessão de encostar no id 42 da enfermagem.
    /// A ficha sabe abrir e cancelar a SESSÃO MÉDICA; a evolução de enfermagem é lida aqui
    /// e escrita pelo botão do cabeçalho (o bit é outro, e o registro leva COREN).
    ///
    /// ⚠️ `MostrarDocumentos = false`: a aba Documentos ao lado é a porta do papel e faz
    /// mais do que este componente faria. Duas listas do mesmo papel na mesma tela fazem a
    /// pessoa procurar a diferença que não existe.
    /// </summary>
    public LinhaDoTempoClinicaViewModel LinhaDoTempo { get; }

    public FichaPacienteViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;

        LinhaDoTempo = new LinhaDoTempoClinicaViewModel(escopos)
        {
            MostrarDocumentos = false,
            NaturezasComAcao = [NaturezaRegistroClinico.SessaoMedica, NaturezaRegistroClinico.ArquivoDaFicha],
            AcessoParaMexer = Permissao.EditarProntuario,
            AoAbrir = AbrirRegistroAsync,
            AoCancelar = CancelarRegistroAsync
        };
    }

    /// <summary>
    /// Cancela o registro escolhido na linha do tempo — ROTEADO PELA NATUREZA.
    ///
    /// ⚠️ O <c>switch</c> é a razão de o item carregar <c>Natureza</c> + <c>Id</c>: sem
    /// ele, "cancelar o 42" seria ambíguo entre duas tabelas, e o erro apareceria como o
    /// registro de outro paciente sumindo do prontuário — sem estourar, sem avisar.
    /// </summary>
    private Task CancelarRegistroAsync(RegistroClinicoPaciente item) => item.Natureza switch
    {
        NaturezaRegistroClinico.SessaoMedica => CancelarSessaoAsync(item),
        NaturezaRegistroClinico.ArquivoDaFicha => CancelarArquivoDaFichaAsync(item),

        // ⚠️ FALHA ALTO, e não em silêncio. Hoje isto é inalcançável (`NaturezasComAcao`
        // tem só as duas de cima), mas `TemAcaoCancelar` olha só a lista de naturezas: no
        // dia em que alguém acrescentar uma ali, o botão "Cancelar…" acende na linha dela
        // e o clique NÃO FAZ NADA — o defeito da parcela 41 embutido na abstração nova.
        // Estourar aqui é o que faz o próximo `NaturezasComAcao` ser conferido.
        _ => throw new NotSupportedException(
            $"A ficha do paciente não sabe cancelar {CatalogoRegistroClinico.Rotular(item.Natureza)}.")
    };

    /// <summary>Abre o registro escolhido na linha do tempo — ROTEADO PELA NATUREZA, como o cancelar.</summary>
    private Task AbrirRegistroAsync(RegistroClinicoPaciente item) => item.Natureza switch
    {
        NaturezaRegistroClinico.SessaoMedica => AbrirEvolucaoAsync(item.Id),
        NaturezaRegistroClinico.ArquivoDaFicha => AbrirArquivoDaFichaAsync(item),
        _ => throw new NotSupportedException(
            $"A ficha do paciente não sabe abrir {CatalogoRegistroClinico.Rotular(item.Natureza)}.")
    };

    /// <summary>
    /// Os ARQUIVOS DA FICHA (set/2026) — a receita importada do sistema anterior, o laudo
    /// que chegou pelo WhatsApp. Abrir e cancelar passam pelo ponto único do shell
    /// (<see cref="ArquivosDaFicha"/>), o MESMO do Consultório e da tela da Enfermagem.
    /// </summary>
    private async Task AbrirArquivoDaFichaAsync(RegistroClinicoPaciente item)
    {
        try
        {
            var erro = await ArquivosDaFicha.AbrirAsync(_escopos, item.Id);
            Mensagem = erro ?? string.Empty;
            MensagemEhErro = erro is not null;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — arquivo da ficha não pôde ser aberto", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    private async Task CancelarArquivoDaFichaAsync(RegistroClinicoPaciente item)
    {
        try
        {
            if (!await ArquivosDaFicha.CancelarAsync(_escopos, _dialogo, item.Id, item.Titulo)) return;
            _snackbar.Info("Arquivo cancelado (guardado no prontuário).");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — arquivo da ficha não pôde ser cancelado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): abrir duas fichas em sequência
    /// deixa oito leituras de cada uma no ar, e a resposta atrasada da primeira chegando
    /// por último misturaria nome, foto e alertas de pacientes diferentes na mesma tela.
    /// </summary>
    private int _geracaoCarga;

    /// <summary>Carrega a ficha de um paciente (ou limpa, quando o id é nulo).</summary>
    public async Task AbrirAsync(int? pacienteId)
    {
        var geracao = ++_geracaoCarga;

        if (pacienteId is null)
        {
            TemPaciente = false;
            PacienteId = 0;
            return;
        }

        // O id fica numa LOCAL: o campo muda quando outra abertura chega durante os
        // awaits, e as leituras seguintes responderiam pelo paciente errado.
        var id = pacienteId.Value;
        PacienteId = id;
        TemPaciente = true;

        // A trilha de LEITURA (parcela 52). Fica em AbrirAsync, e não em CarregarAsync,
        // porque este é o ponto em que o paciente MUDA — CarregarAsync é rechamado a cada
        // gravação da própria ficha, e ali o acesso já foi registrado.
        using (var escopo = _escopos.CreateScope())
        {
            await escopo.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                .RegistrarAsync(id, SessaoUsuario.Atual.Operador,
                    OrigemAcessoProntuario.FichaPaciente);
        }

        // Chegou tarde: outra carga mais nova já foi pedida.
        if (geracao != _geracaoCarga) return;

        await CarregarAsync();
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        if (PacienteId == 0) return;

        var geracao = ++_geracaoCarga;
        var id = PacienteId;

        try
        {
            Carregando = true;
            Mensagem = string.Empty;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var pacientes = scope.ServiceProvider.GetRequiredService<PacienteService>();

            var paciente = await pacientes.ObterComHistoricoAsync(id);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            if (paciente is null)
            {
                TemPaciente = false;
                return;
            }

            AplicarCadastro(paciente);

            var foto = await pacientes.ObterFotoAsync(id);
            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;
            Foto = foto ?? paciente.FotoMiniatura;

            await CarregarProntuarioAsync(scope, id, geracao);
            if (geracao != _geracaoCarga) return;
            await CarregarConsentimentosAsync(scope, id, geracao);
            if (geracao != _geracaoCarga) return;
            await CarregarDocumentosAsync(scope, id, geracao);
            if (geracao != _geracaoCarga) return;
            await CarregarTermosAsync(scope, id, geracao);
            if (geracao != _geracaoCarga) return;
            await CarregarCrmAsync(scope, id, geracao);
            if (geracao != _geracaoCarga) return;
            await CarregarElegibilidadeAsync(scope, id, geracao);
            if (geracao != _geracaoCarga) return;
            await CarregarFaltasAsync(scope, id, geracao);
            if (geracao != _geracaoCarga) return;
            await CarregarAutorizacoesAsync(scope, id, geracao);
            if (geracao != _geracaoCarga) return;
            await CarregarProximosHorariosAsync(scope, id, geracao);
        }
        catch (Exception ex)
        {
            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Clinica.Application.Diagnostico.Registrar("Recepção — ficha do paciente não pôde ser carregada", ex);
            Mensagem = $"Não foi possível carregar a ficha: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    private void AplicarCadastro(Paciente p)
    {
        Nome = p.Nome;
        Documento = string.IsNullOrWhiteSpace(p.Documento) ? "—" : Cpf.Formatar(p.Documento);
        Telefone = string.IsNullOrWhiteSpace(p.Telefone) ? "—" : p.Telefone!;
        Nascimento = p.DataNascimento is { } n ? $"{n:dd/MM/yyyy} ({Idade(n)} anos)" : "—";
        _convenioCodigo = p.ConvenioCodigo ?? p.Convenio.ToString();
        Convenio = CatalogoConvenios.Nome(_convenioCodigo);
        Carteirinha = string.IsNullOrWhiteSpace(p.Carteirinha)
            ? "—"
            : p.ValidadeCarteirinha is { } v
                ? $"{p.Carteirinha} · vence {v:dd/MM/yyyy}"
                : p.Carteirinha!;
        Observacoes = p.Observacoes ?? string.Empty;

        // Origem em branco é "ninguém perguntou", e a ficha diz isso com todas as letras
        // em vez de deixar o campo vazio: campo vazio parece defeito, e a recepção só vai
        // colher o dado se souber que ele falta.
        // O rótulo em si vem de RotulosEnum, que é o ponto único (parcela 62) — aqui
        // fica só o que é DESTA tela: o "não perguntado" e o nome de quem indicou.
        Origem = p.Origem switch
        {
            null => "Não perguntado",
            OrigemPaciente.Indicacao when !string.IsNullOrWhiteSpace(p.IndicadoPor)
                => $"Indicação de {p.IndicadoPor}",
            var o => RotulosEnum.De(o)
        };

        var codigos = p.Atendimentos.SelectMany(a => a.Codigos).ToList();
        TotalSessoes = p.Atendimentos.Count.ToString();
        GuiasEmAberto = codigos.Count(c => c.DataBaixa is null).ToString();
        UltimaSessao = p.Atendimentos.Count == 0
            ? "—"
            : p.Atendimentos.Max(a => a.Data).ToString("dd/MM/yyyy");
    }

    private static int Idade(DateOnly nascimento)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var idade = hoje.Year - nascimento.Year;
        if (nascimento > hoje.AddYears(-idade)) idade--;
        return idade;
    }

    private async Task CarregarProntuarioAsync(IServiceScope scope, int pacienteId, int geracao)
    {
        // ⚠️ NEM LER NEM DESENHAR (art. 5º, II). Sem esta linha, os perfis que só têm
        // `VerFichaPaciente` — Recepção, Financeiro, Faturista — carregavam a evolução, a
        // EVA e a curva de dor de qualquer paciente. A aba já some pelo XAML; carregar
        // assim mesmo seria manter o dado sensível na memória de quem não pode vê-lo.
        if (!SessaoUsuario.Atual.Pode(Permissao.VerProntuario))
        {
            await LinhaDoTempo.CarregarAsync(0);
            return;
        }

        // O prontuário inteiro — sessão médica, enfermagem e infusão — sai do componente
        // compartilhado (parcela 72). Ele tem o próprio contador de geração e o próprio
        // filtro de acesso por natureza.
        await LinhaDoTempo.CarregarAsync(pacienteId);
        if (geracao != _geracaoCarga) return;

        // A curva de dor é da aba VISÃO GERAL, não desta: ela responde "o tratamento está
        // funcionando?", que é outra pergunta.
        var dor = await scope.ServiceProvider.GetRequiredService<ProntuarioService>()
            .EvolucaoDaDorAsync(pacienteId);
        if (geracao != _geracaoCarga) return;
        AplicarEvolucaoDaDor(dor);
    }

    /// <summary>
    /// Registra uma passagem pela enfermagem que NÃO veio de folha de infusão — curativo,
    /// sala de observação, triagem. Todo paciente passa pela enfermagem, e a maioria dessas
    /// passagens não tem folha nenhuma.
    /// </summary>
    [RelayCommand]
    private async Task RegistrarEnfermagemAsync()
    {
        if (PacienteId == 0)
        {
            // A guarda DIZ por que não dá (a lição da parcela 41), em vez de voltar calada.
            Mensagem = "Escolha um paciente para registrar a evolução de enfermagem.";
            MensagemEhErro = true;
            return;
        }

        SessaoUsuario.Atual.Exigir(
            Permissao.RegistrarEvolucaoEnfermagem, "registrar evolução de enfermagem");

        Clinica.Desktop.Shell.Componentes.EvolucaoEnfermagemWindow.Abrir(
            _escopos, _dialogo, PacienteId, Nome);

        await CarregarAsync();
    }

    private void AplicarEvolucaoDaDor(EvolucaoDaDor dor)
    {
        SemMedidaEva = dor.SessoesComMedida == 0;

        if (SemMedidaEva)
        {
            DorInicial = DorAtual = GanhoAcumulado = AlivioMedio = "—";
            ResumoEva = dor.SessoesRegistradas == 0
                ? "Nenhuma sessão registrada no prontuário ainda."
                : $"{dor.SessoesRegistradas} sessão(ões) registradas, nenhuma com o par EVA "
                  + "(antes e depois). Sem o par não dá para dizer se a dor melhorou.";
            return;
        }

        DorInicial = $"{dor.DorInicial}/10";
        DorAtual = $"{dor.DorAtual}/10";
        GanhoAcumulado = dor.GanhoAcumulado is { } ganho
            ? (ganho >= 0 ? $"−{ganho} pontos" : $"+{-ganho} pontos")
            : "—";
        AlivioMedio = $"{dor.AlivioMedioPorSessao:0.#} por sessão";
        ResumoEva = $"{dor.SessoesComMedida} de {dor.SessoesRegistradas} sessão(ões) com EVA medida.";
    }

    /// <summary>
    /// Padrão de falta do paciente. Isolado do resto: se falhar, a ficha continua
    /// mostrando tudo o mais — o histórico de falta é informação de apoio, não a razão
    /// de a tela existir.
    ///
    /// Cancelamento avisado aparece SEPARADO das faltas: quem desmarcou deu à clínica a
    /// chance de reocupar o horário, e misturar os dois esconderia exatamente o
    /// comportamento que o número existe para mostrar.
    /// </summary>
    private async Task CarregarFaltasAsync(IServiceScope scope, int pacienteId, int geracao)
    {
        try
        {
            var servico = scope.ServiceProvider.GetRequiredService<RelacionamentoService>();
            var h = await servico.FaltasDoPacienteAsync(pacienteId);
            if (geracao != _geracaoCarga) return;

            Faltas = h.Faltas == 0
                ? "nenhuma"
                : h.TaxaPercentual is { } taxa
                    ? $"{h.Faltas} ({taxa:0.#}%)"
                    : h.Faltas.ToString();

            Cancelamentos = h.Cancelamentos == 0 ? "nenhum" : h.Cancelamentos.ToString();
            FaltaReincidente = h.Reincidente;

            AvisoFaltas = h.Reincidente
                ? $"Faltou {h.Faltas} vez(es)"
                  + (h.UltimaFalta is { } ultima ? $", a última em {ultima:dd/MM/yyyy}" : string.Empty)
                  + ". Vale confirmar a sessão na véspera antes de dar a ele um horário disputado."
                : string.Empty;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — histórico de falta não pôde ser lido", ex);
            if (geracao != _geracaoCarga) return;
            // Terceiro estado: "—" é diferente de "nenhuma falta". Dizer que o paciente
            // nunca faltou por causa de uma consulta quebrada seria falha exibida como
            // sucesso — e neste caso a favor de quem falta.
            Faltas = "não verificado";
            Cancelamentos = "não verificado";
            FaltaReincidente = false;
            AvisoFaltas = string.Empty;
        }
    }

    /// <summary>
    /// Os próximos horários AGENDADOS do paciente (set/2026), com teto. Isolado como o
    /// padrão de falta: falhar aqui não derruba a ficha, e vira o terceiro estado — dizer
    /// "nenhum horário" por causa de uma leitura quebrada mandaria a recepcionista marcar
    /// por cima de uma sessão que existe.
    ///
    /// Só quem tem <c>VerAgenda</c> lê: horário é dado da agenda, e a ficha abre para quem
    /// só tem a ficha. Sem o bit, a região nem é desenhada (<see cref="PodeVerAgenda"/>).
    /// </summary>
    private async Task CarregarProximosHorariosAsync(IServiceScope scope, int pacienteId, int geracao)
    {
        if (!PodeVerAgenda) return;
        try
        {
            var repo = scope.ServiceProvider.GetRequiredService<Clinica.Application.Abstracoes.IClinicaRepositorio>();
            var horarios = await repo.AgendamentosFuturosDoPacienteAsync(
                pacienteId, DateTime.Now, TetoDeProximosHorarios);
            if (geracao != _geracaoCarga) return;

            var linhas = horarios.Select(LinhaHorarioFuturo.De).ToList();
            ProximosHorarios.Clear();
            foreach (var l in linhas) ProximosHorarios.Add(l);
            _proximosHorariosLidos = true;
            ProximosHorariosNaoVerificados = false;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — próximos horários do paciente não puderam ser lidos", ex);
            if (geracao != _geracaoCarga) return;
            ProximosHorarios.Clear();
            _proximosHorariosLidos = true;
            ProximosHorariosNaoVerificados = true;
        }
        finally
        {
            if (geracao == _geracaoCarga) OnPropertyChanged(nameof(SemProximosHorarios));
        }
    }

    /// <summary>
    /// Abre a Agenda no DIA daquele horário. A Agenda é montada uma vez pelo shell, então
    /// o dia viaja por <see cref="PedidoAgenda"/> e é lido quando ela aparece.
    /// </summary>
    [RelayCommand]
    private void AbrirNaAgenda(LinhaHorarioFuturo? linha)
    {
        if (linha is null) return;
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.VerAgenda, "abrir a agenda");
            using var scope = _escopos.CreateScope();
            scope.ServiceProvider.GetRequiredService<PedidoAgenda>().AbrirEm(linha.Dia);
            if (!Clinica.Desktop.Shell.Modulos.NavegacaoSuite.Ir(Modulo.ModuloRecepcao.ChaveAgenda))
            {
                Mensagem = "A agenda não está disponível para este usuário.";
                MensagemEhErro = true;
            }
        }
        catch (Exception ex)
        {
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// As senhas do convênio deste paciente, com o saldo apurado (parcela 48).
    ///
    /// O consumo NÃO é digitado: o <see cref="AutorizacaoService"/> conta os atendimentos
    /// dentro da vigência de cada senha. É por isso que a coluna "usadas" muda sozinha
    /// quando o balcão finaliza uma sessão.
    /// </summary>
    private async Task CarregarAutorizacoesAsync(IServiceScope scope, int pacienteId, int geracao)
    {
        try
        {
            AutorizacoesNaoVerificadas = false;

            var servico = scope.ServiceProvider.GetRequiredService<AutorizacaoService>();
            var saldos = await servico.SaldosAsync(
                pacienteId, DateOnly.FromDateTime(DateTime.Today));
            if (geracao != _geracaoCarga) return;

            Autorizacoes.Clear();
            foreach (var saldo in saldos)
                Autorizacoes.Add(saldo);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — autorizações do paciente não puderam ser lidas", ex);
            if (geracao != _geracaoCarga) return;
            // Terceiro estado. Lista vazia aqui se lê como "este paciente não tem senha
            // nenhuma", que é exatamente a conclusão oposta à verdadeira quando a
            // consulta falhou — e leva alguém a marcar dez sessões sem cota.
            AutorizacoesNaoVerificadas = true;
        }
    }

    /// <summary>Registra a senha que o convênio liberou.</summary>
    [RelayCommand]
    private async Task NovaAutorizacaoAsync() => await AbrirAutorizacaoAsync(null);

    /// <summary>Corrige uma senha já registrada (quantidade, validade, encerramento).</summary>
    [RelayCommand]
    private async Task EditarAutorizacaoAsync(SaldoAutorizacao? linha)
        => await AbrirAutorizacaoAsync(linha?.Autorizacao.Id);

    private async Task AbrirAutorizacaoAsync(int? autorizacaoId)
    {
        // ⚠️ `EditarPaciente`, e não `EditarProntuario`: a autorização é a COTA de sessões
        // do convênio — dado administrativo, do lado cadastral do corte da parcela 49.
        // Com o bit do prontuário aqui, o perfil Recepção (que não o tem) via o botão
        // ACESO — o XAML acende com PodeEditarCadastro — clicava e levava recusa, no
        // fluxo que a parcela 48 construiu justamente para o balcão ("quem recebe a senha
        // da operadora é quem atende o telefone"). A janela que ele abre sempre gravou com
        // `EditarPaciente`: as duas barreiras discordavam entre si.
        SessaoUsuario.Atual.Exigir(Permissao.EditarPaciente, "registrar autorização");

        if (PacienteId == 0)
        {
            Mensagem = "Escolha um paciente antes de registrar a autorização.";
            MensagemEhErro = true;
            return;
        }

        var vm = new AutorizacaoEdicaoViewModel(_escopos, PacienteId);
        await vm.CarregarAsync(autorizacaoId, _convenioCodigo);

        var janela = new Janelas.AutorizacaoWindow(vm)
        {
            Owner = JanelaDona.Atual()
        };

        if (janela.ShowDialog() == true)
        {
            _snackbar.Sucesso("Autorização registrada.");
            await CarregarAsync();
        }
    }

    /// <summary>
    /// Apaga uma senha registrada. Ela não é registro do que aconteceu — é o que o
    /// convênio liberou —, então se apaga mesmo, como o modelo e o protocolo da parcela
    /// 25. O que ficou gravado dos atendimentos não muda.
    /// </summary>
    [RelayCommand]
    private async Task ExcluirAutorizacaoAsync(SaldoAutorizacao? linha)
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarPaciente, "excluir autorização");

        if (linha is null) return;

        if (!_dialogo.ConfirmarPerigo(
                "Excluir autorização",
                $"Apagar a senha {linha.Autorizacao.Numero ?? "(sem número)"}? "
                + "Os atendimentos já lançados não mudam — o que se perde é o controle de cota."))
            return;

        try
        {
            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<AutorizacaoService>();
            await servico.RemoverAsync(linha.Autorizacao.Id, SessaoUsuario.Atual.Operador);

            _snackbar.Sucesso("Autorização excluída.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — autorização não pôde ser excluída", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    private async Task CarregarConsentimentosAsync(IServiceScope scope, int pacienteId, int geracao)
    {
        var servico = scope.ServiceProvider.GetRequiredService<ConsentimentoService>();
        var situacao = await servico.SituacaoAsync(pacienteId);
        if (geracao != _geracaoCarga) return;

        Consentimentos.Clear();
        foreach (var finalidade in ConsentimentoService.Finalidades)
        {
            situacao.TryGetValue(finalidade, out var atual);

            Consentimentos.Add(new LinhaConsentimento
            {
                Finalidade = finalidade,
                Rotulo = ConsentimentoService.Rotular(finalidade),
                Situacao = Descrever(atual),
                Vigente = atual?.Vigente == true,
                PodeRevogar = atual?.Vigente == true,
                RegistroId = atual?.Id
            });
        }
    }

    /// <summary>
    /// Fecha o ciclo do marketing: a campanha sai no Gerente e o resultado dela aparece
    /// aqui, no balcão, onde alguém vai atender essa pessoa e precisa saber o que já foi
    /// falado com ela. Falhar aqui não derruba a ficha — é histórico, não o conteúdo.
    /// </summary>
    private async Task CarregarCrmAsync(IServiceScope scope, int pacienteId, int geracao)
    {
        try
        {
            var campanhas = scope.ServiceProvider.GetRequiredService<CampanhaService>();
            var contatos = await campanhas.DoPacienteAsync(pacienteId);
            if (geracao != _geracaoCarga) return;

            Contatos.Clear();
            foreach (var c in contatos)
                Contatos.Add(LinhaContato.De(c));
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — histórico de campanhas não pôde ser lido", ex);
        }
    }

    /// <summary>
    /// Os termos que o procedimento de HOJE exige (parcela 66, 2ª rodada).
    ///
    /// Lê pelo MESMO <see cref="TermoProcedimentoService"/> da fila — nunca uma segunda
    /// consulta escrita aqui. Duas definições de "falta assinar" divergiriam na primeira
    /// correção, e a que ninguém lembraria de ajustar é a que a ficha mostra: o erro
    /// apareceria como seção limpa, indistinguível de termo em dia.
    ///
    /// Falhar aqui NÃO derruba a ficha, como o histórico de campanhas acima: é uma seção
    /// da aba, e o paciente na frente do balcão precisa da ficha inteira. Mas também não
    /// passa calado — vai para o log, senão a clínica acredita estar coberta e não está.
    /// </summary>
    private async Task CarregarTermosAsync(IServiceScope scope, int pacienteId, int geracao)
    {
        // ⚠️ LIMPA ANTES de ir ao banco. Sem isto, uma falha na leitura deixaria as linhas
        // do paciente ANTERIOR na tela — com o nome do novo no cabeçalho e o botão
        // "Colher assinatura…" aceso apontando para o `DocumentoId` do outro. Um clique
        // assinaria o termo de quem já saiu, em nome de quem está na frente. É o pior
        // desfecho possível para uma tela que existe para provar consentimento.
        Termos.Clear();
        TermosNaoVerificados = false;
        OnPropertyChanged(nameof(TemTermoDoDia));
        OnPropertyChanged(nameof(ResumoTermos));

        // Dado de SAÚDE (parcela 59): o termo diz qual procedimento a pessoa vai fazer e o
        // que ela declarou sobre o próprio corpo. Os perfis Financeiro e Faturista têm
        // `VerFichaPaciente` e abrem esta ficha — a lista de documentos abaixo já os filtra
        // pelo acesso de cada papel, e a seção precisa da mesma barreira. Nem ler nem
        // desenhar: `TemTermoDoDia` fica falso e a região SOME.
        if (!SessaoUsuario.Atual.Pode(Permissao.VerProntuario)) return;

        try
        {
            var servico = scope.ServiceProvider.GetRequiredService<TermoProcedimentoService>();

            var situacoes = await servico.SituacaoDoDiaAsync(
                pacienteId, DateOnly.FromDateTime(DateTime.Today));

            if (geracao != _geracaoCarga) return;

            // Monta em lista local e só então publica: entre o Clear() e o último Add não
            // pode haver await, senão duas cargas se intercalam na mesma coleção.
            var linhas = situacoes.Select(LinhaTermo.De).ToList();

            Termos.Clear();
            foreach (var l in linhas) Termos.Add(l);

            OnPropertyChanged(nameof(TemTermoDoDia));
            OnPropertyChanged(nameof(ResumoTermos));
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            // TERCEIRO ESTADO, nunca lista vazia silenciosa: "não há termo" e "não deu para
            // conferir" são respostas diferentes, e a segunda não pode ser lida como a
            // primeira num papel que o procedimento exige.
            TermosNaoVerificados = true;
            OnPropertyChanged(nameof(ResumoTermos));

            Clinica.Application.Diagnostico.Registrar(
                "Recepção — termos do dia não puderam ser conferidos na ficha", ex);
        }
    }

    private async Task CarregarDocumentosAsync(IServiceScope scope, int pacienteId, int geracao)
    {
        var servico = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();

        // ⚠️ LIMPA ANTES de ir ao banco, como a carga dos termos do dia logo abaixo. Uma
        // falha na leitura deixaria as linhas do paciente ANTERIOR na tela, com o nome do
        // novo no cabeçalho — e o botão "Colher assinatura…" apontando para o
        // `TermoLgpdPendenteId` do outro. Um clique assinaria o termo de quem já saiu, em
        // nome de quem está na frente (a lição da parcela 66, 2ª rodada).
        Documentos.Clear();
        TermoLgpdPendenteId = null;
        OnPropertyChanged(nameof(TermoLgpdPendenteId));
        TermoLgpdSituacao = "Conferindo…";

        var doPaciente = await servico.DoPacienteAsync(pacienteId);
        if (geracao != _geracaoCarga) return;

        // Só os papéis que o acesso desta pessoa alcança (parcela 59). A ficha é a porta
        // que o balcão abre o dia inteiro, e listar "Receituário 2026/0012" aqui contaria
        // a existência da receita a quem não pode lê-la — que é metade do que a direção
        // pediu para fechar.
        Documentos.Clear();
        foreach (var d in doPaciente)
        {
            var linha = LinhaDocumento.De(d);
            if (SessaoUsuario.Atual.Pode(linha.AcessoParaVer)) Documentos.Add(linha);
        }

        // O termo LGPD sai da mesma leitura, mas precisa de UMA pergunta a mais: quais
        // deles carregam as finalidades (parcela 89, 2ª rodada). A resposta não vem daqui
        // porque `DocumentosDoPacienteAsync` não traz os itens de propósito.
        var comFinalidade = await servico.TermosLgpdComFinalidadeAsync(pacienteId);
        if (geracao != _geracaoCarga) return;

        AplicarTermoLgpd(doPaciente, comFinalidade);
    }

    /// <summary>
    /// A situação do termo LGPD, derivada dos documentos do paciente.
    ///
    /// ⚠️ O CANCELADO não conta como assinado nem segura a coleta: cancelar um termo é
    /// justamente o gesto que pede outro. E o pendente é o MAIS RECENTE em aberto — se
    /// houver mais de um (dois cliques em máquinas diferentes), reaproveitar o antigo
    /// deixaria o novo pendente para sempre na tela.
    ///
    /// ⚠️ SÓ os termos que carregam FINALIDADE contam, nas DUAS pontas (parcela 89, 2ª
    /// rodada — o defeito que a clínica encontrou):
    ///
    /// - como PENDENTE, porque reaproveitar um termo da versão anterior levava o paciente
    ///   a assinar um papel que não registra consentimento nenhum;
    /// - como ASSINADO, porque escrever "Assinado em 26/08" sobre um papel desses seria a
    ///   tela AFIRMANDO que a manifestação existe enquanto o alerta "sem consentimento
    ///   LGPD" continua aceso no balcão. Duas verdades sobre o mesmo fato.
    /// </summary>
    /// <param name="comFinalidade">
    /// Os ids dos termos cujos itens carregam a finalidade. Vem de uma consulta própria: a
    /// leitura dos documentos não traz os itens, e decidir por uma navegação vazia faria
    /// TODO termo — o novo inclusive — parecer da versão anterior, com o teste passando
    /// pelo fixup do EF (a lição da parcela 68).
    /// </param>
    private void AplicarTermoLgpd(
        IReadOnlyList<DocumentoClinico> doPaciente, IReadOnlyList<int> comFinalidade)
    {
        var respondiveis = comFinalidade.ToHashSet();

        var termos = doPaciente
            .Where(d => d.Tipo == TipoDocumentoClinico.Consentimento)
            .ToList();

        var assinado = termos
            .Where(d => d.PacienteAssinou && !d.Cancelado && respondiveis.Contains(d.Id))
            .OrderByDescending(d => d.PacienteAssinadoEm)
            .FirstOrDefault();

        var pendente = termos
            .Where(d => d.AguardaAssinaturaDoPaciente && respondiveis.Contains(d.Id))
            .OrderByDescending(d => d.Id)
            .FirstOrDefault();

        // Um termo da versão anterior que alguém já assinou. Não vale como manifestação, e
        // a tela DIZ isso: sumir com ele faria a recepcionista jurar que colheu — ela
        // colheu mesmo, num papel que não registra.
        var antigoAssinado = assinado is null
                             && termos.Any(d => d.PacienteAssinou && !d.Cancelado
                                                && !respondiveis.Contains(d.Id));

        TermoLgpdPendenteId = pendente?.Id;
        OnPropertyChanged(nameof(TermoLgpdPendenteId));
        OnPropertyChanged(nameof(PodeColherTermoLgpd));

        TermoLgpdSituacao = pendente is not null
            ? $"Termo {pendente.Numero} emitido em {pendente.Data:dd/MM/yyyy} e ainda NÃO assinado."
            : assinado is not null
                ? $"Assinado em {assinado.PacienteAssinadoEm:dd/MM/yyyy 'às' HH:mm} "
                  + $"(termo {assinado.Numero})."
                : antigoAssinado
                    ? "Há termo assinado de uma versão anterior do sistema — ele não traz as "
                      + "respostas por finalidade e NÃO vale como manifestação do titular. "
                      + "Colha um novo."
                    // ⚠️ A frase fala do TERMO, e só dele. Dizer aqui que "as finalidades
                    // continuam sem manifestação" seria FALSO para a base que já existe:
                    // os consentimentos colhidos pela caixinha antiga estão gravados, e a
                    // lista abaixo os mostra com a data. Uma afirmação por pergunta.
                    : "Nenhum termo assinado por este paciente.";
    }

    /// <summary>
    /// A situação de uma finalidade, com a PROCEDÊNCIA (parcela 89).
    ///
    /// O número do termo entra na frase porque é ele que responde "onde está a prova?".
    /// Sem isso a linha diria "Concedido em 12/03" e a auditoria continuaria tendo de
    /// acreditar na palavra de quem clicou — que é justamente o que a assinatura veio
    /// resolver. Registro anterior à parcela não tem termo, e a frase simplesmente não
    /// afirma um que não existe.
    /// </summary>
    private static string Descrever(ConsentimentoLgpd? registro)
    {
        if (registro is null) return "Nunca perguntado";

        var origem = string.IsNullOrWhiteSpace(registro.VersaoTermo)
            ? string.Empty
            : $" · termo {registro.VersaoTermo}";

        return registro switch
        {
            { RevogadoEm: { } revogado } => $"Revogado em {revogado:dd/MM/yyyy}{origem}",
            { Concedido: true } r => $"Concedido em {r.RegistradoEm:dd/MM/yyyy}{origem}",
            var r => $"Recusado em {r.RegistradoEm:dd/MM/yyyy}{origem}"
        };
    }

    /// <summary>
    /// Conferência de elegibilidade — isolada de propósito: se ela falhar, a ficha
    /// continua abrindo e a tela diz que NÃO conseguiu conferir.
    /// </summary>
    private async Task CarregarElegibilidadeAsync(IServiceScope scope, int pacienteId, int geracao)
    {
        try
        {
            var servico = scope.ServiceProvider.GetRequiredService<ElegibilidadeService>();
            var resultado = await servico.ConferirAsync(pacienteId, DateOnly.FromDateTime(DateTime.Today));
            if (geracao != _geracaoCarga) return;

            Alertas.Clear();
            foreach (var a in resultado.Alertas)
                Alertas.Add(new LinhaAlerta
                {
                    Descricao = a.Descricao,
                    EhVermelho = a.Urgencia == NivelUrgencia.Vermelho
                });

            ElegibilidadeNaoVerificada = false;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — elegibilidade não pôde ser conferida", ex);
            if (geracao != _geracaoCarga) return;
            Alertas.Clear();
            ElegibilidadeNaoVerificada = true;
        }
    }

    // ==================== Comandos ====================

    [RelayCommand]
    private async Task EditarAsync()
    {
        // O que esta janela edita é o CADASTRO (contato), não o prontuário — o bit é o
        // da parcela 49. Com EditarProntuario aqui, a barreira NEGAVA ao balcão o
        // cadastro que ele deve editar, com o botão aceso pelo bit certo ao lado.
        SessaoUsuario.Atual.Exigir(Permissao.EditarPaciente, "editar o cadastro do paciente");

        if (PacienteId == 0) return;

        var vm = new PacienteEdicaoViewModel(_escopos, PacienteId);
        var janela = new Janelas.PacienteWindow(vm)
        {
            Owner = JanelaDona.Atual()
        };

        if (janela.ShowDialog() != true) return;
        _snackbar.Sucesso("Cadastro atualizado.");
        await CarregarAsync();
        Alterou?.Invoke();
    }

    [RelayCommand]
    private async Task NovaEvolucaoAsync() => await AbrirEvolucaoAsync(null);


    /// <summary>
    /// Cancela a sessão do prontuário — nunca apaga (Lei 13.787/2018, guarda de 20 anos).
    ///
    /// ⚠️ O QUE ESTAVA ERRADO AQUI, e derrubava dois pontos do compromisso de conformidade
    /// (parcela 72). A chamada era
    /// <c>CancelarAsync(linha.EvolucaoId, SessaoUsuario.Atual.Operador)</c> —
    /// <b>POSICIONAL</b>, com o login caindo na vaga do MOTIVO. Compilava porque os dois
    /// são <c>string</c>, e o resultado era: <c>MotivoCancelamento = "ana.silva"</c>,
    /// <c>CanceladaPor = null</c> e auditoria com <c>Operador = "?"</c>. E como
    /// <c>SessaoUsuario.Operador</c> nunca é vazio (cai em <c>Environment.UserName</c>), a
    /// única recusa do serviço — "diga por que a sessão está sendo cancelada" — NUNCA
    /// disparava. Ponto 1 (cancela-se com motivo obrigatório) e ponto 6 (quem assina é quem
    /// fez login), os dois de pé só no papel.
    ///
    /// E o rótulo mentia junto: botão "Excluir", diálogo "APAGAR a sessão? os anexos dela
    /// vão junto" e snackbar "sessão EXCLUÍDA" — nada é apagado desde a parcela 52, e os
    /// anexos não vão a lugar nenhum. A tela irmã do mesmo módulo (<c>ProntuarioViewModel</c>)
    /// já fazia certo; esta é a cópia que ficou para trás.
    /// </summary>
    private async Task CancelarSessaoAsync(RegistroClinicoPaciente item)
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            var motivo = _dialogo.PerguntarTexto(
                "Cancelar sessão do prontuário",
                $"Por que a sessão de {item.Data:dd/MM/yyyy} está sendo cancelada? Ela NÃO é "
                + "apagada — sai do prontuário que se lê e fica guardada, com este motivo "
                + "ao lado.");
            if (string.IsNullOrWhiteSpace(motivo)) return;

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();
            await prontuario.CancelarAsync(item.Id, motivo, SessaoUsuario.Atual.Operador);

            _snackbar.Info("Sessão cancelada (guardada no prontuário).");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — evolução não pôde ser cancelada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    private async Task AbrirEvolucaoAsync(int? evolucaoId)
    {
        // ⚠️ `Exigir` LANÇA, e este método é chamado de dois lugares — inclusive do
        // componente da linha do tempo, cujo comando não tem try. Fora do try, a recusa
        // sobe até a rede do Dispatcher em vez de virar a frase que explica: é o mesmo
        // defeito que a parcela 72 corrigiu nos botões desta tela, uma camada abaixo.
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");
            if (PacienteId == 0) return;

            var vm = new EvolucaoEdicaoViewModel(_escopos, PacienteId, evolucaoId);
            var janela = new Janelas.EvolucaoWindow(vm)
            {
                Owner = JanelaDona.Atual()
            };

            if (janela.ShowDialog() != true) return;
            _snackbar.Sucesso("Prontuário atualizado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — sessão do prontuário não pôde ser aberta", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Colhe o TERMO LGPD assinado pelo paciente (parcela 89) — o único caminho para
    /// conceder ou recusar as quatro finalidades.
    ///
    /// ⚠️ Até aqui o balcão marcava "Concedeu"/"Recusou" em quatro pares de botões, sem
    /// assinatura nenhuma: o sistema afirmava, para uma auditoria, que o titular havia
    /// consentido — e a única prova era a palavra de quem clicou. O art. 8º da LGPD pede
    /// manifestação do TITULAR, e o §2º põe o ônus da prova em quem trata o dado.
    ///
    /// A janela é a MESMA do termo de procedimento, e ela já oferece as duas formas: o
    /// traço na tela do balcão (ou na segunda tela, quando a clínica tem o monitor) e o
    /// envio do link pelo WhatsApp, que volta assinado do celular do paciente. Uma porta
    /// só, porque quem escolhe a forma é quem está com a pessoa na frente.
    /// </summary>
    [RelayCommand]
    private async Task ColherTermoLgpdAsync()
    {
        // A barreira que IMPEDE, e ela DIZ por que recusou (a lição da parcela 41).
        if (!SessaoUsuario.Atual.Pode(Permissao.ColherAssinaturaPaciente))
        {
            MensagemEhErro = true;
            Mensagem = "Você não tem permissão para colher a assinatura do paciente. "
                       + "Peça à direção o acesso \"Colher assinatura do paciente\".";
            return;
        }

        if (PacienteId == 0)
        {
            MensagemEhErro = true;
            Mensagem = "Abra a ficha de um paciente antes de colher o termo.";
            return;
        }

        try
        {
            var concluiu = Clinica.Desktop.Shell.Componentes.ColetaDeTermo.AbrirConsentimentoLgpd(
                // Reaproveita o termo já emitido e não assinado, quando existe: emitir
                // outro deixaria dois papéis do mesmo ato com números diferentes.
                _escopos, PacienteId, Nome, TermoLgpdPendenteId);

            // Recarrega mesmo sem concluir: abrir a janela já EMITE o termo numerado.
            await CarregarAsync();

            if (concluiu) _snackbar.Sucesso("Termo de consentimento assinado.");
        }
        catch (Exception ex)
        {
            MensagemEhErro = true;
            Mensagem = $"Não foi possível abrir o termo de consentimento: {ex.Message}";
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — coleta do termo LGPD pela ficha", ex);
        }
    }

    [RelayCommand]
    private async Task RevogarAsync(LinhaConsentimento? linha)
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarPaciente, "revogar consentimento");

        if (linha?.RegistroId is not { } registroId) return;

        var motivo = _dialogo.PerguntarTexto(
            "Revogar consentimento",
            $"Por que \"{linha.Rotulo}\" está sendo revogado? O registro anterior NÃO é apagado — "
            + "ele continua provando o consentimento do período já tratado.");
        if (string.IsNullOrWhiteSpace(motivo)) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<ConsentimentoService>();
            await servico.RevogarAsync(registroId, SessaoUsuario.Atual.Operador, motivo);
            _snackbar.Info("Consentimento revogado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — consentimento não pôde ser revogado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    // ==================== Documentos clínicos ====================

    /// <summary>Abre a janela de emissão (receita, atestado, declaração, pedido de exame).</summary>
    [RelayCommand]
    private async Task NovoDocumentoAsync()
    {
        // A janela oferece receita, atestado, declaração e pedido de exame — três dos
        // quatro mandam alguém tomar ou fazer alguma coisa. `EditarPaciente` (o bit do
        // CADASTRO) autorizava todos eles até a parcela 59.
        SessaoUsuario.Atual.Exigir(Permissao.Prescrever, "emitir documento clínico");

        if (PacienteId == 0) return;

        var vm = new DocumentoEdicaoViewModel(_escopos, PacienteId);
        var janela = new Clinica.Desktop.Shell.Componentes.DocumentoWindow(vm)
        {
            Owner = JanelaDona.Atual()
        };

        if (janela.ShowDialog() != true)
        {
            // Mesmo quando o usuário fecha sem concluir, um documento pode ter sido
            // emitido e só a impressão ter falhado — a lista precisa refletir isso.
            await CarregarAsync();
            return;
        }

        _snackbar.Sucesso("Documento emitido.");
        await CarregarAsync();
    }

    /// <summary>
    /// Colhe a assinatura do paciente no termo do procedimento de hoje (parcela 66, 2ª
    /// rodada) — a porta que o cliente pediu dentro da ficha.
    ///
    /// A janela é a MESMA da fila (<c>AssinaturaPacienteWindow</c>, no shell): duas telas
    /// para o mesmo ato divergem na primeira correção, e o que elas colhem é a prova de
    /// que o paciente consentiu.
    ///
    /// Reaproveita o documento já emitido quando ele existe (<c>DocumentoId</c>): o termo
    /// apresentado e não assinado é um fato, e emitir outro deixaria dois papéis do mesmo
    /// ato com números diferentes.
    /// </summary>
    [RelayCommand]
    private async Task ColherTermoAsync(LinhaTermo? linha)
    {
        // Guarda sobre PARÂMETRO: vindo de botão de linha ela nunca dispara, e é a exceção
        // que a checagem 21 reconhece.
        if (linha is null) return;

        // A barreira que IMPEDE, e ela DIZ por que recusou (a lição da parcela 41).
        if (!SessaoUsuario.Atual.Pode(Permissao.ColherAssinaturaPaciente))
        {
            MensagemEhErro = true;
            Mensagem = "Você não tem permissão para colher a assinatura do paciente. "
                       + "Peça à direção o acesso \"Colher assinatura do paciente\".";
            return;
        }

        if (!linha.Pendente)
        {
            MensagemEhErro = true;
            Mensagem = "Este termo já foi resolvido hoje.";
            return;
        }

        await AbrirColetaAsync(
            linha.ModeloId, linha.DocumentoId,
            // O profissional do horário de hoje, quando a linha o conhece: sem ele o termo
            // nasce órfão e a via impressa sai sem o nome e o CRM de quem faz o procedimento.
            linha.ProfissionalId);
    }

    /// <summary>
    /// Colher um termo AVULSO — sem procedimento marcado para hoje (parcela 66, 3ª rodada).
    ///
    /// É a porta que a cliente pediu: o paciente aparece para tirar dúvidas, ou passa no
    /// balcão, e a assinatura se colhe ali. O termo vale a partir da assinatura, então não
    /// há por que esperar o dia — e o dia é justamente quando ninguém tem tempo de ler.
    /// </summary>
    [RelayCommand]
    private async Task ColherTermoAvulsoAsync() => await AbrirColetaAsync(null, null, null);

    /// <summary>
    /// O caminho ÚNICO da ficha para a coleta — as duas portas daqui e as outras três da
    /// suíte passam pelo mesmo <c>ColetaDeTermo.Abrir</c>.
    /// </summary>
    private async Task AbrirColetaAsync(int? modeloId, int? documentoId, int? profissionalId)
    {
        // A barreira que IMPEDE, e ela DIZ por que recusou (a lição da parcela 41).
        if (!SessaoUsuario.Atual.Pode(Permissao.ColherAssinaturaPaciente))
        {
            MensagemEhErro = true;
            Mensagem = "Você não tem permissão para colher a assinatura do paciente. "
                       + "Peça à direção o acesso \"Colher assinatura do paciente\".";
            return;
        }

        if (PacienteId == 0)
        {
            MensagemEhErro = true;
            Mensagem = "Abra a ficha de um paciente antes de colher o termo.";
            return;
        }

        try
        {
            var concluiu = Clinica.Desktop.Shell.Componentes.ColetaDeTermo.Abrir(
                _escopos, PacienteId, Nome, modeloId, documentoId, profissionalId);

            // Recarrega mesmo quando a janela foi fechada sem concluir: o termo pode ter
            // sido EMITIDO na abertura e só a assinatura ter faltado, e a seção precisa
            // refletir isso — é a mesma razão do NovoDocumentoAsync acima.
            await CarregarAsync();

            if (concluiu) _snackbar.Sucesso("Termo do procedimento resolvido.");
        }
        catch (Exception ex)
        {
            MensagemEhErro = true;
            Mensagem = $"Não foi possível abrir o termo: {ex.Message}";
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — coleta do termo pela ficha", ex);
        }
    }

    /// <summary>Relatório de evolução da dor, montado do prontuário.</summary>
    [RelayCommand]
    private Task EmitirRelatorioAsync()
        => EmitirMontadoAsync(TipoDocumentoClinico.RelatorioEvolucao);

    /// <summary>Anamnese: preenchida com o que o prontuário sabe, em linhas com o resto.</summary>
    [RelayCommand]
    private Task EmitirAnamneseAsync()
        => EmitirMontadoAsync(TipoDocumentoClinico.Anamnese);

    private async Task EmitirMontadoAsync(TipoDocumentoClinico tipo)
    {
        // Os dois montados saem do PRONTUÁRIO. O termo LGPD saiu desta lista na parcela
        // 89: ele passou a ser assinado pelo paciente, e a porta dele é a aba LGPD.
        SessaoUsuario.Atual.Exigir(
            CentralDocumentosService.AcessoParaEmitir(tipo),
            $"emitir {TipoDocumentoInfo.Rotular(tipo).ToLowerInvariant()}");

        if (PacienteId == 0) return;

        try
        {
            DocumentoClinico emitido;
            using (var scope = _escopos.CreateScope())
            {
                var servico = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();
                var operador = SessaoUsuario.Atual.Operador;

                emitido = tipo switch
                {
                    TipoDocumentoClinico.RelatorioEvolucao =>
                        await servico.EmitirRelatorioEvolucaoAsync(PacienteId, operador: operador),
                    _ => await servico.EmitirAnamneseAsync(PacienteId, operador: operador)
                };
            }

            await CarregarAsync();
            await ImprimirAsync(
                emitido.Id,
                $"{TipoDocumentoInfo.Rotular(tipo)}-{emitido.Numero.Replace('/', '-')}.pdf",
                emitido.Numero);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — documento montado do prontuário não pôde ser emitido", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>Segunda via: reimprime exatamente o que foi emitido.</summary>
    [RelayCommand]
    private async Task ImprimirDocumentoAsync(LinhaDocumento? linha)
    {
        if (linha is null) return;
        await ImprimirAsync(linha.DocumentoId, linha.NomeArquivo, linha.Numero);
    }

    private async Task ImprimirAsync(int documentoId, string nomeArquivo, string numero)
    {
        try
        {
            byte[] pdf;
            using (var scope = _escopos.CreateScope())
            {
                var pdfs = scope.ServiceProvider.GetRequiredService<DocumentosClinicosPdfService>();
                var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
                pdf = await pdfs.GerarAsync(documentoId, await parametros.ObterPrestadorAsync());
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                pdf, ImpressaoPdf.NomeSeguro(nomeArquivo));

            if (erro is null)
            {
                Mensagem = string.Empty;
                MensagemEhErro = false;
                return;
            }

            Mensagem = erro;
            MensagemEhErro = true;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — documento clínico não pôde ser impresso", ex);
            Mensagem = $"O documento {numero} está emitido, mas o PDF não pôde ser gerado: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Cancela um documento. Ele NÃO some da lista: a via que o paciente levou continua
    /// no mundo, e o registro é o que prova que ela não vale mais.
    /// </summary>
    /// <summary>
    /// ASSINAR e ENVIAR o documento, na ficha (parcela 72).
    ///
    /// ⚠️ <c>LinhaDocumento.PodeAssinar</c> e <c>PodeEnviar</c> eram calculados aqui e
    /// NUNCA lidos: a ficha imprimia e cancelava, e a tela irmã do MESMO módulo
    /// (<c>PrescricoesView</c>) tinha os quatro botões. Sem assinatura o arquivo não vale
    /// (art. 13 da Lei 14.063/2020, para o atestado; art. 14 para receita e pedido), e a
    /// porta incompleta é a que fica aberta com o paciente na frente — ele sai com o papel
    /// e o PDF assinado fica no computador da clínica.
    ///
    /// Os dois comandos são os MESMOS da tela irmã, e é de propósito: duas versões da
    /// assinatura divergiriam na primeira correção, e a que ninguém lembraria de ajustar é
    /// a que produz o arquivo com valor jurídico.
    /// </summary>
    [RelayCommand]
    private async Task AssinarDocumentoAsync(LinhaDocumento? linha)
    {
        if (linha is null) return;

        // Guarda de ESTADO: diz por que não dá, em vez de voltar calada.
        if (!linha.PodeAssinar)
        {
            Mensagem = linha.Cancelado
                ? $"O documento {linha.Numero} está cancelado e não pode ser assinado."
                : $"O documento {linha.Numero} já foi assinado digitalmente.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            // ⚠️ O bit do TIPO que está na linha, não o da porta (a regra da parcela 60).
            // Com `Prescrever` fixo, as duas barreiras DISCORDAVAM sobre que ato é aquele:
            // o botão acende por `AcessoParaMexer` — que é `EditarPaciente` na declaração
            // de comparecimento, `VerProntuario` no relatório de evolução e
            // `ColherAssinaturaPaciente` no termo —, e o comando exigia `Prescrever`. Em
            // quatro dos oito tipos isso é o corredor sem saída da parcela 69: a pessoa
            // atravessa a porta, faz o trabalho e leva a recusa no fim. E havia a fuga
            // oposta: quem tem `Prescrever` e não tem o bit do tipo passava direto — a
            // segunda barreira mais FROUXA que a primeira.
            SessaoUsuario.Atual.Exigir(linha.AcessoParaMexer, "assinar documento clínico");

            var certificado = EscolherCertificadoWindow.Perguntar(
                $"Assinar {linha.Tipo.ToLowerInvariant()} {linha.Numero}",
                System.Windows.Application.Current?.MainWindow, _escopos);

            if (certificado is null) return;   // diálogo cancelado: sair calado é o certo

            DocumentoAssinado assinado;
            using (var scope = _escopos.CreateScope())
            {
                var assinaturas = scope.ServiceProvider
                    .GetRequiredService<AssinaturaDeDocumentoClinicoService>();

                assinado = await assinaturas.AssinarAsync(
                    linha.DocumentoId, certificado,
                    SessaoUsuario.Atual.Autenticado ? SessaoUsuario.Atual.UsuarioId : null,
                    SessaoUsuario.Atual.Operador);
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                assinado.Pdf, ImpressaoPdf.NomeSeguro(assinado.NomeArquivo));

            if (erro is not null)
            {
                Mensagem = $"{erro} O documento foi assinado e está guardado no sistema.";
                MensagemEhErro = true;
            }
            else
            {
                _snackbar.Sucesso("Documento assinado. Entregue o ARQUIVO ao paciente.");
                Mensagem = null;
                MensagemEhErro = false;
            }

            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — documento não pôde ser assinado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Entrega o ARQUIVO assinado ao paciente pelo WhatsApp (parcela 43, 2ª rodada).
    ///
    /// A assinatura vive nos bytes: quem sai só com o papel leva um documento sem a
    /// garantia que o sistema produziu, e a farmácia recusa — com razão. Ver
    /// <see cref="EntregaAoPaciente"/>, inclusive por que o anexo não é automático.
    /// </summary>
    [RelayCommand]
    private async Task EnviarDocumentoAsync(LinhaDocumento? linha)
    {
        if (linha is null) return;

        if (!linha.PodeEnviar)
        {
            Mensagem = linha.Cancelado
                ? $"O documento {linha.Numero} está cancelado."
                : $"O documento {linha.Numero} ainda não foi assinado digitalmente. "
                  + "Sem assinatura, o que vale é a via impressa e assinada à caneta — "
                  + "assine antes de enviar o arquivo.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            // ⚠️ A barreira que NÃO EXISTIA (parcela 72). Este comando não tinha `Exigir`
            // nenhum, e a metade visível (`PodeEnviar`) era estado puro — `Assinado &&
            // !Cancelado`, sem permissão. Enviar é DADO DE SAÚDE SAINDO para o WhatsApp do
            // paciente, que é o que uma investigação procura (a lição da parcela 60); e o
            // `Cancelar` ao lado já guardava com o mesmo bit. Três comandos vizinhos
            // guardados e um não: o errado é o um.
            SessaoUsuario.Atual.Exigir(linha.AcessoParaMexer, "enviar documento clínico");

            byte[] pdf;
            Paciente? paciente;
            string? nomeClinica;

            using (var scope = _escopos.CreateScope())
            {
                var pdfs = scope.ServiceProvider.GetRequiredService<DocumentosClinicosPdfService>();
                var pacientes = scope.ServiceProvider.GetRequiredService<PacienteService>();
                var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();

                // Bytes GUARDADOS: o documento está assinado, e regerar produziria um
                // arquivo que abre como inválido no leitor de quem confere.
                pdf = await pdfs.GerarAsync(linha.DocumentoId);
                paciente = await pacientes.ObterComHistoricoAsync(linha.PacienteId);

                var prestador = await parametros.ObterPrestadorAsync();
                nomeClinica = prestador.NomeFantasia ?? prestador.RazaoSocial;
            }

            var entrega = EntregaAoPaciente.Entregar(
                pdf, linha.NomeArquivoAssinado, paciente?.Telefone,
                paciente?.Nome ?? "paciente", linha.Tipo, nomeClinica);

            Mensagem = entrega.Frase;
            MensagemEhErro = entrega.EhErro;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — documento não pôde ser entregue ao paciente", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    [RelayCommand]
    private async Task CancelarDocumentoAsync(LinhaDocumento? linha)
    {
        if (linha is null || linha.Cancelado) return;

        SessaoUsuario.Atual.Exigir(
            linha.AcessoParaMexer, $"cancelar {linha.Tipo.ToLowerInvariant()}");

        var motivo = _dialogo.PerguntarTexto(
            "Cancelar documento",
            $"Por que o(a) {linha.Tipo.ToLowerInvariant()} {linha.Numero} está sendo cancelado? "
            + "Ele continua na lista, marcado como cancelado — a via impressa não desaparece "
            + "por ser apagada do sistema.");
        if (string.IsNullOrWhiteSpace(motivo)) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();
            await servico.CancelarAsync(linha.DocumentoId, motivo, SessaoUsuario.Atual.Operador);

            _snackbar.Info("Documento cancelado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — documento clínico não pôde ser cancelado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    // ==================== Direitos do titular (LGPD) ====================

    /// <summary>
    /// Entrega ao paciente tudo o que a clínica guarda sobre ele (LGPD, art. 18).
    ///
    /// Sai em TEXTO, não em PDF diagramado: o direito é de receber os dados em formato
    /// legível e reutilizável — quem pede costuma querer levá-los a outro serviço, e um
    /// relatório bonito seria menos útil. É a mesma razão de os relatórios exportarem CSV.
    /// </summary>
    [RelayCommand]
    private async Task ExportarDadosAsync()
    {
        // ⚠️ `VerProntuario`, e não `VerFichaPaciente`: o que sai daqui é o prontuário
        // INTEIRO em texto — evolução, avaliações, medidas. Com o bit da ficha, Financeiro
        // e Faturista (que têm contato, não saúde) exportavam dado sensível para arquivo.
        // É a decisão escrita na parcela 26 ("o balcão exporta com VerProntuario, a direção
        // elimina") e a mesma regra do export CSV da parcela 60.
        SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "exportar os dados do titular");

        if (PacienteId == 0) return;

        try
        {
            string texto;
            using (var scope = _escopos.CreateScope())
            {
                var servico = scope.ServiceProvider.GetRequiredService<TitularDadosService>();
                texto = await servico.ExportarAsync(PacienteId, SessaoUsuario.Atual.Operador);

                // TRILHA DE LEITURA (parcela 62): a exportação do art. 18 leva o
                // prontuário INTEIRO num arquivo — é o maior acesso que esta tela permite.
                // O `TitularDadosService` grava a própria linha de auditoria, mas com ação
                // "DadosDoTitularExportados", que NÃO tem o prefixo `ProntuarioAcessado:`
                // e por isso não aparece na trilha de leitura filtrada — nem em "quem
                // abriu este prontuário". O MESMO ato pelo Gerente já registrava aqui.
                await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(PacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.ExportacaoTitular);
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                System.Text.Encoding.UTF8.GetBytes(texto),
                ImpressaoPdf.NomeSeguro($"dados-{Nome}.txt"),
                "Texto (*.txt)|*.txt", ".txt");

            if (erro is null)
            {
                Mensagem = string.Empty;
                MensagemEhErro = false;
                return;
            }

            Mensagem = erro;
            MensagemEhErro = true;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — dados do titular não puderam ser exportados", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Atende ao pedido de eliminação anonimizando o cadastro — e DIZ, antes de fazer,
    /// que o prontuário fica.
    ///
    /// Prometer "apagar tudo" e manter o histórico seria mentir para o paciente por
    /// escrito. A guarda do prontuário é obrigação legal do profissional de saúde
    /// (CFM 1.821/2007) e a própria LGPD a preserva (art. 16, II); o que se apaga é a
    /// identificação. A confirmação existe para que ninguém descubra isso depois.
    /// </summary>
    [RelayCommand]
    private async Task AnonimizarAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.AnonimizarDados, "anonimizar dados do titular");

        if (PacienteId == 0) return;

        var motivo = _dialogo.PerguntarTexto(
            "Anonimizar a pedido do titular",
            $"Registre o pedido de {Nome}: quando pediu, por qual canal e quem recebeu. "
            + "É o que prova que a clínica atendeu — e ela precisa poder provar.");
        if (string.IsNullOrWhiteSpace(motivo)) return;

        if (!_dialogo.ConfirmarPerigo(
                "Anonimizar cadastro",
                $"Nome, documento, telefone, carteirinha, nascimento e foto de {Nome} serão "
                + "apagados e NÃO voltam.\n\n"
                + "O prontuário FICA: sessões, evoluções, guias e documentos continuam "
                + "guardados, sem dono identificável. A guarda do prontuário é obrigação "
                + "legal do profissional de saúde, e a LGPD a preserva expressamente "
                + "(art. 16, II) — o sistema não pode prometer apagá-lo.\n\n"
                + "Confirmar?"))
            return;

        try
        {
            ResultadoAnonimizacao resultado;
            using (var scope = _escopos.CreateScope())
            {
                var servico = scope.ServiceProvider.GetRequiredService<TitularDadosService>();
                resultado = await servico.AnonimizarAsync(
                    PacienteId, motivo, SessaoUsuario.Atual.Operador);
            }

            await CarregarAsync();
            Alterou?.Invoke();

            // O que aconteceu vem escrito, item por item: "pronto" deixaria a clínica sem
            // saber o que continua guardado se o titular perguntar amanhã.
            _dialogo.Aviso(
                "Cadastro anonimizado",
                $"O cadastro passou a se chamar \"{resultado.NomeAnonimo}\".\n"
                + $"Consentimentos revogados: {resultado.ConsentimentosRevogados}.\n"
                + $"Foto removida: {(resultado.FotoRemovida ? "sim" : "não havia")}.\n"
                + (resultado.PreservouProntuario
                    ? $"Sessões mantidas sob guarda legal: {resultado.SessoesPreservadas}."
                    : "Não havia histórico clínico a guardar."));
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — cadastro não pôde ser anonimizado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>Abre o WhatsApp do paciente direto da ficha.</summary>
    [RelayCommand]
    private void AbrirWhatsapp()
    {
        if (PacienteId == 0 || Telefone == "—") return;

        var erro = Whatsapp.Abrir(
            Telefone, Nome,
            $"Olá, {Nome.Split(' ').FirstOrDefault() ?? Nome}!");

        if (erro is null) return;
        Mensagem = erro;
        MensagemEhErro = true;
    }
}

/// <summary>Um dos próximos horários do paciente, como a ficha o mostra (set/2026).</summary>
public sealed class LinhaHorarioFuturo
{
    public required int AgendamentoId { get; init; }
    public required DateTime Quando { get; init; }
    public required DateOnly Dia { get; init; }

    /// <summary>"qua 16/09 · 14:00–14:30".</summary>
    public required string Rotulo { get; init; }

    /// <summary>"Acupuntura · Dra. Ana · Sala 2" — só o que existe, separado por ponto.</summary>
    public required string Contexto { get; init; }

    public required bool Encaixe { get; init; }

    public static LinhaHorarioFuturo De(Agendamento a)
    {
        // Nome do CATÁLOGO, nunca o enum: `ToString()` escrevia "AcupunturaComEletro" na
        // ficha (parcela 41).
        var modalidade = CatalogoModalidades.Nome(a.ModalidadeCodigo ?? a.ModalidadePrevista.ToString());
        var partes = new[] { modalidade, a.Profissional?.Rotulo, a.Sala?.Nome }
            .Where(p => !string.IsNullOrWhiteSpace(p));

        return new LinhaHorarioFuturo
        {
            AgendamentoId = a.Id,
            Quando = a.DataHora,
            Dia = DateOnly.FromDateTime(a.DataHora),
            Rotulo = $"{DiaDaSemana(a.DataHora)} {a.DataHora:dd/MM} · {a.DataHora:HH:mm}–{a.FimPrevisto:HH:mm}",
            Contexto = string.Join(" · ", partes),
            Encaixe = a.Encaixe
        };
    }

    private static string DiaDaSemana(DateTime d) => d.DayOfWeek switch
    {
        DayOfWeek.Monday => "seg",
        DayOfWeek.Tuesday => "ter",
        DayOfWeek.Wednesday => "qua",
        DayOfWeek.Thursday => "qui",
        DayOfWeek.Friday => "sex",
        DayOfWeek.Saturday => "sáb",
        _ => "dom"
    };
}
