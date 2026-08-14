using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Toda coluna de data e hora do modelo é <c>timestamp without time zone</c>.
///
/// Por que isto precisa de uma rede, e por que os 1580 testes não a davam
/// ----------------------------------------------------------------------
/// O sistema grava hora de PAREDE: <c>DateTime.Now</c>, com <c>Kind=Local</c>. O Npgsql
/// <b>recusa</b> escrever isso numa coluna <c>timestamp WITH time zone</c> — "only UTC is
/// supported" —, e o padrão do provedor para <c>DateTime</c> é justamente <b>com</b> fuso.
/// Ou seja: esquecer o <c>HasColumnType</c> numa propriedade nova não quebra nada até
/// alguém, em produção, tentar gravá-la.
///
/// E os testes rodam em <b>SQLite</b>, que guarda data como texto e não liga para o
/// <c>Kind</c>. Por isso a suíte inteira fica verde enquanto a clínica leva
/// "An error occurred while saving the entity changes" — foi o que aconteceu em
/// 14/08/2026, na publicação de um documento que tinha acabado de ser assinado com sucesso.
///
/// Seis colunas estavam assim, todas das parcelas 52 e 53 (cancelar evolução, anexo,
/// avaliação e medida; versionar evolução; publicar documento) — isto é, as garantias de
/// prontuário imutável que a auditoria da cliente exigiu, todas quebradas no Postgres e
/// nenhuma detectada pelos testes.
///
/// Esta varredura lê o MODELO, não o banco: ela vale para qualquer propriedade nova, e
/// falha no commit em que alguém esquecer o mapeamento — que é meses antes de a clínica
/// esbarrar nele.
/// </summary>
public class DatasSemFusoTests
{
    private const string SemFuso = "timestamp without time zone";

    [Fact]
    public void Toda_coluna_de_data_do_modelo_e_SEM_FUSO()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();

        using var db = new ClinicaDbContext(
            new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conn).Options);

        var faltando = db.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties()
                .Where(p => Nu(p.ClrType) == typeof(DateTime))
                .Where(p => !string.Equals(p.GetColumnType(), SemFuso, StringComparison.OrdinalIgnoreCase))
                .Select(p => $"{t.ClrType.Name}.{p.Name} → {p.GetColumnType() ?? "(padrão do provedor)"}"))
            .OrderBy(x => x)
            .ToList();

        faltando.Should().BeEmpty(
            "o Npgsql RECUSA DateTime com Kind=Local em coluna 'with time zone', e o sistema "
            + "grava DateTime.Now em toda parte. Declare "
            + $"HasColumnType(\"{SemFuso}\") nestas propriedades:\n  "
            + string.Join("\n  ", faltando));
    }

    private static Type Nu(Type t) => Nullable.GetUnderlyingType(t) ?? t;
}
