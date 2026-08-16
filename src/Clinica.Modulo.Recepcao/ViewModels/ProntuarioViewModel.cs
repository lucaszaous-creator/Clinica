using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// Tela de PRONTUÁRIO: escolhe o paciente à esquerda, trabalha as sessões dele à direita.
///
/// Ela existe porque o prontuário é um item de primeiro nível da proposta comercial e no
/// sistema só se chegava nele por dentro da ficha do paciente — não havia entrada de menu
/// em lugar nenhum. Quem abria o app procurando "Prontuário" não achava, e concluía que
/// não tinha sido entregue.
///
/// Não substitui a ficha: lá o prontuário é uma seção entre outras (cadastro,
/// elegibilidade, LGPD, guias), para quem está com o paciente na frente. Aqui é a tela
/// inteira, para quem vai escrever a evolução do dia. As duas listam a MESMA
/// <see cref="LinhaEvolucao"/>, pela mesma fábrica, para nunca descreverem a mesma sessão
/// de dois jeitos.
/// </summary>
public sealed partial class ProntuarioViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public SeletorPacienteViewModel Seletor { get; }

    public ObservableCollection<LinhaEvolucao> Sessoes { get; } = [];

    /// <summary>
    /// Busca por texto dentro do prontuário (parcela 28).
    ///
    /// O prontuário de um paciente de tratamento longo passa de cinquenta sessões, e a
    /// pergunta que o profissional faz antes de atender é sempre a mesma: "o que eu fiz
    /// da última vez que ele veio com dor no ombro?". Sem busca, a resposta era rolar a
    /// lista inteira — e na prática ninguém rolava.
    /// </summary>
    [ObservableProperty] private string _termoSessao = string.Empty;

    /// <summary>O que a lista está mostrando. Filtro invisível é filtro que engana.</summary>
    [ObservableProperty] private string _resumoSessoes = string.Empty;

    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, lista vazia por erro fica idêntica
    /// a lista vazia por não haver nada.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Nenhum paciente escolhido — a direita mostra o convite, não uma lista vazia.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeNovaSessao))]
    private bool _semPaciente = true;

    [ObservableProperty] private string _paciente = string.Empty;

    // ===== Evolução da dor (EVA) =====
    [ObservableProperty] private string _dorInicial = "—";
    [ObservableProperty] private string _dorAtual = "—";
    [ObservableProperty] private string _ganhoAcumulado = "—";
    [ObservableProperty] private string _alivioMedio = "—";
    [ObservableProperty] private string _resumoEva = string.Empty;

    /// <summary>
    /// Últimos escores das escalas aplicadas pelo Consultório (parcela 36).
    ///
    /// Elas são gravadas noutro módulo, e sem esta linha só seriam lidas lá — enquanto a
    /// recepção, que atende o telefone do paciente e monta o relatório para o convênio,
    /// veria um prontuário que não menciona a metade medida do tratamento. Vazio quando
    /// nenhuma escala foi aplicada: a linha some em vez de dizer "nenhuma", que ocuparia
    /// espaço para informar ausência.
    /// </summary>
    [ObservableProperty] private string _resumoEscalas = string.Empty;

    /// <summary>
    /// Metade VISÍVEL da permissão: quem não pode escrever vê os botões apagados com o
    /// motivo. A que impede é o <c>Exigir</c> dentro do comando.
    /// </summary>
    public bool PodeEditarProntuario => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    /// <summary>
    /// A "Nova sessão" precisa de paciente ESCOLHIDO além da permissão — a tela abre
    /// pela sidebar sem ninguém em foco, e botão aceso que volta calado faz quem clica
    /// concluir que o sistema quebrou (parcela 41).
    /// </summary>
    public bool PodeNovaSessao => !SemPaciente && PodeEditarProntuario;

    public ProntuarioViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;

        // O seletor da suíte, com o corte no SQL e o descarte de resposta fora de ordem
        // já resolvidos. Tela nova que escolhe paciente usa ele — não se reescreve busca.
        Seletor = new SeletorPacienteViewModel(escopos);
        Seletor.SelecaoMudou += AoTrocarPaciente;

        // A busca INICIAL. Sem ela a tela abre com a caixa de busca vazia e a lista vazia
        // ao lado — e tela vazia se lê como sistema quebrado, não como "digite um nome".
        // É o mesmo `_ =` que `PacientesViewModel` já fazia; faltava aqui e nas Prescrições.
        _ = Seletor.BuscarAsync(imediato: true);
    }

    /// <summary>
    /// LISTA → TELA DO ITEM (parcela 48). Antes a lista de pacientes era uma coluna de
    /// 340 px grudada à esquerda para sempre — o padrão que o `README.md` proíbe e que a
    /// cliente reprovou sete vezes. É o MESMO desenho de `PacientesViewModel`: dois
    /// desenhos para a mesma coisa é o que faz a recepcionista achar que abriu outro
    /// programa.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrandoLista))]
    private bool _mostrandoProntuario;

    /// <summary>Estamos na lista? O par existe porque XAML não nega booleano sem conversor.</summary>
    public bool MostrandoLista => !MostrandoProntuario;

    private void AoTrocarPaciente(Paciente? paciente)
    {
        if (paciente is null) return;

        MostrandoProntuario = true;

        // Trocar de paciente limpa a busca: "ombro" digitado para um não é pergunta sobre
        // o próximo, e a lista voltaria filtrada sem ninguém entender por quê. Quando há
        // termo, é o SETTER que dispara a carga — chamar de novo aqui era o duplo disparo
        // garantido que deixava duas leituras concorrendo sobre a mesma coleção.
        var setterNaoDispara = TermoSessao.Length == 0;
        TermoSessao = string.Empty;
        if (setterNaoDispara) _ = CarregarAsync();
    }

    /// <summary>
    /// Volta para a lista. LIMPA a seleção de propósito: sem isso, clicar de novo no mesmo
    /// paciente não dispararia <c>SelecaoMudou</c> e o prontuário não reabriria — botão que
    /// funciona uma vez e depois não.
    /// </summary>
    [RelayCommand]
    private void Voltar()
    {
        MostrandoProntuario = false;
        Seletor.Limpar();
    }

    partial void OnTermoSessaoChanged(string value) => _ = CarregarAsync();

    /// <summary>
    /// A sessão contém o termo em algum dos campos escritos. Sem acento e sem caixa: quem
    /// digita no balcão escreve "lombalgia" e o prontuário diz "Lombalgia", e uma busca
    /// que erra por isso é uma busca que ninguém usa duas vezes.
    /// </summary>
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

    private int PacienteId => Seletor.Selecionado?.Id ?? 0;

    /// <summary>Último paciente cujo acesso já foi registrado nesta tela (parcela 52).</summary>
    private int _acessoRegistradoDe;

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): a tela recarrega a cada tecla do
    /// filtro e a cada troca de paciente, e a resposta atrasada chegando por último
    /// ANEXARIA as sessões do termo (ou do paciente) anterior à lista atual.
    /// </summary>
    private int _geracaoCarga;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        SemPaciente = PacienteId == 0;
        Sessoes.Clear();

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
            Paciente = Seletor.Selecionado?.Nome ?? string.Empty;

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();

            // Trilha de LEITURA (parcela 52). Esta tela é item PRÓPRIO da sidebar: chega-se
            // a ela sem passar pela ficha, escolhe-se qualquer paciente no seletor e lê-se
            // o prontuário inteiro. Era o maior buraco que sobrou do registro de acesso —
            // e justamente na porta mais fácil de usar para ler o que não se deve.
            if (_acessoRegistradoDe != PacienteId)
            {
                _acessoRegistradoDe = PacienteId;
                await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(PacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.ProntuarioClinico);
            }

            var todas = await prontuario.DoPacienteAsync(PacienteId);
            var termo = TermoSessao.Trim();

            var filtradas = termo.Length == 0
                ? todas.ToList()
                : todas.Where(e => Casa(e, termo)).ToList();

            // Uma consulta para o prontuário inteiro (parcela 37). Antes era uma por
            // sessão: num tratamento de quarenta, quarenta idas a um banco remoto para
            // desenhar quarenta clipes.
            var contagens = await prontuario.ContagemDeAnexosAsync(
                filtradas.Select(e => e.Id).ToList());

            // Chegou tarde: outra tecla ou outro paciente já pediu uma carga mais nova.
            if (geracao != _geracaoCarga) return;

            foreach (var e in filtradas)
                Sessoes.Add(LinhaEvolucao.De(
                    e, contagens.TryGetValue(e.Id, out var quantos) ? quantos : 0));

            ResumoSessoes = termo.Length == 0
                ? $"{todas.Count} sessão(ões) no prontuário."
                // A lista filtrada DIZ que está filtrada, e diz sobre quantas: "3 sessões"
                // sozinho faria o profissional concluir que o paciente veio três vezes.
                : $"{Sessoes.Count} de {todas.Count} sessão(ões) contêm “{termo}”.";

            // A evolução da dor é sempre do prontuário INTEIRO, nunca do filtro: ela mede
            // o tratamento, e medir só as sessões que citam uma palavra daria um gráfico
            // que não corresponde a tratamento nenhum.
            var dor = await prontuario.EvolucaoDaDorAsync(PacienteId);
            if (geracao != _geracaoCarga) return;
            AplicarEvolucaoDaDor(dor);

            var avaliacoes = scope.ServiceProvider.GetRequiredService<AvaliacaoClinicaService>();
            var escalas = await avaliacoes.DoPacienteAsync(PacienteId);
            if (geracao != _geracaoCarga) return;
            AplicarEscalas(escalas);
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar("Recepção — prontuário não pôde ser carregado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    /// <summary>
    /// A EVA vale em PAR: medir só antes (ou só depois) não diz se o tratamento
    /// funcionou. Sem par, a tela mostra "—" e diz quantas sessões têm a medida — 0 e
    /// "não medido" são coisas diferentes.
    /// </summary>
    private void AplicarEvolucaoDaDor(EvolucaoDaDor dor)
    {
        if (dor.SessoesComMedida == 0)
        {
            DorInicial = DorAtual = GanhoAcumulado = AlivioMedio = "—";
            ResumoEva = dor.SessoesRegistradas == 0
                ? "Nenhuma sessão registrada no prontuário ainda."
                : $"{dor.SessoesRegistradas} sessão(ões) registradas, nenhuma com o par EVA "
                  + "(antes e depois). Sem o par não dá para dizer se a dor melhorou.";
            return;
        }

        DorInicial = $"{dor.DorInicial}/10";
        DorAtual = $"{dor.DorAtual}/10";
        GanhoAcumulado = dor.GanhoAcumulado is { } ganho
            ? (ganho >= 0 ? $"−{ganho} pontos" : $"+{-ganho} pontos")
            : "—";
        AlivioMedio = $"{dor.AlivioMedioPorSessao:0.#} por sessão";
        ResumoEva = $"{dor.SessoesComMedida} de {dor.SessoesRegistradas} sessão(ões) com EVA medida.";
    }

    /// <summary>
    /// O ÚLTIMO escore de cada instrumento — não o histórico inteiro. A curva mora no
    /// Consultório, que é de quem aplica; aqui a pergunta é "o que já foi medido nesta
    /// pessoa?", e ela se responde com uma linha.
    /// </summary>
    private void AplicarEscalas(IReadOnlyList<AvaliacaoClinica> avaliacoes)
    {
        if (avaliacoes.Count == 0)
        {
            ResumoEscalas = string.Empty;
            return;
        }

        // A lista vem da mais recente para a mais antiga, então a primeira de cada
        // instrumento já é a última aplicação dele.
        var ultimas = avaliacoes
            .GroupBy(a => a.InstrumentoCodigo)
            .Select(g => g.First())
            .OrderByDescending(a => a.Data)
            .ToList();

        ResumoEscalas = "Escalas aplicadas: " + string.Join(" · ", ultimas.Select(a =>
            $"{a.InstrumentoNome} — {a.PontuacaoFormatada} em {a.Data:dd/MM/yyyy}"
            + (string.IsNullOrWhiteSpace(a.FaixaNome) ? string.Empty : $" ({a.FaixaNome})")));
    }

    [RelayCommand]
    private Task NovaSessaoAsync() => AbrirAsync(null);

    [RelayCommand]
    private Task EditarSessaoAsync(LinhaEvolucao? linha)
        => linha is null ? Task.CompletedTask : AbrirAsync(linha.EvolucaoId);

    private async Task AbrirAsync(int? evolucaoId)
    {
        if (PacienteId == 0)
        {
            Mensagem = "Escolha um paciente antes de abrir a sessão.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            var vm = new EvolucaoEdicaoViewModel(_escopos, PacienteId, evolucaoId);
            var janela = new Janelas.EvolucaoWindow(vm)
            {
                Owner = JanelaDona.Atual()
            };

            if (janela.ShowDialog() != true) return;

            _snackbar.Sucesso("Prontuário atualizado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — sessão do prontuário não pôde ser aberta", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Prontuário é documento clínico: apagar deixa rastro na auditoria e leva os anexos
    /// junto. Por isso a confirmação diz as duas coisas antes.
    /// </summary>
    [RelayCommand]
    private async Task ExcluirSessaoAsync(LinhaEvolucao? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            // Cancelar, nunca apagar (parcela 52): a Lei 13.787/2018 manda guardar o
            // prontuário por 20 anos, e o motivo escrito é o que separa a correção da
            // reescrita.
            var motivo = _dialogo.PerguntarTexto(
                "Cancelar sessão do prontuário",
                $"Por que a sessão de {linha.Data} está sendo cancelada? Ela NÃO é apagada — "
                + "sai do prontuário que se lê e fica guardada, com este motivo ao lado.");
            if (string.IsNullOrWhiteSpace(motivo)) return;

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();
            await prontuario.CancelarAsync(linha.EvolucaoId, motivo, SessaoUsuario.Atual.Operador);

            _snackbar.Info("Sessão cancelada (guardada no prontuário).");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — evolução não pôde ser excluída", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
