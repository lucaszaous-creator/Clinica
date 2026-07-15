using Clinica.Domain.Entities;

namespace Clinica.Domain.Regras;

/// <summary>
/// Unimed Costa do Sol (Padrão). Consulta a cada 22 dias.
/// - Acu + Eletro COM app  → 1º código acupuntura hoje + 2º código eletro em +24h (obter via app/ligação). VERDE.
/// - Acu + Eletro SEM app  → apenas acupuntura (não realizar eletro, sem 2º código). AMARELA.
/// - BSV + acupuntura      → inverte datas no sistema; 2º código depende do app.
/// </summary>
public sealed class RegraUnimedPadrao : IRegraConvenio
{
    public Convenio Convenio => Convenio.UnimedPadrao;

    public ResultadoFaturamento Gerar(Paciente paciente, Atendimento atendimento, ContextoFaturamento contexto)
    {
        var r = new ResultadoFaturamento();
        var hoje = atendimento.Data;
        var amanha = hoje.AddDays(1);

        switch (atendimento.Modalidade)
        {
            case ModalidadeAtendimento.AcupunturaSimples:
                r.Codigos.Add(Codigo(TipoCodigo.Acupuntura, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica));
                r.Categoria = Categoria.Amarela;
                r.Avisos.Add("Apenas acupuntura: não há 2º código. Categoria AMARELA.");
                break;

            case ModalidadeAtendimento.AcupunturaComEletro:
                r.Codigos.Add(Codigo(TipoCodigo.Acupuntura, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica));
                if (paciente.PossuiApp)
                {
                    r.Codigos.Add(Codigo(TipoCodigo.Eletroacupuntura, OrdemCodigo.Segundo, amanha, FormaObtencao.Ligacao,
                        "Após 24h: ligar para o paciente e solicitar o QR Code da eletroacupuntura pelo app."));
                    r.Categoria = Categoria.Verde;
                    r.Avisos.Add("2º código (eletroacupuntura) previsto para +24h — ligar para o paciente e pedir o QR Code.");
                }
                else
                {
                    // Sem app: NÃO realizar eletroacupuntura; não é possível gerar 2º código.
                    r.Categoria = Categoria.Amarela;
                    r.Avisos.Add("Paciente SEM app: realizar APENAS acupuntura. 2º código (eletro) não é possível. Categoria AMARELA.");
                }
                break;

            case ModalidadeAtendimento.BsvComAcupuntura:
                // Na prática: BSV + acupuntura no mesmo dia. No sistema, inverter as datas.
                r.Codigos.Add(Codigo(TipoCodigo.Bsv, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica,
                    "1º código do dia (BSV). No sistema do convênio, lançar a acupuntura para hoje (inversão de datas)."));
                if (paciente.PossuiApp)
                {
                    r.Codigos.Add(Codigo(TipoCodigo.Acupuntura, OrdemCodigo.Segundo, amanha, FormaObtencao.Ligacao,
                        "2º código (acupuntura) em +24h. No sistema do convênio, lançar o BSV para amanhã. Ligar para o paciente e pedir autorização no app."));
                    r.Categoria = Categoria.Verde;
                    r.Avisos.Add("2º código previsto para +24h — ligar para o paciente e pedir autorização no app.");
                }
                else
                {
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
