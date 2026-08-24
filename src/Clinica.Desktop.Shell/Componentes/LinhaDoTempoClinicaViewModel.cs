using System.Collections.ObjectModel;
using Clinica.Application;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Um chip de seção: o rótulo com a contagem, e a natureza que ele representa.</summary>
public sealed partial class ChipSecaoClinica : ObservableObject
{
    public required NaturezaRegistroClinico Natureza { get; init; }
    public required string Rotulo { get; init; }
    public required int Quantidade { get; init; }

    [ObservableProperty] private bool _marcado;

    /// <summary>"Enfermagem (31)" — a contagem ANTES do clique é o que faz o chip valer.</summary>
    public string Texto => $"{Rotulo} ({Quantidade})";
}

/// <summary>
/// A LINHA DO TEMPO CLÍNICA DO PACIENTE (parcela 72) — o componente que as TRÊS portas
/// compartilham: a ficha da Recepção, o Consultório e a tela da Enfermagem.
///
/// O que ela responde
/// ------------------
/// <b>"O que já aconteceu com este paciente"</b> — a sessão médica, a evolução de
/// enfermagem, a folha de infusão e os documentos emitidos, num lugar só. Até aqui cada
/// uma dessas quatro morava numa tela diferente, de um módulo diferente, e quem executa a
/// infusão não alcançava a conduta da consulta de hoje.
///
/// ⚠️ POR QUE CHIPS DE SEÇÃO E NÃO UMA LISTA FUNDIDA — duas razões, e cada uma sozinha
/// decide:
///
/// 1. <b>Os ids são POR TABELA.</b> A evolução de enfermagem nº 42 e a <c>Evolucao</c> nº
///    42 são registros de pacientes diferentes. Uma lista fundida cujo comando destrutivo
///    recebesse só o id cancelaria o registro errado — <b>não estoura, não avisa</b>. O
///    comentário que documenta esse bug está em <c>PacientesView.xaml</c> desde a parcela
///    71, escrito por quem construiu a seção. Aqui o item carrega
///    <c>Natureza</c> + <c>Id</c>, e é isso que permite fundir as listas no dia em que
///    fizer sentido, sem reabrir o buraco.
/// 2. <b>A ordenação cronológica é impossível hoje.</b> <c>Evolucao.Data</c> é
///    <c>DateOnly</c>; a evolução de enfermagem tem data E hora. Ordenar a médica às 00:00
///    a poria antes de todas as aferições do dia, inclusive da reação que a motivou;
///    ordenar por <c>CriadoEm</c> usaria quando o texto foi DIGITADO — e o módulo do
///    Consultório existe inteiro porque isso acontece dias depois. <b>Ordenar um prontuário
///    por uma hora que não existe é fabricar sequência de eventos num documento que
///    responde em auditoria.</b>
///
/// ⚠️ Os chips desmarcados MOSTRAM A CONTAGEM. É a contagem visível que faz a enfermeira
/// descobrir que há 12 sessões médicas para ler — chip pré-marcado sem número ao lado
/// deixaria a entrega desligada justamente na tela de quem mais precisa dela.
///
/// ⚠️ O filtro de ACESSO não mora aqui: ele é parâmetro de
/// <see cref="LinhaDoTempoClinica.Montar"/>, na Application, onde o <c>dotnet test</c>
/// alcança. Regra de LGPD repetida em três telas é regra que a quarta esquece, e o erro
/// aparece como uma linha a mais numa lista — que ninguém percebe.
/// </summary>
public sealed partial class LinhaDoTempoClinicaViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    /// <summary>Descarte de resposta fora de ordem: o componente troca de paciente a cada clique.</summary>
    private int _geracaoCarga;

    private IReadOnlyDictionary<NaturezaRegistroClinico, IReadOnlyList<RegistroClinicoPaciente>>
        _porNatureza = new Dictionary<NaturezaRegistroClinico, IReadOnlyList<RegistroClinicoPaciente>>();

    private int _pacienteId;

    /// <summary>As quatro naturezas que este componente sabe desenhar, na ordem dos chips.</summary>
    private static readonly NaturezaRegistroClinico[] Conhecidas =
    [
        NaturezaRegistroClinico.SessaoMedica,
        NaturezaRegistroClinico.EvolucaoEnfermagem,
        NaturezaRegistroClinico.PrescricaoInterna,
        NaturezaRegistroClinico.DocumentoClinico
    ];

    /// <summary>Rótulo CURTO do chip — o do catálogo é a frase da contagem, longa demais aqui.</summary>
    private static string RotuloCurto(NaturezaRegistroClinico natureza) => natureza switch
    {
        NaturezaRegistroClinico.SessaoMedica => "Médica",
        NaturezaRegistroClinico.EvolucaoEnfermagem => "Enfermagem",
        NaturezaRegistroClinico.PrescricaoInterna => "Infusões",
        NaturezaRegistroClinico.DocumentoClinico => "Documentos",
        _ => CatalogoRegistroClinico.Rotular(natureza)
    };

    public ObservableCollection<ChipSecaoClinica> Chips { get; } = new();
    public ObservableCollection<RegistroClinicoPaciente> Itens { get; } = new();

    /// <summary>Nenhuma natureza é alcançável por quem está lendo — ver <see cref="MontarChips"/>.</summary>
    [ObservableProperty] private bool _semAcesso;

    /// <summary>
    /// Metade que DESLIGA a sobreposição de vazio quando não há acesso: ali quem explica
    /// é o resumo ("o seu acesso não permite…"), e "Nada registrado" por cima dele seria
    /// mentira com cara de estado vazio.
    /// </summary>
    public bool ComAcesso => !SemAcesso;

    partial void OnSemAcessoChanged(bool value) => OnPropertyChanged(nameof(ComAcesso));

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;

    /// <summary>
    /// Modo COMPACTO (a coluna direita do atendimento): três linhas por seção, sem
    /// detalhe. Não é um segundo componente — é a mesma leitura, cortada.
    /// </summary>
    [ObservableProperty] private bool _compacto;

    /// <summary>O que a lista está mostrando — a frase que evita o "filtro esquecido".</summary>
    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>
    /// A seção marcada. ⚠️ UMA por vez, e por isso não é filtro combinável: o que se lê
    /// aqui é o histórico de um TIPO de registro, e misturar dois traria de volta o
    /// problema do id ambíguo.
    /// </summary>
    [ObservableProperty] private NaturezaRegistroClinico _secao = NaturezaRegistroClinico.SessaoMedica;

    /// <summary>
    /// A natureza que a porta quer ver primeiro. A tela da Enfermagem abre em Enfermagem;
    /// a ficha e o Consultório, na sessão médica.
    /// </summary>
    public NaturezaRegistroClinico SecaoInicial { get; init; } = NaturezaRegistroClinico.SessaoMedica;

    /// <summary>
    /// ⚠️ Na ficha da Recepção este chip é FALSO: a aba Documentos ao lado é a porta do
    /// papel e faz mais do que este componente faria (emitir, termo, assinar, enviar).
    /// Duas listas do mesmo papel na mesma tela fazem a pessoa procurar a diferença que
    /// não existe.
    /// </summary>
    public bool MostrarDocumentos { get; init; } = true;

    /// <summary>
    /// As seções que ESTA porta mostra. Vazio = as quatro que o componente conhece.
    ///
    /// ⚠️ Existe porque a aba Prontuário do CONSULTÓRIO mantém a lista rica de sessões que
    /// ela já tinha — com busca no texto, contagem de anexos, correções e os botões da
    /// linha — e o componente entra ali para as naturezas que faltavam. Substituir aquela
    /// lista pela genérica tiraria capacidade de quem a usava todo dia, que é a regra 3 do
    /// bloco do faturamento aplicada a uma tela clínica.
    /// </summary>
    public IReadOnlyCollection<NaturezaRegistroClinico> SecoesVisiveis { get; init; } = [];

    // ---- As ações, injetadas pela PORTA ----
    //
    // ⚠️ O componente NÃO decide o que fazer com o item: quem sabe abrir a evolução da
    // sessão é a ficha, quem sabe abrir a folha é a sala, e as duas telas têm janelas
    // diferentes. O que o componente garante é que a ação chegue com a NATUREZA junto —
    // é o discriminador que impede o comando da sessão de encostar no id 42 da
    // enfermagem.

    /// <summary>Abrir o registro escolhido. Nulo = a porta não oferece isso.</summary>
    public Func<RegistroClinicoPaciente, Task>? AoAbrir { get; init; }

    /// <summary>Cancelar o registro escolhido (com motivo, sempre — nunca apagar).</summary>
    public Func<RegistroClinicoPaciente, Task>? AoCancelar { get; init; }

    /// <summary>
    /// As naturezas em que a porta sabe abrir/cancelar. Vazio = nenhuma, e os botões não
    /// existem — botão aceso que não faz nada é o defeito da parcela 41.
    /// </summary>
    public IReadOnlyCollection<NaturezaRegistroClinico> NaturezasComAcao { get; init; } = [];

    /// <summary>A permissão que a porta exige para MEXER (a metade visível).</summary>
    public Permissao AcessoParaMexer { get; init; } = Permissao.EditarProntuario;

    /// <summary>A seção atual tem ação, e quem está logado pode exercê-la.</summary>
    public bool PodeMexerNaSecao =>
        NaturezasComAcao.Contains(Secao) && SessaoUsuario.Atual.Pode(AcessoParaMexer);

    public bool TemAcaoAbrir => AoAbrir is not null && PodeMexerNaSecao;
    public bool TemAcaoCancelar => AoCancelar is not null && PodeMexerNaSecao;

    /// <summary>
    /// A seção ainda não foi resolvida contra <see cref="SecaoInicial"/>.
    ///
    /// ⚠️ ELA NÃO PODE SER RESOLVIDA NO CONSTRUTOR, e foi assim que a tela da Enfermagem
    /// nasceu abrindo no chip ERRADO: <c>SecaoInicial</c> é <c>init</c>, e propriedade de
    /// <i>object initializer</i> é atribuída <b>depois</b> do corpo do construtor. Ler
    /// <c>SecaoInicial</c> lá dentro devolve sempre o DEFAULT — o que a porta pediu chega
    /// tarde demais.
    ///
    /// O socorro era acidental: <see cref="MontarChips"/> só trocava a seção quando a atual
    /// não estava visível, então as duas portas que restringem <c>SecoesVisiveis</c>
    /// funcionavam por tabela — e a Enfermagem, que mostra tudo, abria em "Médica",
    /// listando as sessões do médico na tela cuja razão de existir é a evolução DELA.
    /// </summary>
    private bool _secaoPendente = true;

    public LinhaDoTempoClinicaViewModel(IServiceScopeFactory escopos)
        => _escopos = escopos;

    partial void OnSecaoChanged(NaturezaRegistroClinico value)
    {
        Publicar();
        OnPropertyChanged(nameof(PodeMexerNaSecao));
        OnPropertyChanged(nameof(TemAcaoAbrir));
        OnPropertyChanged(nameof(TemAcaoCancelar));
    }

    [RelayCommand]
    private async Task AbrirAsync(RegistroClinicoPaciente? item)
    {
        // Guarda sobre PARÂMETRO: não dispara vindo de botão de linha (exceção da 21).
        if (item is null || AoAbrir is null) return;
        await AoAbrir(item);
    }

    [RelayCommand]
    private async Task CancelarAsync(RegistroClinicoPaciente? item)
    {
        if (item is null || AoCancelar is null) return;
        await AoCancelar(item);
    }

    /// <summary>Troca a seção pelo chip. Marcar um DESMARCA o irmão — sempre há uma marcada.</summary>
    [RelayCommand]
    private void Escolher(ChipSecaoClinica? chip)
    {
        if (chip is null) return;
        Secao = chip.Natureza;
    }

    /// <summary>
    /// Lê tudo do paciente. Cinco serviços que já existem — nenhuma consulta nova foi
    /// escrita para este componente.
    /// </summary>
    public async Task CarregarAsync(int pacienteId)
    {
        _pacienteId = pacienteId;

        if (pacienteId == 0)
        {
            // ⚠️ AVANÇA a geração antes de limpar. Sem isto, a leitura do paciente
            // anterior que ainda está no ar responde DEPOIS do Limpar e republica o
            // prontuário DELE numa tela que já trocou de pessoa — o defeito da parcela 60
            // no caminho que parece não ter carga nenhuma.
            ++_geracaoCarga;
            Limpar();
            SemAcesso = false;
            return;
        }

        // ⚠️ NEM LER NEM DESENHAR (art. 5º, II), no PONTO ÚNICO. Duas das três portas
        // chamavam CarregarAsync sem conferir o bit antes, contando com o filtro do
        // montador — mas àquela altura o prontuário inteiro já tinha vindo do banco para a
        // memória de quem não pode vê-lo, que é metade do que a regra proíbe.
        if (!SessaoUsuario.Atual.Pode(Permissao.VerProntuario))
        {
            Limpar();
            SemAcesso = true;
            Resumo = "O seu acesso não permite ler o prontuário deste paciente.";
            return;
        }

        var geracao = ++_geracaoCarga;
        Carregando = true;
        NaoVerificado = false;
        Mensagem = null;

        try
        {
            using var scope = _escopos.CreateScope();
            var servicos = scope.ServiceProvider;

            var prontuario = servicos.GetRequiredService<ProntuarioService>();
            var sessoes = await prontuario.DoPacienteAsync(pacienteId);
            if (geracao != _geracaoCarga) return;

            var anexos = await prontuario.ContagemDeAnexosAsync(
                sessoes.Select(e => e.Id).ToList());
            if (geracao != _geracaoCarga) return;

            var enfermagem = await servicos.GetRequiredService<EvolucaoEnfermagemService>()
                .DoPacienteAsync(pacienteId, limite: 200);
            if (geracao != _geracaoCarga) return;

            var infusoes = await servicos.GetRequiredService<PrescricaoInternaService>()
                .DoPacienteAsync(pacienteId, limite: 200);
            if (geracao != _geracaoCarga) return;

            var documentos = MostrarDocumentos
                ? await servicos.GetRequiredService<DocumentoClinicoService>()
                    .DoPacienteAsync(pacienteId)
                : [];
            if (geracao != _geracaoCarga) return;

            // ⚠️ `Efetivas` e não `Permissoes` cru: sem sessão autenticada `Pode` LIBERA
            // (a regra do projeto), e ler o campo direto faria o componente abrir vazio
            // fora do login — tela vazia se lê como defeito.
            _porNatureza = LinhaDoTempoClinica.Montar(
                SessaoUsuario.Atual.Efetivas,
                sessoes, anexos, enfermagem, infusoes, documentos);

            MontarChips();
            Publicar();
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;
            NaoVerificado = true;
            Diagnostico.Registrar("Linha do tempo clínica não pôde ser carregada", ex);
            Mensagem = ex.Message;
        }
        finally
        {
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    public Task RecarregarAsync() => CarregarAsync(_pacienteId);

    private void Limpar()
    {
        Carregando = false;
        _porNatureza = new Dictionary<NaturezaRegistroClinico, IReadOnlyList<RegistroClinicoPaciente>>();
        Chips.Clear();
        Itens.Clear();
        Resumo = string.Empty;
    }

    private void MontarChips()
    {
        var visiveis = Conhecidas
            .Where(n => SecoesVisiveis.Count == 0 || SecoesVisiveis.Contains(n))
            .Where(n => MostrarDocumentos || n != NaturezaRegistroClinico.DocumentoClinico)
            // Seção que a pessoa não pode ler vem VAZIA do montador, e some — anunciar
            // "Médica (0)" a quem não tem `VerProntuario` contaria que existem sessões.
            .Where(n => SessaoUsuario.Atual.Efetivas.HasFlag(
                CatalogoRegistroClinico.Obter(n).PermissaoVer))
            .ToList();

        // Entre o Clear() e o último Add não pode haver await (parcela 62) — aqui não há,
        // e o padrão fica pelo mesmo motivo de sempre.
        var novos = visiveis
            .Select(n => new ChipSecaoClinica
            {
                Natureza = n,
                Rotulo = RotuloCurto(n),
                Quantidade = _porNatureza.GetValueOrDefault(n)?.Count ?? 0
            })
            .ToList();

        Chips.Clear();
        foreach (var c in novos) Chips.Add(c);

        // Nenhuma seção visível = esta pessoa não alcança NADA desta lista. É estado
        // próprio, e não uma seção qualquer: `visiveis.FirstOrDefault()` sobre lista vazia
        // devolve `SessaoMedica` (valor 0 do enum) — um valor plausível no lugar de um
        // sinal de ausência —, e a tela então afirmava "sem registro de sessões" para quem
        // não pode nem saber se elas existem.
        SemAcesso = visiveis.Count == 0;
        if (SemAcesso)
        {
            Itens.Clear();
            Resumo = "O seu acesso não permite ler o prontuário deste paciente.";
            return;
        }

        // A seção pedida pela PORTA, resolvida aqui — nunca no construtor (ver
        // `_secaoPendente`). Depois da primeira montagem, só se troca quando a seção
        // escolhida deixou de existir para esta pessoa.
        if (_secaoPendente || !visiveis.Contains(Secao))
        {
            _secaoPendente = false;
            Secao = visiveis.Contains(SecaoInicial) ? SecaoInicial : visiveis[0];
        }
    }

    private void Publicar()
    {
        if (SemAcesso) return;

        foreach (var chip in Chips) chip.Marcado = chip.Natureza == Secao;

        var lista = _porNatureza.GetValueOrDefault(Secao) ?? [];

        // Do mais recente para o mais antigo, DENTRO da natureza — nunca entre naturezas
        // (ver o comentário da classe).
        var ordenada = lista.OrderByDescending(r => r.Momento).ThenByDescending(r => r.Id);
        var recorte = (Compacto ? ordenada.Take(3) : ordenada).ToList();

        Itens.Clear();
        foreach (var r in recorte) Itens.Add(r);

        var total = lista.Count;

        // ⚠️ Vazio NÃO escreve resumo: quem responde "não há nada" é o EstadoDaTela da
        // lista — duas respostas para a mesma pergunta saíam desenhadas uma por cima da
        // outra (o print da parcela 79). "Um estado vazio por pergunta" (parcela 37).
        Resumo = total == 0
            ? string.Empty
            : Compacto && total > recorte.Count
                ? $"{recorte.Count} de {CatalogoRegistroClinico.Contar(Secao, total)}"
                : CatalogoRegistroClinico.Contar(Secao, total);
    }
}
