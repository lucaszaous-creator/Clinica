using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed class EvolucaoFormatadaTests : IDisposable
{
    private readonly SqliteConnection conexao=new("DataSource=:memory:");
    private readonly ClinicaDbContext db;
    private readonly ProntuarioService servico;
    private readonly int pacienteId;
    private const string Texto="Evolução fictícia com acentuação\nConduta e orientação.";

    public EvolucaoFormatadaTests()
    {
        conexao.Open();
        db=new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conexao).Options);
        db.Database.EnsureCreated();
        var paciente=new Paciente{Nome="Paciente fictício da formatação"};
        db.Pacientes.Add(paciente);db.SaveChanges();pacienteId=paciente.Id;
        servico=new ProntuarioService(new ClinicaRepositorio(db));
    }

    private Evolucao Dados(string texto=Texto,string? formato=null,int id=0)=>new(){
        Id=id,PacienteId=pacienteId,Data=new DateOnly(2026,10,5),
        TextoEvolucao=texto,TextoEvolucaoFormatado=formato};

    private static string Formato(string texto=Texto,bool italico=false)
        =>TextoFormatado.Guardar([new TrechoTexto(texto,Negrito:!italico,Italico:italico)])!;

    private async Task<Evolucao> Reabrir(int id)
    {
        db.ChangeTracker.Clear();
        return (await servico.ObterAsync(id))!;
    }

    [Fact]
    public async Task Salvar_reabrir_preserva_texto_multilinha_acentos_e_estilos()
    {
        var formato=TextoFormatado.Guardar([new("Evolução fictícia",true),new(" com acentuação\n"),new("Conduta e orientação.",false,true)]);
        var salvo=await servico.SalvarAsync(Dados(formato:formato),"medico.ficticio");
        var reaberto=await Reabrir(salvo.Id);
        reaberto.TextoEvolucao.Should().Be(Texto);
        TextoFormatado.Ler(reaberto.TextoEvolucao,reaberto.TextoEvolucaoFormatado).Should().Equal(
            new TrechoTexto("Evolução fictícia",true),new TrechoTexto(" com acentuação\n"),new TrechoTexto("Conduta e orientação.",false,true));
    }

    [Fact]
    public async Task Normalizacao_do_texto_recorta_apenas_bordas_preservando_estilo()
    {
        const string entrada=" \r\nEvolução fictícia\r\nOrientação.  ";
        var salvo=await servico.SalvarAsync(Dados(entrada,Formato(entrada)),"medico.ficticio");
        var reaberto=await Reabrir(salvo.Id);
        reaberto.TextoEvolucao.Should().Be(entrada.Trim());
        TextoFormatado.Ler(reaberto.TextoEvolucao,reaberto.TextoEvolucaoFormatado)
            .Should().Equal(new TrechoTexto("Evolução fictícia\nOrientação.",true));
    }

    [Fact]
    public async Task Correcao_so_de_estilo_guarda_a_apresentacao_anterior_na_versao()
    {
        var salvo=await servico.SalvarAsync(Dados(formato:Formato()),"medico.ficticio");
        await servico.SalvarAsync(Dados(formato:Formato(italico:true),id:salvo.Id),"medico.ficticio","Ajuste de apresentação");
        var reaberto=await Reabrir(salvo.Id);
        TextoFormatado.Ler(reaberto.TextoEvolucao,reaberto.TextoEvolucaoFormatado).Should().Equal(new TrechoTexto(Texto,false,true));
        var versao=(await servico.VersoesAsync(salvo.Id)).Should().ContainSingle().Subject;
        versao.TextoEvolucao.Should().Be(Texto);
        TextoFormatado.Ler(versao.TextoEvolucao,versao.TextoEvolucaoFormatado).Should().Equal(new TrechoTexto(Texto,true));
    }

    [Fact]
    public async Task Cliente_legado_omitindo_formato_preserva_estilo_quando_texto_nao_mudou()
    {
        var salvo=await servico.SalvarAsync(Dados(formato:Formato()),"medico.ficticio");
        await servico.SalvarAsync(Dados(id:salvo.Id),"cliente.legado");
        var reaberto=await Reabrir(salvo.Id);
        reaberto.TextoEvolucao.Should().Be(Texto);
        TextoFormatado.Ler(reaberto.TextoEvolucao,reaberto.TextoEvolucaoFormatado).Should().Equal(new TrechoTexto(Texto,true));
    }

    [Fact]
    public async Task Cliente_legado_alterando_plain_limpa_metadata_obsoleta_e_preserva_versao()
    {
        var salvo=await servico.SalvarAsync(Dados(formato:Formato()),"medico.ficticio");
        const string novo="Nova evolução fictícia\nOrientação corrigida.";
        await servico.SalvarAsync(Dados(novo,id:salvo.Id),"cliente.legado");
        var reaberto=await Reabrir(salvo.Id);
        reaberto.TextoEvolucao.Should().Be(novo);
        reaberto.TextoEvolucaoFormatado.Should().BeNull();
        var versao=(await servico.VersoesAsync(salvo.Id)).Should().ContainSingle().Subject;
        TextoFormatado.Ler(versao.TextoEvolucao,versao.TextoEvolucaoFormatado).Should().Equal(new TrechoTexto(Texto,true));
    }

    [Fact]
    public async Task Formato_vazio_remove_estilo_explicitamente_sem_apagar_texto()
    {
        var salvo=await servico.SalvarAsync(Dados(formato:Formato()),"medico.ficticio");
        await servico.SalvarAsync(Dados(formato:"",id:salvo.Id),"medico.ficticio");
        var reaberto=await Reabrir(salvo.Id);
        reaberto.TextoEvolucao.Should().Be(Texto);
        reaberto.TextoEvolucaoFormatado.Should().BeNull();
    }

    [Theory]
    [InlineData("{json inválido")]
    [InlineData("[{\"texto\":\"outro conteúdo\",\"negrito\":true}]")]
    public async Task Metadata_invalida_nunca_substitui_o_texto_clinico(string formato)
    {
        var salvo=await servico.SalvarAsync(Dados(formato:formato),"medico.ficticio");
        var reaberto=await Reabrir(salvo.Id);
        reaberto.TextoEvolucao.Should().Be(Texto);
        reaberto.TextoEvolucaoFormatado.Should().BeNull();
    }

    [Fact]
    public async Task Registro_antigo_sem_metadata_continua_texto_simples()
    {
        var salvo=await servico.SalvarAsync(Dados(),"cliente.legado");
        var reaberto=await Reabrir(salvo.Id);
        reaberto.TextoEvolucao.Should().Be(Texto);
        reaberto.TextoEvolucaoFormatado.Should().BeNull();
        TextoFormatado.Ler(reaberto.TextoEvolucao,reaberto.TextoEvolucaoFormatado).Should().Equal(new TrechoTexto(Texto));
    }

    public void Dispose(){db.Dispose();conexao.Dispose();}
}
