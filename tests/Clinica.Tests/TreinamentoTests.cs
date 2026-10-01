using Clinica.Desktop.Shell.Treinamento;
using System.Net;
using System.Security.Cryptography;
using Xunit;

namespace Clinica.Tests;

public sealed class TreinamentoTests : IDisposable
{
    private readonly string _root=Path.Combine(Path.GetTempPath(),"clinica-treinamento-test-"+Guid.NewGuid().ToString("N"));
    public TreinamentoTests()=>Directory.CreateDirectory(_root);
    public void Dispose()=>Directory.Delete(_root,true);
    [Fact]
    public void AulasCompartilhadasAparecemUmaVezESoComTelasPermitidas()
    {
        var caixa=new AulaTreinamento{Id="caixa",Telas=["caixa"]};
        var infusao=new AulaTreinamento{Id="infusao",Telas=["infusao"]};
        var combinada=new AulaTreinamento{Id="combinada",Telas=["caixa","infusao"]};
        Assert.Equal(new[]{caixa},CatalogoTreinamento.Filtrar([caixa,infusao,caixa,combinada,new(){Id="sem-rota"}],["caixa"]));
        Assert.Equal(3,CatalogoTreinamento.Filtrar([caixa,infusao,caixa,combinada],["caixa","infusao"]).Count);
    }
    [Fact]
    public void ProgressoPersisteIsoladoPorUsuario()
    {
        new AcervoTreinamento(10,_root).Salvar("agenda",15.5,true);
        Assert.Equal(new ProgressoAula(15.5,true),new AcervoTreinamento(10,_root).Progresso("agenda"));
        Assert.Equal(new ProgressoAula(),new AcervoTreinamento(20,_root).Progresso("agenda"));
    }
    [Fact]
    public async Task DownloadIncompletoNaoEntraNoCacheETentativaSeguinteRecupera()
    {
        byte[] valid=[1,2,3,4];var handler=new Handler([9,9]);using var http=new HttpClient(handler);
        var aula=new AulaTreinamento{Id="agenda",Sha256=Convert.ToHexString(SHA256.HashData(valid))};
        var acervo=new AcervoTreinamento(10,_root,_root,http);
        await Assert.ThrowsAsync<IOException>(()=>acervo.ObterVideoAsync(aula,CancellationToken.None));
        Assert.Empty(Directory.GetFiles(_root,"*.tmp",SearchOption.AllDirectories));
        handler.Content=valid;
        var file=await acervo.ObterVideoAsync(aula,CancellationToken.None);
        Assert.Equal(valid,await File.ReadAllBytesAsync(file));
        handler.Content=[];
        Assert.Equal(file,await acervo.ObterVideoAsync(aula,CancellationToken.None));
        Assert.Equal(2,handler.Calls);
    }
    [Fact]
    public async Task IdInvalidoNaoFazDownload()
    {
        var handler=new Handler([]);using var http=new HttpClient(handler);
        var acervo=new AcervoTreinamento(10,_root,_root,http);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>acervo.ObterVideoAsync(new(){Id="../outro",Sha256=new string('a',64)},CancellationToken.None));
        Assert.Equal(0,handler.Calls);
    }
    private sealed class Handler(byte[] content):HttpMessageHandler
    {
        public byte[] Content=content;public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {Calls++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(Content)});}
    }
}
