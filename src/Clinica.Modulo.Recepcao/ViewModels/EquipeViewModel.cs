using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Clinica.Desktop.Shell.Componentes;

namespace Clinica.Recepcao.ViewModels;

/// <summary>Uma linha do cadastro de profissionais.</summary>
public sealed class LinhaProfissional
{
    public required int Id { get; init; }
    public required string Nome { get; init; }
    public required string Registro { get; init; }
    public required string Duracao { get; init; }
    public required string Situacao { get; init; }
    public required bool Ativo { get; init; }
}

/// <summary>Uma linha do cadastro de salas.</summary>
public sealed class LinhaSala
{
    public required int Id { get; init; }
    public required string Nome { get; init; }
    public required string Capacidade { get; init; }
    public required string Situacao { get; init; }
    public required bool Ativa { get; init; }
}

/// <summary>
/// Cadastro da equipe: quem atende e onde. É a tela que destrava tudo o mais — agenda
/// por profissional, repasse no financeiro e produtividade no BI se apoiam nela.
///
/// Excluir só vale enquanto o registro nunca foi usado; depois disso o serviço recusa e
/// o caminho é DESATIVAR, para a agenda do passado continuar dizendo quem atendeu.
/// </summary>
/// <summary>Um período de agenda fechada, como a lista mostra.</summary>
public sealed class LinhaBloqueio
{
    public required int Id { get; init; }
    public required string Alvo { get; init; }
    public required string Periodo { get; init; }
    public required string Motivo { get; init; }
    public required string Situacao { get; init; }

    /// <summary>Há sessão marcada dentro do período fechado — alguém precisa remarcar.</summary>
    public required bool TemConflito { get; init; }
}

public sealed partial class EquipeViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaProfissional> Profissionais { get; } = [];
    public ObservableCollection<LinhaSala> Salas { get; } = [];

    /// <summary>
    /// Agenda fechada: férias, feriado, folga, sala em manutenção. Mora aqui, junto de
    /// quem atende e de onde se atende, porque é sobre a DISPONIBILIDADE da equipe — e
    /// não sobre um horário de paciente, que é o assunto da tela de Agenda.
    /// </summary>
    public ObservableCollection<LinhaBloqueio> Bloqueios { get; } = [];

    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, lista vazia por erro fica idêntica
    /// a lista vazia por não haver nada.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>
    /// Habilita os botões de escrita da tela. É a metade VISÍVEL da permissão: o
    /// botão apagado explica por que não dá; a guarda no comando é que impede.
    /// Só desabilitar seria enfeite — um atalho de teclado passaria direto.
    /// </summary>
    public bool PodeGerenciarEquipe => SessaoUsuario.Atual.Pode(Permissao.GerenciarEquipe);

    /// <summary>
    /// Fechar a agenda e empurrar as sessões são atos de AGENDA (parcela 62) — o mesmo
    /// bit do botão "Fechar agenda…" que a tela de Agenda ganhou. Só o CADASTRO da equipe
    /// (profissional, sala) é que continua sob <c>GerenciarEquipe</c>.
    /// </summary>
    public bool PodeEditarAgenda => SessaoUsuario.Atual.Pode(Permissao.EditarAgenda);

    public EquipeViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50). Aqui ele não corrige só uma tela
    /// desatualizada: TODA ação da tela (excluir, bloquear, reabrir, remarcar em lote)
    /// termina chamando esta carga, e duas cargas no ar ao mesmo tempo se INTERCALAVAM —
    /// a segunda limpava a coleção que a primeira ainda estava preenchendo, e a lista
    /// saía com profissionais repetidos ou faltando. Daí montar tudo em listas locais e
    /// só ENTÃO publicar: entre o Clear e o último Add não pode haver await.
    /// </summary>
    private int _geracaoCarga;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = string.Empty;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var equipe = scope.ServiceProvider.GetRequiredService<EquipeService>();

            var profissionais = (await equipe.ProfissionaisAsync())
                .Select(p => new LinhaProfissional
                {
                    Id = p.Id,
                    Nome = p.Nome,
                    Registro = string.IsNullOrWhiteSpace(p.RegistroConselho) ? "—" : p.RegistroConselho!,
                    Duracao = p.DuracaoPadraoMinutos is { } d ? $"{d} min" : "padrão da clínica",
                    Situacao = p.Ativo ? "Ativo" : "Inativo",
                    Ativo = p.Ativo
                })
                .ToList();
            if (geracao != _geracaoCarga) return;

            var salas = (await equipe.SalasAsync())
                .Select(s => new LinhaSala
                {
                    Id = s.Id,
                    Nome = s.Nome,
                    Capacidade = s.Capacidade == 1 ? "1 atendimento" : $"{s.Capacidade} simultâneos",
                    Situacao = s.Ativa ? "Ativa" : "Inativa",
                    Ativa = s.Ativa
                })
                .ToList();
            if (geracao != _geracaoCarga) return;

            var bloqueios = scope.ServiceProvider.GetRequiredService<BloqueioAgendaService>();

            var linhasBloqueio = new List<LinhaBloqueio>();
            foreach (var b in await bloqueios.ListarAsync())
            {
                // Quem já está marcado dentro do período fechado aparece na linha:
                // bloquear NÃO desmarca ninguém, e o paciente combinou aquele horário
                // com uma pessoa — quem desmarca avisa.
                var marcados = await bloqueios.MarcadosDentroAsync(b);
                if (geracao != _geracaoCarga) return;

                linhasBloqueio.Add(new LinhaBloqueio
                {
                    Id = b.Id,
                    Alvo = b.DaClinica
                        ? "Clínica inteira"
                        : b.Profissional?.Rotulo ?? (b.Sala?.Nome is { } sala ? $"Sala {sala}" : "—"),
                    Periodo = b.Inicio.Date == b.Fim.Date
                        ? $"{b.Inicio:dd/MM/yyyy} · {b.Inicio:HH:mm} às {b.Fim:HH:mm}"
                        : $"{b.Inicio:dd/MM/yyyy HH:mm} a {b.Fim:dd/MM/yyyy HH:mm}",
                    Motivo = b.Motivo,
                    TemConflito = marcados.Count > 0,
                    Situacao = marcados.Count == 0
                        ? "nada marcado dentro"
                        : $"{marcados.Count} sessão(ões) marcada(s) dentro — remarque"
                });
            }

            Profissionais.Clear();
            foreach (var linha in profissionais) Profissionais.Add(linha);

            Salas.Clear();
            foreach (var linha in salas) Salas.Add(linha);

            Bloqueios.Clear();
            foreach (var linha in linhasBloqueio) Bloqueios.Add(linha);
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar("Recepção — equipe não pôde ser carregada", ex);
            Mensagem = $"Não foi possível carregar a equipe: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    [RelayCommand]
    private async Task NovoProfissionalAsync() => await AbrirProfissionalAsync(null);

    [RelayCommand]
    private async Task EditarProfissionalAsync(LinhaProfissional? linha)
    {
        if (linha is null) return;
        await AbrirProfissionalAsync(linha.Id);
    }

    [RelayCommand]
    private async Task ExcluirProfissionalAsync(LinhaProfissional? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.GerenciarEquipe, "mexer no cadastro da equipe");
        if (!_dialogo.ConfirmarPerigo("Excluir profissional",
                $"Excluir {linha.Nome}? Só é possível enquanto ele não tem agenda registrada — "
                + "se já tiver, desative-o em vez de excluir.")) return;

        await ExecutarAsync(async equipe =>
        {
            await equipe.ExcluirProfissionalAsync(linha.Id);
            _snackbar.Sucesso("Profissional excluído.");
        });
    }

    [RelayCommand]
    private async Task NovaSalaAsync() => await AbrirSalaAsync(null);

    [RelayCommand]
    private async Task EditarSalaAsync(LinhaSala? linha)
    {
        if (linha is null) return;
        await AbrirSalaAsync(linha.Id);
    }

    [RelayCommand]
    private async Task ExcluirSalaAsync(LinhaSala? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.GerenciarEquipe, "mexer no cadastro da equipe");
        if (!_dialogo.ConfirmarPerigo("Excluir sala",
                $"Excluir a sala {linha.Nome}? Só é possível enquanto ela não tem agenda registrada.")) return;

        await ExecutarAsync(async equipe =>
        {
            await equipe.ExcluirSalaAsync(linha.Id);
            _snackbar.Sucesso("Sala excluída.");
        });
    }

    [RelayCommand]
    private async Task NovoBloqueioAsync()
    {
        // A UNIÃO dos bits das portas (a lição da parcela 69): o Salvar da janela aceita
        // `EditarAgenda` OU `GerenciarEquipe` — a porta não pode ser mais estreita que
        // ele, senão quem recebeu só `GerenciarEquipe` em Acessos é barrado na ENTRADA
        // de uma janela cujo Salvar o aceitaria (o corredor sem saída, pelo avesso).
        SessaoUsuario.Atual.ExigirAlgum(
            Permissao.EditarAgenda | Permissao.GerenciarEquipe, "fechar a agenda");

        var vm = new BloqueioEdicaoViewModel(_escopos);
        var janela = new Janelas.BloqueioWindow(vm)
        {
            Owner = JanelaDona.Atual()
        };

        if (janela.ShowDialog() != true) return;

        // O aviso do que já estava marcado é o motivo de a janela existir: bloquear não
        // desmarca ninguém, e sumir com essa informação faria a recepção descobrir o
        // choque quando o paciente aparecesse na porta.
        if (vm.MarcadosDentro.Count > 0)
            _dialogo.Aviso("Agenda fechada — mas já havia sessão marcada",
                $"{vm.MarcadosDentro.Count} sessão(ões) estão marcadas dentro do período fechado. "
                + "Elas continuam na agenda: remarque com o paciente.\n\n"
                + string.Join("\n", vm.MarcadosDentro));
        else
            _snackbar.Sucesso("Agenda fechada no período.");

        await CarregarAsync();
    }

    /// <summary>Reabre a agenda. O que estava marcado dentro continua marcado.</summary>
    /// <summary>
    /// Empurra em bloco as sessões que caíram dentro do período fechado (parcela 28).
    ///
    /// Bloquear continua NÃO desmarcando ninguém — e é exatamente por isso que este botão
    /// precisava existir. Até aqui a tela dizia "3 sessões marcadas dentro — remarque" e
    /// a recepção remarcava uma a uma. Num mês de férias são trinta, e quem tem trinta
    /// para remarcar não remarca: empurra o problema e descobre no dia.
    /// </summary>
    [RelayCommand]
    private async Task RemarcarBloqueioAsync(LinhaBloqueio? linha)
    {
        if (linha is null || !linha.TemConflito) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "remarcar sessões");

        var resposta = _dialogo.PerguntarTexto(
            "Empurrar as sessões",
            $"Em quantos dias empurrar as sessões marcadas dentro de {linha.Periodo}?\n\n"
            + "O HORÁRIO é mantido — o paciente organizou a vida em torno dele, e "
            + "reorganizar horário é conversa de quem fala com ele, não decisão do "
            + "sistema. Data nova que esbarrar em choque é PULADA e aparece na lista, "
            + "para você resolver as poucas que sobrarem.",
            "7");
        if (string.IsNullOrWhiteSpace(resposta)) return;

        if (!int.TryParse(resposta.Trim(), out var dias) || dias == 0)
        {
            _dialogo.Aviso("Número inválido", "Informe em quantos dias empurrar (ex.: 7).");
            return;
        }

        try
        {
            using var scope = _escopos.CreateScope();
            var bloqueios = scope.ServiceProvider.GetRequiredService<BloqueioAgendaService>();
            var r = await bloqueios.RemarcarEmLoteAsync(
                linha.Id, dias, SessaoUsuario.Atual.Operador);

            if (r.Vazio)
            {
                _snackbar.Info("Não havia sessão a remarcar dentro do período.");
            }
            else if (r.TudoRemarcado)
            {
                _snackbar.Sucesso(
                    $"{r.Remarcados.Count} sessão(ões) empurradas em {dias} dia(s).");
            }
            else
            {
                // As recusadas aparecem uma a uma: "3 não deram" mandaria a recepção
                // procurar quais são, que é o trabalho que este botão veio eliminar.
                _dialogo.Aviso(
                    "Nem todas puderam ser empurradas",
                    $"{r.Remarcados.Count} sessão(ões) foram remarcadas.\n\n"
                    + $"{r.Recusados.Count} ficaram como estavam:\n"
                    + string.Join("\n", r.Recusados.Select(Descrever)));
            }

            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — remarcação em lote falhou", ex);
            _snackbar.Erro(ex.Message);
        }
    }

    private static string Descrever((Agendamento Sessao, string Motivo) recusa)
    {
        var nome = recusa.Sessao.Paciente?.Nome ?? "paciente";
        return $"· {nome} — {recusa.Sessao.DataHora:dd/MM HH:mm}: {recusa.Motivo}";
    }

    [RelayCommand]
    private async Task ExcluirBloqueioAsync(LinhaBloqueio? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "reabrir a agenda");
        if (!_dialogo.Confirmar("Reabrir a agenda",
                $"Tirar o bloqueio de {linha.Alvo} ({linha.Periodo})? "
                + "A agenda volta a aceitar marcação nesse período.")) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var bloqueios = scope.ServiceProvider.GetRequiredService<BloqueioAgendaService>();
            await bloqueios.ExcluirAsync(linha.Id, SessaoUsuario.Atual.Operador);

            _snackbar.Info("Agenda reaberta.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — bloqueio não pôde ser removido", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    private async Task AbrirProfissionalAsync(int? id)
    {
        SessaoUsuario.Atual.Exigir(Permissao.GerenciarEquipe, "mexer no cadastro da equipe");
        var vm = new ProfissionalEdicaoViewModel(_escopos, id);
        var janela = new Janelas.ProfissionalWindow(vm)
        {
            Owner = JanelaDona.Atual()
        };

        if (janela.ShowDialog() != true) return;
        _snackbar.Sucesso("Profissional salvo.");
        await CarregarAsync();
    }

    private async Task AbrirSalaAsync(int? id)
    {
        SessaoUsuario.Atual.Exigir(Permissao.GerenciarEquipe, "mexer no cadastro da equipe");
        var vm = new SalaEdicaoViewModel(_escopos, id);
        var janela = new Janelas.SalaWindow(vm)
        {
            Owner = JanelaDona.Atual()
        };

        if (janela.ShowDialog() != true) return;
        _snackbar.Sucesso("Sala salva.");
        await CarregarAsync();
    }

    private async Task ExecutarAsync(Func<EquipeService, Task> acao)
    {
        try
        {
            using var scope = _escopos.CreateScope();
            await acao(scope.ServiceProvider.GetRequiredService<EquipeService>());
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — operação na equipe falhou", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
