using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public class TesteCopiaPrescricaoEntrePacientesTests
{
    [Theory]
    [InlineData(SituacaoPrescricao.Assinada)]
    [InlineData(SituacaoPrescricao.Liberada)]
    [InlineData(SituacaoPrescricao.Encerrada)]
    public async Task Copia_exatamente_a_ultima_do_paciente_entre_84_prescricoes_mais_recentes_de_homonimos(
        SituacaoPrescricao situacao)
    {
        using var conexao = new SqliteConnection("DataSource=:memory:");
        conexao.Open();
        using var db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseSqlite(conexao).Options);
        db.Database.EnsureCreated();

        // Mesmo nome e mesma descrição: a seleção precisa usar o ID do paciente.
        var pacientes = Enumerable.Range(0, 86)
            .Select(_ => new Paciente { Nome = "Paciente sintético homônimo", Convenio = Convenio.UnimedIntercambio })
            .ToArray();
        db.AddRange(pacientes);
        await db.SaveChangesAsync();
        var alvo = pacientes[0];
        var dia = new DateOnly(2026, 10, 7);
        var sequencia = 0;

        PrescricaoInterna Criar(int pacienteId, string marcador, DateOnly data, TimeOnly hora,
            SituacaoPrescricao estado) => new()
        {
            Numero = $"PRE 2026/{++sequencia:0000}", CodigoVerificacao = $"CP84{sequencia:0000}",
            PacienteId = pacienteId, Data = data, Hora = hora, Situacao = estado,
            AssinadaEm = estado == SituacaoPrescricao.Assinada ? data.ToDateTime(hora) : null,
            LiberadaSemAssinaturaEm = estado == SituacaoPrescricao.Liberada ? data.ToDateTime(hora) : null,
            EncerradaEm = estado == SituacaoPrescricao.Encerrada ? data.ToDateTime(hora) : null,
            Indicacao = $"Indicação {marcador}", Observacoes = $"Orientação {marcador}",
            IndicacaoFormatada = TextoFormatado.Guardar([new($"Indicação {marcador}", true)]),
            ObservacoesFormatadas = TextoFormatado.Guardar([new($"Orientação {marcador}", true)]),
            Itens = [new()
            {
                Ordem = 1, GrupoInfusao = 1, Descricao = "Medicamento sintético comum",
                DescricaoFormatada = TextoFormatado.Guardar([new("Medicamento sintético comum", true)]),
                Dose = $"Dose {marcador}", Diluente = $"Diluente {marcador}", Volume = $"Volume {marcador}",
                Via = ViaAdministracao.Endovenosa, TempoInfusao = $"Tempo {marcador}",
                SeNecessario = true, Observacoes = $"Cuidado {marcador}",
                ObservacoesFormatadas = TextoFormatado.Guardar([new($"Cuidado {marcador}", true)]),
                HoraPrevista = new TimeOnly(14, 0)
            }]
        };

        // Data, hora e ID são exercitados separadamente no histórico do mesmo paciente.
        db.Add(Criar(alvo.Id, "dia anterior", dia.AddDays(-1), new(23, 59), situacao));
        db.Add(Criar(alvo.Id, "hora anterior", dia, new(8, 59), situacao));
        db.Add(Criar(alvo.Id, "mesmo horário ID anterior", dia, new(9, 0), situacao));
        await db.SaveChangesAsync();
        var correta = Criar(alvo.Id, "ALVO", dia, new(9, 0), situacao);
        correta.Itens.Add(new() { Ordem = 2, Descricao = "Item suspenso não deve voltar", SuspensoEm = dia.ToDateTime(new(10, 0)) });
        correta.Itens[0].Checagens.Add(new()
        {
            Situacao = SituacaoChecagem.Realizado, HoraRealizacao = new(14, 5),
            DataRealizacao = dia, ExecutanteNome = "Profissional sintético"
        });
        db.Add(correta);
        await db.SaveChangesAsync();

        for (var i = 1; i <= 84; i++)
            db.Add(Criar(pacientes[i].Id, $"OUTRO {i:00}", dia.AddDays(1), new TimeOnly(9, 0).AddMinutes(i), situacao));

        // Até os registros mais novos do próprio alvo devem respeitar os filtros de emissão.
        db.Add(Criar(alvo.Id, "rascunho", dia.AddDays(2), new(9, 0), SituacaoPrescricao.Rascunho));
        db.Add(Criar(alvo.Id, "cancelada", dia.AddDays(2), new(10, 0), SituacaoPrescricao.Cancelada));
        var canceladaPorData = Criar(alvo.Id, "data de cancelamento", dia.AddDays(2), new(11, 0), situacao);
        canceladaPorData.CanceladaEm = dia.AddDays(2).ToDateTime(new(11, 30));
        db.Add(canceladaPorData);
        var enfermagem = Criar(alvo.Id, "origem enfermagem", dia.AddDays(2), new(12, 0), situacao);
        enfermagem.OrigemEnfermagem = true;
        db.Add(enfermagem);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await db.PrescricoesInternas.CountAsync(p => p.PacienteId != alvo.Id && p.Data > correta.Data))
            .Should().Be(84, "o cenário precisa ter exatamente 84 prescrições concorrentes de outros pacientes");
        var totalAntes = await db.PrescricoesInternas.CountAsync();
        var repo = new ClinicaRepositorio(db);
        var servico = new PrescricaoInternaService(repo, new PrescricaoService(repo));
        var selecionada = await repo.UltimaPrescricaoEmitidaDoPacienteAsync(alvo.Id);
        selecionada.Should().NotBeNull();
        selecionada!.Id.Should().Be(correta.Id);
        selecionada.PacienteId.Should().Be(alvo.Id);
        selecionada.Situacao.Should().Be(situacao);

        var modelo = await servico.UltimaComoModeloAsync(alvo.Id);
        modelo.Should().NotBeNull();
        modelo!.Indicacao.Should().Be("Indicação ALVO");
        modelo.Observacoes.Should().Be("Orientação ALVO");
        modelo.IndicacaoFormatada.Should().Be(correta.IndicacaoFormatada);
        modelo.ObservacoesFormatadas.Should().Be(correta.ObservacoesFormatadas);
        var copiado = ModeloInfusao.Para(modelo.Itens.Should().ContainSingle().Subject);
        copiado.Should().BeEquivalentTo(new ItemPrescricaoInterna
        {
            GrupoInfusao = 1, Descricao = "Medicamento sintético comum",
            DescricaoFormatada = TextoFormatado.Guardar([new("Medicamento sintético comum", true)]),
            Dose = "Dose ALVO", Diluente = "Diluente ALVO", Volume = "Volume ALVO",
            Via = ViaAdministracao.Endovenosa, TempoInfusao = "Tempo ALVO", SeNecessario = true,
            Observacoes = "Cuidado ALVO",
            ObservacoesFormatadas = TextoFormatado.Guardar([new("Cuidado ALVO", true)])
        });
        copiado.Id.Should().Be(0);
        copiado.PrescricaoInternaId.Should().Be(0);
        copiado.HoraPrevista.Should().BeNull();
        copiado.Checagens.Should().BeEmpty();

        // A mesma instância não deve guardar a seleção ao alternar paciente.
        (await servico.UltimaComoModeloAsync(pacientes[84].Id))!.Itens.Single().Dose.Should().Be("Dose OUTRO 84");
        (await servico.UltimaComoModeloAsync(pacientes[85].Id)).Should().BeNull();
        (await servico.UltimaComoModeloAsync(alvo.Id))!.Itens.Single().Dose.Should().Be("Dose ALVO");
        (await db.PrescricoesInternas.CountAsync()).Should().Be(totalAntes, "copiar não deve salvar outra prescrição");
        (await db.PrescricoesInternas.Include(p => p.Itens).ThenInclude(i => i.Checagens)
            .SingleAsync(p => p.Id == correta.Id)).Itens.Single(i => i.Ordem == 1)
            .Checagens.Should().ContainSingle("a execução original deve permanecer intacta");
    }
}
