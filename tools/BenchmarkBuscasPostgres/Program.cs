using System.Data.Common;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

// Nunca lê configuração do aplicativo. A conexão administrativa tem de ser explícita
// e local. Cada execução CRIA um banco novo; não apaga nem reutiliza bancos existentes.
var entrada = Environment.GetEnvironmentVariable("CLINICA_BENCH_POSTGRES")
    ?? throw new InvalidOperationException("Informe CLINICA_BENCH_POSTGRES para o PostgreSQL isolado.");
var admin = new NpgsqlConnectionStringBuilder(entrada);
if (admin.Host != "127.0.0.1" || admin.Database != "postgres" || admin.Username != "bench_clinica" || admin.Port != 55439)
    throw new InvalidOperationException("Use o cluster sintético em 127.0.0.1:55439, usuário bench_clinica, banco administrativo postgres.");
var reutilizar = Environment.GetEnvironmentVariable("CLINICA_BENCH_REUTILIZAR");
if(reutilizar is not null && !System.Text.RegularExpressions.Regex.IsMatch(reutilizar,"^bench_clinica_[a-f0-9]{32}$")) throw new Exception("Nome de banco sintético inválido.");
var nomeBanco = reutilizar ?? "bench_clinica_" + Guid.NewGuid().ToString("N");
if(reutilizar is null)
await using (var c = new NpgsqlConnection(admin.ConnectionString))
{
    await c.OpenAsync();
    await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{nomeBanco}\"", c);
    await cmd.ExecuteNonQueryAsync();
}
var cadeia = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = nomeBanco, CommandTimeout = 180 }.ConnectionString;
var saida = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/buscas-postgres");
Directory.CreateDirectory(saida);
const int pacientes = 50000, registros = 200000, repeticoes = 15;
var data = new DateOnly(2026, 10, 14);
var coleta = new ColetaSql();
ClinicaDbContext Contexto(bool coletar = false) => new(new DbContextOptionsBuilder<ClinicaDbContext>()
    .UseNpgsql(cadeia).AddInterceptors(coletar ? [coleta] : []).Options);
async Task Sql(string sql)
{
    await using var c = new NpgsqlConnection(cadeia); await c.OpenAsync();
    await using var cmd = new NpgsqlCommand(sql, c) { CommandTimeout = 180 };
    await cmd.ExecuteNonQueryAsync();
}
Console.WriteLine($"Banco exclusivo: {nomeBanco}; migrações reais e dados fictícios.");
if(reutilizar is null)
{
await using (var db = Contexto())
{
    await db.Database.MigrateAsync();
    var prof = new Profissional { Nome = "Profissional fictício", Ativo = true };
    var p = new Paciente { Nome = "Zefirina Quitéria Fictícia", Documento = "00000000001" };
    db.AddRange(prof, p); await db.SaveChangesAsync();
    if (p.Id != 1) throw new InvalidOperationException("Banco sintético inesperadamente contém pacientes.");
    db.AddRange(new DocumentoClinico { PacienteId = 1, ProfissionalId = prof.Id, Numero = "B1", CodigoVerificacao = "B1", Tipo = TipoDocumentoClinico.Receita, Data = data, Corpo = new string('x', 400) },
        new PrescricaoInterna { PacienteId = 1, ProfissionalId = prof.Id, Numero = "B1", CodigoVerificacao = "B1", Data = data, Hora = new TimeOnly(8, 0), Situacao = SituacaoPrescricao.Liberada, Indicacao = "Conteúdo fictício para benchmark" },
        new Evolucao { PacienteId = 1, ProfissionalId = prof.Id, Data = data, QueixaPrincipal = "Conteúdo fictício", Conduta = new string('x', 400) },
        new Agendamento { PacienteId = 1, DataHora = data.ToDateTime(new TimeOnly(8, 0)), Status = StatusAgendamento.Realizado });
    await db.SaveChangesAsync();
    db.AddRange(new ItemDocumento { DocumentoClinicoId = 1, Ordem = 1, Descricao = "Item fictício", Detalhe = new string('x', 200) },
        new ItemPrescricaoInterna { PrescricaoInternaId = 1, Ordem = 1, Descricao = "Item fictício", Dose = "Dose fictícia" });
    await db.SaveChangesAsync();
}

// A linha-modelo vem do próprio EF e preserva todos os defaults/colunas das migrações.
async Task Clonar(string tabela, int total, string campos)
{
    var colunas = new List<string>();
    await using (var c = new NpgsqlConnection(cadeia))
    {
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name=@t ORDER BY ordinal_position", c);
        cmd.Parameters.AddWithValue("t", tabela);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) colunas.Add('"' + r.GetString(0).Replace("\"", "\"\"") + '"');
    }
    var cols = string.Join(',', colunas);
    var valores = string.Join(',', colunas.Select(c => "(novo.r)." + c));
    await Sql($"WITH novo AS MATERIALIZED (SELECT jsonb_populate_record(NULL::\"{tabela}\", to_jsonb(t) || jsonb_build_object('Id',g,{campos})) r FROM \"{tabela}\" t CROSS JOIN generate_series(2,{total}) g WHERE t.\"Id\"=1) INSERT INTO \"{tabela}\" ({cols}) SELECT {valores} FROM novo;");
    Console.WriteLine($"Gerados {total:N0}: {tabela}.");
}
await Clonar("Pacientes", pacientes, "'Nome',(CASE WHEN g%97=0 THEN 'José Ávila' WHEN g%13=0 THEN 'Maria Oliveira' WHEN g%2=0 THEN 'Ana Silva' ELSE 'João Santos' END)||' Fictício '||lpad(g::text,6,'0'),'Documento',CASE WHEN g%17=0 THEN NULL ELSE lpad(g::text,11,'0') END");
string dono = $"CASE WHEN g<=3000 THEN 1 ELSE 2+((g*7919)%{pacientes - 1}) END";
string dia = "to_char(DATE '2026-10-14' - (g%1500)::int,'YYYY-MM-DD')";
await Clonar("DocumentosClinicos", registros, $"'PacienteId',{dono},'Data',{dia},'Numero','B'||g,'CodigoVerificacao','B'||g,'Tipo',CASE WHEN g%4=0 THEN 'TermoProcedimento' WHEN g%4=1 THEN 'PedidoExame' WHEN g%4=2 THEN 'Atestado' ELSE 'Receita' END");
await Clonar("PrescricoesInternas", registros, $"'PacienteId',{dono},'Data',{dia},'Hora',to_char(TIME '08:00'+(g%600)*INTERVAL '1 minute','HH24:MI:SS'),'Numero','B'||g,'CodigoVerificacao','B'||g,'Situacao',CASE WHEN g%5=0 THEN 'Rascunho' ELSE 'Liberada' END,'OrigemEnfermagem',g%7=0");
await Clonar("Evolucoes", registros, $"'PacienteId',{dono},'Data',{dia}");
await Clonar("Agendamentos", registros, $"'PacienteId',{dono},'DataHora',to_char(TIMESTAMP '2026-10-14 08:00'-(g%1500)*INTERVAL '1 day'+(g%600)*INTERVAL '1 minute','YYYY-MM-DD HH24:MI:SS'),'Status',CASE WHEN g%3=0 THEN 'Agendado' ELSE 'Realizado' END");
await Clonar("ItensDocumento", registros, "'DocumentoClinicoId',g");
await Clonar("ItensPrescricaoInterna", registros, "'PrescricaoInternaId',g");
await Sql("ANALYZE;");
}
await using(var db=Contexto())
{
    if(await db.Pacientes.CountAsync()!=pacientes || await db.DocumentosClinicos.CountAsync()!=registros || await db.Pacientes.Where(p=>p.Id==1).Select(p=>p.Nome).SingleAsync()!="Zefirina Quitéria Fictícia") throw new Exception("Fixture sintético não confere.");
}
await Sql("DO $$ DECLARE i record; BEGIN FOR i IN SELECT indexname FROM pg_indexes WHERE schemaname='public' AND indexname LIKE 'bench_%' LOOP EXECUTE format('DROP INDEX %I',i.indexname); END LOOP; END $$; ANALYZE;");

var cenarios = new List<Cenario>();
void Paciente(string nome, string? termo, int pagina = 0, FiltroListaPacientes filtro = FiltroListaPacientes.Todos)
    => cenarios.Add(new(nome, async repo => (await repo.BuscarPaginaPacientesAsync(termo, pagina, 101, filtro)).Select(p => p.Id).ToArray()));
Paciente("nome-raro", "zefirina"); Paciente("nome-comum", "silva"); Paciente("nome-com-acento", "josé");
Paciente("nome-sem-acento-sem-correspondencia", "jose"); Paciente("um-caractere", "a"); Paciente("dois-caracteres", "jo");
Paciente("tres-caracteres", "ana"); Paciente("inexistente", "nomeinexistente"); Paciente("lista-inicial", null);
Paciente("pagina-1000", null, 1000); Paciente("cpf-inteiro", "00000012345"); Paciente("cpf-parcial", "123");
Paciente("cpf-curto", "12"); Paciente("caractere-coringa-literal", "%");
Paciente("em-tratamento", "silva", 0, FiltroListaPacientes.EmTratamento);
Paciente("cadastro-incompleto", "silva", 0, FiltroListaPacientes.CadastroIncompleto);
foreach (int paciente in new[] { 1, 10001, pacientes + 1 })
{
    cenarios.Add(new($"documentos-paciente-{paciente}", async repo => (await repo.DocumentosDoPacienteAsync(paciente)).Select(d => d.Id).ToArray()));
    cenarios.Add(new($"receitas-paciente-{paciente}", async repo => (await repo.DocumentosClinicosNoPeriodoAsync(data.AddYears(-5), data, TipoDocumentoClinico.Receita, paciente)).Select(d => d.Id).ToArray()));
    cenarios.Add(new($"prescricoes-paciente-{paciente}", async repo => (await repo.PrescricoesInternasDoPacienteAsync(paciente)).Select(d => d.Id).ToArray()));
    cenarios.Add(new($"ultima-prescricao-{paciente}", async repo => (await repo.UltimaPrescricaoEmitidaDoPacienteAsync(paciente)) is { } d ? [d.Id] : []));
    cenarios.Add(new($"ficha-sessoes-{paciente}", async repo => (await repo.SessoesNaFichaAsync(paciente, 0, 51)).Select(d => d.AgendamentoId).ToArray()));
    cenarios.Add(new($"evolucoes-paciente-{paciente}", async repo => (await repo.EvolucoesDoPacienteAsync(paciente)).Select(d => d.Id).ToArray()));
    cenarios.Add(new($"termos-paciente-{paciente}", async repo => (await repo.TermosDoPacienteAsync(paciente)).Select(d => d.Id).ToArray()));
    cenarios.Add(new($"exames-paciente-{paciente}", async repo => (await repo.PedidosDeExameAsync(pacienteId: paciente)).Select(d => d.DocumentoId).ToArray()));
}

var resultados = new List<Medida>();
var assinaturas = new Dictionary<string, string>();
var custos = new List<object>();
var sqlRegistrados = new Dictionary<string, Comando[]>();
async Task Rodada(string fase)
{
    // Ordem determinística alternada para não beneficiar sempre o último cenário.
    foreach (var cenario in (fase.EndsWith('2') ? cenarios.AsEnumerable().Reverse() : cenarios))
    {
        var tempos = new List<double>(); int quantidade = 0; string assinatura = "";
        for (int i = -2; i < repeticoes; i++)
        {
            coleta.Comandos.Clear();
            await using var db = Contexto(i == 0);
            await db.Database.OpenConnectionAsync(); // Pool/rede fora desta medição de consulta + materialização.
            var relogio = Stopwatch.StartNew();
            var ids = await cenario.Executar(new ClinicaRepositorio(db));
            relogio.Stop();
            quantidade = ids.Length;
            assinatura = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(',', ids))));
            if (i >= 0) tempos.Add(relogio.Elapsed.TotalMilliseconds);
            if (i == 0) sqlRegistrados[cenario.Nome] = coleta.Comandos.ToArray();
            if (assinaturas.TryGetValue(cenario.Nome, out var anterior) && anterior != assinatura)
                throw new InvalidOperationException("Resultado/ordem mudou: " + cenario.Nome);
            assinaturas[cenario.Nome] = assinatura;
        }
        var planos = new List<JsonElement>();
        foreach (var original in sqlRegistrados[cenario.Nome])
        {
            await using var conexao = new NpgsqlConnection(cadeia); await conexao.OpenAsync();
            await using var cmd = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + original.Sql, conexao);
            foreach (var parametro in original.Parametros) cmd.Parameters.Add(parametro.Clone());
            using var json = JsonDocument.Parse((string)(await cmd.ExecuteScalarAsync())!);
            planos.Add(json.RootElement.Clone());
        }
        await File.WriteAllTextAsync(Path.Combine(saida, $"{fase}-{cenario.Nome}-plano.json"), JsonSerializer.Serialize(planos, JsonBonito.Opcoes));
        if (fase == "antes") await File.WriteAllTextAsync(Path.Combine(saida, cenario.Nome + ".sql"), string.Join("\n\n", sqlRegistrados[cenario.Nome].Select(c => c.Sql)));
        tempos.Sort();
        resultados.Add(new(fase, cenario.Nome, quantidade, assinatura, tempos[tempos.Count/2], tempos[(int)Math.Ceiling(tempos.Count*.95)-1], tempos.ToArray(), planos.Sum(p => p[0].GetProperty("Execution Time").GetDouble())));
        Console.WriteLine($"{fase,-12} {cenario.Nome,-45} p50={tempos[tempos.Count/2]:F2} ms ({quantidade} linhas)");
    }
    // Escrita sintética com rollback; mesmas 1000 linhas novas em todas as fases.
    var escrita = new List<double>();
    for (int i = -1; i < 7; i++)
    {
        await using var c = new NpgsqlConnection(cadeia); await c.OpenAsync();
        await using var tx = await c.BeginTransactionAsync();
        await using var cmd = new NpgsqlCommand("WITH novos AS MATERIALIZED (SELECT jsonb_populate_record(NULL::\"Pacientes\",to_jsonb(p)||jsonb_build_object('Id',1000000+g,'Nome','Paciente teste escrita '||g,'Documento',lpad(g::text,11,'0'))) r FROM \"Pacientes\" p CROSS JOIN generate_series(1,1000) g WHERE p.\"Id\"=1) INSERT INTO \"Pacientes\" SELECT (r).* FROM novos", c, tx);
        var cron = Stopwatch.StartNew(); await cmd.ExecuteNonQueryAsync(); cron.Stop();
        await tx.RollbackAsync(); if(i>=0) escrita.Add(cron.Elapsed.TotalMilliseconds);
    }
    escrita.Sort();
    await Sql("VACUUM ANALYZE \"Pacientes\";");
    await using (var c = new NpgsqlConnection(cadeia))
    {
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT indexrelname, pg_relation_size(indexrelid) FROM pg_stat_user_indexes WHERE indexrelname LIKE 'bench_%' ORDER BY indexrelname", c);
        await using var r = await cmd.ExecuteReaderAsync(); var indices = new Dictionary<string,long>();
        while (await r.ReadAsync()) indices[r.GetString(0)] = r.GetInt64(1);
        custos.Add(new { fase, escrita1000P50Ms = escrita[escrita.Count/2], indices });
    }
    await Salvar();
}
async Task Salvar()
{
    await File.WriteAllTextAsync(Path.Combine(saida,"resultado.json"), JsonSerializer.Serialize(new {
        bancoSintetico=nomeBanco, pacientes, registrosPorTabela=registros, repeticoes, aquecimentos=2, somenteSelecionados=args.Contains("--selecionados"),
        limite="Dados fictícios; loopback, cache aquecido, sem medir rede/VPS ou tela Windows. Sem alteração em produção.", resultados, custos
    }, JsonBonito.Opcoes));
}
await Rodada("antes");
var cronIndice = Stopwatch.StartNew();
await Sql(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"indices-texto.sql")));
Console.WriteLine($"Criação de índices de texto: {cronIndice.Elapsed.TotalSeconds:F2}s");
await Sql("ANALYZE;"); await Rodada("texto");
cronIndice.Restart();
await Sql(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,args.Contains("--selecionados") ? "indices-selecionados.sql" : "indices-historico.sql")));
Console.WriteLine($"Criação de índices compostos: {cronIndice.Elapsed.TotalSeconds:F2}s");
await Sql("ANALYZE;"); await Rodada("completo");
// Segunda rodada sem recriar dados, para revelar variação e efeitos do cache.
await Rodada("completo2");
await Sql("DO $$ DECLARE i record; BEGIN FOR i IN SELECT indexname FROM pg_indexes WHERE schemaname='public' AND indexname LIKE 'bench_%' LOOP EXECUTE format('DROP INDEX %I', i.indexname); END LOOP; END $$; ANALYZE;");
await Rodada("antes2");
Console.WriteLine("APROVADO: mesmas linhas e mesma ordem em todas as fases. Banco sintético mantido para inspeção local.");

record Cenario(string Nome, Func<ClinicaRepositorio,Task<int[]>> Executar);
record Medida(string Fase,string Cenario,int Linhas,string Assinatura,double MedianaMs,double P95Ms,double[] AmostrasMs,double ExecucaoSqlMs);
record Comando(string Sql,NpgsqlParameter[] Parametros);
static class JsonBonito { public static readonly JsonSerializerOptions Opcoes = new() { WriteIndented=true }; }
sealed class ColetaSql : DbCommandInterceptor
{
    public List<Comando> Comandos { get; }=[];
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,InterceptionResult<DbDataReader> result,CancellationToken cancellationToken=default)
    {
        Comandos.Add(new(command.CommandText,command.Parameters.Cast<NpgsqlParameter>().Select(p=>p.Clone()).ToArray()));
        return ValueTask.FromResult(result);
    }
}
