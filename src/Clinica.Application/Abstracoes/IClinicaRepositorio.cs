using Clinica.Domain.Entities;

namespace Clinica.Application.Abstracoes;

/// <summary>Acesso a dados usado pelos serviços. Implementado sobre EF Core na camada de infraestrutura.</summary>
public interface IClinicaRepositorio
{
    Task<Paciente?> ObterPacienteAsync(int pacienteId, CancellationToken ct = default);

    /// <summary>Códigos do paciente lançados no mês informado (usado pela rotação de especialidades da Petrobras).</summary>
    Task<IReadOnlyList<CodigoFaturamento>> CodigosDoPacienteNoMesAsync(int pacienteId, int ano, int mes, CancellationToken ct = default);

    /// <summary>Todos os códigos ainda em aberto (não baixados e não "não aplicável"), com paciente carregado.</summary>
    Task<IReadOnlyList<CodigoFaturamento>> CodigosEmAbertoAsync(CancellationToken ct = default);

    /// <summary>Códigos cujo atendimento ocorreu no período [inicio, fim], com paciente carregado (usado nos relatórios).</summary>
    Task<IReadOnlyList<CodigoFaturamento>> CodigosNoPeriodoAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    /// <summary>Pacientes com seus atendimentos carregados (usado para calcular renovação de consulta).</summary>
    Task<IReadOnlyList<Paciente>> PacientesComAtendimentosAsync(CancellationToken ct = default);

    Task<CodigoFaturamento?> ObterCodigoAsync(int codigoId, CancellationToken ct = default);

    Task AdicionarAtendimentoAsync(Atendimento atendimento, CancellationToken ct = default);

    /// <summary>Atendimento com paciente e códigos carregados (para gerar a capa de faturamento).</summary>
    Task<Atendimento?> ObterAtendimentoAsync(int atendimentoId, CancellationToken ct = default);

    // ---- Busca / ficha do paciente / faturados ----

    /// <summary>Busca pacientes por nome ou CPF (termo normalizado). Termo vazio devolve todos.</summary>
    Task<IReadOnlyList<Paciente>> BuscarPacientesAsync(string? termo, CancellationToken ct = default);

    /// <summary>Paciente com todo o histórico (atendimentos e seus códigos) carregado.</summary>
    Task<Paciente?> ObterPacienteComHistoricoAsync(int pacienteId, CancellationToken ct = default);

    /// <summary>Guias baixadas cujo atendimento ocorreu no período (tela de Faturados).</summary>
    Task<IReadOnlyList<CodigoFaturamento>> CodigosBaixadosNoPeriodoAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    /// <summary>Guias glosadas. Se somenteEmAberto, traz apenas as ainda não recuperadas.</summary>
    Task<IReadOnlyList<CodigoFaturamento>> CodigosGlosadosAsync(bool somenteEmAberto, CancellationToken ct = default);

    /// <summary>Consulta central de guias com filtros combinados (paciente, nº guia, período, status, convênio).</summary>
    Task<IReadOnlyList<CodigoFaturamento>> ConsultarCodigosAsync(Modelos.FiltroConsultaGuias filtro, CancellationToken ct = default);

    Task AdicionarPacienteAsync(Paciente paciente, CancellationToken ct = default);
    Task RemoverPacienteAsync(int pacienteId, CancellationToken ct = default);

    // ---- Agenda ----
    // ---- Parâmetros dos convênios ----
    Task<IReadOnlyList<ParametroConvenio>> ParametrosAsync(CancellationToken ct = default);
    Task SalvarParametroAsync(ParametroConvenio parametro, CancellationToken ct = default);

    /// <summary>Valor da configuração global (chave/valor no banco), ou nulo se nunca salva.</summary>
    Task<string?> ObterConfiguracaoAsync(string chave, CancellationToken ct = default);
    Task SalvarConfiguracaoAsync(string chave, string valor, CancellationToken ct = default);

    /// <summary>Catálogo de convênios (todos, ativos e inativos).</summary>
    Task<IReadOnlyList<ConvenioCadastro>> ConveniosAsync(CancellationToken ct = default);
    Task SalvarConvenioAsync(ConvenioCadastro convenio, CancellationToken ct = default);

    // ---- Consultas (renováveis) ----
    Task AdicionarConsultaAsync(Consulta consulta, CancellationToken ct = default);

    /// <summary>Consultas do paciente, da mais recente para a mais antiga.</summary>
    Task<IReadOnlyList<Consulta>> ConsultasDoPacienteAsync(int pacienteId, CancellationToken ct = default);

    /// <summary>Todos os pacientes com suas consultas carregadas (para a aba de Consultas).</summary>
    Task<IReadOnlyList<Paciente>> PacientesComConsultasAsync(CancellationToken ct = default);

    Task AdicionarAgendamentoAsync(Agendamento agendamento, CancellationToken ct = default);
    Task<Agendamento?> ObterAgendamentoAsync(int agendamentoId, CancellationToken ct = default);
    Task<IReadOnlyList<Agendamento>> AgendamentosNoPeriodoAsync(DateTime inicio, DateTime fim, CancellationToken ct = default);
    Task RemoverAgendamentoAsync(int agendamentoId, CancellationToken ct = default);

    Task<int> SalvarAsync(CancellationToken ct = default);
}
