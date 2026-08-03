using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// O dia de quem ATENDE (parcela 36) — a leitura que faltava para o sistema chegar à
/// máquina do médico e do fisioterapeuta.
///
/// Por que não é a fila da recepção
/// --------------------------------
/// A agenda e a fila do balcão já existem desde a parcela 1, e respondem "quem chegou" e
/// "quem falta chamar". O consultório pergunta outra coisa: **o que eu ainda não
/// escrevi.** É a mesma família do defeito que dá nome ao produto — a guia obtida +24h
/// depois que ninguém lembra —, só que do lado clínico: a sessão acontece, o paciente vai
/// embora e a evolução fica "para depois". Depois é o dia em que já não se lembra o que
/// foi feito, e um prontuário escrito de memória vale menos do que um prontuário vazio,
/// porque parece registro.
///
/// Por que mora fora do <c>AgendaService</c>
/// -----------------------------------------
/// Pela mesma razão de o <c>FechamentoSessaoService</c> morar fora do
/// <c>AtendimentoService</c>: aquele é compartilhado com o faturamento congelado, e dar
/// efeito ou dependência nova a ele muda o comportamento de um app em produção. Este
/// serviço só LÊ, e lê pelo repositório.
///
/// Sem profissional vinculado
/// --------------------------
/// Um usuário do consultório pode não ter <c>Profissional</c> ligado ao login (a clínica
/// ainda não cadastrou a equipe, ou é um residente usando o acesso da casa). Nesse caso o
/// serviço devolve o dia INTEIRO da clínica, com o nome "Todos os profissionais" — a
/// alternativa seria uma tela vazia, que se lê como defeito, e não como "falta cadastro".
/// </summary>
public sealed class ConsultorioService
{
    private readonly IClinicaRepositorio _repo;

    /// <summary>
    /// Até quantos dias para trás procurar sessão sem evolução escrita. Não é limite
    /// técnico: passado disso o registro não se escreve mais de memória, e uma lista que
    /// cresce sem fim vira ruído que a pessoa aprende a fechar sem ler.
    /// </summary>
    public const int JanelaRegistroPendenteDias = 30;

    public ConsultorioService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>
    /// O dia do profissional: os horários dele, com a marca do que já tem evolução.
    /// </summary>
    public async Task<DiaDoProfissional> DoDiaAsync(
        DateOnly dia, int? profissionalId, CancellationToken ct = default)
    {
        var agendamentos = await _repo.AgendamentosNoPeriodoAsync(
            dia.ToDateTime(TimeOnly.MinValue), dia.ToDateTime(TimeOnly.MaxValue), ct);

        if (profissionalId is { } id)
            agendamentos = agendamentos.Where(a => a.ProfissionalId == id).ToList();

        var evolucoes = await _repo.EvolucoesNoPeriodoAsync(dia, dia, ct, profissionalId);

        var sessoes = agendamentos
            .OrderBy(a => a.DataHora)
            .Select(a => Montar(a, evolucoes))
            .ToList();

        var nome = profissionalId is null
            ? "Todos os profissionais"
            : (await _repo.ObterProfissionalAsync(profissionalId.Value, ct))?.Rotulo
              ?? "Profissional";

        return new DiaDoProfissional(dia, profissionalId, nome, sessoes);
    }

    /// <summary>
    /// Sessões de dias ANTERIORES que aconteceram e continuam sem evolução escrita.
    ///
    /// O dia de hoje fica fora de propósito: a sessão das 14h ainda não tem evolução às
    /// 14h05 porque o paciente ainda está na sala, e cobrá-la ali transformaria a lista
    /// numa contagem do trabalho em curso. Quem cuida de hoje é o
    /// <see cref="DiaDoProfissional.RegistrosPendentes"/>.
    /// </summary>
    public async Task<IReadOnlyList<RegistroPendente>> RegistrosPendentesAsync(
        DateOnly hoje, int? profissionalId, CancellationToken ct = default)
    {
        var inicio = hoje.AddDays(-JanelaRegistroPendenteDias);
        var ontem = hoje.AddDays(-1);
        if (ontem < inicio) return [];

        var agendamentos = await _repo.AgendamentosNoPeriodoAsync(
            inicio.ToDateTime(TimeOnly.MinValue), ontem.ToDateTime(TimeOnly.MaxValue), ct);

        if (profissionalId is { } id)
            agendamentos = agendamentos.Where(a => a.ProfissionalId == id).ToList();

        var evolucoes = await _repo.EvolucoesNoPeriodoAsync(inicio, ontem, ct, profissionalId);

        return agendamentos
            .Where(a => a.Status == StatusAgendamento.Realizado)
            .Select(a => (Agendamento: a, Sessao: Montar(a, evolucoes)))
            .Where(x => x.Sessao.RegistroPendente)
            .OrderBy(x => x.Agendamento.DataHora)
            .Select(x => new RegistroPendente(
                x.Agendamento.Id,
                x.Agendamento.DataHora,
                x.Agendamento.PacienteId,
                x.Agendamento.Paciente?.Nome ?? "Paciente",
                CatalogoModalidades.Nome(x.Agendamento.ModalidadeCodigo),
                x.Agendamento.AtendimentoId))
            .ToList();
    }

    /// <summary>
    /// Os pacientes deste profissional, com a leitura da dor de cada um já resolvida.
    ///
    /// A dor vem do prontuário, uma consulta por paciente — e é por isso que a lista tem
    /// teto. Com <paramref name="comDor"/> desligado a tela abre instantânea e mostra só
    /// quem é e quando veio; ligado, ela responde "como este tratamento está indo?" sem
    /// abrir o prontuário de ninguém, que é a pergunta do profissional antes de chamar o
    /// próximo.
    /// </summary>
    public async Task<IReadOnlyList<PacienteDoProfissional>> MeusPacientesAsync(
        int? profissionalId, int limite = 200, bool comDor = true, CancellationToken ct = default)
    {
        // Sem profissional vinculado, a carteira é a da clínica: o consultório continua
        // servindo (residente, profissional recém-cadastrado), só não filtra por dono.
        var pacientes = profissionalId is { } id
            ? await _repo.PacientesDoProfissionalAsync(id, limite, ct)
            : (await _repo.BuscarPacientesAsync(null, limite, ct))
                .Select(p => new PacienteDoProfissional(p.Id, p.Nome, null, 0))
                .ToList();

        if (!comDor) return pacientes;

        var comLeitura = new List<PacienteDoProfissional>(pacientes.Count);
        foreach (var p in pacientes)
        {
            var evolucoes = await _repo.EvolucoesDoPacienteAsync(p.PacienteId, ct);

            // Só o par EVA completo entra, como no resto do projeto: uma medida solta não
            // diz se o tratamento funcionou, e puxaria a leitura por falta de dado.
            var medidas = evolucoes
                .Where(e => e.TemParEva)
                .OrderBy(e => e.Data).ThenBy(e => e.Id)
                .ToList();

            comLeitura.Add(p with
            {
                DorInicial = medidas.Count == 0 ? null : medidas[0].EvaAntes,
                UltimaDor = medidas.Count == 0 ? null : medidas[^1].EvaDepois
            });
        }

        return comLeitura;
    }

    /// <summary>
    /// A sessão do dia com a evolução casada.
    ///
    /// O casamento é pelo <c>AgendamentoId</c> da evolução — que é o vínculo real — e cai
    /// para paciente + data quando ele é nulo. O caminho de baixo existe porque a evolução
    /// escrita direto no prontuário (fora da fila) não conhece o agendamento, e sem ele o
    /// consultório cobraria para sempre um registro que já foi escrito.
    /// </summary>
    private static SessaoDoDia Montar(Agendamento a, IReadOnlyList<Evolucao> evolucoes)
    {
        var evolucao = evolucoes.FirstOrDefault(e => e.AgendamentoId == a.Id)
                       ?? evolucoes.FirstOrDefault(e =>
                            e.AgendamentoId is null
                            && e.PacienteId == a.PacienteId
                            && e.Data == DateOnly.FromDateTime(a.DataHora));

        return new SessaoDoDia(
            a.Id,
            a.DataHora,
            a.PacienteId,
            a.Paciente?.Nome ?? "Paciente",
            CatalogoModalidades.Nome(a.ModalidadeCodigo),
            a.Sala?.Nome,
            a.Status,
            a.Etapa,
            a.Encaixe,
            a.AtendimentoId,
            evolucao?.Id)
        {
            EsperaMinutos = a.EsperaMinutos(DateTime.Now),
            ChamadoHaMinutos = a.ChamadoHaMinutos(DateTime.Now)
        };
    }
}
