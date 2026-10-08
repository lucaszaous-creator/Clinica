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

public class TesteCopiaEvolucaoEntrePacientesTests
{
    private static readonly DateOnly Dia = new(2026, 10, 7);

    [Fact]
    public async Task Copia_evolucao_exata_entre_84_outros_pacientes_homonimos_e_alterna_sem_vazar_conteudo()
    {
        using var conexao = new SqliteConnection("DataSource=:memory:");
        await conexao.OpenAsync();
        using var db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseSqlite(conexao).Options);
        await db.Database.EnsureCreatedAsync();

        // Nomes idênticos: somente a identidade persistida pode escolher o prontuário.
        var pacientes = Enumerable.Range(0, 86).Select(_ => new Paciente
        {
            Nome = "Paciente fictício homônimo", Convenio = Convenio.UnimedIntercambio
        }).ToArray();
        var campo = new CampoPersonalizadoProntuario
        {
            Rotulo = "Anotação da sessão", Tipo = TipoCampoPersonalizado.TextoLongo
        };
        db.AddRange(pacientes);
        db.Add(campo);
        await db.SaveChangesAsync();

        // O alvo é deliberadamente o mais antigo; os outros 84 são mais recentes.
        var origens = pacientes.Take(85).Select((p, i) => Registro(p.Id, i, campo.Id)).ToArray();
        db.AddRange(origens);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await db.Evolucoes.CountAsync()).Should().Be(85);
        (await db.Evolucoes.CountAsync(e => e.PacienteId != pacientes[0].Id))
            .Should().Be(84, "o cenário precisa conter 84 evoluções alheias ao alvo");
        var servico = new ProntuarioService(new ClinicaRepositorio(db));

        // Percorre os 85 pacientes em ordem inversa e volta ao alvo: um cache global
        // ou uma consulta sem PacienteId falharia apesar de todos terem o mesmo nome.
        foreach (var indice in Enumerable.Range(0, 85).Reverse().Append(0))
        {
            var sessoes = await servico.DoPacienteAsync(pacientes[indice].Id);
            sessoes.Should().ContainSingle().Which.PacienteId.Should().Be(pacientes[indice].Id);
            var copia = CopiaEvolucaoPaciente.Ultima(sessoes, pacientes[indice].Id, 0, Dia);
            ConferirConteudo(copia, indice, campo.Id);
        }

        var semHistorico = await servico.DoPacienteAsync(pacientes[85].Id);
        semHistorico.Should().BeEmpty();
        CopiaEvolucaoPaciente.Ultima(semHistorico, pacientes[85].Id, 0, Dia).Should().BeNull();
        (await db.Evolucoes.CountAsync()).Should().Be(85, "copiar não pode salvar uma evolução");
        (await db.ValoresCampoPersonalizado.CountAsync()).Should().Be(85);
        db.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public void Selecao_recusa_84_evolucoes_alheias_mesmo_quando_recebe_historico_misturado()
    {
        const int pacienteAlvo = 901;
        const int campoId = 71;
        var alvo = Registro(pacienteAlvo, 0, campoId);
        alvo.Id = 10;
        var alheias = Enumerable.Range(1, 84).Select(i => Registro(1000 + i, i, campoId)).ToList();
        var atual = Registro(pacienteAlvo, 90, campoId);
        atual.Id = 200;
        var cancelada = Registro(pacienteAlvo, 91, campoId);
        cancelada.Id = 201;
        cancelada.CanceladaEm = Dia.ToDateTime(new TimeOnly(12, 0));
        var futura = Registro(pacienteAlvo, 92, campoId);
        futura.Id = 202;
        futura.Data = Dia.AddDays(1);
        var historico = alheias.Concat([atual, cancelada, futura]).Prepend(alvo).ToList();

        var copia = CopiaEvolucaoPaciente.Ultima(historico, pacienteAlvo, atual.Id, Dia);
        ConferirConteudo(copia, 0, campoId);
        historico.Reverse();
        ConferirConteudo(CopiaEvolucaoPaciente.Ultima(historico, pacienteAlvo, atual.Id, Dia), 0, campoId);
        CopiaEvolucaoPaciente.Ultima(historico, 99999, 0, Dia).Should().BeNull();

        alvo.TextoEvolucao = "modificação posterior da origem";
        alvo.CamposPersonalizados[0].Valor = "modificação posterior da resposta";
        ConferirConteudo(copia, 0, campoId);
    }

    private static Evolucao Registro(int pacienteId, int indice, int campoId)
    {
        var marca = $"Paciente {indice:000}";
        var texto = $"{marca}: evolução exclusiva";
        return new Evolucao
        {
            PacienteId = pacienteId, Data = indice == 0 ? Dia.AddDays(-1) : Dia,
            CriadoEm = Dia.ToDateTime(new TimeOnly(8, 0)).AddMinutes(indice),
            QueixaPrincipal = $"{marca}: queixa", HistoriaDoencaAtual = $"{marca}: história",
            ExameFisico = $"{marca}: exame", HipoteseDiagnostica = $"{marca}: hipótese",
            CidSessao = $"CID-{indice:000}", Conduta = $"{marca}: conduta",
            TextoEvolucao = texto, TextoEvolucaoFormatado = TextoFormatado.Guardar([new(texto, true)]),
            Orientacoes = $"{marca}: orientações", PlanoTerapeutico = $"{marca}: plano",
            Encaminhamento = $"{marca}: encaminhamento", EvaAntes = indice % 11,
            CamposPersonalizados = [new()
            {
                CampoId = campoId, Rotulo = "Anotação da sessão", Tipo = TipoCampoPersonalizado.TextoLongo,
                Valor = $"{marca}: resposta personalizada exclusiva"
            }]
        };
    }

    private static void ConferirConteudo(CopiaEvolucaoPaciente? copia, int indice, int campoId)
    {
        var marca = $"Paciente {indice:000}";
        var texto = $"{marca}: evolução exclusiva";
        copia.Should().NotBeNull();
        copia!.DataOrigem.Should().Be(indice == 0 ? Dia.AddDays(-1) : Dia);
        copia.QueixaPrincipal.Should().Be($"{marca}: queixa");
        copia.HistoriaDoencaAtual.Should().Be($"{marca}: história");
        copia.ExameFisico.Should().Be($"{marca}: exame");
        copia.HipoteseDiagnostica.Should().Be($"{marca}: hipótese");
        copia.CidSessao.Should().Be($"CID-{indice:000}");
        copia.Conduta.Should().Be($"{marca}: conduta");
        copia.TextoEvolucao.Should().Be(texto);
        copia.TextoEvolucaoFormatado.Should().Be(TextoFormatado.Guardar([new(texto, true)]));
        copia.Orientacoes.Should().Be($"{marca}: orientações");
        copia.PlanoTerapeutico.Should().Be($"{marca}: plano");
        copia.Encaminhamento.Should().Be($"{marca}: encaminhamento");
        copia.CamposPersonalizados.Should().ContainSingle().Which.Key.Should().Be(campoId);
        copia.CamposPersonalizados[campoId].Should().Be($"{marca}: resposta personalizada exclusiva");
    }
}
