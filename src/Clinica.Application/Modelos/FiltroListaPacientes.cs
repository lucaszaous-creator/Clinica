namespace Clinica.Application.Modelos;

/// <summary>
/// Filtros da listagem, aplicados no banco antes do limite da página.
///
/// ⚠️ Valores novos sempre no FIM (out/2026, os recortes do mockup aprovado "3 · Pacientes"):
/// o índice não é gravado, mas a ordem é a que os testes e as telas leem.
/// </summary>
public enum FiltroListaPacientes
{
    Todos,
    CadastroIncompleto,
    EmTratamento,

    /// <summary>Tem horário que ocupa a agenda HOJE (marcado ou realizado).</summary>
    ComHorarioHoje,

    /// <summary>A ficha veio do sistema anterior sem convênio ("a definir").</summary>
    SemConvenioDefinido,

    /// <summary>Faz aniversário de hoje até daqui a seis dias.</summary>
    AniversarioNaSemana
}
