using System.Text.Json;
using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// O TERMO ASSINADO PELO CELULAR DO PACIENTE — o link pelo WhatsApp (parcela 81).
/// A decisão inteira está em <c>docs/termo-pelo-whatsapp.md</c>; em resumo:
///
/// O desktop publica um PEDIDO minimizado no balde (o texto do termo e as declarações,
/// atrás de um token de 2^127 — a mesma barreira das receitas), abre o WhatsApp com o
/// link, e fica LENDO o balde à espera da RESPOSTA que o Worker grava (traço + respostas
/// + evidência). A resposta NÃO sela nada sozinha: ela volta à janela da técnica, que
/// confere a identidade e conclui pelo MESMO <c>ColherAsync</c> da coleta no balcão —
/// o papel deste fluxo é tirar o custo do pad, não a pessoa do circuito.
///
/// ⚠️ O Worker nunca toca o banco: ele só enxerga o prefixo <c>t/</c> do balde. Vazamento
/// da borda expõe no máximo os pedidos em aberto — nunca uma credencial do Postgres.
/// </summary>
public sealed class ColetaRemotaTermoService
{
    private readonly IClinicaRepositorio _repo;
    private readonly IArmazenamentoPublico _armazenamento;
    private readonly ParametrosService _parametros;
    private readonly Func<DateTime> _agora;

    // O acento sai em UTF-8 legível, não em \u00E1: o pedido é lido pelo NOSSO Worker e
    // conferido por gente em suporte; quem escapa para HTML é a página, na borda.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public ColetaRemotaTermoService(
        IClinicaRepositorio repo,
        IArmazenamentoPublico armazenamento,
        ParametrosService parametros,
        Func<DateTime>? agora = null)
    {
        _repo = repo;
        _armazenamento = armazenamento;
        _parametros = parametros;
        // Relógio INJETADO (a regra da checagem de enfermagem): a expiração é regra de
        // segurança, e regra de segurança que não dá para testar apodrece sem ninguém notar.
        _agora = agora ?? (() => DateTime.Now);
    }

    /// <summary>
    /// Publica o pedido e devolve o link pronto para o WhatsApp.
    ///
    /// IDEMPOTENTE por documento: o segundo clique reaproveita o envio em aberto (mesmo
    /// token — o paciente pode já estar com o link na mão), e só um envio VENCIDO gera
    /// token novo, com o velho cancelado e registrado.
    /// </summary>
    public async Task<EnvioRemotoTermo> EnviarAsync(
        int documentoId, string operador, CancellationToken ct = default)
    {
        var baseUrl = await _parametros.ObterUrlPublicacaoAsync(ct)
            ?? throw new InvalidOperationException(
                "O endereço público da clínica não está configurado. É o mesmo da "
                + "publicação de receitas — Gerente → Configurações → Publicação. Sem "
                + "ele não há para onde apontar o link do WhatsApp.");

        var documento = await _repo.ObterDocumentoAsync(documentoId, ct)
            ?? throw new InvalidOperationException("Termo não encontrado.");

        if (!documento.AguardaAssinaturaDoPaciente)
            throw new InvalidOperationException(
                "Este documento não está aguardando a assinatura do paciente — ou já foi "
                + "assinado/recusado, ou não é um termo que o paciente assina.");

        var paciente = await _repo.ObterPacienteAsync(documento.PacienteId, ct)
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        if (string.IsNullOrWhiteSpace(paciente.Telefone))
            throw new InvalidOperationException(
                $"A ficha de {PrimeiroNome(paciente.Nome)} não tem celular cadastrado — "
                + "cadastre na ficha do paciente antes de enviar o link.");

        var agora = _agora();
        var aberta = await _repo.ColetaRemotaAbertaDoDocumentoAsync(documentoId, ct);

        if (aberta is not null && !aberta.Vencida(agora))
            return Montar(baseUrl, aberta.Token, paciente.Nome, aberta.TelefoneDestino);

        if (aberta is not null)
        {
            // Vencida: cancela ANTES de criar outra — duas coletas em aberto do mesmo
            // documento fariam a resposta de uma cair na espera da outra.
            aberta.CanceladaEm = agora;
            aberta.CanceladaPor = "expirada — reenviada";
            await RemoverObjetosAsync(aberta.Token, ct);
        }

        var token = PublicacaoDocumento.GerarToken();
        var expira = agora.AddHours(ColetaRemotaTermo.HorasNoAr);

        // ⚠️ MINIMIZADO por decisão (docs/termo-pelo-whatsapp.md §3): o pedido carrega SÓ
        // o que a leitura exige. Sobrenome, CPF, carteirinha e nascimento NÃO entram —
        // há teste fixando isso, porque campo a mais aqui é dado de saúde a mais no ar.
        var pedido = JsonSerializer.SerializeToUtf8Bytes(new
        {
            versao = 1,
            titulo = documento.TituloImpresso,
            numero = documento.Numero,
            paciente = PrimeiroNome(paciente.Nome),
            corpo = documento.Corpo ?? string.Empty,
            declaracoes = documento.Itens
                .OrderBy(i => i.Ordem)
                .Select(i => new { ordem = i.Ordem, texto = i.Descricao, detalhe = i.Detalhe })
                .ToList(),
            expiraEmUnixMs = new DateTimeOffset(expira).ToUnixTimeMilliseconds()
        }, Json);

        await _armazenamento.PublicarAsync(
            ColetaRemotaTermo.CaminhoPedido(token), pedido,
            "application/json; charset=utf-8", ct: ct);

        await _repo.AdicionarColetaRemotaAsync(new ColetaRemotaTermo
        {
            DocumentoClinicoId = documentoId,
            Token = token,
            TelefoneDestino = paciente.Telefone.Trim(),
            EnviadaPor = operador,
            CriadaEm = agora,
            ExpiraEm = expira
        }, ct);

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = operador,
            Acao = "TermoEnvioRemoto",
            PacienteId = documento.PacienteId,
            Detalhe = $"{documento.Numero} enviado para {paciente.Telefone.Trim()} "
                    + $"(link WhatsApp) · vale até {expira:dd/MM/yyyy HH:mm}"
        }, ct);

        await _repo.SalvarAsync(ct);

        return Montar(baseUrl, token, paciente.Nome, paciente.Telefone.Trim());
    }

    /// <summary>
    /// A resposta do celular, quando já chegou — ou <c>null</c> enquanto o paciente lê.
    /// É o que a janela chama no polling. Traço fora do tamanho de traço é RECUSADO com a
    /// saída escrita (cancelar e reenviar), porque write-once não tem segunda gravação.
    /// </summary>
    public async Task<RespostaRemotaTermo?> ColherRespostaAsync(
        int documentoId, CancellationToken ct = default)
    {
        var coleta = await _repo.ColetaRemotaAbertaDoDocumentoAsync(documentoId, ct);
        if (coleta is null) return null;

        var bytes = await _armazenamento.LerAsync(
            ColetaRemotaTermo.CaminhoResposta(coleta.Token), ct);
        if (bytes is null) return null;

        using var json = JsonDocument.Parse(bytes);
        var raiz = json.RootElement;

        var respostas = new Dictionary<int, string?>();
        if (raiz.TryGetProperty("respostas", out var r))
            foreach (var par in r.EnumerateObject())
                if (int.TryParse(par.Name, out var ordem))
                    respostas[ordem] = par.Value.GetString();

        var traco = DecodificarTraco(raiz);

        if (traco.Png.Length is < AssinaturaDoPacienteService.TamanhoMinimoTraco
            or > AssinaturaDoPacienteService.TamanhoMaximoTraco)
            throw new InvalidOperationException(
                "A assinatura que chegou do celular não é um traço válido. Cancele este "
                + "envio e mande um link novo — o link antigo não aceita segunda assinatura.");

        if (coleta.RespondidaEm is null)
        {
            coleta.RespondidaEm = _agora();
            coleta.EvidenciaResposta = Evidencia(raiz);
            await _repo.SalvarAsync(ct);
        }

        return new RespostaRemotaTermo(
            respostas, traco.Png, traco.Largura, traco.Altura,
            coleta.TelefoneDestino, coleta.EvidenciaResposta);
    }

    /// <summary>
    /// Fecha o circuito DEPOIS de o documento ter sido selado pelo <c>ColherAsync</c>.
    /// Devolve <c>false</c> quando os objetos não puderam sair do ar — a coleta fica
    /// concluída mesmo assim (o selo já existe), e a janela avisa em vez de calar:
    /// dado de saúde no ar é a falha grave deste fluxo.
    /// </summary>
    public async Task<bool> ConcluirAsync(
        int documentoId, string operador, CancellationToken ct = default)
    {
        var coleta = await _repo.ColetaRemotaAbertaDoDocumentoAsync(documentoId, ct);
        if (coleta is null) return true;

        coleta.ConcluidaEm = _agora();

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = operador,
            Acao = "TermoColetaRemotaConcluida",
            PacienteId = coleta.Documento?.PacienteId ?? 0,
            Detalhe = $"Assinatura colhida pelo celular do paciente ({coleta.TelefoneDestino}) "
                    + $"· {coleta.EvidenciaResposta}"
        }, ct);

        await _repo.SalvarAsync(ct);
        return await RemoverObjetosAsync(coleta.Token, ct);
    }

    /// <summary>Cancela o envio e tira o pedido do ar. O registro fica, marcado.</summary>
    public async Task<bool> CancelarAsync(
        int documentoId, string operador, CancellationToken ct = default)
    {
        var coleta = await _repo.ColetaRemotaAbertaDoDocumentoAsync(documentoId, ct);
        if (coleta is null) return true;

        coleta.CanceladaEm = _agora();
        coleta.CanceladaPor = operador;
        await _repo.SalvarAsync(ct);
        return await RemoverObjetosAsync(coleta.Token, ct);
    }

    /// <summary>
    /// A varredura de limpeza — roda junto da despublicação de receitas vencidas. Vencer
    /// não depende dela (o Worker recusa pedido vencido pela data DENTRO do JSON); ela
    /// existe para o objeto não ficar no balde depois de morto.
    /// </summary>
    public async Task<int> LimparVencidasAsync(CancellationToken ct = default)
    {
        var vencidas = await _repo.ColetasRemotasVencidasAsync(_agora(), ct);
        foreach (var coleta in vencidas)
        {
            coleta.CanceladaEm = _agora();
            coleta.CanceladaPor = "expiração automática";
            await RemoverObjetosAsync(coleta.Token, ct);
        }

        if (vencidas.Count > 0) await _repo.SalvarAsync(ct);
        return vencidas.Count;
    }

    private static EnvioRemotoTermo Montar(
        string baseUrl, string token, string nomePaciente, string telefone)
    {
        var url = ColetaRemotaTermo.Url(baseUrl, token);
        // A mensagem é MÍNIMA de propósito: notificação de WhatsApp aparece na tela
        // bloqueada, e o nome do procedimento não é assunto de tela bloqueada.
        var mensagem = $"Olá, {PrimeiroNome(nomePaciente)}! Seu termo para leitura e "
                     + $"assinatura: {url}\nO link vale por 24 horas e abre no navegador.";
        return new EnvioRemotoTermo(token, url, mensagem, telefone);
    }

    private async Task<bool> RemoverObjetosAsync(string token, CancellationToken ct)
    {
        try
        {
            await _armazenamento.RemoverAsync(ColetaRemotaTermo.CaminhoPedido(token), ct);
            await _armazenamento.RemoverAsync(ColetaRemotaTermo.CaminhoResposta(token), ct);
            return true;
        }
        catch (Exception ex)
        {
            // Falha aqui não desfaz o que já aconteceu (selo, cancelamento) — mas nunca
            // passa calada: dado de saúde que ficou no ar é o que a chamadora avisa.
            Diagnostico.Registrar("Coleta remota — objetos não saíram do ar", ex);
            return false;
        }
    }

    private static (byte[] Png, int Largura, int Altura) DecodificarTraco(JsonElement raiz)
    {
        var dataUrl = raiz.TryGetProperty("traco", out var t) ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(dataUrl))
            throw new InvalidOperationException("A resposta chegou sem assinatura.");

        const string prefixo = "base64,";
        var i = dataUrl.IndexOf(prefixo, StringComparison.Ordinal);
        byte[] png;
        try
        {
            png = Convert.FromBase64String(i >= 0 ? dataUrl[(i + prefixo.Length)..] : dataUrl);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("A assinatura chegou num formato ilegível.");
        }

        var largura = raiz.TryGetProperty("tracoLargura", out var l) ? l.GetInt32() : 0;
        var altura = raiz.TryGetProperty("tracoAltura", out var a) ? a.GetInt32() : 0;
        return (png, largura, altura);
    }

    private static string Evidencia(JsonElement raiz)
    {
        var partes = new List<string>();
        if (raiz.TryGetProperty("respondidoEmUnixMs", out var q) && q.TryGetInt64(out var ms))
            partes.Add("respondido às " + DateTimeOffset.FromUnixTimeMilliseconds(ms)
                .ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
        if (raiz.TryGetProperty("ip", out var ip) && ip.GetString() is { Length: > 0 } vip)
            partes.Add($"IP {vip}");
        if (raiz.TryGetProperty("aparelho", out var ua) && ua.GetString() is { Length: > 0 } vua)
            partes.Add(vua.Length > 160 ? vua[..160] : vua);
        return string.Join(" · ", partes);
    }

    private static string PrimeiroNome(string nome)
        => nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nome;
}

/// <summary>O que a janela precisa para abrir o WhatsApp.</summary>
public sealed record EnvioRemotoTermo(string Token, string Url, string Mensagem, string Telefone);

/// <summary>A assinatura que chegou do celular, pronta para a técnica conferir e concluir.</summary>
public sealed record RespostaRemotaTermo(
    IReadOnlyDictionary<int, string?> Respostas,
    byte[] TracoPng,
    int Largura,
    int Altura,
    string TelefoneDestino,
    string? Evidencia);
