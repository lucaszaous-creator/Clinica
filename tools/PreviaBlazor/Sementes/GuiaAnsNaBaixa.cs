// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// O que falta para a prévia abrir a GUIA DE CONSULTA da baixa já preenchida (nov/2026): a
/// consulta pendente da paciente da Saúde Petrobras (semente do faturamento) ganha o HORÁRIO com
/// a profissional — é por ele que a guia chega ao executante, e é do cadastro dela que saem
/// conselho, número, UF e CBO —, e os cadastros ganham o registro ANS da operadora, o CNES e o
/// preço da consulta. Sem isto a prévia mostraria a guia com metade dos campos em branco, que é
/// o estado de uma clínica sem cadastro, e não o da tela em uso.
///
/// Roda DEPOIS da semente das configurações do faturamento (47), que grava os dados do
/// prestador por inteiro: aqui eles são lidos e completados, nunca substituídos.
/// </summary>
public sealed class SementeGuiaAnsNaBaixa : ISemente
{
    public int Ordem => 48;

    public void Semear(ClinicaDbContext db)
    {
        var repo = new ClinicaRepositorio(db);
        var parametros = new ParametrosService(repo);
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        // Registro ANS e CNES de mentira: seis e sete dígitos, como o padrão pede, e nada mais.
        var prestador = parametros.ObterPrestadorAsync().GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(prestador.RegistroAnsOperadora)) prestador.RegistroAnsOperadora = "000000";
        if (string.IsNullOrWhiteSpace(prestador.Cnes)) prestador.Cnes = "0000000";
        parametros.SalvarPrestadorAsync(prestador).GetAwaiter().GetResult();

        db.Add(new PrecoConvenio
        {
            ConvenioCodigo = Convenio.Petrobras.ToString(), Tipo = TipoCodigo.Consulta, Valor = 139m,
            CriadoPor = "Prévia", Observacoes = "Consulta — valor de exemplo"
        });

        // A consulta de HOJE da paciente Petrobras: o horário das 07:30 com a Dra. Helena, já
        // realizado e amarrado ao atendimento que gerou a guia. 07:30 fica antes de todo horário
        // que a semente da agenda do dia marca — não disputa sala nem profissional com nenhum.
        var helena = db.Set<Profissional>().FirstOrDefault(p => p.NomeCurto == "Dra. Helena");
        var atendimento = db.Set<Atendimento>()
            .Where(a => a.Data == hoje && a.Modalidade == ModalidadeAtendimento.Consulta && a.Paciente!.Convenio == Convenio.Petrobras)
            .OrderBy(a => a.Id)
            .FirstOrDefault();
        if (helena is not null && atendimento is not null)
        {
            var inicio = DateTime.Today.AddHours(7).AddMinutes(30);
            db.Add(new Agendamento
            {
                PacienteId = atendimento.PacienteId, ProfissionalId = helena.Id, DataHora = inicio,
                ModalidadePrevista = ModalidadeAtendimento.Consulta, Status = StatusAgendamento.Realizado,
                AtendimentoId = atendimento.Id, Origem = OrigemAgendamento.Manual,
                ChegadaEm = inicio.AddMinutes(-8), InicioAtendimentoEm = inicio.AddMinutes(2), FimAtendimentoEm = inicio.AddMinutes(29),
                CriadoPor = "recepcao", CriadoEm = inicio.AddDays(-2)
            });
        }

        db.SaveChanges();
        db.ChangeTracker.Clear();
    }
}
