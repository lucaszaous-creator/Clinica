using Bunit;
using Clinica.Application;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Componentes;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// "Às vezes não libera o botão azul" (vídeo da secretária, 26/11/2026). Duas causas na tela
/// Marcar horário: (1) a trava de consulta do paciente ANTERIOR ficava presa no botão — a box
/// nasce de novo a cada paciente e só avisava quando o estado DELA mudava, então "sem trava" do
/// paciente seguinte nunca chegava ao formulário; (2) o botão apagava ou virava diálogo sem dizer
/// o que faltava. Aqui: a ordem do que falta, a hora como a recepção digita, e a box avisando
/// já na primeira leitura.
/// </summary>
[Collection(ColecaoDiagnostico.Nome)]
public sealed class MarcarHorarioOQueFaltaTests : IClassFixture<BancoDasTelas>, IDisposable
{
    private readonly BancoDasTelas _banco;

    public MarcarHorarioOQueFaltaTests(BancoDasTelas banco)
    {
        _banco = banco;
        new SessaoUsuario().Entrar(banco.Gerente);
    }

    public void Dispose() => new SessaoUsuario().Entrar(new UsuarioSistema());

    private static PendenciaDoBalcao? Falta(bool forma = false, bool modalidade = true, bool especialidade = false,
        bool infertilidade = false, bool hora = true, bool profissional = false, bool preventivo = false, bool trava = false)
        => AtendimentoDoBalcaoNaTela.OQueFalta(true, forma, modalidade, especialidade, infertilidade, hora, profissional, preventivo, trava);

    [Fact]
    public void Nada_faltando_o_botao_grava()
        => Assert.Null(Falta());

    [Fact]
    public void O_botao_diz_a_primeira_pendencia_na_ordem_da_tela()
    {
        var tudo = Falta(forma: true, modalidade: false, especialidade: true, infertilidade: true, hora: false,
            profissional: true, preventivo: true, trava: true);
        Assert.Equal(OndeFalta.Forma, tudo!.Onde);
        Assert.Equal("Falta: diga se é pelo plano ou particular", tudo.Botao);
        Assert.Equal(FormaDeAtendimentoNaTela.FaltaResponder, tudo.Mensagem);

        Assert.Equal(OndeFalta.Modalidade, Falta(modalidade: false, hora: false)!.Onde);
        Assert.Equal(OndeFalta.Especialidade, Falta(especialidade: true, hora: false)!.Onde);
        Assert.Equal(OndeFalta.Infertilidade, Falta(infertilidade: true, hora: false)!.Onde);
        Assert.Equal(OndeFalta.Hora, Falta(hora: false, profissional: true)!.Onde);
        Assert.Equal(OndeFalta.Profissional, Falta(profissional: true, trava: true)!.Onde);
        Assert.Equal(OndeFalta.Preventivo, Falta(preventivo: true, trava: true)!.Onde);

        var trava = Falta(trava: true)!;
        Assert.Equal(OndeFalta.Trava, trava.Onde);
        Assert.Equal(TravaDeConsultaNaTela.SalvarTravado, trava.Mensagem);
    }

    [Fact]
    public void Todo_rotulo_de_pendencia_e_diferente_do_rotulo_de_gravar()
    {
        foreach (var p in new[]
                 {
                     Falta(forma: true), Falta(modalidade: false), Falta(especialidade: true), Falta(infertilidade: true),
                     Falta(hora: false), Falta(profissional: true), Falta(preventivo: true), Falta(trava: true)
                 })
        {
            Assert.NotNull(p);
            Assert.DoesNotContain("Marcar", p!.Botao);
            Assert.False(string.IsNullOrWhiteSpace(p.Mensagem));
        }
    }

    [Theory]
    [InlineData("14", "14:00")]
    [InlineData("14h", "14:00")]
    [InlineData("14h30", "14:30")]
    [InlineData("1430", "14:30")]
    [InlineData("930", "09:30")]
    [InlineData("9:5", "09:05")]
    [InlineData("14:00", "14:00")]
    [InlineData(" 8 ", "08:00")]
    public void A_hora_digitada_como_a_recepcao_digita_vira_HH_mm(string digitado, string esperado)
        => Assert.Equal(esperado, AtendimentoDoBalcaoNaTela.NormalizarHora(digitado));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("25")]
    [InlineData("14:75")]
    [InlineData("abc")]
    [InlineData("12345")]
    public void O_que_nao_e_hora_continua_faltando(string? digitado)
        => Assert.Null(AtendimentoDoBalcaoNaTela.NormalizarHora(digitado));

    /// <summary>
    /// A causa do "às vezes": a box de um paciente SEM trava precisa avisar "solto" já na primeira
    /// leitura. Antes ela só avisava quando o próprio estado mudava — e uma box nova começa "sem
    /// trava", então o botão travado pelo paciente anterior nunca acendia de novo.
    /// </summary>
    [Fact]
    public void A_box_da_trava_avisa_solto_ja_na_primeira_leitura()
    {
        var marca = Guid.NewGuid().ToString("N")[..10];
        Paciente paciente;
        using (var db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_banco.Conexao).Options))
        {
            paciente = new Paciente { Nome = $"Paciente Trava Solta {marca}", ConvenioCodigo = "Particular" };
            db.Add(paciente);
            db.SaveChanges();
        }

        using var ctx = new TestContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddClinica("Host=teste;Database=teste");
        foreach (var d in ctx.Services.Where(d =>
                     d.ServiceType == typeof(DbContextOptions<ClinicaDbContext>) ||
                     d.ServiceType == typeof(DbContextOptions) ||
                     d.ServiceType == typeof(ClinicaDbContext)).ToList())
            ctx.Services.Remove(d);
        ctx.Services.AddDbContext<ClinicaDbContext>(o => o.UseSqlite(_banco.Conexao));
        ctx.Services.AddTelasBlazor();
        ctx.Services.AddSingleton<IHostDaTela>(new HostMudo());

        var avisos = new List<bool>();
        ctx.RenderComponent<TravaDeConsultaBox>(p => p
            .Add(x => x.Paciente, paciente)
            .Add(x => x.Quando, DateTime.Today.AddDays(200).AddHours(10))
            .Add(x => x.Modalidade, ModalidadeAtendimento.Consulta)
            .Add(x => x.TravadoChanged, (bool t) => { lock (avisos) avisos.Add(t); }));

        var prazo = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < prazo && avisos.Count == 0) Thread.Sleep(25);
        lock (avisos)
        {
            Assert.NotEmpty(avisos);
            Assert.False(avisos[^1]);
        }
    }

    private sealed class HostMudo : IHostDaTela
    {
        public Task<string?> ExportarCsvAsync(IReadOnlyList<string> c, IReadOnlyList<IReadOnlyList<string>> l, string n) => Task.FromResult<string?>(null);
        public Task<string?> SalvarEAbrirAsync(byte[] c, string n, string f, string e) => Task.FromResult<string?>(null);
        public Task<ArquivoEscolhido?> EscolherArquivoAsync(string f, string t) => Task.FromResult<ArquivoEscolhido?>(null);
        public Task<bool> CopiarAsync(string texto) => Task.FromResult(true);
        public Task<bool> NavegarAsync(string chave) => Task.FromResult(false);
        public Task<bool> AbrirEnderecoAsync(string url) => Task.FromResult(false);
        public Task<bool> AbrirFichaDoPacienteAsync(int pacienteId) => Task.FromResult(false);
        public Task<string?> PerguntarOndeSalvarAsync(string n, string f, string e) => Task.FromResult<string?>(null);
        public Task<string?> PerguntarPastaAsync(string t) => Task.FromResult<string?>(null);
        public Task<string?> GravarArquivoAsync(string c, byte[] b) => Task.FromResult<string?>(null);
    }
}
