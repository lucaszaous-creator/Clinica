using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a tela "Importar pacientes" do Gerente AFIRMA (Blazor desde out/2026, etapa 6) — antes no
/// <c>ImportacaoPacientesViewModel</c> do WPF: os rótulos das colunas, a sugestão do mapeamento e
/// dos convênios, a ordem da prévia, as frases e a conferência.
/// </summary>
public class ImportacaoDePacientesNaTelaTests
{
    private static TabelaImportada Tabela(string[] colunas, params string[][] linhas) => new()
    {
        Colunas = colunas, Linhas = linhas, Separador = ';', Codificacao = "UTF-8"
    };

    private static ConvenioCadastro Conv(string codigo, string nome, bool ativo = true)
        => new() { Codigo = codigo, Nome = nome, Familia = Convenio.Personalizado, Ativo = ativo };

    // ============================================================ colunas

    [Fact]
    public void Coluna_sem_nome_e_nome_repetido_viram_rotulos_unicos()
    {
        var r = ImportacaoDePacientesNaTela.RotulosUnicos(["Nome", "Telefone", "", "telefone", "Telefone"]);
        r.Should().Equal("Nome", "Telefone", "(coluna 3)", "telefone (2)", "Telefone (3)");
    }

    [Fact]
    public void A_sugestao_marca_a_coluna_pelo_nome_e_o_resto_fica_sem_importar()
    {
        var t = Tabela(["Nome", "CPF", "Celular", "Coisa nenhuma"]);
        var rotulos = ImportacaoDePacientesNaTela.RotulosUnicos(t.Colunas);
        var s = ImportacaoDePacientesNaTela.SugestaoDeColunas(t, rotulos);

        s[CampoImportacao.Nome].Should().Be("Nome");
        s[CampoImportacao.Cpf].Should().Be("CPF");
        s[CampoImportacao.Telefone].Should().Be("Celular");
        s[CampoImportacao.Convenio].Should().Be(ImportacaoDePacientesNaTela.NaoImportar);
        s.Keys.Should().BeEquivalentTo(CamposImportacao.Todos, "todo campo da ficha tem uma escolha");
    }

    [Fact]
    public void O_mapeamento_segue_o_rotulo_mesmo_com_colunas_de_nome_repetido()
    {
        var t = Tabela(["Nome", "Telefone", "Telefone"]);
        var rotulos = ImportacaoDePacientesNaTela.RotulosUnicos(t.Colunas);
        var escolhas = CamposImportacao.Todos.ToDictionary(c => c, _ => ImportacaoDePacientesNaTela.NaoImportar);
        escolhas[CampoImportacao.Nome] = "Nome";
        escolhas[CampoImportacao.Telefone] = "Telefone (2)";

        var mapa = ImportacaoDePacientesNaTela.Mapeamento(escolhas, rotulos);
        mapa.ColunaDe(CampoImportacao.Nome).Should().Be(0);
        mapa.ColunaDe(CampoImportacao.Telefone).Should().Be(2, "a segunda coluna 'Telefone' continua alcançável");
        mapa.Tem(CampoImportacao.Cpf).Should().BeFalse("'não importar' não entra no mapa");
    }

    [Fact]
    public void So_o_nome_e_obrigatorio()
    {
        CamposImportacao.Todos.Where(ImportacaoDePacientesNaTela.Obrigatorio)
            .Should().Equal(CampoImportacao.Nome);
    }

    // ============================================================ convênios

    [Fact]
    public void A_primeira_opcao_e_definir_depois_e_o_a_definir_do_catalogo_nao_se_repete()
    {
        var catalogo = ImportacaoDePacientesNaTela.CatalogoAtivo([
            Conv("Unimed", "Unimed"), Conv(ConvenioCadastro.CodigoADefinir, "A definir"),
            Conv("Velho", "Velho inativo", ativo: false), Conv("Amil", "Amil")]);
        var opcoes = ImportacaoDePacientesNaTela.OpcoesDeConvenio(catalogo);

        opcoes[0].Rotulo.Should().Be(ImportacaoDePacientesNaTela.DefinirDepois);
        opcoes[0].Cadastro.Codigo.Should().Be(ConvenioCadastro.CodigoADefinir);
        opcoes.Skip(1).Select(o => o.Rotulo).Should().Equal("Amil", "Unimed");
    }

    [Fact]
    public void A_sugestao_do_convenio_e_pelo_nome_e_sem_parecido_fica_definir_depois()
    {
        var opcoes = ImportacaoDePacientesNaTela.OpcoesDeConvenio([Conv("Amil", "Amil"), Conv("Unimed", "Unimed Costa do Sol")]);

        ImportacaoDePacientesNaTela.SugerirConvenio("AMIL", opcoes).Should().Be(1);
        ImportacaoDePacientesNaTela.SugerirConvenio("unimed", opcoes).Should().Be(2, "o nome daqui contém o do arquivo");
        ImportacaoDePacientesNaTela.SugerirConvenio("Bradesco", opcoes).Should().Be(0);
        ImportacaoDePacientesNaTela.SugerirConvenio("", opcoes).Should().Be(0);
        ImportacaoDePacientesNaTela.SugerirConvenio("Amil", []).Should().Be(0, "nunca um índice numa lista vazia");
    }

    [Fact]
    public void Os_convenios_a_mapear_contam_as_linhas_de_cada_texto()
    {
        var t = Tabela(["Nome", "Convênio"],
            ["Ana", "Unimed"], ["Bia", "unimed"], ["Caio", ""], ["Duda", "Amil"]);
        var mapa = new MapeamentoImportacao();
        mapa.Definir(CampoImportacao.Nome, 0);
        mapa.Definir(CampoImportacao.Convenio, 1);

        var lista = ImportacaoDePacientesNaTela.ConveniosAMapear(t, mapa);
        lista.Should().HaveCount(3);
        lista.Single(c => c.Texto == "Unimed").Linhas.Should().Be(2, "o texto casa sem diferenciar maiúsculas");
        lista.Single(c => c.Texto == ImportacaoPacientesService.ConvenioEmBranco).LinhasTexto.Should().Be("1 linha");
    }

    [Fact]
    public void Sem_coluna_de_convenio_a_lista_e_uma_linha_em_branco_com_todas_as_linhas()
    {
        var t = Tabela(["Nome"], ["Ana"], ["Bia"]);
        var mapa = new MapeamentoImportacao();
        mapa.Definir(CampoImportacao.Nome, 0);

        var lista = ImportacaoDePacientesNaTela.ConveniosAMapear(t, mapa);
        lista.Should().ContainSingle().Which.Should().Be(new ConvenioDoArquivo(ImportacaoPacientesService.ConvenioEmBranco, 2));
    }

    [Fact]
    public void Os_convenios_escolhidos_seguem_o_indice_de_cada_texto()
    {
        var opcoes = ImportacaoDePacientesNaTela.OpcoesDeConvenio([Conv("Amil", "Amil")]);
        var d = ImportacaoDePacientesNaTela.ConveniosEscolhidos([("AMIL", 1), ("Outro", 0), ("Fora", 9)], opcoes);

        d["amil"].Codigo.Should().Be("Amil", "a chave não diferencia maiúsculas");
        d["Outro"].Codigo.Should().Be(ConvenioCadastro.CodigoADefinir);
        d.Should().NotContainKey("Fora", "índice fora das opções não vira convênio");
    }

    // ============================================================ prévia

    private static LinhaPrevia Linha(int n, DestinoLinha d, params string[] avisos)
        => new(n, $"P{n}", null, null, d, "detalhe", avisos);

    [Fact]
    public void Quem_pede_acao_sobe_problema_depois_aviso_depois_o_resto_pela_linha()
    {
        var previa = new PreviaImportacao("smartclinic", [
            Linha(1, DestinoLinha.Criar), Linha(2, DestinoLinha.Problema), Linha(3, DestinoLinha.Completar, "sexo em branco"),
            Linha(4, DestinoLinha.JaImportada), Linha(5, DestinoLinha.Problema)], []);

        ImportacaoDePacientesNaTela.LinhasEmOrdem(previa).Select(l => l.Numero).Should().Equal(2, 5, 3, 1, 4);
    }

    [Fact]
    public void O_resumo_e_o_botao_dizem_o_tamanho_do_lote()
    {
        var previa = new PreviaImportacao("smartclinic", [
            Linha(1, DestinoLinha.Criar), Linha(2, DestinoLinha.Criar, "aviso"), Linha(3, DestinoLinha.Completar),
            Linha(4, DestinoLinha.Problema)], ["Aviso geral"]);

        ImportacaoDePacientesNaTela.ResumoDaPrevia(previa).Should().Be(
            "2 ficha(s) nova(s) · 1 já cadastrada(s) a completar · 0 já importada(s) · 1 linha(s) que não entram · 1 com aviso");
        ImportacaoDePacientesNaTela.RotuloImportar(previa, null).Should().Be("Importar 3 ficha(s)");
        ImportacaoDePacientesNaTela.AvisosGerais(previa, null).Should().Be("Aviso geral");
        ImportacaoDePacientesNaTela.ConfirmacaoDaImportacao(previa, null)
            .Should().StartWith("Vão ser criadas 2 ficha(s) nova(s) e completadas 1 já cadastrada(s). As 1 linha(s) com problema ficam de fora.")
            .And.Contain("Não há desfazer em lote");
    }

    [Fact]
    public void Sem_trabalho_o_botao_diz_nada_a_importar()
    {
        var previa = new PreviaImportacao("smartclinic", [Linha(1, DestinoLinha.JaImportada)], []);
        ImportacaoDePacientesNaTela.TemTrabalho(previa, null).Should().BeFalse();
        ImportacaoDePacientesNaTela.RotuloImportar(previa, null).Should().Be("Nada a importar");
        ImportacaoDePacientesNaTela.AvisosGerais(previa, null).Should().BeNull();
    }

    [Fact]
    public void No_pacote_o_prontuario_e_a_agenda_contam_como_trabalho_e_entram_no_botao()
    {
        var pacientes = new PreviaImportacao("smartclinic", [Linha(1, DestinoLinha.JaImportada)], []);
        var pacote = new PreviaSmartClinic(pacientes,
            [new ResumoArquivoClinico("pos_operatorio.csv", "Pós-operatório", 5, 2, 4, 1, 0, 0)],
            new ResumoAgendaAnterior(3, 2, 1, 10, 4, ["Dra. Ana"], []),
            ["Ana Autora"], [], ["Aviso do pacote"]);

        ImportacaoDePacientesNaTela.TemTrabalho(pacientes, pacote).Should().BeTrue("há prontuário e agenda a gravar");
        ImportacaoDePacientesNaTela.RotuloImportar(pacientes, pacote)
            .Should().Be("Importar o pacote (0 ficha(s) · 4 registro(s) · 2 horário(s))");
        ImportacaoDePacientesNaTela.AvisosGerais(pacientes, pacote).Should().Be("Aviso do pacote");

        var resumo = ImportacaoDePacientesNaTela.ResumoDoPacote(pacote);
        resumo[0].Should().Be("Pós-operatório: 5 registro(s) de 2 paciente(s) — 4 entram, 1 já importado(s).");
        resumo.Should().Contain(l => l.StartsWith("Agenda antiga: 3 horário(s) de hoje em diante viram horário aqui (2 novo(s), 1 já importado(s));"));
        resumo.Should().Contain("Autores do prontuário reconhecidos na Equipe: Ana Autora.");
        resumo.Should().Contain("Profissionais da agenda reconhecidos na Equipe: Dra. Ana.");
        resumo[^1].Should().Contain("Login e senha do sistema antigo não entram");
        ImportacaoDePacientesNaTela.ConfirmacaoDaImportacao(pacientes, pacote)
            .Should().Contain("Do prontuário antigo entram 4 registro(s), e 2 horário(s) futuro(s) da agenda antiga.");
    }

    [Fact]
    public void A_previa_dos_anexos_lista_as_linhas_de_fora_ate_o_teto()
    {
        var linhas = Enumerable.Range(1, 63)
            .Select(i => new LinhaAnexoPrevia(i, $"a{i}", "Pac", "Receita", new DateOnly(2025, 1, 1),
                DestinoAnexo.SemPaciente, "sem ficha", null, null, null))
            .Append(new LinhaAnexoPrevia(99, "ok", "Pac", "Laudo", new DateOnly(2025, 1, 1), DestinoAnexo.Novo, null, 1, "ok.pdf", null))
            .ToList();
        var previa = new PreviaAnexosSmartClinic(linhas, 1, 0, 63, 0, 0, 1, 2 * 1024 * 1024, []);

        var tela = ImportacaoDePacientesNaTela.PreviaDosAnexos(previa);
        tela.Resumo[0].Should().Be("Arquivos da ficha: 1 entram em 1 ficha(s) (2 MB) · 63 sem ficha.");
        tela.Resumo.Count(l => l.StartsWith("  • Não entra")).Should().Be(ImportacaoDePacientesNaTela.TetoDeLinhasDeFora);
        tela.Resumo.Should().Contain("  • … e mais 3 linha(s) de fora.");
        tela.ResumoPrevia.Should().Be("1 arquivo(s) entram · 0 já importado(s) · 63 linha(s) que não entram");
        tela.RotuloImportar.Should().Be("Importar 1 arquivo(s)");
        tela.TemTrabalho.Should().BeTrue();
        tela.Avisos.Should().BeNull();
    }

    // ============================================================ resultado e conferência

    [Fact]
    public void O_resultado_do_csv_com_erro_e_meio_sucesso_e_nao_sucesso()
    {
        var erros = Enumerable.Range(1, 22).Select(i => $"linha {i}: recusada").ToList();
        var (texto, teveErro) = ImportacaoDePacientesNaTela.ResultadoDoCsv(new ResultadoImportacao(3, 1, 2, erros));

        teveErro.Should().BeTrue();
        texto.Should().StartWith("Importação concluída: 3 ficha(s) nova(s), 1 completada(s), 2 já existente(s).")
            .And.Contain("22 linha(s) não gravaram:").And.EndWith("\n…");
        ImportacaoDePacientesNaTela.ResultadoDoCsv(new ResultadoImportacao(1, 0, 0, [])).TeveErro.Should().BeFalse();
    }

    [Fact]
    public void O_resultado_do_pacote_soma_os_erros_e_diz_os_revinculos()
    {
        var r = new ResultadoSmartClinic(new ResultadoImportacao(2, 1, 0, ["ficha x"]), 10, 1, 2, 3, 0, 0, ["evolução y"])
        {
            Revinculados = 4
        };
        var (texto, teveErro) = ImportacaoDePacientesNaTela.ResultadoDoPacote(r);
        teveErro.Should().BeTrue();
        texto.Should().Contain("10 registro(s) de prontuário (1 já importado(s), 2 sem ficha)")
            .And.Contain("2 erro(s):\nficha x\nevolução y")
            .And.Contain("4 registro(s) de rodadas anteriores ganharam o vínculo com a Equipe.");
    }

    [Fact]
    public void O_resultado_dos_anexos_diz_o_que_gravou()
    {
        var (texto, teveErro) = ImportacaoDePacientesNaTela.ResultadoDosAnexos(new ResultadoAnexosSmartClinic(5, 1, 2, []));
        teveErro.Should().BeFalse();
        texto.Should().Be("Importação concluída: 5 arquivo(s) gravado(s) na ficha, 1 já importado(s), 2 sem ficha.");
    }

    [Fact]
    public void A_conferencia_que_fecha_e_a_que_nao_fecha_tem_titulos_e_sinais_diferentes()
    {
        var completo = new ItemConferencia("Fichas", 3, 3, [], 0);
        var comMotivo = new ItemConferencia("Pós-operatório", 4, 2, ["linha 3: sem ficha"], 0);
        var semMotivo = new ItemConferencia("Agenda", 2, 1, [], 0);

        // "Fechou" não quer dizer "tudo entrou": quer dizer que o que não entrou está LISTADO com a razão.
        var fechou = ImportacaoDePacientesNaTela.ConferenciaDoPacote([completo, comMotivo]);
        fechou.Fechou.Should().BeTrue();
        fechou.Titulo.Should().StartWith("CONFERÊNCIA: fechou");
        fechou.Linhas.Select(l => l.Texto).Should().Equal(
            "✓ Fichas: 3 de 3 no sistema.",
            "✗ Pós-operatório: 2 de 4 no sistema · 1 de fora (veja o motivo).",
            "– linha 3: sem ficha");
        fechou.Linhas[2].EhMotivo.Should().BeTrue();

        var aberta = ImportacaoDePacientesNaTela.ConferenciaDoPacote([completo, semMotivo]);
        aberta.Fechou.Should().BeFalse();
        aberta.Titulo.Should().StartWith("CONFERÊNCIA: NÃO fechou");
        aberta.Linhas[1].Texto.Should().Be("✗ Agenda: 1 de 2 no sistema.");

        ImportacaoDePacientesNaTela.ConferenciaDosAnexos([completo]).Titulo.Should().Contain("todo arquivo do índice está no sistema");
    }

    [Fact]
    public void Cada_destino_tem_um_tom_proprio()
    {
        Enum.GetValues<DestinoLinha>().Select(ImportacaoDePacientesNaTela.TomDoDestino)
            .Should().OnlyHaveUniqueItems("a cor diz o que acontece com a linha");
    }

    [Fact]
    public void As_linhas_que_descrevem_o_arquivo_escolhido()
    {
        var t = Tabela(["Nome", "CPF"], ["Ana", "1"], ["Bia", "2"]);
        ImportacaoDePacientesNaTela.InfoDoCsv(t)
            .Should().Be("2 linha(s) · 2 coluna(s) · separador: ponto e vírgula · codificação: UTF-8");
    }
}
