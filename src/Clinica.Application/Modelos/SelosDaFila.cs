using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>O peso visual de um selo do cartão — o XAML traduz em <c>Badge.*</c>.</summary>
public enum TomDoSelo
{
    /// <summary>Impede o passo, ou cobra AGORA com o paciente aqui (vermelho).</summary>
    Erro,

    /// <summary>Espera uma decisão do balcão (âmbar).</summary>
    Aviso,

    /// <summary>O recado bom do consultório (verde).</summary>
    Sucesso
}

/// <summary>Um selo do cartão da fila: o texto, o peso e a dica que o explica.</summary>
public sealed record SeloFila(string Texto, TomDoSelo Tom, string Dica);

/// <summary>
/// O que vira SELO no cartão da fila, e em que ordem (set/2026 — a cliente: "quanto mais
/// simples, melhor"; o desenho B de <c>docs/mockups/balcao-dois-desenhos.html</c>,
/// aprovado).
///
/// O cartão podia carregar OITO selos ao mesmo tempo — atraso, confirmação, pacote,
/// encerramento, guia pendente, termo pendente, encaixe e o "retorno do 2º código"
/// legado. Cada um entrou por uma boa razão, e somados viravam uma tela dentro do
/// cartão, que a recepcionista lê de relance com o paciente na frente. Esta regra não
/// tira informação: decide o que MERECE selo e manda o resto para a linha de contexto
/// do cartão ("Acupuntura · Dra. Ana · Sala 2 · pacote 3/10 · encaixe") ou para a
/// dica. Selo a menos não é dado a menos — todo dado que saiu do selo ganhou leitor
/// no mesmo commit.
///
/// A ordem é FIXA, e o cartão nunca escolhe sozinho:
/// <list type="number">
///   <item><b>Impede o passo</b> — termo do procedimento pendente. Sem ele o
///   procedimento para na maca.</item>
///   <item><b>Cobra agora, com o paciente aqui</b> — a guia pendente (o documento que
///   só é barato pedir agora) e o pacote no fim (penúltima, última, esgotado). "Pacote
///   3/10" não é aviso: é contexto, e vai para a linha de texto.</item>
///   <item><b>Estado da coluna</b> — o atraso, só em AGUARDANDO; o encerramento pelo
///   consultório, só em EM ATENDIMENTO. Fora da coluna certa o selo não diz nada.</item>
/// </list>
/// O que NÃO vira selo: "Confirmou" é um ✓ ao lado da hora (sem ✓ = foi avisado e não
/// respondeu; a dica explica); "Encaixe" e "pacote N/M" moram na linha de contexto; o
/// "retorno do 2º código" (legado) fica só na dica — é marca para a clínica limpar, não
/// aviso para o balcão.
///
/// Mora na Application, e não no ViewModel, pela razão de sempre: o que decide o que a
/// tela AFIRMA precisa morar onde o <c>dotnet test</c> alcança.
/// </summary>
public static class SelosDaFila
{
    /// <summary>O teto. Três é o que se lê de relance; o quarto já é uma lista.</summary>
    public const int Maximo = 3;

    /// <summary>
    /// A partir de quantas sessões RESTANTES o pacote vira selo: 2 (penúltima), 1
    /// (última) e 0 (esgotado — a sessão de hoje já não está coberta).
    /// </summary>
    public const int RestantesParaAvisar = 2;

    /// <param name="etapa">A coluna em que o cartão está.</param>
    /// <param name="termoPendente">O procedimento de hoje exige termo assinado e ele não foi assinado.</param>
    /// <param name="guiaPendente">Há guia de atendimento anterior por baixar.</param>
    /// <param name="sessoesUsadas">Sessões consumidas do pacote ativo; nulo sem pacote.</param>
    /// <param name="sessoesContratadas">Sessões contratadas do pacote ativo; nulo sem pacote.</param>
    /// <param name="atrasoMinutos">Minutos além da hora marcada sem check-in; nulo sem atraso.</param>
    /// <param name="encerradoEm">A hora em que o profissional encerrou o atendimento; nulo se não encerrou.</param>
    public static IReadOnlyList<SeloFila> Montar(
        EtapaFila etapa,
        bool termoPendente,
        bool guiaPendente,
        int? sessoesUsadas,
        int? sessoesContratadas,
        int? atrasoMinutos,
        DateTime? encerradoEm)
    {
        var selos = new List<SeloFila>(Maximo);

        // 1º — impede o passo.
        if (termoPendente)
            selos.Add(new SeloFila("Termo pendente", TomDoSelo.Erro,
                "Falta o termo do procedimento assinado — colha no “⋯” antes de o paciente entrar"));

        // 2º — cobra agora, com o paciente aqui.
        if (guiaPendente)
            selos.Add(new SeloFila("Guia pendente", TomDoSelo.Erro,
                "Guia de atendimento anterior por baixar — peça o documento enquanto ele está aqui"));

        if (sessoesUsadas is { } usadas && sessoesContratadas is { } contratadas)
        {
            var restantes = contratadas - usadas;
            if (restantes <= RestantesParaAvisar)
            {
                var (texto, tom) = restantes switch
                {
                    <= 0 => ("Pacote esgotado", TomDoSelo.Erro),
                    1 => ("Última do pacote", TomDoSelo.Aviso),
                    _ => ("Penúltima do pacote", TomDoSelo.Aviso)
                };
                selos.Add(new SeloFila(texto, tom,
                    $"Pacote {usadas}/{contratadas} — "
                    + (restantes <= 0
                        ? "a sessão de hoje já não está coberta; combine a renovação"
                        : "a próxima compra se combina agora, com o paciente aqui")));
            }
        }

        // 3º — estado da coluna.
        if (etapa == EtapaFila.Aguardando && atrasoMinutos is { } atraso)
            selos.Add(new SeloFila($"Atrasado {atraso} min", TomDoSelo.Aviso,
                "A hora marcada passou e o paciente ainda não chegou"));

        if (etapa == EtapaFila.EmAtendimento && encerradoEm is { } fim)
            selos.Add(new SeloFila($"Encerrado às {fim:HH\\:mm}", TomDoSelo.Sucesso,
                "O profissional encerrou o atendimento — o paciente está vindo fechar a sessão"));

        return selos.Count <= Maximo ? selos : selos.GetRange(0, Maximo);
    }
}
