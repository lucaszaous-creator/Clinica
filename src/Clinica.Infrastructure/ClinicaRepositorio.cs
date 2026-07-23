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
            .Where(c => c.DataBaixa == null && c.Status != StatusCodigo.NaoAplicavel
                        && c.Status != StatusCodigo.NaoConformidade)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CodigoFaturamento>> CodigosEmNaoConformidadeAsync(CancellationToken ct = default)
        => await _db.Codigos
            .Include(c => c.Atendimento!).ThenInclude(a => a.Paciente!)
            .Where(c => c.Status == StatusCodigo.NaoConformidade)
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

        if (!string.IsNullOrWhiteSpace(filtro.TermoObservacao))
        {
            var obs = filtro.TermoObservacao.ToLower();
            q = q.Where(c => c.ObservacaoPendencia != null && c.ObservacaoPendencia.ToLower().Contains(obs));
        }

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

    // ---- Lotes TISS ----

    public async Task AdicionarLoteAsync(LoteTiss lote, CancellationToken ct = default)
        => await _db.LotesTiss.AddAsync(lote, ct);

    public async Task<IReadOnlyList<LoteTiss>> LotesTissAsync(CancellationToken ct = default)
        => await _db.LotesTiss
            .Include(l => l.Codigos).ThenInclude(c => c.Atendimento!).ThenInclude(a => a.Paciente!)
            .OrderByDescending(l => l.Numero)
            .ToListAsync(ct);

    public Task<LoteTiss?> ObterLoteTissAsync(int loteId, CancellationToken ct = default)
        => _db.LotesTiss
            .Include(l => l.Codigos).ThenInclude(c => c.Atendimento!).ThenInclude(a => a.Paciente!)
            .FirstOrDefaultAsync(l => l.Id == loteId, ct);

    public async Task<IReadOnlyList<CodigoFaturamento>> CodigosBaixadosSemLoteAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => await _db.Codigos
            .Include(c => c.Atendimento!).ThenInclude(a => a.Paciente!)
            .Where(c => c.DataBaixa != null && c.LoteTissId == null
                        && c.Atendimento!.Data >= inicio && c.Atendimento!.Data <= fim)
            .OrderBy(c => c.Atendimento!.Data)
            .ToListAsync(ct);

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

    // ---- Parâmetros ----

    public async Task<IReadOnlyList<ParametroConvenio>> ParametrosAsync(CancellationToken ct = default)
        => await _db.Parametros.ToListAsync(ct);

    public async Task SalvarParametroAsync(ParametroConvenio parametro, CancellationToken ct = default)
    {
        var existe = await _db.Parametros.FirstOrDefaultAsync(p => p.Convenio == parametro.Convenio, ct);
        if (existe is null)
            await _db.Parametros.AddAsync(parametro, ct);
        else
        {
            existe.Nome = parametro.Nome;
            existe.Ativo = parametro.Ativo;
            existe.ValidadeConsultaDias = parametro.ValidadeConsultaDias;
            existe.DiasSegundoCodigo = parametro.DiasSegundoCodigo;
            existe.CategoriaComApp = parametro.CategoriaComApp;
            existe.CategoriaSemApp = parametro.CategoriaSemApp;
        }
    }

    public async Task<IReadOnlyList<ConvenioCadastro>> ConveniosAsync(CancellationToken ct = default)
        => await _db.Convenios.AsNoTracking().ToListAsync(ct);

    public async Task SalvarConvenioAsync(ConvenioCadastro convenio, CancellationToken ct = default)
    {
        var existe = await _db.Convenios.FirstOrDefaultAsync(c => c.Codigo == convenio.Codigo, ct);
        if (existe is null)
            await _db.Convenios.AddAsync(convenio, ct);
        else
        {
            existe.Nome = convenio.Nome;
            existe.Familia = convenio.Familia;
            existe.Ativo = convenio.Ativo;
            existe.FazEletro = convenio.FazEletro;
            existe.TemSegundoCodigo = convenio.TemSegundoCodigo;
            existe.FormaSegundoCodigo = convenio.FormaSegundoCodigo;
            existe.SegundoCodigoDependeApp = convenio.SegundoCodigoDependeApp;
            existe.DiasSegundoCodigo = convenio.DiasSegundoCodigo;
            existe.FaturaBsv = convenio.FaturaBsv;
            existe.InverteDatasBsv = convenio.InverteDatasBsv;
            existe.ValidadeConsultaDias = convenio.ValidadeConsultaDias;
            existe.CategoriaComApp = convenio.CategoriaComApp;
            existe.CategoriaSemApp = convenio.CategoriaSemApp;
        }
    }

    public async Task ExcluirConvenioAsync(string codigo, CancellationToken ct = default)
    {
        var existe = await _db.Convenios.FirstOrDefaultAsync(c => c.Codigo == codigo, ct);
        if (existe is not null)
            _db.Convenios.Remove(existe);
    }

    public async Task<bool> ConvenioEmUsoAsync(string codigo, CancellationToken ct = default)
        => await _db.Pacientes.AnyAsync(p => p.ConvenioCodigo == codigo, ct);

    public async Task<IReadOnlyList<ModalidadeCadastro>> ModalidadesAsync(CancellationToken ct = default)
        => await _db.Modalidades.AsNoTracking().ToListAsync(ct);

    public async Task SalvarModalidadeAsync(ModalidadeCadastro modalidade, CancellationToken ct = default)
    {
        var existe = await _db.Modalidades.FirstOrDefaultAsync(m => m.Codigo == modalidade.Codigo, ct);
        if (existe is null)
            await _db.Modalidades.AddAsync(modalidade, ct);
        else
        {
            existe.Nome = modalidade.Nome;
            existe.Base = modalidade.Base;
            existe.Ativo = modalidade.Ativo;
        }
    }

    public async Task ExcluirModalidadeAsync(string codigo, CancellationToken ct = default)
    {
        var existe = await _db.Modalidades.FirstOrDefaultAsync(m => m.Codigo == codigo, ct);
        if (existe is not null)
            _db.Modalidades.Remove(existe);
    }

    public async Task<bool> ModalidadeEmUsoAsync(string codigo, CancellationToken ct = default)
        => await _db.Pacientes.AnyAsync(p => p.ModalidadePreferidaCodigo == codigo, ct)
           || await _db.Atendimentos.AnyAsync(a => a.ModalidadeCodigo == codigo, ct)
           || await _db.Agendamentos.AnyAsync(a => a.ModalidadeCodigo == codigo, ct);

    public async Task<IReadOnlyList<EspecialidadeCadastro>> EspecialidadesAsync(CancellationToken ct = default)
        => await _db.Especialidades.AsNoTracking().ToListAsync(ct);

    public async Task SalvarEspecialidadeAsync(EspecialidadeCadastro especialidade, CancellationToken ct = default)
    {
        var existe = await _db.Especialidades.FirstOrDefaultAsync(e => e.Codigo == especialidade.Codigo, ct);
        if (existe is null)
            await _db.Especialidades.AddAsync(especialidade, ct);
        else
        {
            existe.Nome = especialidade.Nome;
            existe.Ativo = especialidade.Ativo;
        }
    }

    public async Task ExcluirEspecialidadeAsync(string codigo, CancellationToken ct = default)
    {
        var existe = await _db.Especialidades.FirstOrDefaultAsync(e => e.Codigo == codigo, ct);
        if (existe is not null)
            _db.Especialidades.Remove(existe);
    }

    public async Task<bool> EspecialidadeEmUsoAsync(string codigo, CancellationToken ct = default)
        => await _db.Atendimentos.AnyAsync(a => a.EspecialidadeConsultaCodigo == codigo, ct)
           || await _db.Codigos.AnyAsync(c => c.EspecialidadeCodigo == codigo, ct)
           || await _db.Agendamentos.AnyAsync(a => a.EspecialidadeConsultaCodigo == codigo, ct);

    public async Task<string?> ObterConfiguracaoAsync(string chave, CancellationToken ct = default)
        => (await _db.Configuracoes.AsNoTracking().FirstOrDefaultAsync(c => c.Chave == chave, ct))?.Valor;

    public async Task SalvarConfiguracaoAsync(string chave, string valor, CancellationToken ct = default)
    {
        var existe = await _db.Configuracoes.FirstOrDefaultAsync(c => c.Chave == chave, ct);
        if (existe is null)
            await _db.Configuracoes.AddAsync(new ConfiguracaoGlobal { Chave = chave, Valor = valor }, ct);
        else
            existe.Valor = valor;
    }

    // ---- Consultas ----

    public async Task AdicionarConsultaAsync(Consulta consulta, CancellationToken ct = default)
        => await _db.Consultas.AddAsync(consulta, ct);

    public async Task<IReadOnlyList<Consulta>> ConsultasDoPacienteAsync(int pacienteId, CancellationToken ct = default)
        => await _db.Consultas
            .Where(c => c.PacienteId == pacienteId)
            .OrderByDescending(c => c.DataEmissao)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Paciente>> PacientesComConsultasAsync(CancellationToken ct = default)
        => await _db.Pacientes.Include(p => p.Consultas).OrderBy(p => p.Nome).ToListAsync(ct);

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

    // ---- Auditoria ----

    public async Task RegistrarAuditoriaAsync(EventoAuditoria evento, CancellationToken ct = default)
        => await _db.Auditoria.AddAsync(evento, ct);

    public async Task<IReadOnlyList<EventoAuditoria>> EventosAuditoriaAsync(int limite = 200, CancellationToken ct = default)
        => await _db.Auditoria.AsNoTracking()
            .OrderByDescending(e => e.DataHora).ThenByDescending(e => e.Id)
            .Take(limite)
            .ToListAsync(ct);

    public async Task<int> SalvarAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Duas máquinas editaram o mesmo registro ao mesmo tempo (token xmin do Postgres).
            // Sem isto, a última gravação sobrescreveria a outra em silêncio.
            throw new InvalidOperationException(
                "Outro computador alterou este registro enquanto você editava. " +
                "Atualize a tela (F5) para ver a versão mais recente e repita a operação.", ex);
        }
    }
}
