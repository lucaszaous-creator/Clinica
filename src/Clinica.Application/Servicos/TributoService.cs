using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// O imposto de um recebimento, aberto por tributo.
///
/// <paramref name="Detalhe"/> é a PROCEDÊNCIA copiada para o lançamento ("ISS 3% + PIS
/// 0,65%"), pelo mesmo motivo de a taxa de cartão copiar o percentual: o catálogo é
/// renegociado, e sem a procedência ninguém saberia de onde saiu a retenção de um
/// recebimento de seis meses atrás.
/// </summary>
public sealed record ImpostoApurado(decimal Aliquota, decimal Valor, string? Detalhe)
{
    public static readonly ImpostoApurado Nenhum = new(0m, 0m, null);

    public bool Houve => Valor > 0m;

    /// <summary>
    /// Foi RETIDO pela operadora, não recolhido pela clínica (parcela 18). Muda o que a
    /// clínica faz com o número: retido já saiu, recolhido ainda vai sair.
    /// </summary>
    public bool RetidoNaFonte { get; init; }
}

/// <summary>
/// Regime tributário da clínica (parcela 15).
///
/// A parcela 9 resolveu o imposto com UMA alíquota global, e resolveu mal por dois
/// motivos que só aparecem no escritório do contador: um número somado responde "quanto
/// saiu" e não "de quê" (a clínica no Presumido tem cinco guias), e uma alíquota sem
/// vigência muda o passado no dia em que o município reajusta o ISS.
///
/// Compatibilidade: a chave `ParametrosService.ChaveAliquotaImposto` continua valendo
/// como FALLBACK enquanto não houver nenhum tributo cadastrado. O valor já está gravado
/// na base do cliente, e trocar o mecanismo zerando a retenção faria a clínica emitir um
/// mês inteiro sem imposto sem perceber. Cadastrou o primeiro tributo, os tributos mandam.
/// </summary>
public sealed class TributoService
{
    private readonly IClinicaRepositorio _repo;
    private readonly ParametrosService? _parametros;

    public TributoService(IClinicaRepositorio repo, ParametrosService? parametros = null)
    {
        _repo = repo;
        _parametros = parametros;
    }

    public Task<IReadOnlyList<Tributo>> CatalogoAsync(
        bool somenteAtivos = false, CancellationToken ct = default)
        => _repo.TributosAsync(somenteAtivos, ct);

    /// <summary>
    /// Os tributos que valem no dia — a base de qualquer cálculo.
    ///
    /// Com <paramref name="convenioCodigo"/>, procura primeiro a RETENÇÃO cadastrada para
    /// aquele convênio; achando, é ela que vale e os tributos gerais NÃO entram. Os dois
    /// representam o mesmo imposto — a operadora reteve o que a clínica deveria recolher —
    /// e somá-los contaria duas vezes, que é o erro que mais aparece em planilha de
    /// clínica que fatura convênio.
    ///
    /// Sem retenção cadastrada, o convênio cai nos tributos gerais: é o comportamento de
    /// antes da parcela 18, e mudá-lo faria a receita de convênio parar de sofrer imposto
    /// da noite para o dia, em toda base que já existe.
    /// </summary>
    public async Task<IReadOnlyList<Tributo>> VigentesEmAsync(
        DateOnly dia, string? convenioCodigo = null, CancellationToken ct = default)
    {
        var vigentes = (await _repo.TributosAsync(somenteAtivos: true, ct))
            .Where(t => t.VigenteEm(dia))
            .ToList();

        if (!string.IsNullOrWhiteSpace(convenioCodigo))
        {
            var doConvenio = vigentes
                .Where(t => string.Equals(t.ConvenioCodigo, convenioCodigo, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.Sigla)
                .ToList();
            if (doConvenio.Count > 0) return doConvenio;
        }

        // Tributo de convênio nunca vaza para recebimento que não é daquele convênio: a
        // retenção da Unimed não incide sobre o particular pago em dinheiro.
        return vigentes
            .Where(t => !t.RetidoNaFonte)
            .OrderBy(t => t.Sigla)
            .ToList();
    }

    public async Task<Tributo> SalvarAsync(
        Tributo dados, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dados.Sigla))
            throw new InvalidOperationException("Informe a sigla do tributo (ISS, PIS, COFINS…).");
        if (dados.Percentual is < 0m or > 100m)
            throw new InvalidOperationException("A alíquota vai de 0 a 100.");
        if (dados.BasePercentual is <= 0m or > 100m)
            throw new InvalidOperationException(
                "A base de cálculo vai de 0 a 100 — 100 quando o tributo incide sobre a receita inteira.");
        if (dados.VigenteDe is { } de && dados.VigenteAte is { } ate && de > ate)
            throw new InvalidOperationException("A vigência começa antes de terminar.");

        var tributo = dados.Id == 0
            ? null
            : await _repo.ObterTributoAsync(dados.Id, ct)
              ?? throw new InvalidOperationException("Tributo não encontrado.");

        if (tributo is null)
        {
            tributo = new Tributo { CriadoPor = operador };
            await _repo.AdicionarTributoAsync(tributo, ct);
        }

        tributo.Sigla = dados.Sigla.Trim().ToUpperInvariant();
        tributo.Nome = string.IsNullOrWhiteSpace(dados.Nome) ? tributo.Sigla : dados.Nome.Trim();
        tributo.Percentual = dados.Percentual;
        tributo.BasePercentual = dados.BasePercentual;
        tributo.ConvenioCodigo = string.IsNullOrWhiteSpace(dados.ConvenioCodigo)
            ? null
            : dados.ConvenioCodigo.Trim();
        tributo.VigenteDe = dados.VigenteDe;
        tributo.VigenteAte = dados.VigenteAte;
        tributo.Ativo = dados.Ativo;
        tributo.Observacoes = string.IsNullOrWhiteSpace(dados.Observacoes) ? null : dados.Observacoes.Trim();

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Acao = dados.Id == 0 ? "TributoCriado" : "TributoAtualizado",
            Detalhe = tributo.Descricao + $" · {tributo.Vigencia}",
            Operador = operador ?? Environment.UserName
        }, ct);
        await _repo.SalvarAsync(ct);
        return tributo;
    }

    /// <summary>
    /// Excluir só serve para o que foi cadastrado errado. Tributo que já reteve alguma
    /// coisa deve ser ENCERRADO pela vigência: o lançamento guardou o valor copiado, mas
    /// apagar a regra apaga a explicação de por que a retenção daquele mês foi aquela.
    /// </summary>
    public async Task ExcluirAsync(int tributoId, CancellationToken ct = default)
    {
        await _repo.RemoverTributoAsync(tributoId, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// Quanto de imposto sai de um recebimento, e de quê.
    ///
    /// Cada tributo é arredondado ao centavo SEPARADAMENTE, e a alíquota devolvida é a
    /// soma das efetivas. Arredondar só no fim daria um total que não bate com a soma das
    /// linhas do detalhe — e é o detalhe que a clínica leva para o contador.
    /// </summary>
    public async Task<ImpostoApurado> ApurarAsync(
        decimal valorBruto, DateOnly dia, string? convenioCodigo = null,
        CancellationToken ct = default)
    {
        if (valorBruto <= 0m) return ImpostoApurado.Nenhum;

        var vigentes = await VigentesEmAsync(dia, convenioCodigo, ct);

        if (vigentes.Count == 0)
        {
            // Fallback da parcela 9: a base do cliente já tem a alíquota única gravada, e
            // trocar o mecanismo zerando a retenção faria a clínica emitir um mês inteiro
            // sem imposto sem perceber.
            var antiga = _parametros is null ? 0m : await _parametros.ObterAliquotaImpostoAsync(ct);
            if (antiga <= 0m) return ImpostoApurado.Nenhum;

            return new ImpostoApurado(
                antiga, Arredondar(valorBruto * antiga / 100m),
                // pt-BR fixo pelo mesmo motivo do Tributo.Descricao: este texto é gravado.
                string.Format(new System.Globalization.CultureInfo("pt-BR"),
                    "Alíquota única {0:0.##}%", antiga));
        }

        var total = 0m;
        var aliquota = 0m;
        var partes = new List<string>(vigentes.Count);

        foreach (var t in vigentes)
        {
            var efetiva = t.AliquotaEfetiva;
            if (efetiva <= 0m) continue;

            aliquota += efetiva;
            total += Arredondar(valorBruto * efetiva / 100m);
            partes.Add(t.Descricao);
        }

        if (partes.Count == 0) return ImpostoApurado.Nenhum;

        // "Retido na fonte" é informação de CAIXA, não decoração: esse dinheiro não sai
        // pela clínica no vencimento da guia — ele já não entrou. Sem a marca, o contador
        // recolheria de novo o que a operadora já reteve.
        var detalhe = vigentes[0].RetidoNaFonte
            ? $"{string.Join(" + ", partes)} (retido na fonte)"
            : string.Join(" + ", partes);

        return new ImpostoApurado(aliquota, total, detalhe)
        {
            RetidoNaFonte = vigentes[0].RetidoNaFonte
        };
    }

    /// <summary>
    /// Alíquota efetiva total do dia, para a tela mostrar "hoje sai X% de cada recebimento".
    /// </summary>
    public async Task<decimal> AliquotaTotalAsync(
        DateOnly dia, string? convenioCodigo = null, CancellationToken ct = default)
    {
        var vigentes = await VigentesEmAsync(dia, convenioCodigo, ct);
        if (vigentes.Count > 0) return vigentes.Sum(t => t.AliquotaEfetiva);

        return _parametros is null ? 0m : await _parametros.ObterAliquotaImpostoAsync(ct);
    }

    /// <summary>
    /// A apuração do MÊS, tributo a tributo (parcela 28).
    ///
    /// Este serviço separa ISS, PIS, COFINS, IRPJ e CSLL desde a parcela 15 — e toda tela
    /// do sistema consolidava `ValorImposto` num número só. O resultado prático: a clínica
    /// sabia quanto de imposto saiu no mês e **não sabia de quê**, que é exatamente a
    /// pergunta que o contador faz, e a única que permite conferir cinco guias antes de
    /// pagá-las.
    ///
    /// Duas decisões que mudam o número:
    ///
    /// 1. **O período é pelo dia do RECEBIMENTO**, não pela competência do lançamento —
    ///    a mesma data que o custo de transação usa. Imposto sobre receita recolhida
    ///    acompanha o dinheiro que entrou.
    /// 2. **A retenção do convênio é apurada por recebimento**, com o código da operadora
    ///    daquele recebimento. Apurar o mês inteiro com os tributos gerais somaria o que a
    ///    clínica recolhe com o que a operadora já reteve — contando duas vezes o mesmo
    ///    tributo, que é o erro clássico da planilha de convênio.
    ///
    /// O total apurado é comparado com o que está GRAVADO nos lançamentos, e a divergência
    /// aparece em vez de ser escondida: ela significa que a regra mudou depois de o
    /// dinheiro entrar, e é a própria clínica que precisa decidir qual dos dois números
    /// vai para a guia.
    /// </summary>
    public async Task<ApuracaoMensal> ApuracaoMensalAsync(
        int ano, int mes, CancellationToken ct = default)
    {
        var inicio = new DateOnly(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        var recebimentos = await _repo.RecebimentosComDeducaoAsync(inicio, fim, ct);

        var receita = recebimentos.Sum(r => r.Valor);
        var gravado = recebimentos.Sum(r => r.ValorImposto ?? 0m);

        var acumulado = new Dictionary<string, LinhaApuracaoTributo>();

        foreach (var r in recebimentos)
        {
            if (r.Valor <= 0m) continue;

            var vigentes = await VigentesEmAsync(r.Dia, r.ConvenioCodigo, ct);
            if (vigentes.Count == 0) continue;

            foreach (var t in vigentes)
            {
                var efetiva = t.AliquotaEfetiva;
                if (efetiva <= 0m) continue;

                // Cada tributo arredondado SEPARADAMENTE, como no cálculo do recebimento:
                // arredondar só no fim daria um total que não bate com a soma das linhas,
                // e é o detalhe que vai para o contador.
                var valor = Arredondar(r.Valor * efetiva / 100m);
                var chave = $"{t.Sigla}|{t.RetidoNaFonte}";

                acumulado[chave] = acumulado.TryGetValue(chave, out var atual)
                    ? atual with
                    {
                        Base = atual.Base + r.Valor,
                        Valor = atual.Valor + valor
                    }
                    : new LinhaApuracaoTributo(
                        t.Sigla, t.Nome, efetiva, r.Valor, valor, t.RetidoNaFonte);
            }
        }

        var linhas = acumulado.Values
            // Retido primeiro: é o que a clínica NÃO paga no vencimento, e separar as duas
            // naturezas é justamente o que evita recolher de novo o que já saiu.
            .OrderByDescending(l => l.RetidoNaFonte)
            .ThenByDescending(l => l.Valor)
            .ToList();

        return new ApuracaoMensal(ano, mes, receita, gravado, linhas.Sum(l => l.Valor), linhas);
    }

    private static decimal Arredondar(decimal valor)
        => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Um tributo no mês: quanto incidiu, sobre quanto, e quem recolhe.</summary>
public sealed record LinhaApuracaoTributo(
    string Sigla,
    string Nome,
    decimal Aliquota,
    decimal Base,
    decimal Valor,
    bool RetidoNaFonte)
{
    /// <summary>
    /// Quem recolhe. Retido já saiu — a operadora descontou antes de depositar; recolhido
    /// ainda vai sair, na guia do mês. Somar os dois na mesma linha faria a clínica pagar
    /// duas vezes.
    /// </summary>
    public string Natureza => RetidoNaFonte ? "retido na fonte" : "a recolher";
}

/// <summary>A apuração de um mês, pronta para conferência e para o contador.</summary>
public sealed record ApuracaoMensal(
    int Ano,
    int Mes,
    decimal ReceitaBruta,
    decimal TotalGravado,
    decimal TotalApurado,
    IReadOnlyList<LinhaApuracaoTributo> Linhas)
{
    public bool Vazia => Linhas.Count == 0;

    /// <summary>O que a clínica precisa recolher — sem o que a operadora já reteve.</summary>
    public decimal ARecolher => Linhas.Where(l => !l.RetidoNaFonte).Sum(l => l.Valor);

    public decimal Retido => Linhas.Where(l => l.RetidoNaFonte).Sum(l => l.Valor);

    /// <summary>
    /// O apurado agora não bate com o que foi gravado quando o dinheiro entrou. Não é
    /// erro do sistema: significa que a regra mudou depois. Aparece porque esconder
    /// faria a clínica levar um número ao contador sem saber que existe outro.
    /// </summary>
    public bool Diverge => Math.Abs(TotalGravado - TotalApurado) >= 0.01m;

    public decimal Divergencia => TotalApurado - TotalGravado;
}
