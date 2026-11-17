using Clinica.Domain.Entities;

namespace Clinica.Domain.Regras;

/// <summary>
/// Regra de faturamento CONFIGURÁVEL (família "Personalizado"). Gera os códigos a partir da
/// <see cref="ConfiguracaoRegraGenerica"/> do convênio (passada em <see cref="ContextoFaturamento.Generica"/>),
/// cobrindo o padrão comum: acupuntura (± eletro como 2º código) e BSV, com ou sem 2º código.
/// </summary>
public sealed class RegraGenerica : IRegraConvenio
{
    public Convenio Convenio => Convenio.Personalizado;

    public ResultadoFaturamento Gerar(Paciente paciente, Atendimento atendimento, ContextoFaturamento contexto)
    {
        var cfg = contexto.Generica ?? new ConfiguracaoRegraGenerica();
        var r = new ResultadoFaturamento
        {
            Categoria = paciente.PossuiApp ? cfg.CategoriaComApp : cfg.CategoriaSemApp
        };
        var hoje = atendimento.Data;
        var depois = hoje.AddDays(contexto.DiasSegundoCodigo);

        // O 2º código só existe se o convênio o prevê e (quando exigido) o paciente tem app.
        var podeSegundo = cfg.TemSegundoCodigo && (!cfg.SegundoCodigoDependeApp || paciente.PossuiApp);

        switch (atendimento.Modalidade)
        {
            case ModalidadeAtendimento.AcupunturaSimples:
                r.Codigos.Add(Acupuntura(r, cfg, contexto, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica));
                break;

            case ModalidadeAtendimento.AcupunturaComEletro:
                r.Codigos.Add(Acupuntura(r, cfg, contexto, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica));
                if (cfg.FazEletro && cfg.EletroNoMesmoDia)
                {
                    // Acupuntura + eletro no MESMO dia (Sul América): as duas são 1º código.
                    // Se o intervalo da eletro ainda não venceu, ela nasce na data em que o
                    // convênio a libera — e aí é obtida depois, como um 2º código.
                    var data = Liberar(r, contexto, TipoCodigo.Eletroacupuntura, hoje, cfg.IntervaloEletroDias, out var motivo);
                    r.Codigos.Add(data == hoje
                        ? Codigo(TipoCodigo.Eletroacupuntura, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica)
                        : Codigo(TipoCodigo.Eletroacupuntura, OrdemCodigo.Segundo, data, cfg.FormaSegundoCodigo,
                            $"Eletroacupuntura liberada em {Data(data)} — {motivo} {Capitalizar(InstrucaoObtencao(cfg.FormaSegundoCodigo))}."));
                }
                else if (cfg.FazEletro && podeSegundo)
                {
                    var data = Liberar(r, contexto, TipoCodigo.Eletroacupuntura, depois, cfg.IntervaloEletroDias, out var motivo);
                    r.Codigos.Add(Codigo(TipoCodigo.Eletroacupuntura, OrdemCodigo.Segundo, data, cfg.FormaSegundoCodigo,
                        $"2º código (eletroacupuntura) em +{contexto.DiasSegundoCodigo}d — {InstrucaoObtencao(cfg.FormaSegundoCodigo)}."
                        + (motivo is null ? "" : $" Adiado para {Data(data)}: {motivo}")));
                }
                else if (cfg.FazEletro && cfg.TemSegundoCodigo)
                    r.Avisos.Add("Paciente sem app: 2º código (eletroacupuntura) não é possível. Apenas a acupuntura foi faturada.");
                break;

            case ModalidadeAtendimento.BsvComAcupuntura:
                if (cfg.FaturaBsv)
                {
                    r.Codigos.Add(Codigo(TipoCodigo.Bsv, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica,
                        cfg.InverteDatasBsv ? "1º código (BSV). No sistema do convênio, inverter as datas (acupuntura para hoje)." : null));
                    if (podeSegundo)
                        r.Codigos.Add(Acupuntura(r, cfg, contexto, OrdemCodigo.Segundo, depois, cfg.FormaSegundoCodigo,
                            $"2º código (acupuntura) em +{contexto.DiasSegundoCodigo}d — {InstrucaoObtencao(cfg.FormaSegundoCodigo)}."));
                    else if (cfg.TemSegundoCodigo)
                        r.Avisos.Add("Paciente sem app: 2º código (acupuntura) não é possível. Apenas o BSV foi faturado.");
                }
                else
                {
                    // Convênio não fatura BSV: fatura a acupuntura do dia.
                    r.Codigos.Add(Acupuntura(r, cfg, contexto, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica));
                    r.Avisos.Add("Este convênio não fatura BSV — apenas a acupuntura foi faturada.");
                }
                break;

            case ModalidadeAtendimento.BsvApenas:
                if (cfg.FaturaBsv)
                    r.Codigos.Add(Codigo(TipoCodigo.Bsv, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica));
                else
                    r.Avisos.Add("Este convênio não fatura BSV — nada foi faturado neste atendimento.");
                break;

            case ModalidadeAtendimento.Consulta:
                RegraConsultaAvulsa.Aplicar(r, atendimento, Categoria.Amarela);
                break;
        }

        return r;
    }

    /// <summary>
    /// A guia de acupuntura, respeitando o intervalo mínimo do convênio. Adiada, ela leva na
    /// descrição o porquê — é o que a linha da pendência mostra no dia em que ela aparece.
    /// </summary>
    private static CodigoFaturamento Acupuntura(ResultadoFaturamento r, ConfiguracaoRegraGenerica cfg,
        ContextoFaturamento contexto, OrdemCodigo ordem, DateOnly desejada, FormaObtencao forma, string? descricao = null)
    {
        var data = Liberar(r, contexto, TipoCodigo.Acupuntura, desejada, cfg.IntervaloAcupunturaDias, out var motivo);
        if (motivo is not null)
            descricao = (descricao is null ? "" : descricao + " ") + $"Acupuntura liberada em {Data(data)} — {motivo}";
        return Codigo(TipoCodigo.Acupuntura, ordem, data, forma, descricao);
    }

    /// <summary>
    /// A primeira data, a partir de <paramref name="desejada"/>, em que o convênio autoriza mais
    /// um código do <paramref name="tipo"/>: nenhuma outra guia do mesmo tipo do paciente pode
    /// estar a menos de <paramref name="intervaloDias"/> dias dela — antes OU depois, porque um
    /// lançamento retroativo também não pode encostar numa guia já adiada para a frente.
    /// Guias "não aplicável" (estornadas, sem app) não contam: não foram autorizadas.
    /// Quando adia, avisa e devolve o motivo; sem intervalo ou sem conflito, devolve a data pedida.
    /// </summary>
    internal static DateOnly Liberar(ResultadoFaturamento r, ContextoFaturamento contexto, TipoCodigo tipo,
        DateOnly desejada, int intervaloDias, out string? motivo)
    {
        motivo = null;
        if (intervaloDias <= 0) return desejada;

        var datas = contexto.CodigosRecentes
            .Where(c => c.Tipo == tipo && c.Status != StatusCodigo.NaoAplicavel)
            .Select(c => c.DataPrevistaFaturamento)
            .ToList();

        var candidata = desejada;
        DateOnly? anterior = null;
        while (true)
        {
            var conflitos = datas.Where(d => Math.Abs(d.DayNumber - candidata.DayNumber) < intervaloDias).ToList();
            if (conflitos.Count == 0) break;
            anterior = conflitos.Max();
            candidata = anterior.Value.AddDays(intervaloDias);
        }

        if (anterior is { } a)
        {
            var nome = tipo == TipoCodigo.Eletroacupuntura ? "eletroacupuntura" : "acupuntura";
            var frequencia = intervaloDias == 1 ? "por dia" : $"a cada {intervaloDias} dias";
            motivo = $"o convênio só autoriza uma {nome} {frequencia}, e a anterior está em {Data(a)}.";
            r.Avisos.Add($"A guia de {nome} fica para {Data(candidata)}: {motivo}");
        }
        return candidata;
    }

    /// <summary>dd/MM/aaaa fixo — a barra não pode depender da cultura da máquina.</summary>
    private static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

    private static string Capitalizar(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string InstrucaoObtencao(FormaObtencao forma) => forma switch
    {
        FormaObtencao.Sistema => "solicitar diretamente no sistema do convênio",
        FormaObtencao.App => "ligar para o paciente e pedir o QR Code pelo app",
        FormaObtencao.Ligacao => "ligar para o paciente e pedir a autorização",
        _ => "obter conforme o convênio"
    };

    private static CodigoFaturamento Codigo(TipoCodigo tipo, OrdemCodigo ordem, DateOnly data, FormaObtencao forma, string? descricao = null)
        => new()
        {
            Tipo = tipo,
            Ordem = ordem,
            DataPrevistaFaturamento = data,
            FormaObtencao = forma,
            Status = StatusCodigo.Aberto,
            Descricao = descricao
        };
}
