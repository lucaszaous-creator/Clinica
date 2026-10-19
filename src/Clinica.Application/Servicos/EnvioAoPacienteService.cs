using Clinica.Application.Abstracoes;
using Clinica.Application.Email;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>O que o envio fez, já na frase da tela. <c>Ok</c> falso = nada saiu, e a frase diz por quê.</summary>
public sealed record ResultadoEnvioAoPaciente(bool Ok, string Frase);

/// <summary>De onde vem o PDF que vai ao paciente.</summary>
public enum OrigemDoArquivo
{
    /// <summary>Receita, atestado, pedido de exame, declaração e os demais documentos clínicos.</summary>
    DocumentoClinico
}

/// <summary>Qual papel vai: a origem e o id dele naquela origem.</summary>
public sealed record ArquivoDoPaciente(OrigemDoArquivo Origem, int Id)
{
    public static ArquivoDoPaciente Documento(int documentoId) => new(OrigemDoArquivo.DocumentoClinico, documentoId);
}

/// <summary>Os caminhos do "Enviar ao paciente". A ORDEM é a da tela: e-mail primeiro.</summary>
public enum CanalDeEnvio
{
    /// <summary>E-mail da clínica (SMTP próprio). O PADRÃO: não custa nada por envio.</summary>
    Email,

    /// <summary>WhatsApp oficial, pela fila que o CRM executa. Cada envio é cobrado pela Meta.</summary>
    WhatsAppPeloCrm,

    /// <summary>O WhatsApp deste computador (wa.me + Ctrl+V) — para quando o CRM está desligado.</summary>
    WhatsAppDesteComputador
}

/// <summary>Uma opção da janela: disponível ou não (com o motivo), e a nota que a acompanha.</summary>
public sealed record OpcaoDeEnvio(CanalDeEnvio Canal, string Rotulo, bool Disponivel, string? Motivo, string? Nota);

/// <summary>
/// Para onde o papel iria, antes de a pessoa confirmar. A tela PERGUNTA com isto — mostrar o
/// número e o e-mail antes do clique é a conferência que evita mandar o papel para a pessoa
/// errada.
/// </summary>
/// <param name="Email">
/// O e-mail da FICHA, quando houver e for válido — é só a sugestão do campo. A clínica não
/// colhe e-mail no cadastro: quem atende digita o que o paciente pedir.
/// </param>
/// <param name="MotivoSemEmail">
/// Nulo quando o canal e-mail serve. O canal NUNCA some por falta de e-mail na ficha; ele só
/// fica indisponível sem o servidor de saída da clínica configurado, e a frase diz onde.
/// </param>
public sealed record DestinosDoEnvio(
    string Paciente, string Documento, bool Assinado,
    string? Celular, string? MotivoSemWhatsApp,
    string? Email, string? MotivoSemEmail,
    string? JaNaFila)
{
    /// <summary>A frase que acompanha o WhatsApp oficial (pedido da direção, out/2026).</summary>
    public const string CustoDoWhatsApp = "Cada envio pelo WhatsApp é cobrado pela Meta — prefira o e-mail.";

    /// <summary>
    /// As opções da janela, na ordem da tela: e-mail PRIMEIRO, WhatsApp oficial depois, o
    /// WhatsApp deste computador por último. As TRÊS sempre aparecem — a indisponível vem
    /// apagada com o motivo, nunca escondida.
    /// </summary>
    public IReadOnlyList<OpcaoDeEnvio> Opcoes =>
    [
        new(CanalDeEnvio.Email, "E-mail", MotivoSemEmail is null, MotivoSemEmail,
            "O PDF vai anexado, saindo do e-mail da clínica."),
        new(CanalDeEnvio.WhatsAppPeloCrm, "WhatsApp (pelo CRM)",
            Celular is not null && JaNaFila is null, MotivoSemWhatsApp ?? JaNaFila,
            Celular is null ? CustoDoWhatsApp : $"Para {Celular}. {CustoDoWhatsApp}"),
        new(CanalDeEnvio.WhatsAppDesteComputador, "Abrir no WhatsApp deste computador",
            Celular is not null, MotivoSemWhatsApp,
            "Para quando o CRM está desligado: abre a conversa e o arquivo é colado com Ctrl+V.")
    ];

    /// <summary>
    /// O canal que a janela já traz marcado: o e-mail sempre que ele serve (é o padrão da
    /// direção); senão o primeiro disponível; nulo quando nenhum serve.
    /// </summary>
    public CanalDeEnvio? Padrao => Opcoes.FirstOrDefault(o => o.Disponivel)?.Canal;
}

/// <summary>
/// Entregar ao paciente o PDF de um documento clínico emitido — por e-mail (direto do servidor da clínica, o PADRÃO) ou por
/// WhatsApp (pela fila que o CRM executa, com a API oficial da Meta) — out/2026.
///
/// O que vai é a 2ª VIA: os bytes que a reimpressão daria. Documento clínico:
/// <see cref="DocumentosClinicosPdfService.GerarAsync"/> — os GUARDADOS quando assinado (nunca
/// se regenera um PDF assinado), a mesma montagem do papel emitido quando não.
///
/// A folha de infusão NÃO sai (decisão da direção, out/2026): é folha interna de execução
/// da enfermagem, não papel do paciente.
///
/// Toda saída grava a trilha (<c>Auditoria</c>) com QUEM, por qual CANAL e PARA ONDE, no
/// mesmo <c>SaveChanges</c> do ato.
/// </summary>
public sealed class EnvioAoPacienteService
{
    public const string AcaoNaFilaDoCrm = "DocumentoNaFilaWhatsAppCrm";
    public const string AcaoEmailEnviado = "DocumentoEnviadoPorEmail";
    public const string AcaoEmailFalhou = "DocumentoEmailRecusado";
    public const string AcaoWhatsAppLocal = "DocumentoAbertoNoWhatsAppLocal";

    /// <summary>A frase do canal e-mail indisponível — a única razão para ele ficar indisponível.</summary>
    public const string SemServidorDeEmail = "O e-mail da clínica não está configurado. Configure o servidor de saída em "
        + "Gerente → Configurações → Lembretes por e-mail.";

    private readonly IClinicaRepositorio _repo;
    private readonly DocumentosClinicosPdfService _pdfs;
    private readonly ParametrosService _parametros;
    private readonly IEnviadorDeEmail _email;
    private readonly Func<DateTime> _agora;

    public EnvioAoPacienteService(
        IClinicaRepositorio repo, DocumentosClinicosPdfService pdfs, ParametrosService parametros,
        IEnviadorDeEmail email)
        : this(repo, pdfs, parametros, email, HoraDaClinica.Agora) { }

    public EnvioAoPacienteService(
        IClinicaRepositorio repo, DocumentosClinicosPdfService pdfs, ParametrosService parametros,
        IEnviadorDeEmail email, Func<DateTime> agora)
    {
        _repo = repo;
        _pdfs = pdfs;
        _parametros = parametros;
        _email = email;
        _agora = agora;
    }

    /// <summary>
    /// Os papéis que saem como ARQUIVO mesmo sem assinatura digital: os quatro que a clínica
    /// emite sem certificado (continuidade sem A1, out/2026). Sem eles a entrega por arquivo
    /// não existiria — nenhum deles chega a ser assinado hoje. A frase que acompanha o
    /// arquivo não diz "assinado", e a tela avisa o caso do atestado (art. 13).
    /// </summary>
    public static bool EnviavelSemAssinatura(TipoDocumentoClinico tipo)
        => tipo is TipoDocumentoClinico.Receita or TipoDocumentoClinico.Atestado
            or TipoDocumentoClinico.PedidoExame or TipoDocumentoClinico.Comparecimento;

    /// <summary>Pode sair como arquivo: válido, e assinado ou de um dos quatro tipos acima.</summary>
    public static bool PodeEnviar(TipoDocumentoClinico tipo, bool assinado, bool cancelado)
        => !cancelado && (assinado || EnviavelSemAssinatura(tipo));

    /// <summary>A frase do bloqueio, quando <see cref="PodeEnviar(TipoDocumentoClinico, bool, bool)"/> é falso.</summary>
    public static string PorQueNaoEnvia(string numero, bool cancelado)
        => cancelado
            ? $"O documento {numero} está cancelado e não se envia."
            : $"O documento {numero} ainda não foi assinado digitalmente. Sem assinatura, o que vale "
              + "é a via impressa e assinada à caneta — assine antes de enviar o arquivo.";

    // ==================== O papel, de qualquer origem ====================

    /// <summary>O que o envio precisa saber do papel, já conferido que ele pode sair.</summary>
    private sealed record Papel(
        Paciente Paciente, string Rotulo, string Numero, bool Assinado,
        int DocumentoClinicoId, string Origem,
        Func<CancellationToken, Task<(byte[] Pdf, string Nome)>> Gerar)
    {
        public string Descricao => $"{Rotulo} {Numero}";
    }

    private async Task<Papel> CarregarAsync(ArquivoDoPaciente arquivo, CancellationToken ct)
    {
        var d = await _repo.ObterDocumentoAsync(arquivo.Id, ct)
            ?? throw new InvalidOperationException("Documento não encontrado.");
        if (!PodeEnviar(d.Tipo, d.AssinadoEletronicamente, d.Cancelado))
            throw new InvalidOperationException(PorQueNaoEnvia(d.Numero, d.Cancelado));
        var paciente = d.Paciente ?? await _repo.ObterPacienteAsync(d.PacienteId, ct)
            ?? throw new InvalidOperationException("Paciente do documento não encontrado.");
        return new Papel(paciente, TipoDocumentoInfo.Rotular(d.Tipo), d.Numero, d.AssinadoEletronicamente,
            d.Id, FilaEnviosCrm.OrigemDocumentoClinico,
            async c => (await _pdfs.GerarAsync(d.Id, await _parametros.ObterPrestadorAsync(c), c), NomeDoArquivo(d)));
    }

    // ==================== Antes de perguntar ====================

    public Task<DestinosDoEnvio> DestinosAsync(int documentoId, CancellationToken ct = default)
        => DestinosAsync(ArquivoDoPaciente.Documento(documentoId), ct);

    /// <summary>
    /// Os destinos possíveis deste papel, para a tela perguntar antes de enviar. Lança
    /// quando o papel não existe ou não pode sair (a tela já não ofereceria o botão).
    /// </summary>
    public async Task<DestinosDoEnvio> DestinosAsync(ArquivoDoPaciente arquivo, CancellationToken ct = default)
    {
        var papel = await CarregarAsync(arquivo, ct);
        var paciente = papel.Paciente;
        var celular = FilaEnviosCrm.Telefone(paciente.Telefone);
        var smtp = await _parametros.ObterOpcoesEmailAsync(ct);
        var aberto = await EmAbertoAsync(papel, _agora(), ct);

        return new DestinosDoEnvio(
            paciente.Nome, papel.Descricao, papel.Assinado,
            celular.Length == 0 ? null : paciente.TelefoneFormatado,
            celular.Length == 0 ? $"A ficha de {paciente.Nome} não tem celular válido (com DDD)." : null,
            EnderecoDeEmail.SeValido(paciente.Email),
            smtp is null ? SemServidorDeEmail : null,
            aberto is null ? null : $"Já está na fila do CRM desde {aberto.CriadoEm:dd/MM HH:mm} ({FilaEnviosCrm.Descrever(aberto.Estado).ToLowerInvariant()}).");
    }

    private Task<EnvioPacienteCrm?> EmAbertoAsync(Papel papel, DateTime agora, CancellationToken ct)
        => _repo.EnvioPacienteCrmEmAbertoAsync(papel.DocumentoClinicoId, FilaEnviosCrm.CanalWhatsApp, agora, ct);

    // ==================== WhatsApp oficial (fila do CRM) ====================

    public Task<ResultadoEnvioAoPaciente> EnfileirarWhatsAppAsync(int documentoId, string operador, CancellationToken ct = default)
        => EnfileirarWhatsAppAsync(ArquivoDoPaciente.Documento(documentoId), operador, ct);

    /// <summary>
    /// Põe o papel na fila que o CRM envia pelo WhatsApp oficial. Não envia nada daqui:
    /// o CRM consulta a fila a cada 15 s e o pedido vale 24 h. Repetir o clique enquanto o
    /// pedido está na fila não cria outro.
    /// </summary>
    public async Task<ResultadoEnvioAoPaciente> EnfileirarWhatsAppAsync(
        ArquivoDoPaciente arquivo, string operador, CancellationToken ct = default)
    {
        var papel = await CarregarAsync(arquivo, ct);
        var paciente = papel.Paciente;
        var celular = FilaEnviosCrm.Telefone(paciente.Telefone);
        if (celular.Length == 0)
            return new(false, $"A ficha de {paciente.Nome} não tem celular válido (com DDD). Corrija o telefone na ficha e tente de novo.");

        var agora = _agora();
        if (await EmAbertoAsync(papel, agora, ct) is { } aberto)
            return new(true, $"{papel.Descricao} já está na fila do CRM desde {aberto.CriadoEm:HH:mm} — "
                + "não foi pedido de novo. Acompanhe em CRM → Envios do Clínico.");

        var (pdf, nome) = await papel.Gerar(ct);
        var nomeClinica = await NomeDaClinicaAsync(ct);

        var envio = new EnvioPacienteCrm
        {
            Id = Guid.NewGuid().ToString("N"),
            PacienteId = paciente.Id,
            DocumentoClinicoId = papel.DocumentoClinicoId,
            Nome = FilaEnviosCrm.Cortar(paciente.Nome, 200),
            Telefone = celular,
            Canal = FilaEnviosCrm.CanalWhatsApp,
            Origem = papel.Origem,
            Operador = FilaEnviosCrm.Cortar(operador, 120),
            Estado = FilaEnviosCrm.Fila,
            Detalhe = FilaEnviosCrm.Descrever(FilaEnviosCrm.Fila),
            CriadoEm = agora,
            ExpiraEm = agora + FilaEnviosCrm.Validade,
            AtualizadoEm = agora,
            Texto = FilaEnviosCrm.Cortar(TextoDoEnvio.Legenda(paciente.Nome, papel.Rotulo, papel.Assinado, nomeClinica), 2000),
            NomeArquivo = FilaEnviosCrm.Cortar(nome, 200),
            Arquivo = pdf
        };

        await _repo.AdicionarEnvioPacienteCrmAsync(envio, ct);
        await Trilha(AcaoNaFilaDoCrm, envio.Operador, paciente.Id, "WhatsApp (pelo CRM)",
            paciente.TelefoneFormatado, $"{papel.Descricao} · pedido {envio.Id}", agora, ct);
        await _repo.SalvarAsync(ct);

        return new(true, $"{papel.Descricao} na fila do CRM para o WhatsApp {paciente.TelefoneFormatado}. "
            + "O CRM envia em instantes quando está ligado; o pedido vale por 24 horas. "
            + "Acompanhe em CRM → Envios do Clínico.");
    }

    // ==================== E-mail da clínica (o padrão) ====================

    public Task<ResultadoEnvioAoPaciente> EnviarPorEmailAsync(
        int documentoId, string operador, string destinatario, bool guardarNaFicha = false, CancellationToken ct = default)
        => EnviarPorEmailAsync(ArquivoDoPaciente.Documento(documentoId), operador, destinatario, guardarNaFicha, ct);

    /// <summary>
    /// Envia o PDF anexado ao e-mail que o PACIENTE pediu, pelo servidor de saída da CLÍNICA
    /// (Configurações → Lembretes por e-mail). Sempre um clique de quem atende — não há envio
    /// automático de documento por e-mail. O endereço vem da tela (sugerido pela ficha, editável);
    /// com <paramref name="guardarNaFicha"/>, ele vai para a ficha DEPOIS que o servidor aceitar
    /// o envio, pelo <see cref="PacienteService.DefinirEmailAsync"/>.
    ///
    /// O corpo é curto e não diz nada clínico — o conteúdo é o anexo. "Enviado" aqui quer dizer
    /// ACEITO pelo servidor de saída: e-mail não tem recibo de entrega.
    /// </summary>
    public async Task<ResultadoEnvioAoPaciente> EnviarPorEmailAsync(
        ArquivoDoPaciente arquivo, string operador, string destinatario, bool guardarNaFicha = false,
        CancellationToken ct = default)
    {
        var papel = await CarregarAsync(arquivo, ct);
        var paciente = papel.Paciente;
        var destino = EnderecoDeEmail.SeValido(destinatario);
        if (destino is null)
            return new(false, "Informe um e-mail válido (ex.: nome@provedor.com.br).");

        var opcoes = await _parametros.ObterOpcoesEmailAsync(ct);
        if (opcoes is null)
            return new(false, SemServidorDeEmail);

        var (pdf, nome) = await papel.Gerar(ct);
        var nomeClinica = await NomeDaClinicaAsync(ct);
        var quem = FilaEnviosCrm.Cortar(operador, 120);

        try
        {
            await _email.EnviarComAnexoAsync(opcoes, destino,
                TextoDoEnvio.AssuntoEmail(nomeClinica),
                TextoDoEnvio.CorpoEmail(paciente.Nome, papel.Assinado, nomeClinica),
                new AnexoDeEmail(nome, pdf, "application/pdf"), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Diagnostico.Registrar($"{papel.Descricao} — e-mail ao paciente recusado", ex);
            await Trilha(AcaoEmailFalhou, quem, paciente.Id, "e-mail", destino,
                $"{papel.Descricao}: {FilaEnviosCrm.Cortar(ex.Message, 200)}", _agora(), ct);
            await _repo.SalvarAsync(ct);
            return new(false, $"O servidor de e-mail não aceitou o envio para {destino}: {ex.Message} "
                + "Confira o endereço e a configuração em Gerente → Configurações → Lembretes por e-mail.");
        }

        await Trilha(AcaoEmailEnviado, quem, paciente.Id, "e-mail", destino, papel.Descricao, _agora(), ct);
        await _repo.SalvarAsync(ct);

        var frase = $"{papel.Descricao} enviado por e-mail para {destino}. "
            + "O servidor aceitou; e-mail não tem confirmação de leitura.";
        if (!guardarNaFicha) return new(true, frase);

        // O envio JÁ saiu: falhar ao guardar na ficha não pode virar "não enviado".
        try
        {
            await new PacienteService(_repo).DefinirEmailAsync(paciente.Id, destino, quem, ct);
            return new(true, frase + " O e-mail ficou guardado na ficha.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Diagnostico.Registrar($"Paciente {paciente.Id} — e-mail do envio não pôde ir para a ficha", ex);
            return new(true, frase + $" Mas o e-mail NÃO ficou guardado na ficha: {ex.Message}");
        }
    }

    // ==================== WhatsApp deste computador ====================

    /// <summary>O PDF da 2ª via e a legenda, para o caminho local (wa.me + Ctrl+V).</summary>
    public async Task<(byte[] Pdf, string NomeArquivo, string? Telefone, string Paciente, string Legenda)> ArquivoParaEntregaLocalAsync(
        ArquivoDoPaciente arquivo, CancellationToken ct = default)
    {
        var papel = await CarregarAsync(arquivo, ct);
        var (pdf, nome) = await papel.Gerar(ct);
        return (pdf, nome, papel.Paciente.Telefone, papel.Paciente.Nome,
            TextoDoEnvio.Legenda(papel.Paciente.Nome, papel.Rotulo, papel.Assinado, await NomeDaClinicaAsync(ct)));
    }

    /// <summary>
    /// A trilha do caminho local: o sistema abriu a conversa com o arquivo copiado. Não prova
    /// que a pessoa colou e enviou — a frase da trilha diz isso.
    /// </summary>
    public async Task RegistrarEntregaLocalAsync(ArquivoDoPaciente arquivo, string operador, CancellationToken ct = default)
    {
        var papel = await CarregarAsync(arquivo, ct);
        await Trilha(AcaoWhatsAppLocal, FilaEnviosCrm.Cortar(operador, 120), papel.Paciente.Id,
            "WhatsApp deste computador", papel.Paciente.TelefoneFormatado,
            $"{papel.Descricao} (conversa aberta com o arquivo copiado; o envio é de quem atende)", _agora(), ct);
        await _repo.SalvarAsync(ct);
    }

    // ==================== Comum ====================

    /// <summary>A linha da trilha: QUEM (operador), por qual CANAL e PARA ONDE — sempre os três.</summary>
    private Task Trilha(string acao, string operador, int pacienteId, string canal, string destino, string papel, DateTime quando, CancellationToken ct)
        => _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            DataHora = quando, Operador = operador, Acao = acao, PacienteId = pacienteId,
            Detalhe = $"Canal: {canal} · Para: {destino} · {papel}"
        }, ct);

    private async Task<string?> NomeDaClinicaAsync(CancellationToken ct)
    {
        var prestador = await _parametros.ObterPrestadorAsync(ct);
        return string.IsNullOrWhiteSpace(prestador.NomeFantasia) ? prestador.RazaoSocial : prestador.NomeFantasia;
    }

    /// <summary>O mesmo nome que a entrega local grava ("Receita-2026-0001.pdf").</summary>
    public static string NomeDoArquivo(DocumentoClinico d)
        => $"{TipoDocumentoInfo.Rotular(d.Tipo)}-{d.Numero.Replace('/', '-')}"
           + (d.AssinadoEletronicamente ? "-assinado" : string.Empty) + ".pdf";
}

/// <summary>
/// As frases que acompanham o documento até o paciente — WhatsApp (local ou pelo CRM) e
/// e-mail. Num lugar só para os três caminhos dizerem a mesma coisa.
/// </summary>
public static class TextoDoEnvio
{
    /// <summary>
    /// A legenda do PDF. Assinado: ensina que é o ARQUIVO que vale (sem isso ele manda foto
    /// do papel e a farmácia recusa). Sem assinatura: não promete assinatura nenhuma.
    /// </summary>
    public static string Legenda(string nomePaciente, TipoDocumentoClinico tipo, bool assinado, string? nomeClinica)
        => Legenda(nomePaciente, TipoDocumentoInfo.Rotular(tipo), assinado, nomeClinica);

    public static string Legenda(string nomePaciente, string rotulo, bool assinado, string? nomeClinica)
    {
        var artigo = Feminino(rotulo) ? "sua" : "seu";
        var corpo = assinado
            ? $"Olá, {PrimeiroNome(nomePaciente)}! Segue {artigo} {rotulo.ToLowerInvariant()} "
              + "assinado(a) digitalmente. "
              + "Apresente o ARQUIVO PDF (não a foto nem a impressão) — é nele que fica a "
              + $"assinatura. A farmácia confere em {DocumentosClinicosPdfService.ValidadorOficial}."
            : $"Olá, {PrimeiroNome(nomePaciente)}! Segue {artigo} {rotulo.ToLowerInvariant()} "
              + "emitido(a) pela clínica. Se tiver qualquer dúvida, é só responder esta mensagem.";
        return corpo + (string.IsNullOrWhiteSpace(nomeClinica) ? string.Empty : $" — {nomeClinica}");
    }

    public static string AssuntoEmail(string? nomeClinica)
        => string.IsNullOrWhiteSpace(nomeClinica) ? "Seu documento da clínica" : $"Seu documento — {nomeClinica}";

    /// <summary>Curto e sem nada clínico: o conteúdo é o anexo, e caixa de e-mail é lida por outras pessoas.</summary>
    public static string CorpoEmail(string nomePaciente, bool assinado, string? nomeClinica)
    {
        var quem = string.IsNullOrWhiteSpace(nomeClinica) ? "a clínica" : nomeClinica;
        return $"Olá, {PrimeiroNome(nomePaciente)}!\n\n"
            + $"Segue em anexo o documento que {quem} preparou para você."
            + (assinado ? " O arquivo PDF tem assinatura digital: guarde e apresente o arquivo, não a impressão." : string.Empty)
            + "\n\nSe tiver qualquer dúvida, é só responder este e-mail.\n\n"
            + quem;
    }

    public static string PrimeiroNome(string nome)
        => nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nome;

    private static bool Feminino(string rotulo)
        => rotulo.StartsWith("Receita", StringComparison.OrdinalIgnoreCase)
           || rotulo.StartsWith("Declaração", StringComparison.OrdinalIgnoreCase)
           || rotulo.StartsWith("Anamnese", StringComparison.OrdinalIgnoreCase);
}
