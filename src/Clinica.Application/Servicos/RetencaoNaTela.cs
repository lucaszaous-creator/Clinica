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

/// <summary>Os três filtros do modo Retenção.</summary>
public enum FiltroDaRetencao { ParaHoje, Aguardando, Fechados }

/// <summary>Um grupo da lista (o título com a contagem, e as linhas na ordem do trabalho). <see cref="Tom"/> é o do selo.</summary>
public sealed record GrupoDaRetencao(string Chave, string Titulo, string Tom, IReadOnlyList<LinhaAcompanhamento> Linhas);

/// <summary>Os números do seletor (Retenção = Para hoje) e dos três filtros.</summary>
public sealed record ContagemDaRetencao(int ParaHoje, int Aguardando, int Fechados);

/// <summary>
/// O bloco que toma o lugar do campo de escrever na conversa de retenção que ainda não tem
/// mensagem. <see cref="ModeloSugerido"/> é o nome do modelo que o botão principal abre, ou
/// nulo quando nenhum envio cabe (sem autorização, sem telefone).
/// </summary>
public sealed record AberturaDaRetencao(string Titulo, string Texto, IReadOnlyList<BotaoPedido> Botoes, string? ModeloSugerido);

/// <summary>
/// O MODO RETENÇÃO da tela do WhatsApp (out/2026 — o segundo modo da lista, pedido da
/// direção): a fila do Acompanhamento (<see cref="LinhaAcompanhamento"/>, Recall e Novo BSV)
/// agrupada como o mockup aprovado. Puro e sem relógio, como o <see cref="ConversasNaTela"/>.
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
/// <item><b>Retornos pedidos</b> ficam FORA: são outra fila (<see cref="Modelos.RetornosAMarcar"/>),
/// sem modalidade nem autorização na linha — misturá-los daria uma segunda porta para a
/// pergunta que a tela "Retornos a marcar" já responde, e sem como saber se a pessoa trata
/// nem se autorizou mensagem.</item>
/// <item><b>Sem autorização, nenhum caminho envia</b>: o único botão é "Já liguei"
/// (<see cref="Abertura"/>, <see cref="ConversasNaTela.PedeAcao"/>, <see cref="ModeloDoConvite"/>).</item>
/// </list>
/// </summary>
public static class RetencaoNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// O "Quem aparece aqui?" do modo Retenção (o ⓘ ao lado dos filtros — 25/10/2026: o rodapé da lista é
    /// do estado da ligação, nos dois modos).
    /// </summary>
    public const string QuemApareceAqui = "Só quem está em tratamento: BSV, acupuntura ou os dois, e passou dos dias sem vir que a direção definiu. "
        + "Um por um, revisado: aqui não há disparo em massa.";

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

    /// <summary>Fechado nesta semana (de segunda até hoje), pela volta ou pelo último contato. Sem data nenhuma, não entra.</summary>
    public static bool FechadoNestaSemana(LinhaAcompanhamento l, DateOnly hoje)
    {
        if (l.Pendente) return false;
        var segunda = hoje.AddDays(-(((int)hoje.DayOfWeek + 6) % 7));
        var contato = l.UltimoContato is { } u ? DateOnly.FromDateTime(u) : (DateOnly?)null;
        var quando = new[] { l.VoltouEm, contato }.Where(d => d is not null).Max();
        return quando is { } q && q >= segunda && q <= hoje;
    }

    // ---------------------------------------------------------------- a lista

    /// <summary>
    /// Os grupos da lista, na ordem do mockup. Para hoje: <i>Responderam · decidir</i>,
    /// <i>Sem vir há N dias</i> do maior limite ao menor (cada recall vai para o
    /// maior limite que já alcançou; abaixo do menor, <i>Sem vir há menos de N dias</i>) e
    /// <i>Novos no BSV</i>. Aguardando e Fechados são um grupo cada. Grupo vazio não aparece. Os títulos
    /// cabem numa linha da coluna de 300 px (25/10/2026): o título fixo cortado não dizia o grupo.
    /// <paramref name="limitesDeDias"/> são os do Gerente; <paramref name="ultimaRecebidaPorPaciente"/>
    /// sai de <see cref="ConversasNaTela.UltimaRecebidaPorPaciente"/>.
    /// </summary>
    public static IReadOnlyList<GrupoDaRetencao> Agrupar(IEnumerable<LinhaAcompanhamento> linhas, FiltroDaRetencao filtro,
        IEnumerable<int> limitesDeDias, IReadOnlyDictionary<int, DateTimeOffset> ultimaRecebidaPorPaciente, DateTimeOffset agora,
        string? busca = null)
    {
        var hoje = DateOnly.FromDateTime(agora.DateTime);
        DateTimeOffset? Ultima(LinhaAcompanhamento l) => ultimaRecebidaPorPaciente.TryGetValue(l.PacienteId, out var u) ? u : null;
        var termo = busca?.Trim() ?? "";
        var digitos = Telefone.Normalizar(termo);
        var doFiltro = linhas.Where(EmTratamento)
            .Where(l => termo.Length == 0
                || PtBr.CompareInfo.IndexOf(l.Paciente, termo, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0
                || digitos.Length >= ConversasNaTela.DigitosParaBuscarTelefone && Telefone.Normalizar(l.Telefone).Contains(digitos, StringComparison.Ordinal))
            .Select(l => (Linha: l, Situacao: Situacao(l, Ultima(l), agora)))
            .Where(x => NoFiltro(x.Linha, x.Situacao, filtro, hoje))
            .ToList();

        if (filtro == FiltroDaRetencao.Aguardando)
            return Grupo("aguardando", "Aguardando", "neutro", Ordenar(doFiltro.Select(x => x.Linha)));
        if (filtro == FiltroDaRetencao.Fechados)
            return Grupo("fechado", "Fechados esta semana", "neutro",
                doFiltro.Select(x => x.Linha).OrderByDescending(l => l.VoltouEm ?? (l.UltimoContato is { } u ? DateOnly.FromDateTime(u) : DateOnly.MinValue))
                    .ThenBy(l => l.Paciente, StringComparer.CurrentCultureIgnoreCase).ToList());

        var grupos = new List<GrupoDaRetencao>();
        var responderam = doFiltro.Where(x => x.Situacao == SituacaoNaRetencao.Respondeu).Select(x => x.Linha)
            .OrderByDescending(l => Ultima(l)).ThenBy(l => l.Paciente, StringComparer.CurrentCultureIgnoreCase).ToList();
        grupos.AddRange(Grupo("respondeu", "Responderam · decidir", "sucesso", responderam));

        var paraHoje = doFiltro.Where(x => x.Situacao == SituacaoNaRetencao.ParaHoje).Select(x => x.Linha).ToList();
        var recall = paraHoje.Where(l => l.Tipo == TipoAcompanhamento.Recall).ToList();
        var limites = Limites(limitesDeDias);
        if (limites.Count == 0)
            grupos.AddRange(Grupo("recall", "Sem vir", "neutro", Ordenar(recall)));
        else
        {
            for (var i = 0; i < limites.Count; i++)
            {
                var limite = limites[i];
                var teto = i == 0 ? int.MaxValue : limites[i - 1];
                grupos.AddRange(Grupo($"dias-{limite}", $"Sem vir há {limite} dias", i == 0 ? "perigo" : i == 1 ? "aviso" : "neutro",
                    Ordenar(recall.Where(l => l.Dias >= limite && l.Dias < teto))));
            }
            var menor = limites[^1];
            grupos.AddRange(Grupo("dias-abaixo", $"Sem vir há menos de {menor} dias", "neutro", Ordenar(recall.Where(l => l.Dias < menor))));
        }
        grupos.AddRange(Grupo("novo-bsv", "Novos no BSV · sem a 1ª sessão", "info",
            Ordenar(paraHoje.Where(l => l.Tipo == TipoAcompanhamento.NovoBsv))));
        return grupos;
    }

    private static bool NoFiltro(LinhaAcompanhamento l, SituacaoNaRetencao s, FiltroDaRetencao f, DateOnly hoje) => f switch
    {
        FiltroDaRetencao.ParaHoje => s is SituacaoNaRetencao.ParaHoje or SituacaoNaRetencao.Respondeu,
        FiltroDaRetencao.Aguardando => s == SituacaoNaRetencao.Aguardando,
        _ => s == SituacaoNaRetencao.Fechado && FechadoNestaSemana(l, hoje)
    };

    /// <summary>Os limites do Gerente, sem repetição, positivos, do maior ao menor.</summary>
    private static List<int> Limites(IEnumerable<int> limites) => limites.Where(d => d > 0).Distinct().OrderByDescending(d => d).ToList();

    private static IReadOnlyList<GrupoDaRetencao> Grupo(string chave, string titulo, string tom, IReadOnlyList<LinhaAcompanhamento> linhas)
        => linhas.Count == 0 ? [] : [new GrupoDaRetencao(chave, titulo, tom, linhas)];

    /// <summary>A ordem do trabalho dentro do grupo: o contato atrasado, depois quem está há mais tempo sem vir.</summary>
    private static IReadOnlyList<LinhaAcompanhamento> Ordenar(IEnumerable<LinhaAcompanhamento> linhas)
        => linhas.OrderByDescending(l => l.Atrasado).ThenByDescending(l => l.Dias)
            .ThenBy(l => l.Paciente, StringComparer.CurrentCultureIgnoreCase).ToList();

    /// <summary>Os números dos filtros (e o do seletor, que é o Para hoje).</summary>
    public static ContagemDaRetencao Contagens(IEnumerable<LinhaAcompanhamento> linhas,
        IReadOnlyDictionary<int, DateTimeOffset> ultimaRecebidaPorPaciente, DateTimeOffset agora)
    {
        var hoje = DateOnly.FromDateTime(agora.DateTime);
        int paraHoje = 0, aguardando = 0, fechados = 0;
        foreach (var l in linhas.Where(EmTratamento))
        {
            var s = Situacao(l, ultimaRecebidaPorPaciente.TryGetValue(l.PacienteId, out var u) ? u : null, agora);
            if (NoFiltro(l, s, FiltroDaRetencao.ParaHoje, hoje)) paraHoje++;
            else if (NoFiltro(l, s, FiltroDaRetencao.Aguardando, hoje)) aguardando++;
            else if (NoFiltro(l, s, FiltroDaRetencao.Fechados, hoje)) fechados++;
        }
        return new(paraHoje, aguardando, fechados);
    }

    public static string Rotulo(FiltroDaRetencao f) => f switch
    {
        FiltroDaRetencao.ParaHoje => "Para hoje",
        FiltroDaRetencao.Aguardando => "Aguardando",
        _ => "Fechados"
    };

    /// <summary>O vazio de cada filtro, separado do vazio da busca.</summary>
    public static (string Titulo, string Texto) Vazio(FiltroDaRetencao f, bool comBusca)
        => comBusca ? ("Ninguém com esse nome", "Confira o nome.")
            : f switch
            {
                FiltroDaRetencao.ParaHoje => ("Ninguém para hoje", "Todo mundo em tratamento veio ou já foi contatado."),
                FiltroDaRetencao.Aguardando => ("Ninguém aguardando", "Quem foi contatado e ainda não respondeu aparece aqui."),
                _ => ("Nenhum caso fechado esta semana", "Quem voltar, marcar ou for encerrado com motivo aparece aqui.")
            };

    // ---------------------------------------------------------------- a linha

    /// <summary>
    /// O selo da linha: fechado com o desfecho (o selo do Acompanhamento), respondeu, aguardando,
    /// e, para hoje, o que decide o caminho — sem telefone, sem autorização (ligar), novo no
    /// BSV ou "Autorizou". Curtos (25/10/2026): na coluna de 300 px o selo é o que não pode ser
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
        if (!l.Consentimento) return new("Sem autorização", "aviso");
        if (l.Tipo == TipoAcompanhamento.NovoBsv) return new("Indicado ao BSV", "info");
        return new("Autorizou", "sucesso");
    }

    /// <summary>O número ao lado do nome: "32 dias" (recall) ou "indicado há 3 dias" (novo no BSV).</summary>
    public static string Dias(LinhaAcompanhamento l)
        => l.Tipo == TipoAcompanhamento.NovoBsv ? "indicado há " + ConversaDoAcompanhamento.TempoTexto(l) : ConversaDoAcompanhamento.TempoTexto(l);

    /// <summary>"BSV · última 22/09 · 6 sessões · pacote com saldo" / "BSV · indicado em 21/10 por Dr. Marcos".</summary>
    public static string Detalhe(LinhaAcompanhamento l)
    {
        var tratamento = l.Tipo == TipoAcompanhamento.NovoBsv && !EhBsv(l.Modalidade) ? "BSV" : Tratamento(l.Modalidade);
        if (l.Tipo == TipoAcompanhamento.NovoBsv)
            return $"{tratamento} · indicado em {l.ReferenciaEm.ToString("dd/MM", PtBr)}"
                + (l.Profissional is { Length: > 0 } p && p != "—" ? $" por {p}" : "");
        return string.Join(" · ", new[]
        {
            tratamento, $"última {l.ReferenciaEm.ToString("dd/MM", PtBr)}",
            l.TotalSessoes switch { <= 0 => null, 1 => "1 sessão", var n => $"{n} sessões" },
            l.PacoteComSaldo ? "pacote com saldo" : null
        }.Where(x => x is not null));
    }

    private static bool EhBsv(ModalidadeAtendimento m) => m is ModalidadeAtendimento.BsvApenas or ModalidadeAtendimento.BsvComAcupuntura;

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

    /// <summary>Os cinco "Como foi?" na ordem do mockup, com os rótulos do Acompanhamento.</summary>
    public static IReadOnlyList<BotaoPedido> BotoesDoComoFoi() =>
    [
        new(BotaoDaAcao.Desfecho, AcompanhamentoNaTela.Rotulo(DesfechoContato.QuerMarcar), Primario: true, Desfecho: DesfechoContato.QuerMarcar),
        new(BotaoDaAcao.Desfecho, AcompanhamentoNaTela.Rotulo(DesfechoContato.PediuOutraData), Desfecho: DesfechoContato.PediuOutraData),
        new(BotaoDaAcao.Desfecho, AcompanhamentoNaTela.Rotulo(DesfechoContato.AguardandoPlano), Desfecho: DesfechoContato.AguardandoPlano),
        new(BotaoDaAcao.Desfecho, AcompanhamentoNaTela.Rotulo(DesfechoContato.NaoRespondeu), Desfecho: DesfechoContato.NaoRespondeu),
        new(BotaoDaAcao.Desfecho, AcompanhamentoNaTela.Rotulo(DesfechoContato.NaoQuerVoltar), Perigo: true, Desfecho: DesfechoContato.NaoQuerVoltar)
    ];
}
