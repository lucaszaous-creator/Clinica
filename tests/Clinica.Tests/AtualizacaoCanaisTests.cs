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
        Assert.Equal(2,rede.Paginas);
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
