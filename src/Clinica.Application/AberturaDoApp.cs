namespace Clinica.Application;

/// <summary>
/// A PRIMEIRA TELA vem antes do resto (out/2026 — a direção relatou 15–30 s entre o login e os
/// dados da tela inicial, nos cinco apps; medido em <c>AberturaDosAppsTests</c>).
///
/// Na abertura, junto com a tela inicial, o app disparava de uma vez o que roda por trás dela:
/// os avisos do cabeçalho (pedidos da enfermagem, respostas dos pacientes, assinaturas de
/// infusão), a conclusão automática das sessões, a cópia de segurança
/// do Gerente e a rodada/aviso de pendências do Faturamento. Nada disso é o que a pessoa
/// espera ao entrar — e tudo disputava com a tela inicial as poucas conexões abertas do pool:
/// cada conexão FÍSICA nova com a VPS custa 2,7–4,7 s (TCP + TLS + mTLS + autenticação).
///
/// Agora a tela inicial avisa quando terminou de ler (<see cref="PrimeiraTelaPronta"/>: a base
/// das telas Blazor e o "Meu dia" do Consultório), e quem roda por trás espera esse aviso
/// (<see cref="DepoisDaPrimeiraTelaAsync"/>) — com teto, porque a tela inicial de quem não tem
/// permissão para a tela de sempre pode ser uma tela que não avisa, e o fundo não pode esperar
/// para sempre.
/// </summary>
public static class AberturaDoApp
{
    /// <summary>O teto da espera: passado ele, o fundo segue mesmo sem o aviso da tela.</summary>
    public static readonly TimeSpan TetoDaEspera = TimeSpan.FromSeconds(20);

    private static readonly TaskCompletionSource Pronta = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Já houve a primeira tela com dados neste processo?</summary>
    public static bool PrimeiraTelaJaPronta => Pronta.Task.IsCompleted;

    /// <summary>A primeira tela do app terminou de ler. Chamadas seguintes não fazem nada.</summary>
    public static void PrimeiraTelaPronta() => Pronta.TrySetResult();

    /// <summary>
    /// Espera a primeira tela ter os dados (ou o <paramref name="teto"/>, o que vier primeiro).
    /// Nunca lança: é espera de cortesia, não condição.
    /// </summary>
    public static Task DepoisDaPrimeiraTelaAsync(TimeSpan? teto = null)
        => Pronta.Task.IsCompleted ? Task.CompletedTask : Task.WhenAny(Pronta.Task, Task.Delay(teto ?? TetoDaEspera));
}
