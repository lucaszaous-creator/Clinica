using Clinica.Domain.Entities;
using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>Um paciente na carteira do profissional, já com a leitura da dor pronta.</summary>
public sealed class LinhaPacienteClinico
{
    public required int PacienteId { get; init; }
    public required string Nome { get; init; }
    public required string Sessoes { get; init; }
    public required string UltimaSessao { get; init; }
    public required string Dor { get; init; }
    public required string Ganho { get; init; }

    /// <summary>Sem vir há muito tempo — o destaque que faz a clínica ligar.</summary>
    public required bool Sumido { get; init; }

    /// <summary>Nunca teve o par EVA completo: não há o que dizer sobre a dor dele.</summary>
    public required bool SemMedida { get; init; }

    /// <summary>Dias sem vir a partir dos quais a linha ganha destaque.</summary>
    public const int DiasParaDestaque = 45;

    public static LinhaPacienteClinico De(PacienteDoProfissional p, DateOnly hoje)
    {
        var dias = p.DiasSemVir(hoje);

        return new LinhaPacienteClinico
        {
            PacienteId = p.PacienteId,
            Nome = p.Nome,
            Sessoes = p.Sessoes == 0 ? "—" : $"{p.Sessoes} sessão(ões)",
            UltimaSessao = p.UltimaSessao is { } d
                ? $"{d:dd/MM/yyyy}" + (dias is { } n and > 0 ? $" · há {n} dias" : " · hoje")
                : "sem sessão registrada",
            Dor = p.UltimaDor is { } atual ? $"{atual}/10" : "—",
            Ganho = p.GanhoAcumulado is { } g
                ? (g >= 0 ? $"−{g} desde o início" : $"+{-g} desde o início")
                : "—",
            Sumido = dias is { } sem && sem > DiasParaDestaque,
            SemMedida = p.UltimaDor is null
        };
    }
}

/// <summary>
/// A carteira do profissional: quem ele atende, quando veio pela última vez e como a dor
/// está.
///
/// É diferente da lista de pacientes da recepção, e a diferença não é cosmética. Lá a
/// lista é de CADASTRO — todo mundo, ordenado por nome, com telefone e convênio, para
/// achar quem ligou. Aqui é de TRATAMENTO — só quem este profissional atendeu, ordenado
/// por quem veio por último, com a leitura da dor ao lado. Uma responde "quem é essa
/// pessoa?", a outra "como está indo o tratamento dela?".
/// </summary>
public sealed partial class MeusPacientesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    /// <summary>Todos os pacientes lidos; a lista visível é o filtro sobre eles.</summary>
    private readonly List<LinhaPacienteClinico> _todos = [];

    public ObservableCollection<LinhaPacienteClinico> Pacientes { get; } = [];

    [ObservableProperty] private string _termo = string.Empty;

    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>Mostrar só quem está sem vir há mais tempo que o destaque.</summary>
    [ObservableProperty] private bool _somenteSumidos;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>
    /// A carteira mostrada é a da CLÍNICA, e não a de uma pessoa. São dois caminhos
    /// até aqui (ver <see cref="PostoClinico"/>): não haver cadastro vinculado, e não
    /// haver agenda própria — que é o caso da enfermagem.
    /// </summary>
    [ObservableProperty] private bool _listaDaClinica;

    /// <summary>POR QUE a lista é da clínica — a frase certa para cada motivo.</summary>
    [ObservableProperty] private string? _motivoDaLista;

    // ==================== Os dois modos da lista (parcela 88) ====================
    //
    // A cliente corrigiu o pedido da enfermagem para valer TAMBÉM para quem consulta:
    // "ver todos os pacientes e clicar em atender". E o buraco era real e simétrico —
    // `MeusPacientesAsync(profissionalId)` devolve só quem ELE já atendeu, então o
    // paciente de primeira consulta, o do colega e o que o balcão acabou de cadastrar
    // eram INALCANÇÁVEIS do Consultório: não havia segunda porta.
    //
    // ⚠️ A carteira dele CONTINUA sendo o padrão, e isso é decisão: "quem eu acompanho"
    // é a pergunta que a tela responde todo dia, e trocá-la pela clínica inteira
    // afogaria os pacientes dele no cadastro. O que faltava era o segundo clique.

    /// <summary>Modo "carteira da clínica" — ligado pelo chip.</summary>
    [ObservableProperty] private bool _mostrandoTodos;

    /// <summary>
    /// O irmão do chip acima. Os dois modos são EXCLUSIVOS, e o par existe para a régua
    /// dizer qual está no ar ANTES do clique — dois botões iguais não dizem em qual dos
    /// dois você está, e filtro esquecido respondendo "ninguém aqui" faz o profissional
    /// concluir que a carteira dele está vazia.
    /// </summary>
    public bool MostrandoMinha => !MostrandoTodos;

    /// <summary>
    /// Os chips só EXISTEM para quem tem carteira própria. Sem vínculo — ou sendo
    /// enfermagem, que não tem agenda própria — os dois modos mostrariam a MESMA lista, e
    /// dois chips que fazem a mesma coisa são a pior espécie de filtro: a pessoa clica nos
    /// dois para descobrir que não muda nada. Aí quem explica é <see cref="MotivoDaLista"/>.
    /// </summary>
    public bool TemCarteiraPropria => !ListaDaClinica;

    partial void OnListaDaClinicaChanged(bool value)
        => OnPropertyChanged(nameof(TemCarteiraPropria));

    partial void OnMostrandoTodosChanged(bool value)
    {
        OnPropertyChanged(nameof(MostrandoMinha));
        OnPropertyChanged(nameof(PlaceholderDaBusca));
    }

    /// <summary>
    /// O que a busca faz NESTE modo — e a diferença não é enfeite: na carteira própria ela
    /// filtra o que já está na tela; no modo "todos" ela vai ao BANCO e alcança o cadastro
    /// inteiro, por nome OU CPF. Prometer "filtrar pelo nome" ali faria ninguém tentar o
    /// CPF, que é justamente como se separa o homônimo.
    /// </summary>
    public string PlaceholderDaBusca => MostrandoTodos
        ? "Buscar no cadastro da clínica — nome ou CPF"
        : "Filtrar pelo nome";

    /// <summary>
    /// Agrupa as teclas da busca quando ela vai ao BANCO. É o mesmo atraso do
    /// <c>SeletorPacienteViewModel</c>, e pela mesma razão: no modo "todos" o termo é
    /// resolvido no SQL, e uma consulta por letra digitada é o que a carteira própria
    /// evita filtrando em memória.
    /// </summary>
    private const int AtrasoDigitacaoMs = 300;

    private CancellationTokenSource? _digitacao;

    // ⚠️ Ela NÃO recebe mais o `PacienteEmFoco`: quem entrega o paciente ao posto é
    // `EntregaDoPaciente.AoPostoAsync`, no shell, que resolve o singleton por conta
    // própria e amarra o horário de hoje junto. Guardar aqui uma referência que ninguém
    // lê seria o defeito recorrente do projeto na versão mais barata de cometer.
    public MeusPacientesViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        _ = CarregarAsync();
    }

    partial void OnTermoChanged(string value)
    {
        // ⚠️ Na carteira própria o filtro é em MEMÓRIA (ela tem teto e cabe na tela). Na
        // da clínica ele tem de ir ao SQL: a lista já vem cortada no limite, e filtrar em
        // memória o que veio cortado faz a busca responder "não existe" para todo paciente
        // além do teto — que é a resposta errada mais cara que uma busca de paciente pode
        // dar, porque leva a cadastrar a pessoa de novo (o CPF duplicado da parcela 57).
        if (MostrandoTodos) _ = RebuscarAsync();
        else Filtrar();
    }

    partial void OnSomenteSumidosChanged(bool value) => Filtrar();

    /// <summary>Recarrega do banco depois de agrupar as teclas.</summary>
    private async Task RebuscarAsync()
    {
        var atual = new CancellationTokenSource();
        var anterior = _digitacao;
        _digitacao = atual;
        anterior?.Cancel();

        try
        {
            await Task.Delay(AtrasoDigitacaoMs, atual.Token);
            await CarregarAsync();
        }
        catch (OperationCanceledException)
        {
            // Outra tecla chegou antes — a busca desta foi abandonada de propósito.
        }
    }

    [RelayCommand]
    private async Task MinhaCarteiraAsync()
    {
        MostrandoTodos = false;
        await CarregarAsync();
    }

    [RelayCommand]
    private async Task TodosAsync()
    {
        MostrandoTodos = true;
        await CarregarAsync();
    }

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 60): dois cliques no Atualizar largam
    /// duas leituras no ar, e num banco remoto a VELHA pode responder por último.
    /// </summary>
    private int _geracaoCarga;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;

            // De quem é esta lista — a resposta mora num lugar só (PostoClinico):
            // a enfermagem NÃO tem agenda própria, e filtrar por ela devolvia a
            // tela vazia justamente para quem está cadastrado certo.
            var profissionalId = PostoClinico.ProfissionalDaLista();
            ListaDaClinica = profissionalId is null;
            MotivoDaLista = PostoClinico.MotivoDaListaAmpla();

            var hoje = DateOnly.FromDateTime(DateTime.Today);

            using var scope = _escopos.CreateScope();
            var consultorio = scope.ServiceProvider.GetRequiredService<ConsultorioService>();

            // No modo "todos" o dono some da consulta e o TERMO desce para o SQL. Nulo
            // dos dois lados é o mesmo caminho que a enfermagem já percorre.
            var pacientes = MostrandoTodos
                ? await consultorio.MeusPacientesAsync(
                    profissionalId: null, termo: string.IsNullOrWhiteSpace(Termo) ? null : Termo)
                : await consultorio.MeusPacientesAsync(profissionalId);
            if (geracao != _geracaoCarga) return;

            // Clear e Adds JUNTOS, depois do await: entre um e outro a carteira ficaria
            // vazia na tela, e duas cargas intercaladas a deixariam duplicada (parcela 62).
            _todos.Clear();
            foreach (var p in pacientes) _todos.Add(LinhaPacienteClinico.De(p, hoje));

            Filtrar();
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar("Consultório — carteira de pacientes não pôde ser lida", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            // Só a carga VIGENTE apaga o "Carregando": a superada desligaria o indicador
            // enquanto a nova ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    /// <summary>Teto da carteira da clínica — o mesmo padrão do serviço.</summary>
    private const int TetoDaClinica = 200;

    /// <summary>
    /// Na carteira PRÓPRIA o filtro roda em memória sobre o que já veio: ela tem teto e
    /// cabe na tela, e ir ao banco a cada tecla daria uma consulta por letra digitada.
    ///
    /// ⚠️ No modo "todos" o termo JÁ foi resolvido no SQL, e refiltrar por nome aqui
    /// derrubaria a busca por CPF — o servidor casa nome OU documento, e o filtro de
    /// memória só conhece o nome. Achar a pessoa pelo CPF e vê-la sumir da lista é a
    /// espécie de defeito que leva a cadastrar o paciente de novo.
    ///
    /// A lista filtrada DIZ que está filtrada, como no resto do projeto — "8 pacientes"
    /// sozinho faria o profissional concluir que atende oito pessoas.
    /// </summary>
    private void Filtrar()
    {
        var termo = Termo.Trim();
        var filtrarPorNome = !MostrandoTodos && termo.Length > 0;

        // Entre o Clear() e o último Add não pode haver await — aqui não há nenhum, e a
        // montagem é síncrona de propósito.
        Pacientes.Clear();
        foreach (var p in _todos)
        {
            if (SomenteSumidos && !p.Sumido) continue;
            if (filtrarPorNome && !p.Nome.Contains(termo, StringComparison.OrdinalIgnoreCase))
                continue;
            Pacientes.Add(p);
        }

        if (MostrandoTodos)
        {
            // ⚠️ O teto é DITO. Uma lista cortada em 200 que se anuncia como "todos os
            // pacientes" faz quem não achou alguém concluir que ele não está cadastrado —
            // corte silencioso é a regra que este projeto recusa desde sempre.
            var cortada = _todos.Count >= TetoDaClinica;
            Resumo = termo.Length > 0
                ? $"Busca “{termo}” — {Pacientes.Count} paciente(s) do cadastro da clínica."
                : $"Todos os pacientes — {Pacientes.Count} em ordem de nome"
                  + (cortada
                      ? $", que é o teto desta tela. Busque pelo nome ou CPF para alcançar quem está fora."
                      : ".");
            return;
        }

        var filtrado = termo.Length > 0 || SomenteSumidos;
        Resumo = filtrado
            ? $"{Pacientes.Count} de {_todos.Count} paciente(s) na sua carteira."
            : $"{_todos.Count} paciente(s) na sua carteira, do que veio por último ao mais antigo.";
    }

    /// <summary>
    /// Leva ao atendimento com este paciente em foco.
    ///
    /// ⚠️ Ele passa pelo PONTO ÚNICO de entrega, que amarra o HORÁRIO de hoje quando há um
    /// (parcela 88). Antes ele fixava só o id e o nome — e isso era aceitável enquanto a
    /// carteira era só a dele: o caminho normal era "Meu dia", que já traz o agendamento.
    /// Agora que a lista alcança a clínica inteira, ela virou o caminho de quem cobre o
    /// colega — e sem o vínculo a evolução nasceria solta, deixando a sessão em "Sessões
    /// sem evolução" para sempre, mesmo depois de escrita.
    /// </summary>
    [RelayCommand]
    private async Task AtenderAsync(LinhaPacienteClinico? linha)
    {
        // Guarda sobre PARÂMETRO: nunca dispara vindo de botão de linha (a exceção
        // declarada da checagem 21).
        if (linha is null) return;

        // A seção de escrita de QUEM clicou: quem consulta cai no S-O-A-P, quem executa
        // cai na passagem de enfermagem. Uma palavra, dois destinos certos.
        await AbrirNaSecaoAsync(linha, PostoClinico.ChaveDoAtendimento());
    }

    [RelayCommand]
    private Task VerDorAsync(LinhaPacienteClinico? linha)
        => AbrirNaSecaoAsync(linha, ModuloClinico.ChaveEvolucaoDor);

    [RelayCommand]
    private Task VerAvaliacoesAsync(LinhaPacienteClinico? linha)
        => AbrirNaSecaoAsync(linha, ModuloClinico.ChaveAvaliacoes);

    /// <summary>
    /// ⚠️ As três portas da linha entregam o paciente do MESMO jeito, e isso não é
    /// arrumação: elas caem na MESMA tela, onde o rail troca de seção sem trocar de
    /// paciente. Se "Dor" fixasse o foco sem o horário de hoje, bastaria clicar nela e
    /// mudar para o Atendimento pelo rail para a evolução nascer SOLTA — e a sessão
    /// continuaria em "Sessões sem evolução" depois de escrita. Duas saídas para o mesmo
    /// gesto, na mesma tela.
    /// </summary>
    private async Task AbrirNaSecaoAsync(LinhaPacienteClinico? linha, string chave)
    {
        if (linha is null) return;

        try
        {
            await EntregaDoPaciente.AoPostoAsync(_escopos, linha.PacienteId, linha.Nome);
            NavegacaoSuite.Ir(chave);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — o paciente não pôde ser aberto pela carteira", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
