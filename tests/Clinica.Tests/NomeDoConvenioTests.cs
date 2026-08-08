using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using FluentAssertions;

namespace Clinica.Tests;

/// <summary>
/// O NOME do convênio na tela.
///
/// Dois defeitos que o cliente achou em produção, na mesma lista de pacientes, e que são
/// o mesmo erro visto de dois ângulos: a tela perguntou a FAMÍLIA quando queria a
/// OPERADORA.
///
/// · `{Binding Convenio}` num TextBlock faz o WPF chamar `ToString()` no enum e escrever
///   "UnimedIntercambio" no crachá do paciente.
/// · Resolver pela família — `Nome(p.Convenio)` — devolve "Personalizado" para TODA
///   operadora que a clínica cadastrou, porque `Convenio.Personalizado` é a regra que
///   todas elas compartilham. A clínica cadastra "Sul América" em Configurações, o nome
///   fica no banco, e a tela não o alcança.
///
/// Os testes ficam do lado do DOMÍNIO porque é lá que a resposta é decidida — a tela só
/// amarra `ConvenioNome`. Fixar isto aqui é o que impede a próxima tela de escrever
/// `Nome(p.Convenio)` outra vez e ninguém notar até a clínica ver o crachá.
/// </summary>
public class NomeDoConvenioTests : IDisposable
{
    public NomeDoConvenioTests() => CatalogoConvenios.Atualizar(
    [
        new EntradaConvenio("UnimedPadrao", "Unimed Costa do Sol", Convenio.UnimedPadrao, true),
        new EntradaConvenio("SulAmerica", "Sul América", Convenio.Personalizado, true),
    ]);

    // O catálogo é estático e compartilhado pelo processo de teste: deixá-lo sujo faria
    // outro teste responder pelo que este cadastrou.
    public void Dispose() => CatalogoConvenios.Atualizar([]);

    [Fact]
    public void Operadora_cadastrada_aparece_com_o_nome_dela_e_nao_como_Personalizado()
    {
        var paciente = new Paciente
        {
            Nome = "Maria",
            Convenio = Convenio.Personalizado,
            ConvenioCodigo = "SulAmerica"
        };

        paciente.ConvenioNome.Should().Be("Sul América");

        // O jeito errado, para deixar claro o que se está evitando: pela família, toda
        // operadora personalizada responde a mesma coisa.
        CatalogoConvenios.Nome(paciente.Convenio).Should().NotBe("Sul América");
    }

    [Fact]
    public void Convenio_embutido_usa_o_nome_do_catalogo_e_nunca_o_identificador_do_enum()
    {
        var paciente = new Paciente
        {
            Nome = "João",
            Convenio = Convenio.UnimedPadrao,
            ConvenioCodigo = null   // embutido: o código é o próprio nome do enum
        };

        paciente.ConvenioNome.Should().Be("Unimed Costa do Sol");
        paciente.ConvenioNome.Should().NotBe(nameof(Convenio.UnimedPadrao));
    }

    [Fact]
    public void Sem_codigo_cai_na_familia_em_vez_de_ficar_em_branco()
    {
        // Convênio fora do catálogo: o nome padrão da família é a melhor resposta
        // disponível, e é melhor que vazio — crachá em branco parece defeito.
        CatalogoConvenios.Nome(null, Convenio.Amil).Should().NotBeNullOrWhiteSpace();
        CatalogoConvenios.Nome("   ", Convenio.Amil)
            .Should().Be(CatalogoConvenios.Nome(null, Convenio.Amil));
    }

    [Fact]
    public void O_codigo_vence_a_familia_quando_os_dois_existem()
    {
        // O par que as entidades carregam. Se a família vencesse, a linha da operadora
        // cadastrada seria escrita com o nome da REGRA que ela usa.
        CatalogoConvenios.Nome("SulAmerica", Convenio.Personalizado).Should().Be("Sul América");
        CatalogoConvenios.Nome("SulAmerica", Convenio.UnimedPadrao).Should().Be("Sul América");
    }
}
