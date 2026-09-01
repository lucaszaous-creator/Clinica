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

    [Fact]
    public void Mapas_das_evolucoes_em_LOTE_traduzem()
    {
        using var db = Postgres();

        // O `Contains` sobre a lista de ids é o que evita um laço com `await` dentro: sem
        // ele, um relatório de quarenta sessões faria quarenta idas ao banco REMOTO para
        // montar uma folha só. Ele traduz no SQLite; a pergunta é se traduz no Npgsql.
        int[] ids = [1, 2, 3];

        var sql = db.MapasCorporais.AsNoTracking()
            .Include(m => m.Pontos)
            .Where(m => ids.Contains(m.EvolucaoId))
            .ToQueryString();

        sql.Should().Contain("PontosMapa",
            "sem o Include os pontos vêm vazios em produção — e o teste passa mesmo assim, "
            + "pelo relationship fixup do EF num DbContext compartilhado (parcela 68)");
    }

    [Fact]
    public void Sessoes_dos_pacientes_em_LOTE_traduzem()
    {
        using var db = Postgres();

        // O enriquecimento da BUSCA da carteira (parcela 88, 3ª rodada). Ele soma três
        // coisas que o SQLite aceita de olhos fechados e o Npgsql pode não aceitar: um
        // `Contains` sobre a lista de ids, um `GroupBy` por coluna e duas agregações.
        int[] ids = [1, 2, 3];

        var sql = db.Agendamentos.AsNoTracking()
            .Where(a => ids.Contains(a.PacienteId)
                        && a.Status == Clinica.Domain.Entities.StatusAgendamento.Realizado)
            .GroupBy(a => a.PacienteId)
            .Select(g => new
            {
                PacienteId = g.Key,
                Ultima = g.Max(a => a.DataHora),
                Sessoes = g.Count()
            })
            .ToQueryString();

        sql.Should().Contain("max(").And.Contain("count(");

        // ⚠️ E o agrupamento tem de acontecer no BANCO. Materializar os agendamentos para
        // contar em memória traria milhares de linhas de uma clínica com dois anos de casa
        // para produzir uma linha por paciente — o custo que a parcela 69 já pagou uma vez.
        sql.Should().Contain("GROUP BY");
    }

    /// <summary>
    /// ⚠️ O AUTOTESTE DA REDE — ela precisa ter dentes.
    ///
    /// É a regra da checagem 34: rede nova sem prova contra o caso REAL nasce cega. Este teste
    /// escreve a consulta como ela estava ANTES da correção — usando a propriedade derivada — e
    /// afirma que a compilação REPROVA. Se um dia o EF passar a traduzir isso, este teste
    /// falha e alguém relê os outros três.
    /// </summary>
    /// <summary>
    /// A pergunta "quais termos LGPD carregam finalidade" (parcela 89, 2ª rodada) usa
    /// NAVEGAÇÃO dentro do <c>Where</c> — um <c>EXISTS</c> — e projeta uma coluna só.
    ///
    /// Os dois lados importam: se o EXISTS não traduzisse, a ficha estouraria ao abrir; e
    /// se a projeção trouxesse a entidade inteira, a leitura arrastaria o
    /// <c>Desenho</c> dos itens a cada abertura de ficha.
    /// </summary>
    [Fact]
    public void Termos_lgpd_com_finalidade_traduzem_COM_exists_e_UMA_coluna()
    {
        using var db = Postgres();

        var sql = db.DocumentosClinicos.AsNoTracking()
            .Where(d => d.PacienteId == 1
                        && d.Tipo == Clinica.Domain.Entities.TipoDocumentoClinico.Consentimento
                        && d.Itens.Any(i => i.Codigo != null))
            .Select(d => d.Id)
            .ToQueryString();

        sql.Should().Contain("EXISTS");
        sql.Should().NotContain("Desenho", "a projeção é de UMA coluna — o id");
    }

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

    /// <summary>
    /// A lista de Prontuários (set/2026): a projeção compila no Npgsql e NÃO arrasta os
    /// textos da evolução — meio megabyte por carga é o preço de esquecer isto.
    /// </summary>
    [Fact]
    public void Linhas_de_prontuario_traduzem_E_nao_trazem_os_textos()
    {
        using var db = Postgres();

        var sql = db.Evolucoes.AsNoTracking()
            .Where(e => e.CanceladaEm == null)
            .Select(e => new LinhaEvolucaoProntuarios(
                e.Id, e.PacienteId, e.Paciente!.Nome, e.Data, e.AgendamentoId,
                e.Agendamento == null
                    ? (Clinica.Domain.ModalidadeAtendimento?)null
                    : e.Agendamento.ModalidadePrevista,
                e.Agendamento == null ? null : e.Agendamento.ModalidadeCodigo,
                e.Agendamento == null ? null : e.Agendamento.EspecialidadeConsulta,
                e.Versoes.Count,
                e.Profissional == null ? null : e.Profissional.Nome))
            .ToQueryString();

        sql.Should().NotContain("TextoEvolucao",
            "a lista mostra cinco colunas — o prontuário inteiro fica fora do SELECT");
        sql.Should().NotContain("HistoriaDoencaAtual");
    }

    /// <summary>
    /// A lista de resultados do paciente é lida a cada abertura de tela e NÃO pode
    /// arrastar os PDFs dos laudos — é por isso que os bytes moram em tabela 1:1
    /// (o padrão do retrato do paciente; a lição da parcela 74).
    /// </summary>
    [Fact]
    public void A_lista_de_resultados_NAO_traz_os_bytes_do_laudo()
    {
        using var db = Postgres();

        var sql = db.ResultadosExame.AsNoTracking()
            .Where(r => r.PacienteId == 1 && r.CanceladoEm == null)
            .OrderByDescending(r => r.Data)
            .ToQueryString();

        sql.Should().Contain("ArquivoNome", "os METADADOS ficam na linha do resultado");
        sql.Should().NotContain("ArquivosResultadoExame",
            "os BYTES só são buscados por quem clica em abrir o laudo");
    }

    /// <summary>
    /// A tela de Exames (set/2026): pedidos com a contagem de resultados vigentes numa
    /// subconsulta — compila no Npgsql sem trazer a foto do paciente nem o corpo do
    /// documento.
    /// </summary>
    [Fact]
    public void Pedidos_de_exame_traduzem_com_a_contagem_de_resultados()
    {
        using var db = Postgres();

        var sql = db.DocumentosClinicos.AsNoTracking()
            .Where(d => d.Tipo == Clinica.Domain.Entities.TipoDocumentoClinico.PedidoExame)
            .Select(d => new PedidoDeExameLinha(
                d.Id, d.Numero, d.PacienteId, d.Paciente!.Nome, d.Data,
                d.Itens.OrderBy(it => it.Ordem).Select(it => it.Descricao).FirstOrDefault(),
                d.Itens.Count,
                db.ResultadosExame.Count(r => r.PedidoDocumentoId == d.Id
                    && r.CanceladoEm == null),
                d.CanceladoEm != null,
                d.Profissional == null ? null : d.Profissional.Nome))
            .ToQueryString();

        sql.Should().NotContain("FotoMiniatura",
            "o nome do paciente vem por projeção — a linha inteira dele arrastaria a miniatura");
        sql.Should().NotContain("\"Corpo\"");
    }
}
