using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// O valor proposto para uma guia, com a procedência do número.
///
/// <paramref name="Procedencia"/> existe para a tela poder dizer DE ONDE o valor veio.
/// Um campo que se preenche sozinho sem explicar é pior que um campo vazio: a pessoa
/// confirma sem conferir, e o erro entra no caixa com aparência de conferido.
/// </summary>
public sealed record PrecoProposto(decimal Valor, string Procedencia)
{
    public static readonly PrecoProposto Nenhum = new(0m, string.Empty);

    public bool Houve => Valor > 0m;
}

/// <summary>
/// Tabela de preço por convênio (parcela 20).
///
/// **Cadastrada no Gerente, lida pelo Financeiro.** É o pedido literal da clínica, e
/// também é onde faz sentido: quem negocia tabela com a operadora é a direção; quem
/// concilia guia é o balcão. O cadastro é do primeiro e o uso é do segundo, sobre o mesmo
/// banco — nenhuma sincronização, nenhuma cópia.
///
/// O que ela resolve: o valor da guia era **digitado à mão em cada conciliação**. Um
/// R$ 45 no lugar de R$ 145 não é recusado por ninguém, e a diferença só apareceria numa
/// conferência que a clínica não faz. Com a tabela, a conciliação deixa de ser digitação e
/// passa a ser CONFERÊNCIA contra o demonstrativo — que é o trabalho que importa.
///
/// Três regras que sustentam o resto:
///
/// 1. **A mais ESPECÍFICA ganha.** Um preço com especialidade vence o preço genérico do
///    tipo, senão a clínica cadastraria a exceção e continuaria vendo o valor da regra
///    geral.
/// 2. **Vigência.** A operadora reajusta, e o que vale na guia de março é o valor de
///    março. Reajustar a tabela não reescreve o que já foi conciliado.
/// 3. **Sem preço cadastrado não se inventa valor.** Devolve
///    <see cref="PrecoProposto.Nenhum"/> e o campo fica vazio para ser digitado — como
///    sempre foi. Chutar um valor "de mercado" produziria uma receita errada com aparência
///    de exata, que é pior que campo em branco.
/// </summary>
public sealed class PrecoConvenioService
{
    private readonly IClinicaRepositorio _repo;

    public PrecoConvenioService(IClinicaRepositorio repo) => _repo = repo;

    public Task<IReadOnlyList<PrecoConvenio>> CatalogoAsync(
        bool somenteAtivos = false, CancellationToken ct = default)
        => _repo.PrecosConvenioAsync(somenteAtivos, ct);

    public async Task<PrecoConvenio> SalvarAsync(
        PrecoConvenio dados, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dados.ConvenioCodigo))
            throw new InvalidOperationException("Diga de qual convênio é o preço.");
        if (dados.Valor <= 0m)
            throw new InvalidOperationException(
                "O valor deve ser maior que zero — preço zero é o mesmo que não ter tabela, "
                + "e nesse caso é melhor não cadastrar.");
        if (dados.VigenteDe is { } de && dados.VigenteAte is { } ate && de > ate)
            throw new InvalidOperationException("A vigência começa antes de terminar.");

        var preco = dados.Id == 0
            ? null
            : await _repo.ObterPrecoConvenioAsync(dados.Id, ct)
              ?? throw new InvalidOperationException("Preço não encontrado.");

        if (preco is null)
        {
            preco = new PrecoConvenio { CriadoPor = operador };
            await _repo.AdicionarPrecoConvenioAsync(preco, ct);
        }

        preco.ConvenioCodigo = dados.ConvenioCodigo.Trim();
        preco.Tipo = dados.Tipo;
        preco.Especialidade = dados.Especialidade;
        preco.Valor = dados.Valor;
        preco.VigenteDe = dados.VigenteDe;
        preco.VigenteAte = dados.VigenteAte;
        preco.Ativo = dados.Ativo;
        preco.Observacoes = string.IsNullOrWhiteSpace(dados.Observacoes) ? null : dados.Observacoes.Trim();

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Acao = dados.Id == 0 ? "PrecoConvenioCriado" : "PrecoConvenioAtualizado",
            Detalhe = $"{preco.Descricao} · {preco.Vigencia}",
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador
        }, ct);
        await _repo.SalvarAsync(ct);
        return preco;
    }

    /// <summary>
    /// Excluir só serve para o que foi cadastrado errado. Preço que já propôs valor a
    /// alguma guia deve ser ENCERRADO pela vigência: o lançamento guardou o valor copiado,
    /// mas apagar a linha apaga a explicação de por que aquela guia valeu aquilo.
    /// </summary>
    public async Task ExcluirAsync(int precoId, CancellationToken ct = default)
    {
        await _repo.RemoverPrecoConvenioAsync(precoId, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// Acha o preço que vale para esta guia. A mais ESPECÍFICA ganha: um preço com
    /// especialidade declarada vence o preço genérico do tipo — senão a clínica cadastraria
    /// a exceção e continuaria vendo o número da regra geral.
    /// </summary>
    public async Task<PrecoConvenio?> ResolverAsync(
        string? convenioCodigo,
        TipoCodigo tipo,
        DateOnly dia,
        Especialidade? especialidade = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(convenioCodigo)) return null;

        var candidatos = (await _repo.PrecosConvenioAsync(somenteAtivos: true, ct))
            .Where(p => string.Equals(p.ConvenioCodigo, convenioCodigo, StringComparison.OrdinalIgnoreCase))
            .Where(p => p.Tipo == tipo)
            .Where(p => p.VigenteEm(dia))
            // Preço de especialidade só se aplica à especialidade dele; preço sem
            // especialidade vale para qualquer uma.
            .Where(p => p.Especialidade is null || p.Especialidade == especialidade)
            .ToList();

        return candidatos
            // Especialidade declarada é mais específica que "qualquer".
            .OrderByDescending(p => p.Especialidade is not null)
            // Entre iguais, a que começou a valer mais tarde é o reajuste mais novo.
            .ThenByDescending(p => p.VigenteDe ?? DateOnly.MinValue)
            .FirstOrDefault();
    }

    /// <summary>
    /// O valor a propor para uma guia, já com a procedência escrita. Não grava nada — é
    /// proposta, e quem confirma é quem está conferindo o demonstrativo.
    /// </summary>
    public async Task<PrecoProposto> ProporAsync(
        GuiaSemLancamento guia, Especialidade? especialidade = null, CancellationToken ct = default)
    {
        var preco = await ResolverAsync(guia.ConvenioCodigo, guia.Tipo, guia.DataBaixa, especialidade, ct);
        return preco is null
            ? PrecoProposto.Nenhum
            : new PrecoProposto(preco.Valor, $"tabela: {preco.Descricao} ({preco.Vigencia})");
    }
}
