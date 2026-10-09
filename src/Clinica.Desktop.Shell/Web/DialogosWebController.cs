using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Windows.Input;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Desktop.Shell.Web;

public sealed record DialogoWebDto(string Id, PaginaWebDto Pagina, bool Ocupado, bool PodeFechar);

/// <summary>
/// Apresenta os mesmos ViewModels em formulários web por uma lista explícita de campos e
/// comandos. IDs de modal, opção e linha pertencem à instância aberta; o navegador nunca
/// escolhe tipos .NET, caminhos arbitrários ou entidades fora das coleções carregadas.
/// </summary>
public sealed partial class DialogosWebController : IDialogosDaSessao, IDisposable
{
    private readonly List<Instancia> _pilha = [];
    private readonly Dictionary<(string,Type),RegistroDialogo> _registro;
    public DialogosWebController(IEnumerable<RegistroDialogo> registro) => _registro=registro.ToDictionary(r=>(r.Chave,r.Tipo));
    public sealed record RegistroDialogo(string Chave, Type Tipo, Definicao Definicao, Permissao Permissao = Permissao.Nenhuma, Func<object,Task>? AoAbrir = null, Func<bool>? Autorizado = null);
    private Definicao Definir(string chave,Type tipo) => tipo==typeof(Pergunta) ? new(chave,null,chave=="Pergunta"?[new Campo("Texto","Texto","Resposta","textarea")]:[],chave=="Aviso"?[]:[new Acao("confirmar","Continuar","","primario")],[]) :
        _registro.TryGetValue((chave,tipo),out var r)?r.Definicao:throw new InvalidOperationException("Formulário não registrado: "+chave);

    private readonly int _usuario = SessaoUsuario.Atual.UsuarioId;
    private readonly Permissao _permissoes = SessaoUsuario.Atual.Efetivas;
    private bool _descartado;
    public event Action? Mudou;
    public DialogoWebDto? EstadoAtual
    {
        get
        {
            if (_pilha.Count == 0 || _descartado) return null;
            ExigirSessao();
            ExigirPermissao(_pilha[^1].Tipo,_pilha[^1].Vm.GetType());
            return Montar(_pilha[^1]);
        }
    }

    public async Task<bool?> AbrirAsync(string tipo, object viewModel)
    {
        ExigirSessao();
        ExigirPermissao(tipo,viewModel.GetType());
        var definicao = Definir(tipo, viewModel.GetType());
        var errosContrato = ValidarDefinicao(tipo, viewModel.GetType(), definicao);
        if (errosContrato.Count > 0) throw new InvalidOperationException(string.Join("; ", errosContrato));
        var instancia = new Instancia(tipo, viewModel, definicao);
        _pilha.Add(instancia);
        Observar(instancia, viewModel);
        foreach (var campo in definicao.Campos.Where(c => c.Caminho.Contains('.')))
            if (Ler(viewModel, campo.Caminho.Split('.')[0]) is { } filho) Observar(instancia, filho);
        foreach (var campo in definicao.Campos.Where(c => c.Opcoes is not null))
            if (Ler(viewModel, campo.Opcoes!) is { } opcoes) Observar(instancia, opcoes);
        foreach (var tabela in definicao.Tabelas)
            if (Ler(viewModel, tabela.Colecao) is { } linhas) Observar(instancia, linhas);
        foreach (var evento in new[] { "Concluido", "Confirmado", "Salvou", "Concluiu", "Fechar", "Concluir", "Escolheu", "Vinculou", "Aplicou" }.Select(n => viewModel.GetType().GetEvent(n)).Where(e => e is not null))
        {
            Delegate? fechar = evento!.EventHandlerType == typeof(Action) ? new Action(() => Concluir(instancia, true))
                : evento.EventHandlerType == typeof(Action<bool>) ? new Action<bool>(resultado => Concluir(instancia, resultado)) : null;
            if (fechar is null) continue;
            evento.AddEventHandler(viewModel, fechar);
            instancia.Limpezas.Add(() => evento.RemoveEventHandler(viewModel, fechar));
        }
        foreach(var nome in new[]{"Fechar","Concluir"})
        {
            var propriedade=viewModel.GetType().GetProperty(nome);
            if(propriedade?.SetMethod?.IsPublic!=true)continue;
            var anterior=propriedade.GetValue(viewModel) as Delegate;
            Delegate? callback=propriedade.PropertyType==typeof(Action)?new Action(()=>{anterior?.DynamicInvoke();Concluir(instancia,true);}):
                propriedade.PropertyType==typeof(Action<bool>)?new Action<bool>(resultado=>{anterior?.DynamicInvoke(resultado);Concluir(instancia,resultado);}):null;
            if(callback is null)continue;propriedade.SetValue(viewModel,callback);
            instancia.Limpezas.Add(()=>{if(Equals(propriedade.GetValue(viewModel),callback))propriedade.SetValue(viewModel,anterior);});
        }
        Notificar();
        if (_registro.TryGetValue((tipo,viewModel.GetType()),out var registro) && registro.AoAbrir is not null) await registro.AoAbrir(viewModel);
        var resultado = await instancia.Resposta.Task;
        ExigirSessao();
        return resultado;
    }

    public async Task<string?> PerguntarTextoAsync(string titulo, string pergunta, string? textoInicial, bool obrigatorio)
    {
        var perguntaVm = new Pergunta { Titulo = titulo, Descricao = pergunta, Texto = textoInicial ?? string.Empty, Obrigatorio = obrigatorio };
        return await AbrirPerguntaAsync("Pergunta", perguntaVm) == true ? perguntaVm.Texto : null;
    }

    public async Task<bool> ConfirmarAsync(string titulo, string mensagem, bool perigoso) =>
        await AbrirPerguntaAsync("Confirmacao", new Pergunta { Titulo = titulo, Descricao = mensagem, Perigoso = perigoso }) == true;

    public async Task AvisoAsync(string titulo, string mensagem) =>
        await AbrirPerguntaAsync("Aviso", new Pergunta { Titulo = titulo, Descricao = mensagem });

    private Task<bool?> AbrirPerguntaAsync(string tipo, Pergunta vm) => AbrirAsync(tipo, vm);

    public Task AtualizarCampoAsync(string id, string campo, JsonElement valor, string? tabela = null, string? linhaId = null)
    {
        var instancia = Atual(id);
        if (instancia.Executando || Ocupado(instancia.Vm)) throw new InvalidOperationException("Aguarde a operação atual.");
        object alvo = instancia.Vm;
        Campo? definido;
        if (tabela is not null && linhaId is not null)
        {
            var linha = LinhasAtuais(instancia).SingleOrDefault(l => l.Id == linhaId && l.Tabela.Chave == tabela)
                ?? throw new InvalidOperationException("Registro não pertence ao formulário atual.");
            if (!Condicao(instancia.Vm, linha.Tabela.Habilitado)) throw new InvalidOperationException("Os registros estão somente para consulta.");
            definido = linha.Tabela.Campos?.SingleOrDefault(c => c.Chave == campo);
            alvo = linha.Objeto;
        }
        else if (tabela is not null || linhaId is not null) throw new InvalidOperationException("Informe a tabela e o registro do campo.");
        else definido = instancia.Definicao.Campos.SingleOrDefault(c => c.Chave == campo);
        if (definido is null) throw new InvalidOperationException("Campo não permitido neste formulário.");
        if (definido.Tipo is "leitura" or "imagem-leitura" or "texto-rico-leitura" || !Condicao(alvo, definido.Visivel) || !Condicao(alvo, definido.Habilitado))
            throw new InvalidOperationException("Este campo não pode ser alterado agora.");
        using var contexto = DialogosDaSessao.Usar(this);
        var (dono, propriedade) = Propriedade(alvo, definido.Caminho);
        var tipo = Nullable.GetUnderlyingType(propriedade.PropertyType) ?? propriedade.PropertyType;
        object? convertido;
        var texto = valor.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? string.Empty
            : valor.ValueKind == JsonValueKind.String ? valor.GetString() ?? string.Empty : valor.ToString();
        if (definido.Tipo != "texto-rico" && texto.Length > definido.Maximo) throw new InvalidOperationException($"{definido.Rotulo}: limite de {definido.Maximo} caracteres.");
        if (definido.Tipo == "texto-rico")
        { PaginasWebController.AtualizarTextoRico(alvo, definido.Caminho, definido.Formato, valor); Notificar(); return Task.CompletedTask; }
        if ((definido.Opcoes is not null && definido.Tipo != "sugestao") || tipo.IsEnum)
        {
            convertido = null;
            if (texto.Length > 0)
            {
                var opcoesAtuais = Opcoes(instancia, definido, alvo);
                var opcao = opcoesAtuais.FirstOrDefault(o => o.Id == texto)
                    ?? throw new InvalidOperationException("A opção não está mais disponível. Atualize a seleção.");
                convertido = definido.ValorOpcao is null ? opcao.Objeto : Ler(opcao.Objeto,definido.ValorOpcao);
            }
            if (convertido is null && propriedade.PropertyType.IsValueType && Nullable.GetUnderlyingType(propriedade.PropertyType) is null)
                throw new InvalidOperationException("Selecione uma opção.");
        }
        else if (texto.Length==0 && Nullable.GetUnderlyingType(propriedade.PropertyType) is not null) convertido=null;
        else if (tipo == typeof(string)) convertido = texto;
        else if (tipo == typeof(bool)) convertido = string.Equals(texto, "true", StringComparison.OrdinalIgnoreCase) ? true : string.Equals(texto, "false", StringComparison.OrdinalIgnoreCase) ? false : throw new InvalidOperationException("Valor booleano inválido.");
        else if (tipo == typeof(DateTime))
        {
            if (texto.Length == 0 && Nullable.GetUnderlyingType(propriedade.PropertyType) is not null) convertido = null;
            else if (DateTime.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)) convertido = data;
            else throw new InvalidOperationException("Data inválida.");
        }
        else if (tipo == typeof(int) && int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var inteiro)) convertido = inteiro;
        else if (tipo == typeof(TimeSpan)) convertido = TimeSpan.Parse(texto,CultureInfo.InvariantCulture);
        else if (tipo == typeof(decimal)) convertido = decimal.Parse(texto,CultureInfo.GetCultureInfo("pt-BR"));
        else if (tipo == typeof(double)) convertido = double.Parse(texto,CultureInfo.GetCultureInfo("pt-BR"));
        else throw new InvalidOperationException("Tipo de campo não permitido.");
        propriedade.SetValue(dono, convertido);
        instancia.Erro = null;
        Notificar();
        return Task.CompletedTask;
    }

    public async Task ExecutarAcaoAsync(string id, string acao, string? linhaId = null, string? tabela = null)
    {
        var instancia = Atual(id);
        if (instancia.Executando || Ocupado(instancia.Vm)) throw new InvalidOperationException("Aguarde a operação atual.");
        if (acao == "fechar") { Fechar(id); return; }
        if (instancia.Vm is Pergunta pergunta)
        {
            if (acao != "confirmar") throw new InvalidOperationException("Resposta não permitida.");
            if (pergunta.Obrigatorio && string.IsNullOrWhiteSpace(pergunta.Texto))
            { instancia.Erro = "Preencha o texto para continuar."; Notificar(); return; }
            Concluir(instancia, true);
            return;
        }
        object? parametro = null;
        Acao? definida;
        if (linhaId is null) definida = instancia.Definicao.Acoes.SingleOrDefault(a => a.Chave == acao);
        else
        {
            var linha = LinhasAtuais(instancia).SingleOrDefault(l => l.Id == linhaId && (tabela is null || l.Tabela.Chave == tabela))
                ?? throw new InvalidOperationException("Registro não pertence ao formulário atual.");
            parametro = linha.Objeto;
            definida = linha.Tabela.Acoes.SingleOrDefault(a => a.Chave == acao);
        }
        if (definida is null || !Condicao(parametro ?? instancia.Vm, definida.Visivel) || !Condicao(instancia.Vm, definida.Habilitado) || !Condicao(parametro ?? instancia.Vm, definida.HabilitadoLinha))
            throw new InvalidOperationException("Ação não permitida neste formulário.");
        var comando = Ler(instancia.Vm, definida.Comando) as ICommand ?? throw new InvalidOperationException("Comando indisponível.");
        if (!comando.CanExecute(parametro)) throw new InvalidOperationException("Ação indisponível agora.");
        instancia.Executando = true;
        instancia.Erro = null;
        Notificar();
        try
        {
            using var contexto = DialogosDaSessao.Usar(this);
            if (comando is IAsyncRelayCommand assincrono) await assincrono.ExecuteAsync(parametro);
            else comando.Execute(parametro);
        }
        catch (Exception ex) { if (!_descartado) instancia.Erro = ex.GetBaseException().Message; }
        finally { instancia.Executando = false; Notificar(); }
    }

    public void Fechar(string id)
    {
        var instancia = Atual(id);
        if (instancia.Executando || Ocupado(instancia.Vm) || Ler(instancia.Vm, "PodeFechar") is false) throw new InvalidOperationException("Conclua a operação antes de fechar.");
        Concluir(instancia, false);
    }

    private Instancia Atual(string id)
    {
        ExigirSessao();
        if (_pilha.Count == 0 || _pilha[^1].Id != id) throw new InvalidOperationException("Este formulário já foi fechado ou substituído.");
        ExigirPermissao(_pilha[^1].Tipo,_pilha[^1].Vm.GetType());
        return _pilha[^1];
    }

    private void ExigirPermissao(string chave,Type tipo)
    { if(tipo==typeof(Pergunta))return;
      if(!_registro.TryGetValue((chave,tipo),out var r))throw new InvalidOperationException("Formulário não registrado.");
      SessaoUsuario.Atual.Exigir(r.Permissao,"abrir este formulário");
      if(r.Autorizado is not null && !r.Autorizado())throw new UnauthorizedAccessException("Formulário não autorizado nesta sessão."); }

    private void ExigirSessao()
    {
        if (_descartado || !SessaoUsuario.Atual.Autenticado || SessaoUsuario.Atual.UsuarioId != _usuario || SessaoUsuario.Atual.Efetivas != _permissoes)
            throw new OperationCanceledException("A sessão ou as permissões mudaram. Abra novamente o formulário.");
    }

    private void Concluir(Instancia instancia, bool resultado)
    {
        if (_descartado || !_pilha.Contains(instancia)) return;
        // A conclusão do pai não encerra silenciosamente um filho ainda aberto.
        if (_pilha[^1] != instancia) throw new InvalidOperationException("Conclua o diálogo atual primeiro.");
        _pilha.RemoveAt(_pilha.Count - 1);
        instancia.Limpar();
        instancia.Resposta.TrySetResult(resultado);
        Notificar();
    }

    private void Observar(Instancia instancia, object objeto)
    {
        if (!instancia.Observados.Add(objeto)) return;
        if (objeto is INotifyPropertyChanged observavel)
        {
            PropertyChangedEventHandler handler = (_, _) => Notificar();
            observavel.PropertyChanged += handler;
            instancia.Limpezas.Add(() => observavel.PropertyChanged -= handler);
        }
        if (objeto is INotifyCollectionChanged lista)
        {
            NotifyCollectionChangedEventHandler handler = (_, _) => Notificar();
            lista.CollectionChanged += handler;
            instancia.Limpezas.Add(() => lista.CollectionChanged -= handler);
        }
    }

    private void Notificar() { if (!_descartado) Mudou?.Invoke(); }
    public void Dispose()
    {
        if (_descartado) return;
        _descartado = true;
        foreach (var instancia in _pilha) { instancia.Limpar(); instancia.Resposta.TrySetCanceled(); }
        _pilha.Clear();
    }

    private DialogoWebDto Montar(Instancia i)
    {
        var ocupado = i.Executando || Ocupado(i.Vm);
        var campos = i.Definicao.Campos.Select(c =>
        {
            var valor = Ler(i.Vm, c.Caminho);
            var busca = c.Chave is "Seletor.Selecionado" or "Escolhido" ? Ler(i.Vm, "Seletor") as SeletorPacienteViewModel : null;
            var opcoes = Opcoes(i, c);
            object? valorWeb = c.Tipo is "texto-rico" or "texto-rico-leitura" ? new {texto=valor,formato=c.Formato is null?null:Ler(i.Vm,c.Formato)} : valor switch { DateTime data => data.ToString("yyyy-MM-dd"), bool b => b, null => null, _ => Formatar(valor) };
            if (c.Tipo == "senha") valorWeb = null;
            if ((c.Opcoes is not null && c.Tipo != "sugestao") || valor is Enum) valorWeb = valor is null ? "" : opcoes.FirstOrDefault(o => Equals(c.ValorOpcao is null?o.Objeto:Ler(o.Objeto,c.ValorOpcao), valor))?.Id ?? "";
            return new CampoWebDto(c.Chave, c.Rotulo, c.Tipo, valorWeb,
                opcoes.Select(o => new OpcaoWebDto(o.Id, o.Rotulo)).ToArray(), Condicao(i.Vm, c.Visivel),
                !ocupado && c.Tipo is not ("leitura" or "imagem-leitura" or "texto-rico-leitura") && Condicao(i.Vm, c.Habilitado), c.Obrigatorio || i.Vm is Pergunta { Obrigatorio: true }, c.Ajuda,c.Maximo, busca is null ? null : new(busca.Termo ?? "", busca.Buscando, busca.Erro, (valor as Paciente)?.Nome));
        }).ToArray();
        var tabelas = i.Definicao.Tabelas.Select(t => new TabelaWebDto(t.Chave, t.Titulo,
            t.Colunas.Select(c => new ColunaWebDto(c.Caminho, c.Rotulo)).ToArray(),
            LinhasAtuais(i).Where(l => l.Tabela == t).Select(l => new LinhaWebDto(l.Id,
                t.Colunas.ToDictionary(c => c.Caminho, c => Formatar(Ler(l.Objeto, c.Caminho))),
                (t.Campos ?? []).Select(c => CampoDaLinha(i,t,c,l.Objeto,ocupado)).ToArray(),
                t.Acoes.Select(a => AcaoDto(i, a, ocupado, l.Objeto)).ToArray())).ToArray())).ToArray();
        var acoes = i.Definicao.Acoes.Select(a => AcaoDto(i, a, ocupado)).Append(new AcaoWebDto("fechar",
            i.Definicao.Acoes.Any(a => a.Chave is "salvar" or "confirmar") ? "Cancelar" : "Fechar", !ocupado && Ler(i.Vm,"PodeFechar") is not false)).ToArray();
        var mensagem = i.Erro ?? Ler(i.Vm, "Mensagem")?.ToString() ?? Ler(i.Vm, "Erro")?.ToString();
        var titulo = i.Vm is Pergunta p ? p.Titulo : Ler(i.Vm, "Titulo")?.ToString() ?? i.Definicao.Titulo;
        var descricao = i.Vm is Pergunta pergunta ? pergunta.Descricao : Ler(i.Vm,"Descricao")?.ToString() ?? i.Definicao.Descricao;
        var pagina = new PaginaWebDto(i.Tipo, titulo, descricao, campos, [],
            tabelas.Length == 0 ? [] : [new SecaoWebDto("registros", "", null, [], [], tabelas, [])],
            acoes, ocupado, Ler(i.Vm, "NaoVerificado") is true, mensagem,
            i.Erro is not null || !string.IsNullOrEmpty(Ler(i.Vm, "Erro")?.ToString()) || Ler(i.Vm, "MensagemEhErro") is true || (i.Tipo == "Recebimento" && !string.IsNullOrEmpty(mensagem)), false);
        return new DialogoWebDto(i.Id, pagina, ocupado, !ocupado && Ler(i.Vm,"PodeFechar") is not false);
    }

    private static AcaoWebDto AcaoDto(Instancia i, Acao a, bool ocupado, object? linha = null) =>
        new(a.Chave, a.Rotulo, !ocupado && Condicao(i.Vm, a.Habilitado) && Condicao(linha ?? i.Vm, a.HabilitadoLinha)
            && (i.Vm is Pergunta || (Ler(i.Vm, a.Comando) as ICommand)?.CanExecute(linha) == true), i.Vm is Pergunta { Perigoso: true } ? "perigo" : a.Estilo, Condicao(linha??i.Vm,a.Visivel));

    private static CampoWebDto CampoDaLinha(Instancia i,Tabela tabela,Campo c,object alvo,bool ocupado)
    {
        var valor=Ler(alvo,c.Caminho);var opcoes=Opcoes(i,c,alvo);
        object? exibido=valor is DateTime d?d.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture):valor;
        if(c.Tipo is "texto-rico" or "texto-rico-leitura")exibido=new{texto=valor,formato=c.Formato is null?null:Ler(alvo,c.Formato)};
        else if(c.Tipo=="senha")exibido=null;
        else if((c.Opcoes is not null&&c.Tipo!="sugestao")||valor is Enum)exibido=opcoes.FirstOrDefault(o=>Equals(c.ValorOpcao is null?o.Objeto:Ler(o.Objeto,c.ValorOpcao),valor))?.Id??"";
        return new(c.Chave,c.Rotulo,c.Tipo,exibido,opcoes.Select(o=>new OpcaoWebDto(o.Id,o.Rotulo)).ToArray(),Condicao(alvo,c.Visivel),!ocupado&&c.Tipo is not ("leitura" or "imagem-leitura" or "texto-rico-leitura")&&Condicao(i.Vm,tabela.Habilitado)&&Condicao(alvo,c.Habilitado),c.Obrigatorio,c.Ajuda,c.Maximo);
    }

    private static bool Ocupado(object vm) => new[] { "Salvando", "Ocupado", "Carregando" }.Any(p => Ler(vm, p) is true);
    private static bool Condicao(object vm, string? caminho) => caminho is null || caminho.Split('&').All(parte =>
        parte.StartsWith('!') ? Ler(vm, parte[1..]) is false : Ler(vm, parte) is true);
    private static object? Ler(object vm, string caminho)
    {
        if(caminho==".")return vm;
        object? atual = vm;
        foreach (var parte in caminho.Split('.')) atual = atual?.GetType().GetProperty(parte)?.GetValue(atual);
        return atual;
    }
    private static (object Dono, PropertyInfo Propriedade) Propriedade(object vm, string caminho)
    {
        var partes = caminho.Split('.');
        foreach (var parte in partes.SkipLast(1)) vm = Ler(vm, parte) ?? throw new InvalidOperationException("Campo sem contexto.");
        var propriedade = vm.GetType().GetProperty(partes[^1]);
        if (propriedade?.SetMethod is null || !propriedade.SetMethod.IsPublic) throw new InvalidOperationException("Campo somente de leitura.");
        return (vm, propriedade);
    }
    private static string Formatar(object? valor) => valor switch
    {
        null => "", Enum e => RotulosEnum.De(e), DateTime d => d.ToString("dd/MM/yyyy"), DateOnly d => d.ToString("dd/MM/yyyy"),
        bool b => b ? "Sim" : "Não", _ => valor.ToString() ?? ""
    };
    private static List<Opcao> Opcoes(Instancia i, Campo campo, object? alvo = null)
    {
        IEnumerable? lista=campo.Opcoes is null ? null : Ler(campo.Opcoes.StartsWith("vm:")?i.Vm:alvo??i.Vm,campo.Opcoes.Replace("vm:","")) as IEnumerable;
        if(lista is null && campo.Tipo is "enum" or "selecao")
        {
            var (_,prop)=Propriedade(alvo??i.Vm,campo.Caminho);var tipo=Nullable.GetUnderlyingType(prop.PropertyType)??prop.PropertyType;
            if(tipo.IsEnum)lista=Enum.GetValues(tipo);
        }
        if(lista is null)return [];
        return lista.Cast<object?>().Where(o=>o is not null).Select(o => new Opcao(i.Token(o!), o!,
            o is Enum ? Formatar(o) : Ler(o!, campo.RotuloOpcao)?.ToString() ?? Ler(o!, "Nome")?.ToString() ?? Formatar(o))).ToList();
    }
    private static IEnumerable<Linha> LinhasAtuais(Instancia i) => i.Definicao.Tabelas.SelectMany(t =>
        (Ler(i.Vm, t.Colecao) as IEnumerable)?.Cast<object>().Select(o => new Linha(t.Chave + ":" + i.Token(o), o, t)) ?? []);

    private sealed class Instancia(string tipo, object vm, Definicao definicao)
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string Tipo { get; } = tipo;
        public object Vm { get; } = vm;
        public Definicao Definicao { get; } = definicao;
        public TaskCompletionSource<bool?> Resposta { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<Action> Limpezas { get; } = [];
        public HashSet<object> Observados { get; } = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<object, string> _tokens = [];
        public string Token(object objeto) { if (!_tokens.TryGetValue(objeto, out var token)) _tokens[objeto] = token = Guid.NewGuid().ToString("N"); return token; }
        public bool Executando { get; set; }
        public string? Erro { get; set; }
        public void Limpar() { foreach (var limpar in Limpezas) limpar(); Limpezas.Clear(); if (Vm is TrocaSenhaWebViewModel senha) senha.Dispose(); }
    }
    private sealed class Pergunta
    {
        public string Titulo { get; init; } = "";
        public string Descricao { get; init; } = "";
        public string Texto { get; set; } = "";
        public bool Obrigatorio { get; init; }
        public bool Perigoso { get; init; }
    }
    public sealed record Campo(string Chave, string Caminho, string Rotulo, string Tipo = "texto", string? Opcoes = null,
        string RotuloOpcao = "Rotulo", string? Visivel = null, string? Habilitado = null, bool Obrigatorio = false, int Maximo = 4000, string? Ajuda = null, string? Formato = null, string? ValorOpcao = null);
    public sealed record Acao(string Chave, string Rotulo, string Comando, string Estilo = "secundario", string? Habilitado = null, string? HabilitadoLinha = null, string? Visivel = null);
    public sealed record Coluna(string Caminho, string Rotulo);
    public sealed record Tabela(string Chave, string Titulo, string Colecao, Coluna[] Colunas, Acao[] Acoes, Campo[]? Campos = null, string? Habilitado = null);
    public sealed record Definicao(string Titulo, string? Descricao, Campo[] Campos, Acao[] Acoes, Tabela[] Tabelas);
    private sealed record Opcao(string Id, object Objeto, string Rotulo);
    private sealed record Linha(string Id, object Objeto, Tabela Tabela);
}
