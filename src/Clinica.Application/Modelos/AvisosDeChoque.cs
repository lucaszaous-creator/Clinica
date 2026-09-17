namespace Clinica.Application.Modelos;

/// <summary>
/// Um aviso do horário escolhido, como as telas de marcação o escrevem: o texto e se ele
/// é GRAVE, e se a trava opcional do profissional impede marcar.
/// </summary>
public sealed record AvisoDeChoque(string Texto, bool Grave, bool ImpedeMarcar = false);

/// <summary>
/// O VOCABULÁRIO dos avisos do horário — o que as duas telas de marcação escrevem quando
/// o horário escolhido tem alguém dentro, ou a agenda está fechada.
///
/// Mora na Application, e não em cada ViewModel, pela razão de sempre: o Novo atendimento
/// e o formulário da agenda tinham CADA UM a sua função <c>Descrever</c>, com a mesma
/// frase copiada — e duas redações divergem na primeira correção. E o que a tela AFIRMA
/// precisa morar onde o <c>dotnet test</c> alcança.
///
/// A trava é opcional por profissional. O cabeçalho diferencia avisos de impedimentos;
/// encaixe não ignora uma trava ativa.
/// </summary>
public static class AvisosDeChoque
{
    /// <summary>
    /// A frase acima da lista. Ela DIZ que nada impede: sem isso, uma tarja vermelha com
    /// o botão "Marcar" habilitado ao lado é uma tela que se contradiz — e a recepcionista
    /// que já levou a recusa antiga fica procurando o que fazer para o aviso sumir.
    /// </summary>
    public static string CabecalhoPara(IEnumerable<AvisoDeChoque> avisos) => avisos.Any(a => a.ImpedeMarcar)
        ? "Agenda protegida — escolha outro horário para resolver os impedimentos:" : Cabecalho;

    public const string Cabecalho = "Avisos deste horário — nenhum impede marcar:";

    /// <summary>
    /// Agenda FECHADA (feriado, férias, folga, sala em manutenção) e hora fora da jornada
    /// declarada de quem atende. É o aviso que não tem outro paciente do outro lado: ele
    /// não fala de disputa, fala de a clínica não estar aberta.
    /// </summary>
    public static bool EhGrave(RecursoAgenda recurso)
        => recurso is RecursoAgenda.Bloqueio or RecursoAgenda.Expediente;

    /// <summary>
    /// Os avisos na ordem em que se leem: o GRAVE primeiro. A recepcionista lê a primeira
    /// linha e decide; deixar "o profissional já atende fulano" acima de "a clínica está
    /// fechada" enterraria o único que costuma mudar a decisão.
    ///
    /// Duplicata sai (dois horários no mesmo intervalo produzem a mesma frase quando o
    /// que muda é o id), preservando a ordem — <c>Distinct</c> do LINQ é estável.
    /// </summary>
    public static IReadOnlyList<AvisoDeChoque> Montar(IEnumerable<ConflitoAgenda> conflitos)
        => conflitos
            .Select(c => new AvisoDeChoque(c.Descricao, c.ImpedeMarcar || EhGrave(c.Recurso), c.ImpedeMarcar))
            .Distinct()
            .OrderByDescending(a => a.Grave)
            .ToList();
}
