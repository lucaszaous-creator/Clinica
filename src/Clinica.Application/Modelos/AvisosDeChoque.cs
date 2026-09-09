namespace Clinica.Application.Modelos;

/// <summary>
/// Um aviso do horário escolhido, como as telas de marcação o escrevem: o texto e se ele
/// é GRAVE. Nenhum impede marcar — ver <see cref="AvisosDeChoque"/>.
/// </summary>
public sealed record AvisoDeChoque(string Texto, bool Grave);

/// <summary>
/// O VOCABULÁRIO dos avisos do horário — o que as duas telas de marcação escrevem quando
/// o horário escolhido tem alguém dentro, ou a agenda está fechada.
///
/// Mora na Application, e não em cada ViewModel, pela razão de sempre: o Novo atendimento
/// e o formulário da agenda tinham CADA UM a sua função <c>Descrever</c>, com a mesma
/// frase copiada — e duas redações divergem na primeira correção. E o que a tela AFIRMA
/// precisa morar onde o <c>dotnet test</c> alcança.
///
/// ⛔ <b>Nenhum destes avisos impede marcar</b> (set/2026 — ver
/// <c>AgendaService.ConflitosAsync</c>). Foi por isso que o sufixo "(aviso — não impede
/// marcar)", que só o choque do próprio paciente carregava, saiu de todas as linhas e
/// virou UMA frase acima da lista (<see cref="Cabecalho"/>): repetido em quatro linhas
/// ele é o ruído que faz ninguém ler a quarta.
///
/// ⚠️ <b>A gravidade continua existindo, e é a metade que sustenta a decisão de não
/// impedir.</b> "O profissional já atende alguém às 14h" é a rotina da casa — na
/// acupuntura o paciente fica na maca com as agulhas enquanto outro é atendido. "A clínica
/// está fechada neste dia" é outra coisa: não há ninguém para atender, e quem marcar ali
/// marcou para um dia em que a porta não abre. Pintar as duas da mesma cor treinaria a
/// recepção a ignorar as duas — que é exatamente o que a recusa fazia pelo avesso.
/// </summary>
public static class AvisosDeChoque
{
    /// <summary>
    /// A frase acima da lista. Ela DIZ que nada impede: sem isso, uma tarja vermelha com
    /// o botão "Marcar" habilitado ao lado é uma tela que se contradiz — e a recepcionista
    /// que já levou a recusa antiga fica procurando o que fazer para o aviso sumir.
    /// </summary>
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
            .Select(c => new AvisoDeChoque(c.Descricao, EhGrave(c.Recurso)))
            .Distinct()
            .OrderByDescending(a => a.Grave)
            .ToList();
}
