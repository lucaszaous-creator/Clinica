// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// A BASE fictícia que toda tela pode usar: plano de contas, 24 meses de caixa, pacientes e
/// profissionais. Tela que precisa de mais (contas, estoque, repasses…) escreve a SUA
/// semente em `Sementes/`, com Ordem maior que zero, e lê daqui o que precisar.
/// </summary>
public sealed class SementeBase : ISemente
{
    public int Ordem => 0;

    public void Semear(ClinicaDbContext db)
    {
        string[] nomes = ["Ana Beatriz Souza", "Carlos Eduardo Lima", "Fernanda Ribeiro", "João Pedro Alves",
            "Mariana Costa", "Paulo Henrique Martins", "Renata Oliveira", "Tiago Fernandes", "Luciana Prado", "Roberto Nunes"];
        var nasc = new DateOnly(1970, 3, 1);
        for (var p = 0; p < nomes.Length; p++)
            db.Add(new Paciente { Nome = nomes[p], Telefone = $"(21) 9{p}{p}12-34{p}{p}", DataNascimento = nasc.AddYears(p * 3).AddDays(p * 37) });
        db.Add(new Profissional { Nome = "Dra. Helena Duarte", NomeCurto = "Dra. Helena", RegistroConselho = "CRM-RJ 52.1234" });
        db.Add(new Profissional { Nome = "Dr. Marcos Vieira", NomeCurto = "Dr. Marcos", RegistroConselho = "CRM-RJ 61.9876" });
        db.Add(new Profissional { Nome = "Bianca Moraes", NomeCurto = "Bianca", RegistroConselho = "CREFITO-2 12345-F" });
        db.SaveChanges();

        var cat = new Dictionary<string, CategoriaFinanceira>();
        void C(string codigo, string nome, TipoLancamento tipo) =>
            cat[nome] = db.Add(new CategoriaFinanceira { Codigo = codigo, Nome = nome, Tipo = tipo }).Entity;
        C("E1", "Convênios", TipoLancamento.Entrada);
        C("E2", "Particular", TipoLancamento.Entrada);
        C("E3", "Pacotes", TipoLancamento.Entrada);
        C("S1", "Folha de pagamento", TipoLancamento.Saida);
        C("S2", "Aluguel", TipoLancamento.Saida);
        C("S3", "Insumos e medicamentos", TipoLancamento.Saida);
        C("S4", "Impostos", TipoLancamento.Saida);
        C("S5", "Marketing", TipoLancamento.Saida);
        db.SaveChanges();

        var rnd = new Random(7);
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        for (var i = 23; i >= 0; i--)
        {
            var mes = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-i);
            var sazonal = 1 + 0.12 * Math.Sin((mes.Month - 3) / 12.0 * Math.Tau);
            decimal V(double baseValor) => Math.Round((decimal)(baseValor * sazonal * (0.9 + rnd.NextDouble() * 0.2)), 2);
            var atual = i == 0;

            void L(string categoria, TipoLancamento tipo, decimal valor, int dia, bool previsto = false)
            {
                var data = mes.AddDays(Math.Min(dia, 27));
                db.Add(new LancamentoFinanceiro
                {
                    Data = data, Tipo = tipo, Valor = valor, Descricao = categoria,
                    Status = previsto ? StatusLancamento.Previsto : StatusLancamento.Realizado,
                    DataPagamento = previsto ? null : data,
                    DataVencimento = previsto ? data : null,
                    CategoriaFinanceiraId = cat[categoria].Id
                });
            }

            // Até o dia de hoje o mês corrente é realizado; o resto ainda vai vencer.
            bool Prev(int dia) => atual && dia > hoje.Day;
            L("Convênios", TipoLancamento.Entrada, V(38_000), 10, Prev(10));
            L("Convênios", TipoLancamento.Entrada, V(21_000), 25, Prev(25));
            L("Particular", TipoLancamento.Entrada, V(14_500), 15, Prev(15));
            L("Pacotes", TipoLancamento.Entrada, V(9_800), 20, Prev(20));
            L("Folha de pagamento", TipoLancamento.Saida, V(31_000), 5, Prev(5));
            L("Aluguel", TipoLancamento.Saida, 8_500m, 10, Prev(10));
            L("Insumos e medicamentos", TipoLancamento.Saida, V(7_200), 18, Prev(18));
            L("Impostos", TipoLancamento.Saida, V(6_400), 20, Prev(20));
            if (mes.Month % 3 == 0) L("Marketing", TipoLancamento.Saida, V(4_000), 12, Prev(12));
            if (i % 5 == 0) db.Add(new LancamentoFinanceiro
            {
                Data = mes.AddDays(8), Tipo = TipoLancamento.Saida, Valor = V(1_150), Descricao = "Manutenção",
                Status = StatusLancamento.Realizado, DataPagamento = mes.AddDays(8)
            });
        }
        db.SaveChanges();
    }
}
