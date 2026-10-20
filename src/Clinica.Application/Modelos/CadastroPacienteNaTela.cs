using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que a pessoa preencheu no cadastro do paciente (out/2026 — era o estado do
/// <c>CadastroPacienteViewModel</c> WPF). Mutável de propósito: o formulário Blazor amarra os
/// campos direto aqui, e a regra de <see cref="CadastroPacienteNaTela"/> confere e copia.
/// </summary>
public sealed class DadosDoCadastro
{
    public string Nome { get; set; } = string.Empty;
    public string? Documento { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public string? Endereco { get; set; }
    public DateOnly? DataNascimento { get; set; }
    public Sexo Sexo { get; set; } = Sexo.Feminino;

    /// <summary>
    /// Nasce NULO no cadastro novo: "convênio ou particular?" é respondido por quem está com o
    /// paciente na frente, e o Salvar recusa sem resposta.
    /// </summary>
    public OpcaoDeConvenio? Convenio { get; set; }

    public string? Carteirinha { get; set; }
    public DateOnly? ValidadeCarteirinha { get; set; }
    public EntradaModalidade? ModalidadePreferida { get; set; }
    public bool PossuiApp { get; set; }
    public string? Observacoes { get; set; }

    /// <summary>Nulo = ninguém perguntou. Origem chutada é pior que origem em branco.</summary>
    public OrigemPaciente? Origem { get; set; }

    public string? IndicadoPor { get; set; }

    public Categoria Categoria { get; set; }

    /// <summary>A categoria foi escolhida à mão (e então o convênio deixa de sugeri-la).</summary>
    public bool CategoriaManual { get; set; }

    /// <summary>O campo "quem indicou" só vale para a origem Indicação.</summary>
    public bool EhIndicacao => Origem == OrigemPaciente.Indicacao;
}

/// <summary>O resultado da conferência do Salvar: a frase geral e o erro de cada campo sinalizado.</summary>
public sealed record ConferenciaDoCadastro(string? Mensagem, string? ErroDocumento = null, string? ErroEndereco = null)
{
    public bool Valido => Mensagem is null;
}

/// <summary>
/// O cadastro do paciente (out/2026 — migrado do <c>CadastroPacienteWindow</c> +
/// <c>CadastroPacienteViewModel</c> do shell WPF, que serviam à ficha, ao Lançar/Marcar e à
/// tela Pacientes, e saíram junto com a ficha WPF). Tudo o que o formulário DECIDE mora aqui, com teste: a ordem das recusas
/// do Salvar, a categoria sugerida pelo convênio, o convênio e a modalidade que a ficha já tem
/// e a cópia campo a campo para a entidade. O Razor só desenha e chama o <c>PacienteService</c>.
/// </summary>
public static class CadastroPacienteNaTela
{
    /// <summary>O convite quando nada foi escolhido — é a linha que diz que existe o particular.</summary>
    public const string ConviteDoConvenio = "Escolha o convênio do paciente — ou \"Particular\", se ele paga do bolso.";

    public const string FotoCapturada = "Foto capturada — ela será gravada ao salvar o cadastro.";
    public const string FotoSeraApagada = "A foto será apagada ao salvar o cadastro.";
    public const string CameraSemImagem = "A câmera não devolveu a imagem. Tente capturar de novo.";
    public const string AguardeCarregamento = "Aguarde o carregamento do cadastro. Se houve falha, feche e abra novamente.";
    public const string SemPermissao = "Seu acesso não permite editar o cadastro de pacientes.";

    public static IReadOnlyList<Sexo> Sexos { get; } = [Sexo.Feminino, Sexo.Masculino];

    /// <summary>Origens oferecidas, com o vazio na frente ("não perguntado").</summary>
    public static IReadOnlyList<OrigemPaciente?> Origens { get; } =
        [null, .. Enum.GetValues<OrigemPaciente>().Cast<OrigemPaciente?>()];

    public static IReadOnlyList<Categoria> Categorias { get; } = Enum.GetValues<Categoria>();

    public static string Titulo(int? pacienteId) => pacienteId is null ? "Novo paciente" : "Editar paciente";

    /// <summary>O que a escolha do convênio significa, ao lado do campo — ou o convite.</summary>
    public static string ExplicacaoDoConvenio(OpcaoDeConvenio? convenio) => convenio?.Explicacao ?? ConviteDoConvenio;

    /// <summary>
    /// As opções do cadastro: o "a definir" fica na LISTA (a ficha importada continua mostrando
    /// o convênio que tem), mas nada vem pré-selecionado.
    /// </summary>
    public static IReadOnlyList<OpcaoDeConvenio> Convenios(IEnumerable<EntradaConvenio> catalogo)
        => OpcoesDeConvenio.Montar(catalogo, incluirADefinir: true);

    /// <summary>A modalidade habitual de quem é cadastrado agora: acupuntura com eletro, ou a primeira.</summary>
    public static EntradaModalidade? ModalidadePadrao(IReadOnlyList<EntradaModalidade> modalidades)
        => modalidades.FirstOrDefault(m => m.Base == ModalidadeAtendimento.AcupunturaComEletro)
           ?? modalidades.FirstOrDefault();

    /// <summary>
    /// O cadastro novo: nada de convênio, a modalidade padrão e a categoria que o convênio
    /// sugerir quando ele for escolhido.
    /// </summary>
    public static DadosDoCadastro Novo(IReadOnlyList<EntradaModalidade> modalidades)
        => new() { ModalidadePreferida = ModalidadePadrao(modalidades) };

    /// <summary>
    /// O que a ficha já tem, nos campos do formulário. O CÓDIGO do convênio vence; a família é
    /// o caminho de baixo, para a ficha antiga cujo código é o próprio nome do enum. Sem achar
    /// nenhum dos dois o convênio fica vazio e o Salvar cobra a escolha — melhor do que marcar
    /// uma operadora qualquer na ficha de quem talvez seja particular.
    /// </summary>
    public static DadosDoCadastro Da(Paciente p, IReadOnlyList<OpcaoDeConvenio> convenios,
        IReadOnlyList<EntradaModalidade> modalidades)
    {
        ArgumentNullException.ThrowIfNull(p);
        return new DadosDoCadastro
        {
            Nome = p.Nome,
            Documento = p.Documento,
            Telefone = p.Telefone,
            Email = p.Email,
            Endereco = p.Endereco,
            DataNascimento = p.DataNascimento,
            Sexo = p.Sexo,
            Convenio = convenios.FirstOrDefault(c => c.Codigo == p.ConvenioCodigo)
                       ?? convenios.FirstOrDefault(c => c.Familia == p.Convenio),
            Carteirinha = p.Carteirinha,
            ValidadeCarteirinha = p.ValidadeCarteirinha,
            ModalidadePreferida = modalidades.FirstOrDefault(m => m.Codigo == p.ModalidadePreferidaCodigo)
                                  ?? modalidades.FirstOrDefault(m => m.Base == p.ModalidadePreferida)
                                  ?? ModalidadePadrao(modalidades),
            PossuiApp = p.PossuiApp,
            Observacoes = p.Observacoes,
            Origem = p.Origem,
            IndicadoPor = p.IndicadoPor,
            Categoria = p.Categoria,
            CategoriaManual = p.Categoria != CategoriaConvenio.Base(p.Convenio, p.PossuiApp)
        };
    }

    /// <summary>
    /// O convênio ou o aplicativo mudou: a categoria acompanha a sugestão — a não ser que
    /// alguém a tenha escolhido à mão, e aí ela fica.
    /// </summary>
    public static void SugerirCategoria(DadosDoCadastro d)
    {
        if (d.CategoriaManual || d.Convenio is null) return;
        d.Categoria = CategoriaConvenio.Base(d.Convenio.Familia, d.PossuiApp);
    }

    /// <summary>
    /// As recusas do Salvar, na MESMA ordem do formulário WPF: nome, convênio, CPF e endereço
    /// juntos (os dois campos sinalizados de uma vez), e por último o e-mail.
    /// </summary>
    public static ConferenciaDoCadastro Conferir(DadosDoCadastro d)
    {
        ArgumentNullException.ThrowIfNull(d);
        if (string.IsNullOrWhiteSpace(d.Nome))
            return new("Informe o nome do paciente.");

        // A frase diz as duas saídas, porque "escolha o convênio" não responde a quem não tem
        // convênio nenhum.
        if (d.Convenio is null)
            return new("Escolha o convênio — ou \"Particular\", se o paciente paga do bolso.");

        var erroCpf = ValidacaoCadastroPaciente.ErroCpf(d.Documento);
        var erroEndereco = ValidacaoCadastroPaciente.ErroEndereco(d.Endereco);
        if (erroCpf is not null || erroEndereco is not null)
            return new("Confira os campos sinalizados. Os dados preenchidos foram mantidos.", erroCpf, erroEndereco);

        // E-mail errado gravado é lembrete que nunca chega — e ninguém descobre, porque o envio
        // conta o endereço inválido como "sem e-mail" e segue.
        if (!string.IsNullOrWhiteSpace(d.Email) && !EnderecoDeEmail.Valido(d.Email))
            return new("E-mail inválido. Confira o endereço (ou deixe em branco).");

        return new(null);
    }

    /// <summary>
    /// Copia o formulário para a ficha, campo a campo. A categoria só é escrita quando foi
    /// escolhida à mão — senão quem decide é o serviço, pela regra do convênio.
    /// </summary>
    public static void Aplicar(DadosDoCadastro d, Paciente paciente)
    {
        ArgumentNullException.ThrowIfNull(d);
        ArgumentNullException.ThrowIfNull(paciente);
        if (d.Convenio is null) throw new InvalidOperationException("Escolha o convênio do paciente.");

        paciente.Nome = d.Nome.Trim();
        paciente.Documento = Limpar(d.Documento);
        paciente.Telefone = Limpar(d.Telefone);
        paciente.Email = EnderecoDeEmail.Normalizar(d.Email);
        paciente.Endereco = Limpar(d.Endereco);
        paciente.DataNascimento = d.DataNascimento;
        paciente.Sexo = d.Sexo;
        paciente.Convenio = d.Convenio.Familia;
        paciente.ConvenioCodigo = d.Convenio.Codigo;
        paciente.Carteirinha = Limpar(d.Carteirinha);
        paciente.ValidadeCarteirinha = d.ValidadeCarteirinha;
        paciente.ModalidadePreferida = d.ModalidadePreferida?.Base ?? paciente.ModalidadePreferida;
        paciente.ModalidadePreferidaCodigo = d.ModalidadePreferida?.Codigo;
        paciente.PossuiApp = d.PossuiApp;
        paciente.Observacoes = Limpar(d.Observacoes);
        paciente.Origem = d.Origem;
        // Quem indicou só faz sentido com a origem "Indicação": o nome preso a outra origem
        // seria um dado órfão que ninguém sabe ler depois.
        paciente.IndicadoPor = d.Origem == OrigemPaciente.Indicacao ? Limpar(d.IndicadoPor) : null;
        if (d.CategoriaManual) paciente.Categoria = d.Categoria;
    }

    public static string? Limpar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

/// <summary>Uma linha da escolha do convênio — o cadastro do catálogo, o nome e o que ele significa.</summary>
public sealed record LinhaDeConvenio(ConvenioCadastro Entrada, string Nome, string Detalhe, bool GeraGuia);

/// <summary>
/// "Qual é o convênio deste paciente?" — a pergunta feita NO MEIO do lançamento (parcela 92;
/// out/2026 migrada da <c>EscolhaDeConvenioWindow</c> do shell, que saiu). O lançamento sem convênio é
/// RECUSADO pelo serviço; isto é a metade VISÍVEL, que pergunta antes, com o paciente na frente.
/// O convênio é obrigatório; carteirinha e validade são opcionais e, em branco, preservam o que
/// a ficha já tinha.
/// </summary>
public static class EscolhaDeConvenioNaTela
{
    public const string SemPermissao =
        "Seu acesso não permite editar a ficha do paciente. Peça a quem tem essa permissão para "
        + "escolher o convênio — ou peça o bit em Acessos.";

    /// <summary>
    /// A lista nunca nasce vazia (o catálogo garante os embutidos e o Particular); vazia, alguém
    /// desativou todos — e a frase aponta o app CERTO, o único que tem o cadastro de convênios.
    /// </summary>
    public const string CatalogoVazio =
        "Nenhum convênio ativo no catálogo. Reative um em Configurações → Convênios, no aplicativo de Faturamento.";

    public const string EscolhaObrigatoria = "Escolha o convênio do paciente.";

    /// <summary>
    /// As linhas na ordem e com a frase de <see cref="OpcoesDeConvenio"/>, SEM o "a definir": ele
    /// é a pergunta, e oferecê-lo como resposta devolveria um sucesso e um lançamento recusado.
    /// </summary>
    public static IReadOnlyList<LinhaDeConvenio> Linhas(IReadOnlyList<ConvenioCadastro> catalogo)
    {
        var porCodigo = catalogo
            .GroupBy(c => c.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        return OpcoesDeConvenio.Montar(catalogo, incluirADefinir: false)
            .Select(o => new LinhaDeConvenio(porCodigo[o.Codigo], o.Nome, o.Explicacao, o.GeraGuia))
            .ToList();
    }

    /// <summary>O botão acende só com uma escolha, fora da carga e da gravação, e com o bit.</summary>
    public static bool PodeVincular(LinhaDeConvenio? selecionado, bool carregando, bool gravando, bool podeEditarFicha)
        => selecionado is not null && !carregando && !gravando && podeEditarFicha;
}
