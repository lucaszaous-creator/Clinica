using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Threading;

namespace Clinica.Desktop.Shell;

/// <summary>Um aviso "Atenção" do @SemDor na lista do sino (Clinica#307).</summary>
public sealed record AvisoDoSemDorShell(string Chave, string Paciente, string Assunto, string Quando, string Link);

/// <summary>
/// OS AVISOS "ATENÇÃO" DO @SEMDOR NO SINO (out/2026 — Clinica#307; regra 3 da direção: o que pede
/// ação vira aviso). A IA do CRM lê as mensagens dos pacientes e marca o que é sintoma grave,
/// reação ao tratamento ou reclamação; enquanto ninguém responde, o aviso fica aqui, para quem está
/// no balcão sem o CRM aberto. Clicar abre a conversa no CRM (navegador) — a resposta é lá.
///
/// Segue o desenho das respostas dos pacientes (<c>AvisosConfirmacaoShell</c>): a regra mora em
/// <see cref="AvisosDoSemDorRegras"/> (Application, testada) e a leitura em
/// <see cref="Clinica.Infrastructure.AvisosDoSemDorService"/>; aqui só se lê, se anuncia e se desenha.
/// </summary>
public sealed partial class ShellViewModel
{
    private DispatcherTimer? _relogioSemDor;
    private bool _semdorEmCena, _consultandoSemDor, _semdorSemeado;
    private int _geracaoSemDor;
    private readonly HashSet<string> _semdorAnunciados = [];

    public ObservableCollection<AvisoDoSemDorShell> AvisosDoSemDor { get; } = [];

    /// <summary>Falso enquanto o CRM nunca mandou lista: o @SemDor nos apps não está ligado, e a seção não aparece.</summary>
    public bool SemDorLigado { get; private set; }

    public string SituacaoSemDor { get; private set; } = "";

    /// <summary>Quem atende o WhatsApp (<see cref="AcessoAoWhatsApp"/>: recepção e direção, com o bit), e só quem pode ver a ficha (o aviso traz o primeiro nome do paciente).</summary>
    public bool PodeVerAvisosDoSemDor => _sessaoClinica is { Autenticado: true } sessao && AcessoAoWhatsApp.Pode(sessao)
        && sessao.Pode(Permissao.VerFichaPaciente);

    private void IniciarAvisosDoSemDor()
    {
        if (_semdorEmCena || !PodeVerAvisosDoSemDor) return;
        _semdorEmCena = true;
        // 1 min: é o ritmo em que o CRM manda a lista; ler mais vezes não traz notícia.
        _relogioSemDor ??= new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _relogioSemDor.Tick -= BatidaSemDor; _relogioSemDor.Tick += BatidaSemDor;
        _relogioSemDor.Start(); _ = AtualizarAvisosDoSemDorAsync();
    }

    private void PararAvisosDoSemDor()
    {
        _semdorEmCena = false; ++_geracaoSemDor; _relogioSemDor?.Stop();
    }

    private void BatidaSemDor(object? sender, EventArgs e) => _ = AtualizarAvisosDoSemDorAsync();

    public async Task AtualizarAvisosDoSemDorAsync()
    {
        if (!_semdorEmCena || _consultandoSemDor || !PodeVerAvisosDoSemDor) return;
        var g = _geracaoSemDor; var usuario = _sessaoClinica!.UsuarioId; _consultandoSemDor = true;
        try
        {
            var agora = DateTime.Now;
            using var scope = _servicos.CreateScope();
            var (avisos, atual, ligado) = await scope.ServiceProvider.GetRequiredService<Clinica.Infrastructure.AvisosDoSemDorService>()
                .ListarAsync(agora);
            if (!_semdorEmCena || g != _geracaoSemDor || usuario != _sessaoClinica.UsuarioId) return;

            var linhas = avisos.Select(a => new AvisoDoSemDorShell(a.Chave, a.PrimeiroNome, a.Assunto,
                AvisosDoSemDorRegras.Quando(a.Em, agora), a.Link)).ToList();
            // Entre o Clear e o último Add não há await.
            AvisosDoSemDor.Clear();
            foreach (var l in linhas) AvisosDoSemDor.Add(l);
            SemDorLigado = ligado;
            SituacaoSemDor = ligado && !atual ? "O CRM não manda os avisos há alguns minutos. Confira as conversas no CRM." : "";
            AvisosDoSemDorMudaram(linhas.Select(l => l.Chave));

            // A primeira leitura SEMEIA: reabrir o app não repete o anúncio do que já estava lá.
            var novos = linhas.Where(l => _semdorAnunciados.Add(l.Chave)).ToList();
            if (_semdorSemeado && Snackbar is not null)
                foreach (var n in novos.Take(3)) Snackbar.Info(AvisosDoSemDorRegras.Frase(n.Paciente, n.Assunto));
            _semdorSemeado = true;
        }
        catch (Exception ex)
        {
            if (_semdorEmCena && g == _geracaoSemDor)
            {
                // Terceiro estado: lista vazia por falha não pode parecer "ninguém precisa de resposta".
                SituacaoSemDor = "Não foi possível ler os avisos do @SemDor. Confira as conversas no CRM.";
                NotificarSino();
                Clinica.Application.Diagnostico.Registrar("Avisos do @SemDor no sino", ex);
            }
        }
        finally { _consultandoSemDor = false; }
    }

    /// <summary>
    /// Abre a conversa na aba WhatsApp do próprio app (fase 3, 24/10: a tela nativa) e marca o aviso
    /// como lido. App sem a aba (ou link sem conversa): o CRM no navegador, como antes.
    /// </summary>
    public void AbrirAvisoDoSemDor(AvisoDoSemDorShell aviso)
    {
        if (!PodeVerAvisosDoSemDor) return;
        LerAviso(aviso.Chave);
        AvisosAbertos = false;
        if (AvisosDoSemDorRegras.ConversaDoLink(aviso.Link) is { } conversa
            && Itens.Any(i => i.Chave == ConversaNoCrm.ChaveDaAba)
            && _servicos.GetService<Clinica.Ui.Blazor.Hospedagem.PedidoDeConversaNoCrm>() is { } fila)
        {
            fila.Definir(new PedidoDeConversa("", aviso.Paciente, null, null, conversa));
            if (IrPara(ConversaNoCrm.ChaveDaAba, apenasConferir: false)) return;
            fila.Consumir();
        }
        if (!AvisosDoSemDorRegras.LinkSeguro(aviso.Link)) { Snackbar?.Info("O endereço da conversa no CRM não é seguro (https); abra o CRM e procure pelo paciente."); return; }
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(aviso.Link) { UseShellExecute = true }); }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Avisos do @SemDor — abrir o CRM", ex);
            Snackbar?.Info("Não foi possível abrir o navegador. Abra o CRM e procure pelo paciente.");
        }
    }
}
