using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A TRAVA DO APP CONGELADO.
///
/// `Clinica.Desktop` fatura a clínica hoje. O `CLAUDE.md` manda não encostar nele, e a
/// parte fácil dessa regra é não editar os arquivos dele — isso qualquer um lembra. A
/// parte difícil é que o faturamento **lê as camadas compartilhadas**, e uma mudança
/// inocente lá dentro chega à tela dele sem que ninguém tenha aberto uma pasta sua.
///
/// Foi o que aconteceu na parcela 36: acrescentar um valor ao enum
/// <see cref="Especialidade"/> — para um módulo NOVO — faria uma opção nova aparecer no
/// seletor do lançamento de atendimento do app em produção, porque
/// `EspecialidadeCatalogoService.ListarAsync` garante as embutidas percorrendo
/// `Enum.GetValues`. Ninguém tocou em `Clinica.Desktop`; a tela dele mudaria assim mesmo.
///
/// Estes testes fixam a superfície que o faturamento consome. Eles não testam regra
/// nenhuma — testam que a regra não mudou. Quando um deles falhar, a pergunta certa não é
/// "como faço o teste passar?", é **"esta mudança vai aparecer na tela de quem fatura
/// amanhã de manhã?"**. Se a resposta for sim e for intencional, aí sim se atualiza o
/// número aqui, junto de uma release do faturamento.
/// </summary>
public class FaturamentoCongeladoTests
{
    /// <summary>
    /// As especialidades EMBUTIDAS. Elas alimentam o seletor da consulta avulsa
    /// (`NovoAtendimentoViewModel`), a tela de Configurações e a rotação da Petrobras.
    ///
    /// Especialidade nova entra pelo CATÁLOGO (`EspecialidadeCadastro`), cadastrada pela
    /// clínica — nunca por um valor a mais aqui.
    /// </summary>
    [Fact]
    public void Especialidades_embutidas_sao_exatamente_as_do_faturamento()
        => Enum.GetNames<Especialidade>().Should().BeEquivalentTo(
            "Psiquiatria", "Geriatria", "Ginecologia",
            "Acupuntura", "ClinicaDaDor", "Endocrinologia");

    /// <summary>
    /// As modalidades embutidas decidem QUAIS CÓDIGOS a guia recebe (o motor de regras
    /// despacha por elas). Uma a mais no enum entra no seletor do lançamento; uma
    /// renomeada quebra o catálogo, que grava o nome como texto.
    /// </summary>
    [Fact]
    public void Modalidades_embutidas_sao_exatamente_as_do_faturamento()
        => Enum.GetNames<ModalidadeAtendimento>().Should().BeEquivalentTo(
            "AcupunturaSimples", "AcupunturaComEletro",
            "BsvComAcupuntura", "BsvApenas", "Consulta");

    /// <summary>
    /// Os convênios embutidos são os cinco fluxogramas modelados. Acrescentar um valor
    /// aqui sem a `IRegraConvenio` correspondente faz o `RegistroRegras` não achar regra
    /// para ele — no meio do lançamento, com o paciente esperando.
    /// </summary>
    [Fact]
    public void Convenios_embutidos_sao_exatamente_os_modelados()
    {
        Enum.GetNames<Convenio>().Should().BeEquivalentTo(
            "UnimedPadrao", "UnimedIntercambio", "Amil", "Petrobras", "Personalizado");

        var registro = new RegistroRegras();
        foreach (var convenio in Enum.GetValues<Convenio>())
            registro.Invoking(r => r.Para(convenio)).Should()
                .NotThrow($"{convenio} precisa de uma regra registrada");
    }

    /// <summary>
    /// Os enums que descrevem o CICLO DA GUIA. São gravados como texto no banco e lidos
    /// pelo XML TISS: renomear um valor invalida o histórico já gravado, e acrescentar um
    /// muda o que o painel de pendências conta.
    /// </summary>
    [Fact]
    public void Ciclo_da_guia_nao_mudou()
    {
        Enum.GetNames<TipoCodigo>().Should().BeEquivalentTo(
            "Consulta", "Acupuntura", "Eletroacupuntura", "Bsv", "ConsultaEspecialidade");

        Enum.GetNames<StatusCodigo>().Should().BeEquivalentTo(
            "Aberto", "Baixado", "NaoAplicavel", "NaoConformidade");

        Enum.GetNames<OrdemCodigo>().Should().BeEquivalentTo("Primeiro", "Segundo");

        Enum.GetNames<FormaObtencao>().Should().BeEquivalentTo(
            "NaoAplica", "App", "Sistema", "Ligacao");

        Enum.GetNames<Categoria>().Should().BeEquivalentTo("Verde", "Amarela", "Vermelha");

        Enum.GetNames<StatusGlosa>().Should().BeEquivalentTo(
            "SemGlosa", "Glosada", "Reapresentada", "Recuperada");
    }

    /// <summary>
    /// O catálogo que o faturamento carrega na abertura devolve, em base VAZIA,
    /// exatamente as embutidas — nem uma a mais.
    ///
    /// É o mesmo caminho que o `App.xaml.cs` dele percorre
    /// (`RecarregarCacheAsync` → `ListarAsync`), e é por onde a regressão da parcela 36
    /// teria passado.
    /// </summary>
    [Fact]
    public async Task Catalogo_em_base_vazia_devolve_so_as_embutidas()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conn).Options;
        using var db = new ClinicaDbContext(options);
        db.Database.EnsureCreated();

        var repo = new ClinicaRepositorio(db);

        var especialidades = await new EspecialidadeCatalogoService(repo).ListarAsync();
        especialidades.Select(e => e.Codigo).Should()
            .BeEquivalentTo(Enum.GetNames<Especialidade>());

        var modalidades = await new ModalidadeCatalogoService(repo).ListarAsync();
        modalidades.Select(m => m.Codigo).Should()
            .BeEquivalentTo(Enum.GetNames<ModalidadeAtendimento>());
    }
}
