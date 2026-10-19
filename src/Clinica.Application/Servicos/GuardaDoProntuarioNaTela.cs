using System.Globalization;
using System.Text;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Os números da guarda da clínica inteira, já escritos.</summary>
public sealed record RetratoDaGuarda(
    string Pacientes, string ComRegistro, string Registros, string Elegiveis, string MaisAntigo, string Resumo);

/// <summary>A guarda de UM paciente, já escrita.</summary>
public sealed record GuardaDoPacienteNaTela(string Nome, string Situacao, string Detalhe);

/// <summary>Uma linha de "quem abriu este prontuário".</summary>
public sealed record LinhaDeAcessoAoProntuario(string Quando, string Operador, string Porta);

/// <summary>
/// O que a tela da GUARDA DO PRONTUÁRIO do Gerente AFIRMA (out/2026, etapa 6 — a tela saiu do
/// WPF para o Blazor). Era o <c>GuardaProntuarioViewModel</c> + <c>LinhaAcessoProntuario</c>:
/// o retrato da clínica (com a frase que diz que "0 elegíveis" é o NORMAL), a guarda de um
/// paciente (a contagem sai do CATÁLOGO, parcela 72), a trilha de leitura — "quem abriu este
/// prontuário" — e o que a exportação grava (a pasta datada e o UTF-8 com BOM). Nada aqui lê o
/// banco: a tela lê com os MESMOS <see cref="GuardaProntuarioService"/>,
/// <see cref="AcessoProntuarioService"/> e <see cref="ExportacaoProntuarioService"/>.
///
/// O que NÃO há aqui, de propósito: eliminar prontuário vencido. O prazo é PISO de guarda, não
/// agendamento de destruição (Lei 13.787/2018, art. 7º).
/// </summary>
public static class GuardaDoProntuarioNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Quantos acessos a trilha do paciente traz (corte no banco), como no WPF.</summary>
    public const int LimiteDeAcessos = 100;

    public static string Prazo => $"{GuardaProntuario.AnosDeGuarda} anos";

    /// <summary>
    /// O retrato. "0 elegíveis" é a resposta NORMAL numa clínica com menos de 20 anos de casa;
    /// sem a frase, o zero se lê como consulta quebrada.
    /// </summary>
    public static RetratoDaGuarda Retrato(ResumoGuarda r) => new(
        Pacientes: r.Pacientes.ToString("N0", PtBr),
        ComRegistro: r.ComRegistro.ToString("N0", PtBr),
        Registros: r.TotalDeRegistros.ToString("N0", PtBr),
        Elegiveis: r.JaCumpriramPrazo.ToString("N0", PtBr),
        MaisAntigo: r.RegistroMaisAntigo?.ToString("dd/MM/yyyy", PtBr) ?? "—",
        Resumo: r.NadaElegivel
            ? "Nenhum prontuário cumpriu os 20 anos ainda — o que é o esperado, e "
              + "significa que TODOS continuam sob guarda obrigatória."
            : $"{r.JaCumpriramPrazo} prontuário(s) já cumpriram o prazo mínimo e são "
              + "elegíveis a eliminação. O sistema não elimina nada: a decisão é da "
              + "clínica, com a comissão de revisão prevista no art. 7º da Lei 13.787/2018.");

    /// <summary>
    /// A guarda de uma pessoa. A contagem sai do CATÁLOGO (<see cref="SituacaoGuarda.DescreverContagens"/>):
    /// escrita à mão ela contava cinco naturezas enquanto o prazo era calculado sobre sete.
    /// </summary>
    public static GuardaDoPacienteNaTela Paciente(SituacaoGuarda s, DateOnly hoje) => new(
        Nome: s.PacienteNome,
        Situacao: s.Descrever(hoje),
        Detalhe: s.DescreverContagens()
                 + (s.UltimoRegistro is { } data
                     ? $" — último registro em {data.ToString("dd/MM/yyyy", PtBr)}"
                       + (s.Origem is { } o ? $" ({o})" : string.Empty)
                     : " — sem registro no prontuário"));

    /// <summary>
    /// A linha da trilha de leitura. A PORTA é o rótulo humano gravado pelo serviço
    /// ("Prontuário clínico aberto"); a Acao ("ProntuarioAcessado:Documento") é identificador, e
    /// identificador na tela é o defeito da parcela 41.
    /// </summary>
    public static LinhaDeAcessoAoProntuario Acesso(EventoAuditoria e) => new(
        Quando: e.DataHora.ToString("dd/MM/yyyy HH:mm", PtBr),
        Operador: e.Operador,
        Porta: string.IsNullOrWhiteSpace(e.Detalhe) ? "Prontuário acessado" : e.Detalhe!);

    /// <summary>A frase sobre a trilha — o vazio DIZ desde quando ela existe.</summary>
    public static string ResumoDosAcessos(int quantos)
        => quantos == 0
            ? "Nenhum acesso registrado — a trilha de leitura existe desde a versão da "
              + "parcela 52; aberturas anteriores a ela não deixaram rastro."
            : $"{quantos} acesso(s) registrados"
              + (quantos == LimiteDeAcessos ? $" (mostrando os {LimiteDeAcessos} mais recentes)" : string.Empty)
              + ".";

    // ---- Exportação ----

    public const string TituloDaConfirmacao = "Exportar o prontuário da clínica";

    public const string MensagemDaConfirmacao =
        "Vai sair o prontuário de TODOS os pacientes, com dado de saúde, em "
        + "arquivos que qualquer pessoa abre. Grave em local controlado e "
        + "entregue só a quem tem direito de recebê-lo.";

    /// <summary>
    /// A subpasta que recebe os CSV: vários arquivos numa PASTA, não um zip — o destino costuma
    /// ser um pendrive entregue a outro fornecedor, e pasta se confere abrindo.
    /// </summary>
    public static string PastaDaExportacao(DateTime agora)
        => $"prontuario-{agora.ToString("yyyy-MM-dd-HHmm", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// O conteúdo de cada CSV em UTF-8 COM BOM, pela convenção do projeto: sem ele o Excel lê
    /// como ANSI e "Ocupação" vira "OcupaÃ§Ã£o".
    /// </summary>
    public static byte[] Bytes(string conteudo)
    {
        var codificacao = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return [.. codificacao.GetPreamble(), .. codificacao.GetBytes(conteudo)];
    }

    public static string ExportacaoGravada(string destino, int arquivos)
        => $"Exportação gravada em {destino} — {arquivos} arquivo(s).";
}
