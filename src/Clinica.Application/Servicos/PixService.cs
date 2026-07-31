using System.Globalization;
using System.Text;

namespace Clinica.Application.Servicos;

/// <summary>Tipo da chave Pix, deduzido do formato do que a clínica digitou.</summary>
public enum TipoChavePix
{
    Cpf,
    Cnpj,
    Email,
    Telefone,
    Aleatoria,
    Desconhecida
}

/// <summary>O código Pix pronto, do jeito que ele chega ao paciente.</summary>
public sealed record CobrancaPix(
    string CopiaECola,
    decimal? Valor,
    string Recebedor,
    string Chave,
    TipoChavePix TipoChave,
    string? Identificador)
{
    /// <summary>
    /// A matriz do QR Code, linha a linha (<c>true</c> = módulo escuro).
    /// Preenchida por quem for desenhar; o payload sozinho já resolve o copia-e-cola.
    /// </summary>
    public IReadOnlyList<string> Linhas { get; init; } = [];
}

/// <summary>
/// O código Pix da clínica (parcela 34).
///
/// `FormaPagamento.Pix` existe no sistema desde a parcela 4 e é a forma como a maior
/// parte dos pacientes paga hoje — mas o sistema só REGISTRAVA que foi Pix. A chave
/// ficava num papel colado no monitor, e a secretária a ditava de cabeça: é assim que
/// dinheiro cai na conta errada, e é assim que a clínica descobre no fim do mês que uma
/// sessão nunca foi paga porque o paciente digitou um dígito a mais.
///
/// **Não há integração com banco nenhum, e isso é decisão.** O que este serviço faz é o
/// BR Code estático do Banco Central — o mesmo "copia e cola" que todo aplicativo de
/// banco lê —, montado inteiramente offline. Integrar com PSP exigiria credencial,
/// certificado e um contrato por banco; o código estático resolve o balcão hoje, sem
/// nada disso e sem dependência nova num app que se auto-atualiza.
///
/// O que ele NÃO faz, e a tela precisa dizer: **ele não confirma pagamento.** Quem
/// confere o recebimento continua sendo quem olha o extrato — prometer baixa automática
/// aqui seria mentir sobre o que este código garante.
/// </summary>
public sealed class PixService
{
    /// <summary>Identificador da carteira Pix dentro do padrão EMV.</summary>
    private const string Gui = "br.gov.bcb.pix";

    /// <summary>Real brasileiro, pela ISO 4217.</summary>
    private const string MoedaReal = "986";

    /// <summary>Sem identificador próprio, o padrão manda três asteriscos.</summary>
    private const string SemIdentificador = "***";

    /// <summary>
    /// Monta o código Pix.
    /// </summary>
    /// <param name="chave">Chave Pix da clínica (CPF, CNPJ, e-mail, telefone ou aleatória).</param>
    /// <param name="recebedor">Nome que aparece no aplicativo do paciente. Máx. 25 caracteres.</param>
    /// <param name="cidade">Cidade do recebedor. Máx. 15 caracteres.</param>
    /// <param name="valor">
    /// Valor da cobrança. Nulo deixa o paciente digitar — o que é certo para doação e
    /// errado para sessão: valor em aberto é a origem do pagamento a menos que ninguém
    /// confere.
    /// </param>
    /// <param name="identificador">
    /// Referência da cobrança (o número do recibo, por exemplo). É o que permite casar o
    /// Pix com a sessão no extrato; sem ele, o extrato traz só o nome do pagador.
    /// </param>
    /// <param name="tipoDeclarado">
    /// Tipo da chave, quando a clínica quiser tirar a dúvida do sistema. CPF e celular
    /// têm os mesmos onze dígitos, e é o único caso em que a dedução pode errar.
    /// </param>
    public CobrancaPix Gerar(
        string chave, string recebedor, string cidade,
        decimal? valor = null, string? identificador = null,
        TipoChavePix? tipoDeclarado = null)
    {
        if (string.IsNullOrWhiteSpace(chave))
            throw new InvalidOperationException(
                "Sem a chave Pix o código não existe. Cadastre a chave da clínica em Configurações.");

        if (valor is <= 0m)
            throw new InvalidOperationException(
                "Cobrança de Pix com valor zero ou negativo não é cobrança.");

        var chaveLimpa = Normalizar(chave);
        var tipo = tipoDeclarado ?? Classificar(chaveLimpa);

        // CPF, CNPJ e telefone são digitados com pontuação e precisam ir SEM ela — o
        // aplicativo do banco recusa o código com um ponto a mais, e a mensagem que ele
        // dá não ajuda ninguém a descobrir por quê.
        var chaveNoCodigo = tipo switch
        {
            TipoChavePix.Cpf or TipoChavePix.Cnpj => SoDigitos(chaveLimpa),
            TipoChavePix.Telefone => ComDdi(chaveLimpa),
            _ => chaveLimpa
        };

        var conta = Campo("00", Gui) + Campo("01", chaveNoCodigo);

        var extras = Campo("05",
            string.IsNullOrWhiteSpace(identificador)
                ? SemIdentificador
                : Alfanumerico(identificador, 25));

        var corpo =
            Campo("00", "01") +
            Campo("01", "12") +
            Campo("26", conta) +
            Campo("52", "0000") +
            Campo("53", MoedaReal) +
            (valor is { } v ? Campo("54", v.ToString("0.00", CultureInfo.InvariantCulture)) : "") +
            Campo("58", "BR") +
            Campo("59", Texto(recebedor, 25, "RECEBEDOR")) +
            Campo("60", Texto(cidade, 15, "CIDADE")) +
            Campo("62", extras);

        // O CRC entra no fim, e ele cobre o próprio cabeçalho "6304" — por isso o
        // cálculo é feito sobre o corpo JÁ com ele anexado.
        var comCabecalhoCrc = corpo + "6304";
        var codigo = comCabecalhoCrc + Crc16(comCabecalhoCrc);

        return new CobrancaPix(codigo, valor, Texto(recebedor, 25, "RECEBEDOR"),
            chaveLimpa, tipo, string.IsNullOrWhiteSpace(identificador) ? null : identificador);
    }

    // ------------------------------------------------------------ EMV / TLV

    /// <summary>
    /// Um campo no formato do padrão: identificador, tamanho em dois dígitos, valor.
    ///
    /// O tamanho é o do valor em CARACTERES depois de normalizado — é onde acento
    /// derrubaria a conta, porque "ç" ocupa dois bytes e um caractere só.
    /// </summary>
    private static string Campo(string id, string valor)
        => id + valor.Length.ToString("00", CultureInfo.InvariantCulture) + valor;

    /// <summary>
    /// CRC-16/CCITT-FALSE — polinômio 0x1021, valor inicial 0xFFFF.
    ///
    /// É o dígito verificador do código inteiro: um caractere trocado no meio faz o
    /// aplicativo do banco recusar em vez de mandar dinheiro para o lugar errado.
    /// </summary>
    private static string Crc16(string texto)
    {
        ushort crc = 0xFFFF;

        foreach (var b in Encoding.UTF8.GetBytes(texto))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0
                    ? (ushort)((crc << 1) ^ 0x1021)
                    : (ushort)(crc << 1);
        }

        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }

    // ------------------------------------------------------------- higiene

    private static string Normalizar(string s) => s.Trim();

    private static string SoDigitos(string s)
        => new(s.Where(char.IsDigit).ToArray());

    /// <summary>
    /// Telefone no formato que o padrão exige: <c>+</c> seguido de DDI, DDA e número.
    ///
    /// A clínica digita "(11) 99999-8888" e a chave precisa virar "+5511999998888". Se
    /// ela já tiver escrito o 55, não se acrescenta outro — chave com DDI dobrado é
    /// aceita pelo leitor e cai em conta nenhuma.
    /// </summary>
    private static string ComDdi(string chave)
    {
        var digitos = SoDigitos(chave);

        return digitos.StartsWith("55", StringComparison.Ordinal) && digitos.Length is 12 or 13
            ? "+" + digitos
            : "+55" + digitos;
    }

    /// <summary>
    /// Nome e cidade vão SEM acento e em maiúsculas.
    ///
    /// O padrão aceita só um subconjunto de caracteres, e "São Paulo" com til faz o
    /// aplicativo do banco mostrar caractere quebrado — quando não recusa o código.
    /// Vazio vira um valor padrão em vez de campo em branco, porque campo obrigatório
    /// vazio invalida o código inteiro.
    /// </summary>
    private static string Texto(string? valor, int limite, string padrao)
    {
        var limpo = SemAcento(valor ?? string.Empty)
            .ToUpperInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == ' ')
            .ToArray();

        var texto = new string(limpo).Trim();
        if (texto.Length == 0) texto = padrao;

        return texto.Length > limite ? texto[..limite].Trim() : texto;
    }

    private static string Alfanumerico(string valor, int limite)
    {
        var limpo = new string(SemAcento(valor)
            .Where(char.IsLetterOrDigit)
            .ToArray());

        if (limpo.Length == 0) return SemIdentificador;
        return limpo.Length > limite ? limpo[..limite] : limpo;
    }

    private static string SemAcento(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);

        foreach (var c in decomposto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Descobre o tipo da chave pelo formato — a clínica digita, não escolhe numa lista.
    /// </summary>
    /// <remarks>
    /// A ordem importa: a chave aleatória tem o formato de GUID e nenhum outro colide com
    /// ela; o e-mail é o único com arroba; e a diferença entre CPF e CNPJ é só a contagem
    /// de dígitos, então ela vem depois de descartar os dois anteriores.
    ///
    /// **CPF e celular brasileiro têm os mesmos ONZE dígitos**, e essa ambiguidade é real:
    /// `11999998888` pode ser os dois. Como errar aqui manda o dinheiro para outra conta —
    /// que é justamente o que este serviço existe para evitar —, a desambiguação usa a
    /// PONTUAÇÃO que a pessoa digitou, que é o sinal que ela de fato deu:
    ///
    /// - parênteses ou "+" → telefone (ninguém escreve CPF com parênteses);
    /// - ponto separando grupos de três → CPF;
    /// - onze dígitos secos → **CPF**, que é a chave mais comum de pessoa física.
    ///
    /// Quem quiser tirar a dúvida do sistema informa o tipo explicitamente em
    /// <see cref="Gerar"/> — e a tela deve mostrar o tipo detectado ao lado da chave,
    /// para o erro aparecer no cadastro e não no extrato.
    /// </remarks>
    public static TipoChavePix Classificar(string chave)
    {
        var texto = chave.Trim();
        if (texto.Length == 0) return TipoChavePix.Desconhecida;

        if (texto.Contains('@')) return TipoChavePix.Email;

        if (Guid.TryParse(texto, out _)) return TipoChavePix.Aleatoria;

        var digitos = SoDigitos(texto);

        if (texto.StartsWith('+')
            || texto.Contains('(')
            || (digitos.Length is 12 or 13 && digitos.StartsWith("55", StringComparison.Ordinal)))
            return TipoChavePix.Telefone;

        return digitos.Length switch
        {
            11 => TipoChavePix.Cpf,
            14 => TipoChavePix.Cnpj,
            _ => TipoChavePix.Desconhecida
        };
    }
}
