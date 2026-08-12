using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// Uma linha da lista, com o semáforo e o nome do convênio JÁ RESOLVIDOS.
///
/// A suíte resolve isso no ViewModel e não por conversor de XAML (é o padrão do
/// <c>LinhaPendencia</c> do Gerente): os conversores <c>UrgenciaParaCor</c> e
/// <c>EnumDescricao</c> são do design system do faturamento e não existem aqui — e
/// duplicá-los para uma tela só seria pagar o débito do design system uma terceira vez.
/// </summary>
public sealed class LinhaConsulta
{
    public required StatusConsultaPaciente Status { get; init; }

    public required string Paciente { get; init; }
    public required string Convenio { get; init; }
    public required string UltimaEmissao { get; init; }
    public required string Vencimento { get; init; }
    public required string Situacao { get; init; }

    public bool EhVermelha => Status.Urgencia == NivelUrgencia.Vermelho;
    public bool EhAmarela => Status.Urgencia == NivelUrgencia.Amarelo;
    public bool EhVerde => Status.Urgencia == NivelUrgencia.Verde;

    /// <summary>Convênio sem consulta renovável não tem o que renovar — o botão fica apagado.</summary>
    public bool TemRenovacao => Status.UsaConsulta;

    /// <summary>
    /// A situação como OPÇÃO DE FILTRO. Deriva do mesmo estado que pinta o semáforo e
    /// escreve a frase — uma segunda classificação divergiria da primeira na primeira
    /// mudança de regra.
    /// </summary>
    public string SituacaoFiltro =>
        !Status.UsaConsulta ? ConsultasViewModel.SituacaoNaoUsa
        : Status.PrecisaRenovar ? ConsultasViewModel.SituacaoPrecisaRenovar
        : Status.Urgencia == NivelUrgencia.Amarelo ? ConsultasViewModel.SituacaoVenceEmBreve
        : Status.UltimaEmissao is null ? ConsultasViewModel.SituacaoNuncaEmitiu
        : ConsultasViewModel.SituacaoEmDia;

    public static LinhaConsulta De(StatusConsultaPaciente s) => new()
    {
        Status = s,
        Paciente = s.PacienteNome,
        Convenio = CatalogoConvenios.Nome(s.ConvenioCodigo, s.Convenio),
        UltimaEmissao = s.UltimaEmissao?.ToString("dd/MM/yyyy") ?? "nunca emitida",
        Vencimento = s.Vencimento?.ToString("dd/MM/yyyy") ?? "—",

        // A frase do modelo (AvisoRenovacao) é a mesma que a agenda e o balcão mostram —
        // é ela que impede o faturamento e a suíte de escreverem duas versões da mesma
        // cobrança. Quando não há o que renovar, ela é nula e a linha diz o estado.
        Situacao = s.AvisoRenovacao
                   ?? (!s.UsaConsulta ? "O convênio não usa consulta renovável."
                       : s.UltimaEmissao is null ? "Nunca emitiu consulta."
                       : "Em dia.")
    };
}

/// <summary>
/// Consultas renováveis: quem está vencido, quem vence logo, e o botão que renova.
///
/// Veio do app de FATURAMENTO na parcela 46, junto do lançamento de atendimento. O motivo
/// não é arrumação de menu: <b>renovar a consulta é uma assinatura, e assinatura se colhe
/// com o paciente presente</b>. A tela morava no posto do faturamento, que é onde a pessoa
/// NÃO está — a recepção via o selo "consulta a renovar" na agenda desde a parcela 44 e não
/// tinha por onde renovar. Descobrir a consulta vencida na hora de faturar é ligar para quem
/// já foi embora, com a guia recusada na mão.
///
/// Ela não deixou de existir para o faturamento: <see cref="ConsultaService"/> é
/// compartilhado, e a consulta renovada aqui é a MESMA linha que
/// <c>PendenciaService.ConsultasAVencerAsync</c> lê do outro lado. O que mudou foi a porta.
/// </summary>
public partial class ConsultasViewModel : ObservableObject, ICarregarAoAbrir
{
    private readonly IServiceScopeFactory _escopos;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaConsulta> Consultas { get; } = new();

    /// <summary>Tudo o que o banco devolveu; <see cref="Consultas"/> é o recorte do filtro.</summary>
    private readonly List<LinhaConsulta> _todas = [];

    // ---- Filtro (a lista é UMA LINHA POR PACIENTE de convênio e só cresce com a
    // carteira; sem filtro, achar a Maria para renovar com ela na frente é rolar a
    // clínica inteira). Os rótulos são const porque a linha e o combo usam os MESMOS —
    // string repetida divergiria na primeira correção de texto.
    public const string TodasSituacoes = "Todas as situações";
    public const string SituacaoPrecisaRenovar = "Precisa renovar";
    public const string SituacaoVenceEmBreve = "Vence em breve";
    public const string SituacaoEmDia = "Em dia";
    public const string SituacaoNuncaEmitiu = "Nunca emitiu";
    public const string SituacaoNaoUsa = "Não usa consulta";

    public const string TodosConvenios = "Todos os convênios";

    // "OpcoesSituacao", e não "Situacoes": a checagem 20 resolve o tipo do ItemsSource
    // pelo NOME da propriedade num mapa global, e "Situacoes" já é coleção de enum em
    // outro ViewModel — o nome único evita o falso positivo sem desligar a rede.
    public string[] OpcoesSituacao { get; } =
    [
        TodasSituacoes, SituacaoPrecisaRenovar, SituacaoVenceEmBreve,
        SituacaoEmDia, SituacaoNuncaEmitiu, SituacaoNaoUsa
    ];

    /// <summary>As operadoras da CARTEIRA carregada — oferecer as sem paciente daria filtro que só leva a vazio.</summary>
    public ObservableCollection<string> Convenios { get; } = [TodosConvenios];

    [ObservableProperty] private string _filtroSituacao = TodasSituacoes;
    [ObservableProperty] private string _filtroConvenio = TodosConvenios;
    [ObservableProperty] private string _filtroPaciente = string.Empty;

    /// <summary>O `Clear()` do combo devolve nulo pelo binding (lição da parcela 56) — remonta sob guarda.</summary>
    private bool _montandoConvenios;

    partial void OnFiltroSituacaoChanged(string value) => Refiltrar();
    partial void OnFiltroPacienteChanged(string value) => Refiltrar();
    partial void OnFiltroConvenioChanged(string value)
    {
        if (value is null)
        {
            FiltroConvenio = TodosConvenios;
            return;
        }
        if (!_montandoConvenios) Refiltrar();
    }

    public bool FiltroAtivo =>
        FiltroSituacao != TodasSituacoes
        || FiltroConvenio != TodosConvenios
        || !string.IsNullOrWhiteSpace(FiltroPaciente);

    [RelayCommand]
    private void LimparFiltro()
    {
        FiltroSituacao = TodasSituacoes;
        FiltroConvenio = TodosConvenios;
        FiltroPaciente = string.Empty;
    }

    /// <summary>
    /// O estado vazio muda de frase quando há filtro: "nenhum paciente com consulta" e
    /// "nenhum bate com o filtro" são respostas diferentes (a lição da lista de espera,
    /// parcela 25) — sem isso, um filtro esquecido faz a recepção dar a carteira por vazia.
    /// </summary>
    [ObservableProperty] private string _vazioDescricao =
        "Nenhum paciente de convênio com consulta renovável foi encontrado.";

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, lista vazia por erro fica idêntica a
    /// lista vazia por não haver nada a renovar, e a recepção conclui que está tudo em dia.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;

    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>Quantos precisam renovar HOJE — o número que decide se vale abrir a tela.</summary>
    [ObservableProperty] private int _totalAlerta;

    /// <summary>
    /// Metade VISÍVEL da permissão. Renovar consulta CRIA a linha que sustenta o
    /// faturamento do convênio, e por isso pede o mesmo bit de lançar atendimento — os
    /// dois são "o que habilita faturar", e separá-los daria à direção duas caixinhas
    /// para a mesma decisão.
    /// </summary>
    public bool PodeRenovar => SessaoUsuario.Atual.Pode(Permissao.LancarAtendimento);

    public ConsultasViewModel(IServiceScopeFactory escopos, IDialogoService dialogo)
    {
        _escopos = escopos;
        _dialogo = dialogo;
    }

    public Task CarregarAsync() => RecarregarAsync();

    [RelayCommand]
    public async Task RecarregarAsync()
    {
        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ConsultaService>();
            var hoje = DateOnly.FromDateTime(DateTime.Today);

            var lista = await service.ListarAsync(hoje);

            _todas.Clear();
            // Quem precisa renovar primeiro: a lista existe para agir, e ordenar por nome
            // faria a recepção varrer trinta linhas em dia para achar as três que pedem
            // assinatura.
            foreach (var c in lista.OrderByDescending(c => c.PrecisaRenovar)
                                   .ThenBy(c => c.DiasParaVencer ?? int.MaxValue)
                                   .ThenBy(c => c.PacienteNome))
                _todas.Add(LinhaConsulta.De(c));

            TotalAlerta = lista.Count(c => c.PrecisaRenovar);

            // As operadoras da carteira, preservando a escolha quando ela ainda existe —
            // atualizar a lista não pode desfazer o filtro de quem está trabalhando nela.
            var escolhido = FiltroConvenio;
            _montandoConvenios = true;
            try
            {
                Convenios.Clear();
                Convenios.Add(TodosConvenios);
                foreach (var nome in _todas.Select(l => l.Convenio).Distinct().OrderBy(n => n))
                    Convenios.Add(nome);
                FiltroConvenio = Convenios.Contains(escolhido) ? escolhido : TodosConvenios;
            }
            finally
            {
                _montandoConvenios = false;
            }

            Refiltrar();
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            LogSuite.Registrar("Consultas — a situação não pôde ser lida", ex);
            Erro($"Não foi possível ler as consultas: {ex.Message}");
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>
    /// Aplica o filtro sobre o que já foi lido — em memória, sem ida ao banco: a carga
    /// trouxe a carteira inteira, e refazer a consulta a cada tecla seria pagar o banco
    /// remoto para responder o que a tela já sabe.
    /// </summary>
    private void Refiltrar()
    {
        Consultas.Clear();
        foreach (var l in _todas.Where(l =>
                     (FiltroSituacao == TodasSituacoes || l.SituacaoFiltro == FiltroSituacao)
                     && (FiltroConvenio == TodosConvenios || l.Convenio == FiltroConvenio)
                     && Busca.Casa(l.Paciente, FiltroPaciente)))
            Consultas.Add(l);

        OnPropertyChanged(nameof(FiltroAtivo));

        // O resumo DIZ que está filtrado: "12 pacientes" e "12 de 90 no filtro" respondem
        // perguntas diferentes, e quem volta à tela depois do café não lembra o combo.
        Resumo = _todas.Count == 0
            ? "Nenhum paciente de convênio com consulta renovável."
            : FiltroAtivo
                ? $"{Consultas.Count} de {_todas.Count} paciente(s) no filtro · "
                  + $"{TotalAlerta} precisam renovar no total."
                : TotalAlerta == 0
                    ? $"{_todas.Count} paciente(s) — nenhum precisa renovar hoje."
                    : $"{TotalAlerta} de {_todas.Count} paciente(s) precisam renovar a consulta.";

        VazioDescricao = FiltroAtivo
            ? "Nenhum paciente bate com o filtro — limpe-o para ver a carteira inteira."
            : "Nenhum paciente de convênio com consulta renovável foi encontrado.";
    }

    /// <summary>
    /// Gera/renova a consulta do paciente para hoje. A validade sai da configuração do
    /// convênio (<see cref="ConsultaService"/>), não daqui.
    /// </summary>
    [RelayCommand]
    private async Task RenovarAsync(LinhaConsulta? linha)
    {
        if (linha is null) return;
        var item = linha.Status;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.LancarAtendimento, "renovar a consulta");

            // Convênio que não usa consulta renovável não tem o que renovar. É AVISO e não
            // erro: quem clicou não fez nada de errado, só escolheu a linha que não pede
            // ação — e a linha aparece na lista porque a lista é de todos os pacientes.
            if (!item.UsaConsulta)
            {
                Info($"{item.PacienteNome}: o convênio não usa consulta renovável.");
                return;
            }

            if (!_dialogo.Confirmar("Renovar consulta",
                    $"Gerar/renovar a consulta de {item.PacienteNome} para hoje?")) return;

            using var scope = _escopos.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ConsultaService>();
            await service.RenovarAsync(item.PacienteId, DateOnly.FromDateTime(DateTime.Today));

            Info($"Consulta de {item.PacienteNome} renovada.");
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Consultas — renovação não pôde ser registrada", ex);
            Erro(ex.Message);
            return;
        }

        await RecarregarAsync();
    }

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }

    private void Info(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = false;
    }
}
