using Clinica.Domain;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O rótulo de enum na tela (parcela 41).
///
/// O defeito que originou isto foi encontrado pelo CLIENTE, em produção: a tela de
/// documento clínico oferecia "PedidoExame" no seletor de tipo. O build passava, os
/// testes passavam — porque nenhum deles olhava a tela.
/// </summary>
public class RotulosEnumTests
{
    /// <summary>O caso que o cliente viu.</summary>
    [Fact]
    public void PedidoExame_NaoSaiComoIdentificador()
    {
        RotulosEnum.De(TipoDocumentoClinico.PedidoExame).Should().Be("Pedido de exame");
    }

    /// <summary>
    /// Os sete tipos de documento têm rótulo escrito à mão — nenhum cai no humanizador,
    /// que produziria "Relatorio evolucao" (sem acento, porque o identificador não tem).
    /// </summary>
    [Theory]
    [InlineData(TipoDocumentoClinico.Receita, "Receita")]
    [InlineData(TipoDocumentoClinico.Atestado, "Atestado")]
    [InlineData(TipoDocumentoClinico.Comparecimento, "Declaração de comparecimento")]
    [InlineData(TipoDocumentoClinico.PedidoExame, "Pedido de exame")]
    [InlineData(TipoDocumentoClinico.RelatorioEvolucao, "Relatório de evolução")]
    [InlineData(TipoDocumentoClinico.Consentimento, "Termo de consentimento")]
    [InlineData(TipoDocumentoClinico.Anamnese, "Anamnese")]
    public void TipoDocumento_UsaORotuladorQueJaExistia(TipoDocumentoClinico tipo, string esperado)
    {
        RotulosEnum.De(tipo).Should().Be(esperado);
    }

    [Theory]
    [InlineData(FormaPagamento.CartaoCredito, "Cartão de crédito")]
    [InlineData(FormaPagamento.CartaoDebito, "Cartão de débito")]
    [InlineData(FormaPagamento.Pix, "PIX")]
    [InlineData(FormaPagamento.Transferencia, "Transferência")]
    public void FormaPagamento_SaiComoAClinicaFala(FormaPagamento forma, string esperado)
    {
        RotulosEnum.De(forma).Should().Be(esperado);
    }

    /// <summary>
    /// O acento não vem de graça: o identificador é `Saida`, e sem rótulo a tela
    /// escreveria "Saida" — que é como o defeito se disfarça de coisa aceitável.
    /// </summary>
    [Fact]
    public void TipoLancamento_SaiComAcento()
    {
        RotulosEnum.De(TipoLancamento.Entrada).Should().Be("Entrada");
        RotulosEnum.De(TipoLancamento.Saida).Should().Be("Saída");
    }

    [Fact]
    public void BaseRepasse_NaoSaiEmPascalCase()
    {
        RotulosEnum.De(BaseRepasse.PercentualDaReceita).Should().Be("Percentual da receita");
        RotulosEnum.De(BaseRepasse.ValorPorAtendimento).Should().Be("Valor por atendimento");
    }

    [Fact]
    public void Acento_ApareceMesmoQuandoOIdentificadorNaoTem()
    {
        RotulosEnum.De(TipoMovimentoEstoque.Saida).Should().Be("Saída");
        RotulosEnum.De(TipoPacote.Sessoes).Should().Be("Pacote de sessões");
    }

    [Fact]
    public void Nulo_NaoQuebraNemEscreveNada()
    {
        RotulosEnum.De(null).Should().BeEmpty();
    }

    /// <summary>
    /// O conversor é usado em telas que nem sempre listam enum. Passar texto tem de
    /// devolver o próprio texto, senão um seletor de período viraria vazio.
    /// </summary>
    [Fact]
    public void ValorQueNaoEhEnum_VoltaComoEsta()
    {
        RotulosEnum.De("Últimos 90 dias").Should().Be("Últimos 90 dias");
        RotulosEnum.De(42).Should().Be("42");
    }

    // ============================================ o fallback

    /// <summary>
    /// Enum sem rótulo declarado não pode sair como identificador. O humanizador é a
    /// rede — pior que o rótulo escrito à mão, melhor que "PedidoExame".
    /// </summary>
    [Theory]
    [InlineData("PedidoExame", "Pedido exame")]
    [InlineData("RelatorioEvolucao", "Relatorio evolucao")]
    [InlineData("CreditoAVista", "Credito a vista")]
    [InlineData("Receita", "Receita")]
    public void Humanizar_QuebraOPascalCase(string identificador, string esperado)
    {
        RotulosEnum.Humanizar(identificador).Should().Be(esperado);
    }

    /// <summary>
    /// Sigla colada não se quebra letra a letra: "BSV" tem de continuar "BSV", e não
    /// virar "B S V". É o caso que mais aparece neste domínio.
    /// </summary>
    [Theory]
    [InlineData("BSV", "BSV")]
    [InlineData("BsvApenas", "Bsv apenas")]
    [InlineData("TISS", "TISS")]
    public void Humanizar_PreservaSigla(string identificador, string esperado)
    {
        RotulosEnum.Humanizar(identificador).Should().Be(esperado);
    }

    [Fact]
    public void Humanizar_TextoVazio_NaoQuebra()
    {
        RotulosEnum.Humanizar("").Should().BeEmpty();
        RotulosEnum.Humanizar("   ").Should().BeEmpty();
    }

    /// <summary>
    /// Varredura: NENHUM valor dos enums que a suíte mostra pode sair com duas palavras
    /// coladas em PascalCase. É o teste que pega o enum novo que alguém acrescentar.
    /// </summary>
    [Fact]
    public void NenhumEnumDaTela_SaiEmPascalCase()
    {
        var tipos = new[]
        {
            typeof(TipoDocumentoClinico), typeof(FormaPagamento), typeof(TipoLancamento),
            typeof(StatusLancamento), typeof(BaseRepasse), typeof(PeriodicidadeRecorrencia),
            typeof(TipoMovimentoEstoque), typeof(TipoPacote), typeof(TecnicaPonto),
            typeof(Sexo), typeof(TipoCodigo)
        };

        var colados = new List<string>();

        foreach (var tipo in tipos)
        foreach (var valor in Enum.GetValues(tipo))
        {
            var rotulo = RotulosEnum.De(valor);

            // "CartaoCredito" tem maiúscula depois de minúscula sem espaço no meio.
            for (var i = 1; i < rotulo.Length; i++)
            {
                if (char.IsUpper(rotulo[i]) && char.IsLower(rotulo[i - 1]))
                    colados.Add($"{tipo.Name}.{valor} → \"{rotulo}\"");
            }
        }

        colados.Should().BeEmpty("nenhum rótulo de tela pode sair em PascalCase");
    }
}
