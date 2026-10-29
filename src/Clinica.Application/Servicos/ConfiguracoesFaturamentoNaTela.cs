using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que a tela de "Regras e catálogos TISS" (Configurações do faturamento) decide, fora do
/// Razor (out/2026, migração para Blazor). Era tudo do <c>ParametrosViewModel</c> e das
/// <c>*Edicao</c> do WPF — onde o <c>dotnet test</c> não alcança. Aqui mora: os rótulos da
/// tela, o nome único do item novo, a recusa de nomes repetidos, a cópia campo a campo do
/// convênio (lugar 3: o que não estiver na cópia é apagado na primeira edição), a leitura
/// dos prazos e — o ponto que a verificação do Windows prova — o Salvar que RELÊ o
/// prestador e só troca o que a tela edita.
/// </summary>
public static class ConfiguracoesFaturamentoNaTela
{
    // ===================== Rótulos (o EnumDescricaoConverter do faturamento) =====================

    /// <summary>
    /// A FAMÍLIA de regra pelo nome do catálogo — a mesma resposta do WPF. Esta tela é das
    /// poucas em que a família é a resposta certa (a tabela "Números das regras" é por família).
    /// </summary>
    public static string Familia(Convenio familia) => ConvenioInfo.NomeExibicao(familia);

    /// <summary>As famílias oferecidas no seletor do convênio (embutidas + Personalizado).</summary>
    public static IReadOnlyList<Convenio> Familias { get; } = Enum.GetValues<Convenio>();

    /// <summary>As formas de obter o 2º código oferecidas na regra personalizada (sem "não aplica").</summary>
    public static IReadOnlyList<FormaObtencao> FormasObtencao { get; } =
        [FormaObtencao.Sistema, FormaObtencao.App, FormaObtencao.Ligacao];

    public static string Forma(FormaObtencao forma) => forma switch
    {
        FormaObtencao.NaoAplica => "—",
        FormaObtencao.App => "Pelo app (QR Code)",
        FormaObtencao.Sistema => "Pelo sistema",
        FormaObtencao.Ligacao => "Ligar para o paciente",
        _ => RotulosEnum.De(forma)
    };

    public static string Modalidade(ModalidadeAtendimento modalidade) => ModalidadeInfo.NomeExibicao(modalidade);

    public static string Categoria(Categoria categoria) => RotulosEnum.De(categoria);

    public static string Formato(FormatoNumeroGuia formato) => RotulosEnum.De(formato);

    /// <summary>
    /// Coluna "Guias" da lista (parcela 69): escrever só "Não" faria a linha do particular
    /// parecer convênio mal configurado; o que ela é tem nome.
    /// </summary>
    public static string ResumoGuia(bool geraGuia) => geraGuia ? "Gera guia" : "Particular (sem guia)";

    /// <summary>Embutido: a regra vive no código; não muda de família nem se exclui.</summary>
    public static bool ConvenioEmbutido(ConvenioCadastro c) => ConvenioCatalogoService.EhEmbutido(c.Codigo);
    /// <summary>
    /// Excluir só a variante cadastrada pela clínica. O PARTICULAR e o "A DEFINIR" também
    /// ficam de fora (desvio deliberado do WPF): o catálogo os GARANTE na leitura, então
    /// excluí-los não os tira de lugar nenhum — eles voltariam na próxima abertura com os
    /// padrões de fábrica, apagando em silêncio o nome ou o formato que a clínica deu a eles.
    /// </summary>
    public static bool ConvenioPodeSerExcluido(ConvenioCadastro c)
        => !ConvenioEmbutido(c)
           && !string.Equals(c.Codigo, ConvenioCadastro.CodigoParticular, StringComparison.OrdinalIgnoreCase)
           && !string.Equals(c.Codigo, ConvenioCadastro.CodigoADefinir, StringComparison.OrdinalIgnoreCase);

    public static bool ModalidadeEmbutida(ModalidadeCadastro m) => ModalidadeCatalogoService.EhEmbutida(m.Codigo);
    public static bool EspecialidadeEmbutida(EspecialidadeCadastro e) => EspecialidadeCatalogoService.EhEmbutida(e.Codigo);

    // ===================== Itens novos =====================

    /// <summary>"Base", "Base 2", … — o primeiro nome que ainda não existe (sem diferenciar caixa).</summary>
    public static string NomeUnico(string baseNome, IEnumerable<string> existentes)
    {
        var usados = existentes.ToList();
        var nome = baseNome;
        for (var n = 2; usados.Any(x => string.Equals(x, nome, StringComparison.OrdinalIgnoreCase)); n++)
            nome = $"{baseNome} {n}";
        return nome;
    }

    /// <summary>
    /// Convênio novo: nasce PERSONALIZADO (a regra configurável pela clínica), ativo e com
    /// nome provisório único — senão criar dois seguidos esbarraria na recusa de duplicados.
    /// </summary>
    public static ConvenioCadastro NovoConvenio(IEnumerable<string> nomesExistentes) => new()
    {
        Codigo = "CV" + Guid.NewGuid().ToString("N")[..8],
        Nome = NomeUnico("Novo convênio", nomesExistentes),
        Familia = Convenio.Personalizado,
        Ativo = true
    };

    public static ModalidadeCadastro NovaModalidade(IEnumerable<string> nomesExistentes) => new()
    {
        Codigo = "MD" + Guid.NewGuid().ToString("N")[..8],
        Nome = NomeUnico("Nova modalidade", nomesExistentes),
        Base = ModalidadeAtendimento.AcupunturaSimples,
        Ativo = true
    };

    public static EspecialidadeCadastro NovaEspecialidade(IEnumerable<string> nomesExistentes) => new()
    {
        Codigo = "ES" + Guid.NewGuid().ToString("N")[..8],
        Nome = NomeUnico("Nova especialidade", nomesExistentes),
        Ativo = true
    };

    // ===================== Antes de salvar =====================

    /// <summary>Os nomes repetidos (sem diferenciar caixa nem espaço nas pontas), ou null.</summary>
    public static string? NomesDuplicados(IEnumerable<string?> nomes)
    {
        var dup = nomes
            .GroupBy(n => (n ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        return dup.Count > 0 ? string.Join(", ", dup) : null;
    }

    /// <summary>
    /// A recusa do "Salvar configurações": nomes repetidos confundem os cadastros e os
    /// lançamentos. Null = pode gravar.
    /// </summary>
    public static string? ConferirAntesDeSalvar(
        IEnumerable<ConvenioCadastro> convenios,
        IEnumerable<ModalidadeCadastro> modalidades,
        IEnumerable<EspecialidadeCadastro> especialidades)
    {
        if (NomesDuplicados(convenios.Select(c => c.Nome)) is { } c)
            return $"Há convênios com o mesmo nome: {c}. Dê nomes diferentes para não confundir os cadastros.";
        if (NomesDuplicados(modalidades.Select(m => m.Nome)) is { } m)
            return $"Há modalidades com o mesmo nome: {m}. Dê nomes diferentes para não confundir os lançamentos.";
        if (NomesDuplicados(especialidades.Select(e => e.Nome)) is { } e)
            return $"Há especialidades com o mesmo nome: {e}. Dê nomes diferentes para não confundir os lançamentos.";
        return null;
    }

    /// <summary>
    /// A cópia campo a campo do convênio (o <c>ParaCadastro</c> do WPF). Serve às duas pontas:
    /// a tela edita uma CÓPIA (não o objeto que o serviço devolveu) e grava outra, com o
    /// registro ANS em branco virando nulo — em branco o lote usa o registro da clínica.
    /// ⚠️ Campo novo em <see cref="ConvenioCadastro"/> entra AQUI (há teste por reflexão).
    /// </summary>
    public static ConvenioCadastro Copia(ConvenioCadastro c) => new()
    {
        Codigo = c.Codigo,
        Nome = c.Nome,
        Familia = c.Familia,
        Ativo = c.Ativo,
        FormatoNumeroGuia = c.FormatoNumeroGuia,
        RegistroAnsOperadora = string.IsNullOrWhiteSpace(c.RegistroAnsOperadora) ? null : c.RegistroAnsOperadora.Trim(),
        GeraGuia = c.GeraGuia,
        FazEletro = c.FazEletro,
        TemSegundoCodigo = c.TemSegundoCodigo,
        FormaSegundoCodigo = c.FormaSegundoCodigo,
        SegundoCodigoDependeApp = c.SegundoCodigoDependeApp,
        DiasSegundoCodigo = c.DiasSegundoCodigo,
        FaturaBsv = c.FaturaBsv,
        InverteDatasBsv = c.InverteDatasBsv,
        ValidadeConsultaDias = c.ValidadeConsultaDias,
        CategoriaComApp = c.CategoriaComApp,
        CategoriaSemApp = c.CategoriaSemApp
    };

    public static ModalidadeCadastro Copia(ModalidadeCadastro m) => new()
    {
        Codigo = m.Codigo, Nome = m.Nome, Base = m.Base, Ativo = m.Ativo
    };

    public static EspecialidadeCadastro Copia(EspecialidadeCadastro e) => new()
    {
        Codigo = e.Codigo, Nome = e.Nome, Ativo = e.Ativo
    };

    public static ParametroConvenio Copia(ParametroConvenio p) => new()
    {
        Convenio = p.Convenio,
        Nome = p.Nome,
        Ativo = p.Ativo,
        ValidadeConsultaDias = p.ValidadeConsultaDias,
        DiasSegundoCodigo = p.DiasSegundoCodigo,
        CategoriaComApp = p.CategoriaComApp,
        CategoriaSemApp = p.CategoriaSemApp
    };

    // ===================== Prazos (o RegrasFaturamentoViewModel do shell) =====================

    /// <summary>
    /// Lê os três prazos como a tela os digitou. Antecedência do alerta de 0 a 365; recurso de
    /// glosa e prazo de decisão de 1 a 365. Null + erro = recusa, com a frase do WPF.
    /// </summary>
    public static PrazosDoFaturamento? LerPrazos(string? janela, string? glosa, string? rodada, out string? erro)
    {
        if (!int.TryParse(janela?.Trim(), out var j) || j is < 0 or > 365 ||
            !int.TryParse(glosa?.Trim(), out var g) || g is < 1 or > 365 ||
            !int.TryParse(rodada?.Trim(), out var r) || r is < 1 or > 365)
        {
            erro = "Informe a antecedência entre 0 e 365 dias e os demais prazos entre 1 e 365 dias.";
            return null;
        }
        erro = null;
        return new PrazosDoFaturamento(j, g, r);
    }

    // ===================== Prestador: relê e mescla =====================

    /// <summary>
    /// Troca SÓ os códigos TUSS que a tela edita, sobre o prestador que está no banco AGORA.
    /// Os outros campos (Pix, nome fantasia, endereço…) podem ter sido gravados por outra
    /// tela desde que esta abriu, e a tela de TUSS não os conhece: regravá-los com o que ela
    /// leu na abertura apagaria a mudança da outra pessoa.
    /// </summary>
    public static DadosPrestador MesclarTuss(DadosPrestador atual, IReadOnlyDictionary<TipoCodigo, string?> tuss)
    {
        var codigos = new Dictionary<TipoCodigo, string>(atual.CodigosTuss);
        foreach (var (tipo, codigo) in tuss)
            codigos[tipo] = codigo?.Trim() ?? string.Empty;
        atual.CodigosTuss = codigos;
        return atual;
    }

    /// <summary>
    /// Troca SÓ os dados de identificação da clínica (o <c>DadosClinicaViewModel</c>), sobre o
    /// prestador do banco — os códigos TUSS que outra tela gravou ficam intactos.
    /// </summary>
    public static DadosPrestador MesclarDadosDaClinica(DadosPrestador atual, DadosDaClinica d)
    {
        static string? Limpo(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        atual.RazaoSocial = Limpo(d.RazaoSocial);
        atual.NomeFantasia = Limpo(d.NomeFantasia);
        atual.Cnpj = Limpo(d.Cnpj);
        atual.Cnes = Limpo(d.Cnes);
        atual.Endereco = Limpo(d.Endereco);
        atual.Telefone = Limpo(d.Telefone);
        atual.Email = Limpo(d.Email);
        atual.ChavePix = Limpo(d.ChavePix);
        atual.Cidade = Limpo(d.Cidade);
        atual.CodigoNaOperadora = Limpo(d.CodigoNaOperadora);
        atual.RegistroAnsOperadora = Limpo(d.RegistroAnsOperadora);
        return atual;
    }

    /// <summary>
    /// O Salvar dos códigos TUSS: RELÊ o prestador e mescla (<see cref="MesclarTuss"/>). É o
    /// caminho que a verificação do Windows prova ("Salvar catálogo TISS preserva Pix
    /// atualizado em outro acesso").
    /// </summary>
    public static async Task SalvarCodigosTussAsync(this ParametrosService parametros,
        IReadOnlyDictionary<TipoCodigo, string?> tuss, CancellationToken ct = default)
    {
        var atual = await parametros.ObterPrestadorAsync(ct);
        await parametros.SalvarPrestadorAsync(MesclarTuss(atual, tuss), ct);
    }

    /// <summary>O Salvar dos dados da clínica: relê o prestador e mescla, preservando os TUSS.</summary>
    public static async Task SalvarDadosDaClinicaAsync(this ParametrosService parametros,
        DadosDaClinica dados, CancellationToken ct = default)
    {
        var atual = await parametros.ObterPrestadorAsync(ct);
        await parametros.SalvarPrestadorAsync(MesclarDadosDaClinica(atual, dados), ct);
    }

    /// <summary>Os códigos TUSS que a tela mostra, na ordem dela — um por <see cref="TipoCodigo"/>.</summary>
    public static IReadOnlyList<(TipoCodigo Tipo, string Rotulo)> CodigosTussDaTela { get; } =
    [
        (TipoCodigo.Acupuntura, "Acupuntura"),
        (TipoCodigo.Eletroacupuntura, "Eletroacupuntura"),
        (TipoCodigo.Bsv, "BSV"),
        (TipoCodigo.Consulta, "Consulta"),
        (TipoCodigo.ConsultaEspecialidade, "Consulta especialidade"),
        (TipoCodigo.Preventivo, "Preventivo (coleta)")
    ];
}

/// <summary>Os três prazos globais do faturamento, já validados.</summary>
public sealed record PrazosDoFaturamento(int JanelaAlertaConsultaDias, int PrazoRecursoGlosaDias, int IntervaloRodadaPendenciasDias);

/// <summary>Os campos de identificação da clínica que a aba "Clínica / prestador" edita.</summary>
public sealed record DadosDaClinica(
    string? RazaoSocial, string? NomeFantasia, string? Cnpj, string? Cnes, string? Endereco,
    string? Telefone, string? Email, string? ChavePix, string? Cidade,
    string? CodigoNaOperadora, string? RegistroAnsOperadora)
{
    public static DadosDaClinica De(DadosPrestador d) => new(
        d.RazaoSocial, d.NomeFantasia, d.Cnpj, d.Cnes, d.Endereco, d.Telefone, d.Email,
        d.ChavePix, d.Cidade, d.CodigoNaOperadora, d.RegistroAnsOperadora);
}
