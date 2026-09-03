using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O DOCUMENTO E O TELEFONE COMO SE LEEM, na lista de escolher paciente (set/2026).
///
/// O defeito
/// ---------
/// Toda lista de escolha amarrava <c>Documento</c> direto e mostrava <b>10400975700</b> —
/// onze dígitos colados. Ninguém confere isso de relance, e é justamente o número que a
/// recepcionista compara com o cartão na mão do paciente para desempatar homônimos.
/// <c>Cpf.Formatar</c> existia no domínio desde sempre; a lista simplesmente não o usava.
///
/// A armadilha da correção, que é o que estes testes amarram
/// ---------------------------------------------------------
/// <b><c>Cpf.Formatar</c> fora dos 11 dígitos devolve SÓ OS DÍGITOS</b> — é o contrato
/// dele, e faz sentido para um campo que se chama CPF. Só que o campo da ficha se chama
/// <see cref="Paciente.Documento"/>: a clínica cadastra RG, e um RG "12.345.678-9" passado
/// por ele voltaria "123456789". A correção teria APAGADO a pontuação de um documento de
/// verdade — trocando um defeito de exibição por outro, mais difícil de notar.
/// </summary>
public class DocumentoEhTelefoneNaListaTests
{
    private static Paciente Ficha(string? documento = null, string? telefone = null)
        => new() { Nome = "ACELINO CALDAS JUNIOR", Documento = documento, Telefone = telefone };

    [Fact]
    public void Cpf_de_onze_digitos_sai_pontuado()
        => Ficha(documento: "10400975700").DocumentoFormatado.Should().Be("104.009.757-00");

    [Fact]
    public void Cpf_ja_pontuado_continua_igual()
        => Ficha(documento: "104.009.757-00").DocumentoFormatado.Should().Be("104.009.757-00");

    /// <summary>
    /// O teste que existe pelo motivo escrito no resumo da classe: documento que NÃO é CPF
    /// sai como está gravado, e não reduzido a dígitos.
    /// </summary>
    [Theory]
    [InlineData("12.345.678-9")]
    [InlineData("MG-14.567.890")]
    [InlineData("passaporte FX193028")]
    public void Documento_que_nao_e_cpf_sai_como_esta_na_ficha(string documento)
        => Ficha(documento: documento).DocumentoFormatado.Should().Be(documento);

    [Fact]
    public void Documento_com_espaco_em_volta_e_aparado()
        => Ficha(documento: "  12.345.678-9  ").DocumentoFormatado.Should().Be("12.345.678-9");

    /// <summary>
    /// Ficha sem documento devolve VAZIO, não nulo: o `TextBlock` da lista amarra direto
    /// nele, e vazio é o que não desenha nada.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ficha_sem_documento_nao_escreve_nada(string? documento)
        => Ficha(documento: documento).DocumentoFormatado.Should().BeEmpty();

    [Fact]
    public void Celular_de_onze_digitos_sai_com_ddd()
        => Ficha(telefone: "22999883626").TelefoneFormatado.Should().Be("(22) 99988-3626");

    /// <summary>
    /// A formatação é IDEMPOTENTE, e precisa ser: a maior parte das fichas já foi gravada
    /// com máscara, e passar todas pelo formatador não pode mudar as que já estavam certas.
    /// </summary>
    [Fact]
    public void Telefone_ja_formatado_continua_igual()
        => Ficha(telefone: "(22) 99988-3626").TelefoneFormatado.Should().Be("(22) 99988-3626");

    /// <summary>
    /// <c>Telefone.Formatar</c> devolve a entrada quando não reconhece o padrão — ao
    /// contrário do <c>Cpf.Formatar</c>. É por isso que o telefone pôde ser passado pelo
    /// formatador sem a guarda que o documento exigiu.
    /// </summary>
    [Fact]
    public void Telefone_fora_do_padrao_volta_como_esta()
        => Ficha(telefone: "ramal 4021").TelefoneFormatado.Should().Be("ramal 4021");
}
