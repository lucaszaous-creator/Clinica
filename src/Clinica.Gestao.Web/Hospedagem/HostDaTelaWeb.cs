using System.Text.RegularExpressions;
using Clinica.Application.Formatacao;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.JSInterop;

namespace Clinica.Gestao.Web.Hospedagem;

/// <summary>
/// O hospedeiro das telas no NAVEGADOR (nov/2026). O desktop grava no disco e abre o arquivo; aqui
/// o arquivo DESCE (download), o arquivo escolhido SOBE (upload) e copiar usa a área de
/// transferência do navegador. Os bytes trafegam por stream (DotNetStreamReference /
/// IJSStreamReference), nunca numa mensagem gigante do WebSocket.
///
/// O que só existe na máquina da clínica — escolher uma PASTA (backup, exportação do prontuário
/// em pasta) e abrir a ficha num app — responde com a frase de que está no aplicativo do computador,
/// em vez de um botão que não faz nada.
/// </summary>
public sealed partial class HostDaTelaWeb(
    IJSRuntime js, Notificacoes notificacoes, NavegacaoDaGestao navegacao, IServiceScopeFactory escopos,
    PedidoDaTelaDoPaciente pedidoDaTela, PedidoDaFichaDoBalcao pedidoDaFicha) : IHostDaTela
{
    /// <summary>Teto do arquivo que SOBE (OFX, CSV/ZIP de importação). Acima disso, a tela pede outro.</summary>
    public const long TetoDoEnvio = 25 * 1024 * 1024;

    private const string ApenasNoApp = "Isto só existe no aplicativo do computador da clínica.";

    public Task<string?> ExportarCsvAsync(IReadOnlyList<string> cabecalho, IReadOnlyList<IReadOnlyList<string>> linhas, string nomeArquivo)
        => BaixarAsync(CsvParaExcel.Montar(cabecalho, linhas), Garantir(nomeArquivo, ".csv"), "text/csv;charset=utf-8");

    public Task<string?> SalvarEAbrirAsync(byte[] conteudo, string nomeSugerido, string filtro, string extensao)
        => BaixarAsync(conteudo, Garantir(nomeSugerido, extensao), Tipo(extensao));

    public Task<string?> AbrirPreviaAsync(byte[] pdf, string nomeSugerido)
        => BaixarAsync(pdf, Garantir(nomeSugerido, ".pdf"), "application/pdf", abrir: true);

    public async Task<ArquivoEscolhido?> EscolherArquivoAsync(string filtro, string titulo)
    {
        try
        {
            var r = await js.InvokeAsync<EscolhaDoNavegador?>("gestaoWeb.escolher", Aceitar(filtro), TetoDoEnvio);
            if (r is null) return null;
            if (r.Erro is { } erro) { notificacoes.Erro(erro); return null; }
            if (r.Conteudo is null) return null;
            await using var stream = await r.Conteudo.OpenReadStreamAsync(TetoDoEnvio);
            using var memoria = new MemoryStream();
            await stream.CopyToAsync(memoria);
            return new ArquivoEscolhido(Path.GetFileName(r.Nome ?? "arquivo"), memoria.ToArray());
        }
        catch (JSException) { notificacoes.Erro("Não foi possível ler o arquivo escolhido."); return null; }
    }

    public async Task<bool> CopiarAsync(string texto)
    {
        try { return await js.InvokeAsync<bool>("gestaoWeb.copiar", texto); }
        catch (JSException) { return false; }
    }

    public Task<bool> NavegarAsync(string chave) => Task.FromResult(navegacao.Navegar(chave));

    public bool TemTela(string chave) => navegacao.Conhece(chave);

    public Task<bool> AbrirEnderecoAsync(string url)
    {
        // Só endereço https: o resto (file:, javascript:, app local) não abre do navegador.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps) return Task.FromResult(false);
        return Abrir(u.ToString());
    }

    private async Task<bool> Abrir(string url)
    {
        try { await js.InvokeVoidAsync("gestaoWeb.abrir", url); return true; }
        catch (JSException) { return false; }
    }

    /// <summary>
    /// A ficha do paciente, como no app: a tela do paciente para quem alcança o prontuário; a ficha
    /// do balcão (a administrativa) para quem não alcança. Falso só quando nenhuma das duas existe
    /// para este acesso — e quem chamou diz isso.
    /// </summary>
    public async Task<bool> AbrirFichaDoPacienteAsync(int pacienteId)
    {
        var telaDoPaciente = navegacao.Conhece(ChavesDaGestao.TelaDoPaciente);
        if (!telaDoPaciente && !navegacao.Conhece(PedidoDaFichaDoBalcao.Chave)) return false;
        Clinica.Domain.Entities.Paciente? paciente;
        using (var escopo = escopos.CreateScope())
            paciente = await escopo.ServiceProvider.GetRequiredService<Clinica.Application.Servicos.PacienteService>().ObterAsync(pacienteId);
        if (paciente is null) return false;
        if (telaDoPaciente)
        {
            pedidoDaTela.Definir(new PacienteNoPosto(paciente.Id, paciente.Nome, null, null, null),
                Clinica.Application.Modelos.TelaDoPacienteNaTela.FichaDoPaciente);
            return navegacao.Navegar(ChavesDaGestao.TelaDoPaciente);
        }
        pedidoDaFicha.Definir(paciente.Id, paciente.Nome);
        return navegacao.Navegar(PedidoDaFichaDoBalcao.Chave);
    }

    // "Onde salvar?" na web é o download: o caminho é só o NOME, e gravar é baixar.
    public Task<string?> PerguntarOndeSalvarAsync(string nomeSugerido, string filtro, string extensao)
        => Task.FromResult<string?>("download:" + Garantir(nomeSugerido, extensao));

    public Task<string?> PerguntarPastaAsync(string titulo)
    {
        notificacoes.Info(ApenasNoApp + " (escolher uma pasta)");
        return Task.FromResult<string?>(null);
    }

    public async Task<string?> GravarArquivoAsync(string caminho, byte[] conteudo)
    {
        if (!caminho.StartsWith("download:", StringComparison.Ordinal)) return ApenasNoApp;
        var nome = caminho["download:".Length..];
        return await BaixarAsync(conteudo, nome, Tipo(Path.GetExtension(nome)));
    }

    private async Task<string?> BaixarAsync(byte[] conteudo, string nome, string tipo, bool abrir = false)
    {
        try
        {
            using var referencia = new DotNetStreamReference(new MemoryStream(conteudo));
            await js.InvokeVoidAsync("gestaoWeb.baixar", NomeSeguro(nome), tipo, referencia, abrir);
            return null;
        }
        catch (JSException) { return "O navegador não aceitou o download. Tente de novo."; }
    }

    private sealed record EscolhaDoNavegador(string? Nome, IJSStreamReference? Conteudo, string? Erro);

    private static string Garantir(string nome, string extensao)
    {
        extensao = extensao.StartsWith('.') ? extensao : "." + extensao;
        return nome.EndsWith(extensao, StringComparison.OrdinalIgnoreCase) ? nome : nome + extensao;
    }

    /// <summary>Nome de arquivo sem caminho nem caractere que o sistema recuse.</summary>
    public static string NomeSeguro(string nome)
    {
        var limpo = InvalidosNoNome().Replace(Path.GetFileName(nome), "_").Trim();
        return string.IsNullOrEmpty(limpo) ? "arquivo" : limpo.Length > 120 ? limpo[..120] : limpo;
    }

    /// <summary>"CSV (*.csv)|*.csv|ZIP (*.zip)|*.zip" → ".csv,.zip" (o accept do input de arquivo).</summary>
    public static string Aceitar(string filtro)
    {
        var partes = filtro.Split('|');
        var padroes = partes.Where((_, i) => i % 2 == 1).SelectMany(p => p.Split(';'))
            .Select(p => p.Trim().TrimStart('*')).Where(p => p.StartsWith('.') && p.Length > 1).Distinct();
        return string.Join(',', padroes);
    }

    private static string Tipo(string extensao) => extensao.TrimStart('.').ToLowerInvariant() switch
    {
        "pdf" => "application/pdf",
        "csv" => "text/csv;charset=utf-8",
        "zip" => "application/zip",
        "xml" => "application/xml",
        "json" => "application/json",
        "txt" => "text/plain;charset=utf-8",
        _ => "application/octet-stream",
    };

    [GeneratedRegex(@"[\\/:*?""<>|\x00-\x1F]")]
    private static partial Regex InvalidosNoNome();
}
