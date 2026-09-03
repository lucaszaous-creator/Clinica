using Clinica.Domain.Regras;

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
    /// Chave de IDEMPOTÊNCIA da importação do sistema anterior (set/2026 — a clínica
    /// migrou do Smart Clinic). Forma: <c>IMPORT:{sistema}:{id de lá}</c>; nula para
    /// toda ficha cadastrada aqui.
    ///
    /// É o que faz a importação poder RODAR DE NOVO: o arquivo exportado é reprocessado
    /// (a segunda exportação, a linha corrigida, a queda de conexão no meio) e a ficha
    /// que já entrou por esta chave é PULADA em vez de duplicada. O CPF não serve de
    /// chave: metade da base do sistema antigo pode não tê-lo, e duas fichas da mesma
    /// pessoa partem o histórico em dois (parcela 57).
    ///
    /// Quem lê é o próprio importador (<c>ImportacaoPacientesService</c>) — a chave é o
    /// leitor, e a trilha de auditoria a carrega no detalhe de cada ficha criada.
    /// </summary>
    public string? ChaveImportacao { get; set; }

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

    /// <summary>
    /// Nome do convênio como a clínica o chama — é este que vai para a tela, nunca
    /// <see cref="Convenio"/>, que é a FAMÍLIA de regra.
    ///
    /// Amarrar `{Binding Convenio}` num TextBlock faz o WPF chamar `ToString()` e escrever
    /// "UnimedIntercambio" no crachá do paciente; e resolver só pela família escreve
    /// "Personalizado" para toda operadora que a clínica cadastrou. As duas coisas foram
    /// vistas na lista de pacientes em produção.
    /// </summary>
    public string ConvenioNome => CatalogoConvenios.Nome(ConvenioCodigo, Convenio);

    /// <summary>A ficha veio do sistema anterior sem convênio e ninguém escolheu ainda
    /// (<see cref="ConvenioCadastro.CodigoADefinir"/>). Quem acusa é a elegibilidade.</summary>
    public bool ConvenioADefinir =>
        string.Equals(ConvenioCodigo, ConvenioCadastro.CodigoADefinir, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// O par de <see cref="ConvenioADefinir"/>. São duas propriedades porque nem a suíte
    /// nem o faturamento têm conversor de booleano invertido, e é o mesmo par que as telas
    /// já usam (<c>SemPaciente</c>/<c>PacienteEscolhido</c>) — criar um conversor para um
    /// uso só seria mais peça para manter.
    /// </summary>
    public bool TemConvenioEscolhido => !ConvenioADefinir;

    /// <summary>Carteirinha com validade já passada — guia recusada na hora pelo convênio.</summary>
    public bool CarteirinhaVencida =>
        ValidadeCarteirinha is { } validade && validade < DateOnly.FromDateTime(DateTime.Today);

    /// <summary>
    /// O documento COMO SE LÊ. Toda lista de escolha amarrava <c>Documento</c> direto e
    /// mostrava "10400975700" — onze dígitos colados, que ninguém confere de relance e
    /// ninguém compara com o cartão na mão do paciente.
    ///
    /// ⚠️ NÃO é <c>Cpf.Formatar</c> direto: fora dos 11 dígitos ele devolve SÓ OS DÍGITOS,
    /// e o campo se chama <see cref="Documento"/>, não CPF — uma ficha com RG "12.345.678-9"
    /// voltaria "123456789", com a pontuação de um documento de verdade apagada na
    /// exibição. Formata quando é CPF; no resto, mostra o que está gravado.
    /// </summary>
    public string DocumentoFormatado
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Documento)) return string.Empty;
            var digitos = Cpf.Normalizar(Documento);
            return digitos.Length == 11 ? Cpf.Formatar(digitos) : Documento.Trim();
        }
    }

    /// <summary>
    /// O telefone como se lê. <c>Telefone.Formatar</c> é seguro para qualquer entrada — o
    /// que não reconhece volta como está —, então vale também para a ficha que já foi
    /// gravada com máscara.
    ///
    /// ⚠️ O nome do TIPO vem qualificado porque a PROPRIEDADE <see cref="Telefone"/> o
    /// esconde dentro desta classe: `Telefone.Formatar(...)` aqui resolveria para a string,
    /// não para o utilitário.
    /// </summary>
    public string TelefoneFormatado => Clinica.Domain.Telefone.Formatar(Telefone);

    public List<Atendimento> Atendimentos { get; set; } = new();

    public List<Consulta> Consultas { get; set; } = new();
}
