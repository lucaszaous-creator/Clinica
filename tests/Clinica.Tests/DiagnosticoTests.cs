using Clinica.Application;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A ponte de log das camadas que não enxergam a UI. O contrato que importa é
/// "nunca atrapalha quem chamou": ela é usada dentro de catch de degradação
/// deliberada, então uma falha dela não pode virar uma segunda exceção.
/// </summary>
public class DiagnosticoTests : IDisposable
{
    private readonly Action<string, Exception>? _sinkOriginal = Diagnostico.Sink;

    [Fact]
    public void Registrar_EntregaContextoEExcecaoAoSink()
    {
        (string Contexto, Exception Ex)? recebido = null;
        Diagnostico.Sink = (c, e) => recebido = (c, e);

        var erro = new InvalidOperationException("banco fora do ar");
        Diagnostico.Registrar("Parâmetros — teste", erro);

        recebido.Should().NotBeNull();
        recebido!.Value.Contexto.Should().Be("Parâmetros — teste");
        recebido.Value.Ex.Should().BeSameAs(erro);
    }

    [Fact]
    public void Registrar_SemSink_NaoLanca()
    {
        Diagnostico.Sink = null;

        var acao = () => Diagnostico.Registrar("sem destino", new Exception("x"));

        acao.Should().NotThrow();
    }

    [Fact]
    public void Registrar_SinkQueFalha_NaoPropagaOErro()
    {
        // Disco cheio na hora de escrever o log não pode derrubar quem estava
        // apenas degradando com elegância.
        Diagnostico.Sink = (_, _) => throw new IOException("disco cheio");

        var acao = () => Diagnostico.Registrar("sink quebrado", new Exception("x"));

        acao.Should().NotThrow();
    }

    public void Dispose() => Diagnostico.Sink = _sinkOriginal;
}
