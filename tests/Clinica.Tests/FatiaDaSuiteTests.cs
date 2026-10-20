using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A divisão da suíte em fatias do CI (<see cref="FatiaDaSuite"/>). Errar aqui não reprova
/// nada: o teste que caísse em duas fatias rodaria duas vezes, e o que caísse em nenhuma
/// simplesmente deixaria de rodar no PR — com o check verde.
/// </summary>
public sealed class FatiaDaSuiteTests
{
    private static readonly Dictionary<string, double> Exemplo = new()
    {
        ["A"] = 300, ["B"] = 240, ["C"] = 200, ["D"] = 120, ["E"] = 60, ["F"] = 30, ["G"] = 10, ["H"] = 1,
    };

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(6)]
    public void Toda_chave_medida_cai_em_UMA_fatia_valida(int total)
    {
        var mapa = FatiaDaSuite.Distribuir(Exemplo, total);
        Assert.Equal(Exemplo.Keys.Order(), mapa.Keys.Order());
        Assert.All(mapa.Values, f => Assert.InRange(f, 1, total));
    }

    [Fact]
    public void Divide_pelo_tempo_e_a_fatia_mais_pesada_fica_perto_da_media()
    {
        var mapa = FatiaDaSuite.Distribuir(Exemplo, 4);
        var cargas = Enumerable.Range(1, 4).Select(f => mapa.Where(m => m.Value == f).Sum(m => Exemplo[m.Key])).ToList();
        // LPT: a mais pesada não passa da média mais o maior item — aqui, 300 de 961 em 4.
        Assert.True(cargas.Max() <= Exemplo.Values.Sum() / 4 + Exemplo.Values.Max(), string.Join(" · ", cargas));
        Assert.Equal(300, cargas.Max());
    }

    [Fact]
    public void A_mesma_tabela_da_a_mesma_divisao_em_todo_job()
    {
        var invertida = Exemplo.Reverse().ToDictionary(k => k.Key, k => k.Value);
        Assert.Equal(FatiaDaSuite.Distribuir(Exemplo, 6), FatiaDaSuite.Distribuir(invertida, 6));
    }

    [Fact]
    public void A_tabela_embutida_foi_lida_e_teste_novo_cai_pelo_hash()
    {
        // As telas são a maior parte do tempo da suíte: sem a tabela, a divisão voltaria ao hash
        // em silêncio (o recurso não embutido não quebra nada — só desequilibra o CI).
        var chave = "Clinica.Tests.TelasBlazorAbremTests.Cada_botao_da_tela_e_dos_formularios_que_ele_abre_responde_sem_quebrar(nome: \"VisaoGeralDaAgenda\")";
        Assert.True(FatiaDaSuite.TemposMedidos.Count > 100, $"só {FatiaDaSuite.TemposMedidos.Count} chaves na tabela embutida");
        Assert.True(FatiaDaSuite.TemposMedidos.ContainsKey(chave), "a chave por caso não bate com a do FatiaDaSuite.ChaveDe");
        Assert.Equal(FatiaDaSuite.Distribuir(FatiaDaSuite.TemposMedidos, 6)[chave], FatiaDaSuite.FatiaDe(chave, 6));
        var nova = "Clinica.Tests.UmaClasseQueAindaNaoFoiMedida";
        Assert.Equal(FatiaDaSuite.FatiaPeloHash(nova, 6), FatiaDaSuite.FatiaDe(nova, 6));
    }
}
