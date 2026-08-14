using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Recibo e orçamento — os dois documentos da página 21 que faltavam depois da parcela 3.
///
/// Seguem as MESMAS regras dos documentos clínicos, porque o problema é o mesmo: papel
/// entregue existe no mundo. Não se apaga nem se reescreve — cancela-se com motivo e
/// emite-se outro; e os valores ficam gravados na emissão, para a segunda via não sair
/// diferente da primeira porque a tabela de preços mudou.
/// </summary>
public sealed class DocumentoFinanceiroService
{
    private readonly IClinicaRepositorio _repo;

    private const string AlfabetoCodigo = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>Validade padrão de um orçamento, em dias.</summary>
    public const int ValidadePadraoOrcamentoDias = 30;

    public DocumentoFinanceiroService(IClinicaRepositorio repo) => _repo = repo;

    public Task<DocumentoFinanceiro?> ObterAsync(int documentoId, CancellationToken ct = default)
        => _repo.ObterDocumentoFinanceiroAsync(documentoId, ct);

    /// <summary>Confere uma via em papel pelo código impresso no rodapé.</summary>
    public Task<DocumentoFinanceiro?> PorCodigoAsync(string codigo, CancellationToken ct = default)
        => _repo.ObterDocumentoFinanceiroPorCodigoAsync(
            (codigo ?? string.Empty).Trim().ToUpperInvariant(), ct);

    public Task<IReadOnlyList<DocumentoFinanceiro>> DoPeriodoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => _repo.DocumentosFinanceirosAsync(inicio, fim, ct);

    /// <summary>Emite um recibo ou orçamento com as linhas informadas.</summary>
    public async Task<DocumentoFinanceiro> EmitirAsync(
        DocumentoFinanceiro dados, string? operador = null, CancellationToken ct = default)
    {
        var itens = dados.Itens
            .Where(i => !string.IsNullOrWhiteSpace(i.Descricao))
            .ToList();

        if (itens.Count == 0)
            throw new InvalidOperationException(
                dados.Tipo == TipoDocumentoFinanceiro.Recibo
                    ? "Um recibo sem nenhuma linha não comprova nada: diga do que ele é."
                    : "Um orçamento precisa de ao menos um item.");

        if (itens.Any(i => i.ValorUnitario < 0 || i.Quantidade <= 0))
            throw new InvalidOperationException(
                "Quantidade tem de ser maior que zero e valor não pode ser negativo.");

        var paciente = dados.PacienteId is { } pacienteId
            ? await _repo.ObterPacienteAsync(pacienteId, ct)
              ?? throw new InvalidOperationException("Paciente não encontrado.")
            : null;

        // Quem paga nem sempre é o paciente (pai, empresa, plano) — mas alguém tem de
        // ser: recibo sem destinatário não vale para quem precisa dele.
        var destinatario = string.IsNullOrWhiteSpace(dados.Destinatario)
            ? paciente?.Nome
            : dados.Destinatario.Trim();

        if (string.IsNullOrWhiteSpace(destinatario))
            throw new InvalidOperationException("Informe para quem o documento está sendo emitido.");

        var data = dados.Data == default ? DateOnly.FromDateTime(DateTime.Today) : dados.Data;

        // Tipado explicitamente: com `var`, o compilador não acha tipo comum entre
        // DateOnly e null (CS0173). Recibo não tem validade; orçamento sempre tem.
        DateOnly? validoAte = dados.Tipo == TipoDocumentoFinanceiro.Orcamento
            ? dados.ValidoAte ?? data.AddDays(ValidadePadraoOrcamentoDias)
            : null;

        if (validoAte is { } limite && limite < data)
            throw new InvalidOperationException("A validade do orçamento é anterior à emissão.");

        var documento = new DocumentoFinanceiro
        {
            Numero = await NumerarAsync(data.Year, dados.Tipo, ct),
            CodigoVerificacao = GerarCodigo(),
            Tipo = dados.Tipo,
            PacienteId = dados.PacienteId,
            Destinatario = destinatario!,
            DocumentoDestinatario = Limpar(dados.DocumentoDestinatario) ?? paciente?.Documento,
            Data = data,
            ValidoAte = validoAte,
            Titulo = Limpar(dados.Titulo),
            Corpo = Limpar(dados.Corpo),
            Observacoes = Limpar(dados.Observacoes),
            FormaPagamento = dados.FormaPagamento,
            LancamentoFinanceiroId = dados.LancamentoFinanceiroId,
            CriadoEm = DateTime.Now,
            CriadoPor = operador
        };

        var ordem = 1;
        foreach (var i in itens)
            documento.Itens.Add(new ItemDocumentoFinanceiro
            {
                Ordem = ordem++,
                Descricao = i.Descricao.Trim(),
                Quantidade = i.Quantidade,
                ValorUnitario = i.ValorUnitario
            });

        await _repo.AdicionarDocumentoFinanceiroAsync(documento, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "DocumentoFinanceiroEmitido",
            Detalhe = $"{documento.Numero} — {destinatario} — {documento.ValorTotal:C}",
            PacienteId = documento.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return documento;
    }

    /// <summary>
    /// Recibo a partir de um lançamento já realizado no caixa. É o caminho normal: o
    /// dinheiro entrou, o papel comprova aquele dinheiro — e fica apontando para ele,
    /// para ninguém emitir dois recibos do mesmo pagamento sem perceber.
    /// </summary>
    public async Task<DocumentoFinanceiro> EmitirReciboDoLancamentoAsync(
        int lancamentoId, string? destinatario = null, string? operador = null,
        CancellationToken ct = default)
    {
        var lancamento = await _repo.ObterLancamentoAsync(lancamentoId, ct)
            ?? throw new InvalidOperationException("Lançamento não encontrado.");

        if (lancamento.Tipo != TipoLancamento.Entrada)
            throw new InvalidOperationException("Só se dá recibo de dinheiro que entrou.");

        if (lancamento.Status == StatusLancamento.Cancelado)
            throw new InvalidOperationException("Este lançamento está cancelado.");

        return await EmitirAsync(new DocumentoFinanceiro
        {
            Tipo = TipoDocumentoFinanceiro.Recibo,
            PacienteId = lancamento.PacienteId,
            Destinatario = destinatario ?? string.Empty,
            Data = lancamento.DataPagamento ?? lancamento.Data,
            FormaPagamento = lancamento.FormaPagamento,
            LancamentoFinanceiroId = lancamento.Id,
            Corpo = "Recebi a importância descrita abaixo, referente aos serviços prestados.",
            Itens =
            [
                new ItemDocumentoFinanceiro
                {
                    Descricao = lancamento.Descricao,
                    Quantidade = 1,
                    ValorUnitario = lancamento.Valor
                }
            ]
        }, operador, ct);
    }

    /// <summary>Orçamento de um pacote do catálogo — a proposta que o balcão entrega.</summary>
    public async Task<DocumentoFinanceiro> EmitirOrcamentoDoPacoteAsync(
        int catalogoId, int? pacienteId = null, string? destinatario = null,
        string? operador = null, CancellationToken ct = default)
    {
        var pacote = await _repo.ObterPacoteCatalogoAsync(catalogoId, ct)
            ?? throw new InvalidOperationException("Pacote não encontrado no catálogo.");

        var descricao = pacote.SessoesIncluidas is { } sessoes
            ? $"{pacote.Nome} — {sessoes} sessões"
            : pacote.Nome;

        return await EmitirAsync(new DocumentoFinanceiro
        {
            Tipo = TipoDocumentoFinanceiro.Orcamento,
            PacienteId = pacienteId,
            Destinatario = destinatario ?? string.Empty,
            Data = DateOnly.FromDateTime(DateTime.Today),
            Corpo = pacote.ValidadeDias is { } dias
                ? $"Proposta para o pacote abaixo. Após a compra, o pacote vale por {dias} dias."
                : "Proposta para o pacote abaixo.",
            Itens =
            [
                new ItemDocumentoFinanceiro
                {
                    Descricao = descricao,
                    Quantidade = 1,
                    ValorUnitario = pacote.Valor
                }
            ]
        }, operador, ct);
    }

    /// <summary>Cancela o documento. Não apaga: a via entregue continua existindo.</summary>
    public async Task CancelarAsync(
        int documentoId, string motivo, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Diga por que o documento está sendo cancelado.");

        var documento = await _repo.ObterDocumentoFinanceiroAsync(documentoId, ct)
            ?? throw new InvalidOperationException("Documento não encontrado.");

        if (documento.Cancelado)
            throw new InvalidOperationException("Este documento já foi cancelado.");

        documento.CanceladoEm = DateTime.Now;
        documento.MotivoCancelamento = motivo.Trim();

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "DocumentoFinanceiroCancelado",
            Detalhe = $"{documento.Numero} — {motivo.Trim()}",
            PacienteId = documento.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
    }

    private async Task<string> NumerarAsync(int ano, TipoDocumentoFinanceiro tipo, CancellationToken ct)
    {
        var sequencial = await _repo.ProximoNumeroDocumentoFinanceiroAsync(ano, tipo, ct);
        return $"{DocumentoFinanceiro.Prefixo(tipo)} {ano}/{sequencial:0000}";
    }

    /// <summary>Código curto de conferência da via em papel, vindo de um Guid.</summary>
    private static string GerarCodigo()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        var letras = new char[11];
        var origem = 0;

        for (var i = 0; i < letras.Length; i++)
            letras[i] = i == 5 ? '-' : AlfabetoCodigo[bytes[origem++] % AlfabetoCodigo.Length];

        return new string(letras);
    }

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
