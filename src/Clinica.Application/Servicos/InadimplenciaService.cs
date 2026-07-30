using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Por onde começar a cobrar.</summary>
public enum OrdemInadimplencia
{
    /// <summary>Mais antigo primeiro — o padrão. Dívida velha é a que menos volta.</summary>
    MaisAntigo,

    /// <summary>Maior valor primeiro, para quando a clínica tem pouco tempo de cobrança.</summary>
    MaiorValor
}

/// <summary>Uma conta vencida de um paciente.</summary>
public sealed record ContaEmAtraso(
    int LancamentoId,
    string Descricao,
    DateOnly Vencimento,
    int DiasEmAtraso,
    decimal Valor);

/// <summary>
/// Quem deve, quanto e desde quando. O <c>Telefone</c> vem junto porque a próxima ação
/// depois de ler esta linha é ligar ou mandar mensagem — buscar o contato numa segunda
/// tela é o que faz a cobrança não acontecer.
/// </summary>
public sealed record PacienteInadimplente(
    int PacienteId,
    string Nome,
    string? Telefone,
    decimal Total,
    int Contas,
    int DiasMaiorAtraso,
    DateOnly VencimentoMaisAntigo,
    IReadOnlyList<ContaEmAtraso> Detalhe)
{
    /// <summary>
    /// Faixa de envelhecimento. Não é enfeite de relatório: é o que separa o atraso que
    /// se resolve com um lembrete do que já exige decisão (acordo, ou parar de contar
    /// com o dinheiro).
    /// </summary>
    public string Faixa => FaixasInadimplencia.De(DiasMaiorAtraso);
}

/// <summary>Uma faixa de envelhecimento, com valor — não só contagem.</summary>
public sealed record FaixaInadimplencia(string Faixa, int Pacientes, decimal Valor)
{
    /// <summary>Fração do total em atraso. Vem normalizada porque o DataTemplate não vê os irmãos.</summary>
    public double Fracao { get; init; }
}

/// <summary>Os números do topo da tela.</summary>
public sealed record ResumoInadimplencia(
    decimal Total,
    int Pacientes,
    int Contas,
    IReadOnlyList<FaixaInadimplencia> Faixas)
{
    public bool Vazio => Pacientes == 0;

    /// <summary>
    /// Quanto deve, em média, quem deve. <c>null</c> sem ninguém devendo — dividir por
    /// zero daria "R$ 0,00 por paciente", que se lê como "ninguém deve quase nada".
    /// </summary>
    public decimal? MedioPorPaciente => Pacientes > 0 ? Total / Pacientes : null;
}

/// <summary>As faixas de envelhecimento, num lugar só.</summary>
public static class FaixasInadimplencia
{
    public const string Ate30 = "1 a 30 dias";
    public const string De31A60 = "31 a 60 dias";
    public const string De61A90 = "61 a 90 dias";
    public const string Acima90 = "mais de 90 dias";

    /// <summary>Ordem de exibição: do mais novo para o mais velho, como se lê um aging.</summary>
    public static IReadOnlyList<string> Todas { get; } = [Ate30, De31A60, De61A90, Acima90];

    public static string De(int diasEmAtraso) => diasEmAtraso switch
    {
        <= 30 => Ate30,
        <= 60 => De31A60,
        <= 90 => De61A90,
        _ => Acima90
    };
}

/// <summary>
/// Inadimplência por paciente (parcela 23).
///
/// A parcela 12 deu ao sistema o vencimento, e com ele a resposta para "o que vence esta
/// semana". Ficou faltando a pergunta que vem depois, e que é a que a clínica de fato faz:
/// **quem me deve?** A conta a receber vencida existia desde então, mas só aparecia
/// dissolvida na lista de contas — uma linha por lançamento, misturada com o que a clínica
/// tem a PAGAR. Ninguém consegue cobrar assim: para saber que o mesmo paciente tem três
/// sessões em aberto era preciso ler a lista inteira e somar de cabeça.
///
/// Decisões que valem registro:
///
/// 1. **Só conta de PACIENTE entra.** Entrada prevista vencida sem paciente — reembolso
///    de convênio, venda de produto, aporte — é a receber, não inadimplência. Juntar as
///    duas faria a clínica cobrar quem não deve.
/// 2. **A situação é CALCULADA, nunca gravada.** Não existe (e não deve existir) um campo
///    "inadimplente" no cadastro: paciente marcado assim continua marcado depois de pagar,
///    e quem lê a ficha dois meses depois trata como caloteiro alguém que quitou.
/// 3. **A ordem padrão é o mais ANTIGO, não o maior valor.** A chance de receber cai com o
///    tempo, e o caso de seis meses é o que precisa de decisão — não de mais um lembrete.
///    Quem tem pouco tempo de cobrança pede <see cref="OrdemInadimplencia.MaiorValor"/>.
/// 4. **Não há pagamento parcial.** O sistema não tem baixa parcial de lançamento, e
///    inventá-la aqui criaria um saldo que nenhuma outra tela conhece. Receber é receber a
///    conta inteira, pelo <see cref="FinanceiroService"/> — quem grava dinheiro continua
///    sendo um só.
/// </summary>
public sealed class InadimplenciaService
{
    private readonly IClinicaRepositorio _repo;
    private readonly FinanceiroService _financeiro;

    public InadimplenciaService(IClinicaRepositorio repo, FinanceiroService financeiro)
    {
        _repo = repo;
        _financeiro = financeiro;
    }

    /// <summary>
    /// Quem deve, agrupado por paciente. Só entrada prevista, vencida e com paciente.
    /// </summary>
    public async Task<IReadOnlyList<PacienteInadimplente>> PorPacienteAsync(
        DateOnly hoje,
        OrdemInadimplencia ordem = OrdemInadimplencia.MaisAntigo,
        CancellationToken ct = default)
    {
        // O repositório já traz só o previsto com vencimento, e com o paciente carregado.
        var abertas = await _repo.LancamentosComVencimentoAteAsync(hoje, TipoLancamento.Entrada, ct);

        var devedores = abertas
            .Where(l => l.EstaVencido(hoje) && l.Paciente is not null)
            .GroupBy(l => l.Paciente!)
            .Select(g =>
            {
                var detalhe = g
                    .Select(l => new ContaEmAtraso(
                        l.Id,
                        string.IsNullOrWhiteSpace(l.Descricao) ? "(sem descrição)" : l.Descricao,
                        l.DataVencimento!.Value,
                        hoje.DayNumber - l.DataVencimento!.Value.DayNumber,
                        l.Valor))
                    // Dentro do paciente, a mais antiga primeiro: é a que explica a
                    // conversa ("desde março").
                    .OrderByDescending(c => c.DiasEmAtraso)
                    .ToList();

                return new PacienteInadimplente(
                    g.Key.Id,
                    g.Key.Nome,
                    g.Key.Telefone,
                    detalhe.Sum(c => c.Valor),
                    detalhe.Count,
                    detalhe.Max(c => c.DiasEmAtraso),
                    detalhe.Min(c => c.Vencimento),
                    detalhe);
            })
            .ToList();

        return (ordem == OrdemInadimplencia.MaiorValor
                ? devedores.OrderByDescending(d => d.Total).ThenByDescending(d => d.DiasMaiorAtraso)
                : devedores.OrderByDescending(d => d.DiasMaiorAtraso).ThenByDescending(d => d.Total))
            .ThenBy(d => d.Nome)
            .ToList();
    }

    /// <summary>
    /// Totais e envelhecimento. Conta SOBRE OS MESMOS DADOS da lista — se divergisse, a
    /// tela mostraria dois totais para a mesma dívida.
    /// </summary>
    public async Task<ResumoInadimplencia> ResumoAsync(DateOnly hoje, CancellationToken ct = default)
    {
        var devedores = await PorPacienteAsync(hoje, ct: ct);
        if (devedores.Count == 0)
            return new ResumoInadimplencia(0m, 0, 0, []);

        var total = devedores.Sum(d => d.Total);

        // Todas as faixas aparecem, mesmo vazias: um aging em que a faixa de 90+ dias
        // simplesmente não existe se lê como "não há dívida velha", e o correto é ver
        // R$ 0,00 escrito ali.
        var faixas = FaixasInadimplencia.Todas
            .Select(f =>
            {
                var doGrupo = devedores.Where(d => d.Faixa == f).ToList();
                var valor = doGrupo.Sum(d => d.Total);
                return new FaixaInadimplencia(f, doGrupo.Count, valor)
                {
                    Fracao = total > 0m ? (double)(valor / total) : 0d
                };
            })
            .ToList();

        return new ResumoInadimplencia(
            total, devedores.Count, devedores.Sum(d => d.Contas), faixas);
    }

    /// <summary>
    /// A dívida de um paciente só — para a ficha dele e para a conversa no balcão.
    /// </summary>
    public async Task<PacienteInadimplente?> DoPacienteAsync(
        int pacienteId, DateOnly hoje, CancellationToken ct = default)
        => (await PorPacienteAsync(hoje, ct: ct)).FirstOrDefault(d => d.PacienteId == pacienteId);

    /// <summary>
    /// Registra o recebimento da conta. Delega ao <see cref="FinanceiroService"/> de
    /// propósito: quem grava dinheiro continua sendo um só, com a auditoria no mesmo
    /// SaveChanges — duplicar a gravação aqui daria dois caminhos para o mesmo fato, e
    /// só um deles receberia a próxima correção.
    /// </summary>
    public Task ReceberAsync(
        int lancamentoId, DateOnly dia, FormaPagamento? forma = null,
        string? operador = null, CancellationToken ct = default)
        => _financeiro.RealizarAsync(lancamentoId, dia, forma, operador, ct);

    /// <summary>
    /// Mensagem de cobrança, escrita aqui para sair igual em toda tela que cobrar.
    ///
    /// É **lembrete, não ameaça**: quem está em atraso na clínica quase sempre esqueceu,
    /// e um texto duro custa o paciente inteiro para recuperar uma sessão. Também não traz
    /// diagnóstico nem o que foi feito — mensagem de WhatsApp não é lugar de dado clínico,
    /// e o telefone pode não ser só do paciente.
    /// </summary>
    public static string MensagemDeCobranca(PacienteInadimplente devedor, string? nomeClinica = null)
    {
        var primeiroNome = devedor.Nome
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? devedor.Nome;

        var oQue = devedor.Contas == 1
            ? $"um valor de {Moeda(devedor.Total)} com vencimento em {devedor.VencimentoMaisAntigo:dd/MM}"
            : $"{devedor.Contas} valores em aberto, somando {Moeda(devedor.Total)}, " +
              $"o mais antigo de {devedor.VencimentoMaisAntigo:dd/MM}";

        return $"Olá, {primeiroNome}! Passando para lembrar que consta {oQue} aqui conosco. "
             + "Se já tiver sido pago, é só nos avisar que conferimos. "
             + "Qualquer coisa, podemos combinar a melhor forma por aqui."
             + (string.IsNullOrWhiteSpace(nomeClinica) ? string.Empty : $" — {nomeClinica}");
    }

    /// <summary>
    /// pt-BR fixo: o texto é montado aqui e sai numa mensagem para o paciente. Dois postos
    /// com regionalização diferente escreveriam "R$ 300,00" e "$300.00" para a mesma
    /// dívida — e o segundo não faz sentido nenhum para quem recebe.
    /// </summary>
    private static readonly CultureInfo Brasil = new("pt-BR");

    private static string Moeda(decimal valor) => valor.ToString("C2", Brasil);
}
