using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que o cadastro do paciente e a escolha do convênio (Blazor, out/2026) DECIDEM — o mesmo que
/// o <c>CadastroPacienteViewModel</c> e o <c>EscolhaDeConvenioViewModel</c> do WPF decidiam,
/// agora onde o <c>dotnet test</c> alcança.
/// </summary>
public sealed class CadastroPacienteNaTelaTests
{
    private const string CpfValido = "111.444.777-35";

    private static readonly OpcaoDeConvenio Unimed =
        new("UnimedPadrao", "Unimed", Convenio.UnimedPadrao, "Gera guia para o faturamento.", true, false);

    private static readonly OpcaoDeConvenio Particular =
        new("Particular", "Particular", Convenio.Personalizado, "Sem guia: o paciente paga a sessão.", false, false);

    private static readonly IReadOnlyList<EntradaModalidade> Modalidades =
    [
        new("Acupuntura", "Acupuntura", ModalidadeAtendimento.AcupunturaSimples, true),
        new("AcupunturaComEletro", "Acupuntura com eletro", ModalidadeAtendimento.AcupunturaComEletro, true),
    ];

    private static DadosDoCadastro Valido() => new()
    {
        Nome = "  Maria da Silva ",
        Documento = CpfValido,
        Convenio = Unimed,
        Telefone = " (21) 99999-0000 ",
        Email = "Maria@Exemplo.com",
    };

    [Fact]
    public void Cadastro_novo_nasce_sem_convenio_e_com_eletro_como_modalidade_habitual()
    {
        var d = CadastroPacienteNaTela.Novo(Modalidades);
        d.Convenio.Should().BeNull("a pergunta 'convênio ou particular?' é de quem está com o paciente na frente");
        d.ModalidadePreferida!.Base.Should().Be(ModalidadeAtendimento.AcupunturaComEletro);
        CadastroPacienteNaTela.Titulo(null).Should().Be("Novo paciente");
        CadastroPacienteNaTela.Titulo(7).Should().Be("Editar paciente");
    }

    [Fact]
    public void Sem_convenio_a_explicacao_e_o_convite_que_cita_o_particular()
    {
        CadastroPacienteNaTela.ExplicacaoDoConvenio(null).Should().Contain("Particular");
        CadastroPacienteNaTela.ExplicacaoDoConvenio(Particular).Should().Be(Particular.Explicacao);
    }

    [Fact]
    public void As_recusas_seguem_a_ordem_do_formulario_WPF()
    {
        var d = Valido();
        d.Nome = " ";
        d.Convenio = null;
        d.Documento = "123";
        CadastroPacienteNaTela.Conferir(d).Mensagem.Should().Be("Informe o nome do paciente.");

        d.Nome = "Maria";
        CadastroPacienteNaTela.Conferir(d).Mensagem.Should().Contain("Escolha o convênio").And.Contain("Particular");

        d.Convenio = Unimed;
        var cpf = CadastroPacienteNaTela.Conferir(d);
        cpf.Valido.Should().BeFalse();
        cpf.ErroDocumento.Should().NotBeNull("o CPF é sinalizado no campo");
        cpf.Mensagem.Should().Contain("Confira os campos sinalizados");

        d.Documento = CpfValido;
        d.Email = "sem-arroba";
        CadastroPacienteNaTela.Conferir(d).Mensagem.Should().StartWith("E-mail inválido");

        d.Email = null;
        CadastroPacienteNaTela.Conferir(d).Valido.Should().BeTrue();
    }

    [Fact]
    public void CPF_e_endereco_sao_sinalizados_juntos()
    {
        var d = Valido();
        d.Documento = null;
        d.Endereco = new string('x', 301);
        var c = CadastroPacienteNaTela.Conferir(d);
        c.ErroDocumento.Should().NotBeNull();
        c.ErroEndereco.Should().NotBeNull();
    }

    [Fact]
    public void A_categoria_acompanha_o_convenio_ate_alguem_a_escolher_a_mao()
    {
        var d = Valido();
        d.PossuiApp = true;
        CadastroPacienteNaTela.SugerirCategoria(d);
        d.Categoria.Should().Be(CategoriaConvenio.Base(Convenio.UnimedPadrao, true));

        d.Categoria = Categoria.Vermelha;
        d.CategoriaManual = true;
        d.PossuiApp = false;
        CadastroPacienteNaTela.SugerirCategoria(d);
        d.Categoria.Should().Be(Categoria.Vermelha, "escolhida à mão, a categoria não volta a seguir o convênio");
    }

    [Fact]
    public void Aplicar_copia_campo_a_campo_limpando_e_normalizando()
    {
        var d = Valido();
        d.Origem = OrigemPaciente.Internet;
        d.IndicadoPor = "Fulana";
        d.ModalidadePreferida = Modalidades[0];
        var p = new Paciente { Categoria = Categoria.Amarela };

        CadastroPacienteNaTela.Aplicar(d, p);

        p.Nome.Should().Be("Maria da Silva");
        p.Telefone.Should().Be("(21) 99999-0000");
        p.Email.Should().Be(EnderecoDeEmail.Normalizar("Maria@Exemplo.com"));
        p.Convenio.Should().Be(Convenio.UnimedPadrao);
        p.ConvenioCodigo.Should().Be("UnimedPadrao");
        p.ModalidadePreferidaCodigo.Should().Be("Acupuntura");
        p.Observacoes.Should().BeNull("texto em branco vira nulo");
        p.IndicadoPor.Should().BeNull("quem indicou só vale para a origem Indicação");
        p.Categoria.Should().Be(Categoria.Amarela, "sem escolha manual quem decide a categoria é o serviço");

        d.Origem = OrigemPaciente.Indicacao;
        d.CategoriaManual = true;
        d.Categoria = Categoria.Verde;
        CadastroPacienteNaTela.Aplicar(d, p);
        p.IndicadoPor.Should().Be("Fulana");
        p.Categoria.Should().Be(Categoria.Verde);
    }

    [Fact]
    public void Da_ficha_o_codigo_vence_e_a_familia_e_o_caminho_de_baixo()
    {
        var convenios = new[] { Unimed, Particular };
        var antiga = new Paciente { Nome = "A", Convenio = Convenio.UnimedPadrao, ConvenioCodigo = null };
        CadastroPacienteNaTela.Da(antiga, convenios, Modalidades).Convenio.Should().Be(Unimed);

        var particular = new Paciente { Nome = "B", Convenio = Convenio.Personalizado, ConvenioCodigo = "Particular" };
        CadastroPacienteNaTela.Da(particular, convenios, Modalidades).Convenio.Should().Be(Particular);

        var semNada = new Paciente { Nome = "C", Convenio = Convenio.Amil, ConvenioCodigo = "Sumiu" };
        CadastroPacienteNaTela.Da(semNada, convenios, Modalidades).Convenio.Should().BeNull(
            "sem achar o convênio o Salvar cobra a escolha, em vez de marcar uma operadora qualquer");
    }

    [Fact]
    public void Categoria_diferente_da_sugerida_e_lida_como_escolha_manual()
    {
        var p = new Paciente { Nome = "A", Convenio = Convenio.UnimedPadrao, ConvenioCodigo = "UnimedPadrao", PossuiApp = false };
        p.Categoria = CategoriaConvenio.Base(p.Convenio, p.PossuiApp);
        CadastroPacienteNaTela.Da(p, [Unimed], Modalidades).CategoriaManual.Should().BeFalse();

        p.Categoria = p.Categoria == Categoria.Vermelha ? Categoria.Verde : Categoria.Vermelha;
        CadastroPacienteNaTela.Da(p, [Unimed], Modalidades).CategoriaManual.Should().BeTrue();
    }

    [Fact]
    public void A_escolha_do_convenio_nao_oferece_o_a_definir_e_poe_quem_gera_guia_primeiro()
    {
        var catalogo = new List<ConvenioCadastro>
        {
            ConvenioCadastro.ADefinir(),
            ConvenioCadastro.Particular(),
            new() { Codigo = "Amil", Nome = "Amil", Familia = Convenio.Amil, Ativo = true },
            new() { Codigo = "Velho", Nome = "Antigo", Familia = Convenio.Amil, Ativo = false },
        };

        var linhas = EscolhaDeConvenioNaTela.Linhas(catalogo);

        linhas.Select(l => l.Entrada.Codigo).Should().Equal("Amil", "Particular");
        linhas[0].GeraGuia.Should().BeTrue();
        linhas[1].Detalhe.Should().Contain("paga");
    }

    [Fact]
    public void O_vinculo_so_acende_com_escolha_fora_da_carga_e_com_o_bit()
    {
        var linha = new LinhaDeConvenio(new ConvenioCadastro { Codigo = "Amil" }, "Amil", "", true);
        EscolhaDeConvenioNaTela.PodeVincular(linha, false, false, true).Should().BeTrue();
        EscolhaDeConvenioNaTela.PodeVincular(null, false, false, true).Should().BeFalse();
        EscolhaDeConvenioNaTela.PodeVincular(linha, true, false, true).Should().BeFalse();
        EscolhaDeConvenioNaTela.PodeVincular(linha, false, true, true).Should().BeFalse();
        EscolhaDeConvenioNaTela.PodeVincular(linha, false, false, false).Should().BeFalse();
    }
}
