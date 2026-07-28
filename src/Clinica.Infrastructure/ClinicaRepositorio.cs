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

    public async Task<IReadOnlyList<CodigoFaturamento>> CodigosEmNaoConformidadeDoPacienteAsync(int pacienteId, CancellationToken ct = default)
        => await _db.Codigos
            .Include(c => c.Atendimento!).ThenInclude(a => a.Paciente!)
            .Where(c => c.Status == StatusCodigo.NaoConformidade && c.Atendimento!.PacienteId == pacienteId)
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

    public async Task<IReadOnlyList<Paciente>> BuscarPacientesAsync(string? termo, int? limite = null, CancellationToken ct = default)
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

        query = query.OrderBy(p => p.Nome);
        // O corte vai para o SQL (LIMIT), não para um Take() depois de materializar a lista.
        if (limite is > 0) query = query.Take(limite.Value);

        return await query.ToListAsync(ct);
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

    // ---- Retrato do paciente ----

    public Task<PacienteFoto?> ObterFotoPacienteAsync(int pacienteId, CancellationToken ct = default)
        => _db.PacientesFotos.AsNoTracking().FirstOrDefaultAsync(f => f.PacienteId == pacienteId, ct);

    public async Task DefinirFotoPacienteAsync(int pacienteId, byte[] conteudo, byte[] miniatura, CancellationToken ct = default)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct)
            ?? throw new InvalidOperationException("Paciente não encontrado para gravar a foto.");

        // Hora de parede (sem fuso), como no restante do sistema.
        var agora = DateTime.Now;
        var foto = await _db.PacientesFotos.FirstOrDefaultAsync(f => f.PacienteId == pacienteId, ct);
        if (foto is null)
        {
            await _db.PacientesFotos.AddAsync(
                new PacienteFoto { PacienteId = pacienteId, Conteudo = conteudo, AtualizadaEm = agora }, ct);
        }
        else
        {
            foto.Conteudo = conteudo;
            foto.AtualizadaEm = agora;
        }

        paciente.FotoMiniatura = miniatura;
        paciente.FotoAtualizadaEm = agora;
    }

    // ---- Autorizações de sessões (cota do convênio) ----

    public async Task<IReadOnlyList<AutorizacaoSessoes>> AutorizacoesDoPacienteAsync(int pacienteId, CancellationToken ct = default)
        => await _db.Autorizacoes
            .Where(a => a.PacienteId == pacienteId)
            .OrderByDescending(a => a.DataEmissao)
            .ThenByDescending(a => a.Id)
            .ToListAsync(ct);

    public Task<AutorizacaoSessoes?> ObterAutorizacaoAsync(int autorizacaoId, CancellationToken ct = default)
        => _db.Autorizacoes.FirstOrDefaultAsync(a => a.Id == autorizacaoId, ct);

    public async Task AdicionarAutorizacaoAsync(AutorizacaoSessoes autorizacao, CancellationToken ct = default)
        => await _db.Autorizacoes.AddAsync(autorizacao, ct);

    public async Task RemoverAutorizacaoAsync(int autorizacaoId, CancellationToken ct = default)
    {
        var autorizacao = await _db.Autorizacoes.FirstOrDefaultAsync(a => a.Id == autorizacaoId, ct);
        if (autorizacao is not null)
            _db.Autorizacoes.Remove(autorizacao);
    }

    public Task<int> ContarAtendimentosDoPacienteAsync(int pacienteId, DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => _db.Atendimentos.CountAsync(a => a.PacienteId == pacienteId && a.Data >= inicio && a.Data <= fim, ct);

    public async Task RemoverFotoPacienteAsync(int pacienteId, CancellationToken ct = default)
    {
        var foto = await _db.PacientesFotos.FirstOrDefaultAsync(f => f.PacienteId == pacienteId, ct);
        if (foto is not null)
            _db.PacientesFotos.Remove(foto);

        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct);
        if (paciente is not null)
        {
            paciente.FotoMiniatura = null;
            paciente.FotoAtualizadaEm = null;
        }
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

    // Profissional e Sala vêm juntos: a grade da agenda os desenha em coluna, e a
    // duração efetiva do horário depende do padrão do profissional.
    public Task<Agendamento?> ObterAgendamentoAsync(int agendamentoId, CancellationToken ct = default)
        => _db.Agendamentos
            .Include(a => a.Paciente)
            .Include(a => a.Profissional)
            .Include(a => a.Sala)
            .FirstOrDefaultAsync(a => a.Id == agendamentoId, ct);

    public async Task<IReadOnlyList<Agendamento>> AgendamentosNoPeriodoAsync(DateTime inicio, DateTime fim, CancellationToken ct = default)
        => await _db.Agendamentos
            .Include(a => a.Paciente)
            .Include(a => a.Profissional)
            .Include(a => a.Sala)
            .Where(a => a.DataHora >= inicio && a.DataHora <= fim)
            .OrderBy(a => a.DataHora)
            .ToListAsync(ct);

    public async Task RemoverAgendamentoAsync(int agendamentoId, CancellationToken ct = default)
    {
        var ag = await _db.Agendamentos.FirstOrDefaultAsync(a => a.Id == agendamentoId, ct);
        if (ag is not null)
            _db.Agendamentos.Remove(ag);
    }

    // ---- Equipe: profissionais e salas ----

    public async Task<IReadOnlyList<Profissional>> ProfissionaisAsync(CancellationToken ct = default)
        => await _db.Profissionais.AsNoTracking()
            .OrderBy(p => p.Ordem).ThenBy(p => p.Nome)
            .ToListAsync(ct);

    public Task<Profissional?> ObterProfissionalAsync(int profissionalId, CancellationToken ct = default)
        => _db.Profissionais.FirstOrDefaultAsync(p => p.Id == profissionalId, ct);

    public async Task AdicionarProfissionalAsync(Profissional profissional, CancellationToken ct = default)
        => await _db.Profissionais.AddAsync(profissional, ct);

    public async Task<bool> ProfissionalEmUsoAsync(int profissionalId, CancellationToken ct = default)
        => await _db.Agendamentos.AnyAsync(a => a.ProfissionalId == profissionalId, ct)
           || await _db.ListaEspera.AnyAsync(l => l.ProfissionalId == profissionalId, ct);

    public async Task RemoverProfissionalAsync(int profissionalId, CancellationToken ct = default)
    {
        var p = await _db.Profissionais.FirstOrDefaultAsync(x => x.Id == profissionalId, ct);
        if (p is not null)
            _db.Profissionais.Remove(p);
    }

    public async Task<IReadOnlyList<Sala>> SalasAsync(CancellationToken ct = default)
        => await _db.Salas.AsNoTracking()
            .OrderBy(s => s.Ordem).ThenBy(s => s.Nome)
            .ToListAsync(ct);

    public Task<Sala?> ObterSalaAsync(int salaId, CancellationToken ct = default)
        => _db.Salas.FirstOrDefaultAsync(s => s.Id == salaId, ct);

    public async Task AdicionarSalaAsync(Sala sala, CancellationToken ct = default)
        => await _db.Salas.AddAsync(sala, ct);

    public async Task<bool> SalaEmUsoAsync(int salaId, CancellationToken ct = default)
        => await _db.Agendamentos.AnyAsync(a => a.SalaId == salaId, ct);

    public async Task RemoverSalaAsync(int salaId, CancellationToken ct = default)
    {
        var s = await _db.Salas.FirstOrDefaultAsync(x => x.Id == salaId, ct);
        if (s is not null)
            _db.Salas.Remove(s);
    }

    // ---- Lista de espera ----

    public async Task AdicionarListaEsperaAsync(ListaEspera pedido, CancellationToken ct = default)
        => await _db.ListaEspera.AddAsync(pedido, ct);

    public Task<ListaEspera?> ObterListaEsperaAsync(int pedidoId, CancellationToken ct = default)
        => _db.ListaEspera
            .Include(l => l.Paciente)
            .Include(l => l.Profissional)
            .FirstOrDefaultAsync(l => l.Id == pedidoId, ct);

    public async Task<IReadOnlyList<ListaEspera>> ListaEsperaAsync(
        bool somenteAguardando = true, CancellationToken ct = default)
    {
        var q = _db.ListaEspera
            .Include(l => l.Paciente)
            .Include(l => l.Profissional)
            .AsQueryable();

        if (somenteAguardando)
            q = q.Where(l => l.Status == StatusListaEspera.Aguardando);

        // Prioritário primeiro; dentro de cada grupo, quem pediu antes é chamado antes.
        return await q
            .OrderByDescending(l => l.Prioritario)
            .ThenBy(l => l.CriadoEm)
            .ThenBy(l => l.Id)
            .ToListAsync(ct);
    }

    // ---- Prontuário ----

    public async Task AdicionarEvolucaoAsync(Evolucao evolucao, CancellationToken ct = default)
        => await _db.Evolucoes.AddAsync(evolucao, ct);

    public Task<Evolucao?> ObterEvolucaoAsync(int evolucaoId, CancellationToken ct = default)
        => _db.Evolucoes
            .Include(e => e.Profissional)
            .FirstOrDefaultAsync(e => e.Id == evolucaoId, ct);

    // Sem Include dos anexos de propósito: os bytes só saem do banco quando alguém
    // pede um arquivo específico (ver ConteudoDoAnexoAsync).
    public async Task<IReadOnlyList<Evolucao>> EvolucoesDoPacienteAsync(
        int pacienteId, CancellationToken ct = default)
        => await _db.Evolucoes.AsNoTracking()
            .Include(e => e.Profissional)
            .Where(e => e.PacienteId == pacienteId)
            .OrderByDescending(e => e.Data).ThenByDescending(e => e.Id)
            .ToListAsync(ct);

    public async Task RemoverEvolucaoAsync(int evolucaoId, CancellationToken ct = default)
    {
        var evolucao = await _db.Evolucoes.FirstOrDefaultAsync(e => e.Id == evolucaoId, ct);
        if (evolucao is not null)
            _db.Evolucoes.Remove(evolucao);
    }

    public async Task<IReadOnlyList<Clinica.Application.Modelos.AnexoResumo>> AnexosDaEvolucaoAsync(
        int evolucaoId, CancellationToken ct = default)
        => await _db.AnexosProntuario.AsNoTracking()
            .Where(a => a.EvolucaoId == evolucaoId)
            .OrderBy(a => a.CriadoEm).ThenBy(a => a.Id)
            // A projeção é o ponto: o SELECT não inclui Conteudo.
            .Select(a => new Clinica.Application.Modelos.AnexoResumo(
                a.Id, a.EvolucaoId, a.NomeArquivo, a.Tipo, a.TipoConteudo,
                a.Tamanho, a.Descricao, a.CriadoEm))
            .ToListAsync(ct);

    public async Task<byte[]?> ConteudoDoAnexoAsync(int anexoId, CancellationToken ct = default)
        => await _db.AnexosProntuario.AsNoTracking()
            .Where(a => a.Id == anexoId)
            .Select(a => a.Conteudo)
            .FirstOrDefaultAsync(ct);

    public async Task AdicionarAnexoAsync(AnexoProntuario anexo, CancellationToken ct = default)
        => await _db.AnexosProntuario.AddAsync(anexo, ct);

    public async Task RemoverAnexoAsync(int anexoId, CancellationToken ct = default)
    {
        var anexo = await _db.AnexosProntuario.FirstOrDefaultAsync(a => a.Id == anexoId, ct);
        if (anexo is not null)
            _db.AnexosProntuario.Remove(anexo);
    }

    // ---- Consentimento LGPD ----

    public async Task<IReadOnlyList<ConsentimentoLgpd>> ConsentimentosDoPacienteAsync(
        int pacienteId, CancellationToken ct = default)
        => await _db.Consentimentos.AsNoTracking()
            .Where(c => c.PacienteId == pacienteId)
            .OrderByDescending(c => c.RegistradoEm).ThenByDescending(c => c.Id)
            .ToListAsync(ct);

    public Task<ConsentimentoLgpd?> ObterConsentimentoAsync(
        int consentimentoId, CancellationToken ct = default)
        => _db.Consentimentos.FirstOrDefaultAsync(c => c.Id == consentimentoId, ct);

    public async Task AdicionarConsentimentoAsync(
        ConsentimentoLgpd consentimento, CancellationToken ct = default)
        => await _db.Consentimentos.AddAsync(consentimento, ct);

    // ---- Ato clínico: mapa corporal e protocolos ----

    // Rastreado (sem AsNoTracking) de propósito: quem abre o mapa é quem vai regravá-lo.
    public Task<MapaCorporal?> ObterMapaDaEvolucaoAsync(int evolucaoId, CancellationToken ct = default)
        => _db.MapasCorporais
            .Include(m => m.Pontos)
            .FirstOrDefaultAsync(m => m.EvolucaoId == evolucaoId, ct);

    public async Task AdicionarMapaAsync(MapaCorporal mapa, CancellationToken ct = default)
        => await _db.MapasCorporais.AddAsync(mapa, ct);

    public async Task RemoverPontosDoMapaAsync(int mapaId, CancellationToken ct = default)
    {
        var pontos = await _db.PontosMapa.Where(p => p.MapaCorporalId == mapaId).ToListAsync(ct);
        _db.PontosMapa.RemoveRange(pontos);
    }

    public async Task<IReadOnlyList<ProtocoloCorporal>> ProtocolosCorporaisAsync(
        int? pacienteId, bool somenteAtivos = true, CancellationToken ct = default)
        => await _db.ProtocolosCorporais.AsNoTracking()
            .Include(p => p.Pontos)
            // Os da clínica valem para todo mundo; os do paciente, só para ele.
            .Where(p => p.PacienteId == null || p.PacienteId == pacienteId)
            .Where(p => !somenteAtivos || p.Ativo)
            .OrderBy(p => p.PacienteId == null ? 0 : 1).ThenBy(p => p.Nome)
            .ToListAsync(ct);

    public Task<ProtocoloCorporal?> ObterProtocoloCorporalAsync(
        int protocoloId, CancellationToken ct = default)
        => _db.ProtocolosCorporais
            .Include(p => p.Pontos)
            .FirstOrDefaultAsync(p => p.Id == protocoloId, ct);

    public async Task AdicionarProtocoloCorporalAsync(
        ProtocoloCorporal protocolo, CancellationToken ct = default)
        => await _db.ProtocolosCorporais.AddAsync(protocolo, ct);

    public async Task RemoverProtocoloCorporalAsync(int protocoloId, CancellationToken ct = default)
    {
        var protocolo = await _db.ProtocolosCorporais
            .FirstOrDefaultAsync(p => p.Id == protocoloId, ct);
        if (protocolo is not null)
            _db.ProtocolosCorporais.Remove(protocolo);
    }

    // ---- Documentos clínicos ----

    public async Task AdicionarDocumentoAsync(DocumentoClinico documento, CancellationToken ct = default)
        => await _db.DocumentosClinicos.AddAsync(documento, ct);

    public Task<DocumentoClinico?> ObterDocumentoAsync(int documentoId, CancellationToken ct = default)
        => _db.DocumentosClinicos
            .Include(d => d.Itens)
            .Include(d => d.Paciente)
            .Include(d => d.Profissional)
            .FirstOrDefaultAsync(d => d.Id == documentoId, ct);

    public Task<DocumentoClinico?> ObterDocumentoPorCodigoAsync(
        string codigo, CancellationToken ct = default)
        => _db.DocumentosClinicos.AsNoTracking()
            .Include(d => d.Itens)
            .Include(d => d.Paciente)
            .Include(d => d.Profissional)
            .FirstOrDefaultAsync(d => d.CodigoVerificacao == codigo, ct);

    // Sem os itens: a lista da ficha mostra número, tipo e data — o corpo só é lido
    // quando alguém abre ou reimprime um documento específico.
    public async Task<IReadOnlyList<DocumentoClinico>> DocumentosDoPacienteAsync(
        int pacienteId, CancellationToken ct = default)
        => await _db.DocumentosClinicos.AsNoTracking()
            .Include(d => d.Profissional)
            .Where(d => d.PacienteId == pacienteId)
            .OrderByDescending(d => d.Data).ThenByDescending(d => d.Id)
            .ToListAsync(ct);

    public async Task<int> ProximoNumeroDocumentoAsync(int ano, CancellationToken ct = default)
    {
        // Contar serve porque documento não se apaga: cancelar mantém a linha (e o número).
        var prefixo = ano.ToString() + "/";
        var emitidos = await _db.DocumentosClinicos.AsNoTracking()
            .CountAsync(d => d.Numero.StartsWith(prefixo), ct);
        return emitidos + 1;
    }

    public async Task<IReadOnlyList<ModeloDocumento>> ModelosDocumentoAsync(
        TipoDocumentoClinico? tipo = null, CancellationToken ct = default)
        => await _db.ModelosDocumento.AsNoTracking()
            .Include(m => m.Itens)
            .Where(m => tipo == null || m.Tipo == tipo)
            .OrderBy(m => m.Ordem).ThenBy(m => m.Nome)
            .ToListAsync(ct);

    public Task<ModeloDocumento?> ObterModeloDocumentoAsync(int modeloId, CancellationToken ct = default)
        => _db.ModelosDocumento
            .Include(m => m.Itens)
            .FirstOrDefaultAsync(m => m.Id == modeloId, ct);

    public Task<ModeloDocumento?> ObterModeloDocumentoPorNomeAsync(
        TipoDocumentoClinico tipo, string nome, CancellationToken ct = default)
        => _db.ModelosDocumento
            .Include(m => m.Itens)
            .FirstOrDefaultAsync(m => m.Tipo == tipo && m.Nome == nome, ct);

    public async Task AdicionarModeloDocumentoAsync(ModeloDocumento modelo, CancellationToken ct = default)
        => await _db.ModelosDocumento.AddAsync(modelo, ct);

    public async Task RemoverModeloDocumentoAsync(int modeloId, CancellationToken ct = default)
    {
        var modelo = await _db.ModelosDocumento.FirstOrDefaultAsync(m => m.Id == modeloId, ct);
        if (modelo is not null)
            _db.ModelosDocumento.Remove(modelo);
    }

    public async Task RemoverItensDoModeloAsync(int modeloId, CancellationToken ct = default)
    {
        var itens = await _db.ItensModelo.Where(i => i.ModeloDocumentoId == modeloId).ToListAsync(ct);
        _db.ItensModelo.RemoveRange(itens);
    }

    // ---- Pacotes, planos e vouchers ----

    public async Task<IReadOnlyList<PacoteCatalogo>> PacotesCatalogoAsync(
        bool somenteAtivos = true, CancellationToken ct = default)
        => await _db.PacotesCatalogo.AsNoTracking()
            .Where(p => !somenteAtivos || p.Ativo)
            .OrderBy(p => p.Ordem).ThenBy(p => p.Nome)
            .ToListAsync(ct);

    public Task<PacoteCatalogo?> ObterPacoteCatalogoAsync(int catalogoId, CancellationToken ct = default)
        => _db.PacotesCatalogo.FirstOrDefaultAsync(p => p.Id == catalogoId, ct);

    public async Task AdicionarPacoteCatalogoAsync(PacoteCatalogo pacote, CancellationToken ct = default)
        => await _db.PacotesCatalogo.AddAsync(pacote, ct);

    public async Task RemoverPacoteCatalogoAsync(int catalogoId, CancellationToken ct = default)
    {
        var pacote = await _db.PacotesCatalogo.FirstOrDefaultAsync(p => p.Id == catalogoId, ct);
        if (pacote is not null) _db.PacotesCatalogo.Remove(pacote);
    }

    // Com os consumos: o saldo é calculado a partir deles, e uma lista de pacotes sem
    // consumo mostraria todo mundo com o pacote cheio.
    public async Task<IReadOnlyList<PacotePaciente>> PacotesDoPacienteAsync(
        int pacienteId, CancellationToken ct = default)
        => await _db.PacotesPaciente.AsNoTracking()
            .Include(p => p.Consumos)
            .Where(p => p.PacienteId == pacienteId)
            .OrderByDescending(p => p.DataCompra).ThenByDescending(p => p.Id)
            .ToListAsync(ct);

    public Task<PacotePaciente?> ObterPacotePacienteAsync(int pacoteId, CancellationToken ct = default)
        => _db.PacotesPaciente
            .Include(p => p.Consumos)
            .Include(p => p.Paciente)
            .FirstOrDefaultAsync(p => p.Id == pacoteId, ct);

    public async Task AdicionarPacotePacienteAsync(PacotePaciente pacote, CancellationToken ct = default)
        => await _db.PacotesPaciente.AddAsync(pacote, ct);

    public async Task<IReadOnlyList<PacotePaciente>> PacotesVendidosAsync(CancellationToken ct = default)
        => await _db.PacotesPaciente.AsNoTracking()
            .Include(p => p.Consumos)
            .Include(p => p.Paciente)
            .OrderByDescending(p => p.DataCompra).ThenByDescending(p => p.Id)
            .ToListAsync(ct);

    public Task<bool> AtendimentoJaConsumiuPacoteAsync(int atendimentoId, CancellationToken ct = default)
        => _db.ConsumosPacote.AsNoTracking()
            .AnyAsync(c => c.AtendimentoId == atendimentoId && c.CanceladoEm == null, ct);

    public Task<ConsumoPacote?> ObterConsumoPacoteAsync(int consumoId, CancellationToken ct = default)
        => _db.ConsumosPacote.FirstOrDefaultAsync(c => c.Id == consumoId, ct);

    // ---- Repasse por profissional ----

    public async Task<IReadOnlyList<RegraRepasse>> RegrasRepasseAsync(
        int? profissionalId = null, CancellationToken ct = default)
        => await _db.RegrasRepasse.AsNoTracking()
            .Include(r => r.Profissional)
            .Where(r => profissionalId == null || r.ProfissionalId == profissionalId)
            .OrderBy(r => r.ProfissionalId).ThenByDescending(r => r.VigenteDe)
            .ToListAsync(ct);

    public Task<RegraRepasse?> ObterRegraRepasseAsync(int regraId, CancellationToken ct = default)
        => _db.RegrasRepasse.FirstOrDefaultAsync(r => r.Id == regraId, ct);

    public async Task AdicionarRegraRepasseAsync(RegraRepasse regra, CancellationToken ct = default)
        => await _db.RegrasRepasse.AddAsync(regra, ct);

    public async Task RemoverRegraRepasseAsync(int regraId, CancellationToken ct = default)
    {
        var regra = await _db.RegrasRepasse.FirstOrDefaultAsync(r => r.Id == regraId, ct);
        if (regra is not null) _db.RegrasRepasse.Remove(regra);
    }

    public async Task<IReadOnlyList<RepasseApurado>> RepassesApuradosAsync(
        int? profissionalId = null, CancellationToken ct = default)
        => await _db.RepassesApurados.AsNoTracking()
            .Include(r => r.Profissional)
            .Where(r => profissionalId == null || r.ProfissionalId == profissionalId)
            .OrderByDescending(r => r.Inicio).ThenByDescending(r => r.Id)
            .ToListAsync(ct);

    public Task<RepasseApurado?> ObterRepasseApuradoAsync(int repasseId, CancellationToken ct = default)
        => _db.RepassesApurados.FirstOrDefaultAsync(r => r.Id == repasseId, ct);

    public async Task AdicionarRepasseApuradoAsync(RepasseApurado repasse, CancellationToken ct = default)
        => await _db.RepassesApurados.AddAsync(repasse, ct);

    public async Task<IReadOnlyList<Agendamento>> AgendamentosComAtendimentoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        var de = inicio.ToDateTime(TimeOnly.MinValue);
        var ate = fim.ToDateTime(TimeOnly.MaxValue);

        return await _db.Agendamentos.AsNoTracking()
            .Include(a => a.Profissional)
            .Include(a => a.Paciente)
            .Where(a => a.DataHora >= de && a.DataHora <= ate)
            .Where(a => a.AtendimentoId != null && a.ProfissionalId != null)
            .OrderBy(a => a.DataHora)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LancamentoFinanceiro>> LancamentosDosAtendimentosAsync(
        IReadOnlyCollection<int> atendimentoIds, CancellationToken ct = default)
    {
        if (atendimentoIds.Count == 0) return [];

        return await _db.Lancamentos.AsNoTracking()
            .Where(l => l.AtendimentoId != null && atendimentoIds.Contains(l.AtendimentoId.Value))
            .Where(l => l.Status != StatusLancamento.Cancelado)
            .ToListAsync(ct);
    }

    // ---- Estoque ----

    public async Task<IReadOnlyList<ItemEstoque>> ItensEstoqueAsync(
        bool somenteAtivos = false, CancellationToken ct = default)
        => await _db.ItensEstoque.AsNoTracking()
            .Where(i => !somenteAtivos || i.Ativo)
            .OrderBy(i => i.Nome)
            .ToListAsync(ct);

    public Task<ItemEstoque?> ObterItemEstoqueAsync(int itemId, CancellationToken ct = default)
        => _db.ItensEstoque.FirstOrDefaultAsync(i => i.Id == itemId, ct);

    public async Task AdicionarItemEstoqueAsync(ItemEstoque item, CancellationToken ct = default)
        => await _db.ItensEstoque.AddAsync(item, ct);

    public async Task RemoverItemEstoqueAsync(int itemId, CancellationToken ct = default)
    {
        var item = await _db.ItensEstoque.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is not null) _db.ItensEstoque.Remove(item);
    }

    public async Task<IReadOnlyList<MovimentoEstoque>> MovimentosDoItemAsync(
        int itemId, CancellationToken ct = default)
        => await _db.MovimentosEstoque.AsNoTracking()
            .Where(m => m.ItemEstoqueId == itemId)
            .OrderByDescending(m => m.Data).ThenByDescending(m => m.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MovimentoEstoque>> MovimentosNoPeriodoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => await _db.MovimentosEstoque.AsNoTracking()
            .Include(m => m.Item)
            .Where(m => m.Data >= inicio && m.Data <= fim)
            .OrderByDescending(m => m.Data).ThenByDescending(m => m.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MovimentoEstoque>> MovimentosDoAtendimentoAsync(
        int atendimentoId, CancellationToken ct = default)
        => await _db.MovimentosEstoque.AsNoTracking()
            .Include(m => m.Item)
            .Where(m => m.AtendimentoId == atendimentoId)
            .OrderBy(m => m.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MovimentoEstoque>> UltimoConsumoDeSessaoAsync(
        CancellationToken ct = default)
    {
        // Duas consultas de propósito: a primeira acha SÓ o id da última sessão que
        // gastou insumo (uma coluna, uma linha), a segunda traz os movimentos dela.
        // Ordenar por data e trazer tudo para depois agrupar em memória arrastaria o
        // extrato inteiro pela rede — e o banco é remoto.
        var ultimo = await _db.MovimentosEstoque.AsNoTracking()
            .Where(m => m.AtendimentoId != null && m.Tipo == TipoMovimentoEstoque.Saida)
            .OrderByDescending(m => m.Data).ThenByDescending(m => m.Id)
            .Select(m => m.AtendimentoId)
            .FirstOrDefaultAsync(ct);

        return ultimo is null ? [] : await MovimentosDoAtendimentoAsync(ultimo.Value, ct);
    }

    public async Task AdicionarMovimentoEstoqueAsync(
        MovimentoEstoque movimento, CancellationToken ct = default)
        => await _db.MovimentosEstoque.AddAsync(movimento, ct);

    // A soma acontece no BANCO: puxar o extrato inteiro para somar em memória seria
    // arrastar todo o histórico de movimentos a cada abertura da tela.
    public async Task<IReadOnlyDictionary<int, decimal>> SaldosEstoqueAsync(CancellationToken ct = default)
    {
        // A PROJEÇÃO é o que importa aqui: só três colunas saem do banco (item, tipo e
        // quantidade) — nada de lote, observação ou vínculo com atendimento.
        //
        // A soma, porém, é feita em memória e não em SQL: o SQLite, onde a suíte de
        // testes roda, não sabe somar `decimal` (NotSupportedException na tradução), e a
        // saída seria somar em ponto flutuante — trocar exatidão de estoque por uma
        // linha de SQL. Movimento de insumo é volume baixo (alguns por dia); a projeção
        // já evita o que era caro, que era arrastar o extrato inteiro.
        var movimentos = await _db.MovimentosEstoque.AsNoTracking()
            .Select(m => new { m.ItemEstoqueId, m.Tipo, m.Quantidade })
            .ToListAsync(ct);

        return movimentos
            .GroupBy(m => m.ItemEstoqueId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(m => m.Tipo == TipoMovimentoEstoque.Entrada ? m.Quantidade : -m.Quantidade));
    }

    // ---- Recibo e orçamento ----

    public async Task AdicionarDocumentoFinanceiroAsync(
        DocumentoFinanceiro documento, CancellationToken ct = default)
        => await _db.DocumentosFinanceiros.AddAsync(documento, ct);

    public Task<DocumentoFinanceiro?> ObterDocumentoFinanceiroAsync(
        int documentoId, CancellationToken ct = default)
        => _db.DocumentosFinanceiros
            .Include(d => d.Itens)
            .Include(d => d.Paciente)
            .FirstOrDefaultAsync(d => d.Id == documentoId, ct);

    public async Task<IReadOnlyList<DocumentoFinanceiro>> DocumentosFinanceirosAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => await _db.DocumentosFinanceiros.AsNoTracking()
            .Include(d => d.Itens)
            .Include(d => d.Paciente)
            .Where(d => d.Data >= inicio && d.Data <= fim)
            .OrderByDescending(d => d.Data).ThenByDescending(d => d.Id)
            .ToListAsync(ct);

    public async Task<int> ProximoNumeroDocumentoFinanceiroAsync(
        int ano, TipoDocumentoFinanceiro tipo, CancellationToken ct = default)
    {
        // Contar serve porque documento não se apaga: cancelar mantém a linha (e o número).
        var prefixo = DocumentoFinanceiro.Prefixo(tipo) + " " + ano + "/";
        var emitidos = await _db.DocumentosFinanceiros.AsNoTracking()
            .CountAsync(d => d.Numero.StartsWith(prefixo), ct);
        return emitidos + 1;
    }

    public async Task<IReadOnlyList<int>> PacientesComConsentimentoVigenteAsync(
        FinalidadeConsentimento finalidade,
        IReadOnlyCollection<int> pacienteIds,
        CancellationToken ct = default)
    {
        if (pacienteIds.Count == 0) return [];

        // Só os registros DESTES pacientes e DESTA finalidade saem do banco; o "mais
        // recente vence" é resolvido aqui porque é a mesma regra do ConsentimentoService
        // (situação atual = último registro), e duplicá-la em SQL é convite a divergir.
        var registros = await _db.Consentimentos.AsNoTracking()
            .Where(c => c.Finalidade == finalidade && pacienteIds.Contains(c.PacienteId))
            .OrderByDescending(c => c.RegistradoEm).ThenByDescending(c => c.Id)
            .ToListAsync(ct);

        return registros
            .GroupBy(c => c.PacienteId)
            .Where(g => g.First().Vigente)
            .Select(g => g.Key)
            .ToList();
    }

    // ---- Auditoria ----

    public async Task RegistrarAuditoriaAsync(EventoAuditoria evento, CancellationToken ct = default)
        => await _db.Auditoria.AddAsync(evento, ct);

    public async Task<IReadOnlyList<EventoAuditoria>> EventosAuditoriaAsync(int limite = 200, CancellationToken ct = default)
        => await _db.Auditoria.AsNoTracking()
            .OrderByDescending(e => e.DataHora).ThenByDescending(e => e.Id)
            .Take(limite)
            .ToListAsync(ct);

    // ---- Financeiro ----

    public async Task AdicionarLancamentoAsync(LancamentoFinanceiro lancamento, CancellationToken ct = default)
        => await _db.Lancamentos.AddAsync(lancamento, ct);

    public async Task<LancamentoFinanceiro?> ObterLancamentoAsync(int lancamentoId, CancellationToken ct = default)
        => await _db.Lancamentos.FirstOrDefaultAsync(l => l.Id == lancamentoId, ct);

    public async Task<IReadOnlyList<LancamentoFinanceiro>> LancamentosNoPeriodoAsync(
        DateOnly inicio, DateOnly fim, int? limite = null, CancellationToken ct = default)
    {
        var q = _db.Lancamentos
            .Include(l => l.Categoria)
            .Include(l => l.Paciente)
            .Where(l => l.Data >= inicio && l.Data <= fim)
            .OrderByDescending(l => l.Data).ThenByDescending(l => l.Id);

        // O corte vai para o SQL: materializar o período inteiro para depois descartar
        // é exatamente o que a convenção do projeto proíbe (o banco é remoto).
        return limite is { } n && n > 0
            ? await q.Take(n).ToListAsync(ct)
            : await q.ToListAsync(ct);
    }

    public Task<LancamentoFinanceiro?> UltimoRecebimentoDoPacienteAsync(
        int pacienteId, CancellationToken ct = default)
        => _db.Lancamentos.AsNoTracking()
            .Where(l => l.PacienteId == pacienteId)
            .Where(l => l.Tipo == TipoLancamento.Entrada)
            .Where(l => l.Status == StatusLancamento.Realizado)
            .OrderByDescending(l => l.Data).ThenByDescending(l => l.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Clinica.Application.Modelos.ValorLancamento>>
        ValoresDeLancamentoNoPeriodoAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => await _db.Lancamentos.AsNoTracking()
            .Where(l => l.Data >= inicio && l.Data <= fim)
            .Select(l => new Clinica.Application.Modelos.ValorLancamento(l.Tipo, l.Status, l.Valor))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<int>> CodigosComLancamentoAsync(
        IReadOnlyCollection<int> codigoIds, CancellationToken ct = default)
    {
        if (codigoIds.Count == 0) return [];
        return await _db.Lancamentos.AsNoTracking()
            .Where(l => l.CodigoFaturamentoId != null
                     && codigoIds.Contains(l.CodigoFaturamentoId.Value)
                     && l.Status != StatusLancamento.Cancelado)
            .Select(l => l.CodigoFaturamentoId!.Value)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CategoriaFinanceira>> CategoriasFinanceirasAsync(CancellationToken ct = default)
        => await _db.CategoriasFinanceiras.AsNoTracking()
            .OrderBy(c => c.Ordem).ThenBy(c => c.Nome)
            .ToListAsync(ct);

    public async Task AdicionarCategoriaFinanceiraAsync(CategoriaFinanceira categoria, CancellationToken ct = default)
        => await _db.CategoriasFinanceiras.AddAsync(categoria, ct);

    public Task<CategoriaFinanceira?> ObterCategoriaFinanceiraAsync(
        int categoriaId, CancellationToken ct = default)
        => _db.CategoriasFinanceiras.FirstOrDefaultAsync(c => c.Id == categoriaId, ct);
    // ---- Indicadores gerenciais ----

    public async Task<IReadOnlyList<Evolucao>> EvolucoesNoPeriodoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => await _db.Evolucoes.AsNoTracking()
            .Include(e => e.Profissional)
            .Where(e => e.Data >= inicio && e.Data <= fim)
            .OrderBy(e => e.Data)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Clinica.Application.Modelos.InatividadePaciente>> InatividadeAsync(
        DateOnly referencia, CancellationToken ct = default)
    {
        var apartirDe = referencia.ToDateTime(TimeOnly.MinValue);

        // Projeção pura: o banco devolve uma linha por paciente com duas agregações, em
        // vez de mandar a base de atendimentos inteira para a aplicação contar.
        return await _db.Pacientes.AsNoTracking()
            .Select(p => new Clinica.Application.Modelos.InatividadePaciente(
                p.Id,
                p.Nome,
                p.Telefone,
                p.Atendimentos.Max(a => (DateOnly?)a.Data),
                _db.Agendamentos
                    .Where(a => a.PacienteId == p.Id
                                && a.DataHora >= apartirDe
                                && a.Status == StatusAgendamento.Agendado)
                    .Min(a => (DateTime?)a.DataHora)))
            .ToListAsync(ct);
    }

    // ---- Campanhas: confirmação, NPS e recall ----

    public async Task AdicionarContatoAsync(ContatoCampanha contato, CancellationToken ct = default)
        => await _db.Contatos.AddAsync(contato, ct);

    public Task<ContatoCampanha?> ObterContatoAsync(int contatoId, CancellationToken ct = default)
        => _db.Contatos
            .Include(c => c.Paciente)
            .Include(c => c.Agendamento)
            .FirstOrDefaultAsync(c => c.Id == contatoId, ct);

    public async Task<IReadOnlyList<ContatoCampanha>> ContatosAsync(
        TipoContato? tipo, StatusContato? status,
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        var q = _db.Contatos.AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Agendamento)
            .Where(c => c.Referencia >= inicio && c.Referencia <= fim);

        if (tipo is { } t) q = q.Where(c => c.Tipo == t);
        if (status is { } s) q = q.Where(c => c.Status == s);

        // Pendente primeiro: a tela existe para trabalhar a fila, não para ler histórico.
        return await q
            .OrderBy(c => c.Status)
            .ThenBy(c => c.Referencia)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> OrigensDeContatoAsync(
        TipoContato tipo, IReadOnlyCollection<string> origens, CancellationToken ct = default)
    {
        if (origens.Count == 0) return [];

        return await _db.Contatos.AsNoTracking()
            .Where(c => c.Tipo == tipo && origens.Contains(c.Origem))
            .Select(c => c.Origem)
            .ToListAsync(ct);
    }

    // ---- Usuários e permissões ----

    public async Task<IReadOnlyList<UsuarioSistema>> UsuariosAsync(CancellationToken ct = default)
        => await _db.Usuarios.AsNoTracking()
            .Include(u => u.Profissional)
            .OrderByDescending(u => u.Ativo)
            .ThenBy(u => u.Nome)
            .ToListAsync(ct);

    public Task<UsuarioSistema?> ObterUsuarioAsync(int usuarioId, CancellationToken ct = default)
        => _db.Usuarios
            .Include(u => u.Profissional)
            .FirstOrDefaultAsync(u => u.Id == usuarioId, ct);

    public Task<UsuarioSistema?> ObterUsuarioPorLoginAsync(string login, CancellationToken ct = default)
        => _db.Usuarios
            .Include(u => u.Profissional)
            .FirstOrDefaultAsync(u => u.Login == login, ct);

    public async Task AdicionarUsuarioAsync(UsuarioSistema usuario, CancellationToken ct = default)
        => await _db.Usuarios.AddAsync(usuario, ct);

    public async Task RemoverUsuarioAsync(int usuarioId, CancellationToken ct = default)
    {
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.Id == usuarioId, ct);
        if (u is not null)
            _db.Usuarios.Remove(u);
    }

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
