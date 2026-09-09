namespace Clinica.Domain;

/// <summary>
/// A IDADE, numa definição só — e a recusa da data IMPLAUSÍVEL (set/2026).
///
/// O que ela corrige
/// -----------------
/// O crachá do consultório escreveu <b>"1851 anos"</b> na tela que a direção fotografou.
/// A conta estava certa: quem estava errada era a DATA, vinda da importação do Smart
/// Clinic (2.238 fichas, boa parte com campos incompletos) ou de um dedo no teclado. O
/// cabeçalho imprimia o que a subtração devolvia, sem perguntar se aquilo podia ser uma
/// pessoa viva.
///
/// ⚠️ E a conta existia em TRÊS cópias — <c>ConsultorioService.IdadeEm</c>,
/// <c>FichaPacienteViewModel.Idade</c> e <c>NovoAtendimentoViewModel.IdadeEm</c> —, as
/// três com o mesmo algoritmo escrito à mão e as três imprimindo a idade impossível.
/// Duas definições da mesma regra divergem na primeira correção, e a que ninguém lembra
/// de ajustar é sempre a segunda: corrigir as três à mão seria criar o próximo defeito.
///
/// A REGRA É A DA MEDIDA CLÍNICA: recusa-se o IMPLAUSÍVEL, nunca o anormal
/// ................................................................
/// É a mesma decisão do <c>CatalogoMedidas</c> ("2500 kg é dedo no teclado; 210 kg é
/// anormal e possível, e recusá-lo esconderia quem precisa de atenção"). O teto é
/// <see cref="AnosPlausiveis"/> = 130: o recorde humano verificado é 122, então 130 deixa
/// passar qualquer pessoa viva com folga e ainda assim pega o ano digitado errado. Um
/// teto apertado — 100, digamos — apagaria a idade da paciente de 103 anos, que é
/// justamente aquela em que a idade MUDA a conduta.
///
/// ⚠️ AUSENTE ≠ IMPLAUSÍVEL, e é por isso que há dois métodos. Ficha sem data de
/// nascimento é o caso NORMAL da clínica (criança, paciente cadastrado pela carteirinha,
/// quem chegou sem documento): ali não há nada a dizer, e a linha do crachá simplesmente
/// pula a idade. Data implausível é um ERRO DE CADASTRO, e some-la em silêncio deixaria a
/// ficha errada para sempre — quem a conserta é o balcão, e ele precisa ver que ela
/// existe. Daí o terceiro estado escrito ("idade a conferir"), que é a regra do projeto
/// desde sempre: falha nunca se exibe como sucesso, e ausência não se confunde com erro.
/// </summary>
public static class IdadeDoPaciente
{
    /// <summary>
    /// O teto de plausibilidade, em anos. Ver o comentário da classe: ele não é o limite
    /// da longevidade humana, é a fronteira acima da qual a data só pode ter sido digitada
    /// errada.
    /// </summary>
    public const int AnosPlausiveis = 130;

    /// <summary>
    /// Anos COMPLETOS, ou <c>null</c> quando não há data ou ela não descreve uma pessoa
    /// viva (futuro, ou mais velha que <see cref="AnosPlausiveis"/>).
    ///
    /// ⚠️ A conta é pelo ANIVERSÁRIO, não pela subtração dos anos: <c>hoje.Year - n.Year</c>
    /// erra metade do ano de todo mundo, e num crachá clínico a idade errada muda conduta
    /// — a dose pediátrica e a geriátrica não são a mesma.
    /// </summary>
    public static int? Anos(DateOnly? nascimento, DateOnly hoje)
    {
        if (nascimento is not { } n || n > hoje) return null;
        var anos = hoje.Year - n.Year;
        if (hoje < n.AddYears(anos)) anos--;
        return anos > AnosPlausiveis ? null : anos;
    }

    /// <summary>
    /// A idade COMO SE ESCREVE ao lado da data: "38 anos", "1 ano", ou "data a conferir"
    /// quando ela é implausível. <c>null</c> quando não há data — aí não há nada a dizer, e
    /// quem chama decide o que pôr no lugar ("—", ou nada).
    ///
    /// ⚠️ Existe porque a frase também estava em três cópias — a ficha do balcão, a ficha
    /// do faturamento e o PDF que o paciente leva embora. Corrigir a CONTA e deixar a
    /// FRASE espalhada seria fazer metade do serviço: é a frase que o paciente lê.
    /// </summary>
    public static string? Texto(DateOnly? nascimento, DateOnly hoje)
    {
        if (nascimento is null) return null;
        return Anos(nascimento, hoje) is { } anos
            ? (anos == 1 ? "1 ano" : $"{anos} anos")
            : "data a conferir";
    }

    /// <summary>
    /// A ficha TEM data de nascimento e ela não pode ser de uma pessoa viva — data futura,
    /// ou idade acima do teto. É o que faz o erro de cadastro APARECER em vez de sumir.
    ///
    /// Falso quando não há data: ausência não é erro (ver o comentário da classe).
    /// </summary>
    public static bool Implausivel(DateOnly? nascimento, DateOnly hoje)
        => nascimento is { } n && Anos(n, hoje) is null;
}
