using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Clinica.Application.Servicos;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// Manutenção pontual: nenhuma rota HTTP, tela, migration ou rotina periódica.
// A conexão fica no ambiente do processo, nunca em argumentos ou relatórios.
var json = new JsonSerializerOptions { WriteIndented = true };
try
{
    var cs = Environment.GetEnvironmentVariable("CLINICA_DB")
        ?? throw new InvalidOperationException("Configure CLINICA_DB no processo administrativo.");
    var connection = new NpgsqlConnectionStringBuilder(cs);
    var banco = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{connection.Host}:{connection.Port}/{connection.Database}")));
    await using var db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseNpgsql(cs).Options);
    var repo = new ClinicaRepositorio(db);
    await new ConvenioCatalogoService(repo).RecarregarCacheAsync();
    await new ModalidadeCatalogoService(repo).RecarregarCacheAsync();
    await new EspecialidadeCatalogoService(repo).RecarregarCacheAsync();
    var parametros = new ParametrosService(repo);
    var atendimento = new AtendimentoService(repo, parametros: parametros, consultas: new(repo, parametros));
    var svc = new RecuperacaoGuiasService(db, repo, new(repo, atendimento, parametros), atendimento);

    if (args.Length == 5 && args[0] == "prever")
    {
        var login = args[1];
        var gerente = await db.Usuarios.AsNoTracking().Where(u => u.Login == login).Select(u => u.Id).SingleAsync();
        var inicio = DateOnly.ParseExact(args[2], "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var fim = DateOnly.ParseExact(args[3], "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var itens = new List<Entrada>();
        // Materializa TODO o plano antes da primeira escrita: não pula sessões quando a lista diminui.
        for (var pagina = 0; ; pagina++)
        {
            var previa = await svc.PreverAsync(gerente, new(inicio, fim, pagina));
            itens.AddRange(previa.Itens.Select(i => new Entrada(i.Id, i.EvolucaoId, i.Versao, i.PodeRecuperar, i.Situacao, i.Escrita)));
            db.ChangeTracker.Clear();
            if (!previa.Mais) break;
        }
        var plano = new Plano(banco, gerente, DateTimeOffset.UtcNow, inicio, fim, itens.ToArray());
        await GravarNovo(args[4], plano);
        Console.WriteLine(JsonSerializer.Serialize(new { Candidatas = itens.Count, Aptas = itens.Count(i => i.Apta),
            AptasSemEscrita = itens.Count(i => i.Apta && !i.Escrita),
            Pendencias = itens.Where(i => !i.Apta).GroupBy(i => i.Situacao).Select(g => new { Motivo = g.Key, Quantidade = g.Count() }) }, json));
    }
    else if (args.Length == 3 && args[0] == "aplicar")
    {
        var plano = JsonSerializer.Deserialize<Plano>(await File.ReadAllTextAsync(args[1]))!;
        if (plano.Banco != banco || plano.CriadoEm < DateTimeOffset.UtcNow.AddHours(-6) || plano.CriadoEm > DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Plano de outro banco ou expirado; gere a prévia novamente.");
        if (plano.Itens.Select(i => i.Id).Distinct().Count() != plano.Itens.Length)
            throw new InvalidOperationException("Plano contém sessões repetidas.");
        var backup = JsonSerializer.Deserialize<Backup>(await File.ReadAllTextAsync(args[2]))!;
        if (backup.Banco != banco || backup.CriadoEm < plano.CriadoEm || backup.CriadoEm > DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Faça backup deste banco após gerar o plano.");
        await using (var arquivo = File.OpenRead(backup.Arquivo))
            if (arquivo.Length == 0 || Convert.ToHexString(await SHA256.HashDataAsync(arquivo)) != backup.Sha256)
                throw new InvalidOperationException("Backup não confere.");
        var saida = args[1] + ".resultado-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".jsonl";
        using var stream = NovoArquivo(saida);
        await using var writer = new StreamWriter(stream);
        var sucesso = 0; var falha = 0; var guias = 0;
        foreach (var lote in plano.Itens.Where(i => i.Apta).Chunk(50))
        {
            var resultados = await svc.RecuperarLoteAsync(plano.GerenteId, lote.Select(i => new PedidoRecuperacaoGuia(i.Id, i.Versao)).ToArray());
            foreach (var r in resultados)
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(r)); await writer.FlushAsync();
                if (r.Sucesso) sucesso++; else falha++;
                guias += r.Guias;
            }
            Console.WriteLine(JsonSerializer.Serialize(new { Processadas = sucesso + falha, Sucesso = sucesso, Falhas = falha, GuiasCriadas = guias }));
        }
        if (falha > 0) Environment.ExitCode = 2;
    }
    else throw new InvalidOperationException("Uso: prever LOGIN INICIO FIM plano.json | aplicar plano.json backup.json. Datas yyyy-MM-dd.");
}
catch (Exception ex)
{
    // Não imprime exceções de conexão/SQL que possam conter credenciais ou dados clínicos.
    Console.Error.WriteLine(ex is InvalidOperationException ? ex.Message : $"Operação interrompida ({ex.GetType().Name}). Confira a configuração e os registros administrativos.");
    Environment.ExitCode = 1;
}

FileStream NovoArquivo(string path)
{
    var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
    if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    return new FileStream(path, options);
}
async Task GravarNovo<T>(string path, T value)
{
    await using var stream = NovoArquivo(path);
    await JsonSerializer.SerializeAsync(stream, value, json);
}
record Entrada(int Id, int? EvolucaoId, string Versao, bool Apta, string Situacao, bool Escrita);
record Plano(string Banco, int GerenteId, DateTimeOffset CriadoEm, DateOnly Inicio, DateOnly Fim, Entrada[] Itens);
record Backup(string Banco, DateTimeOffset CriadoEm, string Arquivo, string Sha256);
