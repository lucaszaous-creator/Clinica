using System.Globalization;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Quantos pacientes cabem em cada recorte da lista — o número de cada chip.</summary>
public sealed record RecortesDePacientes(int Total, int ComHorarioHoje, int CadastroIncompleto, int SemConvenio, int AniversarioNaSemana);

/// <summary>A última visita e o próximo horário de um paciente — as duas colunas da lista.</summary>
public sealed record VisitaDoPaciente(int PacienteId, DateTime? Ultima, string? UltimaAtendimento, DateTime? Proximo, string? ProximoProfissional);

/// <summary>
/// O que a lista de Pacientes AFIRMA em cada linha (out/2026, mockup aprovado "3 · Pacientes").
/// Pura, pela regra da casa: o projeto WPF não compila no <c>dotnet test</c>.
///
/// - **Documento**: CPF formatado quando tem 11 dígitos, senão o que está gravado (RG, por
///   exemplo) — nunca só os dígitos (a armadilha do <c>Cpf.Formatar</c>, parcela 57).
/// - **Idade**: a MESMA regra da ficha (<see cref="IdadeDoPaciente"/>) — data implausível
///   aparece "a conferir", nunca "1851 anos".
/// - **Última visita** é sessão REALIZADA; o que veio do sistema anterior não virou sessão,
///   então "sem visita registrada" é a verdade — e não um zero.
/// </summary>
public static class ListaDePacientes
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Os pares mês·dia (mês × 100 + dia) de hoje até daqui a seis dias.</summary>
    public static IReadOnlyList<int> DiasDoAniversario(DateOnly hoje)
        => Enumerable.Range(0, 7).Select(i => hoje.AddDays(i)).Select(d => d.Month * 100 + d.Day).ToList();

    /// <summary>"CPF 123.456.789-00 · 54 anos" — o que identifica a pessoa sem abrir a ficha.</summary>
    public static string Identificacao(Paciente p, DateOnly hoje)
    {
        var digitos = Cpf.Normalizar(p.Documento);
        var documento = string.IsNullOrWhiteSpace(p.Documento) ? "sem documento"
            : digitos.Length == 11 ? $"CPF {Cpf.Formatar(digitos)}"
            : p.Documento!.Trim();
        var idade = IdadeDoPaciente.Implausivel(p.DataNascimento, hoje)
            ? "data de nascimento a conferir"
            : IdadeDoPaciente.Texto(p.DataNascimento, hoje);
        return idade is null ? documento : $"{documento} · {idade}";
    }

    public static string UltimaVisita(VisitaDoPaciente? v, DateOnly hoje)
    {
        if (v?.Ultima is not { } quando) return "sem visita registrada";
        var data = quando.Year == hoje.Year ? quando.ToString("dd/MM", PtBr) : quando.ToString("dd/MM/yy", PtBr);
        return string.IsNullOrWhiteSpace(v.UltimaAtendimento) ? data : $"{data} · {v.UltimaAtendimento}";
    }

    public static string ProximoHorario(VisitaDoPaciente? v, DateOnly hoje)
    {
        if (v?.Proximo is not { } quando) return "—";
        var dia = DateOnly.FromDateTime(quando);
        var data = (dia.DayNumber - hoje.DayNumber) switch
        {
            0 => $"hoje {quando:HH:mm}",
            1 => $"amanhã {quando:HH:mm}",
            _ => $"{RodadaDeConfirmacao.TituloDoDia(dia, DateOnly.MinValue)}"
        };
        return string.IsNullOrWhiteSpace(v.ProximoProfissional) ? data : $"{data} · {v.ProximoProfissional}";
    }

    /// <summary>"6 pacientes para “maria s”" — o rodapé diz o que está na tela e de onde veio.</summary>
    public static string Rodape(int quantidade, string? termo, string? recorte)
    {
        var conta = quantidade == 1 ? "1 paciente" : $"{quantidade} pacientes";
        if (!string.IsNullOrWhiteSpace(termo)) return $"{conta} para “{termo.Trim()}” · Enter abre a ficha quando sobra um só";
        return string.IsNullOrWhiteSpace(recorte) ? conta : $"{conta} · {recorte}";
    }
}
