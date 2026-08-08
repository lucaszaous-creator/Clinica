using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>
/// Uma visita na linha do tempo da ficha, já com o placar de guias daquele dia —
/// é o que a secretária quer ver primeiro: o atendimento fechou ou ficou pendurado?
/// </summary>
public sealed record LinhaAtendimento(
    DateOnly Data,
    string? Numero,
    string Modalidade,
    int Guias,
    int Baixadas,
    int Pendentes,
    string Situacao,
    bool TemPendencia);

/// <summary>Ficha do paciente: dados + histórico completo de códigos, com estorno de baixas.</summary>
public partial class FichaPacienteViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Controls.IDialogoService _dialogo;
    private int _pacienteId;

    [ObservableProperty] private Paciente? _paciente;
    [ObservableProperty] private string _cpfFormatado = string.Empty;
    [ObservableProperty] private string? _nomeConvenio;
    [ObservableProperty] private string? _mensagem;

    /// <summary>Ex.: "14/03/1962 (64 anos)"; "—" quando não informado.</summary>
    [ObservableProperty] private string _nascimentoTexto = "—";
    [ObservableProperty] private string _carteirinhaTexto = "—";
    [ObservableProperty] private bool _carteirinhaVencida;
    [ObservableProperty] private string? _validadeCarteirinhaTexto;

    /// <summary>Retrato em tamanho cheio; null quando o paciente ainda não tem foto.</summary>
    [ObservableProperty] private byte[]? _foto;

    /// <summary>Ex.: "Foto de 24/07/2026"; vazio quando não há retrato.</summary>
    [ObservableProperty] private string? _fotoTexto;

    /// <summary>Nome da modalidade habitual do paciente (pré-preenche a Agenda).</summary>
    [ObservableProperty] private string? _modalidadeTexto;

    /// <summary>Telefone formatado ou "—" quando não informado.</summary>
    [ObservableProperty] private string _telefoneTexto = "—";

    // Indicadores da ficha
    [ObservableProperty] private int _totalSessoes;
    [ObservableProperty] private int _totalBaixados;
    [ObservableProperty] private int _totalPendentes;
    [ObservableProperty] private string _ultimaSessao = "—";

    /// <summary>Nome da clínica (assinatura da mensagem de WhatsApp).</summary>
    private string? _nomeClinica;

    public ObservableCollection<CodigoFaturamento> Codigos { get; } = new();

    /// <summary>Linha do tempo de visitas, da mais recente para a mais antiga.</summary>
    public ObservableCollection<LinhaAtendimento> Atendimentos { get; } = new();

    /// <summary>Consultas autorizadas do paciente (o que vence e precisa renovar).</summary>
    public ObservableCollection<Consulta> Consultas { get; } = new();

    /// <summary>Cotas de sessões liberadas pelo convênio, com o saldo já calculado.</summary>
    public ObservableCollection<SaldoAutorizacao> Autorizacoes { get; } = new();

    /// <summary>Resumo da cota vigente para o cabeçalho; null quando não há autorização vigente.</summary>
    [ObservableProperty] private string? _autorizacaoTexto;

    /// <summary>Cota esgotada ou vencida — o cabeçalho alerta.</summary>
    [ObservableProperty] private bool _autorizacaoEmRisco;

    /// <summary>Resumo da consulta vigente para o cabeçalho; null quando não há consulta.</summary>
    [ObservableProperty] private string? _consultaTexto;

    /// <summary>Consulta vencida ou a menos de uma semana de vencer — cabeçalho alerta.</summary>
    [ObservableProperty] private bool _consultaEmRisco;

    public event Action? Voltar;

    /// <summary>Pede ao shell para abrir o cadastro já em modo de edição deste paciente.</summary>
    public event Action<int>? EditarSolicitado;

    public FichaPacienteViewModel(IServiceScopeFactory scopeFactory, Controls.IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;
    }

    public async Task CarregarAsync(int pacienteId)
    {
        _pacienteId = pacienteId;
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PacienteService>();
        Paciente = await service.ObterComHistoricoAsync(pacienteId);
        CpfFormatado = Cpf.Formatar(Paciente?.Documento);

        Codigos.Clear();
        Atendimentos.Clear();
        Consultas.Clear();
        Autorizacoes.Clear();
        if (Paciente is not null)
        {
            var todos = Paciente.Atendimentos
                .SelectMany(a => a.Codigos)
                .OrderByDescending(c => c.DataPrevistaFaturamento);
            foreach (var c in todos) Codigos.Add(c);

            // Pelo código do catálogo, para refletir variantes/renomeações feitas na clínica.
            NomeConvenio = Domain.Regras.CatalogoConvenios.Nome(
                Paciente.ConvenioCodigo ?? Paciente.Convenio.ToString());

            var hoje = DateOnly.FromDateTime(DateTime.Today);

            if (Paciente.DataNascimento is { } nasc)
            {
                var idade = hoje.Year - nasc.Year;
                if (new DateOnly(hoje.Year, nasc.Month, Math.Min(nasc.Day, DateTime.DaysInMonth(hoje.Year, nasc.Month))) > hoje)
                    idade--;
                NascimentoTexto = $"{nasc:dd/MM/yyyy} ({idade} anos)";
            }
            else
            {
                NascimentoTexto = "—";
            }

            CarteirinhaTexto = string.IsNullOrWhiteSpace(Paciente.Carteirinha) ? "—" : Paciente.Carteirinha;
            TelefoneTexto = string.IsNullOrWhiteSpace(Paciente.Telefone) ? "—" : Paciente.Telefone;
            ModalidadeTexto = Domain.Regras.CatalogoModalidades.Nome(
                Paciente.ModalidadePreferidaCodigo ?? Paciente.ModalidadePreferida.ToString());
            CarteirinhaVencida = Paciente.CarteirinhaVencida;
            ValidadeCarteirinhaTexto = Paciente.ValidadeCarteirinha is { } v2
                ? (CarteirinhaVencida ? $"Carteirinha VENCIDA em {v2:dd/MM/yyyy}" : $"Carteirinha válida até {v2:dd/MM/yyyy}")
                : null;
            TotalSessoes = Paciente.Atendimentos.Count;
            TotalBaixados = Codigos.Count(c => c.Baixado);
            TotalPendentes = Codigos.Count(c => c.EstaPendente(hoje));
            UltimaSessao = Paciente.Atendimentos.Count > 0
                ? Paciente.Atendimentos.Max(a => a.Data).ToString("dd/MM/yyyy")
                : "—";

            MontarLinhaDoTempo(hoje);
            await CarregarConsultasAsync(service, pacienteId, hoje);
            await CarregarAutorizacoesAsync(pacienteId, hoje);

            // Retrato: a miniatura já veio com o paciente e entra na hora; a foto cheia
            // (tabela à parte) é buscada logo em seguida para o cabeçalho da ficha.
            Foto = Paciente.FotoMiniatura;
            FotoTexto = Paciente.FotoAtualizadaEm is { } capturada
                ? $"Foto de {capturada:dd/MM/yyyy}"
                : null;
            try
            {
                var cheia = await service.ObterFotoAsync(pacienteId);
                if (cheia is not null && _pacienteId == pacienteId) Foto = cheia;
            }
            catch (Exception ex)
            {
                // Sem a foto cheia a ficha segue com a miniatura.
                Configuracao.LogErros.Registrar("Ficha — foto do paciente não pôde ser carregada", ex);
            }
        }

        try
        {
            var prestador = await scope.ServiceProvider.GetRequiredService<ParametrosService>().ObterPrestadorAsync();
            _nomeClinica = string.IsNullOrWhiteSpace(prestador.NomeFantasia) ? prestador.RazaoSocial : prestador.NomeFantasia;
        }
        catch (Exception ex)
        {
            // Sem assinatura na mensagem; não impede a ficha.
            Configuracao.LogErros.Registrar("Ficha — nome da clínica não pôde ser lido", ex);
        }
    }

    /// <summary>Agrupa os códigos por visita: cada linha mostra se aquele dia fechou ou ficou pendurado.</summary>
    private void MontarLinhaDoTempo(DateOnly hoje)
    {
        if (Paciente is null) return;

        foreach (var a in Paciente.Atendimentos.OrderByDescending(x => x.Data).ThenByDescending(x => x.Id))
        {
            var baixadas = a.Codigos.Count(c => c.Baixado);
            var pendentes = a.Codigos.Count(c => c.EstaPendente(hoje));
            var situacao = a.Codigos.Count == 0 ? "Sem guias"
                : pendentes > 0 ? $"{pendentes} guia(s) em aberto"
                : baixadas == a.Codigos.Count ? "Tudo baixado"
                : "Sem pendência ativa";

            Atendimentos.Add(new LinhaAtendimento(
                a.Data,
                a.Numero,
                Domain.Regras.CatalogoModalidades.Nome(a.ModalidadeCodigo ?? a.Modalidade.ToString()),
                a.Codigos.Count,
                baixadas,
                pendentes,
                situacao,
                pendentes > 0));
        }
    }

    /// <summary>Consultas autorizadas + resumo da vigente (o que precisa renovar e quando).</summary>
    private async Task CarregarConsultasAsync(PacienteService service, int pacienteId, DateOnly hoje)
    {
        try
        {
            foreach (var c in await service.ConsultasAsync(pacienteId))
                Consultas.Add(c);
        }
        catch (Exception ex)
        {
            // A ficha não depende das consultas para ser útil.
            Configuracao.LogErros.Registrar("Ficha — consultas do paciente não puderam ser lidas", ex);
            return;
        }

        var vigente = Consultas.FirstOrDefault(c => c.Status == StatusConsulta.Ativa);
        if (vigente is null)
        {
            ConsultaTexto = Consultas.Count == 0 ? null : "Sem consulta vigente — precisa renovar";
            ConsultaEmRisco = Consultas.Count > 0;
            return;
        }

        var dias = vigente.DiasParaVencer(hoje);
        ConsultaEmRisco = dias <= 5;
        ConsultaTexto = dias < 0
            ? $"Consulta vencida em {vigente.DataVencimento:dd/MM/yyyy}"
            : dias == 0
                ? "Consulta vence hoje"
                : $"Consulta válida até {vigente.DataVencimento:dd/MM/yyyy} ({dias} dia(s))";
    }

    /// <summary>Cotas de sessões do convênio + resumo da vigente para o cabeçalho.</summary>
    private async Task CarregarAutorizacoesAsync(int pacienteId, DateOnly hoje)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AutorizacaoService>();
            foreach (var s in await service.SaldosAsync(pacienteId, hoje))
                Autorizacoes.Add(s);

            var vigente = Autorizacoes.FirstOrDefault(s => s.Autorizacao.Vigente(hoje) && s.Utilizavel)
                ?? Autorizacoes.FirstOrDefault(s => s.Autorizacao.Vigente(hoje));
            if (vigente is null)
            {
                AutorizacaoTexto = Autorizacoes.Count == 0 ? null : "Sem autorização vigente";
                AutorizacaoEmRisco = Autorizacoes.Count > 0;
                return;
            }

            AutorizacaoTexto = $"Sessões: {vigente.Resumo}";
            AutorizacaoEmRisco = !vigente.Utilizavel || vigente.NaUltima;
        }
        catch (Exception ex)
        {
            // A ficha não depende da cota para ser útil.
            Configuracao.LogErros.Registrar("Ficha — cota de sessões não pôde ser lida", ex);
            AutorizacaoTexto = null;
            AutorizacaoEmRisco = false;
        }
    }

    /// <summary>Abre o cadastro de autorização (nova ou existente) e recarrega a ficha.</summary>
    private async Task AbrirAutorizacaoAsync(int? autorizacaoId)
    {
        if (Paciente is null) return;

        var janela = new Alertas.AutorizacaoWindow(
            new AutorizacaoEdicaoViewModel(_scopeFactory, Paciente.Id),
            autorizacaoId,
            Paciente.ConvenioCodigo ?? Paciente.Convenio.ToString())
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (janela.ShowDialog() != true) return;

        await CarregarAsync(_pacienteId);
    }

    [RelayCommand]
    private async Task NovaAutorizacao() => await AbrirAutorizacaoAsync(null);

    [RelayCommand]
    private async Task EditarAutorizacao(SaldoAutorizacao? saldo)
    {
        if (saldo is not null) await AbrirAutorizacaoAsync(saldo.Autorizacao.Id);
    }

    [RelayCommand]
    private async Task ExcluirAutorizacao(SaldoAutorizacao? saldo)
    {
        if (saldo is null) return;
        if (!_dialogo.ConfirmarPerigo("Excluir autorização",
            "Excluir esta autorização de sessões? O histórico de atendimentos não é afetado.")) return;

        using (var scope = _scopeFactory.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<AutorizacaoService>();
            await service.RemoverAsync(saldo.Autorizacao.Id, SessaoUsuario.Atual.Operador);
        }
        await CarregarAsync(_pacienteId);
    }

    /// <summary>Abre a conversa de WhatsApp (wa.me) com o paciente.</summary>
    [RelayCommand]
    private void Whatsapp()
    {
        if (Paciente is null) return;

        var fone = Domain.Telefone.Normalizar(Paciente.Telefone);
        if (fone.Length is < 10 or > 13)
        {
            Mensagem = "Telefone ausente ou inválido no cadastro (edite em Pacientes).";
            return;
        }
        if (fone.Length is 10 or 11)
            fone = "55" + fone; // wa.me exige DDI

        var primeiroNome = Paciente.Nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? Paciente.Nome;
        var texto = $"Olá, {primeiroNome}!" +
                    (string.IsNullOrWhiteSpace(_nomeClinica) ? string.Empty : $" Aqui é da {_nomeClinica}.");

        try
        {
            Process.Start(new ProcessStartInfo($"https://wa.me/{fone}?text={Uri.EscapeDataString(texto)}")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível abrir o WhatsApp: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Estornar(CodigoFaturamento? codigo)
    {
        if (codigo is null || !codigo.Baixado) return;

        if (!_dialogo.Confirmar("Confirmar estorno",
            "Estornar a baixa desta guia? A pendência voltará a aparecer no painel.")) return;

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<FaturamentoService>();
        await service.EstornarBaixaAsync(codigo.Id, "estorno pela ficha do paciente", SessaoUsuario.Atual.Operador);

        await CarregarAsync(_pacienteId);
    }

    [RelayCommand]
    private void FecharFicha() => Voltar?.Invoke();

    [RelayCommand]
    private void EditarCadastro()
    {
        if (_pacienteId != 0) EditarSolicitado?.Invoke(_pacienteId);
    }
}
