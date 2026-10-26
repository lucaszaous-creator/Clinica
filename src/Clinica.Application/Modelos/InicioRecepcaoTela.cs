using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>
/// Um contador de "O que resolver no balcão". <see cref="Chave"/> é o identificador estável
/// (guias · confirmar · retornos · recall); quem traduz a chave em DESTINO de navegação é a
/// tela, porque as chaves do menu moram no módulo (WPF) e a Application não as enxerga.
/// </summary>
public sealed record ItemDoBalcao(string Chave, string Titulo, string Valor, string Detalhe, string Tom)
{
    /// <summary>O contador não pôde ser lido: "—" e a frase dizem isso, nunca zero.</summary>
    public bool NaoVerificado => Valor == "—";
}

/// <summary>Uma guia do 2º código de quem vem hoje, com as frases da lista.</summary>
public sealed record GuiaDoBalcao(int PacienteId, string Paciente, string Descricao, string Atraso, string? Telefone)
{
    public bool TemTelefone => !string.IsNullOrWhiteSpace(Telefone);
}

/// <summary>Um aniversariante da semana, com a frase do detalhe.</summary>
public sealed record AniversarianteDoBalcao(int PacienteId, string Paciente, string Detalhe, string? Telefone, bool VemHoje)
{
    public bool TemTelefone => !string.IsNullOrWhiteSpace(Telefone);
}

/// <summary>
/// O que a tela "Início" da Recepção AFIRMA além do <see cref="InicioDoBalcao"/> (out/2026,
/// migração para Blazor). Estas frases moravam no <c>PainelViewModel</c> WPF, onde o
/// <c>dotnet test</c> não alcança; subiram para cá INTEIRAS, com as mesmas palavras, para a
/// tela nova não ganhar uma segunda redação.
///
/// Regras que não são só texto:
/// - <b>Contador que não pôde ser lido mostra "—" e diz "não foi possível conferir"</b> —
///   zero por falha seria indistinguível de "nada a resolver".
/// - <b>"Guias do 2º código" conta só o SEGUNDO código</b> dos pacientes que vêm hoje: é a
///   guia que nasce +24 h e que o balcão cobra com o paciente presente.
/// - <b>Recall de hoje</b> = pendentes com o próximo contato vencido ou de hoje.
/// - <b>A pílula ganha o tom do que ela pede</b>: confirmou (sucesso), não confirmou
///   (aviso), NÃO VEM (perigo — ligar e liberar o horário), no local (info). "Marcado" não
///   tem pílula (sem contato não se acusa ninguém).
/// </summary>
public static class InicioRecepcaoTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Os dias que a lista de aniversariantes cobre além de hoje (a semana).</summary>
    public const int JanelaAniversarioDias = 6;

    /// <summary>"62% da agenda ocupada · 2 encaixes" — sem horário de atendimento, a ocupação não é medida.</summary>
    public static string DetalheAgendados(decimal? percentualOcupado, int encaixes)
    {
        var e = $"{encaixes} {(encaixes == 1 ? "encaixe" : "encaixes")}";
        return percentualOcupado is { } p
            ? $"{Math.Round(p).ToString("0", PtBr)}% da agenda ocupada · {e}"
            : $"ocupação sem horário de atendimento cadastrado · {e}";
    }

    /// <summary>A fração da barra de ocupação (0 a 1); nula quando não há horário de atendimento.</summary>
    public static double? FracaoOcupada(decimal? percentualOcupado)
        => percentualOcupado is { } p ? Math.Clamp((double)p / 100.0, 0, 1) : null;

    /// <summary>Quem já fez check-in (no local ou chamado) e ainda não entrou na sala.</summary>
    public static int NoLocal(IEnumerable<Agendamento> doDia)
        => doDia.Count(a => a.Etapa is EtapaFila.Chegou or EtapaFila.Chamado);

    /// <summary>"ninguém no local ainda" · "1 já está no local" · "3 já estão no local".</summary>
    public static string DetalheAAtender(int noLocal) => noLocal switch
    {
        0 => "ninguém no local ainda",
        1 => "1 já está no local",
        var n => $"{n} já estão no local"
    };

    /// <summary>"1 pessoa" · "4 pessoas" — o selo do cabeçalho de "Próximos a chegar".</summary>
    public static string ResumoProximos(int quantos) => $"{quantos} {(quantos == 1 ? "pessoa" : "pessoas")}";

    /// <summary>O tom da pílula da linha (as classes <c>tom-*</c> do design system).</summary>
    public static string TomDaPilula(SituacaoChegada situacao) => situacao switch
    {
        SituacaoChegada.Confirmou => "sucesso",
        SituacaoChegada.NaoConfirmou => "aviso",
        SituacaoChegada.NaoVem => "perigo",
        SituacaoChegada.NoLocal => "info",
        _ => "neutro"
    };

    /// <summary>As guias do 2º código dos pacientes de hoje, com descrição e atraso por extenso.</summary>
    public static IReadOnlyList<GuiaDoBalcao> GuiasDoSegundoCodigo(IEnumerable<PendenciaCodigo> pendencias)
        => pendencias.Where(x => x.Ordem == OrdemCodigo.Segundo)
            .Select(x => new GuiaDoBalcao(
                x.PacienteId,
                x.PacienteNome,
                x.Descricao ?? $"{RotulosEnum.De(x.Tipo)} ({RotulosEnum.De(x.Ordem)})",
                x.DiasEmAtraso <= 0 ? "vence hoje" : $"{x.DiasEmAtraso} dia(s) em atraso",
                x.PacienteTelefone))
            .ToList();

    /// <summary>Recall de hoje: pendentes cujo próximo contato venceu ou é hoje.</summary>
    public static int RecallDeHoje(IEnumerable<LinhaAcompanhamento> fila, DateOnly hoje)
        => fila.Count(a => a.Pendente && a.ProximoContato <= hoje);

    /// <summary>
    /// Os quatro contadores, na ordem do mockup aprovado. Valor nulo = a leitura falhou
    /// (ou, no "Confirmar amanhã", não há rodada gerada — e a frase do contador diz qual).
    /// </summary>
    public static IReadOnlyList<ItemDoBalcao> ItensDoBalcao(
        int? guias, ContadorDoBalcao? confirmarAmanha, int? recall)
    {
        var confirmar = confirmarAmanha ?? new ContadorDoBalcao("Confirmar amanhã", null, "não foi possível conferir");
        return
        [
            Item("guias", "Guias do 2º código", guias, "de pacientes que vêm hoje", "perigo"),
            new ItemDoBalcao("confirmar", confirmar.Titulo, confirmar.ValorTexto, confirmar.Detalhe, "info"),
            Item("recall", "Recall de hoje", recall, "contatos combinados para hoje", "neutro")
        ];
    }

    private static ItemDoBalcao Item(string chave, string titulo, int? valor, string detalhe, string tom)
        => new(chave, titulo, valor?.ToString(PtBr) ?? "—", valor is null ? "não foi possível conferir" : detalhe, tom);

    /// <summary>"52 anos · 14/10 · vem hoje" — sem idade quando a data não serve.</summary>
    public static AniversarianteDoBalcao Aniversariante(Aniversariante a)
        => new(a.PacienteId, a.Nome,
            (a.Idade is { } idade ? $"{idade} anos · " : string.Empty) + a.DiaEMes + (a.VemHoje ? " · vem hoje" : string.Empty),
            a.Telefone, a.VemHoje);

    /// <summary>A frase do rodapé; nulo = a leitura falhou.</summary>
    public static string RodapeAniversarios(int? quantos) => quantos switch
    {
        null => "Aniversariantes: não foi possível conferir",
        0 => "Nenhum aniversariante nesta semana",
        1 => "1 aniversariante nesta semana",
        var n => $"{n} aniversariantes nesta semana"
    };

    /// <summary>
    /// A cobrança do documento/guia do 2º código pelo WhatsApp. É a MESMA redação do
    /// <c>Whatsapp.CobrancaDeGuia</c> do shell — que passou a ler daqui, para as duas portas
    /// não divergirem na primeira correção.
    /// </summary>
    public static string CobrancaDeGuia(string nomePaciente, string? nomeClinica = null)
        => $"Olá, {PrimeiroNome(nomePaciente)}! Precisamos do documento/guia da sua última sessão para"
           + " concluir o faturamento junto ao convênio. Consegue nos enviar?"
           + (string.IsNullOrWhiteSpace(nomeClinica) ? string.Empty : $" — {nomeClinica}");

    /// <summary>O parabéns que o balcão manda pelo WhatsApp (o mesmo texto da tela WPF).</summary>
    public static string Parabens(string nomePaciente)
        => $"Feliz aniversário, {PrimeiroNome(nomePaciente)}! Toda a equipe da clínica deseja um ótimo dia.";

    private static string PrimeiroNome(string nome)
        => nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nome;
}
