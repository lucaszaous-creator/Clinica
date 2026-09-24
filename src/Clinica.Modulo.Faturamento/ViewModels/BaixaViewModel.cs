using System.Windows.Input;
using System.Diagnostics;
using System.IO;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace Clinica.Desktop.ViewModels;

/// <summary>Registra a BAIXA de uma guia: data, número real da guia e forma de obtenção. Não trata recebíveis.</summary>
public partial class BaixaViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Controls.IDialogoService _dialogo;
    private int _codigoId;

    [ObservableProperty] private CodigoFaturamento? _codigo;
    [ObservableProperty] private string _pacienteNome = string.Empty;
    [ObservableProperty] private DateTime _dataBaixa = DateTime.Today;
    [ObservableProperty] private string? _numeroGuia;
    [ObservableProperty] private string? _observacao;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _ocupado;

    /// <summary>Observação registrada enquanto a guia estava pendente (por que não deu para baixar antes).</summary>
    [ObservableProperty] private string? _observacaoPendencia;
    public bool TemObservacaoPendencia => !string.IsNullOrWhiteSpace(ObservacaoPendencia);
    partial void OnObservacaoPendenciaChanged(string? value) => OnPropertyChanged(nameof(TemObservacaoPendencia));

    /// <summary>
    /// Forma do número da guia no sistema DESTE convênio (parcela 45) — o que decide se o
    /// campo aceita letra. Resolvida ao carregar a guia, e não no clique: é ela que
    /// escreve a dica ao lado do campo.
    /// </summary>
    public FormatoNumeroGuia FormatoGuia { get; private set; } = FormatoNumeroGuia.SemValidacao;

    /// <summary>
    /// "Este convênio aceita somente números." — a frase ao lado do campo, ANTES de a
    /// pessoa errar. Vazia quando o convênio não tem formato declarado: dica que não diz
    /// nada ocupa a linha e ensina a ignorar o lugar onde as dicas aparecem.
    /// </summary>
    [ObservableProperty] private string? _dicaNumeroGuia;
    public bool TemDicaNumeroGuia => !string.IsNullOrWhiteSpace(DicaNumeroGuia);
    partial void OnDicaNumeroGuiaChanged(string? value) => OnPropertyChanged(nameof(TemDicaNumeroGuia));

    /// <summary>
    /// A crítica do que está DIGITADO AGORA — nula enquanto o número serve.
    ///
    /// A dica acima é estática ("aceita somente números") e, sozinha, deixava digitar oito
    /// letras sem nada acontecer até o clique em Confirmar: quem digita conclui que o
    /// sistema aceita. O serviço recusa de qualquer jeito — é lá que a regra mora, porque
    /// a baixa tem quatro portas —, mas descobrir o requisito DEPOIS de confirmar é o que
    /// faz a pessoa desistir da tela.
    ///
    /// É a mesma regra do domínio, sem cópia: quem decide continua sendo
    /// <see cref="RegraNumeroGuia.Criticar"/>.
    /// </summary>
    public string? CriticaNumeroGuia => RegraNumeroGuia.Criticar(NumeroGuia, FormatoGuia);
    public bool TemCriticaNumeroGuia => CriticaNumeroGuia is not null;

    /// <summary>
    /// O número serve? Falso também com o campo VAZIO — não porque a forma o recuse (número
    /// em branco passa em qualquer formato, por decisão do domínio), mas porque a baixa
    /// exige o número, e o botão precisa dizer isso antes do clique.
    /// </summary>
    public bool NumeroGuiaServe =>
        !string.IsNullOrWhiteSpace(NumeroGuia) && CriticaNumeroGuia is null;

    partial void OnNumeroGuiaChanged(string? value)
    {
        OnPropertyChanged(nameof(CriticaNumeroGuia));
        OnPropertyChanged(nameof(TemCriticaNumeroGuia));
        OnPropertyChanged(nameof(NumeroGuiaServe));
        OnPropertyChanged(nameof(PodeConfirmar));
    }

    /// <summary>
    /// Metade VISÍVEL da barreira de permissão: o botão explica. A que IMPEDE é o
    /// <c>Exigir</c> dentro do comando — só desabilitar seria enfeite, porque o atalho
    /// Ctrl+S do shell dispara o mesmo comando por outro caminho.
    /// </summary>
    public bool PodeBaixar => SessaoUsuario.Atual.Pode(Permissao.BaixarGuia);

    /// <summary>
    /// As DUAS pré-condições do botão, juntas: permissão e número que serve. A guarda
    /// dentro do comando continua existindo e continua DIZENDO por quê — botão apagado
    /// explica, guarda impede, e guarda que volta calada é botão que não faz nada.
    /// </summary>
    public bool PodeConfirmar => PodeBaixar && NumeroGuiaServe;

    public event Action? BaixaConcluida;
    public event Action? Cancelado;

    public BaixaViewModel(IServiceScopeFactory scopeFactory, Controls.IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;
    }

    public async Task CarregarAsync(int codigoId)
    {
        _codigoId = codigoId;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        Codigo = await db.Codigos
            .Include(c => c.Atendimento!).ThenInclude(a => a.Paciente!)
            .FirstOrDefaultAsync(c => c.Id == codigoId);
        PacienteNome = Codigo?.Atendimento?.Paciente?.Nome ?? string.Empty;

        var paciente = Codigo?.Atendimento?.Paciente;
        FormatoGuia = paciente is null
            ? FormatoNumeroGuia.SemValidacao
            : CatalogoConvenios.FormatoDoNumeroDaGuia(
                paciente.ConvenioCodigo ?? paciente.Convenio.ToString());
        DicaNumeroGuia = RegraNumeroGuia.Dica(FormatoGuia);

        // O formato só é conhecido AQUI, depois de ler o paciente — e é ele que decide a
        // crítica. Sem reavaliar, o campo já preenchido ficaria sem julgamento nenhum.
        OnPropertyChanged(nameof(CriticaNumeroGuia));
        OnPropertyChanged(nameof(TemCriticaNumeroGuia));
        OnPropertyChanged(nameof(NumeroGuiaServe));
        OnPropertyChanged(nameof(PodeConfirmar));
        ObservacaoPendencia = Codigo?.ObservacaoPendencia is { } obs && Codigo.ObservacaoPendenciaEm is { } quando
            ? $"{obs}  (anotado em {quando:dd/MM/yyyy HH:mm})"
            : Codigo?.ObservacaoPendencia;
    }

    [RelayCommand]
    private async Task Confirmar()
    {
        SessaoUsuario.Atual.Exigir(Permissao.BaixarGuia, "dar baixa na guia");

        if (string.IsNullOrWhiteSpace(NumeroGuia))
        {
            Mensagem = "Informe o número da guia gerada no sistema do convênio.";
            return;
        }

        // Crítica do formato ANTES da confirmação: o serviço recusa de qualquer jeito
        // (é lá que a regra mora, porque a baixa tem quatro portas), mas descobrir o
        // requisito depois de confirmar é o que faz a pessoa desistir da tela.
        if (RegraNumeroGuia.Criticar(NumeroGuia, FormatoGuia) is { } critica)
        {
            Mensagem = critica;
            return;
        }

        if (!_dialogo.Confirmar("Confirmar baixa",
            $"Confirmar a baixa da guia {NumeroGuia} de {PacienteNome}?")) return;

        var atendimentoId = Codigo?.AtendimentoId ?? 0;

        // Guarda contra duplo clique: baixa duplicada corrompe o histórico da guia.
        if (Ocupado) return;
        Ocupado = true;
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<FaturamentoService>();
                await service.DarBaixaAsync(_codigoId, DateOnly.FromDateTime(DataBaixa),
                    NumeroGuia, SessaoUsuario.Atual.Operador, Observacao);
            }
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível registrar a baixa: {ex.Message}";
            return;
        }
        finally
        {
            Ocupado = false;
        }

        await OferecerCapaConclusaoAsync(atendimentoId);

        BaixaConcluida?.Invoke();
    }

    /// <summary>Se esta baixa concluiu a fatura, oferece gerar a capa de conclusão para imprimir e arquivar.</summary>
    private async Task OferecerCapaConclusaoAsync(int atendimentoId)
    {
        if (atendimentoId == 0) return;

        try
        {
            CapaConclusao resultado;
            using (var scope = _scopeFactory.CreateScope())
            {
                var capa = scope.ServiceProvider.GetRequiredService<CapaFaturamentoService>();
                var prestador = await scope.ServiceProvider.GetRequiredService<ParametrosService>().ObterPrestadorAsync();
                resultado = await capa.GerarConclusaoAsync(atendimentoId, prestador);
                if (!resultado.Concluido || resultado.Pdf is null) return;
            }

            if (!_dialogo.Confirmar("Fatura concluída",
                "Fatura concluída! Gerar a capa de conclusão para imprimir e arquivar na pasta do dia?")) return;

            var dialog = new SaveFileDialog
            {
                FileName = $"Capa-CONCLUIDA-{resultado.Numero}-{resultado.Data:yyyy-MM-dd}.pdf",
                Filter = "PDF (*.pdf)|*.pdf",
                DefaultExt = ".pdf"
            };
            if (dialog.ShowDialog() != true) return;

            await File.WriteAllBytesAsync(dialog.FileName, resultado.Pdf);
            Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            // A geração da capa nunca deve impedir a baixa.
            Configuracao.LogErros.Registrar("Baixa — geração da capa falhou", ex);
        }
    }

    [RelayCommand]
    private void Cancelar() => Cancelado?.Invoke();

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoSalvar => ConfirmarCommand;
}
