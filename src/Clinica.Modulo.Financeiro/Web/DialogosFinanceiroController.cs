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

namespace Clinica.Financeiro.Web;

public sealed record DialogoFinanceiroDto(string Id, PaginaFinanceiroDto Pagina, bool Ocupado, bool PodeFechar);

/// <summary>
/// Apresenta os mesmos ViewModels em formulários web por uma lista explícita de campos e
/// comandos. IDs de modal, opção e linha pertencem à instância aberta; o navegador nunca
/// escolhe tipos .NET, caminhos arbitrários ou entidades fora das coleções carregadas.
/// </summary>
public sealed partial class DialogosFinanceiroController : IDialogosDaSessao, IDisposable
{
    private readonly List<Instancia> _pilha = [];
    private readonly int _usuario = SessaoUsuario.Atual.UsuarioId;
    private readonly Permissao _permissoes = SessaoUsuario.Atual.Efetivas;
    private bool _descartado;
    public event Action? Mudou;
    public DialogoFinanceiroDto? EstadoAtual
    {
        get
        {
            if (_pilha.Count == 0 || _descartado) return null;
            ExigirSessao();
            ExigirPermissao(_pilha[^1].Tipo);
            return Montar(_pilha[^1]);
        }
    }

    public async Task<bool?> AbrirAsync(string tipo, object viewModel)
    {
        ExigirSessao();
        ExigirPermissao(tipo);
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
        if ((viewModel.GetType().GetEvent("Concluido") ?? viewModel.GetType().GetEvent("Confirmado")) is { EventHandlerType: { } tipoEvento } evento && tipoEvento == typeof(Action))
        {
            Action fechar = () => Concluir(instancia, true);
            evento.AddEventHandler(viewModel, fechar);
            instancia.Limpezas.Add(() => evento.RemoveEventHandler(viewModel, fechar));
        }
        Notificar();
        if (viewModel is ConsumosPacoteViewModel consumos) await consumos.CarregarAsync();
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
        if (definido.Tipo == "leitura" || !Condicao(alvo, definido.Visivel) || !Condicao(alvo, definido.Habilitado))
            throw new InvalidOperationException("Este campo não pode ser alterado agora.");
        using var contexto = DialogosDaSessao.Usar(this);
        var (dono, propriedade) = Propriedade(alvo, definido.Caminho);
        var tipo = Nullable.GetUnderlyingType(propriedade.PropertyType) ?? propriedade.PropertyType;
        object? convertido;
        var texto = valor.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? string.Empty
            : valor.ValueKind == JsonValueKind.String ? valor.GetString() ?? string.Empty : valor.ToString();
        if (texto.Length > definido.Maximo) throw new InvalidOperationException($"{definido.Rotulo}: limite de {definido.Maximo} caracteres.");
        if (definido.Opcoes is not null)
        {
            convertido = null;
            if (texto.Length > 0)
            {
                var opcoesAtuais = Opcoes(instancia, definido);
                convertido = opcoesAtuais.FirstOrDefault(o => o.Id == texto)?.Objeto
                    ?? throw new InvalidOperationException("A opção não está mais disponível. Atualize a seleção.");
            }
            if (convertido is null && propriedade.PropertyType.IsValueType && Nullable.GetUnderlyingType(propriedade.PropertyType) is null)
                throw new InvalidOperationException("Selecione uma opção.");
        }
        else if (tipo == typeof(string)) convertido = texto;
        else if (tipo == typeof(bool)) convertido = string.Equals(texto, "true", StringComparison.OrdinalIgnoreCase) ? true : string.Equals(texto, "false", StringComparison.OrdinalIgnoreCase) ? false : throw new InvalidOperationException("Valor booleano inválido.");
        else if (tipo == typeof(DateTime))
        {
            if (texto.Length == 0 && Nullable.GetUnderlyingType(propriedade.PropertyType) is not null) convertido = null;
            else if (DateTime.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)) convertido = data;
            else throw new InvalidOperationException("Data inválida.");
        }
        else if (tipo == typeof(int) && int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var inteiro)) convertido = inteiro;
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
        if (definida is null || !Condicao(instancia.Vm, definida.Habilitado) || !Condicao(parametro ?? instancia.Vm, definida.HabilitadoLinha))
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
        if (instancia.Executando || Ocupado(instancia.Vm)) throw new InvalidOperationException("Aguarde a operação para fechar.");
        Concluir(instancia, false);
    }

    private Instancia Atual(string id)
    {
        ExigirSessao();
        if (_pilha.Count == 0 || _pilha[^1].Id != id) throw new InvalidOperationException("Este formulário já foi fechado ou substituído.");
        ExigirPermissao(_pilha[^1].Tipo);
        return _pilha[^1];
    }

    private static void ExigirPermissao(string tipo)
    {
        if (tipo is "Pergunta" or "Confirmacao" or "Aviso" or "TrocaSenha") return;
        if (tipo is "PacoteVenda" or "PacoteCatalogo")
        {
            SessaoUsuario.Atual.ExigirAlgum(Permissao.VenderPacote | Permissao.EditarFinanceiro, "editar pacotes");
            return;
        }
        if (tipo is "Lancamento" or "Recebimento" or "Conta" or "Recorrente" or "Categoria" or "Orcamento"
            or "RegraRepasse" or "Taxa" or "Tributo" or "ItemEstoque" or "MovimentoEstoque" or "MateriaisProcedimento")
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "editar informações financeiras");
        else SessaoUsuario.Atual.Exigir(Permissao.VerFinanceiro, "consultar informações financeiras");
    }

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

    private DialogoFinanceiroDto Montar(Instancia i)
    {
        var ocupado = i.Executando || Ocupado(i.Vm);
        var campos = i.Definicao.Campos.Select(c =>
        {
            var valor = Ler(i.Vm, c.Caminho);
            var opcoes = Opcoes(i, c);
            object? valorWeb = valor switch { DateTime data => data.ToString("yyyy-MM-dd"), bool b => b, null => null, _ => Formatar(valor) };
            if (c.Tipo == "senha") valorWeb = null;
            if (c.Opcoes is not null) valorWeb = valor is null ? "" : opcoes.FirstOrDefault(o => Equals(o.Objeto, valor))?.Id ?? "";
            return new CampoFinanceiroDto(c.Chave, c.Rotulo, c.Tipo, valorWeb,
                opcoes.Select(o => new OpcaoFinanceiroDto(o.Id, o.Rotulo)).ToArray(), Condicao(i.Vm, c.Visivel),
                !ocupado && c.Tipo != "leitura" && Condicao(i.Vm, c.Habilitado), c.Obrigatorio || i.Vm is Pergunta { Obrigatorio: true }, c.Ajuda);
        }).ToArray();
        var tabelas = i.Definicao.Tabelas.Select(t => new TabelaFinanceiroDto(t.Chave, t.Titulo,
            t.Colunas.Select(c => new ColunaFinanceiroDto(c.Caminho, c.Rotulo)).ToArray(),
            LinhasAtuais(i).Where(l => l.Tabela == t).Select(l => new LinhaFinanceiroDto(l.Id,
                t.Colunas.ToDictionary(c => c.Caminho, c => Formatar(Ler(l.Objeto, c.Caminho))),
                (t.Campos ?? []).Select(c => new CampoFinanceiroDto(c.Chave, c.Rotulo, c.Tipo, Ler(l.Objeto, c.Caminho), [],
                    Condicao(l.Objeto, c.Visivel), !ocupado && Condicao(i.Vm, t.Habilitado) && Condicao(l.Objeto, c.Habilitado), c.Obrigatorio, c.Ajuda)).ToArray(),
                t.Acoes.Select(a => AcaoDto(i, a, ocupado, l.Objeto)).ToArray())).ToArray())).ToArray();
        var acoes = i.Definicao.Acoes.Select(a => AcaoDto(i, a, ocupado)).Append(new AcaoFinanceiroDto("fechar",
            i.Definicao.Acoes.Any(a => a.Chave is "salvar" or "confirmar") ? "Cancelar" : "Fechar", !ocupado)).ToArray();
        var mensagem = i.Erro ?? Ler(i.Vm, "Mensagem")?.ToString() ?? Ler(i.Vm, "Erro")?.ToString();
        var titulo = i.Vm is Pergunta p ? p.Titulo : Ler(i.Vm, "Titulo")?.ToString() ?? i.Definicao.Titulo;
        var descricao = i.Vm is Pergunta pergunta ? pergunta.Descricao : i.Definicao.Descricao;
        var pagina = new PaginaFinanceiroDto(i.Tipo, titulo, descricao, campos, [],
            tabelas.Length == 0 ? [] : [new SecaoFinanceiroDto("registros", "", null, [], [], tabelas, [])],
            acoes, ocupado, Ler(i.Vm, "NaoVerificado") is true, mensagem,
            i.Erro is not null || !string.IsNullOrEmpty(Ler(i.Vm, "Erro")?.ToString()) || Ler(i.Vm, "MensagemEhErro") is true || (i.Tipo == "Recebimento" && !string.IsNullOrEmpty(mensagem)), false);
        return new DialogoFinanceiroDto(i.Id, pagina, ocupado, !ocupado);
    }

    private static AcaoFinanceiroDto AcaoDto(Instancia i, Acao a, bool ocupado, object? linha = null) =>
        new(a.Chave, a.Rotulo, !ocupado && Condicao(i.Vm, a.Habilitado) && Condicao(linha ?? i.Vm, a.HabilitadoLinha)
            && (i.Vm is Pergunta || (Ler(i.Vm, a.Comando) as ICommand)?.CanExecute(linha) == true), i.Vm is Pergunta { Perigoso: true } ? "perigo" : a.Estilo);

    private static bool Ocupado(object vm) => new[] { "Salvando", "Ocupado", "Carregando" }.Any(p => Ler(vm, p) is true);
    private static bool Condicao(object vm, string? caminho) => caminho is null || caminho.Split('&').All(parte =>
        parte.StartsWith('!') ? Ler(vm, parte[1..]) is false : Ler(vm, parte) is true);
    private static object? Ler(object vm, string caminho)
    {
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
    private static List<Opcao> Opcoes(Instancia i, Campo campo)
    {
        if (campo.Opcoes is null || Ler(i.Vm, campo.Opcoes) is not IEnumerable lista) return [];
        return lista.Cast<object>().Select(o => new Opcao(i.Token(o), o,
            o is Enum ? Formatar(o) : Ler(o, campo.RotuloOpcao)?.ToString() ?? Ler(o, "Nome")?.ToString() ?? Formatar(o))).ToList();
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
        public void Limpar() { foreach (var limpar in Limpezas) limpar(); Limpezas.Clear(); if (Vm is DialogosTrocaSenhaViewModel senha) senha.Dispose(); }
    }
    private sealed class Pergunta
    {
        public string Titulo { get; init; } = "";
        public string Descricao { get; init; } = "";
        public string Texto { get; set; } = "";
        public bool Obrigatorio { get; init; }
        public bool Perigoso { get; init; }
    }
    private sealed record Campo(string Chave, string Caminho, string Rotulo, string Tipo = "texto", string? Opcoes = null,
        string RotuloOpcao = "Rotulo", string? Visivel = null, string? Habilitado = null, bool Obrigatorio = false, int Maximo = 4000, string? Ajuda = null);
    private sealed record Acao(string Chave, string Rotulo, string Comando, string Estilo = "secundario", string? Habilitado = null, string? HabilitadoLinha = null);
    private sealed record Coluna(string Caminho, string Rotulo);
    private sealed record Tabela(string Chave, string Titulo, string Colecao, Coluna[] Colunas, Acao[] Acoes, Campo[]? Campos = null, string? Habilitado = null);
    private sealed record Definicao(string Titulo, string? Descricao, Campo[] Campos, Acao[] Acoes, Tabela[] Tabelas);
    private sealed record Opcao(string Id, object Objeto, string Rotulo);
    private sealed record Linha(string Id, object Objeto, Tabela Tabela);
}
