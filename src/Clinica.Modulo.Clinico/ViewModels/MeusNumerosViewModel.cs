using Clinica.Domain.Entities;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>
/// OS MEUS NÚMEROS (parcela 39): a produtividade do profissional, na tela dele.
///
/// A lacuna
/// --------
/// <c>ProdutividadeProfissional</c> existe desde a parcela 5 e ganhou
/// <c>CompletudeProntuario</c> na 36 — e o único leitor de ambos é o BI do GERENTE. Ou
/// seja: o sistema mede quem atende, a direção vê o número, e a pessoa medida não tem
/// como olhar o próprio. Isso é ruim de produto por dois motivos, e o segundo é o que
/// importa: indicador que só o chefe vê não corrige comportamento nenhum — ele só produz
/// a conversa desagradável no fim do mês, quando o mês já passou.
///
/// A completude de prontuário é o caso exemplar: ela mede o que ESTE módulo existe para
/// resolver (a evolução que fica "para depois"), e quem pode agir sobre ela é justamente
/// quem não a enxergava.
///
/// O que esta tela NÃO é
/// ---------------------
/// Não é o BI da direção com um filtro. Ela mostra <b>só quem está logado</b> e não
/// compara com colegas: ranking entre profissionais é decisão de gestão, e colocá-lo no
/// app de cada um transformaria a métrica num placar. Sem vínculo de profissional no
/// login, a tela diz que está somando a clínica inteira em vez de mostrar número que a
/// pessoa leria como sendo dela.
/// </summary>
public sealed partial class MeusNumerosViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    /// <summary>Períodos oferecidos. Mês corrente é o padrão: é o ciclo da clínica.</summary>
    public IReadOnlyList<string> Periodos { get; } =
        ["Este mês", "Mês passado", "Últimos 90 dias", "Este ano"];

    [ObservableProperty] private string _periodoEscolhido = "Este mês";

    [ObservableProperty] private string _profissional = string.Empty;
    [ObservableProperty] private string _intervalo = string.Empty;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _listaDaClinica;

    /// <summary>POR QUE a lista é da clínica — a frase certa para cada motivo.</summary>
    [ObservableProperty] private string? _motivoDaLista;

    /// <summary>Não houve movimento no período — nem sessão, nem falta.</summary>
    [ObservableProperty] private bool _semMovimento;

    [ObservableProperty] private string _atendidos = "—";
    [ObservableProperty] private string _pacientes = "—";
    [ObservableProperty] private string _faltas = "—";
    [ObservableProperty] private string _noShow = "—";
    [ObservableProperty] private string _ocupacao = "—";
    [ObservableProperty] private string _completude = "—";
    [ObservableProperty] private string _evolucoes = "—";
    [ObservableProperty] private string _melhoraDor = "—";

    /// <summary>
    /// Quantas sessões continuam sem evolução. É o número ACIONÁVEL da tela — os outros
    /// descrevem o passado, este ainda dá para resolver hoje — e por isso ele leva à lista.
    /// </summary>
    [ObservableProperty] private string _dividaProntuario = string.Empty;

    [ObservableProperty] private bool _temDivida;

    /// <summary>
    /// A leitura da completude em palavras. O número sozinho não diz se 80% é bom, e o
    /// profissional que vê "80%" sem régua conclui o que quiser.
    /// </summary>
    [ObservableProperty] private string _leituraCompletude = string.Empty;

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): trocar o período dispara uma carga
    /// por escolha, e a resposta do período anterior pode chegar por último — os números de
    /// "Mês passado" sairiam sob o título "Este mês". Só a carga mais nova escreve na tela.
    /// </summary>
    private int _geracaoCarga;

    public MeusNumerosViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        _ = CarregarAsync();
    }

    partial void OnPeriodoEscolhidoChanged(string value) => _ = CarregarAsync();

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;

            // De quem é esta lista — a resposta mora num lugar só (PostoClinico):
            // a enfermagem NÃO tem agenda própria, e filtrar por ela devolvia a
            // tela vazia justamente para quem está cadastrado certo.
            var profissionalId = PostoClinico.ProfissionalDaLista();
            ListaDaClinica = profissionalId is null;
            MotivoDaLista = PostoClinico.MotivoDaListaAmpla();

            var (inicio, fim) = Intervalo_(PeriodoEscolhido);
            Intervalo = $"{inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}";

            using var scope = _escopos.CreateScope();
            var indicadores = scope.ServiceProvider.GetRequiredService<IndicadoresService>();
            var consultorio = scope.ServiceProvider.GetRequiredService<ConsultorioService>();

            var todos = await indicadores.ProdutividadeAsync(inicio, fim);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            // Sem vínculo, somar a clínica inteira é a única leitura honesta — e a tela
            // diz isso em texto, porque um número da casa apresentado como "meu" é pior
            // do que número nenhum.
            var meu = profissionalId is { } id
                ? todos.FirstOrDefault(p => p.ProfissionalId == id)
                : Somar(todos);

            Profissional = profissionalId is null
                ? "Todos os profissionais"
                : meu?.Nome ?? "Profissional";

            Preencher(meu);

            // A dívida é de HOJE, não do período: ela é a fila de trabalho, e mostrá-la
            // recortada pelo filtro faria escolher "mês passado" esconder o que está em
            // aberto agora.
            var pendentes = await consultorio.RegistrosPendentesAsync(
                DateOnly.FromDateTime(DateTime.Today), profissionalId);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            TemDivida = pendentes.Count > 0;
            DividaProntuario = pendentes.Count switch
            {
                0 => "Nenhuma sessão em aberto — prontuário em dia.",
                1 => "1 sessão dos últimos dias continua sem evolução escrita.",
                var n => $"{n} sessões dos últimos dias continuam sem evolução escrita."
            };
        }
        catch (Exception ex)
        {
            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — os indicadores do profissional não puderam ser carregados", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    private void Preencher(ProdutividadeProfissional? p)
    {
        // Sem linha no período, a pessoa não trabalhou nele — e isso é "—", nunca zero:
        // 0% de ocupação e "não houve agenda" são coisas diferentes, e a segunda não
        // acusa ninguém de nada. É a mesma regra do resto da suíte.
        SemMovimento = p is null || p.Agendados == 0;

        if (p is null)
        {
            Atendidos = Pacientes = Faltas = NoShow = Ocupacao = Completude = "—";
            Evolucoes = MelhoraDor = "—";
            LeituraCompletude = string.Empty;
            return;
        }

        Atendidos = p.Atendidos.ToString();
        Pacientes = p.PacientesDistintos.ToString();
        Faltas = p.Faltas.ToString();
        Evolucoes = p.Evolucoes.ToString();

        // Taxa sem base não é 0%, é "não medido" — quem teve zero horário fechado no
        // período não tem taxa de falta.
        NoShow = p.Fechados == 0 ? "—" : $"{p.TaxaNoShow:0.#}%";
        Ocupacao = p.OcupacaoPercentual is { } o ? $"{o:0.#}%" : "—";

        MelhoraDor = p.MelhoraMediaEva is { } eva
            ? $"{eva:0.#} pontos"
            : "—";

        if (p.CompletudeProntuario is { } completude)
        {
            Completude = $"{completude:0.#}%";
            LeituraCompletude = completude switch
            {
                >= 95 => "Praticamente toda sessão atendida tem registro escrito.",
                >= 80 => "A maior parte tem registro; o que falta ainda dá para escrever.",
                _ => "Boa parte das sessões atendidas ficou sem registro escrito."
            };
        }
        else
        {
            // Nula, nunca 0% — sem sessão atendida, acusar de negligência quem tirou
            // férias seria o defeito que a parcela 36 já documentou.
            Completude = "—";
            LeituraCompletude = "Nenhuma sessão atendida no período — não há o que medir.";
        }
    }

    /// <summary>
    /// Soma da clínica, para quem não tem profissional vinculado. As TAXAS não se somam:
    /// elas são recalculadas sobre os totais, senão a média de percentuais daria um
    /// número que não corresponde a nenhuma leitura real.
    /// </summary>
    private static ProdutividadeProfissional? Somar(IReadOnlyList<ProdutividadeProfissional> todos)
    {
        if (todos.Count == 0) return null;

        var evas = todos.Where(p => p.MelhoraMediaEva is not null).ToList();

        return new ProdutividadeProfissional(
            ProfissionalId: null,
            Nome: "Todos os profissionais",
            Agendados: todos.Sum(p => p.Agendados),
            Atendidos: todos.Sum(p => p.Atendidos),
            Faltas: todos.Sum(p => p.Faltas),
            Cancelados: todos.Sum(p => p.Cancelados),
            Encaixes: todos.Sum(p => p.Encaixes),
            // Pacientes distintos POR profissional não se somam sem duplicar quem é
            // atendido por dois — é o mais próximo que este agregado consegue, e por isso
            // a tela não o apresenta como exato quando não há vínculo.
            PacientesDistintos: todos.Sum(p => p.PacientesDistintos),
            MinutosOcupados: todos.Sum(p => p.MinutosOcupados),
            DiasComAgenda: todos.Sum(p => p.DiasComAgenda),
            MinutosDisponiveis: todos.Sum(p => p.MinutosDisponiveis),
            Evolucoes: todos.Sum(p => p.Evolucoes),
            MelhoraMediaEva: evas.Count == 0 ? null : evas.Average(p => p.MelhoraMediaEva!.Value));
    }

    private static (DateOnly Inicio, DateOnly Fim) Intervalo_(string periodo)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        return periodo switch
        {
            "Mês passado" => (
                new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-1),
                new DateOnly(hoje.Year, hoje.Month, 1).AddDays(-1)),
            "Últimos 90 dias" => (hoje.AddDays(-89), hoje),
            "Este ano" => (new DateOnly(hoje.Year, 1, 1), hoje),
            _ => (new DateOnly(hoje.Year, hoje.Month, 1), hoje)
        };
    }

    /// <summary>Leva à lista do que ainda dá para resolver.</summary>
    [RelayCommand]
    private void AbrirPendentes()
    {
        // O destino exige VerProntuario, e sem o bit ele nem existe na navegação —
        // NavegacaoSuite.Ir voltaria false EM SILÊNCIO e o botão não faria nada (parcela 41).
        if (!SessaoUsuario.Atual.Pode(Permissao.VerProntuario))
        {
            Mensagem = "A lista de sessões sem evolução é prontuário, e o seu acesso não "
                       + "tem essa permissão. A direção libera em Acessos.";
            MensagemEhErro = true;
            return;
        }

        NavegacaoSuite.Ir(ModuloClinico.ChaveRegistrosPendentes);
    }
}
