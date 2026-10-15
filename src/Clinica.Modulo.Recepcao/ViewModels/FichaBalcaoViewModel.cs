using System.Collections.Specialized;
using Clinica.Application.Modelos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// A FICHA DO BALCÃO (out/2026, mockup aprovado "4 · Ficha").
///
/// Até aqui a Recepção abria a ficha ÚNICA do Consultório — rail de prontuário à esquerda,
/// sessão médica na frente e o cadastro enterrado numa aba da seção "Ficha do paciente".
/// Para quem está no balcão a pergunta é outra: quem é, quando volta, o convênio está em
/// dia, o que falta assinar. Esta é a tela dessa pergunta: cabeçalho da pessoa uma vez, os
/// alertas como pílulas, e sete abas — Resumo, Agenda e sessões, Convênio e guias, Pacotes
/// e pagamentos, Documentos, Contatos e LGPD.
///
/// NADA foi reescrito: os dados e as ações são os do <see cref="FichaPacienteViewModel"/>
/// (a mesma leitura, as mesmas barreiras, a mesma trilha de acesso), e as abas Convênio,
/// Contatos, LGPD e Termos são as views que já existiam. O prontuário clínico continua a um
/// botão — "Prontuário", para quem tem <c>VerProntuario</c> — e abre a ficha única de sempre.
/// </summary>
public sealed partial class FichaBalcaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly Action? _voltar;

    public FichaPacienteViewModel Ficha { get; }

    [ObservableProperty] private int _abaAtual;

    /// <summary>"Senha 88123 · 7 de 10 sessões usadas" — a autorização vigente do convênio.</summary>
    [ObservableProperty] private string _autorizacaoTexto = "Nenhuma autorização registrada";
    [ObservableProperty] private string _autorizacaoValidade = "—";

    public bool PodeMarcar => SessaoUsuario.Atual.Pode(Permissao.EditarAgenda);
    public bool PodeVerProntuario => SessaoUsuario.Atual.Pode(Permissao.VerProntuario);
    public bool PodeEditarCadastro => SessaoUsuario.Atual.Pode(Permissao.EditarPaciente);

    public FichaBalcaoViewModel(IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo,
        int pacienteId, Action? voltar, int abaInicial = 0)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _voltar = voltar;
        AbaAtual = abaInicial;
        Ficha = new FichaPacienteViewModel(escopos, snackbar, dialogo);
        Ficha.Autorizacoes.CollectionChanged += (_, _) => ResumirAutorizacao();
        _ = Ficha.AbrirAsync(pacienteId);
    }

    private void ResumirAutorizacao()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var vigente = Ficha.Autorizacoes.FirstOrDefault(a => !a.Vencida && a.Autorizacao.Vigente(hoje))
                      ?? Ficha.Autorizacoes.FirstOrDefault();
        if (vigente is null)
        {
            AutorizacaoTexto = "Nenhuma autorização registrada";
            AutorizacaoValidade = "—";
            return;
        }
        var senha = string.IsNullOrWhiteSpace(vigente.Autorizacao.Numero) ? "Sem senha" : $"Senha {vigente.Autorizacao.Numero}";
        AutorizacaoTexto = $"{senha} · {vigente.Utilizadas} de {vigente.Autorizadas} sessões usadas";
        AutorizacaoValidade = vigente.Vencida
            ? $"vencida em {vigente.Autorizacao.DataValidade:dd/MM/yyyy}"
            : $"até {vigente.Autorizacao.DataValidade:dd/MM/yyyy}";
    }

    [RelayCommand] private void Voltar() => _voltar?.Invoke();

    [RelayCommand] private void IrPara(string? aba)
    {
        if (int.TryParse(aba, out var indice)) AbaAtual = indice;
    }

    [RelayCommand]
    private void Marcar()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "marcar atendimento");
            using var scope = _escopos.CreateScope();
            scope.ServiceProvider.GetRequiredService<PreenchimentoNovoAtendimento>()
                .Definir(new PedidoNovoAtendimento(MarcarParaDepois: true, DataHora: DateTime.Today,
                    ProfissionalId: null, SalaId: null, PacienteId: Ficha.PacienteId));
            if (!NavegacaoSuite.Ir(Modulo.ModuloRecepcao.ChaveMarcarHorario))
                _snackbar.Info("A tela de marcar atendimento não está disponível para o seu acesso.");
        }
        catch (Exception ex)
        {
            _snackbar.Erro(ex.Message);
        }
    }

    /// <summary>
    /// O prontuário clínico — a ficha ÚNICA do Consultório, numa janela por cima. A porta
    /// que a ficha do balcão não pode tirar de quem já a tinha (a regra 3 do faturamento).
    /// </summary>
    [RelayCommand]
    private void AbrirProntuario()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "abrir o prontuário");
            using var escopo = _escopos.CreateScope();
            var foco = new PacienteEmFoco();
            foco.Definir(Ficha.PacienteId, Ficha.Nome);
            ConsultaContextualWindow? janela = null;
            var ficha = escopo.ServiceProvider.GetRequiredService<IFabricaFichaPaciente>()
                .Criar(foco, () => janela?.Close());
            janela = new ConsultaContextualWindow($"Prontuário — {Ficha.Nome}", ficha, "Voltar à ficha");
            janela.ShowDialog();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — prontuário pela ficha do balcão", ex);
            _snackbar.Erro(ex.Message);
        }
    }
}
