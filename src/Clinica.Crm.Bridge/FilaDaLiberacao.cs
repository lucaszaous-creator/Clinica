using Clinica.Infrastructure;

/// <summary>
/// A confirmação ao paciente da consulta liberada pela gerência (trava de consulta, nov/2026), na
/// fila de mensagens ao paciente que o CRM já lê. O contrato (<c>PatientMessage</c>) não muda: o
/// Id é texto, e o prefixo diz de quem é a mensagem na volta do ack.
/// </summary>
public static class FilaDaLiberacao
{
    public const string Prefixo = "liberacao:";

    /// <summary>A fila nova não pode derrubar a antiga (o papel da ponte ainda sem o GRANT das tabelas novas).</summary>
    public static async Task<IReadOnlyList<AvisoDaLiberacao>> DoPacienteAsync(LiberacaoDeTravaService s, CancellationToken ct)
    {
        try { return await s.AvisosParaPacienteAsync(ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Clinica.Application.Diagnostico.Registrar("Ponte — fila da liberação de trava (paciente) não pôde ser lida", ex);
            return [];
        }
    }

    public static int? Ler(string? id)
        => id is not null && id.StartsWith(Prefixo, StringComparison.Ordinal)
           && int.TryParse(id.AsSpan(Prefixo.Length), System.Globalization.NumberStyles.None,
               System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : null;
}
