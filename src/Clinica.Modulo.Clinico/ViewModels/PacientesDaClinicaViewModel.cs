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

    public static LinhaPacienteClinico De(PacienteDaCarteira p, DateOnly hoje)
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
/// A CARTEIRA DA CLÍNICA: quem vem se tratando aqui, quando veio pela última vez e como a
/// dor está.
///
/// ⚠️ Ela era "a carteira do profissional" e deixou de ser (parcela 88, 3ª rodada). A
/// clínica disse a frase que apaga a premissa: <b>"não existe 'meu paciente', todos
/// atendem todos"</b>. Enquanto a lista filtrava por dono, o paciente de PRIMEIRA
/// CONSULTA, o do colega e o que o balcão acabou de cadastrar eram inalcançáveis daqui —
/// e não havia segunda porta.
///
/// A autoria não se perdeu com isso: quem atendeu continua gravado no agendamento, quem
/// escreveu assina a evolução, e "Meus números" continua medindo o trabalho de cada um. O
/// que deixou de existir é a noção de DONO na lista de pacientes.
///
/// Ela continua diferente da lista da recepção, e a diferença não é cosmética. Lá a lista
/// é de CADASTRO — ordenada por nome, com telefone e convênio, para achar quem ligou.
/// Aqui é de TRATAMENTO — de quem veio por último ao mais antigo, com a leitura da dor ao
/// lado. Uma responde "quem é essa pessoa?", a outra "como está indo o tratamento dela?".
/// </summary>
public sealed partial class PacientesDaClinicaViewModel : ObservableObject
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
    /// Agrupa as teclas da busca. É o mesmo atraso do <c>SeletorPacienteViewModel</c>, e
    /// pela mesma razão: o termo é resolvido no SQL — a lista é o cadastro da clínica,
    /// cortada no teto, e filtrar em memória o que veio cortado faria a busca responder
    /// "não existe" para todo paciente além dele —, e uma consulta por letra digitada é o
    /// que este atraso evita.
    /// </summary>
    private const int AtrasoDigitacaoMs = 300;

    private CancellationTokenSource? _digitacao;

    // ⚠️ Ela NÃO recebe mais o `PacienteEmFoco`: quem entrega o paciente ao posto é
    // `EntregaDoPaciente.AoPostoAsync`, no shell, que resolve o singleton por conta
    // própria e amarra o horário de hoje junto. Guardar aqui uma referência que ninguém
    // lê seria o defeito recorrente do projeto na versão mais barata de cometer.
    public PacientesDaClinicaViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        _ = CarregarAsync();
    }

    // ⚠️ A busca vai ao SQL, sempre. A lista é o cadastro da clínica cortado no teto, e
    // filtrar em memória o que veio cortado faz a busca responder "não existe" para todo
    // paciente além dele — a resposta errada mais cara que uma busca de paciente pode dar,
    // porque leva a cadastrar a pessoa de novo (o CPF duplicado da parcela 57).
    partial void OnTermoChanged(string value) => _ = RebuscarAsync();

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

            var hoje = DateOnly.FromDateTime(DateTime.Today);

            using var scope = _escopos.CreateScope();
            var consultorio = scope.ServiceProvider.GetRequiredService<ConsultorioService>();

            // UMA fonte, e ela é a da clínica: não existe "meu paciente". Sem termo, quem
            // já foi atendido, do mais recente; com termo, o cadastro inteiro — é assim
            // que a primeira consulta fica alcançável.
            var pacientes = await consultorio.PacientesAsync(
                termo: string.IsNullOrWhiteSpace(Termo) ? null : Termo);
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

    /// <summary>Teto da carteira — o mesmo padrão do serviço.</summary>
    private const int TetoDaCarteira = 200;

    // ==================== O vazio tem DUAS perguntas (parcela 88, 3ª rodada) ============
    //
    // ⚠️ "não há ninguém em tratamento" e "esta busca não achou ninguém" são respostas
    // diferentes, e a segunda passou a existir quando a busca deixou de filtrar a tela e
    // passou a alcançar o CADASTRO INTEIRO. Responder "entra aqui quem já foi atendido" a
    // quem digitou um nome faria a pessoa concluir que o paciente EXISTE e só não foi
    // atendido — quando o que o sistema está dizendo é que ele não está cadastrado. E é
    // essa leitura errada que leva a cadastrar alguém que já tem ficha (parcela 57).

    [ObservableProperty] private string _tituloVazio = "Nenhum paciente em tratamento";

    [ObservableProperty] private string _descricaoVazio =
        "Entra aqui quem a clínica já atendeu: o horário precisa ter tido a presença "
        + "confirmada na recepção. Busque pelo nome ou CPF para alcançar quem ainda não veio.";

    /// <summary>
    /// Publica a lista. O TERMO já foi resolvido no SQL — o que sobra em memória é o
    /// recorte de "sumidos", que é sobre o que já veio.
    ///
    /// ⚠️ Refiltrar o termo aqui derrubaria a busca por CPF: o servidor casa nome OU
    /// documento, e um filtro de nome em memória só conhece o nome. Achar a pessoa pelo
    /// CPF e vê-la SUMIR da lista é a espécie de defeito que leva a cadastrar o paciente
    /// de novo.
    ///
    /// A lista DIZ o que está mostrando, como no resto do projeto — "8 pacientes" sozinho
    /// faria o profissional concluir que a clínica atende oito pessoas.
    /// </summary>
    private void Filtrar()
    {
        // Entre o Clear() e o último Add não pode haver await — aqui não há nenhum, e a
        // montagem é síncrona de propósito.
        Pacientes.Clear();
        foreach (var p in _todos)
        {
            if (SomenteSumidos && !p.Sumido) continue;
            Pacientes.Add(p);
        }

        var termo = Termo.Trim();

        // ⚠️ TRÊS vazios, não um. Quando a busca TROUXE gente e o "só quem sumiu" a
        // escondeu, dizer "não achou ninguém no cadastro" seria mentir sobre a causa — e
        // mandaria cadastrar de novo alguém que a tela acabou de achar. Quem esconde
        // responde por si (a regra de "um estado vazio por pergunta", parcela 37).
        if (_todos.Count > 0 && Pacientes.Count == 0)
        {
            TituloVazio = "Ninguém está sem vir há mais tempo";
            DescricaoVazio =
                $"Os {_todos.Count} paciente(s) desta lista vieram nos últimos "
                + $"{LinhaPacienteClinico.DiasParaDestaque} dias. Desmarque "
                + "“Só quem sumiu” para ver todos.";
        }
        else if (termo.Length > 0)
        {
            TituloVazio = "Ninguém com esse nome ou CPF";
            DescricaoVazio =
                $"A busca “{termo}” não achou ninguém no cadastro da clínica — nem entre "
                + "quem já veio, nem entre quem ainda não veio. Confira a grafia antes de "
                + "cadastrar de novo: ficha repetida parte o histórico do paciente em duas.";
        }
        else
        {
            TituloVazio = "Nenhum paciente em tratamento";
            DescricaoVazio =
                "Entra aqui quem a clínica já atendeu: o horário precisa ter tido a presença "
                + "confirmada na recepção. Busque pelo nome ou CPF para alcançar quem ainda "
                + "não veio.";
        }

        if (termo.Length > 0)
        {
            Resumo = $"Busca “{termo}” — {Pacientes.Count} paciente(s) no cadastro da clínica."
                     + (SomenteSumidos ? " Só quem está sem vir há mais tempo." : string.Empty);
            return;
        }

        // ⚠️ O teto é DITO. Uma lista cortada que se anuncia como a carteira inteira faz
        // quem não achou alguém concluir que ele não está cadastrado — corte silencioso é
        // o que este projeto recusa desde sempre.
        var cortada = _todos.Count >= TetoDaCarteira;

        var quantos = SomenteSumidos
            ? $"{Pacientes.Count} de {_todos.Count} paciente(s), só quem está sem vir há mais tempo"
            : $"{_todos.Count} paciente(s) atendidos na clínica, do que veio por último ao mais antigo";

        Resumo = cortada
            ? quantos + $" — os {TetoDaCarteira} mais recentes, que é o teto desta tela. "
              + "Busque pelo nome ou CPF para alcançar quem está fora, inclusive quem nunca veio."
            : quantos + ".";
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
