using System.Text;
using System.Text.Json;
using Clinica.Atualizacao;
using Velopack.Logging;
using Velopack.Sources;
using Xunit;

namespace Clinica.Tests;

public class AtualizacaoCanaisTests
{
    private static object Release(string tag, string canal, bool draft=false, bool pre=false) => new {
        tag_name=tag, name=tag, draft, prerelease=pre, published_at="2026-09-17T19:00:00Z",
        assets=new[] {new {name=$"releases.{canal}.json", browser_download_url=$"https://example.test/{tag}.json"}}
    };

    [Fact]
    public async Task Encontra_clinico_na_segunda_pagina_e_escolhe_versao_maior_fora_da_ordem_da_api()
    {
        var pagina1=Enumerable.Range(1,100).Select(n=>Release($"v1.0.{n}","win")).ToArray();
        var pagina2=new[] {Release("clinico-v1.2.31","clinico"),Release("clinico-v1.2.29","clinico"),
            Release("clinico-v9.0.0","clinico",draft:true),Release("clinico-v8.0.0","clinico",pre:true)};
        var rede=new Rede(pagina1,pagina2);
        var feed=await new FonteAtualizacaoGithub(rede).GetReleaseFeed(new NullVelopackLogger(),"Clinica.Clinico","clinico");
        Assert.Equal("1.2.31",Assert.Single(feed.Assets).Version.ToString());
        // Sem os cinco canais no repositório novo, o de antes também é lido (as mesmas duas páginas aqui).
        Assert.Equal(4,rede.Paginas);
        Assert.Equal(new[]{"https://example.test/clinico-v1.2.31.json"},rede.Indices);
    }

    [Fact]
    public async Task Falha_da_paginacao_nao_se_disfarca_de_nenhuma_atualizacao()
    {
        var rede=new Rede(Enumerable.Range(1,100).Select(n=>Release($"v1.0.{n}","win")).ToArray());
        await Assert.ThrowsAsync<HttpRequestException>(()=>new FonteAtualizacaoGithub(rede)
            .GetReleaseFeed(new NullVelopackLogger(),"Clinica.Clinico","clinico"));
    }

    [Fact]
    public async Task Download_que_excede_abertura_e_aplicado_ao_fechar_sem_bloquear_o_atendimento()
    {
        var download=new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agendada=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var imediato=await DownloadComPrazo.AguardarAsync(download.Task,TimeSpan.Zero,v=>agendada.SetResult(v),e=>agendada.SetException(e));
        Assert.Null(imediato); Assert.False(agendada.Task.IsCompleted);
        download.SetResult("1.2.32");
        Assert.Equal("1.2.32",await agendada.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Erro_de_download_apos_o_limite_fica_registrado()
    {
        var download=new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var erro=new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        await DownloadComPrazo.AguardarAsync(download.Task,TimeSpan.Zero,_=>throw new Exception("Não deveria aplicar"),e=>erro.SetResult(e));
        download.SetException(new HttpRequestException("Rede indisponível"));
        Assert.IsType<HttpRequestException>(await erro.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Download_pronto_na_abertura_retorna_sem_agendar_segunda_aplicacao()
    {
        var agendou=false;
        Assert.Equal("1.2.32",await DownloadComPrazo.AguardarAsync(Task.FromResult<string?>("1.2.32"),TimeSpan.FromSeconds(1),_=>agendou=true,_=>{}));
        Assert.False(agendou);
    }

    // ==================== transição para o repositório público de instaladores (nov/2026) ====================

    private static object[] TodosOsCanais(string versao) =>
        [Release($"v{versao}","win"),Release($"recepcao-v{versao}","recepcao"),Release($"financeiro-v{versao}","financeiro"),
         Release($"gerente-v{versao}","gerente"),Release($"clinico-v{versao}","clinico")];

    [Fact]
    public async Task Repositorio_novo_ainda_inexistente_le_do_Clinica()
    {
        var rede=new RedePorRepositorio(novo:null,anterior:TodosOsCanais("1.2.40"));
        var feed=await new FonteAtualizacaoGithub(rede).GetReleaseFeed(new NullVelopackLogger(),"Clinica.Gerente","gerente");
        Assert.Equal("1.2.40",Assert.Single(feed.Assets).Version.ToString());
        Assert.Contains(rede.Consultados,u=>u.Contains("/repos/lucaszaous-creator/clinica-atualizacoes/"));
        Assert.Contains(rede.Consultados,u=>u.Contains("/repos/lucaszaous-creator/Clinica/"));
    }

    [Fact]
    public async Task Com_os_cinco_canais_no_novo_o_Clinica_nem_e_consultado()
    {
        var rede=new RedePorRepositorio(novo:TodosOsCanais("1.2.41"),anterior:null);
        var feed=await new FonteAtualizacaoGithub(rede).GetReleaseFeed(new NullVelopackLogger(),"Clinica.Recepcao","recepcao");
        Assert.Equal("1.2.41",Assert.Single(feed.Assets).Version.ToString());
        Assert.DoesNotContain(rede.Consultados,u=>u.Contains("/repos/lucaszaous-creator/Clinica/"));
    }

    [Fact]
    public async Task Canal_que_falta_no_novo_vem_do_Clinica_e_vence_a_versao_maior()
    {
        var rede=new RedePorRepositorio(novo:[Release("recepcao-v1.2.50","recepcao")],
            anterior:[Release("recepcao-v1.2.49","recepcao"),Release("gerente-v1.2.48","gerente")]);
        var fonte=new FonteAtualizacaoGithub(rede);
        Assert.Equal("1.2.50",Assert.Single((await fonte.GetReleaseFeed(new NullVelopackLogger(),"Clinica.Recepcao","recepcao")).Assets).Version.ToString());
        Assert.Equal("1.2.48",Assert.Single((await new FonteAtualizacaoGithub(rede).GetReleaseFeed(new NullVelopackLogger(),"Clinica.Gerente","gerente")).Assets).Version.ToString());
    }

    [Fact]
    public async Task Depois_da_transicao_o_Clinica_privado_404_nao_impede_atualizar()
    {
        var rede=new RedePorRepositorio(novo:[Release("recepcao-v1.2.60","recepcao")],anterior:null);
        var feed=await new FonteAtualizacaoGithub(rede).GetReleaseFeed(new NullVelopackLogger(),"Clinica.Recepcao","recepcao");
        Assert.Equal("1.2.60",Assert.Single(feed.Assets).Version.ToString());
    }

    /// <summary>Uma página por repositório; nulo = o repositório responde 404 (não existe ou é privado).</summary>
    private sealed class RedePorRepositorio(object[]? novo, object[]? anterior) : IFileDownloader
    {
        public List<string> Consultados {get;}=[];
        public Task<string> DownloadString(string url, IDictionary<string,string>? headers=null,double timeout=30)
        {
            Consultados.Add(url);
            var lista=url.Contains("/repos/lucaszaous-creator/clinica-atualizacoes/")?novo:anterior;
            if(lista is null)throw new HttpRequestException("Not Found",null,System.Net.HttpStatusCode.NotFound);
            return Task.FromResult(JsonSerializer.Serialize(lista));
        }
        public Task<byte[]> DownloadBytes(string url, IDictionary<string,string>? headers=null,double timeout=30)
        {
            var tag=url.Split('/').Last().Replace(".json","");
            var m=System.Text.RegularExpressions.Regex.Match(tag,@"^(?:(\w+)-)?v(.+)$");
            var canal=m.Groups[1].Success?m.Groups[1].Value:"win";
            var pacote=canal switch{"win"=>"Clinica.Faturamento","recepcao"=>"Clinica.Recepcao","financeiro"=>"Clinica.Financeiro","gerente"=>"Clinica.Gerente",_=>"Clinica.Clinico"};
            var feed=new{Assets=new[]{new{PackageId=pacote,Version=m.Groups[2].Value,Type="Full",FileName=$"{pacote}-{m.Groups[2].Value}-full.nupkg",SHA256=new string('a',64),SHA1=new string('a',40),Size=1L}}};
            return Task.FromResult(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(feed)));
        }
        public Task DownloadFile(string url,string targetFile,Action<int> progress,IDictionary<string,string>? headers=null,double timeout=30,CancellationToken cancelToken=default)
            =>throw new NotSupportedException();
    }

    private sealed class Rede(params object[][] paginas) : IFileDownloader
    {
        public int Paginas {get;private set;}
        public List<string> Indices {get;}=[];
        public Task<string> DownloadString(string url, IDictionary<string,string>? headers=null,double timeout=30)
        {
            Assert.Contains("per_page=100",url);
            var pagina=int.Parse(url.Split("&page=")[1]); Paginas++;
            if(pagina>paginas.Length)throw new HttpRequestException("Página indisponível");
            return Task.FromResult(JsonSerializer.Serialize(paginas[pagina-1]));
        }
        public Task<byte[]> DownloadBytes(string url, IDictionary<string,string>? headers=null,double timeout=30)
        {
            Indices.Add(url);
            var feed=new {Assets=new[]{new {PackageId="Clinica.Clinico",Version="1.2.31",Type="Full",
                FileName="Clinica.Clinico-1.2.31-clinico-full.nupkg",Size=100,SHA1=new string('A',40),SHA256=new string('A',64)}}};
            return Task.FromResult(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(feed)));
        }
        public Task DownloadFile(string url,string targetFile,Action<int> progress,IDictionary<string,string>? headers=null,double timeout=30,CancellationToken cancelToken=default)
            => throw new NotSupportedException();
    }
}
