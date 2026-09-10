using System.Globalization;
using Clinica.Application.Servicos;

namespace Clinica.Application.Modelos;

/// <summary>
/// Como ESTA sessão vai ser paga — a frase que o balcão lê ANTES de lançar (set/2026).
///
/// O problema que isto resolve
/// ---------------------------
/// A prévia do Novo atendimento dizia, para todo particular, <i>"Particular — R$ 180,00
/// (tabela do particular…). O pagamento é registrado no Finalizar da sessão."</i> — inclusive
/// para quem tem um pacote de dez sessões com sete pelo uso. Nesse caso a sessão NÃO é
/// cobrada: ela debita do pacote, e é isso que o Finalizar vai propor
/// (<c>FechamentoSessaoService.PrepararAsync</c> não sugere lançamento quando há pacote —
/// sessão comprada já foi paga).
///
/// Ou seja: a tela AFIRMAVA uma cobrança que não ia acontecer, com um valor ao lado. É a
/// família de defeito que este projeto mais persegue — nada falha, e quem lê acredita.
///
/// ⚠️ Quem decide qual pacote debita é <see cref="Clinica.Domain.Entities.PacotePaciente.ADebitar"/>,
/// a MESMA escolha do consumo automático e da proposta do Finalizar. Apontar aqui um pacote
/// diferente do que o serviço vai debitar muda o número que o paciente paga.
/// </summary>
/// <param name="Frase">
/// O que dizer na linha. VAZIA quando o convênio gera guia: ali o dinheiro vem pela
/// conciliação da guia, e escrever qualquer coisa sobre pagamento no balcão seria inventar
/// um passo que não existe.
/// </param>
/// <param name="DebitaDePacote">
/// A sessão sai de um pacote já comprado — não há o que cobrar. É o que a frase diz, e é o
/// que o Finalizar vai propor.
/// </param>
/// <param name="SemPreco">
/// Particular sem pacote e sem preço de tabela para esta modalidade: o valor é combinado no
/// balcão. A frase aponta ONDE se cadastra o preço, porque quem lê é quem pode fazê-lo.
/// </param>
public sealed record CobrancaDaSessao(string Frase, bool DebitaDePacote, bool SemPreco)
{
    /// <summary>
    /// O dinheiro sai em pt-BR FIXO, não na cultura da máquina.
    ///
    /// ⚠️ Esta frase é montada na APPLICATION, e a Application também é lida pela web de
    /// leitura, que roda em Linux — onde a cultura do processo costuma ser a invariante e
    /// <c>:C</c> imprime "¤180.00". Quem lê é uma clínica brasileira. (Foi o teste que
    /// mostrou: ele rodou com a cultura invariante e a frase saiu com o símbolo genérico.)
    /// </summary>
    private static readonly CultureInfo Brasil = new("pt-BR");

    /// <summary>O convênio gera guia: esta linha não tem o que dizer.</summary>
    public static readonly CobrancaDaSessao Nenhuma = new(string.Empty, false, false);

    /// <summary>
    /// Monta a frase a partir do que o balcão tem à mão: é particular?, qual o preço de
    /// tabela, e qual pacote debitaria.
    /// </summary>
    /// <param name="ehParticular">
    /// O convênio da ficha NÃO gera guia (parcela 60). É a mesma pergunta que o
    /// <c>FechamentoSessaoService</c> faz — e tem de ser, senão as duas telas discordam
    /// sobre a mesma sessão.
    /// </param>
    /// <param name="preco">A proposta da tabela do particular. <c>Nenhum</c> = não cadastrada.</param>
    /// <param name="pacote">O pacote que debitaria, ou nulo.</param>
    public static CobrancaDaSessao Montar(
        bool ehParticular, PrecoProposto preco, SaldoPacote? pacote)
    {
        if (!ehParticular) return Nenhuma;

        // O PACOTE vem primeiro porque ele responde a pergunta inteira: tendo sessão
        // comprada, não há valor a combinar nem cobrança a registrar.
        if (pacote is not null)
            return new CobrancaDaSessao(
                $"Particular — pacote \"{pacote.Nome}\" ({pacote.SaldoRotulo}): esta sessão "
                + "DEBITA do pacote, sem cobrança no caixa.",
                DebitaDePacote: true, SemPreco: false);

        if (preco.Houve)
            return new CobrancaDaSessao(
                $"Particular — {preco.Valor.ToString("C", Brasil)} ({preco.Procedencia}). O pagamento é "
                + "registrado no Finalizar da sessão.",
                DebitaDePacote: false, SemPreco: false);

        // ⚠️ A frase aponta a porta do BALCÃO (set/2026): ela mandava a recepcionista ao
        // "Gerente → Tabela de preço", que é outro app e outra pessoa — e a Recepção publica
        // a mesma tela desde que ela nasceu. Instrução que manda procurar no lugar errado é
        // pior que nenhuma, e foi metade do "não consegui entender como fazer um atendimento
        // particular".
        return new CobrancaDaSessao(
            "Particular — sem preço cadastrado para esta modalidade. Cadastre em "
            + "\"Particular e pacotes → Preço da sessão\", ou combine o valor no Finalizar "
            + "da sessão.",
            DebitaDePacote: false, SemPreco: true);
    }
}
