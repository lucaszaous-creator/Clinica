namespace Clinica.Desktop.Shell.WebClinica;

public sealed record LinhaAgendaWeb(string Id, string Data, string Hora, string Paciente,
    string Modalidade, string Profissional, string Sala, string Situacao, string Grupo,
    string Detalhe, string Registro, string Observacoes, string Acao, bool Habilitada);
