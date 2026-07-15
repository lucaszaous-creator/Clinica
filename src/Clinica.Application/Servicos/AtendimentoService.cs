using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>Resultado do lançamento de um atendimento: o atendimento criado e os avisos para a secretária.</summary>
public sealed record ResultadoLancamento(Atendimento Atendimento, IReadOnlyList<string> Avisos);

/// <summary>
/// Lança um atendimento e gera automaticamente os códigos de faturamento pela regra do convênio.
/// É aqui que o 2º código nasce já com data prevista de +24h — para nunca ser esquecido.
/// </summary>
public sealed class AtendimentoService
{
    private readonly IClinicaRepositorio _repo;
    private readonly RegistroRegras _regras;

    private readonly ParametrosService? _parametros;

    public AtendimentoService(IClinicaRepositorio repo, RegistroRegras? regras = null, ParametrosService? parametros = null)
    {
        _repo = repo;
        _regras = regras ?? new RegistroRegras();
        _parametros = parametros;
    }

    public async Task<ResultadoLancamento> LancarAsync(
        int pacienteId, DateOnly data, ModalidadeAtendimento modalidade, string? observacoes = null,
        CancellationToken ct = default)
    {
        var paciente = await _repo.ObterPacienteAsync(pacienteId, ct)
            ?? throw new InvalidOperationException($"Paciente {pacienteId} não encontrado.");

        var atendimento = new Atendimento
        {
            PacienteId = pacienteId,
            Data = data,
            Modalidade = modalidade,
            Observacoes = observacoes
        };

        var historicoMes = await _repo.CodigosDoPacienteNoMesAsync(pacienteId, data.Year, data.Month, ct);
        var dias = _parametros is null ? 1 : (await _parametros.ObterAsync(ct)).DiasSegundoCodigo(paciente.Convenio);
        var contexto = new ContextoFaturamento { CodigosNoMes = historicoMes, DiasSegundoCodigo = dias };

        var resultado = _regras.Para(paciente.Convenio).Gerar(paciente, atendimento, contexto);

        atendimento.Categoria = resultado.Categoria;
        atendimento.Codigos.AddRange(resultado.Codigos);

        // Mantém a categoria mais recente na ficha do paciente.
        paciente.Categoria = resultado.Categoria;

        await _repo.AdicionarAtendimentoAsync(atendimento, ct);
        await _repo.SalvarAsync(ct);

        // Número/protocolo do atendimento (o Id já existe após salvar) — base do lastro de faturamento.
        atendimento.Numero = $"{data.Year}-{atendimento.Id:D6}";
        await _repo.SalvarAsync(ct);

        return new ResultadoLancamento(atendimento, resultado.Avisos);
    }
}
