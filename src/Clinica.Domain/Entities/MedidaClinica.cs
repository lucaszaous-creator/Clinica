using Clinica.Domain.Avaliacoes;
using Clinica.Domain.Medidas;

namespace Clinica.Domain.Entities;

/// <summary>
/// Uma medida colhida de um paciente num dia — peso, cintura, pressão, glicemia (parcela 37).
///
/// A regra que governa a entidade é a mesma da avaliação clínica: <b>tudo o que descreve o
/// tipo é COPIADO na colheita</b>. Nome, unidade, faixa e interpretação ficam gravados
/// aqui. Se a linha apontasse para o catálogo, mexer no corte do IMC amanhã reescreveria a
/// leitura de uma consulta de dois anos atrás — e prontuário descreve o que ACONTECEU.
///
/// O que NÃO se copia é o valor derivado: o IMC é calculado na leitura, do peso com a
/// altura mais recente. Gravá-lo criaria um terceiro número livre para contradizer os dois
/// que o originam.
///
/// O vínculo com <see cref="Evolucao"/> é opcional e N:1 — na mesma consulta se afere
/// pressão e peso, e o paciente pode passar na recepção só para se pesar, sem sessão
/// escrita nenhuma.
/// </summary>
public class MedidaClinica
{
    public int Id { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Quem colheu. Null quando a clínica ainda não cadastrou a equipe.</summary>
    public int? ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }

    /// <summary>Sessão do prontuário a que a medida pertence, quando há uma.</summary>
    public int? EvolucaoId { get; set; }
    public Evolucao? Evolucao { get; set; }

    public DateOnly Data { get; set; }

    /// <summary>Código do tipo no <see cref="CatalogoMedidas"/> (ex.: <c>PESO</c>).</summary>
    public string TipoCodigo { get; set; } = string.Empty;

    /// <summary>Nome do tipo no dia da colheita — copiado.</summary>
    public string TipoNome { get; set; } = string.Empty;

    /// <summary>Unidade no dia da colheita — copiada.</summary>
    public string Unidade { get; set; } = string.Empty;

    public decimal Valor { get; set; }

    /// <summary>
    /// A segunda metade do par, quando o tipo tem uma (a diastólica). Fica na MESMA linha
    /// porque 120/80 é uma medida só: em duas linhas seria possível gravar a sistólica de
    /// hoje ao lado da diastólica da semana passada.
    /// </summary>
    public decimal? ValorSecundario { get; set; }

    /// <summary>Nome da faixa em que o valor caiu — copiado. Null quando o tipo não tem faixa.</summary>
    public string? FaixaNome { get; set; }

    /// <summary>Leitura publicada da faixa — copiada.</summary>
    public string? FaixaInterpretacao { get; set; }

    /// <summary>Gravidade da faixa no dia — copiada.</summary>
    public GravidadeFaixa FaixaGravidade { get; set; } = GravidadeFaixa.Normal;

    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    /// <summary>
    /// Quando a colheita foi CANCELADA. Não se apaga (parcela 52): a série é o dado, e
    /// medida que some da curva reescreve a história do tratamento — um peso retirado
    /// muda a inclinação de tudo o que veio depois.
    /// </summary>
    public DateTime? CanceladaEm { get; set; }

    public string? MotivoCancelamento { get; set; }

    public string? CanceladaPor { get; set; }

    public bool Cancelada => CanceladaEm is not null;

    /// <summary>O par completo, como o prontuário o escreve ("128/84 mmHg").</summary>
    public string ValorFormatado
    {
        get
        {
            var tipo = CatalogoMedidas.Obter(TipoCodigo);
            if (tipo is not null) return tipo.Formatar(Valor, ValorSecundario);

            // Tipo fora do catálogo de hoje: a linha continua legível pelo que foi copiado.
            var texto = Valor.ToString("0.##", CulturaFixa);
            if (ValorSecundario is { } s) texto += "/" + s.ToString("0.##", CulturaFixa);
            return string.IsNullOrWhiteSpace(Unidade) ? texto : $"{texto} {Unidade}";
        }
    }

    /// <summary>A colheita tem leitura publicada — a tela pinta a faixa.</summary>
    public bool TemFaixa => !string.IsNullOrWhiteSpace(FaixaNome);

    private static readonly System.Globalization.CultureInfo CulturaFixa = new("pt-BR");
}
