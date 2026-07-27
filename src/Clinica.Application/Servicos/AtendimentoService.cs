using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>Resultado do lançamento de um atendimento: o atendimento criado e os avisos para a secretária.</summary>
public sealed record ResultadoLancamento(Atendimento Atendimento, IReadOnlyList<string> Avisos);

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
        string? especialidadeConsultaCodigo = null)
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
            Observacoes = observacoes
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
}
