using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Clinica.Desktop.Controls;
using Clinica.Domain.Entities;
using Clinica.Financeiro.ViewModels;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Exercita carga e filtros reais sobre dados sintéticos, sem consultar produção.</summary>
public static class SeriesQa
{
    public static async Task Executar(IServiceProvider services)
    {
        using var escopo = services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        Exigir(db.Database.IsSqlite() &&
            new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource == ":memory:",
            "As séries só podem ser verificadas no SQLite em memória.");
        var janeiro = new DateOnly(2037, 1, 1);
        var fevereiro = janeiro.AddMonths(1);
        var marco = janeiro.AddMonths(2);
        Exigir(!await db.Set<LancamentoFinanceiro>().AnyAsync(l => l.Data >= janeiro && l.Data < marco.AddMonths(1)),
            "Os meses sintéticos das séries devem começar vazios.");

        LancamentoFinanceiro Criar(int dia, decimal valor, TipoLancamento tipo,
            StatusLancamento status, string descricao) => new()
        {
            Data = janeiro.AddDays(dia - 1), Valor = valor, Tipo = tipo,
            Status = status, Descricao = descricao,
            DataPagamento = status == StatusLancamento.Realizado ? janeiro.AddDays(dia - 1) : null
        };
        var alvo = Criar(1, 100m, TipoLancamento.Entrada, StatusLancamento.Realizado, "ALVO_SERIE — consulta sintética");
        alvo.ValorTaxa = 10m;
        db.AddRange(alvo,
            Criar(2, 200m, TipoLancamento.Entrada, StatusLancamento.Realizado, "Segunda entrada realizada"),
            Criar(2, 50m, TipoLancamento.Saida, StatusLancamento.Realizado, "Saída realizada"),
            Criar(3, 500m, TipoLancamento.Entrada, StatusLancamento.Previsto, "Entrada prevista não é curva realizada"),
            Criar(4, 999m, TipoLancamento.Entrada, StatusLancamento.Cancelado, "Entrada cancelada não é curva"),
            Criar(4, 888m, TipoLancamento.Saida, StatusLancamento.Cancelado, "Saída cancelada não é curva"));
        for (var i = 0; i < 293; i++)
            db.Add(Criar(5, 1m, TipoLancamento.Entrada, StatusLancamento.Previsto, $"Previsão sintética {i:000}"));
        db.Add(new LancamentoFinanceiro
        {
            Data = fevereiro.AddDays(9), DataPagamento = fevereiro.AddDays(9),
            Tipo = TipoLancamento.Entrada, Status = StatusLancamento.Realizado,
            Valor = 75m, Descricao = "Outro mês — entrada independente"
        });
        await db.SaveChangesAsync();

        var caixa = new CaixaViewModel(services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<ISnackbarService>(), services.GetRequiredService<IDialogoService>());
        await Aguardar(caixa);
        caixa.Mes = janeiro.ToDateTime(TimeOnly.MinValue);
        await Aguardar(caixa);
        Exigir(caixa.Linhas.Count == 299 && caixa.SerieDisponivel,
            "299 lançamentos devem produzir série completa sem consulta adicional.");
        Exigir(Moeda(caixa.Entradas) == 300m && Moeda(caixa.Saidas) == 50m && Moeda(caixa.Saldo) == 240m,
            "Totais realizados e resultado líquido devem excluir previstos/cancelados e descontar a taxa.");
        var entradas = Pontos(caixa.GraficoEntradas);
        var saidas = Pontos(caixa.GraficoSaidas);
        Exigir(entradas.Count == 31 && saidas.Count == 31, "Janeiro precisa representar todos os 31 dias.");
        // Valores conhecidos: 100/200 em entradas e 50 em saídas. Os dois gráficos
        // compartilham a escala; 999 cancelados e 500 previstos não alteram os picos.
        Exigir(Perto(entradas[0].Y, 26) && Perto(entradas[1].Y, 4) && Perto(saidas[1].Y, 37),
            "As curvas não representam os realizados conhecidos na mesma escala.");
        Exigir(entradas.Skip(2).All(p => Perto(p.Y, 48)) &&
            saidas.Where((_, i) => i != 1).All(p => Perto(p.Y, 48)),
            "Dias sem realizados, previstos e cancelados devem ficar na linha zero.");
        var curvaEntradasCompleta = caixa.GraficoEntradas;
        var curvaSaidasCompleta = caixa.GraficoSaidas;

        db.Add(Criar(10, 40m, TipoLancamento.Entrada, StatusLancamento.Previsto, "Trezentésimo registro"));
        await db.SaveChangesAsync(); await caixa.CarregarAsync();
        Exigir(caixa.Linhas.Count == 300 && !caixa.SerieDisponivel &&
            string.IsNullOrEmpty(caixa.GraficoEntradas) && string.IsNullOrEmpty(caixa.GraficoSaidas),
            "No limite exato de 300 a carga não prova completude: a curva deve ficar indisponível.");
        db.Add(Criar(12, 40m, TipoLancamento.Saida, StatusLancamento.Previsto, "Trezentésimo primeiro registro"));
        await db.SaveChangesAsync(); await caixa.CarregarAsync();
        Exigir(caixa.Linhas.Count == 300 && caixa.Truncado && !caixa.SerieDisponivel,
            "301 registros devem manter a lista limitada e não desenhar curva parcial.");

        caixa.FiltroTexto = "ALVO_SERIE";
        await Aguardar(caixa);
        Exigir(caixa.Linhas.Count == 1 && caixa.Linhas[0].Id == alvo.Id && caixa.Linhas[0].Valor == 100m,
            "O filtro deve recuperar a entrada de 100 que estava fora do corte de 300.");
        Exigir(caixa.SerieDisponivel && caixa.GraficoEntradas == curvaEntradasCompleta &&
            caixa.GraficoSaidas == curvaSaidasCompleta && Moeda(caixa.Entradas) == 300m,
            "A carga completa deve restaurar as curvas do mês, sem reduzi-las à linha filtrada.");
        caixa.FiltroSituacao = CaixaViewModel.SituacaoPrevisto;
        Exigir(caixa.Linhas.Count == 0 && caixa.GraficoEntradas == curvaEntradasCompleta,
            "Filtro visual sem resultados não pode zerar a série mensal.");
        caixa.LimparFiltroCommand.Execute(null);
        Exigir(caixa.Linhas.Count == 300 && caixa.Truncado && caixa.SerieDisponivel &&
            caixa.GraficoEntradas == curvaEntradasCompleta && caixa.GraficoSaidas == curvaSaidasCompleta,
            "Limpar o filtro mantém o corte visual de 300 e as curvas da carga completa de 301.");

        caixa.Mes = fevereiro.ToDateTime(TimeOnly.MinValue);
        await Aguardar(caixa);
        Exigir(caixa.SerieDisponivel && caixa.Linhas.Count == 1 && Moeda(caixa.Entradas) == 75m &&
            Moeda(caixa.Saidas) == 0m && Moeda(caixa.Saldo) == 75m &&
            caixa.UltimoMovimento == "Outro mês — entrada independente" && caixa.FimEixoSerie == "28",
            "Trocar de mês não pode herdar valores, último movimento nem extensão do mês anterior.");
        Exigir(Pontos(caixa.GraficoEntradas).Count == 28 &&
            Pontos(caixa.GraficoEntradas).Where((_, i) => i != 9).All(p => Perto(p.Y, 48)),
            "Fevereiro deve mostrar apenas a entrada do dia 10.");
        caixa.Mes = marco.ToDateTime(TimeOnly.MinValue);
        await Aguardar(caixa);
        Exigir(caixa.SerieDisponivel && caixa.Linhas.Count == 0 && Moeda(caixa.Saldo) == 0m &&
            caixa.UltimoMovimento == "Nenhum lançamento neste período" &&
            Pontos(caixa.GraficoEntradas).All(p => Perto(p.Y, 48)),
            "Mês vazio não pode herdar movimento, resultado ou desenho anterior.");
        Console.WriteLine("OK séries reais: 299/300/301, realizados, cancelados, previstos, escala comum, filtro completo e troca de mês.");
    }

    private static async Task Aguardar(CaixaViewModel caixa)
    {
        var prazo = DateTime.UtcNow.AddSeconds(15);
        while (caixa.Carregando && DateTime.UtcNow < prazo) await Task.Delay(10);
        Exigir(!caixa.Carregando && !caixa.NaoVerificado, "A leitura real do caixa não terminou corretamente.");
    }
    private static decimal Moeda(string valor)
        => decimal.Parse(valor, NumberStyles.Currency, CultureInfo.GetCultureInfo("pt-BR"));
    private static bool Perto(double valor, double esperado) => Math.Abs(valor - esperado) < 0.01;
    private static IReadOnlyList<Point> Pontos(string curva)
    {
        var geometria = Geometry.Parse(curva).GetFlattenedPathGeometry();
        Exigir(geometria.Figures.Count == 1, "A curva deve formar uma única série contínua.");
        var figura = geometria.Figures[0];
        var pontos = new List<Point> { figura.StartPoint };
        foreach (var segmento in figura.Segments)
        {
            if (segmento is PolyLineSegment linha) pontos.AddRange(linha.Points);
            else if (segmento is LineSegment trecho) pontos.Add(trecho.Point);
            else throw new InvalidOperationException("A série contém um segmento inesperado.");
        }
        return pontos;
    }
    private static void Exigir(bool condicao, string mensagem)
    {
        if (!condicao) throw new InvalidOperationException(mensagem);
    }
}
