using Clinica.Domain.Entities;

namespace Clinica.Domain.Regras;

/// <summary>
/// Amil. Consulta renova a cada 30 dias. Acupuntura: 1 código por semana.
/// NÃO realizar eletroacupuntura. NÃO existe 2º código. Todo paciente é AMARELO.
/// </summary>
public sealed class RegraAmil : IRegraConvenio
{
    public Convenio Convenio => Convenio.Amil;

    public ResultadoFaturamento Gerar(Paciente paciente, Atendimento atendimento, ContextoFaturamento contexto)
    {
        var r = new ResultadoFaturamento { Categoria = Categoria.Amarela };
        var hoje = atendimento.Data;

        switch (atendimento.Modalidade)
        {
            case ModalidadeAtendimento.AcupunturaSimples:
                r.Codigos.Add(Codigo(TipoCodigo.Acupuntura, hoje));
                break;

            case ModalidadeAtendimento.AcupunturaComEletro:
                // Amil não realiza eletroacupuntura: fatura somente a acupuntura.
                r.Codigos.Add(Codigo(TipoCodigo.Acupuntura, hoje));
                r.Avisos.Add("Amil NÃO realiza eletroacupuntura. Apenas a acupuntura (1 código/semana) foi faturada.");
                break;

            case ModalidadeAtendimento.BsvComAcupuntura:
                r.Codigos.Add(Codigo(TipoCodigo.Bsv, hoje));
                r.Codigos.Add(Codigo(TipoCodigo.Acupuntura, hoje));
                break;

            case ModalidadeAtendimento.BsvApenas:
                r.Codigos.Add(Codigo(TipoCodigo.Bsv, hoje));
                break;
        }

        return r;
    }

    private static CodigoFaturamento Codigo(TipoCodigo tipo, DateOnly data)
        => new()
        {
            Tipo = tipo,
            Ordem = OrdemCodigo.Primeiro,
            DataPrevistaFaturamento = data,
            FormaObtencao = FormaObtencao.NaoAplica,
            Status = StatusCodigo.Aberto
        };
}
