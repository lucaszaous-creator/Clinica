using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Clinica.Application.Abstracoes;
using Clinica.Desktop.Shell.WebClinica;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Recepcao.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.Views;

/// <summary>Caixa compartilhada de avisos. Atualiza enquanto sua página ou a agenda está em cena.</summary>
internal sealed class PedidosEnfermagemWeb(IServiceScopeFactory escopos, Func<DateTime> dia, Action<DateTime> abrirAgenda) : IDisposable
{
    public PedidosEnfermagemWeb(AgendaViewModel vm) : this(vm.EscoposPlanejamento, () => vm.Dia, data =>
    {
        vm.MostrarTodosOsProfissionais = true; vm.FiltroProfissional = null; vm.FiltroSala = null;
        vm.ModoSemana = false; vm.Dia = data;
    }) { }
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(1) };
    private IReadOnlyList<LinhaSolicitacaoEnfermagem> _pedidos = [];
    private DetalheSolicitacaoEnfermagem? _detalhe;
    private bool _ativa, _carregando, _registrado;
    private int _geracao;
    private string? _erro;
    private Window? _janela;
    private readonly string _contexto = Guid.NewGuid().ToString("N");
    public event Action? EstadoAlterado;
    private static bool Permitido => SessaoUsuario.Atual.Autenticado
        && SessaoUsuario.Atual.Perfil is PerfilAcesso.Recepcao or PerfilAcesso.Gerente
        && SessaoUsuario.Atual.Pode(Permissao.VerAgenda | Permissao.EditarAgenda | Permissao.VerFichaPaciente);

    public object Resumo => new { permitido = Permitido, carregando = _carregando, erro = _erro,
        novos = _erro is null ? _pedidos.Count(p => p.Ativa && p.Situacao == "Novo") : (int?)null,
        abertos = _erro is null ? _pedidos.Count(p => p.Ativa) : (int?)null,
        noDia = _erro is null ? _pedidos.Count(p => p.Ativa && p.Inicio.Date == dia().Date) : (int?)null };
    internal object Estado() => new { contexto = _contexto, pedidos = _pedidos, detalhe = _detalhe,
        dia = dia().ToString("yyyy-MM-dd"), carregando = _carregando, erro = _erro };
    internal static void Exigir()
    {
        if (!Permitido) throw new UnauthorizedAccessException("Este acesso não permite consultar os pedidos da recepção.");
    }

    public void Iniciar()
    {
        _ativa = true;
        _detalhe = null;
        if (!_registrado) { _timer.Tick += AtualizarTimer; _registrado = true; }
        if (Permitido) { _timer.Start(); _ = Atualizar(); }
    }
    public void Parar() { _ativa = false; _geracao++; _timer.Stop(); }
    private void AtualizarTimer(object? sender, EventArgs e) { _ = Atualizar(); }
    private async Task Atualizar()
    {
        if (!_ativa || _carregando || !Permitido) return;
        var g = _geracao; var usuario = SessaoUsuario.Atual.UsuarioId;
        _carregando = true;
        EstadoAlterado?.Invoke();
        try
        {
            using var escopo = escopos.CreateScope();
            var linhas = await escopo.ServiceProvider.GetRequiredService<IPlanejamentoEnfermagemService>().ListarAsync(usuario);
            if (!_ativa || g != _geracao || usuario != SessaoUsuario.Atual.UsuarioId) return;
            _pedidos = linhas; _erro = null;
        }
        catch (Exception ex) { if (_ativa && g == _geracao) _erro = "Avisos indisponíveis. Abra os pedidos e tente Atualizar. " + ex.Message; }
        finally
        {
            _carregando = false;
            EstadoAlterado?.Invoke();
            // Se saiu e voltou durante a consulta, sua resposta foi descartada acima.
            // Iniciar não pôde reler enquanto ela estava pendente: atualize a geração atual.
            if (_ativa && g != _geracao && usuario == SessaoUsuario.Atual.UsuarioId) _ = Atualizar();
        }
    }

    public async Task Abrir()
    {
        Exigir();
        if (_janela is not null) { _janela.Activate(); return; }
        await Atualizar();
        _detalhe = null;
        using var painel = new PainelClinicoWeb("pedidos-enfermagem", Estado, Executar, Exigir);
        _janela = new Window { Title = "Pedidos da enfermagem", Width = 1020, Height = 740, MinWidth = 620, MinHeight = 460,
            MaxWidth = SystemParameters.WorkArea.Width, MaxHeight = SystemParameters.WorkArea.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = painel,
            Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) };
        try { _janela.ShowDialog(); }
        finally { _janela = null; _detalhe = null; await Atualizar(); }
    }

    internal async Task Executar(JsonElement m)
    {
        Exigir();
        if (!m.TryGetProperty("contexto", out var contexto) || contexto.GetString() != _contexto) return;
        var acao = m.GetProperty("acao").GetString();
        if (acao == "atualizar")
        {
            await Atualizar();
            if (_detalhe is not null) await Detalhar(_detalhe.Pedido.Id, false);
            return;
        }
        if (acao == "voltar") { _detalhe = null; return; }
        if (!m.TryGetProperty("id", out var valor) || !valor.TryGetInt32(out var id)) return;
        var pedido = _pedidos.SingleOrDefault(p => p.Id == id) ?? throw new InvalidOperationException("Atualize a lista de avisos.");
        if (acao == "abrir-aviso") { await Detalhar(id, true); return; }
        if (acao == "ver-agenda")
        {
            // Destino vem da consulta autorizada, nunca de uma data enviada pelo navegador.
            abrirAgenda(pedido.Inicio.Date);
            _janela?.Close(); return;
        }
        if (acao != "encerrar" || _detalhe?.Pedido.Id != id) return;
        var versao = m.GetProperty("versao").GetGuid();
        var chave = m.GetProperty("idempotencia").GetGuid();
        var observacao = m.TryGetProperty("observacao", out var texto) ? texto.GetString() ?? "" : "";
        using (var escopo = escopos.CreateScope())
            await escopo.ServiceProvider.GetRequiredService<IPlanejamentoEnfermagemService>().AtualizarAsync(
                SessaoUsuario.Atual.UsuarioId, id, new(chave, versao, "encerrar", observacao));
        NotificacoesEnfermagem.SinalizarMudanca();
        await Atualizar(); await Detalhar(id, false);
    }

    private async Task Detalhar(int id, bool registrarLeitura)
    {
        var geracao = _geracao;
        using var escopo = escopos.CreateScope();
        var svc = escopo.ServiceProvider.GetRequiredService<IPlanejamentoEnfermagemService>();
        var usuario = SessaoUsuario.Atual.UsuarioId;
        var detalhe = await svc.DetalheAsync(usuario, id);
        if (!_ativa || geracao != _geracao || usuario != SessaoUsuario.Atual.UsuarioId) return;
        if (registrarLeitura && detalhe.Pedido is { Ativa: true, Situacao: "Novo" })
        {
            await svc.AtualizarAsync(usuario, id, new(Guid.NewGuid(), detalhe.Pedido.Versao, "visto"));
            NotificacoesEnfermagem.SinalizarMudanca();
            detalhe = await svc.DetalheAsync(usuario, id);
            await Atualizar();
        }
        if (_ativa && geracao == _geracao && usuario == SessaoUsuario.Atual.UsuarioId) _detalhe = detalhe;
    }
    public void Dispose() { Parar(); _timer.Tick -= AtualizarTimer; _janela?.Close(); }
}
