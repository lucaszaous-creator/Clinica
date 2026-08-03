using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
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

/// <summary>O último valor de uma medida, no cartão que abre com o paciente.</summary>
public sealed class CartaoMedida
{
    public required string Rotulo { get; init; }
    public required string Valor { get; init; }
    public required string Detalhe { get; init; }
    public required bool Alerta { get; init; }
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
    /// da colheita é outro (<see cref="TiposRegistraveis"/>), e a diferença é o ponto: o
    /// IMC se vê, não se digita.
    /// </summary>
    public IReadOnlyList<TipoMedida> TiposAcompanhaveis { get; } =
        CatalogoMedidas.TodasAsMedidas;

    public IReadOnlyList<TipoMedida> TiposRegistraveis { get; } = CatalogoMedidas.Registraveis;

    [ObservableProperty] private TipoMedida? _tipoAcompanhado;

    // ===== formulário da colheita =====
    [ObservableProperty] private TipoMedida? _tipoNovo;
    [ObservableProperty] private DateTime _dataNova = DateTime.Today;
    [ObservableProperty] private string? _valorNovo;
    [ObservableProperty] private string? _valorSecundarioNovo;
    [ObservableProperty] private string? _observacoesNovas;

    [ObservableProperty] private string _paciente = string.Empty;
    [ObservableProperty] private bool _semPaciente = true;

    // ===== leitura da série =====
    [ObservableProperty] private string _primeiro = "—";
    [ObservableProperty] private string _atual = "—";
    [ObservableProperty] private string _variacao = "—";
    [ObservableProperty] private string _leituraSerie = string.Empty;

    /// <summary>A variação da série é de PIORA — a tela destaca.</summary>
    [ObservableProperty] private bool _variacaoPreocupa;

    /// <summary>O IMC derivado, com a procedência da altura usada.</summary>
    [ObservableProperty] private string _imc = "—";
    [ObservableProperty] private string _imcDetalhe = string.Empty;
    [ObservableProperty] private bool _imcAlerta;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Metade VISÍVEL da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditarProntuario => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    /// <summary>O tipo escolhido para colher pede dois números (a pressão).</summary>
    public bool TemSegundoValor => TipoNovo?.TemSegundoValor ?? false;

    public string RotuloSegundoValor => TipoNovo?.RotuloSegundoValor ?? string.Empty;

    /// <summary>Unidade e nota do tipo escolhido, ao lado do campo.</summary>
    public string AjudaDoTipo => TipoNovo is null
        ? string.Empty
        : $"Em {TipoNovo.Unidade}. {TipoNovo.Nota}".Trim();

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
        TipoNovo = TiposRegistraveis.FirstOrDefault(t => t.Codigo == CatalogoMedidas.Peso);

        Seletor = new SeletorClinicoViewModel(escopos, foco);
        Seletor.Escolhido += escolhido => _ = CarregarAsync();

        if (_foco.Definido) _ = CarregarAsync();
    }

    private int PacienteId => _foco.PacienteId ?? 0;

    partial void OnTipoAcompanhadoChanged(TipoMedida? value) => _ = CarregarAsync();

    partial void OnTipoNovoChanged(TipoMedida? value)
    {
        // Trocar de tipo esquece o segundo campo: a diastólica digitada para a pressão não
        // é número nenhum quando o tipo passa a ser peso, e o serviço a recusaria.
        ValorSecundarioNovo = null;
        OnPropertyChanged(nameof(TemSegundoValor));
        OnPropertyChanged(nameof(RotuloSegundoValor));
        OnPropertyChanged(nameof(AjudaDoTipo));
    }

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
        foreach (var m in resumo.Ultimas)
            Cartoes.Add(new CartaoMedida
            {
                Rotulo = m.TipoNome,
                Valor = m.ValorFormatado,
                Detalhe = m.TemFaixa
                    ? $"{m.FaixaNome} · {m.Data:dd/MM/yyyy}"
                    : $"colhido em {m.Data:dd/MM/yyyy}",
                Alerta = m.FaixaGravidade == GravidadeFaixa.Alerta
            });

        if (!resumo.TemImc)
        {
            Imc = "—";
            ImcAlerta = false;
            ImcDetalhe = resumo.Ultimas.Any(m => m.TipoCodigo == CatalogoMedidas.Peso)
                ? "Falta a ALTURA para calcular o IMC — o peso sozinho não se interpreta."
                : "Registre peso e altura para acompanhar o IMC.";
            return;
        }

        Imc = $"{resumo.Imc!.Value.ToString("0.#", Cultura)} kg/m²";
        ImcAlerta = resumo.ImcGravidade == GravidadeFaixa.Alerta;

        // A PROCEDÊNCIA vai junto: um IMC derivado de uma altura de três anos atrás
        // continua sendo a melhor leitura disponível, desde que quem lê saiba disso.
        ImcDetalhe = $"{resumo.ImcFaixa} · peso de {resumo.ImcEm:dd/MM/yyyy} com a "
                     + $"altura medida em {resumo.AlturaUsadaEm:dd/MM/yyyy}.";
    }

    private void AplicarSerie(SerieMedida serie)
    {
        foreach (var p in serie.Pontos)
            Curva.Add(new PontoGrafico(p.Data.ToString("dd/MM"), (double)p.Valor));

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
    /// Grava a colheita. O número é lido com ponto decimal INVARIANTE depois de trocada a
    /// vírgula: quem digita "78,5" no balcão e "78.5" no consultório precisa gravar o mesmo
    /// peso — é a mesma regra da alíquota do <c>ParametrosService</c>, e ler "2,5" como 25
    /// multiplicaria a medida por dez.
    /// </summary>
    [RelayCommand]
    private async Task RegistrarAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            if (PacienteId == 0)
                throw new InvalidOperationException("Escolha o paciente antes de registrar a medida.");

            if (TipoNovo is not { } tipo)
                throw new InvalidOperationException("Escolha o que está sendo medido.");

            var valor = Numero(ValorNovo)
                ?? throw new InvalidOperationException($"Digite o valor de {tipo.Nome}.");

            decimal? segundo = null;
            if (tipo.TemSegundoValor)
                segundo = Numero(ValorSecundarioNovo)
                    ?? throw new InvalidOperationException(
                        $"{tipo.Nome} precisa dos dois valores: {tipo.RotuloSegundoValor} está em branco.");

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<MedidaClinicaService>();

            await servico.RegistrarAsync(new MedidaClinica
            {
                PacienteId = PacienteId,
                ProfissionalId = SessaoUsuario.Atual.ProfissionalId,
                Data = DateOnly.FromDateTime(DataNova),
                TipoCodigo = tipo.Codigo,
                Valor = valor,
                ValorSecundario = segundo,
                Observacoes = ObservacoesNovas
            }, SessaoUsuario.Atual.Operador);

            ValorNovo = null;
            ValorSecundarioNovo = null;
            ObservacoesNovas = null;

            _snackbar.Sucesso($"{tipo.Nome} registrado.");

            // A tela acompanha o que acabou de ser medido: registrar peso e continuar
            // olhando a curva da pressão faria a colheita parecer não ter entrado.
            TipoAcompanhado = TiposAcompanhaveis.FirstOrDefault(t => t.Codigo == tipo.Codigo)
                              ?? TipoAcompanhado;

            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — medida não pôde ser registrada", ex);
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

    /// <summary>
    /// Lê o número digitado. Aceita vírgula e ponto (a mesma pessoa escreve dos dois
    /// jeitos), e a conversão é INVARIANTE: a cultura da máquina faria "2,5" virar 25 num
    /// posto e 2,5 no outro.
    /// </summary>
    private static decimal? Numero(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        var limpo = texto.Trim().Replace(',', '.');
        return decimal.TryParse(
            limpo, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var valor)
            ? valor
            : null;
    }

    private static readonly System.Globalization.CultureInfo Cultura = new("pt-BR");
}
