using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Ui.Blazor.Telas;

/// <summary>
/// A AGENDA DENTRO DA CONVERSA (nov/2026, aprovada pelo dono): a aba "Agenda" do painel do contato. A secretária confere
/// vagas, marca, remarca e desmarca sem sair da conversa. A tela só desenha e chama: as opções, quem atende, as vagas
/// juntas e as frases vêm de <see cref="AgendaDaConversa"/>; as vagas, do <see cref="BuscaDeVagasService"/>; marcar e
/// remarcar abrem o MESMO formulário da Agenda (<see cref="Formularios.AgendamentoForm"/>) já com a vaga escolhida — as
/// travas e os avisos do convênio, o conflito e o encaixe continuam num lugar só; desmarcar é o mesmo cancelamento com
/// motivo da conversa. Nada vai à paciente sem o Enviar: depois de cada ato, a mensagem fica pronta no campo.
/// </summary>
public partial class ConversasDoWhatsApp
{
    private enum AbaDoPainel { Contato, Agenda }

    /// <summary>A aba do painel do contato. A Agenda é a que abre pelo botão do topo e pelo aviso "Remarcar".</summary>
    private AbaDoPainel _abaDoPainel = AbaDoPainel.Agenda;

    /// <summary>As conversas cuja ficha ligou sozinha nesta sessão (o aviso "Ligada pelo telefone" e o "Trocar").</summary>
    private readonly HashSet<string> _ligadasSozinhas = [];

    private IReadOnlyList<Profissional> _equipeDaAgenda = [];
    private IReadOnlyList<AtendimentoNaAgenda> _opcoesDaAgenda = [];
    private AtendimentoNaAgenda? _oQue;
    private int? _profissionalDaAgenda;
    private IReadOnlyList<VagaNaConversa> _vagas = [];
    private DateOnly _mesDaAgenda = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateOnly? _diaDaAgenda;
    private bool _carregandoVagas;
    private string? _falhaDasVagas;
    private int _geracaoDasVagas;

    /// <summary>O horário que está sendo REMARCADO pela Agenda (a próxima vaga tocada vira o novo horário dele).</summary>
    private HorarioDoPaciente? _remarcando;

    /// <summary>A vaga tocada: abre o formulário da Agenda já preenchido (marcar ou remarcar).</summary>
    private (int PacienteId, string Nome, VagaNaConversa Vaga, AtendimentoNaAgenda? OQue, HorarioDoPaciente? Antes)? _vagaEscolhida;

    private void AbrirAgenda(HorarioDoPaciente? remarcar = null)
    {
        _fichaAberta = true;
        _abaDoPainel = AbaDoPainel.Agenda;
        if (remarcar is not null)
        {
            if (!Exigir(Permissao.EditarAgenda, "remarcar o horário")) return;
            _remarcando = remarcar;
            _profissionalDaAgenda = remarcar.ProfissionalId;
            _oQue = null;
        }
        _agendaPedidaPara = _aberta?.Conversa?.Id ?? "caso-" + _aberta?.Caso?.Id;
        _ = CarregarAgendaAsync();
    }

    /// <summary>A conversa cuja agenda já foi pedida: a aba pede as vagas UMA vez por conversa, quando aparece (no monitor
    /// largo ela fica sempre aberta; no notebook, só depois do botão).</summary>
    private string? _agendaPedidaPara;

    private void PedirAgendaSePreciso(ConversaDoWhatsApp a)
    {
        var chave = a.Conversa?.Id ?? "caso-" + a.Caso?.Id;
        if (a.Paciente is null || _agendaPedidaPara == chave) return;
        _agendaPedidaPara = chave;
        _ = InvokeAsync(CarregarAgendaAsync);
    }

    private void AbrirContato() { _fichaAberta = true; _abaDoPainel = AbaDoPainel.Contato; }

    /// <summary>O botão "Agenda" do topo: abre a aba; com ela já aberta, fecha o painel.</summary>
    private void AlternarAgenda()
    {
        if (_fichaAberta && _abaDoPainel == AbaDoPainel.Agenda) _fichaAberta = false;
        else AbrirAgenda();
    }

    /// <summary>O nome no topo: abre (ou fecha) o Contato, como no WhatsApp.</summary>
    private void AlternarContato()
    {
        if (_fichaAberta && _abaDoPainel == AbaDoPainel.Contato) _fichaAberta = false;
        else AbrirContato();
    }

    /// <summary>A equipe (uma vez por abertura do painel) e as vagas do "o quê" e do profissional escolhidos.</summary>
    private async Task CarregarAgendaAsync()
    {
        try
        {
            if (_equipeDaAgenda.Count == 0)
            {
                using var escopo = Escopo();
                _equipeDaAgenda = (await escopo.ServiceProvider.GetRequiredService<EquipeService>().ProfissionaisAtivosAsync()).ToList();
                _opcoesDaAgenda = AgendaDaConversa.Opcoes(_equipeDaAgenda);
            }
            if (_remarcando is { } r && _oQue is null)
                _oQue = _opcoesDaAgenda.FirstOrDefault(o => CatalogoModalidades.Base(o.ModalidadeCodigo) == r.Modalidade
                            && (r.ProfissionalId is not { } pid || _equipeDaAgenda.Any(p => p.Id == pid && p.Atende(o.ModalidadeCodigo, o.EspecialidadeCodigo))));
            _oQue ??= _opcoesDaAgenda.FirstOrDefault();
            await LerVagasAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Blazor — a Agenda da conversa não pôde ser lida", ex);
            _falhaDasVagas = "Não foi possível ler a agenda agora. Tente de novo em instantes.";
        }
        if (!Descartada) await InvokeAsync(StateHasChanged);
    }

    /// <summary>As vagas de cada profissional, EM SEQUÊNCIA (o mesmo DbContext — parcela 74), juntas pela hora.</summary>
    private async Task LerVagasAsync()
    {
        var g = ++_geracaoDasVagas;
        _carregandoVagas = true;
        _falhaDasVagas = null;
        if (!Descartada) await InvokeAsync(StateHasChanged);
        try
        {
            var quem = ProfissionaisDaAgenda();
            using var escopo = Escopo();
            var busca = escopo.ServiceProvider.GetRequiredService<BuscaDeVagasService>();
            var porProfissional = new List<(Profissional, IReadOnlyList<Vaga>)>();
            foreach (var p in quem)
                porProfissional.Add((p, (await busca.ProximasAsync(p.Id, DateTime.Now, null, AgendaDaConversa.VagasPorProfissional)).Vagas));
            if (g != _geracaoDasVagas || Descartada) return;
            _vagas = AgendaDaConversa.Juntar(porProfissional);
            if (_diaDaAgenda is { } d && !AgendaDaConversa.DiasComVaga(_vagas).Contains(d)) _diaDaAgenda = null;
        }
        catch (Exception ex)
        {
            if (g != _geracaoDasVagas) return;
            Clinica.Application.Diagnostico.Registrar("Blazor — as vagas da Agenda da conversa não puderam ser lidas", ex);
            _falhaDasVagas = "Não foi possível ler as vagas agora. Tente de novo em instantes.";
            _vagas = [];
        }
        finally { if (g == _geracaoDasVagas) _carregandoVagas = false; }
        if (!Descartada) await InvokeAsync(StateHasChanged);
    }

    private IReadOnlyList<Profissional> ProfissionaisDaAgenda()
    {
        var quem = _oQue is { } o ? AgendaDaConversa.QuemAtende(o, _equipeDaAgenda) : _equipeDaAgenda;
        return _profissionalDaAgenda is { } id ? quem.Where(p => p.Id == id).ToList() : quem;
    }

    private async Task TrocarOQueAsync(ChangeEventArgs e)
    {
        var i = int.TryParse(e.Value?.ToString(), out var n) ? n : -1;
        _oQue = i >= 0 && i < _opcoesDaAgenda.Count ? _opcoesDaAgenda[i] : null;
        if (_profissionalDaAgenda is { } pid && _oQue is { } o && !AgendaDaConversa.QuemAtende(o, _equipeDaAgenda).Any(p => p.Id == pid)) _profissionalDaAgenda = null;
        _diaDaAgenda = null;
        await LerVagasAsync();
    }

    private async Task TrocarProfissionalDaAgendaAsync(ChangeEventArgs e)
    {
        _profissionalDaAgenda = int.TryParse(e.Value?.ToString(), out var id) ? id : null;
        _diaDaAgenda = null;
        await LerVagasAsync();
    }

    private void MudarMesDaAgenda(int k) { _mesDaAgenda = _mesDaAgenda.AddMonths(k); _diaDaAgenda = null; }

    /// <summary>Tocou numa vaga: o formulário da Agenda abre já com paciente, dia, hora, profissional e o "o quê".</summary>
    private void EscolherVaga(ConversaDoWhatsApp a, VagaNaConversa v)
    {
        if (!Exigir(Permissao.EditarAgenda, _remarcando is null ? "marcar horário" : "remarcar o horário") || a.Paciente is not { } p) return;
        _horariosAntesDeMarcar = a.Dados.Horarios;
        _vagaEscolhida = (p.Id, p.Nome, v, _oQue, _remarcando);
    }

    /// <summary>O formulário da Agenda por cima, preenchido com a vaga (desenhado na camada dos formulários).</summary>
    private RenderFragment FormularioDaVaga() => b =>
    {
        if (_vagaEscolhida is not { } e || _aberta?.Paciente?.Id != e.PacienteId) return;
        b.OpenComponent<Formularios.AgendamentoForm>(0);
        if (e.Antes is { } antes) b.AddAttribute(1, nameof(Formularios.AgendamentoForm.AgendamentoId), (int?)antes.AgendamentoId);
        else
        {
            b.AddAttribute(2, nameof(Formularios.AgendamentoForm.PacienteId), (int?)e.PacienteId);
            b.AddAttribute(3, nameof(Formularios.AgendamentoForm.Titulo), $"Marcar horário para {e.Nome}");
            if (e.OQue is { } o)
            {
                b.AddAttribute(4, nameof(Formularios.AgendamentoForm.ModalidadeCodigo), o.ModalidadeCodigo);
                b.AddAttribute(5, nameof(Formularios.AgendamentoForm.EspecialidadeCodigo), o.EspecialidadeCodigo);
            }
        }
        b.AddAttribute(6, nameof(Formularios.AgendamentoForm.Data), (DateTime?)e.Vaga.Inicio.Date);
        b.AddAttribute(7, nameof(Formularios.AgendamentoForm.Hora), e.Vaga.Inicio.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture));
        b.AddAttribute(8, nameof(Formularios.AgendamentoForm.ProfissionalPreferidoId), (int?)e.Vaga.ProfissionalId);
        if (e.Antes is not null) b.AddAttribute(9, nameof(Formularios.AgendamentoForm.TituloDaEdicao), "Remarcar horário");
        b.AddAttribute(10, nameof(Formularios.AgendamentoForm.AoFechar), EventCallback.Factory.Create(this, () => _vagaEscolhida = null));
        b.AddAttribute(11, nameof(Formularios.AgendamentoForm.AoConcluir), EventCallback.Factory.Create<IReadOnlyList<string>>(this, ConcluiuVagaAsync));
        b.CloseComponent();
    };

    /// <summary>Marcou ou remarcou pela Agenda: relê, deixa a mensagem do protótipo no campo (nunca envia) e atualiza as vagas.</summary>
    private async Task ConcluiuVagaAsync(IReadOnlyList<string> avisos)
    {
        var e = _vagaEscolhida;
        _vagaEscolhida = null;
        var antes = _horariosAntesDeMarcar;
        await ReleiaDepoisDoAtoAsync();
        bool preparou = false, janelaFechada = false;
        if (e is { } x && _aberta is { Conversa: not null } a && a.Paciente?.Id == x.PacienteId)
        {
            var novo = x.Antes is { } velho ? a.Dados.Horarios.FirstOrDefault(h => h.AgendamentoId == velho.AgendamentoId)
                                            : RascunhosDaConversa.HorarioNovo(antes, a.Dados.Horarios);
            if (novo is not null)
            {
                if (!a.Janela.PodeEscreverLivre) janelaFechada = true;
                else
                {
                    var r = Rascunho(a);
                    r.Nota = false;
                    r.Texto = x.Antes is { } velho2 ? RascunhosDaConversa.Remarcacao(a.Paciente?.Nome ?? a.Nome, velho2.Quando, novo, Hoje)
                                                    : RascunhosDaConversa.Marcacao(a.Paciente?.Nome ?? a.Nome, novo, Hoje);
                    preparou = true;
                }
            }
        }
        _remarcando = null;
        if (preparou) _feitoNaAgenda = e?.Antes is null ? "Marcado na agenda." : "Remarcado na agenda.";
        Notificacoes.Sucesso(e?.Antes is null ? RascunhosDaConversa.DepoisDeMarcar(preparou, janelaFechada)
            : preparou ? "Horário remarcado na agenda. A mensagem ficou pronta no campo: revise e envie." : "Horário remarcado na agenda.");
        if (avisos.Count > 0) await Dialogos.AvisoAsync("Atenção — guias deste horário", string.Join("\n\n", avisos));
        await LerVagasAsync();
    }

    /// <summary>"Enviar estes horários": cola as 3 próximas vagas na mensagem (a secretária revisa e envia).</summary>
    private void EnviarHorariosLivres(ConversaDoWhatsApp a)
    {
        if (!PodeEscrever || _oQue is not { } o) return;
        var proximas = AgendaDaConversa.Proximas(_vagas);
        if (AgendaDaConversa.HorariosLivres(a.Paciente?.Nome ?? a.Nome, o.Rotulo, proximas, Hoje, _profissionalDaAgenda is not null) is not { } texto) return;
        var r = Rascunho(a);
        r.Nota = false;
        r.Texto = string.IsNullOrWhiteSpace(r.Texto) ? texto : r.Texto.TrimEnd() + "\n" + texto;
        _feitoNaAgenda = "Os horários ficaram na mensagem.";
    }

    /// <summary>
    /// A ficha que liga SOZINHA ao abrir a conversa (pedido do dono): UMA ficha com o número, conversa ainda sem ficha e o bit
    /// da recepção. A trilha fica no CRM (quem ligou) e na clínica; o "Não é esta pessoa? Trocar" fica à vista no painel.
    /// </summary>
    private async Task LigarFichaSozinhaAsync()
    {
        if (_aberta is not { Conversa: { PatientId: null } c } a || !Pode(Permissao.EditarAgenda)
            || AgendaDaConversa.Vinculo(c.PatientId, a.Dados.Candidatas.Count) != DecisaoDoVinculo.LigarSozinha) return;
        if (await GravarNoCrmAsync(s => s.LigarSozinhaAsync(c, a.Telefone, a.Dados.Candidatas), null, "ligar a ficha à conversa") is { PatientId: not null } ligada)
        {
            _ligadasSozinhas.Add(ligada.Id);
            await ReleiaDepoisDoAtoAsync();
        }
    }

    /// <summary>"Não é esta pessoa? Trocar": desliga a ficha; o painel volta a oferecer as fichas do número e a busca.</summary>
    private async Task TrocarFichaDaConversaAsync(ConversaDoWhatsApp a)
    {
        if (a.Conversa is not { PatientId: not null } c) return;
        if (!await Dialogos.ConfirmarAsync("Trocar a ficha desta conversa?", "A ficha atual sai da conversa e você escolhe a certa. Nada muda na agenda nem na ficha."))
            return;
        if (await GravarNoCrmAsync(s => s.DesligarFichaAsync(c), "Ficha desligada. Escolha a certa.", "trocar a ficha da conversa") is not null)
        {
            _ligadasSozinhas.Remove(c.Id);
            await ReleiaDepoisDoAtoAsync();
        }
    }

    /// <summary>
    /// O que acabou de acontecer pela Agenda ("Marcado", "Os horários ficaram na mensagem"): a caixa verde no topo da aba, com
    /// "Ir para a mensagem" — que fecha o painel no celular (onde ele cobre a conversa) e põe o cursor no campo.
    /// </summary>
    private string? _feitoNaAgenda;

    private void IrParaAMensagem() { _feitoNaAgenda = null; _fichaAberta = false; _focarCampo = true; }
}
