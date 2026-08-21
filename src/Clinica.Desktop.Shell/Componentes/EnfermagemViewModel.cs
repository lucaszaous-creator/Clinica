using System.Collections.ObjectModel;
using Clinica.Application;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// A TELA DA ENFERMAGEM (parcela 71) — onde ela acompanha e escreve.
///
/// Por que ela precisou existir
/// ----------------------------
/// A evolução de enfermagem nasceu com duas portas: a fila da sala de infusão e a ficha do
/// paciente. As duas resolvem o caso da INFUSÃO — e a clínica disse a frase que muda o
/// desenho: <b>"todo paciente precisa passar pela enfermagem"</b>. A maioria dessas
/// passagens não tem folha nenhuma (curativo, triagem, observação, pós-consulta), e a
/// enfermeira não tinha de onde alcançá-las: a sala só mostra as folhas do dia, e a ficha
/// exige saber o nome e passar pelo módulo da recepção.
///
/// ⚠️ É tela SEPARADA da sala de infusão, e isso é decisão: a sala responde <i>"o que
/// executar agora"</i>; esta responde <i>"quem eu atendi e o que escrevi"</i>. Terceira
/// pergunta, terceira tela — e juntá-las faria a fila do dia disputar espaço com a carteira
/// inteira da clínica.
///
/// A forma: LISTA → TELA DO PACIENTE
/// ---------------------------------
/// A lista tem a largura inteira e traz <b>todos os pacientes cadastrados</b>
/// (<c>limite: null</c>); a evolução mora atrás de um clique. Grudar a lista à esquerda da
/// evolução seria a faixa lateral que o README proíbe — e que o cliente já reprovou sete
/// vezes.
/// </summary>
public partial class EnfermagemViewModel : ObservableObject, ICarregarAoAbrir
{
    private readonly IServiceScopeFactory _escopos;
    private readonly IDialogoService _dialogo;

    /// <summary>Descarte de resposta fora de ordem: a lista troca de paciente a cada clique.</summary>
    private int _geracaoCarga;

    /// <summary>
    /// TODOS os pacientes cadastrados — <c>limite: null</c>, como a listagem do balcão. A
    /// clínica pediu isto com todas as letras: a enfermagem enxerga a carteira inteira,
    /// não só quem tem folha hoje.
    /// </summary>
    public SeletorPacienteViewModel Seletor { get; }

    public ObservableCollection<LinhaEvolucaoEnfermagem> Registros { get; } = new();

    [ObservableProperty] private bool _mostrandoLista = true;
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    [ObservableProperty] private string _paciente = string.Empty;
    [ObservableProperty] private string _contexto = string.Empty;
    private int _pacienteId;

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeRegistrar =>
        SessaoUsuario.Atual.Pode(Permissao.RegistrarEvolucaoEnfermagem);

    public EnfermagemViewModel(IServiceScopeFactory escopos, IDialogoService dialogo)
    {
        _escopos = escopos;
        _dialogo = dialogo;

        Seletor = new SeletorPacienteViewModel(escopos, limite: null);

        // UM clique abre a tela do paciente: quem escolhe alguém na carteira quer a
        // evolução dele, não uma seleção que não faz nada.
        Seletor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SeletorPacienteViewModel.Selecionado)
                && Seletor.Selecionado is { } escolhido)
                _ = AbrirAsync(escolhido);
        };
    }

    /// <summary>
    /// O shell resolve só o DataContext — quem abre a busca é este contrato. Sem ele a
    /// tela nasceria com a lista VAZIA, e tela vazia se lê como sistema quebrado.
    /// </summary>
    public Task CarregarAsync() => Seletor.BuscarAsync(imediato: true);

    /// <summary>Abre a tela DO PACIENTE — a evolução dele, com a largura inteira.</summary>
    [RelayCommand]
    private async Task AbrirAsync(Paciente? paciente)
    {
        // Guarda sobre PARÂMETRO: nunca dispara vindo de botão de linha (a exceção
        // declarada da checagem 21).
        if (paciente is null) return;

        _pacienteId = paciente.Id;
        Paciente = paciente.Nome;
        Contexto = string.IsNullOrWhiteSpace(paciente.ConvenioNome)
            ? string.Empty
            : paciente.ConvenioNome;
        MostrandoLista = false;
        Mensagem = null;

        await RecarregarAsync();
    }

    [RelayCommand]
    private void Voltar()
    {
        // ⚠️ Limpar a seleção: sem isto, escolher o MESMO paciente de novo não dispara o
        // PropertyChanged, e o clique não faz nada — o defeito da parcela 41.
        Seletor.Selecionado = null;
        MostrandoLista = true;
        Mensagem = null;
        Registros.Clear();
        _pacienteId = 0;
    }

    /// <summary>
    /// Escreve a evolução do paciente aberto — a passagem AVULSA, que é o caso normal
    /// fora da infusão. A janela é a mesma da fila da sala: quatro montagens da mesma tela
    /// divergiriam na primeira correção.
    /// </summary>
    [RelayCommand]
    private async Task RegistrarAsync()
    {
        if (_pacienteId == 0)
        {
            // A guarda DIZ por que não dá, em vez de voltar calada (parcela 41).
            Mensagem = "Abra um paciente para escrever a evolução.";
            MensagemEhErro = true;
            return;
        }

        SessaoUsuario.Atual.Exigir(
            Permissao.RegistrarEvolucaoEnfermagem, "registrar evolução de enfermagem");

        EvolucaoEnfermagemWindow.Abrir(_escopos, _dialogo, _pacienteId, Paciente);

        await RecarregarAsync();
    }

    private async Task RecarregarAsync()
    {
        if (_pacienteId == 0) return;

        var geracao = ++_geracaoCarga;
        var pacienteId = _pacienteId;
        Carregando = true;
        NaoVerificado = false;

        try
        {
            using var scope = _escopos.CreateScope();

            // ⚠️ A trilha de LEITURA (parcela 52): abrir a evolução de alguém é acesso a
            // dado de saúde, e é disparada na TROCA de paciente — nunca a cada carga, que
            // acontece também depois de escrever.
            if (geracao == _geracaoCarga)
                await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(pacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.ProntuarioClinico);

            var lista = await scope.ServiceProvider
                .GetRequiredService<EvolucaoEnfermagemService>()
                .DoPacienteAsync(pacienteId, limite: 100);

            if (geracao != _geracaoCarga) return;

            var substituidas = lista
                .Where(e => e.RetificaEvolucaoId is not null)
                .Select(e => e.RetificaEvolucaoId!.Value)
                .ToHashSet();

            // Entre o Clear() e o último Add não pode haver await (parcela 62).
            var linhas = lista
                .Select(e => LinhaEvolucaoEnfermagem.De(
                    e, substituidas.Contains(e.Id), mostrarData: true))
                .ToList();

            Registros.Clear();
            foreach (var l in linhas) Registros.Add(l);
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;
            NaoVerificado = true;
            Diagnostico.Registrar("Enfermagem — evolução do paciente não pôde ser carregada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }
}
