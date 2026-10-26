using Clinica.Atualizacao;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O laço da tela de atualização (clínica, 26/10/2026): a instalação falhava, o Update.exe
/// reabria o app antigo e ele mandava instalar a mesma versão de novo, para sempre.
/// </summary>
public class TentativasDeAtualizacaoTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "tentativas-" + Guid.NewGuid().ToString("N"));
    private string Arquivo => Path.Combine(_pasta, "atualizacao-Clinica.Gerente.json");
    private static readonly DateTime Agora = new(2026, 10, 26, 14, 0, 0, DateTimeKind.Utc);

    public void Dispose()
    {
        try { Directory.Delete(_pasta, true); } catch { }
    }

    [Fact]
    public void Depois_do_limite_a_mesma_versao_nao_e_reaplicada_e_o_motivo_vai_para_o_log()
    {
        var t = new TentativasDeAtualizacao(Arquivo);
        for (var i = 0; i < TentativasDeAtualizacao.Limite; i++)
        {
            Assert.Null(t.MotivoParaNaoAplicar("1.2.91", Agora.AddMinutes(i)));
            t.Registrar("1.2.91", Agora.AddMinutes(i));
        }

        // Nova instância = o app reaberto pelo Update.exe: a memória está no disco.
        var motivo = new TentativasDeAtualizacao(Arquivo).MotivoParaNaoAplicar("1.2.91", Agora.AddMinutes(5));
        Assert.NotNull(motivo);
        Assert.Contains("1.2.91", motivo);
        Assert.Contains("velopack", motivo);
    }

    [Fact]
    public void Versao_nova_publicada_comeca_do_zero()
    {
        var t = new TentativasDeAtualizacao(Arquivo);
        t.Registrar("1.2.91", Agora);
        t.Registrar("1.2.91", Agora);
        Assert.Null(t.MotivoParaNaoAplicar("1.2.92", Agora));
        t.Registrar("1.2.92", Agora);
        Assert.Null(t.MotivoParaNaoAplicar("1.2.92", Agora));
    }

    [Fact]
    public void Passada_a_janela_ganha_mais_uma_rodada()
    {
        var t = new TentativasDeAtualizacao(Arquivo);
        t.Registrar("1.2.91", Agora);
        t.Registrar("1.2.91", Agora);
        var depois = Agora + TentativasDeAtualizacao.Janela;
        Assert.Null(t.MotivoParaNaoAplicar("1.2.91", depois));
        t.Registrar("1.2.91", depois);
        Assert.Null(t.MotivoParaNaoAplicar("1.2.91", depois));
        t.Registrar("1.2.91", depois);
        Assert.NotNull(t.MotivoParaNaoAplicar("1.2.91", depois));
    }

    [Fact]
    public void Arquivo_corrompido_ou_pasta_invalida_nunca_impedem_a_abertura()
    {
        Directory.CreateDirectory(_pasta);
        File.WriteAllText(Arquivo, "{ isto não é json");
        var t = new TentativasDeAtualizacao(Arquivo);
        Assert.Null(t.MotivoParaNaoAplicar("1.2.91", Agora));
        t.Registrar("1.2.91", Agora); // regrava por cima, sem lançar
        Assert.Null(t.MotivoParaNaoAplicar("1.2.91", Agora));

        var invalida = new TentativasDeAtualizacao(Path.Combine(_pasta, "\0inválido", "x.json"));
        invalida.Registrar("1.2.91", Agora);
        Assert.Null(invalida.MotivoParaNaoAplicar("1.2.91", Agora));
    }
}
