using Clinica.Domain.Entities;

namespace Clinica.Domain.Regras;

/// <summary>
/// Unimed Costa do Sol (Padrão). Consulta a cada 22 dias.
/// - Acu + Eletro COM app  → 1º código hoje + 2º código em +24h (obter via app/ligação). VERDE.
/// - Acu + Eletro SEM app  → apenas acupuntura (não realizar eletro, sem 2º código). AMARELA.
/// - BSV + acupuntura      → inverte datas no sistema; 2º código depende do app.
/// Nas modalidades duplas, a ordem (qual código sai hoje e qual em +24h) pode ser escolhida
/// no lançamento (contexto.PrimeiroCodigoPreferido); o padrão é acupuntura/BSV primeiro.
/// </summary>
public sealed class RegraUnimedPadrao : IRegraConvenio
{
    public Convenio Convenio => Convenio.UnimedPadrao;

    public ResultadoFaturamento Gerar(Paciente paciente, Atendimento atendimento, ContextoFaturamento contexto)
    {
        var r = new ResultadoFaturamento();
        var hoje = atendimento.Data;
        var amanha = hoje.AddDays(contexto.DiasSegundoCodigo);

        switch (atendimento.Modalidade)
        {
            case ModalidadeAtendimento.AcupunturaSimples:
                r.Codigos.Add(Codigo(TipoCodigo.Acupuntura, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica));
                r.Categoria = Categoria.Amarela;
                r.Avisos.Add("Apenas acupuntura: não há 2º código. Categoria AMARELA.");
                break;

            case ModalidadeAtendimento.AcupunturaComEletro:
                if (paciente.PossuiApp)
                {
                    var (primeiro, segundo) = OrdemDupla(contexto, TipoCodigo.Acupuntura, TipoCodigo.Eletroacupuntura);
                    r.Codigos.Add(Codigo(primeiro, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica));
                    r.Codigos.Add(Codigo(segundo, OrdemCodigo.Segundo, amanha, FormaObtencao.Ligacao,
                        $"Após 24h: ligar para o paciente e solicitar o QR Code ({NomeTipo(segundo)}) pelo app."));
                    r.Categoria = Categoria.Verde;
                    r.Avisos.Add($"2º código ({NomeTipo(segundo)}) previsto para +24h — ligar para o paciente e pedir o QR Code.");
                }
                else
                {
                    // Sem app: NÃO realizar eletroacupuntura; não é possível gerar 2º código.
                    r.Codigos.Add(Codigo(TipoCodigo.Acupuntura, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica));
                    r.Categoria = Categoria.Amarela;
                    r.Avisos.Add("Paciente SEM app: realizar APENAS acupuntura. 2º código (eletro) não é possível. Categoria AMARELA.");
                }
                break;

            case ModalidadeAtendimento.BsvComAcupuntura:
                // Na prática: BSV + acupuntura no mesmo dia. No sistema, inverter as datas.
                if (paciente.PossuiApp)
                {
                    var (primeiro, segundo) = OrdemDupla(contexto, TipoCodigo.Bsv, TipoCodigo.Acupuntura);
                    r.Codigos.Add(Codigo(primeiro, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica,
                        $"1º código do dia ({NomeTipo(primeiro)}). No sistema do convênio, lançar {NomeTipo(segundo)} para hoje (inversão de datas)."));
                    r.Codigos.Add(Codigo(segundo, OrdemCodigo.Segundo, amanha, FormaObtencao.Ligacao,
                        $"2º código ({NomeTipo(segundo)}) em +24h. No sistema do convênio, lançar {NomeTipo(primeiro)} para amanhã. Ligar para o paciente e pedir autorização no app."));
                    r.Categoria = Categoria.Verde;
                    r.Avisos.Add("2º código previsto para +24h — ligar para o paciente e pedir autorização no app.");
                }
                else
                {
                    r.Codigos.Add(Codigo(TipoCodigo.Bsv, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica,
                        "1º código do dia (BSV). Sem app: 2º código (acupuntura) não é possível."));
                    r.Categoria = Categoria.Amarela;
                    r.Avisos.Add("Paciente SEM app: 2º código não é possível. O BSV (1º código) será faturado normalmente. Categoria AMARELA.");
                }
                break;

            case ModalidadeAtendimento.BsvApenas:
                r.Codigos.Add(Codigo(TipoCodigo.Bsv, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica));
                r.Categoria = Categoria.Amarela;
                break;
        }

        return r;
    }

    /// <summary>
    /// Define qual tipo é o 1º código (hoje) e qual é o 2º (+24h) numa modalidade dupla.
    /// Só inverte a ordem padrão quando o convênio libera primeiro o outro código
    /// (contexto.PrimeiroCodigoPreferido aponta para <paramref name="padraoSegundo"/>).
    /// </summary>
    private static (TipoCodigo primeiro, TipoCodigo segundo) OrdemDupla(
        ContextoFaturamento contexto, TipoCodigo padraoPrimeiro, TipoCodigo padraoSegundo)
        => contexto.PrimeiroCodigoPreferido == padraoSegundo
            ? (padraoSegundo, padraoPrimeiro)
            : (padraoPrimeiro, padraoSegundo);

    private static string NomeTipo(TipoCodigo t) => t switch
    {
        TipoCodigo.Acupuntura => "acupuntura",
        TipoCodigo.Eletroacupuntura => "eletroacupuntura",
        TipoCodigo.Bsv => "BSV",
        _ => t.ToString()
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
