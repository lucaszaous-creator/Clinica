using Clinica.Application.Servicos;

// Ferramenta de linha de comando para conferir uma carteira no web service TISS da Saúde
// Petrobras. Não toca o banco da clínica, não grava nada e não é chamada por app nenhum:
// existe para provar login, hash e formato da mensagem antes de a consulta entrar numa tela.
// A senha só vem de variável de ambiente e nunca é impressa. Ver README.md desta pasta.

const string VarLogin = "TISS_PETROBRAS_LOGIN";
const string VarSenha = "TISS_PETROBRAS_SENHA";
const string VarCodigo = "TISS_PETROBRAS_CODIGO_PRESTADOR";

if (args.Length == 0 || args[0] is "-h" or "--help" || args[0] != "elegibilidade")
{
    Console.WriteLine("""
        Uso:
          TissPetrobras elegibilidade --carteira NUMERO [--validade AAAA-MM-DD] [--simular] [--ocultar-nome]

        Variáveis de ambiente:
          TISS_PETROBRAS_LOGIN             login da conectividade do Portal TISS
          TISS_PETROBRAS_SENHA             senha da conectividade (em claro; vai em MD5 na mensagem)
          TISS_PETROBRAS_CODIGO_PRESTADOR  código do prestador na operadora (na Petrobras, o CNPJ só com dígitos)

          --simular       monta e mostra a mensagem (senha mascarada) e NÃO envia
          --ocultar-nome  não imprime o nome devolvido pela operadora (para colar o resultado num chamado)

        Não existe ambiente de homologação no manual da operadora: sem --simular, a consulta é em produção.
        """);
    return args.Length == 0 ? 2 : 0;
}

string? Opcao(string nome)
{
    var i = Array.IndexOf(args, nome);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

var simular = args.Contains("--simular");
var ocultarNome = args.Contains("--ocultar-nome");
var carteira = Opcao("--carteira");
if (string.IsNullOrWhiteSpace(carteira))
{
    Console.Error.WriteLine("Informe --carteira.");
    return 2;
}

DateOnly? validade = null;
if (Opcao("--validade") is { } textoValidade)
{
    if (!DateOnly.TryParseExact(textoValidade, "yyyy-MM-dd", out var lida))
    {
        Console.Error.WriteLine("--validade vai no formato AAAA-MM-DD.");
        return 2;
    }
    validade = lida;
}

var login = Environment.GetEnvironmentVariable(VarLogin);
var senha = Environment.GetEnvironmentVariable(VarSenha);
var codigo = Environment.GetEnvironmentVariable(VarCodigo);
if (simular)
{
    // A simulação não precisa de credencial de verdade: o que ela mostra é a forma da mensagem.
    login ??= "LOGIN";
    senha ??= "senha-de-exemplo";
    codigo ??= "00000000000000";
}
if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(senha) || string.IsNullOrWhiteSpace(codigo))
{
    Console.Error.WriteLine($"Defina {VarLogin}, {VarSenha} e {VarCodigo} (ou use --simular).");
    return 2;
}

var agora = DateTime.Now;
PedidoElegibilidadeTiss pedido;
try
{
    pedido = new PedidoElegibilidadeTiss
    {
        RegistroAnsOperadora = TissWebserviceCliente.RegistroAnsSaudePetrobras,
        CodigoPrestadorNaOperadora = new string(codigo.Where(char.IsLetterOrDigit).ToArray()),
        NumeroCarteira = carteira.Trim(),
        ValidadeCarteira = validade,
        Login = login.Trim(),
        SenhaMd5 = TissElegibilidadeWs.SenhaEmMd5(senha),
        // Dentro dos 12 caracteres do padrão: ano, mês, dia, hora, minuto e segundo. Duas consultas
        // no mesmo segundo repetiriam o número — a ferramenta faz uma por execução.
        Sequencial = agora.ToString("yyMMddHHmmss"),
        Momento = agora
    };
    TissElegibilidadeWs.MontarPedido(pedido);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine("Dados recusados antes do envio: " + ex.Message);
    return 2;
}

if (simular)
{
    Console.WriteLine("SIMULAÇÃO — nada foi enviado. Mensagem (senha mascarada):");
    Console.WriteLine(TissElegibilidadeWs.MontarEnvelopeParaExibir(pedido));
    return 0;
}

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
var resposta = await new TissWebserviceCliente(http)
    .VerificarElegibilidadeAsync(TissWebserviceCliente.ElegibilidadeSaudePetrobras, pedido);

Console.WriteLine($"Consulta de elegibilidade — Saúde Petrobras — {agora:dd/MM/yyyy HH:mm:ss} — sequencial {pedido.Sequencial}");
Console.WriteLine(resposta.Resumo);
if (resposta.NomeBeneficiario is not null)
    Console.WriteLine("Beneficiário: " + (ocultarNome ? "(devolvido pela operadora; oculto)" : resposta.NomeBeneficiario));
if (resposta.ValidadeCarteira is { } v)
    Console.WriteLine($"Validade da carteira: {v:dd/MM/yyyy}");

// 0 = elegível; 1 = não elegível; 3 = a operadora não avaliou (login, hash, schema, rede).
return resposta.Elegivel switch { true => 0, false => 1, null => 3 };
