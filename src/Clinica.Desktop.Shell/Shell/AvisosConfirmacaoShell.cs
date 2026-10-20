using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Threading;

namespace Clinica.Desktop.Shell;

/// <summary>Uma resposta de paciente na lista do sino (out/2026).</summary>
public sealed record RespostaNoSino(
    int AgendamentoId, TipoAvisoConfirmacao Tipo, string Titulo, string Sessao, string Detalhe,
    bool PedeAcao, DateOnly Dia, string Paciente = "");

/// <summary>
/// AS RESPOSTAS DOS PACIENTES NO SINO (out/2026 — pedido da clínica: a confirmação, o "não
/// vou" e o silêncio do SMS só apareciam em Relacionamento → Confirmações, e ninguém fica
/// com essa tela aberta). Segue o desenho dos pedidos da enfermagem
/// (<c>AvisosEnfermagemShell</c>): o sino CONTA o que pede ação, a NOTIFICAÇÃO diz o que
/// aconteceu na hora, e clicar leva à tela que resolve.
///
/// A regra do que é aviso mora em <see cref="AvisosDeConfirmacao"/> (Application, testada);
/// aqui só se lê, se anuncia e se desenha. O painel da direção conta pela MESMA decisão.
/// </summary>
public sealed partial class ShellViewModel
{
    private DispatcherTimer? _relogioConfirmacoes;
    private bool _confirmacoesEmCena, _consultandoConfirmacoes;
    private int _geracaoConfirmacoes;

    /// <summary>
    /// O que já virou notificação nesta execução do app. Em memória de propósito — a
    /// primeira leitura SEMEIA o que aconteceu antes da abertura (ver
    /// <see cref="AvisosDeConfirmacao.ParaAnunciar"/>), então reabrir o app não repete o dia.
    /// </summary>
    private readonly HashSet<(int AgendamentoId, TipoAvisoConfirmacao Tipo)> _confirmacoesAnunciadas = [];
    private bool _confirmacoesSemeadas;
    private readonly DateTime _abertoEm = DateTime.Now;
    private DateTime _ultimaLeituraConfirmacoes = DateTime.MinValue;

    public ObservableCollection<RespostaNoSino> RespostasNoSino { get; } = [];

    [ObservableProperty, NotifyPropertyChangedFor(nameof(TotalAvisos), nameof(ContadorAvisos), nameof(TemAvisos), nameof(ContadorRespostas))]
    private int _respostasParaAgir;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ContadorRespostas))]
    private bool _respostasConsultadas;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ContadorAvisos), nameof(TemAvisos), nameof(ContadorRespostas))]
    private bool _erroRespostas;
    [ObservableProperty] private string _situacaoRespostas = "Consultando respostas…";

    /// <summary>
    /// Os mesmos perfis do sino de hoje (Recepção e Gerente), e só onde a tela que resolve
    /// existe para quem está logado — aviso que leva a lugar nenhum é botão que não faz nada.
    /// </summary>
    public bool PodeVerConfirmacoes => Itens.Any(i => i.Chave == ChavesSuite.ConfirmacoesAgenda)
        && _sessaoClinica is { Autenticado: true, Perfil: PerfilAcesso.Recepcao or PerfilAcesso.Gerente }
        && _sessaoClinica.Pode(Permissao.VerAgenda);

    /// <summary>"2 para ligar", "nenhuma pendente", "…" enquanto lê, "!" quando falhou.</summary>
    public string ContadorRespostas => ErroRespostas ? "!" : !RespostasConsultadas ? "…"
        : RespostasParaAgir == 0 ? "nenhuma pendente" : $"{RespostasParaAgir} para ligar";

    private void IniciarAvisosConfirmacao()
    {
        if (_confirmacoesEmCena || !PodeVerConfirmacoes) return;
        _confirmacoesEmCena = true;
        Clinica.Infrastructure.OuvinteDeMudancas.Mudou += MudouConfirmacaoNoBanco;
        // 1 min: as respostas chegam pelo relógio do SMS, que lê o balde a cada 5 min; ler
        // mais vezes do que elas podem mudar seria custo sem notícia. A batida é UMA consulta
        // pequena (projeção das sessões das próximas ~36 h).
        _relogioConfirmacoes ??= new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _relogioConfirmacoes.Tick -= BatidaConfirmacoes; _relogioConfirmacoes.Tick += BatidaConfirmacoes;
        _relogioConfirmacoes.Start(); _ = AtualizarConfirmacoesAsync();
    }

    private void PararAvisosConfirmacao()
    {
        _confirmacoesEmCena = false; ++_geracaoConfirmacoes; _relogioConfirmacoes?.Stop();
        Clinica.Infrastructure.OuvinteDeMudancas.Mudou -= MudouConfirmacaoNoBanco;
    }

    private void BatidaConfirmacoes(object? sender, EventArgs e) => _ = AtualizarConfirmacoesAsync();

    /// <summary>
    /// "Contatos" não tem gatilho de aviso; "Agendamentos" tem — e cancelar ou remarcar o
    /// horário é o que RESOLVE um "não vem". "*" é a escuta que voltou depois de uma queda.
    /// Chega numa thread do Npgsql.
    /// </summary>
    private void MudouConfirmacaoNoBanco(string tabela)
    {
        if (tabela is not ("Agendamentos" or "Contatos" or "*")) return;
        // A agenda muda o dia inteiro (chegada, chamada, fim): no máximo uma releitura a cada
        // 20 s por esse caminho — o relógio de 1 min continua sendo a rede.
        if (tabela != "*" && DateTime.Now - _ultimaLeituraConfirmacoes < TimeSpan.FromSeconds(20)) return;
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => { _ = AtualizarConfirmacoesAsync(); });
    }

    public async Task AtualizarConfirmacoesAsync()
    {
        if (!_confirmacoesEmCena || _consultandoConfirmacoes || !PodeVerConfirmacoes) return;
        var g = _geracaoConfirmacoes; var usuario = _sessaoClinica!.UsuarioId; _consultandoConfirmacoes = true;
        try
        {
            var agora = DateTime.Now;
            _ultimaLeituraConfirmacoes = agora;
            using var scope = _servicos.CreateScope();
            // Confirmação é notícia do DIA: a de ontem para a sessão de amanhã já foi vista.
            var avisos = await scope.ServiceProvider.GetRequiredService<AvisosDeConfirmacaoService>()
                .ListarAsync(agora, agora.Date);
            if (!_confirmacoesEmCena || g != _geracaoConfirmacoes || usuario != _sessaoClinica.UsuarioId) return;

            var (naoVem, semResposta) = AvisosDeConfirmacao.Contar(avisos);
            var linhas = avisos.Take(MaximoNoSino).Select(a => NoSino(a, agora)).ToList();
            // Entre o Clear e o último Add não há await.
            RespostasNoSino.Clear();
            foreach (var l in linhas) RespostasNoSino.Add(l);
            RespostasParaAgir = naoVem + semResposta;
            RespostasMudaram(avisos);
            ErroRespostas = false; RespostasConsultadas = true;
            SituacaoRespostas = avisos.Count == 0 ? "Nenhuma resposta nova nem lembrete pendente."
                : avisos.Count > linhas.Count ? $"Mostrando {linhas.Count} de {avisos.Count}." : "";

            var semearAte = _confirmacoesSemeadas ? (DateTime?)null : _abertoEm - AvisosDeConfirmacao.FolgaDaAbertura;
            _confirmacoesSemeadas = true;
            AnunciarRespostas(AvisosDeConfirmacao.ParaAnunciar(avisos, _confirmacoesAnunciadas, semearAte), agora);
        }
        catch (Exception ex)
        {
            if (_confirmacoesEmCena && g == _geracaoConfirmacoes)
            {
                // Terceiro estado: lista vazia por falha não pode parecer "ninguém respondeu".
                ErroRespostas = true;
                SituacaoRespostas = "Não foi possível consultar as respostas. Abra as confirmações para conferir.";
                Clinica.Application.Diagnostico.Registrar("Respostas dos pacientes no sino", ex);
            }
        }
        finally { _consultandoConfirmacoes = false; }
    }

    /// <summary>Teto da lista do sino: o resto está na tela de Confirmações, a um clique.</summary>
    private const int MaximoNoSino = 8;

    /// <summary>
    /// Cada aviso novo vira notificação UMA vez. Teto de cinco por leitura, como os pedidos:
    /// a primeira rodada de SMS da manhã não pode virar uma rajada que cobre a tela.
    /// </summary>
    private void AnunciarRespostas(IReadOnlyList<AvisoDeConfirmacao> novos, DateTime agora)
    {
        if (Snackbar is null || novos.Count == 0) return;
        // O que pede ação é anunciado por ÚLTIMO (a lista vem com "não vem" na frente): é ele
        // que fica à vista no aviso passageiro e no topo do histórico do sino.
        foreach (var a in novos.Take(5).Reverse()) Snackbar.Info(AvisosDeConfirmacao.Frase(a, agora));
        if (novos.Count > 5) Snackbar.Info($"E mais {novos.Count - 5} resposta(s) de pacientes — veja em Confirmações.");
    }

    public static RespostaNoSino NoSino(AvisoDeConfirmacao a, DateTime agora)
        => new(a.AgendamentoId, a.Tipo, AvisosDeConfirmacao.Titulo(a), AvisosDeConfirmacao.Sessao(a, agora),
            AvisosDeConfirmacao.Detalhe(a, agora), a.PedeAcao, DateOnly.FromDateTime(a.Sessao), a.Paciente);

    [RelayCommand]
    private void AbrirRespostaDoSino(RespostaNoSino? resposta)
    {
        if (resposta is null || !PodeVerConfirmacoes) return;
        LerAviso(LeituraDoSino.ChaveResposta(resposta.AgendamentoId, resposta.Tipo));
        AberturaDasConfirmacoes.PedirDia(resposta.Dia);
        AvisosAbertos = false;
        if (!IrPara(ChavesSuite.ConfirmacoesAgenda, false)) AberturaDasConfirmacoes.ConsumirDia();
    }

    [RelayCommand]
    private void AbrirConfirmacoes()
    {
        if (!PodeVerConfirmacoes) return;
        AvisosAbertos = false;
        IrPara(ChavesSuite.ConfirmacoesAgenda, false);
    }
}
