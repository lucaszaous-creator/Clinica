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
                r.Codigos.Add(Codigo(TipoCodigo.Acupuntura, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica));
                break;

            case ModalidadeAtendimento.AcupunturaComEletro:
                r.Codigos.Add(Codigo(TipoCodigo.Acupuntura, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica));
                if (cfg.FazEletro && podeSegundo)
                    r.Codigos.Add(Codigo(TipoCodigo.Eletroacupuntura, OrdemCodigo.Segundo, depois, cfg.FormaSegundoCodigo,
                        $"2º código (eletroacupuntura) em +{contexto.DiasSegundoCodigo}d — {InstrucaoObtencao(cfg.FormaSegundoCodigo)}."));
                else if (cfg.FazEletro && cfg.TemSegundoCodigo)
                    r.Avisos.Add("Paciente sem app: 2º código (eletroacupuntura) não é possível. Apenas a acupuntura foi faturada.");
                break;

            case ModalidadeAtendimento.BsvComAcupuntura:
                if (cfg.FaturaBsv)
                {
                    r.Codigos.Add(Codigo(TipoCodigo.Bsv, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica,
                        cfg.InverteDatasBsv ? "1º código (BSV). No sistema do convênio, inverter as datas (acupuntura para hoje)." : null));
                    if (podeSegundo)
                        r.Codigos.Add(Codigo(TipoCodigo.Acupuntura, OrdemCodigo.Segundo, depois, cfg.FormaSegundoCodigo,
                            $"2º código (acupuntura) em +{contexto.DiasSegundoCodigo}d — {InstrucaoObtencao(cfg.FormaSegundoCodigo)}."));
                    else if (cfg.TemSegundoCodigo)
                        r.Avisos.Add("Paciente sem app: 2º código (acupuntura) não é possível. Apenas o BSV foi faturado.");
                }
                else
                {
                    // Convênio não fatura BSV: fatura a acupuntura do dia.
                    r.Codigos.Add(Codigo(TipoCodigo.Acupuntura, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica));
                    r.Avisos.Add("Este convênio não fatura BSV — apenas a acupuntura foi faturada.");
                }
                break;

            case ModalidadeAtendimento.BsvApenas:
                if (cfg.FaturaBsv)
                    r.Codigos.Add(Codigo(TipoCodigo.Bsv, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica));
                else
                    r.Avisos.Add("Este convênio não fatura BSV — nada foi faturado neste atendimento.");
                break;
        }

        return r;
    }

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
