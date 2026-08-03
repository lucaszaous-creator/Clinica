using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Clinico.Janelas;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Avaliacoes;
using Clinica.Domain.Entities;
using Clinica.Domain.Medidas;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>Uma colheita, como a tabela da série a mostra.</summary>
public sealed class LinhaMedida
{
    public required int MedidaId { get; init; }
    public required string Data { get; init; }
    public required string Tipo { get; init; }
    public required string Valor { get; init; }
    public required string Faixa { get; init; }
    public required string Observacoes { get; init; }

    /// <summary>A faixa é de alerta — a tela pinta o selo de vermelho.</summary>
    public required bool FaixaAlerta { get; init; }

    /// <summary>A colheita tem leitura publicada. Sem ela o selo some, em vez de dizer "normal".</summary>
    public required bool TemFaixa { get; init; }

    public static LinhaMedida De(MedidaClinica m) => new()
    {
        MedidaId = m.Id,
        Data = m.Data.ToString("dd/MM/yyyy"),
        Tipo = m.TipoNome,
        Valor = m.ValorFormatado,
        Faixa = m.FaixaNome ?? string.Empty,
        Observacoes = m.Observacoes ?? string.Empty,
        FaixaAlerta = m.FaixaGravidade == GravidadeFaixa.Alerta,
        TemFaixa = m.TemFaixa
    };
}

/// <summary>Uma ficha da fileira "onde o paciente está hoje".</summary>
public sealed class CartaoMedida
{
    public required string Rotulo { get; init; }
    public required string Valor { get; init; }
    public required string Detalhe { get; init; }
    public required bool Alerta { get; init; }

    /// <summary>
    /// O sistema CALCULOU em vez de ter colhido — hoje só o IMC. A ficha se pinta de
    /// outra cor por isso: número derivado com a mesma cara do medido faz o profissional
    /// procurar no papel uma aferição que nunca existiu.
    /// </summary>
    public bool Derivado { get; init; }
}

/// <summary>
/// ANTROPOMETRIA E SINAIS VITAIS — a série que faltava ao prontuário (parcela 37).
///
/// Por que existe
/// --------------
/// A parcela 36 deu número às cinco especialidades pelas escalas e deixou de fora o número
/// mais básico de todos. O FINDRISC, aplicado desde então, PERGUNTA o IMC e a
/// circunferência de cintura: o paciente responde, o escore é gravado e os dados que o
/// produziram evaporavam. A endocrinologia pesa toda consulta, a geriatria acompanha perda
/// de peso, e as duas escreviam "PA 140/90" em texto livre na evolução — que não desenha
/// curva, não compara consulta com consulta e não vai para o relatório. É a mesma tese da
/// EVA, aplicada ao resto do corpo.
///
/// O que a tela NÃO faz
/// --------------------
/// Não diagnostica e não inventa leitura. A faixa é a referência publicada, copiada na
/// colheita; sem faixa cadastrada para o tipo, o selo SOME em vez de dizer "normal" — o
/// peso isolado não se interpreta, e quem responde "é muito?" é o IMC, que precisa da
/// altura. E o IMC não se digita: ele é derivado, e a tela diz de quando é a altura usada.
/// </summary>
public sealed partial class MedidasViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;
    private readonly PacienteEmFoco _foco;

    /// <summary>
    /// O painel do consultório, que já abre com a agenda do dia e a carteira — e não com
    /// uma caixa de busca vazia sobre uma coluna em branco.
    /// </summary>
    public SeletorClinicoViewModel Seletor { get; }

    /// <summary>Últimas colheitas, uma por tipo — a leitura de abertura.</summary>
    public ObservableCollection<CartaoMedida> Cartoes { get; } = [];

    /// <summary>Histórico do tipo escolhido, do mais recente para o mais antigo.</summary>
    public ObservableCollection<LinhaMedida> Historico { get; } = [];

    /// <summary>A curva do tipo escolhido, em ordem cronológica.</summary>
    public ObservableCollection<PontoGrafico> Curva { get; } = [];

    /// <summary>
    /// Tipos que a tela oferece para ACOMPANHAR — inclui o IMC, que é derivado. O seletor
    /// do diálogo de colheita é outro (<c>CatalogoMedidas.Registraveis</c>), e a diferença
    /// é o ponto: o IMC se vê, não se digita.
    /// </summary>
    public IReadOnlyList<TipoMedida> TiposAcompanhaveis { get; } =
        CatalogoMedidas.TodasAsMedidas;

    [ObservableProperty] private TipoMedida? _tipoAcompanhado;

    [ObservableProperty] private string _paciente = string.Empty;
    [ObservableProperty] private bool _semPaciente = true;

    // ===== leitura da série =====
    [ObservableProperty] private string _primeiro = "—";
    [ObservableProperty] private string _atual = "—";
    [ObservableProperty] private string _variacao = "—";
    [ObservableProperty] private string _leituraSerie = string.Empty;

    /// <summary>A variação da série é de PIORA — a tela destaca.</summary>
    [ObservableProperty] private bool _variacaoPreocupa;

    /// <summary>
    /// A série do tipo acompanhado tem pelo menos um ponto.
    ///
    /// É o que faz o gráfico SUMIR quando não há o que desenhar. Antes ficavam 200 px de
    /// branco com "sem dados no período" flutuando no meio — a área vazia dizia a mesma
    /// coisa que a frase logo acima dela, duas vezes, ocupando um terço do cartão.
    /// </summary>
    [ObservableProperty] private bool _temSerie;

    /// <summary>Linha de contexto da faixa do paciente: o que já se acompanha dele.</summary>
    [ObservableProperty] private string _resumoAtual = string.Empty;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Metade VISÍVEL da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditarProntuario => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    public MedidasViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar,
        IDialogoService dialogo, PacienteEmFoco foco)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _foco = foco;

        // A abertura é pelo peso: é a medida que toda especialidade colhe, e a que dá o
        // IMC. Abrir num tipo que a clínica não usa mostraria curva vazia como boas-vindas.
        TipoAcompanhado = TiposAcompanhaveis.FirstOrDefault(t => t.Codigo == CatalogoMedidas.Peso);

        Seletor = new SeletorClinicoViewModel(escopos, foco);
        Seletor.Escolhido += escolhido => _ = CarregarAsync();

        if (_foco.Definido) _ = CarregarAsync();
    }

    private int PacienteId => _foco.PacienteId ?? 0;

    partial void OnTipoAcompanhadoChanged(TipoMedida? value) => _ = CarregarAsync();

    [RelayCommand]
    public async Task CarregarAsync()
    {
        SemPaciente = PacienteId == 0;
        Cartoes.Clear();
        Historico.Clear();
        Curva.Clear();

        if (SemPaciente)
        {
            Paciente = string.Empty;
            return;
        }

        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;
            Paciente = _foco.Nome;
            Seletor.Sincronizar();

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<MedidaClinicaService>();

            AplicarResumo(await servico.ResumoAsync(PacienteId));

            if (TipoAcompanhado is { } tipo)
            {
                AplicarSerie(await servico.SerieAsync(PacienteId, tipo.Codigo));

                // O IMC é derivado e não tem linha própria no banco: o histórico dele é a
                // própria curva, e listar "colheitas" que ninguém colheu confundiria.
                if (!tipo.Derivada)
                    foreach (var m in await servico.DoPacienteAsync(PacienteId, tipo.Codigo))
                        Historico.Add(LinhaMedida.De(m));
            }
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — medidas do paciente não puderam ser lidas", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    private void AplicarResumo(ResumoMedidas resumo)
    {
        // O IMC abre a fileira, e não um cartão à parte do tamanho da tela com um traço
        // no meio: ele é uma leitura entre as outras — só que calculada, e a ficha diz
        // isso pela cor e pela procedência da altura usada.
        Cartoes.Add(new CartaoMedida
        {
            Rotulo = "IMC (calculado)",
            Valor = resumo.TemImc
                ? $"{resumo.Imc!.Value.ToString("0.#", Cultura)} kg/m²"
                : "—",
            Detalhe = resumo.TemImc
                // A PROCEDÊNCIA vai junto: um IMC derivado de uma altura de três anos
                // atrás continua sendo a melhor leitura disponível, desde que quem lê
                // saiba disso.
                ? $"{resumo.ImcFaixa} · peso de {resumo.ImcEm:dd/MM/yy} "
                  + $"com a altura de {resumo.AlturaUsadaEm:dd/MM/yy}"
                : resumo.Ultimas.Any(m => m.TipoCodigo == CatalogoMedidas.Peso)
                    ? "falta a ALTURA — o peso sozinho não se interpreta"
                    : "registre peso e altura",
            Alerta = resumo.TemImc && resumo.ImcGravidade == GravidadeFaixa.Alerta,
            Derivado = true
        });

        foreach (var m in resumo.Ultimas)
            Cartoes.Add(new CartaoMedida
            {
                Rotulo = m.TipoNome,
                Valor = m.ValorFormatado,
                Detalhe = m.TemFaixa
                    ? $"{m.FaixaNome} · {m.Data:dd/MM/yy}"
                    : $"colhido em {m.Data:dd/MM/yy}",
                Alerta = m.FaixaGravidade == GravidadeFaixa.Alerta
            });

        ResumoAtual = resumo.Ultimas.Count == 0
            ? "Nenhuma medida registrada para este paciente ainda."
            : $"{resumo.Ultimas.Count} medida(s) acompanhada(s) · última colheita em "
              + $"{resumo.Ultimas.Max(m => m.Data):dd/MM/yyyy}";
    }

    private void AplicarSerie(SerieMedida serie)
    {
        foreach (var p in serie.Pontos)
            Curva.Add(new PontoGrafico(p.Data.ToString("dd/MM"), (double)p.Valor));

        TemSerie = serie.TemDados;

        if (!serie.TemDados)
        {
            Primeiro = Atual = Variacao = "—";
            VariacaoPreocupa = false;
            LeituraSerie = $"Nenhuma colheita de {serie.TipoNome} para este paciente ainda.";
            return;
        }

        Primeiro = Formatar(serie.Primeiro, serie);
        Atual = Formatar(serie.Atual, serie);

        if (serie.Variacao is not { } v)
        {
            // Uma medida só não é variação nenhuma. Mostrar 0 diria que o paciente
            // estacionou — e ele nem foi medido duas vezes.
            Variacao = "—";
            VariacaoPreocupa = false;
            LeituraSerie = $"Uma colheita só de {serie.TipoNome}: é a linha de base. "
                           + "A leitura começa na segunda.";
            return;
        }

        var sinal = v > 0 ? "+" : "−";
        Variacao = $"{sinal}{Math.Abs(v).ToString("0.#", Cultura)} {serie.Unidade}";
        VariacaoPreocupa = serie.Melhorou == false;

        LeituraSerie = serie.Melhorou switch
        {
            true => $"{serie.TipoNome} caminhou na direção esperada entre "
                    + $"{serie.Pontos[0].Data:dd/MM/yyyy} e {serie.Pontos[^1].Data:dd/MM/yyyy}, "
                    + $"em {serie.Pontos.Count} colheita(s).",
            false => $"{serie.TipoNome} piorou entre {serie.Pontos[0].Data:dd/MM/yyyy} e "
                     + $"{serie.Pontos[^1].Data:dd/MM/yyyy}. Vale revisar a conduta.",
            _ => $"{serie.TipoNome} não mudou entre {serie.Pontos[0].Data:dd/MM/yyyy} e "
                 + $"{serie.Pontos[^1].Data:dd/MM/yyyy}."
        };
    }

    private static string Formatar(decimal? valor, SerieMedida serie)
        => valor is { } v ? $"{v.ToString("0.#", Cultura)} {serie.Unidade}" : "—";

    /// <summary>
    /// Abre o diálogo da colheita (parcela 37, rodada de leiaute).
    ///
    /// O formulário morava aberto nesta tela, ocupando um terço dela com cinco campos
    /// esticados de ponta a ponta — para um ato que acontece uma vez por consulta. O que
    /// se olha aqui é a SÉRIE; colher é pontual, e pontual não merece painel permanente.
    /// </summary>
    [RelayCommand]
    private async Task RegistrarAsync()
    {
        if (PacienteId == 0) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            var vm = new MedidaEdicaoViewModel(
                _escopos, PacienteId, Paciente, TipoAcompanhado?.Codigo);

            var janela = new RegistrarMedidaWindow(vm)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };

            if (janela.ShowDialog() != true) return;

            _snackbar.Sucesso("Medida registrada.");

            // A tela passa a acompanhar o que acabou de ser medido: registrar peso e
            // continuar olhando a curva da pressão faria a colheita parecer não ter entrado.
            TipoAcompanhado = TiposAcompanhaveis
                                  .FirstOrDefault(t => t.Codigo == vm.TipoRegistrado)
                              ?? TipoAcompanhado;

            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — colheita não pôde ser aberta", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    [RelayCommand]
    private async Task ExcluirAsync(LinhaMedida? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            if (!_dialogo.ConfirmarPerigo("Excluir medida",
                    $"Apagar {linha.Tipo} de {linha.Valor} em {linha.Data}? "
                    + "A exclusão fica registrada na auditoria."))
                return;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<MedidaClinicaService>();
            await servico.ExcluirAsync(linha.MedidaId, SessaoUsuario.Atual.Operador);

            _snackbar.Info("Medida excluída.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — medida não pôde ser excluída", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// A série em planilha. Sai com o mesmo "—" da tela nas colunas sem base — trocar por
    /// zero faria o Excel calcular média sobre um número que não existe.
    /// </summary>
    [RelayCommand]
    private async Task ExportarAsync()
    {
        try
        {
            if (Historico.Count == 0)
            {
                Mensagem = "Não há colheita para exportar.";
                MensagemEhErro = true;
                return;
            }

            var csv = ExportacaoCsv.Montar(
                ["Data", "Medida", "Valor", "Faixa", "Observações"],
                Historico.Select(l => new[]
                {
                    l.Data, l.Tipo, l.Valor,
                    l.TemFaixa ? l.Faixa : "—",
                    string.IsNullOrWhiteSpace(l.Observacoes) ? "—" : l.Observacoes
                }));

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                csv,
                ImpressaoPdf.NomeSeguro(
                    $"medidas-{Paciente}-{TipoAcompanhado?.Nome}-{DateTime.Today:yyyy-MM-dd}.csv"),
                "CSV (*.csv)|*.csv", ".csv");

            if (erro is null) return;
            Mensagem = erro;
            MensagemEhErro = true;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — série de medidas não pôde ser exportada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>Abre o prontuário do mesmo paciente sem perder o foco do posto.</summary>
    [RelayCommand]
    private void VerProntuario() => NavegacaoSuite.Ir(ModuloClinico.ChaveProntuario);

    private static readonly System.Globalization.CultureInfo Cultura = new("pt-BR");
}
