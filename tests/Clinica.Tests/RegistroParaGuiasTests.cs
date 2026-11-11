using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O REGISTRO PARA GUIAS DE CONVÊNIO no cadastro do profissional (nov/2026, decisão da direção:
/// conselho, número, UF e CBO são dados do PROFISSIONAL). Três coisas que a tela e a guia da
/// baixa afirmam e que nenhuma rede pegaria se quebrassem — nada estoura, o campo só some:
///
/// 1. a regra pura (o que é válido, o que se propõe do texto livre, de onde sai cada campo do
///    executante na guia e o que o aviso diz);
/// 2. o serviço GRAVA os quatro campos e — o lugar 3 e o 6 da auditoria de linha — quem não os
///    edita os PRESERVA: nulo é "não sei", vazio é "apague";
/// 3. o texto livre do conselho continua intocado: ele é o que sai nos documentos clínicos.
/// </summary>
public sealed class RegistroParaGuiasTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly EquipeService _equipe;

    public RegistroParaGuiasTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _equipe = new EquipeService(new ClinicaRepositorio(_db));
        new SessaoUsuario().Entrar(new UsuarioSistema());
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    // ==================== a regra pura ====================

    [Theory]
    [InlineData("52.1234-5", "5212345")]
    [InlineData(" 12345-f ", "12345F")]
    [InlineData("12/345", "12345")]
    [InlineData("   ", null)]
    [InlineData(".-/", null)]
    [InlineData(null, null)]
    public void O_numero_e_guardado_como_a_operadora_o_recebe(string? digitado, string? esperado)
        => RegistroDoProfissionalParaGuias.LimparNumero(digitado).Should().Be(esperado);

    [Fact]
    public void Em_branco_nunca_e_erro_e_preencher_aos_poucos_tambem_nao()
    {
        RegistroDoProfissionalParaGuias.Criticar(null, null, null, null).Should().BeEmpty();
        RegistroDoProfissionalParaGuias.Criticar("", " ", "", "").Should().BeEmpty();
        // Só o CBO, ou só o conselho: opcional é opcional — o cadastro não trava ninguém.
        RegistroDoProfissionalParaGuias.Criticar(null, null, null, "225105").Should().BeEmpty();
        RegistroDoProfissionalParaGuias.Criticar("06", null, null, null).Should().BeEmpty();
        RegistroDoProfissionalParaGuias.Criticar("06", "52.1234", "33", "225105").Should().BeEmpty();
    }

    [Fact]
    public void So_se_recusa_o_que_a_operadora_recusaria_e_cada_campo_tem_a_sua_frase()
    {
        // A SIGLA no lugar do código é o engano provável de quem grava por fora da tela.
        var erros = RegistroDoProfissionalParaGuias.Criticar("CRM", "12#45", "RJ", "123456");

        erros.Keys.Should().BeEquivalentTo(
            nameof(Profissional.ConselhoProfissional), nameof(Profissional.NumeroConselho),
            nameof(Profissional.UfConselho), nameof(Profissional.Cbos));
        erros[nameof(Profissional.NumeroConselho)].Should().Contain("letras e números");

        RegistroDoProfissionalParaGuias.Criticar(null, new string('1', 16), null, null)[nameof(Profissional.NumeroConselho)]
            .Should().Contain("15");
        // Quinze cabem; e a pontuação não conta para o tamanho, porque sai antes de gravar.
        RegistroDoProfissionalParaGuias.Criticar(null, "123.456.789-012.345", null, null).Should().BeEmpty();
    }

    [Fact]
    public void A_proposta_do_texto_livre_so_existe_com_os_tres_campos_em_branco_e_nunca_traz_CBO()
    {
        var proposta = RegistroDoProfissionalParaGuias.PropostaDoTextoLivre("CRM-RJ 52.1234", "", " ", null);

        proposta.Should().Be(new RegistroParaGuias("06", "521234", "33", null));
        RegistroDoProfissionalParaGuias.FraseDaProposta(proposta!, "CRM-RJ 52.1234")
            .Should().Contain("CRM").And.Contain("521234").And.Contain("RJ").And.Contain("CBO");

        // Qualquer um dos três já preenchido: a pessoa está cadastrando, e não se propõe por cima.
        RegistroDoProfissionalParaGuias.PropostaDoTextoLivre("CRM-RJ 52.1234", "06", null, null).Should().BeNull();
        RegistroDoProfissionalParaGuias.PropostaDoTextoLivre("CRM-RJ 52.1234", null, "1", null).Should().BeNull();
        RegistroDoProfissionalParaGuias.PropostaDoTextoLivre("CRM-RJ 52.1234", null, null, "33").Should().BeNull();
        // Texto vazio ou que não deixa ler nada: sem proposta — nunca um palpite.
        RegistroDoProfissionalParaGuias.PropostaDoTextoLivre(null, null, null, null).Should().BeNull();
        RegistroDoProfissionalParaGuias.PropostaDoTextoLivre("a combinar", null, null, null).Should().BeNull();
    }

    [Fact]
    public void A_lista_de_CBO_vai_por_nome_e_a_de_UF_por_sigla_sem_perder_nenhum_codigo()
    {
        RegistroDoProfissionalParaGuias.CbosPorNome.Select(v => v.Codigo).Should().BeEquivalentTo(DominiosTiss.Cbos.Select(v => v.Codigo));
        RegistroDoProfissionalParaGuias.UfsPorSigla.Select(v => v.Codigo).Should().BeEquivalentTo(DominiosTiss.Uf.Select(v => v.Codigo));

        // Os termos que a ANS publica com "- " na frente entram pela PALAVRA, não pelo traço.
        var nomes = RegistroDoProfissionalParaGuias.CbosPorNome.Select(RegistroDoProfissionalParaGuias.NomeDoCbo).ToList();
        nomes.Should().NotContain(n => n.StartsWith('-') || n.StartsWith(' '));
        nomes.Should().BeInAscendingOrder(StringComparer.Create(new System.Globalization.CultureInfo("pt-BR"), ignoreCase: true));
        RegistroDoProfissionalParaGuias.RotuloDoCbo(DominiosTiss.Cbos.Single(v => v.Codigo == "226310")).Should().Be("Arteterapeuta (226310)");
        RegistroDoProfissionalParaGuias.UfsPorSigla.Select(v => v.Termo).Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    // ==================== de onde sai o executante da guia ====================

    private static Profissional Medica(string? conselho = null, string? numero = null, string? uf = null, string? cbos = null,
        string? livre = "CRM-RJ 123456")
        => new()
        {
            Nome = "Dra. Ana de Teste", RegistroConselho = livre,
            ConselhoProfissional = conselho, NumeroConselho = numero, UfConselho = uf, Cbos = cbos
        };

    private static GuiaTissConsulta UltimaGuia() => new()
    {
        NomeProfissional = "Dra. Ana de Teste", ConselhoProfissional = "09", NumeroConselho = "777", UfConselho = "35", Cbos = "251510"
    };

    [Fact]
    public void Cadastro_completo_manda_sozinho_e_nao_ha_aviso()
    {
        var (proposto, aviso) = RegistroDoProfissionalParaGuias.ParaAGuia(Medica("06", "52.1234", "33", "225105"), UltimaGuia());

        // Nem a última guia nem o texto livre passam por cima do cadastro.
        proposto.Should().Be(new RegistroParaGuias("06", "521234", "33", "225105"));
        aviso.Should().BeNull();
    }

    [Fact]
    public void Sem_cadastro_a_ultima_guia_conferida_vem_antes_do_texto_livre()
    {
        var (proposto, aviso) = RegistroDoProfissionalParaGuias.ParaAGuia(Medica(), UltimaGuia());

        proposto.Should().Be(new RegistroParaGuias("09", "777", "35", "251510"));
        aviso.Should().Contain("última guia conferida").And.Contain(RegistroDoProfissionalParaGuias.OndeCadastrar)
            .And.NotContain("registro em texto").And.NotContain("informe");
    }

    [Fact]
    public void Sem_cadastro_e_sem_guia_anterior_vale_o_texto_livre_e_o_CBO_fica_em_branco()
    {
        var (proposto, aviso) = RegistroDoProfissionalParaGuias.ParaAGuia(Medica(), null);

        proposto.Should().Be(new RegistroParaGuias("06", "123456", "33", null));
        aviso.Should().Contain("registro em texto (\"CRM-RJ 123456\")").And.Contain("informe CBO")
            .And.Contain(RegistroDoProfissionalParaGuias.OndeCadastrar);
    }

    /// <summary>
    /// A MISTURA é por campo: o cadastro tem o CBO e a UF, não tem conselho nem número. O CBO
    /// da última guia (outro!) não entra; o conselho e o número dela, sim.
    /// </summary>
    [Fact]
    public void Cadastro_parcial_fica_com_o_que_tem_e_busca_so_o_que_falta()
    {
        var (proposto, aviso) = RegistroDoProfissionalParaGuias.ParaAGuia(Medica(uf: "33", cbos: "225105"), UltimaGuia());

        proposto.Should().Be(new RegistroParaGuias("09", "777", "33", "225105"));
        aviso.Should().Contain("está sem conselho e número para guias").And.Contain("conselho e número vieram da última guia");
    }

    /// <summary>Cadastro com CBO e sem número, sem guia anterior e com texto que só dá o conselho.</summary>
    [Fact]
    public void O_que_nenhuma_fonte_sabe_fica_em_branco_e_o_aviso_manda_informar()
    {
        var (proposto, aviso) = RegistroDoProfissionalParaGuias.ParaAGuia(Medica(cbos: "225105", livre: "CRM a confirmar"), null);

        proposto.Should().Be(new RegistroParaGuias("06", null, null, "225105"));
        aviso.Should().Contain("conselho foi lido do registro em texto").And.Contain("informe número e UF")
            .And.NotContain("informe CBO"); // o CBO veio do cadastro: não é falta
        aviso.Should().Contain("está sem conselho, número e UF para guias");
    }

    // ==================== o serviço: grava, preserva, apaga e recusa ====================

    [Fact]
    public async Task Salvar_grava_os_quatro_campos_nos_codigos_e_limpa_o_numero()
    {
        var salvo = await _equipe.SalvarProfissionalAsync(new Profissional
        {
            Nome = "Dra. Ana de Teste", RegistroConselho = "CRM-RJ 52.1234",
            ConselhoProfissional = "06", NumeroConselho = " 52.1234 ", UfConselho = "33", Cbos = "225105"
        });
        _db.ChangeTracker.Clear();

        var lido = await _db.Profissionais.AsNoTracking().SingleAsync(p => p.Id == salvo.Id);
        (lido.ConselhoProfissional, lido.NumeroConselho, lido.UfConselho, lido.Cbos).Should().Be(("06", "521234", "33", "225105"));
        // O texto livre é de quem o digitou: vai para os documentos como estava.
        lido.RegistroConselho.Should().Be("CRM-RJ 52.1234");
    }

    /// <summary>
    /// Quem não edita, PRESERVA: a tela de Acessos e a importação regravam o profissional sem
    /// estes campos (nulo). Antes desta regra a primeira correção de CPF apagaria o CBO.
    /// </summary>
    [Fact]
    public async Task Regravar_sem_os_campos_nao_os_apaga_e_vazio_apaga_so_o_que_veio_vazio()
    {
        var salvo = await _equipe.SalvarProfissionalAsync(new Profissional
        {
            Nome = "Dra. Ana de Teste", ConselhoProfissional = "06", NumeroConselho = "521234", UfConselho = "33", Cbos = "225105"
        });
        _db.ChangeTracker.Clear();

        // Outra porta mexe no telefone e não conhece o registro para guias.
        await _equipe.SalvarProfissionalAsync(new Profissional { Id = salvo.Id, Nome = "Dra. Ana de Teste", Telefone = "(21) 90000-0000" });
        _db.ChangeTracker.Clear();
        var depois = await _db.Profissionais.AsNoTracking().SingleAsync(p => p.Id == salvo.Id);
        depois.Telefone.Should().Be("(21) 90000-0000");
        (depois.ConselhoProfissional, depois.NumeroConselho, depois.UfConselho, depois.Cbos).Should().Be(("06", "521234", "33", "225105"));

        // A tela devolve VAZIO no campo que a pessoa apagou — e só ele some.
        await _equipe.SalvarProfissionalAsync(new Profissional
        {
            Id = salvo.Id, Nome = "Dra. Ana de Teste", ConselhoProfissional = "06", NumeroConselho = "", UfConselho = "33", Cbos = "225105"
        });
        _db.ChangeTracker.Clear();
        var apagado = await _db.Profissionais.AsNoTracking().SingleAsync(p => p.Id == salvo.Id);
        (apagado.ConselhoProfissional, apagado.NumeroConselho, apagado.UfConselho, apagado.Cbos).Should().Be(("06", null, "33", "225105"));
    }

    [Theory]
    [InlineData("CRM", null, null, null, "Conselho")]
    [InlineData(null, null, "RJ", null, "UF")]
    [InlineData(null, null, null, "000001", "CBO")]
    [InlineData(null, "1234567890123456", null, null, "15")]
    public async Task Codigo_fora_da_tabela_do_padrao_e_recusado_e_nada_e_gravado(
        string? conselho, string? numero, string? uf, string? cbos, string trecho)
    {
        var gravar = () => _equipe.SalvarProfissionalAsync(new Profissional
        {
            Nome = "Dra. Ana de Teste", ConselhoProfissional = conselho, NumeroConselho = numero, UfConselho = uf, Cbos = cbos
        });

        (await gravar.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain(trecho);
        _db.ChangeTracker.Clear();
        (await _db.Profissionais.CountAsync()).Should().Be(0);
    }
}
