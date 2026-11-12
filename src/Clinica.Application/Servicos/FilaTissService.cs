using System.Text.RegularExpressions;
using System.Xml.Linq;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
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

    /// <summary>
    /// O que a operadora disse de cada guia do lote (envio e status). Vazio = ela respondeu pelo
    /// lote sem detalhar as guias: no envio com protocolo, todas contam como recebidas.
    /// </summary>
    public IReadOnlyList<ResultadoDaGuiaTiss> Guias { get; init; } = [];
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

    /// <summary>
    /// A MARCA DA DÚVIDA: a ordem com que terminam as duas frases abaixo. É por ela que se reconhece,
    /// no erro gravado, o envio que saiu e ficou sem resposta em que confiar — e que se distingue
    /// dele a RECUSA CLARA da operadora (ela respondeu que não: o erro traz o motivo, sem a marca).
    /// </summary>
    public const string MarcaDeDuvida = "Confira no portal da operadora antes de pedir um novo envio.";

    public const string FraseEnvioInterrompido =
        "O envio foi interrompido depois de começar e não se sabe se a operadora recebeu. " + MarcaDeDuvida;

    public const string FraseSemNovaTentativa =
        "A mensagem chegou a sair para a operadora; ela não é reenviada sozinha. " + MarcaDeDuvida;

    /// <summary>
    /// O erro no tamanho da coluna SEM perder a marca da dúvida: ela vem no fim do texto, e um corte
    /// simples a levaria embora — a guia em dúvida passaria por recusa clara e voltaria a ser enviável.
    /// </summary>
    public static string CortarErro(string erro, int maximo)
    {
        if (erro.Length <= maximo) return erro;
        if (!erro.Contains(MarcaDeDuvida, StringComparison.Ordinal)) return erro[..maximo];
        var semMarca = erro.Replace(FraseSemNovaTentativa, string.Empty, StringComparison.Ordinal)
            .Replace(FraseEnvioInterrompido, string.Empty, StringComparison.Ordinal)
            .Replace(MarcaDeDuvida, string.Empty, StringComparison.Ordinal).TrimEnd();
        var cabe = Math.Max(0, maximo - FraseSemNovaTentativa.Length - 1);
        return (semMarca.Length <= cabe ? semMarca : semMarca[..cabe]) + " " + FraseSemNovaTentativa;
    }

    /// <summary>
    /// A recusa do pedido novo de uma guia cujo último envio ficou em dúvida, quando quem pede não
    /// declarou ter conferido no portal. Diz o que fazer nos dois casos.
    /// </summary>
    public const string RecusaSemConferenciaNoPortal =
        "Não se sabe se o último envio desta guia chegou à operadora. Confira no portal antes de enviar de novo: "
        + "se ela estiver lá, dê baixa com o número do portal; se não estiver, declare a conferência para liberar o novo envio.";

    /// <summary>
    /// NÃO SE SABE SE CHEGOU: a transação escreve na operadora, a mensagem saiu, ela terminou em
    /// falha e o erro gravado traz a <see cref="MarcaDeDuvida"/>. É o estado em que reenviar pode
    /// apresentar a mesma guia duas vezes — a fila o marca assim tanto para "ninguém respondeu"
    /// quanto para "a resposta não deixa saber". A RECUSA CLARA (a operadora respondeu que não, com
    /// o motivo) também é falha de mensagem que saiu, e NÃO é dúvida: nada ficou lá, e reenviar a
    /// guia corrigida é o caminho. Um lugar só decide: a guarda do reenvio (aqui) e o destaque da
    /// tela usam esta mesma pergunta. Quem a faz precisa ter lido o <see cref="TransacaoTiss.Erro"/>.
    /// </summary>
    public static bool NaoSeSabeSeChegou(TransacaoTiss? t)
        => t is { EscreveNaOperadora: true, Estado: EstadoTransacaoTiss.Falhou, EnvioIniciadoEm: not null }
           && t.Erro is { } erro && erro.Contains(MarcaDeDuvida, StringComparison.Ordinal);

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
/// <item><b>O envio é um LOTE</b> de 1 a 100 guias do mesmo convênio, todas com a guia ANS
/// conferida (que sai sempre como guia de consulta) e sem baixa. O resultado da operadora volta POR GUIA.</item>
/// <item><b>Uma guia não entra duas vezes.</b> Guia que está num envio na fila, em andamento ou
/// já recebido pela operadora é recusada, com a frase na linha dela.</item>
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
    /// Confere um pedido de envio SEM gravar nada: o que impede o lote e o que impede cada guia.
    /// É a mesma conta que <see cref="EnfileirarEnvioAsync"/> faz — a tela a usa para avisar na
    /// linha, antes do clique; o serviço, para recusar.
    /// </summary>
    /// <param name="conferidasNoPortal">
    /// As guias que quem pede DECLARA ter conferido no portal da operadora e não ter achado lá.
    /// Só importa para a guia cujo último envio ficou em dúvida (<see cref="FilaTiss.NaoSeSabeSeChegou"/>):
    /// sem a declaração, ela é recusada.
    /// </param>
    public async Task<ConferenciaDeEnvioTiss> ConferirEnvioAsync(
        string convenioCodigo, IReadOnlyList<int> codigoIds, CancellationToken ct = default,
        IReadOnlyCollection<int>? conferidasNoPortal = null)
        => (await MontarLoteAsync(convenioCodigo, codigoIds, conferidasNoPortal, ct)).Conferencia;

    /// <summary>
    /// Pede o envio de um LOTE de guias à operadora (de 1 a 100, do mesmo convênio). É este pedido, feito por quem está logado, que autoriza o envio DAQUELAS guias — o
    /// conector não envia nada que não tenha passado por aqui. Qualquer guia recusada recusa o
    /// pedido inteiro (<see cref="EnvioTissRecusadoException"/>, com a frase de cada linha): lote
    /// enviado pela metade sem a pessoa saber é pior que lote não enviado.
    /// </summary>
    /// <param name="conferidasNoPortal">
    /// As guias que <paramref name="operador"/> declara ter conferido no portal e não ter achado
    /// lá. É o que libera o pedido novo de uma guia cujo último envio ficou em dúvida — e a
    /// declaração fica na trilha de auditoria, com o nome de quem a fez, no mesmo ato do pedido.
    /// </param>
    public async Task<TransacaoTiss> EnfileirarEnvioAsync(
        string convenioCodigo, IReadOnlyList<int> codigoIds, string operador, CancellationToken ct = default,
        IReadOnlyCollection<int>? conferidasNoPortal = null)
    {
        var quem = Operador(operador);
        var lote = await MontarLoteAsync(convenioCodigo, codigoIds, conferidasNoPortal, ct);
        if (!lote.Conferencia.PodeEnviar)
            throw new EnvioTissRecusadoException(lote.Conferencia);
        // A declaração de quem liberou o reenvio: uma linha por guia, gravada junto com o pedido
        // (o SaveChanges é o do EnfileirarAsync, logo abaixo — pedido sem a linha não existe).
        foreach (var guia in lote.Guias.Where(g => g.LiberadaPorConferenciaNoPortal))
            await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Operador = quem, Acao = "ReenvioTissAposConferenciaNoPortal",
                Detalhe = "Declarou ter conferido no portal da operadora que a guia do envio anterior (sem resposta) não está lá; novo envio autorizado",
                CodigoId = guia.Codigo.Id, PacienteId = guia.Codigo.Atendimento?.PacienteId
            }, ct);

        var unica = lote.Guias.Count == 1 ? lote.Guias[0] : null;
        return await EnfileirarAsync(new TransacaoTiss
        {
            Tipo = TipoTransacaoTiss.EnvioGuia,
            // Com uma guia só, a transação aponta para ela e para o paciente (a trilha diz de quem é).
            CodigoId = unica?.Codigo.Id,
            PacienteId = unica?.Codigo.Atendimento?.PacienteId,
            Guias = lote.Guias.Select(g => new TransacaoTissGuia
            {
                CodigoId = g.Codigo.Id, NumeroGuiaPrestador = g.NumeroGuiaPrestador, Situacao = SituacaoGuiaTiss.Pendente
            }).ToList()
        }, convenioCodigo, quem, "PedidoEnvioGuiaTiss",
            lote.Guias.Count == 1 ? "Envio da guia à operadora autorizado" : $"Envio de {lote.Guias.Count} guias à operadora autorizado", ct);
    }

    /// <summary>Uma guia só: é o lote com um item.</summary>
    public Task<TransacaoTiss> EnfileirarEnvioGuiaAsync(
        string convenioCodigo, int codigoId, string operador, CancellationToken ct = default)
        => EnfileirarEnvioAsync(convenioCodigo, [codigoId], operador, ct, null);

    /// <summary>
    /// As guias de um lote que a operadora RECEBEU e ainda não têm baixa: o que a baixa em lote
    /// tem a fazer. Guia glosada, falha ou sem resposta NÃO aparece — a baixa dela pede gente.
    /// </summary>
    public Task<IReadOnlyList<GuiaTissRecebidaSemBaixa>> GuiasRecebidasSemBaixaAsync(int transacaoId, CancellationToken ct = default)
        => _repo.GuiasTissRecebidasSemBaixaAsync(transacaoId, ct);

    /// <summary>
    /// Pede a situação do protocolo de um ENVIO já concluído. A resposta atualiza as MESMAS
    /// guias daquele lote (uma guia que estava sem resposta passa a recebida ou glosada).
    /// </summary>
    public async Task<TransacaoTiss> EnfileirarStatusDoEnvioAsync(int transacaoDeEnvioId, string operador, CancellationToken ct = default)
    {
        var envio = await _repo.ObterTransacaoTissAsync(transacaoDeEnvioId, ct);
        if (envio is null || envio.Tipo != TipoTransacaoTiss.EnvioGuia)
            throw new InvalidOperationException("Envio à operadora não encontrado.");
        if (string.IsNullOrWhiteSpace(envio.Protocolo))
            throw new InvalidOperationException("Este envio ainda não tem protocolo da operadora: não há o que consultar.");
        return await EnfileirarAsync(new TransacaoTiss
        {
            Tipo = TipoTransacaoTiss.StatusProtocolo,
            Protocolo = envio.Protocolo,
            CodigoId = envio.CodigoId,
            PacienteId = envio.PacienteId,
            TransacaoDeEnvioId = envio.Id
        }, envio.ConvenioCodigo, operador, "PedidoStatusProtocoloTiss", $"Consulta do protocolo {envio.Protocolo}", ct);
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
        GuiasSemEnvio(t, "O envio foi cancelado antes de ir à operadora.");
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = quem, Acao = "CancelamentoPedidoTiss",
            Detalhe = $"{t.Tipo} cancelada antes do envio", CodigoId = t.CodigoId, PacienteId = t.PacienteId
        }, ct);
        return await SalvarSeAindaForMinhaAsync(ct);
    }

    // ==================== A tela acompanha ====================

    /// <summary>A transação com as guias do lote, sem os XML. Nulo se não existe.</summary>
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

    /// <summary>
    /// As guias de um convênio que já foram CONFERIDAS e ainda não têm baixa nem estão num envio
    /// que as prenda: é a lista de onde a tela monta o lote.
    /// </summary>
    public Task<IReadOnlyList<GuiaProntaParaEnvioTiss>> ProntasParaEnvioAsync(string convenioCodigo, int limite = 200, CancellationToken ct = default)
        => _repo.GuiasTissProntasParaEnvioAsync((convenioCodigo ?? string.Empty).Trim(), Math.Clamp(limite, 1, 500), ct);

    /// <summary>
    /// O selo de envio de cada guia pedida, para a linha do painel. Guia que nunca entrou num envio
    /// também vem, como "não enviada": a tela não precisa distinguir "sem resposta" de "sem linha".
    /// </summary>
    public async Task<IReadOnlyDictionary<int, SeloDeEnvioTiss>> SelosDeEnvioAsync(IReadOnlyCollection<int> codigoIds, CancellationToken ct = default)
    {
        var ids = codigoIds.Distinct().ToList();
        // Do mais novo para o mais velho: o primeiro item de cada guia é o do último envio.
        var ultimo = (await _repo.ItensDeEnvioTissDasGuiasAsync(ids, ct))
            .Where(i => i.CodigoId is not null).GroupBy(i => i.CodigoId!.Value).ToDictionary(g => g.Key, g => g.First());
        return ids.ToDictionary(id => id, id => SeloDeEnvioTiss.Do(ultimo.GetValueOrDefault(id)));
    }

    /// <summary>Os últimos ENVIOS (de um convênio, ou de todos), cada um com as guias do lote, sem os XML.</summary>
    public Task<IReadOnlyList<TransacaoTiss>> EnviosRecentesAsync(string? convenioCodigo = null, int limite = 30, CancellationToken ct = default)
        => _repo.EnviosTissRecentesComGuiasAsync(string.IsNullOrWhiteSpace(convenioCodigo) ? null : convenioCodigo.Trim(),
            Math.Clamp(limite, 1, 100), ct);

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
                    GuiasSemEnvio(t, FilaTiss.FraseEnvioInterrompido);
                    await _repo.SalvarAsync(ct);
                    break; // relê a fila
                }

                if (t.Tentativas > FilaTiss.MaximoDeTentativas)
                {
                    // Reserva vencida repetidas vezes (o conector morre sempre nesta linha).
                    Encerrar(t, EstadoTransacaoTiss.Falhou, agora);
                    t.Erro = $"Abandonada depois de {FilaTiss.MaximoDeTentativas} tentativas sem desfecho.";
                    t.Resultado = "Não foi possível falar com a operadora.";
                    GuiasSemEnvio(t, t.Erro);
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

        if (t.Tipo == TipoTransacaoTiss.EnvioGuia)
            AplicarPorGuia(t, conclusao.Guias, agora, semDetalheContaComoRecebida: !string.IsNullOrWhiteSpace(t.Protocolo));
        else if (t.Tipo == TipoTransacaoTiss.StatusProtocolo && t.TransacaoDeEnvioId is { } envioId && conclusao.Guias.Count > 0
                 && await _repo.ObterTransacaoTissAsync(envioId, ct) is { } envio)
        {
            // A consulta de status atualiza as MESMAS guias do lote consultado.
            AplicarPorGuia(envio, conclusao.Guias, agora, semDetalheContaComoRecebida: false);
            envio.Versao++;
        }

        else if (t.Tipo == TipoTransacaoTiss.CancelamentoGuia && t.CodigoId is { } guiaCancelada)
            await MarcarCanceladaNoEnvioAsync(guiaCancelada, t.NumeroGuiaPrestador, agora, ct);

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
        t.Erro = FilaTiss.CortarErro(string.IsNullOrWhiteSpace(erro) ? "Falha sem descrição." : erro.Trim(), TransacaoTissLimites.Erro);
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
                t.Erro = FilaTiss.CortarErro(t.Erro + " " + FilaTiss.FraseSemNovaTentativa, TransacaoTissLimites.Erro);
            t.Resultado = Cortar(t.EscreveNaOperadora && mensagemSaiu
                ? "Falhou: confira no portal da operadora antes de pedir de novo."
                : "Falhou: " + t.Erro, TransacaoTissLimites.Resultado);
            // O lote falhou inteiro: cada guia dele fica marcada, com o mesmo motivo na linha.
            GuiasSemEnvio(t, t.Erro);
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
        GuiasSemEnvio(t, motivo);
        await _repo.SalvarAsync(ct);
    }

    // ==================== peças ====================

    private sealed record GuiaDoLote(CodigoFaturamento Codigo, string NumeroGuiaPrestador, bool LiberadaPorConferenciaNoPortal = false);

    /// <summary>
    /// Lê as guias pedidas e diz, uma a uma, se podem ir. É a ÚNICA conta: a conferência da tela
    /// e a recusa do serviço saem daqui.
    /// </summary>
    private async Task<(ConferenciaDeEnvioTiss Conferencia, IReadOnlyList<GuiaDoLote> Guias)> MontarLoteAsync(
        string convenioCodigo, IReadOnlyList<int>? codigoIds, IReadOnlyCollection<int>? conferidasNoPortal, CancellationToken ct)
    {
        var convenio = (convenioCodigo ?? string.Empty).Trim();
        if (convenio.Length is 0 or > 40)
            return (new ConferenciaDeEnvioTiss { Impedimento = "Informe o convênio do envio." }, []);
        if (codigoIds is null || codigoIds.Count == 0)
            return (new ConferenciaDeEnvioTiss { Impedimento = "Escolha ao menos uma guia para enviar à operadora." }, []);
        if (codigoIds.Count > TransacaoTissLimites.GuiasPorLote)
            return (new ConferenciaDeEnvioTiss
            {
                Impedimento = $"Um lote leva até {TransacaoTissLimites.GuiasPorLote} guias e este tem {codigoIds.Count}: divida o envio."
            }, []);

        var distintas = codigoIds.Distinct().ToList();
        var recusas = new Dictionary<int, string>();
        foreach (var repetida in codigoIds.GroupBy(id => id).Where(g => g.Count() > 1))
            recusas[repetida.Key] = "Esta guia aparece mais de uma vez na lista.";

        var codigos = (await _repo.CodigosParaEnvioTissAsync(distintas, ct)).ToDictionary(c => c.Id);
        var anteriores = (await _repo.ItensDeEnvioTissDasGuiasAsync(distintas, ct))
            .Where(i => i.CodigoId is not null).ToLookup(i => i.CodigoId!.Value);

        // "Um só tipo de guia por lote" (regra do padrão) está garantido pela conferência: toda guia
        // conferida sai como GUIA DE CONSULTA (TissExportService.GuiaConsultaConferida), seja qual
        // for o tipo no sistema — inclusive o BSV da Petrobras, que a clínica lança nessa tela.

        var guias = new List<GuiaDoLote>();
        foreach (var id in distintas)
        {
            if (recusas.ContainsKey(id)) continue;
            if (!codigos.TryGetValue(id, out var codigo))
            {
                recusas[id] = "Guia não encontrada: pode ter sido estornada noutra máquina. Atualize a lista.";
                continue;
            }
            var paciente = codigo.Atendimento?.Paciente;
            var convenioDaGuia = paciente is null ? null : paciente.ConvenioCodigo ?? paciente.Convenio.ToString();
            var emOutroEnvio = anteriores[id].FirstOrDefault(i => PrendeAGuia(i));
            // O ÚLTIMO envio desta guia ficou em dúvida (a mensagem saiu e não houve resposta em que
            // confiar)? Então o pedido novo só passa com a declaração de quem conferiu no portal.
            // A guarda mora AQUI, e não só na tela: a fila tem mais de uma porta (a aba do envio,
            // o formulário de baixa), e trava só de tela deixaria a outra passando.
            var emDuvida = FilaTiss.NaoSeSabeSeChegou(anteriores[id].FirstOrDefault()?.Transacao);
            var declarada = emDuvida && conferidasNoPortal is not null && conferidasNoPortal.Contains(id);

            var recusa =
                codigo.Baixado ? "Esta guia já tem baixa: não é enviada de novo."
                : !string.Equals(convenioDaGuia, convenio, StringComparison.OrdinalIgnoreCase)
                    ? $"Esta guia é de outro convênio ({convenioDaGuia ?? "não identificado"}): um envio leva guias de um convênio só."
                : codigo.GuiaAns is null ? "Confira a guia no padrão ANS antes de enviar: esta ainda não foi conferida."
                : emOutroEnvio is not null ? FraseDeOutroEnvio(emOutroEnvio)
                : emDuvida && !declarada ? FilaTiss.RecusaSemConferenciaNoPortal
                : null;
            if (recusa is not null)
            {
                recusas[id] = recusa;
                continue;
            }
            // O número muda a cada envio da mesma guia (C12, C12R2, C12R3…): a operadora nunca vê
            // o mesmo número duas vezes, e a resposta acha a guia de volta por ele.
            guias.Add(new GuiaDoLote(codigo, NumeroDoPrestador(id, anteriores[id].Count() + 1), declarada));
        }
        return (new ConferenciaDeEnvioTiss { PorGuia = recusas }, guias);
    }

    /// <summary>
    /// O item de um envio anterior ainda "prende" a guia? Sim enquanto o envio está na fila ou em
    /// andamento, e depois dele concluído se a guia foi recebida ou ainda não teve resposta. Envio
    /// que falhou ou foi cancelado, e guia glosada ou que falhou, soltam: aí reenviar é decisão
    /// de quem está logado, depois de conferir.
    /// </summary>
    private static bool PrendeAGuia(TransacaoTissGuia item) => item.Transacao?.Estado switch
    {
        EstadoTransacaoTiss.Pendente or EstadoTransacaoTiss.EmEnvio => true,
        EstadoTransacaoTiss.Concluida => item.Situacao is SituacaoGuiaTiss.Recebida or SituacaoGuiaTiss.Pendente,
        _ => false
    };

    private static string FraseDeOutroEnvio(TransacaoTissGuia item) => item.Transacao?.Estado switch
    {
        EstadoTransacaoTiss.Concluida when item.Situacao == SituacaoGuiaTiss.Recebida =>
            $"Esta guia já foi recebida pela operadora (protocolo {item.Transacao.Protocolo ?? "não informado"}). Falta dar baixa, não enviar de novo.",
        EstadoTransacaoTiss.Concluida =>
            $"Esta guia está num envio já protocolado ({item.Transacao.Protocolo ?? "sem número"}) e ainda sem resposta da operadora: consulte o protocolo antes de enviar de novo.",
        _ => "Esta guia já está num envio à operadora, na fila ou em andamento. Acompanhe o que já foi pedido."
    };

    /// <summary>
    /// O número da guia no prestador para o envio número <paramref name="vez"/> desta guia. Uma
    /// convenção só no sistema: é <see cref="NumeroGuiaPrestadorTiss.Gerar"/> ("C12", depois "C12R2").
    /// </summary>
    public static string NumeroDoPrestador(int codigoId, int vez) => NumeroGuiaPrestadorTiss.Gerar(codigoId, Math.Max(1, vez));

    /// <summary>
    /// Grava o que a operadora disse de cada guia. A guia é achada pelo número que a clínica mandou.
    /// Guia que a resposta não cita fica como estava, com o aviso na linha — nunca vira "recebida"
    /// por omissão quando a operadora detalhou as outras.
    /// </summary>
    private static void AplicarPorGuia(TransacaoTiss envio, IReadOnlyList<ResultadoDaGuiaTiss> resultados, DateTime agora,
        bool semDetalheContaComoRecebida)
    {
        if (resultados.Count == 0)
        {
            if (!semDetalheContaComoRecebida) return;
            foreach (var item in envio.Guias.Where(g => g.Situacao == SituacaoGuiaTiss.Pendente))
            {
                item.Situacao = SituacaoGuiaTiss.Recebida;
                item.Motivo = null;
                item.AtualizadaEm = agora;
            }
            return;
        }

        foreach (var item in envio.Guias)
        {
            var resultado = resultados.FirstOrDefault(r =>
                string.Equals(r.NumeroGuiaPrestador?.Trim(), item.NumeroGuiaPrestador, StringComparison.OrdinalIgnoreCase));
            if (resultado is null)
            {
                if (item.Situacao == SituacaoGuiaTiss.Pendente)
                    item.Motivo = "A operadora não citou esta guia na resposta. Consulte o protocolo antes de dar baixa.";
                continue;
            }
            item.Situacao = resultado.Situacao;
            item.Motivo = Cortar(string.IsNullOrWhiteSpace(resultado.Motivo) ? null : resultado.Motivo.Trim(), TransacaoTissLimites.MotivoDaGuia);
            if (!string.IsNullOrWhiteSpace(resultado.NumeroGuiaOperadora))
                item.NumeroGuiaOperadora = Cortar(resultado.NumeroGuiaOperadora.Trim(), 20);
            item.AtualizadaEm = agora;
        }
    }

    /// <summary>
    /// A operadora cancelou a guia: o item dela no envio deixa de ser "recebida" — sai da lista da
    /// baixa e fica livre para um envio novo, pedido por gente.
    /// </summary>
    private async Task MarcarCanceladaNoEnvioAsync(int codigoId, string? numeroGuiaPrestador, DateTime agora, CancellationToken ct)
    {
        var item = (await _repo.ItensDeEnvioTissDasGuiasAsync([codigoId], ct))
            .FirstOrDefault(i => string.Equals(i.NumeroGuiaPrestador, numeroGuiaPrestador?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (item is null || await _repo.ObterTransacaoTissAsync(item.TransacaoId, ct) is not { } envio) return;
        var rastreado = envio.Guias.FirstOrDefault(g => g.Id == item.Id);
        if (rastreado is null) return;
        rastreado.Situacao = SituacaoGuiaTiss.Cancelada;
        rastreado.Motivo = $"Cancelada na operadora em {agora:dd/MM/yyyy}.";
        rastreado.AtualizadaEm = agora;
        envio.Versao++;
    }

    /// <summary>O lote não foi (ou não se sabe se foi): cada guia ainda sem resposta fica marcada como falha, com o motivo.</summary>
    private void GuiasSemEnvio(TransacaoTiss t, string? motivo)
    {
        var agora = _agora();
        foreach (var item in t.Guias.Where(g => g.Situacao == SituacaoGuiaTiss.Pendente))
        {
            item.Situacao = SituacaoGuiaTiss.Falhou;
            item.Motivo = Cortar(motivo, TransacaoTissLimites.MotivoDaGuia);
            item.AtualizadaEm = agora;
        }
    }

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
        EnvioIniciadoEm = t.EnvioIniciadoEm, ProximaTentativaEm = t.ProximaTentativaEm, Versao = t.Versao,
        TransacaoDeEnvioId = t.TransacaoDeEnvioId,
        // As guias do lote vão junto: é por elas que a tela mostra o resultado por linha.
        Guias = t.Guias.OrderBy(g => g.Id).Select(g => new TransacaoTissGuia
        {
            Id = g.Id, TransacaoId = g.TransacaoId, CodigoId = g.CodigoId, NumeroGuiaPrestador = g.NumeroGuiaPrestador,
            NumeroGuiaOperadora = g.NumeroGuiaOperadora, Situacao = g.Situacao, Motivo = g.Motivo, AtualizadaEm = g.AtualizadaEm
        }).ToList()
    };
}
