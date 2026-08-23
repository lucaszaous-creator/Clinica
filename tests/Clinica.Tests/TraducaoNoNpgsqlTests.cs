using Clinica.Application.Modelos;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// As consultas novas TRADUZEM para o SQL do provedor que a clínica usa (parcela 74, 2ª rodada).
///
/// Por que esta suíte existe
/// -------------------------
/// A auditoria da parcela 74 achou uma consulta que <b>estourava em runtime</b>:
/// <c>AnexosDoPacienteAsync</c> filtrava por <c>!a.Evolucao.Cancelada</c>, e <c>Cancelada</c>
/// é propriedade DERIVADA (<c>=&gt; CanceladaEm is not null</c>), não mapeada. O EF recusa
/// com <i>"Translation of member 'Cancelada' failed"</i>. Nada disso quebra o build: a
/// tradução acontece em RUNTIME, e o método não tinha um único teste que o executasse.
///
/// ⚠️ E há um agravante que a suíte inteira do projeto tem: <b>os testes rodam em SQLite e a
/// clínica roda Postgres</b>, e o suporte a tradução DIFERE entre provedores — um
/// <c>GroupBy</c> ou uma função de string que o SQLite aceita pode não existir no Npgsql.
/// Isso é a mesma família do <c>xmin</c> e das datas com fuso.
///
/// Como ela funciona, e por que é barata
/// -------------------------------------
/// <c>ToQueryString()</c> COMPILA a consulta contra o provedor real e devolve o SQL — <b>sem
/// abrir conexão nenhuma</b>. A cadeia aponta para um banco que não existe e isso não importa:
/// se a expressão não for traduzível, a compilação lança aqui, no <c>dotnet test</c>, meses
/// antes de a clínica esbarrar.
///
/// <b>Consulta nova que use navegação, agregação ou função entra aqui.</b>
/// </summary>
public class TraducaoNoNpgsqlTests
{
    /// <summary>
    /// Contexto configurado para o Npgsql. Nunca conecta — só compila consultas.
    /// </summary>
    private static ClinicaDbContext Postgres()
        => new(new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseNpgsql("Host=nao-conecta;Database=x;Username=u;Password=p")
            .Options);

    [Fact]
    public void Historico_de_sessoes_traduz()
    {
        using var db = Postgres();

        // O GroupBy sobre constante é o jeito de pedir MIN e COUNT numa consulta só. Ele
        // traduz no SQLite; a pergunta que este teste responde é se traduz no Npgsql.
        var sql = db.Atendimentos.AsNoTracking()
            .Where(a => a.PacienteId == 1 && a.RealizadoEm != null)
            .GroupBy(_ => 1)
            .Select(g => new { Primeira = (DateOnly?)g.Min(a => a.Data), Total = g.Count() })
            .ToQueryString();

        sql.Should().Contain("min(").And.Contain("count(");
    }

    [Fact]
    public void Hipoteses_recentes_traduzem()
    {
        using var db = Postgres();

        var sql = db.Evolucoes.AsNoTracking()
            .Where(e => e.PacienteId == 1 && e.CanceladaEm == null
                        && e.HipoteseDiagnostica != null && e.HipoteseDiagnostica != "")
            .OrderByDescending(e => e.Data).ThenByDescending(e => e.Id)
            .Take(30)
            .Select(e => e.HipoteseDiagnostica!)
            .ToQueryString();

        // A projeção é o ponto: UMA coluna, não o prontuário inteiro.
        sql.Should().Contain("\"HipoteseDiagnostica\"").And.NotContain("\"TextoEvolucao\"");
    }

    [Fact]
    public void Anexos_do_paciente_traduzem_E_nao_trazem_os_BYTES()
    {
        using var db = Postgres();

        var sql = db.AnexosProntuario.AsNoTracking()
            .Where(a => a.CanceladoEm == null && a.Evolucao!.PacienteId == 1
                        && a.Evolucao.CanceladaEm == null)
            .OrderByDescending(a => a.Evolucao!.Data).ThenByDescending(a => a.CriadoEm)
            .Select(a => new AnexoDoPaciente(
                a.Id, a.EvolucaoId, a.Evolucao!.Data, a.NomeArquivo, a.Tipo,
                a.TipoConteudo, a.Tamanho, a.Descricao, a.CriadoEm))
            .ToQueryString();

        // Um laudo em PDF tem megabytes. Trazê-los para desenhar uma lista de nomes seria a
        // leitura cara que a parcela 69 já pagou uma vez.
        sql.Should().NotContain("\"Conteudo\"");
    }

    [Fact]
    public void Anamnese_do_paciente_traduz_COM_as_versoes()
    {
        using var db = Postgres();

        var sql = db.Anamneses
            .Include(a => a.Versoes)
            .Where(a => a.PacienteId == 1)
            .ToQueryString();

        // O Include é obrigatório: é a CONTAGEM das versões que numera a próxima. Sem ele a
        // contagem seria sempre zero e toda correção nasceria como "versão 1".
        sql.Should().Contain("VersoesAnamnese");
    }

    /// <summary>
    /// ⚠️ O AUTOTESTE DA REDE — ela precisa ter dentes.
    ///
    /// É a regra da checagem 34: rede nova sem prova contra o caso REAL nasce cega. Este teste
    /// escreve a consulta como ela estava ANTES da correção — usando a propriedade derivada — e
    /// afirma que a compilação REPROVA. Se um dia o EF passar a traduzir isso, este teste
    /// falha e alguém relê os outros três.
    /// </summary>
    [Fact]
    public void A_rede_REPROVA_a_propriedade_derivada_que_causou_o_defeito()
    {
        using var db = Postgres();

        var consultaQuebrada = () => db.AnexosProntuario.AsNoTracking()
            .Where(a => a.CanceladoEm == null && !a.Evolucao!.Cancelada)
            .ToQueryString();

        consultaQuebrada.Should().Throw<InvalidOperationException>()
            .WithMessage("*could not be translated*");
    }
}
