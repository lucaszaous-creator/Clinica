using System.Globalization;
using Clinica.Application.Formatacao;

namespace Clinica.Application.Modelos;

/// <summary>Uma sessão de dia anterior que ficou sem evolução escrita.</summary>
public sealed record LinhaRegistroPendente
{
    public required int AgendamentoId { get; init; }

    /// <summary>Dia do horário — viaja para o posto porque o casamento sessão × evolução cai para paciente + data.</summary>
    public required DateOnly Data { get; init; }
    public required int PacienteId { get; init; }
    public required string Paciente { get; init; }

    /// <summary>"12/10 às 14:30".</summary>
    public required string Quando { get; init; }
    public required string Modalidade { get; init; }

    /// <summary>"ontem" / "há 6 dias".</summary>
    public required string Atraso { get; init; }

    /// <summary>Quem atendeu — vazio quando o agendamento não tem profissional. Alimenta o filtro do modo sem vínculo.</summary>
    public string Profissional { get; init; } = string.Empty;
}

/// <summary>
/// "SESSÕES SEM EVOLUÇÃO" — a dívida de prontuário, o que a tela afirma (out/2026; era o
/// <c>RegistrosPendentesViewModel</c> WPF). O filtro é em memória, sobre o que já foi lido, e o
/// resumo DIZ que está filtrado: a dívida é fila de trabalho, e "3 sessões" que eram 20 faria
/// alguém dar o prontuário por quase em dia.
/// </summary>
public static class RegistrosPendentesNaTela
{
    public const string TodosProfissionais = "Todos os profissionais";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static string Corte => PoliticaRegistroPendente.DataInicio.ToString("dd/MM/yyyy", PtBr);

    public static LinhaRegistroPendente Linha(RegistroPendente r, DateOnly hoje)
    {
        var dias = r.DiasEmAberto(hoje);
        return new LinhaRegistroPendente
        {
            AgendamentoId = r.AgendamentoId,
            Data = DateOnly.FromDateTime(r.DataHora),
            PacienteId = r.PacienteId,
            Paciente = r.PacienteNome,
            Profissional = r.Profissional ?? string.Empty,
            Quando = r.DataHora.ToString("dd/MM 'às' HH:mm", PtBr),
            Modalidade = r.Modalidade,
            Atraso = dias == 1 ? "ontem" : $"há {dias} dias"
        };
    }

    /// <summary>
    /// As opções do combo de profissional: "Todos" + quem TEM dívida na lista, em ordem de nome.
    /// Só no modo SEM VÍNCULO (a lista da clínica): com vínculo a lista já é de um profissional,
    /// e o combo seria uma caixinha que não recorta nada.
    /// </summary>
    public static IReadOnlyList<string> Profissionais(IEnumerable<LinhaRegistroPendente> linhas, bool listaDaClinica)
    {
        var lista = new List<string> { TodosProfissionais };
        if (listaDaClinica)
            lista.AddRange(linhas.Select(l => l.Profissional).Where(n => n.Length > 0).Distinct().OrderBy(n => n, StringComparer.Create(PtBr, false)));
        return lista;
    }

    /// <summary>A escolha sobrevive à releitura só enquanto o profissional continua na lista.</summary>
    public static string ManterEscolha(string? escolhido, IReadOnlyList<string> opcoes)
        => escolhido is not null && opcoes.Contains(escolhido) ? escolhido : TodosProfissionais;

    public static bool FiltroAtivo(string? profissional, string? paciente)
        => (profissional ?? TodosProfissionais) != TodosProfissionais || !string.IsNullOrWhiteSpace(paciente);

    /// <summary>Aplica o filtro (profissional exato + parte do nome, sem acento).</summary>
    public static IReadOnlyList<LinhaRegistroPendente> Filtrar(
        IEnumerable<LinhaRegistroPendente> linhas, string? profissional, string? paciente)
        => linhas.Where(l =>
                ((profissional ?? TodosProfissionais) == TodosProfissionais || l.Profissional == profissional)
                && Busca.Casa(l.Paciente, paciente))
            .ToList();

    public static string Resumo(int total, int visiveis, bool filtroAtivo) => total == 0
        ? $"Nenhuma sessão desde {Corte} está sem evolução escrita."
        : filtroAtivo
            ? $"{visiveis} de {total} sessão(ões) sem evolução no filtro. A mais antiga primeiro."
            : $"{total} sessão(ões) desde {Corte} continuam sem evolução escrita. A mais antiga primeiro.";

    /// <summary>O estado vazio muda de frase com filtro — vazio filtrado não é "prontuário em dia".</summary>
    public static string VazioDescricao(bool filtroAtivo) => filtroAtivo
        ? "Nenhuma sessão bate com o filtro — limpe-o para ver a dívida inteira."
        : $"Toda sessão atendida desde {Corte} já tem evolução escrita.";

    /// <summary>A frase a mais da lista da clínica: é a dívida do MÉDICO, não a passagem da enfermagem.</summary>
    public const string FraseDaListaDaClinica =
        "Esta é a dívida de EVOLUÇÃO da clínica inteira — o registro de quem consulta. A passagem da enfermagem é escrita em “Atendimento de enfermagem”, dentro do paciente.";
}
