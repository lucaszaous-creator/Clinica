using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>Registra a BAIXA de uma guia (faturamento efetivado). Não trata de recebimento/recebíveis.</summary>
public sealed class FaturamentoService
{
    private readonly IClinicaRepositorio _repo;

    public FaturamentoService(IClinicaRepositorio repo) => _repo = repo;

    public async Task DarBaixaAsync(
        int codigoId, DateOnly dataBaixa, string? numeroGuia, string? usuario, string? observacao,
        CancellationToken ct = default)
    {
        var codigo = await _repo.ObterCodigoAsync(codigoId, ct)
            ?? throw new InvalidOperationException($"Código {codigoId} não encontrado.");

        // A crítica de formato mora AQUI, e não em cada tela, porque a baixa tem quatro
        // portas (tela de baixa, baixa em lote, rodada de pendências e fila do Gerente).
        // Validar na tela cobriria uma; validar no caminho único cobre as quatro.
        if (RegraNumeroGuia.Criticar(numeroGuia, FormatoDaGuia(codigo)) is { } critica)
            throw new InvalidOperationException(critica);

        codigo.DarBaixa(dataBaixa, numeroGuia, usuario, observacao);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(usuario) ? "?" : usuario,
            Acao = "BaixaGuia",
            Detalhe = $"Baixa em {dataBaixa:dd/MM/yyyy}" +
                      (string.IsNullOrWhiteSpace(numeroGuia) ? "" : $", guia {numeroGuia}"),
            CodigoId = codigo.Id,
            PacienteId = codigo.Atendimento?.PacienteId
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// Anota (ou atualiza/limpa) por que a guia ainda não foi baixada — a explicação fica
    /// visível na pendência para consulta futura. Não dá baixa: só registra a situação.
    /// </summary>
    public async Task RegistrarObservacaoPendenciaAsync(
        int codigoId, string? observacao, string? usuario, CancellationToken ct = default)
    {
        var codigo = await _repo.ObterCodigoAsync(codigoId, ct)
            ?? throw new InvalidOperationException($"Código {codigoId} não encontrado.");

        var limpando = string.IsNullOrWhiteSpace(observacao);
        codigo.RegistrarObservacaoPendencia(observacao);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(usuario) ? "?" : usuario,
            Acao = limpando ? "ObservacaoPendenciaRemovida" : "ObservacaoPendencia",
            Detalhe = limpando ? "Observação da pendência removida" : observacao!.Trim(),
            CodigoId = codigo.Id,
            PacienteId = codigo.Atendimento?.PacienteId
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>Estorna a baixa de uma guia (reabre a pendência), quando foi baixada por engano.</summary>
    public async Task EstornarBaixaAsync(int codigoId, string? motivo, string? usuario, CancellationToken ct = default)
    {
        var codigo = await _repo.ObterCodigoAsync(codigoId, ct)
            ?? throw new InvalidOperationException($"Código {codigoId} não encontrado.");

        var guiaAnterior = codigo.NumeroGuiaReal;
        codigo.EstornarBaixa(motivo, usuario);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(usuario) ? "?" : usuario,
            Acao = "EstornoBaixa",
            Detalhe = (string.IsNullOrWhiteSpace(motivo) ? "Estorno" : motivo) +
                      (string.IsNullOrWhiteSpace(guiaAnterior) ? "" : $" (guia anterior: {guiaAnterior})"),
            CodigoId = codigo.Id,
            PacienteId = codigo.Atendimento?.PacienteId
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// Formato do número da guia que vale para ESTA guia — o do convênio do paciente.
    ///
    /// Sem paciente carregado devolve <see cref="FormatoNumeroGuia.SemValidacao"/>: não dá
    /// para afirmar o formato de um convênio que não se sabe qual é, e recusar a baixa por
    /// isso travaria o faturamento por causa de um dado que falta no NOSSO lado.
    /// </summary>
    private static FormatoNumeroGuia FormatoDaGuia(CodigoFaturamento codigo)
    {
        var paciente = codigo.Atendimento?.Paciente;
        if (paciente is null) return FormatoNumeroGuia.SemValidacao;

        // ConvenioCodigo nulo significa convênio embutido, e aí o código é o nome da
        // família — a mesma convenção que o catálogo usa em todo o resto do sistema.
        return CatalogoConvenios.FormatoDoNumeroDaGuia(
            paciente.ConvenioCodigo ?? paciente.Convenio.ToString());
    }
}
