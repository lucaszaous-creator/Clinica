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

    // ---- Busca / ficha do paciente / faturados ----

    /// <summary>Busca pacientes por nome ou CPF (termo normalizado). Termo vazio devolve todos.</summary>
    Task<IReadOnlyList<Paciente>> BuscarPacientesAsync(string? termo, CancellationToken ct = default);

    /// <summary>Paciente com todo o histórico (atendimentos e seus códigos) carregado.</summary>
    Task<Paciente?> ObterPacienteComHistoricoAsync(int pacienteId, CancellationToken ct = default);

    /// <summary>Guias baixadas cujo atendimento ocorreu no período (tela de Faturados).</summary>
    Task<IReadOnlyList<CodigoFaturamento>> CodigosBaixadosNoPeriodoAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    Task AdicionarPacienteAsync(Paciente paciente, CancellationToken ct = default);
    Task RemoverPacienteAsync(int pacienteId, CancellationToken ct = default);

    Task<int> SalvarAsync(CancellationToken ct = default);
}
