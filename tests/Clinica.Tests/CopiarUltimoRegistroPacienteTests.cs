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

public class CopiarUltimoRegistroPacienteTests
{
    [Fact]
    public void Evolucao_escolhe_ultima_salva_do_paciente_sem_atual_cancelada_ou_futura()
    {
        var dia = new DateOnly(2026, 10, 7);
        var anteriores = new[]
        {
            new Evolucao { Id = 1, PacienteId = 8, Data = dia.AddDays(-2), TextoEvolucao = "antiga" },
            new Evolucao { Id = 2, PacienteId = 8, Data = dia.AddDays(-1), TextoEvolucao = "correta", Conduta = "conduta", EvaAntes = 9 },
            new Evolucao { Id = 3, PacienteId = 8, Data = dia, TextoEvolucao = "atual" },
            new Evolucao { Id = 4, PacienteId = 9, Data = dia, TextoEvolucao = "outro paciente" },
            new Evolucao { Id = 5, PacienteId = 8, Data = dia, CanceladaEm = DateTime.Now },
            new Evolucao { Id = 6, PacienteId = 8, Data = dia.AddDays(1) }
        };
        var copia = CopiaEvolucaoPaciente.Ultima(anteriores, 8, 3, dia)!;
        copia.TextoEvolucao.Should().Be("correta");
        copia.Conduta.Should().Be("conduta");
        anteriores[1].TextoEvolucao = "alteração posterior";
        copia.TextoEvolucao.Should().Be("correta");
        CopiaEvolucaoPaciente.Ultima(anteriores, 99, 0, dia).Should().BeNull();
    }

    [Fact]
    public void Evolucao_preserva_formatacao_e_copia_respostas_sem_reutilizar_valores_persistidos()
    {
        var formato = TextoFormatado.Guardar([new("Evolução", true)]);
        var e = new Evolucao { Id = 8, PacienteId = 4, Data = new(2026, 10, 6),
            TextoEvolucao = "Evolução", TextoEvolucaoFormatado = formato,
            CamposPersonalizados = [new() { Id = 90, CampoId = 17, Tipo = TipoCampoPersonalizado.TextoLongo,
                Rotulo = "Anotação", Valor = "Resposta anterior" }] };
        var copia = CopiaEvolucaoPaciente.Ultima([e], 4, 0, new(2026, 10, 7))!;
        copia.TextoEvolucaoFormatado.Should().Be(formato);
        copia.CamposPersonalizados[17].Should().Be("Resposta anterior");
        e.CamposPersonalizados[0].Valor = "Alteração posterior";
        copia.CamposPersonalizados[17].Should().Be("Resposta anterior");
    }

    [Fact]
    public void Evolucao_desempata_por_horario_e_identificador()
    {
        var dia = new DateOnly(2026, 10, 7);
        var hora = dia.ToDateTime(new TimeOnly(10, 0));
        var sessoes = new[]
        {
            new Evolucao { Id = 3, PacienteId = 8, Data = dia, CriadoEm = hora, TextoEvolucao = "última" },
            new Evolucao { Id = 2, PacienteId = 8, Data = dia, CriadoEm = hora, TextoEvolucao = "anterior" },
            new Evolucao { Id = 7, PacienteId = 8, Data = dia, CriadoEm = hora.AddHours(-1), TextoEvolucao = "mais cedo" }
        };
        CopiaEvolucaoPaciente.Ultima(sessoes, 8, 0, dia)!.TextoEvolucao.Should().Be("última");
    }

    [Theory]
    [InlineData(false, SituacaoPrescricao.Assinada)]
    [InlineData(true, SituacaoPrescricao.Assinada)]
    [InlineData(false, SituacaoPrescricao.Liberada)]
    [InlineData(true, SituacaoPrescricao.Liberada)]
    [InlineData(false, SituacaoPrescricao.Encerrada)]
    [InlineData(true, SituacaoPrescricao.Encerrada)]
    public async Task Infusao_le_ultima_emitida_sem_limite_do_historico_e_copia_sem_execucao(
        bool diluicaoUnica, SituacaoPrescricao situacaoDaUltima)
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        var paciente = new Paciente { Nome = "Paciente teste", Convenio = Convenio.UnimedIntercambio };
        var outro = new Paciente { Nome = "Outro paciente", Convenio = Convenio.UnimedIntercambio };
        db.AddRange(paciente, outro);
        await db.SaveChangesAsync();
        var dia = new DateOnly(2026, 10, 7);
        PrescricaoInterna Folha(int numero, int pacienteId, SituacaoPrescricao estado, bool externa = false) => new()
        {
            Numero = $"PRE 2026/{numero:0000}", CodigoVerificacao = $"TEST{numero:0000}",
            PacienteId = pacienteId, Data = dia, Hora = new TimeOnly(9, 0).AddMinutes(numero),
            Situacao = estado, OrigemEnfermagem = externa,
            DiluicaoUnica = diluicaoUnica, DiluenteGlobal = diluicaoUnica ? "Glicose 5%" : null,
            VolumeTotal = diluicaoUnica ? "250 mL" : null,
            // A continuidade atual permite uma folha emitida/encerrada sem assinatura eletrônica.
            AssinadaEm = estado is SituacaoPrescricao.Rascunho or SituacaoPrescricao.Liberada
                || numero == 2 && estado == SituacaoPrescricao.Encerrada
                ? null : dia.ToDateTime(new TimeOnly(9, 0)).AddMinutes(numero),
            LiberadaSemAssinaturaEm = estado == SituacaoPrescricao.Liberada
                ? dia.ToDateTime(new TimeOnly(9, 0)).AddMinutes(numero) : null,
            Indicacao = "Indicação", IndicacaoFormatada = "formato indicação", Observacoes = "Orientações",
            Itens = [new() { Ordem = 1, GrupoInfusao = diluicaoUnica ? null : 2,
                Descricao = $"Item {numero}", DescricaoFormatada = "formato item",
                Dose = "dose", Volume = "volume", HoraPrevista = new TimeOnly(10, 0), SeNecessario = true },
                new() { Ordem = 2, Descricao = "Suspenso", SuspensoEm = DateTime.Now }]
        };
        db.AddRange(Folha(1, paciente.Id, SituacaoPrescricao.Assinada),
            Folha(2, paciente.Id, situacaoDaUltima),
            Folha(3, outro.Id, SituacaoPrescricao.Assinada),
            Folha(4, paciente.Id, SituacaoPrescricao.Cancelada),
            Folha(5, paciente.Id, SituacaoPrescricao.Encerrada, externa: true));
        for (var i = 6; i < 66; i++) db.Add(Folha(i, paciente.Id, SituacaoPrescricao.Rascunho));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var repo = new ClinicaRepositorio(db);
        var servico = new PrescricaoInternaService(repo, new PrescricaoService(repo));
        var antes = await db.PrescricoesInternas.CountAsync();
        var modelo = (await servico.UltimaComoModeloAsync(paciente.Id))!;
        modelo.Itens.Should().ContainSingle().Which.Descricao.Should().Be("Item 2");
        modelo.IndicacaoFormatada.Should().Be("formato indicação");
        modelo.DiluicaoUnica.Should().Be(diluicaoUnica);
        modelo.DiluenteGlobal.Should().Be(diluicaoUnica ? "Glicose 5%" : null);
        modelo.VolumeTotal.Should().Be(diluicaoUnica ? "250 mL" : null);
        var item = ModeloInfusao.Para(modelo.Itens[0]);
        item.DescricaoFormatada.Should().Be("formato item");
        item.SeNecessario.Should().BeTrue();
        item.GrupoInfusao.Should().Be(diluicaoUnica ? null : 2);
        item.Id.Should().Be(0);
        item.PrescricaoInternaId.Should().Be(0);
        item.HoraPrevista.Should().BeNull();
        item.Checagens.Should().BeEmpty();
        item.Suspenso.Should().BeFalse();
        (await db.PrescricoesInternas.CountAsync()).Should().Be(antes);
        (await servico.UltimaComoModeloAsync(999)).Should().BeNull();
    }

    [Fact]
    public void Consulta_da_ultima_infusao_traduz_no_Postgres()
    {
        using var db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseNpgsql("Host=localhost;Database=nao_usado;Username=nao_usado").Options);
        new ClinicaRepositorio(db).ConsultaUltimaPrescricaoEmitida(8).ToQueryString()
            .Should().Contain("LIMIT").And.Contain("PacienteId").And.Contain("AssinadaEm");
    }
}
