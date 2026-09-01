using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Resultados de exame estruturados (ago/2026) — registrar e cancelar; nunca editar nem
/// apagar (Lei 13.787/2018; regra 1 do compromisso de conformidade). O molde é o
/// <see cref="MedidaClinicaService"/>: entidade nova montada campo a campo, auditoria no
/// MESMO SaveChanges do ato, operador vindo da TELA (no balcão duas pessoas dividem a
/// máquina — quem sabe quem está logado é o chamador).
/// </summary>
public sealed class ResultadoExameService
{
    private readonly IClinicaRepositorio _repo;

    public ResultadoExameService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>
    /// Registra um resultado. As recusas são as de PLAUSIBILIDADE e completude, nunca de
    /// conteúdo: o valor é texto livre por desenho (a entidade explica), e recusar "não
    /// reagente" por não ser número seria a regra apertada demais que o projeto rejeita.
    /// </summary>
    public async Task<ResultadoExame> RegistrarAsync(
        ResultadoExame dados, string? operador = null, CancellationToken ct = default)
    {
        _ = await _repo.ObterPacienteAsync(dados.PacienteId, ct)
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        if (string.IsNullOrWhiteSpace(dados.Nome))
            throw new InvalidOperationException("Diga QUAL exame é — o nome é obrigatório.");
        if (string.IsNullOrWhiteSpace(dados.Valor))
            throw new InvalidOperationException(
                "Escreva o resultado como o laudo o escreve — o valor é obrigatório.");

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        if (dados.Data > hoje)
            throw new InvalidOperationException(
                "A data do exame não pode ser futura — resultado é registro do que já foi medido.");
        if (dados.Data.Year < 1900)
            throw new InvalidOperationException("Confira a data do exame — o ano não é plausível.");

        // O vínculo com o pedido é conferido ANTES de gravar: amarrado no pedido de
        // OUTRO paciente, este resultado daria baixa na espera de outra pessoa — e a
        // tela de Exames diria "Resultado disponível" sobre um exame que não chegou.
        if (dados.PedidoDocumentoId is { } pedidoId)
        {
            var pedido = await _repo.ObterDocumentoAsync(pedidoId, ct)
                ?? throw new InvalidOperationException("O pedido de exame informado não existe.");
            if (pedido.Tipo != TipoDocumentoClinico.PedidoExame)
                throw new InvalidOperationException(
                    "O documento informado não é um pedido de exame — o vínculo só vale para pedidos.");
            if (pedido.PacienteId != dados.PacienteId)
                throw new InvalidOperationException(
                    "O pedido informado é de OUTRO paciente — amarrar aqui daria baixa na espera errada.");
        }

        // Entidade NOVA, campo a campo: o que não estiver aqui não é gravado, e é
        // exatamente por isso que a lista é explícita (o lugar 3 da conferência).
        var resultado = new ResultadoExame
        {
            PacienteId = dados.PacienteId,
            PedidoDocumentoId = dados.PedidoDocumentoId,
            Data = dados.Data,
            Nome = dados.Nome.Trim(),
            Valor = dados.Valor.Trim(),
            Unidade = Aparar(dados.Unidade),
            Referencia = Aparar(dados.Referencia),
            Laboratorio = Aparar(dados.Laboratorio),
            Observacoes = Aparar(dados.Observacoes),
            CriadoEm = DateTime.Now,
            CriadoPor = Aparar(operador)
        };

        await _repo.AdicionarResultadoExameAsync(resultado, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "ResultadoExameRegistrado",
            Detalhe = $"{resultado.Nome} = {resultado.ValorComUnidade} em {resultado.Data:dd/MM/yyyy}",
            PacienteId = resultado.PacienteId
        }, ct);
        await _repo.SalvarAsync(ct);

        return resultado;
    }

    /// <summary>
    /// Cancela com MOTIVO — a única recusa dura do serviço, pela razão da divergência do
    /// fechamento de caixa: registro clínico desdito sem justificativa é apagar com uma
    /// etapa a mais. A linha fica, marcada.
    /// </summary>
    public async Task CancelarAsync(
        int resultadoId, string motivo, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException(
                "Escreva o motivo do cancelamento — registro clínico não se desdiz sem justificativa.");

        var resultado = await _repo.ObterResultadoExameAsync(resultadoId, ct)
            ?? throw new InvalidOperationException("Resultado de exame não encontrado.");

        if (resultado.Cancelado)
            throw new InvalidOperationException("Este resultado já está cancelado.");

        resultado.CanceladoEm = DateTime.Now;
        resultado.MotivoCancelamento = motivo.Trim();
        resultado.CanceladoPor = Aparar(operador);

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "ResultadoExameCancelado",
            Detalhe = $"{resultado.Nome} de {resultado.Data:dd/MM/yyyy} — motivo: {motivo.Trim()}",
            PacienteId = resultado.PacienteId
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>Os resultados vigentes do paciente, do mais recente para o mais antigo.</summary>
    public Task<IReadOnlyList<ResultadoExame>> DoPacienteAsync(
        int pacienteId, CancellationToken ct = default)
        => _repo.ResultadosExameDoPacienteAsync(pacienteId, incluirCancelados: false, ct);

    private static string? Aparar(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
