using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// A IDENTIDADE CLÍNICA da pessoa que está na sala (parcela 74).
///
/// O que ela corrige
/// -----------------
/// O cabeçalho do consultório mostrava um avatar de iniciais, o nome e uma linha de
/// contexto. Quem atende precisa, antes de abrir a boca, de quatro coisas que o sistema
/// já tinha e não mostrava: <b>a idade</b> (a conduta de um paciente de 78 anos não é a
/// de um de 30), <b>o convênio</b> (é ele que decide o que pode ser pedido), <b>desde
/// quando esta pessoa se trata aqui</b> e <b>o que não se pode esquecer sobre ela</b>.
///
/// ⚠️ A ALERGIA sai do balde de "alertas" e vira parte da identidade, e isso é decisão:
/// alerta é uma faixa que se lê uma vez e se ignora nas próximas quarenta sessões — é a
/// razão pela qual o projeto recusa alerta que dispara para todo mundo desde a parcela 26.
/// A alergia não é um aviso do dia: é um atributo da pessoa, e por isso fica no crachá
/// dela, ao lado do nome, enquanto o prontuário estiver aberto.
///
/// ⚠️ E os ÚLTIMOS DIAGNÓSTICOS são o primeiro leitor da <c>HipoteseDiagnostica</c> que a
/// parcela 73 criou. Sem eles, a hipótese seria mais um campo gravado sem leitor — o
/// defeito recorrente do projeto cometido na parcela seguinte à que criou o dado.
/// </summary>
/// <param name="PacienteId">Quem.</param>
/// <param name="Nome">Como ele se chama.</param>
/// <param name="Foto">
/// A miniatura do cadastro (~160 px), que já viaja na própria linha do paciente. A foto
/// grande mora noutra tabela e não é carregada aqui: o cabeçalho é desenhado em 48 px.
/// </param>
/// <param name="Idade">Anos completos hoje. Null quando a data de nascimento não foi
/// cadastrada — e null é a resposta certa, porque "0 ano" seria um recém-nascido.</param>
/// <param name="Sexo">Como está no cadastro.</param>
/// <param name="ConvenioNome">
/// O nome da OPERADORA, resolvido pelo catálogo com a família como caminho de baixo
/// (parcela 68). Escrever a família aqui daria "Convênio personalizado" no crachá de quem
/// tem plano de verdade.
/// </param>
/// <param name="Carteirinha">O número, para quem precisa dele na mão.</param>
/// <param name="CarteirinhaVencida">A validade já passou — o balcão resolve, mas quem
/// atende precisa saber que a guia de hoje pode ser recusada.</param>
/// <param name="PrimeiraSessao">Quando esta pessoa foi atendida aqui pela primeira vez.
/// Null = ainda não foi atendida (cadastro novo).</param>
/// <param name="TotalSessoes">Quantas sessões ela já teve. É o número que diz, sem ler
/// nada, se este é um tratamento longo ou uma primeira consulta.</param>
/// <param name="Alergias">O que ela não pode receber. Ver o comentário da classe.</param>
/// <param name="ProblemasAtivos">Diagnósticos e condições em curso.</param>
/// <param name="UltimosDiagnosticos">As hipóteses das últimas sessões, da mais recente
/// para a mais antiga.</param>
public sealed record CabecalhoClinicoPaciente(
    int PacienteId,
    string Nome,
    byte[]? Foto,
    int? Idade,
    Sexo Sexo,
    string ConvenioNome,
    string? Carteirinha,
    bool CarteirinhaVencida,
    DateOnly? PrimeiraSessao,
    int TotalSessoes,
    IReadOnlyList<string> Alergias,
    IReadOnlyList<string> ProblemasAtivos,
    IReadOnlyList<string> UltimosDiagnosticos)
{
    /// <summary>
    /// A linha de identificação: "45 anos · feminino · Amil · desde 26/03/2022 · 18 sessões".
    ///
    /// Montada aqui, e não no XAML, porque ela pula o que não existe: um paciente sem data
    /// de nascimento não pode produzir "· anos ·" com um vão no meio, e cadastro novo não
    /// tem "desde". Frase montada por concatenação de binding não sabe pular.
    /// </summary>
    public string Linha
    {
        get
        {
            var partes = new List<string>();
            if (Idade is { } i) partes.Add($"{i} anos");
            partes.Add(Sexo == Sexo.Feminino ? "feminino" : "masculino");
            partes.Add(ConvenioNome);
            if (PrimeiraSessao is { } p) partes.Add($"paciente desde {p:dd/MM/yyyy}");
            if (TotalSessoes > 0)
                partes.Add(TotalSessoes == 1 ? "1 sessão" : $"{TotalSessoes} sessões");
            return string.Join("  ·  ", partes);
        }
    }

    /// <summary>"Alergia a dipirona, a AAS" — uma frase só, para caber numa linha.</summary>
    public string? AlergiasTexto => Alergias.Count == 0
        ? null
        : (Alergias.Count == 1 ? "Alergia: " : "Alergias: ") + string.Join(" · ", Alergias);

    public bool TemAlergia => Alergias.Count > 0;

    public string? ProblemasTexto => ProblemasAtivos.Count == 0
        ? null : string.Join(" · ", ProblemasAtivos);

    public string? DiagnosticosTexto => UltimosDiagnosticos.Count == 0
        ? null : string.Join(" · ", UltimosDiagnosticos);

    /// <summary>Vazio quando não há o que dizer — a região some em vez de mostrar traços.</summary>
    public bool TemContextoClinico
        => TemAlergia || ProblemasAtivos.Count > 0 || UltimosDiagnosticos.Count > 0;
}
