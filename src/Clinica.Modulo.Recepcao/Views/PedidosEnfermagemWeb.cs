using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Clinica.Application.Abstracoes;
using Clinica.Desktop.Shell.WebClinica;
using Clinica.Domain.Entities;
using Clinica.Recepcao.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.Views;

/// <summary>Caixa compartilhada de avisos. O ciclo de atualização acompanha a presença da agenda na tela.</summary>
internal sealed class PedidosEnfermagemWeb(AgendaViewModel vm) : IDisposable
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(1) };
    private IReadOnlyList<LinhaSolicitacaoEnfermagem> _pedidos = [];
    private DetalheSolicitacaoEnfermagem? _detalhe;
    private bool _ativa, _carregando, _registrado;
    private int _geracao;
    private string? _erro;
    private Window? _janela;
    private readonly string _contexto = Guid.NewGuid().ToString("N");
    private static bool Permitido => SessaoUsuario.Atual.Autenticado
        && SessaoUsuario.Atual.Perfil is PerfilAcesso.Recepcao or PerfilAcesso.Gerente
        && SessaoUsuario.Atual.Pode(Permissao.VerAgenda | Permissao.EditarAgenda | Permissao.VerFichaPaciente);

    public object Resumo => new { permitido = Permitido, carregando = _carregando, erro = _erro,
        novos = _erro is null ? _pedidos.Count(p => p.Ativa && p.Situacao == "Novo") : (int?)null,
        abertos = _erro is null ? _pedidos.Count(p => p.Ativa) : (int?)null,
        noDia = _erro is null ? _pedidos.Count(p => p.Ativa && p.Inicio.Date == vm.Dia.Date) : (int?)null };
    private object Estado() => new { contexto = _contexto, pedidos = _pedidos, detalhe = _detalhe,
        dia = vm.Dia.ToString("yyyy-MM-dd"), carregando = _carregando, erro = _erro };
    private static void Exigir()
    {
        if (!Permitido) throw new UnauthorizedAccessException("Este acesso não permite consultar os pedidos da recepção.");
    }

    public void Iniciar()
    {
        _ativa = true;
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
        try
        {
            using var escopo = vm.EscoposPlanejamento.CreateScope();
            var linhas = await escopo.ServiceProvider.GetRequiredService<IPlanejamentoEnfermagemService>().ListarAsync(usuario);
            if (!_ativa || g != _geracao || usuario != SessaoUsuario.Atual.UsuarioId) return;
            _pedidos = linhas; _erro = null;
        }
        catch (Exception ex) { if (_ativa && g == _geracao) _erro = "Avisos indisponíveis. Abra os pedidos e tente Atualizar. " + ex.Message; }
        finally { _carregando = false; }
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

    private async Task Executar(JsonElement m)
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
            vm.MostrarTodosOsProfissionais = true; vm.FiltroProfissional = null; vm.FiltroSala = null;
            vm.ModoSemana = false; vm.Dia = pedido.Inicio.Date;
            _janela?.Close(); return;
        }
        if (acao != "encerrar" || _detalhe?.Pedido.Id != id) return;
        var versao = m.GetProperty("versao").GetGuid();
        var chave = m.GetProperty("idempotencia").GetGuid();
        var observacao = m.TryGetProperty("observacao", out var texto) ? texto.GetString() ?? "" : "";
        using (var escopo = vm.EscoposPlanejamento.CreateScope())
            await escopo.ServiceProvider.GetRequiredService<IPlanejamentoEnfermagemService>().AtualizarAsync(
                SessaoUsuario.Atual.UsuarioId, id, new(chave, versao, "encerrar", observacao));
        await Atualizar(); await Detalhar(id, false);
    }

    private async Task Detalhar(int id, bool registrarLeitura)
    {
        using var escopo = vm.EscoposPlanejamento.CreateScope();
        var svc = escopo.ServiceProvider.GetRequiredService<IPlanejamentoEnfermagemService>();
        var usuario = SessaoUsuario.Atual.UsuarioId;
        var detalhe = await svc.DetalheAsync(usuario, id);
        if (registrarLeitura && detalhe.Pedido is { Ativa: true, Situacao: "Novo" })
        {
            await svc.AtualizarAsync(usuario, id, new(Guid.NewGuid(), detalhe.Pedido.Versao, "visto"));
            detalhe = await svc.DetalheAsync(usuario, id);
            await Atualizar();
        }
        if (_janela is not null && usuario == SessaoUsuario.Atual.UsuarioId) _detalhe = detalhe;
    }
    public void Dispose() { Parar(); _timer.Tick -= AtualizarTimer; _janela?.Close(); }
}
