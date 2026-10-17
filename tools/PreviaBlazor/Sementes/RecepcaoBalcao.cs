// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// O BALCÃO da Recepção (out/2026): o que as quatro telas migradas para Blazor precisam
/// desenhar —
/// - LANÇAMENTOS: sessões de hoje e de ontem lançadas por duas pessoas, uma com o 2º código
///   que só libera amanhã (a pendência), uma particular (sem guia) e uma sem registro de quem
///   lançou (a linha antiga);
/// - CONSULTAS DE CONVÊNIO: uma vencida, uma a vencer, uma em dia e uma que nunca emitiu;
/// - PAGAMENTOS: uma cobrança vencida, uma a receber e uma recebida, do mesmo paciente;
/// - VISÃO GERAL DA AGENDA: a semana com horários de dois profissionais (um com jornada
///   declarada, para os vãos livres aparecerem quando filtrado), um cancelado e uma falta.
/// </summary>
public sealed class SementeRecepcaoBalcao : ISemente
{
    public int Ordem => 80;

    public void Semear(ClinicaDbContext db)
    {
        var repo = new ClinicaRepositorio(db);
        new ConvenioCatalogoService(repo).RecarregarCacheAsync().GetAwaiter().GetResult();
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        Paciente P(string nome, Convenio familia, string? codigo, string telefone)
        {
            var p = db.Add(new Paciente
            {
                Nome = nome, Convenio = familia, ConvenioCodigo = codigo, Telefone = telefone,
                Carteirinha = "0 032 " + Math.Abs(nome.GetHashCode() % 1_000_000).ToString("000000"),
                ValidadeCarteirinha = hoje.AddDays(200), DataNascimento = new DateOnly(1981, 4, 9)
            }).Entity;
            db.SaveChanges();
            return p;
        }

        var otavio = P("Otávio Rezende Campos", Convenio.UnimedPadrao, "UnimedPadrao", "(21) 98111-2001");
        var silvia = P("Sílvia Monteiro Freitas", Convenio.UnimedIntercambio, "UnimedIntercambio", "(21) 98111-2002");
        var tereza = P("Tereza Cristina Bastos", Convenio.Amil, "Amil", "(21) 98111-2003");
        var vinicius = P("Vinícius Albuquerque", Convenio.Petrobras, "Petrobras", "(21) 98111-2004");
        var wagner = P("Wagner Nogueira Lopes", Convenio.UnimedPadrao, "UnimedPadrao", "(21) 98111-2005");

        // ---------- Lançamentos ----------
        var atendimentos = new AtendimentoService(repo);
        void Lancar(Paciente p, DateOnly data, ModalidadeAtendimento m, string? operador)
        {
            atendimentos.LancarAsync(p.Id, data, m, operador: operador).GetAwaiter().GetResult();
            db.ChangeTracker.Clear();
        }
        Lancar(otavio, hoje, ModalidadeAtendimento.AcupunturaComEletro, "Ana Lima");
        Lancar(silvia, hoje, ModalidadeAtendimento.AcupunturaSimples, "Bruno Castro");
        Lancar(tereza, hoje, ModalidadeAtendimento.Consulta, "Ana Lima");
        Lancar(vinicius, hoje.AddDays(-1), ModalidadeAtendimento.BsvComAcupuntura, null);

        // ---------- Consultas de convênio ----------
        var consultas = new ConsultaService(repo);
        consultas.RenovarAsync(otavio.Id, hoje.AddDays(-40)).GetAwaiter().GetResult(); // vencida (22 dias)
        consultas.RenovarAsync(silvia.Id, hoje.AddDays(-19)).GetAwaiter().GetResult(); // vence em breve
        consultas.RenovarAsync(tereza.Id, hoje.AddDays(-2)).GetAwaiter().GetResult();  // em dia
        db.ChangeTracker.Clear();
        // Wagner nunca emitiu.

        // ---------- Pagamentos ----------
        var particular = db.CategoriasFinanceiras.FirstOrDefault(c => c.Nome == "Particular");
        LancamentoFinanceiro Cobranca(string descricao, decimal valor, DateOnly data, DateOnly? vence, bool recebida) => new()
        {
            PacienteId = wagner.Id, Tipo = TipoLancamento.Entrada, Valor = valor, Descricao = descricao, Data = data,
            DataVencimento = vence, CategoriaFinanceiraId = particular?.Id,
            Status = recebida ? StatusLancamento.Realizado : StatusLancamento.Previsto,
            DataPagamento = recebida ? data : null, FormaPagamento = recebida ? FormaPagamento.Pix : null
        };
        db.AddRange(
            Cobranca("Sessão particular de acupuntura — parcela 1/2", 180m, hoje.AddDays(-20), hoje.AddDays(-10), false),
            Cobranca("Sessão particular de acupuntura — parcela 2/2", 180m, hoje.AddDays(-20), hoje.AddDays(10), false),
            Cobranca("Avaliação inicial", 250m, hoje.AddDays(-35), null, true));
        db.SaveChanges();

        // ---------- Visão geral da agenda ----------
        var helena = db.Profissionais.First(p => p.Nome == "Dra. Helena Duarte");
        var marcos = db.Profissionais.First(p => p.Nome == "Dr. Marcos Vieira");
        helena.DiasDeAtendimento = Profissional.BitDe(DayOfWeek.Monday) | Profissional.BitDe(DayOfWeek.Tuesday)
            | Profissional.BitDe(DayOfWeek.Wednesday) | Profissional.BitDe(DayOfWeek.Thursday) | Profissional.BitDe(DayOfWeek.Friday);
        helena.AtendeDas = new TimeOnly(8, 0);
        helena.AtendeAte = new TimeOnly(17, 0);
        db.SaveChanges();

        var segunda = hoje.AddDays(-(((int)hoje.DayOfWeek + 6) % 7));
        var pessoas = new[] { otavio, silvia, tereza, vinicius, wagner };
        var modalidades = new[] { ModalidadeAtendimento.AcupunturaSimples, ModalidadeAtendimento.AcupunturaComEletro,
            ModalidadeAtendimento.BsvApenas, ModalidadeAtendimento.Consulta, ModalidadeAtendimento.BsvComAcupuntura };
        var k = 0;
        for (var d = 0; d < 6; d++)
        {
            var dia = segunda.AddDays(d);
            foreach (var (hora, prof) in new[] { (9, helena), (10, marcos), (11, helena), (14, marcos), (15, helena) })
            {
                if (d == 5 && hora > 11) continue; // sábado só de manhã
                var status = (d, hora) switch
                {
                    (1, 14) => StatusAgendamento.Cancelado,
                    (2, 10) => StatusAgendamento.Faltou,
                    _ => StatusAgendamento.Agendado
                };
                db.Add(new Agendamento
                {
                    PacienteId = pessoas[k % pessoas.Length].Id, ProfissionalId = prof.Id,
                    DataHora = dia.ToDateTime(new TimeOnly(hora, 0)), DuracaoMinutos = 50,
                    ModalidadePrevista = modalidades[k % modalidades.Length], Status = status, CriadoPor = "previa"
                });
                k++;
            }
        }
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }
}
