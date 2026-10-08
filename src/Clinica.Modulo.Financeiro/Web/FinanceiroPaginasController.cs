using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Financeiro.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Financeiro.Web;

/// <summary>Adaptador de apresentação. As propriedades refletidas são constantes deste registro,
/// nunca caminhos recebidos do JavaScript. IDs opacos só resolvem objetos ainda presentes na coleção.</summary>
public sealed partial class FinanceiroPaginasController : IDisposable
{
    private readonly IServiceProvider _services;
    private readonly CaixaViewModel? _caixa;
    private readonly Dictionary<string, Pagina> _paginas = CriarRegistro();
    private readonly Dictionary<object, string> _ids = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, string> _opcaoIds = new(ReferenceEqualityComparer.Instance);
    private readonly List<Action> _desinscrever = [];
    private Pagina? _pagina;
    private object? _vm;
    private bool _ocupado, _disposed;
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    public string Contexto { get; private set; } = Guid.NewGuid().ToString("N");
    public event Action? Changed;

    public FinanceiroPaginasController(IServiceProvider services, CaixaViewModel? caixa = null)
    { _services = services; _caixa = caixa; }

    /// <summary>Verifica os contratos estáticos sem criar VMs nem consultar dados.</summary>
    public static IReadOnlyList<string> ValidarRegistro()
    {
        var erros = new List<string>();
        foreach (var p in CriarRegistro().Values)
        {
            void Propriedade(Type tipo, string? nome)
            {
                if (nome is not null && tipo.GetProperty(nome) is null) erros.Add($"{p.Chave}: {tipo.Name}.{nome} ausente");
            }
            void CampoValido(Type tipo, Campo c)
            {
                Propriedade(tipo, c.Propriedade); Propriedade(tipo, c.Opcoes);
                if (tipo.GetProperty(c.Propriedade)?.SetMethod is null) erros.Add($"{p.Chave}: {tipo.Name}.{c.Propriedade} não editável");
                if (c.Guarda is not null) Propriedade(c.Guarda.StartsWith("vm:") ? p.Tipo : tipo, c.Guarda.Replace("vm:", ""));
            }
            void AcaoValida(Type tipo, Acao a)
            {
                Propriedade(p.Tipo, a.Comando + "Command"); Propriedade(p.Tipo, a.Selecao);
                if (a.Guarda is not null) Propriedade(a.Guarda.StartsWith("vm:") ? p.Tipo : tipo, a.Guarda.Replace("vm:", ""));
            }
            foreach (var c in p.Campos.Concat(p.Secoes.SelectMany(s => s.Campos))) CampoValido(p.Tipo, c);
            foreach (var a in p.Acoes.Concat(p.Secoes.SelectMany(s => s.Acoes))) AcaoValida(p.Tipo, a);
            foreach (var indicador in p.Indicadores.Concat(p.Secoes.SelectMany(s => s.Indicadores))) Propriedade(p.Tipo, indicador.Split('|')[0]);
            foreach (var secao in p.Secoes)
            {
                Propriedade(p.Tipo, secao.Descricao);
                foreach (var tab in secao.Tabelas)
                {
                    var origem = p.Tipo.GetProperty(tab.Origens[0]);
                    if (origem is null) { Propriedade(p.Tipo, tab.Origens[0]); continue; }
                    var tipo = tab.Chave == "ContasAtrasadas" ? typeof(LinhaContaAtraso) : origem.PropertyType.GetGenericArguments().FirstOrDefault();
                    if (tipo is null) { erros.Add($"{p.Chave}: tipo da tabela {tab.Chave} desconhecido"); continue; }
                    foreach (var c in tab.Colunas)
                        if (tab.Celula is null) Propriedade(tipo, c.Propriedade);
                    foreach (var c in tab.Campos) CampoValido(tipo, c);
                    foreach (var a in tab.Acoes) AcaoValida(tipo, a);
                }
            }
        }
        return erros;
    }

    public Task NavegarAsync(string chave)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_ocupado) throw new InvalidOperationException("Conclua a operação atual antes de navegar.");
        if (!_paginas.TryGetValue(chave, out var pagina)) throw new InvalidOperationException("Página financeira inexistente.");
        Exigir(pagina);
        Desinscrever();
        _ids.Clear(); _opcaoIds.Clear();
        Contexto = Guid.NewGuid().ToString("N");
        _pagina = pagina;
        _vm = pagina.Tipo == typeof(CaixaViewModel) && _caixa is not null ? _caixa : _services.GetRequiredService(pagina.Tipo);
        Assinar();
        Changed?.Invoke();
        // Os VMs iniciam suas cargas nos próprios construtores; não duplicar consultas.
        return Task.CompletedTask;
    }

    public PaginaFinanceiroDto ObterPagina()
    {
        var (p, vm) = Atual();
        var vivos = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var secoes = p.Secoes.Select(s => new SecaoFinanceiroDto(s.Chave, s.Titulo,
            TextoOpcional(vm, s.Descricao), s.Campos.Select(c => CriarCampo(vm, c)).ToArray(),
            Indicadores(vm, s.Indicadores), s.Tabelas.Select(t => Tabela(vm, t, vivos)).ToArray(),
            s.Acoes.Select(a => CriarAcao(vm, a, null)).ToArray(), Graficos(vm, s.Chave))).ToArray();
        foreach (var morto in _ids.Keys.Where(k => !vivos.Contains(k)).ToArray()) _ids.Remove(morto);
        return new(p.Chave, p.Titulo, TextoOpcional(vm, p.Subtitulo),
            p.Campos.Select(c => CriarCampo(vm, c)).ToArray(), Indicadores(vm, p.Indicadores), secoes,
            p.Acoes.Select(a => CriarAcao(vm, a, null)).ToArray(), _ocupado || B(vm, "Carregando"),
            p.Falhas.Any(f => B(vm, f)), TextoOpcional(vm, "Mensagem"), B(vm, "MensagemEhErro"), B(vm, "Truncado"), Contexto);
    }

    public Task AtualizarCampoAsync(string chave, JsonElement valor, string? tabela = null, string? linha = null)
    {
        var (p, vm) = Atual();
        if (_ocupado || B(vm, "Carregando")) throw new InvalidOperationException("Aguarde a operação atual.");
        object alvo = vm;
        IEnumerable<Campo> campos = p.Campos.Concat(p.Secoes.SelectMany(s => s.Campos));
        if (tabela is not null)
        {
            var t = EncontrarTabela(p, tabela);
            alvo = ResolverLinha(vm, t, linha);
            campos = t.Campos;
        }
        var campo = campos.SingleOrDefault(c => c.Propriedade == chave) ?? throw new InvalidOperationException("Campo inválido.");
        if (!Permitido(campo.Permissao, campo.Guarda, alvo, vm)) throw new UnauthorizedAccessException("Campo indisponível para esta sessão.");
        var prop = alvo.GetType().GetProperty(campo.Propriedade) ?? throw new InvalidOperationException("Campo não configurado.");
        object? convertido;
        if (campo.Tipo == "selecao")
        {
            var token = valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;
            var opcoes = Opcoes(alvo, campo).ToArray();
            var opcao = opcoes.SingleOrDefault(o => TokenOpcao(o.Valor) == token);
            if (opcao is null) throw new InvalidOperationException("Opção não pertence à lista atual.");
            convertido = opcao.Valor;
        }
        else convertido = Converter(valor, prop.PropertyType, campo.Tipo);
        prop.SetValue(alvo, convertido);
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public async Task ExecutarAcaoAsync(string chave, string? tabela = null, string? linha = null)
    {
        var (p, vm) = Atual();
        if (_ocupado || B(vm, "Carregando")) throw new InvalidOperationException("Aguarde a operação atual.");
        object? parametro = null;
        IEnumerable<Acao> acoes = p.Acoes.Concat(p.Secoes.SelectMany(s => s.Acoes));
        if (tabela is not null)
        {
            var t = EncontrarTabela(p, tabela);
            parametro = ResolverLinha(vm, t, linha);
            acoes = t.Acoes;
        }
        var acao = acoes.SingleOrDefault(a => a.Comando == chave) ?? throw new InvalidOperationException("Ação inválida.");
        if (!Permitido(acao.Permissao, acao.Guarda, parametro ?? vm, vm)) throw new UnauthorizedAccessException("Ação indisponível para esta sessão.");
        var comando = Ler(vm, acao.Comando + "Command") as ICommand ?? throw new InvalidOperationException("Comando não configurado.");
        if (acao.Selecao is not null) vm.GetType().GetProperty(acao.Selecao)!.SetValue(vm, parametro);
        if (acao.SemParametro) parametro = null;
        if (!comando.CanExecute(parametro)) throw new InvalidOperationException("Ação indisponível neste momento.");
        _ocupado = true; Changed?.Invoke();
        try
        {
            if (comando is IAsyncRelayCommand asyncCommand) await asyncCommand.ExecuteAsync(parametro);
            else comando.Execute(parametro);
        }
        finally { _ocupado = false; Assinar(); Changed?.Invoke(); }
    }

    private (Pagina, object) Atual()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_pagina is null || _vm is null) throw new InvalidOperationException("Selecione uma página.");
        Exigir(_pagina); return (_pagina, _vm);
    }
    private static void Exigir(Pagina p)
    {
        var sessao = SessaoUsuario.Atual;
        if (!sessao.Autenticado || !sessao.Pode(Permissao.VerFinanceiro) || !sessao.Pode(p.Permissao))
            throw new UnauthorizedAccessException("Sem acesso a esta página financeira.");
    }
    private static bool Permitido(Permissao permissao, string? guarda, object alvo, object vm) =>
        SessaoUsuario.Atual.Autenticado && SessaoUsuario.Atual.Pode(permissao) &&
        (guarda is null || (guarda.StartsWith("vm:", StringComparison.Ordinal) ? B(vm, guarda[3..]) : B(alvo, guarda)));
    private static Tab EncontrarTabela(Pagina p, string chave) => p.Secoes.SelectMany(s => s.Tabelas)
        .SingleOrDefault(t => t.Chave == chave) ?? throw new InvalidOperationException("Tabela inválida.");
    private object ResolverLinha(object vm, Tab tabela, string? id) => tabela.Linhas(vm)
        .SingleOrDefault(row => _ids.TryGetValue(row, out var token) && token == id)
        ?? throw new InvalidOperationException("O registro não está mais nesta lista. Atualize a página.");
    private TabelaFinanceiroDto Tabela(object vm, Tab t, HashSet<object> vivos)
    {
        var linhas = t.Linhas(vm).Select(row =>
        {
            vivos.Add(row);
            if (!_ids.TryGetValue(row, out var id)) _ids[row] = id = Guid.NewGuid().ToString("N");
            return new LinhaFinanceiroDto(id, t.Colunas.ToDictionary(c => c.Propriedade, c => t.Celula?.Invoke(vm, row, c.Propriedade) ?? Formatar(Ler(row, c.Propriedade), c.Tipo)),
                t.Campos.Select(c => CriarCampo(row, c)).ToArray(), t.Acoes.Select(a => CriarAcao(vm, a, row)).ToArray());
        }).ToArray();
        var vazio = B(vm, "Carregando") ? "Consultando os registros…"
            : B(vm, "NaoVerificado") ? "A consulta não pôde ser verificada. Atualize para tentar novamente."
            : B(vm, "FiltroAtivo") ? "Nenhum registro corresponde aos filtros. Limpe os filtros para ver os demais."
            : "Nenhum registro neste período.";
        return new(t.Chave, t.Titulo, t.Colunas.Select(c => new ColunaFinanceiroDto(c.Propriedade, c.Rotulo, c.Tipo)).ToArray(), linhas, vazio);
    }
    private CampoFinanceiroDto CriarCampo(object alvo, Campo c)
    {
        var valor = Ler(alvo, c.Propriedade);
        object? exibido = c.Tipo == "selecao" ? TokenOpcao(valor) : valor is DateTime dt ? dt.ToString(c.Tipo == "mes" ? "yyyy-MM" : "yyyy-MM-dd", CultureInfo.InvariantCulture) : valor;
        return new(c.Propriedade, c.Rotulo, c.Tipo, exibido, Opcoes(alvo, c).Select(o => new OpcaoFinanceiroDto(TokenOpcao(o.Valor), o.Rotulo)).ToArray(),
            Habilitado: !_ocupado && !B(_vm!, "Carregando") && Permitido(c.Permissao, c.Guarda, alvo, _vm!));
    }
    private AcaoFinanceiroDto CriarAcao(object vm, Acao a, object? row)
    {
        var cmd = Ler(vm, a.Comando + "Command") as ICommand;
        return new(a.Comando, a.Rotulo, !_ocupado && !B(vm, "Carregando") && Permitido(a.Permissao, a.Guarda, row ?? vm, vm) && cmd?.CanExecute(a.SemParametro ? null : row) == true, a.Estilo);
    }
    private static IndicadorFinanceiroDto[] Indicadores(object vm, string[] propriedades) => propriedades.Select(p =>
    { var partes = p.Split('|'); return new IndicadorFinanceiroDto(partes.Length > 1 ? partes[1] : partes[0], Formatar(Ler(vm, partes[0]))); }).ToArray();
    private sealed record Opcao(object? Valor, string Rotulo);
    private IEnumerable<Opcao> Opcoes(object alvo, Campo c)
    {
        if (c.Fixas is not null) return c.Fixas.Select(v => new Opcao(v, Formatar(v)));
        if (c.Opcoes is null) return [];
        return Lista(alvo, c.Opcoes).Select(o => new Opcao(c.ValorOpcao is null ? o : Ler(o, c.ValorOpcao), c.RotuloOpcao is null ? Formatar(o) : Formatar(Ler(o, c.RotuloOpcao))));
    }
    private string TokenOpcao(object? valor)
    {
        if (valor is null) return "";
        if (valor is string or ValueType) return Convert.ToString(valor, CultureInfo.InvariantCulture) ?? "";
        if (!_opcaoIds.TryGetValue(valor, out var token)) _opcaoIds[valor] = token = Guid.NewGuid().ToString("N");
        return token;
    }
    private static object? Converter(JsonElement valor, Type tipo, string formato)
    {
        var baseTipo = Nullable.GetUnderlyingType(tipo) ?? tipo;
        if (valor.ValueKind == JsonValueKind.Null && (!tipo.IsValueType || Nullable.GetUnderlyingType(tipo) is not null)) return null;
        var texto = valor.ValueKind == JsonValueKind.String ? valor.GetString() ?? "" : valor.GetRawText();
        if (texto.Length > 5000) throw new InvalidOperationException("Texto muito longo.");
        if (baseTipo == typeof(string)) return texto;
        if (baseTipo == typeof(bool)) return valor.ValueKind is JsonValueKind.True or JsonValueKind.False ? valor.GetBoolean() : bool.Parse(texto);
        if (baseTipo == typeof(DateTime))
        {
            if (texto.Length == 0 && Nullable.GetUnderlyingType(tipo) is not null) return null;
            return DateTime.ParseExact(texto, formato == "mes" ? "yyyy-MM" : "yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        if (baseTipo == typeof(int)) { var n = int.Parse(texto, CultureInfo.InvariantCulture); if (n < 0 || n > 3650) throw new InvalidOperationException("Intervalo inválido."); return n; }
        throw new InvalidOperationException("Tipo de campo não permitido.");
    }
    private static object? Ler(object o, string p) => o.GetType().GetProperty(p)?.GetValue(o);
    private static bool B(object o, string p) => Ler(o, p) is true;
    private static string? TextoOpcional(object o, string? p) => p is null ? null : Ler(o, p)?.ToString();
    private static IEnumerable<object> Lista(object o, string p) => (Ler(o, p) as IEnumerable)?.Cast<object>() ?? [];
    private static string Formatar(object? v, string tipo = "texto") => v switch
    {
        null => "—", DateTime d => d.ToString("dd/MM/yyyy", Cultura), DateOnly d => d.ToString("dd/MM/yyyy", Cultura),
        decimal n => n.ToString(tipo == "numero" ? "N2" : "C", Cultura), double n => tipo == "percentual" ? n.ToString("0.#", Cultura) + "%" : n.ToString("N2", Cultura),
        bool b => b ? "Sim" : "Não", Enum e => RotulosEnum.De(e), _ => v.ToString() ?? "—"
    };
    private void Assinar()
    {
        Desinscrever();
        if (_disposed || _vm is null || _pagina is null) return;
        var vistos = new HashSet<object>(ReferenceEqualityComparer.Instance);
        void Item(object obj)
        {
            if (!vistos.Add(obj)) return;
            if (obj is INotifyPropertyChanged notify)
            {
                PropertyChangedEventHandler handler = (_, _) => { if (!_disposed) Changed?.Invoke(); };
                notify.PropertyChanged += handler; _desinscrever.Add(() => notify.PropertyChanged -= handler);
            }
        }
        Item(_vm);
        foreach (var tabela in _pagina.Secoes.SelectMany(s => s.Tabelas))
        {
            foreach (var origem in tabela.Origens)
                if (Ler(_vm, origem) is INotifyCollectionChanged collection)
                {
                    NotifyCollectionChangedEventHandler handler = (_, _) => { if (!_disposed) { Assinar(); Changed?.Invoke(); } };
                    collection.CollectionChanged += handler; _desinscrever.Add(() => collection.CollectionChanged -= handler);
                }
            foreach (var row in tabela.Linhas(_vm)) Item(row);
        }
    }
    private void Desinscrever() { foreach (var remover in _desinscrever) remover(); _desinscrever.Clear(); }
    public void Dispose() { if (_disposed) return; _disposed = true; Desinscrever(); _ids.Clear(); _opcaoIds.Clear(); Changed = null; }

    private static IReadOnlyList<GraficoFinanceiroDto> Graficos(object vm, string secao)
    {
        if (B(vm, "NaoVerificado") || B(vm, "MensagemEhErro") || B(vm, "Carregando")) return [];
        if (vm is FluxoCaixaViewModel fluxo)
        {
            if (secao == "meses") return [
                new("entradas", "Entradas realizadas", "linha", "moeda", fluxo.SerieEntradas.Select(p => new PontoFinanceiroDto(p.Rotulo, p.Valor, p.Valor?.ToString("C", Cultura) ?? "Não verificado")).ToArray()),
                new("resultado", "Resultado realizado", "linha", "moeda", fluxo.SerieResultado.Select(p => new PontoFinanceiroDto(p.Rotulo, p.Valor, p.Valor?.ToString("C", Cultura) ?? "Não verificado")).ToArray())];
            if (secao == "categorias") return [
                new("categorias-entradas", "De onde veio", "barra", "percentual", fluxo.Entradas.Select(p => new PontoFinanceiroDto(p.Rotulo, p.Fracao * 100, p.ValorRotulo)).ToArray()),
                new("categorias-saidas", "Para onde foi", "barra", "percentual", fluxo.Saidas.Select(p => new PontoFinanceiroDto(p.Rotulo, p.Fracao * 100, p.ValorRotulo)).ToArray())];
        }
        if (vm is ResultadoViewModel resultado)
        {
            if (secao == "resultado" && !resultado.ResultadoNaoVerificado) return [
                new("categorias-receitas", "Receitas por categoria", "barra", "percentual", resultado.Entradas.Select(p => new PontoFinanceiroDto(p.Categoria, p.Fracao * 100, p.Valor)).ToArray()),
                new("categorias-despesas", "Despesas por categoria", "barra", "percentual", resultado.Saidas.Select(p => new PontoFinanceiroDto(p.Categoria, p.Fracao * 100, p.Valor)).ToArray())];
            if (secao == "tetos" && !resultado.OrcamentoNaoVerificado) return [
                new("orcamento", "Uso do teto por categoria", "barra", "percentual", resultado.Orcamentos.Select(p => new PontoFinanceiroDto(p.Categoria, p.Fracao * 100, $"{p.Gasto} de {p.Teto} · {p.Situacao}")).ToArray())];
        }
        if (vm is InadimplenciaViewModel inadimplencia && secao == "faixas") return [
            new("atrasos", "Atraso por faixa", "barra", "percentual", inadimplencia.Faixas.Select(p => new PontoFinanceiroDto(p.Rotulo, p.Fracao * 100, p.ValorRotulo)).ToArray())];
        return [];
    }

    private sealed record Campo(string Propriedade, string Rotulo, string Tipo = "texto", string? Opcoes = null,
        string? RotuloOpcao = null, string? ValorOpcao = null, object[]? Fixas = null, Permissao Permissao = Permissao.Nenhuma, string? Guarda = null);
    private sealed record Acao(string Comando, string Rotulo, Permissao Permissao = Permissao.Nenhuma, string? Guarda = null,
        string Estilo = "secundario", string? Selecao = null, bool SemParametro = false);
    private sealed record Coluna(string Propriedade, string Rotulo, string Tipo = "texto");
    private sealed record Tab(string Chave, string Titulo, string[] Origens, Func<object, IEnumerable<object>> Linhas, Coluna[] Colunas, Campo[] Campos, Acao[] Acoes,
        Func<object, object, string, string?>? Celula = null);
    private sealed record Secao(string Chave, string Titulo, string? Descricao, Campo[] Campos, string[] Indicadores, Tab[] Tabelas, Acao[] Acoes);
    private sealed record Pagina(string Chave, string Titulo, Type Tipo, Campo[] Campos, string[] Indicadores, Secao[] Secoes, Acao[] Acoes,
        string? Subtitulo = "Resumo", Permissao Permissao = Permissao.VerFinanceiro, string[]? FalhasExtras = null)
    { public string[] Falhas => ["NaoVerificado", .. FalhasExtras ?? []]; }
}
