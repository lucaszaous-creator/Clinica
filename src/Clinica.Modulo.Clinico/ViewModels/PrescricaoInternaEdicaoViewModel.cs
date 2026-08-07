using Clinica.Application.Servicos;
using Clinica.Clinico.Janelas;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using Clinica.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace Clinica.Clinico.ViewModels;

/// <summary>
/// Uma linha da prescrição sendo escrita.
///
/// Os campos são separados — droga, dose, diluente, volume, via, tempo — e não uma caixa
/// de texto livre, porque cada um deles é conferido isoladamente por quem prepara. Lido de
/// um campo só, "Dipirona 1g + SF 0,9% 100mL EV em 30 min" obriga a técnica a fazer a
/// separação de cabeça, toda vez, com o paciente na cadeira — e é aí que se troca o diluente.
/// </summary>
public sealed partial class LinhaItemPrescricao : ObservableObject
{
    [ObservableProperty] private string _descricao = string.Empty;
    [ObservableProperty] private string? _dose;
    [ObservableProperty] private string? _diluente;
    [ObservableProperty] private string? _volume;
    [ObservableProperty] private ViaAdministracao _via = ViaAdministracao.Endovenosa;
    [ObservableProperty] private string? _tempoInfusao;
    [ObservableProperty] private string? _horaPrevista;
    [ObservableProperty] private bool _seNecessario;
    [ObservableProperty] private string? _observacoes;

    public static LinhaItemPrescricao De(ItemPrescricaoInterna item) => new()
    {
        Descricao = item.Descricao,
        Dose = item.Dose,
        Diluente = item.Diluente,
        Volume = item.Volume,
        Via = item.Via,
        TempoInfusao = item.TempoInfusao,
        HoraPrevista = item.HoraPrevista?.ToString("HH\\:mm"),
        SeNecessario = item.SeNecessario,
        Observacoes = item.Observacoes
    };

    public ItemPrescricaoInterna Para() => new()
    {
        Descricao = Descricao,
        Dose = Dose,
        Diluente = Diluente,
        Volume = Volume,
        Via = Via,
        TempoInfusao = TempoInfusao,
        // Hora vazia é o normal (numa sessão de infusão a ordem basta) e hora ilegível não
        // trava o salvamento: vira "sem horário previsto", que é o que ela significa.
        HoraPrevista = TimeOnly.TryParse(HoraPrevista, out var hora) ? hora : null,
        SeNecessario = SeNecessario,
        Observacoes = Observacoes
    };
}

/// <summary>
/// Escrever e ASSINAR a prescrição de execução interna (parcela 42).
///
/// O fluxo tem dois passos porque a assinatura é irreversível
/// ----------------------------------------------------------
/// Salvar deixa em rascunho — editável, invisível para a sala de infusão. Assinar é o que
/// põe a folha na sala, e a partir daí ela não se edita mais: corrigir passa a ser
/// suspender o item e prescrever outro. Juntar os dois num botão só faria a médica assinar
/// sem perceber que assinou.
///
/// A conferência de alergia acontece ANTES do certificado
/// ------------------------------------------------------
/// Quando há coincidência com alergia registrada, a tela mostra os achados e pede uma
/// confirmação escrita — e só depois abre a escolha do certificado. A ordem importa:
/// confirmar depois de assinar é o mesmo que não confirmar.
/// </summary>
public sealed partial class PrescricaoInternaEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly IDialogoService _dialogo;
    private readonly int _pacienteId;
    private readonly int? _profissionalId;
    private readonly int? _agendamentoId;

    public ObservableCollection<LinhaItemPrescricao> Itens { get; } = [];

    /// <summary>Alergias e medicação contínua do paciente — o que se olha ANTES de escrever.</summary>
    public ObservableCollection<string> Alertas { get; } = [];

    public IReadOnlyList<ViaAdministracao> Vias { get; } = Enum.GetValues<ViaAdministracao>();

    [ObservableProperty] private string _paciente = string.Empty;
    [ObservableProperty] private string _numero = "—";
    [ObservableProperty] private string? _indicacao;
    [ObservableProperty] private string? _observacoes;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private bool _temAlertas;

    /// <summary>A folha já foi assinada nesta janela — quem abriu recarrega a lista.</summary>
    public bool Assinou { get; private set; }

    private int _prescricaoId;

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodePrescrever => SessaoUsuario.Atual.Pode(Permissao.Prescrever);

    /// <summary>Sem item não se assina — e o botão diz isso antes do clique.</summary>
    public bool PodeAssinar => PodePrescrever && !Ocupado && Itens.Count > 0;

    /// <param name="prescricaoId">
    /// Rascunho existente a REABRIR. Nulo cria uma prescrição nova.
    ///
    /// Sem este parâmetro, salvar um rascunho e fechar a janela deixava a prescrição
    /// inalcançável: a lista só oferecia "Abrir", que leva à folha de EXECUÇÃO (a tela da
    /// enfermagem), e não havia caminho de volta para a edição. <c>PrescricaoInterna
    /// .PodeEditar</c> existe no domínio desde a parcela 42 dizendo que rascunho se edita —
    /// e nenhuma tela oferecia isso.
    /// </param>
    public PrescricaoInternaEdicaoViewModel(
        IServiceScopeFactory escopos, IDialogoService dialogo,
        int pacienteId, string paciente, int? profissionalId, int? agendamentoId = null,
        int? prescricaoId = null)
    {
        _escopos = escopos;
        _dialogo = dialogo;
        _pacienteId = pacienteId;
        _profissionalId = profissionalId;
        _agendamentoId = agendamentoId;
        _prescricaoId = prescricaoId ?? 0;
        Paciente = paciente;

        Itens.CollectionChanged += (_, _) => OnPropertyChanged(nameof(PodeAssinar));

        // Só a prescrição NOVA nasce com uma linha em branco: reabrir um rascunho e ganhar
        // um item vazio no fim faria a folha impressa sair com uma linha fantasma se
        // ninguém reparasse.
        if (_prescricaoId == 0) AcrescentarItem();

        _ = PrepararAsync();
    }

    partial void OnOcupadoChanged(bool value) => OnPropertyChanged(nameof(PodeAssinar));

    /// <summary>Cria o rascunho no banco e carrega o contexto clínico do paciente.</summary>
    private async Task PrepararAsync()
    {
        try
        {
            Ocupado = true;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>();

            var prescricao = _prescricaoId == 0
                ? await servico.CriarAsync(
                    _pacienteId, _profissionalId, _agendamentoId,
                    operador: SessaoUsuario.Atual.Operador)
                : await servico.ObterAsync(_prescricaoId)
                  ?? throw new InvalidOperationException("Prescrição não encontrada.");

            // Assinada não se edita: os bytes já foram selados, e deixar a tela abrir para
            // edição faria a médica digitar por nada e descobrir na hora de salvar.
            if (!prescricao.PodeEditar)
                throw new InvalidOperationException(
                    $"A prescrição {prescricao.Numero} já foi assinada e não pode mais ser "
                    + "editada. Cancele e emita outra, se for o caso.");

            _prescricaoId = prescricao.Id;
            Numero = prescricao.Numero;

            if (prescricao.Itens.Count > 0)
            {
                Indicacao = prescricao.Indicacao;
                Observacoes = prescricao.Observacoes;

                Itens.Clear();
                foreach (var item in prescricao.Itens.OrderBy(i => i.Ordem))
                    Itens.Add(LinhaItemPrescricao.De(item));

                AcrescentarItem();   // uma linha livre para acrescentar
            }

            var contexto = await servico.ContextoAsync(_pacienteId);
            Alertas.Clear();
            foreach (var alergia in contexto.Alergias)
                Alertas.Add($"ALERGIA: {alergia.Rotulo}");
            foreach (var medicacao in contexto.MedicacoesEmUso)
                Alertas.Add($"Em uso contínuo: {medicacao.Descricao}");

            TemAlertas = Alertas.Count > 0;
        }
        catch (Exception ex)
        {
            Application.Diagnostico.Registrar(
                "Consultório — rascunho de prescrição não pôde ser criado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Ocupado = false;
        }
    }

    [RelayCommand]
    private void AcrescentarItem() => Itens.Add(new LinhaItemPrescricao());

    [RelayCommand]
    private void RemoverItem(LinhaItemPrescricao? linha)
    {
        if (linha is null) return;
        Itens.Remove(linha);
    }

    [RelayCommand]
    private async Task SalvarRascunhoAsync()
    {
        if (await GravarAsync() is null) return;
        Mensagem = "Rascunho salvo. Ele ainda NÃO aparece na sala de infusão — só a "
                 + "assinatura o põe lá.";
        MensagemEhErro = false;
    }

    /// <summary>
    /// Confere, pede confirmação se houver alergia, escolhe o certificado e assina.
    /// </summary>
    [RelayCommand]
    private async Task AssinarAsync()
    {
        // Guardas que DIZEM por que não dá. O botão já nasce apagado nos dois casos, mas
        // atalho de teclado e corrida de carregamento chegam aqui — e guarda que volta em
        // silêncio é botão que não faz nada.
        if (Itens.Count == 0)
        {
            Mensagem = "Acrescente ao menos um item antes de assinar: uma folha assinada em "
                     + "branco vira espaço para alguém escrever uma linha depois.";
            MensagemEhErro = true;
            return;
        }

        if (_prescricaoId == 0)
        {
            Mensagem = "O rascunho ainda não foi criado. Aguarde um instante e tente de novo.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.Prescrever, "prescrever");

            if (await GravarAsync() is null) return;

            Ocupado = true;

            var confirmouAlergia = false;

            using (var scope = _escopos.CreateScope())
            {
                var servico = scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>();
                var conferencia = await servico.ConferirParaAssinaturaAsync(_prescricaoId);

                if (conferencia.ExigeConfirmacao)
                {
                    var achados = conferencia.Alertas
                        .Where(a => a.Gravidade == GravidadePrescricao.Alergia)
                        .Select(a => $"• {a.Item}\n  {a.Motivo}");

                    confirmouAlergia = _dialogo.ConfirmarPerigo(
                        "Alergia registrada",
                        "Há item prescrito que bate com alergia registrada deste paciente:\n\n"
                        + string.Join("\n\n", achados)
                        + "\n\nO sistema não impede — quem decide é quem assina. Confirma "
                        + "que viu os alertas e quer prosseguir?");

                    if (!confirmouAlergia)
                    {
                        Mensagem = "Assinatura cancelada. Corrija os itens ou revise a lista "
                                 + "de problemas do paciente.";
                        MensagemEhErro = false;
                        return;
                    }
                }
            }

            var certificado = EscolherCertificadoWindow.Perguntar(
                $"Prescrição {Numero} — {Paciente}",
                System.Windows.Application.Current?.MainWindow, _escopos);

            if (certificado is null)
            {
                // Diálogo cancelado: sair calado é o certo aqui, e é a exceção que a
                // regra do "botão que não faz nada" prevê para guarda sobre variável local.
                return;
            }

            using (var scope = _escopos.CreateScope())
            {
                var assinaturas = scope.ServiceProvider
                    .GetRequiredService<AssinaturaDePrescricaoService>();

                // UsuarioId é 0 quando não há login (o shell só chega aqui autenticado,
                // mas `SessaoUsuario` libera sem sessão de propósito). Gravar 0 numa chave
                // estrangeira quebraria a inserção — nulo é o que "sem login" significa.
                await assinaturas.AssinarPrescricaoAsync(
                    _prescricaoId, certificado, confirmouAlergia,
                    SessaoUsuario.Atual.Autenticado ? SessaoUsuario.Atual.UsuarioId : null,
                    SessaoUsuario.Atual.Operador);
            }

            Assinou = true;
            Fechar?.Invoke();
        }
        catch (Exception ex)
        {
            Application.Diagnostico.Registrar(
                "Consultório — prescrição não pôde ser assinada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>Grava o rascunho. Devolve null quando recusou — o chamador não segue.</summary>
    private async Task<PrescricaoInterna?> GravarAsync()
    {
        var itens = Itens
            .Where(i => !string.IsNullOrWhiteSpace(i.Descricao))
            .Select(i => i.Para())
            .ToList();

        if (itens.Count == 0)
        {
            Mensagem = "Escreva ao menos um item (o medicamento é obrigatório; dose, "
                     + "diluente e volume ajudam quem vai preparar).";
            MensagemEhErro = true;
            return null;
        }

        try
        {
            Ocupado = true;
            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>();

            var salva = await servico.SalvarRascunhoAsync(
                _prescricaoId, Indicacao, Observacoes, itens,
                SessaoUsuario.Atual.Operador);

            Mensagem = null;
            MensagemEhErro = false;
            return salva;
        }
        catch (Exception ex)
        {
            Application.Diagnostico.Registrar(
                "Consultório — rascunho de prescrição não pôde ser salvo", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
            return null;
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>A janela liga isto ao próprio fechamento — o VM não conhece WPF.</summary>
    public Action? Fechar { get; set; }
}
