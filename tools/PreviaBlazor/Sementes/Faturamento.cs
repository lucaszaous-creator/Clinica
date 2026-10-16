// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// O FATURAMENTO de guias (out/2026): atendimentos lançados pelo MESMO motor de regras do app
/// (<see cref="AtendimentoService"/>), não códigos escritos à mão — é o que faz o painel ter 1º e
/// 2º código com as datas que a regra do convênio dá, e não as que a semente inventaria. Por cima,
/// os desfechos que cada tela precisa ver: guias baixadas (Faturados, lote TISS), uma glosa em
/// prazo e uma vencida (Glosas), uma não conformidade (NC), uma observação de pendência e um lote
/// exportado e enviado (TISS).
/// </summary>
public sealed class SementeFaturamento : ISemente
{
    public int Ordem => 35;

    public void Semear(ClinicaDbContext db)
    {
        var repo = new ClinicaRepositorio(db);
        new ConvenioCatalogoService(repo).RecarregarCacheAsync().GetAwaiter().GetResult();
        var atendimentos = new AtendimentoService(repo);
        var faturamento = new FaturamentoService(repo);
        var glosas = new GlosaService(repo);
        var rodada = new RodadaPendenciasService(repo, new PendenciaService(repo), new ParametrosService(repo));
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        Paciente P(string nome, Convenio familia, string codigo, string carteirinha, int validadeDias) =>
            db.Add(new Paciente
            {
                Nome = nome, Convenio = familia, ConvenioCodigo = codigo, Telefone = "(21) 97777-1234",
                Carteirinha = carteirinha, ValidadeCarteirinha = hoje.AddDays(validadeDias), Sexo = Sexo.Feminino
            }).Entity;

        var pacientes = new[]
        {
            P("Adriana Lopes Machado", Convenio.UnimedIntercambio, "UnimedIntercambio", "0 865 000123456789 0", 200),
            P("Bruno Siqueira Campos", Convenio.UnimedIntercambio, "UnimedIntercambio", "0 865 000987654321 1", 12),
            P("Camila Fontes Barreto", Convenio.UnimedPadrao, "UnimedPadrao", "0 032 112233445566 2", -3),
            P("Diego Albuquerque", Convenio.Amil, "Amil", "AM-4477120", 300),
            P("Elaine Cristina Moura", Convenio.Petrobras, "Petrobras", "PB 0099-77", 90),
            P("Fábio Nogueira Reis", Convenio.UnimedIntercambio, "UnimedIntercambio", "0 865 000555444333 9", 60)
        };
        db.SaveChanges();

        var lancados = new List<CodigoFaturamento>();
        void Lancar(Paciente p, int diasAtras, ModalidadeAtendimento m)
        {
            var r = atendimentos.LancarAsync(p.Id, hoje.AddDays(-diasAtras), m, operador: "Prévia").GetAwaiter().GetResult();
            lancados.AddRange(r.Atendimento.Codigos);
        }

        // Pendências em todos os estados de urgência (atrasada, vence hoje, no prazo).
        Lancar(pacientes[0], 15, ModalidadeAtendimento.AcupunturaComEletro);
        Lancar(pacientes[1], 3, ModalidadeAtendimento.AcupunturaComEletro);
        Lancar(pacientes[2], 1, ModalidadeAtendimento.AcupunturaComEletro);
        Lancar(pacientes[3], 6, ModalidadeAtendimento.Consulta);
        Lancar(pacientes[4], 0, ModalidadeAtendimento.Consulta);
        Lancar(pacientes[5], 22, ModalidadeAtendimento.AcupunturaComEletro);
        Lancar(pacientes[0], 40, ModalidadeAtendimento.AcupunturaComEletro);
        Lancar(pacientes[3], 35, ModalidadeAtendimento.Consulta);
        db.ChangeTracker.Clear();

        // Baixadas (Faturados e lote TISS): os dois códigos das sessões mais antigas.
        var antigos = lancados.Where(c => c.DataPrevistaFaturamento <= hoje.AddDays(-20)).ToList();
        var n = 900_100;
        foreach (var c in antigos)
            faturamento.DarBaixaAsync(c.Id, c.DataPrevistaFaturamento.AddDays(1), (n++).ToString(), "Prévia", "baixa da prévia").GetAwaiter().GetResult();
        db.ChangeTracker.Clear();

        // Uma glosa ainda no prazo de recurso e outra com o prazo vencido.
        if (antigos.Count >= 2)
        {
            glosas.RegistrarAsync(antigos[0].Id, hoje.AddDays(-5), "Carteirinha sem cobertura para o procedimento", "1801", "Prévia").GetAwaiter().GetResult();
            glosas.RegistrarAsync(antigos[1].Id, hoje.AddDays(-45), "Guia sem assinatura do beneficiário", "2006", "Prévia").GetAwaiter().GetResult();
        }
        db.ChangeTracker.Clear();

        // Uma não conformidade justificada e uma observação numa pendência aberta.
        var abertos = lancados.Where(c => c.DataPrevistaFaturamento > hoje.AddDays(-20)).OrderBy(c => c.DataPrevistaFaturamento).ToList();
        if (abertos.Count >= 2)
        {
            rodada.MarcarNaoConformidadeAsync(abertos[0].Id, "Portal da operadora fora do ar na semana; paciente não retornou", "Prévia").GetAwaiter().GetResult();
            var obs = db.Set<CodigoFaturamento>().Single(c => c.Id == abertos[1].Id);
            obs.RegistrarObservacaoPendencia("Aguardando a senha da operadora — ligar na quinta");
            db.SaveChanges();
        }
        db.ChangeTracker.Clear();

        // Um lote exportado e enviado com as guias baixadas que sobraram sem glosa.
        var lotes = new LoteTissService(repo, new ParametrosService(repo));
        var lote = lotes.CriarAsync(hoje.AddDays(-60), hoje, "123456", "Prévia", "UnimedIntercambio").GetAwaiter().GetResult();
        if (lote is not null)
            lotes.MarcarEnviadoAsync(lote.Id, hoje.AddDays(-10), "PROT-2026-0042", "Prévia").GetAwaiter().GetResult();
        db.ChangeTracker.Clear();
    }
}
