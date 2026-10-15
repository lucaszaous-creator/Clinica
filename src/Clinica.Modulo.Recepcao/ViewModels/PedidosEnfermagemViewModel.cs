using System.Collections.ObjectModel;
using System.Windows.Threading;
using Clinica.Application.Abstracoes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>Um aviso da enfermagem na lista, já com as frases que a tela escreve.</summary>
public sealed class LinhaPedidoEnfermagem(LinhaSolicitacaoEnfermagem p)
{
    public LinhaSolicitacaoEnfermagem Pedido { get; } = p;
    public int Id => Pedido.Id;
    public bool Novo => Pedido.Ativa && Pedido.Situacao == "Novo";
    public string Data => Pedido.Inicio.ToString("dd/MM/yyyy");
    public string Faixa => $"{Pedido.Inicio:HH:mm}–{Pedido.Fim:HH:mm}";
    public string Status => PedidosEnfermagemViewModel.RotuloSituacao(Pedido.Situacao);
    public string Titulo => $"Até {Pedido.Quantidade} atendimentos adicionais · {Pedido.Modalidade}";
    public string Observacao => string.IsNullOrWhiteSpace(Pedido.Observacao) ? "Sem observação adicional." : Pedido.Observacao;
    public string Meta => $"{Pedido.Autora} · válido até {Pedido.Validade:dd/MM/yyyy HH:mm}";
    public string Acessivel => $"Ver pedido de {Pedido.Quantidade} {Pedido.Modalidade}, {Data} às {Pedido.Inicio:HH:mm}";
}

/// <summary>Uma linha do histórico do aviso.</summary>
public sealed record LinhaHistoricoPedido(string Acao, string Quando, string Observacao)
{
    public bool TemObservacao => !string.IsNullOrWhiteSpace(Observacao);
}

/// <summary>
/// Os pedidos da enfermagem para a recepção (out/2026 — de volta ao WPF depois da versão
/// em navegador embutido). Caixa COMPARTILHADA: os avisos são de toda a recepção, sem distribuição
/// de responsáveis. Ela vive em duas portas — a tela "Pedidos da enfermagem" e o resumo
/// no alto da grade da Agenda, que abre a mesma lista numa janela — e as duas leem por
/// AQUI: duas cópias da regra "o que é novo" divergiriam na primeira correção.
///
/// A atualização é por relógio (1 min) e pelo sinal <see cref="NotificacoesEnfermagem"/>,
/// que dispara quando ESTE app marca um aviso como visto ou o encerra. Quem liga e desliga
/// é a View, no Loaded/Unloaded — timer no ViewModel manteria viva cada tela já trocada.
/// </summary>
public sealed partial class PedidosEnfermagemViewModel : ObservableObject, IDisposable
{
    private readonly IServiceScopeFactory _escopos;
    private readonly Func<DateTime> _dia;
    private readonly Action<DateTime> _abrirAgenda;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(1) };
    private IReadOnlyList<LinhaSolicitacaoEnfermagem> _pedidos = [];
    private bool _ativa, _registrado, _jaLeu;
    private int _geracao;
    private (string Texto, Guid Chave)? _envio;

    public PedidosEnfermagemViewModel(IServiceScopeFactory escopos, Func<DateTime> dia, Action<DateTime> abrirAgenda)
    {
        _escopos = escopos;
        _dia = dia;
        _abrirAgenda = abrirAgenda;
    }

    /// <summary>A mesma regra do serviço (<c>AcessoPlanejamentoEnfermagem.Tratar</c>), lida da sessão.</summary>
    public static bool Permitido => SessaoUsuario.Atual.Autenticado
        && SessaoUsuario.Atual.Perfil is PerfilAcesso.Recepcao or PerfilAcesso.Gerente
        && SessaoUsuario.Atual.Pode(Permissao.VerAgenda | Permissao.EditarAgenda | Permissao.VerFichaPaciente);

    public static void Exigir()
    {
        if (!Permitido) throw new UnauthorizedAccessException("Este acesso não permite consultar os pedidos da recepção.");
    }

    public static string RotuloSituacao(string situacao) => situacao switch
    {
        "Novo" => "Novo",
        "Visto" => "Visto pela recepção",
        "Encerrado" => "Aviso encerrado",
        "Retirado" => "Retirado pela enfermagem",
        "Expirado" => "Expirado",
        _ => situacao
    };

    // ==================== Estado ====================

    public ObservableCollection<LinhaPedidoEnfermagem> Lista { get; } = [];
    public ObservableCollection<LinhaHistoricoPedido> Historico { get; } = [];
    public ObservableCollection<string> Bloqueios { get; } = [];

    /// <summary>"novos", "abertos" ou "historico".</summary>
    [ObservableProperty] private string _filtro = "abertos";
    [ObservableProperty] private bool _soDia;
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private string? _erro;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private int _novos;
    [ObservableProperty] private int _abertos;
    [ObservableProperty] private int _noDia;
    [ObservableProperty] private string _resumoLista = "";
    [ObservableProperty] private string _tituloVazio = "";
    [ObservableProperty] private string _textoVazio = "";
    [ObservableProperty] private bool _listaVazia;
    [ObservableProperty] private DetalheSolicitacaoEnfermagem? _detalhe;
    [ObservableProperty] private string _observacaoEncerramento = "";
    [ObservableProperty] private bool _confirmandoEncerramento;

    public bool PodeVerResumo => Permitido;

    /// <summary>
    /// Só a PRIMEIRA leitura cobre a lista com "carregando": a releitura de cada minuto é
    /// silenciosa (a linha de resumo diz "Atualizando…"), senão a lista piscaria em branco.
    /// </summary>
    public bool PrimeiraCarga => Carregando && !_jaLeu;
    public bool Bloqueado => Ocupado || Carregando;
    public bool Livre => !Bloqueado;
    public bool TemErro => !string.IsNullOrEmpty(Erro);
    public bool MostrandoLista => Detalhe is null && !TemErro;
    public bool MostrandoDetalhe => Detalhe is not null && !TemErro;
    public string DiaTexto => _dia().ToString("dd/MM/yyyy");
    public string SoDiaRotulo => $"Somente {DiaTexto}";
    public string RotuloNovos => $"Novos ({Novos})";
    public string RotuloAbertos => $"Em aberto ({Abertos})";
    public bool FiltroNovos => Filtro == "novos";
    public bool FiltroAbertos => Filtro == "abertos";
    public bool FiltroHistorico => Filtro == "historico";

    /// <summary>A linha do resumo no alto da Agenda.</summary>
    public string ResumoAviso => Erro is not null ? "Não foi possível atualizar os avisos"
        : Carregando ? "Atualizando…"
        : $"{Novos} novos · {NoDia} neste dia · {Abertos} em aberto";

    public string TituloTela => Detalhe is null ? "Pedidos da enfermagem" : "Pedido de atendimentos";
    public string SubtituloTela => Detalhe is null
        ? "Avisos compartilhados com toda a recepção, sem distribuição de responsáveis."
        : "Consulte o aviso e confira o período antes de marcar.";

    // Detalhe — frases prontas para a tela.
    public LinhaSolicitacaoEnfermagem? PedidoAberto => Detalhe?.Pedido;
    public string DetalheStatus => PedidoAberto is { } p ? RotuloSituacao(p.Situacao) : "";
    public string DetalheFaixa => PedidoAberto is { } p ? $"Pedido #{p.Id} · {p.Autora}" : "";
    public string DetalheTitulo => PedidoAberto is { } p ? $"{p.Inicio:dd/MM/yyyy} · {p.Inicio:HH:mm} às {p.Fim:HH:mm}" : "";
    public string DetalheQuantidade => PedidoAberto is { } p ? $"Até {p.Quantidade} atendimentos adicionais · {p.Modalidade}" : "";
    public string DetalheObservacao => PedidoAberto is { } p
        ? (string.IsNullOrWhiteSpace(p.Observacao) ? "Sem observação adicional." : p.Observacao) : "";
    public string DetalheMeta => PedidoAberto is { } p
        ? $"Válido até {p.Validade:dd/MM/yyyy} às {p.Validade:HH:mm}"
          + (p.VistaEm is { } v ? $" · Visto por {p.VistaPor} em {v:dd/MM/yyyy} às {v:HH:mm}" : "")
        : "";
    public bool DetalheNovo => PedidoAberto is { Ativa: true, Situacao: "Novo" };
    public bool PodeTratar => PedidoAberto?.PodeTratar == true;
    public bool MostrarBotaoEncerrar => PodeTratar && !ConfirmandoEncerramento;
    public bool MostrarEncerramento => PodeTratar && ConfirmandoEncerramento;
    public bool AgendaAlterada => Detalhe?.AgendaAlterada == true;
    public string TextoAgendaAlterada => Detalhe is { } d
        ? $"Havia {d.Pedido.TotalReferencia} agendados; agora há {d.Periodo.Total}. Confira horários, modalidades e bloqueios antes de marcar. A quantidade solicitada não representa vagas reservadas."
        : "";
    public string ResumoPeriodo => Detalhe is { } d
        ? $"{d.Periodo.Total} agendados · {d.Periodo.Bsv} BSV · {d.Periodo.BsvAcupuntura} BSV + acupuntura · {d.Periodo.OutrasModalidades} outras modalidades"
        : "";
    public bool TemBloqueios => Bloqueios.Count > 0;

    partial void OnFiltroChanged(string value) { Remontar(); AvisarFiltros(); }
    partial void OnSoDiaChanged(bool value) => Remontar();
    partial void OnCarregandoChanged(bool value)
    {
        AvisarBloqueio(); Remontar();
        OnPropertyChanged(nameof(ResumoAviso)); OnPropertyChanged(nameof(PrimeiraCarga));
    }
    partial void OnOcupadoChanged(bool value) => AvisarBloqueio();
    partial void OnErroChanged(string? value)
    {
        OnPropertyChanged(nameof(TemErro)); OnPropertyChanged(nameof(MostrandoLista));
        OnPropertyChanged(nameof(MostrandoDetalhe)); OnPropertyChanged(nameof(ResumoAviso));
    }
    partial void OnNovosChanged(int value) { OnPropertyChanged(nameof(RotuloNovos)); OnPropertyChanged(nameof(ResumoAviso)); }
    partial void OnAbertosChanged(int value) { OnPropertyChanged(nameof(RotuloAbertos)); OnPropertyChanged(nameof(ResumoAviso)); }
    partial void OnNoDiaChanged(int value) => OnPropertyChanged(nameof(ResumoAviso));
    partial void OnConfirmandoEncerramentoChanged(bool value)
    {
        OnPropertyChanged(nameof(MostrarBotaoEncerrar)); OnPropertyChanged(nameof(MostrarEncerramento));
    }
    partial void OnDetalheChanged(DetalheSolicitacaoEnfermagem? oldValue, DetalheSolicitacaoEnfermagem? newValue)
    {
        // Trocar de aviso zera o rascunho do encerramento — como o formulário da versão web.
        if (oldValue?.Pedido.Id != newValue?.Pedido.Id)
        {
            ObservacaoEncerramento = ""; ConfirmandoEncerramento = false; _envio = null;
        }
        Historico.Clear();
        Bloqueios.Clear();
        if (newValue is { } d)
        {
            foreach (var h in d.Historico)
                Historico.Add(new(h.Acao, $"{h.Em:dd/MM/yyyy} às {h.Em:HH:mm} · {h.Autor}", h.Observacao));
            foreach (var b in d.Periodo.Bloqueios)
                Bloqueios.Add($"{b.Inicio:HH:mm}–{b.Fim:HH:mm} · {b.Recurso}");
        }
        foreach (var nome in new[] { nameof(PedidoAberto), nameof(DetalheStatus), nameof(DetalheFaixa), nameof(DetalheTitulo),
            nameof(DetalheQuantidade), nameof(DetalheObservacao), nameof(DetalheMeta), nameof(DetalheNovo), nameof(PodeTratar),
            nameof(MostrarBotaoEncerrar), nameof(MostrarEncerramento), nameof(AgendaAlterada), nameof(TextoAgendaAlterada),
            nameof(ResumoPeriodo), nameof(TemBloqueios), nameof(MostrandoLista), nameof(MostrandoDetalhe),
            nameof(TituloTela), nameof(SubtituloTela) })
            OnPropertyChanged(nome);
    }

    private void AvisarBloqueio() { OnPropertyChanged(nameof(Bloqueado)); OnPropertyChanged(nameof(Livre)); }
    private void AvisarFiltros()
    {
        OnPropertyChanged(nameof(FiltroNovos)); OnPropertyChanged(nameof(FiltroAbertos)); OnPropertyChanged(nameof(FiltroHistorico));
    }

    /// <summary>Lista, contagens e frases a partir da última leitura — sem ir ao banco.</summary>
    private void Remontar()
    {
        var dia = _dia().Date;
        Novos = _pedidos.Count(p => p.Ativa && p.Situacao == "Novo");
        Abertos = _pedidos.Count(p => p.Ativa);
        NoDia = _pedidos.Count(p => p.Ativa && p.Inicio.Date == dia);
        var lista = _pedidos.Where(p => (Filtro == "novos" ? p.Ativa && p.Situacao == "Novo"
                : Filtro == "abertos" ? p.Ativa : !p.Ativa)
            && (!SoDia || p.Inicio.Date == dia)).Select(p => new LinhaPedidoEnfermagem(p)).ToList();
        // Entre o Clear e o último Add não há await (parcela 62).
        Lista.Clear();
        foreach (var l in lista) Lista.Add(l);
        ListaVazia = lista.Count == 0;
        ResumoLista = Carregando ? "Atualizando avisos…"
            : $"{lista.Count} avisos · {(SoDia ? "dia consultado" : "todas as datas")}";
        TituloVazio = Filtro == "novos" ? "Nenhum aviso novo" : "Nenhum aviso neste filtro";
        TextoVazio = SoDia ? "Desmarque o filtro do dia para consultar as outras datas."
            : "Os pedidos enviados pela enfermagem aparecerão aqui.";
        OnPropertyChanged(nameof(DiaTexto)); OnPropertyChanged(nameof(SoDiaRotulo));
    }

    // ==================== Ciclo ====================

    public void Iniciar()
    {
        _ativa = true;
        if (!_registrado)
        {
            _timer.Tick += AoBaterRelogio;
            NotificacoesEnfermagem.Mudaram += AoMudarPedidos;
            _registrado = true;
        }
        if (Permitido) { _timer.Start(); _ = AtualizarAsync(); }
        OnPropertyChanged(nameof(PodeVerResumo));
    }

    public void Parar()
    {
        _ativa = false; _geracao++; _timer.Stop();
        if (_registrado)
        {
            _timer.Tick -= AoBaterRelogio;
            NotificacoesEnfermagem.Mudaram -= AoMudarPedidos;
            _registrado = false;
        }
    }

    public void Dispose() => Parar();

    private void AoBaterRelogio(object? sender, EventArgs e) => _ = AtualizarAsync();

    /// <summary>Outra tela deste app mudou um aviso: relê, com o relógio da UI.</summary>
    private void AoMudarPedidos()
    {
        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.BeginInvoke(() => _ = AtualizarAsync());
        else _ = AtualizarAsync();
    }

    /// <summary>Relê a lista. Silenciosa: falha vira o terceiro estado escrito, nunca lista vazia.</summary>
    public async Task AtualizarAsync()
    {
        if (!_ativa || Carregando || !Permitido) return;
        var g = _geracao; var usuario = SessaoUsuario.Atual.UsuarioId;
        Carregando = true;
        try
        {
            using var escopo = _escopos.CreateScope();
            var linhas = await escopo.ServiceProvider.GetRequiredService<IPlanejamentoEnfermagemService>().ListarAsync(usuario);
            if (!_ativa || g != _geracao || usuario != SessaoUsuario.Atual.UsuarioId) return;
            _pedidos = linhas; _jaLeu = true; Erro = null;
        }
        catch (Exception ex)
        {
            if (_ativa && g == _geracao) Erro = "Avisos indisponíveis. Abra os pedidos e tente Atualizar. " + ex.Message;
            Clinica.Application.Diagnostico.Registrar("Pedidos da enfermagem", ex);
        }
        finally { Carregando = false; }
        // O sino pediu um aviso: abre-o depois da lista (é ela que autoriza o id).
        if (_ativa && g == _geracao && _jaLeu && NotificacoesEnfermagem.ConsumirAbertura() is { } pedido)
        {
            if (_pedidos.Any(p => p.Id == pedido)) await ExecutarAsync(() => DetalharAsync(pedido, true));
            else Mensagem = "Este aviso não está mais na caixa — ele pode ter sido encerrado em outra máquina.";
        }
    }

    // ==================== Comandos ====================

    private async Task ExecutarAsync(Func<Task> acao)
    {
        Exigir();
        if (Bloqueado) return;
        Ocupado = true; Mensagem = null;
        try { await acao(); }
        catch (Exception ex) { Mensagem = ex.Message; }
        finally { Ocupado = false; }
    }

    [RelayCommand]
    private Task Atualizar() => ExecutarAsync(async () =>
    {
        await AtualizarAsync();
        if (Detalhe is { } d) await DetalharAsync(d.Pedido.Id, false);
    });

    [RelayCommand]
    private void EscolherFiltro(string? filtro)
    {
        Exigir();
        if (filtro is "novos" or "abertos" or "historico") Filtro = filtro;
        else AvisarFiltros();
    }

    [RelayCommand]
    private void Voltar()
    {
        Exigir();
        Detalhe = null; Mensagem = null;
    }

    [RelayCommand]
    private Task AbrirAviso(LinhaPedidoEnfermagem? linha) => ExecutarAsync(async () =>
    {
        if (linha is null) return;
        if (_pedidos.All(p => p.Id != linha.Id)) throw new InvalidOperationException("Atualize a lista de avisos.");
        await DetalharAsync(linha.Id, true);
    });

    [RelayCommand]
    private void VerAgenda()
    {
        Exigir();
        if (PedidoAberto is not { } p) return;
        // Destino vem da consulta autorizada, nunca de uma data digitada.
        var pedido = _pedidos.SingleOrDefault(x => x.Id == p.Id) ?? p;
        _abrirAgenda(pedido.Inicio.Date);
        PediuFechar?.Invoke();
    }

    /// <summary>Disparado depois de "Ver este dia na agenda": a janela da Agenda se fecha.</summary>
    public event Action? PediuFechar;

    [RelayCommand]
    private void PedirEncerramento()
    {
        Exigir();
        if (PodeTratar) ConfirmandoEncerramento = true;
    }

    [RelayCommand]
    private void ContinuarAberto()
    {
        Exigir();
        ConfirmandoEncerramento = false;
    }

    [RelayCommand]
    private Task ConfirmarEncerramento() => ExecutarAsync(async () =>
    {
        if (PedidoAberto is not { PodeTratar: true } p) return;
        // A mesma chave para a mesma tentativa: o segundo clique depois de uma resposta
        // perdida não encerra duas vezes (idempotência do serviço).
        var texto = $"{p.Id}|{p.Versao}|{ObservacaoEncerramento}";
        if (_envio?.Texto != texto) _envio = (texto, Guid.NewGuid());
        using (var escopo = _escopos.CreateScope())
            await escopo.ServiceProvider.GetRequiredService<IPlanejamentoEnfermagemService>().AtualizarAsync(
                SessaoUsuario.Atual.UsuarioId, p.Id, new(_envio.Value.Chave, p.Versao, "encerrar", ObservacaoEncerramento));
        NotificacoesEnfermagem.SinalizarMudanca();
        await AtualizarAsync();
        await DetalharAsync(p.Id, false);
    });

    private async Task DetalharAsync(int id, bool registrarLeitura)
    {
        var geracao = _geracao;
        using var escopo = _escopos.CreateScope();
        var svc = escopo.ServiceProvider.GetRequiredService<IPlanejamentoEnfermagemService>();
        var usuario = SessaoUsuario.Atual.UsuarioId;
        var detalhe = await svc.DetalheAsync(usuario, id);
        if (!_ativa || geracao != _geracao || usuario != SessaoUsuario.Atual.UsuarioId) return;
        if (registrarLeitura && detalhe.Pedido is { Ativa: true, Situacao: "Novo" })
        {
            // Abrir um aviso novo é LÊ-LO: ele passa a "Visto pela recepção" para todo o balcão.
            await svc.AtualizarAsync(usuario, id, new(Guid.NewGuid(), detalhe.Pedido.Versao, "visto"));
            NotificacoesEnfermagem.SinalizarMudanca();
            detalhe = await svc.DetalheAsync(usuario, id);
            await AtualizarAsync();
        }
        if (_ativa && geracao == _geracao && usuario == SessaoUsuario.Atual.UsuarioId) Detalhe = detalhe;
    }
}
