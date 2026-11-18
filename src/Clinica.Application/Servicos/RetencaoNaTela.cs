using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Onde o caso está, para a lista da retenção.</summary>
public enum SituacaoNaRetencao
{
    /// <summary>Aberto, com o próximo contato vencido ou para hoje: é a vez da recepção.</summary>
    ParaHoje,

    /// <summary>O paciente escreveu DEPOIS do último contato registrado: pede o "Como foi?".</summary>
    Respondeu,

    /// <summary>Aberto, com o próximo contato marcado para depois de hoje: esperando o paciente.</summary>
    Aguardando,

    /// <summary>Voltou, agendou ou foi encerrado com motivo.</summary>
    Fechado
}

/// <summary>
/// O bloco que toma o lugar do campo de escrever na conversa de retenção que ainda não tem
/// mensagem. <see cref="ModeloSugerido"/> é o nome do modelo que o botão principal abre, ou
/// nulo quando nenhum envio cabe (sem autorização, sem telefone).
/// </summary>
public sealed record AberturaDaRetencao(string Titulo, string Texto, IReadOnlyList<BotaoPedido> Botoes, string? ModeloSugerido);

/// <summary>
/// A RETENÇÃO dentro da conversa do WhatsApp: o caso do Acompanhamento (<see cref="LinhaAcompanhamento"/>,
/// Recall e Novo BSV) ligado à conversa — a etiqueta, o selo, a abertura e o "Como foi?". A LISTA da
/// retenção saiu da aba WhatsApp em nov/2026: ela é a Fila 360 (<see cref="Fila360NaTela"/>), uma porta por
/// pergunta. Puro e sem relógio, como o <see cref="ConversasNaTela"/>.
///
/// O que não se recalcula aqui: quem está na fila e há quantos dias (o
/// <c>IAcompanhamentoPacienteService</c> decide), o que o desfecho grava
/// (<see cref="AcompanhamentoNaTela.Gravar"/>), o selo do caso fechado
/// (<see cref="AcompanhamentoNaTela.Selo"/>). O que se decide aqui:
/// <list type="bullet">
/// <item><b>Só quem trata</b>: BSV, acupuntura ou os dois — quem só fez consulta não entra
/// (a mesma regra de <c>ClinicRelationshipBridge.Tratamento</c>, sem referenciar a ponte).</item>
/// <item><b>Os dias são do Gerente</b>: a tela entrega os limites. Hoje Configurações guarda
/// UM número (<see cref="ParametrosService.ObterDiasInatividadeRecallAsync"/>, "Recall: dias
/// sem vir", padrão 60) — a partir dele o paciente entra na fila. Os degraus 30 / 20 / 15 do
/// mockup ainda não têm lugar na configuração; até terem, a tela passa o número que existe, e
/// a lista sai com um grupo só. Nenhum número fica escrito aqui.</item>
/// <item><b>Respondeu</b> = o paciente escreveu (a última recebida da conversa com a ficha
/// conferida) DEPOIS do último contato registrado no caso.</item>
/// <item><b>Sem autorização, nenhum caminho envia</b>: o único botão é "Já liguei"
/// (<see cref="Abertura"/>, <see cref="ConversasNaTela.PedeAcao"/>, <see cref="ModeloDoConvite"/>).</item>
/// </list>
/// </summary>
public static class RetencaoNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Quem trata na clínica: BSV, acupuntura ou os dois. Consulta avulsa não é tratamento.</summary>
    public static bool EmTratamento(ModalidadeAtendimento m) => m != ModalidadeAtendimento.Consulta;

    public static bool EmTratamento(LinhaAcompanhamento l) => l.Tipo == TipoAcompanhamento.NovoBsv || EmTratamento(l.Modalidade);

    /// <summary>"BSV", "BSV e acupuntura", "Acupuntura" — o tratamento da linha.</summary>
    public static string Tratamento(ModalidadeAtendimento m) => m switch
    {
        ModalidadeAtendimento.BsvApenas => "BSV",
        ModalidadeAtendimento.BsvComAcupuntura => "BSV e acupuntura",
        ModalidadeAtendimento.AcupunturaSimples or ModalidadeAtendimento.AcupunturaComEletro => "Acupuntura",
        _ => "Consulta"
    };

    /// <summary>O tratamento no meio da frase: a sigla fica em maiúscula, a acupuntura não.</summary>
    public static string TratamentoNaFrase(ModalidadeAtendimento m) => m switch
    {
        ModalidadeAtendimento.BsvApenas => "BSV",
        ModalidadeAtendimento.BsvComAcupuntura => "BSV e acupuntura",
        ModalidadeAtendimento.AcupunturaSimples or ModalidadeAtendimento.AcupunturaComEletro => "acupuntura",
        _ => "consulta"
    };

    // ---------------------------------------------------------------- situação

    /// <summary>
    /// O paciente respondeu ao contato: o caso está aberto, já teve contato registrado, e a
    /// última mensagem RECEBIDA (carimbo do CRM, convertido para o fuso de <paramref name="agora"/>)
    /// é posterior a ele. Registrar o "Como foi?" grava um contato novo e encerra o "respondeu".
    /// </summary>
    public static bool Respondeu(LinhaAcompanhamento l, DateTimeOffset? ultimaRecebida, DateTimeOffset agora)
        => l.Pendente && l.UltimoContato is { } contato && ConversasNaTela.PacienteJaEscreveu(ultimaRecebida)
           && ultimaRecebida!.Value.ToOffset(agora.Offset).DateTime > contato;

    public static SituacaoNaRetencao Situacao(LinhaAcompanhamento l, DateTimeOffset? ultimaRecebida, DateTimeOffset agora)
    {
        if (!l.Pendente) return SituacaoNaRetencao.Fechado;
        if (Respondeu(l, ultimaRecebida, agora)) return SituacaoNaRetencao.Respondeu;
        return l.ProximoContato > DateOnly.FromDateTime(agora.DateTime) ? SituacaoNaRetencao.Aguardando : SituacaoNaRetencao.ParaHoje;
    }

    // ---------------------------------------------------------------- a linha

    /// <summary>
    /// O selo da linha: fechado com o desfecho (o selo do Acompanhamento), respondeu, aguardando,
    /// e, para hoje, o que decide o caminho — sem telefone, sem autorização (ligar), novo no
    /// BSV ou "Pode convidar". Curtos (25/10/2026): na coluna de 300 px o selo é o que não pode ser
    /// cortado; o que ele quer dizer por inteiro está no aviso da conversa ao abrir.
    /// </summary>
    public static SeloDoWhatsApp Selo(LinhaAcompanhamento l, DateTimeOffset? ultimaRecebida, DateTimeOffset agora)
    {
        var hoje = DateOnly.FromDateTime(agora.DateTime);
        switch (Situacao(l, ultimaRecebida, agora))
        {
            case SituacaoNaRetencao.Fechado:
                var (texto, tom) = AcompanhamentoNaTela.Selo(l, hoje);
                return new(texto, string.IsNullOrEmpty(tom) ? "neutro" : tom);
            case SituacaoNaRetencao.Respondeu:
                return new("Respondeu", "sucesso");
            case SituacaoNaRetencao.Aguardando:
                return new($"Aguardando · {l.ProximoContato.ToString("dd/MM", PtBr)}", "info");
        }
        if (!TemTelefone(l)) return new("Sem telefone", "aviso");
        if (!l.Consentimento) return new("Pediu para não receber", "aviso");
        if (l.Tipo == TipoAcompanhamento.NovoBsv) return new("Indicado ao BSV", "info");
        return new("Pode convidar", "sucesso");
    }

    /// <summary>
    /// A "Situação" na coluna do caso — só quando ela diz algo que a coluna ainda não diz: fechado, respondeu
    /// ou aguardando. Antes do contato o selo da lista é o telefone ou a autorização, e a coluna já mostra os dois
    /// ("Mensagens: autorizou" e "Situação: Autorizou" eram a mesma frase duas vezes); o "Indicado ao BSV" já está
    /// no título ("Acompanhamento · Novo BSV").
    /// </summary>
    public static SeloDoWhatsApp? SeloNaColuna(LinhaAcompanhamento l, DateTimeOffset? ultimaRecebida, DateTimeOffset agora)
        => Situacao(l, ultimaRecebida, agora) is SituacaoNaRetencao.Fechado or SituacaoNaRetencao.Respondeu or SituacaoNaRetencao.Aguardando
            ? Selo(l, ultimaRecebida, agora) : null;

    /// <summary>O número ao lado do nome: "32 dias" (recall) ou "indicado há 3 dias" (novo no BSV).</summary>
    public static string Dias(LinhaAcompanhamento l)
        => l.Tipo == TipoAcompanhamento.NovoBsv ? "indicado há " + ConversaDoAcompanhamento.TempoTexto(l) : ConversaDoAcompanhamento.TempoTexto(l);

    /// <summary>
    /// A etiqueta da faixa da conversa: "Retenção · BSV · 32 dias · com Patrycia" (nov/2026). Quem atende pela
    /// recepção vê com quem o caso está antes de marcar ou combinar algo, e quem cuida dele lê "com você".
    /// Sem responsável, a etiqueta não inventa um.
    /// </summary>
    public static string Etiqueta(LinhaAcompanhamento caso, int usuarioId)
    {
        var etiqueta = $"Retenção · {Tratamento(caso.Modalidade)} · {Dias(caso)}";
        if (caso.ResponsavelId is null) return etiqueta;
        return etiqueta + " · com " + (caso.ResponsavelId == usuarioId ? "você" : ConversaDoAcompanhamento.Dono(caso, usuarioId));
    }

    private static bool TemTelefone(LinhaAcompanhamento l) => ConversaNoCrm.Telefone(l.Telefone) is not null;

    // ---------------------------------------------------------------- o convite

    /// <summary>
    /// Cabe convite (o modelo que a clínica manda primeiro)? Só para caso aberto de quem trata,
    /// com telefone que serve para mensagem e com a autorização colhida.
    /// </summary>
    public static bool PodeConvidar(LinhaAcompanhamento l) => l.Pendente && EmTratamento(l) && TemTelefone(l) && l.Consentimento;

    /// <summary>
    /// O modelo do convite: <c>retorno_tratamento</c> para o recall, <c>primeiro_contato</c>
    /// para o novo no BSV (os do mockup). Nulo quando não cabe convite (ver <see cref="PodeConvidar"/>).
    /// </summary>
    public static string? ModeloDoConvite(LinhaAcompanhamento l)
        => !PodeConvidar(l) ? null : l.Tipo == TipoAcompanhamento.NovoBsv ? ModeloDoWhatsApp.PrimeiroContato : ModeloDoWhatsApp.RetornoTratamento;

    /// <summary>
    /// Os modelos do convite em ordem de preferência: o APROVADO na Meta primeiro
    /// (<c>semdor_recall</c>; <c>semdor_iniciar_atendimento</c> para o novo no BSV) e o nome do
    /// mockup como reserva (a prévia e instalações antigas). A tela usa o primeiro que o CRM
    /// devolver em <c>GET /api/templates</c>; nenhum = o convite não é oferecido, e a tela diz.
    /// Vazio para quem não pode ser convidado (sem consentimento, caso fechado) — a mesma regra de <see cref="ModeloDoConvite"/>.
    /// </summary>
    public static IReadOnlyList<string> CandidatosDoConvite(LinhaAcompanhamento l)
        => !PodeConvidar(l) ? []
            : l.Tipo == TipoAcompanhamento.NovoBsv ? [ModeloDoWhatsApp.IniciarAtendimentoReal, ModeloDoWhatsApp.PrimeiroContato]
            : [ModeloDoWhatsApp.RecallReal, ModeloDoWhatsApp.RetornoTratamento];

    /// <summary>
    /// O bloco da conversa de retenção SEM mensagens (o lugar do campo de escrever). Quem não
    /// autorizou nunca recebe botão de envio — vale em qualquer situação aberta; quem autorizou
    /// e é "para hoje" recebe o modelo como primeira mensagem (a pessoa ainda não escreveu).
    /// Nulo quando a conversa já tem mensagem, o caso está fechado ou está aguardando (aí vale o
    /// bloco normal da janela de 24 h).
    /// </summary>
    public static AberturaDaRetencao? Abertura(LinhaAcompanhamento l, AppConversation? conversa, DateTimeOffset agora)
    {
        if (!l.Pendente || !EmTratamento(l) || conversa is { Messages.Length: > 0 }) return null;
        var nome = ConversasNaTela.PrimeiroNomeOu(l.Paciente, "A pessoa");
        if (!TemTelefone(l))
            return new("Sem telefone no cadastro", "Sem um celular na ficha não há conversa nem ligação. Atualize o telefone na ficha do paciente.",
                [new(BotaoDaAcao.AtualizarTelefone, "Atualizar telefone", Primario: true)], null);
        if (!l.Consentimento)
            return new("Sem autorização de mensagens", "Nada sai daqui até a autorização ser colhida no termo. Ligue e registre como foi.",
                [new(BotaoDaAcao.JaLiguei, "Já liguei", Primario: true), new(BotaoDaAcao.NotaInterna, "Nota interna")], null);
        if (Situacao(l, conversa?.LastIncoming, agora) != SituacaoNaRetencao.ParaHoje) return null;
        var novoBsv = l.Tipo == TipoAcompanhamento.NovoBsv;
        return new("Primeira mensagem desta conversa: um modelo aprovado",
            $"{nome} ainda não escreveu, então a Meta só aceita modelo. Depois da resposta, o campo livre abre.",
            [novoBsv ? new(BotaoDaAcao.PrepararMensagem, "Preparar mensagem", Primario: true) : new(BotaoDaAcao.PrepararConvite, "Preparar convite", Primario: true),
             new(BotaoDaAcao.NotaInterna, "Nota interna")],
            ModeloDoConvite(l));
    }

    /// <summary>
    /// Os cinco "Como foi?" na ordem do mockup, com os rótulos do Acompanhamento. Todos NEUTROS (item 22 da revisão,
    /// 25/10): "Quer marcar" em azul parecia a resposta já escolhida. Só o "Não quer voltar" leva o contorno de perigo,
    /// porque encerra o caso.
    /// </summary>
    public static IReadOnlyList<BotaoPedido> BotoesDoComoFoi() =>
    [
        new(BotaoDaAcao.Desfecho, AcompanhamentoNaTela.Rotulo(DesfechoContato.QuerMarcar), Desfecho: DesfechoContato.QuerMarcar),
        new(BotaoDaAcao.Desfecho, AcompanhamentoNaTela.Rotulo(DesfechoContato.PediuOutraData), Desfecho: DesfechoContato.PediuOutraData),
        new(BotaoDaAcao.Desfecho, AcompanhamentoNaTela.Rotulo(DesfechoContato.AguardandoPlano), Desfecho: DesfechoContato.AguardandoPlano),
        new(BotaoDaAcao.Desfecho, AcompanhamentoNaTela.Rotulo(DesfechoContato.NaoRespondeu), Desfecho: DesfechoContato.NaoRespondeu),
        new(BotaoDaAcao.Desfecho, AcompanhamentoNaTela.Rotulo(DesfechoContato.NaoQuerVoltar), Perigo: true, Desfecho: DesfechoContato.NaoQuerVoltar)
    ];

    /// <summary>
    /// O "Como foi?" aparece na coluna do paciente? Só depois de um contato de verdade com ele — um convite ou uma
    /// ligação registrados no caso (<see cref="LinhaAcompanhamento.Tentativas"/>; a abertura do ciclo não conta) — e
    /// só quando o aviso da conversa ainda não o mostra (item 22 e item 6 da revisão, 25/10: antes ele aparecia para
    /// quem nem tinha sido convidado, e nos dois lugares ao mesmo tempo).
    /// </summary>
    public static bool ComoFoiNaColuna(LinhaAcompanhamento caso, AcaoPedida? acaoDaConversa)
        => caso.Pendente && caso.Tentativas > 0 && !ConversasNaTela.AvisoOferece(acaoDaConversa, BotaoDaAcao.Desfecho);
}
