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
}
