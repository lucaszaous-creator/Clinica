namespace Clinica.Domain.Entities;

/// <summary>Ficha do paciente. Convênio, modalidade e app determinam as regras de faturamento aplicadas.</summary>
public class Paciente
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Documento { get; set; }
    public string? Telefone { get; set; }

    public Convenio Convenio { get; set; }

    /// <summary>Unimed Padrão: indica se o paciente possui o app e consegue gerar QR Code (2º código).</summary>
    public bool PossuiApp { get; set; }

    /// <summary>Usado pela Petrobras para a rotação de especialidades (Ginecologia só para mulheres).</summary>
    public Sexo Sexo { get; set; }

    /// <summary>Categoria mais recente registrada na ficha (derivada do convênio + app; editável).</summary>
    public Categoria Categoria { get; set; }

    /// <summary>Modalidade de atendimento habitual do paciente. Pré-preenche Novo Atendimento e Agenda.</summary>
    public ModalidadeAtendimento ModalidadePreferida { get; set; } = ModalidadeAtendimento.AcupunturaComEletro;

    public string? Observacoes { get; set; }

    public List<Atendimento> Atendimentos { get; set; } = new();

    public List<Consulta> Consultas { get; set; } = new();
}
