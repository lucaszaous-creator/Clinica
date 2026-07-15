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

    public async Task<IReadOnlyList<Paciente>> PacientesComAtendimentosAsync(CancellationToken ct = default)
        => await _db.Pacientes.Include(p => p.Atendimentos).ToListAsync(ct);

    public Task<CodigoFaturamento?> ObterCodigoAsync(int codigoId, CancellationToken ct = default)
        => _db.Codigos.FirstOrDefaultAsync(c => c.Id == codigoId, ct);

    public async Task AdicionarAtendimentoAsync(Atendimento atendimento, CancellationToken ct = default)
        => await _db.Atendimentos.AddAsync(atendimento, ct);

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

    public Task<int> SalvarAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
