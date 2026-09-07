using System.Collections.ObjectModel;
using System.IO;
using Clinica.Application;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// ESCREVER UMA SESSÃO fora do posto — a porta do balcão e da ficha do paciente
/// (set/2026), com a MESMA folha da tela de Atendimento.
///
/// O que ela substituiu
/// --------------------
/// A Recepção tinha uma janela própria (<c>EvolucaoWindow</c>): duas colunas, o mapa
/// corporal aberto num painel fixo de 530 px e QUATRO dos doze campos da sessão. Ela
/// preservava os outros oito na gravação (a regra do lugar 6), e é por isso que a
/// divergência nunca virou perda de dado — nem apareceu para ninguém. A cliente viu a
/// tela: <i>"esse modal aí não está condizente com o do atendimento médico/enfermagem"</i>.
///
/// ⚠️ O que esta janela acrescenta à folha é o que só existe FORA do posto, e cada coisa
/// tem razão:
///
/// <list type="bullet">
///   <item><b>Quem atendeu</b> — no Consultório é sempre quem fez login; aqui a
///   recepcionista registra a sessão de outra pessoa, e sem o campo a sessão nasceria
///   assinada por ela (o repasse lê o profissional, e a auditoria também).</item>
///   <item><b>Os anexos</b> — a foto da região e o laudo, que o balcão anexa desde a
///   parcela 2. Tirá-los seria tirar capacidade de quem a usa (a regra 3).</item>
/// </list>
///
/// ⚠️ Salvar NÃO fecha, como na folha do Consultório: o rodapé passa a dizer a hora da
/// última gravação, e é isso que destrava o "Anexar" numa sessão nova — o anexo aponta
/// para a evolução, e antes de ela existir não há a que se prender.
/// </summary>
public sealed partial class EscreverSessaoViewModel : FolhaDaSessaoViewModel
{
    private readonly IDialogoService _dialogo;

    /// <summary>Quem pode ter atendido — a equipe ativa. Vazia enquanto a carga não volta.</summary>
    public ObservableCollection<Profissional> Profissionais { get; } = [];

    public ObservableCollection<AnexoResumo> Anexos { get; } = [];

    /// <summary>
    /// Quem atendeu. É o gancho <see cref="ProfissionalDaSessao"/> desta porta — no
    /// Consultório ele é o do login, e aqui é uma escolha.
    /// </summary>
    [ObservableProperty] private Profissional? _profissional;

    [ObservableProperty] private string _titulo = "Nova sessão no prontuário";

    /// <summary>
    /// A última sessão numa linha, acima da folha — a MESMA frase que o Consultório
    /// mostra (<see cref="ResumoSessaoAnterior.ContextoDaUltima"/>). Ela responde "por que
    /// este paciente está aqui hoje", e não pode custar um clique.
    /// </summary>
    [ObservableProperty] private string _contexto = string.Empty;

    /// <summary>
    /// Anexar só é possível depois de a sessão existir: o anexo aponta para a evolução, e
    /// num registro novo o Id ainda não nasceu.
    /// </summary>
    public bool PodeAnexar => EvolucaoId != 0;

    /// <summary>A janela fecha por aqui, e o chamador relê quando alguma gravação houve.</summary>
    public bool Gravou { get; private set; }

    /// <summary>
    /// Quem a sessão dizia ter atendido quando ela foi aberta.
    ///
    /// ⚠️ Ele existe porque o combo lista só a equipe ATIVA: a sessão escrita por quem
    /// saiu da clínica abriria com o campo em branco, e o Salvar gravaria
    /// <c>ProfissionalId = null</c> — apagando quem atendeu numa edição que ninguém pediu.
    /// É "quem não edita, PRESERVA" (parcela 74) na variante em que o campo ESTÁ na tela e
    /// o valor é que não está na lista. O repasse e a auditoria leem essa coluna.
    /// </summary>
    private int? _profissionalOriginal;

    protected override int? ProfissionalDaSessao => Profissional?.Id ?? _profissionalOriginal;

    protected override string ContextoDoLog => "Prontuário";

    public EscreverSessaoViewModel(
        IServiceScopeFactory escopos, IDialogoService dialogo,
        int pacienteId, string paciente, int? evolucaoId = null)
        : base(escopos)
    {
        _dialogo = dialogo;
        PacienteId = pacienteId;
        Paciente = paciente;
        SemPaciente = pacienteId == 0;
        EvolucaoId = evolucaoId ?? 0;

        // ⚠️ O `partial void OnEvolucaoIdChanged` é gerado NA BASE e a derivada não pode
        // implementá-lo — o "Anexar" ficaria apagado para sempre depois do primeiro
        // Salvar, que é justamente quando ele passa a valer.
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EvolucaoId)) OnPropertyChanged(nameof(PodeAnexar));
        };

        _ = CarregarAsync();
    }

    /// <summary>
    /// Monta a folha: a equipe, os campos personalizados, o mapa e — quando é edição — a
    /// sessão que está sendo corrigida.
    ///
    /// ⚠️ TUDO no mesmo escopo e SEQUENCIAL, nunca <c>WhenAll</c>: é o mesmo
    /// <c>DbContext</c>, e o SQLite dos testes esconde a sobreposição (parcela 74).
    /// </summary>
    private async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        try
        {
            Carregando = true;

            using var scope = _escopos.CreateScope();
            var servicos = scope.ServiceProvider;

            var equipe = await servicos.GetRequiredService<EquipeService>().ProfissionaisAtivosAsync();
            if (geracao != _geracaoCarga) return;

            Profissionais.Clear();
            foreach (var p in equipe) Profissionais.Add(p);

            await CarregarCamposPersonalizadosAsync(scope, geracao);
            if (geracao != _geracaoCarga) return;

            var prontuario = servicos.GetRequiredService<ProntuarioService>();

            // As anteriores alimentam o "Repetir a última" (dentro de Modelos…) e a linha
            // de contexto. A sessão que está sendo EDITADA não conta como anterior de si
            // mesma — ela apareceria como "a última" e o botão repetiria o próprio texto.
            var sessoes = await prontuario.DoPacienteAsync(PacienteId);
            if (geracao != _geracaoCarga) return;

            var evolucao = EvolucaoId == 0 ? null : sessoes.FirstOrDefault(e => e.Id == EvolucaoId);
            if (evolucao is not null)
            {
                Titulo = "Editar sessão do prontuário";
                Preencher(evolucao);
                _profissionalOriginal = evolucao.ProfissionalId;
                Profissional = Profissionais.FirstOrDefault(p => p.Id == evolucao.ProfissionalId);
                await RecarregarAnexosAsync(prontuario);
                if (geracao != _geracaoCarga) return;
            }

            Anteriores.Clear();
            foreach (var e in sessoes.Where(e => e.Id != EvolucaoId).Take(5))
                Anteriores.Add(ResumoSessaoAnterior.De(e));

            Contexto = ResumoSessaoAnterior.ContextoDaUltima(Anteriores.FirstOrDefault());

            // O mapa vem depois de resolvida a sessão: ele carrega os pontos DELA quando é
            // edição, e os protocolos quando é nova.
            var mapa = new MapaCorporalViewModel(_escopos, PacienteId, EvolucaoId == 0 ? null : EvolucaoId);
            await mapa.CarregarAsync();
            if (geracao != _geracaoCarga) return;
            Mapa = mapa;
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            Diagnostico.Registrar("Prontuário — a sessão não pôde ser aberta", ex);
            Mensagem = $"Não foi possível carregar a sessão: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    /// <summary>
    /// Depois de gravar: a janela CONTINUA aberta (é o que destrava o anexo numa sessão
    /// nova) e o chamador passa a ter o que reler quando ela fechar.
    /// </summary>
    protected override async Task DepoisDeSalvarAsync()
    {
        Gravou = true;
        OnPropertyChanged(nameof(PodeAnexar));

        using var scope = _escopos.CreateScope();
        await RecarregarAnexosAsync(scope.ServiceProvider.GetRequiredService<ProntuarioService>());
    }

    private async Task RecarregarAnexosAsync(ProntuarioService prontuario)
    {
        if (EvolucaoId == 0)
        {
            Anexos.Clear();
            return;
        }

        // Monta fora e publica de uma vez: entre o Clear() e o último Add não pode haver
        // await (parcela 62).
        var lidos = await prontuario.AnexosAsync(EvolucaoId);
        Anexos.Clear();
        foreach (var a in lidos) Anexos.Add(a);
    }

    /// <summary>Anexa um arquivo (foto da região, laudo, exame) à sessão.</summary>
    [RelayCommand]
    private async Task AnexarAsync()
    {
        if (EvolucaoId == 0)
        {
            // Guarda que DIZ por que não dá — o botão apagado explica, esta impede.
            Mensagem = "Salve a sessão antes de anexar arquivos: o anexo se prende à sessão, "
                       + "e ela ainda não existe.";
            MensagemEhErro = true;
            return;
        }

        var dialogo = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Anexar ao prontuário",
            Filter = "Imagens, documentos e mídia "
                     + "(*.jpg;*.jpeg;*.png;*.pdf;*.mp4;*.mov;*.m4a;*.mp3)"
                     + "|*.jpg;*.jpeg;*.png;*.pdf;*.mp4;*.mov;*.m4a;*.mp3"
                     + "|Todos os arquivos (*.*)|*.*"
        };
        if (dialogo.ShowDialog() != true) return;

        try
        {
            // Escrita de prontuário: mesma barreira do Salvar.
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            var bytes = await File.ReadAllBytesAsync(dialogo.FileName);
            var nome = Path.GetFileName(dialogo.FileName);
            var tipoConteudo = MidiaClinica.MimeDe(nome);

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();

            // Tipo pelo arquivo, e quem decide banco × armazenamento é o TAMANHO — a mesma
            // porta do Consultório (set/2026). A cópia que ficasse para trás recusaria o
            // vídeo justamente na tela do balcão, onde o paciente está na frente.
            await prontuario.AnexarAsync(
                EvolucaoId, nome, bytes,
                MidiaClinica.TipoDe(tipoConteudo, nome),
                tipoConteudo: tipoConteudo,
                operador: SessaoUsuario.Atual.Operador,
                midia: scope.ServiceProvider.GetRequiredService<MidiaProntuarioService>());

            await RecarregarAnexosAsync(prontuario);
            Mensagem = "Arquivo anexado.";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Prontuário — anexo não pôde ser gravado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>Salva o anexo em disco para abrir no programa padrão do Windows.</summary>
    [RelayCommand]
    private async Task BaixarAnexoAsync(AnexoResumo? anexo)
    {
        if (anexo is null) return;

        var dialogo = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Salvar anexo",
            FileName = anexo.NomeArquivo
        };
        if (dialogo.ShowDialog() != true) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();
            var bytes = await prontuario.ConteudoAnexoAsync(
                anexo.Id, scope.ServiceProvider.GetRequiredService<MidiaProntuarioService>());

            // TRILHA DE LEITURA (parcela 62): laudo e imagem de exame saindo para o disco é
            // dado de saúde deixando o sistema — o que uma investigação procura primeiro.
            await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                .RegistrarAsync(PacienteId, SessaoUsuario.Atual.Operador,
                    OrigemAcessoProntuario.ExportacaoClinica);

            if (bytes is null)
            {
                Mensagem = "O arquivo não foi encontrado. Se ele é um vídeo ou áudio, confira "
                           + "o armazenamento da clínica em Configurações.";
                MensagemEhErro = true;
                return;
            }

            await File.WriteAllBytesAsync(dialogo.FileName, bytes);
            Mensagem = $"Anexo salvo em {dialogo.FileName}.";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Prontuário — anexo não pôde ser salvo em disco", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    [RelayCommand]
    private async Task RemoverAnexoAsync(AnexoResumo? anexo)
    {
        if (anexo is null) return;

        try
        {
            // Escrita de prontuário: mesma barreira do Salvar.
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            // Retirar, nunca apagar (parcela 52): o laudo que sustentou uma conduta é parte
            // da prova de que ela era razoável, e a guarda de 20 anos não admite que um
            // clique o destrua.
            var motivo = _dialogo.PerguntarTexto(
                "Retirar anexo",
                $"Por que \"{anexo.NomeArquivo}\" está saindo do prontuário? O arquivo NÃO é "
                + "apagado — sai da lista e fica guardado, com este motivo.");
            if (string.IsNullOrWhiteSpace(motivo)) return;

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();
            await prontuario.CancelarAnexoAsync(anexo.Id, motivo, SessaoUsuario.Atual.Operador);
            await RecarregarAnexosAsync(prontuario);
            Mensagem = "Anexo retirado (guardado no prontuário).";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Prontuário — anexo não pôde ser removido", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
