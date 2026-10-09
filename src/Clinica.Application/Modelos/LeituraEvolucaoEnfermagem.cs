using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>Leitura integral, usada tanto no histórico quanto na sessão vinculada.</summary>
public sealed record LeituraEvolucaoEnfermagem(string Paciente, string Texto)
{
    public string Titulo => "Evolução de enfermagem";

    public static string Conteudo(EvolucaoEnfermagem e, bool substituida)
    {
        var partes = new List<string>
        {
            $"Atendimento: {e.Data:dd/MM/yyyy} às {e.Hora:HH\\:mm}",
            $"Profissional: {e.AutorNome} · {e.AutorConselho}",
            $"Registrado em: {e.RegistradoEm:dd/MM/yyyy HH:mm}",
            e.Agendamento is { } sessao ? $"Sessão vinculada: {sessao.DataHora:dd/MM/yyyy HH:mm}" : "Sem sessão vinculada"
        };
        void Somar(string rotulo, string? texto) { if (!string.IsNullOrWhiteSpace(texto)) partes.Add($"{rotulo}: {texto}"); }
        if (e.Cancelada) Somar("CANCELADA", e.MotivoCancelamento);
        else if (substituida) partes.Add("CORRIGIDA — vale o registro seguinte");
        if (e.EhRetificacao) Somar("Motivo da retificação", e.MotivoRetificacao);
        Somar("Fase", e.FaseAtendimento switch { "Chegada" => "Chegada", "AposAplicacao" => "Após aplicação", _ => e.FaseAtendimento });
        Somar("Prescrição", e.Prescricao?.Numero);
        Somar("Evolução", e.Texto);
        Somar("Sinais vitais", e.SinaisVitaisResumidos);
        Somar("Acesso venoso", e.AcessoResumo);
        Somar("Histórico", e.Historico);
        Somar("Exame físico", e.ExameFisico);
        Somar("Diagnósticos", string.Join("\n", e.Diagnosticos.OrderBy(d => d.Ordem).Select(d => d.Redacao)));
        Somar("Cuidados", string.Join("\n", e.Cuidados.OrderBy(c => c.Ordem).Select(c => c.Redacao)));
        Somar("Avaliação", e.Avaliacao);
        if (e.Intercorrencia) partes.Add("INTERCORRÊNCIA registrada");
        return string.Join("\n\n", partes);
    }
}
