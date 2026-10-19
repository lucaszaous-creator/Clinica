using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a tela de Acessos do Gerente AFIRMA (Blazor desde out/2026, etapa 6) — antes no
/// <c>AcessosViewModel</c> + <c>UsuarioEdicaoViewModel</c> do WPF: a procedência de cada
/// permissão, o resumo das exceções e o DELTA que vira extras/negadas.
/// </summary>
public class AcessosNaTelaTests
{
    private static UsuarioSistema Usuario(PerfilAcesso perfil, Permissao extras = Permissao.Nenhuma,
        Permissao negadas = Permissao.Nenhuma, bool ativo = true, DateTime? ultimoAcesso = null)
        => new()
        {
            Id = 7, Nome = "Ana Teste", Login = "ana", Perfil = perfil, PermissoesExtras = extras,
            PermissoesNegadas = negadas, Ativo = ativo, UltimoAcessoEm = ultimoAcesso
        };

    [Fact]
    public void Procedencia_usa_os_quatro_textos_do_WPF()
    {
        AcessosNaTela.Procedencia(marcada: true, doPerfil: true).Should().Be("padrão do perfil");
        AcessosNaTela.Procedencia(marcada: true, doPerfil: false).Should().Be("LIBERADA para esta pessoa");
        AcessosNaTela.Procedencia(marcada: false, doPerfil: true).Should().Be("TIRADA desta pessoa");
        AcessosNaTela.Procedencia(marcada: false, doPerfil: false).Should().Be("não concedida");
        AcessosNaTela.EhExcecao(true, false).Should().BeTrue();
        AcessosNaTela.EhExcecao(false, true).Should().BeTrue();
        AcessosNaTela.EhExcecao(true, true).Should().BeFalse();
    }

    [Fact]
    public void Delta_marcada_fora_do_padrao_vira_extra_e_desmarcada_dentro_vira_negada()
    {
        var padrao = PerfisAcesso.Padrao(PerfilAcesso.Recepcao);
        var marcadas = (padrao | Permissao.EditarFinanceiro) & ~Permissao.EditarPaciente;

        var (extras, negadas) = AcessosNaTela.Delta(PerfilAcesso.Recepcao, marcadas);

        extras.Should().Be(Permissao.EditarFinanceiro);
        negadas.Should().Be(Permissao.EditarPaciente);
    }

    [Fact]
    public void Delta_do_padrao_exato_e_vazio_e_o_resumo_diz_que_nada_foi_mexido()
    {
        var padrao = PerfisAcesso.Padrao(PerfilAcesso.Financeiro);
        AcessosNaTela.Delta(PerfilAcesso.Financeiro, padrao).Should().Be((Permissao.Nenhuma, Permissao.Nenhuma));
        AcessosNaTela.ResumoDasExcecoes(PerfilAcesso.Financeiro, padrao)
            .Should().Be("Exatamente o padrão de Financeiro — nada foi mexido à mão.");
    }

    [Fact]
    public void Resumo_diz_para_que_lado_a_pessoa_foge_do_padrao()
    {
        var padrao = PerfisAcesso.Padrao(PerfilAcesso.Recepcao);
        AcessosNaTela.ResumoDasExcecoes(PerfilAcesso.Recepcao, padrao | Permissao.VerFinanceiro | Permissao.EditarFinanceiro)
            .Should().Be("2 permissão(ões) LIBERADA(S) além do padrão de Recepção.");
        AcessosNaTela.ResumoDasExcecoes(PerfilAcesso.Recepcao, padrao & ~Permissao.EditarPaciente)
            .Should().Be("1 permissão(ões) TIRADA(S) do padrão de Recepção.");
        AcessosNaTela.ResumoDasExcecoes(PerfilAcesso.Recepcao, (padrao | Permissao.VerFinanceiro) & ~Permissao.EditarPaciente)
            .Should().Be("1 liberada(s) e 1 tirada(s) em relação ao padrão de Recepção.");
    }

    [Fact]
    public void Grupos_cobrem_todas_as_permissoes_individuais_uma_vez_com_a_consequencia()
    {
        var grupos = AcessosNaTela.Grupos();
        var itens = grupos.SelectMany(g => g.Itens).ToList();

        itens.Select(i => i.Valor).Should().BeEquivalentTo(PerfisAcesso.Individuais);
        itens.Select(i => i.Valor).Should().OnlyHaveUniqueItems();
        itens.Should().OnlyContain(i => !string.IsNullOrWhiteSpace(i.Explicacao) && !string.IsNullOrWhiteSpace(i.Rotulo));
        grupos.Select(g => g.Assunto).Should().OnlyHaveUniqueItems();
        grupos.First().Assunto.Should().Be(PerfisAcesso.Assunto(PerfisAcesso.Individuais[0]),
            "a ordem dos blocos é a do enum, como no agrupamento do WPF");
        foreach (var g in grupos)
            g.Itens.Should().OnlyContain(i => PerfisAcesso.Assunto(i.Valor) == g.Assunto);
    }

    [Fact]
    public void Linha_mostra_os_ajustes_feitos_a_mao_e_quem_pode_ser_excluido()
    {
        var padrao = Usuario(PerfilAcesso.Recepcao);
        var linha = AcessosNaTela.Linha(padrao);
        linha.Ajustes.Should().Be("padrão do perfil");
        linha.TemAjuste.Should().BeFalse();
        linha.UltimoAcesso.Should().Be("nunca entrou");
        linha.PodeExcluir.Should().BeTrue();
        linha.Perfil.Should().Be("Recepção");
        linha.Profissional.Should().Be("—");

        var ajustado = AcessosNaTela.Linha(Usuario(PerfilAcesso.Recepcao, extras: Permissao.EditarFinanceiro,
            negadas: Permissao.EditarPaciente, ultimoAcesso: new DateTime(2026, 10, 3, 14, 5, 0)));
        ajustado.Ajustes.Should().Be("1 liberada(s) · 1 tirada(s) à mão");
        ajustado.TemAjuste.Should().BeTrue();
        ajustado.UltimoAcesso.Should().Be("03/10/2026 14:05");
        ajustado.PodeExcluir.Should().BeFalse("quem já entrou se desativa, para a auditoria continuar dizendo quem era quem");
    }

    [Fact]
    public void Resumo_conta_quem_pode_conceder_permissao_inclusive_a_si_mesmo()
    {
        var usuarios = new[]
        {
            Usuario(PerfilAcesso.Gerente),
            Usuario(PerfilAcesso.Recepcao, extras: Permissao.GerenciarUsuarios),
            Usuario(PerfilAcesso.Gerente, ativo: false),
            Usuario(PerfilAcesso.Financeiro)
        };
        var r = AcessosNaTela.Resumo(usuarios);
        r.Ativos.Should().Be(3);
        r.Inativos.Should().Be(1);
        r.Gestores.Should().Be(2, "o inativo não entra, então não concede nada");
        r.ComAjuste.Should().Be(1);
    }

    [Fact]
    public void Psicologia_ignora_prescrever_checar_e_evolucao_de_enfermagem()
    {
        var bits = Permissao.Prescrever | Permissao.ChecarPrescricao | Permissao.RegistrarEvolucaoEnfermagem;
        AcessosNaTela.Ignoradas(PerfilAcesso.Psicologia, null).Should().Be(bits);
        AcessosNaTela.Ignoradas(PerfilAcesso.Profissional, "Psicologia").Should().Be(bits);
        AcessosNaTela.Ignoradas(PerfilAcesso.Profissional, "Acupuntura").Should().Be(Permissao.Nenhuma);
    }

    [Fact]
    public void Vinculo_inativo_atual_continua_no_seletor_para_o_salvar_nao_desfaze_lo()
    {
        var profissionais = new[]
        {
            new Profissional { Id = 1, Nome = "Dra. Ativa", Ativo = true },
            new Profissional { Id = 2, Nome = "Dr. Saiu", Ativo = false },
            new Profissional { Id = 3, Nome = "Dr. Outro inativo", Ativo = false }
        };
        var opcoes = AcessosNaTela.Vinculos(profissionais, vinculadoAtual: 2);
        opcoes[0].Id.Should().BeNull();
        opcoes.Select(o => o.Id).Should().Equal(null, 1, 2);
        opcoes.Single(o => o.Id == 2).Nome.Should().EndWith("(inativo)");
    }

    [Fact]
    public void Senha_e_criticada_pela_regra_do_dominio_e_pela_repeticao_sem_ecoar_a_senha()
    {
        AcessosNaTela.CriticarSenha("", "").Should().Be("Informe a senha.");
        AcessosNaTela.CriticarSenha("abc", "abc").Should().Contain(HashSenha.TamanhoMinimoSenha.ToString());
        var critica = AcessosNaTela.CriticarSenha("segredo-1", "segredo-2");
        critica.Should().Be("As duas senhas não são iguais. Digite de novo.");
        critica.Should().NotContain("segredo");
        AcessosNaTela.CriticarSenha("segredo-1", "segredo-1").Should().BeNull();
    }

    [Fact]
    public void Na_edicao_senha_em_branco_quer_dizer_nao_mexer()
    {
        AcessosNaTela.CriticarCadastro(novo: false, "Ana", "ana", "", "").Should().BeNull();
        AcessosNaTela.CriticarCadastro(novo: true, "Ana", "ana", "", "").Should().Be("Informe a senha.");
        AcessosNaTela.CriticarCadastro(novo: true, " ", "ana", "segredo1", "segredo1").Should().Be("Informe o nome do usuário.");
        AcessosNaTela.CriticarCadastro(novo: true, "Ana", "", "segredo1", "segredo1").Should().Be("Informe o login.");
        AcessosNaTela.CriticarCadastro(novo: false, "Ana", "ana", "segredo1", "outra123").Should().NotBeNull();
    }

    [Fact]
    public void Cpf_so_conta_como_mudado_quando_muda_de_fato()
    {
        AcessosNaTela.CpfMudou("123.456.789-09", "12345678909").Should().BeFalse();
        AcessosNaTela.CpfMudou(null, "  ").Should().BeFalse();
        AcessosNaTela.CpfMudou(null, "12345678909").Should().BeTrue();
    }

    [Fact]
    public void Bit_ignorado_conta_como_padrao_e_nao_vira_excecao_gravada()
    {
        var ign = AcessosNaTela.Ignoradas(PerfilAcesso.Profissional, "Psicologia");
        var padrao = PerfisAcesso.Padrao(PerfilAcesso.Profissional);
        // Efetivas já vem sem Prescrever (cortado na leitura): não pode virar "negada".
        var (extras, negadas) = AcessosNaTela.Delta(PerfilAcesso.Profissional, padrao & ~ign, ign);
        extras.Should().Be(Permissao.Nenhuma);
        negadas.Should().Be(Permissao.Nenhuma);
        AcessosNaTela.ResumoDasExcecoes(PerfilAcesso.Profissional, padrao & ~ign, ign)
            .Should().StartWith("Exatamente o padrão");
    }

    [Fact]
    public void Frase_da_recusa_tira_o_nome_do_parametro()
    {
        AcessosNaTela.Frase(new ArgumentException("O login precisa de pelo menos 3 caracteres.", "login"))
            .Should().Be("O login precisa de pelo menos 3 caracteres.");
        AcessosNaTela.Frase(new InvalidOperationException("Usuário não encontrado.")).Should().Be("Usuário não encontrado.");
    }

    [Fact]
    public void Ajuda_da_senha_nunca_fala_da_senha_so_da_regra()
    {
        AcessosNaTela.AjudaDaSenha(novo: true).Should().Be($"Mínimo de {HashSenha.TamanhoMinimoSenha} caracteres.");
        AcessosNaTela.AjudaDaSenha(novo: false).Should().Be("Deixe em branco para manter a senha atual.");
    }
}
