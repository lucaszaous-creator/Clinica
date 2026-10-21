using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Planejamento;

/// <summary>Ocupação do tempo dos profissionais, sem pressupor capacidade de salas ou vagas.</summary>
public sealed class OcupacaoAgendaService(ClinicaDbContext db) : IOcupacaoAgendaService
{
    public Task<OcupacaoAgenda> ConsultarMovimentoAsync(int usuarioId, DateOnly inicio, DateOnly fim,
        int? profissionalId = null, CancellationToken ct = default)
        => ConsultarInternoAsync(usuarioId, inicio, fim, profissionalId, false, ct);

    public Task<OcupacaoAgenda> ConsultarAsync(int usuarioId, DateOnly inicio, DateOnly fim,
        int? profissionalId = null, CancellationToken ct = default)
        => ConsultarInternoAsync(usuarioId, inicio, fim, profissionalId, true, ct);

    private async Task<OcupacaoAgenda> ConsultarInternoAsync(int usuarioId, DateOnly inicio, DateOnly fim,
        int? profissionalId, bool calcularJornadas, CancellationToken ct)
    {
        var u = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == usuarioId, ct);
        if (u is null || !u.Ativo || !u.Pode(Permissao.VerAgenda) || u.Travado(DateTime.Now))
            throw new UnauthorizedAccessException("Seu acesso não permite consultar a agenda.");
        if (fim < inicio || fim.DayNumber - inicio.DayNumber > 41)
            throw new InvalidOperationException("Consulte um período de até seis semanas.");
        var de = inicio.ToDateTime(TimeOnly.MinValue); var ate = fim.AddDays(1).ToDateTime(TimeOnly.MinValue);
        // Projeção limitada ao calendário. Não carrega prontuário nem listas por paciente.
        var linhas = await ConsultaHorarios(de, ate, profissionalId)
            .OrderBy(a => a.DataHora).ThenBy(a => a.Id)
            .Select(a => new { a.Id, a.DataHora, a.ProfissionalId, a.Status,
                a.ChegadaEm, a.ChamadoEm, a.InicioAtendimentoEm, a.FimAtendimentoEm,
                Duracao = a.DuracaoMinutos ?? (a.Profissional != null ? a.Profissional.DuracaoPadraoMinutos : null) ?? 30,
                Profissional = a.Profissional != null ? a.Profissional.Nome : "Sem profissional",
                Paciente = a.Paciente!.Nome, a.ModalidadePrevista }).ToListAsync(ct);
        var ids = linhas.Where(a => a.ProfissionalId != null).Select(a => a.ProfissionalId!.Value).Distinct().ToArray();
        var profissionais = await db.Profissionais.AsNoTracking()
            .Where(p => (p.Ativo || ids.Contains(p.Id)) && (profissionalId == null || p.Id == profissionalId))
            .OrderBy(p => p.Nome).Select(p => new Profissional { Id = p.Id, Nome = p.Nome,
                Ativo = p.Ativo, DiasDeAtendimento = p.DiasDeAtendimento, AtendeDas = p.AtendeDas, AtendeAte = p.AtendeAte,
                HorariosAtendimento = p.HorariosAtendimento }).ToListAsync(ct);
        if (profissionalId != null && profissionais.Count == 0)
            throw new InvalidOperationException("O profissional não está disponível neste período.");
        var bloqueios = calcularJornadas ? await db.BloqueiosAgenda.AsNoTracking().Where(b => b.Inicio < ate && b.Fim > de)
            .Select(b => new BloqueioAgenda { ProfissionalId = b.ProfissionalId, SalaId = b.SalaId, Inicio = b.Inicio, Fim = b.Fim, Motivo = b.Motivo }).ToListAsync(ct) : [];
        var horarios = linhas.Select(a =>
        {
            // A etapa sai da MESMA derivação da entidade (Agendamento.Etapa): duas contas de
            // "quem já chegou" divergiriam entre a Visão geral e a Agenda do dia.
            var etapa = new Agendamento { Status = a.Status, ChegadaEm = a.ChegadaEm, ChamadoEm = a.ChamadoEm,
                InicioAtendimentoEm = a.InicioAtendimentoEm, FimAtendimentoEm = a.FimAtendimentoEm }.Etapa;
            return new HorarioOcupacao(a.Id, a.DataHora, a.DataHora.AddMinutes(a.Duracao),
                a.ProfissionalId, a.Profissional, a.Paciente, RotulosEnum.De(a.ModalidadePrevista), RotulosEnum.De(a.Status),
                a.Status is StatusAgendamento.Agendado or StatusAgendamento.Realizado)
            {
                Familia = a.ModalidadePrevista, Etapa = etapa, ChegadaEm = a.ChegadaEm,
                SituacaoDoDia = StatusDaFila.Palavra(a.Status, etapa)
            };
        }).ToList();
        MedidaOcupacao Medir(DateOnly primeiro, DateOnly ultimo, IReadOnlyList<Profissional> equipe, IReadOnlyList<HorarioOcupacao> agenda)
        {
            var ocupados = 0; var capacidade = 0; var bloqueados = 0; var fora = new HashSet<int>();
            var relevantes = agenda.Where(a => a.Inicio < ultimo.AddDays(1).ToDateTime(TimeOnly.MinValue)
                && a.Fim > primeiro.ToDateTime(TimeOnly.MinValue)).ToList();
            if (!calcularJornadas)
                return new(relevantes.Count(a => a.Ocupa), relevantes.Count(a => a.Situacao == RotulosEnum.De(StatusAgendamento.Cancelado)),
                    relevantes.Count(a => a.Situacao == RotulosEnum.De(StatusAgendamento.Faltou)), 0, 0, 0, 0, 0);
            var sem = equipe.Count(p => !p.HorarioCompleto);
            if (relevantes.Any(a => a.Ocupa && a.ProfissionalId == null)) sem++;
            foreach (var p in equipe)
            {
                if (!p.HorarioCompleto) continue;
                var marcados = agenda.Where(a => a.Ocupa && a.ProfissionalId == p.Id).ToList();
                for (var d = primeiro; d <= ultimo; d = d.AddDays(1))
                {
                    var diaInicio = d.ToDateTime(TimeOnly.MinValue); var diaFim = diaInicio.AddDays(1);
                    var doDia = marcados.Where(a => a.Inicio < diaFim && a.Fim > diaInicio).ToList();
                    var faixas = p.FaixasEm(d.DayOfWeek) ?? [];
                    if (faixas.Count == 0) { foreach (var a in doDia) fora.Add(a.Id); continue; }
                    // Cada faixa do dia (manhã, tarde) mede-se sozinha: o almoço não é vago.
                    var todosFechados = new List<(DateTime Inicio, DateTime Fim)>();
                    foreach (var faixa in faixas)
                    {
                        var abre = d.ToDateTime(faixa.Inicio); var fecha = d.ToDateTime(faixa.Fim);
                        var fechados = Unir(bloqueios.Where(b => b.AlcancaRecurso(p.Id, null))
                            .Select(b => Cortar(b.Inicio, b.Fim, abre, fecha)));
                        var ocupacao = Unir(doDia.Select(a => Cortar(a.Inicio, a.Fim, abre, fecha)));
                        var bloqueio = Minutos(fechados);
                        var intersecao = Minutos(Unir(ocupacao.SelectMany(a => fechados.Select(b => Cortar(a.Inicio, a.Fim, b.Inicio, b.Fim)))));
                        bloqueados += bloqueio; capacidade += (int)(fecha - abre).TotalMinutes - bloqueio;
                        ocupados += Minutos(ocupacao) - intersecao;
                        todosFechados.AddRange(fechados);
                    }
                    foreach (var a in doDia.Where(a => !faixas.Any(f => a.Inicio >= d.ToDateTime(f.Inicio) && a.Fim <= d.ToDateTime(f.Fim))
                        || todosFechados.Any(b => a.Inicio < b.Fim && a.Fim > b.Inicio))) fora.Add(a.Id);
                }
            }
            return new(relevantes.Count(a => a.Ocupa), relevantes.Count(a => a.Situacao == RotulosEnum.De(StatusAgendamento.Cancelado)),
                relevantes.Count(a => a.Situacao == RotulosEnum.De(StatusAgendamento.Faltou)), ocupados, capacidade, bloqueados, sem, fora.Count);
        }
        var dias = Enumerable.Range(0, fim.DayNumber - inicio.DayNumber + 1).Select(i => inicio.AddDays(i))
            .Select(d => new DiaOcupacao(d, Medir(d, d, profissionais, horarios))).ToArray();
        // Vãos e dias fechados (out/2026): os vãos só com UM profissional escolhido — "livre"
        // para a equipe inteira não diz com quem marcar.
        var filtrado = profissionalId is { } fid ? profissionais.FirstOrDefault(p => p.Id == fid) : null;
        var vagas = calcularJornadas && filtrado is not null
            ? Clinica.Application.Modelos.VaosDaAgenda.Calcular(filtrado, inicio, fim, horarios, bloqueios, DateTime.Now) : [];
        var fechados = calcularJornadas ? Clinica.Application.Modelos.VaosDaAgenda.Fechados(filtrado, inicio, fim, bloqueios) : [];
        return new(inicio, fim, DateTime.Now, Medir(inicio, fim, profissionais, horarios), dias,
            profissionais.Select(p =>
            {
                var dele = horarios.Where(a => a.ProfissionalId == p.Id).ToList();
                // Dia a dia (a matriz da semana e a linha do tempo do dia): só com horários calculados.
                var fechadosDele = calcularJornadas ? Clinica.Application.Modelos.VaosDaAgenda.Fechados(p, inicio, fim, bloqueios) : [];
                var porDia = calcularJornadas
                    ? Enumerable.Range(0, fim.DayNumber - inicio.DayNumber + 1).Select(i => inicio.AddDays(i))
                        .Select(d => new DiaDoProfissionalOcupacao(d, Medir(d, d, [p], dele), p.FaixasEm(d.DayOfWeek),
                            fechadosDele.FirstOrDefault(f => f.Dia == d)?.Motivo)).ToArray()
                    : [];
                return new ProfissionalOcupacao(p.Id, p.Nome,
                    !calcularJornadas ? "" : !p.HorarioCompleto ? "Horário de atendimento não cadastrado" : p.DescricaoJornada ?? "",
                    Medir(inicio, fim, [p], dele)) { Dias = porDia };
            }).ToArray(), horarios)
        {
            Vagas = vagas, Fechados = fechados,
            SemJornadaParaVagas = calcularJornadas && filtrado is not null && !filtrado.HorarioCompleto
        };
    }

    internal IQueryable<Agendamento> ConsultaHorarios(DateTime de, DateTime ate, int? profissionalId)
        => db.Agendamentos.AsNoTracking().Where(a => a.DataHora < ate && a.DataHora.AddMinutes(a.DuracaoMinutos
            ?? (a.Profissional != null ? a.Profissional.DuracaoPadraoMinutos : null) ?? 30) > de
            && (profissionalId == null || a.ProfissionalId == profissionalId));

    private static (DateTime Inicio, DateTime Fim) Cortar(DateTime inicio, DateTime fim, DateTime de, DateTime ate)
        => (inicio > de ? inicio : de, fim < ate ? fim : ate);
    private static List<(DateTime Inicio, DateTime Fim)> Unir(IEnumerable<(DateTime Inicio, DateTime Fim)> intervalos)
    {
        var resultado = new List<(DateTime Inicio, DateTime Fim)>();
        foreach (var atual in intervalos.Where(i => i.Fim > i.Inicio).OrderBy(i => i.Inicio))
        {
            if (resultado.Count == 0 || resultado[^1].Fim < atual.Inicio) resultado.Add(atual);
            else if (atual.Fim > resultado[^1].Fim) resultado[^1] = (resultado[^1].Inicio, atual.Fim);
        }
        return resultado;
    }
    private static int Minutos(IEnumerable<(DateTime Inicio, DateTime Fim)> intervalos)
        => (int)intervalos.Sum(i => (i.Fim - i.Inicio).TotalMinutes);
}
