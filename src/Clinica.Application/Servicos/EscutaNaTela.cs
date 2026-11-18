using System.Globalization;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Uma sessão realizada, como a Escuta a enxerga (só agenda: dia e modalidade).</summary>
public sealed record SessaoRealizada(int AtendimentoId, int PacienteId, DateOnly Data, ModalidadeAtendimento Modalidade);

/// <summary>Uma sessão que pede escuta: o paciente, a sessão e por quê. Nada do prontuário.</summary>
public sealed record SessaoParaEscutar(int AtendimentoId, int PacienteId, string Paciente, string? Telefone, DateOnly Data,
    ModalidadeAtendimento Modalidade, int? ProfissionalId, string Profissional, MotivoDaEscuta Motivo);

/// <summary>O aviso de piora para o profissional da sessão (o Meu dia).</summary>
public sealed record AvisoDePiora(int EscutaId, int PacienteId, string Paciente, DateOnly SessaoEm, ModalidadeAtendimento Modalidade,
    string? Relato, DateTime RegistradoEm, string RegistradoPor);

/// <summary>O que a tela manda registrar.</summary>
public sealed record RegistrarEscuta(int AtendimentoId, ComoEstaPaciente Como, string? Relato, CanalContato Canal);

/// <summary>
/// As regras da ESCUTA PÓS-SESSÃO (nov/2026, Fila 360), puras e testadas: quais sessões pedem escuta, o que o
/// registro exige e como cada resposta se escreve. A leitura e a gravação são do <c>IEscutaPosSessaoService</c>.
/// </summary>
public static class EscutaNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Dias depois da sessão em que ela ainda aparece para escutar: passou disso, a pergunta perde o sentido.</summary>
    public const int DiasParaEscutar = 7;

    /// <summary>Relato mínimo quando o paciente piorou: o profissional precisa saber O QUE piorou.</summary>
    public const int RelatoMinimoDaPiora = 5;

    public const int RelatoMaximo = 1000;

    /// <summary>As respostas na ordem da tela.</summary>
    public static IReadOnlyList<ComoEstaPaciente> Respostas { get; } =
        [ComoEstaPaciente.Melhor, ComoEstaPaciente.Igual, ComoEstaPaciente.Pior, ComoEstaPaciente.NaoRespondeu];

    /// <summary>
    /// Por que a sessão pede escuta, ou nulo. Só tratamento (BSV, acupuntura — consulta avulsa não é tratamento):
    /// <list type="bullet">
    /// <item>a sessão de VOLTA de um caso da retenção (o dia está em <paramref name="voltasDoPaciente"/>);</item>
    /// <item>a PRIMEIRA sessão da família do tratamento (BSV com ou sem acupuntura; acupuntura simples ou com
    /// eletro) — nenhuma sessão anterior da mesma família.</item>
    /// </list>
    /// A volta vence a primeira sessão: o motivo diz o que a Patrycia vai perguntar.
    /// </summary>
    public static MotivoDaEscuta? Motivo(SessaoRealizada sessao, IEnumerable<SessaoRealizada> doPaciente, IEnumerable<DateOnly> voltasDoPaciente)
    {
        if (!RetencaoNaTela.EmTratamento(sessao.Modalidade)) return null;
        if (voltasDoPaciente.Contains(sessao.Data)) return MotivoDaEscuta.VoltouPelaRetencao;
        var familia = Familia(sessao.Modalidade);
        var antes = doPaciente.Any(s => s.AtendimentoId != sessao.AtendimentoId && Familia(s.Modalidade) == familia
            && (s.Data < sessao.Data || s.Data == sessao.Data && s.AtendimentoId < sessao.AtendimentoId));
        return antes ? null : MotivoDaEscuta.PrimeiraSessao;
    }

    /// <summary>A família do tratamento: BSV (com ou sem acupuntura) é uma; acupuntura (simples ou com eletro) é outra.</summary>
    public static string Familia(ModalidadeAtendimento m) => m switch
    {
        ModalidadeAtendimento.BsvApenas or ModalidadeAtendimento.BsvComAcupuntura => "bsv",
        ModalidadeAtendimento.AcupunturaSimples or ModalidadeAtendimento.AcupunturaComEletro => "acupuntura",
        _ => m.ToString()
    };

    /// <summary>A sessão ainda está na janela da escuta (de hoje até <see cref="DiasParaEscutar"/> dias atrás).</summary>
    public static bool NaJanela(DateOnly sessao, DateOnly hoje) => sessao <= hoje && sessao >= hoje.AddDays(-DiasParaEscutar);

    /// <summary>A recusa do registro, ou nulo. A mesma frase no formulário (antes do clique) e no serviço.</summary>
    public static string? Recusa(ComoEstaPaciente como, string? relato)
    {
        if (!Enum.IsDefined(como)) return "Escolha como o paciente disse que está.";
        var texto = relato?.Trim() ?? string.Empty;
        if (texto.Length > RelatoMaximo) return $"O relato passa de {RelatoMaximo} caracteres.";
        if (como == ComoEstaPaciente.Pior && texto.Length < RelatoMinimoDaPiora)
            return "Conte o que piorou, nas palavras do paciente: é o que o profissional vai ler no aviso.";
        return null;
    }

    public static string Rotulo(ComoEstaPaciente c) => c switch
    {
        ComoEstaPaciente.Melhor => "Melhorou",
        ComoEstaPaciente.Igual => "Está igual",
        ComoEstaPaciente.Pior => "Piorou",
        _ => "Não respondeu"
    };

    /// <summary>O tom do selo: verde, neutro, vermelho e cinza.</summary>
    public static string Tom(ComoEstaPaciente c) => c switch
    {
        ComoEstaPaciente.Melhor => "sucesso",
        ComoEstaPaciente.Pior => "perigo",
        _ => "neutro"
    };

    public static string Rotulo(MotivoDaEscuta m) => m switch
    {
        MotivoDaEscuta.VoltouPelaRetencao => "Voltou pela retenção",
        _ => "1ª sessão do tratamento"
    };

    /// <summary>A pergunta sugerida para a mensagem — quem envia revisa e ajusta.</summary>
    public static string Pergunta(string paciente, MotivoDaEscuta motivo, string? clinica)
    {
        var nome = paciente.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? paciente;
        var de = string.IsNullOrWhiteSpace(clinica) ? "da clínica" : $"da {clinica.Trim()}";
        return motivo == MotivoDaEscuta.VoltouPelaRetencao
            ? $"Oi, {nome}! Aqui é {de}. Que bom ter você de volta! Como você está depois da sessão?"
            : $"Oi, {nome}! Aqui é {de}. Como você está depois da sua primeira sessão?";
    }

    /// <summary>"Melhorou · 18/11 · pela retenção" — a linha de uma escuta na Ficha 360 e no documento do titular.</summary>
    public static string Linha(EscutaPosSessao e)
        => $"{Rotulo(e.Como)} · sessão de {e.SessaoEm.ToString("dd/MM/yyyy", PtBr)} ({RotulosEnum.De(e.Modalidade)}) · {Rotulo(e.Motivo).ToLowerInvariant()}"
           + (string.IsNullOrWhiteSpace(e.Relato) ? string.Empty : $" · \"{e.Relato.Trim()}\"")
           + $" · registrada por {e.RegistradoPor} em {e.RegistradoEm.ToString("dd/MM/yyyy HH:mm", PtBr)}"
           + (e.Cancelada ? $" (CANCELADA: {e.MotivoCancelamento})" : string.Empty);
}
