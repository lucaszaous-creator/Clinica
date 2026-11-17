using System.Data.Common;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Tiss.Conector;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// OS DOIS PAPÉIS DO BANCO DO CONECTOR TISS, MEDIDOS (nov/2026). O primeiro teste real na homologação
/// da Amil parou em <c>42501: permission denied for table Convenios</c>: o comando <c>ambiente</c>, com o
/// usuário <c>tiss_operacao</c>, lê o cadastro de convênios, e o <c>papel-tiss-operacao.sql</c> não o
/// concedia. A lista de GRANTs tinha sido escrita "a partir do código", sem medida.
///
/// Aqui ela é MEDIDA: cada comando do <c>comando.sh</c> e cada tipo de transação do serviço rodam sobre um
/// banco com um interceptador que anota o SQL; as tabelas e as operações (SELECT, INSERT, UPDATE,
/// DELETE) que aparecem são comparadas com os GRANTs dos dois arquivos SQL — nos DOIS sentidos: nada
/// usado sem permissão (o 42501) e nada concedido sem uso (o mínimo). Que comando usa que papel vem do
/// próprio <c>comando.sh</c> (os que rodam só com o <c>conector.env</c>).
/// </summary>
public sealed class PapeisDoConectorTissTests : IDisposable
{
    private const string Amil = "Amil";
    private const string Petrobras = TissBancoDeTeste.Petrobras;
    private const string Login = "000123";
    private const string Senha = "senha-ficticia-de-teste";
    private const string Op = "operador.servidor";

    private readonly TissBancoDeTeste _b = new();
    private readonly Rede _rede = new();
    private readonly Dictionary<string, HashSet<(string Tabela, string Operacao)>> _usado = new()
    {
        ["tiss_operacao"] = [], ["tiss_conector"] = []
    };

    public PapeisDoConectorTissTests() => _b.Agora = new DateTime(2026, 11, 3, 10, 0, 0); // dentro da janela da Amil

    public void Dispose() => _b.Dispose();

    [Fact]
    public async Task Cada_papel_tem_exatamente_as_permissoes_que_os_comandos_e_o_servico_usam()
    {
        var doConectorEnv = ComandosQueUsamOConectorEnv();
        doConectorEnv.Should().Contain(["situacao", "cancelar", "testar-lote"], "o comando.sh é a fonte de que papel cada comando usa");

        // ---- preparo (fora da medida) ----
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(Petrobras, Login, Senha, TissBancoDeTeste.Operador);
        var guiaAmil = await GuiaAmilConferidaAsync();

        // ---- os comandos de quem opera, na ordem de uma instalação ----
        string[][] comandos =
        [
            ["credencial", Amil, "--operador", Op],
            ["envio", Petrobras, "ligar", "--operador", Op],
            ["endereco", Petrobras, "elegibilidade", "https://exemplo.invalid/eleg", "--operador", Op],
            ["endereco", Petrobras, "todos", "https://exemplo.invalid/todos", "--operador", Op],
            ["codigo", Amil, "login", "--operador", Op],
            ["codigo", Amil, "padrao", "--operador", Op],
            ["situacao"],
            ["situacao", Amil],
            ["ambiente", Amil, "homologacao", "--operador", Op],
            ["situacao", Amil],
            ["testar-elegibilidade", Amil, "--carteira", TissBancoDeTeste.CarteiraFicticia, "--operador", Op],
            ["testar-lote", Amil, "--exemplo", "--carteira", TissBancoDeTeste.CarteiraFicticia, "--operador", Op],
            ["testar-lote", Amil, "--guias", guiaAmil.ToString(), "--operador", Op],
            ["testar-protocolo", Amil, "--protocolo", "777001", "--operador", Op],
            ["ambiente", Amil, "producao", "--operador", Op],
            ["envio", Petrobras, "desligar", "--operador", Op],
            ["credencial", Amil, "--remover", "--operador", Op],
        ];
        foreach (var args in comandos)
        {
            _rede.ResponderSempre(HttpStatusCode.InternalServerError);
            var (saida, erro) = (new StringWriter(), new StringWriter());
            var papel = doConectorEnv.Contains(args[0]) ? "tiss_conector" : "tiss_operacao";
            var codigo = await ComandosDoConector.ExecutarAsync(args, Servicos(papel), saida, erro,
                ambiente: v => v == ComandosDoConector.VariavelLogin ? Login : v == ComandosDoConector.VariavelSenha ? Senha : null);
            codigo.Should().BeOneOf([0, 1], $"{string.Join(' ', args)}: {erro}");
            erro.ToString().Should().NotContain("Exception", string.Join(' ', args));
        }
        // cancelar: um pedido da tela (fora da medida), cancelado pelo comando (medido).
        var pedido = await _b.Fila().EnfileirarElegibilidadeAsync(Petrobras, "000999", null, null, TissBancoDeTeste.Operador);
        (await ComandosDoConector.ExecutarAsync(["cancelar", pedido.Id.ToString(), "--operador", Op], Servicos("tiss_conector"),
            new StringWriter(), new StringWriter())).Should().Be(0);

        // ---- o serviço: cada tipo de transação, até o desfecho ----
        await _b.Credenciais().SalvarAsync(Amil, Login, Senha, TissBancoDeTeste.Operador);
        await _b.Credenciais().DefinirEnvioAsync(Petrobras, true, TissBancoDeTeste.Operador);
        foreach (var tipo in new[] { TipoTransacaoTiss.Elegibilidade, TipoTransacaoTiss.EnvioGuia, TipoTransacaoTiss.StatusProtocolo, TipoTransacaoTiss.CancelamentoGuia })
        {
            _rede.ResponderSempre(HttpStatusCode.InternalServerError);
            await PedirAsync(tipo);
            (await Conector().ProcessarProximaAsync()).Should().BeTrue(tipo.ToString());
        }
        // E o lote fora da janela (agendado de volta à fila) e a recusa por prazo.
        _b.Agora = new DateTime(2026, 11, 17, 10, 0, 0);
        await _b.Credenciais().DefinirEnvioAsync(Amil, true, TissBancoDeTeste.Operador);
        var dentro = await _b.Fila().EnfileirarEnvioAsync(Amil, [guiaAmil], TissBancoDeTeste.Operador);
        _b.Agora = new DateTime(2026, 12, 1, 8, 0, 0);
        _rede.ResponderSempre(HttpStatusCode.OK, Recusa3091());
        (await Conector().ProcessarProximaAsync()).Should().BeTrue();
        (await _b.NovoContexto().TransacoesTiss.AsNoTracking().SingleAsync(t => t.Id == dentro.Id)).Resultado.Should().StartWith(FilaTiss.PrefixoAgendado);

        // ---- a comparação ----
        foreach (var (papel, arquivo) in new[] { ("tiss_operacao", "papel-tiss-operacao.sql"), ("tiss_conector", "papel-tiss-conector.sql") })
        {
            var concedido = Concedido(arquivo, papel);
            var usado = _usado[papel];
            usado.Should().NotBeEmpty();
            string.Join(", ", usado.Except(concedido).OrderBy(x => x)).Should().BeEmpty(
                $"{arquivo}: o que o código usa e o papel não pode (seria o 42501 em produção)");
            string.Join(", ", concedido.Except(usado).OrderBy(x => x)).Should().BeEmpty(
                $"{arquivo}: o que o papel pode e nenhum caminho usa (não é o mínimo)");
        }
    }

    // ==================== a medida ====================

    /// <summary>As tabelas e as operações de um SQL do EF (SQLite): o que um GRANT precisa cobrir.</summary>
    public static IEnumerable<(string Tabela, string Operacao)> Operacoes(string sql)
    {
        foreach (Match m in Regex.Matches(sql, @"INSERT INTO ""(\w+)""")) yield return (m.Groups[1].Value, "INSERT");
        foreach (Match m in Regex.Matches(sql, @"UPDATE ""(\w+)""")) yield return (m.Groups[1].Value, "UPDATE");
        foreach (Match m in Regex.Matches(sql, @"DELETE FROM ""(\w+)""")) yield return (m.Groups[1].Value, "DELETE");
        foreach (Match m in Regex.Matches(sql, @"(?<!DELETE )(?:FROM|JOIN) ""(\w+)""")) yield return (m.Groups[1].Value, "SELECT");
        // UPDATE … WHERE "coluna" = … também LÊ a tabela no Postgres (precisa de SELECT nas colunas do WHERE).
        foreach (Match m in Regex.Matches(sql, @"UPDATE ""(\w+)""[^;]*?\bWHERE\b")) yield return (m.Groups[1].Value, "SELECT");
    }

    /// <summary>Os GRANTs de tabela inteira do arquivo SQL para o papel (os de coluna, só para o RETURNING, ficam de fora).</summary>
    private static HashSet<(string Tabela, string Operacao)> Concedido(string arquivo, string papel)
    {
        var sql = File.ReadAllText(Path.Combine(Raiz(), "deploy", "tiss-conector", arquivo));
        sql = Regex.Replace(sql, @"--[^\n]*", string.Empty);
        var concedido = new HashSet<(string, string)>();
        foreach (Match m in Regex.Matches(sql, $@"GRANT ([A-Z, ]+) ON public\.""(\w+)"" TO {papel}"))
            foreach (var op in m.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                concedido.Add((m.Groups[2].Value, op));
        foreach (Match m in Regex.Matches(sql, @"\['(\w+)','([A-Z, ]+)'\]"))
            foreach (var op in m.Groups[2].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                concedido.Add((m.Groups[1].Value, op));
        return concedido;
    }

    /// <summary>Os comandos que o comando.sh roda com o conector.env (o usuário do serviço). Os outros vão pelo admin.env.</summary>
    private static HashSet<string> ComandosQueUsamOConectorEnv()
    {
        var script = File.ReadAllText(Path.Combine(Raiz(), "deploy", "tiss-conector", "comando.sh"));
        var bloco = script[script.IndexOf("case \"$1\" in", StringComparison.Ordinal)..];
        return Regex.Matches(bloco, @"^\s*([a-z|\-]+)\)\s*;;", RegexOptions.Multiline)
            .SelectMany(m => m.Groups[1].Value.Split('|')).ToHashSet();
    }

    private sealed class Captura(HashSet<(string, string)> destino) : DbCommandInterceptor
    {
        private void Anotar(DbCommand c) { foreach (var o in Operacoes(c.CommandText)) destino.Add(o); }
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r) { Anotar(c); return r; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken ct = default) { Anotar(c); return ValueTask.FromResult(r); }
        public override InterceptionResult<int> NonQueryExecuting(DbCommand c, CommandEventData e, InterceptionResult<int> r) { Anotar(c); return r; }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<int> r, CancellationToken ct = default) { Anotar(c); return ValueTask.FromResult(r); }
        public override InterceptionResult<object> ScalarExecuting(DbCommand c, CommandEventData e, InterceptionResult<object> r) { Anotar(c); return r; }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<object> r, CancellationToken ct = default) { Anotar(c); return ValueTask.FromResult(r); }
    }

    private ClinicaRepositorio RepoMedido(string papel) => new(_b.NovoContexto(new Captura(_usado[papel])));

    private IServiceProvider Servicos(string papel)
    {
        var repo = RepoMedido(papel);
        return new ServiceCollection()
            .AddSingleton<IClinicaRepositorio>(repo)
            .AddSingleton(_b.Credenciais(repo)).AddSingleton(_b.Fila(repo))
            .AddSingleton(new ParametrosService(repo, _b.Protecao))
            .AddSingleton(new TissWebserviceCliente(new HttpClient(_rede)))
            .AddSingleton(new RelogioDoConector(() => _b.Agora))
            .BuildServiceProvider();
    }

    private ExecutorDeTransacoesTiss Conector()
    {
        var repo = RepoMedido("tiss_conector");
        var credenciais = _b.Credenciais(repo);
        var parametros = new ParametrosService(repo, _b.Protecao);
        IProcessadorTransacaoTiss[] processadores =
        [
            new ProcessadorDeElegibilidade(repo, parametros, credenciais, () => _b.Agora),
            new ProcessadorDeEnvioDeGuia(repo, parametros, credenciais, () => _b.Agora),
            new ProcessadorDeStatusDeProtocolo(repo, parametros, credenciais, () => _b.Agora),
            new ProcessadorDeCancelamentoDeGuia(repo, parametros, credenciais, () => _b.Agora)
        ];
        return new ExecutorDeTransacoesTiss(_b.Fila(repo), credenciais, processadores, new TissWebserviceCliente(new HttpClient(_rede)),
            new OpcoesDoConector { EnvioHabilitado = true, Consumidor = "conector-de-teste" }, NullLogger<ExecutorDeTransacoesTiss>.Instance);
    }

    /// <summary>Um pedido de cada tipo, como a tela o faz (fora da medida): Petrobras, envio ligado.</summary>
    private async Task PedirAsync(TipoTransacaoTiss tipo)
    {
        var fila = _b.Fila();
        switch (tipo)
        {
            case TipoTransacaoTiss.Elegibilidade:
                await fila.EnfileirarElegibilidadeAsync(Petrobras, TissBancoDeTeste.CarteiraFicticia, null, null, TissBancoDeTeste.Operador);
                break;
            case TipoTransacaoTiss.EnvioGuia:
                await fila.EnfileirarEnvioAsync(Petrobras, [(await _b.GuiaAsync()).Guia.Id], TissBancoDeTeste.Operador);
                break;
            case TipoTransacaoTiss.StatusProtocolo:
                await fila.EnfileirarStatusProtocoloAsync(Petrobras, "123456", null, TissBancoDeTeste.Operador);
                break;
            case TipoTransacaoTiss.CancelamentoGuia:
                var (_, guia) = await _b.GuiaAsync();
                await fila.EnfileirarCancelamentoGuiaAsync(Petrobras, guia.Id, "G" + guia.Id, "123456", TissBancoDeTeste.Operador);
                break;
        }
    }

    private async Task<int> GuiaAmilConferidaAsync()
    {
        var (_, guia) = await _b.GuiaAsync(conferida: false, convenio: Convenio.Amil);
        var db = _b.NovoContexto();
        var codigo = await db.Codigos.SingleAsync(c => c.Id == guia.Id);
        var data = new DateOnly(2026, 11, 3);
        GuiaAnsNaBaixa.Aplicar(codigo, new DadosGuiaConsultaAns
        {
            Modelo = ModeloDaGuiaTiss.SpSadt, RegistroAns = "326305", NumeroCarteira = TissBancoDeTeste.CarteiraFicticia,
            NomeBeneficiario = "Paciente de Teste", CodigoPrestadorNaOperadora = Login, NomeContratado = "Clínica de Teste Ltda",
            Cnes = "9999999", NomeProfissional = "Dra. Ana de Teste", ConselhoProfissional = "06", NumeroConselho = "123456",
            UfConselho = "33", Cbos = "225105", IndicacaoAcidente = "9", RegimeAtendimento = "01", DataAtendimento = data,
            CodigoTabela = "22", TipoConsulta = "1", CodigoProcedimento = "10101012", ValorProcedimento = 139.00m,
            Senha = "SENHA0001", DataAutorizacao = data, CodigoValidacao = "123456", TipoAtendimento = "04",
            CaraterAtendimento = "1", DescricaoProcedimento = "Consulta em consultório"
        }, TissBancoDeTeste.Operador, _b.Agora);
        await db.SaveChangesAsync();
        return guia.Id;
    }

    private static string Recusa3091() => TissWsDeTeste.Resposta("protocoloRecebimentoWS", "PROTOCOLO_RECEBIMENTO",
        "<ans:recebimentoLote><ans:mensagemErro><ans:codigoGlosa>3091</ans:codigoGlosa><ans:descricaoGlosa>FORA DO PRAZO</ans:descricaoGlosa></ans:mensagemErro></ans:recebimentoLote>");

    private static string Raiz()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Clinica.sln"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("Clinica.sln não encontrado.");
    }

    /// <summary>A "operadora": responde sempre a mesma coisa (o desfecho não importa; o caminho, sim).</summary>
    private sealed class Rede : HttpMessageHandler
    {
        private HttpStatusCode _status = HttpStatusCode.InternalServerError;
        private string _corpo = "";

        public void ResponderSempre(HttpStatusCode status, string corpo = "") => (_status, _corpo) = (status, corpo);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(_status) { Content = new ByteArrayContent(Encoding.Latin1.GetBytes(_corpo)) });
    }
}
