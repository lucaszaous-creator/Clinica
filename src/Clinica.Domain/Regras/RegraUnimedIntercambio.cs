using Clinica.Domain.Entities;

namespace Clinica.Domain.Regras;

/// <summary>
/// Unimed Costa do Sol Intercâmbio. Consulta a cada 22 dias.
/// O 2º código é sempre obtido diretamente pelo sistema após 24h (não precisa ligar para o paciente). VERDE.
/// </summary>
public sealed class RegraUnimedIntercambio : IRegraConvenio
{
    public Convenio Convenio => Convenio.UnimedIntercambio;

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
                r.Codigos.Add(Codigo(TipoCodigo.Acupuntura, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica));
                r.Codigos.Add(Codigo(TipoCodigo.Eletroacupuntura, OrdemCodigo.Segundo, amanha, FormaObtencao.Sistema,
                    "Após 24h: solicitar o 2º código da eletroacupuntura diretamente pelo sistema Unimed (não precisa ligar)."));
                r.Categoria = Categoria.Verde;
                r.Avisos.Add("2º código (eletroacupuntura) previsto para +24h — solicitar pelo sistema.");
                break;

            case ModalidadeAtendimento.BsvComAcupuntura:
                r.Codigos.Add(Codigo(TipoCodigo.Bsv, OrdemCodigo.Primeiro, hoje, FormaObtencao.NaoAplica,
                    "1º código do dia (BSV). No sistema do convênio, inverter as datas (acupuntura para hoje)."));
                r.Codigos.Add(Codigo(TipoCodigo.Acupuntura, OrdemCodigo.Segundo, amanha, FormaObtencao.Sistema,
                    "2º código (acupuntura) em +24h — liberar diretamente no sistema (Intercâmbio, sem avisar o paciente)."));
                r.Categoria = Categoria.Verde;
                r.Avisos.Add("2º código previsto para +24h — liberar pelo sistema.");
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
