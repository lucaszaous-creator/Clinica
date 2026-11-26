using System.Data;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

/// <summary>
/// RESERVA DE HORÁRIO SEM FICHA (nov/2026, pedido do dono, caminho B). Ver <see cref="ReservaDeHorario"/> e
/// <see cref="ReservaDeHorarioRegras"/>.
///
/// - Reservar/remarcar: o horário é conferido e gravado na MESMA transação, com a tabela de agendamentos travada
///   (a mesma trava da reserva da ponte, <c>ClinicBookingBridge.Hold</c>): duas pessoas não seguram o mesmo horário,
///   e uma marcação comum não entra por cima no meio.
/// - Virar paciente: ficha (nova ou existente) + agendamento normal no mesmo horário + reserva "virou agendamento",
///   numa transação só — falhou qualquer parte (cobertura do convênio, trava de consulta, cadastro), nada fica.
///   Idempotente: repetir devolve o mesmo agendamento.
/// - Nada se apaga (decisão do dono): desmarcar e não comparecer mudam a situação; a linha fica.
/// - A trilha (Auditoria) registra quem criou, viu e alterou — sem o nome nem o telefone da pessoa.
/// </summary>
public sealed class ReservaDeHorarioService : IReservaDeHorarioService
{
    private readonly ClinicaDbContext db;
    private readonly ClinicaRepositorio repo;
    private readonly AgendaService agenda;
    private readonly PacienteService pacientes;
    private readonly Func<DateTime> agora;

    public ReservaDeHorarioService(ClinicaDbContext db, AgendaService agenda, PacienteService pacientes)
        : this(db, agenda, pacientes, () => DateTime.Now) { }

    public ReservaDeHorarioService(ClinicaDbContext db, AgendaService agenda, PacienteService pacientes, Func<DateTime> agora)
    {
        this.db = db;
        repo = new ClinicaRepositorio(db);
        this.agenda = agenda;
        this.pacientes = pacientes;
        this.agora = agora;
    }

    /// <summary>Para quem só tem o banco (a ponte do CRM): as mesmas peças do app.</summary>
    public static ReservaDeHorarioService Sobre(ClinicaDbContext db, Func<DateTime>? agora = null)
    {
        var repo = new ClinicaRepositorio(db);
        var parametros = new ParametrosService(repo);
        var atendimentos = new AtendimentoService(repo, parametros: parametros, consultas: new ConsultaService(repo));
        return new ReservaDeHorarioService(db, new AgendaService(repo, atendimentos, parametros), new PacienteService(repo),
            agora ?? (() => DateTime.Now));
    }

    // ================================================================ reservar

    public Task<ReservaDeHorario> ReservarAsync(NovaReserva nova, string operador, CancellationToken ct = default)
        => Transacao(async c =>
        {
            if (ReservaDeHorarioRegras.Recusa(nova, agora()) is { } recusa) throw new InvalidOperationException(recusa);
            var telefone = FichaPeloTelefone.Normalizar(nova.Telefone);
            var profissional = await ProfissionalQueAtende(nova.ProfissionalId, nova.ModalidadeCodigo, nova.EspecialidadeConsultaCodigo, c);
            var duracao = nova.DuracaoMinutos ?? profissional.DuracaoPadraoMinutos ?? Agendamento.DuracaoPadraoMinutos;

            var abertas = await db.ReservasDeHorario.CountAsync(r => r.Telefone == telefone
                && r.Situacao == SituacaoDaReserva.Reservada && r.DataHora > agora(), c);
            if (ReservaDeHorarioRegras.RecusaPeloLimite(abertas) is { } limite) throw new InvalidOperationException(limite);

            await GarantirLivreAsync(nova.DataHora, duracao, profissional.Id, nova.SalaId, null, c);

            var reserva = new ReservaDeHorario
            {
                Nome = ReservaDeHorarioRegras.NomeLimpo(nova.Nome), Telefone = telefone,
                ConversaId = string.IsNullOrWhiteSpace(nova.ConversaId) ? null : nova.ConversaId.Trim(),
                DataHora = nova.DataHora, DuracaoMinutos = duracao, ProfissionalId = profissional.Id, SalaId = nova.SalaId,
                ModalidadeCodigo = nova.ModalidadeCodigo.Trim(), EspecialidadeConsultaCodigo = nova.EspecialidadeConsultaCodigo,
                Particular = nova.Particular, ConvenioNome = nova.Particular ? null : nova.ConvenioNome!.Trim(),
                CriadaEm = agora(), CriadaPor = Operador(operador), Profissional = profissional
            };
            await repo.AdicionarReservaAsync(reserva, c);
            await db.SaveChangesAsync(c);
            Trilha("ReservaSemFichaCriada", reserva, "criada", operador);
            await db.SaveChangesAsync(c);
            return reserva;
        }, ct);

    // ================================================================ remarcar, desmarcar, não compareceu

    public Task<ReservaDeHorario> RemarcarAsync(int reservaId, DateTime novoInicio, string operador, CancellationToken ct = default)
        => Transacao(async c =>
        {
            var r = await Aberta(reservaId, c);
            if (r.DataHora == novoInicio) return r; // repetir o mesmo pedido não faz nada (idempotente)
            if (novoInicio <= agora()) throw new InvalidOperationException("Escolha um horário que ainda não passou.");
            await GarantirLivreAsync(novoInicio, r.DuracaoMinutos, r.ProfissionalId, r.SalaId, r.Id, c);
            var antes = r.DataHora;
            r.DataHora = novoInicio;
            Alterar(r, operador);
            Trilha("ReservaSemFichaRemarcada", r, $"remarcada de {antes:dd/MM/yyyy HH:mm}", operador);
            await db.SaveChangesAsync(c);
            return r;
        }, ct);

    public Task<ReservaDeHorario> DesmarcarAsync(int reservaId, string motivo, string operador, CancellationToken ct = default)
        => Transacao(async c =>
        {
            if (!ReservaDeHorarioRegras.MotivoValido(motivo))
                throw new InvalidOperationException("Escolha o motivo: " + string.Join(", ", ReservaDeHorario.MotivosDeDesmarcar) + ".");
            var r = await Carregar(reservaId, c);
            if (r.Situacao == SituacaoDaReserva.Desmarcada) return r; // idempotente
            if (r.Situacao != SituacaoDaReserva.Reservada) throw new InvalidOperationException(JaNaoEstaReservada(r));
            r.Situacao = SituacaoDaReserva.Desmarcada;
            r.MotivoDesmarcacao = motivo;
            Alterar(r, operador);
            Trilha("ReservaSemFichaDesmarcada", r, $"desmarcada ({motivo})", operador);
            await db.SaveChangesAsync(c);
            return r;
        }, ct);

    public Task<ReservaDeHorario> NaoCompareceuAsync(int reservaId, string operador, CancellationToken ct = default)
        => Transacao(async c =>
        {
            var r = await Carregar(reservaId, c);
            if (r.Situacao == SituacaoDaReserva.NaoCompareceu) return r;
            if (r.Situacao != SituacaoDaReserva.Reservada) throw new InvalidOperationException(JaNaoEstaReservada(r));
            if (r.DataHora > agora()) throw new InvalidOperationException("Só dá para marcar a falta depois do horário.");
            r.Situacao = SituacaoDaReserva.NaoCompareceu;
            Alterar(r, operador);
            Trilha("ReservaSemFichaNaoCompareceu", r, "não compareceu", operador);
            await db.SaveChangesAsync(c);
            return r;
        }, ct);

    // ================================================================ virar paciente

    public Task<Agendamento> VirarPacienteAsync(int reservaId, ComoVirarPaciente como, string operador, CancellationToken ct = default)
        => Transacao(async c =>
        {
            var r = await Carregar(reservaId, c);
            if (r.Situacao == SituacaoDaReserva.VirouAgendamento && r.AgendamentoId is { } feito)
                return await db.Agendamentos.SingleAsync(a => a.Id == feito, c); // idempotente: o mesmo agendamento
            if (r.Situacao != SituacaoDaReserva.Reservada) throw new InvalidOperationException(JaNaoEstaReservada(r));
            if ((como.PacienteExistenteId is null) == (como.Novo is null))
                throw new InvalidOperationException("Escolha a ficha que já existe ou preencha o cadastro novo.");

            Paciente paciente;
            if (como.PacienteExistenteId is { } existente)
                paciente = await repo.ObterPacienteAsync(existente, c) ?? throw new InvalidOperationException("Ficha não encontrada.");
            else
            {
                var novo = como.Novo!;
                // Nome e telefone vêm da reserva (a recepção corrige se quiser); o resto, a pessoa informa no balcão.
                if (string.IsNullOrWhiteSpace(novo.Nome)) novo.Nome = r.Nome;
                if (string.IsNullOrWhiteSpace(novo.Telefone)) novo.Telefone = r.Telefone;
                paciente = await pacientes.SalvarNovoAsync(novo, ct: c); // a MESMA validação do cadastro
            }

            var ag = await agenda.AgendarAsync(paciente.Id, r.DataHora, CatalogoModalidades.Base(r.ModalidadeCodigo), null,
                OrigemAgendamento.Manual, c, modalidadeCodigo: r.ModalidadeCodigo,
                especialidadeConsultaCodigo: r.EspecialidadeConsultaCodigo, profissionalId: r.ProfissionalId, salaId: r.SalaId,
                duracaoMinutos: r.DuracaoMinutos, operador: Operador(operador), particular: r.Particular, ignorarReservaId: r.Id);

            r.Situacao = SituacaoDaReserva.VirouAgendamento;
            r.AgendamentoId = ag.Id;
            r.PacienteId = paciente.Id;
            Alterar(r, operador);
            db.Auditoria.Add(new EventoAuditoria
            {
                DataHora = agora(), Operador = Operador(operador), Acao = "ReservaSemFichaVirouPaciente", PacienteId = paciente.Id,
                Detalhe = ReservaDeHorarioRegras.Trilha(r, $"virou o agendamento {ag.Id} da ficha {paciente.Id}")
            });
            await db.SaveChangesAsync(c);
            return ag;
        }, ct);

    // ================================================================ leituras

    public Task<IReadOnlyList<ReservaDeHorario>> DoDiaAsync(DateOnly dia, CancellationToken ct = default)
        => repo.ReservasNoPeriodoAsync(dia.ToDateTime(TimeOnly.MinValue), dia.AddDays(1).ToDateTime(TimeOnly.MinValue), ct);

    /// <summary>As reservas do telefone da conversa. Ver também fica na trilha (só quando há o que ver).</summary>
    public async Task<IReadOnlyList<ReservaDeHorario>> DoTelefoneAsync(string telefone, string operador, CancellationToken ct = default)
    {
        var numero = FichaPeloTelefone.Normalizar(telefone);
        if (numero.Length == 0) return [];
        var lista = await repo.ReservasDoTelefoneAsync(numero, ct);
        if (lista.Count > 0)
        {
            db.Auditoria.Add(new EventoAuditoria
            {
                DataHora = agora(), Operador = Operador(operador), Acao = "ReservaSemFichaConsultada",
                Detalhe = $"Consultou {lista.Count} reserva(s) sem ficha: nº {string.Join(", ", lista.Select(r => r.Id))}."
            });
            await db.SaveChangesAsync(ct);
        }
        return lista;
    }

    // ================================================================ peças

    private Task<T> Transacao<T>(Func<CancellationToken, Task<T>> unidade, CancellationToken ct)
        => TransacaoResiliente.ExecutarAsync(db, async c =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(
                db.Database.IsNpgsql() ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, c);
            if (db.Database.IsNpgsql())
            {
                // A mesma trava da reserva da ponte: quem grava horário espera quem confere.
                await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Agendamentos\" IN SHARE ROW EXCLUSIVE MODE", c);
                await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"ReservasDeHorario\" IN SHARE ROW EXCLUSIVE MODE", c);
            }
            var resultado = await unidade(c);
            await tx.CommitAsync(c);
            return resultado;
        }, ct);

    /// <summary>Recusa se o horário não está livre para uma reserva (ver <see cref="ReservaDeHorarioRegras.RecusaReserva"/>).</summary>
    private async Task GarantirLivreAsync(DateTime inicio, int duracao, int profissionalId, int? salaId, int? ignorarReserva, CancellationToken ct)
    {
        var choques = (await agenda.ConflitosAsync(inicio, duracao, profissionalId, salaId, null, null, ct, ignorarReserva))
            .Where(ReservaDeHorarioRegras.RecusaReserva).Select(c => c.Descricao).Distinct().ToArray();
        if (choques.Length > 0)
            throw new InvalidOperationException("Este horário não está livre. " + string.Join(" ", choques));
    }

    private async Task<Profissional> ProfissionalQueAtende(int id, string modalidade, string? especialidade, CancellationToken ct)
    {
        var p = await db.Profissionais.SingleOrDefaultAsync(x => x.Id == id && x.Ativo, ct)
            ?? throw new InvalidOperationException("Profissional inativo ou inexistente.");
        if (!p.Atende(modalidade, especialidade))
            throw new InvalidOperationException($"{p.Rotulo} não faz este atendimento. Escolha outro profissional.");
        return p;
    }

    private async Task<ReservaDeHorario> Carregar(int id, CancellationToken ct)
        => await repo.ObterReservaAsync(id, ct) ?? throw new InvalidOperationException("Reserva não encontrada.");

    private async Task<ReservaDeHorario> Aberta(int id, CancellationToken ct)
    {
        var r = await Carregar(id, ct);
        if (r.Situacao != SituacaoDaReserva.Reservada) throw new InvalidOperationException(JaNaoEstaReservada(r));
        return r;
    }

    private static string JaNaoEstaReservada(ReservaDeHorario r) => r.Situacao switch
    {
        SituacaoDaReserva.Desmarcada => "Esta reserva já foi desmarcada.",
        SituacaoDaReserva.NaoCompareceu => "Esta reserva ficou como não compareceu.",
        SituacaoDaReserva.VirouAgendamento => "Esta reserva já virou agendamento.",
        _ => "A reserva mudou. Atualize a tela."
    };

    private void Alterar(ReservaDeHorario r, string operador)
    {
        r.AlteradaEm = agora();
        r.AlteradaPor = Operador(operador);
        r.Versao++;
    }

    private void Trilha(string acao, ReservaDeHorario r, string oQue, string operador)
        => db.Auditoria.Add(new EventoAuditoria
        {
            DataHora = agora(), Operador = Operador(operador), Acao = acao, Detalhe = ReservaDeHorarioRegras.Trilha(r, oQue)
        });

    private static string Operador(string operador)
    {
        var o = (operador ?? "").Trim();
        if (o.Length == 0) throw new InvalidOperationException("Quem está fazendo a reserva?");
        return o.Length > 80 ? o[..80] : o;
    }
}
