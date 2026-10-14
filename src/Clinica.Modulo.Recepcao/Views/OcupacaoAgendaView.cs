using System.Globalization;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Threading;
using Clinica.Application.Abstracoes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Desktop.Shell.WebClinica;
using Clinica.Domain.Entities;
using Clinica.Recepcao.Modulo;
using Clinica.Recepcao.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.Views;

/// <summary>Panorama da agenda. Datas, filtros, autorização e ocupação são resolvidos no host.</summary>
public sealed class OcupacaoAgendaView : UserControl
{
    private readonly IServiceProvider _servicos;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly int _usuario = SessaoUsuario.Atual.UsuarioId;
    private OcupacaoAgenda? _dados;
    private IReadOnlyList<object> _profissionais = [];
    private HashSet<int> _idsProfissionais = [];
    private DateOnly _dia = DateOnly.FromDateTime(DateTime.Today);
    private string _modo = "semana", _contexto = Guid.NewGuid().ToString("N");
    private int? _profissional;
    private int _geracao;
    private bool _carregando, _ativa;
    private string? _erro;
    private static readonly IReadOnlyDictionary<string, string> Rotas = new Dictionary<string, string>
    {
        ["confirmacoes"] = ChavesSuite.ConfirmacoesAgenda, ["retornos"] = "retornos-a-marcar",
        ["conferir"] = "lancamentos", ["convenio"] = "consultas", ["enfermagem"] = "consultorio-sessoes-enfermagem"
    };
    public OcupacaoAgendaView(IServiceProvider servicos)
    {
        _servicos = servicos;
        Content = new PainelClinicoWeb("ocupacao-agenda", Estado, Executar, Exigir);
        _timer.Tick += async (_, _) => { if (!_carregando) await Carregar(); };
        Loaded += async (_, _) => { _ativa = true; _timer.Start(); await Carregar(); };
        Unloaded += (_, _) => { _ativa = false; ++_geracao; _timer.Stop(); };
    }
    private void Exigir()
    {
        if (!SessaoUsuario.Atual.Autenticado || SessaoUsuario.Atual.UsuarioId != _usuario)
            throw new UnauthorizedAccessException("Abra a agenda novamente após entrar.");
        SessaoUsuario.Atual.Exigir(Permissao.VerAgenda, "consultar a ocupação");
    }
    private object Estado() => new { contexto = _contexto, dia = _dia, modo = _modo, profissionalId = _profissional,
        profissionais = _profissionais, dados = _dados, carregando = _carregando, erro = _erro,
        podeAgendar = SessaoUsuario.Atual.Pode(Permissao.EditarAgenda) && NavegacaoSuite.Existe(ModuloRecepcao.ChaveMarcarHorario),
        operacoes = Rotas.Where(r => NavegacaoSuite.Existe(r.Value)).Select(r => r.Key).ToArray() };
    private (DateOnly Inicio, DateOnly Fim) Periodo()
    {
        if (_modo == "dia") return (_dia, _dia);
        if (_modo == "semana") { var segunda = _dia.AddDays(-(((int)_dia.DayOfWeek + 6) % 7)); return (segunda, segunda.AddDays(6)); }
        var primeiro = new DateOnly(_dia.Year, _dia.Month, 1); return (primeiro, primeiro.AddMonths(1).AddDays(-1));
    }
    private async Task Carregar()
    {
        var g = ++_geracao; _carregando = true; _erro = null; _contexto = Guid.NewGuid().ToString("N");
        var (inicio, fim) = Periodo(); var filtro = _profissional;
        try
        {
            Exigir();
            using var scope = _servicos.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IOcupacaoAgendaService>();
            var resultado = await svc.ConsultarAsync(_usuario, inicio, fim, filtro);
            if (!_ativa || g != _geracao || SessaoUsuario.Atual.UsuarioId != _usuario) return;
            _dados = resultado;
            if (filtro is null)
            {
                _profissionais = resultado.Profissionais.Select(p => (object)new { p.Id, p.Nome }).ToArray();
                _idsProfissionais = resultado.Profissionais.Select(p => p.Id).ToHashSet();
            }
        }
        catch (Exception ex)
        {
            if (_ativa && g == _geracao) { _dados = null; _erro = "Não foi possível consultar a ocupação. Tente atualizar."; }
            Clinica.Application.Diagnostico.Registrar("Ocupação da agenda", ex);
        }
        finally { if (g == _geracao) _carregando = false; }
    }
    private async Task Executar(JsonElement m)
    {
        Exigir();
        if (!m.TryGetProperty("contexto", out var c) || c.GetString() != _contexto || _carregando) return;
        var acao = m.GetProperty("acao").GetString();
        if (acao == "atualizar") { await Carregar(); return; }
        if (acao == "modo")
        {
            var modo = m.GetProperty("valor").GetString();
            if (modo is not ("dia" or "semana" or "mes")) return;
            _modo = modo; await Carregar(); return;
        }
        if (acao == "profissional")
        {
            var valor = m.GetProperty("valor");
            int? id = valor.ValueKind == JsonValueKind.Null ? null : valor.GetInt32();
            if (id is not null && !_idsProfissionais.Contains(id.Value)) return;
            _profissional = id; await Carregar(); return;
        }
        if (acao is "data" or "dia")
        {
            if (!DateOnly.TryParseExact(m.GetProperty("valor").GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                || d.Year is < 1901 or > 2199) return;
            if (acao == "dia" && _dados?.Dias.Any(x => x.Dia == d) != true) return;
            _dia = d; if (acao == "dia") _modo = "dia";
            await Carregar(); return;
        }
        if (acao is "anterior" or "proximo" or "hoje")
        {
            var passo = acao == "anterior" ? -1 : 1;
            var destino = acao == "hoje" ? DateOnly.FromDateTime(DateTime.Today)
                : _modo == "mes" ? _dia.AddMonths(passo) : _dia.AddDays(passo * (_modo == "semana" ? 7 : 1));
            if (destino.Year is < 1901 or > 2199) return;
            _dia = destino;
            await Carregar(); return;
        }
        if (acao == "operacao")
        {
            if (Rotas.TryGetValue(m.GetProperty("valor").GetString() ?? "", out var destino)) NavegacaoSuite.Ir(destino);
            return;
        }
        if (_dados is null || _erro != null) return;
        if (acao == "agendar")
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "agendar");
            var ponte = _servicos.GetRequiredService<PreenchimentoNovoAtendimento>();
            ponte.Definir(new(true, _dia.ToDateTime(TimeOnly.MinValue), _profissional, null));
            if (!NavegacaoSuite.Ir(ModuloRecepcao.ChaveMarcarHorario)) ponte.Consumir();
        }
        if (acao == "grade")
        {
            var ponte = _servicos.GetRequiredService<PedidoAgenda>();
            ponte.AbrirComFiltro(_dia, _profissional);
            if (!NavegacaoSuite.Ir(ChavesSuite.AgendaRecepcao)) ponte.Consumir();
        }
    }
}
