using Clinica.Domain.Entities;

namespace Clinica.Domain.Regras;

/// <summary>
/// Petrobras (Código Vermelho). NÃO possui código de acupuntura próprio nem eletroacupuntura.
/// - BSV: 1 sessão por semana.
/// - Acupuntura: faturada como consulta de especialidade diferente, 1 por especialidade/mês.
///   Rotação: Psiquiatria → Geriatria → Ginecologia (só mulher).
///   Mulher = 3 sessões/mês; Homem = 2 sessões/mês (sem Ginecologia). 4ª sessão não é possível.
/// </summary>
public sealed class RegraPetrobras : IRegraConvenio
{
    public Convenio Convenio => Convenio.Petrobras;

    public ResultadoFaturamento Gerar(Paciente paciente, Atendimento atendimento, ContextoFaturamento contexto)
    {
        var r = new ResultadoFaturamento { Categoria = Categoria.Vermelha };
        var hoje = atendimento.Data;

        // Consulta avulsa: código único com a especialidade informada; não participa da rotação mensal.
        if (atendimento.Modalidade == ModalidadeAtendimento.Consulta)
        {
            RegraConsultaAvulsa.Aplicar(r, atendimento, Categoria.Vermelha);
            return r;
        }

        var fazBsv = atendimento.Modalidade is ModalidadeAtendimento.BsvApenas or ModalidadeAtendimento.BsvComAcupuntura;
        var fazAcupuntura = atendimento.Modalidade is ModalidadeAtendimento.AcupunturaSimples
            or ModalidadeAtendimento.AcupunturaComEletro or ModalidadeAtendimento.BsvComAcupuntura;

        if (atendimento.Modalidade == ModalidadeAtendimento.AcupunturaComEletro)
            r.Avisos.Add("Petrobras NÃO realiza eletroacupuntura. Apenas a acupuntura foi considerada.");

        if (fazBsv)
        {
            r.Codigos.Add(new CodigoFaturamento
            {
                Tipo = TipoCodigo.Bsv,
                Ordem = OrdemCodigo.Primeiro,
                DataPrevistaFaturamento = hoje,
                FormaObtencao = FormaObtencao.NaoAplica,
                Status = StatusCodigo.Aberto,
                Descricao = "BSV Petrobras: 1 sessão por semana."
            });
        }

        if (fazAcupuntura)
        {
            var especialidade = ProximaEspecialidadeDisponivel(paciente.Sexo, contexto);
            if (especialidade is not null)
            {
                r.Codigos.Add(new CodigoFaturamento
                {
                    Tipo = TipoCodigo.ConsultaEspecialidade,
                    Especialidade = especialidade,
                    EspecialidadeCodigo = especialidade.ToString(),
                    Ordem = OrdemCodigo.Primeiro,
                    DataPrevistaFaturamento = hoje,
                    FormaObtencao = FormaObtencao.NaoAplica,
                    Status = StatusCodigo.Aberto,
                    Descricao = $"Acupuntura faturada como consulta de {especialidade} (Petrobras não possui código de acupuntura)."
                });
            }
            else
            {
                var limite = LimiteSessoesMes(paciente.Sexo);
                r.Codigos.Add(new CodigoFaturamento
                {
                    Tipo = TipoCodigo.ConsultaEspecialidade,
                    Ordem = OrdemCodigo.Primeiro,
                    DataPrevistaFaturamento = hoje,
                    FormaObtencao = FormaObtencao.NaoAplica,
                    Status = StatusCodigo.NaoAplicavel,
                    Descricao = $"Sem especialidade disponível no mês (limite de {limite} sessões atingido). Acupuntura não pode ser faturada."
                });
                r.Avisos.Add($"Limite mensal de acupuntura atingido ({limite} sessões). Não há especialidade disponível para faturar.");
            }
        }

        return r;
    }

    /// <summary>Especialidades permitidas na ordem de rotação, conforme o sexo.</summary>
    private static IReadOnlyList<Especialidade> EspecialidadesPermitidas(Sexo sexo) => sexo == Sexo.Feminino
        ? new[] { Especialidade.Psiquiatria, Especialidade.Geriatria, Especialidade.Ginecologia }
        : new[] { Especialidade.Psiquiatria, Especialidade.Geriatria };

    private static int LimiteSessoesMes(Sexo sexo) => EspecialidadesPermitidas(sexo).Count;

    /// <summary>Retorna a próxima especialidade ainda não usada no mês, ou null se o limite foi atingido.</summary>
    private static Especialidade? ProximaEspecialidadeDisponivel(Sexo sexo, ContextoFaturamento contexto)
    {
        var usadas = contexto.CodigosNoMes
            .Where(c => c.Tipo == TipoCodigo.ConsultaEspecialidade
                        && c.Status != StatusCodigo.NaoAplicavel
                        && c.Especialidade is not null)
            .Select(c => c.Especialidade!.Value)
            .ToHashSet();

        foreach (var esp in EspecialidadesPermitidas(sexo))
            if (!usadas.Contains(esp))
                return esp;

        return null;
    }
}
