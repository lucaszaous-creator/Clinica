using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed class MedicamentosEGruposTests : IDisposable
{
    private readonly SqliteConnection conn = new("Data Source=:memory:");
    private readonly ClinicaDbContext db;
    private readonly ClinicaRepositorio repo;
    public MedicamentosEGruposTests()
    {
        conn.Open();
        db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated(); repo = new(db);
    }
    public void Dispose() { db.Dispose(); conn.Dispose(); }

    [Fact] public async Task Catalogo_inicial_prioriza_as_sete_apresentacoes_sem_inventar_concentracao()
    {
        var todos = await new MedicamentoCatalogoService(repo).ListarAsync();
        Assert.Equal(7, todos.Count(m => m.Codigo.StartsWith("clinica:")));
        var catalogo = BuscaMedicamentos.Catalogo(todos);
        Assert.Empty(BuscaMedicamentos.Buscar(catalogo, "l"));
        var lidocaina = BuscaMedicamentos.Buscar(catalogo, "LI")[0];
        Assert.Equal("clinica:lidocaina-2-20ml", lidocaina.Codigo);
        Assert.Contains("20 mL", lidocaina.Texto);
        Assert.Contains("sem vasoconstritor", lidocaina.Texto);
        Assert.Equal(lidocaina, BuscaMedicamentos.Buscar(catalogo, "lidocaina")[0]);
        Assert.Equal("2 mL · ampola", BuscaMedicamentos.Buscar(catalogo, "dip")[0].Apresentacao);
        Assert.Equal("Cetoprofeno", BuscaMedicamentos.Buscar(catalogo, "ceto")[0].Nome);
    }

    [Fact] public async Task Cadastro_compartilhado_permite_editar_e_desativar_sem_apagar_referencia()
    {
        var u = await Usuario(PerfilAcesso.Gerente);
        var svc = new MedicamentoCatalogoService(repo);
        var m = (await svc.ListarAsync()).Single(m => m.Codigo == "clinica:lidocaina-2-20ml");
        m.Fabricante = "Fabricante de teste"; m.Ativo = false;
        await svc.SalvarAsync(m, u.Id);
        db.ChangeTracker.Clear();
        var emOutroEscopo = await new MedicamentoCatalogoService(new ClinicaRepositorio(db)).ListarAsync();
        var salvo = emOutroEscopo.Single(i => i.Codigo == m.Codigo);
        Assert.Equal(m.Fabricante, salvo.Fabricante); Assert.False(salvo.Ativo);
        Assert.DoesNotContain(BuscaMedicamentos.Catalogo(emOutroEscopo), i => i.Codigo == m.Codigo);
        Assert.Single(await db.MedicamentosCadastro.ToListAsync());
    }

    [Fact] public async Task Cadastro_recusa_importacao_duplicada_inteira_e_acesso_sem_permissao()
    {
        var u = await Usuario(PerfilAcesso.Profissional);
        var svc = new MedicamentoCatalogoService(repo);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SalvarLoteAsync([
            new() { Nome="Produto fictício", Apresentacao="Apresentação fictícia" },
            new() { Nome="Produto fictício", Apresentacao="Apresentação fictícia" }], u.Id));
        Assert.Empty(await db.MedicamentosCadastro.ToListAsync());
        var recepcao = await Usuario(PerfilAcesso.Recepcao);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SalvarAsync(new() { Nome="Outro teste" }, recepcao.Id));
        Assert.Empty(await db.MedicamentosCadastro.ToListAsync());
    }

    [Fact] public async Task Grupos_sobrevivem_ao_banco_modelo_pdf_e_protegem_contra_cliente_antigo()
    {
        var paciente = new Paciente { Nome="Paciente fictício para conferência" };
        db.Pacientes.Add(paciente); await db.SaveChangesAsync();
        var svc = new PrescricaoInternaService(repo, new(repo));
        var p = await svc.CriarAsync(paciente.Id, null);
        var itens = new[] { Item(1,"Medicamento demonstrativo A","SF 0,9%","250 mL"), Item(1,"Medicamento demonstrativo B","SF 0,9%","250 mL"), Item(2,"Medicamento demonstrativo C","Diluente demonstrativo","100 mL") };
        await svc.SalvarRascunhoAsync(p.Id, null, null, itens, diluicaoUnica:false);
        db.ChangeTracker.Clear();
        p = (await repo.ObterPrescricaoInternaAsync(p.Id))!;
        Assert.Equal(new int?[]{1,1,2},p.Itens.OrderBy(i=>i.Ordem).Select(i=>i.GrupoInfusao));
        var modelo = ModeloInfusao.Ler(new ModeloInfusao(null,null,p.Itens.OrderBy(i=>i.Ordem).Select(ModeloInfusao.De).ToArray()).Guardar());
        Assert.Equal(new int?[]{1,1,2},modelo.Itens.Select(i=>i.GrupoInfusao));
        Assert.Equal("100 mL",modelo.Itens[2].Volume);
        var pdf = await new PrescricaoInternaPdfService(repo,new(repo)).GerarPrescricaoAsync(p.Id);
        Assert.True(pdf.Length > 1000);
        if(Environment.GetEnvironmentVariable("CLINICA_PDF_QA") is {} pasta) { Directory.CreateDirectory(pasta); await File.WriteAllBytesAsync(Path.Combine(pasta,"infusoes-agrupadas.pdf"),pdf); }
        await Assert.ThrowsAsync<InvalidOperationException>(()=>svc.SalvarRascunhoAsync(p.Id,null,null,[new() {Descricao="Cliente antigo"}],diluicaoUnica:false));
        Assert.Equal(3,p.Itens.Count);
    }

    [Fact] public void Grupos_nao_podem_misturar_preparos_nem_voltar_a_grupo_anterior()
    {
        Assert.Throws<InvalidOperationException>(()=>GruposInfusao.Validar([Item(1,"A","SF","100 mL"),Item(1,"B","SF","250 mL")],false));
        Assert.Throws<InvalidOperationException>(()=>GruposInfusao.Validar([Item(1,"A","SF","100 mL"),Item(2,"B","SF","100 mL"),Item(1,"C","SF","100 mL")],false));
        Assert.Throws<InvalidOperationException>(()=>GruposInfusao.Validar([Item(1,"A","SF","100 mL"),new(){Descricao="Sem grupo"}],false));
        Assert.Throws<InvalidOperationException>(()=>GruposInfusao.Validar([Item(1,"A","SF","100 mL")],true));
        GruposInfusao.Validar([new(){Descricao="Registro antigo"}],true);
    }

    private Task<UsuarioSistema> Usuario(PerfilAcesso perfil) => new AcessoService(repo).CriarAsync("Acesso fictício",Guid.NewGuid().ToString("N"),"Teste#Catalogo2026",perfil);
    private static ItemPrescricaoInterna Item(int grupo,string nome,string diluente,string volume) => new() {GrupoInfusao=grupo,Descricao=nome,Diluente=diluente,Volume=volume,Via=ViaAdministracao.Endovenosa};
}
