namespace Clinica.Domain.Entities;

/// <summary>
/// Retrato do paciente em tamanho cheio, numa tabela à parte de propósito: assim a
/// listagem de pacientes (e qualquer consulta que carregue a entidade) não arrasta os
/// bytes da imagem. Para os avatares existe a miniatura em <c>Paciente.FotoMiniatura</c>.
/// </summary>
public class PacienteFoto
{
    /// <summary>Chave primária e estrangeira: cada paciente tem no máximo uma foto.</summary>
    public int PacienteId { get; set; }

    /// <summary>JPEG quadrado do retrato (lado ~640px), como capturado pela webcam da recepção.</summary>
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();

    /// <summary>Data/hora da última captura (hora de parede, sem fuso).</summary>
    public DateTime AtualizadaEm { get; set; }

    public Paciente? Paciente { get; set; }
}
