using System.Security.Cryptography;

namespace Clinica.Domain;

/// <summary>
/// O LINK DE CONFIRMAÇÃO que vai no SMS (out/2026 — pedido da cliente: *"enviar SMS para os
/// pacientes 24h antes da consulta para que o paciente possa clicar no link e confirmar se
/// irá ou não"*). O desenho é o do termo pelo WhatsApp (parcela 81), e pela mesma razão:
/// o desktop publica um PEDIDO minimizado no balde, o Worker da clínica serve a página e
/// grava a RESPOSTA ao lado, e o desktop a lê de volta — <b>o Worker nunca toca o banco</b>.
///
/// Tudo o que decide caminho, tamanho de token e momento de envio mora AQUI, e não no
/// serviço, porque são DOIS lados que precisam concordar (o app e o Worker em
/// <c>tools/worker-clinica.js</c>) e porque é regra que o <c>dotnet test</c> alcança puro.
/// </summary>
public static class ConfirmacaoPorLink
{
    /// <summary>
    /// O alfabeto das receitas, sem os caracteres que se confundem ao ler (0/O, 1/I/L).
    /// O Worker valida com <c>/^[A-Z2-9]{12}$/</c> — os dois lados precisam bater.
    /// </summary>
    private const string Alfabeto = "ABCDEFGHJKMNPQRSTVWXYZ23456789";

    /// <summary>
    /// 12 caracteres, e não os 26 das receitas: o link vai num SMS de 160 caracteres, e
    /// cada caractere de token é um caractere a menos de mensagem. 30^12 ≈ 2^59 — o pedido
    /// carrega só o primeiro nome, o dia e a hora, e sai do ar quando a sessão passa.
    /// </summary>
    public const int TamanhoToken = 12;

    /// <summary>Antecedência padrão do lembrete — o pedido da cliente.</summary>
    public const int AntecedenciaPadraoHoras = 24;

    /// <summary>
    /// Mais perto da sessão do que isto, o SMS não sai: perguntar "você vem?" uma hora antes
    /// não deixa tempo de reocupar o horário, e o paciente já está a caminho.
    /// </summary>
    public const int MinimoDeHorasAntes = 2;

    public static string GerarToken()
    {
        var token = new char[TamanhoToken];
        for (var i = 0; i < token.Length; i++)
            token[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
        return new string(token);
    }

    /// <summary>Token bem-formado (o mesmo teste que o Worker faz).</summary>
    public static bool TokenValido(string? token)
        => token is { Length: TamanhoToken } && token.All(c => Alfabeto.Contains(c));

    /// <summary>Onde o desktop publica o pedido (público — é o que a página lê).</summary>
    public static string CaminhoPedido(string token) => $"c/{token[..2]}/{token}.json";

    /// <summary>Onde o Worker grava a resposta (write-once).</summary>
    public static string CaminhoResposta(string token) => $"c/{token[..2]}/{token}.resposta.json";

    /// <summary>O link que vai no SMS: <c>https://dominio/c/TOKEN</c>.</summary>
    public static string Url(string baseUrl, string token) => $"{baseUrl.TrimEnd('/')}/c/{token}";

    /// <summary>
    /// O instante em que o lembrete DEVE sair: a hora da sessão menos a antecedência.
    ///
    /// ⚠️ <b>O fim de semana adianta para a sexta</b> quando <paramref name="adiantarFimDeSemana"/>
    /// está ligado (o padrão): o sistema roda nas máquinas da clínica, e no sábado e no
    /// domingo ninguém as liga — sem a regra, a sessão de segunda às 9h teria o lembrete
    /// "devido" no domingo às 9h e só sairia na própria segunda de manhã, uma hora antes. É
    /// a mesma razão de <c>LembreteEmailService.DiasDaAbertura</c>.
    /// </summary>
    public static DateTime MomentoDoLembrete(DateTime sessao, int antecedenciaHoras, bool adiantarFimDeSemana = true)
    {
        var momento = sessao.AddHours(-Math.Max(1, antecedenciaHoras));
        if (!adiantarFimDeSemana) return momento;
        while (momento.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            momento = momento.AddDays(-1);
        return momento;
    }

    /// <summary>
    /// Já é hora de mandar? Sim quando o momento do lembrete chegou e ainda faltam pelo
    /// menos <see cref="MinimoDeHorasAntes"/> horas para a sessão. Mandar DEPOIS do momento
    /// (o app abriu tarde) é melhor do que não mandar — desde que ainda dê tempo de agir.
    /// </summary>
    public static bool HoraDeEnviar(DateTime sessao, DateTime agora, int antecedenciaHoras, bool adiantarFimDeSemana = true)
        => agora >= MomentoDoLembrete(sessao, antecedenciaHoras, adiantarFimDeSemana)
           && sessao - agora >= TimeSpan.FromHours(MinimoDeHorasAntes);

    /// <summary>
    /// A janela de busca das sessões candidatas: de agora + mínimo até agora + antecedência
    /// + 4 dias (a sexta que adianta a segunda de um feriado prolongado cabe aqui).
    /// </summary>
    public static (DateTime Inicio, DateTime Fim) JanelaDeBusca(DateTime agora, int antecedenciaHoras)
        => (agora.AddHours(MinimoDeHorasAntes), agora.AddHours(Math.Max(1, antecedenciaHoras)).AddDays(4));
}

/// <summary>O que o paciente respondeu pelo link.</summary>
public enum RespostaPeloLink
{
    Confirmou,
    NaoVai
}
