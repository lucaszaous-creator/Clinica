// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// Guias faturadas NO MÊS CORRENTE (out/2026): a tela "Guias faturadas" abre no mês de hoje,
/// e as baixas da <see cref="SementeFaturamento"/> caem semanas atrás — sem esta, a tela
/// abriria sempre vazia na prévia. Duas operadoras, um paciente com a glosa já registrada e
/// um 2º código ainda em aberto do mesmo atendimento (a Consulta de guias mostra as duas
/// situações lado a lado).
/// </summary>
public sealed class SementeFaturamentoGuias : ISemente
{
    public int Ordem => 37;

    public void Semear(ClinicaDbContext db)
    {
        var repo = new ClinicaRepositorio(db);
        new ConvenioCatalogoService(repo).RecarregarCacheAsync().GetAwaiter().GetResult();
        var atendimentos = new AtendimentoService(repo);
        var faturamento = new FaturamentoService(repo);
        var glosas = new GlosaService(repo);
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var inicioDoMes = new DateOnly(hoje.Year, hoje.Month, 1);
        DateOnly NoMes(int diasAtras) => hoje.AddDays(-diasAtras) < inicioDoMes ? inicioDoMes : hoje.AddDays(-diasAtras);

        Paciente P(string nome, Convenio familia, string codigo, string carteirinha) =>
            db.Add(new Paciente
            {
                Nome = nome, Convenio = familia, ConvenioCodigo = codigo, Telefone = "(21) 96666-4321",
                Carteirinha = carteirinha, ValidadeCarteirinha = hoje.AddDays(180), Sexo = Sexo.Masculino
            }).Entity;

        var gustavo = P("Gustavo Henrique Prado", Convenio.UnimedPadrao, "UnimedPadrao", "0 032 998877665544 3");
        var helena = P("Helena Martins Teixeira", Convenio.Amil, "Amil", "AM-5512903");
        var igor = P("Igor Batista Ramalho", Convenio.UnimedIntercambio, "UnimedIntercambio", "0 865 000111222333 4");
        db.SaveChanges();

        var n = 910_200;
        void LancarEBaixar(Paciente p, int diasAtras, ModalidadeAtendimento m, bool baixarTodos)
        {
            var r = atendimentos.LancarAsync(p.Id, NoMes(diasAtras), m, operador: "Recepção (prévia)").GetAwaiter().GetResult();
            var codigos = r.Atendimento.Codigos.OrderBy(c => c.Ordem).ToList();
            foreach (var c in baixarTodos ? codigos : codigos.Take(1))
                faturamento.DarBaixaAsync(c.Id, NoMes(Math.Max(0, diasAtras - 1)), (n++).ToString(), "Faturista (prévia)", null)
                    .GetAwaiter().GetResult();
            db.ChangeTracker.Clear();
        }

        LancarEBaixar(gustavo, 9, ModalidadeAtendimento.AcupunturaComEletro, baixarTodos: true);
        LancarEBaixar(helena, 5, ModalidadeAtendimento.Consulta, baixarTodos: true);
        LancarEBaixar(igor, 3, ModalidadeAtendimento.AcupunturaComEletro, baixarTodos: false);

        // Uma glosa registrada numa baixa do mês — a coluna e o selo "Glosada".
        var umaDoMes = db.Set<CodigoFaturamento>()
            .Where(c => c.Atendimento!.PacienteId == helena.Id && c.DataBaixa != null)
            .Select(c => c.Id).FirstOrDefault();
        if (umaDoMes != 0)
            glosas.RegistrarAsync(umaDoMes, hoje, "Operadora pediu o relatório da consulta", "1705", "Faturista (prévia)").GetAwaiter().GetResult();
        db.ChangeTracker.Clear();
    }
}
