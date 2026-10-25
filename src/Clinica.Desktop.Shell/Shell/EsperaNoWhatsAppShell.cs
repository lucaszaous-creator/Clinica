using System.Collections.ObjectModel;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Threading;

namespace Clinica.Desktop.Shell;

/// <summary>Uma conversa em que o paciente espera a clínica, na lista do sino (25/10, R8).</summary>
public sealed record EsperaNoWhatsAppShell(string Chave, string ConversaId, string Nome, string Quando);

/// <summary>
/// O WHATSAPP FORA DA ABA (25/10 — R8 e D12 da revisão dos casos de uso; docs/whatsapp-nativo-implementacao.md §16).
/// A aba WhatsApp para o próprio relógio quando sai de vista; quem cuida do CRM com ela fechada é a JANELA:
/// <list type="bullet">
/// <item>o <b>pulso de presença</b> a cada 30 s (decisão da direção: quem usa só o app conta como conectado);</item>
/// <item><b>quem espera resposta</b> a cada 60 s: o número no item "WhatsApp" do menu e UMA linha no sino por
/// espera (a marca de leitura é a do sino: a mesma espera não pisca de novo a cada minuto), com a notificação
/// passageira só para a espera NOVA e só fora da aba.</item>
/// </list>
/// Só para quem tem a aba (o item do menu existe — <see cref="Permissao.EditarAgenda"/>), e só com o CRM com
/// endereço (sem ele, nada aparece). A regra mora em <see cref="ConversasNaTela.PedemResposta"/> e o leitor em
/// <see cref="VigiaDoWhatsApp"/> (Application, testados); aqui só se liga o relógio, se anuncia e se desenha.
/// O relógio liga com os avisos globais (<see cref="IniciarAvisosGlobais"/>) e PARA no fechamento da janela,
/// antes de o app avisar a saída ao CRM — o vigia cancela o pulso em voo.
/// </summary>
public sealed partial class ShellViewModel
{
    private DispatcherTimer? _relogioWhatsApp;
    private VigiaDoWhatsApp? _vigia;
    private bool _whatsAppEmCena, _lendoEspera, _pulsando, _pulsoFalhou, _esperaFalhou;
    private DateTimeOffset _ultimaLeituraDaEspera;

    public ObservableCollection<EsperaNoWhatsAppShell> EsperasNoWhatsApp { get; } = [];

    /// <summary>O CRM respondeu com endereço: a seção do sino e o número do menu existem.</summary>
    public bool WhatsAppLigado { get; private set; }

    /// <summary>A frase da última leitura que falhou (a lista que fica pode estar velha); vazio quando leu.</summary>
    public string SituacaoWhatsApp { get; private set; } = "";

    /// <summary>O número no item "WhatsApp" do menu: quantos pacientes esperam resposta (nada quando ninguém).</summary>
    public string? ContadorDoWhatsApp => WhatsAppLigado ? ConversasNaTela.ContadorDoMenu(EsperasNoWhatsApp.Count) : null;

    /// <summary>Quem tem a aba WhatsApp neste app (o item do menu só existe com EditarAgenda).</summary>
    public bool PodeVigiarWhatsApp => _sessaoClinica is { Autenticado: true } && _sessaoClinica.Pode(Permissao.EditarAgenda)
        && Itens.Any(i => i.Chave == ConversaNoCrm.ChaveDaAba);

    private bool NaAbaDoWhatsApp => Itens.Any(i => i.Chave == ConversaNoCrm.ChaveDaAba && i.EstaAtivo);

    private void IniciarVigiaDoWhatsApp()
    {
        if (_whatsAppEmCena || !PodeVigiarWhatsApp || _servicos.GetService<IClienteDoCrm>() is not { } crm) return;
        _whatsAppEmCena = true;
        _vigia = new VigiaDoWhatsApp(crm);
        _relogioWhatsApp ??= new DispatcherTimer { Interval = VigiaDoWhatsApp.Pulso };
        _relogioWhatsApp.Tick -= BatidaWhatsApp; _relogioWhatsApp.Tick += BatidaWhatsApp;
        _relogioWhatsApp.Start();
        _ = BaterWhatsAppAsync(lerEspera: true);
    }

    /// <summary>Para o relógio e o vigia (que cancela o que estiver em voo): nada mais vai ao CRM por aqui.</summary>
    private void PararVigiaDoWhatsApp()
    {
        _whatsAppEmCena = false;
        _relogioWhatsApp?.Stop();
        _vigia?.Parar();
        _vigia = null;
    }

    // A espera é relida de minuto em minuto; o pulso, a cada batida (30 s). A folga de 5 s evita que um tick
    // adiantado empurre a leitura para a batida seguinte.
    private void BatidaWhatsApp(object? sender, EventArgs e)
        => _ = BaterWhatsAppAsync(DateTimeOffset.Now - _ultimaLeituraDaEspera >= VigiaDoWhatsApp.RitmoDaEspera - TimeSpan.FromSeconds(5));

    private async Task BaterWhatsAppAsync(bool lerEspera)
    {
        if (!_whatsAppEmCena || _vigia is not { } vigia || _sessaoClinica is not { } sessao) return;
        var usuario = sessao.UsuarioId;
        if (!_pulsando)
        {
            _pulsando = true;
            try
            {
                await vigia.PulsarAsync(usuario);
                _pulsoFalhou = false;
            }
            catch (Exception ex)
            {
                // Uma linha no log quando o pulso PASSA a falhar, não uma a cada 30 s com o CRM fora do ar.
                if (!_pulsoFalhou) Clinica.Application.Diagnostico.Registrar("WhatsApp — pulso de presença no CRM", ex);
                _pulsoFalhou = true;
            }
            finally { _pulsando = false; }
        }
        if (!lerEspera || _lendoEspera || !_whatsAppEmCena || !ReferenceEquals(vigia, _vigia)) return;
        _lendoEspera = true;
        _ultimaLeituraDaEspera = DateTimeOffset.Now;
        try
        {
            var leitura = await vigia.LerAsync(usuario, DateTimeOffset.Now);
            if (!_whatsAppEmCena || !ReferenceEquals(vigia, _vigia) || usuario != _sessaoClinica?.UsuarioId) return;
            var linhas = leitura.Esperando
                .Select(e => new EsperaNoWhatsAppShell(e.Chave, e.Id, e.Nome, ConversasNaTela.HaQuanto(e.Minutos))).ToList();
            // Entre o Clear e o último Add não há await.
            EsperasNoWhatsApp.Clear();
            foreach (var l in linhas) EsperasNoWhatsApp.Add(l);
            WhatsAppLigado = leitura.Ligado;
            SituacaoWhatsApp = "";
            _esperaFalhou = false;
            EsperasMudaram(linhas.Select(l => l.Chave));
            OnPropertyChanged(nameof(ContadorDoWhatsApp));
            // A notificação passageira só para a espera NOVA, e não para quem já está olhando a lista.
            if (Snackbar is not null && !NaAbaDoWhatsApp)
                foreach (var n in leitura.Novas.Take(3)) Snackbar.Info(ConversasNaTela.FraseDaEspera(n));
        }
        catch (Exception ex)
        {
            if (!_whatsAppEmCena || !ReferenceEquals(vigia, _vigia)) return;
            // Terceiro estado: a lista que fica pode estar velha, e o sino diz isso (nunca "ninguém espera").
            SituacaoWhatsApp = "O CRM não respondeu na última leitura: a lista pode estar desatualizada.";
            NotificarSino();
            if (!_esperaFalhou) Clinica.Application.Diagnostico.Registrar("WhatsApp — quem espera resposta (fora da aba)", ex);
            _esperaFalhou = true;
        }
        finally { _lendoEspera = false; }
    }

    /// <summary>
    /// Abre a conversa na aba WhatsApp (o mesmo caminho do aviso do @SemDor: <see cref="ConversaNoCrm.ChaveDaAba"/> e o
    /// <c>PedidoDeConversa.ConversaId</c>) e marca o aviso como lido.
    /// </summary>
    public void AbrirEsperaNoWhatsApp(EsperaNoWhatsAppShell espera)
    {
        if (!PodeVigiarWhatsApp) return;
        LerAviso(espera.Chave);
        AvisosAbertos = false;
        if (_servicos.GetService<Clinica.Ui.Blazor.Hospedagem.PedidoDeConversaNoCrm>() is not { } fila) return;
        fila.Definir(new PedidoDeConversa("", espera.Nome, null, null, espera.ConversaId));
        if (!IrPara(ConversaNoCrm.ChaveDaAba, apenasConferir: false)) fila.Consumir();
    }
}
