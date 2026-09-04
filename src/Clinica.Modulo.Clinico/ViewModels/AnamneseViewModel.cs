using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Controls;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>Uma versão anterior da anamnese, como a tela a mostra.</summary>
public sealed class LinhaVersaoAnamnese
{
    public required string Titulo { get; init; }
    public required string Quando { get; init; }
    public required string Motivo { get; init; }
    public required string Texto { get; init; }
}

/// <summary>
/// Uma âncora da anamnese: o rótulo, se ela é a aberta e se ainda está em branco.
/// </summary>
public sealed partial class ChipAnamnese : ObservableObject
{
    public required string Chave { get; init; }
    public required string Rotulo { get; init; }

    [ObservableProperty] private bool _marcado;

    /// <summary>Nada escrito ainda — o chip diz isso em vez de a página abrir um vão.</summary>
    [ObservableProperty] private bool _emBranco = true;

    public string Texto => EmBranco ? $"{Rotulo} · em branco" : Rotulo;

    partial void OnEmBrancoChanged(bool value) => OnPropertyChanged(nameof(Texto));
}

/// <summary>
/// A ANAMNESE do paciente na tela de quem atende (parcela 75).
///
/// Ver <see cref="AnamnesePaciente"/> para por que ela não é a evolução com mais campos nem
/// a lista de problemas com texto livre.
///
/// O que a tela decide, e que o serviço não decide
/// ----------------------------------------------
/// - <b>Ela abre em modo de LEITURA.</b> A anamnese é escrita uma vez e lida dezenas; abrir
///   seis caixas de texto editáveis toda vez convida à edição acidental de um registro que
///   versiona a cada gravação — e encheria o histórico de versões idênticas.
/// - <b>O botão diz o que vai acontecer</b>: "Colher anamnese" quando não há, "Revisar"
///   quando há. São atos diferentes e a trilha os separa.
/// - <b>Nunca some quando a leitura falha</b>: terceiro estado, porque anamnese ausente por
///   erro de banco se leria como paciente sem antecedentes.
/// </summary>
public sealed partial class AnamneseViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly PacienteEmFoco _foco;
    private readonly IDialogoService _dialogo;

    /// <summary>Descarte de resposta fora de ordem — a regra da parcela 60.</summary>
    private int _geracaoCarga;

    public ObservableCollection<LinhaVersaoAnamnese> Versoes { get; } = [];

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Já foi colhida. Decide o rótulo do botão e o que a tela mostra.</summary>
    [ObservableProperty] private bool _colhida;

    /// <summary>Os campos estão editáveis. Ver o comentário da classe.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SomenteLeitura))]
    private bool _editando;

    /// <summary>
    /// O inverso de <see cref="Editando"/>, para o <c>IsReadOnly</c> dos campos.
    ///
    /// ⚠️ Mora aqui e não num conversor de propósito: ligar `IsReadOnly` a um
    /// `BooleanToVisibilityConverter` — que é o conversor que a mão escreve primeiro —
    /// devolveria um `Visibility` para uma propriedade `bool`, e o WPF trataria isso como
    /// falso SEMPRE. Os campos ficariam editáveis o tempo todo, sem erro nenhum. É a
    /// armadilha do conversor pelo tipo, do item 4 da auditoria de linha.
    /// </summary>
    public bool SomenteLeitura => !Editando;

    /// <summary>O inverso de <see cref="Colhida"/>, para o estado vazio da região.</summary>
    public bool NaoColhida => !Colhida;

    [ObservableProperty] private string _antecedentesPessoais = string.Empty;
    [ObservableProperty] private string _antecedentesFamiliares = string.Empty;
    [ObservableProperty] private string _habitosDeVida = string.Empty;
    [ObservableProperty] private string _historiaObstetrica = string.Empty;
    [ObservableProperty] private string _revisaoDeSistemas = string.Empty;
    [ObservableProperty] private string _observacoes = string.Empty;

    // ==================== AS ÂNCORAS (mockup 01) ====================
    //
    // Eram seis caixas de texto empilhadas numa rolagem: achar onde parou era rolar, e as
    // vazias ocupavam meia tela cada uma para não dizer nada. Agora a régua diz o que já
    // foi escrito e o que está em branco, e a folha mostra UMA seção — do tamanho da tela,
    // como a do atendimento.
    //
    // ⚠️ Os SEIS campos continuam sendo seis colunas no banco. Ao contrário da sessão, aqui
    // a divisão por assunto é usada de verdade: é assim que a anamnese sai no papel que o
    // convênio pede, separada por tópico.

    /// <summary>As seções da anamnese, na ordem em que se escreve.</summary>
    public ObservableCollection<ChipAnamnese> Secoes { get; } = [];

    [ObservableProperty] private string _secaoEscolhida = ChaveAntecedentesPessoais;

    public const string ChaveAntecedentesPessoais = "AntecedentesPessoais";
    public const string ChaveAntecedentesFamiliares = "AntecedentesFamiliares";
    public const string ChaveHabitosDeVida = "HabitosDeVida";
    public const string ChaveRevisaoDeSistemas = "RevisaoDeSistemas";
    public const string ChaveHistoriaObstetrica = "HistoriaObstetrica";
    public const string ChaveObservacoes = "Observacoes";

    /// <summary>
    /// O texto da seção escolhida — a folha. Ler e escrever caem no campo certo, e é isso
    /// que permite UMA caixa no lugar de seis.
    /// </summary>
    public string TextoDaSecao
    {
        get => SecaoEscolhida switch
        {
            ChaveAntecedentesFamiliares => AntecedentesFamiliares,
            ChaveHabitosDeVida => HabitosDeVida,
            ChaveRevisaoDeSistemas => RevisaoDeSistemas,
            ChaveHistoriaObstetrica => HistoriaObstetrica,
            ChaveObservacoes => Observacoes,
            _ => AntecedentesPessoais
        };
        set
        {
            // ⚠️ A marca existe para o gancho abaixo NÃO reemitir `TextoDaSecao` enquanto a
            // pessoa digita: o campo escreve no ViewModel a cada tecla, e devolver o valor
            // à caixa no meio da digitação é como se perde a posição do cursor.
            _escrevendoNaFolha = true;
            switch (SecaoEscolhida)
            {
                case ChaveAntecedentesFamiliares: AntecedentesFamiliares = value; break;
                case ChaveHabitosDeVida: HabitosDeVida = value; break;
                case ChaveRevisaoDeSistemas: RevisaoDeSistemas = value; break;
                case ChaveHistoriaObstetrica: HistoriaObstetrica = value; break;
                case ChaveObservacoes: Observacoes = value; break;
                default: AntecedentesPessoais = value; break;
            }
            _escrevendoNaFolha = false;
        }
    }

    private bool _escrevendoNaFolha;

    /// <summary>O rótulo e a dica da seção aberta — o que o rótulo da caixa dizia.</summary>
    public string RotuloDaSecao =>
        Secoes.FirstOrDefault(c => c.Chave == SecaoEscolhida)?.Rotulo ?? "Anamnese";

    public string DicaDaSecao => SecaoEscolhida switch
    {
        ChaveAntecedentesFamiliares =>
            "O que decide conduta é o parentesco e a idade do evento — “pai infartou aos 58” pesa diferente de “avô aos 89”.",
        ChaveHabitosDeVida => "Tabagismo, etilismo, atividade física, sono, alimentação.",
        ChaveRevisaoDeSistemas =>
            "A varredura por aparelhos que a primeira consulta faz e as seguintes não repetem.",
        ChaveHistoriaObstetrica => "Quando se aplica.",
        ChaveObservacoes =>
            "Profissão, com quem mora, quem cuida, o que a pessoa espera do tratamento.",
        _ => "Doenças prévias, internações, cirurgias, fraturas."
    };

    /// <summary>Trocar de seção pelo chip.</summary>
    [RelayCommand]
    private void AbrirSecao(ChipAnamnese? chip)
    {
        if (chip is null) return;
        SecaoEscolhida = chip.Chave;
    }

    partial void OnSecaoEscolhidaChanged(string value)
    {
        MarcarSecoes();
        OnPropertyChanged(nameof(TextoDaSecao));
        OnPropertyChanged(nameof(RotuloDaSecao));
        OnPropertyChanged(nameof(DicaDaSecao));
    }

    /// <summary>
    /// Monta e atualiza a régua. O "em branco" é o que faz a âncora valer: ele diz o que
    /// falta escrever sem abrir um vão vazio na página.
    /// </summary>
    private void MarcarSecoes()
    {
        if (Secoes.Count == 0)
        {
            Secoes.Add(new ChipAnamnese { Chave = ChaveAntecedentesPessoais, Rotulo = "Antecedentes pessoais" });
            Secoes.Add(new ChipAnamnese { Chave = ChaveAntecedentesFamiliares, Rotulo = "Antecedentes familiares" });
            Secoes.Add(new ChipAnamnese { Chave = ChaveHabitosDeVida, Rotulo = "Hábitos de vida" });
            Secoes.Add(new ChipAnamnese { Chave = ChaveRevisaoDeSistemas, Rotulo = "Interrogatório" });
            Secoes.Add(new ChipAnamnese { Chave = ChaveHistoriaObstetrica, Rotulo = "História obstétrica" });
            Secoes.Add(new ChipAnamnese { Chave = ChaveObservacoes, Rotulo = "Observações" });
        }

        foreach (var chip in Secoes)
        {
            chip.Marcado = chip.Chave == SecaoEscolhida;
            chip.EmBranco = string.IsNullOrWhiteSpace(chip.Chave switch
            {
                ChaveAntecedentesFamiliares => AntecedentesFamiliares,
                ChaveHabitosDeVida => HabitosDeVida,
                ChaveRevisaoDeSistemas => RevisaoDeSistemas,
                ChaveHistoriaObstetrica => HistoriaObstetrica,
                ChaveObservacoes => Observacoes,
                _ => AntecedentesPessoais
            });
        }
    }

    /// <summary>
    /// Qualquer campo mudou: a régua reconta o que está em branco e a folha reflete o
    /// campo certo.
    ///
    /// ⚠️ Gancho único em vez de seis pares de `NotifyPropertyChangedFor`: atributo
    /// repetido seis vezes é atributo que a sétima seção não ganha, e aí a âncora passa a
    /// dizer "em branco" sobre um campo escrito.
    /// </summary>
    private static readonly string[] CamposDaAnamnese =
    [
        nameof(AntecedentesPessoais), nameof(AntecedentesFamiliares), nameof(HabitosDeVida),
        nameof(RevisaoDeSistemas), nameof(HistoriaObstetrica), nameof(Observacoes)
    ];

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName is not { } nome) return;
        if (Array.IndexOf(CamposDaAnamnese, nome) < 0) return;

        MarcarSecoes();

        // Só quando o campo mudou POR FORA — ao carregar a anamnese do banco, por exemplo.
        // Durante a digitação, devolver o valor à caixa é como se perde a posição do
        // cursor; ver a marca no setter de TextoDaSecao.
        if (!_escrevendoNaFolha) OnPropertyChanged(nameof(TextoDaSecao));
    }

    /// <summary>
    /// "Revisada em 12/03/2026 por dra.ana" — e a idade dela, que é o que faz alguém decidir
    /// se vale reperguntar. Anamnese de três anos atrás não está errada; está VELHA.
    /// </summary>
    [ObservableProperty] private string _procedencia = string.Empty;

    public bool PodeLer => SessaoUsuario.Atual.Pode(Permissao.VerProntuario);
    public bool PodeEditar => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    /// <summary>O rótulo diz o ATO: colher e revisar são coisas diferentes na trilha.</summary>
    public string RotuloDoBotao => Colhida ? "Revisar anamnese" : "Colher anamnese";

    public AnamneseViewModel(
        IServiceScopeFactory escopos, PacienteEmFoco foco, IDialogoService dialogo)
    {
        _escopos = escopos;
        _foco = foco;
        _dialogo = dialogo;

        // ⚠️ A régua tem de ser montada AQUI: `SecaoEscolhida` é inicializada no campo, e
        // inicializador de campo não emite PropertyChanged — sem esta linha a tela abriria
        // sem âncora nenhuma, com a folha funcionando e sem como trocar de seção.
        MarcarSecoes();

        _ = CarregarAsync();
    }

    partial void OnColhidaChanged(bool value)
    {
        OnPropertyChanged(nameof(RotuloDoBotao));
        OnPropertyChanged(nameof(NaoColhida));
    }

    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        if (_foco.PacienteId is not { } id || !PodeLer)
        {
            LimparCampos();
            Colhida = false;
            return;
        }

        Carregando = true;
        NaoVerificado = false;
        try
        {
            using var escopo = _escopos.CreateScope();
            var servico = escopo.ServiceProvider.GetRequiredService<AnamneseService>();
            var a = await servico.DoPacienteAsync(id);

            var versoes = a is null
                ? []
                : await servico.VersoesAsync(a.Id);

            if (geracao != _geracaoCarga) return;

            Colhida = a is not null;

            if (a is null)
            {
                LimparCampos();
                Procedencia = string.Empty;
            }
            else
            {
                AntecedentesPessoais = a.AntecedentesPessoais ?? string.Empty;
                AntecedentesFamiliares = a.AntecedentesFamiliares ?? string.Empty;
                HabitosDeVida = a.HabitosDeVida ?? string.Empty;
                HistoriaObstetrica = a.HistoriaObstetrica ?? string.Empty;
                RevisaoDeSistemas = a.RevisaoDeSistemas ?? string.Empty;
                Observacoes = a.Observacoes ?? string.Empty;
                Procedencia = Descrever(a);
            }

            // ⚠️ Entre o Clear() e o último Add não pode haver await (parcela 62).
            var linhas = versoes.OrderByDescending(v => v.Versao).Select(Montar).ToList();
            Versoes.Clear();
            foreach (var l in linhas) Versoes.Add(l);
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — anamnese não pôde ser lida", ex);
            // Anamnese ausente por FALHA se leria como paciente sem antecedentes, e a
            // conduta sairia sem o que já se sabe dele. Terceiro estado, sempre.
            NaoVerificado = true;
        }
        finally
        {
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    /// <summary>Abre os campos para escrever. Ver o comentário da classe sobre o modo leitura.</summary>
    [RelayCommand]
    private void Editar()
    {
        // Guarda que diz por quê — botão apagado explica, guarda impede (parcela 41).
        if (!PodeEditar)
        {
            Mensagem = "O seu acesso não permite escrever no prontuário deste paciente.";
            MensagemEhErro = true;
            return;
        }

        if (_foco.PacienteId is null)
        {
            Mensagem = "Escolha um paciente antes de colher a anamnese.";
            MensagemEhErro = true;
            return;
        }

        Editando = true;
        Mensagem = null;
    }

    [RelayCommand]
    private async Task CancelarEdicaoAsync()
    {
        Editando = false;
        Mensagem = null;
        await CarregarAsync();
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (_foco.PacienteId is not { } id) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever a anamnese");

            // ⚠️ O motivo é pedido SÓ na revisão, e é OPCIONAL — a razão da
            // VersaoEvolucao.Motivo: exigir justificativa a cada revisão produziria trinta
            // "atualização" por semana, que é rastro com aparência de controle.
            // ⚠️ O estado é capturado ANTES de gravar: o CarregarAsync logo abaixo põe
            // `Colhida` em true, e ler a propriedade depois dele fazia a PRIMEIRA colheita
            // responder "Anamnese revisada — o que ela dizia antes está guardado abaixo" com
            // o histórico vazio ao lado. Frase que promete um histórico que não existe manda
            // a pessoa procurar o que nunca houve.
            var jaExistia = Colhida;

            string? motivo = null;
            if (jaExistia)
            {
                motivo = _dialogo.PerguntarTexto(
                    "Revisar a anamnese",
                    "O que ela dizia antes fica guardado e recuperável. Se quiser, diga o "
                    + "que mudou (opcional):",
                    string.Empty,
                    obrigatorio: false);

                // ⚠️ Cancelar é "desisti", e o que se grava aqui é PRONTUÁRIO: a revisão cria
                // uma VersaoAnamnese, carimba quem revisou e grava auditoria — não se desfaz.
                // `null` vem SÓ do Cancelar/Esc; a resposta em branco volta como string vazia,
                // que é o "revisar sem dizer por quê" que a própria pergunta oferece (motivo
                // obrigatório a cada Salvar produziria trinta "ajuste" por dia — parcela 52).
                if (motivo is null) return;
            }

            using var escopo = _escopos.CreateScope();
            var servico = escopo.ServiceProvider.GetRequiredService<AnamneseService>();

            await servico.SalvarAsync(id, new AnamnesePaciente
            {
                AntecedentesPessoais = AntecedentesPessoais,
                AntecedentesFamiliares = AntecedentesFamiliares,
                HabitosDeVida = HabitosDeVida,
                HistoriaObstetrica = HistoriaObstetrica,
                RevisaoDeSistemas = RevisaoDeSistemas,
                Observacoes = Observacoes
            }, SessaoUsuario.Atual.Operador, motivo);

            Editando = false;
            await CarregarAsync();

            Mensagem = jaExistia
                ? "Anamnese revisada. O que ela dizia antes está guardado abaixo."
                : "Anamnese colhida.";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — anamnese não pôde ser gravada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    private static LinhaVersaoAnamnese Montar(VersaoAnamnese v) => new()
    {
        Titulo = $"Versão {v.Versao}",
        Quando = $"substituída em {v.SubstituidaEm:dd/MM/yyyy HH\\:mm}"
                 + (string.IsNullOrWhiteSpace(v.SubstituidaPor)
                     ? string.Empty : $" por {v.SubstituidaPor}"),
        // O motivo é opcional, e a frase é o que impede a ausência de parecer erro de
        // gravação (a mesma decisão da VersaoEvolucao).
        Motivo = string.IsNullOrWhiteSpace(v.Motivo)
            ? "Revisão sem motivo informado."
            : v.Motivo,
        Texto = Juntar(v.AntecedentesPessoais, v.AntecedentesFamiliares, v.HabitosDeVida,
                       v.HistoriaObstetrica, v.RevisaoDeSistemas, v.Observacoes)
    };

    /// <summary>Junta os seis PULANDO o que não existe — rótulo sobre o vazio mente.</summary>
    private static string Juntar(string? pessoais, string? familiares, string? habitos,
                                 string? obstetrica, string? sistemas, string? observacoes)
    {
        var partes = new List<string>();
        void Campo(string rotulo, string? v)
        {
            if (!string.IsNullOrWhiteSpace(v)) partes.Add($"{rotulo}: {v.Trim()}");
        }

        Campo("Antecedentes pessoais", pessoais);
        Campo("Antecedentes familiares", familiares);
        Campo("Hábitos de vida", habitos);
        Campo("História obstétrica", obstetrica);
        Campo("Interrogatório", sistemas);
        Campo("Observações", observacoes);
        return string.Join("\n", partes);
    }

    private static string Descrever(AnamnesePaciente a)
    {
        var quem = a.AtualizadaPor ?? a.CriadaPor;
        var verbo = a.AtualizadaEm is null ? "Colhida" : "Revisada";
        var texto = $"{verbo} em {a.UltimaRevisao:dd/MM/yyyy}"
                    + (string.IsNullOrWhiteSpace(quem) ? string.Empty : $" por {quem}");

        // A IDADE dela é o que faz alguém decidir se vale reperguntar. Um ano é o corte da
        // casa: abaixo disso a frase seria ruído em toda ficha.
        var meses = (int)((DateTime.Now - a.UltimaRevisao).TotalDays / 30);
        return meses >= 12
            ? texto + $"  ·  há mais de {meses / 12} ano(s) — vale reperguntar"
            : texto;
    }

    private void LimparCampos()
    {
        AntecedentesPessoais = AntecedentesFamiliares = HabitosDeVida =
            HistoriaObstetrica = RevisaoDeSistemas = Observacoes = string.Empty;
        Versoes.Clear();
    }
}
