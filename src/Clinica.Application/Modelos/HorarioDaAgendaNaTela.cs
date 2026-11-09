namespace Clinica.Application.Modelos;

/// <summary>
/// O que a JANELA DO HORÁRIO afirma na troca entre convênio e particular de uma sessão já
/// atendida (nov/2026). Os textos moram aqui, com teste, porque são eles que dizem à
/// recepção o que acontece com as GUIAS e com a COBRANÇA antes do clique — e texto de
/// confirmação escrito no Razor é texto que ninguém confere.
/// </summary>
public static class HorarioDaAgendaNaTela
{
    public static string RotuloDaTroca(bool jaEhParticular)
        => jaEhParticular ? "Foi pelo convênio…" : "Foi particular…";

    public static string DicaDaTroca(bool jaEhParticular)
        => jaEhParticular
            ? "Desfaz a marca de particular: as guias do convênio são geradas para este atendimento"
            : "A sessão foi particular: dispensa as guias do convênio e deixa a cobrança para o caixa";

    public static string TituloDaTroca(bool paraParticular)
        => paraParticular ? "Este atendimento foi particular?" : "Este atendimento foi pelo convênio?";

    public static string PerguntaDaTroca(string paciente, bool paraParticular)
        => paraParticular
            ? $"O atendimento de {paciente} passa a ser PARTICULAR: as guias em aberto do convênio são "
              + "dispensadas (nada é apagado) e a sessão entra na conciliação dos particulares, para ser "
              + "cobrada no caixa. O convênio da ficha não muda. Guia já baixada ou em lote impede a troca."
            : $"O atendimento de {paciente} volta a ser pelo CONVÊNIO da ficha: as guias são geradas de "
              + "novo para a data da sessão. O que já foi recebido no caixa não é desfeito — confira.";

    public static string TrocaFeita(bool paraParticular)
        => paraParticular
            ? "Atendimento marcado como particular."
            : "Atendimento devolvido ao convênio.";
}
