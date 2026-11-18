using System.Reflection;
using System.Text.RegularExpressions;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A TRAVA DO DEPLOY DO PORTAL (nov/2026): a publicação recusa qualquer credencial cifrada ("enc:") em
/// "Configuracoes", exceto as chaves que SÓ o conector TISS grava e abre. A lista de exceções mora em dois
/// lugares que precisam ser o mesmo — <see cref="CredenciaisTissService.PrefixosCifradosForaDoPortal"/> e
/// <c>deploy/tablet/regras_publicacao.py</c> — e o serviço não pode cifrar nada fora dela: senão a trava
/// passaria a deixar escapar (ou a recusar) sem ninguém perceber. Dados fictícios.
/// </summary>
public sealed class CredenciaisCifradasForaDoPortalTests : IDisposable
{
    private readonly TissBancoDeTeste _b = new();

    public void Dispose() => _b.Dispose();

    [Fact]
    public void A_lista_do_CSharp_e_a_mesma_do_regras_publicacao_py()
    {
        var python = File.ReadAllText(Path.Combine(Raiz(), "deploy", "tablet", "regras_publicacao.py"));
        var tupla = Regex.Match(python, @"^PREFIXOS_CIFRADOS_DO_CONECTOR_TISS\s*=\s*\((?<itens>[^)]*)\)", RegexOptions.Multiline);
        tupla.Success.Should().BeTrue("regras_publicacao.py declara PREFIXOS_CIFRADOS_DO_CONECTOR_TISS");
        var doPython = Regex.Matches(tupla.Groups["itens"].Value, "\"([^\"]*)\"").Select(m => m.Groups[1].Value).ToList();

        doPython.Should().Equal(CredenciaisTissService.PrefixosCifradosForaDoPortal);
        CredenciaisTissService.PrefixosCifradosForaDoPortal.Should().Equal("TissSenha:");
        CredenciaisTissService.ChaveSenha("Petrobras").Should().StartWith(CredenciaisTissService.PrefixoSenha);
    }

    [Fact]
    public async Task O_servico_so_cifra_chaves_da_lista_em_todo_metodo_que_grava()
    {
        var credenciais = _b.Credenciais();
        await _b.PrestadorAsync();
        const string op = TissBancoDeTeste.Operador;
        foreach (var convenio in new[] { TissBancoDeTeste.Petrobras, "Amil" })
        {
            await credenciais.SalvarAsync(convenio, "000123", "senha-ficticia", op);
            await credenciais.DefinirEnvioAsync(convenio, true, op);
            // Homologação só existe para quem a tem no catálogo (a Petrobras não tem).
            if (convenio == "Amil") await credenciais.DefinirAmbienteAsync(convenio, true, op);
            await credenciais.DefinirAmbienteAsync(convenio, false, op);
            await credenciais.DefinirCodigoDoPrestadorAsync(convenio, "987654", op);
            await credenciais.DefinirEnderecoAsync(convenio, TipoTransacaoTiss.Elegibilidade, "https://exemplo.invalid/eleg", op);
            await credenciais.DefinirEnderecoAsync(convenio, TipoTransacaoTiss.EnvioGuia, CredenciaisTissService.EnderecoPadraoDoCatalogo, op);
        }
        await credenciais.DefinirAutorizacaoAsync("Amil", true, op);
        await credenciais.RemoverAsync("Amil", op);
        await credenciais.SalvarAsync("Amil", "000124", "outra-senha-ficticia", op);

        var cifradas = (await _b.NovoContexto().Configuracoes.AsNoTracking().ToListAsync())
            .Where(c => ProtecaoSegredoGlobal.EstaProtegido(c.Valor)).Select(c => c.Chave).ToList();
        cifradas.Should().BeEquivalentTo(["TissSenha:Petrobras", "TissSenha:Amil"]);
        cifradas.Should().OnlyContain(c => CredenciaisTissService.PrefixosCifradosForaDoPortal.Any(p => c.StartsWith(p, StringComparison.Ordinal)),
            "a trava do deploy só dispensa estas chaves: cifrar outra aqui exige pô-la na lista do C# e do regras_publicacao.py");
    }

    [Fact]
    public void Todo_metodo_que_grava_esta_no_teste_acima()
    {
        // Método novo que grava configuração entra no teste de cima (e, se cifrar, nas duas listas).
        var queGravam = typeof(CredenciaisTissService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name).Where(n => n.StartsWith("Salvar") || n.StartsWith("Definir") || n.StartsWith("Remover")).Distinct().OrderBy(n => n);
        queGravam.Should().Equal("DefinirAmbienteAsync", "DefinirAutorizacaoAsync", "DefinirCodigoDoPrestadorAsync",
            "DefinirEnderecoAsync", "DefinirEnvioAsync", "RemoverAsync", "SalvarAsync");
    }

    [Fact]
    public void Nenhum_segredo_do_portal_comeca_com_o_prefixo_dispensado()
    {
        // Os segredos que o portal usa (ParametrosService) continuam travados: nenhum cai na exceção.
        string[] doPortal = [ParametrosService.ChaveArmazenamentoChave, ParametrosService.ChaveArmazenamentoSegredo, ParametrosService.ChaveSafeIDClientSecret];
        doPortal.Should().OnlyContain(c => !CredenciaisTissService.PrefixosCifradosForaDoPortal.Any(p => c.StartsWith(p, StringComparison.Ordinal)));
    }

    private static string Raiz()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Clinica.sln"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("Clinica.sln não encontrado.");
    }
}
