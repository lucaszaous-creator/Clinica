using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>Uma pessoa na lista do painel, já pronta para a linha desenhar.</summary>
public sealed class ItemPacienteClinico
{
    public required int PacienteId { get; init; }
    public required string Nome { get; init; }

    /// <summary>Linha de apoio: o horário e a modalidade, ou a leitura do acompanhamento.</summary>
    public required string Detalhe { get; init; }

    /// <summary>
    /// Agendamento de origem, quando a pessoa veio da agenda do dia. É ELE que permite à
    /// evolução nascer ligada ao horário — sem esse vínculo o consultório continuaria
    /// cobrando o registro de uma sessão que acabou de ser escrita.
    /// </summary>
    public int? AgendamentoId { get; init; }

    public int? AtendimentoId { get; init; }

    /// <summary>A sessão aconteceu e ainda não tem evolução — a linha ganha o ponto.</summary>
    public bool RegistroPendente { get; init; }

    /// <summary>Miniatura do cadastro, quando existe. O avatar cai nas iniciais sem ela.</summary>
    public byte[]? Foto { get; init; }

    /// <summary>
    /// Cabeçalho sob o qual a linha aparece ("Do seu dia", "Recentes", "Resultados").
    ///
    /// É PROPRIEDADE DO ITEM, e não uma lista por bloco, porque a tela agrupa com
    /// <c>CollectionViewSource</c> sobre UMA coleção. Uma ListBox por bloco parece a
    /// mesma coisa e não é: duas ListBox amarradas ao mesmo <c>SelectedItem</c> se
    /// limpam mutuamente — a que não contém o item escolhido devolve <c>null</c> ao
    /// binding e apaga a seleção que a outra acabou de fazer.
    /// </summary>
    public required string Grupo { get; init; }
}

/// <summary>
/// O PAINEL DE PACIENTE do consultório (parcela 37, rodada de UI).
///
/// O defeito que ele corrige
/// -------------------------
/// As telas clínicas abriam com uma caixa de busca vazia, uma coluna de 300 px em branco
/// da altura inteira da janela e um miolo de mil e duzentos pixels com uma frase cinza
/// flutuando no meio. Funcionava — e parecia um esboço. Pior: obrigava o profissional a
/// DIGITAR o nome de alguém que o sistema já sabia que ele ia atender às 9h.
///
/// O consultório conhece duas listas antes de qualquer busca: a **fila do dia** dele
/// (`ConsultorioService.DoDiaAsync`) e a **carteira** (`MeusPacientesAsync`). As duas já
/// existiam, cada uma servindo a uma tela só. Aqui elas viram a abertura de todas as
/// telas de paciente — e a busca deixa de ser o único caminho para virar o atalho de
/// quem procura alguém fora do dia.
///
/// O que ele NÃO faz
/// -----------------
/// Não reescreve a busca. Quando há termo digitado, quem consulta é o
/// <see cref="SeletorPacienteViewModel"/> do shell, que já resolve o corte no SQL, o
/// agrupamento das teclas e o descarte de resposta fora de ordem. Este painel só decide
/// **o que mostrar quando não há termo nenhum** — que era justamente o buraco.
/// </summary>
public sealed partial class SeletorClinicoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly PacienteEmFoco _foco;

    /// <summary>
    /// Quantos da carteira aparecem sem busca. Vinte cobre o mês de quem atende todo dia
    /// e ainda cabe numa rolagem curta; a lista inteira mora em "Meus pacientes".
    /// </summary>
    private const int RecentesVisiveis = 20;

    private const string GrupoDoDia = "Do seu dia";
    private const string GrupoDaClinica = "A agenda de hoje";
    private const string GrupoRecentes = "Recentes";
    private const string GrupoDaCasa = "Pacientes da clínica";
    private const string GrupoBusca = "Resultados da busca";

    private readonly List<ItemPacienteClinico> _doDia = [];
    private readonly List<ItemPacienteClinico> _recentes = [];

    /// <summary>O seletor do shell, para o modo BUSCA. Não se reescreve busca de paciente.</summary>
    public SeletorPacienteViewModel Busca { get; }

    /// <summary>
    /// TUDO o que o painel mostra, numa coleção só — a tela agrupa por
    /// <see cref="ItemPacienteClinico.Grupo"/>. Ver o comentário daquela propriedade para
    /// o motivo de não haver uma lista por bloco.
    /// </summary>
    public ObservableCollection<ItemPacienteClinico> Itens { get; } = [];

    /// <summary>Disparado quando alguém é escolhido — a tela recarrega o que mostra.</summary>
    public event Action<ItemPacienteClinico>? Escolhido;

    [ObservableProperty] private ItemPacienteClinico? _selecionado;

    [ObservableProperty] private bool _carregando;

    /// <summary>Falha de leitura das listas. O painel avisa em vez de emudecer.</summary>
    [ObservableProperty] private string? _erro;

    /// <summary>
    /// O login não está ligado a um profissional cadastrado. A tela DIZ isso: painel com a
    /// clínica inteira sem explicação se lê como defeito, e não como cadastro faltando.
    /// </summary>
    [ObservableProperty] private bool _semVinculo;

    /// <summary>Nada a mostrar em modo nenhum — o painel cai no seu próprio estado vazio.</summary>
    [ObservableProperty] private bool _vazio;

    /// <summary>
    /// O que o vazio DIZ. "Não achei ninguém com esse nome" e "você não tem agenda hoje"
    /// são respostas diferentes, e a mesma frase para as duas faz quem busca concluir que
    /// o sistema está sem pacientes.
    /// </summary>
    [ObservableProperty] private string _textoVazio = string.Empty;

    /// <summary>
    /// Último termo cuja busca já VOLTOU do banco. Sem ele, o instante entre digitar a
    /// primeira letra e a resposta chegar mostraria "nenhum paciente encontrado" — um
    /// vazio que ainda não é resposta de nada.
    /// </summary>
    private string? _termoJaBuscado;

    public SeletorClinicoViewModel(IServiceScopeFactory escopos, PacienteEmFoco foco)
    {
        _escopos = escopos;
        _foco = foco;

        Busca = new SeletorPacienteViewModel(escopos);

        // Uma remontagem por busca CONCLUÍDA, e não uma por linha inserida: `Resultados`
        // é limpa e repovoada item a item, e reagir ao CollectionChanged reconstruiria a
        // lista cinquenta vezes — piscando e derrubando a seleção no meio do caminho.
        Busca.Atualizou += () =>
        {
            _termoJaBuscado = Termo;
            Remontar();
        };

        // Apagar o termo não conclui busca nenhuma, e ainda assim tem de DEVOLVER o dia e
        // a carteira à tela.
        Busca.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SeletorPacienteViewModel.Termo)) Remontar();
        };

        _ = CarregarAsync();
    }

    private string? Termo => Busca.Termo?.Trim();

    private bool Buscando => !string.IsNullOrEmpty(Termo);

    /// <summary>
    /// Lê as duas listas que o consultório conhece de antemão. Falha SOZINHA: sem elas o
    /// painel ainda busca, e uma tela que não abre por causa da lista de cortesia seria
    /// pior do que a lista faltando.
    /// </summary>
    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            Erro = null;
            _doDia.Clear();
            _recentes.Clear();

            var profissionalId = SessaoUsuario.Atual.ProfissionalId;
            SemVinculo = profissionalId is null;

            using var scope = _escopos.CreateScope();
            var consultorio = scope.ServiceProvider.GetRequiredService<ConsultorioService>();

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var dia = await consultorio.DoDiaAsync(hoje, profissionalId);

            foreach (var s in dia.Sessoes)
                _doDia.Add(new ItemPacienteClinico
                {
                    PacienteId = s.PacienteId,
                    Nome = s.PacienteNome,
                    Detalhe = $"{s.DataHora:HH:mm} · {s.Modalidade}"
                              + (s.RegistroPendente ? " · falta a evolução" : string.Empty),
                    AgendamentoId = s.AgendamentoId,
                    AtendimentoId = s.AtendimentoId,
                    RegistroPendente = s.RegistroPendente,
                    Grupo = SemVinculo ? GrupoDaClinica : GrupoDoDia
                });

            // A carteira sem a leitura de dor: aqui a lista é para ESCOLHER alguém, e a
            // consulta da EVA por paciente custaria uma ida ao banco por linha para
            // desenhar um número que a tela ao lado já mostra.
            var pacientes = await consultorio.MeusPacientesAsync(
                profissionalId, RecentesVisiveis, comDor: false);

            var noDia = _doDia.Select(i => i.PacienteId).ToHashSet();
            foreach (var p in pacientes)
            {
                // Quem já está na fila de hoje não se repete embaixo: a mesma pessoa em
                // dois blocos faz a lista parecer maior do que é e some com quem falta ver.
                if (noDia.Contains(p.PacienteId)) continue;

                _recentes.Add(new ItemPacienteClinico
                {
                    PacienteId = p.PacienteId,
                    Nome = p.Nome,
                    Detalhe = Acompanhamento(p.UltimaSessao, p.Sessoes, hoje),
                    Grupo = SemVinculo ? GrupoDaCasa : GrupoRecentes
                });
            }
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — painel de pacientes não pôde ser carregado", ex);
            Erro = "Não foi possível ler a sua agenda e a sua carteira. A busca continua "
                   + "funcionando.";
        }
        finally
        {
            Carregando = false;
            Remontar();
        }
    }

    private static string Acompanhamento(DateOnly? ultima, int sessoes, DateOnly hoje)
    {
        var quantas = sessoes == 1 ? "1 sessão" : $"{sessoes} sessões";
        if (ultima is not { } dia) return quantas;

        var dias = hoje.DayNumber - dia.DayNumber;
        var quando = dias switch
        {
            <= 0 => "hoje",
            1 => "ontem",
            < 7 => $"há {dias} dias",
            < 60 => $"há {dias / 7} semana(s)",
            _ => $"em {dia:dd/MM/yyyy}"
        };

        return $"{quantas} · última {quando}";
    }

    /// <summary>Monta a lista conforme o modo: busca, ou a abertura com dia + carteira.</summary>
    private void Remontar()
    {
        Itens.Clear();

        if (Buscando)
        {
            foreach (var p in Busca.Resultados)
                Itens.Add(new ItemPacienteClinico
                {
                    PacienteId = p.Id,
                    Nome = p.Nome,
                    Detalhe = Cadastro(p),
                    Foto = p.FotoMiniatura,
                    Grupo = GrupoBusca
                });

            Vazio = Itens.Count == 0 && !Busca.Buscando && _termoJaBuscado == Termo;
            TextoVazio = $"Nenhum paciente encontrado para “{Termo}”.";
            return;
        }

        // Digitar troca a lista INTEIRA de propósito: manter "Do seu dia" acima dos
        // resultados faria o primeiro nome da tela não ser o que se procurou.
        foreach (var i in _doDia) Itens.Add(i);
        foreach (var i in _recentes) Itens.Add(i);

        Vazio = Itens.Count == 0 && !Carregando;
        TextoVazio = "Ninguém na agenda de hoje e nenhum atendimento ainda. "
                     + "Busque pelo nome acima.";
    }

    private static string Cadastro(Paciente p)
    {
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(p.Documento)) partes.Add(p.Documento!);
        if (p.DataNascimento is { } nascimento)
        {
            var idade = DateTime.Today.Year - nascimento.Year;
            if (nascimento > DateOnly.FromDateTime(DateTime.Today.AddYears(-idade))) idade--;
            partes.Add($"{idade} anos");
        }
        return string.Join(" · ", partes);
    }

    /// <summary>
    /// Escolhe a pessoa: grava o contexto do posto e avisa a tela.
    ///
    /// O <c>AgendamentoId</c> só viaja quando a escolha veio da FILA. Escolher pela busca
    /// tem de derrubar o vínculo do paciente anterior, senão a evolução de um nasceria
    /// amarrada à sessão do outro — é a razão de <c>PacienteEmFoco.Definir</c> receber os
    /// dois últimos parâmetros como opcionais em vez de haver uma sobrecarga curta.
    /// </summary>
    partial void OnSelecionadoChanged(ItemPacienteClinico? value)
    {
        if (value is null) return;

        _foco.Definir(value.PacienteId, value.Nome, value.AgendamentoId, value.AtendimentoId);
        Escolhido?.Invoke(value);
    }

    /// <summary>
    /// Repõe a seleção visual quando a tela abre com paciente já em foco (veio de outra
    /// aba). Sem isso o painel mostraria a lista sem nada marcado enquanto o conteúdo à
    /// direita fala de alguém — e a tela pareceria dessincronizada.
    /// </summary>
    public void Sincronizar()
    {
        if (_foco.PacienteId is not { } id) return;

        var atual = _doDia.Concat(_recentes).FirstOrDefault(i => i.PacienteId == id);
        if (atual is null) return;

        // `SetProperty` do ObservableObject, e não a propriedade gerada: só esta avisa a
        // tela sem passar pelo `OnSelecionadoChanged`, que dispararia `Escolhido` e
        // recarregaria a tela que acabou de abrir.
        if (!ReferenceEquals(_selecionado, atual))
            SetProperty(ref _selecionado, atual, nameof(Selecionado));
    }
}
