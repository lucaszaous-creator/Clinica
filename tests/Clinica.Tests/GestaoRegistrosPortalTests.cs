using System.Text.Json;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Tablet;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed class GestaoRegistrosPortalTests
{
    [Fact]
    public void Somente_gerente_ativo_com_permissao_de_gestao_pode_revisar_registros()
    {
        var gerente = new UsuarioSistema { Perfil = PerfilAcesso.Gerente, Ativo = true };
        Assert.True(GestaoRegistrosPortalService.PodeGerenciar(gerente));

        gerente.PermissoesNegadas = Permissao.GerenciarUsuarios;
        Assert.False(GestaoRegistrosPortalService.PodeGerenciar(gerente));

        gerente.PermissoesNegadas = Permissao.Nenhuma;
        gerente.DeveTrocarSenha = true;
        Assert.False(GestaoRegistrosPortalService.PodeGerenciar(gerente));

        gerente.DeveTrocarSenha = false;
        gerente.Perfil = PerfilAcesso.Profissional;
        gerente.PermissoesExtras = Permissao.GerenciarUsuarios;
        Assert.False(GestaoRegistrosPortalService.PodeGerenciar(gerente));
    }

    [Fact]
    public async Task Modo_paciente_nao_pode_consultar_registros_administrativos()
    {
        var gerente = new UsuarioSistema { Perfil = PerfilAcesso.Gerente, Ativo = true };
        var sessao = new SessaoTablet { Usuario = gerente, Modo = "paciente" };
        var servico = new GestaoRegistrosPortalService(null!, null!, null!, null!);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            servico.BuscarPacientesAsync(sessao, "Teste", default));
    }

    [Fact]
    public async Task Gerente_localiza_registros_do_paciente_sem_expor_os_de_outro()
    {
        await using var conexao = new SqliteConnection("Data Source=:memory:");
        await conexao.OpenAsync();
        await using var db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseSqlite(conexao).Options);
        await db.Database.EnsureCreatedAsync();
        var paciente = new Paciente { Nome = "Paciente Teste" };
        var outro = new Paciente { Nome = "Outro Paciente" };
        db.Pacientes.AddRange(paciente, outro);
        db.Atendimentos.Add(new Atendimento { Paciente = paciente, Numero = "2026-TESTE", Data = new(2026, 9, 26) });
        db.PrescricoesInternas.Add(new PrescricaoInterna { Paciente = paciente, Numero = "PRE 2026/TESTE", Data = new(2026, 9, 26) });
        db.Atendimentos.Add(new Atendimento { Paciente = outro, Numero = "OUTRO", Data = new(2026, 9, 26) });
        await db.SaveChangesAsync();

        var gerente = new UsuarioSistema { Perfil = PerfilAcesso.Gerente, Login = "gerente", Ativo = true };
        var sessao = new SessaoTablet { Usuario = gerente, Modo = "equipe" };
        var servico = new GestaoRegistrosPortalService(db, null!, null!, null!);
        var busca = JsonSerializer.Serialize(await servico.BuscarPacientesAsync(sessao, "Paciente Teste", default));
        var registros = JsonSerializer.Serialize(await servico.RegistrosAsync(sessao, paciente.Id, default));

        Assert.Contains("Paciente Teste", busca);
        Assert.Contains("2026-TESTE", registros);
        Assert.Contains("PRE 2026/TESTE", registros);
        Assert.DoesNotContain("OUTRO", registros);
        Assert.Single(await db.Auditoria.ToListAsync());
    }
}
