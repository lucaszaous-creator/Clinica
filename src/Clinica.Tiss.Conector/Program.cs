using Clinica.Application.Servicos;
using Clinica.Infrastructure;
using Clinica.Tiss.Conector;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// O conector do web service TISS (nov/2026). Roda no servidor, ao lado do banco da clínica:
// lê a fila TransacoesTiss, fala com a operadora e grava o resultado. Não escuta em porta
// nenhuma — só faz conexões de SAÍDA (banco local e HTTPS da operadora). Não cria nem migra
// banco. Roteiro de instalação e operação: docs/tiss-conector.md.
//
// Sem argumentos: o serviço. Com argumentos: os comandos de quem opera (credencial, envio,
// endereco, situacao) — ver ComandosDoConector.

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSystemd();

var conexao = builder.Configuration.GetConnectionString("Clinica");
if (string.IsNullOrWhiteSpace(conexao))
    throw new InvalidOperationException("Configure a conexão da clínica (ConnectionStrings__Clinica). O conector não cria nem migra bancos.");

builder.Services.AddClinica(conexao);
builder.Services.AddConectorTiss(OpcoesDoConector.DoAmbiente(builder.Configuration));
// O transporte é um só no sistema: o mesmo TissWebserviceCliente da ferramenta de diagnóstico.
builder.Services.AddHttpClient<TissWebserviceCliente>(http => http.Timeout = TimeSpan.FromSeconds(60));

// O SQL do EF em nível de informação imprimiria parâmetros de consulta no journal.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Critical);
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

if (args.Length > 0)
{
    using var anfitriao = builder.Build();
    using var escopo = anfitriao.Services.CreateScope();
    return await ComandosDoConector.ExecutarAsync(args, escopo.ServiceProvider, Console.Out, Console.Error);
}

// A chave de cifra é conferida NA PARTIDA: sem ela o serviço não abriria credencial nenhuma, e
// é melhor não subir do que subir recusando tudo.
_ = ProtecaoSegredoGlobal.DoAmbiente();
builder.Services.AddHostedService<ServicoDoConector>();
await builder.Build().RunAsync();
return 0;
