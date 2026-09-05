using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// As próximas vagas de um profissional (set/2026): três leituras em SEQUÊNCIA (mesmo
/// <c>DbContext</c> — parcela 74) e o cálculo puro de <see cref="BuscaDeVagas"/>.
/// Não grava nada — quem marca é o Marcar, com a vaga escolhida já preenchida.
/// </summary>
public sealed class BuscaDeVagasService
{
    private readonly IClinicaRepositorio _repo;

    public BuscaDeVagasService(IClinicaRepositorio repo) => _repo = repo;

    /// <param name="duracaoMinutos">A duração pedida; nula usa a padrão do profissional, e na falta dela a da clínica.</param>
    public async Task<ResultadoBuscaDeVagas> ProximasAsync(
        int profissionalId, DateTime aPartirDe, int? duracaoMinutos = null,
        int quantidade = BuscaDeVagas.QuantidadePadrao, CancellationToken ct = default)
    {
        var profissional = await _repo.ObterProfissionalAsync(profissionalId, ct)
            ?? throw new InvalidOperationException("Profissional não encontrado.");

        var duracao = duracaoMinutos ?? profissional.DuracaoPadraoMinutos ?? Agendamento.DuracaoPadraoMinutos;
        var ateQuando = aPartirDe.Date.AddDays(BuscaDeVagas.DiasMaximos);

        // Só os horários DELE no período — a agenda da clínica inteira por dois meses seria
        // milhares de linhas com paciente e sala para não usar nenhum.
        var ocupados = await _repo.AgendamentosDoProfissionalNoPeriodoAsync(
            profissionalId, aPartirDe.Date, ateQuando, ct);
        var bloqueios = await _repo.BloqueiosNoPeriodoAsync(aPartirDe.Date, ateQuando, ct);

        var vagas = BuscaDeVagas.Calcular(aPartirDe, duracao, profissional, ocupados, bloqueios, quantidade);

        return new ResultadoBuscaDeVagas(
            profissional.Rotulo,
            duracao,
            aPartirDe,
            ateQuando,
            BuscaDeVagas.JornadaPresumida(profissional),
            profissional.DescricaoJornada,
            vagas);
    }
}
