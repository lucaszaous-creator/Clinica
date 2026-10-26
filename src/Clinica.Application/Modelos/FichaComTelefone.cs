namespace Clinica.Application.Modelos;

/// <summary>
/// Uma ficha que tem o telefone da conversa (out/2026, WhatsApp da Recepção): só o que a
/// recepção usa para CONFERIR quem é a pessoa — nome, nascimento, o fim do documento — e o
/// telefone como está no cadastro. Projeção: nada da foto nem das observações.
/// </summary>
public sealed record FichaComTelefone(int Id, string Nome, DateOnly? DataNascimento, string? Documento, string? Telefone);

/// <summary>De quem é um horário: o par que liga o aviso do sino (só o agendamento) à ficha da conversa.</summary>
public sealed record AgendamentoDoPaciente(int AgendamentoId, int PacienteId);

/// <summary>A miniatura do retrato de uma ficha (25/10, o avatar das conversas do WhatsApp): só o id e os bytes.</summary>
public sealed record MiniaturaDoPaciente(int PacienteId, byte[] Miniatura);
