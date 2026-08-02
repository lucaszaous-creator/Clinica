using Clinica.Domain;
using Clinica.Domain.Avaliacoes;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Os instrumentos de avaliação clínica (parcela 36) — o motor de pontuação.
///
/// É o equivalente, do lado clínico, dos testes de fluxograma de convênio: cada escala é
/// coberta de ponta a ponta, porque um peso errado num item produz um escore que
/// continua parecendo válido. Escore errado com cara de exato é o pior tipo de defeito
/// que um prontuário pode carregar.
/// </summary>
public class InstrumentosAvaliacaoTests
{
    private static Dictionary<string, int> Respostas(IInstrumentoAvaliacao instrumento, int valor)
        => instrumento.Itens.ToDictionary(i => i.Codigo, _ => valor);

    // ---------------------------------------------------------------- catálogo

    [Fact]
    public void Catalogo_resolve_instrumento_pelo_codigo()
    {
        RegistroInstrumentos.Obter("PHQ9").Should().BeOfType<InstrumentoPhq9>();
        RegistroInstrumentos.Obter("phq9").Should().BeOfType<InstrumentoPhq9>("o código não é sensível à caixa");
    }

    [Fact]
    public void Codigo_desconhecido_devolve_null_em_vez_de_explodir()
    {
        // Uma avaliação gravada guarda o CÓDIGO, e uma versão futura pode ter retirado a
        // escala do catálogo. O histórico continua legível porque tudo foi copiado.
        RegistroInstrumentos.Obter("ESCALA-QUE-NAO-EXISTE").Should().BeNull();
        RegistroInstrumentos.Obter(null).Should().BeNull();
    }

    [Theory]
    [InlineData(nameof(Especialidade.Psiquiatria), "PHQ9")]
    [InlineData(nameof(Especialidade.Psiquiatria), "GAD7")]
    [InlineData(nameof(Especialidade.Geriatria), "KATZ")]
    [InlineData(nameof(Especialidade.Endocrinologia), "FINDRISC")]
    [InlineData(InstrumentoOswestry.CodigoNeurocirurgia, "ODI")]
    [InlineData(nameof(Especialidade.Acupuntura), "ODI")]
    public void Cada_especialidade_da_clinica_recebe_o_instrumento_dela(string especialidade, string codigo)
        => RegistroInstrumentos.DaEspecialidade(especialidade)
            .Should().Contain(i => i.Codigo == codigo);

    [Fact]
    public void Especialidade_sem_instrumento_proprio_recebe_o_catalogo_inteiro()
    {
        // Lista vazia seria lida como "este sistema não tem avaliação nenhuma", e o
        // profissional sem especialidade cadastrada precisa poder trabalhar.
        RegistroInstrumentos.DaEspecialidade("EspecialidadeInventada")
            .Should().HaveCount(RegistroInstrumentos.Todos.Count);

        RegistroInstrumentos.DaEspecialidade(null)
            .Should().HaveCount(RegistroInstrumentos.Todos.Count);
    }

    [Fact]
    public void Nenhum_item_do_catalogo_tem_codigo_repetido()
    {
        // Código repetido faria uma resposta sobrescrever a outra no dicionário, e o
        // escore sairia menor sem nenhum erro aparecer.
        foreach (var instrumento in RegistroInstrumentos.Todos)
            instrumento.Itens.Select(i => i.Codigo).Should()
                .OnlyHaveUniqueItems($"os itens de {instrumento.Codigo} identificam a resposta");

        RegistroInstrumentos.Todos.Select(i => i.Codigo).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Toda_pontuacao_possivel_cai_em_alguma_faixa()
    {
        // Faixa faltando produziria uma avaliação gravada sem leitura nenhuma — o número
        // apareceria sozinho, e é a faixa que diz o que ele significa.
        foreach (var instrumento in RegistroInstrumentos.Todos)
            for (var p = 0; p <= instrumento.PontuacaoMaxima; p++)
                instrumento.FaixaDe(p).Should()
                    .NotBeNull($"{instrumento.Codigo} precisa interpretar o escore {p}");
    }

    [Fact]
    public void Faixas_nao_se_sobrepoem()
    {
        // Sobreposição faria a leitura depender da ORDEM da lista, que é detalhe de
        // implementação — a mesma pontuação teria duas interpretações legítimas.
        foreach (var instrumento in RegistroInstrumentos.Todos)
            for (var p = 0; p <= instrumento.PontuacaoMaxima; p++)
                instrumento.Faixas.Count(f => f.Contem(p)).Should()
                    .Be(1, $"{instrumento.Codigo} no escore {p}");
    }

    // ---------------------------------------------------------------- PHQ-9

    [Fact]
    public void Phq9_soma_os_nove_itens()
    {
        IInstrumentoAvaliacao phq9 = new InstrumentoPhq9();

        phq9.Itens.Should().HaveCount(9);
        phq9.PontuacaoMaxima.Should().Be(27);
        phq9.Pontuar(Respostas(phq9, 3)).Should().Be(27);
        phq9.Pontuar(Respostas(phq9, 0)).Should().Be(0);
    }

    [Theory]
    [InlineData(0, "Sintomas mínimos")]
    [InlineData(4, "Sintomas mínimos")]
    [InlineData(5, "Sintomas leves")]
    [InlineData(10, "Sintomas moderados")]
    [InlineData(15, "Sintomas moderadamente graves")]
    [InlineData(20, "Sintomas graves")]
    [InlineData(27, "Sintomas graves")]
    public void Phq9_classifica_nas_faixas_publicadas(int pontuacao, string faixa)
        => ((IInstrumentoAvaliacao)new InstrumentoPhq9()).FaixaDe(pontuacao)!.Nome.Should().Be(faixa);

    [Fact]
    public void Phq9_alerta_o_item_9_mesmo_com_escore_minimo()
    {
        // A regra que dá razão de existir ao AlertaDeItem: o item de ideação suicida vale
        // 3 dos 27 pontos, e um paciente pode marcá-lo respondendo o resto com zeros —
        // escore 3, faixa "sintomas mínimos", e a única resposta que exigia conduta
        // imediata desaparece dentro do total.
        IInstrumentoAvaliacao phq9 = new InstrumentoPhq9();
        var respostas = Respostas(phq9, 0);
        respostas[InstrumentoPhq9.ItemIdeacao] = 3;

        phq9.Pontuar(respostas).Should().Be(3);
        phq9.FaixaDe(3)!.Nome.Should().Be("Sintomas mínimos");
        phq9.AlertaDeItem(respostas).Should().NotBeNull();
    }

    [Fact]
    public void Phq9_nao_alerta_quando_o_item_9_e_zero()
    {
        IInstrumentoAvaliacao phq9 = new InstrumentoPhq9();
        var respostas = Respostas(phq9, 3);
        respostas[InstrumentoPhq9.ItemIdeacao] = 0;

        phq9.AlertaDeItem(respostas).Should().BeNull("o alerta é do item, não do total");
    }

    // ---------------------------------------------------------------- GAD-7

    [Fact]
    public void Gad7_soma_os_sete_itens_e_classifica()
    {
        IInstrumentoAvaliacao gad7 = new InstrumentoGad7();

        gad7.Itens.Should().HaveCount(7);
        gad7.PontuacaoMaxima.Should().Be(21);
        gad7.Pontuar(Respostas(gad7, 2)).Should().Be(14);
        gad7.FaixaDe(14)!.Nome.Should().Be("Ansiedade moderada");
        gad7.FaixaDe(15)!.Nome.Should().Be("Ansiedade grave");
    }

    // ---------------------------------------------------------------- Oswestry

    [Fact]
    public void Oswestry_devolve_porcentagem_e_nao_soma_bruta()
    {
        IInstrumentoAvaliacao odi = new InstrumentoOswestry();

        odi.Unidade.Should().Be("%");
        odi.PontuacaoMaxima.Should().Be(100);
        odi.Pontuar(Respostas(odi, 5)).Should().Be(100);
        odi.Pontuar(Respostas(odi, 0)).Should().Be(0);
        // Metade da escala em todas as seções = metade da incapacidade.
        odi.Pontuar(Respostas(odi, 2)).Should().Be(40, "2 de 5 em cada seção");
    }

    [Fact]
    public void Oswestry_calcula_sobre_as_secoes_respondidas()
    {
        // A regra publicada do ODI. Tratar a seção em branco como zero diria "não tenho
        // nenhuma limitação nisso", que é o oposto do que costuma acontecer.
        IInstrumentoAvaliacao odi = new InstrumentoOswestry();

        var todas = Respostas(odi, 5);
        var faltaUma = new Dictionary<string, int>(todas);
        faltaUma.Remove(odi.Itens[0].Codigo);

        odi.Pontuar(faltaUma).Should().Be(100, "8 de 8 seções no máximo continua sendo 100%");
        odi.AceitaItemOmitido.Should().BeTrue();
    }

    [Fact]
    public void Oswestry_sem_nenhuma_resposta_nao_divide_por_zero()
        => ((IInstrumentoAvaliacao)new InstrumentoOswestry()).Pontuar(new Dictionary<string, int>()).Should().Be(0);

    // ---------------------------------------------------------------- Katz

    [Fact]
    public void Katz_inverte_o_sentido_da_escala()
    {
        // Único instrumento em que SUBIR é melhorar. Sem isto, a curva mostraria a
        // recuperação funcional do paciente como piora.
        IInstrumentoAvaliacao katz = new InstrumentoKatz();

        katz.MelhorQuandoMenor.Should().BeFalse();
        katz.PontuacaoMaxima.Should().Be(6);
        katz.Pontuar(Respostas(katz, 1)).Should().Be(6);
        katz.FaixaDe(6)!.Gravidade.Should().Be(GravidadeFaixa.Normal);
        katz.FaixaDe(0)!.Gravidade.Should().Be(GravidadeFaixa.Alerta);
    }

    [Fact]
    public void Demais_instrumentos_melhoram_quando_o_escore_cai()
        => RegistroInstrumentos.Todos
            .Where(i => i.Codigo != InstrumentoKatz.CodigoInstrumento)
            .Should().OnlyContain(i => i.MelhorQuandoMenor);

    // ---------------------------------------------------------------- FINDRISC

    [Fact]
    public void Findrisc_soma_ate_26_e_classifica_o_risco()
    {
        IInstrumentoAvaliacao findrisc = new InstrumentoFindrisc();

        findrisc.Itens.Should().HaveCount(8);
        findrisc.PontuacaoMaxima.Should().Be(26);

        // O pior caso possível: a alternativa de maior peso em cada item.
        var pior = findrisc.Itens.ToDictionary(i => i.Codigo, i => i.ValorMaximo);
        findrisc.Pontuar(pior).Should().Be(26);
        findrisc.FaixaDe(26)!.Nome.Should().Be("Risco muito alto");

        var melhor = findrisc.Itens.ToDictionary(i => i.Codigo, i => i.Opcoes.Min(o => o.Valor));
        findrisc.Pontuar(melhor).Should().Be(0);
        findrisc.FaixaDe(0)!.Nome.Should().Be("Risco baixo");
    }

    [Fact]
    public void Findrisc_pesa_a_glicemia_alta_em_cinco_pontos()
    {
        // Não é erro de digitação: é o item de maior valor preditivo do instrumento.
        IInstrumentoAvaliacao findrisc = new InstrumentoFindrisc();
        var item = findrisc.Itens.Single(i => i.Codigo == "FDR_GLICEMIA");

        item.ValorMaximo.Should().Be(5);
    }

    // ---------------------------------------------------------------- itens

    [Fact]
    public void Rotulo_de_peso_inexistente_e_nulo()
    {
        // É o que permite ao serviço recusar um peso que o item não oferece, em vez de
        // gravar um escore que a escala não sabe interpretar.
        IInstrumentoAvaliacao phq9 = new InstrumentoPhq9();

        phq9.Itens[0].RotuloDe(3).Should().NotBeNull();
        phq9.Itens[0].RotuloDe(9).Should().BeNull();
    }

    [Fact]
    public void Todo_item_tem_ao_menos_duas_alternativas()
        => RegistroInstrumentos.Todos.SelectMany(i => i.Itens).Should()
            .OnlyContain(i => i.Opcoes.Count >= 2, "item com uma alternativa só não é pergunta");
}
