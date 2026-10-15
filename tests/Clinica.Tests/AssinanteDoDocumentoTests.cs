using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Quem a janela de documento propõe como assinante (out/2026 — o login da direção,
/// "Lucas", via o atestado proposto em nome de OUTRA médica, a primeira em ordem
/// alfabética da Equipe).
///
/// A asserção que carrega a suíte é a do caso real: login sem profissional ligado NÃO
/// recebe ninguém por padrão — e a frase diz por quê.
/// </summary>
public class AssinanteDoDocumentoTests
{
    private static readonly Profissional Beatriz = new() { Id = 7, Nome = "Beatriz Cristina de Cerqueira Moreira" };
    private static readonly Profissional Lucas = new() { Id = 12, Nome = "Lucas Souza" };
    private static readonly IReadOnlyList<Profissional> Ativos = [Beatriz, Lucas];

    [Fact]
    public void Login_ligado_a_um_profissional_ativo_assina_o_documento()
    {
        var escolha = AssinanteDoDocumento.Resolver(Ativos, profissionalDoLogin: 12);

        escolha.Profissional.Should().BeSameAs(Lucas);
        escolha.Aviso.Should().BeNull();
    }

    [Fact]
    public void Login_SEM_profissional_ligado_nao_recebe_o_primeiro_da_lista()
    {
        var escolha = AssinanteDoDocumento.Resolver(Ativos, profissionalDoLogin: null);

        escolha.Profissional.Should().BeNull("o primeiro em ordem alfabética é o nome de uma colega");
        escolha.Aviso.Should().Be(AssinanteDoDocumento.AvisoSemVinculo);
        escolha.Aviso.Should().Contain("seu usuário não está ligado a um profissional");
    }

    [Fact]
    public void Vinculo_que_aponta_para_cadastro_fora_dos_ativos_tambem_fica_em_branco_e_diz_por_que()
    {
        var escolha = AssinanteDoDocumento.Resolver(Ativos, profissionalDoLogin: 99);

        escolha.Profissional.Should().BeNull();
        escolha.Aviso.Should().Be(AssinanteDoDocumento.AvisoVinculoInativo);
    }

    [Fact]
    public void Identificador_zero_e_tratado_como_sem_vinculo()
    {
        AssinanteDoDocumento.Resolver(Ativos, 0).Aviso.Should().Be(AssinanteDoDocumento.AvisoSemVinculo);
    }

    [Theory]
    [InlineData(TipoDocumentoClinico.Receita, true)]
    [InlineData(TipoDocumentoClinico.Atestado, true)]
    [InlineData(TipoDocumentoClinico.LaudoMedico, true)]
    [InlineData(TipoDocumentoClinico.PedidoExame, true)]
    [InlineData(TipoDocumentoClinico.Comparecimento, false)]
    [InlineData(TipoDocumentoClinico.RelatorioEvolucao, false)]
    public void Motivo_do_Emitir_apagado_so_existe_quando_o_tipo_exige_assinante(
        TipoDocumentoClinico tipo, bool exige)
    {
        var motivo = AssinanteDoDocumento.MotivoSemAssinante(tipo, escolhido: null,
            AssinanteDoDocumento.AvisoSemVinculo);

        AssinanteDoDocumento.Exige(tipo).Should().Be(exige);
        if (exige) motivo.Should().Be(AssinanteDoDocumento.AvisoSemVinculo);
        else motivo.Should().BeNull();
    }

    [Fact]
    public void Escolher_alguem_apaga_o_motivo()
    {
        AssinanteDoDocumento.MotivoSemAssinante(TipoDocumentoClinico.Atestado, Beatriz,
            AssinanteDoDocumento.AvisoSemVinculo).Should().BeNull();
    }

    [Fact]
    public void Sem_aviso_da_abertura_o_motivo_nomeia_o_papel()
    {
        AssinanteDoDocumento.MotivoSemAssinante(TipoDocumentoClinico.Atestado, null, null)
            .Should().StartWith("Atestado precisa de quem assina");
    }
}
