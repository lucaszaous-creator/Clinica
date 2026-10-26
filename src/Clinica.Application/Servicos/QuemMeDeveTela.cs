using System.Globalization;
using Clinica.Application.Formatacao;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que a tela "Quem me deve" AFIRMA, fora do Razor (out/2026 — a tela saiu do WPF para
/// Blazor). Até aqui estas decisões moravam no <c>InadimplenciaViewModel</c>, num projeto
/// WPF que o <c>dotnet test</c> não alcança: o corte de "crítico", a frase de cada devedor,
/// o recorte do filtro e o resumo que DIZ que está filtrado. Subiram para cá, puras e com
/// teste, para a tela nova só desenhar.
/// </summary>
public static class QuemMeDeveTela
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Acima de 90 dias a cobrança deixa de ser lembrete: é onde se decide acordo ou parar
    /// de contar com o dinheiro. É o mesmo corte da faixa "mais de 90 dias".
    /// </summary>
    public const int DiasCritico = 90;

    public static bool EhCritico(PacienteInadimplente devedor) => devedor.DiasMaiorAtraso > DiasCritico;

    /// <summary>"1 conta · vencida faz 20 dia(s)" / "3 contas · a mais antiga faz 80 dia(s)".</summary>
    public static string ResumoDoDevedor(PacienteInadimplente devedor) => devedor.Contas == 1
        ? $"1 conta · vencida faz {devedor.DiasMaiorAtraso} dia(s)"
        : $"{devedor.Contas} contas · a mais antiga faz {devedor.DiasMaiorAtraso} dia(s)";

    public static string AtrasoDaConta(ContaEmAtraso conta) => $"venceu faz {conta.DiasEmAtraso} dia(s)";

    public static bool FiltroAtivo(string? termo, bool soCriticos)
        => soCriticos || !string.IsNullOrWhiteSpace(termo);

    /// <summary>
    /// O recorte do filtro, em memória: a lista já veio inteira, e achar a Maria com o
    /// telefone na mão não pode ser rolar a clínica. O acento não atrapalha (<see cref="Busca"/>).
    /// </summary>
    public static IReadOnlyList<PacienteInadimplente> Filtrar(
        IReadOnlyList<PacienteInadimplente> devedores, string? termo, bool soCriticos)
        => devedores.Where(d => (!soCriticos || EhCritico(d)) && Busca.Casa(d.Nome, termo)).ToList();

    /// <summary>
    /// O resumo DIZ que está filtrado: "3 devedores" e "3 de 30 no filtro" respondem
    /// perguntas diferentes — e os totais do topo continuam sendo do TODO.
    /// </summary>
    public static string ResumoDaLista(ResumoInadimplencia resumo, int noFiltro, bool filtroAtivo)
    {
        if (filtroAtivo) return $"{noFiltro} de {resumo.Pacientes} devedor(es) no filtro.";
        return resumo.Vazio
            ? "Nenhuma conta de paciente vencida. Nada a cobrar hoje."
            : $"{resumo.Pacientes} paciente(s) com {resumo.Contas} conta(s) vencida(s).";
    }

    /// <summary>
    /// "Ninguém devendo" e "ninguém bate com o filtro" são respostas diferentes — um
    /// filtro esquecido não pode dar a inadimplência por zerada.
    /// </summary>
    public static string TituloDoVazio(bool filtroAtivo)
        => filtroAtivo ? "Nenhum devedor bate com o filtro" : "Ninguém devendo";

    public static string DescricaoDoVazio(bool filtroAtivo) => filtroAtivo
        ? "Nenhum devedor bate com o filtro — limpe-o para ver todos."
        : "Só conta de PACIENTE entra: reembolso de convênio é outra conversa.";

    /// <summary>
    /// TODAS as faixas, sempre — inclusive quando ninguém deve. Um aging em que a faixa de
    /// 90+ dias simplesmente não existe se lê como "não há dívida velha"; o correto é ver
    /// R$ 0,00 escrito ali. (O <see cref="InadimplenciaService.Resumir"/> devolve a lista
    /// vazia quando não há devedor; aqui ela é completada com zeros, na ordem do aging.)
    /// </summary>
    public static IReadOnlyList<FaixaInadimplencia> FaixasDoEnvelhecimento(ResumoInadimplencia resumo)
        => FaixasInadimplencia.Todas
            .Select(f => resumo.Faixas.FirstOrDefault(x => x.Faixa == f) ?? new FaixaInadimplencia(f, 0, 0m))
            .ToList();

    /// <summary>"R$ 1.200,00 · 3" — o valor e quantos pacientes; sem paciente, só o valor.</summary>
    public static string ValorDaFaixa(FaixaInadimplencia faixa) => faixa.Pacientes > 0
        ? $"{Moeda(faixa.Valor)} · {faixa.Pacientes}"
        : Moeda(faixa.Valor);

    /// <summary>
    /// O número no formato que o wa.me aceita (com DDI), ou null quando o cadastro não tem
    /// telefone utilizável. É a MESMA regra do <c>Whatsapp.Abrir</c> do shell: 10 a 13
    /// dígitos, e 10/11 ganham o 55 na frente.
    /// </summary>
    public static string? TelefoneParaWhatsApp(string? telefone)
    {
        var fone = Clinica.Domain.Telefone.SemPrefixoDeDiscagem(Clinica.Domain.Telefone.Normalizar(telefone));
        if (fone.Length is < 10 or > 13) return null;
        return fone.Length is 10 or 11 ? "55" + fone : fone;
    }

    /// <summary>O endereço do wa.me com a mensagem pronta, ou null sem telefone utilizável.</summary>
    public static string? LinkWhatsApp(string? telefone, string mensagem)
        => TelefoneParaWhatsApp(telefone) is { } fone
            ? $"https://wa.me/{fone}?text={Uri.EscapeDataString(mensagem)}"
            : null;

    private static string Moeda(decimal valor) => valor.ToString("C2", Brasil);
}
