using Clinica.Domain.Entities;

namespace Clinica.Domain.Regras;

/// <summary>
/// Tratamento comum da modalidade Consulta (consulta médica avulsa), igual em todos os convênios:
/// um único código de consulta, faturável no dia, carregando a especialidade informada no
/// lançamento — é ela que permite saber quantas consultas de cada especialidade a clínica fez.
/// </summary>
public static class RegraConsultaAvulsa
{
    public static void Aplicar(ResultadoFaturamento r, Atendimento atendimento, Categoria categoria)
    {
        r.Categoria = categoria;
        r.Codigos.Add(new CodigoFaturamento
        {
            Tipo = TipoCodigo.Consulta,
            Especialidade = atendimento.EspecialidadeConsulta,
            Ordem = OrdemCodigo.Primeiro,
            DataPrevistaFaturamento = atendimento.Data,
            FormaObtencao = FormaObtencao.NaoAplica,
            Status = StatusCodigo.Aberto,
            Descricao = atendimento.EspecialidadeConsulta is { } esp
                ? $"Consulta de {EspecialidadeInfo.NomeExibicao(esp)}."
                : "Consulta (especialidade não informada)."
        });

        if (atendimento.EspecialidadeConsulta is null)
            r.Avisos.Add("Especialidade da consulta não informada — informe para constar no relatório por especialidade.");
    }
}
