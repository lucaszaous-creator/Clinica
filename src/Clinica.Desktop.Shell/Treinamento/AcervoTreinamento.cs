using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Clinica.Desktop.Shell.Treinamento;

public sealed class AcervoTreinamento
{
    private static readonly HttpClient Http=new(){Timeout=TimeSpan.FromMinutes(4)};
    private readonly string _raiz;
    private readonly HttpClient _http;
    private readonly string _midia;
    private readonly string _progresso;
    private readonly Dictionary<string,ProgressoAula> _assistidas;
    public AcervoTreinamento(int usuarioId,string? raiz=null,string? midia=null,HttpClient? http=null)
    {
        _raiz=raiz??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ClinicaSemDor","Treinamento");
        _http=http??Http;
        _midia=midia??Environment.GetEnvironmentVariable("CLINICA_TREINAMENTO_MIDIA")??Path.Combine(AppContext.BaseDirectory,"Treinamento","videos");
        _progresso=Path.Combine(_raiz,$"progresso-{usuarioId}.json");
        try{_assistidas=JsonSerializer.Deserialize<Dictionary<string,ProgressoAula>>(File.ReadAllText(_progresso))??[];}
        catch(IOException){_assistidas=[];} catch(JsonException){_assistidas=[];} catch(UnauthorizedAccessException){_assistidas=[];}
    }
    public ProgressoAula Progresso(string id)=>_assistidas.GetValueOrDefault(id)??new();
    public void Salvar(string id,double posicao,bool concluida)
    {
        _assistidas[id]=new(Math.Max(0,posicao),concluida);
        Directory.CreateDirectory(_raiz);
        var temporario=_progresso+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temporario,JsonSerializer.Serialize(_assistidas));File.Move(temporario,_progresso,true);}
        finally{if(File.Exists(temporario))File.Delete(temporario);}
    }
    public async Task<string> ObterVideoAsync(AulaTreinamento aula,CancellationToken ct)
    {
        if(!Regex.IsMatch(aula.Id,@"\A[a-z0-9]+(?:-[a-z0-9]+)*\z") || !Regex.IsMatch(aula.Sha256,@"\A[a-fA-F0-9]{64}\z"))
            throw new InvalidOperationException("Esta aula ainda não tem um vídeo publicado.");
        var local=Path.Combine(_midia,aula.Id+".mp4");
        if(await Valido(local,aula.Sha256,ct))return local;
        var cache=Path.Combine(_raiz,"videos",aula.Id+"-"+aula.Sha256.ToLowerInvariant()+".mp4");
        if(await Valido(cache,aula.Sha256,ct))return cache;
        Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
        var temporario=cache+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using var response=await _http.GetAsync("https://github.com/lucaszaous-creator/Clinica/releases/download/treinamento-desktop-20260928/"+aula.Sha256.ToLowerInvariant()+".mp4",HttpCompletionOption.ResponseHeadersRead,ct);
            response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength is > 150_000_000)throw new IOException("Vídeo maior que o tamanho permitido.");
            await using(var input=await response.Content.ReadAsStreamAsync(ct))
            await using(var dest=File.Create(temporario))
            {
                var buffer=new byte[81920];long total=0;int count;
                while((count=await input.ReadAsync(buffer,ct))>0)
                {total+=count;if(total>150_000_000)throw new IOException("Vídeo maior que o tamanho permitido.");await dest.WriteAsync(buffer.AsMemory(0,count),ct);}
            }
            if(!await Valido(temporario,aula.Sha256,ct))throw new IOException("O vídeo chegou incompleto. Tente novamente.");
            File.Move(temporario,cache,true);return cache;
        }
        finally{if(File.Exists(temporario))File.Delete(temporario);}
    }
    private static async Task<bool> Valido(string path,string expected,CancellationToken ct)
    {
        if(!File.Exists(path))return false;
        await using var stream=File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream,ct)).Equals(expected,StringComparison.OrdinalIgnoreCase);
    }
}
