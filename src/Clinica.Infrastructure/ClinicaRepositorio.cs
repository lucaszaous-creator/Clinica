using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;
using Clinica.Domain;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

public sealed class ClinicaRepositorio : IClinicaRepositorio
{
    private readonly ClinicaDbContext _db;

    public ClinicaRepositorio(ClinicaDbContext db) => _db = db;

    public Task<Paciente?> ObterPacienteAsync(int pacienteId, CancellationToken ct = default)
        => _db.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct);

    public async Task<IReadOnlyList<CodigoFaturamento>> CodigosDoPacienteNoMesAsync(int pacienteId, int ano, int mes, CancellationToken ct = default)
    {
        var inicio = new DateOnly(ano, mes, 1);
        var fim = inicio.AddMonths(1);
        return await _db.Codigos
            .Where(c => c.Atendimento!.PacienteId == pacienteId
                        && c.DataPrevistaFaturamento >= inicio
                        && c.DataPrevistaFaturamento < fim)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CodigoFaturamento>> CodigosEmAbertoAsync(CancellationToken ct = default)
        => await _db.Codigos
            .Include(c => c.Atendimento!).ThenInclude(a => a.Paciente!)
            .Where(c => c.DataBaixa == null && c.Status != StatusCodigo.NaoAplicavel)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CodigoFaturamento>> CodigosNoPeriodoAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => await _db.Codigos
            .Include(c => c.Atendimento!).ThenInclude(a => a.Paciente!)
            .Where(c => c.Atendimento!.Data >= inicio && c.Atendimento!.Data <= fim
                        && c.Status != StatusCodigo.NaoAplicavel)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CodigoFaturamento>> CodigosGlosadosAsync(bool somenteEmAberto, CancellationToken ct = default)
    {
        var query = _db.Codigos
            .Include(c => c.Atendimento!).ThenInclude(a => a.Paciente!)
            .Where(c => c.Glosa != StatusGlosa.SemGlosa);
        if (somenteEmAberto)
            query = query.Where(c => c.Glosa == StatusGlosa.Glosada || c.Glosa == StatusGlosa.Reapresentada);
        return await query.OrderByDescending(c => c.DataGlosa).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CodigoFaturamento>> ConsultarCodigosAsync(Clinica.Application.Modelos.FiltroConsultaGuias filtro, CancellationToken ct = default)
    {
        var q = _db.Codigos
            .Include(c => c.Atendimento!).ThenInclude(a => a.Paciente!)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.TermoPaciente))
        {
            var nome = filtro.TermoPaciente.ToLower();
            var digitos = Cpf.Normalizar(filtro.TermoPaciente);
            q = q.Where(c => c.Atendimento!.Paciente!.Nome.ToLower().Contains(nome)
                             || (digitos.Length > 0 && c.Atendimento!.Paciente!.Documento != null
                                 && c.Atendimento!.Paciente!.Documento.Contains(digitos)));
        }

        if (!string.IsNullOrWhiteSpace(filtro.NumeroGuia))
            q = q.Where(c => c.NumeroGuiaReal != null && c.NumeroGuiaReal.Contains(filtro.NumeroGuia));

        if (filtro.Inicio is { } inicio)
            q = q.Where(c => c.Atendimento!.Data >= inicio);
        if (filtro.Fim is { } fim)
            q = q.Where(c => c.Atendimento!.Data <= fim);

        if (filtro.Convenio is { } conv)
            q = q.Where(c => c.Atendimento!.Paciente!.Convenio == conv);

        q = filtro.Status switch
        {
            Clinica.Application.Modelos.FiltroStatusGuia.Aberto =>
                q.Where(c => c.DataBaixa == null && c.Status != StatusCodigo.NaoAplicavel),
            Clinica.Application.Modelos.FiltroStatusGuia.Baixado =>
                q.Where(c => c.DataBaixa != null),
            Clinica.Application.Modelos.FiltroStatusGuia.Glosado =>
                q.Where(c => c.Glosa != StatusGlosa.SemGlosa),
            _ => q
        };

        return await q.OrderByDescending(c => c.Atendimento!.Data).Take(500).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Paciente>> PacientesComAtendimentosAsync(CancellationToken ct = default)
        => await _db.Pacientes.Include(p => p.Atendimentos).ToListAsync(ct);

    public Task<CodigoFaturamento?> ObterCodigoAsync(int codigoId, CancellationToken ct = default)
        => _db.Codigos.FirstOrDefaultAsync(c => c.Id == codigoId, ct);

    public async Task AdicionarAtendimentoAsync(Atendimento atendimento, CancellationToken ct = default)
        => await _db.Atendimentos.AddAsync(atendimento, ct);

    public Task<Atendimento?> ObterAtendimentoAsync(int atendimentoId, CancellationToken ct = default)
        => _db.Atendimentos
            .Include(a => a.Paciente)
            .Include(a => a.Codigos)
            .FirstOrDefaultAsync(a => a.Id == atendimentoId, ct);

    public async Task<IReadOnlyList<Paciente>> BuscarPacientesAsync(string? termo, CancellationToken ct = default)
    {
        var query = _db.Pacientes.AsQueryable();
        termo = Cpf.Normalizar(termo).Length > 0 ? termo!.Trim() : termo?.Trim();

        if (!string.IsNullOrWhiteSpace(termo))
        {
            var nome = termo.ToLower();
            var digitos = Cpf.Normalizar(termo);
            query = query.Where(p =>
                p.Nome.ToLower().Contains(nome)
                || (digitos.Length > 0 && p.Documento != null && p.Documento.Contains(digitos)));
        }

        return await query.OrderBy(p => p.Nome).ToListAsync(ct);
    }

    public Task<Paciente?> ObterPacienteComHistoricoAsync(int pacienteId, CancellationToken ct = default)
        => _db.Pacientes
            .Include(p => p.Atendimentos).ThenInclude(a => a.Codigos)
            .FirstOrDefaultAsync(p => p.Id == pacienteId, ct);

    public async Task<IReadOnlyList<CodigoFaturamento>> CodigosBaixadosNoPeriodoAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => await _db.Codigos
            .Include(c => c.Atendimento!).ThenInclude(a => a.Paciente!)
            .Where(c => c.DataBaixa != null && c.DataBaixa >= inicio && c.DataBaixa <= fim)
            .OrderByDescending(c => c.DataBaixa)
            .ToListAsync(ct);

    public async Task AdicionarPacienteAsync(Paciente paciente, CancellationToken ct = default)
        => await _db.Pacientes.AddAsync(paciente, ct);

    public async Task RemoverPacienteAsync(int pacienteId, CancellationToken ct = default)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct);
        if (paciente is not null)
            _db.Pacientes.Remove(paciente);
    }

    // ---- Agenda ----

    public async Task AdicionarAgendamentoAsync(Agendamento agendamento, CancellationToken ct = default)
        => await _db.Agendamentos.AddAsync(agendamento, ct);

    public Task<Agendamento?> ObterAgendamentoAsync(int agendamentoId, CancellationToken ct = default)
        => _db.Agendamentos.Include(a => a.Paciente).FirstOrDefaultAsync(a => a.Id == agendamentoId, ct);

    public async Task<IReadOnlyList<Agendamento>> AgendamentosNoPeriodoAsync(DateTime inicio, DateTime fim, CancellationToken ct = default)
        => await _db.Agendamentos
            .Include(a => a.Paciente)
            .Where(a => a.DataHora >= inicio && a.DataHora <= fim)
            .OrderBy(a => a.DataHora)
            .ToListAsync(ct);

    public async Task RemoverAgendamentoAsync(int agendamentoId, CancellationToken ct = default)
    {
        var ag = await _db.Agendamentos.FirstOrDefaultAsync(a => a.Id == agendamentoId, ct);
        if (ag is not null)
            _db.Agendamentos.Remove(ag);
    }

    public Task<int> SalvarAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
