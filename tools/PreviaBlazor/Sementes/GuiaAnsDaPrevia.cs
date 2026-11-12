// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace PreviaBlazor.Sementes;

/// <summary>
/// A guia ANS que as sementes entregam ao dar baixa numa guia da Saúde Petrobras (nov/2026):
/// desde que a baixa dela pede a conferência da guia, semente que desse baixa só com o número
/// seria recusada pelo serviço — a mesma recusa que a clínica veria. Devolve nulo para as guias
/// que não a pedem, e aí a baixa segue como sempre.
/// </summary>
public static class GuiaAnsDaPrevia
{
    public static DadosGuiaConsultaAns? Para(CodigoFaturamento codigo, Paciente paciente, DateOnly dataAtendimento)
        => !GuiaAnsNaBaixa.Exige(codigo.Tipo, paciente.ConvenioCodigo, paciente.Convenio)
            ? null
            : new DadosGuiaConsultaAns
            {
                RegistroAns = "422631",
                NumeroCarteira = (paciente.Carteirinha ?? "000000000000").Replace(" ", string.Empty),
                ValidadeCarteira = paciente.ValidadeCarteirinha,
                NomeBeneficiario = paciente.Nome,
                CodigoPrestadorNaOperadora = "12345678000190",
                NomeContratado = "Clínica da Prévia Ltda",
                Cnes = GuiaAnsNaBaixa.CnesDeQuemNaoTem,
                NomeProfissional = "Dra. Helena Duarte",
                ConselhoProfissional = "06",
                NumeroConselho = "521234",
                UfConselho = "33",
                Cbos = "225105",
                IndicacaoAcidente = GuiaAnsNaBaixa.AcidentePadrao,
                RegimeAtendimento = GuiaAnsNaBaixa.RegimePadrao,
                DataAtendimento = dataAtendimento,
                CodigoTabela = GuiaAnsNaBaixa.TabelaPadrao,
                TipoConsulta = GuiaAnsNaBaixa.TipoConsultaPrimeira,
                CodigoProcedimento = "10101012",
                ValorProcedimento = 139.00m
            };
}
