using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Clinico.Janelas;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>Uma sessão do prontuário, como a lista do consultório a mostra.</summary>
public sealed class LinhaSessaoProntuario
{
    public required int EvolucaoId { get; init; }
    public required string Data { get; init; }
    public required string Profissional { get; init; }
    public required string Eva { get; init; }
    public required string Queixa { get; init; }
    public required string Conduta { get; init; }
    public required string Evolucao { get; init; }
    public required string Orientacoes { get; init; }

    /// <summary>Quantos arquivos a sessão tem — o clipe da linha.</summary>
    public required int Anexos { get; init; }

    public bool TemAnexos => Anexos > 0;

    public string AnexosTexto => Anexos switch
    {
        0 => "Anexos",
        1 => "1 anexo",
        _ => $"{Anexos} anexos"
    };

    public static LinhaSessaoProntuario De(Evolucao e, int anexos) => new()
    {
        EvolucaoId = e.Id,
        Data = e.Data.ToString("dd/MM/yyyy"),
        Profissional = e.Profissional?.Nome ?? "—",
        Eva = e.TemParEva ? $"EVA {e.EvaAntes} → {e.EvaDepois}" : "EVA não medida",
        Queixa = string.IsNullOrWhiteSpace(e.QueixaPrincipal) ? "—" : e.QueixaPrincipal!,
        Conduta = string.IsNullOrWhiteSpace(e.Conduta) ? "—" : e.Conduta!,
        Evolucao = string.IsNullOrWhiteSpace(e.TextoEvolucao) ? "—" : e.TextoEvolucao!,
        Orientacoes = string.IsNullOrWhiteSpace(e.Orientacoes) ? "—" : e.Orientacoes!,
        Anexos = anexos
    };
}

/// <summary>Uma linha da lista de problemas, com o que a tela precisa desenhar.</summary>
public sealed class LinhaProblema
{
    public required ProblemaPaciente Fonte { get; init; }
    public required string Rotulo { get; init; }
    public required string Natureza { get; init; }
    public required string Periodo { get; init; }
    public required string Observacoes { get; init; }
    public required string Situacao { get; init; }
    public required bool Ativo { get; init; }
    public required bool Alerta { get; init; }
    public required bool Descartado { get; init; }

    private static string Natu(NaturezaProblema n) => n switch
    {
        NaturezaProblema.Alergia => "Alergia",
        NaturezaProblema.Antecedente => "Antecedente",
        NaturezaProblema.MedicacaoContinua => "Uso contínuo",
        _ => "Diagnóstico"
    };

    public static LinhaProblema De(ProblemaPaciente p) => new()
    {
        Fonte = p,
        Rotulo = p.Rotulo,
        Natureza = Natu(p.Natureza),
        Periodo = p switch
        {
            { Inicio: { } i, Fim: { } f } => $"de {i:dd/MM/yyyy} a {f:dd/MM/yyyy}",
            { Inicio: { } i } => $"desde {i:dd/MM/yyyy}",
            { Fim: { } f } => $"encerrado em {f:dd/MM/yyyy}",
            _ => "sem data informada"
        },
        Observacoes = string.IsNullOrWhiteSpace(p.Observacoes) ? string.Empty : p.Observacoes!,
        Situacao = p.Situacao switch
        {
            SituacaoProblema.Resolvido => "Resolvido",
            // O motivo vai JUNTO da situação: linha descartada sem o porquê deixa o
            // próximo leitor sem saber se ela era falsa ou se o paciente melhorou.
            SituacaoProblema.Descartado => string.IsNullOrWhiteSpace(p.MotivoDescarte)
                ? "Descartado"
                : $"Descartado — {p.MotivoDescarte}",
            _ => "Ativo"
        },
        Ativo = p.EstaAtivo,
        Alerta = p.EhAlertaDeAtendimento,
        Descartado = p.Situacao == SituacaoProblema.Descartado
    };
}

/// <summary>
/// O PRONTUÁRIO COMPLETO na máquina de quem atende (parcela 37).
///
/// Por que existe
/// --------------
/// A tela de Atendimento mostra as TRÊS últimas sessões ao lado do formulário, que é o
/// certo para escrever a de hoje. Num tratamento de quarenta, a sessão 12 era
/// inalcançável — e a busca por texto dentro do prontuário existia, testada e em uso,
/// dentro do módulo da RECEPÇÃO. O comentário que a acompanha lá diz, com todas as letras:
/// "a pergunta que o profissional faz antes de atender é sempre a mesma: o que eu fiz da
/// última vez que ele veio com dor no ombro?". A feature foi justificada pelo profissional
/// e entregue na tela de quem não atende.
///
/// O que esta tela acrescenta ao que a Recepção já fazia
/// ----------------------------------------------------
/// - A <b>lista de problemas</b> (parcela 37), que é do consultório por natureza: quem
///   registra alergia e diagnóstico é quem examina.
/// - Os <b>anexos</b> por sessão, que fecham o circuito aberto pelo próprio módulo — ele
///   emitia pedido de exame desde a parcela 36 e não tinha onde ler o laudo de volta.
/// - A sessão inteira ABERTA na lista (queixa, conduta, evolução, orientações), em vez de
///   uma linha que se clica para editar: aqui a leitura é o uso principal, e no balcão o
///   uso principal é a manutenção do registro.
///
/// A contagem de anexos sai em UMA consulta (<c>ContagemDeAnexosAsync</c>). Perguntar
/// sessão a sessão, como a Recepção ainda fazia, dá quarenta idas a um banco remoto para
/// desenhar quarenta números.
/// </summary>
public sealed partial class ProntuarioClinicoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;
    private readonly PacienteEmFoco _foco;

    public SeletorPacienteViewModel Seletor { get; }

    public ObservableCollection<LinhaSessaoProntuario> Sessoes { get; } = [];

    public ObservableCollection<LinhaProblema> Problemas { get; } = [];

    /// <summary>
    /// Busca por texto dentro do prontuário — a pergunta central de quem vai atender.
    /// Sem acento e sem caixa: quem digita escreve "lombalgia" e o prontuário diz
    /// "Lombalgia", e uma busca que erra por isso é uma busca que ninguém usa duas vezes.
    /// </summary>
    [ObservableProperty] private string _termoSessao = string.Empty;

    /// <summary>O que a lista está mostrando. Filtro invisível é filtro que engana.</summary>
    [ObservableProperty] private string _resumoSessoes = string.Empty;

    /// <summary>Mostrar também o que foi resolvido e descartado na lista de problemas.</summary>
    [ObservableProperty] private bool _incluirProblemasEncerrados;

    [ObservableProperty] private string _resumoProblemas = string.Empty;

    [ObservableProperty] private string _paciente = string.Empty;
    [ObservableProperty] private bool _semPaciente = true;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Metade VISÍVEL da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditarProntuario => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    public ProntuarioClinicoViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar,
        IDialogoService dialogo, PacienteEmFoco foco)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _foco = foco;

        Seletor = new SeletorPacienteViewModel(escopos);
        Seletor.SelecaoMudou += p =>
        {
            if (p is null) return;
            _foco.Definir(p.Id, p.Nome);
            // Trocar de paciente limpa a busca: "ombro" digitado para um não é pergunta
            // sobre o próximo, e a lista voltaria filtrada sem ninguém entender por quê.
            TermoSessao = string.Empty;
            _ = CarregarAsync();
        };

        if (_foco.Definido) _ = CarregarAsync();
    }

    private int PacienteId => _foco.PacienteId ?? 0;

    partial void OnTermoSessaoChanged(string value) => _ = CarregarAsync();

    partial void OnIncluirProblemasEncerradosChanged(bool value) => _ = CarregarAsync();

    [RelayCommand]
    public async Task CarregarAsync()
    {
        SemPaciente = PacienteId == 0;
        Sessoes.Clear();
        Problemas.Clear();

        if (SemPaciente)
        {
            Paciente = string.Empty;
            return;
        }

        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;
            Paciente = _foco.Nome;

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();

            var todas = await prontuario.DoPacienteAsync(PacienteId);
            var termo = TermoSessao.Trim();

            var filtradas = termo.Length == 0
                ? todas.ToList()
                : todas.Where(e => Casa(e, termo)).ToList();

            // Uma consulta para o prontuário inteiro — e só sobre o que vai à tela.
            var contagens = await prontuario.ContagemDeAnexosAsync(
                filtradas.Select(e => e.Id).ToList());

            foreach (var e in filtradas)
                Sessoes.Add(LinhaSessaoProntuario.De(
                    e, contagens.TryGetValue(e.Id, out var quantos) ? quantos : 0));

            ResumoSessoes = termo.Length == 0
                ? $"{todas.Count} sessão(ões) no prontuário."
                // A lista filtrada DIZ que está filtrada, e diz sobre quantas: "3 sessões"
                // sozinho faria o profissional concluir que o paciente veio três vezes.
                : $"{Sessoes.Count} de {todas.Count} sessão(ões) contêm “{termo}”.";

            await CarregarProblemasAsync(scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — prontuário não pôde ser carregado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>
    /// A lista de problemas falha SOZINHA: o prontuário não pode deixar de abrir porque
    /// uma consulta quebrou. É a mesma regra dos blocos do painel da direção.
    /// </summary>
    private async Task CarregarProblemasAsync(IServiceProvider servicos)
    {
        try
        {
            var servico = servicos.GetRequiredService<ProblemaPacienteService>();
            var lista = await servico.DoPacienteAsync(
                PacienteId, somenteAtivos: !IncluirProblemasEncerrados);

            foreach (var p in lista) Problemas.Add(LinhaProblema.De(p));

            var alertas = Problemas.Count(p => p.Alerta);
            ResumoProblemas = Problemas.Count == 0
                ? (IncluirProblemasEncerrados
                    ? "Nenhum problema registrado para este paciente."
                    : "Nenhum problema ATIVO. Pode haver linhas resolvidas ou descartadas — "
                      + "marque a caixa para vê-las.")
                : $"{Problemas.Count} linha(s)"
                  + (alertas > 0 ? $", {alertas} com alerta de atendimento." : ".");
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — lista de problemas não pôde ser lida", ex);

            // Terceiro estado, dito no lugar onde o profissional olha: lista vazia por
            // falha não pode se parecer com "este paciente não tem alergia nenhuma".
            ResumoProblemas = "Não foi possível ler a lista de problemas deste paciente — "
                              + "ela está vazia por falha de leitura, não porque não haja nada.";
        }
    }

    /// <summary>A sessão contém o termo em algum dos campos escritos.</summary>
    private static bool Casa(Evolucao e, string termo)
    {
        var alvo = Normalizar(string.Join(" ",
            e.QueixaPrincipal, e.Conduta, e.TextoEvolucao, e.Orientacoes));
        return alvo.Contains(Normalizar(termo), StringComparison.Ordinal);
    }

    private static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var decomposto = texto.Normalize(System.Text.NormalizationForm.FormD);
        var limpo = new System.Text.StringBuilder(decomposto.Length);

        foreach (var c in decomposto)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                limpo.Append(char.ToLowerInvariant(c));

        return limpo.ToString();
    }

    // ---------------------------------------------------------------- anexos

    /// <summary>
    /// Abre os anexos de uma sessão — o laudo que voltou do exame que ESTE app pediu.
    /// </summary>
    [RelayCommand]
    private async Task VerAnexosAsync(LinhaSessaoProntuario? linha)
    {
        if (linha is null) return;

        try
        {
            var vm = new AnexosSessaoViewModel(
                _escopos, linha.EvolucaoId, $"Sessão de {linha.Data} — {Paciente}");

            new AnexosSessaoWindow(vm)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            }.ShowDialog();

            // A contagem do clipe muda com o que aconteceu na janela.
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — anexos não puderam ser abertos", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    // ------------------------------------------------------ lista de problemas

    [RelayCommand]
    private Task NovoProblemaAsync() => AbrirProblemaAsync(null);

    [RelayCommand]
    private Task EditarProblemaAsync(LinhaProblema? linha)
        => linha is null ? Task.CompletedTask : AbrirProblemaAsync(linha.Fonte);

    private async Task AbrirProblemaAsync(ProblemaPaciente? existente)
    {
        if (PacienteId == 0) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            var vm = new ProblemaEdicaoViewModel(_escopos, PacienteId, existente);
            var janela = new ProblemaWindow(vm)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };

            if (janela.ShowDialog() != true) return;

            _snackbar.Sucesso("Lista de problemas atualizada.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — problema não pôde ser aberto", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    [RelayCommand]
    private async Task ResolverProblemaAsync(LinhaProblema? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            if (!_dialogo.Confirmar("Marcar como resolvido",
                    $"Encerrar “{linha.Rotulo}” com a data de hoje? A linha continua no "
                    + "prontuário — ela sai da lista de ativos, não da base."))
                return;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<ProblemaPacienteService>();
            await servico.ResolverAsync(
                linha.Fonte.Id, operador: SessaoUsuario.Atual.Operador);

            _snackbar.Info("Problema marcado como resolvido.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — problema não pôde ser resolvido", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Desdiz a linha. O motivo é PEDIDO, e o serviço o exige — sem ele, quem ler depois
    /// não sabe se a linha era falsa ou se o paciente melhorou.
    /// </summary>
    [RelayCommand]
    private async Task DescartarProblemaAsync(LinhaProblema? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            var motivo = _dialogo.PerguntarTexto(
                "Descartar do prontuário",
                $"Por que “{linha.Rotulo}” está sendo descartado? A linha não é apagada — "
                + "ela esteve no prontuário, e conduta pode ter sido tomada com base nela.");

            if (string.IsNullOrWhiteSpace(motivo)) return;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<ProblemaPacienteService>();
            await servico.DescartarAsync(
                linha.Fonte.Id, motivo, SessaoUsuario.Atual.Operador);

            _snackbar.Info("Linha descartada, com o motivo registrado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — problema não pôde ser descartado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    [RelayCommand]
    private async Task ReabrirProblemaAsync(LinhaProblema? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<ProblemaPacienteService>();
            await servico.ReabrirAsync(linha.Fonte.Id, SessaoUsuario.Atual.Operador);

            _snackbar.Info("Problema reaberto.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — problema não pôde ser reaberto", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>Abre a tela de medidas do mesmo paciente, sem perder o foco do posto.</summary>
    [RelayCommand]
    private void VerMedidas() => NavegacaoSuite.Ir(ModuloClinico.ChaveMedidas);
}
