using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Colhe a assinatura do PACIENTE num termo de procedimento (parcela 66) — o pedido da
/// clínica para o BSV, com a declaração de jejum junto.
///
/// O que esta assinatura É, e o que ela não é
/// ------------------------------------------
/// ⚠️ Ela <b>NÃO</b> é a assinatura ICP-Brasil das parcelas 42 e 43. Aquela é do
/// PROFISSIONAL, com e-CPF, e existe para o documento valer perante a farmácia e o RH. Esta
/// é do PACIENTE, e a lei a trata de outro jeito: termo de consentimento é documento
/// <b>entre as partes</b>, e a MP 2.200-2/2001 (art. 10, §2º) admite assinatura por outro
/// meio quando as partes o aceitam — a Lei 14.063/2020 chama isso de assinatura SIMPLES.
/// Exigir e-CPF do paciente seria inviável e desnecessário.
///
/// <b>O que dá valor a ela não é certificado: é EVIDÊNCIA.</b> Quem assinou, quando, diante
/// de quem, com que documento conferido, e — o que mais importa numa contestação —
/// exatamente que texto ele tinha na frente. É isso que <see cref="SelarConteudo"/>
/// captura, e é só isso que o rodapé do PDF pode afirmar
/// (<see cref="DocumentoClinico.FraseAssinaturaPaciente"/>).
///
/// A ordem das duas assinaturas é uma amarra técnica
/// -------------------------------------------------
/// ⚠️ <b>O PDF não se assina incrementalmente</b> — é a restrição que a parcela 42 já
/// encontrou. A assinatura qualificada sela uma faixa de BYTES; carimbar o traço do
/// paciente depois invalidaria o selo do profissional. Logo o traço tem de estar DENTRO do
/// arquivo antes dele, e a ordem é: emitir → paciente assina → gerar o PDF com o traço →
/// profissional sela → guardar os bytes.
///
/// Por isso este serviço RECUSA colher assinatura em documento que já foi assinado
/// eletronicamente. Não é preciosismo: a alternativa é produzir, em silêncio, um arquivo
/// cujo selo não fecha — e um PDF que abre como inválido no validador é a garantia
/// aparente que este projeto se recusa a produzir desde a parcela 3.
/// </summary>
public sealed class AssinaturaDoPacienteService
{
    private readonly IClinicaRepositorio _repo;

    /// <summary>
    /// Piso de bytes para um PNG ser um traço de verdade. Um canvas em branco exportado
    /// ainda produz um arquivo válido de algumas centenas de bytes, e gravá-lo daria um
    /// termo "assinado" com a linha vazia — pior do que um termo pendente, porque some da
    /// lista de quem falta.
    /// </summary>
    public const int TamanhoMinimoTraco = 256;

    /// <summary>Teto do traço. Um PNG de assinatura tem dezenas de KB; MB é defeito.</summary>
    public const int TamanhoMaximoTraco = 2 * 1024 * 1024;

    public AssinaturaDoPacienteService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>
    /// Grava a assinatura do paciente, as respostas das declarações e a evidência, tudo no
    /// MESMO <c>SaveChanges</c> da auditoria.
    /// </summary>
    /// <param name="respostas">
    /// Resposta de cada declaração, pela ORDEM do item no documento. Declaração sem
    /// resposta fica em branco e aparece assim no papel — inventar "Sim" para o que
    /// ninguém respondeu seria fabricar consentimento.
    /// </param>
    /// <param name="documentoConferido">
    /// O documento de identidade apresentado ("CPF 123.456.789-00"). É o que substitui o
    /// certificado, e por isso é exigido.
    /// </param>
    /// <param name="testemunha">
    /// Quem da clínica estava na frente do paciente. <c>SessaoUsuario.Atual.Operador</c>,
    /// nunca <c>Environment.UserName</c>: no balcão duas pessoas dividem a máquina.
    /// </param>
    public async Task<DocumentoClinico> ColherAsync(
        int documentoId,
        byte[] tracoPng,
        int largura,
        int altura,
        IReadOnlyDictionary<int, string?> respostas,
        string documentoConferido,
        string testemunha,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tracoPng);
        ArgumentNullException.ThrowIfNull(respostas);

        var documento = await CarregarParaAssinarAsync(documentoId, ct);

        if (tracoPng.Length < TamanhoMinimoTraco)
            throw new InvalidOperationException(
                "A assinatura está em branco. Peça ao paciente para assinar na área indicada.");

        if (tracoPng.Length > TamanhoMaximoTraco)
            throw new InvalidOperationException(
                "A imagem da assinatura está grande demais para ser um traço.");

        if (string.IsNullOrWhiteSpace(documentoConferido))
            throw new InvalidOperationException(
                "Informe o documento de identidade conferido — é ele que prova quem "
                + "assinou, já que o paciente não usa certificado digital.");

        if (string.IsNullOrWhiteSpace(testemunha))
            throw new InvalidOperationException(
                "Não há usuário identificado para testemunhar a assinatura. Faça login.");

        // As respostas entram ANTES do selo: o hash tem de cobrir o que o paciente viu na
        // tela, declarações respondidas incluídas. Selar só o corpo deixaria de fora
        // justamente a parte que se contesta ("eu nunca disse que estava em jejum").
        foreach (var item in documento.Itens)
            if (respostas.TryGetValue(item.Ordem, out var resposta))
                item.Quantidade = string.IsNullOrWhiteSpace(resposta) ? null : resposta.Trim();

        var traco = new TracoAssinatura
        {
            Conteudo = tracoPng,
            Largura = largura,
            Altura = altura,
            ColhidoEm = DateTime.Now
        };

        documento.TracoAssinatura = traco;
        documento.PacienteAssinadoEm = DateTime.Now;
        documento.PacienteAssinaturaMeio = MeioAssinaturaPaciente.NaClinica;
        documento.PacienteAssinaturaHash = SelarConteudo(documento);
        documento.PacienteDocumentoConferido = documentoConferido.Trim();
        documento.PacienteAssinaturaTestemunha = testemunha.Trim();

        var negadas = documento.Itens
            .Where(i => RespostaDeclaracao.EhNegativa(i.Quantidade))
            .Select(i => i.Descricao)
            .ToList();

        var detalhe = $"{TipoDocumentoInfo.Rotular(documento.Tipo)} {documento.Numero} — "
                      + $"{documento.Paciente?.Nome}";

        // A declaração negada vai para a TRILHA e não só para a tela: ela é o fato que uma
        // investigação procura, e a tela some quando o dia acaba.
        if (negadas.Count > 0)
            detalhe += $" — respondeu NÃO em: {string.Join("; ", negadas)}";

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = testemunha.Trim(),
            Acao = "TermoAssinadoPeloPaciente",
            Detalhe = detalhe,
            PacienteId = documento.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return documento;
    }

    /// <summary>
    /// Registra que o paciente RECUSOU assinar.
    ///
    /// Recusa é fato, e precisa de onde ser escrita: sem isso o termo fica eternamente
    /// "pendente" e ninguém distingue quem ainda não foi chamado de quem disse não — e é a
    /// segunda pessoa que exige decisão de quem vai fazer o procedimento.
    ///
    /// O motivo é OBRIGATÓRIO, pela razão da justificativa do fechamento de caixa e do
    /// descarte de problema: recusa sem motivo é a mesma linha em branco que o sistema
    /// teria sem o registro.
    /// </summary>
    public async Task<DocumentoClinico> RecusarAsync(
        int documentoId, string motivo, string testemunha, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException(
                "Diga por que o paciente não assinou — é o que distingue uma recusa de um "
                + "termo que ninguém chegou a apresentar.");

        if (string.IsNullOrWhiteSpace(testemunha))
            throw new InvalidOperationException(
                "Não há usuário identificado para registrar a recusa. Faça login.");

        var documento = await CarregarParaAssinarAsync(documentoId, ct);

        documento.PacienteRecusouEm = DateTime.Now;
        documento.MotivoRecusaPaciente = motivo.Trim();
        documento.PacienteAssinaturaTestemunha = testemunha.Trim();

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = testemunha.Trim(),
            Acao = "TermoRecusadoPeloPaciente",
            Detalhe = $"{TipoDocumentoInfo.Rotular(documento.Tipo)} {documento.Numero} — "
                      + $"{documento.Paciente?.Nome}: {motivo.Trim()}",
            PacienteId = documento.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return documento;
    }

    /// <summary>Os bytes do traço, para o PDF desenhá-lo.</summary>
    public Task<TracoAssinatura?> TracoAsync(int tracoId, CancellationToken ct = default)
        => _repo.ObterTracoAssinaturaAsync(tracoId, ct);

    /// <summary>
    /// Confere se o conteúdo de um documento assinado continua sendo o que foi selado.
    ///
    /// É a metade que torna a evidência verificável em vez de decorativa: guardar um hash
    /// que ninguém recalcula é guardar um número. Devolve null quando o documento não tem
    /// selo — "não sei" é resposta diferente de "não bate".
    /// </summary>
    public static bool? ConteudoIntacto(DocumentoClinico documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        return string.IsNullOrWhiteSpace(documento.PacienteAssinaturaHash)
            ? null
            : string.Equals(documento.PacienteAssinaturaHash, SelarConteudo(documento),
                StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// SHA-256 do que o paciente tinha na frente.
    ///
    /// A montagem é fixa e em cultura INVARIANTE de propósito: o hash é GRAVADO e
    /// recalculado meses depois, possivelmente noutra máquina — uma data formatada na
    /// cultura local faria a conferência acusar adulteração em toda clínica que trocasse
    /// de computador. É o mesmo cuidado que o <c>ParametrosService</c> toma ao ler número.
    /// </summary>
    private static string SelarConteudo(DocumentoClinico documento)
    {
        var texto = new StringBuilder();

        texto.Append(documento.Numero).Append('\n');
        texto.Append(documento.Data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('\n');
        texto.Append(documento.TituloImpresso).Append('\n');
        texto.Append(documento.Corpo ?? string.Empty).Append('\n');

        foreach (var item in documento.Itens.OrderBy(i => i.Ordem))
            texto.Append(item.Ordem).Append('|')
                 .Append(item.Descricao).Append('|')
                 .Append(item.Detalhe ?? string.Empty).Append('|')
                 .Append(item.Quantidade ?? string.Empty).Append('\n');

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(texto.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task<DocumentoClinico> CarregarParaAssinarAsync(
        int documentoId, CancellationToken ct)
    {
        var documento = await _repo.ObterDocumentoAsync(documentoId, ct)
            ?? throw new InvalidOperationException("Documento não encontrado.");

        if (!TipoDocumentoInfo.AssinadoPeloPaciente(documento.Tipo))
            throw new InvalidOperationException(
                $"{TipoDocumentoInfo.Rotular(documento.Tipo)} não é assinado pelo paciente.");

        if (documento.Cancelado)
            throw new InvalidOperationException(
                "Este termo foi cancelado. Emita outro para colher a assinatura.");

        if (documento.PacienteAssinou)
            throw new InvalidOperationException(
                "Este termo já foi assinado pelo paciente. Corrigir um termo assinado é "
                + "cancelá-lo com motivo e emitir outro.");

        if (documento.PacienteRecusou)
            throw new InvalidOperationException(
                "O paciente já recusou assinar este termo. Emita outro para apresentá-lo "
                + "de novo — a recusa é um fato e não se apaga.");

        // A amarra da ordem das assinaturas, explicada no cabeçalho da classe.
        if (documento.AssinadoEletronicamente)
            throw new InvalidOperationException(
                "Este termo já foi assinado eletronicamente pelo profissional. A assinatura "
                + "do paciente precisa entrar ANTES: ela vai dentro dos bytes que o "
                + "certificado sela, e acrescentá-la agora invalidaria o selo.");

        return documento;
    }
}
