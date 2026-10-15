// Prévia das telas Blazor com dados FICTÍCIOS (ver PreviaBlazor.csproj).
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Hospedagem;
using Clinica.Ui.Blazor.Telas;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Um SQLite em memória que vive enquanto a prévia estiver no ar.
var conexao = new SqliteConnection("Data Source=:memory:");
conexao.Open();
builder.Services.AddDbContext<ClinicaDbContext>(o => o.UseSqlite(conexao));
builder.Services.AddScoped<IClinicaRepositorio, ClinicaRepositorio>();
builder.Services.AddScoped<FluxoCaixaService>();
builder.Services.AddSingleton<IHostDaTela, HostDaPrevia>();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

using (var escopo = app.Services.CreateScope())
{
    var db = escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>();
    db.Database.EnsureCreated();
    DadosFicticios.Semear(db);
}

// Os MESMOS arquivos embutidos que o BlazorWebView do WPF serve.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new ManifestEmbeddedFileProvider(typeof(FluxoDeCaixa).Assembly, "Ativos")
});
app.UseAntiforgery();
app.MapRazorComponents<PreviaBlazor.Componentes.App>().AddInteractiveServerRenderMode();
app.Run();

sealed class HostDaPrevia : IHostDaTela
{
    public Task<string?> ExportarCsvAsync(IReadOnlyList<string> cabecalho,
        IReadOnlyList<IReadOnlyList<string>> linhas, string nomeArquivo)
        => Task.FromResult<string?>($"Na prévia o CSV não é gravado ({linhas.Count} linhas montadas).");
}

static class DadosFicticios
{
    public static void Semear(ClinicaDbContext db)
    {
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
