using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Tiss.Conector;

/// <summary>
/// Elegibilidade: pergunta à operadora se a carteira está ativa. Usa as mensagens que já
/// existem (<see cref="TissElegibilidadeWs"/>, padrão 4.01.00) — este arquivo só junta os dados.
/// </summary>
public sealed class ProcessadorDeElegibilidade(
    IClinicaRepositorio repo, ParametrosService parametros, CredenciaisTissService credenciais, Func<DateTime> agora)
    : IProcessadorTransacaoTiss
{
    public TipoTransacaoTiss Tipo => TipoTransacaoTiss.Elegibilidade;

    public async Task<PreparoTiss> PrepararAsync(TransacaoTiss transacao, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(transacao.NumeroCarteira))
            return PreparoTiss.Falhar("O pedido de elegibilidade veio sem o número da carteira.");

        var credencial = await credenciais.AbrirParaEnvioAsync(transacao.ConvenioCodigo, ct);
        if (credencial is null)
            return PreparoTiss.Recusar($"Não há login do web service cadastrado para {transacao.ConvenioCodigo}. Cadastre a credencial no servidor do conector.");

        var destino = await DadosDoConvenio.LerAsync(repo, parametros, credenciais, transacao.ConvenioCodigo, Tipo, ct);
        if (destino.Pendencia is { } pendencia)
            return PreparoTiss.Recusar(pendencia);

        var pedido = new PedidoElegibilidadeTiss
        {
            RegistroAnsOperadora = destino.RegistroAns!,
            CodigoPrestadorNaOperadora = destino.CodigoPrestador!,
            NumeroCarteira = transacao.NumeroCarteira.Trim(),
            ValidadeCarteira = transacao.ValidadeCarteira,
            Login = credencial.Login,
            SenhaMd5 = credencial.SenhaMd5,
            Sequencial = DadosDoConvenio.Sequencial(transacao),
            Momento = agora()
        };
        try
        {
            return PreparoTiss.Enviar(destino.Endereco!, TissElegibilidadeWs.MontarEnvelope(pedido), credencial.SenhaMd5);
        }
        catch (ArgumentException ex)
        {
            // As frases de MontarPedido dizem qual campo está fora do padrão, sem repetir o valor.
            return PreparoTiss.Falhar("Dados recusados antes do envio: " + ex.Message);
        }
    }

    public DesfechoTiss Ler(TransacaoTiss transacao, RespostaBrutaTissWs bruta)
    {
        var xmlDaResposta = bruta.Corpo!;
        var resposta = TissElegibilidadeWs.LerResposta(xmlDaResposta);
        if (resposta.Elegivel is null)
            // Login recusado, hash, schema ou glosa da mensagem: a operadora não avaliou a carteira.
            return DesfechoTiss.Falhar(resposta.Resumo);

        var validade = resposta.ValidadeCarteira is { } v ? $" Validade da carteira: {v:dd/MM/yyyy}." : string.Empty;
        var nome = string.IsNullOrWhiteSpace(resposta.NomeBeneficiario) ? string.Empty
            : $" {EnvioTissNaTela.RotuloDoNomeNaOperadora} {resposta.NomeBeneficiario.Trim()}.";
        return DesfechoTiss.Concluir(new ConclusaoTransacaoTiss
        {
            // O NOME que a operadora devolveu vai na frase (nov/2026, pedido da direção): a tela o
            // mostra ao lado do nome da ficha, para CONFERÊNCIA — ninguém o copia para a ficha. É o
            // nome da pessoa cuja carteira a clínica acabou de consultar, lido por quem pediu a
            // consulta; continua fora do log e da auditoria, e o XML inteiro continua só no servidor.
            Resultado = resposta.Resumo + validade + nome,
            XmlRecebido = xmlDaResposta
        });
    }
}

/// <summary>O que toda mensagem precisa saber do convênio e da clínica, lido num lugar só.</summary>
public sealed record DadosDoConvenio(string? RegistroAns, string? CodigoPrestador, Uri? Endereco, string? Pendencia)
{
    public static async Task<DadosDoConvenio> LerAsync(
        IClinicaRepositorio repo, ParametrosService parametros, CredenciaisTissService credenciais,
        string convenioCodigo, TipoTransacaoTiss tipo, CancellationToken ct)
    {
        var prestador = await parametros.ObterPrestadorAsync(ct);
        var cadastro = (await repo.ConveniosAsync(ct))
            .FirstOrDefault(c => string.Equals(c.Codigo, convenioCodigo, StringComparison.OrdinalIgnoreCase));
        // O registro ANS é o do convênio; em branco, o da clínica (a mesma ordem da guia na baixa).
        var registro = Limpo(cadastro?.RegistroAnsOperadora) ?? Limpo(prestador.RegistroAnsOperadora);
        if (registro is null || registro.Length != 6 || !registro.All(char.IsAsciiDigit))
            return new(null, null, null, $"O registro ANS de {convenioCodigo} não está cadastrado (6 dígitos). Confira o cadastro do convênio.");

        // Decisão da direção (nov/2026): o contratado vai em codigoPrestadorNaOperadora com o CNPJ.
        var cnpj = new string((prestador.Cnpj ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (cnpj.Length != 14)
            return new(null, null, null, "O CNPJ da clínica não está cadastrado (14 dígitos). Confira os dados do prestador.");

        // O cadastrado ou, para a Saúde Petrobras, o do manual da operadora (CredenciaisTissService.EnderecoPadrao).
        var endereco = await credenciais.EnderecoAsync(convenioCodigo, tipo, ct);
        if (endereco is null)
            return new(null, null, null, $"O endereço do web service de {tipo} para {convenioCodigo} não está cadastrado.");

        return new(registro, cnpj, endereco, null);
    }

    /// <summary>
    /// O sequencial da mensagem (até 12 caracteres), único por TENTATIVA: nove dígitos do número
    /// da transação e três da tentativa.
    /// </summary>
    public static string Sequencial(TransacaoTiss transacao)
        => $"{transacao.Id % 1_000_000_000:D9}{transacao.Tentativas % 1000:D3}";

    /// <summary>
    /// O número do lote (até 12 caracteres, só dígitos): "9" + o número da transação em onze
    /// posições. Cada transação de envio é UM lote e nunca é reenviada, então o número não se
    /// repete; o "9" na frente o afasta dos números dos lotes gerados em arquivo (1, 2, 3…).
    /// </summary>
    public static string NumeroDoLote(TransacaoTiss transacao) => $"9{transacao.Id % 100_000_000_000L:D11}";

    /// <summary>Quem assina a mensagem: a clínica na operadora, com o login do convênio.</summary>
    public RemetenteTissWs Remetente(CredencialTiss credencial) => new()
    {
        RegistroAnsOperadora = RegistroAns!,
        CodigoPrestadorNaOperadora = CodigoPrestador!,
        Login = credencial.Login,
        SenhaMd5 = credencial.SenhaMd5
    };

    /// <summary>
    /// O que os processadores fazem igual antes de montar a mensagem: abrir a credencial e ler os
    /// dados do convênio. Devolve a recusa pronta quando falta alguma coisa.
    /// </summary>
    public static async Task<(RemetenteTissWs? Remetente, Uri? Endereco, string? SenhaMd5, PreparoTiss? Recusa)> AbrirAsync(
        IClinicaRepositorio repo, ParametrosService parametros, CredenciaisTissService credenciais,
        TransacaoTiss transacao, CancellationToken ct)
    {
        var credencial = await credenciais.AbrirParaEnvioAsync(transacao.ConvenioCodigo, ct);
        if (credencial is null)
            return (null, null, null, PreparoTiss.Recusar(
                $"Não há login do web service cadastrado para {transacao.ConvenioCodigo}. Cadastre a credencial no servidor do conector."));
        var destino = await LerAsync(repo, parametros, credenciais, transacao.ConvenioCodigo, transacao.Tipo, ct);
        return destino.Pendencia is { } pendencia
            ? (null, null, null, PreparoTiss.Recusar(pendencia))
            : (destino.Remetente(credencial), destino.Endereco, credencial.SenhaMd5, null);
    }

    private static string? Limpo(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
