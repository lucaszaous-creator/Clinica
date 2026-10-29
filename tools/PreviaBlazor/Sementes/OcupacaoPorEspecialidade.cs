// Dados FICTÍCIOS da prévia — nenhuma pessoa, CPF ou registro real.
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// A OCUPAÇÃO POR ESPECIALIDADE (out/2026): a Dra. Helena vira a ginecologista da prévia, com
/// o teto de 5 coletas de preventivo por dia, e ganha consultas com coleta nos próximos dias
/// em que atende — um dia LOTADO (5 de 5) e outro com 2, para o mapa mostrar o vermelho, o
/// verde e os "próximos dias com vaga".
/// </summary>
public sealed class SementeOcupacaoPorEspecialidade : ISemente
{
    // Depois da equipe (80), que dá à Dra. Helena o horário por dia.
    public int Ordem => 85;

    public void Semear(ClinicaDbContext db)
    {
        var helena = db.Profissionais.Local.FirstOrDefault(p => p.Nome == "Dra. Helena Duarte")
                     ?? db.Profissionais.FirstOrDefault(p => p.Nome == "Dra. Helena Duarte");
        if (helena is null) return;
        helena.EspecialidadeCodigo ??= Especialidade.Ginecologia.ToString();
        helena.LimitePreventivoPorDia = Profissional.LimitePreventivoPadrao;
        db.SaveChanges();

        // Os dois próximos dias (a partir de amanhã) em que ela atende.
        var dias = Enumerable.Range(1, 14).Select(n => DateTime.Today.AddDays(n))
            .Where(d => helena.AtendeEm(d.DayOfWeek) && d.DayOfWeek != DayOfWeek.Sunday)
            .Take(2).ToList();
        if (dias.Count < 2) return;

        string[] nomes = ["Mariana Lopes", "Ana Paula Ribeiro", "Juliana Freitas", "Cláudia Moraes", "Renata Souza", "Patrícia Gomes", "Luana Teixeira"];
        var n = 0;
        foreach (var (dia, quantas) in new[] { (dias[0], 5), (dias[1], 2) })
            for (var i = 0; i < quantas; i++)
            {
                var paciente = db.Add(new Paciente
                {
                    Nome = nomes[n++ % nomes.Length] + " (prévia)", Convenio = Convenio.UnimedPadrao, Sexo = Sexo.Feminino,
                    Telefone = $"(22) 99800-{n:0000}"
                }).Entity;
                db.Add(new Agendamento
                {
                    Paciente = paciente, ProfissionalId = helena.Id,
                    DataHora = dia.Date.AddHours(8).AddMinutes(40 * i),
                    ModalidadePrevista = ModalidadeAtendimento.Consulta,
                    ModalidadeCodigo = ModalidadeAtendimento.Consulta.ToString(),
                    EspecialidadeConsulta = Especialidade.Ginecologia,
                    EspecialidadeConsultaCodigo = Especialidade.Ginecologia.ToString(),
                    ColetaPreventivo = true,
                    Status = StatusAgendamento.Agendado,
                    CriadoPor = "recepcao", CriadoEm = DateTime.Now
                });
            }

        // HOJE: uma consulta com coleta já atendida, com as DUAS guias abertas — o fim do fluxo,
        // como aparece nas pendências do faturamento.
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var luana = db.Add(new Paciente
        {
            Nome = "Luana Teixeira (prévia)", Convenio = Convenio.UnimedPadrao, Sexo = Sexo.Feminino,
            Telefone = "(22) 99800-0099", Carteirinha = "0 865 000111222333 4"
        }).Entity;
        var atendimento = db.Add(new Atendimento
        {
            Paciente = luana, Data = hoje, Modalidade = ModalidadeAtendimento.Consulta,
            ModalidadeCodigo = ModalidadeAtendimento.Consulta.ToString(),
            EspecialidadeConsulta = Especialidade.Ginecologia,
            EspecialidadeConsultaCodigo = Especialidade.Ginecologia.ToString(),
            ColetaPreventivo = true, Categoria = Categoria.Amarela,
            LancadoPor = "recepcao", LancadoEm = DateTime.Now, RealizadoEm = DateTime.Now
        }).Entity;
        foreach (var (tipo, descricao) in new[]
                 {
                     (TipoCodigo.Consulta, "Consulta de Ginecologia."),
                     (TipoCodigo.Preventivo, "Coleta de preventivo (guia própria, junto da consulta).")
                 })
            atendimento.Codigos.Add(new CodigoFaturamento
            {
                Tipo = tipo, Especialidade = Especialidade.Ginecologia,
                EspecialidadeCodigo = Especialidade.Ginecologia.ToString(),
                Ordem = OrdemCodigo.Primeiro, DataPrevistaFaturamento = hoje,
                FormaObtencao = FormaObtencao.NaoAplica, Status = StatusCodigo.Aberto, Descricao = descricao
            });
        db.Add(new Agendamento
        {
            Paciente = luana, ProfissionalId = helena.Id, Atendimento = atendimento,
            DataHora = DateTime.Today.AddHours(9),
            ModalidadePrevista = ModalidadeAtendimento.Consulta,
            ModalidadeCodigo = ModalidadeAtendimento.Consulta.ToString(),
            EspecialidadeConsulta = Especialidade.Ginecologia,
            EspecialidadeConsultaCodigo = Especialidade.Ginecologia.ToString(),
            ColetaPreventivo = true, Status = StatusAgendamento.Realizado,
            CriadoPor = "recepcao", CriadoEm = DateTime.Now
        });
        db.SaveChanges();
    }
}
