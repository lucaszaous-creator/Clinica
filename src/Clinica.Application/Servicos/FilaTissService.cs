using System.Text.RegularExpressions;
using System.Xml.Linq;
using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>O que o conector grava quando a operadora respondeu e a transação deu certo.</summary>
public sealed record ConclusaoTransacaoTiss
{
    /// <summary>A frase curta para a tela.</summary>
    public required string Resultado { get; init; }

    public string? Protocolo { get; init; }

    public string? NumeroGuiaOperadora { get; init; }

    /// <summary>A resposta da operadora, como veio.</summary>
    public string? XmlRecebido { get; init; }
}

/// <summary>As constantes da fila: quantas vezes se tenta, quanto se espera e quando uma reserva vence.</summary>
public static class FilaTiss
{
    /// <summary>Elegibilidade e status: até três idas à operadora antes de pedir que alguém olhe.</summary>
    public const int MaximoDeTentativas = 3;

    /// <summary>Reserva sem desfecho por mais tempo que isto é de um conector que morreu no meio.</summary>
    public static readonly TimeSpan ReservaVence = TimeSpan.FromMinutes(5);

    /// <summary>Quantas linhas o conector olha por vez ao procurar o que reservar.</summary>
    public const int Lote = 10;

    /// <summary>A espera antes da próxima tentativa: 1 min, depois 5 min.</summary>
    public static TimeSpan Espera(int tentativasFeitas) => tentativasFeitas <= 1 ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(5);

    public const string FraseEnvioInterrompido =
        "O envio foi interrompido depois de começar e não se sabe se a operadora recebeu. "
        + "Confira no portal da operadora antes de pedir um novo envio.";

    public const string FraseSemNovaTentativa =
        "A mensagem chegou a sair para a operadora; ela não é reenviada sozinha. "
        + "Confira no portal da operadora antes de pedir um novo envio.";

    /// <summary>Tira a senha de qualquer XML TISS antes de ele ser guardado.</summary>
    public static string? SemSenha(string? xml)
    {
        if (string.IsNullOrEmpty(xml)) return xml;
        try
        {
            var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            var achou = false;
            foreach (var senha in doc.Descendants().Where(e => e.Name.LocalName == "senhaPrestador"))
            {
                senha.Value = Mascara;
                achou = true;
            }
            if (!achou) return xml;
            var declaracao = doc.Declaration is null ? string.Empty : doc.Declaration.ToString();
            return declaracao + doc.ToString(SaveOptions.DisableFormatting);
        }
        catch (System.Xml.XmlException)
        {
            // Não é XML bem formado: ainda assim a senha não pode ficar. Corta pelo texto.
            return SenhaNoTexto.Replace(xml, "$1" + Mascara + "$3");
        }
    }

    private const string Mascara = "********************************";

    private static readonly Regex SenhaNoTexto = new(
        @"(<(?:\w+:)?senhaPrestador\b[^>]*>)(.*?)(</(?:\w+:)?senhaPrestador\s*>)",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
}

/// <summary>
/// A fila do web service TISS (nov/2026): a tela PEDE, o conector EXECUTA, e este serviço é o
/// único lugar que muda o estado de uma transação.
///
/// As regras que moram aqui, e não na tela nem no conector:
/// <list type="bullet">
/// <item><b>Quem pede é quem fez login.</b> Todo pedido leva o operador e grava a trilha no
/// mesmo salvamento.</item>
/// <item><b>Uma guia não entra duas vezes.</b> Pedido de envio para guia que já tem envio na
/// fila, em andamento ou concluído com protocolo é recusado com a frase do motivo.</item>
/// <item><b>Dois conectores não pegam a mesma linha.</b> A reserva é um UPDATE condicional
/// pela versão (<c>ReservarTransacaoTissAsync</c>).</item>
/// <item><b>Envio de guia nunca é reenviado sozinho.</b> Depois que a mensagem começou a sair
/// (<c>EnvioIniciadoEm</c>), a transação que ESCREVE na operadora só termina: concluída ou
/// falhou. Rede que cai no meio não diz se a operadora recebeu, e reenviar é arriscar a guia
/// em duplicidade. Só elegibilidade e status, que apenas perguntam, tentam de novo.</item>
/// <item><b>A senha não é guardada.</b> Todo XML passa por <see cref="FilaTiss.SemSenha"/>
/// antes de ir para a linha, tenha ou não sido mascarado por quem chamou.</item>
/// </list>
/// </summary>
public sealed class FilaTissService
{
    private readonly IClinicaRepositorio _repo;
    private readonly Func<DateTime> _agora;

    public FilaTissService(IClinicaRepositorio repo) : this(repo, HoraDaClinica.Agora) { }

    public FilaTissService(IClinicaRepositorio repo, Func<DateTime> agora)
    {
        _repo = repo;
        _agora = agora;
    }

    // ==================== A tela pede ====================

    /// <summary>Pede a conferência de uma carteira na operadora.</summary>
    public Task<TransacaoTiss> EnfileirarElegibilidadeAsync(
        string convenioCodigo, string numeroCarteira, DateOnly? validadeCarteira, int? pacienteId, string operador,
        CancellationToken ct = default)
    {
        var carteira = (numeroCarteira ?? string.Empty).Trim();
        if (carteira.Length is 0 or > 20)
            throw new InvalidOperationException("Informe o número da carteira (até 20 caracteres) para conferir a elegibilidade.");
        return EnfileirarAsync(new TransacaoTiss
        {
            Tipo = TipoTransacaoTiss.Elegibilidade,
            NumeroCarteira = carteira,
            ValidadeCarteira = validadeCarteira,
            PacienteId = pacienteId
        }, convenioCodigo, operador, "PedidoElegibilidadeTiss", "Pedido de elegibilidade pelo web service", ct);
    }

    /// <summary>
    /// Pede o envio de UMA guia à operadora. É este pedido, feito por quem está logado, que
    /// autoriza o envio daquela guia — o conector não envia nada que não tenha passado por aqui.
    /// </summary>
    public async Task<TransacaoTiss> EnfileirarEnvioGuiaAsync(
        string convenioCodigo, int codigoId, int? pacienteId, string operador, CancellationToken ct = default)
    {
        foreach (var anterior in await _repo.TransacoesTissDaGuiaAsync(codigoId, ct))
        {
            if (anterior.Tipo != TipoTransacaoTiss.EnvioGuia) continue;
            if (anterior.Estado is EstadoTransacaoTiss.Pendente or EstadoTransacaoTiss.EmEnvio)
                throw new InvalidOperationException("Esta guia já tem um envio à operadora na fila. Acompanhe o que já foi pedido.");
            if (anterior.Estado == EstadoTransacaoTiss.Concluida && !string.IsNullOrWhiteSpace(anterior.Protocolo))
                throw new InvalidOperationException(
                    $"Esta guia já foi enviada à operadora (protocolo {anterior.Protocolo}). Para enviar de novo, cancele a guia na operadora antes.");
        }
        return await EnfileirarAsync(new TransacaoTiss
        {
            Tipo = TipoTransacaoTiss.EnvioGuia,
            CodigoId = codigoId,
            PacienteId = pacienteId
        }, convenioCodigo, operador, "PedidoEnvioGuiaTiss", "Envio da guia à operadora autorizado", ct);
    }

    /// <summary>Pede a situação de um protocolo que a operadora já devolveu.</summary>
    public Task<TransacaoTiss> EnfileirarStatusProtocoloAsync(
        string convenioCodigo, string protocolo, int? codigoId, string operador, CancellationToken ct = default)
    {
        var numero = (protocolo ?? string.Empty).Trim();
        if (numero.Length is 0 or > 20)
            throw new InvalidOperationException("Informe o protocolo devolvido pela operadora para consultar a situação.");
        return EnfileirarAsync(new TransacaoTiss
        {
            Tipo = TipoTransacaoTiss.StatusProtocolo,
            Protocolo = numero,
            CodigoId = codigoId
        }, convenioCodigo, operador, "PedidoStatusProtocoloTiss", $"Consulta do protocolo {numero}", ct);
    }

    /// <summary>Pede o cancelamento, na operadora, de uma guia já enviada.</summary>
    public Task<TransacaoTiss> EnfileirarCancelamentoGuiaAsync(
        string convenioCodigo, int codigoId, string numeroGuiaPrestador, string? protocolo, string operador,
        CancellationToken ct = default)
    {
        var numero = (numeroGuiaPrestador ?? string.Empty).Trim();
        if (numero.Length is 0 or > 20)
            throw new InvalidOperationException("Informe o número da guia (até 20 caracteres) para pedir o cancelamento.");
        return EnfileirarAsync(new TransacaoTiss
        {
            Tipo = TipoTransacaoTiss.CancelamentoGuia,
            CodigoId = codigoId,
            NumeroGuiaPrestador = numero,
            Protocolo = string.IsNullOrWhiteSpace(protocolo) ? null : protocolo.Trim()
        }, convenioCodigo, operador, "PedidoCancelamentoGuiaTiss", $"Cancelamento da guia {numero} na operadora autorizado", ct);
    }

    /// <summary>
    /// Quem pediu desiste — só enquanto a transação ainda está na fila. Devolve falso quando o
    /// conector já a pegou (ou ela já terminou): aí não há o que desistir.
    /// </summary>
    public async Task<bool> CancelarAsync(int id, string operador, CancellationToken ct = default)
    {
        var quem = Operador(operador);
        var t = await _repo.ObterTransacaoTissAsync(id, ct)
            ?? throw new InvalidOperationException("Transação TISS não encontrada.");
        if (t.Estado != EstadoTransacaoTiss.Pendente) return false;

        Encerrar(t, EstadoTransacaoTiss.Cancelada, _agora());
        t.Resultado = Cortar($"Cancelada por {quem} antes de ir à operadora.", TransacaoTissLimites.Resultado);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = quem, Acao = "CancelamentoPedidoTiss",
            Detalhe = $"{t.Tipo} cancelada antes do envio", CodigoId = t.CodigoId, PacienteId = t.PacienteId
        }, ct);
        return await SalvarSeAindaForMinhaAsync(ct);
    }

    // ==================== A tela acompanha ====================

    /// <summary>A transação, sem os XML. Nulo se não existe.</summary>
    public async Task<TransacaoTiss?> ObterAsync(int id, CancellationToken ct = default)
    {
        var t = await _repo.ObterTransacaoTissAsync(id, ct);
        return t is null ? null : SemXml(t);
    }

    /// <summary>As transações de uma guia, da mais nova para a mais velha, sem os XML.</summary>
    public Task<IReadOnlyList<TransacaoTiss>> DaGuiaAsync(int codigoId, CancellationToken ct = default)
        => _repo.TransacoesTissDaGuiaAsync(codigoId, ct);

    /// <summary>As últimas transações (de um convênio, ou de todos), sem os XML.</summary>
    public Task<IReadOnlyList<TransacaoTiss>> RecentesAsync(string? convenioCodigo = null, int limite = 50, CancellationToken ct = default)
        => _repo.TransacoesTissRecentesAsync(string.IsNullOrWhiteSpace(convenioCodigo) ? null : convenioCodigo.Trim(),
            Math.Clamp(limite, 1, 200), ct);

    // ==================== O conector executa ====================

    /// <summary>
    /// Reserva a próxima transação para <paramref name="consumidor"/> e a devolve inteira e
    /// rastreada, ou nulo quando não há nada a fazer. Transação que ESCREVE na operadora e ficou
    /// presa depois de a mensagem ter saído não é devolvida: é encerrada como falha, com a frase
    /// que manda conferir no portal, e a procura segue para a próxima.
    /// </summary>
    public async Task<TransacaoTiss?> ReservarProximaAsync(string consumidor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(consumidor))
            throw new InvalidOperationException("O conector precisa se identificar para reservar uma transação.");
        consumidor = Cortar(consumidor.Trim(), 80)!;

        // Algumas voltas bastam: cada volta ou reserva, ou encerra uma presa, ou perde a corrida.
        for (var volta = 0; volta < FilaTiss.Lote * 2; volta++)
        {
            var agora = _agora();
            var candidatas = await _repo.TransacoesTissReservaveisAsync(agora, agora - FilaTiss.ReservaVence, FilaTiss.Lote, ct);
            if (candidatas.Count == 0) return null;

            foreach (var candidata in candidatas)
            {
                if (!await _repo.ReservarTransacaoTissAsync(candidata.Id, candidata.Versao, consumidor, agora, ct))
                    continue; // outro conector chegou antes

                var t = await _repo.ObterTransacaoTissAsync(candidata.Id, ct);
                if (t is null) continue;
                // A reserva foi um UPDATE direto: se a linha já estava rastreada neste escopo, relê.
                await _repo.RecarregarTransacaoTissAsync(t, ct);

                if (t.EscreveNaOperadora && t.EnvioIniciadoEm is not null)
                {
                    // Estava EmEnvio com a reserva vencida E a mensagem já tinha saído.
                    Encerrar(t, EstadoTransacaoTiss.Falhou, agora);
                    t.Erro = FilaTiss.FraseEnvioInterrompido;
                    t.Resultado = "Envio interrompido: confira no portal da operadora.";
                    await _repo.SalvarAsync(ct);
                    break; // relê a fila
                }

                if (t.Tentativas > FilaTiss.MaximoDeTentativas)
                {
                    // Reserva vencida repetidas vezes (o conector morre sempre nesta linha).
                    Encerrar(t, EstadoTransacaoTiss.Falhou, agora);
                    t.Erro = $"Abandonada depois de {FilaTiss.MaximoDeTentativas} tentativas sem desfecho.";
                    t.Resultado = "Não foi possível falar com a operadora.";
                    await _repo.SalvarAsync(ct);
                    break;
                }

                return t;
            }
        }
        return null;
    }

    /// <summary>
    /// Carimba que a mensagem VAI sair agora e guarda o XML (sem a senha). Chamado
    /// imediatamente antes da ida à rede: a partir daqui, envio de guia não volta para a fila.
    /// </summary>
    public async Task MarcarEnvioIniciadoAsync(int id, string? xmlEnviado, CancellationToken ct = default)
    {
        var t = await EmEnvioAsync(id, ct);
        t.EnvioIniciadoEm = _agora();
        t.XmlEnviado = FilaTiss.SemSenha(xmlEnviado);
        t.Versao++;
        await _repo.SalvarAsync(ct);
    }

    /// <summary>A operadora respondeu e deu certo: grava o resultado e encerra.</summary>
    public async Task ConcluirAsync(int id, ConclusaoTransacaoTiss conclusao, CancellationToken ct = default)
    {
        var t = await EmEnvioAsync(id, ct);
        var agora = _agora();
        Encerrar(t, EstadoTransacaoTiss.Concluida, agora);
        t.Resultado = Cortar(conclusao.Resultado, TransacaoTissLimites.Resultado);
        t.Erro = null;
        // Na consulta de status o protocolo é dado do PEDIDO: resposta sem protocolo não o apaga.
        if (!string.IsNullOrWhiteSpace(conclusao.Protocolo)) t.Protocolo = Cortar(conclusao.Protocolo.Trim(), 20);
        if (!string.IsNullOrWhiteSpace(conclusao.NumeroGuiaOperadora)) t.NumeroGuiaOperadora = Cortar(conclusao.NumeroGuiaOperadora.Trim(), 20);
        t.XmlRecebido = FilaTiss.SemSenha(conclusao.XmlRecebido);

        if (t.EscreveNaOperadora)
            // O que mudou algo na operadora deixa rastro próprio, no mesmo salvamento.
            await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Operador = t.PedidoPor,
                Acao = t.Tipo == TipoTransacaoTiss.EnvioGuia ? "EnvioGuiaTissConcluido" : "CancelamentoGuiaTissConcluido",
                Detalhe = Cortar(t.Resultado + (t.Protocolo is null ? "" : $" (protocolo {t.Protocolo})"), 500),
                CodigoId = t.CodigoId,
                PacienteId = t.PacienteId
            }, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// Não deu certo. Devolve o estado em que a transação ficou: <c>Pendente</c> quando ela vai
    /// ser tentada de novo (só elegibilidade e status, e só até o máximo de tentativas), ou
    /// <c>Falhou</c>. Transação que escreve na operadora e cuja mensagem já saiu NUNCA volta
    /// para a fila, peça quem pedir.
    /// </summary>
    public async Task<EstadoTransacaoTiss> FalharAsync(
        int id, string erro, bool podeTentarDeNovo, string? xmlRecebido = null, CancellationToken ct = default)
    {
        var t = await EmEnvioAsync(id, ct);
        var agora = _agora();
        t.Erro = Cortar(string.IsNullOrWhiteSpace(erro) ? "Falha sem descrição." : erro.Trim(), TransacaoTissLimites.Erro);
        if (xmlRecebido is not null) t.XmlRecebido = FilaTiss.SemSenha(xmlRecebido);

        var mensagemSaiu = t.EnvioIniciadoEm is not null;
        var repete = podeTentarDeNovo
                     && !(t.EscreveNaOperadora && mensagemSaiu)
                     && t.Tentativas < FilaTiss.MaximoDeTentativas;
        if (repete)
        {
            t.Estado = EstadoTransacaoTiss.Pendente;
            t.Consumidor = null;
            t.ReservadaEm = null;
            t.ProximaTentativaEm = agora + FilaTiss.Espera(t.Tentativas);
            t.Resultado = Cortar($"Tentativa {t.Tentativas} de {FilaTiss.MaximoDeTentativas} sem sucesso; o sistema tenta de novo.", TransacaoTissLimites.Resultado);
            t.Versao++;
        }
        else
        {
            Encerrar(t, EstadoTransacaoTiss.Falhou, agora);
            if (t.EscreveNaOperadora && mensagemSaiu && podeTentarDeNovo)
                t.Erro = Cortar(t.Erro + " " + FilaTiss.FraseSemNovaTentativa, TransacaoTissLimites.Erro);
            t.Resultado = Cortar(t.EscreveNaOperadora && mensagemSaiu
                ? "Falhou: confira no portal da operadora antes de pedir de novo."
                : "Falhou: " + t.Erro, TransacaoTissLimites.Resultado);
        }
        await _repo.SalvarAsync(ct);
        return t.Estado;
    }

    /// <summary>
    /// O conector se recusa a levar a transação à operadora (envio desligado para o convênio,
    /// credencial ausente). Nada saiu: a transação termina como <c>Cancelada</c>, com o motivo.
    /// </summary>
    public async Task RecusarAsync(int id, string motivo, CancellationToken ct = default)
    {
        var t = await EmEnvioAsync(id, ct);
        if (t.EnvioIniciadoEm is not null)
            throw new InvalidOperationException("A mensagem já saiu para a operadora: a transação não pode mais ser recusada.");
        Encerrar(t, EstadoTransacaoTiss.Cancelada, _agora());
        t.Resultado = Cortar(motivo, TransacaoTissLimites.Resultado);
        await _repo.SalvarAsync(ct);
    }

    // ==================== peças ====================

    private async Task<TransacaoTiss> EnfileirarAsync(
        TransacaoTiss t, string convenioCodigo, string operador, string acao, string detalhe, CancellationToken ct)
    {
        var convenio = (convenioCodigo ?? string.Empty).Trim();
        if (convenio.Length is 0 or > 40)
            throw new InvalidOperationException("Informe o convênio da transação.");
        t.ConvenioCodigo = convenio;
        t.PedidoPor = Operador(operador);
        t.PedidoEm = _agora();
        t.Estado = EstadoTransacaoTiss.Pendente;
        await _repo.AdicionarTransacaoTissAsync(t, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = t.PedidoPor, Acao = acao, Detalhe = $"{detalhe} ({convenio})",
            CodigoId = t.CodigoId, PacienteId = t.PacienteId
        }, ct);
        await _repo.SalvarAsync(ct);
        return t;
    }

    private async Task<TransacaoTiss> EmEnvioAsync(int id, CancellationToken ct)
    {
        var t = await _repo.ObterTransacaoTissAsync(id, ct)
            ?? throw new InvalidOperationException("Transação TISS não encontrada.");
        if (t.Estado != EstadoTransacaoTiss.EmEnvio)
            throw new InvalidOperationException(
                $"A transação TISS {id} não está reservada (estado {t.Estado}): só quem a reservou grava o desfecho.");
        return t;
    }

    private static void Encerrar(TransacaoTiss t, EstadoTransacaoTiss estado, DateTime agora)
    {
        t.Estado = estado;
        t.ProcessadaEm = agora;
        t.ProximaTentativaEm = null;
        t.Versao++;
    }

    private async Task<bool> SalvarSeAindaForMinhaAsync(CancellationToken ct)
    {
        try
        {
            await _repo.SalvarAsync(ct);
            return true;
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
            // O conector reservou entre a leitura e a gravação: não há mais o que desistir.
            return false;
        }
    }

    private static string Operador(string? operador)
        => string.IsNullOrWhiteSpace(operador)
            ? throw new InvalidOperationException("Toda transação com a operadora leva quem a pediu: faça login antes.")
            : Cortar(operador.Trim(), 120)!;

    private static string? Cortar(string? texto, int maximo)
        => texto is null || texto.Length <= maximo ? texto : texto[..maximo];

    private static TransacaoTiss SemXml(TransacaoTiss t) => new()
    {
        Id = t.Id, Tipo = t.Tipo, ConvenioCodigo = t.ConvenioCodigo, CodigoId = t.CodigoId, PacienteId = t.PacienteId,
        Estado = t.Estado, Tentativas = t.Tentativas, PedidoPor = t.PedidoPor, PedidoEm = t.PedidoEm,
        ProcessadaEm = t.ProcessadaEm, NumeroCarteira = t.NumeroCarteira, ValidadeCarteira = t.ValidadeCarteira,
        NumeroGuiaPrestador = t.NumeroGuiaPrestador, Protocolo = t.Protocolo, NumeroGuiaOperadora = t.NumeroGuiaOperadora,
        Resultado = t.Resultado, Erro = t.Erro, Consumidor = t.Consumidor, ReservadaEm = t.ReservadaEm,
        EnvioIniciadoEm = t.EnvioIniciadoEm, ProximaTentativaEm = t.ProximaTentativaEm, Versao = t.Versao
    };
}
