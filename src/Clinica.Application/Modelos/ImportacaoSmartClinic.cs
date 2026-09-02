using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Uma linha de um CSV com acesso por nome de coluna.</summary>
public sealed class RegistroCsv
{
    private readonly IReadOnlyDictionary<string, int> _indice;
    private readonly string[] _linha;

    public RegistroCsv(IReadOnlyDictionary<string, int> indice, string[] linha)
    {
        _indice = indice;
        _linha = linha;
    }

    /// <summary>O valor da coluna, ou nulo quando ela não existe ou está em branco.</summary>
    public string? this[string coluna]
        => _indice.TryGetValue(coluna, out var i) && i < _linha.Length && !string.IsNullOrWhiteSpace(_linha[i])
            ? _linha[i].Trim()
            : null;

    public IEnumerable<string> Colunas => _indice.Keys;

    public static IReadOnlyDictionary<string, int> Indice(TabelaImportada tabela)
    {
        var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < tabela.Colunas.Count; i++) d.TryAdd(tabela.Colunas[i].Trim(), i);
        return d;
    }
}

/// <summary>Quem escreveu o registro no sistema anterior, como ele gravou.</summary>
public sealed record AutorAnterior(string Nome, string? Conselho)
{
    /// <summary>"Nome (CRM 12345/RJ)" — o que vai para <c>CriadoPor</c> quando o autor não
    /// tem cadastro aqui. Cabe em 80 caracteres.</summary>
    public string Rotulo
    {
        get
        {
            var r = string.IsNullOrWhiteSpace(Conselho) ? Nome : $"{Nome} ({Conselho})";
            return r.Length <= 80 ? r : r[..80];
        }
    }
}

/// <summary>Uma evolução montada a partir de um registro do sistema anterior, ainda sem
/// paciente daqui (o id antigo é resolvido na execução).</summary>
public sealed record EvolucaoPlanejada(
    string Arquivo, string Chave, string IdPacienteAntigo, Evolucao Evolucao, AutorAnterior? Autor);

/// <summary>Um horário FUTURO da agenda antiga, ainda sem paciente daqui.</summary>
public sealed record AgendamentoPlanejado(
    string Chave, string IdPacienteAntigo, Agendamento Agendamento, string? ProfissionalAnterior);

/// <summary>O que um arquivo de prontuário vai render.</summary>
public sealed record ResumoArquivoClinico(
    string Arquivo, string Rotulo, int Registros, int Pacientes, int Novos, int JaImportados,
    int SemPaciente, int Vazios);

public sealed record ResumoAgendaAnterior(
    int Futuros, int FuturosNovos, int FuturosJaImportados, int Passados, int PacientesComHistorico,
    IReadOnlyList<string> ProfissionaisReconhecidos, IReadOnlyList<string> ProfissionaisSemCadastro);

/// <summary>A prévia do pacote inteiro — nada gravado.</summary>
public sealed record PreviaSmartClinic(
    PreviaImportacao Pacientes,
    IReadOnlyList<ResumoArquivoClinico> Prontuario,
    ResumoAgendaAnterior Agenda,
    IReadOnlyList<string> AutoresReconhecidos,
    IReadOnlyList<string> AutoresSemCadastro,
    IReadOnlyList<string> Avisos)
{
    /// <summary>Os planos, para a execução — não são para a tela.</summary>
    public IReadOnlyList<EvolucaoPlanejada> Evolucoes { get; init; } = [];
    public IReadOnlyList<AgendamentoPlanejado> Agendamentos { get; init; } = [];

    public int EvolucoesNovas => Prontuario.Sum(p => p.Novos);
    public bool TemTrabalho => Pacientes.TemTrabalho || EvolucoesNovas > 0 || Agenda.FuturosNovos > 0;
}

public sealed record ResultadoSmartClinic(
    ResultadoImportacao Pacientes,
    int EvolucoesCriadas, int EvolucoesPuladas, int EvolucoesSemPaciente,
    int AgendamentosCriados, int AgendamentosPulados, int AgendamentosSemPaciente,
    IReadOnlyList<string> Erros)
{
    public bool TeveErro => Erros.Count > 0 || Pacientes.TeveErro;

    /// <summary>Registros importados numa rodada ANTERIOR sem vínculo com a Equipe que
    /// ganharam o vínculo nesta — a Equipe pode ser cadastrada depois.</summary>
    public int Revinculados { get; init; }
}

/// <summary>Um registro importado sem profissional vinculado, com o texto por onde o
/// nome do autor se recupera (ver o repositório).</summary>
public sealed record RegistroImportadoSemProfissional(int Id, string? Texto);

/// <summary>Uma linha da CONFERÊNCIA: quanto o arquivo tem, quanto está no sistema e o que
/// ficou de fora — com o motivo, para a direção não precisar acreditar.</summary>
public sealed record ItemConferencia(
    string Rotulo, int NoArquivo, int NoSistema, IReadOnlyList<string> ForaComMotivo, int NaoContam)
{
    /// <summary>Nada ficou de fora sem motivo escrito — o que não entrou é vazio ou tem a
    /// linha e a razão listadas.</summary>
    public bool Completo => NoSistema + NaoContam + ForaComMotivo.Count >= NoArquivo && ForaComMotivo.Count == 0;

    public string Resumo => NoSistema == NoArquivo
        ? $"{Rotulo}: {NoArquivo} de {NoArquivo} no sistema."
        : $"{Rotulo}: {NoSistema} de {NoArquivo} no sistema"
          + (NaoContam > 0 ? $" · {NaoContam} não contam (sem conteúdo)" : "")
          + (ForaComMotivo.Count > 0 ? $" · {ForaComMotivo.Count} de fora (veja o motivo)" : "") + ".";
}

/// <summary>
/// A conferência da importação (set/2026 — "como saberei que funcionou toda a
/// importação?"). Depois de gravar, o sistema RELÊ o mesmo ZIP: a prévia da segunda leitura
/// diz, registro a registro, o que já está no sistema (pela chave de importação) e o que
/// não entrou — e isto é a prova, não a mensagem "concluído". Puro: recebe a prévia da
/// releitura e devolve as linhas.
/// </summary>
public static class ConferenciaSmartClinic
{
    public static IReadOnlyList<ItemConferencia> Montar(PreviaSmartClinic releitura)
    {
        var itens = new List<ItemConferencia>();

        var p = releitura.Pacientes;
        var fora = p.Linhas.Where(l => l.EhProblema)
            .Select(l => $"linha {l.Numero} ({l.Nome}): {l.Detalhe}")
            .ToList();
        // "Completar" na releitura é ficha que EXISTE no sistema (só não tem a chave desta
        // linha — a duplicata de lá, ou a ficha que já existia por outro caminho).
        itens.Add(new ItemConferencia("Fichas (pacientes.csv)", p.Linhas.Count,
            p.JaImportadas + p.Completar, fora, 0));

        foreach (var a in releitura.Prontuario)
        {
            var motivos = new List<string>();
            if (a.SemPaciente > 0)
                motivos.Add($"{a.SemPaciente} registro(s) de paciente que não entrou (veja as fichas de fora)");
            if (a.Novos > 0)
                motivos.Add($"{a.Novos} registro(s) ainda não gravado(s) — importe de novo");
            itens.Add(new ItemConferencia(a.Rotulo, a.Registros, a.JaImportados, motivos, a.Vazios));
        }

        var ag = releitura.Agenda;
        var motivosAgenda = new List<string>();
        if (ag.FuturosNovos > 0)
            motivosAgenda.Add($"{ag.FuturosNovos} horário(s) futuro(s) ainda não gravado(s) — importe de novo");
        var semFicha = ag.Futuros - ag.FuturosJaImportados - ag.FuturosNovos;
        if (semFicha > 0)
            motivosAgenda.Add($"{semFicha} horário(s) de paciente que não entrou (veja as fichas de fora)");
        itens.Add(new ItemConferencia("Agenda — horários de hoje em diante", ag.Futuros, ag.FuturosJaImportados, motivosAgenda, 0));
        itens.Add(new ItemConferencia("Agenda — visitas passadas (nas observações das fichas)",
            ag.Passados, ag.Passados, [], 0));

        return itens;
    }

    /// <summary>Tudo o que tinha de entrar está no sistema, e o que não entrou tem motivo escrito.</summary>
    public static bool Fechou(IReadOnlyList<ItemConferencia> itens)
        => itens.All(i => i.NoSistema + i.NaoContam >= i.NoArquivo || i.ForaComMotivo.Count > 0)
           && itens.All(i => !i.ForaComMotivo.Any(m => m.Contains("ainda não gravado")));
}
