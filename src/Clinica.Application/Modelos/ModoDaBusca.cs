namespace Clinica.Application.Modelos;

/// <summary>O que a busca de paciente está mostrando AGORA.</summary>
public enum ModoDaBusca
{
    /// <summary>Ninguém pediu nada. A lista está vazia por DESENHO, e nada foi ao banco.</summary>
    Ocioso,

    /// <summary>A sugestão da tela — "com horário hoje".</summary>
    Sugestao,

    /// <summary>O cadastro inteiro em ordem de nome, PEDIDO.</summary>
    Todos,

    /// <summary>Resultado da busca por um termo digitado.</summary>
    PorTermo
}

/// <summary>
/// O QUE A BUSCA DE PACIENTE ESTÁ FAZENDO — a decisão, num lugar só (set/2026).
///
/// Por que ela não mora na ViewModel
/// ---------------------------------
/// É a regra do <see cref="ResumoDaCarteira"/> e da <c>GradeSemana</c> (parcela 69) — <b>o
/// que decide o que a tela faz precisa morar onde o <c>dotnet test</c> alcança</b> —, e
/// aqui ela vale mais do que de costume: o <c>SeletorPacienteViewModel</c> vive no projeto
/// WPF do shell, que não compila no projeto de teste, e o que se decide aqui não é uma
/// frase, é <b>se a tela vai ao banco</b>.
///
/// A matriz decide TRÊS coisas de uma vez, e errar qualquer uma faz a tela mentir:
///
/// <list type="number">
///   <item><b>se consulta</b> — no <see cref="ModoDaBusca.Ocioso"/> nada sai da máquina;</item>
///   <item><b>qual pílula acende</b> — elas seguem o que ESTÁ na tela, nunca a flag de
///   configuração: com termo digitado NENHUMA acende, que é a verdade;</item>
///   <item><b>qual vazio aparece</b> — "nenhum paciente encontrado" é a resposta certa
///   para uma BUSCA que não achou e a errada para uma tela recém-aberta, onde ela seria
///   uma afirmação falsa sobre uma clínica de 2.238 fichas (e é essa leitura que leva a
///   cadastrar de novo quem já tem ficha — parcela 57).</item>
/// </list>
/// </summary>
public static class BuscaDePaciente
{
    /// <param name="semBuscaInicial">
    /// A tela abre sem consultar nada (opt-in). Falso nas telas de LISTAGEM, onde a lista
    /// É a resposta e abrir vazio seria trocar um defeito pelo oposto.
    /// </param>
    /// <param name="pediramLista">Alguém já clicou num dos chips.</param>
    /// <param name="temSugestao">A tela fornece uma sugestão (a agenda do dia).</param>
    /// <param name="sugestaoLigada">O chip da sugestão é o escolhido.</param>
    public static ModoDaBusca Modo(
        bool semBuscaInicial, bool pediramLista, string? termo,
        bool temSugestao, bool sugestaoLigada)
    {
        // O TERMO vence tudo: quem digitou está buscando, e nenhum dos modos de lista está
        // no ar. É o que impede a pílula de dizer "com horário hoje" sobre quatro
        // resultados de "pinheiro".
        if (!string.IsNullOrWhiteSpace(termo)) return ModoDaBusca.PorTermo;

        // Sem termo e sem ninguém ter pedido: a tela está ociosa e NADA vai ao banco.
        if (semBuscaInicial && !pediramLista) return ModoDaBusca.Ocioso;

        // ⚠️ `temSugestao` entra na conta: sem ele, uma tela que não fornece sugestão
        // cairia em `Sugestao` e a pílula "Com horário hoje" acenderia sobre o alfabeto —
        // a tela mentindo sobre o que está mostrando.
        return temSugestao && sugestaoLigada ? ModoDaBusca.Sugestao : ModoDaBusca.Todos;
    }

    /// <summary>
    /// Este modo vai ao banco? Só o <see cref="ModoDaBusca.Ocioso"/> não vai — e é este
    /// <c>false</c> que entrega o pedido da direção: doze telas abriam consultando o
    /// começo do alfabeto de 2.238 fichas, que não é o paciente de ninguém.
    /// </summary>
    public static bool Consulta(ModoDaBusca modo) => modo != ModoDaBusca.Ocioso;
}
