using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Clinica.Infrastructure;

/// <summary>
/// Enum gravado como TEXTO que sobrevive a um valor que este build ainda não conhece.
///
/// O problema que ele resolve, e por que não é hipotético
/// -----------------------------------------------------
/// Os cinco apps se auto-atualizam por Velopack, <b>um canal por app</b>, e dividem UM banco.
/// Então existe sempre uma janela — de minutos ou de dias — em que o Consultório já atualizou
/// e a Recepção não, e o app velho lê uma linha escrita pelo novo.
///
/// Colunas e tabelas novas atravessam essa janela sem incidente: o EF só pede as colunas que
/// conhece. <b>Valor novo de enum, não.</b> O <c>HasConversion&lt;string&gt;()</c> do EF chama
/// <c>Enum.TryParse</c> e, ao não achar o nome, LANÇA — e a exceção não derruba a linha, derruba
/// a CONSULTA. Em 14/08/2026 a clínica perdeu a tela de Prescrições inteira por causa de um
/// <c>DocumentoClinico</c> de tipo <c>TermoProcedimento</c>, com a frase
/// "Cannot convert string value 'TermoProcedimento'…" no lugar da lista — que não diz a ninguém
/// que o que falta é atualizar o programa.
///
/// As duas metades da decisão
/// --------------------------
/// <b>Ler é tolerante</b>: o desconhecido vira o valor sentinela, a linha aparece e a tela
/// funciona. Cair no primeiro valor do enum (o <c>default</c>) seria pior do que estourar —
/// um termo de procedimento apareceria como "Receita", que é mentir sobre um registro de
/// prontuário.
///
/// <b>Escrever é RECUSADO</b>, e é o que torna a tolerância segura. Sem essa metade, o app
/// velho leria "não sei o que é isto" e, no primeiro Salvar, gravaria de volta o sentinela
/// POR CIMA do tipo verdadeiro — apagando em silêncio o que a versão nova tinha registrado.
/// Registro clínico não se apaga (Lei 13.787/2018), e apagar por conversão é a forma mais
/// discreta de fazê-lo. A recusa mora AQUI, e não nas telas, porque a escrita tem muitas
/// portas (emitir, cancelar, salvar modelo) e uma que esquecesse bastaria.
/// </summary>
/// <typeparam name="TEnum">O enum gravado como texto.</typeparam>
public sealed class ConversorEnumTolerante<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    /// <param name="desconhecido">
    /// O valor que representa "este build não conhece o nome que veio do banco". Ele não é um
    /// valor de negócio: não entra em seletor, não sai impresso e não pode ser gravado.
    /// </param>
    public ConversorEnumTolerante(TEnum desconhecido)
        : base(valor => ParaBanco(valor, desconhecido),
               texto => DoBanco(texto, desconhecido))
    {
    }

    private static string ParaBanco(TEnum valor, TEnum desconhecido)
        => EqualityComparer<TEnum>.Default.Equals(valor, desconhecido)
            ? throw new RegistroDeVersaoMaisNovaException(typeof(TEnum).Name)
            : valor.ToString();

    private static TEnum DoBanco(string texto, TEnum desconhecido)
        => Enum.TryParse<TEnum>(texto, out var lido) ? lido : desconhecido;
}

/// <summary>
/// A recusa de gravar um registro que esta versão não entende.
///
/// Tem tipo PRÓPRIO porque o EF embrulha tudo o que sai da gravação num
/// <c>DbUpdateException</c> cuja mensagem é "An error occurred while saving the entity
/// changes. See the inner exception for details." — e é ela que chegaria à tela.
/// <c>ClinicaRepositorio.SalvarAsync</c> reconhece este tipo e devolve a frase de baixo, que
/// diz o que fazer. Uma <see cref="InvalidOperationException"/> qualquer não serviria: o
/// tradutor passaria a expor mensagem interna do EF junto com esta.
/// </summary>
public sealed class RegistroDeVersaoMaisNovaException(string tipo)
    : InvalidOperationException(
        $"Este registro é de um tipo que esta versão do sistema não conhece ({tipo}), e por "
        + "isso NÃO pode ser gravado — gravá-lo apagaria o tipo verdadeiro. Atualize o "
        + "sistema e tente de novo.");
