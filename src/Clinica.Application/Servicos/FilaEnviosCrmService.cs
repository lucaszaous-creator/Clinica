using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Um pedido da fila como a ponte o entrega ao CRM. As datas são a hora da CLÍNICA (sem
/// fuso); quem converte para o contrato é a ponte. Texto, arquivo, e-mail e assunto só vêm
/// preenchidos na RESERVA bem-sucedida — a listagem e as recusas não carregam conteúdo.
/// </summary>
public sealed record PedidoDeEnvio(
    string Id, int PacienteId, string Nome, string Telefone, string Origem, string Operador,
    string Estado, string Detalhe, DateTime CriadoEm, DateTime ExpiraEm, string Canal,
    string? NomeArquivo, string? Texto = null, byte[]? Arquivo = null,
    string? Email = null, string? Assunto = null);

/// <summary>A autorização final de envio: <c>Concedida</c> libera UMA chamada ao provedor.</summary>
public sealed record AutorizacaoDeEnvio(bool Concedida, string Estado, string Detalhe);

/// <summary>O estado que a clínica passou a considerar depois do relato do CRM.</summary>
public sealed record RelatoDeEnvio(string Estado, string Detalhe);

/// <summary>
/// As REGRAS da fila de envios ao paciente pelo CRM (out/2026), num lugar só e sem banco —
/// o serviço abaixo só lê, aplica e grava. É o protocolo que o <c>ClinicalDispatchWorker</c>
/// do CRM espera (repositório semdor-crm, <c>docs/envios-clinico-crm.md</c>):
///
/// <list type="number">
/// <item>LISTAR — o que está na fila ou reservado por este CRM, sem texto nem PDF;</item>
/// <item>RESERVAR (protocolo 2) — devolve a linha INTEIRA, com o PDF; repetível pelo mesmo
/// consumidor (o CRM reserva de novo logo antes de enviar, para conferir que nada mudou);</item>
/// <item>AUTORIZAR — de USO ÚNICO: a primeira libera o provedor; qualquer outra é recusada
/// como "incerto", porque a primeira pode ter sido usada e a resposta se perdido;</item>
/// <item>RELATAR — aceite, entrega, leitura, falha, incerteza ou bloqueio, sem regredir
/// (entrega comprovada não volta a "aceito", aceite não volta a "incerto").</item>
/// </list>
/// </summary>
public static class FilaEnviosCrm
{
    public const string Fila = "queued";
    public const string Processando = "processing";
    public const string Enviando = "sending";
    public const string Aceito = "accepted";
    public const string Entregue = "delivered";
    public const string Lido = "seen";
    public const string Falhou = "failed";
    public const string Incerto = "uncertain";
    public const string Bloqueado = "blocked";

    public const string CanalWhatsApp = "whatsapp";
    public const string OrigemDocumentoClinico = "DocumentoClinico";

    /// <summary>O protocolo mínimo: o 2 é o que tem autorização final de uso único.</summary>
    public const int ProtocoloMinimo = 2;

    /// <summary>Quanto o pedido vale: documento conferido hoje não sai amanhã sem ninguém olhar.</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromHours(24);

    /// <summary>Reserva de outro CRM, sem autorização, que passou disto pode ser retomada.</summary>
    public static readonly TimeSpan ReservaVence = TimeSpan.FromMinutes(10);

    /// <summary>O CRM consulta em lotes; a linha mais antiga em <c>AtualizadoEm</c> vem primeiro.</summary>
    public const int Lote = 30;

    /// <summary>O que o CRM pode relatar.</summary>
    public static readonly IReadOnlySet<string> Relataveis =
        new HashSet<string>(StringComparer.Ordinal) { Aceito, Entregue, Lido, Falhou, Incerto, Bloqueado };

    /// <summary>
    /// A matriz de transições do RELATO. Nada regride: "entregue" não volta a "aceito",
    /// "aceito" não volta a "incerto", e o que terminou (lido, falhou, bloqueado) fica.
    /// Da fila e da reserva vale qualquer relato — o provedor é quem sabe o que aconteceu.
    /// </summary>
    public static bool PodeIr(string de, string para)
    {
        if (de == para || !Relataveis.Contains(para)) return false;
        return de switch
        {
            Fila or Processando => true,
            Enviando => true,
            Incerto => para is Aceito or Entregue or Lido or Falhou,
            Aceito => para is Entregue or Lido or Falhou,
            Entregue => para is Lido,
            _ => false // Lido, Falhou, Bloqueado: encerrados.
        };
    }

    /// <summary>Ainda na fila: é só aqui que a linha guarda o PDF.</summary>
    public static bool NaFila(string estado) => estado is Fila or Processando;

    /// <summary>A frase de cada estado, para a tela e para o CRM.</summary>
    public static string Descrever(string estado) => estado switch
    {
        Fila => "Na fila do CRM",
        Processando => "Em preparo no CRM",
        Enviando => "Autorizado; aguardando o WhatsApp",
        Aceito => "Aceito pelo WhatsApp; entrega ainda não confirmada",
        Entregue => "Entregue ao celular do paciente",
        Lido => "Lido pelo paciente",
        Falhou => "Recusado pelo WhatsApp",
        Incerto => "Resultado incerto; confira no CRM antes de pedir de novo",
        Bloqueado => "Não enviado",
        _ => "Situação desconhecida"
    };

    /// <summary>
    /// O telefone do envio: só dígitos, com 55 na frente quando vem só com DDD (10 ou 11
    /// dígitos). Vazio quando não serve — a MESMA regra da agenda na ponte, para o CRM
    /// achar a conversa que já existe com o paciente.
    /// </summary>
    public static string Telefone(string? telefone)
    {
        var digitos = Clinica.Domain.Telefone.SemPrefixoDeDiscagem(new string((telefone ?? string.Empty).Where(char.IsAsciiDigit).ToArray()));
        if (digitos.Length is 10 or 11) digitos = "55" + digitos;
        return digitos.Length is >= 10 and <= 15 ? digitos : string.Empty;
    }

    internal static string Cortar(string? texto, int maximo)
    {
        var t = (texto ?? string.Empty).Trim();
        return t.Length <= maximo ? t : t[..maximo];
    }

    internal static PedidoDeEnvio Pedido(EnvioPacienteCrm e, bool comConteudo, string? estado = null, string? detalhe = null)
        => new(e.Id, e.PacienteId, e.Nome, e.Telefone, e.Origem, e.Operador,
            estado ?? e.Estado, detalhe ?? e.Detalhe, e.CriadoEm, e.ExpiraEm, e.Canal, e.NomeArquivo,
            comConteudo ? e.Texto : null, comConteudo ? e.Arquivo : null,
            comConteudo ? e.Email : null, comConteudo ? e.Assunto : null);
}

/// <summary>
/// A fila de envios ao paciente vista pela PONTE do CRM (out/2026). A ponte só traduz HTTP;
/// o que pode e o que não pode mora em <see cref="FilaEnviosCrm"/>, e a gravação, aqui.
///
/// ⚠️ A unicidade da autorização é garantida pela coluna <c>Versao</c> (concorrência
/// otimista): duas autorizações simultâneas leem a mesma versão e só a primeira grava — a
/// segunda recebe a recusa do <c>SalvarAsync</c> e vira "não concedida". Sem transação
/// aberta à mão, o que mantém a estratégia de repetição do <c>AddClinica</c> funcionando
/// (a lição do <c>TransacaoDaPonte</c>).
/// </summary>
public sealed class FilaEnviosCrmService
{
    private readonly IClinicaRepositorio _repo;
    private readonly Func<DateTime> _agora;

    public FilaEnviosCrmService(IClinicaRepositorio repo) : this(repo, HoraDaClinica.Agora) { }

    public FilaEnviosCrmService(IClinicaRepositorio repo, Func<DateTime> agora)
    {
        _repo = repo;
        _agora = agora;
    }

    /// <summary>Os pedidos que este CRM pode pegar agora, sem texto nem PDF.</summary>
    public async Task<IReadOnlyList<PedidoDeEnvio>> ListarAsync(string consumidor, CancellationToken ct = default)
    {
        Consumidor(consumidor);
        var agora = _agora();
        var linhas = await _repo.EnviosPacienteCrmPendentesAsync(
            consumidor, agora - FilaEnviosCrm.ReservaVence, FilaEnviosCrm.Lote, ct);
        return linhas.Select(e => FilaEnviosCrm.Pedido(e, comConteudo: false)).ToList();
    }

    /// <summary>
    /// Reserva o pedido para este CRM e devolve a linha INTEIRA (com o PDF) quando o estado
    /// volta <c>processing</c>. Em qualquer outro estado não há conteúdo, e o estado diz por quê.
    /// </summary>
    public async Task<PedidoDeEnvio> ReservarAsync(string id, string consumidor, int protocolo, CancellationToken ct = default)
    {
        Consumidor(consumidor);
        var e = await _repo.ObterEnvioPacienteCrmAsync(id, ct)
            ?? throw new InvalidOperationException("Pedido de envio não encontrado.");

        // Cliente antigo não leva o conteúdo: sem o protocolo 2 não há autorização de uso
        // único, e ele poderia enviar duas vezes. Nada é gravado — a linha continua na fila.
        if (protocolo < FilaEnviosCrm.ProtocoloMinimo)
            return FilaEnviosCrm.Pedido(e, false, FilaEnviosCrm.Bloqueado,
                "Atualize o CRM: a ponte exige o protocolo 2 para entregar o conteúdo.");

        if (!FilaEnviosCrm.NaFila(e.Estado) || e.SubmetidoEm is not null)
            return FilaEnviosCrm.Pedido(e, false);

        var agora = _agora();
        if (e.Estado == FilaEnviosCrm.Processando && e.Consumidor is { } outro && outro != consumidor
            && e.ReservadoEm is { } desde && desde >= agora - FilaEnviosCrm.ReservaVence)
            // Outro CRM está com ele. "queued" sem conteúdo faz este CRM só acompanhar.
            return FilaEnviosCrm.Pedido(e, false, FilaEnviosCrm.Fila, "Reservado por outro processamento do CRM.");

        if (await Impedimento(e, agora, ct) is { } motivo)
        {
            await EncerrarAsync(e, FilaEnviosCrm.Bloqueado, motivo, agora, ct);
            return FilaEnviosCrm.Pedido(e, false);
        }

        e.Estado = FilaEnviosCrm.Processando;
        e.Detalhe = "Reservado pelo CRM";
        e.Consumidor = consumidor;
        e.ReservadoEm = agora;
        e.AtualizadoEm = agora;
        e.Versao++;
        await _repo.SalvarAsync(ct);
        return FilaEnviosCrm.Pedido(e, comConteudo: true);
    }

    /// <summary>
    /// A autorização FINAL, de uso único. Só a reserva viva deste CRM, dentro da validade e
    /// com a origem ainda valendo, é concedida — e a concessão tira o PDF da linha.
    /// </summary>
    public async Task<AutorizacaoDeEnvio> AutorizarAsync(string id, string consumidor, int protocolo, CancellationToken ct = default)
    {
        Consumidor(consumidor);
        if (protocolo < FilaEnviosCrm.ProtocoloMinimo)
            return new(false, FilaEnviosCrm.Bloqueado, "Atualize o CRM: a autorização exige o protocolo 2.");

        var e = await _repo.ObterEnvioPacienteCrmAsync(id, ct);
        if (e is null) return new(false, FilaEnviosCrm.Bloqueado, "Pedido de envio não encontrado na clínica.");

        // USO ÚNICO. A primeira autorização pode ter sido usada com a resposta perdida no
        // caminho: dizer "incerto" é o que impede o CRM de mandar o documento duas vezes.
        if (e.SubmetidoEm is not null)
            return new(false, FilaEnviosCrm.Incerto, "A autorização deste envio já foi usada. Confira no CRM antes de pedir de novo.");

        if (e.Estado != FilaEnviosCrm.Processando || e.Consumidor != consumidor)
            return new(false, FilaEnviosCrm.Bloqueado, "Este envio não está reservado para este CRM.");

        var agora = _agora();
        if (await Impedimento(e, agora, ct) is { } motivo)
        {
            await EncerrarAsync(e, FilaEnviosCrm.Bloqueado, motivo, agora, ct);
            return new(false, FilaEnviosCrm.Bloqueado, motivo);
        }

        e.Estado = FilaEnviosCrm.Enviando;
        e.Detalhe = "Autorizado; aguardando o provedor";
        e.SubmetidoEm = agora;
        e.AtualizadoEm = agora;
        e.Arquivo = null; // a cópia já está no CRM; aqui ela não serve mais a ninguém
        e.Versao++;
        try
        {
            await _repo.SalvarAsync(ct);
        }
        catch (InvalidOperationException)
        {
            // Outra autorização gravou primeiro (a Versao mudou), ou a gravação não se
            // confirmou. Em nenhum dos dois casos este CRM pode chamar o provedor.
            return new(false, FilaEnviosCrm.Incerto, "Autorização não confirmada. Confira no CRM antes de pedir de novo.");
        }
        return new(true, FilaEnviosCrm.Enviando, "Autorizado");
    }

    /// <summary>
    /// O CRM conta o que aconteceu. Relato que regrediria o estado é ignorado e a resposta
    /// devolve o estado que VALE — o CRM passa a respeitá-lo.
    /// </summary>
    public async Task<RelatoDeEnvio> RegistrarAsync(string id, string consumidor, string estado, string? detalhe, CancellationToken ct = default)
    {
        Consumidor(consumidor);
        if (!FilaEnviosCrm.Relataveis.Contains(estado ?? string.Empty))
            throw new InvalidOperationException("Situação de envio desconhecida.");
        var e = await _repo.ObterEnvioPacienteCrmAsync(id, ct)
            ?? throw new InvalidOperationException("Pedido de envio não encontrado.");
        if (e.Consumidor is { } dono && dono != consumidor)
            throw new InvalidOperationException("O resultado veio de um CRM que não reservou este envio.");

        if (FilaEnviosCrm.PodeIr(e.Estado, estado!))
        {
            var frase = FilaEnviosCrm.Cortar(detalhe, 500);
            await EncerrarAsync(e, estado!, frase.Length == 0 ? FilaEnviosCrm.Descrever(estado!) : frase, _agora(), ct);
        }
        return new(e.Estado, string.IsNullOrWhiteSpace(e.Detalhe) ? FilaEnviosCrm.Descrever(e.Estado) : e.Detalhe);
    }

    /// <summary>Muda o estado; ao sair da fila, a cópia do PDF sai da linha.</summary>
    private async Task EncerrarAsync(EnvioPacienteCrm e, string estado, string detalhe, DateTime agora, CancellationToken ct)
    {
        e.Estado = estado;
        e.Detalhe = FilaEnviosCrm.Cortar(detalhe, 500);
        e.AtualizadoEm = agora;
        if (!FilaEnviosCrm.NaFila(estado)) e.Arquivo = null;
        e.Versao++;
        await _repo.SalvarAsync(ct);
    }

    /// <summary>O que impede o envio AGORA: vencido, ou o documento foi cancelado depois do pedido.</summary>
    private async Task<string?> Impedimento(EnvioPacienteCrm e, DateTime agora, CancellationToken ct)
    {
        if (e.ExpiraEm <= agora)
            return "Pedido vencido (24 h). Confira o documento e peça o envio de novo na clínica.";
        if (e.DocumentoClinicoId is int documentoId)
        {
            var documento = await _repo.ObterDocumentoAsync(documentoId, ct);
            if (documento is null) return "O documento do pedido não foi encontrado.";
            if (documento.Cancelado) return $"O documento {documento.Numero} foi cancelado depois do pedido.";
            if (documento.PacienteId != e.PacienteId) return "O documento não é mais deste paciente.";
        }
        return null;
    }

    private static void Consumidor(string? consumidor)
    {
        if (string.IsNullOrWhiteSpace(consumidor) || consumidor.Length > 64)
            throw new InvalidOperationException("Identificador do CRM inválido.");
    }
}

/// <summary>
/// A hora de parede da CLÍNICA, igual em qualquer máquina: o desktop grava o pedido, a ponte
/// (outra VPS, outro fuso de sistema) confere a validade — as duas precisam do mesmo relógio.
/// </summary>
public static class HoraDaClinica
{
    public static DateTime Agora()
        => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Sao_Paulo").DateTime;
}
