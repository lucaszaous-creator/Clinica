using Clinica.Domain;

namespace Clinica.Desktop.Controls;

/// <summary>
/// Leitura dos números que a clínica digita. Fica no shell porque dinheiro é digitado em
/// mais de um módulo — no lançamento do Financeiro e no fechamento da sessão da Recepção
/// — e duas cópias da mesma regra viram dois comportamentos diferentes no dia em que uma
/// delas for corrigida.
///
/// ⚠️ <b>A conta em si mora em <see cref="LeituraNumero"/>, no DOMÍNIO.</b> Aqui ficou só
/// o PISO de cada campo, que é decisão de tela. A separação não é arrumação: este projeto
/// é <c>net8.0-windows</c> e a suíte de testes não o alcança, então enquanto a regra morou
/// aqui ela passou parcelas lendo <c>"40.5"</c> como <b>405</b> sem nenhum teste para
/// dizer o contrário — e a documentação deste arquivo prometia justamente o que ele não
/// fazia. Os três métodos abaixo continuam sendo a porta que as telas usam; o que mudou é
/// que agora existe rede embaixo deles.
/// </summary>
public static class Valores
{
    /// <summary>
    /// Dinheiro que precisa ser POSITIVO: o valor do lançamento, o percentual do repasse,
    /// o preço da guia. Aceita "1.250,00" e "1250.00" — e agora os dois querem dizer mil
    /// duzentos e cinquenta. O sinal nunca sai daqui: quem decide entrada ou saída é o
    /// tipo do lançamento.
    /// </summary>
    public static bool TentarLerDecimal(string? texto, out decimal valor)
        => LeituraNumero.TentarLer(texto, out valor) && valor > 0;

    /// <summary>
    /// Dinheiro que PODE ser zero: o pacote dado como cortesia, o insumo doado, o preço
    /// promocional de tabela. É <see cref="TentarLerDecimal"/> sem o piso — e existe
    /// separado dele de propósito, porque os dois casos precisam de respostas diferentes:
    /// no valor do repasse, zero é engano; na venda do pacote, zero é a decisão que a
    /// clínica tomou com o paciente.
    ///
    /// Campo em branco continua sendo <c>false</c>: quem quer dizer "não cobrei nada"
    /// digita zero; quem deixa vazio está dizendo "use o preço da tabela", e cabe a quem
    /// chama distinguir os dois ANTES de perguntar aqui.
    /// </summary>
    public static bool TentarLerValor(string? texto, out decimal valor)
        => LeituraNumero.TentarLer(texto, out valor) && valor >= 0;

    /// <summary>
    /// Quantidade de insumo: aceita fração ("0,5" de um frasco) e recusa o negativo —
    /// quem tira do estoque é o tipo do movimento, não o sinal digitado. Campo vazio
    /// devolve zero e vale como "não consumiu", que é o caso comum.
    /// </summary>
    public static bool TentarLerQuantidade(string? texto, out decimal quantidade)
    {
        quantidade = 0;
        if (string.IsNullOrWhiteSpace(texto)) return true;

        return LeituraNumero.TentarLer(texto, out quantidade) && quantidade >= 0;
    }

    /// <summary>
    /// Contagem: sessões do pacote, dias de validade, número de parcelas. Recusa
    /// separador — contagem não tem grupo nem fração, e aceitar "1.234" como 1234 é a
    /// mesma porta por onde "40.5" entrava valendo 405.
    /// </summary>
    public static bool TentarLerInteiro(string? texto, out int valor)
    {
        valor = 0;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        var limpo = texto.Trim();
        foreach (var c in limpo)
            if (!char.IsAsciiDigit(c) && c != '-' && c != '+') return false;

        return int.TryParse(limpo, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out valor);
    }
}
