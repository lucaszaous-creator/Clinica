using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;
using Clinica.Domain;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Web;

/// <summary>Adaptador de apresentação. As propriedades refletidas são constantes deste registro,
/// nunca caminhos recebidos do JavaScript. IDs opacos só resolvem objetos ainda presentes na coleção.</summary>
public sealed partial class PaginasWebController : IDisposable
{
    private readonly IServiceProvider _services;
    private readonly int _usuario = SessaoUsuario.Atual.UsuarioId;
    private readonly Permissao _permissoes = SessaoUsuario.Atual.Efetivas;
    private readonly Dictionary<string, Pagina> _paginas;
    private readonly Dictionary<object, string> _ids = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, string> _opcaoIds = new(ReferenceEqualityComparer.Instance);
    private readonly List<Action> _desinscrever = [];
    private Pagina? _pagina;
    private object? _vm;
    private bool _ocupado, _disposed;
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    public string Contexto { get; private set; } = Guid.NewGuid().ToString("N");
    public event Action? Changed;

    public PaginasWebController(IServiceProvider services, IEnumerable<Pagina> paginas)
    { _services = services; _paginas = paginas.ToDictionary(p => p.Chave, StringComparer.Ordinal); }
    public object? ViewModelAtual => _vm;
    public IReadOnlyCollection<string> Chaves => _paginas.Keys;
    public string? ChaveAtual => _pagina?.Chave;

    /// <summary>Verifica os contratos estáticos sem criar VMs nem consultar dados.</summary>
    public static IReadOnlyList<string> ValidarRegistro(IEnumerable<Pagina> paginas)
    {
        var erros = new List<string>();
        foreach (var p in paginas)
        {
            void Propriedade(Type tipo, string? nome)
            {
                nome = nome?.Replace("presente:", "");
                if (nome is not null && PropriedadeTipo(tipo, nome) is null) erros.Add($"{p.Chave}: {tipo.Name}.{nome} ausente");
            }
            void CampoValido(Type tipo, Campo c)
            {
                if(c.Propriedade != ".") Propriedade(tipo, c.Propriedade);
                if(c.Opcoes is not null) Propriedade(c.Opcoes.StartsWith("vm:") ? p.Tipo : tipo, c.Opcoes.Replace("vm:",""));
                if (c.Tipo is not ("leitura" or "imagem-leitura" or "texto-rico-leitura") && PropriedadeTipo(tipo, c.Propriedade)?.SetMethod is null) erros.Add($"{p.Chave}: {tipo.Name}.{c.Propriedade} não editável");
                foreach(var expressao in new[]{c.Guarda,c.Visivel}.OfType<string>()) foreach(var parte in expressao.Split('&')) Propriedade(parte.StartsWith("vm:") ? p.Tipo : tipo, parte.Replace("vm:", "").TrimStart('!'));
            }
            void AcaoValida(Type tipo, Acao a)
            {
                Propriedade(a.AlvoLinha?tipo:p.Tipo, a.Comando + "Command"); Propriedade(p.Tipo, a.Selecao);
                if (a.Guarda is not null) foreach(var parte in a.Guarda.Split('&')) Propriedade(parte.StartsWith("vm:") ? p.Tipo : tipo, parte.Replace("vm:", "").TrimStart('!'));
            }
            foreach (var c in p.Campos.Concat(p.Secoes.SelectMany(s => s.Campos))) CampoValido(p.Tipo, c);
            foreach (var a in p.Acoes.Concat(p.Secoes.SelectMany(s => s.Acoes))) AcaoValida(p.Tipo, a);
            foreach (var indicador in p.Indicadores.Concat(p.Secoes.SelectMany(s => s.Indicadores))) Propriedade(p.Tipo, indicador.Split('|')[0]);
            foreach (var secao in p.Secoes)
            {
                Propriedade(p.Tipo, secao.Descricao);
                foreach (var tab in secao.Tabelas)
                {
                    var origem = PropriedadeTipo(p.Tipo, tab.Origens[0]);
                    if (origem is null) { Propriedade(p.Tipo, tab.Origens[0]); continue; }
                    var tipo = tab.TipoLinha ?? origem.PropertyType.GetInterfaces().Append(origem.PropertyType).FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))?.GetGenericArguments()[0];
                    if (tipo is null) { erros.Add($"{p.Chave}: tipo da tabela {tab.Chave} desconhecido"); continue; }
                    foreach (var c in tab.Colunas)
                        if (tab.Celula is null && c.Propriedade != ".") Propriedade(tipo, c.Propriedade);
                    foreach (var c in tab.Campos) CampoValido(tipo, c);
                    foreach (var a in tab.Acoes) AcaoValida(tipo, a);
                }
            }
        }
        return erros;
    }

    public async Task NavegarAsync(string chave)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_ocupado) throw new InvalidOperationException("Conclua a operação atual antes de navegar.");
        if (!_paginas.TryGetValue(chave, out var pagina)) throw new InvalidOperationException("Página inexistente.");
        Exigir(pagina);
        _pagina?.AoFechar?.Invoke(_vm!);
        Desinscrever();
        _ids.Clear(); _opcaoIds.Clear();
        Contexto = Guid.NewGuid().ToString("N");
        _pagina = pagina;
        _vm = pagina.Fabrica?.Invoke(_services) ?? _services.GetRequiredService(pagina.Tipo);
        if (!pagina.Tipo.IsInstanceOfType(_vm)) throw new InvalidOperationException("Modelo incompatível com a página.");
        if (pagina.AoAbrir is not null) await pagina.AoAbrir(_vm);
        else if (_vm is ICarregarAoAbrir carregavel) await carregavel.CarregarAsync();
        Assinar();
        Changed?.Invoke();
        // Sem carga explícita/contrato, o construtor é responsável por iniciar a consulta.
        return;
    }

    public PaginaWebDto ObterPagina()
    {
        var (p, vm) = Atual();
        var vivos = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var secoes = p.Secoes.Where(s => Condicao(vm, s.Visivel)).Select(s => new SecaoWebDto(s.Chave, s.Titulo,
            TextoOpcional(vm, s.Descricao), s.Campos.Select(c => CriarCampo(vm, c)).ToArray(),
            Indicadores(vm, s.Indicadores), s.Tabelas.Where(t => Condicao(vm, t.Visivel)).Select(t => Tabela(vm, t, vivos)).ToArray(),
            s.Acoes.Select(a => CriarAcao(vm, a, null)).ToArray(), p.Graficos?.Invoke(vm, s.Chave) ?? [])).ToArray();
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
        if ((campo.Tipo is "leitura" or "imagem-leitura" or "texto-rico-leitura") || !Condicao(alvo, campo.Visivel)) throw new InvalidOperationException("Campo somente de leitura ou oculto.");
        var (dono, prop) = Propriedade(alvo, campo.Propriedade);
        object? convertido;
        if (campo.Tipo == "texto-rico")
        {
            AtualizarTextoRico(alvo, campo.Propriedade, campo.Formato, valor); Changed?.Invoke(); return Task.CompletedTask;
        }
        if (campo.Tipo == "selecao")
        {
            var token = valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;
            var opcoes = Opcoes(alvo, campo).ToArray();
            var opcao = opcoes.SingleOrDefault(o => TokenOpcao(o.Valor) == token);
            if (opcao is null && (token!="" || prop.PropertyType.IsValueType&&Nullable.GetUnderlyingType(prop.PropertyType) is null)) throw new InvalidOperationException("Opção não pertence à lista atual.");
            convertido = opcao?.Valor;
        }
        else convertido = Converter(valor, prop.PropertyType, campo.Tipo);
        prop.SetValue(dono, convertido);
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
        var acao = acoes.SingleOrDefault(a => ChaveAcao(a) == chave) ?? throw new InvalidOperationException("Ação inválida.");
        if (!Condicao(parametro ?? vm, acao.Visivel) || !Permitido(acao.Permissao, acao.Guarda, parametro ?? vm, vm)) throw new UnauthorizedAccessException("Ação indisponível para esta sessão.");
        var comando = Ler(acao.AlvoLinha?parametro??throw new InvalidOperationException("Escolha um registro."):vm, acao.Comando + "Command") as ICommand ?? throw new InvalidOperationException("Comando não configurado.");
        if (acao.Selecao is not null) Definir(vm, acao.Selecao, parametro);
        if (acao.SemParametro || acao.AlvoLinha) parametro = null;
        if (acao.Parametro is not null) parametro = acao.Parametro;
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
    private void Exigir(Pagina p)
    {
        var sessao = SessaoUsuario.Atual;
        if (!sessao.Autenticado || sessao.UsuarioId != _usuario || sessao.Efetivas != _permissoes || !sessao.Pode(p.Permissao) || (p.Autorizado is not null && !p.Autorizado()))
            throw new UnauthorizedAccessException("Sem acesso a esta página.");
    }
    private static bool Permitido(Permissao permissao, string? guarda, object alvo, object vm) =>
        SessaoUsuario.Atual.Autenticado && SessaoUsuario.Atual.Pode(permissao) &&
        (guarda is null || (guarda.StartsWith("vm:", StringComparison.Ordinal) ? Condicao(vm, guarda[3..]) : Condicao(alvo, guarda)));
    private static Tab EncontrarTabela(Pagina p, string chave) => p.Secoes.SelectMany(s => s.Tabelas)
        .SingleOrDefault(t => t.Chave == chave) ?? throw new InvalidOperationException("Tabela inválida.");
    private object ResolverLinha(object vm, Tab tabela, string? id) => tabela.Linhas(vm)
        .SingleOrDefault(row => _ids.TryGetValue(row, out var token) && token == id)
        ?? throw new InvalidOperationException("O registro não está mais nesta lista. Atualize a página.");
    private TabelaWebDto Tabela(object vm, Tab t, HashSet<object> vivos)
    {
        var linhas = t.Linhas(vm).Select(row =>
        {
            vivos.Add(row);
            if (!_ids.TryGetValue(row, out var id)) _ids[row] = id = Guid.NewGuid().ToString("N");
            return new LinhaWebDto(id, t.Colunas.ToDictionary(c => c.Propriedade, c => t.Celula?.Invoke(vm, row, c.Propriedade) ?? Formatar(Ler(row, c.Propriedade), c.Tipo)),
                t.Campos.Select(c => CriarCampo(row, c)).ToArray(), t.Acoes.Select(a => CriarAcao(vm, a, row)).ToArray());
        }).ToArray();
        var vazio = B(vm, "Carregando") ? "Consultando os registros…"
            : B(vm, "NaoVerificado") ? "A consulta não pôde ser verificada. Atualize para tentar novamente."
            : B(vm, "FiltroAtivo") ? "Nenhum registro corresponde aos filtros. Limpe os filtros para ver os demais."
            : "Nenhum registro neste período.";
        return new(t.Chave, t.Titulo, t.Colunas.Select(c => new ColunaWebDto(c.Propriedade, c.Rotulo, c.Tipo)).ToArray(), linhas, vazio);
    }
    private CampoWebDto CriarCampo(object alvo, Campo c)
    {
        var valor = Ler(alvo, c.Propriedade);
        var busca = c.Propriedade is "Seletor.Selecionado" or "Escolhido" ? Ler(alvo, "Seletor") as SeletorPacienteViewModel : null;
        object? exibido = c.Tipo is "texto-rico" or "texto-rico-leitura" ? new {texto=valor,formato=c.Formato is null?null:Ler(alvo,c.Formato)} : c.Tipo == "selecao" ? TokenOpcao(valor) : valor is DateTime dt ? dt.ToString(c.Tipo == "mes" ? "yyyy-MM" : "yyyy-MM-dd", CultureInfo.InvariantCulture) : valor;
        if(c.Tipo=="senha")exibido=null;
        if(c.Tipo=="leitura")exibido=valor is null?"":Formatar(valor);
        return new(c.Propriedade, c.Rotulo, c.Tipo, exibido, Opcoes(alvo, c).Select(o => new OpcaoWebDto(TokenOpcao(o.Valor), o.Rotulo)).ToArray(),
            Visivel: Condicao(alvo, c.Visivel) && (c.Tipo != "imagem-leitura" || !string.IsNullOrWhiteSpace(valor as string)), Habilitado: c.Tipo is not ("leitura" or "imagem-leitura" or "texto-rico-leitura") && !_ocupado && !B(_vm!, "Carregando") && Permitido(c.Permissao, c.Guarda, alvo, _vm!), BuscaPaciente: busca is null ? null : new(busca.Termo ?? "", busca.Buscando, busca.Erro, (valor as Paciente)?.Nome));
    }
    private AcaoWebDto CriarAcao(object vm, Acao a, object? row)
    {
        var cmd = Ler(a.AlvoLinha?row??vm:vm, a.Comando + "Command") as ICommand;
        return new(ChaveAcao(a), a.Rotulo, !_ocupado && !B(vm, "Carregando") && Permitido(a.Permissao, a.Guarda, row ?? vm, vm) && cmd?.CanExecute(a.Parametro ?? (a.SemParametro || a.AlvoLinha ? null : row)) == true, a.Estilo, Condicao(row ?? vm,a.Visivel));
    }
    private static string ChaveAcao(Acao a)=>a.Chave??(a.Parametro is null?a.Comando:a.Comando+":"+Convert.ToString(a.Parametro,CultureInfo.InvariantCulture));
    private static IndicadorWebDto[] Indicadores(object vm, string[] propriedades) => propriedades.Select(p =>
    { var partes = p.Split('|'); return new IndicadorWebDto(partes.Length > 1 ? partes[1] : partes[0], Formatar(Ler(vm, partes[0])),partes.Length>2?TextoOpcional(vm,partes[2]):null); }).ToArray();
    private sealed record Opcao(object? Valor, string Rotulo);
    private IEnumerable<Opcao> Opcoes(object alvo, Campo c)
    {
        if (c.Fixas is not null) return c.Fixas.Select(v => new Opcao(v, Formatar(v)));
        if (c.Opcoes is null)
        {
            var tipo=PropriedadeTipo(alvo.GetType(),c.Propriedade)?.PropertyType;
            tipo=tipo is null?null:Nullable.GetUnderlyingType(tipo)??tipo;
            return c.Tipo=="selecao"&&tipo?.IsEnum==true?Enum.GetValues(tipo).Cast<object>().Select(v=>new Opcao(v,Formatar(v))):[];
        }
        return Lista(c.Opcoes.StartsWith("vm:") ? _vm! : alvo, c.Opcoes.Replace("vm:", "")).Select(o => new Opcao(c.ValorOpcao is null || o is null ? o : Ler(o, c.ValorOpcao), c.RotuloOpcao is null || o is null ? Formatar(o) : Formatar(Ler(o, c.RotuloOpcao))));
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
        if(texto.Length==0&&Nullable.GetUnderlyingType(tipo) is not null)return null;
        if (texto.Length > 5000) throw new InvalidOperationException("Texto muito longo.");
        if (baseTipo == typeof(string)) return texto;
        if (baseTipo == typeof(bool)) return valor.ValueKind is JsonValueKind.True or JsonValueKind.False ? valor.GetBoolean() : bool.Parse(texto);
        if (baseTipo == typeof(DateTime))
        {
            if (texto.Length == 0 && Nullable.GetUnderlyingType(tipo) is not null) return null;
            return DateTime.ParseExact(texto, formato == "mes" ? "yyyy-MM" : "yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        if (baseTipo == typeof(int)) return int.Parse(texto, CultureInfo.InvariantCulture);
        if (baseTipo == typeof(decimal)) return decimal.Parse(texto, CultureInfo.GetCultureInfo("pt-BR"));
        if (baseTipo == typeof(double)) return double.Parse(texto, CultureInfo.GetCultureInfo("pt-BR"));
        if (baseTipo == typeof(TimeSpan)) return TimeSpan.Parse(texto, CultureInfo.InvariantCulture);
        throw new InvalidOperationException("Tipo de campo não permitido.");
    }
    public static object? Ler(object o, string p)
    { if(p==".")return o; object? atual = o; foreach (var parte in p.Split('.')) atual = atual?.GetType().GetProperty(parte)?.GetValue(atual); return atual; }
    private static PropertyInfo? PropriedadeTipo(Type tipo, string caminho)
    { PropertyInfo? info = null; foreach (var parte in caminho.Split('.')) { info = tipo.GetProperty(parte); if (info is null) return null; tipo = info.PropertyType;
        // Interface compartilhada: o contrato concreto é registrado pela composição do aplicativo.
        if(TiposDeContrato.TryGetValue((info.DeclaringType!,info.Name),out var concreto))tipo=concreto;
      } return info; }
    private static readonly Dictionary<(Type,string),Type> TiposDeContrato = [];
    public static void RegistrarTipoDeContrato<T>(string propriedade,Type concreto) => TiposDeContrato[(typeof(T),propriedade)]=concreto;
    private static (object, PropertyInfo) Propriedade(object o, string caminho)
    { var partes=caminho.Split('.'); foreach(var parte in partes.SkipLast(1)) o=Ler(o,parte) ?? throw new InvalidOperationException("Contexto do campo indisponível.");
      var p=o.GetType().GetProperty(partes[^1]); if(p?.SetMethod?.IsPublic!=true) throw new InvalidOperationException("Campo somente de leitura."); return (o,p); }
    private static void Definir(object o,string p,object? v) { var(dono,prop)=Propriedade(o,p);prop.SetValue(dono,v); }
    private static bool Condicao(object o,string? guarda) => guarda is null || guarda.Split('&').All(p=>p.StartsWith("presente:")?Ler(o,p[9..]) is {} v&&(v is not string t||!string.IsNullOrWhiteSpace(t)):p.StartsWith('!')?Ler(o,p[1..]) is false:Ler(o,p) is true);
    internal static void AtualizarTextoRico(object vm,string texto,string? formato,JsonElement valor)
    { if(formato is null || valor.ValueKind!=JsonValueKind.Object || !valor.TryGetProperty("texto",out var t) || t.ValueKind!=JsonValueKind.String || !valor.TryGetProperty("formato",out var f) || f.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)) throw new InvalidOperationException("Conteúdo do editor inválido.");
      var conteudo=t.GetString()!;var metadados=f.ValueKind==JsonValueKind.Null?null:f.GetString();
      if(conteudo.Length>200000 || metadados?.Length>250000) throw new InvalidOperationException("Conteúdo do editor muito longo.");
      var (donoT,propT)=Propriedade(vm,texto);var(donoF,propF)=Propriedade(vm,formato);
      if(propT.PropertyType!=typeof(string)||propF.PropertyType!=typeof(string)) throw new InvalidOperationException("Contrato do editor inválido.");
      propT.SetValue(donoT,conteudo);propF.SetValue(donoF,metadados); }

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
                PropertyChangedEventHandler handler = (_, _) => { if (!_disposed) { Assinar(); Changed?.Invoke(); } };
                notify.PropertyChanged += handler; _desinscrever.Add(() => notify.PropertyChanged -= handler);
            }
        }
        Item(_vm);
        var caminhos=_pagina.Campos.Concat(_pagina.Secoes.SelectMany(s=>s.Campos)).SelectMany(c=>new[]{c.Propriedade,c.Opcoes,c.Formato}).OfType<string>()
            .Concat(_pagina.Acoes.Concat(_pagina.Secoes.SelectMany(s=>s.Acoes)).Select(a=>a.Comando))
            .Concat(_pagina.Secoes.SelectMany(s=>s.Tabelas).SelectMany(t=>t.Origens));
        foreach(var caminho in caminhos)
        {
            var partes=caminho.Replace("vm:","").Split('.');
            for(var n=1;n<=partes.Length;n++)
            {
                if(Ler(_vm,string.Join('.',partes.Take(n))) is not {} obj)continue;
                Item(obj);
                if(obj is INotifyCollectionChanged lista)
                {NotifyCollectionChangedEventHandler handler=(_,_)=>{if(!_disposed)Changed?.Invoke();};lista.CollectionChanged+=handler;_desinscrever.Add(()=>lista.CollectionChanged-=handler);}
            }
        }
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
    public void Dispose() { if (_disposed) return; _disposed = true; if (_vm is not null) _pagina?.AoFechar?.Invoke(_vm); Desinscrever(); _ids.Clear(); _opcaoIds.Clear(); Changed = null; }

    public sealed record Campo(string Propriedade, string Rotulo, string Tipo = "texto", string? Opcoes = null,
        string? RotuloOpcao = null, string? ValorOpcao = null, object[]? Fixas = null, Permissao Permissao = Permissao.Nenhuma, string? Guarda = null, string? Visivel = null, string? Formato = null);
    public sealed record Acao(string Comando, string Rotulo, Permissao Permissao = Permissao.Nenhuma, string? Guarda = null,
        string Estilo = "secundario", string? Selecao = null, bool SemParametro = false, string? Visivel = null, object? Parametro = null, bool AlvoLinha = false, string? Chave = null);
    public sealed record Coluna(string Propriedade, string Rotulo, string Tipo = "texto");
    public sealed record Tab(string Chave, string Titulo, string[] Origens, Func<object, IEnumerable<object>> Linhas, Coluna[] Colunas, Campo[] Campos, Acao[] Acoes,
        Func<object, object, string, string?>? Celula = null, string? Visivel = null, Type? TipoLinha = null);
    public sealed record Secao(string Chave, string Titulo, string? Descricao, Campo[] Campos, string[] Indicadores, Tab[] Tabelas, Acao[] Acoes, string? Visivel = null);
    public sealed record Pagina(string Chave, string Titulo, Type Tipo, Campo[] Campos, string[] Indicadores, Secao[] Secoes, Acao[] Acoes,
        string? Subtitulo = "Resumo", Permissao Permissao = Permissao.Nenhuma, string[]? FalhasExtras = null, Func<IServiceProvider,object>? Fabrica = null, Func<object,Task>? AoAbrir = null, Func<object,string,IReadOnlyList<GraficoWebDto>>? Graficos = null, Func<bool>? Autorizado = null, Action<object>? AoFechar = null)
    { public string[] Falhas => ["NaoVerificado", .. FalhasExtras ?? []]; }
}
