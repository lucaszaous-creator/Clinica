using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>Resultado do lançamento de um atendimento: o atendimento criado e os avisos para a secretária.</summary>
public sealed record ResultadoLancamento(Atendimento Atendimento, IReadOnlyList<string> Avisos);

/// <summary>
/// O que a regra do convênio VAI gerar, sem ter gerado ainda (parcela 47).
///
/// Os códigos aqui são objetos transitórios: nasceram do mesmo motor de regras que o
/// lançamento usa, mas nunca passaram pelo repositório. Não têm Id, e não devem ser
/// gravados por ninguém — quem grava é <see cref="AtendimentoService.LancarAsync"/>.
/// </summary>
public sealed record PreviaLancamento(
    IReadOnlyList<CodigoFaturamento> Codigos,
    Categoria Categoria,
    IReadOnlyList<string> Avisos)
{
    /// <summary>Guias que já nascem faturáveis na data do atendimento.</summary>
    public int GuiasHoje => Codigos.Count(c => c.Ordem == OrdemCodigo.Primeiro
                                               && c.Status != StatusCodigo.NaoAplicavel);

    /// <summary>
    /// Guias que só liberam depois (o 2º código, +24h). É o número que dá nome ao produto:
    /// de 139 faturas possíveis, 103 se perdiam exatamente aqui.
    /// </summary>
    public int GuiasDepois => Codigos.Count(c => c.Ordem == OrdemCodigo.Segundo
                                                 && c.Status != StatusCodigo.NaoAplicavel);

    /// <summary>Quando a última guia deste atendimento libera. Nulo quando tudo é de hoje.</summary>
    public DateOnly? LiberaEm => Codigos
        .Where(c => c.Ordem == OrdemCodigo.Segundo && c.Status != StatusCodigo.NaoAplicavel)
        .Select(c => (DateOnly?)c.DataPrevistaFaturamento)
        .OrderByDescending(d => d)
        .FirstOrDefault();
}

/// <summary>
/// Lança um atendimento e gera automaticamente os códigos de faturamento pela regra do convênio.
/// É aqui que o 2º código nasce já com data prevista de +24h — para nunca ser esquecido.
/// </summary>
public sealed class AtendimentoService
{
    private readonly IClinicaRepositorio _repo;
    private readonly RegistroRegras _regras;

    private readonly ParametrosService? _parametros;
    private readonly ConsultaService? _consultas;

    public AtendimentoService(IClinicaRepositorio repo, RegistroRegras? regras = null,
        ParametrosService? parametros = null, ConsultaService? consultas = null)
    {
        _repo = repo;
        _regras = regras ?? new RegistroRegras();
        _parametros = parametros;
        _consultas = consultas;
    }

    public async Task<ResultadoLancamento> LancarAsync(
        int pacienteId, DateOnly data, ModalidadeAtendimento modalidade, string? observacoes = null,
        CancellationToken ct = default, bool registrarNaAgenda = false, TipoCodigo? primeiroCodigo = null,
        Especialidade? especialidadeConsulta = null, string? modalidadeCodigo = null,
        string? especialidadeConsultaCodigo = null, string? operador = null)
    {
        var paciente = await _repo.ObterPacienteAsync(pacienteId, ct)
            ?? throw new InvalidOperationException($"Paciente {pacienteId} não encontrado.");

        // Variante do catálogo: a base (comportamento no motor de regras) vem do código.
        // Sem código, usa o enum recebido (chamadas legadas e modalidades embutidas).
        if (modalidadeCodigo is not null)
            modalidade = CatalogoModalidades.Base(modalidadeCodigo);

        var ehConsulta = modalidade == ModalidadeAtendimento.Consulta;
        var especialidadeEnum = ehConsulta
            ? especialidadeConsulta ?? CatalogoEspecialidades.BaseEnum(especialidadeConsultaCodigo)
            : null;

        var atendimento = new Atendimento
        {
            PacienteId = pacienteId,
            Data = data,
            Modalidade = modalidade,
            ModalidadeCodigo = modalidadeCodigo ?? modalidade.ToString(),
            EspecialidadeConsulta = especialidadeEnum,
            EspecialidadeConsultaCodigo = ehConsulta
                ? especialidadeConsultaCodigo ?? especialidadeConsulta?.ToString()
                : null,
            Observacoes = observacoes,
            // Quem lançou, e quando. Vem do chamador (`SessaoUsuario.Atual.Operador` na
            // tela): este serviço é compartilhado com o faturamento e não lê a sessão.
            LancadoPor = string.IsNullOrWhiteSpace(operador) ? null : operador.Trim(),
            LancadoEm = DateTime.Now
        };

        var historicoMes = await _repo.CodigosDoPacienteNoMesAsync(pacienteId, data.Year, data.Month, ct);

        // Convênio personalizado: a config (inclusive dias do 2º código) vem do catálogo.
        var generica = paciente.Convenio == Convenio.Personalizado
            ? CatalogoConvenios.Config(paciente.ConvenioCodigo)
            : null;
        var dias = generica?.DiasSegundoCodigo
            ?? (_parametros is null ? 1 : (await _parametros.ObterAsync(ct)).DiasSegundoCodigo(paciente.Convenio));

        var contexto = new ContextoFaturamento
        {
            CodigosNoMes = historicoMes,
            DiasSegundoCodigo = dias,
            PrimeiroCodigoPreferido = primeiroCodigo,
            Generica = generica
        };

        var resultado = _regras.Para(paciente.Convenio).Gerar(paciente, atendimento, contexto);

        atendimento.Categoria = resultado.Categoria;
        atendimento.Codigos.AddRange(resultado.Codigos);

        // Mantém a categoria mais recente na ficha do paciente.
        paciente.Categoria = resultado.Categoria;

        await _repo.AdicionarAtendimentoAsync(atendimento, ct);
        await _repo.SalvarAsync(ct);

        // Número/protocolo do atendimento (o Id já existe após salvar) — base do lastro de faturamento.
        atendimento.Numero = $"{data.Year}-{atendimento.Id:D6}";
        await _repo.SalvarAsync(ct);

        // Paciente voltou: as não conformidades dele voltam a ser pendência (o motivo de estarem
        // paradas — "aguardando o paciente" — deixou de valer). Avisa a secretária para cobrar agora.
        var naoConformidades = await _repo.CodigosEmNaoConformidadeDoPacienteAsync(pacienteId, ct);
        if (naoConformidades.Count > 0)
        {
            foreach (var nc in naoConformidades)
            {
                nc.ReabrirNaoConformidade();
                await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
                {
                    Operador = "sistema",
                    Acao = "NaoConformidadeReaberta",
                    Detalhe = $"Reaberta automaticamente: paciente retornou (atendimento {atendimento.Numero})",
                    CodigoId = nc.Id,
                    PacienteId = pacienteId
                }, ct);
            }
            await _repo.SalvarAsync(ct);
            resultado.Avisos.Add(
                $"Atenção: {naoConformidades.Count} não conformidade(s) deste paciente foi(ram) reaberta(s) — " +
                "o paciente voltou, cobre a(s) guia(s) agora.");
        }

        // Consulta avulsa também reinicia o ciclo de renovação do plano (Unimed 22 dias,
        // Amil/Petrobras 30): registra a renovação no controle de Consultas, que vigia o vencimento.
        if (modalidade == ModalidadeAtendimento.Consulta && _consultas is not null)
        {
            var validade = paciente.Convenio == Convenio.Personalizado
                ? CatalogoConvenios.ValidadeConsultaDias(paciente.ConvenioCodigo)
                : _parametros is null
                    ? ConvenioInfo.ValidadeConsultaDias(paciente.Convenio)
                    : (await _parametros.ObterAsync(ct)).ValidadeConsultaDias(paciente.Convenio);

            if (validade is not null)
            {
                var rotulo = atendimento.EspecialidadeConsultaCodigo is { } espCod
                    ? $"Consulta de {CatalogoEspecialidades.Nome(espCod)}"
                    : "Consulta";
                var consulta = await _consultas.RenovarAsync(
                    pacienteId, data, $"{rotulo} — atendimento {atendimento.Numero}.", ct);
                resultado.Avisos.Add(
                    $"Renovação registrada: a consulta vale {consulta.ValidadeDias} dias e vence em {consulta.DataVencimento:dd/MM/yyyy}.");
            }
        }

        // Lançamento direto (tela "Novo atendimento"): registra também na agenda do dia, já
        // como presença realizada e vinculado ao atendimento, para que o paciente apareça
        // na agenda na data marcada. Quando o atendimento nasce da própria agenda
        // (AgendaService.ConfirmarPresenca), este registro não é criado (evita duplicidade).
        if (registrarNaAgenda)
        {
            var agendamento = new Agendamento
            {
                PacienteId = pacienteId,
                DataHora = DateTime.SpecifyKind(data.ToDateTime(new TimeOnly(9, 0)), DateTimeKind.Unspecified),
                ModalidadePrevista = modalidade,
                ModalidadeCodigo = atendimento.ModalidadeCodigo,
                EspecialidadeConsulta = atendimento.EspecialidadeConsulta,
                EspecialidadeConsultaCodigo = atendimento.EspecialidadeConsultaCodigo,
                Status = StatusAgendamento.Realizado,
                Origem = OrigemAgendamento.Manual,
                AtendimentoId = atendimento.Id,
                Observacoes = observacoes
            };
            await _repo.AdicionarAgendamentoAsync(agendamento, ct);
            await _repo.SalvarAsync(ct);
        }

        return new ResultadoLancamento(atendimento, resultado.Avisos);
    }

    /// <summary>
    /// O que ACONTECERIA se este atendimento fosse lançado agora — sem lançar nada.
    ///
    /// Por que isto existe
    /// -------------------
    /// A tela de lançamento era "preencha e torça": escolhe-se a modalidade num combo,
    /// clica-se, e só então se descobre quantas guias saíram e quando cada uma libera. Só
    /// que o motor de regras é DETERMINÍSTICO e PURO — <c>IRegraConvenio.Gerar</c> não
    /// grava nada —, então a resposta já existia antes do clique; ela só não estava sendo
    /// mostrada. Com a prévia, a recepcionista vê a consequência no momento da DECISÃO,
    /// que é o único momento em que ela ainda pode escolher diferente.
    ///
    /// Isso serve direto ao motivo de o produto existir: o 2º código obtido +24h depois é
    /// o que se esquece, e agora ele aparece com data na tela antes mesmo de nascer.
    ///
    /// O que este método NÃO faz, e é decisão
    /// --------------------------------------
    /// Não grava, não reabre não conformidade, não renova consulta e não registra na
    /// agenda. Tudo isso são EFEITOS do lançamento, e um deles acontecendo numa prévia
    /// seria pior do que não ter prévia nenhuma — a recepcionista trocaria a modalidade
    /// três vezes para comparar e teria renovado a consulta três vezes.
    ///
    /// Ele também não toca em <c>paciente.Categoria</c>, que o lançamento atualiza: o
    /// paciente vem RASTREADO do repositório, e mexer nele aqui faria a categoria vazar
    /// para o banco no próximo SaveChanges de qualquer outra coisa no mesmo escopo.
    /// </summary>
    public async Task<PreviaLancamento> PreverAsync(
        int pacienteId, DateOnly data, ModalidadeAtendimento modalidade,
        CancellationToken ct = default, TipoCodigo? primeiroCodigo = null,
        Especialidade? especialidadeConsulta = null, string? modalidadeCodigo = null,
        string? especialidadeConsultaCodigo = null)
    {
        var paciente = await _repo.ObterPacienteAsync(pacienteId, ct)
            ?? throw new InvalidOperationException($"Paciente {pacienteId} não encontrado.");

        var historicoMes = await _repo.CodigosDoPacienteNoMesAsync(pacienteId, data.Year, data.Month, ct);
        var parametros = _parametros is null ? null : await _parametros.ObterAsync(ct);

        return Simular(paciente, data, modalidade, historicoMes, parametros,
                       primeiroCodigo, especialidadeConsulta, modalidadeCodigo,
                       especialidadeConsultaCodigo);
    }

    /// <summary>
    /// A prévia de VÁRIAS modalidades de uma vez, para a tela mostrar em cada opção o que
    /// ela geraria — "acupuntura + eletro: 2 guias, a 2ª libera 09/08".
    ///
    /// Existe como método próprio por causa do banco, que é REMOTO: chamar
    /// <see cref="PreverAsync"/> cinco vezes custaria cinco idas buscar o mesmo paciente e
    /// o mesmo histórico do mês. Aqui é uma leitura só, e as cinco simulações rodam em
    /// memória — o motor de regras não toca em banco.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, PreviaLancamento>> PreverModalidadesAsync(
        int pacienteId, DateOnly data, IEnumerable<string> modalidadeCodigos,
        CancellationToken ct = default, Especialidade? especialidadeConsulta = null,
        string? especialidadeConsultaCodigo = null)
    {
        var paciente = await _repo.ObterPacienteAsync(pacienteId, ct)
            ?? throw new InvalidOperationException($"Paciente {pacienteId} não encontrado.");

        var historicoMes = await _repo.CodigosDoPacienteNoMesAsync(pacienteId, data.Year, data.Month, ct);
        var parametros = _parametros is null ? null : await _parametros.ObterAsync(ct);

        var previas = new Dictionary<string, PreviaLancamento>();
        foreach (var codigo in modalidadeCodigos.Distinct())
        {
            // Uma modalidade que o catálogo não conhece não derruba as outras: a tela
            // mostra o cartão sem a prévia, que é melhor do que a lista inteira sumir.
            try
            {
                previas[codigo] = Simular(
                    paciente, data, CatalogoModalidades.Base(codigo), historicoMes, parametros,
                    primeiroCodigo: null, especialidadeConsulta, codigo, especialidadeConsultaCodigo);
            }
            catch (Exception ex)
            {
                Diagnostico.Registrar($"Prévia da modalidade {codigo} falhou", ex);
            }
        }

        return previas;
    }

    /// <summary>
    /// O miolo compartilhado entre o lançamento e a prévia: monta o atendimento
    /// transitório, resolve o contexto do convênio e roda a regra. Sem banco.
    /// </summary>
    private PreviaLancamento Simular(
        Paciente paciente, DateOnly data, ModalidadeAtendimento modalidade,
        IReadOnlyList<CodigoFaturamento> historicoMes, ParametrosSnapshot? parametros,
        TipoCodigo? primeiroCodigo, Especialidade? especialidadeConsulta,
        string? modalidadeCodigo, string? especialidadeConsultaCodigo)
    {
        if (modalidadeCodigo is not null)
            modalidade = CatalogoModalidades.Base(modalidadeCodigo);

        var ehConsulta = modalidade == ModalidadeAtendimento.Consulta;
        var especialidadeEnum = ehConsulta
            ? especialidadeConsulta ?? CatalogoEspecialidades.BaseEnum(especialidadeConsultaCodigo)
            : null;

        var atendimento = new Atendimento
        {
            PacienteId = paciente.Id,
            Data = data,
            Modalidade = modalidade,
            ModalidadeCodigo = modalidadeCodigo ?? modalidade.ToString(),
            EspecialidadeConsulta = especialidadeEnum,
            EspecialidadeConsultaCodigo = ehConsulta
                ? especialidadeConsultaCodigo ?? especialidadeConsulta?.ToString()
                : null
        };

        var generica = paciente.Convenio == Convenio.Personalizado
            ? CatalogoConvenios.Config(paciente.ConvenioCodigo)
            : null;
        var dias = generica?.DiasSegundoCodigo
            ?? (parametros is null ? 1 : parametros.DiasSegundoCodigo(paciente.Convenio));

        var contexto = new ContextoFaturamento
        {
            CodigosNoMes = historicoMes,
            DiasSegundoCodigo = dias,
            PrimeiroCodigoPreferido = primeiroCodigo,
            Generica = generica
        };

        var resultado = _regras.Para(paciente.Convenio).Gerar(paciente, atendimento, contexto);
        return new PreviaLancamento(resultado.Codigos, resultado.Categoria, resultado.Avisos);
    }
}