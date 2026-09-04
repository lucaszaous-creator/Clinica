using System.Collections.ObjectModel;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Clinico.Janelas;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

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
/// A CAPA DO PACIENTE (mockup 01, seção "Paciente").
///
/// Responde as perguntas que até aqui obrigavam a sair do Consultório e abrir a ficha na
/// Recepção: quem é, o que ela tem e o que está assinado em nome dela. É a única tela NOVA
/// do redesenho — as outras seis são reforma.
///
/// ⚠️ É SÓ LEITURA do cadastro. Telefone, convênio e carteirinha continuam sendo editados
/// no balcão, que é quem tem o paciente na frente e a permissão para isso — duas portas de
/// edição do mesmo cadastro divergem na primeira correção, e a de cá seria a que ninguém
/// lembraria de ajustar.
///
/// ⚠️ A lista de problemas foi MOVIDA para cá, não copiada: ela morava no Histórico, que
/// respondia quatro perguntas empilhadas. Ela é atributo da PESSOA — é ela que acende a
/// alergia no crachá e recusa a assinatura de uma prescrição —, e é aqui que se lê "o que
/// este paciente tem".
/// </summary>
public sealed partial class PacienteCapaViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;
    private readonly PacienteEmFoco _foco;

    public ObservableCollection<LinhaProblema> Problemas { get; } = [];

    /// <summary>Mostrar também o que foi resolvido e descartado na lista de problemas.</summary>
    [ObservableProperty] private bool _incluirProblemasEncerrados;

    [ObservableProperty] private string _resumoProblemas = string.Empty;

    // ---- A ficha, em leitura ----
    [ObservableProperty] private string _paciente = string.Empty;
    [ObservableProperty] private bool _semPaciente = true;
    [ObservableProperty] private string _nascimento = "—";
    [ObservableProperty] private string _documento = "—";
    [ObservableProperty] private string _telefone = "—";
    [ObservableProperty] private string _convenio = "—";
    [ObservableProperty] private string _carteirinha = "—";
    [ObservableProperty] private string _validadeCarteirinha = "—";
    [ObservableProperty] private string _emTratamento = "—";

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Metade VISÍVEL da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditarProntuario => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    /// <summary>
    /// A ficha traz contato e convênio, que são dado CADASTRAL — o corte da parcela 49.
    /// Quem só tem <c>VerProntuario</c> (dado de saúde) não vê o telefone; quem só tem
    /// <c>VerFichaPaciente</c> não vê a lista de problemas.
    /// </summary>
    public bool PodeVerFicha => SessaoUsuario.Atual.Pode(Permissao.VerFichaPaciente);

    public bool PodeVerProntuario => SessaoUsuario.Atual.Pode(Permissao.VerProntuario);

    public bool TemPaciente => !SemPaciente;

    /// <summary>
    /// Sem paciente escolhido não há lista de problemas onde escrever, e o botão diz isso
    /// apagado — a tela abre pela sidebar sem ninguém em foco, e botão aceso que não faz
    /// nada faz quem clica concluir que o sistema quebrou (parcela 41).
    /// </summary>
    public bool PodeNovoProblema => TemPaciente && PodeEditarProntuario;

    partial void OnSemPacienteChanged(bool value)
    {
        OnPropertyChanged(nameof(TemPaciente));
        OnPropertyChanged(nameof(PodeNovoProblema));
    }

    public PacienteCapaViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar,
        IDialogoService dialogo, PacienteEmFoco foco)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _foco = foco;

        if (_foco.Definido) _ = CarregarAsync();
    }

    private int PacienteId => _foco.PacienteId ?? 0;

    partial void OnIncluirProblemasEncerradosChanged(bool value) => _ = CarregarAsync();

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 60): a capa recarrega a cada troca de
    /// paciente, e a resposta do paciente ANTERIOR chegando por último mostraria a lista de
    /// problemas de um sob o nome do outro — numa tela cuja razão de existir é dizer o que
    /// a pessoa tem, isso é do tipo que muda conduta.
    /// </summary>
    private int _geracaoCarga;

    /// <summary>
    /// Último paciente cujo acesso já foi registrado nesta tela.
    ///
    /// A trilha de LEITURA é registrada na TROCA de paciente, e não a cada
    /// <c>CarregarAsync</c>: marcar a caixa de "mostrar resolvidos" recarrega a lista, e
    /// uma linha de trilha por clique de filtro é trilha que ninguém consegue ler.
    /// </summary>
    private int _acessoRegistradoDe;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        SemPaciente = PacienteId == 0;
        Problemas.Clear();
        Paciente = _foco.Nome ?? string.Empty;

        if (SemPaciente)
        {
            ResumoProblemas = string.Empty;
            LimparFicha();
            return;
        }

        Carregando = true;
        NaoVerificado = false;

        try
        {
            using var scope = _escopos.CreateScope();

            // Quem abriu este prontuário, e quando (ponto 4 do compromisso de
            // conformidade). A lista de problemas é dado de SAÚDE, e a LGPD alcança a
            // leitura. Não bloqueia nem derruba a tela: o serviço engole a falha com
            // rastro.
            if (_acessoRegistradoDe != PacienteId && PodeVerProntuario)
            {
                _acessoRegistradoDe = PacienteId;
                await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(PacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.ProntuarioClinico);
            }

            await CarregarFichaAsync(scope.ServiceProvider, geracao);
            await CarregarProblemasAsync(scope.ServiceProvider, geracao);
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;
            Clinica.Application.Diagnostico.Registrar("Consultório — capa do paciente", ex);
            NaoVerificado = true;
        }
        finally
        {
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    private void LimparFicha()
    {
        Nascimento = Documento = Telefone = "—";
        Convenio = Carteirinha = ValidadeCarteirinha = EmTratamento = "—";
    }

    /// <summary>
    /// A ficha CADASTRAL, em leitura. Falha sozinha: a lista de problemas não pode deixar
    /// de abrir porque o cadastro não respondeu.
    /// </summary>
    private async Task CarregarFichaAsync(IServiceProvider servicos, int geracao)
    {
        LimparFicha();
        if (!PodeVerFicha) return;

        // ⚠️ A contagem de sessões e o "paciente desde" saem do MESMO lugar que o crachá do
        // topo (ConsultorioService.CabecalhoAsync). Uma segunda conta aqui divergiria da
        // que está desenhada duas linhas acima, na mesma tela — e a que ninguém lembraria
        // de ajustar seria esta.
        var consultorio = servicos.GetRequiredService<ConsultorioService>();
        var cabecalho = await consultorio.CabecalhoAsync(PacienteId);

        var repo = servicos.GetRequiredService<IClinicaRepositorio>();
        var p = await repo.ObterPacienteAsync(PacienteId);

        if (geracao != _geracaoCarga) return;

        if (p is not null)
        {
            Nascimento = p.DataNascimento is { } nasc
                ? $"{nasc:dd/MM/yyyy}" + (cabecalho?.Idade is { } i ? $" · {i} anos" : string.Empty)
                : "não informado";

            // ⚠️ `Cpf.Formatar` fora dos 11 dígitos devolve SÓ OS DÍGITOS, e o campo se
            // chama Documento: a clínica cadastra RG, e "12.345.678-9" voltaria
            // "123456789". Quem resolve isso é `Paciente.DocumentoFormatado`.
            Documento = Ou(p.DocumentoFormatado, "não informado");
            Telefone = Ou(p.TelefoneFormatado, "não informado");
            Convenio = p.ConvenioNome;
            Carteirinha = Ou(p.Carteirinha, "não informada");
            ValidadeCarteirinha = p.ValidadeCarteirinha is { } val
                ? val.ToString("dd/MM/yyyy") + (p.CarteirinhaVencida ? " — VENCIDA" : string.Empty)
                : "sem validade informada";
        }

        EmTratamento = cabecalho is null
            ? "não foi possível conferir"
            : (cabecalho.PrimeiraSessao is { } primeira
                ? $"desde {primeira:dd/MM/yyyy}"
                : "ainda não foi atendida aqui")
              + (cabecalho.TotalSessoes > 0
                  ? $" · {(cabecalho.TotalSessoes == 1 ? "1 sessão" : $"{cabecalho.TotalSessoes} sessões")}"
                  : string.Empty);
    }

    /// <summary>Texto do banco, ou a frase que diz que ele não existe — nunca em branco.</summary>
    private static string Ou(string? valor, string quandoVazio)
        => string.IsNullOrWhiteSpace(valor) ? quandoVazio : valor!;

    /// <summary>
    /// A lista de problemas falha SOZINHA: o prontuário não pode deixar de abrir porque
    /// uma consulta quebrou. É a mesma regra dos blocos do painel da direção.
    /// </summary>
    private async Task CarregarProblemasAsync(IServiceProvider servicos, int geracao)
    {
        try
        {
            var servico = servicos.GetRequiredService<ProblemaPacienteService>();
            var lista = await servico.DoPacienteAsync(
                PacienteId, somenteAtivos: !IncluirProblemasEncerrados);

            // Chegou tarde: outra carga já está no ar, e a lista é dela.
            if (geracao != _geracaoCarga) return;

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

            if (geracao != _geracaoCarga) return;

            // Terceiro estado, dito no lugar onde o profissional olha: lista vazia por
            // falha não pode se parecer com "este paciente não tem alergia nenhuma".
            ResumoProblemas = "Não foi possível ler a lista de problemas deste paciente — "
                              + "ela está vazia por falha de leitura, não porque não haja nada.";
        }
    }

    [RelayCommand]
    private Task NovoProblemaAsync() => AbrirProblemaAsync(null);

    [RelayCommand]
    private Task EditarProblemaAsync(LinhaProblema? linha)
        => linha is null ? Task.CompletedTask : AbrirProblemaAsync(linha.Fonte);

    private async Task AbrirProblemaAsync(ProblemaPaciente? existente)
    {
        if (PacienteId == 0)
        {
            // A guarda DIZ por que não dá, em vez de voltar calada (parcela 41): o botão
            // apagado explica, e esta é a metade que impede quem chega por atalho.
            Mensagem = "Escolha um paciente antes de registrar um problema — a lista é do "
                     + "prontuário de alguém.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            var vm = new ProblemaEdicaoViewModel(_escopos, PacienteId, existente);
            var janela = new ProblemaWindow(vm)
            {
                Owner = JanelaDona.Atual()
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

    private static int Idade(DateOnly nascimento)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var idade = hoje.Year - nascimento.Year;
        if (nascimento > hoje.AddYears(-idade)) idade--;
        return idade;
    }
}
