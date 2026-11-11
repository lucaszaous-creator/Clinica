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

    public DesfechoTiss Ler(TransacaoTiss transacao, string xmlDaResposta)
    {
        var resposta = TissElegibilidadeWs.LerResposta(xmlDaResposta);
        if (resposta.Elegivel is null)
            // Login recusado, hash, schema ou glosa da mensagem: a operadora não avaliou a carteira.
            return DesfechoTiss.Falhar(resposta.Resumo);

        var validade = resposta.ValidadeCarteira is { } v ? $" Validade da carteira: {v:dd/MM/yyyy}." : string.Empty;
        return DesfechoTiss.Concluir(new ConclusaoTransacaoTiss
        {
            // Sem o nome do beneficiário: ele fica só no XML recebido, para quem precisar conferir.
            Resultado = resposta.Resumo + validade,
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
        var familia = cadastro?.Familia
                      ?? (Enum.TryParse<Convenio>(convenioCodigo, ignoreCase: true, out var embutida) ? embutida : Convenio.Personalizado);

        // O registro ANS é o do convênio; em branco, o da clínica (a mesma ordem da guia na baixa).
        var registro = Limpo(cadastro?.RegistroAnsOperadora) ?? Limpo(prestador.RegistroAnsOperadora);
        if (registro is null || registro.Length != 6 || !registro.All(char.IsAsciiDigit))
            return new(null, null, null, $"O registro ANS de {convenioCodigo} não está cadastrado (6 dígitos). Confira o cadastro do convênio.");

        // Decisão da direção (nov/2026): o contratado vai em codigoPrestadorNaOperadora com o CNPJ.
        var cnpj = new string((prestador.Cnpj ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (cnpj.Length != 14)
            return new(null, null, null, "O CNPJ da clínica não está cadastrado (14 dígitos). Confira os dados do prestador.");

        var endereco = await credenciais.EnderecoAsync(convenioCodigo, tipo, ct);
        // O único endereço que o código conhece sozinho: a elegibilidade da Saúde Petrobras, do manual dela.
        if (endereco is null && tipo == TipoTransacaoTiss.Elegibilidade && familia == Convenio.Petrobras)
            endereco = TissWebserviceCliente.ElegibilidadeSaudePetrobras;
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

    private static string? Limpo(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
