using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Um paciente que parou de vir, com o que a clínica precisa para chamá-lo.</summary>
public sealed record PacienteSumido(
    int PacienteId,
    string Nome,
    string? Telefone,
    DateOnly UltimaSessao,
    int DiasSemVir,
    int TotalSessoes,
    bool TemPacoteAberto,
    bool JaChamado)
{
    /// <summary>
    /// Paciente de tratamento longo que sumiu — o que mais dói perder. Quem veio uma vez
    /// e não voltou é outra conversa (e outro problema).
    /// </summary>
    public bool EraFrequente => TotalSessoes >= RetencaoPacienteService.SessoesParaSerFrequente;

    /// <summary>Faixa de sumiço, para a lista separar quem some agora de quem já foi.</summary>
    public string Faixa => DiasSemVir switch
    {
        < 90 => "1 a 3 meses",
        < 180 => "3 a 6 meses",
        _ => "mais de 6 meses"
    };
}

/// <summary>
/// Quem parou de vir (parcela 32) — a leitura de retenção que a clínica não tinha.
///
/// A campanha de RECALL existe desde a parcela 5 e dispara por regra de tempo; os
/// indicadores medem no-show e ocupação. Nenhuma das duas responde a pergunta que a
/// direção faz quando o faturamento cai: **quem sumiu?** — com nome, telefone e há quanto
/// tempo.
///
/// A diferença entre isto e o recall é o que cada um serve: o recall é uma rodada
/// automática de mensagens; esta é a LISTA, para a direção olhar caso a caso e decidir
/// quem vale um telefonema de verdade. Numa clínica de acupuntura, o paciente de
/// tratamento longo que some vale dez recalls disparados no vazio.
///
/// Três regras:
///
/// 1. **Só quem tem alta chance de estar perdido.** A janela mínima é de
///    <see cref="DiasParaConsiderarSumido"/>; abaixo disso o paciente está entre sessões,
///    e ligar para quem virá na semana que vem gasta o crédito da ligação.
/// 2. **Quem tem sessão FUTURA marcada não sumiu**, por mais tempo que faça desde a
///    última — ele já voltou, só ainda não veio.
/// 3. **Pacote em aberto é destaque**, não filtro: o paciente pagou sessões que não usou,
///    e essa ligação a clínica deve tanto a ela quanto a ele.
/// </summary>
public sealed class RetencaoPacienteService
{
    /// <summary>Dias sem vir a partir dos quais o paciente entra na lista.</summary>
    public const int DiasParaConsiderarSumido = 60;

    /// <summary>Sessões que fazem do paciente um caso de tratamento, não uma visita.</summary>
    public const int SessoesParaSerFrequente = 5;

    private readonly IClinicaRepositorio _repo;
    private readonly PacoteService _pacotes;

    public RetencaoPacienteService(IClinicaRepositorio repo, PacoteService pacotes)
    {
        _repo = repo;
        _pacotes = pacotes;
    }

    /// <summary>
    /// Pacientes que não vêm há mais de <paramref name="diasMinimos"/> dias.
    ///
    /// A base é o ATENDIMENTO, não o agendamento: agendamento cancelado não é visita, e
    /// contar por ele diria que o paciente veio no dia em que ele desmarcou.
    /// </summary>
    public async Task<IReadOnlyList<PacienteSumido>> SumidosAsync(
        DateOnly hoje, int? diasMinimos = null, CancellationToken ct = default)
    {
        var janela = Math.Max(diasMinimos ?? DiasParaConsiderarSumido, 1);
        var corte = hoje.AddDays(-janela);

        var pacientes = await _repo.PacientesComAtendimentosAsync(ct);

        var resultado = new List<PacienteSumido>();

        foreach (var p in pacientes)
        {
            if (p.Atendimentos.Count == 0) continue;

            var ultima = p.Atendimentos.Max(a => a.Data);
            if (ultima > corte) continue;

            // Já voltou: tem horário marcado à frente. Por mais tempo que faça desde a
            // última sessão, ele não está perdido — só ainda não veio.
            var futuros = await _repo.AgendamentosDoPacienteAsync(p.Id, ct);
            if (futuros.Any(a =>
                    a.Status == StatusAgendamento.Agendado
                    && DateOnly.FromDateTime(a.DataHora) >= hoje))
                continue;

            var contatos = await _repo.ContatosDoPacienteAsync(p.Id, ct);

            resultado.Add(new PacienteSumido(
                p.Id,
                p.Nome,
                p.Telefone,
                ultima,
                hoje.DayNumber - ultima.DayNumber,
                p.Atendimentos.Count,
                await TemPacoteAbertoAsync(p.Id, hoje, ct),
                // Já chamado por recall depois da última sessão: a lista mostra, para a
                // clínica não repetir a mesma mensagem e queimar o contato.
                contatos.Any(c => c.Tipo == TipoContato.Recall && c.Referencia >= ultima)));
        }

        return resultado
            // Quem era frequente primeiro: é o que mais dói perder, e o que mais responde
            // a um telefonema.
            .OrderByDescending(s => s.EraFrequente)
            .ThenByDescending(s => s.TemPacoteAberto)
            .ThenBy(s => s.DiasSemVir)
            .ToList();
    }

    private async Task<bool> TemPacoteAbertoAsync(
        int pacienteId, DateOnly hoje, CancellationToken ct)
    {
        try
        {
            var saldos = await _pacotes.DoPacienteAsync(pacienteId, hoje, ct);
            // Ativo com saldo, ou livre (sem contagem) e ainda válido: os dois são
            // sessão comprada e não usada.
            return saldos.Any(s => s.Ativo
                                   && (s.SaldoSessoes is null || s.SaldoSessoes > 0));
        }
        catch (Exception ex)
        {
            // Degradação com rastro: o pacote é destaque da linha, não a razão dela
            // existir — perder essa informação não pode tirar o paciente da lista.
            Diagnostico.Registrar(
                "Retenção — saldo de pacote não pôde ser lido para a lista de sumidos", ex);
            return false;
        }
    }
}
