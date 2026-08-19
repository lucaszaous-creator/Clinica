using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
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

    public async Task<IReadOnlyList<CodigoFaturamento>> CodigosGlosadosDoPacienteAsync(
        int pacienteId, CancellationToken ct = default)
        => await _db.Codigos.AsNoTracking()
            .Include(c => c.Atendimento!)
            .Where(c => c.Atendimento!.PacienteId == pacienteId
                     && (c.Glosa == StatusGlosa.Glosada || c.Glosa == StatusGlosa.Reapresentada))
            .OrderBy(c => c.DataLimiteRecurso)
            .ToListAsync(ct);

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

        if (filtro.Modalidade is { } modalidade)
            q = q.Where(c => c.Atendimento!.Modalidade == modalidade);

        // A especialidade da GUIA vem primeiro; a do atendimento é o caminho de baixo,
        // para a guia de acupuntura de um atendimento de Clínica da Dor não sumir do
        // filtro da própria especialidade.
        if (filtro.Especialidade is { } especialidade)
            q = q.Where(c => c.Especialidade == especialidade
                             || (c.Especialidade == null
                                 && c.Atendimento!.EspecialidadeConsulta == especialidade));

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
        => await _db.Pacientes.AsNoTracking().Include(p => p.Atendimentos).ToListAsync(ct);

    /// <summary>
    /// Uma linha por paciente já atendido, agregada NO BANCO.
    ///
    /// O `GroupBy` sai em SQL (`MAX(data)`, `COUNT(*)`), e o `join` com pacientes traz
    /// só as três colunas que a lista mostra. Quem antes fazia isso em memória arrastava
    /// pacientes e atendimentos inteiros pela rede para usar dois números por pessoa.
    /// </summary>
    public async Task<IReadOnlyList<ResumoAtendimentosPaciente>> ResumoAtendimentosPorPacienteAsync(
        CancellationToken ct = default)
        => await _db.Atendimentos.AsNoTracking()
            // Só sessão que ACONTECEU (parcela 70): com a guia nascendo na marcação, a
            // linha registrada não é mais sinônimo de visita — a marcada para o futuro
            // ainda não visitou, e a cancelada nunca visitou ("cancelado não é visita").
            .Where(a => a.RealizadoEm != null)
            .GroupBy(a => a.PacienteId)
            .Select(g => new { PacienteId = g.Key, Ultima = g.Max(a => a.Data), Total = g.Count() })
            .Join(_db.Pacientes.AsNoTracking(),
                r => r.PacienteId, p => p.Id,
                (r, p) => new ResumoAtendimentosPaciente(
                    p.Id, p.Nome, p.Telefone, r.Ultima, r.Total))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<int>> PacientesComAgendamentoFuturoAsync(
        IReadOnlyCollection<int> pacienteIds, DateOnly dia, CancellationToken ct = default)
    {
        if (pacienteIds.Count == 0) return [];

        // `DataHora` é DateTime e o corte é por DIA: comparar contra a meia-noite do dia
        // mantém a sessão marcada para hoje mais tarde dentro do resultado.
        var corte = dia.ToDateTime(TimeOnly.MinValue);

        return await _db.Agendamentos.AsNoTracking()
            .Where(a => pacienteIds.Contains(a.PacienteId)
                        && a.Status == StatusAgendamento.Agendado
                        && a.DataHora >= corte)
            .Select(a => a.PacienteId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<int>> PacientesJaContatadosAsync(
        IReadOnlyDictionary<int, DateOnly> desdeQuandoPorPaciente,
        TipoContato tipo, CancellationToken ct = default)
    {
        if (desdeQuandoPorPaciente.Count == 0) return [];

        var ids = desdeQuandoPorPaciente.Keys.ToList();

        // O corte por paciente é diferente (cada um tem a sua última sessão), e traduzir
        // isso para SQL exigiria um OR por pessoa. Traz-se então o par (paciente, data)
        // do conjunto — que é pequeno, um contato por paciente por rodada — e o corte
        // acontece aqui. Continua sendo UMA consulta.
        var contatos = await _db.Contatos.AsNoTracking()
            .Where(c => ids.Contains(c.PacienteId) && c.Tipo == tipo)
            .Select(c => new { c.PacienteId, c.Referencia })
            .ToListAsync(ct);

        return contatos
            .Where(c => desdeQuandoPorPaciente.TryGetValue(c.PacienteId, out var desde)
                        && c.Referencia >= desde)
            .Select(c => c.PacienteId)
            .Distinct()
            .ToList();
    }

    public async Task<IReadOnlyList<PacotePaciente>> PacotesDosPacientesAsync(
        IReadOnlyCollection<int> pacienteIds, CancellationToken ct = default)
    {
        if (pacienteIds.Count == 0) return [];

        return await _db.PacotesPaciente.AsNoTracking()
            .Include(p => p.Consumos)
            .Where(p => pacienteIds.Contains(p.PacienteId))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Um código pelo id, com o atendimento e o PACIENTE juntos.
    ///
    /// O Include entrou na parcela 45, quando a baixa passou a criticar o número da guia
    /// pelo formato do convênio — e o convênio é do paciente. Ele conserta de quebra um
    /// defeito silencioso: <c>FaturamentoService</c> gravava
    /// <c>PacienteId = codigo.Atendimento?.PacienteId</c> na auditoria da baixa, do estorno
    /// e da glosa, e sem o atendimento carregado isso era null sempre que o contexto ainda
    /// não tivesse visto aquele atendimento por outro caminho. A trilha registrava a ação
    /// sem dizer de quem era a guia.
    /// </summary>
    public Task<CodigoFaturamento?> ObterCodigoAsync(int codigoId, CancellationToken ct = default)
        => _db.Codigos
            .Include(c => c.Atendimento!).ThenInclude(a => a.Paciente!)
            .FirstOrDefaultAsync(c => c.Id == codigoId, ct);

    public async Task AdicionarAtendimentoAsync(Atendimento atendimento, CancellationToken ct = default)
        => await _db.Atendimentos.AddAsync(atendimento, ct);

    public Task<Atendimento?> ObterAtendimentoAsync(int atendimentoId, CancellationToken ct = default)
        => _db.Atendimentos
            .Include(a => a.Paciente)
            .Include(a => a.Codigos)
            .FirstOrDefaultAsync(a => a.Id == atendimentoId, ct);

    public async Task MarcarAtendimentosSemCarimboComoRealizadosAsync(CancellationToken ct = default)
    {
        // Quem tem LancadoEm herda a hora real do lançamento; o resto recebe o momento da
        // ativação — o VALOR importa pouco (os leitores ancoram em "não nulo"; período é
        // sempre o da Data), o que não pode é a linha ficar de fora de "realizado".
        await _db.Atendimentos
            .Where(a => a.RealizadoEm == null && a.LancadoEm != null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.RealizadoEm, a => a.LancadoEm), ct);
        await _db.Atendimentos
            .Where(a => a.RealizadoEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.RealizadoEm, DateTime.Now), ct);
    }

    public async Task<IReadOnlyList<Paciente>> AniversariantesAsync(
        DateOnly dia, int janelaDias = 0, CancellationToken ct = default)
    {
        // Dia e mes, nunca a data inteira: o ano de nascimento nao tem nada a ver com a
        // pergunta. A janela e resolvida em memoria de proposito — sao poucos dias e a
        // virada de ano/mes em SQL exigiria aritmetica que nao vale a complexidade.
        var dias = Enumerable.Range(0, Math.Max(janelaDias, 0) + 1)
            .Select(d => dia.AddDays(d))
            .Select(d => (d.Month, d.Day))
            .ToHashSet();

        var meses = dias.Select(d => d.Month).Distinct().ToList();

        var candidatos = await _db.Pacientes.AsNoTracking()
            .Where(p => p.DataNascimento != null && meses.Contains(p.DataNascimento.Value.Month))
            .ToListAsync(ct);

        return candidatos
            .Where(p => dias.Contains((p.DataNascimento!.Value.Month, p.DataNascimento.Value.Day)))
            .OrderBy(p => p.Nome)
            .ToList();
    }

    public async Task<IReadOnlyList<Agendamento>> AgendamentosDoPacienteAsync(
        int pacienteId, CancellationToken ct = default)
        => await _db.Agendamentos.AsNoTracking()
            .Where(a => a.PacienteId == pacienteId)
            .OrderByDescending(a => a.DataHora)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Agendamento>> AgendamentosDoPacienteNoDiaAsync(
        int pacienteId, DateOnly dia, CancellationToken ct = default)
    {
        var inicio = dia.ToDateTime(TimeOnly.MinValue);
        var fim = dia.ToDateTime(TimeOnly.MaxValue);

        return await _db.Agendamentos.AsNoTracking()
            .Where(a => a.PacienteId == pacienteId
                        && a.DataHora >= inicio && a.DataHora <= fim)
            .OrderBy(a => a.DataHora)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<(int PacienteId, OrigemPaciente? Origem, string? IndicadoPor)>> OrigensDosPacientesAsync(
        CancellationToken ct = default)
    {
        var linhas = await _db.Pacientes.AsNoTracking()
            .Select(p => new { p.Id, p.Origem, p.IndicadoPor })
            .ToListAsync(ct);

        return linhas.Select(l => (l.Id, l.Origem, l.IndicadoPor)).ToList();
    }

    public async Task<IReadOnlyDictionary<int, DateOnly>> PrimeiroAtendimentoPorPacienteAsync(
        CancellationToken ct = default)
    {
        var pares = await _db.Atendimentos.AsNoTracking()
            // "Estreou" é sessão que ACONTECEU (parcela 70): marcada para o futuro ainda
            // não estreou, e cancelada nunca estreou.
            .Where(a => a.RealizadoEm != null)
            .GroupBy(a => a.PacienteId)
            .Select(g => new { g.Key, Primeira = g.Min(a => a.Data) })
            .ToListAsync(ct);

        return pares.ToDictionary(p => p.Key, p => p.Primeira);
    }

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

    public async Task<IReadOnlyList<Paciente>> PacientesPorCpfAsync(
        string cpfSoDigitos, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cpfSoDigitos)) return [];

        // A limpeza acontece NO BANCO (`replace` do SQL, que o EF traduz), e não em
        // memória: carregar a carteira inteira para comparar CPF a cada gravação de ficha
        // seria uma varredura completa numa base remota, a cada Salvar.
        return await _db.Pacientes
            .Where(p => p.Documento != null
                        && p.Documento
                            .Replace(".", "")
                            .Replace("-", "")
                            .Replace("/", "")
                            .Replace(" ", "") == cpfSoDigitos)
            .ToListAsync(ct);
    }

    public async Task AdicionarPacienteAsync(Paciente paciente, CancellationToken ct = default)
        => await _db.Pacientes.AddAsync(paciente, ct);

    public async Task RemoverPacienteAsync(int pacienteId, CancellationToken ct = default)
    {
        var paciente = await _db.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct);
        if (paciente is not null)
            _db.Pacientes.Remove(paciente);
    }

    public async Task<bool> PacienteTemRegistroClinicoAsync(int pacienteId, CancellationToken ct = default)
        // Seis raízes bastam: anexo e mapa pendem da evolução; resposta, da avaliação;
        // checagem, da prescrição — se a raiz não existe, o dependente tampouco.
        => await _db.Evolucoes.AnyAsync(e => e.PacienteId == pacienteId, ct)
           || await _db.AvaliacoesClinicas.AnyAsync(a => a.PacienteId == pacienteId, ct)
           || await _db.MedidasClinicas.AnyAsync(m => m.PacienteId == pacienteId, ct)
           || await _db.DocumentosClinicos.AnyAsync(d => d.PacienteId == pacienteId, ct)
           || await _db.PrescricoesInternas.AnyAsync(p => p.PacienteId == pacienteId, ct)
           || await _db.ProblemasPaciente.AnyAsync(p => p.PacienteId == pacienteId, ct);

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

    public async Task<IReadOnlyList<Atendimento>> AtendimentosDoPacienteNoDiaAsync(
        int pacienteId, DateOnly dia, CancellationToken ct = default)
        => await _db.Atendimentos.AsNoTracking()
            .Include(a => a.Codigos)
            .Where(a => a.PacienteId == pacienteId && a.Data == dia)
            .OrderBy(a => a.Id)
            .ToListAsync(ct);

    public Task<int> ContarAtendimentosAtivosDoPacienteAsync(int pacienteId, DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        // A COTA conta o que consome a autorização (parcela 70): sessão realizada, guia
        // aberta ou baixada — inclusive a MARCADA para o futuro, que é o alerta chegando
        // na hora certa ("a 11ª sessão da autorização de 10 avisa na marcação"). Fica de
        // fora a sessão cancelada/falta, cujas guias foram suspensas: contá-la faria a
        // cota estourar por sessões que não aconteceram nem vão acontecer.
        => _db.Atendimentos.CountAsync(a =>
            a.PacienteId == pacienteId && a.Data >= inicio && a.Data <= fim
            && (a.RealizadoEm != null
                || a.Codigos.Any(c => c.DataBaixa != null || c.Status == StatusCodigo.Aberto)), ct);

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
            existe.FormatoNumeroGuia = convenio.FormatoNumeroGuia;
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
        => await _db.Pacientes.AsNoTracking()
            // Leitura pura (aba Consultas e pendências do painel): sem rastreamento o
            // EF não precisa guardar milhares de pacientes no change tracker, o que
            // também tira custo do SaveChanges seguinte no mesmo escopo.
            .Include(p => p.Consultas).OrderBy(p => p.Nome).ToListAsync(ct);

    public async Task<IReadOnlyList<Consulta>> ConsultasDosPacientesAsync(
        IReadOnlyCollection<int> pacienteIds, CancellationToken ct = default)
    {
        // Lista vazia não vira "WHERE Id IN ()" — o EF traduziria para uma consulta que
        // sempre volta vazia, mas ainda assim ida e volta ao banco por nada.
        if (pacienteIds.Count == 0) return Array.Empty<Consulta>();

        return await _db.Consultas.AsNoTracking()
            .Where(c => pacienteIds.Contains(c.PacienteId))
            .OrderByDescending(c => c.DataEmissao)
            .ToListAsync(ct);
    }

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

    public async Task<IReadOnlyList<Agendamento>> AgendamentosDaSerieAsync(
        string serieId, CancellationToken ct = default)
        // RASTREADOS (sem AsNoTracking): cancelar a serie escreve nestas entidades.
        => await _db.Agendamentos
            .Include(a => a.Paciente)
            .Include(a => a.Profissional)
            .Where(a => a.SerieId == serieId)
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

    /// <summary>
    /// O profissional já deixou rastro em algum lugar?
    ///
    /// A guarda existe para o `EquipeService` poder dizer "desative em vez de excluir,
    /// para não apagar o histórico" — mas ela só olhava agenda e lista de espera, e o
    /// histórico de um profissional é bem maior que isso. Faltavam sete tabelas, entre
    /// elas três em que a exclusão apagaria dado que ninguém tem como recuperar:
    /// evolução e documento clínico (prontuário, cuja guarda é obrigação legal),
    /// repasse apurado (o que já foi pago a ele) e a meta acordada para o mês.
    ///
    /// O usuário de sistema entra na conta pelo motivo oposto: não é histórico, é
    /// ACESSO — apagar o profissional deixaria um login apontando para ninguém.
    /// </summary>
    public async Task<bool> ProfissionalEmUsoAsync(int profissionalId, CancellationToken ct = default)
        => await _db.Agendamentos.AnyAsync(a => a.ProfissionalId == profissionalId, ct)
           || await _db.ListaEspera.AnyAsync(l => l.ProfissionalId == profissionalId, ct)
           || await _db.Evolucoes.AnyAsync(e => e.ProfissionalId == profissionalId, ct)
           || await _db.DocumentosClinicos.AnyAsync(d => d.ProfissionalId == profissionalId, ct)
           || await _db.RepassesApurados.AnyAsync(r => r.ProfissionalId == profissionalId, ct)
           || await _db.RegrasRepasse.AnyAsync(r => r.ProfissionalId == profissionalId, ct)
           || await _db.Metas.AnyAsync(m => m.ProfissionalId == profissionalId, ct)
           || await _db.BloqueiosAgenda.AnyAsync(b => b.ProfissionalId == profissionalId, ct)
           || await _db.Usuarios.AnyAsync(u => u.ProfissionalId == profissionalId, ct);

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

    /// <summary>
    /// A sala já foi usada? Bloqueio conta: uma reforma marcada na sala é registro de
    /// agenda tanto quanto um atendimento, e apagar a sala levaria o bloqueio junto.
    /// </summary>
    public async Task<bool> SalaEmUsoAsync(int salaId, CancellationToken ct = default)
        => await _db.Agendamentos.AnyAsync(a => a.SalaId == salaId, ct)
           || await _db.BloqueiosAgenda.AnyAsync(b => b.SalaId == salaId, ct);

    public async Task RemoverSalaAsync(int salaId, CancellationToken ct = default)
    {
        var s = await _db.Salas.FirstOrDefaultAsync(x => x.Id == salaId, ct);
        if (s is not null)
            _db.Salas.Remove(s);
    }

    // ---- Lista de espera ----

    // ---- Bloqueio de agenda ----

    public async Task<IReadOnlyList<BloqueioAgenda>> BloqueiosNoPeriodoAsync(
        DateTime inicio, DateTime fim, CancellationToken ct = default)
        => await _db.BloqueiosAgenda.AsNoTracking()
            .Include(x => x.Profissional)
            .Include(x => x.Sala)
            .Where(x => x.Inicio < fim && x.Fim > inicio)
            .OrderBy(x => x.Inicio)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<BloqueioAgenda>> BloqueiosAsync(
        DateTime? aPartirDe = null, CancellationToken ct = default)
    {
        var q = _db.BloqueiosAgenda.AsNoTracking()
            .Include(x => x.Profissional)
            .Include(x => x.Sala)
            .AsQueryable();

        // O que ja passou nao some do banco, mas sai da lista por padrao: bloqueio velho
        // e historico, e a tela existe para responder "o que esta fechado daqui pra frente".
        if (aPartirDe is { } desde) q = q.Where(x => x.Fim >= desde);

        return await q.OrderBy(x => x.Inicio).ToListAsync(ct);
    }

    public async Task AdicionarBloqueioAsync(BloqueioAgenda bloqueio, CancellationToken ct = default)
        => await _db.BloqueiosAgenda.AddAsync(bloqueio, ct);

    public Task<BloqueioAgenda?> ObterBloqueioAsync(int bloqueioId, CancellationToken ct = default)
        => _db.BloqueiosAgenda
            .Include(x => x.Profissional)
            .Include(x => x.Sala)
            .FirstOrDefaultAsync(x => x.Id == bloqueioId, ct);

    public async Task RemoverBloqueioAsync(int bloqueioId, CancellationToken ct = default)
    {
        var bloqueio = await _db.BloqueiosAgenda.FindAsync([bloqueioId], ct);
        if (bloqueio is not null) _db.BloqueiosAgenda.Remove(bloqueio);
    }

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

    // Com Include das VERSÕES (parcela 52): quem obtém a evolução para EDITAR precisa
    // delas carregadas, senão `Versoes.Count + 1` recomeçaria do 1 a cada correção e a
    // numeração do histórico se repetiria. É a única leitura que as traz — as listas não
    // precisam do texto antigo.
    public Task<Evolucao?> ObterEvolucaoAsync(int evolucaoId, CancellationToken ct = default)
        => _db.Evolucoes
            .Include(e => e.Profissional)
            .Include(e => e.Versoes)
            .FirstOrDefaultAsync(e => e.Id == evolucaoId, ct);

    // Sem Include dos anexos de propósito: os bytes só saem do banco quando alguém
    // pede um arquivo específico (ver ConteudoDoAnexoAsync).
    //
    // Cancelada NÃO entra (parcela 52): ela continua no banco, guardada pelos 20 anos da
    // Lei 13.787, mas fora do prontuário que se lê — que é exatamente o que "cancelar em
    // vez de apagar" quer dizer. Quem precisa vê-las usa EvolucoesDoPacienteAsync com
    // `incluirCanceladas`.
    public async Task<IReadOnlyList<Evolucao>> EvolucoesDoPacienteAsync(
        int pacienteId, CancellationToken ct = default)
        => await EvolucoesDoPacienteAsync(pacienteId, false, ct);

    public async Task<IReadOnlyDictionary<int, (int Inicial, int Ultima)>> ParesDeEvaDosPacientesAsync(
        IReadOnlyCollection<int> pacienteIds, CancellationToken ct = default)
    {
        if (pacienteIds.Count == 0) return new Dictionary<int, (int, int)>();

        // Só as colunas que a carteira usa. `TemParEva` é propriedade calculada da
        // entidade e não traduz para SQL — a condição vai escrita aqui, e é a MESMA:
        // as duas pontas presentes.
        var medidas = await _db.Evolucoes.AsNoTracking()
            .Where(e => pacienteIds.Contains(e.PacienteId))
            .Where(e => e.CanceladaEm == null)
            .Where(e => e.EvaAntes != null && e.EvaDepois != null)
            .OrderBy(e => e.Data).ThenBy(e => e.Id)
            .Select(e => new { e.PacienteId, e.EvaAntes, e.EvaDepois })
            .ToListAsync(ct);

        return medidas
            .GroupBy(m => m.PacienteId)
            .ToDictionary(
                g => g.Key,
                g => (Inicial: g.First().EvaAntes!.Value, Ultima: g.Last().EvaDepois!.Value));
    }

    public async Task<IReadOnlyList<Evolucao>> EvolucoesDoPacienteAsync(
        int pacienteId, bool incluirCanceladas, CancellationToken ct = default)
        => await _db.Evolucoes.AsNoTracking()
            .Include(e => e.Profissional)
            .Where(e => e.PacienteId == pacienteId)
            .Where(e => incluirCanceladas || e.CanceladaEm == null)
            .OrderByDescending(e => e.Data).ThenByDescending(e => e.Id)
            .ToListAsync(ct);

    /// <summary>As versões anteriores de uma sessão, da mais antiga para a mais nova.</summary>
    public async Task<IReadOnlyList<VersaoEvolucao>> VersoesDaEvolucaoAsync(
        int evolucaoId, CancellationToken ct = default)
        => await _db.VersoesEvolucao.AsNoTracking()
            .Where(v => v.EvolucaoId == evolucaoId)
            .OrderBy(v => v.Versao)
            .ToListAsync(ct);

    // Uma consulta para o prontuário inteiro, como a contagem de anexos: o agrupamento é
    // do SQL, e o que volta são dois inteiros por sessão CORRIGIDA — as outras nem aparecem.
    public async Task<IReadOnlyDictionary<int, int>> ContagemDeVersoesAsync(
        IReadOnlyCollection<int> evolucaoIds, CancellationToken ct = default)
    {
        if (evolucaoIds.Count == 0) return new Dictionary<int, int>();

        var contagens = await _db.VersoesEvolucao.AsNoTracking()
            .Where(v => evolucaoIds.Contains(v.EvolucaoId))
            .GroupBy(v => v.EvolucaoId)
            .Select(g => new { EvolucaoId = g.Key, Quantas = g.Count() })
            .ToListAsync(ct);

        return contagens.ToDictionary(c => c.EvolucaoId, c => c.Quantas);
    }

    public Task<AnexoProntuario?> ObterAnexoAsync(int anexoId, CancellationToken ct = default)
        => _db.AnexosProntuario
            .Include(a => a.Evolucao)
            .FirstOrDefaultAsync(a => a.Id == anexoId, ct);

    public async Task<IReadOnlyList<Clinica.Application.Modelos.AnexoResumo>> AnexosDaEvolucaoAsync(
        int evolucaoId, CancellationToken ct = default)
        => await _db.AnexosProntuario.AsNoTracking()
            .Where(a => a.EvolucaoId == evolucaoId && a.CanceladoEm == null)
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

    // Uma consulta para o prontuário inteiro: o agrupamento é do SQL, e o que volta são
    // dois inteiros por sessão COM anexo — as outras nem aparecem.
    public async Task<IReadOnlyDictionary<int, int>> ContagemDeAnexosAsync(
        IReadOnlyCollection<int> evolucaoIds, CancellationToken ct = default)
    {
        if (evolucaoIds.Count == 0) return new Dictionary<int, int>();

        var contagens = await _db.AnexosProntuario.AsNoTracking()
            .Where(a => evolucaoIds.Contains(a.EvolucaoId) && a.CanceladoEm == null)
            .GroupBy(a => a.EvolucaoId)
            .Select(g => new { EvolucaoId = g.Key, Quantos = g.Count() })
            .ToListAsync(ct);

        return contagens.ToDictionary(c => c.EvolucaoId, c => c.Quantos);
    }

    // ---- Medidas clínicas seriadas (parcela 37) ----

    public async Task AdicionarMedidaAsync(MedidaClinica medida, CancellationToken ct = default)
        => await _db.MedidasClinicas.AddAsync(medida, ct);

    public Task<MedidaClinica?> ObterMedidaAsync(int medidaId, CancellationToken ct = default)
        => _db.MedidasClinicas.FirstOrDefaultAsync(m => m.Id == medidaId, ct);

    public async Task<IReadOnlyList<MedidaClinica>> MedidasDoPacienteAsync(
        int pacienteId, string? tipoCodigo = null, bool incluirCanceladas = false,
        CancellationToken ct = default)
    {
        // Cancelada fora da série (parcela 52): ela fica no banco pelos 20 anos, mas a
        // curva desenha o que vale hoje. A EXPORTAÇÃO passa true — o arquivo carrega o
        // prontuário sob guarda inteiro.
        var consulta = _db.MedidasClinicas.AsNoTracking()
            .Include(m => m.Profissional)
            .Where(m => m.PacienteId == pacienteId);
        if (!incluirCanceladas)
            consulta = consulta.Where(m => m.CanceladaEm == null);

        if (!string.IsNullOrWhiteSpace(tipoCodigo))
            consulta = consulta.Where(m => m.TipoCodigo == tipoCodigo);

        return await consulta
            .OrderByDescending(m => m.Data).ThenByDescending(m => m.Id)
            .ToListAsync(ct);
    }

    // ---- Lista de problemas (parcela 37) ----

    public async Task AdicionarProblemaAsync(ProblemaPaciente problema, CancellationToken ct = default)
        => await _db.ProblemasPaciente.AddAsync(problema, ct);

    public Task<ProblemaPaciente?> ObterProblemaAsync(int problemaId, CancellationToken ct = default)
        => _db.ProblemasPaciente.FirstOrDefaultAsync(p => p.Id == problemaId, ct);

    public async Task<IReadOnlyList<ProblemaPaciente>> ProblemasDoPacienteAsync(
        int pacienteId, bool somenteAtivos = false, CancellationToken ct = default)
    {
        var consulta = _db.ProblemasPaciente.AsNoTracking()
            .Include(p => p.Profissional)
            .Where(p => p.PacienteId == pacienteId);

        if (somenteAtivos)
            consulta = consulta.Where(p => p.Situacao == SituacaoProblema.Ativo);

        // Ativo primeiro (o enum já nasce nessa ordem), depois o mais recente. A ordem é
        // do SQL porque a leitura é sempre esta, e reordenar em memória faria cada tela
        // repetir — e uma delas repetiria diferente.
        return await consulta
            .OrderBy(p => p.Situacao)
            .ThenByDescending(p => p.CriadoEm).ThenByDescending(p => p.Id)
            .ToListAsync(ct);
    }

    // ---- Avaliações clínicas por instrumento (parcela 36) ----

    public async Task AdicionarAvaliacaoAsync(
        AvaliacaoClinica avaliacao, CancellationToken ct = default)
        => await _db.AvaliacoesClinicas.AddAsync(avaliacao, ct);

    // Aqui as respostas VÊM: é a leitura de uma aplicação inteira, a segunda via.
    public Task<AvaliacaoClinica?> ObterAvaliacaoAsync(
        int avaliacaoId, CancellationToken ct = default)
        => _db.AvaliacoesClinicas
            .Include(a => a.Respostas.OrderBy(r => r.Ordem))
            .Include(a => a.Profissional)
            .FirstOrDefaultAsync(a => a.Id == avaliacaoId, ct);

    // Sem Include das respostas de propósito, pelo mesmo motivo dos anexos do prontuário:
    // a lista mostra escore, faixa e data, e trazer as dez respostas de cada aplicação
    // multiplicaria por dez o que passa pela rede para desenhar o que não as mostra.
    public async Task<IReadOnlyList<AvaliacaoClinica>> AvaliacoesDoPacienteAsync(
        int pacienteId, string? instrumentoCodigo = null, bool incluirCanceladas = false,
        CancellationToken ct = default)
    {
        // Cancelada fora da série, como a medida (parcela 52) — salvo na exportação,
        // que carrega o prontuário sob guarda inteiro.
        var consulta = _db.AvaliacoesClinicas.AsNoTracking()
            .Include(a => a.Profissional)
            .Where(a => a.PacienteId == pacienteId);
        if (!incluirCanceladas)
            consulta = consulta.Where(a => a.CanceladaEm == null);

        if (!string.IsNullOrWhiteSpace(instrumentoCodigo))
            consulta = consulta.Where(a => a.InstrumentoCodigo == instrumentoCodigo);

        return await consulta
            .OrderByDescending(a => a.Data).ThenByDescending(a => a.Id)
            .ToListAsync(ct);
    }

    // O agrupamento vai no SQL: um profissional com dois anos de casa tem milhares de
    // agendamentos, e a tela mostra uma linha por paciente. Cancelado e falta ficam de
    // fora — "meus pacientes" é quem eu atendi, não quem marcou e não veio.
    public async Task<IReadOnlyList<Clinica.Application.Modelos.PacienteDoProfissional>>
        PacientesDoProfissionalAsync(
            int profissionalId, int limite = 200, CancellationToken ct = default)
    {
        // O agrupamento sai como DateTime porque é o tipo da coluna; a conversão para
        // DateOnly acontece depois, em memória, sobre as poucas linhas já reduzidas —
        // traduzir DateOnly.FromDateTime para SQL não é coisa que se peça ao provedor.
        var agrupado = await _db.Agendamentos.AsNoTracking()
            .Where(a => a.ProfissionalId == profissionalId
                        && a.Status == StatusAgendamento.Realizado)
            .GroupBy(a => new { a.PacienteId, Nome = a.Paciente!.Nome })
            .Select(g => new
            {
                g.Key.PacienteId,
                g.Key.Nome,
                Ultima = g.Max(a => a.DataHora),
                Sessoes = g.Count()
            })
            .OrderByDescending(x => x.Ultima)
            .Take(limite)
            .ToListAsync(ct);

        return agrupado
            .Select(x => new Clinica.Application.Modelos.PacienteDoProfissional(
                x.PacienteId, x.Nome, DateOnly.FromDateTime(x.Ultima), x.Sessoes))
            .ToList();
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
    public async Task<IReadOnlyList<DocumentoClinico>> DocumentosPublicadosVencidosAsync(
        DateOnly hoje, CancellationToken ct = default)
        => await _db.DocumentosClinicos
            .Where(d => d.TokenPublicacao != null
                        && d.PublicadoAte != null
                        && d.PublicadoAte < hoje)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentoClinico>> DocumentosDoPacienteAsync(
        int pacienteId, CancellationToken ct = default)
        => await _db.DocumentosClinicos.AsNoTracking()
            .Include(d => d.Profissional)
            .Where(d => d.PacienteId == pacienteId)
            .OrderByDescending(d => d.Data).ThenByDescending(d => d.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentoClinico>> DocumentosClinicosNoPeriodoAsync(
        DateOnly inicio, DateOnly fim, TipoDocumentoClinico? tipo = null,
        int? pacienteId = null, CancellationToken ct = default)
    {
        var q = _db.DocumentosClinicos.AsNoTracking()
            .Include(d => d.Paciente)
            .Include(d => d.Profissional)
            .Where(d => d.Data >= inicio && d.Data <= fim);

        if (tipo is { } t) q = q.Where(d => d.Tipo == t);
        if (pacienteId is { } p) q = q.Where(d => d.PacienteId == p);

        // Sem os itens: a lista mostra número, tipo, paciente e data. Quem quer o conteúdo
        // pede a reimpressão, e aí o PDF carrega o documento inteiro.
        return await q
            .OrderByDescending(d => d.Data).ThenByDescending(d => d.Id)
            .ToListAsync(ct);
    }

    public async Task<int> ProximoNumeroDocumentoAsync(int ano, CancellationToken ct = default)
    {
        // Contar serve porque documento não se apaga: cancelar mantém a linha (e o número).
        var prefixo = ano.ToString() + "/";
        var emitidos = await _db.DocumentosClinicos.AsNoTracking()
            .CountAsync(d => d.Numero.StartsWith(prefixo), ct);
        return emitidos + 1;
    }

    // ---- Prescrição de execução interna e checagem de enfermagem (parcela 42) ----

    public async Task AdicionarPrescricaoInternaAsync(
        PrescricaoInterna prescricao, CancellationToken ct = default)
        => await _db.PrescricoesInternas.AddAsync(prescricao, ct);

    public Task<PrescricaoInterna?> ObterPrescricaoInternaAsync(
        int prescricaoId, CancellationToken ct = default)
        => _db.PrescricoesInternas
            .Include(p => p.Paciente)
            .Include(p => p.Profissional)
            .Include(p => p.Itens).ThenInclude(i => i.Checagens)
            .Include(p => p.Assinaturas)
            .FirstOrDefaultAsync(p => p.Id == prescricaoId, ct);

    public Task<PrescricaoInterna?> ObterPrescricaoInternaPorCodigoAsync(
        string codigo, CancellationToken ct = default)
    {
        var limpo = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        return _db.PrescricoesInternas
            .Include(p => p.Paciente)
            .Include(p => p.Profissional)
            .Include(p => p.Itens).ThenInclude(i => i.Checagens)
            .Include(p => p.Assinaturas)
            .FirstOrDefaultAsync(p => p.CodigoVerificacao == limpo, ct);
    }

    public async Task<IReadOnlyList<PrescricaoInterna>> PrescricoesInternasDoPacienteAsync(
        int pacienteId, int limite = 50, CancellationToken ct = default)
        => await _db.PrescricoesInternas.AsNoTracking()
            .Include(p => p.Profissional)
            .Include(p => p.Itens).ThenInclude(i => i.Checagens)
            .Include(p => p.Assinaturas)
            .Where(p => p.PacienteId == pacienteId)
            .OrderByDescending(p => p.Data).ThenByDescending(p => p.Id)
            .Take(limite)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PrescricaoInterna>> PrescricoesInternasDoDiaAsync(
        DateOnly data, int? profissionalId = null, bool incluirEncerradas = false,
        CancellationToken ct = default)
    {
        var q = _db.PrescricoesInternas.AsNoTracking()
            .Include(p => p.Paciente)
            .Include(p => p.Profissional)
            .Include(p => p.Itens).ThenInclude(i => i.Checagens)
            .Include(p => p.Assinaturas)
            .Where(p => p.Data == data);

        // Rascunho e cancelada NUNCA aparecem na sala: a primeira ninguém assinou, e a
        // segunda foi desfeita. Mostrar qualquer uma das duas convidaria a técnica a
        // administrar o que não está mandado.
        q = incluirEncerradas
            ? q.Where(p => p.Situacao == SituacaoPrescricao.Assinada
                        || p.Situacao == SituacaoPrescricao.Encerrada)
            : q.Where(p => p.Situacao == SituacaoPrescricao.Assinada);

        if (profissionalId is int pid)
            q = q.Where(p => p.ProfissionalId == pid);

        return await q.OrderBy(p => p.Hora).ThenBy(p => p.Id).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PrescricaoInterna>> PrescricoesInternasAguardandoAssinaturaAsync(
        int? profissionalId = null, CancellationToken ct = default)
    {
        // A pendência é decidida no BANCO e não em memória: carregar todas as encerradas
        // da história para filtrar depois cresceria com o tempo, e esta consulta roda a
        // cada carga da sala — inclusive na releitura periódica.
        var q = _db.PrescricoesInternas.AsNoTracking()
            .Include(p => p.Paciente)
            .Include(p => p.Profissional)
            .Include(p => p.Itens).ThenInclude(i => i.Checagens)
            .Include(p => p.Assinaturas)
            .Where(p => p.Situacao == SituacaoPrescricao.Encerrada
                        && p.ExigeAssinaturaEletronicaDaExecucao
                        && !p.Assinaturas.Any(a => a.Papel == PapelAssinatura.Executante));

        if (profissionalId is int pid)
            q = q.Where(p => p.ProfissionalId == pid);

        // A mais ANTIGA primeiro: a folha esquecida há três dias é a que precisa de
        // decisão, e a de hoje a técnica resolve sozinha ao encerrar.
        return await q.OrderBy(p => p.Data).ThenBy(p => p.Hora).ThenBy(p => p.Id)
            .ToListAsync(ct);
    }

    public Task<ItemPrescricaoInterna?> ObterItemPrescricaoInternaAsync(
        int itemId, CancellationToken ct = default)
        => _db.ItensPrescricaoInterna
            .Include(i => i.Prescricao)
            .Include(i => i.Checagens)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct);

    public async Task AdicionarChecagemPrescricaoAsync(
        ChecagemPrescricao checagem, CancellationToken ct = default)
        => await _db.ChecagensPrescricao.AddAsync(checagem, ct);

    public async Task<int> ProximoNumeroPrescricaoInternaAsync(int ano, CancellationToken ct = default)
    {
        // Contar serve pela mesma razão do documento clínico: cancelar mantém a linha, e
        // com ela o número — a folha pode ter sido impressa antes do cancelamento.
        var prefixo = $"PRE {ano}/";
        var emitidas = await _db.PrescricoesInternas.AsNoTracking()
            .CountAsync(p => p.Numero.StartsWith(prefixo), ct);
        return emitidas + 1;
    }

    public Task<ArquivoAssinado?> ObterArquivoAssinadoAsync(
        int arquivoId, CancellationToken ct = default)
        => _db.ArquivosAssinados.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == arquivoId, ct);

    public async Task AdicionarArquivoAssinadoAsync(
        ArquivoAssinado arquivo, CancellationToken ct = default)
        => await _db.ArquivosAssinados.AddAsync(arquivo, ct);

    public async Task<int> PrescricoesInternasPendentesAsync(
        DateOnly data, int? profissionalId = null, CancellationToken ct = default)
    {
        // "Pendente" no SQL é: existe item não suspenso cuja última palavra ainda não foi
        // dita. Não dá para reusar ItemPrescricaoInterna.Situacao aqui — ela é C# e
        // traduzi-la obrigaria a materializar o grafo do dia inteiro para contar linhas.
        var q = _db.PrescricoesInternas.AsNoTracking()
            .Where(p => p.Data == data && p.Situacao == SituacaoPrescricao.Assinada);

        if (profissionalId is int pid)
            q = q.Where(p => p.ProfissionalId == pid);

        // O "se necessário" fica de fora pela mesma razão de PrescricaoInterna.Pendentes:
        // condição que não aconteceu não é trabalho atrasado.
        return await q.CountAsync(
            p => p.Itens.Any(i => i.SuspensoEm == null && !i.SeNecessario && !i.Checagens.Any()),
            ct);
    }

    public async Task<IReadOnlyList<ModeloEvolucao>> ModelosEvolucaoAsync(
        int? profissionalId = null, CancellationToken ct = default)
        => await _db.ModelosEvolucao.AsNoTracking()
            .Where(m => m.Ativo
                        && (m.ProfissionalId == null
                            || (profissionalId != null && m.ProfissionalId == profissionalId)))
            // Os da CLÍNICA primeiro: é o padrão combinado, e quem abre a lista pela
            // primeira vez deve encontrá-lo antes dos atalhos pessoais de alguém.
            .OrderBy(m => m.ProfissionalId == null ? 0 : 1)
            .ThenBy(m => m.Ordem).ThenBy(m => m.Nome)
            .ToListAsync(ct);

    public Task<ModeloEvolucao?> ObterModeloEvolucaoAsync(int modeloId, CancellationToken ct = default)
        => _db.ModelosEvolucao.FirstOrDefaultAsync(m => m.Id == modeloId, ct);

    public Task<ModeloEvolucao?> ObterModeloEvolucaoPorNomeAsync(
        int? profissionalId, string nome, CancellationToken ct = default)
        => _db.ModelosEvolucao.FirstOrDefaultAsync(
            m => m.ProfissionalId == profissionalId && m.Nome == nome, ct);

    public async Task AdicionarModeloEvolucaoAsync(
        ModeloEvolucao modelo, CancellationToken ct = default)
        => await _db.ModelosEvolucao.AddAsync(modelo, ct);

    public async Task RemoverModeloEvolucaoAsync(int modeloId, CancellationToken ct = default)
    {
        if (await _db.ModelosEvolucao.FirstOrDefaultAsync(m => m.Id == modeloId, ct) is { } m)
            _db.ModelosEvolucao.Remove(m);
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

    // ---- Termo assinado pelo paciente (parcela 66) ----

    public async Task<IReadOnlyList<ExigenciaTermoProcedimento>> ExigenciasTermoAsync(
        CancellationToken ct = default)
        => await _db.ExigenciasTermo.AsNoTracking()
            .Include(x => x.Modelo)
            .OrderBy(x => x.Modalidade).ThenBy(x => x.ModalidadeCodigo)
            .ToListAsync(ct);

    public Task<ExigenciaTermoProcedimento?> ObterExigenciaTermoAsync(
        int exigenciaId, CancellationToken ct = default)
        => _db.ExigenciasTermo
            .Include(x => x.Modelo)
            .FirstOrDefaultAsync(x => x.Id == exigenciaId, ct);

    public async Task AdicionarExigenciaTermoAsync(
        ExigenciaTermoProcedimento exigencia, CancellationToken ct = default)
        => await _db.ExigenciasTermo.AddAsync(exigencia, ct);

    public async Task<IReadOnlyList<DocumentoClinico>> TermosDoPacienteNaDataAsync(
        int pacienteId, DateOnly data, CancellationToken ct = default)
        => await _db.DocumentosClinicos.AsNoTracking()
            .Include(d => d.Itens)
            .Where(d => d.PacienteId == pacienteId
                        && d.Data == data
                        && d.Tipo == TipoDocumentoClinico.TermoProcedimento)
            .OrderByDescending(d => d.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentoClinico>> TermosDaDataAsync(
        DateOnly data, CancellationToken ct = default)
        => await _db.DocumentosClinicos.AsNoTracking()
            .Include(d => d.Itens)
            .Where(d => d.Data == data
                        && d.Tipo == TipoDocumentoClinico.TermoProcedimento)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentoClinico>> TermosDoPacienteAsync(
        int pacienteId, CancellationToken ct = default)
        => await _db.DocumentosClinicos.AsNoTracking()
            .Include(d => d.Itens)
            .Where(d => d.PacienteId == pacienteId
                        && d.Tipo == TipoDocumentoClinico.TermoProcedimento)
            .OrderByDescending(d => d.Data).ThenByDescending(d => d.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentoClinico>> TermosDosPacientesAsync(
        IReadOnlyCollection<int> pacienteIds, CancellationToken ct = default)
    {
        if (pacienteIds.Count == 0) return [];

        return await _db.DocumentosClinicos.AsNoTracking()
            .Include(d => d.Itens)
            .Where(d => pacienteIds.Contains(d.PacienteId)
                        && d.Tipo == TipoDocumentoClinico.TermoProcedimento)
            .ToListAsync(ct);
    }

    // Sem AsNoTracking e sem Include no documento: quem pede o traço quer os BYTES, e é a
    // única leitura que os traz — arrastá-los em qualquer outra consulta faria a listagem
    // de documentos carregar uma imagem por linha.
    public Task<TracoAssinatura?> ObterTracoAssinaturaAsync(
        int tracoId, CancellationToken ct = default)
        => _db.TracosAssinatura.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tracoId, ct);

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
            // ⚠️ REALIZADO, não só "com atendimento" (parcela 70): com a guia nascendo na
            // MARCAÇÃO, o horário marcado — e até o cancelado, que mantém o AtendimentoId
            // com as guias suspensas — passou a ter atendimento sem a sessão ter
            // acontecido. Este é o alimentador do REPASSE, e a regra "valor por
            // atendimento" pagaria sessão que ninguém deu. No regime antigo o filtro não
            // muda nada: AtendimentoId só nascia junto do Realizado.
            .Where(a => a.Status == StatusAgendamento.Realizado)
            .Where(a => a.AtendimentoId != null && a.ProfissionalId != null)
            .OrderBy(a => a.DataHora)
            .ToListAsync(ct);
    }

    // ---- Taxas de cartao (parcela 9) ----

    public async Task<IReadOnlyList<TaxaCartao>> TaxasCartaoAsync(
        bool somenteAtivas = false, CancellationToken ct = default)
        => await _db.TaxasCartao.AsNoTracking()
            .Where(t => !somenteAtivas || t.Ativa)
            .OrderBy(t => t.Adquirente)
            .ThenBy(t => t.Modalidade)
            .ThenBy(t => t.ParcelasDe)
            .ToListAsync(ct);

    public Task<TaxaCartao?> ObterTaxaCartaoAsync(int taxaId, CancellationToken ct = default)
        => _db.TaxasCartao.FirstOrDefaultAsync(t => t.Id == taxaId, ct);

    public async Task AdicionarTaxaCartaoAsync(TaxaCartao taxa, CancellationToken ct = default)
        => await _db.TaxasCartao.AddAsync(taxa, ct);

    public async Task RemoverTaxaCartaoAsync(int taxaId, CancellationToken ct = default)
    {
        var taxa = await _db.TaxasCartao.FirstOrDefaultAsync(t => t.Id == taxaId, ct);
        if (taxa is not null) _db.TaxasCartao.Remove(taxa);
    }

    // ---- Rentabilidade por convenio (parcela 19) ----

    public async Task<IReadOnlyList<LancamentoFinanceiro>> LancamentosDeCodigosAsync(
        IReadOnlyCollection<int> codigoIds, CancellationToken ct = default)
    {
        if (codigoIds.Count == 0) return [];
        return await _db.Lancamentos.AsNoTracking()
            .Where(l => l.CodigoFaturamentoId != null
                     && codigoIds.Contains(l.CodigoFaturamentoId.Value)
                     && l.Status != StatusLancamento.Cancelado)
            .ToListAsync(ct);
    }

    // ---- Custo de transacao, visao da direcao (parcela 17) ----

    public async Task<IReadOnlyList<Clinica.Application.Modelos.RecebimentoComDeducao>>
        RecebimentosComDeducaoAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => await _db.Lancamentos.AsNoTracking()
            .Where(l => l.Tipo == TipoLancamento.Entrada)
            .Where(l => l.Status == StatusLancamento.Realizado)
            // Pelo dia do PAGAMENTO: o custo de maquininha pertence ao mes da venda.
            .Where(l => (l.DataPagamento ?? l.Data) >= inicio && (l.DataPagamento ?? l.Data) <= fim)
            .Select(l => new Clinica.Application.Modelos.RecebimentoComDeducao(
                l.DataPagamento ?? l.Data, l.Valor, l.ValorTaxa, l.ValorImposto, l.Adquirente)
            {
                ConvenioCodigo = l.ConvenioCodigo
            })
            .ToListAsync(ct);

    // ---- Recebiveis de cartao (parcela 16) ----

    public async Task<IReadOnlyList<LancamentoFinanceiro>> RecebiveisEmAbertoAsync(
        DateOnly ate, CancellationToken ct = default)
        => await _db.Lancamentos.AsNoTracking()
            .Where(l => l.Status != StatusLancamento.Cancelado)
            .Where(l => l.PrevisaoRecebimento != null && l.PrevisaoRecebimento <= ate)
            .Where(l => l.RecebimentoConfirmadoEm == null)
            .OrderBy(l => l.PrevisaoRecebimento).ThenBy(l => l.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LancamentoFinanceiro>> RecebiveisConfirmadosAsync(
        DateOnly de, DateOnly ate, CancellationToken ct = default)
        => await _db.Lancamentos.AsNoTracking()
            .Where(l => l.Status != StatusLancamento.Cancelado)
            .Where(l => l.RecebimentoConfirmadoEm != null
                        && l.RecebimentoConfirmadoEm >= de
                        && l.RecebimentoConfirmadoEm <= ate)
            .OrderByDescending(l => l.RecebimentoConfirmadoEm).ThenBy(l => l.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LancamentoFinanceiro>> LancamentosPorIdAsync(
        IReadOnlyCollection<int> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];
        // RASTREADOS de proposito (sem AsNoTracking): a confirmacao do deposito escreve
        // nestas entidades.
        return await _db.Lancamentos.Where(l => ids.Contains(l.Id)).ToListAsync(ct);
    }

    // ---- Tabela de preco por convenio (parcela 20) ----

    public async Task<IReadOnlyList<PrecoConvenio>> PrecosConvenioAsync(
        bool somenteAtivos = false, CancellationToken ct = default)
        => await _db.PrecosConvenio.AsNoTracking()
            .Where(p => !somenteAtivos || p.Ativo)
            .OrderBy(p => p.ConvenioCodigo)
            .ThenBy(p => p.Tipo)
            .ThenBy(p => p.Especialidade)
            .ToListAsync(ct);

    public Task<PrecoConvenio?> ObterPrecoConvenioAsync(int precoId, CancellationToken ct = default)
        => _db.PrecosConvenio.FirstOrDefaultAsync(p => p.Id == precoId, ct);

    public async Task AdicionarPrecoConvenioAsync(PrecoConvenio preco, CancellationToken ct = default)
        => await _db.PrecosConvenio.AddAsync(preco, ct);

    public async Task RemoverPrecoConvenioAsync(int precoId, CancellationToken ct = default)
    {
        var preco = await _db.PrecosConvenio.FirstOrDefaultAsync(p => p.Id == precoId, ct);
        if (preco is not null) _db.PrecosConvenio.Remove(preco);
    }

    // ---- Metas da direcao (parcela 28) ----

    public async Task<IReadOnlyList<MetaMensal>> MetasDoAnoAsync(
        int ano, CancellationToken ct = default)
        => await _db.Metas.AsNoTracking()
            .Include(m => m.Profissional)
            .Where(m => m.Ano == ano)
            .OrderBy(m => m.Mes)
            .ThenBy(m => m.Indicador)
            .ToListAsync(ct);

    public Task<MetaMensal?> ObterMetaAsync(
        int ano, int mes, IndicadorMeta indicador, int? profissionalId,
        CancellationToken ct = default)
        => _db.Metas.FirstOrDefaultAsync(
            m => m.Ano == ano && m.Mes == mes && m.Indicador == indicador
                 && m.ProfissionalId == profissionalId, ct);

    public Task<MetaMensal?> ObterMetaPorIdAsync(int metaId, CancellationToken ct = default)
        => _db.Metas.FirstOrDefaultAsync(m => m.Id == metaId, ct);

    public async Task AdicionarMetaAsync(MetaMensal meta, CancellationToken ct = default)
        => await _db.Metas.AddAsync(meta, ct);

    public async Task RemoverMetaAsync(int metaId, CancellationToken ct = default)
    {
        var meta = await _db.Metas.FirstOrDefaultAsync(m => m.Id == metaId, ct);
        if (meta is not null) _db.Metas.Remove(meta);
    }

    // ---- Teto de gasto por categoria (parcela 31) ----

    public async Task<IReadOnlyList<OrcamentoCategoria>> OrcamentosDoMesAsync(
        int ano, int mes, CancellationToken ct = default)
        => await _db.Orcamentos.AsNoTracking()
            .Include(o => o.Categoria)
            .Where(o => o.Ano == ano && o.Mes == mes)
            .OrderBy(o => o.Categoria!.Ordem)
            .ThenBy(o => o.Categoria!.Nome)
            .ToListAsync(ct);

    public Task<OrcamentoCategoria?> ObterOrcamentoAsync(
        int ano, int mes, int categoriaId, CancellationToken ct = default)
        => _db.Orcamentos.FirstOrDefaultAsync(
            o => o.Ano == ano && o.Mes == mes && o.CategoriaFinanceiraId == categoriaId, ct);

    public Task<OrcamentoCategoria?> ObterOrcamentoPorIdAsync(
        int orcamentoId, CancellationToken ct = default)
        => _db.Orcamentos.Include(o => o.Categoria)
            .FirstOrDefaultAsync(o => o.Id == orcamentoId, ct);

    public async Task AdicionarOrcamentoAsync(
        OrcamentoCategoria orcamento, CancellationToken ct = default)
        => await _db.Orcamentos.AddAsync(orcamento, ct);

    public async Task RemoverOrcamentoAsync(int orcamentoId, CancellationToken ct = default)
    {
        var orcamento = await _db.Orcamentos.FirstOrDefaultAsync(o => o.Id == orcamentoId, ct);
        if (orcamento is not null) _db.Orcamentos.Remove(orcamento);
    }

    // ---- Regime tributario (parcela 15) ----

    public async Task<IReadOnlyList<Tributo>> TributosAsync(
        bool somenteAtivos = false, CancellationToken ct = default)
        => await _db.Tributos.AsNoTracking()
            .Where(t => !somenteAtivos || t.Ativo)
            .OrderBy(t => t.Sigla)
            .ThenBy(t => t.VigenteDe)
            .ToListAsync(ct);

    public Task<Tributo?> ObterTributoAsync(int tributoId, CancellationToken ct = default)
        => _db.Tributos.FirstOrDefaultAsync(t => t.Id == tributoId, ct);

    public async Task AdicionarTributoAsync(Tributo tributo, CancellationToken ct = default)
        => await _db.Tributos.AddAsync(tributo, ct);

    public async Task RemoverTributoAsync(int tributoId, CancellationToken ct = default)
    {
        var tributo = await _db.Tributos.FirstOrDefaultAsync(t => t.Id == tributoId, ct);
        if (tributo is not null) _db.Tributos.Remove(tributo);
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

    public async Task<IReadOnlyList<MovimentoEstoque>> ConsumosDeSessaoNoPeriodoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => await _db.MovimentosEstoque.AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Paciente)
            .Where(m => m.AtendimentoId != null && m.Tipo != TipoMovimentoEstoque.Entrada)
            .Where(m => m.Data >= inicio && m.Data <= fim)
            .OrderByDescending(m => m.Data).ThenByDescending(m => m.Id)
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
            .Select(m => new { m.ItemEstoqueId, m.Tipo, m.Quantidade, m.AjusteParaCima })
            .ToListAsync(ct);

        return movimentos
            .GroupBy(m => m.ItemEstoqueId)
            .ToDictionary(
                g => g.Key,
                // A regra do sinal (inclusive a direção do AJUSTE, parcela 30) mora no
                // DOMÍNIO — `MovimentoEstoque.DeltaDe` — porque agora há DOIS somadores:
                // este saldo e o extrato do item. Duas cópias da mesma conta divergiriam
                // exatamente no ajuste, que é o movimento raro que ninguém testa de cabeça.
                g => g.Sum(m => MovimentoEstoque.DeltaDe(m.Tipo, m.AjusteParaCima, m.Quantidade)));
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

    public async Task<IReadOnlyList<EventoAuditoria>> ConsultarAuditoriaAsync(
        Clinica.Application.Modelos.FiltroAuditoria filtro, CancellationToken ct = default)
    {
        var q = _db.Auditoria.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro.Acao))
        {
            // PREFIXO na acao: "Conta" acha ContaCriada e ContaReagendada. As acoes sao
            // nomes compostos por familia, e o prefixo e a forma natural de pedir a familia.
            var acao = filtro.Acao.Trim();
            q = q.Where(e => e.Acao.StartsWith(acao));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Operador))
        {
            var operador = filtro.Operador.Trim();
            q = q.Where(e => e.Operador.Contains(operador));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Termo))
        {
            var termo = filtro.Termo.Trim();
            q = q.Where(e => e.Detalhe != null && e.Detalhe.Contains(termo));
        }

        // DataHora e DateTime (hora de parede); o filtro vem em DateOnly, e o dia final
        // entra INTEIRO — senao um evento das 14h de hoje ficaria fora de um filtro que
        // pede "ate hoje".
        if (filtro.Inicio is { } inicio)
        {
            var de = inicio.ToDateTime(TimeOnly.MinValue);
            q = q.Where(e => e.DataHora >= de);
        }
        if (filtro.Fim is { } fim)
        {
            var ate = fim.ToDateTime(TimeOnly.MaxValue);
            q = q.Where(e => e.DataHora <= ate);
        }

        if (filtro.PacienteId is { } paciente) q = q.Where(e => e.PacienteId == paciente);
        if (filtro.CodigoId is { } codigo) q = q.Where(e => e.CodigoId == codigo);

        return await q
            .OrderByDescending(e => e.DataHora).ThenByDescending(e => e.Id)
            .Take(filtro.Limite > 0 ? filtro.Limite : 300)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> AcoesDeAuditoriaAsync(CancellationToken ct = default)
        => await _db.Auditoria.AsNoTracking()
            .Select(e => e.Acao)
            .Distinct()
            .OrderBy(a => a)
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
            // A receita de GUIA fica de fora: quem pagou foi a OPERADORA, e este método
            // alimenta a sugestão de cobrança do fechamento da sessão. Sem o filtro, o
            // primeiro depósito do convênio conciliado virava "Cobrar R$ X" pré-marcado
            // na sessão seguinte do paciente — cobrança em dobro de quem é de convênio.
            // O mesmo vale para a forma de pagamento Convênio lançada à mão no Caixa.
            .Where(l => l.CodigoFaturamentoId == null)
            .Where(l => l.FormaPagamento != FormaPagamento.Convenio)
            .OrderByDescending(l => l.Data).ThenByDescending(l => l.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Clinica.Application.Modelos.ValorLancamento>>
        ValoresDeLancamentoNoPeriodoAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => await _db.Lancamentos.AsNoTracking()
            .Where(l => l.Data >= inicio && l.Data <= fim)
            .Select(l => new Clinica.Application.Modelos.ValorLancamento(
                l.Tipo, l.Status, l.Valor, l.ValorTaxa, l.ValorImposto))
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


    public async Task<IReadOnlyList<Clinica.Application.Modelos.LancamentoDatado>>
        LancamentosDatadosNoPeriodoAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => await _db.Lancamentos.AsNoTracking()
            // Cancelado nunca entra no fluxo: não é dinheiro, e somá-lo inflaria tanto a
            // receita quanto a despesa do mês em que ele foi lançado.
            .Where(l => l.Status != StatusLancamento.Cancelado)
            // O OU das três datas é o que faz a linha aparecer no mês certo: um lançamento
            // com competência em julho e vencimento em agosto pertence aos dois fluxos, e
            // filtrar por uma data só o faria sumir de um deles.
            .Where(l => (l.Data >= inicio && l.Data <= fim)
                     || (l.DataVencimento != null && l.DataVencimento >= inicio && l.DataVencimento <= fim)
                     || (l.DataPagamento != null && l.DataPagamento >= inicio && l.DataPagamento <= fim))
            .Select(l => new Clinica.Application.Modelos.LancamentoDatado(
                l.Data, l.DataVencimento, l.DataPagamento,
                l.Tipo, l.Status, l.Valor,
                l.Categoria != null ? l.Categoria.Nome : null)
            {
                CategoriaId = l.CategoriaFinanceiraId
            })
            .ToListAsync(ct);

    // ---- Fechamento de caixa (parcela 14) ----

    public Task<IReadOnlyList<Clinica.Application.Modelos.LancamentoEspecie>>
        LancamentosEmEspecieDoDiaAsync(DateOnly dia, CancellationToken ct = default)
        => LancamentosEmEspecieNoPeriodoAsync(dia, dia, ct);

    public async Task<IReadOnlyList<Clinica.Application.Modelos.LancamentoEspecie>>
        LancamentosEmEspecieNoPeriodoAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => await _db.Lancamentos.AsNoTracking()
            // Só REALIZADO e só DINHEIRO: previsto não passou pela gaveta, e cartão/PIX
            // caem na conta dias depois. Somá-los faria a conferência nunca bater — e
            // conferência que nunca bate treina a clínica a clicar "OK" sem olhar.
            .Where(l => l.Status == StatusLancamento.Realizado)
            .Where(l => l.FormaPagamento == FormaPagamento.Dinheiro)
            .Where(l => (l.DataPagamento ?? l.Data) >= inicio && (l.DataPagamento ?? l.Data) <= fim)
            .Select(l => new Clinica.Application.Modelos.LancamentoEspecie(
                l.DataPagamento ?? l.Data, l.Tipo, l.Valor))
            .ToListAsync(ct);

    public Task<FechamentoCaixa?> FechamentoCaixaDoDiaAsync(DateOnly dia, CancellationToken ct = default)
        // O ÚLTIMO do dia: reabrir guarda o anterior e grava outro por cima, e o que vale
        // é sempre o mais recente. Rastreado (sem AsNoTracking) porque a reabertura marca
        // justamente este.
        => _db.FechamentosCaixa
            .Where(f => f.Data == dia)
            .OrderByDescending(f => f.Id)
            .FirstOrDefaultAsync(ct);

    public async Task AdicionarFechamentoCaixaAsync(FechamentoCaixa fechamento, CancellationToken ct = default)
        => await _db.FechamentosCaixa.AddAsync(fechamento, ct);

    public async Task<IReadOnlyList<FechamentoCaixa>> FechamentosCaixaNoPeriodoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => await _db.FechamentosCaixa.AsNoTracking()
            .Where(f => f.Data >= inicio && f.Data <= fim)
            .OrderByDescending(f => f.Data).ThenByDescending(f => f.Id)
            .ToListAsync(ct);

    // ---- Contas a pagar e a receber (parcela 12) ----

    public async Task<IReadOnlyList<LancamentoFinanceiro>> LancamentosComVencimentoAteAsync(
        DateOnly ate, TipoLancamento? tipo = null, CancellationToken ct = default)
    {
        var q = _db.Lancamentos
            .Include(l => l.Categoria)
            .Include(l => l.Paciente)
            .Where(l => l.Status == StatusLancamento.Previsto)
            .Where(l => l.DataVencimento != null && l.DataVencimento <= ate);

        if (tipo is { } t) q = q.Where(l => l.Tipo == t);

        // Do vencimento mais antigo para o mais novo: é a ordem em que se paga, e a
        // vencida tem de vir no topo sem que a tela precise reordenar.
        return await q
            .OrderBy(l => l.DataVencimento).ThenBy(l => l.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> OrigensRecorrenciaExistentesAsync(
        IReadOnlyCollection<string> origens, CancellationToken ct = default)
    {
        if (origens.Count == 0) return [];
        return await _db.Lancamentos.AsNoTracking()
            .Where(l => l.OrigemRecorrencia != null && origens.Contains(l.OrigemRecorrencia))
            .Select(l => l.OrigemRecorrencia!)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LancamentoRecorrente>> RecorrentesAsync(
        bool somenteAtivas = false, CancellationToken ct = default)
    {
        var q = _db.Recorrentes.Include(r => r.Categoria).AsQueryable();
        if (somenteAtivas) q = q.Where(r => r.Ativa);
        return await q.OrderBy(r => r.Descricao).ToListAsync(ct);
    }

    public async Task AdicionarRecorrenteAsync(LancamentoRecorrente recorrente, CancellationToken ct = default)
        => await _db.Recorrentes.AddAsync(recorrente, ct);

    public async Task<LancamentoRecorrente?> ObterRecorrenteAsync(int recorrenteId, CancellationToken ct = default)
        => await _db.Recorrentes.FirstOrDefaultAsync(r => r.Id == recorrenteId, ct);

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

    // Uma consulta só para o BI e para o consultório. O filtro de profissional vai no
    // SQL, e a evolução SEM profissional entra junto: ela é a sessão escrita antes de a
    // clínica cadastrar a equipe.
    public async Task<IReadOnlyList<Evolucao>> EvolucoesNoPeriodoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default, int? profissionalId = null)
    {
        var consulta = _db.Evolucoes.AsNoTracking()
            .Include(e => e.Profissional)
            .Where(e => e.Data >= inicio && e.Data <= fim);

        if (profissionalId is { } id)
            consulta = consulta.Where(e => e.ProfissionalId == id || e.ProfissionalId == null);

        return await consulta
            .OrderBy(e => e.Data).ThenBy(e => e.Id)
            .ToListAsync(ct);
    }

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

    // Aqui a ordem é a oposta da fila: é HISTÓRICO, e histórico se lê do mais novo para
    // o mais velho. O corte vai no SQL, como manda a convenção do projeto.
    public async Task<IReadOnlyList<ContatoCampanha>> ContatosDoPacienteAsync(
        int pacienteId, int limite = 20, CancellationToken ct = default)
        => await _db.Contatos.AsNoTracking()
            .Where(c => c.PacienteId == pacienteId)
            .OrderByDescending(c => c.Referencia)
            .ThenByDescending(c => c.Id)
            .Take(limite)
            .ToListAsync(ct);

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
        catch (DbUpdateException ex) when (Traduzir(ex) is { } amigavel)
        {
            throw new InvalidOperationException(amigavel, ex);
        }
    }

    /// <summary>
    /// Traduz a falha de gravação para o que se pode fazer a respeito, ou devolve `null`
    /// para deixá-la subir como está.
    ///
    /// O texto mora em <see cref="MensagensDeErro"/>, que é testado sozinho: a ordem das
    /// regras de duplicidade é o que mais erra ali, e mensagem plausível e errada é pior
    /// que o texto cru do Postgres — ninguém desconfia dela.
    /// </summary>
    private static string? Traduzir(DbUpdateException ex)
    {
        // Registro gravado por uma versão mais nova (ver ConversorEnumTolerante). A frase já
        // vem escrita para quem está na tela; sem isto o que apareceria é o "An error
        // occurred while saving the entity changes" do EF.
        if (ex.GetBaseException() is RegistroDeVersaoMaisNovaException recusa)
            return recusa.Message;

        if (ex.GetBaseException() is Npgsql.PostgresException pg)
        {
            if (pg.SqlState == "23505")                      // unique_violation
                return MensagensDeErro.Duplicidade(pg.ConstraintName);

            if (pg.SqlState == "23503")                      // foreign_key_violation
                return MensagensDeErro.VinculoQuebrado;
        }

        // O banco é remoto e a internet do consultório oscila: este é o caso mais comum.
        if (ex.GetBaseException() is System.Net.Sockets.SocketException or TimeoutException
            || ex.GetBaseException() is Npgsql.NpgsqlException { IsTransient: true })
            return MensagensDeErro.SemConexao;

        // ⚠️ Sem classificação, o que chegava à tela era a frase do EF: "An error occurred
        // while saving the entity changes. See the inner exception for details." Ela não diz
        // NADA a quem está no balcão e esconde justamente a linha que resolve — a do
        // Postgres, com a coluna, a restrição e o valor. Foi assim que ela apareceu na
        // clínica em 14/08/2026, depois de uma assinatura que tinha dado certo.
        //
        // "Veja a inner exception" é uma instrução para o programador impressa na cara do
        // usuário. Levar a causa junto é a mesma lição que a assinatura em nuvem custou seis
        // rodadas para ensinar: mensagem de erro que carrega a evidência substitui a próxima
        // rodada de adivinhação.
        var causa = ex.GetBaseException().Message.Trim();

        if (causa.Length == 0 || causa == ex.Message) return null;

        if (causa.Length > 400) causa = causa[..400] + "…";

        return $"Não foi possível gravar. O banco respondeu: {causa}";
    }
}
