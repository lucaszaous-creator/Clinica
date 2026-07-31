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
    /// <remarks>
    /// A leitura é feita em QUATRO consultas, e não em uma mais três por paciente.
    ///
    /// A primeira versão carregava a tabela de pacientes inteira com a de atendimentos
    /// junto e, para cada pessoa, ia ao banco buscar agendamento futuro, contatos e
    /// pacotes. Numa base de dois mil pacientes são seis mil idas a um Postgres REMOTO —
    /// a tela demoraria minutos e a direção concluiria que ela não funciona. O agrupamento
    /// agora sai do banco, e as três perguntas seguintes são feitas de uma vez só para o
    /// conjunto JÁ FILTRADO, que é pequeno por definição.
    /// </remarks>
    public async Task<IReadOnlyList<PacienteSumido>> SumidosAsync(
        DateOnly hoje, int? diasMinimos = null, CancellationToken ct = default)
    {
        var janela = Math.Max(diasMinimos ?? DiasParaConsiderarSumido, 1);
        var corte = hoje.AddDays(-janela);

        // 1ª consulta: uma linha por paciente atendido, agregada no banco.
        var resumos = (await _repo.ResumoAtendimentosPorPacienteAsync(ct))
            .Where(r => r.UltimaSessao <= corte)
            .ToList();

        if (resumos.Count == 0) return [];

        var ids = resumos.Select(r => r.PacienteId).ToList();

        // 2ª: quem já voltou. Tem horário marcado à frente — por mais tempo que faça
        // desde a última sessão, ele não está perdido, só ainda não veio.
        var jaVoltaram = (await _repo.PacientesComAgendamentoFuturoAsync(ids, hoje, ct))
            .ToHashSet();

        var candidatos = resumos.Where(r => !jaVoltaram.Contains(r.PacienteId)).ToList();
        if (candidatos.Count == 0) return [];

        var restantes = candidatos.Select(r => r.PacienteId).ToList();

        // 3ª: quem já recebeu recall DEPOIS da própria última sessão — a lista mostra,
        // para a clínica não repetir a mesma mensagem e queimar o contato.
        var jaChamados = (await _repo.PacientesJaContatadosAsync(
                candidatos.ToDictionary(r => r.PacienteId, r => r.UltimaSessao),
                TipoContato.Recall, ct))
            .ToHashSet();

        // 4ª: pacotes em aberto de todos eles.
        var comPacote = await PacientesComPacoteAbertoAsync(restantes, hoje, ct);

        var resultado = candidatos
            .Select(r => new PacienteSumido(
                r.PacienteId,
                r.Nome,
                r.Telefone,
                r.UltimaSessao,
                hoje.DayNumber - r.UltimaSessao.DayNumber,
                r.TotalSessoes,
                comPacote.Contains(r.PacienteId),
                jaChamados.Contains(r.PacienteId)))
            .ToList();

        return resultado
            // Quem era frequente primeiro: é o que mais dói perder, e o que mais responde
            // a um telefonema.
            .OrderByDescending(s => s.EraFrequente)
            .ThenByDescending(s => s.TemPacoteAberto)
            .ThenBy(s => s.DiasSemVir)
            .ToList();
    }

    /// <summary>
    /// Quais dos pacientes informados têm pacote em aberto — numa consulta só.
    ///
    /// A leitura em si continua sendo a do `PacoteService`: quem sabe o que é "ativo com
    /// saldo" é ele, e reimplementar a regra aqui criaria duas respostas para a mesma
    /// pergunta. O que mudou é a origem dos dados, que vem em bloco em vez de uma
    /// consulta por pessoa.
    /// </summary>
    private async Task<HashSet<int>> PacientesComPacoteAbertoAsync(
        IReadOnlyCollection<int> pacienteIds, DateOnly hoje, CancellationToken ct)
    {
        try
        {
            var pacotes = await _repo.PacotesDosPacientesAsync(pacienteIds, ct);

            return pacotes
                .GroupBy(p => p.PacienteId)
                .Where(g => _pacotes.Descrever(g, hoje)
                    // Ativo com saldo, ou livre (sem contagem) e ainda válido: os dois
                    // são sessão comprada e não usada.
                    .Any(s => s.Ativo && (s.SaldoSessoes is null || s.SaldoSessoes > 0)))
                .Select(g => g.Key)
                .ToHashSet();
        }
        catch (Exception ex)
        {
            // Degradação com rastro: o pacote é destaque da linha, não a razão dela
            // existir — perder essa informação não pode tirar ninguém da lista.
            Diagnostico.Registrar(
                "Retenção — saldo de pacote não pôde ser lido para a lista de sumidos", ex);
            return [];
        }
    }
}
