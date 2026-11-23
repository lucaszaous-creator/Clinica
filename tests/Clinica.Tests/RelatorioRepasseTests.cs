using System.IO.Compression;
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
/// O relatório de repasse para os médicos (nov/2026): lê só FECHAMENTOS GRAVADOS, filtra no
/// banco, conta o estorno negativo, deixa o cancelado fora do total e exporta com barreira e
/// trilha. Os fechamentos são gravados direto (o cálculo deles é do FechamentoRepasseTests).
/// </summary>
public class RelatorioRepasseTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly RelatorioRepasseService _servico;
    private int _ana, _bruno;

    private static readonly DateOnly Set = new(2026, 9, 1);
    private static readonly DateOnly Out = new(2026, 10, 1);
    private static readonly DateOnly Nov = new(2026, 11, 1);

    public RelatorioRepasseTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _servico = new RelatorioRepasseService(_repo);
        Semear();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private static SessaoUsuario Sessao(Permissao permissoes)
        => SessaoUsuario.Fotografar(new UsuarioSistema
        {
            Id = 9, Login = "marina.fin", Nome = "Marina", Perfil = PerfilAcesso.Recepcao, Ativo = true,
            PermissoesExtras = permissoes, PermissoesNegadas = ~permissoes
        });

    private static ItemRepasse Item(string paciente, string convenio, decimal valor, int atendimentoId,
        string? referencia = null, bool estorno = false)
        => new()
        {
            Origem = convenio == "PARTICULAR" ? OrigemItemRepasse.Particular : OrigemItemRepasse.Convenio,
            Natureza = estorno ? NaturezaItemRepasse.Estorno : NaturezaItemRepasse.Credito,
            Chave = (estorno ? "E:G:" : "G:") + atendimentoId,
            AtendimentoId = atendimentoId, PacienteId = 1, PacienteNome = paciente,
            DataAtendimento = new DateOnly(2026, 8, 1).AddDays(atendimentoId % 28),
            ConvenioCodigo = convenio, ConvenioNome = convenio == "PARTICULAR" ? "Particular" : convenio,
            TipoGuia = TipoCodigo.Consulta, Descricao = "Consulta", NumeroGuia = $"G{atendimentoId}",
            ReferenciaPagamento = referencia, DataRecebimento = new DateOnly(2026, 9, 10),
            ValorRecebido = estorno ? -100m : 100m, RegraDescricao = $"R$ {Math.Abs(valor):0},00 por consulta", Valor = valor
        };

    private RepasseFechamento Fechamento(int profissionalId, string nome, DateOnly competencia, DateOnly? pagoEm,
        bool cancelado, params ItemRepasse[] itens)
    {
        var cancelamento = cancelado ? new DateTime(2026, 11, 2, 10, 0, 0) : (DateTime?)null;
        foreach (var i in itens) i.CanceladoEm = cancelamento;
        var f = new RepasseFechamento
        {
            ProfissionalId = profissionalId, ProfissionalNome = nome, ProfissionalRegistro = "CRM-RJ 1",
            Competencia = competencia, RecebidosAte = competencia.AddMonths(1).AddDays(-1),
            Quantidade = itens.Count(i => i.Natureza == NaturezaItemRepasse.Credito), Valor = itens.Sum(i => i.Valor),
            ConferidoPor = "gerente", ConferidoEm = competencia.AddMonths(1).ToDateTime(new TimeOnly(9, 0)),
            CanceladoEm = cancelamento, CanceladoPor = cancelado ? "gerente" : null,
            MotivoCancelamento = cancelado ? "regra errada" : null,
            Lancamento = new LancamentoFinanceiro
            {
                Data = competencia.AddMonths(1).AddDays(-1), Tipo = TipoLancamento.Saida, Descricao = $"Repasse {nome}",
                Valor = Math.Max(1m, itens.Sum(i => i.Valor)),
                Status = cancelado ? StatusLancamento.Cancelado : pagoEm is null ? StatusLancamento.Previsto : StatusLancamento.Realizado,
                DataPagamento = pagoEm
            },
            Itens = itens.ToList()
        };
        _db.RepassesFechamentos.Add(f);
        return f;
    }

    private void Semear()
    {
        var ana = new Profissional { Nome = "Dra. Ana Souza" };
        var bruno = new Profissional { Nome = "Dr. Bruno Lima" };
        _db.Profissionais.AddRange(ana, bruno);
        _db.SaveChanges();
        (_ana, _bruno) = (ana.Id, bruno.Id);

        Fechamento(_ana, "Dra. Ana Souza", Set, new DateOnly(2026, 10, 5), false,
            Item("José Ávila", "Amil", 65m, 1, "GRD/Conta 5950322456"),
            Item("Maria Clara", "Amil", 65m, 2, "GRD/Conta 5950322456"),
            Item("Carla Petro", "Petrobras", 80m, 3));
        Fechamento(_ana, "Dra. Ana Souza", Out, null, false,
            Item("Diego Sul", "SULAMERICA", 72m, 4, "Lote 14927685"),
            Item("Maria Clara", "Amil", -65m, 2, estorno: true));
        Fechamento(_ana, "Dra. Ana Souza", Out, null, true,
            Item("Paciente Cancelada", "Amil", 65m, 5));
        Fechamento(_ana, "Dra. Ana Souza", Nov, null, false,
            Item("Zé Novembro", "Amil", 65m, 6));
        Fechamento(_bruno, "Dr. Bruno Lima", Set, null, false,
            Item("Pedro Particular", "PARTICULAR", 100m, 7));
        _db.SaveChanges();
    }

    private static FiltroRelatorioRepasse SetAOut(int? profissional = null, string? convenio = null,
        SituacaoNoRelatorioRepasse situacao = SituacaoNoRelatorioRepasse.ConferidosEPagos, string? paciente = null)
        => new(Set, new DateOnly(2026, 10, 15), profissional, convenio, situacao, paciente);

    // ==================== Filtros ====================

    [Fact]
    public async Task Periodo_cobre_duas_competencias_exclui_a_terceira_e_soma_o_estorno_negativo()
    {
        var r = await _servico.GerarAsync(SetAOut());

        r.Linhas.Should().HaveCount(6);
        r.Linhas.Should().NotContain(l => l.Competencia == Nov || l.Cancelado);
        r.Quantidade.Should().Be(5);
        r.Total.Should().Be(65m + 65m + 80m + 72m - 65m + 100m);
        r.Pago.Should().Be(210m);
        r.APagar.Should().Be(107m);

        var ana = r.PorProfissional.Single(p => p.ProfissionalId == _ana);
        ana.Fechamentos.Should().Be(2);
        ana.Quantidade.Should().Be(4);
        ana.Total.Should().Be(217m);
        ana.Pago.Should().Be(210m);
        ana.APagar.Should().Be(7m);

        // Médico, depois competência, convênio, referência, paciente.
        r.Linhas.Select(l => l.Profissional).Should().BeInAscendingOrder(StringComparer.Ordinal);
        r.Linhas.First().Profissional.Should().Be("Dr. Bruno Lima");
        r.Linhas.Where(l => l.ProfissionalId == _ana).Select(l => l.Paciente).Take(3)
            .Should().Equal("José Ávila", "Maria Clara", "Carla Petro");

        var amil = r.Resumo.Single(x => x.ProfissionalId == _ana && x.Convenio == "Amil");
        amil.Quantidade.Should().Be(2);
        amil.ValorUnitario.Should().BeNull("o estorno no grupo torna o unitário enganoso");
        amil.Total.Should().Be(65m);
        r.Resumo.Single(x => x.Convenio == "Petrobras").ValorUnitario.Should().Be(80m);
        r.Clinica.Should().Be("Clínica");
    }

    [Fact]
    public async Task Filtro_de_medico_e_de_convenio()
    {
        (await _servico.GerarAsync(SetAOut(profissional: _bruno))).Linhas
            .Should().ContainSingle().Which.Convenio.Should().Be("Particular");

        var amil = await _servico.GerarAsync(SetAOut(convenio: "Amil"));
        amil.Linhas.Should().HaveCount(3).And.OnlyContain(l => l.ConvenioCodigo == "Amil");
        amil.Total.Should().Be(65m);
        (await _servico.GerarAsync(SetAOut(convenio: "PARTICULAR"))).Total.Should().Be(100m);
    }

    [Fact]
    public async Task Situacao_pagos_a_pagar_e_cancelados_marcados_fora_do_total()
    {
        var pagos = await _servico.GerarAsync(SetAOut(situacao: SituacaoNoRelatorioRepasse.SoPagos));
        pagos.Linhas.Should().HaveCount(3).And.OnlyContain(l => l.Situacao == "Pago" && l.PagoEm == new DateOnly(2026, 10, 5));
        pagos.APagar.Should().Be(0m);

        var aPagar = await _servico.GerarAsync(SetAOut(situacao: SituacaoNoRelatorioRepasse.SoAPagar));
        aPagar.Linhas.Should().HaveCount(3).And.OnlyContain(l => l.Situacao == "Conferido" && l.PagoEm == null);
        aPagar.Total.Should().Be(107m);
        aPagar.Pago.Should().Be(0m);

        var todos = await _servico.GerarAsync(SetAOut(situacao: SituacaoNoRelatorioRepasse.IncluirCancelados));
        todos.Linhas.Should().HaveCount(7);
        todos.Linhas.Single(l => l.Cancelado).Situacao.Should().Be("Cancelado");
        todos.Total.Should().Be(317m);
        todos.Quantidade.Should().Be(5);
        todos.PorProfissional.Single(p => p.ProfissionalId == _ana).Fechamentos.Should().Be(2);
    }

    [Fact]
    public async Task O_curinga_do_banco_nao_vaza_paciente_parecido()
    {
        // "jusa" casa o LIKE de "josé" (vogais viram curinga); a conferência exata o tira.
        (await _servico.GerarAsync(SetAOut(paciente: "jusa"))).Linhas.Should().BeEmpty();
        Clinica.Infrastructure.ClinicaRepositorio.PadraoDoPaciente("jose_a%").Should().Be("%j_s_\\__\\%%");
    }

    [Theory]
    [InlineData("jose avila")]
    [InlineData("ÁVILA")]
    [InlineData("  josé ")]
    public async Task Paciente_sem_o_acento_atrapalhar(string trecho)
    {
        var r = await _servico.GerarAsync(SetAOut(paciente: trecho));
        r.Linhas.Should().ContainSingle().Which.Paciente.Should().Be("José Ávila");
    }

    [Fact]
    public async Task Recusa_periodo_invertido_e_mais_de_24_competencias()
    {
        var invertido = () => _servico.GerarAsync(new FiltroRelatorioRepasse(Out, Set));
        await invertido.Should().ThrowAsync<InvalidOperationException>().WithMessage("*antes da inicial*");
        var longo = () => _servico.GerarAsync(new FiltroRelatorioRepasse(new DateOnly(2024, 10, 1), new DateOnly(2026, 10, 1)));
        await longo.Should().ThrowAsync<InvalidOperationException>().WithMessage("*24 competências*");
        (await _servico.GerarAsync(new FiltroRelatorioRepasse(new DateOnly(2024, 11, 1), new DateOnly(2026, 10, 1))))
            .Linhas.Should().NotBeEmpty();
    }

    // ==================== Exportar ====================

    [Fact]
    public async Task Exportar_recusa_sem_login_e_sem_permissao_antes_de_ler_e_sem_trilha()
    {
        // O filtro é inválido de propósito: se o serviço lesse antes da barreira, a recusa seria outra.
        var invalido = new FiltroRelatorioRepasse(Out, Set);
        foreach (var sessao in new[] { null, SessaoUsuario.SemLoginNoServidor, Sessao(Permissao.VerFichaPaciente) })
        {
            var pdf = () => _servico.ExportarPdfsAsync(invalido, sessao);
            await pdf.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage(RelatorioRepasseService.MensagemSemPermissao);
            var xlsx = () => _servico.ExportarPlanilhaAsync(invalido, sessao);
            await xlsx.Should().ThrowAsync<UnauthorizedAccessException>();
        }
        (await _db.Auditoria.CountAsync(a => a.Acao == RelatorioRepasseService.AcaoDaTrilha)).Should().Be(0);
    }

    [Fact]
    public async Task Um_pdf_por_medico_com_nome_sem_acento_e_uma_linha_de_trilha()
    {
        var arquivos = await _servico.ExportarPdfsAsync(
            SetAOut(situacao: SituacaoNoRelatorioRepasse.IncluirCancelados), Sessao(Permissao.VerFinanceiro));

        arquivos.Select(a => a.NomeArquivo).Should().BeEquivalentTo(
            "Relatorio_Repasse_Dr_Bruno_Lima_09-2026_a_10-2026.pdf",
            "Relatorio_Repasse_Dra_Ana_Souza_09-2026_a_10-2026.pdf");
        arquivos.Should().OnlyContain(a => System.Text.Encoding.ASCII.GetString(a.Conteudo, 0, 4) == "%PDF");

        var trilha = await _db.Auditoria.SingleAsync(a => a.Acao == RelatorioRepasseService.AcaoDaTrilha);
        trilha.Operador.Should().Be("marina.fin");
        trilha.Detalhe.Should().Contain("09/2026 a 10/2026").And.Contain("2 médico(s), 7 linha(s)").And.Contain("2 PDF(s)");

        var soAna = await _servico.ExportarPdfsAsync(
            new FiltroRelatorioRepasse(Nov, Nov, _ana), Sessao(Permissao.VerFinanceiro));
        soAna.Should().ContainSingle().Which.NomeArquivo.Should().Be("Relatorio_Repasse_Dra_Ana_Souza_11-2026.pdf");
        RelatorioRepasseService.Periodo(new FiltroRelatorioRepasse(Nov, Nov)).Should().Be("11/2026");
    }

    [Fact]
    public async Task Filtro_sem_linhas_recusa_a_exportacao()
    {
        var vazio = () => _servico.ExportarPdfsAsync(new FiltroRelatorioRepasse(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 1)),
            Sessao(Permissao.VerFinanceiro));
        await vazio.Should().ThrowAsync<InvalidOperationException>().WithMessage(RelatorioRepasseService.MensagemSemLinhas);
        (await _db.Auditoria.CountAsync(a => a.Acao == RelatorioRepasseService.AcaoDaTrilha)).Should().Be(0);
    }

    [Fact]
    public async Task Planilha_abre_como_xlsx_com_uma_linha_por_item_e_grava_a_trilha()
    {
        var arquivo = await _servico.ExportarPlanilhaAsync(SetAOut(), Sessao(Permissao.VerFinanceiro));

        arquivo.NomeArquivo.Should().Be("Relatorio_Repasse_09-2026_a_10-2026.xlsx");
        using var zip = new ZipArchive(new MemoryStream(arquivo.Conteudo));
        var folha = zip.GetEntry("xl/worksheets/sheet1.xml");
        folha.Should().NotBeNull();
        using var leitor = new StreamReader(folha!.Open());
        var xml = await leitor.ReadToEndAsync();
        xml.Should().Contain("Diego Sul").And.Contain("Referência do pagamento");
        RelatorioRepasseService.Colunas.Should().HaveCount(17);
        (await _db.Auditoria.SingleAsync(a => a.Acao == RelatorioRepasseService.AcaoDaTrilha)).Detalhe
            .Should().Contain("planilha").And.Contain("6 linha(s)");
    }
}
