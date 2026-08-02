using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
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

/// <summary>Uma sessão anterior, do jeito que o consultório precisa relê-la: inteira.</summary>
public sealed class LinhaSessaoAnterior
{
    public required int EvolucaoId { get; init; }
    public required string Data { get; init; }
    public required string Eva { get; init; }
    public required string Queixa { get; init; }
    public required string Conduta { get; init; }
    public required string Evolucao { get; init; }

    public static LinhaSessaoAnterior De(Evolucao e) => new()
    {
        EvolucaoId = e.Id,
        Data = e.Data.ToString("dd/MM/yyyy"),
        Eva = e.TemParEva ? $"EVA {e.EvaAntes} → {e.EvaDepois}" : "EVA não medida",
        Queixa = string.IsNullOrWhiteSpace(e.QueixaPrincipal) ? "—" : e.QueixaPrincipal!,
        Conduta = string.IsNullOrWhiteSpace(e.Conduta) ? "—" : e.Conduta!,
        Evolucao = string.IsNullOrWhiteSpace(e.TextoEvolucao) ? "—" : e.TextoEvolucao!
    };
}

/// <summary>
/// A tela do ATENDIMENTO — onde a sessão é escrita enquanto o paciente ainda está na sala.
///
/// Por que não é a janela de evolução da recepção
/// ----------------------------------------------
/// A recepção escreve evolução de vez em quando, num diálogo modal aberto de dentro do
/// prontuário. O profissional escreve TODA sessão, e enquanto conversa com alguém. São
/// dois usos diferentes do mesmo dado, e a diferença aparece no leiaute: aqui as três
/// últimas sessões ficam ABERTAS ao lado do formulário, porque a primeira coisa que se faz
/// ao receber um paciente de tratamento é reler o que foi feito da última vez. Numa janela
/// modal isso não cabe — e a arquitetura da suíte não permitiria reaproveitá-la de outro
/// módulo de qualquer forma (nenhum módulo conhece os outros).
///
/// A EVA em par
/// ------------
/// Antes e depois, sempre. É a regra que o projeto inteiro aplica: uma medida solta não
/// diz se a sessão funcionou, e o campo "depois" é preenchido no fim do atendimento — por
/// isso a tela permite salvar com só o "antes" e volta a cobrar o par no resumo.
/// </summary>
public sealed partial class AtendimentoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly PacienteEmFoco _foco;

    /// <summary>Quantas sessões anteriores ficam abertas ao lado do formulário.</summary>
    private const int SessoesAnterioresVisiveis = 3;

    public SeletorPacienteViewModel Seletor { get; }

    public ObservableCollection<LinhaSessaoAnterior> Anteriores { get; } = [];

    /// <summary>Evolução em edição. 0 = sessão nova.</summary>
    [ObservableProperty] private int _evolucaoId;

    [ObservableProperty] private string _paciente = string.Empty;
    [ObservableProperty] private bool _semPaciente = true;

    [ObservableProperty] private DateTime _data = DateTime.Today;

    [ObservableProperty] private int? _evaAntes;
    [ObservableProperty] private int? _evaDepois;

    [ObservableProperty] private string? _queixaPrincipal;
    [ObservableProperty] private string? _conduta;
    [ObservableProperty] private string? _textoEvolucao;
    [ObservableProperty] private string? _orientacoes;

    /// <summary>De onde veio a sessão: chamada do dia, ou escolhida na busca.</summary>
    [ObservableProperty] private string _origem = string.Empty;

    [ObservableProperty] private string _resumoDor = string.Empty;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Metade VISÍVEL da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditarProntuario => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    /// <summary>Valores da escala, para os dois seletores de dor.</summary>
    public IReadOnlyList<int> EscalaEva { get; } =
        Enumerable.Range(Evolucao.EvaMinima, Evolucao.EvaMaxima - Evolucao.EvaMinima + 1).ToList();

    public AtendimentoViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, PacienteEmFoco foco)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _foco = foco;

        Seletor = new SeletorPacienteViewModel(escopos);
        Seletor.SelecaoMudou += AoTrocarPaciente;

        // O paciente do posto: quem veio da agenda já chega escolhido, e o profissional
        // não redigita o nome que acabou de clicar.
        if (_foco.Definido)
        {
            Paciente = _foco.Nome;
            SemPaciente = false;
            Origem = _foco.AgendamentoId is null
                ? "Paciente escolhido na busca — a evolução não fica ligada a nenhum horário."
                : "Sessão chamada da agenda de hoje.";
            _ = CarregarAsync();
        }
    }

    private void AoTrocarPaciente(Paciente? paciente)
    {
        if (paciente is null) return;

        // Trocar de pessoa esquece o horário de origem, senão a evolução do novo paciente
        // nasceria amarrada à sessão do anterior.
        _foco.Definir(paciente.Id, paciente.Nome);
        Paciente = paciente.Nome;
        SemPaciente = false;
        Origem = "Paciente escolhido na busca — a evolução não fica ligada a nenhum horário.";
        _ = CarregarAsync();
    }

    private int PacienteId => _foco.PacienteId ?? 0;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        if (PacienteId == 0)
        {
            SemPaciente = true;
            return;
        }

        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;
            Anteriores.Clear();

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();

            var sessoes = await prontuario.DoPacienteAsync(PacienteId);

            // A sessão do horário chamado, quando ela já foi escrita: abrir o atendimento
            // de novo tem de CONTINUAR o registro, nunca criar um segundo para a mesma
            // sessão — dois registros do mesmo atendimento é o defeito que faz a clínica
            // desconfiar do prontuário inteiro.
            var doHorario = _foco.AgendamentoId is { } agendamentoId
                ? sessoes.FirstOrDefault(e => e.AgendamentoId == agendamentoId)
                : null;

            if (doHorario is not null) Preencher(doHorario);
            else Limpar();

            foreach (var e in sessoes.Where(e => e.Id != EvolucaoId).Take(SessoesAnterioresVisiveis))
                Anteriores.Add(LinhaSessaoAnterior.De(e));

            var dor = await prontuario.EvolucaoDaDorAsync(PacienteId);
            ResumoDor = dor.SessoesComMedida == 0
                ? "Nenhuma sessão com o par EVA (antes e depois) ainda."
                : $"Começou em {dor.DorInicial}/10 e está em {dor.DorAtual}/10 — "
                  + $"{dor.SessoesComMedida} sessão(ões) medidas, alívio médio de "
                  + $"{dor.AlivioMedioPorSessao:0.#} por sessão.";
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar("Consultório — atendimento não pôde ser carregado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    private void Preencher(Evolucao e)
    {
        EvolucaoId = e.Id;
        Data = e.Data.ToDateTime(TimeOnly.MinValue);
        EvaAntes = e.EvaAntes;
        EvaDepois = e.EvaDepois;
        QueixaPrincipal = e.QueixaPrincipal;
        Conduta = e.Conduta;
        TextoEvolucao = e.TextoEvolucao;
        Orientacoes = e.Orientacoes;
    }

    private void Limpar()
    {
        EvolucaoId = 0;
        Data = DateTime.Today;
        EvaAntes = null;
        EvaDepois = null;
        QueixaPrincipal = null;
        Conduta = null;
        TextoEvolucao = null;
        Orientacoes = null;
    }

    /// <summary>
    /// Traz a conduta da última sessão para o formulário — sem gravar nada.
    ///
    /// É a mesma regra de "repetir a sessão anterior" do mapa corporal: o botão TRAZ para
    /// a tela, e só o Salvar efetiva. Tratamento de acupuntura repete protocolo por
    /// semanas, e redigitar a mesma conduta é como o registro vira "idem".
    /// </summary>
    [RelayCommand]
    private void RepetirUltima()
    {
        var ultima = Anteriores.FirstOrDefault();
        if (ultima is null)
        {
            Mensagem = "Não há sessão anterior para repetir.";
            MensagemEhErro = true;
            return;
        }

        if (ultima.Conduta != "—") Conduta = ultima.Conduta;
        if (ultima.Queixa != "—" && string.IsNullOrWhiteSpace(QueixaPrincipal))
            QueixaPrincipal = ultima.Queixa;

        Mensagem = $"Conduta da sessão de {ultima.Data} trazida para a tela. "
                   + "Nada foi gravado — confira e salve.";
        MensagemEhErro = false;
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            if (PacienteId == 0)
                throw new InvalidOperationException("Escolha o paciente antes de escrever a sessão.");

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();

            var salva = await prontuario.SalvarAsync(new Evolucao
            {
                Id = EvolucaoId,
                PacienteId = PacienteId,
                ProfissionalId = SessaoUsuario.Atual.ProfissionalId,
                // O vínculo com o horário é o que faz a sessão sair da lista de
                // pendências do consultório depois de escrita.
                AgendamentoId = _foco.AgendamentoId,
                AtendimentoId = _foco.AtendimentoId,
                Data = DateOnly.FromDateTime(Data),
                EvaAntes = EvaAntes,
                EvaDepois = EvaDepois,
                QueixaPrincipal = QueixaPrincipal,
                Conduta = Conduta,
                TextoEvolucao = TextoEvolucao,
                Orientacoes = Orientacoes
            }, SessaoUsuario.Atual.Operador);

            EvolucaoId = salva.Id;
            _snackbar.Sucesso("Sessão registrada no prontuário.");

            // O aviso do par incompleto vem DEPOIS de gravar, e não impede: o "depois" é
            // medido ao fim do atendimento, e recusar a gravação por causa dele faria o
            // profissional escrever tudo de novo — ou desistir de medir.
            Mensagem = EvaAntes is not null && EvaDepois is null
                ? "Gravado. A EVA está só com a medida ANTES — sem o par não dá para dizer "
                  + "se a sessão aliviou. Volte aqui ao terminar para registrar o depois."
                : null;
            MensagemEhErro = false;

            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Consultório — sessão não pôde ser salva", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>Abre a curva de dor deste paciente sem perder o foco do posto.</summary>
    [RelayCommand]
    private void VerEvolucaoDaDor() => NavegacaoSuite.Ir(ModuloClinico.ChaveEvolucaoDor);

    /// <summary>Abre as escalas deste paciente.</summary>
    [RelayCommand]
    private void VerAvaliacoes() => NavegacaoSuite.Ir(ModuloClinico.ChaveAvaliacoes);
}
