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
        var codigoEspecialidade = atendimento.EspecialidadeConsultaCodigo
            ?? atendimento.EspecialidadeConsulta?.ToString();

        r.Categoria = categoria;
        r.Codigos.Add(new CodigoFaturamento
        {
            Tipo = TipoCodigo.Consulta,
            Especialidade = atendimento.EspecialidadeConsulta,
            EspecialidadeCodigo = codigoEspecialidade,
            Ordem = OrdemCodigo.Primeiro,
            DataPrevistaFaturamento = atendimento.Data,
            FormaObtencao = FormaObtencao.NaoAplica,
            Status = StatusCodigo.Aberto,
            Descricao = codigoEspecialidade is not null
                ? $"Consulta de {CatalogoEspecialidades.Nome(codigoEspecialidade)}."
                : "Consulta (especialidade não informada)."
        });

        if (codigoEspecialidade is null)
            r.Avisos.Add("Especialidade da consulta não informada — informe para constar no relatório por especialidade.");
    }
}
