using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Clinico.Janelas;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;
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
    public string? EvolucaoFormatada { get; init; }
    public required string Orientacoes { get; init; }
    public string TextoParaCopia { get; init; } = string.Empty;

    /// <summary>Quantos arquivos a sessão tem — o clipe da linha.</summary>
    public required int Anexos { get; init; }

    /// <summary>
    /// Quantas vezes a sessão foi CORRIGIDA (parcela 52). Marcar isto na lista é o que
    /// torna a retificação visível — a Lei 13.787/2018 pede rastreabilidade, e rastro que
    /// só existe no banco não é rastreável por ninguém.
    /// </summary>
    public required int Correcoes { get; init; }

    public bool Retificada => Correcoes > 0;

    public string CorrecoesTexto => Correcoes switch
    {
        0 => "Correções",
        1 => "1 correção",
        _ => $"{Correcoes} correções"
    };

    public bool TemAnexos => Anexos > 0;

    public string AnexosTexto => Anexos switch
    {
        0 => "Anexos",
        1 => "1 anexo",
        _ => $"{Anexos} anexos"
    };

    public static LinhaSessaoProntuario De(Evolucao e, int anexos, int correcoes, string paciente = "") => new()
    {
        EvolucaoId = e.Id,
        Data = e.Data.ToString("dd/MM/yyyy"),
        Profissional = e.Profissional?.Nome ?? "—",
        Eva = e.TemParEva ? $"EVA {e.EvaAntes} → {e.EvaDepois}" : "EVA não medida",
        Queixa = string.IsNullOrWhiteSpace(e.QueixaPrincipal) ? "—" : e.QueixaPrincipal!,
        Conduta = string.IsNullOrWhiteSpace(e.Conduta) ? "—" : e.Conduta!,
        Evolucao = string.IsNullOrWhiteSpace(e.TextoEvolucao) ? "—" : e.TextoEvolucao!,
        EvolucaoFormatada = e.TextoEvolucaoFormatado,
        Orientacoes = string.IsNullOrWhiteSpace(e.Orientacoes) ? "—" : e.Orientacoes!,
        TextoParaCopia = SessaoDoProntuario.De(e, anexos, correcoes).TextoParaCopiar(paciente),
        Anexos = anexos,
        Correcoes = correcoes
    };
}

/// <summary>
/// O PRONTUÁRIO COMPLETO na máquina de quem atende (parcela 37).
///
/// Por que existe
/// --------------
/// A tela de Atendimento mostra as CINCO últimas sessões, numa aba própria, que é o
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
    private readonly PacienteEmFoco _foco;

    public ObservableCollection<LinhaSessaoProntuario> Sessoes { get; } = [];

    /// <summary>
    /// Busca por texto dentro do prontuário — a pergunta central de quem vai atender.
    /// Sem acento e sem caixa: quem digita escreve "lombalgia" e o prontuário diz
    /// "Lombalgia", e uma busca que erra por isso é uma busca que ninguém usa duas vezes.
    /// </summary>
    [ObservableProperty] private string _termoSessao = string.Empty;

    /// <summary>O que a lista está mostrando. Filtro invisível é filtro que engana.</summary>
    [ObservableProperty] private string _resumoSessoes = string.Empty;

    [ObservableProperty] private string _paciente = string.Empty;
    [ObservableProperty] private bool _semPaciente = true;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Metade VISÍVEL da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditarProntuario => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    public bool TemPaciente => !SemPaciente;

    partial void OnSemPacienteChanged(bool value) => OnPropertyChanged(nameof(TemPaciente));

    /// <summary>
    /// ENFERMAGEM E INFUSÕES (parcela 72) — o mesmo componente da ficha e da tela da
    /// Enfermagem, limitado às duas naturezas que faltavam aqui.
    ///
    /// ⚠️ A lista de SESSÕES desta tela NÃO foi substituída por ele: ela tem busca no
    /// texto, contagem de anexos, marca de correção e os botões da linha. Trocá-la pela
    /// genérica tiraria capacidade de quem a usa todo dia.
    ///
    /// ⚠️ Sem ações: o médico LÊ o que a enfermagem escreveu e não corrige (o registro é
    /// assinado com o COREN dela), e a folha se mexe na sala, com o bit
    /// <c>ChecarPrescricao</c>. Botão aceso que não faz nada é o defeito da parcela 41.
    /// </summary>
    public LinhaDoTempoClinicaViewModel LinhaDoTempo { get; }

    public ProntuarioClinicoViewModel(IServiceScopeFactory escopos, PacienteEmFoco foco)
    {
        _escopos = escopos;
        _foco = foco;

        LinhaDoTempo = new LinhaDoTempoClinicaViewModel(escopos)
        {
            MostrarDocumentos = false,
            SecoesVisiveis =
            [
                NaturezaRegistroClinico.EvolucaoEnfermagem,
                NaturezaRegistroClinico.PrescricaoInterna
            ],
            SecaoInicial = NaturezaRegistroClinico.EvolucaoEnfermagem
        };

        if (_foco.Definido) _ = CarregarAsync();
    }

    private int PacienteId => _foco.PacienteId ?? 0;

    /// <summary>
    /// Último paciente cujo acesso já foi registrado nesta tela.
    ///
    /// A trilha de LEITURA (parcela 52) é registrada quando o PACIENTE muda, e não a cada
    /// <c>CarregarAsync</c>. O filtro usa o conteúdo da tela em memória; digitar não
    /// representa outro acesso nem dispara consultas. Atualizar relê os dados do banco.
    /// </summary>
    private int _acessoRegistradoDe;

    // Cache apenas da tela aberta: uma carga, um paciente. Atualizar/editar/trocar
    // paciente descarta os dados; digitar um filtro não faz uma nova viagem à VPS.
    private int _pacienteCarregado;
    private IReadOnlyList<Evolucao> _sessoesCarregadas = [];
    private IReadOnlyDictionary<int, int> _anexosCarregados = new Dictionary<int, int>();
    private IReadOnlyDictionary<int, int> _correcoesCarregadas = new Dictionary<int, int>();

    partial void OnTermoSessaoChanged(string value) => AplicarFiltro();

    /// <summary>
    /// Descarte de resposta fora de ordem ao atualizar ou trocar de paciente.
    /// </summary>
    private int _geracaoCarga;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;
        var pacienteId = PacienteId;
        _pacienteCarregado = 0;
        _sessoesCarregadas = [];
        _anexosCarregados = new Dictionary<int, int>();
        _correcoesCarregadas = new Dictionary<int, int>();
        ResumoSessoes = string.Empty;

        SemPaciente = pacienteId == 0;
        Sessoes.Clear();

        if (SemPaciente)
        {
            Carregando = false;
            Paciente = string.Empty;
            await LinhaDoTempo.CarregarAsync(0);
            return;
        }

        // O componente tem contador de geração próprio e filtro de acesso por natureza —
        // ele não depende de nada desta carga.
        _ = LinhaDoTempo.CarregarAsync(pacienteId);

        if (!SessaoUsuario.Atual.Pode(Permissao.VerProntuario))
        {
            Carregando = false;
            NaoVerificado = true;
            Mensagem = "O seu acesso não permite ler o prontuário deste paciente.";
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

            // Quem abriu este prontuário, e quando. A LGPD e o dever de prestação de
            // contas alcançam a LEITURA, e até a parcela 52 a trilha só via escrita.
            // Não bloqueia nem derruba a tela: o serviço engole a falha com rastro.
            if (_acessoRegistradoDe != pacienteId)
            {
                _acessoRegistradoDe = pacienteId;
                await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(pacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.ProntuarioClinico);
            }

            if (geracao != _geracaoCarga || pacienteId != PacienteId) return;
            var todas = await prontuario.DoPacienteAsync(pacienteId);
            if (geracao != _geracaoCarga || pacienteId != PacienteId) return;

            // Contagens da carga inteira: mudar o filtro não precisa reler o banco.
            var contagens = await prontuario.ContagemDeAnexosAsync(
                todas.Select(e => e.Id).ToList());
            if (geracao != _geracaoCarga || pacienteId != PacienteId) return;

            var correcoes = await prontuario.ContagemDeVersoesAsync(
                todas.Select(e => e.Id).ToList());

            if (geracao != _geracaoCarga || pacienteId != PacienteId) return;
            _pacienteCarregado = pacienteId;
            _sessoesCarregadas = todas;
            _anexosCarregados = contagens;
            _correcoesCarregadas = correcoes;
            AplicarFiltro();

        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — prontuário não pôde ser carregado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    private void AplicarFiltro()
    {
        Sessoes.Clear();
        ResumoSessoes = string.Empty;
        if (_pacienteCarregado == 0 || _pacienteCarregado != PacienteId
            || !SessaoUsuario.Atual.Pode(Permissao.VerProntuario)) return;

        var termo = TermoSessao.Trim();
        foreach (var e in _sessoesCarregadas.Where(e => termo.Length == 0 || Casa(e, termo)))
            Sessoes.Add(LinhaSessaoProntuario.De(e,
                _anexosCarregados.GetValueOrDefault(e.Id), _correcoesCarregadas.GetValueOrDefault(e.Id), Paciente));

        ResumoSessoes = termo.Length == 0
            ? $"{_sessoesCarregadas.Count} sessão(ões) no prontuário."
            : $"{Sessoes.Count} de {_sessoesCarregadas.Count} sessão(ões) contêm “{termo}”.";
    }


    /// <summary>
    /// A sessão contém o termo em algum dos campos escritos.
    ///
    /// ⚠️ Os QUATRO da parcela 73 entram aqui (acrescentados na 74, 2ª rodada) — sem eles, a
    /// busca ficava cega justamente para o que se procura: quem digita "hérnia" está
    /// procurando a HIPÓTESE, e quem digita "Lasègue" está procurando o EXAME FÍSICO. A
    /// pergunta que a parcela 37 usou para justificar a busca é literalmente essa ("o que o
    /// profissional pergunta antes de atender"), e ela passou a ter resposta em campos que
    /// o filtro não olhava.
    ///
    /// O CID entra junto: "M54.5" é o jeito mais curto de achar todas as lombalgias.
    /// </summary>
    private static bool Casa(Evolucao e, string termo)
        // UMA definição, no domínio: esta busca existia duas vezes — uma por tela —
        // e as duas já tinham divergido uma vez (parcela 77).
        => BuscaNoProntuario.Casa(e, termo);


    // ---------------------------------------------------------------- anexos

    /// <summary>
    /// Abre o histórico de correções da sessão (parcela 52) — a porta que faltava para a
    /// rastreabilidade do art. 3º da Lei 13.787/2018 ser LIDA, e não só guardada.
    /// </summary>
    [RelayCommand]
    private void VerCorrecoes(LinhaSessaoProntuario? linha)
    {
        // Guarda sobre PARÂMETRO: nunca dispara vindo de botão de linha, e por isso pode
        // sair calada (a exceção declarada da checagem 21).
        if (linha is null) return;

        try
        {
            new VersoesEvolucaoWindow
            {
                DataContext = new VersoesEvolucaoViewModel(
                    _escopos, linha.EvolucaoId, $"{linha.Data} — {Paciente}"),
                Owner = JanelaDona.Atual()
            }.ShowDialog();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — histórico de correções", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// ABRIR A SESSÃO por inteiro (set/2026 — o pedido do cliente: <i>"ao abrir o
    /// prontuário não conseguimos abrir o prontuário daquela sessão"</i>).
    ///
    /// A linha da lista mostra QUATRO campos truncados e a sessão tem DOZE: história da
    /// doença atual, exame físico, hipótese, CID, plano terapêutico, retorno sugerido e
    /// encaminhamento estavam gravados e não tinham leitor em tela nenhuma — o defeito
    /// recorrente do projeto, na variante em que nada falha.
    ///
    /// A janela é do SHELL porque são quatro portas em módulos diferentes, e é somente
    /// LEITURA: escrever continua sendo a tela de Atendimento, com o bit de escrita.
    ///
    /// ⚠️ Os ANEXOS voltam como INTENÇÃO: a janela deles mora NESTE módulo e o shell não a
    /// alcança. Quem age é esta ViewModel, pelo MESMO comando que o botão da linha já usa —
    /// uma segunda definição de "abrir os anexos desta sessão" divergiria na primeira
    /// correção. As CORREÇÕES a janela abre sozinha: aquelas são do shell.
    /// </summary>
    [RelayCommand]
    private async Task AbrirSessaoAsync(LinhaSessaoProntuario? linha)
    {
        // Guarda sobre PARÂMETRO: nunca dispara vindo de botão de linha, e por isso pode
        // sair calada (a exceção declarada da checagem 21).
        if (linha is null) return;

        try
        {
            // A recusa aparece na tela de TRÁS, e não dentro de uma janela que já abriu.
            SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "abrir a sessão do prontuário");

            var vm = new SessaoDoProntuarioViewModel(
                _escopos, linha.EvolucaoId, Paciente, ofereceAnexos: true);
            new SessaoDoProntuarioWindow(vm) { Owner = JanelaDona.Atual() }.ShowDialog();

            if (vm.PediuAnexos) await VerAnexosAsync(linha);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — a sessão do prontuário não pôde ser aberta", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Abre os anexos de uma sessão — o laudo que voltou do exame que ESTE app pediu.
    /// </summary>
    [RelayCommand]
    private async Task VerAnexosAsync(LinhaSessaoProntuario? linha)
    {
        if (linha is null) return;

        try
        {
            // ⚠️ A barreira que faltava (parcela 72): esta janela abre LAUDO — dado de
            // saúde —, e o comando não conferia bit nenhum. As ações de DENTRO dela
            // (anexar, retirar, baixar) já exigem o bit certo desde as parcelas 37 e 60;
            // a porta, não. É a guarda de LEITURA sobre um caminho de leitura, e a tela
            // diz por que recusou em vez de abrir vazia.
            SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "ver anexos do prontuário");

            var vm = new AnexosSessaoViewModel(
                _escopos, linha.EvolucaoId, $"Sessão de {linha.Data} — {Paciente}",
                PacienteId);

            new AnexosSessaoWindow(vm)
            {
                Owner = JanelaDona.Atual()
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

}
