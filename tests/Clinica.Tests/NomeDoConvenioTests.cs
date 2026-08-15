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

/// <summary>
/// A LINHA "Personalizado" DO CATÁLOGO É RENOMEÁVEL, E ISSO VAZAVA PARA TODA OPERADORA.
///
/// O defeito que a clínica achou em produção: oito pacientes Sul América apareciam
/// faturando como "Porto Saúde" na consulta de guias — e a lista de pacientes, ao lado,
/// mostrava "SULAMERICA" para os mesmos oito.
///
/// A causa: <c>ConvenioCatalogoService.ListarAsync</c> garante uma linha por família,
/// inclusive uma de código <c>"Personalizado"</c>, e essa linha é editável como qualquer
/// outra. A clínica renomeou-a para a primeira operadora que cadastrou (em vez de usar
/// "Adicionar"), e a partir daí <c>Nome(Convenio.Personalizado)</c> — a resolução por
/// FAMÍLIA — passou a devolver "Porto Saúde" para toda operadora personalizada.
///
/// ⚠️ Por que <see cref="NomeDoConvenioTests"/> não pegou, apesar de ter sido escrito para
/// exatamente este assunto: ele monta um catálogo SEM linha de código "Personalizado" e
/// afirma que a resolução por família não devolve o nome da operadora CERTA. Passa — e
/// deixa passar o caso real, que é devolver o nome de uma operadora ERRADA. A asserção
/// tinha de ser "não é o nome de operadora nenhuma", e é a que está aqui.
/// </summary>
public class NomeDoConvenioPersonalizadoTests : IDisposable
{
    // O catálogo como a clínica o tem em produção: a linha embutida da família
    // renomeada para uma operadora, e outra operadora cadastrada ao lado, com código próprio.
    public NomeDoConvenioPersonalizadoTests() => CatalogoConvenios.Atualizar(
    [
        new EntradaConvenio("Personalizado", "Porto Saude", Convenio.Personalizado, true),
        new EntradaConvenio("CV1a2b3c4d", "SULAMERICA", Convenio.Personalizado, true),
    ]);

    public void Dispose() => CatalogoConvenios.Atualizar([]);

    [Fact]
    public void Duas_operadoras_da_mesma_familia_nunca_compartilham_o_nome()
    {
        var sulAmerica = new Paciente { Nome = "Janine", Convenio = Convenio.Personalizado, ConvenioCodigo = "CV1a2b3c4d" };
        var portoSaude = new Paciente { Nome = "Carlos", Convenio = Convenio.Personalizado, ConvenioCodigo = "Personalizado" };

        sulAmerica.ConvenioNome.Should().Be("SULAMERICA");
        portoSaude.ConvenioNome.Should().Be("Porto Saude");
        sulAmerica.ConvenioNome.Should().NotBe(portoSaude.ConvenioNome);
    }

    [Fact]
    public void A_familia_personalizada_nao_responde_o_nome_de_operadora_nenhuma()
    {
        // É esta a asserção que faltava. A família é a REGRA compartilhada — ela não sabe
        // de que operadora se trata, e responder uma delas é responder a errada para todas
        // as outras, com toda a cara de resposta certa.
        var porFamilia = CatalogoConvenios.Nome(Convenio.Personalizado);

        porFamilia.Should().Be(CatalogoConvenios.RotuloFamiliaPersonalizada);
        porFamilia.Should().NotBe("Porto Saude");
        porFamilia.Should().NotBe("SULAMERICA");
    }

    [Fact]
    public void Renomear_a_linha_embutida_de_uma_familia_de_verdade_continua_valendo()
    {
        // A blindagem é SÓ da família genérica. Renomear a Unimed continua refletindo na
        // tela: ali a família identifica uma operadora de verdade, e era para isso que a
        // resolução por família existia.
        CatalogoConvenios.Atualizar(
        [
            new EntradaConvenio("UnimedPadrao", "Unimed Costa do Sol (Padrão)", Convenio.UnimedPadrao, true),
        ]);

        CatalogoConvenios.Nome(Convenio.UnimedPadrao).Should().Be("Unimed Costa do Sol (Padrão)");
    }

    [Fact]
    public void Paciente_da_linha_embutida_renomeada_continua_com_o_nome_dela()
    {
        // O caminho de baixo (código nulo + família) NÃO passa pelo rótulo neutro: o
        // paciente foi mesmo cadastrado naquela linha, e ela é a melhor resposta que existe.
        // Sem isso, corrigir o vazamento apagaria o nome de quem estava certo.
        CatalogoConvenios.Nome(null, Convenio.Personalizado).Should().Be("Porto Saude");
    }
}
