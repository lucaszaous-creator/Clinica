using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Tests;

/// <summary>
/// A regra de "o que vira selo no cartão da fila" (set/2026, desenho B aprovado). Ela
/// decide o que o balcão LÊ de relance, então mora na Application e é fixada aqui —
/// uma ordem que mudasse de lugar por acidente trocaria o vermelho que impede pelo
/// âmbar que espera, sem quebrar build nenhum.
/// </summary>
public class SelosDaFilaTests
{
    private static IReadOnlyList<SeloFila> Montar(
        EtapaFila etapa = EtapaFila.Aguardando,
        bool termo = false, bool guia = false,
        int? usadas = null, int? contratadas = null,
        int? atraso = null, DateTime? encerradoEm = null)
        => SelosDaFila.Montar(etapa, termo, guia, usadas, contratadas, atraso, encerradoEm);

    [Fact]
    public void Sem_nada_a_dizer_nao_ha_selo()
        => Assert.Empty(Montar());

    [Fact]
    public void A_ordem_e_fixa_impede_cobra_estado()
    {
        var selos = Montar(termo: true, guia: true, atraso: 12);

        Assert.Equal(["Termo pendente", "Guia pendente", "Atrasado 12 min"],
            selos.Select(s => s.Texto).ToArray());
        Assert.Equal(TomDoSelo.Erro, selos[0].Tom);
        Assert.Equal(TomDoSelo.Erro, selos[1].Tom);
        Assert.Equal(TomDoSelo.Aviso, selos[2].Tom);
    }

    [Fact]
    public void Nunca_passa_de_tres_e_o_que_sai_e_o_de_menor_peso()
    {
        // Quatro candidatos: termo, guia, última do pacote e atraso. O atraso é o 3º
        // degrau e fica de fora — o que impede e o que cobra vêm antes do estado.
        var selos = Montar(termo: true, guia: true, usadas: 9, contratadas: 10, atraso: 5);

        Assert.Equal(SelosDaFila.Maximo, selos.Count);
        Assert.DoesNotContain(selos, s => s.Texto.StartsWith("Atrasado"));
        Assert.Equal("Última do pacote", selos[2].Texto);
    }

    [Theory]
    [InlineData(3, 10, null, null)]
    [InlineData(8, 10, "Penúltima do pacote", TomDoSelo.Aviso)]
    [InlineData(9, 10, "Última do pacote", TomDoSelo.Aviso)]
    [InlineData(10, 10, "Pacote esgotado", TomDoSelo.Erro)]
    [InlineData(11, 10, "Pacote esgotado", TomDoSelo.Erro)]
    public void O_pacote_so_vira_selo_no_fim(int usadas, int contratadas, string? texto, TomDoSelo? tom)
    {
        var selos = Montar(usadas: usadas, contratadas: contratadas);

        if (texto is null)
        {
            // "Pacote 3/10" é contexto, não aviso — vai para a linha do cartão.
            Assert.Empty(selos);
            return;
        }

        var selo = Assert.Single(selos);
        Assert.Equal(texto, selo.Texto);
        Assert.Equal(tom, selo.Tom);
        Assert.Contains($"{usadas}/{contratadas}", selo.Dica);
    }

    [Fact]
    public void Pacote_sem_contratadas_nao_conta()
        => Assert.Empty(Montar(usadas: 5, contratadas: null));

    [Theory]
    [InlineData(EtapaFila.Aguardando, true)]
    [InlineData(EtapaFila.Chegou, false)]
    [InlineData(EtapaFila.Chamado, false)]
    [InlineData(EtapaFila.EmAtendimento, false)]
    [InlineData(EtapaFila.Finalizado, false)]
    public void O_atraso_so_existe_em_AGUARDANDO(EtapaFila etapa, bool esperado)
    {
        var selos = Montar(etapa: etapa, atraso: 25);
        Assert.Equal(esperado, selos.Any(s => s.Texto == "Atrasado 25 min"));
    }

    [Theory]
    [InlineData(EtapaFila.EmAtendimento, true)]
    [InlineData(EtapaFila.Chamado, false)]
    [InlineData(EtapaFila.Finalizado, false)]
    public void O_encerramento_so_existe_em_EM_ATENDIMENTO(EtapaFila etapa, bool esperado)
    {
        var selos = Montar(etapa: etapa, encerradoEm: new DateTime(2026, 9, 4, 14, 32, 0));
        Assert.Equal(esperado, selos.Any(s => s.Texto == "Encerrado às 14:32"));
        if (esperado) Assert.Equal(TomDoSelo.Sucesso, selos.Single().Tom);
    }

    [Fact]
    public void Todo_selo_tem_dica_escrita()
    {
        var selos = Montar(etapa: EtapaFila.EmAtendimento, termo: true, guia: true,
            encerradoEm: new DateTime(2026, 9, 4, 14, 32, 0));

        Assert.All(selos, s => Assert.False(string.IsNullOrWhiteSpace(s.Dica)));
    }
}
