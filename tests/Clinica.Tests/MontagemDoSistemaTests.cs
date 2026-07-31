using System.Reflection;
using Clinica.Application.Servicos;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A montagem do sistema — o que quebra depois de compilar.
///
/// Os 800+ testes deste projeto exercitam regra de negócio: dado um paciente e um
/// convênio, quais códigos nascem. Nenhum deles olhava para as duas peças que são
/// escritas **à mão** e que só falham com o app aberto na clínica:
///
/// 1. **O contêiner de DI** (`DependencyInjection.AddClinica`) é uma lista de
///    sessenta e poucas linhas mantida a dedo. Serviço novo que ninguém registra
///    compila, passa no CI e explode com `InvalidOperationException` no clique que
///    abre a tela. Serviço registrado com dependência que não está no contêiner faz
///    o mesmo.
/// 2. **O snapshot do EF** (`ClinicaDbContextModelSnapshot`) também é escrito à mão
///    aqui, porque não há `dotnet ef` neste ambiente. Snapshot que não bate com as
///    entidades faz a próxima migration nascer errada — e migration errada num banco
///    que já fatura a clínica é o pior defeito que este repositório pode produzir.
///
/// São testes de MONTAGEM, não de regra: eles não sabem o que um convênio faz, e é
/// exatamente por isso que pegam o que os outros não pegam.
/// </summary>
public class MontagemDoSistemaTests
{
    /// <summary>
    /// Uma connection string sintática, nunca usada para conectar: tanto o
    /// contêiner quanto a construção do modelo do EF são operações offline.
    /// </summary>
    private const string ConexaoFicticia =
        "Host=localhost;Database=clinica_teste;Username=x;Password=y";

    private static ServiceProvider Montar()
        => new ServiceCollection().AddClinica(ConexaoFicticia).BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

    /// <summary>Todo serviço de aplicação está no contêiner.</summary>
    /// <remarks>
    /// A lista do <c>AddClinica</c> é mantida a dedo, e esquecer uma linha é o erro
    /// mais fácil de cometer e o mais difícil de notar: o CI fica verde e a tela
    /// nova não abre. Aqui a fonte da verdade passa a ser a pasta `Servicos/`.
    /// </remarks>
    [Fact]
    public void Todo_servico_de_aplicacao_esta_registrado()
    {
        var servicos = new ServiceCollection();
        servicos.AddClinica(ConexaoFicticia);
        var registrados = servicos.Select(d => d.ServiceType).ToHashSet();

        var declarados = typeof(AtendimentoService).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true }
                        && t.Namespace == "Clinica.Application.Servicos"
                        && t.Name.EndsWith("Service", StringComparison.Ordinal))
            .ToList();

        declarados.Should().NotBeEmpty("a varredura precisa achar os serviços para valer algo");

        var faltando = declarados.Where(t => !registrados.Contains(t)).Select(t => t.Name).ToList();

        faltando.Should().BeEmpty(
            "serviço fora do AddClinica compila, passa no CI e explode no clique que abre a tela");
    }

    /// <summary>
    /// Todo serviço registrado é CONSTRUÍVEL — e é isso que o registro sozinho não
    /// prova. Registrar `FechamentoSessaoService` sem registrar um dos quatro
    /// serviços que ele recebe passa neste contêiner e falha na clínica.
    /// </summary>
    [Fact]
    public void Todo_servico_registrado_pode_ser_construido()
    {
        using var provedor = Montar();
        using var escopo = provedor.CreateScope();

        var servicos = new ServiceCollection();
        servicos.AddClinica(ConexaoFicticia);

        var falhas = new List<string>();
        foreach (var tipo in servicos.Select(d => d.ServiceType).Distinct())
        {
            // O DbContext exige um provider configurado de verdade; o resto do
            // contêiner é construível offline.
            if (tipo == typeof(ClinicaDbContext)) continue;
            if (tipo.IsGenericTypeDefinition) continue;

            try
            {
                escopo.ServiceProvider.GetRequiredService(tipo);
            }
            catch (Exception ex)
            {
                falhas.Add($"{tipo.Name}: {ex.GetBaseException().Message}");
            }
        }

        falhas.Should().BeEmpty();
    }

    /// <summary>
    /// O snapshot do EF bate com as entidades.
    ///
    /// Sem `dotnet ef` neste ambiente, migration e snapshot são escritos à mão — e
    /// um snapshot desatualizado não dá erro nenhum: ele faz a PRÓXIMA migration
    /// nascer com o diff errado, num banco que fatura a clínica todo dia.
    ///
    /// O modelo é construído com o provider real (Npgsql), nunca com o SQLite dos
    /// outros testes: o snapshot guarda anotações específicas do PostgreSQL (o
    /// `xmin` da concorrência otimista, as identidades), e compará-lo com um modelo
    /// SQLite acusaria diferença em toda entidade. Nada aqui conecta no banco.
    /// </summary>
    [Fact]
    public void Snapshot_do_EF_esta_em_dia_com_as_entidades()
    {
        var opcoes = new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseNpgsql(ConexaoFicticia)
            .Options;

        using var db = new ClinicaDbContext(opcoes);

        db.Database.HasPendingModelChanges().Should().BeFalse(
            "entidade mudou sem migration correspondente — a próxima migration nasceria "
            + "com o diff errado. Gere a migration e atualize o ModelSnapshot.");
    }

    /// <summary>
    /// Toda migration tem o par `.Designer.cs`, e o nome dos dois casa.
    ///
    /// O Designer carrega o modelo-alvo daquela migration. Escrito à mão, é o
    /// arquivo mais fácil de esquecer — e sem ele o EF não sabe de onde a migration
    /// parte, o que só aparece na hora de aplicar.
    /// </summary>
    [Fact]
    public void Toda_migration_tem_designer_e_esta_no_assembly()
    {
        var migrations = typeof(ClinicaDbContext).Assembly
            .GetTypes()
            .Where(t => t.GetCustomAttribute<MigrationAttribute>() is not null)
            .ToList();

        migrations.Should().NotBeEmpty();

        // O atributo [Migration] mora no Designer. Migration sem Designer não
        // aparece nesta lista — então o que se confere é a contragem: toda classe
        // que herda de Migration precisa estar aqui.
        var classes = typeof(ClinicaDbContext).Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Migration)) && !t.IsAbstract)
            .ToList();

        var semDesigner = classes
            .Where(t => t.GetCustomAttribute<MigrationAttribute>() is null)
            .Select(t => t.Name)
            .ToList();

        semDesigner.Should().BeEmpty(
            "migration sem o par .Designer.cs não tem modelo-alvo e só falha ao aplicar");
    }
}
