using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// QUEM VÊ E ATENDE A AGENDA DE QUEM NO CONSULTÓRIO (24/11/2026, decisão do dono): o perfil <b>Gerente</b> vê e atende a
/// agenda de QUALQUER profissional pelo Consultório (o portal do profissional e as telas Blazor) — a diretora que
/// também atende abre a sessão do Dr. X e escreve a evolução. Os outros perfis continuam só com a própria agenda.
/// <list type="bullet">
/// <item><b>O que se registra fica no nome de quem fez login</b>, nunca no do profissional da agenda: a evolução, a
/// prescrição e o documento levam o <c>ProfissionalId</c> e o operador do LOGIN (como sempre foi), e a tela diz
/// "Agenda de Dr. X · registrado por Fulana" (<see cref="Rotulo"/>).</item>
/// <item>Continua valendo tudo o que já valia: os bits <c>VerProntuario</c>/<c>EditarProntuario</c> (quem atende é
/// <c>PoliticaAtendimentoTablet.PodeAtender</c>), o registro de acesso ao prontuário e a assinatura (SafeID/A1),
/// que segue a do usuário logado. A evolução que OUTRO profissional já escreveu continua dele: o Gerente não a
/// sobrescreve.</item>
/// </list>
/// A régua mora AQUI, num lugar só, com teste (<c>AgendaDoConsultorioTests</c>); o portal e as telas só a chamam.
/// </summary>
public static class AgendaDoConsultorio
{
    /// <summary>O que a tela manda no seletor: o próprio ("eu", o padrão), a clínica inteira ou o id de um profissional.</summary>
    public const string Eu = "eu";
    public const string Todos = "todos";

    /// <summary>Este perfil vê e atende a agenda de qualquer profissional.</summary>
    public static bool VeQualquerAgenda(PerfilAcesso perfil) => perfil == PerfilAcesso.Gerente;

    /// <summary>
    /// O profissional cuja agenda a lista mostra (<c>null</c> = todos). Quem não é Gerente fica SEMPRE com a própria —
    /// a escolha que viesse na requisição é ignorada (a trava é do servidor, não do seletor). O Gerente escolhe:
    /// "eu" (ou nada) é a própria agenda quando há vínculo e a clínica inteira quando não há; "todos" é a clínica; um
    /// número é aquele profissional.
    /// </summary>
    public static int? Filtro(PerfilAcesso perfil, int? proprio, string? escolha)
    {
        if (!VeQualquerAgenda(perfil)) return proprio;
        var e = escolha?.Trim();
        if (string.Equals(e, Todos, StringComparison.OrdinalIgnoreCase)) return null;
        if (int.TryParse(e, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) && id > 0) return id;
        return proprio is > 0 ? proprio : null;
    }

    /// <summary>Pode abrir e registrar o atendimento de um horário deste profissional: o próprio, ou qualquer um para o Gerente.</summary>
    public static bool PodeAtenderHorarioDe(PerfilAcesso perfil, int? proprio, int? profissionalDoHorario)
        => VeQualquerAgenda(perfil) || proprio is > 0 && profissionalDoHorario == proprio;

    /// <summary>
    /// "Agenda de Dr. X · registrado por Fulana" quando a agenda é de outro; nulo quando é a própria (nada a avisar).
    /// </summary>
    public static string? Rotulo(int? proprio, int? profissionalDoHorario, string? nomeDaAgenda, string registradoPor)
        => profissionalDoHorario is null || profissionalDoHorario == proprio
            ? null
            : $"Agenda de {(string.IsNullOrWhiteSpace(nomeDaAgenda) ? "outro profissional" : nomeDaAgenda.Trim())} · registrado por {registradoPor}";
}
