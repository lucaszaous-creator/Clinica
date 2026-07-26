using System.Windows.Input;
using System.Collections.ObjectModel;
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

/// <summary>
/// Um código recém-gerado, com a informação que decide a ação da tela: dá para baixar
/// agora ou só depois? O 1º código já nasce faturável hoje; o 2º só a partir de +24h.
/// </summary>
public sealed record CodigoLancado(CodigoFaturamento Codigo, bool PodeBaixar, string? Impedimento)
{
    public bool Baixado => Codigo.Baixado;
}

/// <summary>Lança um atendimento. O sistema gera automaticamente os códigos (inclusive o 2º código +24h).</summary>
public partial class NovoAtendimentoViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Busca de paciente compartilhada (mesmo limite e mesmo comportamento das outras telas).</summary>
    public SeletorPacienteViewModel Seletor { get; }

    /// <summary>Atalho para o paciente escolhido no seletor.</summary>
    public Paciente? PacienteSelecionado => Seletor.Selecionado;

    public ObservableCollection<CodigoLancado> CodigosGerados { get; } = new();
    public ObservableCollection<string> Avisos { get; } = new();

    /// <summary>Placar das baixas do atendimento recém-lançado ("1 de 2 guias baixadas…").</summary>
    [ObservableProperty] private string? _resumoBaixas;

    /// <summary>Modalidades ativas do catálogo (embutidas + variantes criadas pela clínica).</summary>
    public ObservableCollection<EntradaModalidade> Modalidades { get; } = new();

    /// <summary>Especialidades ativas do catálogo (para a consulta avulsa).</summary>
    public ObservableCollection<EntradaEspecialidade> Especialidades { get; } = new();

    /// <summary>Opções de qual código sai primeiro (hoje) numa modalidade dupla. Vazio nas simples.</summary>
    public ObservableCollection<TipoCodigo> OpcoesPrimeiroCodigo { get; } = new();

    [ObservableProperty] private DateTime _data = DateTime.Today;
    [ObservableProperty] private EntradaModalidade? _modalidadeSelecionada;
    [ObservableProperty] private EntradaEspecialidade? _especialidadeSelecionada;
    [ObservableProperty] private TipoCodigo? _primeiroCodigo;
    [ObservableProperty] private string? _observacoes;
    [ObservableProperty] private bool _lancado;
    [ObservableProperty] private string? _numeroAtendimento;
    [ObservableProperty] private string? _mensagem;

    /// <summary>Aviso de guias pendentes do paciente selecionado (para a secretária cobrar na hora). Nulo = sem pendências.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAvisoPendencias))]
    private string? _avisoPendencias;

    /// <summary>Há aviso de pendências a exibir?</summary>
    public bool TemAvisoPendencias => !string.IsNullOrWhiteSpace(AvisoPendencias);

    /// <summary>Aviso de carteirinha vencida do paciente selecionado. Separado da mensagem de erro.</summary>
    [ObservableProperty] private string? _avisoCarteirinha;

    /// <summary>Já há paciente escolhido? Alterna a busca pelo resumo do paciente na tela.</summary>
    [ObservableProperty] private bool _pacienteEscolhido;

    /// <summary>Nome do convênio do paciente selecionado (resolvido pelo catálogo).</summary>
    [ObservableProperty] private string? _convenioPaciente;

    /// <summary>Cota de sessões: "Senha 12345 · 7 de 10 usadas — restam 3". Nulo = sem autorização vigente.</summary>
    [ObservableProperty] private string? _saldoAutorizacao;

    /// <summary>Cota esgotada ou autorização vencida: lançar agora é candidato à glosa 2006.</summary>
    [ObservableProperty] private bool _autorizacaoCritica;

    /// <summary>Resta uma sessão: hora de pedir a renovação da senha.</summary>
    [ObservableProperty] private bool _autorizacaoNaUltima;

    [ObservableProperty] private bool _ocupado;

    private int _ultimoAtendimentoId;

    /// <summary>Comportamento (base) da modalidade selecionada — o que o motor de regras usa.</summary>
    private ModalidadeAtendimento Modalidade =>
        ModalidadeSelecionada?.Base ?? ModalidadeAtendimento.AcupunturaComEletro;

    /// <summary>Modalidade dupla (gera 1º hoje + 2º em +24h): permite escolher qual código sai primeiro.</summary>
    public bool ModalidadeDupla =>
        Modalidade is ModalidadeAtendimento.AcupunturaComEletro or ModalidadeAtendimento.BsvComAcupuntura;

    /// <summary>Consulta avulsa: pede a especialidade (discriminada nos relatórios).</summary>
    public bool ModalidadeConsulta => Modalidade == ModalidadeAtendimento.Consulta;

    public NovoAtendimentoViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        Seletor = new SeletorPacienteViewModel(scopeFactory);
        Seletor.SelecaoMudou += AoTrocarPaciente;
        AtualizarOpcoesPrimeiroCodigo();
    }

    partial void OnModalidadeSelecionadaChanged(EntradaModalidade? value)
    {
        AtualizarOpcoesPrimeiroCodigo();
        if (Modalidade != ModalidadeAtendimento.Consulta)
            EspecialidadeSelecionada = null;
        OnPropertyChanged(nameof(ModalidadeDupla));
        OnPropertyChanged(nameof(ModalidadeConsulta));
    }

    /// <summary>Preenche as opções de "qual código primeiro" conforme a modalidade e escolhe o padrão.</summary>
    private void AtualizarOpcoesPrimeiroCodigo()
    {
        OpcoesPrimeiroCodigo.Clear();
        switch (Modalidade)
        {
            case ModalidadeAtendimento.AcupunturaComEletro:
                OpcoesPrimeiroCodigo.Add(TipoCodigo.Acupuntura);
                OpcoesPrimeiroCodigo.Add(TipoCodigo.Eletroacupuntura);
                break;
            case ModalidadeAtendimento.BsvComAcupuntura:
                OpcoesPrimeiroCodigo.Add(TipoCodigo.Bsv);
                OpcoesPrimeiroCodigo.Add(TipoCodigo.Acupuntura);
                break;
        }
        PrimeiroCodigo = OpcoesPrimeiroCodigo.Count > 0 ? OpcoesPrimeiroCodigo[0] : null;
    }

    public async Task CarregarAsync()
    {
        CarregarCatalogos();
        await Seletor.BuscarAsync(imediato: true);
    }

    /// <summary>Recarrega as opções de modalidade/especialidade do cache (reflete o que foi salvo em Configurações).</summary>
    private void CarregarCatalogos()
    {
        var modalidadeAtual = ModalidadeSelecionada?.Codigo;
        Modalidades.Clear();
        foreach (var m in CatalogoModalidades.Ativas)
            Modalidades.Add(m);
        ModalidadeSelecionada = Modalidades.FirstOrDefault(m => m.Codigo == modalidadeAtual)
            ?? Modalidades.FirstOrDefault(m => m.Base == ModalidadeAtendimento.AcupunturaComEletro)
            ?? Modalidades.FirstOrDefault();

        var especialidadeAtual = EspecialidadeSelecionada?.Codigo;
        Especialidades.Clear();
        foreach (var e in CatalogoEspecialidades.Ativas)
            Especialidades.Add(e);
        EspecialidadeSelecionada = Especialidades.FirstOrDefault(e => e.Codigo == especialidadeAtual);
    }

    // Pré-preenche a modalidade com a habitual do paciente (definida no cadastro)
    // e avisa carteirinha vencida ANTES de gerar uma guia que o convênio vai recusar.
    private void AoTrocarPaciente(Paciente? value)
    {
        OnPropertyChanged(nameof(PacienteSelecionado));
        AvisoPendencias = null;
        AvisoCarteirinha = null;
        SaldoAutorizacao = null;
        AutorizacaoCritica = false;
        AutorizacaoNaUltima = false;
        PacienteEscolhido = value is not null;
        ConvenioPaciente = value is null
            ? null
            : CatalogoConvenios.Nome(value.ConvenioCodigo ?? value.Convenio.ToString());
        if (value is null) return;

        // Pré-seleciona a modalidade habitual do paciente: primeiro pelo código salvo, senão pela base.
        ModalidadeSelecionada = Modalidades.FirstOrDefault(m => m.Codigo == value.ModalidadePreferidaCodigo)
            ?? Modalidades.FirstOrDefault(m => m.Base == value.ModalidadePreferida)
            ?? ModalidadeSelecionada;
        AvisoCarteirinha = value.CarteirinhaVencida
            ? $"A carteirinha de {value.Nome} venceu em {value.ValidadeCarteirinha:dd/MM/yyyy} — o convênio pode recusar a guia."
            : null;

        _ = VerificarPendenciasAsync(value.Id);
        _ = VerificarAutorizacaoAsync(value.Id);
    }

    /// <summary>
    /// Mostra a cota de sessões antes de lançar. É o aviso que evita a glosa 2006
    /// ("quantidade executada acima da autorizada") — o sistema registrava essa glosa
    /// depois do prejuízo e não avisava antes.
    /// </summary>
    private async Task VerificarAutorizacaoAsync(int pacienteId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var autorizacoes = scope.ServiceProvider.GetRequiredService<AutorizacaoService>();
            var saldo = await autorizacoes.VigenteAsync(pacienteId, DateOnly.FromDateTime(Data));

            // A seleção pode ter mudado enquanto a consulta rodava.
            if (PacienteSelecionado?.Id != pacienteId) return;

            if (saldo is null)
            {
                SaldoAutorizacao = null;
                AutorizacaoCritica = false;
                AutorizacaoNaUltima = false;
                return;
            }

            var senha = string.IsNullOrWhiteSpace(saldo.Autorizacao.Numero)
                ? "Autorização"
                : $"Senha {saldo.Autorizacao.Numero}";
            SaldoAutorizacao = saldo.Vencida
                ? $"{senha}: venceu em {saldo.Autorizacao.DataValidade:dd/MM/yyyy} — peça uma nova antes de lançar."
                : $"{senha}: {saldo.Resumo} (válida até {saldo.Autorizacao.DataValidade:dd/MM/yyyy}).";
            AutorizacaoCritica = saldo.Vencida || saldo.Esgotada;
            AutorizacaoNaUltima = !AutorizacaoCritica && saldo.NaUltima;
        }
        catch
        {
            // Aviso é auxiliar: nunca pode impedir o lançamento do atendimento.
            SaldoAutorizacao = null;
            AutorizacaoCritica = false;
            AutorizacaoNaUltima = false;
        }
    }

    /// <summary>Volta para a busca, para trocar o paciente escolhido.</summary>
    [RelayCommand]
    private void TrocarPaciente() => Seletor.Limpar();

    /// <summary>Zera a tela para lançar outro atendimento, sem sair da seção.</summary>
    [RelayCommand]
    private void NovoLancamento()
    {
        Lancado = false;
        NumeroAtendimento = null;
        _ultimoAtendimentoId = 0;
        CodigosGerados.Clear();
        Avisos.Clear();
        ResumoBaixas = null;
        Observacoes = null;
        Mensagem = null;
        Data = DateTime.Today;
        Seletor.Limpar();
        Seletor.Termo = null;
    }

    /// <summary>
    /// Avisa se o paciente selecionado tem guias pendentes de baixa de atendimentos anteriores —
    /// oportunidade de a secretária cobrar a guia em aberto no mesmo instante do novo atendimento.
    /// </summary>
    private async Task VerificarPendenciasAsync(int pacienteId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var pendencias = scope.ServiceProvider.GetRequiredService<PendenciaService>();
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var lista = await pendencias.PendenciasDoPacienteAsync(pacienteId, hoje);
            var ncs = await pendencias.NaoConformidadesDoPacienteAsync(pacienteId, hoje);

            // A seleção pode ter mudado enquanto a consulta rodava.
            if (PacienteSelecionado?.Id != pacienteId) return;
            if (lista.Count == 0 && ncs.Count == 0) { AvisoPendencias = null; return; }

            var partes = new List<string>();
            if (lista.Count > 0)
            {
                var itens = string.Join("; ", lista.Take(3).Select(p =>
                {
                    var ordinal = p.Ordem == OrdemCodigo.Segundo ? "2ª" : "1ª";
                    return $"{ordinal} guia de {RotuloTipo(p.Tipo)} de {p.DataPrevista:dd/MM}";
                }));
                if (lista.Count > 3) itens += $"; +{lista.Count - 3}";
                partes.Add($"{lista.Count} guia(s) pendente(s) de baixa — cobre a guia agora! ({itens}.)");
            }
            // Não conformidade: o paciente voltou, então ela será reaberta ao lançar o atendimento.
            if (ncs.Count > 0)
                partes.Add($"{ncs.Count} não conformidade(s) — serão reabertas ao lançar (o paciente voltou); cobre a(s) guia(s).");

            AvisoPendencias = "Este paciente tem " + string.Join(" ", partes);
        }
        catch
        {
            // Aviso é auxiliar: uma falha aqui nunca pode impedir o lançamento do atendimento.
            AvisoPendencias = null;
        }
    }

    private static string RotuloTipo(TipoCodigo t) => t switch
    {
        TipoCodigo.ConsultaEspecialidade => "consulta de especialidade",
        TipoCodigo.Eletroacupuntura => "eletroacupuntura",
        TipoCodigo.Bsv => "BSV",
        TipoCodigo.Acupuntura => "acupuntura",
        TipoCodigo.Consulta => "consulta",
        _ => t.ToString()
    };

    [RelayCommand]
    private async Task Lancar()
    {
        if (Seletor.Selecionado is not { } paciente)
        {
            Mensagem = "Selecione o paciente.";
            return;
        }
        if (ModalidadeSelecionada is null)
        {
            Mensagem = "Selecione a modalidade.";
            return;
        }
        if (ModalidadeConsulta && EspecialidadeSelecionada is null)
        {
            Mensagem = "Informe a especialidade da consulta.";
            return;
        }

        // Guarda contra duplo clique: dois lançamentos gerariam códigos duplicados.
        if (Ocupado) return;
        Ocupado = true;
        try
        {
            CodigosGerados.Clear();
            Avisos.Clear();
            Mensagem = null;

            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AtendimentoService>();
            var resultado = await service.LancarAsync(
                paciente.Id, DateOnly.FromDateTime(Data), Modalidade, Observacoes,
                registrarNaAgenda: true, primeiroCodigo: ModalidadeDupla ? PrimeiroCodigo : null,
                modalidadeCodigo: ModalidadeSelecionada.Codigo,
                especialidadeConsultaCodigo: ModalidadeConsulta ? EspecialidadeSelecionada?.Codigo : null);

            MontarCodigos(resultado.Atendimento.Codigos);
            foreach (var a in resultado.Avisos)
                Avisos.Add(a);

            _ultimoAtendimentoId = resultado.Atendimento.Id;
            NumeroAtendimento = resultado.Atendimento.Numero;
            Lancado = true;
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível lançar o atendimento: {ex.Message}";
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>
    /// Monta as linhas do resultado marcando o que já dá para baixar hoje. O 1º código
    /// nasce faturável na hora; o 2º só a partir da data prevista (+24h).
    /// </summary>
    private void MontarCodigos(IEnumerable<CodigoFaturamento> codigos)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        CodigosGerados.Clear();

        foreach (var c in codigos.OrderBy(c => c.DataPrevistaFaturamento).ThenBy(c => c.Ordem))
        {
            var podeBaixar = c.EstaPendente(hoje);
            var impedimento = c.Baixado || podeBaixar || c.Status == StatusCodigo.NaoAplicavel
                ? null
                : $"Libera em {c.DataPrevistaFaturamento:dd/MM/yyyy}";
            CodigosGerados.Add(new CodigoLancado(c, podeBaixar, impedimento));
        }

        AtualizarResumoBaixas(hoje);
    }

    private void AtualizarResumoBaixas(DateOnly hoje)
    {
        var faturaveis = CodigosGerados.Where(l => l.Codigo.Status != StatusCodigo.NaoAplicavel).ToList();
        if (faturaveis.Count == 0) { ResumoBaixas = null; return; }

        var baixadas = faturaveis.Count(l => l.Baixado);
        var proxima = faturaveis
            .Where(l => !l.Baixado && l.Codigo.DataPrevistaFaturamento > hoje)
            .OrderBy(l => l.Codigo.DataPrevistaFaturamento)
            .FirstOrDefault();

        ResumoBaixas = $"{baixadas} de {faturaveis.Count} guia(s) baixada(s)"
            + (proxima is null
                ? baixadas == faturaveis.Count ? " — nada pendente deste atendimento." : "."
                : $" — a próxima libera em {proxima.Codigo.DataPrevistaFaturamento:dd/MM/yyyy} e vai para o painel de pendências.");
    }

    /// <summary>
    /// Baixa a guia sem sair da tela. Antes era preciso lançar o atendimento, ir ao painel
    /// de pendências e baixar lá — sendo que a 1ª guia já sai faturável no mesmo instante.
    /// </summary>
    [RelayCommand]
    private async Task DarBaixa(CodigoLancado? linha)
    {
        if (linha is null || !linha.PodeBaixar) return;

        var descricao = $"{PacienteSelecionado?.Nome} — {RotuloTipo(linha.Codigo.Tipo)} " +
                        $"({(linha.Codigo.Ordem == OrdemCodigo.Segundo ? "2º" : "1º")} código) " +
                        $"do atendimento {NumeroAtendimento}";

        var janela = new Alertas.BaixaGuiaWindow(descricao)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (janela.ShowDialog() != true) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var faturamento = scope.ServiceProvider.GetRequiredService<FaturamentoService>();
            await faturamento.DarBaixaAsync(linha.Codigo.Id, janela.DataBaixa, janela.NumeroGuia,
                Environment.UserName, janela.Observacao);
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível registrar a baixa: {ex.Message}";
            return;
        }

        Mensagem = null;
        await RecarregarCodigosAsync();
    }

    /// <summary>Relê os códigos do atendimento para refletir a baixa recém-registrada.</summary>
    private async Task RecarregarCodigosAsync()
    {
        if (_ultimoAtendimentoId == 0) return;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<Clinica.Application.Abstracoes.IClinicaRepositorio>();
            var atendimento = await repo.ObterAtendimentoAsync(_ultimoAtendimentoId);
            if (atendimento is not null) MontarCodigos(atendimento.Codigos);
        }
        catch
        {
            // A baixa já foi gravada; falhar aqui só deixa a tela desatualizada.
        }
    }

    /// <summary>Gera a capa de faturamento (PDF) do atendimento recém-lançado e abre o arquivo.</summary>
    [RelayCommand]
    private async Task GerarCapa()
    {
        if (_ultimoAtendimentoId == 0) return;

        byte[] pdf;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var capa = scope.ServiceProvider.GetRequiredService<CapaFaturamentoService>();
            var prestador = await scope.ServiceProvider.GetRequiredService<ParametrosService>().ObterPrestadorAsync();
            pdf = await capa.GerarPdfAsync(_ultimoAtendimentoId, prestador);
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível gerar a capa: {ex.Message}";
            return;
        }

        var dialog = new SaveFileDialog
        {
            FileName = $"Capa-INICIAL-{NumeroAtendimento ?? _ultimoAtendimentoId.ToString()}-{Data:yyyy-MM-dd}.pdf",
            Filter = "PDF (*.pdf)|*.pdf",
            DefaultExt = ".pdf"
        };
        if (dialog.ShowDialog() != true) return;

        await File.WriteAllBytesAsync(dialog.FileName, pdf);
        Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
    }

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoSalvar => LancarCommand;
    public ICommand? AtalhoImprimir => GerarCapaCommand;
}
