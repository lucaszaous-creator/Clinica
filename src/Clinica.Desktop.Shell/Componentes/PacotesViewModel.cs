using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Uma linha do catálogo de pacotes, formatada para a tela.</summary>
public sealed class LinhaCatalogo
{
    public required int Id { get; init; }
    public required string Nome { get; init; }
    public required string Resumo { get; init; }
    public required string ValorFormatado { get; init; }
    public required bool Ativo { get; init; }
}

/// <summary>Um pacote vendido, com o saldo já calculado.</summary>
public sealed class LinhaPacoteVendido
{
    public required int Id { get; init; }
    public required int PacienteId { get; init; }
    public required string Paciente { get; init; }
    public required string Nome { get; init; }
    public required string Saldo { get; init; }
    public required string Situacao { get; init; }
    public required string ValorFormatado { get; init; }
    public required string Compra { get; init; }
    public required bool Ativo { get; init; }

    /// <summary>
    /// Debitar sessão é ato do BALCÃO (parcela 62): o estado da linha COMPÕE com a
    /// permissão, senão o botão fica aceso e o clique estoura no <c>ExigirAlgum</c> —
    /// o defeito da parcela 41.
    /// </summary>
    public bool PodeUsarSessao => Ativo && SessaoUsuario.Atual.PodeAlgum(
        Permissao.VenderPacote | Permissao.EditarFinanceiro);

    /// <summary>Cancelar a venda desfaz dinheiro de outra pessoa: continua no financeiro.</summary>
    public bool PodeCancelarVenda => Ativo && SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro);
}

/// <summary>
/// Pacotes, planos e vouchers (feature 08): o catálogo do que a clínica vende e o saldo
/// de cada paciente.
///
/// A tela mostra os dois lados juntos de propósito: o balcão vende olhando o catálogo e
/// cobra olhando o saldo, e separar isso em duas seções obrigaria a navegar no meio da
/// conversa com o paciente.
/// </summary>
public sealed partial class PacotesViewModel : ObservableObject
{
    private readonly PacoteService _pacotes;
    private readonly DocumentoFinanceiroService _documentos;
    private readonly DocumentosFinanceirosPdfService _pdfs;
    private readonly ParametrosService _parametros;
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaCatalogo> Catalogo { get; } = [];
    public ObservableCollection<LinhaPacoteVendido> Vendidos { get; } = [];

    /// <summary>Tudo o que o banco devolveu; <see cref="Vendidos"/> é o recorte do filtro.</summary>
    private readonly List<LinhaPacoteVendido> _todosVendidos = [];

    // ---- Filtro (em memória: a lista já veio inteira, e refazer a consulta a cada
    // tecla seria pagar o banco remoto para responder o que a tela já sabe).
    [ObservableProperty] private string _filtroPaciente = string.Empty;

    /// <summary>Só os pacotes com saldo valendo — o recorte de quem está cobrando no balcão.</summary>
    [ObservableProperty] private bool _soAtivos;

    partial void OnFiltroPacienteChanged(string value) => Refiltrar();
    partial void OnSoAtivosChanged(bool value) => Refiltrar();

    public bool FiltroAtivo => SoAtivos || !string.IsNullOrWhiteSpace(FiltroPaciente);

    [RelayCommand]
    private void LimparFiltro()
    {
        FiltroPaciente = string.Empty;
        SoAtivos = false;
    }

    /// <summary>
    /// O estado vazio muda de frase quando há filtro: "nenhum pacote vendido" e "nenhum
    /// bate com o filtro" são respostas diferentes — sem isso, um filtro esquecido faz a
    /// clínica dar a carteira por vazia.
    /// </summary>
    [ObservableProperty] private string _vazioDescricao =
        "A situação do pacote é calculada na hora — vencimento não precisa de rotina.";

    /// <summary>Totais do que foi CARREGADO — o resumo não pode mudar quando o filtro muda o recorte.</summary>
    private int _totalAtivos;
    private int _sessoesEmSaldo;

    [ObservableProperty] private LinhaCatalogo? _catalogoSelecionado;
    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, lista vazia por erro fica idêntica
    /// a lista vazia por não haver nada, e o aviso de falha some junto com o snackbar.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private string _resumo = "—";

    /// <summary>
    /// Habilita os botões de escrita da tela. É a metade VISÍVEL da permissão: o
    /// botão apagado explica por que não dá; a guarda no comando é que impede.
    /// Só desabilitar seria enfeite — um atalho de teclado passaria direto.
    /// </summary>
    public bool PodeEditarFinanceiro => SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro);

    /// <summary>
    /// VENDER, consumir e orçar — os atos do BALCÃO (parcela 62).
    ///
    /// A parcela 60 criou <see cref="Permissao.VenderPacote"/> justamente para a recepção
    /// vender dez sessões sem ganhar o caixa, a conciliação e as contas a pagar junto — e
    /// esta tela continuou exigindo <c>EditarFinanceiro</c> em TUDO. O resultado é o pior
    /// desfecho possível de uma permissão bem-intencionada: o item abre para quem tem o
    /// bit e nenhum botão funciona.
    ///
    /// O corte é o do ato, não o da tela: <b>vender, debitar sessão e orçar</b> são do
    /// balcão; <b>o CATÁLOGO</b> (preço de tabela) e o <b>CANCELAMENTO de uma venda</b>
    /// continuam sob <c>EditarFinanceiro</c> — o primeiro muda o preço para todo mundo,
    /// o segundo desfaz o dinheiro que outra pessoa registrou (a regra 2 da parcela 49,
    /// a mesma que separou <c>EstornarBaixa</c> de <c>BaixarGuia</c>).
    /// </summary>
    public bool PodeVender => SessaoUsuario.Atual.PodeAlgum(
        Permissao.VenderPacote | Permissao.EditarFinanceiro);

    public PacotesViewModel(
        PacoteService pacotes, DocumentoFinanceiroService documentos,
        DocumentosFinanceirosPdfService pdfs, ParametrosService parametros,
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _pacotes = pacotes;
        _documentos = documentos;
        _pdfs = pdfs;
        _parametros = parametros;
        // A venda escolhe o paciente pelo seletor da suíte, que abre escopo próprio a
        // cada busca — por isso a fábrica, e não o serviço já resolvido.
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        _ = CarregarAsync();
    }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = string.Empty;
            MensagemEhErro = false;

            Catalogo.Clear();
            foreach (var p in await _pacotes.CatalogoAsync(somenteAtivos: false))
                Catalogo.Add(new LinhaCatalogo
                {
                    Id = p.Id,
                    Nome = p.Nome,
                    Resumo = ResumirCatalogo(p),
                    ValorFormatado = p.Valor.ToString("C"),
                    Ativo = p.Ativo
                });

            var vendidos = await _pacotes.VendidosAsync();

            _todosVendidos.Clear();
            foreach (var v in vendidos)
                _todosVendidos.Add(new LinhaPacoteVendido
                {
                    Id = v.PacoteId,
                    PacienteId = v.PacienteId,
                    Paciente = v.PacienteNome ?? "—",
                    Nome = v.Nome,
                    Saldo = v.SaldoRotulo,
                    Situacao = Clinica.Domain.RotulosEnum.De(v.Situacao),
                    ValorFormatado = v.Valor.ToString("C"),
                    Compra = v.DataCompra.ToString("dd/MM/yyyy"),
                    Ativo = v.Ativo
                });

            _totalAtivos = vendidos.Count(v => v.Ativo);
            _sessoesEmSaldo = vendidos.Where(v => v.Ativo).Sum(v => v.SaldoSessoes ?? 0);

            Refiltrar();
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar("Financeiro — pacotes não puderam ser carregados", ex);
            Erro($"Não foi possível carregar os pacotes: {ex.Message}");
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>
    /// Aplica o filtro sobre o que já foi lido — em memória, sem ida ao banco.
    /// </summary>
    private void Refiltrar()
    {
        Vendidos.Clear();
        foreach (var v in _todosVendidos.Where(v =>
                     (!SoAtivos || v.Ativo)
                     && Busca.Casa(v.Paciente, FiltroPaciente)))
            Vendidos.Add(v);

        OnPropertyChanged(nameof(FiltroAtivo));

        // O resumo DIZ que está filtrado: "12 pacotes" e "12 de 90 no filtro" respondem
        // perguntas diferentes, e quem volta à tela depois do café não lembra o filtro.
        Resumo = FiltroAtivo
            ? $"{Vendidos.Count} de {_todosVendidos.Count} pacote(s) no filtro · "
              + $"{_totalAtivos} ativo(s) no total"
            : $"{_todosVendidos.Count} pacote(s) vendidos · {_totalAtivos} ativo(s) · "
              + $"{_sessoesEmSaldo} sessão(ões) em saldo";

        VazioDescricao = FiltroAtivo
            ? "Nenhum pacote vendido bate com o filtro — limpe-o para ver todos."
            : "A situação do pacote é calculada na hora — vencimento não precisa de rotina.";
    }

    private static string ResumirCatalogo(PacoteCatalogo p)
    {
        var sessoes = p.SessoesIncluidas is { } n ? $"{n} sessões" : "sessões livres";
        var validade = p.ValidadeDias is { } dias ? $" · vale {dias} dias" : string.Empty;
        // Rótulo, nunca o identificador: "Sessoes" sem cedilha é o enum vazando (parcela 41).
        return $"{Clinica.Domain.RotulosEnum.De(p.Tipo)} · {sessoes}{validade}";
    }

    // ==================== Catálogo ====================

    /// <summary>
    /// Abre o CATÁLOGO — o que está à venda (parcela 49).
    ///
    /// Ele era uma coluna de 380 px permanente ao lado dos pacotes vendidos. A tela existe
    /// para responder "quanto cada paciente ainda tem para usar"; o catálogo é a tabela
    /// que a clínica define uma vez e só revisita em reajuste. É a primeira pergunta do
    /// `README.md`: isto é o que a pessoa VÊ nesta tela, ou o que ela FAZ de vez em
    /// quando? O segundo caso é botão.
    ///
    /// A janela recebe ESTE ViewModel, não uma cópia: catálogo e vendidos saem da mesma
    /// leitura, e dois VMs dariam duas verdades sobre a mesma tabela.
    /// </summary>
    [RelayCommand]
    private void AbrirCatalogo()
    {
        new CatalogoPacotesWindow(this)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        }.ShowDialog();
    }

    /// <summary>
    /// O cadastro do catálogo saiu da página e virou janela. A tela responde "o que a
    /// clínica vende e quem tem saldo"; definir a tabela de pacotes é tarefa de
    /// implantação, e o formulário fixo tomava dois terços da coluna do catálogo.
    /// </summary>
    [RelayCommand]
    private async Task NovoPacoteAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "mexer nos pacotes");

        var vm = new PacoteCatalogoEdicaoViewModel(_pacotes);
        var janela = new PacoteCatalogoWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() != true) return;
        _snackbar.Sucesso("Pacote acrescentado ao catálogo.");
        await CarregarAsync();
    }

    [RelayCommand]
    private async Task ExcluirDoCatalogoAsync(LinhaCatalogo? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "mexer nos pacotes");
        if (!_dialogo.ConfirmarPerigo("Excluir do catálogo",
                $"Tirar \"{linha.Nome}\" da lista de venda? Os pacotes JÁ VENDIDOS continuam "
                + "valendo — eles guardam a própria cópia do que foi contratado.")) return;

        try
        {
            await _pacotes.ExcluirDoCatalogoAsync(linha.Id);
            _snackbar.Info("Pacote removido do catálogo.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — pacote do catálogo não pôde ser excluído", ex);
            Erro(ex.Message);
        }
    }

    // ==================== Venda e consumo ====================

    [RelayCommand]
    private async Task VenderAsync()
    {
        SessaoUsuario.Atual.ExigirAlgum(
            Permissao.VenderPacote | Permissao.EditarFinanceiro, "vender pacote");

        var vm = new PacoteVendaViewModel(_pacotes, _escopos);
        var janela = new PacoteVendaWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() != true) return;
        _snackbar.Sucesso("Pacote vendido.");
        await CarregarAsync();
    }

    /// <summary>Debita uma sessão à mão (a automática acontece ao concluir o atendimento).</summary>
    [RelayCommand]
    private async Task ConsumirAsync(LinhaPacoteVendido? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.ExigirAlgum(
            Permissao.VenderPacote | Permissao.EditarFinanceiro, "debitar sessão do pacote");
        if (!_dialogo.Confirmar("Usar uma sessão",
                $"Debitar uma sessão de \"{linha.Nome}\" ({linha.Paciente})? Hoje o saldo é: "
                + $"{linha.Saldo}.")) return;

        try
        {
            await _pacotes.ConsumirAsync(
                linha.Id, observacao: "Baixa manual pelo Financeiro", operador: SessaoUsuario.Atual.Operador);
            _snackbar.Sucesso("Sessão debitada.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — sessão do pacote não pôde ser debitada", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>
    /// Abre as sessões já debitadas do pacote — e é de lá que uma delas volta ao saldo.
    ///
    /// Até a parcela 25 a tela só sabia cancelar o pacote INTEIRO: a sessão debitada por
    /// engano (o paciente não veio, a baixa automática pegou o pacote errado) não tinha
    /// como ser desfeita, embora <c>CancelarConsumoAsync</c> existisse e fosse testado
    /// desde a parcela 4.
    /// </summary>
    [RelayCommand]
    private async Task VerSessoesAsync(LinhaPacoteVendido? linha)
    {
        if (linha is null) return;

        var vm = new ConsumosPacoteViewModel(
            _pacotes, _dialogo, linha.Id, $"{linha.Nome} — {linha.Paciente}");

        var janela = new ConsumosPacoteWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        janela.ShowDialog();

        // Só recarrega se alguma sessão voltou ao saldo: espiar a lista não pode custar
        // uma consulta à toa.
        if (vm.Mudou) await CarregarAsync();
    }

    [RelayCommand]
    private async Task CancelarPacoteAsync(LinhaPacoteVendido? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "mexer nos pacotes");

        var motivo = _dialogo.PerguntarTexto(
            "Cancelar pacote",
            $"Por que o pacote \"{linha.Nome}\" de {linha.Paciente} está sendo cancelado? "
            + "Ele continua na lista, com o motivo — as sessões já usadas não somem.");
        if (string.IsNullOrWhiteSpace(motivo)) return;

        try
        {
            await _pacotes.CancelarAsync(linha.Id, motivo, SessaoUsuario.Atual.Operador);
            _snackbar.Info("Pacote cancelado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — pacote não pôde ser cancelado", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>Orçamento do pacote escolhido no catálogo, para o paciente levar.</summary>
    [RelayCommand]
    private async Task OrcarAsync()
    {
        if (CatalogoSelecionado is not { } linha)
        {
            Erro("Escolha um pacote do catálogo para orçar.");
            return;
        }

        // O bit que a folha declara no catálogo (parcela 59): orçamento é documento
        // financeiro, e a tela abre com só VerFinanceiro.
        SessaoUsuario.Atual.ExigirAlgum(
            Permissao.VenderPacote | Permissao.EditarFinanceiro, "emitir orçamento");

        var destinatario = _dialogo.PerguntarTexto(
            "Orçamento", "Para quem é o orçamento? (nome de quem vai receber o papel)");
        if (string.IsNullOrWhiteSpace(destinatario)) return;

        try
        {
            var documento = await _documentos.EmitirOrcamentoDoPacoteAsync(
                linha.Id, pacienteId: null, destinatario, SessaoUsuario.Atual.Operador);

            var pdf = await _pdfs.GerarAsync(documento.Id, await _parametros.ObterPrestadorAsync());

            // O documento JÁ está emitido: falha daqui para a frente é de impressão, e
            // dizer "não foi possível emitir" faria alguém emitir de novo.
            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                pdf, ImpressaoPdf.NomeSeguro($"Orcamento-{documento.Numero.Replace('/', '-')}.pdf"));

            if (erro is not null)
            {
                Erro($"{erro} O orçamento {documento.Numero} está emitido.");
                return;
            }

            _snackbar.Sucesso($"Orçamento {documento.Numero} emitido.");
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Financeiro — orçamento não pôde ser emitido", ex);
            Erro(ex.Message);
        }
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}

/// <summary>
/// Pacote do catálogo, na janela: o que a clínica vende.
///
/// Vale lembrar por que o cadastro é separado da venda: a venda **copia** estes dados.
/// Mudar o preço aqui em novembro não reescreve o que o paciente comprou em março — o
/// vínculo com o catálogo fica só como procedência.
/// </summary>
public sealed partial class PacoteCatalogoEdicaoViewModel : ObservableObject
{
    private readonly PacoteService _pacotes;

    public IReadOnlyList<TipoPacote> Tipos { get; } = Enum.GetValues<TipoPacote>();

    [ObservableProperty] private string? _nome;
    [ObservableProperty] private TipoPacote _tipo = TipoPacote.Sessoes;
    [ObservableProperty] private string? _sessoes;
    [ObservableProperty] private string? _valor;
    [ObservableProperty] private string? _validadeDias;

    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    public event Action? Concluido;

    public PacoteCatalogoEdicaoViewModel(PacoteService pacotes) => _pacotes = pacotes;

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = string.Empty;
        MensagemEhErro = false;

        try
        {
            Salvando = true;

            await _pacotes.SalvarCatalogoAsync(new PacoteCatalogo
            {
                Nome = Nome ?? string.Empty,
                Tipo = Tipo,
                SessoesIncluidas = LerInteiro(Sessoes, "as sessões"),
                Valor = LerDecimal(Valor, "o valor") ?? 0m,
                ValidadeDias = LerInteiro(ValidadeDias, "a validade"),
                Ativo = true
            }, SessaoUsuario.Atual.Operador);

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — pacote do catálogo não pôde ser salvo", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Salvando = false;
        }
    }

    // Os dois pelo leitor do projeto (`Valores`), nunca pelo `TryParse` cru: ele lê na
    // cultura da MÁQUINA, e numa máquina pt-BR o ponto é separador de MILHAR — o preço de
    // tabela digitado "1200.00" entraria no catálogo como R$ 120.000 e as "10" sessões
    // digitadas "1.234" viravam 1234. Nenhum dos dois dá erro: eles gravam outro número.
    private static int? LerInteiro(string? texto, string oQue)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        if (Valores.TentarLerInteiro(texto, out var valor)) return valor;
        throw new InvalidOperationException($"Não entendi {oQue}: use só números.");
    }

    private static decimal? LerDecimal(string? texto, string oQue)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        if (Valores.TentarLerValor(texto, out var valor)) return valor;
        throw new InvalidOperationException($"Não entendi {oQue}: use um número como 250,00.");
    }
}
