using System.Globalization;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que a seção "Ficha do paciente" (parte clínica) afirma — os dados cadastrais de leitura, a
/// lista de problemas e a anamnese (out/2026: subido da PacienteCapaViewModel, da
/// ProblemaEdicaoViewModel e da AnamneseViewModel WPF na migração para Blazor). Puro, com teste.
/// </summary>
public static class FichaClinicaNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    // ==================== Dados ====================

    public sealed record Dados(
        string Nascimento, string Documento, string Telefone, string Email, string Endereco, string Sexo,
        string Modalidade, string ObservacoesCadastro, string Convenio, string Carteirinha,
        string ValidadeCarteirinha, bool CarteirinhaVencida, string EmTratamento);

    public static Dados DadosDe(Paciente p, CabecalhoClinicoPaciente? cabecalho, bool podeVerProntuario, DateOnly hoje) => new(
        p.DataNascimento is { } nasc ? $"{nasc:dd/MM/yyyy} · {IdadeDoPaciente.Texto(nasc, hoje)}" : "não informado",
        Ou(p.DocumentoFormatado, "não informado"),
        Ou(p.TelefoneFormatado, "não informado"),
        Ou(p.Email, "não informado"),
        Ou(p.Endereco, "não informado"),
        RotulosEnum.De(p.Sexo),
        CatalogoModalidades.Nome(p.ModalidadePreferidaCodigo, p.ModalidadePreferida),
        Ou(p.Observacoes, "Nenhuma observação de cadastro."),
        p.ConvenioNome,
        Ou(p.Carteirinha, "não informada"),
        p.ValidadeCarteirinha is { } val ? val.ToString("dd/MM/yyyy", PtBr) + (p.CarteirinhaVencida ? " — VENCIDA" : string.Empty) : "sem validade informada",
        p.CarteirinhaVencida,
        EmTratamento(cabecalho, podeVerProntuario));

    /// <summary>Desde quando e quantas sessões — "não foi possível conferir" quando o crachá falhou.</summary>
    public static string EmTratamento(CabecalhoClinicoPaciente? cabecalho, bool podeVerProntuario)
    {
        if (!podeVerProntuario) return "Consulta restrita ao perfil clínico";
        if (cabecalho is null) return "não foi possível conferir";
        var desde = cabecalho.PrimeiraSessao is { } primeira ? $"desde {primeira:dd/MM/yyyy}" : "ainda não foi atendida aqui";
        var total = cabecalho.TotalSessoes switch { 0 => string.Empty, 1 => " · 1 sessão", var n => $" · {n} sessões" };
        return desde + total;
    }

    private static string Ou(string? valor, string quandoVazio) => string.IsNullOrWhiteSpace(valor) ? quandoVazio : valor!;

    // ==================== Problemas ====================

    public sealed record Problema(int Id, string Rotulo, string Natureza, string Periodo, string Observacoes,
        string Situacao, bool Ativo, bool Alerta, bool Descartado, ProblemaPaciente Fonte);

    public sealed record OpcaoNatureza(NaturezaProblema Valor, string Rotulo, string Explicacao);

    public static readonly IReadOnlyList<OpcaoNatureza> Naturezas =
    [
        new(NaturezaProblema.Diagnostico, "Diagnóstico", "O que o paciente tem e está em acompanhamento."),
        new(NaturezaProblema.Alergia, "Alergia ou intolerância", "Vira alerta na tela de atendimento — inclusive depois de dada por resolvida."),
        new(NaturezaProblema.Antecedente, "Antecedente", "Cirurgia, internação, fratura: o que já aconteceu e importa saber."),
        new(NaturezaProblema.MedicacaoContinua, "Medicação de uso contínuo", "Também vira alerta: interação é o segundo motivo de dano evitável no consultório.")
    ];

    public static Problema ProblemaDe(ProblemaPaciente p) => new(
        p.Id, p.Rotulo,
        p.Natureza switch
        {
            NaturezaProblema.Alergia => "Alergia",
            NaturezaProblema.Antecedente => "Antecedente",
            NaturezaProblema.MedicacaoContinua => "Uso contínuo",
            _ => "Diagnóstico"
        },
        p switch
        {
            { Inicio: { } i, Fim: { } f } => $"de {i:dd/MM/yyyy} a {f:dd/MM/yyyy}",
            { Inicio: { } i } => $"desde {i:dd/MM/yyyy}",
            { Fim: { } f } => $"encerrado em {f:dd/MM/yyyy}",
            _ => "sem data informada"
        },
        p.Observacoes?.Trim() ?? string.Empty,
        p.Situacao switch
        {
            SituacaoProblema.Resolvido => "Resolvido",
            SituacaoProblema.Descartado => string.IsNullOrWhiteSpace(p.MotivoDescarte) ? "Descartado" : $"Descartado — {p.MotivoDescarte}",
            _ => "Ativo"
        },
        p.EstaAtivo, p.EhAlertaDeAtendimento, p.Situacao == SituacaoProblema.Descartado, p);

    public static string ResumoProblemas(int linhas, int alertas, bool incluindoEncerrados)
        => linhas == 0
            ? (incluindoEncerrados
                ? "Nenhum problema registrado para este paciente."
                : "Nenhum problema ATIVO. Pode haver linhas resolvidas ou descartadas — marque a caixa para vê-las.")
            : $"{linhas} linha(s)" + (alertas > 0 ? $", {alertas} com alerta de atendimento." : ".");

    public const string ProblemasFalharam =
        "Não foi possível ler a lista de problemas deste paciente — ela está vazia por falha de leitura, não porque não haja nada.";

    // ==================== Anamnese ====================

    public sealed record SecaoAnamnese(string Chave, string Rotulo, string Dica);

    public static readonly IReadOnlyList<SecaoAnamnese> SecoesDaAnamnese =
    [
        new("AntecedentesPessoais", "Antecedentes pessoais", "Doenças prévias, internações, cirurgias, fraturas."),
        new("AntecedentesFamiliares", "Antecedentes familiares",
            "O que decide conduta é o parentesco e a idade do evento — “pai infartou aos 58” pesa diferente de “avô aos 89”."),
        new("HabitosDeVida", "Hábitos de vida", "Tabagismo, etilismo, atividade física, sono, alimentação."),
        new("RevisaoDeSistemas", "Interrogatório", "A varredura por aparelhos que a primeira consulta faz e as seguintes não repetem."),
        new("HistoriaObstetrica", "História obstétrica", "Quando se aplica."),
        new("Observacoes", "Observações", "Profissão, com quem mora, quem cuida, o que a pessoa espera do tratamento.")
    ];

    public sealed record VersaoAnamneseNaTela(string Titulo, string Quando, string Motivo, string Texto);

    public static VersaoAnamneseNaTela VersaoDe(VersaoAnamnese v) => new(
        $"Versão {v.Versao}",
        $"substituída em {v.SubstituidaEm:dd/MM/yyyy HH\\:mm}" + (string.IsNullOrWhiteSpace(v.SubstituidaPor) ? string.Empty : $" por {v.SubstituidaPor}"),
        string.IsNullOrWhiteSpace(v.Motivo) ? "Revisão sem motivo informado." : v.Motivo,
        Juntar(v.AntecedentesPessoais, v.AntecedentesFamiliares, v.HabitosDeVida, v.HistoriaObstetrica, v.RevisaoDeSistemas, v.Observacoes));

    /// <summary>Quem colheu/revisou e quando — e o aviso de que vale reperguntar depois de um ano.</summary>
    public static string Procedencia(AnamnesePaciente a, DateTime agora)
    {
        var quem = a.AtualizadaPor ?? a.CriadaPor;
        var verbo = a.AtualizadaEm is null ? "Colhida" : "Revisada";
        var texto = $"{verbo} em {a.UltimaRevisao:dd/MM/yyyy}" + (string.IsNullOrWhiteSpace(quem) ? string.Empty : $" por {quem}");
        var meses = (int)((agora - a.UltimaRevisao).TotalDays / 30);
        return meses >= 12 ? texto + $"  ·  há mais de {meses / 12} ano(s) — vale reperguntar" : texto;
    }

    private static string Juntar(string? pessoais, string? familiares, string? habitos, string? obstetrica, string? sistemas, string? observacoes)
    {
        var partes = new List<string>();
        void Campo(string rotulo, string? v)
        {
            if (!string.IsNullOrWhiteSpace(v)) partes.Add($"{rotulo}: {v.Trim()}");
        }
        Campo("Antecedentes pessoais", pessoais);
        Campo("Antecedentes familiares", familiares);
        Campo("Hábitos de vida", habitos);
        Campo("História obstétrica", obstetrica);
        Campo("Interrogatório", sistemas);
        Campo("Observações", observacoes);
        return string.Join("\n", partes);
    }

    public static string ResumoSessoes(int pagina, int quantidade)
        => $"Página {pagina + 1} · {quantidade} sessão(ões) da agenda. Registros fora da agenda estão em Histórico.";
}
