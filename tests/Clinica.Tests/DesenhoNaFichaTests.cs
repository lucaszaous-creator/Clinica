using System.Globalization;
using System.Text.RegularExpressions;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A FICHA DESENHADA (parcela 79) — o mapa corporal e a curva da dor no papel.
///
/// ⚠️ A asserção que carrega esta suíte é a última: <b>teste que prova que o arquivo FECHA
/// não prova que a folha MOSTRA</b> (a lição do carimbo invisível, parcela 68). Por isso
/// aqui se abre o PDF gerado e se contam os operadores de desenho dentro dos fluxos de
/// página — é a única forma de saber que a figura existe no arquivo, e não só que o
/// gerador não estourou.
/// </summary>
public class DesenhoNaFichaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ProntuarioService _prontuario;
    private readonly DocumentoClinicoService _documentos;
    private readonly DocumentosClinicosPdfService _pdfs;

    private static readonly DateOnly Dia = new(2026, 8, 12);

    public DesenhoNaFichaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _prontuario = new ProntuarioService(_repo);
        _documentos = new DocumentoClinicoService(_repo, _prontuario, new ConsentimentoService(_repo));
        _pdfs = new DocumentosClinicosPdfService(_repo);
    }

    private async Task<int> PacienteAsync()
    {
        var p = new Paciente { Nome = "Maria de Teste", Convenio = Convenio.UnimedIntercambio };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<Evolucao> SessaoComMapaAsync(
        int pacienteId, DateOnly data, int antes, int depois, params PontoMapa[] pontos)
    {
        var e = await _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            Data = data,
            EvaAntes = antes,
            EvaDepois = depois,
            QueixaPrincipal = "dor lombar",
            Conduta = "acupuntura sistêmica"
        }, "dra. ana");

        if (pontos.Length > 0)
        {
            _db.MapasCorporais.Add(new MapaCorporal { EvolucaoId = e.Id, Pontos = pontos.ToList() });
            await _db.SaveChangesAsync();
        }

        return e;
    }

    // ==================== A silhueta: UMA definição ====================

    [Fact]
    public void A_silhueta_do_SVG_sai_da_mesma_lista_que_a_tela_desenha()
    {
        var svg = SilhuetaCorporal.Svg([], coluna: false);

        // Doze peças, e o SVG traz as doze: se alguém acrescentar um braço à lista, ele
        // aparece nos dois lados — que é a razão de a lista existir.
        var formas = Regex.Matches(svg, "<(ellipse|rect) ").Count;
        formas.Should().Be(SilhuetaCorporal.Formas.Count);

        svg.Should().Contain($"viewBox=\"0 0 {SilhuetaCorporal.Largura} {SilhuetaCorporal.Altura}\"");
        svg.Should().NotContain("<line", "a coluna é o que diferencia a face de COSTAS");

        SilhuetaCorporal.Svg([], coluna: true).Should().Contain("<line");
    }

    [Fact]
    public void O_SVG_sai_com_ponto_decimal_mesmo_em_pt_BR()
    {
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            // A clínica roda em pt-BR. `cx="110,5"` é atributo INVÁLIDO de SVG: o círculo
            // some, ou a figura inteira não desenha — e nada avisa.
            CultureInfo.CurrentCulture = new CultureInfo("pt-BR");

            var svg = SilhuetaCorporal.Svg([new PontoDesenhado(0.425, 0.317, 1)], coluna: false);

            svg.Should().NotContain(",\"", "vírgula decimal dentro de atributo quebra o SVG");
            svg.Should().MatchRegex(@"circle cx=""\d+\.\d+""");
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    // ==================== O desenho gravado ====================

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en-US")]
    public void O_desenho_volta_igual_ao_que_foi_gravado(string cultura)
    {
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultura);

            var original = new DesenhoDaSessao(8, 3,
            [
                new PontoDaSessao(FaceCorpo.Frente, 0.4251, 0.3172, 1, TecnicaPonto.Agulha, "IG4"),
                new PontoDaSessao(FaceCorpo.Costas, 0.5, 0.62, 2, TecnicaPonto.Moxa, null),
                // O nome é digitado pelo profissional e pode trazer os separadores do
                // formato: sem escape, esta linha viraria dois pontos tortos.
                new PontoDaSessao(FaceCorpo.Frente, 0.1, 0.2, 3, TecnicaPonto.Ventosa, "B23, B25|VG4")
            ]);

            var lido = DesenhoDaSessao.Ler(original.Serializar());

            lido.EvaAntes.Should().Be(8);
            lido.EvaDepois.Should().Be(3);
            lido.PontosOuVazio.Should().BeEquivalentTo(original.PontosOuVazio);
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    [Fact]
    public void Ponto_estragado_descarta_SO_ele_e_nunca_a_folha()
    {
        // Uma ficha que se recusa a imprimir por causa de um campo mal gravado é pior do
        // que uma ficha com um ponto a menos: o paciente está esperando o papel.
        var lido = DesenhoDaSessao.Ler("eva=7>2;pontos=F,xx,0.3,1,Agulha,|C,0.5,0.6,2,Moxa,");

        lido.EvaAntes.Should().Be(7);
        lido.PontosOuVazio.Should().ContainSingle().Which.Numero.Should().Be(2);
    }

    [Fact]
    public void Tecnica_desconhecida_mantem_o_ponto_no_papel()
    {
        // Valor de enum que a versão instalada não conhece: o ato ACONTECEU, e sumir com a
        // marcação apagaria do papel algo que foi praticado.
        var lido = DesenhoDaSessao.Ler("pontos=F,0.5,0.5,1,TecnicaDoFuturo,IG4");

        var ponto = lido.PontosOuVazio.Should().ContainSingle().Subject;
        ponto.Tecnica.Should().Be(TecnicaPonto.Outra);
        ponto.Rotulo.Should().Be("IG4");
    }

    [Fact]
    public void A_legenda_numera_1_a_n_e_nao_pela_Ordem_crua()
    {
        // Depois de remover uma marcação a `Ordem` fica com buraco. "1, 2, 5" na figura faz
        // quem lê procurar os pontos 3 e 4 que não existem.
        var desenho = DesenhoDaSessao.De(
            new Evolucao { EvaAntes = 6, EvaDepois = 2 },
            new MapaCorporal
            {
                Pontos =
                [
                    new PontoMapa { Ordem = 1, X = 0.1, Y = 0.1, Face = FaceCorpo.Frente },
                    new PontoMapa { Ordem = 7, X = 0.2, Y = 0.2, Face = FaceCorpo.Frente },
                    new PontoMapa { Ordem = 9, X = 0.3, Y = 0.3, Face = FaceCorpo.Costas }
                ]
            });

        desenho.PontosOuVazio.Select(p => p.Numero).Should().Equal(1, 2, 3);
    }

    // ==================== A cópia na emissão ====================

    [Fact]
    public async Task A_emissao_COPIA_o_mapa_e_a_reimpressao_nao_muda_quando_a_sessao_muda()
    {
        var pacienteId = await PacienteAsync();
        var sessao = await SessaoComMapaAsync(pacienteId, Dia, 8, 3,
            new PontoMapa { Ordem = 1, X = 0.42, Y = 0.31, Face = FaceCorpo.Frente, Nome = "IG4" });

        var ficha = await _documentos.EmitirRelatorioEvolucaoAsync(pacienteId, inicio: Dia, fim: Dia);

        var gravado = DesenhoDaSessao.Ler(ficha.Itens.Single().Desenho);
        gravado.PontosOuVazio.Should().ContainSingle().Which.Rotulo.Should().Be("IG4");
        gravado.EvaAntes.Should().Be(8);

        // O mapa da sessão é corrigido DEPOIS da emissão — mais dois pontos.
        var mapa = await _repo.ObterMapaDaEvolucaoAsync(sessao.Id);
        mapa!.Pontos.Add(new PontoMapa { Ordem = 2, X = 0.5, Y = 0.5, Face = FaceCorpo.Costas });
        await _db.SaveChangesAsync();

        // A via já entregue continua com UM ponto: a segunda via tem de sair idêntica à
        // que o paciente levou (art. 3º da Lei 13.787/2018).
        var recarregado = await _documentos.ObterAsync(ficha.Id);
        DesenhoDaSessao.Ler(recarregado!.Itens.Single().Desenho)
            .PontosOuVazio.Should().ContainSingle();
    }

    [Fact]
    public async Task Sessao_sem_mapa_nao_grava_desenho_nenhum()
    {
        var pacienteId = await PacienteAsync();
        await SessaoComMapaAsync(pacienteId, Dia, 0, 0);

        var ficha = await _documentos.EmitirRelatorioEvolucaoAsync(pacienteId, inicio: Dia, fim: Dia);

        // EVA 0 → 0 é medida (o paciente não tinha dor), então o desenho existe pela EVA e
        // não tem pontos. O que não pode é inventar marcação.
        DesenhoDaSessao.Ler(ficha.Itens.Single().Desenho).PontosOuVazio.Should().BeEmpty();
    }

    // ==================== A curva ====================

    [Fact]
    public void Uma_sessao_so_NAO_desenha_curva()
    {
        var uma = new[] { new MedidaDaDor(Dia, 8, 3) };

        // Uma medida é linha de base, não evolução — a mesma regra da escala aplicada uma
        // vez só. Um ponto solto num eixo prometeria uma evolução que o registro não tem.
        GraficoDaDor.VaiDesenhar(uma).Should().BeFalse();
        GraficoDaDor.VaiDesenhar([.. uma, new MedidaDaDor(Dia.AddDays(7), 6, 2)]).Should().BeTrue();
    }

    [Fact]
    public void A_curva_ancora_o_eixo_em_0_e_10_e_nao_no_menor_valor_medido()
    {
        // Duas sessões quase iguais (8→7 e 8→7): num eixo ajustado aos dados isso viraria
        // um despencar visual. Ancorado em 0..10, as duas linhas ficam quase no mesmo Y.
        var svg = GraficoDaDor.Svg(
        [
            new MedidaDaDor(Dia, 8, 7),
            new MedidaDaDor(Dia.AddDays(7), 8, 7)
        ]);

        var ys = Regex.Matches(svg, @"<polyline points=""([^""]+)""")
            .Select(m => m.Groups[1].Value.Split(' ')
                .Select(p => double.Parse(p.Split(',')[1], CultureInfo.InvariantCulture)).ToList())
            .ToList();

        ys.Should().HaveCount(2, "uma linha para antes e outra para depois da sessão");
        foreach (var linha in ys)
            linha.Distinct().Should().ContainSingle("a dor não mudou entre as duas sessões");

        // E as duas linhas ficam PERTO uma da outra, porque 8 e 7 são perto em 0..10.
        Math.Abs(ys[0][0] - ys[1][0]).Should().BeLessThan(GraficoDaDor.Altura / 5);
    }

    // ==================== O que a folha MOSTRA ====================

    [Fact]
    public async Task O_mapa_e_a_curva_SAEM_no_PDF_e_nao_so_no_banco()
    {
        var pacienteId = await PacienteAsync();
        await SessaoComMapaAsync(pacienteId, Dia, 8, 4,
            new PontoMapa { Ordem = 1, X = 0.42, Y = 0.31, Face = FaceCorpo.Frente, Nome = "IG4" },
            new PontoMapa { Ordem = 2, X = 0.50, Y = 0.55, Face = FaceCorpo.Costas, Nome = "B23" });
        await SessaoComMapaAsync(pacienteId, Dia.AddDays(7), 6, 2,
            new PontoMapa { Ordem = 1, X = 0.30, Y = 0.40, Face = FaceCorpo.Frente });

        var comDesenho = await _documentos.EmitirRelatorioEvolucaoAsync(pacienteId);
        var pdfComDesenho = _pdfs.Gerar(await _documentos.ObterAsync(comDesenho.Id) ?? comDesenho);

        // A MESMA ficha sem o desenho gravado: é a folha que a clínica imprimia até aqui, e
        // é contra ela que a diferença se mede. Sem esta comparação o teste provaria só que
        // o gerador não estourou — e o carimbo invisível da parcela 68 também não estourava.
        var sem = await _documentos.ObterAsync(comDesenho.Id);
        foreach (var item in sem!.Itens) item.Desenho = null;
        var pdfSemDesenho = _pdfs.Gerar(sem);

        OperadoresDeDesenho(pdfComDesenho).Should().BeGreaterThan(
            OperadoresDeDesenho(pdfSemDesenho) + 100,
            "as duas silhuetas e a curva são centenas de traços; se a figura não chegar ao "
            + "fluxo da página, a folha sai com a tabela e mais nada");
    }

    /// <summary>
    /// Conta os operadores de caminho (<c>m</c>, <c>l</c>, <c>c</c>, <c>re</c>) dentro dos
    /// fluxos de conteúdo do PDF. É o que distingue uma folha que DESENHA de uma que só
    /// escreve — o texto vai como IDs de glifo e não se lê de volta.
    /// </summary>
    private static int OperadoresDeDesenho(byte[] pdf)
    {
        var total = 0;
        foreach (Match m in Regex.Matches(
                     System.Text.Encoding.Latin1.GetString(pdf),
                     "stream\r?\n(.*?)endstream", RegexOptions.Singleline))
        {
            var bruto = System.Text.Encoding.Latin1.GetBytes(m.Groups[1].Value);
            byte[] conteudo;
            try
            {
                using var entrada = new MemoryStream(bruto);
                using var zlib = new System.IO.Compression.ZLibStream(
                    entrada, System.IO.Compression.CompressionMode.Decompress);
                using var saida = new MemoryStream();
                zlib.CopyTo(saida);
                conteudo = saida.ToArray();
            }
            catch
            {
                conteudo = bruto;
            }

            total += Regex.Matches(
                System.Text.Encoding.Latin1.GetString(conteudo),
                @"(?<![A-Za-z])(re|c|l|m)(?![A-Za-z])").Count;
        }

        return total;
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
