using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>
/// As sessões atendidas que continuam sem evolução escrita — a dívida de prontuário.
///
/// Por que é uma TELA, e não um bloco do "Meu dia"
/// -----------------------------------------------
/// Ela morava numa caixa de 180 px acima do quadro do dia, e numa base real são dezenas
/// de linhas: a caixa cortava um nome ao meio e a lista rolava dentro de um vão do
/// tamanho de três linhas. Pior do que feio, era a pergunta errada no lugar errado — o
/// "Meu dia" responde o que acontece HOJE, e esta lista é o que ficou de trás.
///
/// Vale a regra que o cliente reprovou duas vezes em voz alta: <b>lista longa merece a
/// largura inteira da tela</b>, e seção que não é sobre o que a tela é sobre vira porta,
/// não caixa. O "Meu dia" ficou com um botão que diz o tamanho da dívida e traz para cá.
/// </summary>
public sealed partial class RegistrosPendentesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly PacienteEmFoco _foco;

    public ObservableCollection<LinhaRegistroPendente> Pendentes { get; } = [];

    /// <summary>Tudo o que o banco devolveu; <see cref="Pendentes"/> é o recorte do filtro.</summary>
    private readonly List<LinhaRegistroPendente> _todas = [];

    // ---- Filtro (em memória, sobre o que já foi lido). Numa base real a dívida chega a
    // dezenas de linhas, e quem abre a tela procurando UM paciente não pode ter de rolar
    // a lista inteira.
    public const string TodosProfissionais = "Todos os profissionais";

    /// <summary>
    /// Os profissionais COM dívida na lista — só faz sentido (e só aparece) no modo SEM
    /// VÍNCULO, em que a tela mostra a clínica inteira: com vínculo, a lista já é de um
    /// profissional só, e o combo seria uma caixinha que não recorta nada.
    /// </summary>
    public ObservableCollection<string> ProfissionaisDaLista { get; } = [TodosProfissionais];

    [ObservableProperty] private string _filtroProfissionalRegistro = TodosProfissionais;
    [ObservableProperty] private string _filtroPacienteRegistro = string.Empty;

    /// <summary>O `Clear()` do combo devolve nulo pelo binding (lição da parcela 56) — remonta sob guarda.</summary>
    private bool _montandoProfissionais;

    partial void OnFiltroPacienteRegistroChanged(string value) => Refiltrar();
    partial void OnFiltroProfissionalRegistroChanged(string value)
    {
        if (value is null)
        {
            FiltroProfissionalRegistro = TodosProfissionais;
            return;
        }
        if (!_montandoProfissionais) Refiltrar();
    }

    public bool FiltroAtivo =>
        FiltroProfissionalRegistro != TodosProfissionais
        || !string.IsNullOrWhiteSpace(FiltroPacienteRegistro);

    [RelayCommand]
    private void LimparFiltro()
    {
        FiltroProfissionalRegistro = TodosProfissionais;
        FiltroPacienteRegistro = string.Empty;
    }

    /// <summary>O estado vazio muda de frase quando há filtro — vazio filtrado não é "prontuário em dia".</summary>
    [ObservableProperty] private string _vazioDescricao =
        "Toda sessão atendida nos últimos dias já tem evolução escrita.";

    [ObservableProperty] private bool _carregando;

    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>A leitura FALHOU — nunca desenhar falha como "não há nada".</summary>
    [ObservableProperty] private bool _naoVerificado;

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>
    /// A lista é a da clínica inteira — e a tela diz POR QUÊ, que são dois motivos
    /// diferentes (ver <see cref="PostoClinico"/>).
    /// </summary>
    [ObservableProperty] private bool _listaDaClinica;

    /// <summary>POR QUE a lista é da clínica — a frase certa para cada motivo.</summary>
    [ObservableProperty] private string? _motivoDaLista;

    public bool Vazio => Pendentes.Count == 0;

    public RegistrosPendentesViewModel(IServiceScopeFactory escopos, PacienteEmFoco foco)
    {
        _escopos = escopos;
        _foco = foco;
        _ = CarregarAsync();
    }

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 60): a tela recarrega por clique e ao
    /// voltar de uma evolução escrita, e num banco remoto a leitura VELHA pode responder
    /// por último — repovoando a fila com o que já foi resolvido.
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

            var pendentes = await consultorio.RegistrosPendentesAsync(hoje, profissionalId);
            if (geracao != _geracaoCarga) return;

            Pendentes.Clear();
            _todas.Clear();
            foreach (var p in pendentes) _todas.Add(LinhaRegistroPendente.De(p, hoje));

            // O combo de profissional só existe no modo SEM VÍNCULO; com vínculo a lista
            // já é de um profissional só e o filtro volta ao neutro.
            var escolhido = FiltroProfissionalRegistro;
            _montandoProfissionais = true;
            try
            {
                ProfissionaisDaLista.Clear();
                ProfissionaisDaLista.Add(TodosProfissionais);
                if (ListaDaClinica)
                    foreach (var nome in _todas.Select(l => l.Profissional)
                                 .Where(n => n.Length > 0).Distinct().OrderBy(n => n))
                        ProfissionaisDaLista.Add(nome);
                FiltroProfissionalRegistro = ProfissionaisDaLista.Contains(escolhido)
                    ? escolhido : TodosProfissionais;
            }
            finally
            {
                _montandoProfissionais = false;
            }

            Refiltrar();
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            // A base também esvazia: refiltrar sobre resto de carga antiga repovoaria a
            // tela por baixo do "não verificado".
            Pendentes.Clear();
            _todas.Clear();
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — sessões sem evolução não puderam ser carregadas", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    /// <summary>
    /// Aplica o filtro sobre o que já foi lido — em memória, sem ida ao banco (o padrão
    /// da tela de Consultas da Recepção).
    /// </summary>
    private void Refiltrar()
    {
        Pendentes.Clear();
        foreach (var l in _todas.Where(l =>
                     (FiltroProfissionalRegistro == TodosProfissionais
                      || l.Profissional == FiltroProfissionalRegistro)
                     && Busca.Casa(l.Paciente, FiltroPacienteRegistro)))
            Pendentes.Add(l);

        OnPropertyChanged(nameof(FiltroAtivo));
        OnPropertyChanged(nameof(Vazio));

        // O resumo DIZ que está filtrado: a dívida é fila de trabalho, e "3 sessões" que
        // eram 20 faria alguém dar o prontuário por quase em dia.
        Resumo = _todas.Count == 0
            ? $"Nenhuma sessão dos últimos {ConsultorioService.JanelaRegistroPendenteDias} "
              + "dias está sem evolução escrita."
            : FiltroAtivo
                ? $"{Pendentes.Count} de {_todas.Count} sessão(ões) sem evolução no filtro. "
                  + "A mais antiga primeiro."
                : $"{_todas.Count} sessão(ões) atendida(s) nos últimos "
                  + $"{ConsultorioService.JanelaRegistroPendenteDias} dias continuam sem "
                  + "evolução escrita. A mais antiga primeiro.";

        VazioDescricao = FiltroAtivo
            ? "Nenhuma sessão bate com o filtro — limpe-o para ver a dívida inteira."
            : "Toda sessão atendida nos últimos dias já tem evolução escrita.";
    }

    /// <summary>
    /// Fixa o paciente no posto e abre o atendimento com o AGENDAMENTO da sessão — é esse
    /// vínculo que faz a linha sair daqui depois de a evolução ser escrita.
    /// </summary>
    [RelayCommand]
    private void Escrever(LinhaRegistroPendente? linha)
    {
        if (linha is null) return;

        _foco.Definir(linha.PacienteId, linha.Paciente, linha.AgendamentoId,
                      dataDoHorario: linha.Data);
        NavegacaoSuite.Ir(ModuloClinico.ChaveAtendimento);
    }
}
