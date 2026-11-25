using System.IO.Compression;
using System.Xml.Linq;
using Clinica.Application.Formatacao;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O RELATÓRIO DE ATENDIMENTOS (nov/2026): "quais pacientes são de BSV do mês e quais são os convênios
/// deles", com filtros no banco e a exportação .xlsx. Só dados fictícios.
/// </summary>
public sealed class RelatorioDeAtendimentosTests : IDisposable
{
    private static readonly DateOnly Inicio = new(2026, 11, 1);
    private static readonly DateOnly Fim = new(2026, 11, 30);

    private readonly SqliteConnection _conexao = new("Data Source=:memory:");
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private Profissional _ana = null!, _bruno = null!;

    public RelatorioDeAtendimentosTests()
    {
        _conexao.Open();
        _db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conexao).Options);
        _db.Database.EnsureCreated();
        _repo = new(_db);
    }

    public void Dispose() { _db.Dispose(); _conexao.Dispose(); }

    private RelatorioDeAtendimentosService Servico => new(_repo);

    private static SessaoUsuario Sessao(Permissao permissoes, PerfilAcesso perfil = PerfilAcesso.Recepcao)
        => SessaoUsuario.Fotografar(new UsuarioSistema
        {
            Id = 7, Login = "recepcao.teste", Nome = "Recepção Teste", Perfil = perfil, Ativo = true,
            PermissoesExtras = permissoes, PermissoesNegadas = ~permissoes
        });

    private async Task<Agendamento> HorarioAsync(string paciente, Convenio convenio, ModalidadeAtendimento modalidade,
        DateTime quando, StatusAgendamento status = StatusAgendamento.Realizado, Profissional? profissional = null,
        bool particular = false, string? carteirinha = null, Especialidade? especialidade = null, Atendimento? atendimento = null)
    {
        var p = new Paciente { Nome = paciente, Convenio = convenio, Carteirinha = carteirinha };
        var h = new Agendamento
        {
            Paciente = p, DataHora = quando, Status = status, ModalidadePrevista = modalidade,
            ProfissionalId = (profissional ?? _ana).Id, Particular = particular, EspecialidadeConsulta = especialidade,
            Atendimento = atendimento
        };
        _db.Agendamentos.Add(h);
        await _db.SaveChangesAsync();
        return h;
    }

    private async Task CenarioAsync()
    {
        _ana = new Profissional { Nome = "Dra. Ana Exemplo", NomeCurto = "Dra. Ana", Ativo = true };
        _bruno = new Profissional { Nome = "Dr. Bruno Exemplo", NomeCurto = "Dr. Bruno", Ativo = true };
        _db.Profissionais.AddRange(_ana, _bruno);
        await _db.SaveChangesAsync();

        var paciente = new Paciente { Nome = "Alice Fictícia", Convenio = Convenio.Amil, Carteirinha = "000111222" };
        var atendimento = new Atendimento
        {
            Paciente = paciente, Data = new DateOnly(2026, 11, 5), Modalidade = ModalidadeAtendimento.BsvApenas, Numero = "2026-000901",
            Codigos =
            [
                new CodigoFaturamento { Tipo = TipoCodigo.Bsv, Ordem = OrdemCodigo.Primeiro, Status = StatusCodigo.Baixado,
                    DataBaixa = new DateOnly(2026, 11, 10), NumeroGuiaReal = "G123", DataPrevistaFaturamento = new DateOnly(2026, 11, 5) }
            ]
        };
        _db.Agendamentos.Add(new Agendamento
        {
            Paciente = paciente, Atendimento = atendimento, DataHora = new DateTime(2026, 11, 5, 9, 30, 0), Status = StatusAgendamento.Realizado,
            ModalidadePrevista = ModalidadeAtendimento.BsvApenas, ProfissionalId = _ana.Id
        });
        await _db.SaveChangesAsync();

        await HorarioAsync("Bento Fictício", Convenio.UnimedPadrao, ModalidadeAtendimento.BsvApenas, new DateTime(2026, 11, 12, 14, 0, 0), StatusAgendamento.Faltou);
        await HorarioAsync("Clara Fictícia", Convenio.Petrobras, ModalidadeAtendimento.AcupunturaComEletro, new DateTime(2026, 11, 13, 10, 0, 0));
        await HorarioAsync("Davi Fictício", Convenio.Amil, ModalidadeAtendimento.BsvApenas, new DateTime(2026, 10, 30, 10, 0, 0)); // fora do mês
        await HorarioAsync("Elisa Fictícia", Convenio.Petrobras, ModalidadeAtendimento.BsvApenas, new DateTime(2026, 11, 20, 8, 0, 0),
            particular: true, profissional: _bruno);
        await HorarioAsync("Fábio Fictício", Convenio.Amil, ModalidadeAtendimento.Consulta, new DateTime(2026, 11, 21, 8, 0, 0),
            especialidade: Especialidade.Psiquiatria, profissional: _bruno);
        await HorarioAsync("Gabi Fictícia", Convenio.Amil, ModalidadeAtendimento.BsvApenas, new DateTime(2026, 11, 22, 8, 0, 0),
            StatusAgendamento.Substituido);
        _db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task BSV_do_mes_traz_so_BSV_com_o_convenio_de_cada_um()
    {
        await CenarioAsync();
        var r = await Servico.GerarAsync(new FiltroRelatorioAtendimentos(Inicio, Fim, Modalidade: nameof(ModalidadeAtendimento.BsvApenas)));

        Assert.Equal(["Alice Fictícia", "Bento Fictício", "Elisa Fictícia"], r.Linhas.Select(l => l.Paciente));
        Assert.All(r.Linhas, l => Assert.Equal(Clinica.Domain.Regras.CatalogoModalidades.Nome(null, ModalidadeAtendimento.BsvApenas), l.Modalidade));
        Assert.Equal("Amil", r.Linhas[0].Convenio);
        Assert.Contains("Unimed", r.Linhas[1].Convenio);
        Assert.Equal("Particular", r.Linhas[2].Convenio); // marcado como particular no horário
        Assert.Equal(["Realizado", "Falta", "Realizado"], r.Linhas.Select(l => l.SituacaoTexto));
        Assert.Equal(3, r.Atendimentos);
        Assert.Equal(3, r.PacientesDistintos);

        var alice = r.Linhas[0];
        Assert.Equal("000111222", alice.Carteirinha);
        Assert.Equal("2026-000901", alice.NumeroAtendimento);
        Assert.Contains("nº G123", alice.GuiasTexto);
        Assert.Equal("Baixada em 10/11/2026", alice.BaixaTexto);
    }

    [Fact]
    public async Task Filtros_de_situacao_convenio_profissional_especialidade_e_paciente_vao_ao_banco()
    {
        await CenarioAsync();
        Task<RelatorioDeAtendimentos> Gerar(FiltroRelatorioAtendimentos f) => Servico.GerarAsync(f);

        var realizados = await Gerar(new(Inicio, Fim, Situacoes: [StatusAgendamento.Realizado]));
        Assert.DoesNotContain(realizados.Linhas, l => l.Situacao != StatusAgendamento.Realizado);
        Assert.DoesNotContain(realizados.Linhas, l => l.Paciente == "Gabi Fictícia"); // substituído nunca entra

        Assert.Equal(["Alice Fictícia", "Fábio Fictício"], (await Gerar(new(Inicio, Fim, Convenio: nameof(Convenio.Amil)))).Linhas.Select(l => l.Paciente));
        Assert.Equal(["Elisa Fictícia"], (await Gerar(new(Inicio, Fim, Convenio: RelatorioDeAtendimentosRegras.Particular))).Linhas.Select(l => l.Paciente));
        // Elisa é Petrobras na ficha, mas foi atendida como particular: não entra no filtro Petrobras.
        Assert.Equal(["Clara Fictícia"], (await Gerar(new(Inicio, Fim, Convenio: nameof(Convenio.Petrobras)))).Linhas.Select(l => l.Paciente));
        Assert.Equal(["Elisa Fictícia", "Fábio Fictício"], (await Gerar(new(Inicio, Fim, ProfissionalId: _bruno.Id))).Linhas.Select(l => l.Paciente));
        var psiquiatria = await Gerar(new(Inicio, Fim, Especialidade: nameof(Especialidade.Psiquiatria)));
        Assert.Equal(["Fábio Fictício"], psiquiatria.Linhas.Select(l => l.Paciente));
        Assert.Equal(["Clara Fictícia"], (await Gerar(new(Inicio, Fim, Paciente: "CLARA"))).Linhas.Select(l => l.Paciente));
    }

    [Fact]
    public async Task Periodo_impossivel_e_recusado_antes_do_banco()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Servico.GerarAsync(new(Fim, Inicio)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Servico.GerarAsync(new(Inicio, Inicio.AddDays(RelatorioDeAtendimentosRegras.MaximoDias))));
        Assert.Equal((new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)), RelatorioDeAtendimentosRegras.EsteMes(new DateOnly(2026, 11, 18)));
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)), RelatorioDeAtendimentosRegras.MesPassado(new DateOnly(2026, 11, 18)));
    }

    [Fact]
    public async Task Planilha_tem_filtro_automatico_cabecalho_e_tipos_certos_e_grava_a_trilha()
    {
        await CenarioAsync();
        var filtro = new FiltroRelatorioAtendimentos(Inicio, Fim, Modalidade: nameof(ModalidadeAtendimento.BsvApenas));
        var arquivo = await Servico.ExportarAsync(filtro, Sessao(Permissao.VerFichaPaciente | Permissao.LancarAtendimento));

        Assert.Equal(3, arquivo.Linhas);
        Assert.Equal("atendimentos-2026-11-01-a-2026-11-30.xlsx", arquivo.NomeArquivo);

        using var zip = new ZipArchive(new MemoryStream(arquivo.Conteudo));
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var folha = XDocument.Load(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var linhas = folha.Descendants(ns + "row").ToList();
        Assert.Equal(4, linhas.Count); // cabeçalho + 3

        // O "filtro do Excel" sobre a tabela inteira, e o nome oculto que o Excel usa para ele.
        Assert.Equal("A1:M4", folha.Descendants(ns + "autoFilter").Single().Attribute("ref")!.Value);
        var livro = XDocument.Load(zip.GetEntry("xl/workbook.xml")!.Open());
        Assert.Contains(livro.Descendants(ns + "definedName"), d => d.Attribute("name")!.Value == "_xlnm._FilterDatabase");

        var cabecalho = linhas[0].Descendants(ns + "t").Select(t => t.Value).ToList();
        Assert.Equal(RelatorioDeAtendimentosRegras.Colunas, cabecalho);
        Assert.Contains("Convênio", cabecalho);
        Assert.Contains("Status da baixa", cabecalho);

        // Data como DATA (número com o estilo de data), não texto; quantidade como número.
        var primeira = linhas[1].Elements(ns + "c").ToList();
        Assert.Equal("A2", primeira[0].Attribute("r")!.Value);
        Assert.Null(primeira[0].Attribute("t"));
        Assert.Equal("2", primeira[0].Attribute("s")!.Value);
        Assert.Equal(new DateTime(2026, 11, 5).ToOADate(), double.Parse(primeira[0].Value, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("3", primeira[1].Attribute("s")!.Value); // hora
        Assert.Contains(primeira, c => c.Attribute("r")!.Value == "L2" && c.Attribute("t") is null && c.Value == "1");
        Assert.Contains("Amil", linhas[1].Value);

        // A trilha: quem, o filtro e quantas linhas.
        var trilha = await _db.Auditoria.AsNoTracking().SingleAsync(a => a.Acao == RelatorioDeAtendimentosService.AcaoDaTrilha);
        Assert.Equal("recepcao.teste", trilha.Operador);
        Assert.Contains("3 linha(s)", trilha.Detalhe);
        Assert.Contains("01/11/2026 a 30/11/2026", trilha.Detalhe);
        Assert.Contains("modalidade", trilha.Detalhe);
    }

    [Fact]
    public async Task Sem_permissao_nao_exporta_nem_grava_trilha()
    {
        await CenarioAsync();
        var filtro = new FiltroRelatorioAtendimentos(Inicio, Fim);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Servico.ExportarAsync(filtro, Sessao(Permissao.LancarAtendimento)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Servico.ExportarAsync(filtro, null));
        Assert.Empty(await _db.Auditoria.AsNoTracking().Where(a => a.Acao == RelatorioDeAtendimentosService.AcaoDaTrilha).ToListAsync());
    }

    [Fact]
    public void Planilha_nao_avalia_formula_e_escapa_xml()
    {
        var bytes = PlanilhaXlsx.Gerar("Teste", ["Nome"], [[CelulaXlsx.Texto("=HYPERLINK(\"x\") <&> \u0001")]]);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var celula = XDocument.Load(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()).Descendants(ns + "row").Last().Elements(ns + "c").Single();
        Assert.Equal("inlineStr", celula.Attribute("t")!.Value); // texto, nunca fórmula
        Assert.Empty(celula.Descendants(ns + "f"));
        Assert.Equal("=HYPERLINK(\"x\") <&> ", celula.Value);
        Assert.Equal("AA", PlanilhaXlsx.Coluna(27));
    }
}
