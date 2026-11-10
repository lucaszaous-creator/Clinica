using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Ui.Blazor.Hospedagem;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// "PLANO OU PARTICULAR?" — a pergunta obrigatória de toda marcação (nov/2026, pedido da
/// direção: "mesmo o paciente sendo plano, ela tem que dizer"). O que estes testes fixam:
/// quando o Salvar pergunta, o que ele pergunta, e que desistir nunca escolhe por ninguém.
/// </summary>
public class PerguntaPlanoOuParticularTests : IDisposable
{
    // O catálogo é cache estático: cada teste monta o seu (o mesmo cuidado de ParticularPorHorarioTests).
    public PerguntaPlanoOuParticularTests() => CatalogoConvenios.Atualizar(
    [
        new EntradaConvenio(nameof(Convenio.Petrobras), "Petrobras", Convenio.Petrobras, true, GeraGuia: true),
        new EntradaConvenio(ConvenioCadastro.CodigoParticular, "Particular", Convenio.Personalizado, true,
            new ConfiguracaoRegraGenerica(), FormatoNumeroGuia.SemValidacao, GeraGuia: false)
    ]);

    public void Dispose() => CatalogoConvenios.Atualizar([]);

    private static Paciente ComPlano() => new()
    {
        Nome = "Maria Souza", Convenio = Convenio.Petrobras, ConvenioCodigo = nameof(Convenio.Petrobras), Sexo = Sexo.Feminino
    };

    private static Paciente Particular() => new()
    {
        Nome = "Rui Lima", Convenio = Convenio.Personalizado, ConvenioCodigo = ConvenioCadastro.CodigoParticular, Sexo = Sexo.Masculino
    };

    [Fact]
    public void Pergunta_quando_ha_plano_na_ficha_e_ninguem_respondeu()
    {
        FormaDeAtendimentoNaTela.PrecisaPerguntar(ComPlano(), respondida: false).Should().BeTrue();
        FormaDeAtendimentoNaTela.PrecisaPerguntar(ComPlano(), respondida: true).Should().BeFalse("quem clicou no seletor já respondeu");
    }

    [Fact]
    public void Ficha_particular_nao_tem_o_que_responder()
    {
        FormaDeAtendimentoNaTela.PrecisaPerguntar(Particular(), respondida: false).Should().BeFalse();
        FormaDeAtendimentoNaTela.PrecisaPerguntar(null, respondida: false).Should().BeFalse();
    }

    [Fact]
    public void A_pergunta_nomeia_o_paciente_e_o_plano_e_as_respostas_sao_duas()
    {
        var p = ComPlano();

        FormaDeAtendimentoNaTela.Pergunta(p).Should().Contain("Maria Souza").And.Contain(p.ConvenioNome).And.Contain("particular");
        var respostas = FormaDeAtendimentoNaTela.Respostas(p);
        respostas.Should().HaveCount(2);
        respostas[0].Should().Be($"Pelo {p.ConvenioNome}");
        respostas[FormaDeAtendimentoNaTela.RespostaParticular].Should().Be("Particular");
    }

    [Fact]
    public void Quando_quem_atende_nao_aceita_o_plano_a_pergunta_avisa_mas_nao_responde()
    {
        var p = ComPlano();
        var endocrino = new Profissional { Nome = "Juliana", NomeCurto = "Dra. Juliana", ConveniosNaoAtendidos = nameof(Convenio.Petrobras) };

        FormaDeAtendimentoNaTela.Pergunta(p, endocrino).Should().Contain("Dra. Juliana não atende pelo");
        // O aviso não é resposta: a pergunta continua de pé.
        FormaDeAtendimentoNaTela.SugereParticular(endocrino, p).Should().BeTrue();
        FormaDeAtendimentoNaTela.PrecisaPerguntar(p, respondida: false).Should().BeTrue();
    }

    [Fact]
    public async Task O_dialogo_devolve_a_opcao_clicada()
    {
        var dialogos = new Dialogos();
        var pergunta = dialogos.EscolherAsync("Plano ou particular?", "…", ["Pelo plano", "Particular"]);

        dialogos.Atual!.Tipo.Should().Be(TipoDialogo.Escolha);
        dialogos.Escolher(dialogos.Atual, 1);

        (await pergunta).Should().Be(1);
        dialogos.Atual.Should().BeNull();
    }

    [Fact]
    public async Task Cancelar_devolve_nulo_e_o_confirmar_generico_nao_escolhe_por_ninguem()
    {
        var dialogos = new Dialogos();
        var pergunta = dialogos.EscolherAsync("Plano ou particular?", "…", ["Pelo plano", "Particular"]);
        var aberto = dialogos.Atual!;

        // O "confirmar" dos outros diálogos não vale aqui: a pergunta continua no ar.
        dialogos.Responder(aberto, confirmou: true);
        dialogos.Atual.Should().BeSameAs(aberto);
        // Opção que não existe também não responde.
        dialogos.Escolher(aberto, 7);
        dialogos.Atual.Should().BeSameAs(aberto);

        dialogos.Responder(aberto, confirmou: false);
        (await pergunta).Should().BeNull();
    }
}
