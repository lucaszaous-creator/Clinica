// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// Lotes e XML TISS (out/2026): o que a semente comum do faturamento não tem para esta tela —
/// guias BAIXADAS NESTE MÊS de duas operadoras ainda sem lote (é o que o "Gerar lote" do
/// período corrente exporta, e as duas operadoras fazem a pergunta "um lote por
/// operadora" aparecer) e um lote GERADO e ainda não enviado (a linha do "Marcar enviado").
///
/// Pacientes e atendimentos NOVOS, só desta semente: as pendências da semente comum
/// continuam nos estados de urgência que as outras telas fotografam.
/// </summary>
public sealed class SementeFaturamentoTiss : ISemente
{
    public int Ordem => 36;

    public void Semear(ClinicaDbContext db)
    {
        var repo = new ClinicaRepositorio(db);
        var atendimentos = new AtendimentoService(repo);
        var faturamento = new FaturamentoService(repo);
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var inicioDoMes = new DateOnly(hoje.Year, hoje.Month, 1);

        Paciente P(string nome, Convenio familia, string codigo, string carteirinha) =>
            db.Add(new Paciente
            {
                Nome = nome, Convenio = familia, ConvenioCodigo = codigo, Telefone = "(21) 96666-4321",
                Carteirinha = carteirinha, ValidadeCarteirinha = hoje.AddDays(180), Sexo = Sexo.Masculino
            }).Entity;

        var gilberto = P("Gilberto Ramos Pacheco", Convenio.Amil, "Amil", "AM-5512098");
        var helena = P("Helena Vasconcelos Prado", Convenio.Petrobras, "Petrobras", "PB 0123-45");
        var igor = P("Igor Tavares Lima", Convenio.Petrobras, "Petrobras", "PB 0456-78");
        db.SaveChanges();

        var numero = 900_500;
        void LancarEBaixar(Paciente p, DateOnly data)
        {
            var r = atendimentos.LancarAsync(p.Id, data, ModalidadeAtendimento.Consulta, operador: "Prévia").GetAwaiter().GetResult();
            foreach (var c in r.Atendimento.Codigos.Where(c => c.DataPrevistaFaturamento <= hoje))
                faturamento.DarBaixaAsync(c.Id, hoje, (numero++).ToString(), "Prévia", "baixa da prévia (TISS)").GetAwaiter().GetResult();
            db.ChangeTracker.Clear();
        }

        // Candidatas do mês corrente, de duas operadoras.
        LancarEBaixar(gilberto, inicioDoMes);
        LancarEBaixar(helena, inicioDoMes);

        // Um lote GERADO e ainda não enviado (mês anterior, Petrobras).
        var mesPassado = inicioDoMes.AddMonths(-1);
        LancarEBaixar(igor, mesPassado.AddDays(4));
        new LoteTissService(repo, new ParametrosService(repo))
            .CriarAsync(mesPassado, inicioDoMes.AddDays(-1), "654321", "Prévia", "Petrobras").GetAwaiter().GetResult();
        db.ChangeTracker.Clear();
    }
}
