using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Serialization;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Shell.Treinamento;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Web;

public sealed record ResultadoBuscaSuiteWebDto(string Rota, string Rotulo, string? Caminho);
public sealed record AvisoSuiteWebDto(string Mensagem, string Tipo, string Hora);
public sealed record FerramentasSuiteWebDto(int NaoLidos, IReadOnlyList<AvisoSuiteWebDto> Avisos,
    bool FilaInfusaoDisponivel, int AssinaturasEnfermagem, int AssinaturasMedicas, int InfusoesDevolvidas,
    string ResumoAssinaturasInfusao, bool TreinamentoDisponivel);
public sealed record AulaSuiteWebDto(string Id, string Titulo, string Categoria, string Descricao,
    string DuracaoTexto, double Duracao, double Posicao, bool Concluida, string Estado,
    IReadOnlyList<CapituloTreinamento> Capitulos);
public sealed record CatalogoAulasSuiteWebDto(IReadOnlyList<AulaSuiteWebDto> Aulas,
    IReadOnlyList<string> Categorias, string Resumo);
public sealed record VideoAulaSuiteWebDto(AulaSuiteWebDto Aula, [property: JsonIgnore] string CaminhoVideo);

/// <summary>Portas explícitas das ferramentas do topo. Não instancia views ou players WPF.</summary>
public sealed class SuiteFerramentasWeb : IDisposable
{
    private readonly IServiceProvider _servicos;
    private readonly SnackbarService? _snackbar;
    private readonly ItemMenuModulo[] _menu;
    private readonly HashSet<string> _rotas;
    private readonly IReadOnlyList<AulaTreinamento> _aulas;
    private readonly AcervoTreinamento _acervo;
    private readonly int _usuario = SessaoUsuario.Atual.UsuarioId;
    private readonly Permissao _permissoes = SessaoUsuario.Atual.Efetivas;
    private int _enfermagem, _medicas, _devolvidas;
    private bool _consultando, _descartado;
    public event Action? Changed;

    public SuiteFerramentasWeb(IServiceProvider servicos, ItemMenuModulo[] menu,
        IEnumerable<string> rotasPermitidas, IEnumerable<string>? telasTreinamento = null, string? dadosLocais = null)
    {
        _servicos = servicos;
        _menu = menu.GroupBy(i => i.Chave).Select(g => g.First()).ToArray();
        _rotas = rotasPermitidas.ToHashSet(StringComparer.Ordinal);
        var telas = (telasTreinamento ?? _menu.Where(i => !i.Oculto).Select(i => i.Chave)).ToHashSet(StringComparer.Ordinal);
        bool mudou;
        do { mudou = false; foreach (var aba in _menu.Where(i => telas.Contains(i.Chave)).SelectMany(i => i.Abas))
            if (_rotas.Contains(aba.Chave)) mudou |= telas.Add(aba.Chave); } while (mudou);
        _aulas = CatalogoTreinamento.Filtrar(CatalogoTreinamento.Ler(), telas.Where(k => _rotas.Contains(k) || _menu.Any(i => i.Chave == k && i.Abas.Any(a => _rotas.Contains(a.Chave)))));
        _acervo = new AcervoTreinamento(_usuario, dadosLocais);
        _snackbar = servicos.GetService<SnackbarService>();
        if (_snackbar is not null) _snackbar.PropertyChanged += AoMudarAviso;
    }

    private void ExigirSessao()
    {
        ObjectDisposedException.ThrowIf(_descartado, this);
        if (SessaoUsuario.Atual.UsuarioId != _usuario || SessaoUsuario.Atual.Efetivas != _permissoes)
            throw new InvalidOperationException("A sessão mudou. Entre novamente para continuar.");
    }
    private bool Permitido(ItemMenuModulo item) => SessaoUsuario.Atual.Pode(item.Requer)
        && (item.RequerAlgum == Permissao.Nenhuma || SessaoUsuario.Atual.PodeAlgum(item.RequerAlgum))
        && (item.PerfilExclusivo is null || SessaoUsuario.Atual.Perfil == item.PerfilExclusivo);
    private string? Resolver(ItemMenuModulo item) => !Permitido(item) ? null : _rotas.Contains(item.Chave) ? item.Chave
        : item.Abas.Select(a => _menu.FirstOrDefault(i => i.Chave == a.Chave)).Where(i => i is not null && Permitido(i))
            .Select(i => i!.Chave).FirstOrDefault(_rotas.Contains);
    private void AoMudarAviso(object? sender, PropertyChangedEventArgs e) => Changed?.Invoke();

    public FerramentasSuiteWebDto Estado()
    {
        ExigirSessao();
        return new(_snackbar?.NaoLidos ?? 0,
            _snackbar?.Historico.Select(a => new AvisoSuiteWebDto(a.Mensagem, a.Tipo.ToString().ToLowerInvariant(), a.HoraTexto)).ToArray() ?? [],
            RotaFilaInfusao() is not null, _enfermagem, _medicas, _devolvidas,
            $"Abrir fila de infusões · assinaturas: enfermagem {_enfermagem}; médico responsável {_medicas}; devolvidas {_devolvidas}", true);
    }
    public void MarcarAvisosLidos() { ExigirSessao(); _snackbar?.MarcarLidos(); }
    public string? RotaFilaInfusao()
    {
        ExigirSessao();
        var item = _menu.FirstOrDefault(i => i.Chave == ChavesSuite.SalaInfusao);
        return item is null ? null : Resolver(item);
    }
    public async Task AtualizarInfusoesAsync()
    {
        ExigirSessao(); var sessao = SessaoUsuario.Atual;
        if (_consultando || !sessao.Autenticado || RotaFilaInfusao() is null ||
            !(sessao.Pode(Permissao.ChecarPrescricao) || sessao.Pode(Permissao.Prescrever) && sessao.ProfissionalId is > 0)) return;
        _consultando = true;
        try
        {
            using var scope = _servicos.CreateScope();
            var p = await scope.ServiceProvider.GetRequiredService<ChecagemPrescricaoService>()
                .ContarAssinaturasPendentesAsync(sessao.UsuarioId, sessao.ProfissionalId, sessao.Pode(Permissao.ChecarPrescricao), sessao.Pode(Permissao.Prescrever));
            ExigirSessao(); _enfermagem = p.Enfermagem; _medicas = p.Medico; _devolvidas = p.Devolvidas; Changed?.Invoke();
        }
        catch (Exception ex) { Application.Diagnostico.Registrar("Shell web — avisos de assinatura de infusão", ex); }
        finally { _consultando = false; }
    }
    public IReadOnlyList<ResultadoBuscaSuiteWebDto> Pesquisar(string? texto)
    {
        ExigirSessao(); var termo = texto?.Trim() ?? "";
        if (termo.Length == 0) return [];
        bool Casa(string valor) => CultureInfo.CurrentCulture.CompareInfo.IndexOf(valor, termo, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
        var resultados = new List<ResultadoBuscaSuiteWebDto>();
        var compostos = _menu.Where(i => !i.Oculto && Resolver(i) is not null && i.Abas.Count > 0).ToArray();
        var reivindicadas = compostos.SelectMany(i => i.Abas).Select(a => a.Chave).ToHashSet(StringComparer.Ordinal);
        foreach (var item in _menu.Where(i => !i.Oculto && !reivindicadas.Contains(i.Chave)))
        {
            var rota = Resolver(item); if (rota is null) continue;
            if (Casa(item.Rotulo) || Casa(GruposSidebar.Rotulo(item.Grupo))) resultados.Add(new(rota, item.Rotulo, null));
            else foreach (var aba in item.Abas)
            {
                var alvo = _menu.FirstOrDefault(i => i.Chave == aba.Chave);
                if (alvo is not null && Resolver(alvo) is { } chave && Casa(aba.Rotulo)) resultados.Add(new(chave, aba.Rotulo, item.Rotulo));
            }
        }
        return resultados.DistinctBy(r => r.Rota).ToArray();
    }
    private AulaTreinamento Aula(string id) => _aulas.FirstOrDefault(a => a.Id == id)
        ?? throw new InvalidOperationException("Aula indisponível neste aplicativo e perfil.");
    private AulaSuiteWebDto Dto(AulaTreinamento a)
    {
        var p = _acervo.Progresso(a.Id);
        return new(a.Id, a.Titulo, a.Categoria, a.Descricao, a.DuracaoTexto, a.Duracao, p.Posicao, p.Concluida,
            p.Concluida ? "Concluída" : p.Posicao > 0 ? "Continuar aula" : "Assistir aula", a.Capitulos);
    }
    public CatalogoAulasSuiteWebDto CatalogoAulas(string? busca = null, string? categoria = null, string situacao = "todas")
    {
        ExigirSessao(); var termo = busca?.Trim() ?? "";
        if (situacao is not ("todas" or "pendentes" or "concluidas")) throw new InvalidOperationException("Situação de aula inválida.");
        var aulas = _aulas.Where(a => (termo.Length == 0 || CultureInfo.CurrentCulture.CompareInfo.IndexOf(string.Join(" ", a.Funcoes.Prepend(a.Titulo).Append(a.Descricao)), termo, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)
            && (string.IsNullOrEmpty(categoria) || categoria == "Todas as categorias" || a.Categoria == categoria)
            && (situacao == "todas" || _acervo.Progresso(a.Id).Concluida == (situacao == "concluidas"))).Select(Dto).ToArray();
        return new(aulas, new[] { "Todas as categorias" }.Concat(_aulas.Select(a => a.Categoria).Distinct().Order()).ToArray(),
            $"{aulas.Length} aulas neste filtro · {_aulas.Count(a => _acervo.Progresso(a.Id).Concluida)} de {_aulas.Count} concluídas");
    }
    public async Task<VideoAulaSuiteWebDto> AbrirAulaAsync(string id, CancellationToken ct = default)
    {
        ExigirSessao(); var aula = Aula(id); var caminho = await _acervo.ObterVideoAsync(aula, ct);
        ExigirSessao(); return new(Dto(aula), caminho);
    }
    public void SalvarProgresso(string id, double posicao, bool? concluida = null)
    {
        ExigirSessao(); var aula = Aula(id);
        if (!double.IsFinite(posicao)) throw new InvalidOperationException("Posição de vídeo inválida.");
        _acervo.Salvar(id, Math.Clamp(posicao, 0, aula.Duracao), concluida ?? _acervo.Progresso(id).Concluida); Changed?.Invoke();
    }
    public void ReiniciarAula(string id) => SalvarProgresso(id, 0);
    public void Dispose() { if (_descartado) return; _descartado = true; if (_snackbar is not null) _snackbar.PropertyChanged -= AoMudarAviso; }
}
