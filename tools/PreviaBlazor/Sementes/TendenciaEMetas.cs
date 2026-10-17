// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// O que as telas de TENDÊNCIA e de METAS precisam além da base e do Repasses:
/// - sessões REALIZADAS em várias categorias no mês corrente e no anterior, desenhadas para a
///   tendência ter de tudo: uma em ALTA (acupuntura simples), uma em BAIXA (BSV), uma NOVA
///   (consulta de geriatria), uma que SUMIU (BSV com acupuntura), uma estável e uma com pouca
///   base; mais uma falta, que NÃO pode contar;
/// - receita ligada a parte delas, para a métrica "Receita" ter alta e baixa também;
/// - metas do mês corrente (da clínica, por categoria, por especialidade, de um profissional e
///   de ocupação) e do mês anterior (uma batida e uma não batida) e do próximo (ainda não
///   começou).
/// Sem Id fixo: tudo se acha pelo que a base gravou.
/// </summary>
public sealed class SementeTendenciaEMetas : ISemente
{
    public int Ordem => 80;

    public void Semear(ClinicaDbContext db)
    {
        var profissionais = db.Set<Profissional>().OrderBy(p => p.Id).ToList();
        var pacientes = db.Set<Paciente>().OrderBy(p => p.Id).Select(p => p.Id).ToList();
        if (profissionais.Count < 3 || pacientes.Count == 0) return;
        var categoria = db.Set<CategoriaFinanceira>().FirstOrDefault(c => c.Nome == "Particular")?.Id;

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var esteMes = new DateOnly(hoje.Year, hoje.Month, 1);
        var mesPassado = esteMes.AddMonths(-1);
        var diasAteHoje = hoje.Day;

        var p = 0;
        void Sessoes(DateOnly mes, int quantas, ModalidadeAtendimento modalidade, Profissional quem,
            string? especialidade = null, decimal? valor = null, StatusAgendamento status = StatusAgendamento.Realizado)
        {
            // No mês corrente, só dias que já passaram: sessão realizada amanhã não existe.
            var dias = mes == esteMes ? diasAteHoje : DateTime.DaysInMonth(mes.Year, mes.Month);
            for (var i = 0; i < quantas; i++)
            {
                var dia = mes.AddDays(i % dias);
                var quando = dia.ToDateTime(new TimeOnly(7 + i % 10, 15));
                var paciente = pacientes[p++ % pacientes.Count];
                Atendimento? atendimento = null;
                if (status == StatusAgendamento.Realizado)
                {
                    atendimento = new Atendimento
                    {
                        PacienteId = paciente, Data = dia, Modalidade = modalidade, ModalidadeCodigo = modalidade.ToString(),
                        EspecialidadeConsultaCodigo = especialidade,
                        Categoria = Clinica.Domain.Categoria.Verde,
                        LancadoEm = quando, LancadoPor = "previa", RealizadoEm = quando
                    };
                    db.Add(atendimento);
                }
                db.Add(new Agendamento
                {
                    PacienteId = paciente, ProfissionalId = quem.Id, DataHora = quando, DuracaoMinutos = 30,
                    Status = status, Atendimento = atendimento,
                    ModalidadePrevista = modalidade, ModalidadeCodigo = modalidade.ToString(),
                    EspecialidadeConsultaCodigo = especialidade
                });
                if (atendimento is not null && valor is { } v && i % 2 == 0)
                    db.Add(new LancamentoFinanceiro
                    {
                        Data = dia, DataPagamento = dia, Tipo = TipoLancamento.Entrada, Valor = v,
                        Status = StatusLancamento.Realizado, Descricao = "Sessão particular (prévia)",
                        CategoriaFinanceiraId = categoria, Atendimento = atendimento
                    });
            }
        }

        var helena = profissionais[0];
        var marcos = profissionais[1];
        var bianca = profissionais[2];

        // Em ALTA: 6 → 18.
        Sessoes(mesPassado, 6, ModalidadeAtendimento.AcupunturaSimples, bianca, valor: 180m);
        Sessoes(esteMes, 18, ModalidadeAtendimento.AcupunturaSimples, bianca, valor: 180m);
        // Em BAIXA: 14 → 4.
        Sessoes(mesPassado, 14, ModalidadeAtendimento.BsvApenas, marcos, valor: 350m);
        Sessoes(esteMes, 4, ModalidadeAtendimento.BsvApenas, marcos, valor: 350m);
        // SUMIU: 5 → 0.
        Sessoes(mesPassado, 5, ModalidadeAtendimento.BsvComAcupuntura, marcos);
        // Consulta de PSIQUIATRIA estável (8 → 9) e de GERIATRIA nova (0 → 6).
        Sessoes(mesPassado, 8, ModalidadeAtendimento.Consulta, helena, "Psiquiatria", 420m);
        Sessoes(esteMes, 9, ModalidadeAtendimento.Consulta, helena, "Psiquiatria", 420m);
        Sessoes(esteMes, 6, ModalidadeAtendimento.Consulta, helena, "Geriatria", 380m);
        // Pouca base: endocrinologia 1 → 2.
        Sessoes(mesPassado, 1, ModalidadeAtendimento.Consulta, helena, "Endocrinologia");
        Sessoes(esteMes, 2, ModalidadeAtendimento.Consulta, helena, "Endocrinologia");
        // A falta NÃO conta.
        Sessoes(esteMes, 3, ModalidadeAtendimento.AcupunturaSimples, bianca, status: StatusAgendamento.Faltou);
        db.SaveChanges();

        // ---- Metas ----
        void Meta(DateOnly mes, IndicadorMeta indicador, decimal valor, int? profissional = null,
            string modalidade = "", string especialidade = "", string? observacoes = null)
            => db.Add(new MetaMensal
            {
                Ano = mes.Year, Mes = mes.Month, Indicador = indicador, Valor = valor,
                ProfissionalId = profissional, ModalidadeCodigo = modalidade, EspecialidadeCodigo = especialidade,
                Observacoes = observacoes, CriadoPor = "previa"
            });

        Meta(esteMes, IndicadorMeta.Faturamento, 90_000m, observacoes: "Reajuste da Unimed entra este mês.");
        Meta(esteMes, IndicadorMeta.Sessoes, 120m);
        Meta(esteMes, IndicadorMeta.Sessoes, 20m, modalidade: nameof(ModalidadeAtendimento.AcupunturaSimples));
        Meta(esteMes, IndicadorMeta.Sessoes, 15m, modalidade: nameof(ModalidadeAtendimento.BsvApenas));
        Meta(esteMes, IndicadorMeta.Sessoes, 10m, modalidade: nameof(ModalidadeAtendimento.Consulta), especialidade: "Psiquiatria");
        Meta(esteMes, IndicadorMeta.Sessoes, 30m, profissional: bianca.Id);
        Meta(esteMes, IndicadorMeta.Ocupacao, 75m);
        Meta(mesPassado, IndicadorMeta.Sessoes, 12m, modalidade: nameof(ModalidadeAtendimento.BsvApenas));
        Meta(mesPassado, IndicadorMeta.Sessoes, 10m, modalidade: nameof(ModalidadeAtendimento.AcupunturaSimples));
        Meta(esteMes.AddMonths(1), IndicadorMeta.Faturamento, 95_000m);
        db.SaveChanges();
    }
}
