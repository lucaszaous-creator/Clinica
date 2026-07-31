using Clinica.Application.Servicos;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O código Pix da clínica (parcela 34).
///
/// `FormaPagamento.Pix` existia desde a parcela 4 e o sistema só REGISTRAVA que foi Pix:
/// a chave ficava num papel colado no monitor e a secretária a ditava de cabeça. É assim
/// que dinheiro cai na conta errada.
///
/// O que estes testes protegem é o que não dá para conferir a olho: **o código ou é
/// válido ou é lixo**. Um caractere fora do lugar e o aplicativo do banco recusa — com o
/// paciente na frente, sem ninguém entender por quê. Por isso os testes DECODIFICAM o
/// que o serviço monta, em vez de comparar com uma string esperada: comparar strings
/// prova que o código não mudou, e não que ele está certo.
/// </summary>
public class PixServiceTests
{
    private readonly PixService _pix = new();

    // ------------------------------------------------------- decodificador

    /// <summary>
    /// Lê o formato do Banco Central (identificador, tamanho, valor) e devolve os campos.
    ///
    /// É um decodificador independente do codificador de propósito: se os dois
    /// compartilhassem código, um erro de contagem passaria pelos dois lados.
    /// </summary>
    private static Dictionary<string, string> Decodificar(string payload)
    {
        var campos = new Dictionary<string, string>();
        var i = 0;

        while (i + 4 <= payload.Length)
        {
            var id = payload.Substring(i, 2);
            var tamanho = int.Parse(payload.Substring(i + 2, 2));
            campos[id] = payload.Substring(i + 4, tamanho);
            i += 4 + tamanho;
        }

        return campos;
    }

    /// <summary>Recalcula o CRC do jeito que o aplicativo do banco recalcula.</summary>
    private static string CrcEsperado(string semOsQuatroUltimos)
    {
        ushort crc = 0xFFFF;
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(semOsQuatroUltimos))
        {
            crc ^= (ushort)(b << 8);
            for (var k = 0; k < 8; k++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc.ToString("X4");
    }

    // ------------------------------------------------------------- estrutura

    [Fact]
    public void Codigo_tem_a_estrutura_do_padrao()
    {
        var c = _pix.Gerar("12345678000199", "Clinica de Acupuntura", "Sao Paulo", 150m);

        var campos = Decodificar(c.CopiaECola);

        campos["00"].Should().Be("01", "indicador de formato do payload");
        campos["53"].Should().Be("986", "real brasileiro, pela ISO 4217");
        campos["58"].Should().Be("BR");
        campos["54"].Should().Be("150.00", "valor com ponto decimal, nunca com vírgula");
        campos.Should().ContainKey("26").And.ContainKey("59").And.ContainKey("60");
    }

    /// <summary>
    /// O dígito verificador do código inteiro. É ele que faz o banco RECUSAR em vez de
    /// mandar dinheiro para o lugar errado quando um caractere se perde no caminho.
    /// </summary>
    [Fact]
    public void Crc_confere_com_o_recalculo()
    {
        var c = _pix.Gerar("12345678000199", "Clinica", "Sao Paulo", 99.90m);

        var corpo = c.CopiaECola[..^4];
        var crc = c.CopiaECola[^4..];

        corpo.Should().EndWith("6304", "o CRC cobre o próprio cabeçalho");
        crc.Should().Be(CrcEsperado(corpo));
    }

    /// <summary>
    /// A carteira Pix dentro do campo 26, com a chave. É o único lugar do código que
    /// diz para quem o dinheiro vai.
    /// </summary>
    [Fact]
    public void Chave_vai_dentro_da_carteira_pix()
    {
        var c = _pix.Gerar("clinica@exemplo.com.br", "Clinica", "Sao Paulo", 150m);

        var conta = Decodificar(Decodificar(c.CopiaECola)["26"]);

        conta["00"].Should().Be("br.gov.bcb.pix");
        conta["01"].Should().Be("clinica@exemplo.com.br");
    }

    // ------------------------------------------------------------ as chaves

    [Theory]
    [InlineData("123.456.789-09", TipoChavePix.Cpf)]
    [InlineData("12.345.678/0001-99", TipoChavePix.Cnpj)]
    [InlineData("clinica@exemplo.com.br", TipoChavePix.Email)]
    [InlineData("+5511999998888", TipoChavePix.Telefone)]
    [InlineData("123e4567-e89b-12d3-a456-426614174000", TipoChavePix.Aleatoria)]
    public void Tipo_da_chave_sai_do_formato(string chave, TipoChavePix esperado)
        => PixService.Classificar(chave).Should().Be(esperado);

    /// <summary>
    /// CPF e CNPJ vão SEM pontuação. A clínica digita com ponto e traço porque é como
    /// está no documento, e o aplicativo do banco recusa o código com um ponto a mais —
    /// dando uma mensagem que não ajuda ninguém a descobrir por quê.
    /// </summary>
    [Fact]
    public void Cpf_e_cnpj_perdem_a_pontuacao_no_codigo()
    {
        var cpf = _pix.Gerar("123.456.789-09", "Clinica", "Sao Paulo");
        Decodificar(Decodificar(cpf.CopiaECola)["26"])["01"].Should().Be("12345678909");

        var cnpj = _pix.Gerar("12.345.678/0001-99", "Clinica", "Sao Paulo");
        Decodificar(Decodificar(cnpj.CopiaECola)["26"])["01"].Should().Be("12345678000199");
    }

    /// <summary>
    /// Telefone ganha o DDI, e **não ganha dois**: chave com 55 dobrado é aceita pelo
    /// leitor e cai em conta nenhuma.
    /// </summary>
    [Theory]
    [InlineData("(11) 99999-8888", "+5511999998888")]
    [InlineData("+55 11 99999-8888", "+5511999998888")]
    [InlineData("5511999998888", "+5511999998888")]
    public void Telefone_sai_com_um_unico_ddi(string digitado, string esperado)
        => Decodificar(Decodificar(_pix.Gerar(digitado, "Clinica", "Sao Paulo").CopiaECola)["26"])["01"]
            .Should().Be(esperado);

    /// <summary>
    /// **CPF e celular brasileiro têm os mesmos onze dígitos** — `11999998888` pode ser
    /// os dois, e errar manda dinheiro para outra conta. A desambiguação usa a pontuação
    /// que a pessoa de fato digitou, e onze dígitos secos ficam sendo CPF (a chave mais
    /// comum de pessoa física).
    /// </summary>
    [Theory]
    [InlineData("(11) 99999-8888", TipoChavePix.Telefone)]
    [InlineData("+5511999998888", TipoChavePix.Telefone)]
    [InlineData("11999998888", TipoChavePix.Cpf)]
    [InlineData("123.456.789-09", TipoChavePix.Cpf)]
    public void Onze_digitos_sao_desambiguados_pela_pontuacao(string chave, TipoChavePix esperado)
        => PixService.Classificar(chave).Should().Be(esperado);

    /// <summary>
    /// E quando nem a pontuação resolve, quem manda é a clínica: o tipo declarado vence
    /// a dedução. É a saída para o único caso em que o sistema pode errar sozinho.
    /// </summary>
    [Fact]
    public void Tipo_declarado_vence_a_deducao()
    {
        var c = _pix.Gerar("11999998888", "Clinica", "Sao Paulo", 150m,
            tipoDeclarado: TipoChavePix.Telefone);

        c.TipoChave.Should().Be(TipoChavePix.Telefone);
        Decodificar(Decodificar(c.CopiaECola)["26"])["01"].Should().Be("+5511999998888");
    }

    // ------------------------------------------------- nome, cidade e valor

    /// <summary>
    /// Nome e cidade vão sem acento e em maiúsculas. "São Paulo" com til faz o aplicativo
    /// mostrar caractere quebrado — quando não recusa o código.
    /// </summary>
    [Fact]
    public void Nome_e_cidade_perdem_acento_e_sobem_para_maiuscula()
    {
        var c = _pix.Gerar("12345678000199", "Clínica São Judas", "São Paulo", 150m);

        var campos = Decodificar(c.CopiaECola);
        campos["59"].Should().Be("CLINICA SAO JUDAS");
        campos["60"].Should().Be("SAO PAULO");
    }

    /// <summary>
    /// Nome longo é CORTADO no limite do padrão, não recusado: a clínica não vai mudar de
    /// razão social por causa disso, e um código que não sai é pior que um nome abreviado
    /// na tela do paciente.
    /// </summary>
    [Fact]
    public void Nome_longo_e_cortado_no_limite_em_vez_de_derrubar_o_codigo()
    {
        var c = _pix.Gerar("12345678000199",
            "Clinica de Acupuntura e Terapias Integrativas Ltda ME", "Sao Paulo", 150m);

        var campos = Decodificar(c.CopiaECola);
        campos["59"].Length.Should().BeLessThanOrEqualTo(25);
        campos["60"].Length.Should().BeLessThanOrEqualTo(15);

        // E o código continua íntegro depois do corte.
        c.CopiaECola[^4..].Should().Be(CrcEsperado(c.CopiaECola[..^4]));
    }

    /// <summary>
    /// Sem valor o campo simplesmente não existe — e o paciente digita quanto vai pagar.
    /// Certo para doação, arriscado para sessão; a decisão é de quem chama, e a tela
    /// precisa deixar isso claro.
    /// </summary>
    [Fact]
    public void Sem_valor_o_campo_nao_entra_no_codigo()
    {
        var c = _pix.Gerar("12345678000199", "Clinica", "Sao Paulo");

        Decodificar(c.CopiaECola).Should().NotContainKey("54");
        c.Valor.Should().BeNull();
    }

    [Fact]
    public void Valor_com_centavos_vai_com_ponto_decimal()
    {
        // Numa máquina em pt-BR, "150,75" quebraria o código: o padrão exige ponto.
        var c = _pix.Gerar("12345678000199", "Clinica", "Sao Paulo", 150.75m);

        Decodificar(c.CopiaECola)["54"].Should().Be("150.75");
    }

    /// <summary>
    /// O identificador é o que permite casar o Pix com a sessão no extrato. Sem ele, o
    /// padrão manda três asteriscos — e o extrato traz só o nome do pagador.
    /// </summary>
    [Fact]
    public void Identificador_entra_no_campo_de_referencia()
    {
        var com = _pix.Gerar("12345678000199", "Clinica", "Sao Paulo", 150m, "REC20260001");
        Decodificar(Decodificar(com.CopiaECola)["62"])["05"].Should().Be("REC20260001");

        var sem = _pix.Gerar("12345678000199", "Clinica", "Sao Paulo", 150m);
        Decodificar(Decodificar(sem.CopiaECola)["62"])["05"].Should().Be("***");
    }

    // ------------------------------------------------------------- recusas

    [Fact]
    public void Sem_chave_nao_ha_codigo()
    {
        var acao = () => _pix.Gerar("   ", "Clinica", "Sao Paulo", 150m);

        acao.Should().Throw<InvalidOperationException>().WithMessage("*chave*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Valor_zero_ou_negativo_e_recusado(decimal valor)
    {
        var acao = () => _pix.Gerar("12345678000199", "Clinica", "Sao Paulo", valor);

        acao.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Cidade em branco não pode virar campo vazio: campo obrigatório sem conteúdo
    /// invalida o código inteiro, e o erro apareceria só no celular do paciente.
    /// </summary>
    [Fact]
    public void Cidade_em_branco_vira_padrao_em_vez_de_campo_vazio()
    {
        var c = _pix.Gerar("12345678000199", "Clinica", "   ", 150m);

        var campos = Decodificar(c.CopiaECola);
        campos["60"].Should().NotBeEmpty();
        c.CopiaECola[^4..].Should().Be(CrcEsperado(c.CopiaECola[..^4]));
    }
}
