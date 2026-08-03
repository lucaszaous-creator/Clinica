using Clinica.Domain.Avaliacoes;

namespace Clinica.Domain.Medidas;

/// <summary>
/// Uma faixa de referência de uma medida, com a leitura publicada dela.
///
/// <paramref name="De"/> é inclusivo e <paramref name="Ate"/> é EXCLUSIVO — as faixas
/// clínicas se encaixam por corte ("IMC 25 já é sobrepeso"), e faixa com as duas pontas
/// inclusivas deixaria buraco ou sobreposição em cada limite. <c>null</c> em qualquer das
/// pontas significa "sem limite deste lado".
/// </summary>
public sealed record FaixaMedida(
    string Nome, decimal? De, decimal? Ate, GravidadeFaixa Gravidade, string? Interpretacao = null)
{
    public bool Contem(decimal valor)
        => (De is null || valor >= De) && (Ate is null || valor < Ate);
}

/// <summary>
/// O que se mede num paciente, com unidade, plausibilidade e faixas de referência.
///
/// Mora em CÓDIGO e não em tabela, pelo mesmo desenho das escalas clínicas e do motor de
/// regras de convênio: o corte do IMC e o limite da pressão arterial não são configuração
/// da clínica — são definição publicada, e deixar editar produziria um número que continua
/// se chamando "IMC" sem ser um. A clínica escolhe QUAIS medidas registrar; não redefine o
/// que cada uma significa.
/// </summary>
public sealed class TipoMedida
{
    public required string Codigo { get; init; }

    public required string Nome { get; init; }

    /// <summary>Unidade em que o valor é digitado e gravado ("kg", "cm", "mmHg").</summary>
    public required string Unidade { get; init; }

    /// <summary>Casas decimais na exibição. Pressão e glicemia são inteiras; peso tem uma.</summary>
    public int Decimais { get; init; }

    /// <summary>
    /// Piso e teto de PLAUSIBILIDADE, não de normalidade: 250 kg é anormal e possível,
    /// 2500 kg é dedo escorregado no teclado. É a única recusa do serviço — o resto é
    /// leitura, e recusar valor anormal faria o sistema esconder justamente o paciente
    /// que precisa de atenção.
    /// </summary>
    public required decimal Minimo { get; init; }
    public required decimal Maximo { get; init; }

    /// <summary>
    /// A medida tem um segundo número indissociável do primeiro (a diastólica da pressão).
    /// Duas linhas separadas para 120/80 permitiriam gravar uma sem a outra, e meia
    /// pressão arterial não é medida nenhuma.
    /// </summary>
    public string? RotuloSegundoValor { get; init; }

    /// <summary>
    /// Subir é piorar (peso, cintura, glicemia). Falso quando é o contrário — hoje nenhuma
    /// medida do catálogo é assim, mas a curva já pergunta, como o Katz pergunta nas
    /// escalas, e descobrir isso na primeira medida invertida seria descobrir tarde.
    /// </summary>
    public bool PiorQuandoMaior { get; init; } = true;

    /// <summary>
    /// Não é digitada: o serviço a calcula de outras. O IMC é o caso — deixá-lo ser
    /// digitado abriria a porta para um IMC que não corresponde ao peso e à altura
    /// gravados logo ao lado, e a tela mostraria os três com a mesma cara de fato.
    /// </summary>
    public bool Derivada { get; init; }

    /// <summary>
    /// Faixas de referência gerais. Quando a leitura depende do sexo (a cintura), estas
    /// ficam vazias e valem as duas listas abaixo.
    /// </summary>
    public IReadOnlyList<FaixaMedida> Faixas { get; init; } = [];

    public IReadOnlyList<FaixaMedida> FaixasMasculino { get; init; } = [];
    public IReadOnlyList<FaixaMedida> FaixasFeminino { get; init; } = [];

    /// <summary>Explicação curta do que a medida é e como colhê-la.</summary>
    public string? Nota { get; init; }

    public bool TemSegundoValor => RotuloSegundoValor is not null;

    /// <summary>
    /// A faixa em que o valor cai, respeitando o sexo quando a medida distingue.
    ///
    /// Devolve <c>null</c> quando a medida não tem faixa publicada (o peso isolado não
    /// tem: 80 kg é obesidade em uma pessoa e magreza em outra — quem responde isso é o
    /// IMC). Faixa ausente e faixa normal são coisas diferentes, e inventar "normal" para
    /// o peso faria a tela afirmar o que ninguém apurou.
    /// </summary>
    public FaixaMedida? FaixaDe(decimal valor, Sexo? sexo)
    {
        var lista = FaixasMasculino.Count > 0 || FaixasFeminino.Count > 0
            ? sexo switch
            {
                Sexo.Masculino => FaixasMasculino,
                Sexo.Feminino => FaixasFeminino,
                // Sem sexo declarado não se escolhe uma das duas tabelas: a leitura sairia
                // pela metade das vezes errada, com aparência de exata.
                _ => []
            }
            : Faixas;

        return lista.FirstOrDefault(f => f.Contem(valor));
    }

    /// <summary>O valor como a tela e o PDF o escrevem, com a unidade.</summary>
    public string Formatar(decimal valor, decimal? segundo = null)
    {
        var texto = valor.ToString("N" + Decimais, Cultura);
        if (TemSegundoValor && segundo is { } s)
            texto += "/" + s.ToString("N" + Decimais, Cultura);
        return $"{texto} {Unidade}";
    }

    /// <summary>
    /// pt-BR FIXO, nunca a cultura da máquina: este texto é GRAVADO em documento e
    /// exportado em planilha, e dois postos escreveriam "78,5" e "78.5" na mesma coluna.
    /// É a mesma decisão do <c>TributoService</c>.
    /// </summary>
    internal static readonly System.Globalization.CultureInfo Cultura = new("pt-BR");
}

/// <summary>
/// As medidas que a clínica acompanha ao longo do tratamento (parcela 37).
///
/// Por que existe
/// --------------
/// A parcela 36 deu número às cinco especialidades da casa pelas escalas — e deixou de
/// fora o número mais básico de todos. O FINDRISC pergunta IMC e circunferência de cintura
/// para calcular o risco de diabete, o paciente responde, e nada disso ficava registrado
/// como SÉRIE: o escore era gravado e os dados que o produziram evaporavam. A endocrino
/// pesa o paciente toda consulta, a geriatria acompanha perda de peso, e as duas escreviam
/// "PA 140/90" em texto livre na evolução — que não desenha curva, não compara consulta
/// com consulta e não vai para o relatório. É a mesma tese da EVA, aplicada ao resto do
/// corpo.
///
/// As regras
/// ---------
/// - O catálogo é de CÓDIGO. Ver <see cref="TipoMedida"/>.
/// - Tudo o que descreve o tipo é COPIADO na medida (nome, unidade, faixa, interpretação),
///   como no protocolo do mapa corporal, no preço por convênio e na escala aplicada: mudar
///   o corte do IMC amanhã não pode reescrever a leitura do que se mediu hoje.
/// - <b>O IMC não se digita.</b> É derivado do peso e da altura mais recente, e vale só
///   como leitura — gravá-lo criaria um terceiro número capaz de contradizer os dois que o
///   originam, com a mesma cara de fato.
/// </summary>
public static class CatalogoMedidas
{
    public const string Peso = "PESO";
    public const string Altura = "ALTURA";
    public const string Imc = "IMC";
    public const string Cintura = "CINTURA";
    public const string PressaoArterial = "PA";
    public const string GlicemiaJejum = "GLICJEJUM";
    public const string HemoglobinaGlicada = "HBA1C";

    private static readonly TipoMedida[] Todos =
    [
        new TipoMedida
        {
            Codigo = Peso, Nome = "Peso", Unidade = "kg", Decimais = 1,
            Minimo = 0.5m, Maximo = 400m,
            // Sem faixa de propósito: peso isolado não se interpreta. Quem responde
            // "é muito?" é o IMC, que precisa da altura.
            Nota = "Sem sapatos, de preferência sempre na mesma balança."
        },
        new TipoMedida
        {
            Codigo = Altura, Nome = "Altura", Unidade = "cm", Decimais = 0,
            Minimo = 30m, Maximo = 250m,
            Nota = "Em centímetros. Alimenta o IMC — sem ela, o peso não vira leitura."
        },
        new TipoMedida
        {
            Codigo = Imc, Nome = "IMC", Unidade = "kg/m²", Decimais = 1,
            Minimo = 5m, Maximo = 100m, Derivada = true,
            Faixas =
            [
                new FaixaMedida("Baixo peso", null, 18.5m, GravidadeFaixa.Atencao,
                    "Abaixo da faixa de referência da OMS."),
                new FaixaMedida("Peso adequado", 18.5m, 25m, GravidadeFaixa.Normal,
                    "Dentro da faixa de referência da OMS."),
                new FaixaMedida("Sobrepeso", 25m, 30m, GravidadeFaixa.Atencao,
                    "Acima da faixa de referência da OMS."),
                new FaixaMedida("Obesidade grau I", 30m, 35m, GravidadeFaixa.Alerta),
                new FaixaMedida("Obesidade grau II", 35m, 40m, GravidadeFaixa.Alerta),
                new FaixaMedida("Obesidade grau III", 40m, null, GravidadeFaixa.Alerta)
            ],
            Nota = "Calculado do peso e da altura mais recente. Não se digita."
        },
        new TipoMedida
        {
            Codigo = Cintura, Nome = "Circunferência abdominal", Unidade = "cm", Decimais = 1,
            Minimo = 30m, Maximo = 250m,
            // Os cortes do FINDRISC, que é a escala que já pergunta esta medida — usar
            // aqui um corte diferente do da escala aplicada ao lado faria a mesma clínica
            // dar duas leituras da mesma fita métrica.
            FaixasMasculino =
            [
                new FaixaMedida("Dentro da referência", null, 94m, GravidadeFaixa.Normal),
                new FaixaMedida("Risco aumentado", 94m, 102m, GravidadeFaixa.Atencao),
                new FaixaMedida("Risco muito aumentado", 102m, null, GravidadeFaixa.Alerta)
            ],
            FaixasFeminino =
            [
                new FaixaMedida("Dentro da referência", null, 80m, GravidadeFaixa.Normal),
                new FaixaMedida("Risco aumentado", 80m, 88m, GravidadeFaixa.Atencao),
                new FaixaMedida("Risco muito aumentado", 88m, null, GravidadeFaixa.Alerta)
            ],
            Nota = "Na altura da cicatriz umbilical, ao fim de uma expiração normal. "
                   + "Os cortes variam com o sexo."
        },
        new TipoMedida
        {
            Codigo = PressaoArterial, Nome = "Pressão arterial", Unidade = "mmHg", Decimais = 0,
            Minimo = 40m, Maximo = 300m, RotuloSegundoValor = "Diastólica",
            // A faixa olha a SISTÓLICA. A diastólica alta com sistólica normal existe e é
            // menos comum; a leitura escrita ao lado do par não substitui quem lê o par.
            Faixas =
            [
                new FaixaMedida("Hipotensão", null, 90m, GravidadeFaixa.Atencao),
                new FaixaMedida("Ótima a normal", 90m, 130m, GravidadeFaixa.Normal),
                new FaixaMedida("Pré-hipertensão", 130m, 140m, GravidadeFaixa.Atencao),
                new FaixaMedida("Hipertensão", 140m, 180m, GravidadeFaixa.Alerta),
                new FaixaMedida("Hipertensão grave", 180m, null, GravidadeFaixa.Alerta,
                    "Valor de crise — confira a aferição e avalie encaminhamento.")
            ],
            Nota = "Sistólica e diastólica são um par: a leitura é da sistólica, "
                   + "e é o par que vai ao prontuário."
        },
        new TipoMedida
        {
            Codigo = GlicemiaJejum, Nome = "Glicemia de jejum", Unidade = "mg/dL", Decimais = 0,
            Minimo = 10m, Maximo = 900m,
            Faixas =
            [
                new FaixaMedida("Hipoglicemia", null, 70m, GravidadeFaixa.Alerta),
                new FaixaMedida("Dentro da referência", 70m, 100m, GravidadeFaixa.Normal),
                new FaixaMedida("Glicemia de jejum alterada", 100m, 126m, GravidadeFaixa.Atencao),
                new FaixaMedida("Faixa de diabete", 126m, null, GravidadeFaixa.Alerta,
                    "Um valor isolado não fecha diagnóstico — a confirmação é do exame.")
            ],
            Nota = "Jejum de 8 h. Anote o resultado do exame; o sistema registra, não diagnostica."
        },
        new TipoMedida
        {
            Codigo = HemoglobinaGlicada, Nome = "Hemoglobina glicada (HbA1c)", Unidade = "%", Decimais = 1,
            Minimo = 2m, Maximo = 20m,
            Faixas =
            [
                new FaixaMedida("Dentro da referência", null, 5.7m, GravidadeFaixa.Normal),
                new FaixaMedida("Pré-diabete", 5.7m, 6.5m, GravidadeFaixa.Atencao),
                new FaixaMedida("Faixa de diabete", 6.5m, null, GravidadeFaixa.Alerta)
            ],
            Nota = "Média glicêmica dos últimos três meses — é o número que mostra "
                   + "se o tratamento está funcionando."
        }
    ];

    /// <summary>Tudo o que a tela oferece para DIGITAR — o IMC fica de fora.</summary>
    public static IReadOnlyList<TipoMedida> Registraveis
        => Todos.Where(t => !t.Derivada).ToList();

    /// <summary>O catálogo inteiro, inclusive as derivadas.</summary>
    public static IReadOnlyList<TipoMedida> TodasAsMedidas => Todos;

    /// <summary>
    /// O tipo de um código, ou <c>null</c> quando ele não está mais no catálogo.
    ///
    /// Devolver null em vez de estourar é o que mantém legível a medida de um tipo
    /// retirado: o valor e o nome foram COPIADOS na gravação, então a série continua
    /// sendo lida sem o catálogo de hoje.
    /// </summary>
    public static TipoMedida? Obter(string? codigo)
        => string.IsNullOrWhiteSpace(codigo)
            ? null
            : Todos.FirstOrDefault(t => t.Codigo.Equals(codigo.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// IMC de um peso (kg) e uma altura (cm). Nulo quando falta um dos dois ou a altura
    /// não é plausível — dividir por zero devolveria infinito, que a tela exibiria com a
    /// mesma confiança de um número medido.
    /// </summary>
    public static decimal? CalcularImc(decimal? pesoKg, decimal? alturaCm)
    {
        if (pesoKg is not { } peso || alturaCm is not { } altura) return null;
        if (altura <= 0 || peso <= 0) return null;

        var metros = altura / 100m;
        return Math.Round(peso / (metros * metros), 1, MidpointRounding.AwayFromZero);
    }
}
