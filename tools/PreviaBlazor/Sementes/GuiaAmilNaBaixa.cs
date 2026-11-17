// Dados FICTÍCIOS da prévia — nenhuma pessoa real, nenhuma carteira real, CPF gerado.
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// UMA GUIA DA AMIL PENDENTE (nov/2026), para fotografar a baixa da Amil — a guia SP/SADT com a
/// confirmação, a equipe e as horas — antes de o envio dela ser ligado. Só aparece conferida porque a
/// prévia registra <c>EnvioTissLigadoNaPrevia</c> (Program.cs); no app, a Amil continua desligada.
/// O atendimento tem horário na agenda e início e fim do consultório, e o profissional tem CPF.
/// </summary>
public sealed class SementeGuiaAmilNaBaixa : ISemente
{
    public int Ordem => 102;

    public void Semear(ClinicaDbContext db)
    {
        var dia = DateOnly.FromDateTime(DateTime.Today).AddDays(-2);
        var profissional = new Profissional
        {
            Nome = "Dr. Caio Prévia Lins", NomeCurto = "Dr. Caio", ConselhoProfissional = "06", NumeroConselho = "520001",
            UfConselho = "33", Cbos = "225105", Cpf = CpfFicticio("529982247"), Ativo = true
        };
        var paciente = new Paciente
        {
            Nome = "Lúcia Amaral Pinto", Convenio = Convenio.Amil, ConvenioCodigo = "Amil", Carteirinha = "0007770002",
            ValidadeCarteirinha = dia.AddDays(300), Telefone = "(21) 98222-0901", DataNascimento = new DateOnly(1975, 3, 8)
        };
        db.AddRange(profissional, paciente);
        db.SaveChanges();

        var atendimento = new Atendimento { PacienteId = paciente.Id, Data = dia, Modalidade = ModalidadeAtendimento.Consulta };
        db.Add(atendimento);
        db.SaveChanges();
        var marcado = dia.ToDateTime(new TimeOnly(10, 0));
        db.Add(new Agendamento
        {
            PacienteId = paciente.Id, ProfissionalId = profissional.Id, AtendimentoId = atendimento.Id, DataHora = marcado,
            DuracaoMinutos = 30, ModalidadePrevista = ModalidadeAtendimento.Consulta, ModalidadeCodigo = nameof(ModalidadeAtendimento.Consulta),
            Status = StatusAgendamento.Realizado, CriadoPor = "recepcao", CriadoEm = marcado.AddDays(-5),
            InicioAtendimentoEm = marcado.AddMinutes(6), FimAtendimentoEm = marcado.AddMinutes(34)
        });
        db.Add(new CodigoFaturamento
        {
            AtendimentoId = atendimento.Id, Tipo = TipoCodigo.Consulta, Ordem = OrdemCodigo.Primeiro, DataPrevistaFaturamento = dia
        });
        db.SaveChanges();
    }

    /// <summary>Um CPF com os dígitos verificadores certos a partir de nove dígitos inventados.</summary>
    private static string CpfFicticio(string nove)
    {
        int Dv(string n, int peso) { var s = n.Select((c, i) => (c - '0') * (peso - i)).Sum() % 11; return s < 2 ? 0 : 11 - s; }
        var d1 = Dv(nove, 10);
        return nove + d1 + Dv(nove + d1, 11);
    }
}
