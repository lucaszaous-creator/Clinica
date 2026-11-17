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
          Clinica.Tiss.Conector endereco CONVENIO elegibilidade|envio|status|cancelamento|todos https://… --operador NOME
          Clinica.Tiss.Conector codigo CONVENIO cnpj|login|CODIGO|padrao --operador NOME
          Clinica.Tiss.Conector situacao [CONVENIO]
          Clinica.Tiss.Conector cancelar ID --operador NOME
              tira da fila uma transação ainda Pendente (ela não vai mais à operadora)
          Clinica.Tiss.Conector ambiente CONVENIO homologacao|producao --operador NOME
              aponta TODOS os serviços do convênio para a homologação da operadora (ou de volta)
          Clinica.Tiss.Conector testar-elegibilidade CONVENIO --carteira N [--operador NOME]
          Clinica.Tiss.Conector testar-lote CONVENIO --guias ID[,ID...] --operador NOME
          Clinica.Tiss.Conector testar-lote CONVENIO --exemplo --carteira N [--senha S] [--token T] [--procedimento TUSS] [--valor 1,00] --operador NOME
          Clinica.Tiss.Conector testar-protocolo CONVENIO --protocolo N --operador NOME
              os testes só rodam com o convênio em HOMOLOGAÇÃO, fazem UMA ida e não gravam nada nas
              guias nem na fila (só uma linha na trilha). [--soap-action VALOR] muda o SOAPAction.
              Fora da janela de envio da operadora, testar-lote avisa e não chama; --mesmo-assim chama.

        CONVENIO é o código do convênio no cadastro (ex.: Petrobras, Amil). NOME é quem está fazendo a mudança: vai para a trilha.
        "todos" grava o mesmo endereço para os quatro serviços (o "link único" que a operadora fornece).
        "codigo" escolhe o que vai em codigoPrestadorNaOperadora: o CNPJ da clínica, o login do web service,
        um código de até 14 caracteres, ou "padrao" para voltar à regra da operadora (Petrobras: CNPJ; Amil: login).
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
                case "endereco" when args.Length >= 4 && args[2] == "todos":
                    foreach (var cada in TiposComEndereco)
                        await credenciais.DefinirEnderecoAsync(args[1], cada, args[3], Opcao(args, "--operador") ?? "", default);
                    saida.WriteLine($"O mesmo endereço gravado para os {TiposComEndereco.Length} serviços de {args[1]}.");
                    return 0;
                case "endereco" when args.Length >= 4 && Tipo(args[2]) is { } tipo:
                    await credenciais.DefinirEnderecoAsync(args[1], tipo, args[3], Opcao(args, "--operador") ?? "", default);
                    saida.WriteLine($"Endereço de {tipo} gravado para {args[1]}.");
                    return 0;
                case "codigo" when args.Length >= 3:
                {
                    var valor = args[2] == "padrao" ? string.Empty : args[2];
                    await credenciais.DefinirCodigoDoPrestadorAsync(args[1], valor, Opcao(args, "--operador") ?? "", default);
                    // Diz o que foi gravado sem reler o cadastro de convênios: o usuário do banco de
                    // quem opera (tiss_operacao) só alcança a configuração e a trilha.
                    var descricao = valor.Trim().ToLowerInvariant() switch
                    {
                        "" => "a regra da operadora (catálogo)",
                        CredenciaisTissService.CodigoPeloCnpj => DescreverCodigo(CodigoDoPrestadorTiss.CnpjDaClinica),
                        CredenciaisTissService.CodigoPeloLogin => DescreverCodigo(CodigoDoPrestadorTiss.LoginDoWebService),
                        _ => DescreverCodigo(CodigoDoPrestadorTiss.Cadastrado)
                    };
                    saida.WriteLine($"Código do prestador de {args[1]}: {descricao}.");
                    return 0;
                }
                case "cancelar" when args.Length >= 2:
                {
                    var operador = Opcao(args, "--operador");
                    if (!int.TryParse(args[1], out var id) || string.IsNullOrWhiteSpace(operador))
                    {
                        erro.WriteLine("Uso: cancelar ID --operador NOME (o ID é o número da primeira coluna do comando situacao).");
                        return 2;
                    }
                    // A MESMA regra do botão "Cancelar" das telas: só sai da fila o que ainda não foi pego.
                    var cancelou = await servicos.GetRequiredService<FilaTissService>().CancelarAsync(id, operador);
                    (cancelou ? saida : erro).WriteLine(cancelou
                        ? $"Transação {id} cancelada: ela não vai mais à operadora."
                        : $"A transação {id} não está Pendente (já foi pega, terminou ou foi cancelada): nada mudou.");
                    return cancelou ? 0 : 1;
                }
                case "ambiente" when args.Length >= 3 && args[2] is "homologacao" or "producao":
                {
                    var homologacao = args[2] == "homologacao";
                    await credenciais.DefinirAmbienteAsync(args[1], homologacao, Opcao(args, "--operador") ?? "", default);
                    var operadora = await credenciais.OperadoraAsync(args[1]);
                    saida.WriteLine(homologacao
                        ? $"{args[1]} em HOMOLOGAÇÃO: todos os serviços vão para {operadora?.EnderecoHomologacao}. Os pedidos das telas ficam recusados até voltar para produção."
                        : $"{args[1]} em produção.");
                    return 0;
                }
                case "testar-elegibilidade" when args.Length >= 2:
                {
                    var carteira = Opcao(args, "--carteira");
                    if (string.IsNullOrWhiteSpace(carteira)) { erro.WriteLine("Uso: testar-elegibilidade CONVENIO --carteira N"); return 2; }
                    return Imprimir(await Testes(servicos).ElegibilidadeAsync(args[1], carteira, Opcao(args, "--operador") ?? "", Opcao(args, "--soap-action")), saida, erro);
                }
                case "testar-lote" when args.Length >= 2:
                {
                    var operador = Opcao(args, "--operador");
                    if (string.IsNullOrWhiteSpace(operador)) { erro.WriteLine("Informe --operador NOME: o teste vai para a trilha."); return 2; }
                    GuiaDeExemploTiss? exemplo = null;
                    if (args.Contains("--exemplo"))
                    {
                        var carteira = Opcao(args, "--carteira");
                        if (string.IsNullOrWhiteSpace(carteira)) { erro.WriteLine("--exemplo pede --carteira N (uma carteira válida de produção)."); return 2; }
                        var valor = Opcao(args, "--valor") is { } v ? GuiaAnsEmEdicao.LerValor(v) : 1.00m;
                        if (valor is null or <= 0) { erro.WriteLine("--valor inválido (ex.: 1,00)."); return 2; }
                        exemplo = new GuiaDeExemploTiss(carteira, Opcao(args, "--senha"), Opcao(args, "--token"),
                            Opcao(args, "--procedimento") ?? "10101012", valor.Value);
                    }
                    var ids = new List<int>();
                    foreach (var parte in (Opcao(args, "--guias") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (!int.TryParse(parte, out var id)) { erro.WriteLine($"--guias: \"{parte}\" não é um número de guia."); return 2; }
                        ids.Add(id);
                    }
                    return Imprimir(await Testes(servicos).LoteAsync(args[1], ids, exemplo, operador, Opcao(args, "--soap-action"),
                        mesmoAssim: args.Contains("--mesmo-assim")), saida, erro);
                }
                case "testar-protocolo" when args.Length >= 2:
                {
                    var protocolo = Opcao(args, "--protocolo");
                    var operador = Opcao(args, "--operador");
                    if (string.IsNullOrWhiteSpace(protocolo) || string.IsNullOrWhiteSpace(operador))
                    { erro.WriteLine("Uso: testar-protocolo CONVENIO --protocolo N --operador NOME"); return 2; }
                    return Imprimir(await Testes(servicos).ProtocoloAsync(args[1], protocolo, operador, Opcao(args, "--soap-action")), saida, erro);
                }
                case "situacao":
                {
                    var fila = servicos.GetRequiredService<FilaTissService>();
                    var convenio = args.Length >= 2 ? args[1] : null;
                    if (convenio is not null)
                    {
                        if (await credenciais.EmHomologacaoAsync(convenio))
                            saida.WriteLine($"*** {convenio}: AMBIENTE DE HOMOLOGAÇÃO — todos os serviços vão para {(await credenciais.OperadoraAsync(convenio))?.EnderecoHomologacao}; os pedidos das telas ficam recusados. Voltar: comando.sh ambiente {convenio} producao --operador NOME ***");
                        else
                            saida.WriteLine($"{convenio}: ambiente de produção.");
                        var c = await credenciais.ParaATelaAsync(convenio);
                        // As idas REAIS de hoje (o que o teto conta) e o teto deste servidor.
                        var teto = servicos.GetService<OpcoesDoConector>()?.LimiteDiarioElegibilidade ?? OpcoesDoConector.PadraoLimiteDiarioElegibilidade;
                        saida.WriteLine($"{convenio}: login {(c.Login is null ? "não cadastrado" : "cadastrado")}, senha {(c.TemSenha ? "cadastrada" : "não cadastrada")}, "
                                        + $"envio {(await credenciais.EnvioLigadoAsync(convenio) ? "LIGADO" : "desligado")}, "
                                        + $"hoje {await fila.IdasDeElegibilidadeHojeAsync(convenio)} de {teto} consultas.");
                        // De onde sai cada dado da mensagem — sem mostrar login, código nem senha.
                        var operadora = await credenciais.OperadoraAsync(convenio);
                        var (regra, _) = await credenciais.CodigoDoPrestadorAsync(convenio);
                        var cadastrado = await credenciais.EnderecoCadastradoAsync(convenio, TipoTransacaoTiss.Elegibilidade);
                        var endereco = await credenciais.EnderecoAsync(convenio, TipoTransacaoTiss.Elegibilidade);
                        saida.WriteLine($"  operadora {(operadora is null ? "fora do catálogo (só vale o cadastrado)" : $"{operadora.Nome}, registro ANS {operadora.RegistroAns} no catálogo")}; "
                                        + $"código do prestador: {DescreverCodigo(regra)}; "
                                        + $"elegibilidade em {(endereco is null ? "nenhum endereço" : endereco.ToString())} ({(cadastrado is null ? "padrão do catálogo" : "cadastrado")}).");
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

    private static TestesNaHomologacao Testes(IServiceProvider servicos) => new(
        servicos.GetRequiredService<Clinica.Application.Abstracoes.IClinicaRepositorio>(), servicos.GetRequiredService<ParametrosService>(),
        servicos.GetRequiredService<CredenciaisTissService>(), servicos.GetRequiredService<TissWebserviceCliente>(),
        servicos.GetService<RelogioDoConector>()?.Agora ?? HoraDaClinica.Agora);

    private static int Imprimir(ResultadoDoTesteTiss resultado, TextWriter saida, TextWriter erro)
    {
        foreach (var linha in resultado.Linhas) (resultado.Foi ? saida : erro).WriteLine(linha);
        return resultado.Foi ? 0 : 1;
    }

    private static string? Opcao(string[] args, string nome)
    {
        var i = Array.IndexOf(args, nome);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static readonly TipoTransacaoTiss[] TiposComEndereco =
        [TipoTransacaoTiss.Elegibilidade, TipoTransacaoTiss.EnvioGuia, TipoTransacaoTiss.StatusProtocolo, TipoTransacaoTiss.CancelamentoGuia];

    private static string DescreverCodigo(CodigoDoPrestadorTiss regra) => regra switch
    {
        CodigoDoPrestadorTiss.CnpjDaClinica => "o CNPJ da clínica",
        CodigoDoPrestadorTiss.LoginDoWebService => "o login do web service",
        _ => "o código cadastrado"
    };

    private static TipoTransacaoTiss? Tipo(string texto) => texto switch
    {
        "elegibilidade" => TipoTransacaoTiss.Elegibilidade,
        "envio" => TipoTransacaoTiss.EnvioGuia,
        "status" => TipoTransacaoTiss.StatusProtocolo,
        "cancelamento" => TipoTransacaoTiss.CancelamentoGuia,
        _ => null
    };
}
