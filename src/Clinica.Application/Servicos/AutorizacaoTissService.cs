using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>O resultado de um pedido de autorização: a autorização (nova ou a que já valia) ou o porquê de não pedir.</summary>
public sealed record PedidoDeAutorizacaoTiss(AutorizacaoTiss? Autorizacao, string? Impedimento, bool Nova)
{
    public static PedidoDeAutorizacaoTiss Recusado(string frase) => new(null, frase, false);
}

/// <summary>
/// A autorização na aba Dia: a mais recente de cada horário, os horários em que o convênio pede
/// autorização pelo sistema (só neles há selo, campo do token e botão) e o aviso do token faltando.
/// </summary>
public sealed record AutorizacoesDaAgenda(IReadOnlyDictionary<int, AutorizacaoTiss> PorHorario, IReadOnlySet<int> HorariosQuePedem, string? AvisoDeToken)
{
    public static readonly AutorizacoesDaAgenda Nenhuma = new(new Dictionary<int, AutorizacaoTiss>(), new HashSet<int>(), null);
}

/// <summary>O sinal pequeno da autorização na linha da agenda: rótulo, tom do <c>Selo</c> e a dica.</summary>
public sealed record SeloDeAutorizacaoTiss(string Rotulo, string Tom, string? Dica, bool PodePedir);

/// <summary>
/// A AUTORIZAÇÃO PELO SISTEMA (nov/2026, Amil): quando o paciente chega (ou pelo botão "Pedir autorização"),
/// o sistema pede a autorização à operadora pela fila do conector e guarda a senha, a data e a validade —
/// a recepção não abre o portal e só digita, na baixa, o token que o paciente mostra no app (o token
/// nasce com a autorização: Manual do Credenciado, p. 25). Pedir NÃO é cobrança: pode ser refeito por
/// clique, mas um horário tem UMA autorização valendo — nunca duas. A chave é por convênio e desligada
/// (<see cref="CredenciaisTissService.AutorizacaoLigadaAsync"/>). Acupuntura em série: um pedido por
/// sessão (o manual pede validação a cada atendimento e o token é por pedido).
/// </summary>
public sealed class AutorizacaoTissService(IClinicaRepositorio repo, ParametrosService parametros,
    CredenciaisTissService credenciais, Func<DateTime>? agora = null)
{
    private readonly Func<DateTime> _agora = agora ?? HoraDaClinica.Agora;

    public const string FraseDesligada = "O pedido de autorização pelo sistema está desligado para este convênio.";

    /// <summary>O procedimento PRINCIPAL que a modalidade do horário pede (a consulta, a acupuntura ou o BSV).</summary>
    public static TipoCodigo TipoDaModalidade(ModalidadeAtendimento modalidade) => modalidade switch
    {
        ModalidadeAtendimento.Consulta => TipoCodigo.Consulta,
        ModalidadeAtendimento.BsvApenas or ModalidadeAtendimento.BsvComAcupuntura => TipoCodigo.Bsv,
        _ => TipoCodigo.Acupuntura
    };

    /// <summary>O convênio do horário pede autorização pelo sistema agora (catálogo + chave ligada)?</summary>
    public async Task<bool> LigadaParaAsync(string convenioCodigo, CancellationToken ct = default)
        => OperadorasTiss.TemAutorizacao(CatalogoConvenios.Familia(convenioCodigo)) && await credenciais.AutorizacaoLigadaAsync(convenioCodigo, ct);

    /// <summary>
    /// O que a aba Dia mostra da autorização: lê a chave UMA vez por convênio do dia e, só se alguma
    /// estiver ligada, as autorizações dos horários (uma consulta). Chave desligada = nada na tela.
    /// </summary>
    public async Task<AutorizacoesDaAgenda> LerDaAgendaAsync(IReadOnlyList<Agendamento> horarios, DateOnly dia, CancellationToken ct = default)
    {
        var ligada = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var pedem = new HashSet<int>();
        foreach (var ag in horarios)
        {
            if (ag.Paciente is not { } p || ag.Particular) continue;
            var convenio = p.ConvenioCodigo ?? p.Convenio.ToString();
            if (!ligada.TryGetValue(convenio, out var sim)) ligada[convenio] = sim = await LigadaParaAsync(convenio, ct);
            if (sim) pedem.Add(ag.Id);
        }
        if (pedem.Count == 0) return AutorizacoesDaAgenda.Nenhuma;
        var porHorario = (await repo.AutorizacoesDosAgendamentosAsync(pedem, ct))
            .GroupBy(a => a.AgendamentoId).ToDictionary(g => g.Key, g => g.OrderBy(a => a.Id).Last());
        var hoje = DateOnly.FromDateTime(_agora());
        var aviso = dia == hoje
            ? AvisoDeTokenFaltando(porHorario.Values.Where(a => horarios.Any(h => h.Id == a.AgendamentoId
                && h.Status is StatusAgendamento.Agendado or StatusAgendamento.Realizado)), hoje)
            : null;
        return new AutorizacoesDaAgenda(porHorario, pedem, aviso);
    }

    /// <summary>
    /// Pede a autorização do horário. Devolve a que já está na fila, em análise ou valendo (nunca duas);
    /// recusa com a frase quando falta dado (carteira, CBO, procedimento) ou a chave está desligada.
    /// </summary>
    public async Task<PedidoDeAutorizacaoTiss> PedirAsync(int agendamentoId, string operador, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(operador)) throw new InvalidOperationException("Toda transação com a operadora leva quem a pediu: faça login antes.");
        var ag = await repo.ObterAgendamentoAsync(agendamentoId, ct) ?? throw new InvalidOperationException("Horário não encontrado.");
        if (ag.Paciente is not { } paciente) return PedidoDeAutorizacaoTiss.Recusado("O horário não tem paciente.");
        if (ag.Particular) return PedidoDeAutorizacaoTiss.Recusado("Atendimento particular: não há autorização a pedir.");
        var convenio = paciente.ConvenioCodigo ?? paciente.Convenio.ToString();
        if (!await LigadaParaAsync(convenio, ct)) return PedidoDeAutorizacaoTiss.Recusado(FraseDesligada);

        var hoje = DateOnly.FromDateTime(_agora());
        var existentes = await repo.AutorizacoesDoAgendamentoAsync(agendamentoId, ct);
        if (existentes.LastOrDefault(a => a.PrendeOHorario(hoje)) is { } valendo)
            return new PedidoDeAutorizacaoTiss(valendo, null, false);

        var carteira = (paciente.Carteirinha ?? string.Empty).Replace(" ", string.Empty).Trim();
        if (carteira.Length is 0 or > 20) return PedidoDeAutorizacaoTiss.Recusado("A ficha do paciente está sem o número da carteira.");
        if (ag.Profissional is not { } prof) return PedidoDeAutorizacaoTiss.Recusado("O horário não tem profissional: a autorização pede o profissional solicitante.");
        var registro = RegistroDoProfissionalParaGuias.Do(prof);
        if (!registro.Completo)
            return PedidoDeAutorizacaoTiss.Recusado($"Complete o registro para guias de {prof.Nome} (conselho, número, UF e CBO): a autorização exige o CBO.");
        var tipo = TipoDaModalidade(ag.ModalidadePrevista);
        var tuss = (await parametros.ObterPrestadorAsync(ct)).CodigoTuss(tipo);
        if (string.IsNullOrWhiteSpace(tuss)) return PedidoDeAutorizacaoTiss.Recusado($"O código TUSS de {tipo} não está em Configurações do faturamento.");

        var autorizacao = new AutorizacaoTiss
        {
            AgendamentoId = ag.Id, PacienteId = paciente.Id, ConvenioCodigo = convenio, NumeroCarteira = carteira,
            CodigoProcedimento = tuss.Trim(), DescricaoProcedimento = GuiaAnsNaBaixa.DescricaoProposta(tipo),
            ProfissionalNome = prof.Nome.Length <= 70 ? prof.Nome : prof.Nome[..70],
            ConselhoProfissional = registro.Conselho!, NumeroConselho = registro.Numero!, UfConselho = registro.Uf!, Cbos = registro.Cbos!,
            PedidaPor = operador.Trim(), PedidaEm = _agora()
        };
        var transacao = new TransacaoTiss
        {
            Tipo = TipoTransacaoTiss.Autorizacao, ConvenioCodigo = convenio, PacienteId = paciente.Id, NumeroCarteira = carteira,
            Autorizacao = autorizacao, Estado = EstadoTransacaoTiss.Pendente, PedidoPor = autorizacao.PedidaPor, PedidoEm = autorizacao.PedidaEm
        };
        await repo.AdicionarTransacaoTissAsync(transacao, ct);
        await repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = autorizacao.PedidaPor, Acao = "PedidoAutorizacaoTiss",
            Detalhe = $"Pedido de autorização à operadora ({convenio}): {tipo}, horário de {ag.DataHora:dd/MM/yyyy HH:mm}", PacienteId = paciente.Id
        }, ct);
        await repo.SalvarAsync(ct);
        // O número da guia de solicitação: "A" + o Id (único por pedido; a situação e o cancelamento o usam).
        autorizacao.NumeroGuiaPrestador = "A" + autorizacao.Id;
        await repo.SalvarAsync(ct);
        return new PedidoDeAutorizacaoTiss(autorizacao, null, true);
    }

    /// <summary>
    /// O horário foi desmarcado (cancelado, falta ou substituído): a autorização dele não pode ficar aberta.
    /// Na fila, o pedido é cancelado e nada sai; com senha emitida, pede o cancelamento à operadora
    /// (<c>cancela-guia</c> da guia de solicitação). Não salva: quem chama salva junto com a mudança do horário.
    /// </summary>
    public static async Task CancelarDoHorarioAsync(IClinicaRepositorio repo, Agendamento ag, string? operador, DateTime agora, CancellationToken ct = default)
    {
        foreach (var a in await repo.AutorizacoesDoAgendamentoAsync(ag.Id, ct))
        {
            if (a.Situacao == SituacaoAutorizacaoTiss.Pedida)
            {
                // Ainda na fila: o pedido sai da fila (o conector não o leva), e a autorização fica cancelada.
                a.Situacao = SituacaoAutorizacaoTiss.Cancelada;
                a.Motivo = "Horário desmarcado antes de o pedido ir à operadora.";
                a.AtualizadaEm = agora;
                continue;
            }
            if (a.Situacao is not (SituacaoAutorizacaoTiss.Autorizada or SituacaoAutorizacaoTiss.EmAnalise)) continue;
            a.Situacao = SituacaoAutorizacaoTiss.CancelamentoPedido;
            a.AtualizadaEm = agora;
            var quem = string.IsNullOrWhiteSpace(operador) ? "?" : operador.Trim();
            await repo.AdicionarTransacaoTissAsync(new TransacaoTiss
            {
                Tipo = TipoTransacaoTiss.CancelaAutorizacao, ConvenioCodigo = a.ConvenioCodigo, PacienteId = a.PacienteId,
                NumeroCarteira = a.NumeroCarteira, NumeroGuiaPrestador = a.NumeroGuiaPrestador, NumeroGuiaOperadora = a.NumeroGuiaOperadora,
                AutorizacaoId = a.Id, Estado = EstadoTransacaoTiss.Pendente, PedidoPor = quem, PedidoEm = agora
            }, ct);
            await repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Operador = quem, Acao = "CancelamentoAutorizacaoTissPedido",
                Detalhe = $"Horário desmarcado: cancelamento da autorização {a.NumeroGuiaPrestador} pedido à operadora ({a.ConvenioCodigo})",
                PacienteId = a.PacienteId
            }, ct);
        }
    }

    /// <summary>
    /// A recepção digita o token que o paciente mostra no app, COM ELE PRESENTE (a baixa é dias depois).
    /// O token é dado de autenticação do beneficiário: fica só na autorização (e depois na guia), e a
    /// trilha diz quem digitou e quando — nunca o token. Devolve o erro para o campo, ou nulo.
    /// </summary>
    public async Task<string?> RegistrarTokenAsync(int autorizacaoId, string? token, string operador, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(operador)) return "Faça login antes de registrar o token.";
        var limpo = (token ?? string.Empty).Trim();
        if (limpo.Length != GuiaAnsNaBaixa.DigitosDoToken || !limpo.All(char.IsAsciiDigit))
            return $"O token tem {GuiaAnsNaBaixa.DigitosDoToken} dígitos (o paciente vê no app da operadora).";
        var a = await repo.ObterAutorizacaoTissAsync(autorizacaoId, ct);
        if (a is null || !a.ValidaEm(DateOnly.FromDateTime(_agora()))) return "A autorização deste horário não está valendo: peça de novo antes do token.";
        var corrigiu = a.ConfirmacaoResolvida;
        a.Token = limpo;
        a.AusenciaToken = null;
        a.TokenRegistradoPor = operador.Trim();
        a.TokenRegistradoEm = _agora();
        await repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = a.TokenRegistradoPor, Acao = corrigiu ? "TokenBeneficiarioCorrigido" : "TokenBeneficiarioRegistrado",
            Detalhe = $"Token do beneficiário {(corrigiu ? "corrigido" : "registrado")} na recepção (autorização {a.NumeroGuiaPrestador}, {a.ConvenioCodigo})",
            PacienteId = a.PacienteId
        }, ct);
        await repo.SalvarAsync(ct);
        return null;
    }

    /// <summary>O paciente não confirmou (sem app, sem sinal…): o motivo da tabela TISS fica no atendimento.</summary>
    public async Task<string?> RegistrarSemTokenAsync(int autorizacaoId, string? motivo, string operador, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(operador)) return "Faça login antes de registrar.";
        if (!DominiosTiss.Contem(GuiaAnsNaBaixa.MotivosSemToken, motivo)) return "Escolha o motivo de o paciente não ter confirmado.";
        var a = await repo.ObterAutorizacaoTissAsync(autorizacaoId, ct);
        if (a is null || !a.ValidaEm(DateOnly.FromDateTime(_agora()))) return "A autorização deste horário não está valendo.";
        a.Token = null;
        a.AusenciaToken = motivo;
        a.TokenRegistradoPor = operador.Trim();
        a.TokenRegistradoEm = _agora();
        await repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = a.TokenRegistradoPor, Acao = "TokenBeneficiarioAusente",
            Detalhe = $"Paciente sem token na recepção: {DominiosTiss.Termo(GuiaAnsNaBaixa.MotivosSemToken, motivo)} (autorização {a.NumeroGuiaPrestador}, {a.ConvenioCodigo})",
            PacienteId = a.PacienteId
        }, ct);
        await repo.SalvarAsync(ct);
        return null;
    }

    /// <summary>
    /// A frase da trilha quando a baixa mudou a senha ou o token que vieram da autorização e da recepção
    /// (nulo = nada mudou). Diz O QUE mudou — nunca o token.
    /// </summary>
    public static string? DivergenciaNaBaixa(AutorizacaoTiss a, DadosGuiaConsultaAns guia)
    {
        var mudou = new List<string>();
        if (!string.Equals(a.Senha?.Trim(), guia.Senha?.Trim(), StringComparison.Ordinal)) mudou.Add("a senha");
        if (!string.Equals(a.Token?.Trim() ?? "", guia.CodigoValidacao?.Trim() ?? "", StringComparison.Ordinal)
            || !string.Equals(a.AusenciaToken ?? "", guia.AusenciaCodigoValidacao ?? "", StringComparison.Ordinal))
            mudou.Add(a.ConfirmacaoResolvida ? "o token da recepção" : "o token (a recepção não tinha registrado)");
        return mudou.Count == 0 ? null : $"Na baixa, mudou {string.Join(" e ", mudou)} da autorização {a.NumeroGuiaPrestador}";
    }

    /// <summary>
    /// O aviso amarelo da agenda: autorizações valendo hoje sem token e sem motivo — enquanto o paciente
    /// ainda está na clínica dá para pedir. Nulo = nada faltando. Uma frase por convênio.
    /// </summary>
    public static string? AvisoDeTokenFaltando(IEnumerable<AutorizacaoTiss> doDia, DateOnly hoje)
    {
        var faltando = doDia.Where(a => a.ValidaEm(hoje) && !a.ConfirmacaoResolvida)
            .GroupBy(a => a.ConvenioCodigo).Select(g => (Nome: CatalogoConvenios.NomeOuCodigo(g.Key), Qtd: g.Select(a => a.AgendamentoId).Distinct().Count()))
            .ToList();
        if (faltando.Count == 0) return null;
        return "Faltou o token de " + string.Join(" e ", faltando.Select(f => $"{f.Qtd} paciente{(f.Qtd == 1 ? "" : "s")} {f.Nome}"))
               + " hoje. Peça ao paciente enquanto ele está na clínica: na baixa ele já foi embora.";
    }

    /// <summary>O selo da linha da agenda para a autorização mais recente do horário (nulo = nada a mostrar).</summary>
    public static SeloDeAutorizacaoTiss Selo(AutorizacaoTiss? a, DateOnly hoje) => a switch
    {
        null => new("", "", null, PodePedir: true),
        { } x when x.ValidaEm(hoje) && !string.IsNullOrWhiteSpace(x.Token) => new("Autorizado · token ok", "sucesso",
            $"Token {AutorizacaoTiss.Mascarar(x.Token)} digitado por {x.TokenRegistradoPor}{(x.TokenRegistradoEm is { } e ? $" às {e:HH:mm}" : "")}.", PodePedir: false),
        { } x when x.ValidaEm(hoje) && !string.IsNullOrWhiteSpace(x.AusenciaToken) => new("Não confirmou", "aviso",
            DominiosTiss.Termo(GuiaAnsNaBaixa.MotivosSemToken, x.AusenciaToken), PodePedir: false),
        { } x when x.ValidaEm(hoje) => new("Autorizado · peça o token ao paciente", "sucesso",
            $"Autorizado pela operadora{(x.DataAutorizacao is { } d ? $" em {d:dd/MM}" : "")}{(x.ValidadeSenha is { } v ? $", senha válida até {v:dd/MM}" : "")}.", PodePedir: false),
        { Situacao: SituacaoAutorizacaoTiss.Pedida } => new("Pedindo autorização…", "info", "O pedido está na fila do serviço de envio.", false),
        { Situacao: SituacaoAutorizacaoTiss.EmAnalise } => new("Autorização em análise", "aviso", "A operadora está analisando; o sistema pergunta de novo sozinho.", false),
        { Situacao: SituacaoAutorizacaoTiss.Negada } x => new("Autorização negada", "perigo", x.Motivo, PodePedir: true),
        { Situacao: SituacaoAutorizacaoTiss.NaoSeSabe } x => new("Autorização sem resposta", "aviso",
            (x.Motivo ?? "A operadora não respondeu") + " — não é negativa. Confira antes de pedir de novo.", PodePedir: true),
        { Situacao: SituacaoAutorizacaoTiss.Autorizada } => new("Autorização vencida", "aviso", "A senha passou da validade: peça de novo.", PodePedir: true),
        _ => new("", "", null, PodePedir: true)
    };
}
