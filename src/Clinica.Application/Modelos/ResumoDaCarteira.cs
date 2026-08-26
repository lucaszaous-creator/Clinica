namespace Clinica.Application.Modelos;

/// <summary>
/// O que a tela de Pacientes AFIRMA sobre a lista que está mostrando: a linha de resumo e,
/// quando não há nada para mostrar, o vazio certo para a pergunta certa.
///
/// Por que isto não mora na ViewModel
/// ----------------------------------
/// É a regra da <c>GradeSemana</c> (parcela 69) e do <see cref="ResumoSessaoAnterior"/>
/// (parcela 77): <b>o que decide o que a tela AFIRMA precisa morar onde o
/// <c>dotnet test</c> alcança</b> — projeto WPF não compila no projeto de teste, e frase de
/// tela é justamente o tipo de coisa que erra em silêncio.
///
/// E aqui ela erra caro. A lista tem TETO e a busca alcança o cadastro inteiro (parcela 88,
/// 3ª rodada), então há três perguntas diferentes se escondendo atrás de "nenhum item":
///
/// <list type="number">
///   <item><b>a clínica não atendeu ninguém ainda</b>;</item>
///   <item><b>a busca não achou a pessoa no cadastro</b> — e é esta que precisa mandar
///   conferir a grafia, porque a leitura errada ("existe, só não foi atendido") é a que
///   leva a cadastrar quem já tem ficha, partindo o histórico em dois (parcela 57);</item>
///   <item><b>o "só quem sumiu" escondeu o que a busca trouxe</b> — dizer "não achei
///   ninguém" aqui mentiria sobre a causa, e mandaria cadastrar de novo alguém que a tela
///   ACABOU de achar.</item>
/// </list>
///
/// Um estado vazio por pergunta, como no resto do projeto (parcela 37).
/// </summary>
public sealed record ResumoDaCarteira(string Resumo, string TituloVazio, string DescricaoVazio)
{
    /// <param name="lidos">Quantos vieram do banco, ANTES do recorte de "sumidos".</param>
    /// <param name="mostrados">Quantos sobraram depois dele — o que a tela desenha.</param>
    /// <param name="termo">O termo JÁ resolvido no SQL, ou nulo/branco quando não há busca.</param>
    /// <param name="teto">O limite da consulta. Atingi-lo é DITO: lista cortada que se
    /// anuncia como a carteira inteira é corte silencioso.</param>
    public static ResumoDaCarteira Montar(
        int lidos, int mostrados, string? termo, bool somenteSumidos,
        int teto, int diasParaDestaque)
    {
        var busca = (termo ?? string.Empty).Trim();
        var buscando = busca.Length > 0;

        // ⚠️ A ordem importa: quem ESCONDEU responde primeiro. Só depois de descartar o
        // recorte é que "não achei" passa a ser verdade sobre o cadastro.
        var (titulo, descricao) = (lidos > 0 && mostrados == 0)
            ? ("Ninguém está sem vir há mais tempo",
               $"Os {lidos} paciente(s) desta lista não estão marcados como sumidos — vieram "
               + $"há menos de {diasParaDestaque} dias, ou ainda não têm sessão registrada. "
               + "Desmarque “Só quem sumiu” para ver todos.")
            : buscando
                ? ("Ninguém com esse nome ou CPF",
                   $"A busca “{busca}” não achou ninguém no cadastro da clínica — nem entre "
                   + "quem já veio, nem entre quem ainda não veio. Confira a grafia antes de "
                   + "cadastrar de novo: ficha repetida parte o histórico do paciente em duas.")
                : ("Nenhum paciente em tratamento",
                   "Entra aqui quem a clínica já atendeu: o horário precisa ter tido a presença "
                   + "confirmada na recepção. Busque pelo nome ou CPF para alcançar quem ainda "
                   + "não veio.");

        if (buscando)
            return new ResumoDaCarteira(
                $"Busca “{busca}” — {mostrados} paciente(s) no cadastro da clínica."
                + (somenteSumidos ? " Só quem está sem vir há mais tempo." : string.Empty),
                titulo, descricao);

        var quantos = somenteSumidos
            ? $"{mostrados} de {lidos} paciente(s), só quem está sem vir há mais tempo"
            : $"{lidos} paciente(s) atendidos na clínica, do que veio por último ao mais antigo";

        return new ResumoDaCarteira(
            lidos >= teto
                ? quantos + $" — os {teto} mais recentes, que é o teto desta tela. "
                  + "Busque pelo nome ou CPF para alcançar quem está fora, inclusive quem nunca veio."
                : quantos + ".",
            titulo, descricao);
    }
}
