using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Tiss.Conector;

/// <summary>
/// Os comandos de quem opera o servidor. Existem porque a credencial só pode ser cifrada onde
/// a chave de cifra está — e ela está só aqui, no servidor, fora do banco.
///
/// A senha vem de variável de ambiente ou da entrada padrão; nunca de argumento (argumento
/// aparece na lista de processos e no histórico do terminal) e nunca é impressa.
/// </summary>
public static class ComandosDoConector
{
    public const string VariavelLogin = "TISS_WS_LOGIN";
    public const string VariavelSenha = "TISS_WS_SENHA";

    public const string Ajuda = """
        Uso (sem argumentos, roda o serviço):
          Clinica.Tiss.Conector credencial CONVENIO --operador NOME [--senha-na-entrada]
              grava o login (TISS_WS_LOGIN) e a senha (TISS_WS_SENHA, ou a primeira linha da entrada)
          Clinica.Tiss.Conector credencial CONVENIO --remover --operador NOME
          Clinica.Tiss.Conector envio CONVENIO ligar|desligar --operador NOME
          Clinica.Tiss.Conector endereco CONVENIO elegibilidade|envio|status|cancelamento https://… --operador NOME
          Clinica.Tiss.Conector situacao [CONVENIO]

        CONVENIO é o código do convênio no cadastro (ex.: Petrobras). NOME é quem está fazendo a mudança: vai para a trilha.
        """;

    public static async Task<int> ExecutarAsync(string[] args, IServiceProvider servicos, TextWriter saida, TextWriter erro,
        TextReader? entrada = null, Func<string, string?>? ambiente = null)
    {
        ambiente ??= Environment.GetEnvironmentVariable;
        var credenciais = servicos.GetRequiredService<CredenciaisTissService>();
        try
        {
            switch (args[0])
            {
                case "credencial" when args.Length >= 2:
                {
                    var convenio = args[1];
                    var operador = Opcao(args, "--operador");
                    if (args.Contains("--remover"))
                    {
                        await credenciais.RemoverAsync(convenio, operador ?? "", default);
                        saida.WriteLine($"Credencial de {convenio} removida.");
                        return 0;
                    }
                    var login = ambiente(VariavelLogin);
                    var senha = args.Contains("--senha-na-entrada") ? LerSenha(entrada, erro) : ambiente(VariavelSenha);
                    if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(senha))
                    {
                        erro.WriteLine($"Defina {VariavelLogin} e {VariavelSenha} (ou use --senha-na-entrada).");
                        return 2;
                    }
                    await credenciais.SalvarAsync(convenio, login, senha, operador ?? "", default);
                    saida.WriteLine($"Credencial de {convenio} gravada (login {login.Trim()}; a senha não é mostrada).");
                    return 0;
                }
                case "envio" when args.Length >= 3 && args[2] is "ligar" or "desligar":
                {
                    var ligar = args[2] == "ligar";
                    await credenciais.DefinirEnvioAsync(args[1], ligar, Opcao(args, "--operador") ?? "", default);
                    saida.WriteLine($"Envio pelo web service {(ligar ? "LIGADO" : "desligado")} para {args[1]}."
                                    + (ligar ? " Ele ainda depende de TISS_ENVIO_HABILITADO=true no ambiente do serviço." : ""));
                    return 0;
                }
                case "endereco" when args.Length >= 4 && Tipo(args[2]) is { } tipo:
                    await credenciais.DefinirEnderecoAsync(args[1], tipo, args[3], Opcao(args, "--operador") ?? "", default);
                    saida.WriteLine($"Endereço de {tipo} gravado para {args[1]}.");
                    return 0;
                case "situacao":
                {
                    var fila = servicos.GetRequiredService<FilaTissService>();
                    var convenio = args.Length >= 2 ? args[1] : null;
                    if (convenio is not null)
                    {
                        var c = await credenciais.ParaATelaAsync(convenio);
                        saida.WriteLine($"{convenio}: login {(c.Login is null ? "não cadastrado" : "cadastrado")}, senha {(c.TemSenha ? "cadastrada" : "não cadastrada")}, "
                                        + $"envio {(await credenciais.EnvioLigadoAsync(convenio) ? "LIGADO" : "desligado")}.");
                    }
                    // Só o que não identifica paciente: número, tipo, convênio, estado e tentativas.
                    foreach (var t in await fila.RecentesAsync(convenio, 20))
                        saida.WriteLine($"{t.Id,6}  {t.PedidoEm:dd/MM HH:mm}  {t.Tipo,-16}  {t.ConvenioCodigo,-14}  {t.Estado,-9}  tentativas {t.Tentativas}");
                    return 0;
                }
                default:
                    (args[0] is "-h" or "--help" or "ajuda" ? saida : erro).WriteLine(Ajuda);
                    return args[0] is "-h" or "--help" or "ajuda" ? 0 : 2;
            }
        }
        catch (InvalidOperationException ex)
        {
            // Recusa de regra (falta operador, endereço sem https, chave de cifra ausente): a frase basta.
            erro.WriteLine(ex.Message);
            return 1;
        }
    }

    /// <summary>
    /// A senha pela entrada padrão. Num terminal de verdade as teclas NÃO são ecoadas (senão a senha
    /// ficaria na tela e na rolagem); com a entrada redirecionada (um cano, um teste) lê-se a linha.
    /// </summary>
    private static string? LerSenha(TextReader? entrada, TextWriter erro)
    {
        if (entrada is not null) return entrada.ReadLine();
        if (Console.IsInputRedirected) return Console.In.ReadLine();

        erro.Write("Senha do web service (não aparece ao digitar): ");
        var senha = new System.Text.StringBuilder();
        while (true)
        {
            var tecla = Console.ReadKey(intercept: true);
            if (tecla.Key == ConsoleKey.Enter) break;
            if (tecla.Key == ConsoleKey.Backspace) { if (senha.Length > 0) senha.Length--; continue; }
            if (!char.IsControl(tecla.KeyChar)) senha.Append(tecla.KeyChar);
        }
        erro.WriteLine();
        return senha.ToString();
    }

    private static string? Opcao(string[] args, string nome)
    {
        var i = Array.IndexOf(args, nome);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static TipoTransacaoTiss? Tipo(string texto) => texto switch
    {
        "elegibilidade" => TipoTransacaoTiss.Elegibilidade,
        "envio" => TipoTransacaoTiss.EnvioGuia,
        "status" => TipoTransacaoTiss.StatusProtocolo,
        "cancelamento" => TipoTransacaoTiss.CancelamentoGuia,
        _ => null
    };
}
