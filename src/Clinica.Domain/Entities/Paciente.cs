namespace Clinica.Domain.Entities;

/// <summary>Ficha do paciente. Convênio, modalidade e app determinam as regras de faturamento aplicadas.</summary>
public class Paciente
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Documento { get; set; }
    public string? Telefone { get; set; }

    /// <summary>
    /// Endereço residencial do paciente.
    ///
    /// Não é enfeite de cadastro: o art. 35 da Lei 5.991/1973 exige o nome E o endereço
    /// residencial do paciente para a receita ser AVIADA — sem ele o farmacêutico pode
    /// recusar a dispensação, e o papel volta com o paciente. O sistema imprimia receita
    /// desde a parcela 3 sem ter onde guardar o dado, então a exigência era descoberta na
    /// farmácia. Opcional no cadastro porque quem não recebe receita não precisa dele;
    /// quem emite receita é avisado na hora (<see cref="ConformidadeDocumentoClinico"/>).
    /// </summary>
    public string? Endereco { get; set; }

    public DateOnly? DataNascimento { get; set; }

    /// <summary>Número da carteirinha do convênio (vai na guia).</summary>
    public string? Carteirinha { get; set; }

    /// <summary>Validade da carteirinha — vencida = guia recusada na hora.</summary>
    public DateOnly? ValidadeCarteirinha { get; set; }

    /// <summary>Família de regra do convênio (define como faturar). Mantida para o motor de regras.</summary>
    public Convenio Convenio { get; set; }

    /// <summary>Código do convênio no catálogo (identifica a variante/nome). Null = convênio embutido = Convenio.ToString().</summary>
    public string? ConvenioCodigo { get; set; }

    /// <summary>Unimed Padrão: indica se o paciente possui o app e consegue gerar QR Code (2º código).</summary>
    public bool PossuiApp { get; set; }

    /// <summary>Usado pela Petrobras para a rotação de especialidades (Ginecologia só para mulheres).</summary>
    public Sexo Sexo { get; set; }

    /// <summary>Categoria mais recente registrada na ficha (derivada do convênio + app; editável).</summary>
    public Categoria Categoria { get; set; }

    /// <summary>Modalidade de atendimento habitual do paciente. Pré-preenche Novo Atendimento e Agenda.</summary>
    public ModalidadeAtendimento ModalidadePreferida { get; set; } = ModalidadeAtendimento.AcupunturaComEletro;

    /// <summary>Código da modalidade preferida no catálogo (identifica a variante/nome). Null = embutida.</summary>
    public string? ModalidadePreferidaCodigo { get; set; }

    public string? Observacoes { get; set; }

    /// <summary>
    /// Como o paciente chegou à clínica (CRM). Null = não perguntado — que é diferente de
    /// "Outro": a clínica só sabe o que perguntou, e um padrão preenchido sozinho
    /// inventaria a origem da carteira inteira já cadastrada.
    /// </summary>
    public OrigemPaciente? Origem { get; set; }

    /// <summary>
    /// Quem indicou, quando a origem foi indicação. Texto livre de propósito: quem
    /// indica pode ser outro paciente, um médico de fora ou uma academia do bairro, e
    /// obrigar a escolher numa lista faria a recepção deixar em branco.
    /// </summary>
    public string? IndicadoPor { get; set; }

    /// <summary>
    /// Miniatura JPEG quadrada (~160px) do retrato. Fica na própria linha do paciente
    /// porque é pequena e alimenta os avatares da lista; a foto cheia mora em
    /// <see cref="Foto"/> (tabela à parte, carregada sob demanda).
    /// </summary>
    public byte[]? FotoMiniatura { get; set; }

    /// <summary>Quando o retrato foi capturado pela última vez. Null = paciente sem foto.</summary>
    public DateTime? FotoAtualizadaEm { get; set; }

    /// <summary>Retrato em tamanho cheio. Só é carregado quando explicitamente pedido.</summary>
    public PacienteFoto? Foto { get; set; }

    /// <summary>Atalho de leitura para a UI: o paciente já tem retrato cadastrado?</summary>
    public bool TemFoto => FotoAtualizadaEm is not null;

    /// <summary>Carteirinha com validade já passada — guia recusada na hora pelo convênio.</summary>
    public bool CarteirinhaVencida =>
        ValidadeCarteirinha is { } validade && validade < DateOnly.FromDateTime(DateTime.Today);

    public List<Atendimento> Atendimentos { get; set; } = new();

    public List<Consulta> Consultas { get; set; } = new();
}
