using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Clinica.Application.Servicos;

/// <summary>
/// As regras da remarcação pedida pelo PACIENTE (@SemDor, out/2026 — docs/semdor-assistente.md):
/// o paciente toca "Remarcar" no lembrete, a recepção escolhe até 3 horários livres, o paciente
/// recebe os botões no WhatsApp, escolhe, e a recepção remarca com um clique. Aqui moram os limites,
/// a chave da oferta, a leitura do botão que volta e as frases ao paciente — que dizem o que a
/// clínica FEZ, nunca o que ela vai fazer (regra do CRM).
/// </summary>
public static partial class RemarcacaoPeloPacienteRegras
{
    public const string OrigemEquipe = "equipe";
    public const string OrigemPaciente = "paciente";

    /// <summary>O WhatsApp aceita até 3 botões de resposta numa mensagem: é o teto da oferta.</summary>
    public const int MaximoDeOpcoes = 3;

    /// <summary>Quantos horários livres o cartão sugere para a recepção escolher.</summary>
    public const int VagasSugeridas = 6;

    /// <summary>O mesmo horizonte da remarcação pela ponte (ClinicSchedulingBridge): 31 dias.</summary>
    public const int DiasMaximos = 31;

    /// <summary>A partir de quando sugerir: daqui a uma hora (ninguém remarca para "agora").</summary>
    public static DateTime InicioDaBusca(DateTime agora) => agora.AddHours(1);

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>A chave da oferta: 8 letras hexadecimais. Vai nos botões; resposta de oferta velha não vale.</summary>
    public static string NovaChave() => Convert.ToHexString(RandomNumberGenerator.GetBytes(4));

    // ------------------------------------------------------------ os horários oferecidos (a coluna)

    public static string Gravar(IEnumerable<DateTime> horarios)
        => string.Join(";", horarios.Select(h => h.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture)));

    public static IReadOnlyList<DateTime> Ler(string? gravado)
        => string.IsNullOrWhiteSpace(gravado)
            ? []
            : gravado.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => DateTime.TryParseExact(t, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var h) ? h : (DateTime?)null)
                .Where(h => h is not null).Select(h => h!.Value).ToList();

    /// <summary>
    /// A recusa da oferta, ou nulo quando ela vale: de 1 a 3 horários, sem repetir, futuros, dentro dos
    /// 31 dias e todos entre as vagas que a clínica tem agora (<paramref name="livres"/>).
    /// </summary>
    public static string? RecusaDaOferta(IReadOnlyList<DateTime> escolhidos, IReadOnlyCollection<DateTime> livres, DateTime agora)
    {
        if (escolhidos.Count == 0) return "Escolha ao menos um horário para oferecer.";
        if (escolhidos.Count > MaximoDeOpcoes) return $"O WhatsApp mostra até {MaximoDeOpcoes} horários. Escolha no máximo {MaximoDeOpcoes}.";
        if (escolhidos.Distinct().Count() != escolhidos.Count) return "O mesmo horário foi escolhido duas vezes.";
        if (escolhidos.Any(h => h <= agora || h > agora.AddDays(DiasMaximos))) return $"Ofereça horários futuros, nos próximos {DiasMaximos} dias.";
        if (escolhidos.Any(h => !livres.Contains(h))) return "Um dos horários deixou de estar livre. Atualize e escolha de novo.";
        return null;
    }

    // ------------------------------------------------------------ o botão que o paciente toca

    /// <summary>O identificador do botão (o que o WhatsApp devolve quando o paciente toca): "OFERTA 1A2B3C4D 2".</summary>
    public static string IdDaOpcao(string chave, int indice) => $"OFERTA {chave} {indice + 1}";

    [GeneratedRegex(@"^OFERTA ([0-9A-F]{8}) ([1-3])$")]
    private static partial Regex Opcao();

    /// <summary>A chave e o índice (0, 1, 2) do botão tocado; nulo quando o texto não é um botão de oferta.</summary>
    public static (string Chave, int Indice)? LerOpcao(string? texto)
    {
        var m = Opcao().Match((texto ?? "").Trim());
        return m.Success ? (m.Groups[1].Value, int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) - 1) : null;
    }

    /// <summary>O rótulo do botão (o WhatsApp corta em 20 caracteres): "sex 24/10 15:30".</summary>
    public static string RotuloDoBotao(DateTime h) => $"{DiaCurto(h)} {h.ToString("dd/MM", PtBr)} {h.ToString("HH:mm", PtBr)}";

    private static string DiaCurto(DateTime h) => h.DayOfWeek switch
    {
        DayOfWeek.Monday => "seg", DayOfWeek.Tuesday => "ter", DayOfWeek.Wednesday => "qua", DayOfWeek.Thursday => "qui",
        DayOfWeek.Friday => "sex", DayOfWeek.Saturday => "sáb", _ => "dom",
    };

    private static string DiaLongo(DateTime h) => h.DayOfWeek switch
    {
        DayOfWeek.Monday => "segunda", DayOfWeek.Tuesday => "terça", DayOfWeek.Wednesday => "quarta", DayOfWeek.Thursday => "quinta",
        DayOfWeek.Friday => "sexta", DayOfWeek.Saturday => "sábado", _ => "domingo",
    };

    /// <summary>"sexta, 24/10, às 15:30" — o horário escrito para o paciente.</summary>
    public static string HorarioParaOPaciente(DateTime h) => $"{DiaLongo(h)}, {h.ToString("dd/MM", PtBr)}, às {h.ToString("HH:mm", PtBr)}";

    /// <summary>O primeiro nome, com só a inicial maiúscula ("MARIA DA SILVA" → "Maria").</summary>
    public static string PrimeiroNome(string nome)
    {
        var primeiro = (nome ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return primeiro.Length == 0 ? "" : char.ToUpper(primeiro[0], PtBr) + primeiro[1..].ToLower(PtBr);
    }

    // ------------------------------------------------------------ as frases ao paciente

    /// <summary>A mensagem da oferta (os horários vão nos botões, embaixo dela).</summary>
    public static string TextoDaOferta(string nome, IReadOnlyList<DateTime> horarios)
    {
        var quem = PrimeiroNome(nome);
        var cabeca = quem.Length > 0 ? $"Olá, {quem}! " : "Olá! ";
        return horarios.Count == 1
            ? cabeca + $"Para remarcar a sua sessão, temos {HorarioParaOPaciente(horarios[0])}. Toque no botão para ficar com ele."
            : cabeca + "Para remarcar a sua sessão, temos estes horários. Toque no que for melhor para você.";
    }

    /// <summary>O que volta quando o paciente toca um horário: a escolha foi RECEBIDA; remarcado é depois.</summary>
    public static string TextoDaEscolhaRecebida(DateTime h)
        => $"Recebemos a sua escolha: {HorarioParaOPaciente(h)}. A recepção confirma em seguida.";

    /// <summary>A oferta não vale mais (outra mais nova, pedido encerrado, botão desconhecido).</summary>
    public const string TextoOfertaVencida = "Esta lista de horários não vale mais. A recepção vai falar com você por aqui.";

    /// <summary>A confirmação depois que a recepção REMARCOU: só sai quando o horário mudou na agenda.</summary>
    public static string TextoRemarcado(string nome, DateTime h)
    {
        var quem = PrimeiroNome(nome);
        return (quem.Length > 0 ? $"Pronto, {quem}! " : "Pronto! ") + $"Sua sessão foi remarcada para {HorarioParaOPaciente(h)}. Até lá!";
    }
}
