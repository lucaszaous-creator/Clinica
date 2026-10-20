using System.IO;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Web.WebView2.Core;

namespace Clinica.Desktop.Shell.Treinamento;

/// <summary>
/// O treinamento do app para a tela Blazor (out/2026 — antes era a TreinamentoView do WPF,
/// que tocava o vídeo num MediaElement). O catálogo e o recorte continuam os mesmos
/// (<see cref="CatalogoTreinamento"/>: uma aula só aparece se TODAS as telas dela existirem
/// neste executável), o progresso continua por usuário em LocalAppData, e o vídeo continua
/// baixado e conferido pelo SHA-256 (<see cref="AcervoTreinamento"/>).
///
/// O que mudou é QUEM toca o vídeo: o <c>&lt;video&gt;</c> do navegador. A página não alcança
/// arquivo local (ela é servida de dentro da DLL), então as duas pastas de vídeo viram
/// endereços próprios do navegador (<see cref="MapearPastas"/>) — o arquivo continua no disco,
/// o navegador só passa a enxergá-lo, e nada sai da máquina.
/// </summary>
public sealed class TreinamentoDaSuite : ITreinamentoDoApp
{
    /// <summary>A pasta de vídeos que acompanha a instalação.</summary>
    public const string HostMidia = "aulas.clinica.local";

    /// <summary>A pasta de vídeos baixados (cache conferido pelo SHA-256).</summary>
    public const string HostCache = "aulas-cache.clinica.local";

    private IReadOnlyList<AulaTreinamento> _aulas = [];
    private AcervoTreinamento _acervo = new(0);

    /// <summary>
    /// Recorta o catálogo pelas telas que ESTE executável publica e abre o progresso de quem
    /// entrou. Chamado pelo ShellViewModel, que é quem sabe as duas coisas.
    /// </summary>
    public void Definir(IEnumerable<string> telasPermitidas, int usuarioId)
    {
        _aulas = CatalogoTreinamento.Filtrar(CatalogoTreinamento.Ler(), telasPermitidas);
        _acervo = new AcervoTreinamento(usuarioId);
    }

    public IReadOnlyList<AulaDoTreinamento> Aulas() => _aulas
        .Select(a => new AulaDoTreinamento(a.Id, a.Titulo, a.Categoria, a.Descricao, a.Funcoes,
            a.Capitulos.Select(c => new CapituloDaAula(c.Inicio, c.Titulo, c.Texto)).ToList(), a.Duracao))
        .ToList();

    public ProgressoDaAula Progresso(string aulaId)
    {
        var p = _acervo.Progresso(aulaId);
        return new ProgressoDaAula(p.Posicao, p.Concluida);
    }

    public void Salvar(string aulaId, double posicao, bool concluida) => _acervo.Salvar(aulaId, posicao, concluida);

    public async Task<string> EnderecoDoVideoAsync(string aulaId, CancellationToken ct)
    {
        var aula = _aulas.FirstOrDefault(a => a.Id == aulaId)
                   ?? throw new InvalidOperationException("Esta aula não está disponível neste aplicativo.");
        var caminho = await _acervo.ObterVideoAsync(aula, ct);
        return Endereco(caminho, _acervo.PastaDeMidia, HostMidia)
               ?? Endereco(caminho, _acervo.PastaDeCache, HostCache)
               ?? throw new InvalidOperationException("O vídeo desta aula está numa pasta que o aplicativo não reconhece.");
    }

    /// <summary>O caminho do arquivo dentro da pasta, como endereço do navegador.</summary>
    private static string? Endereco(string caminho, string pasta, string host)
    {
        var raiz = Path.GetFullPath(pasta).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var completo = Path.GetFullPath(caminho);
        if (!completo.StartsWith(raiz, StringComparison.OrdinalIgnoreCase)) return null;
        var relativo = completo[raiz.Length..].Replace(Path.DirectorySeparatorChar, '/');
        return $"https://{host}/{string.Join('/', relativo.Split('/').Select(Uri.EscapeDataString))}";
    }

    /// <summary>
    /// Dá ao navegador da janela os dois endereços dos vídeos. Chamado quando o navegador nasce;
    /// pasta que não existe (instalação sem vídeos embutidos) simplesmente não ganha endereço.
    /// </summary>
    public void MapearPastas(CoreWebView2 navegador)
    {
        try
        {
            var midia = _acervo.PastaDeMidia;
            if (Directory.Exists(midia))
                navegador.SetVirtualHostNameToFolderMapping(HostMidia, midia, CoreWebView2HostResourceAccessKind.Allow);
            var cache = _acervo.PastaDeCache;
            Directory.CreateDirectory(cache);
            navegador.SetVirtualHostNameToFolderMapping(HostCache, cache, CoreWebView2HostResourceAccessKind.Allow);
        }
        catch (Exception ex)
        {
            // Sem endereço, a aula diz que não conseguiu tocar — o resto do app não depende disto.
            LogSuite.Registrar("Treinamento — as pastas de vídeo não ganharam endereço no navegador", ex);
        }
    }
}
