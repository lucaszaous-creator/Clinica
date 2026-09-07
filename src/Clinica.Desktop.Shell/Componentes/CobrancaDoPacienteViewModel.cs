using System.Collections.ObjectModel;
using System.Globalization;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Uma conta vencida do paciente, como a janela de cobrança a mostra.</summary>
public sealed partial class LinhaContaVencida : ObservableObject
{
    public required int LancamentoId { get; init; }
    public required string Descricao { get; init; }
    public required string Vencimento { get; init; }
    public required string Atraso { get; init; }
    public required decimal Valor { get; init; }

    public string ValorTexto => Valor.ToString("C2", Brasil);

    /// <summary>Como o paciente está pagando AGORA — escolhido na linha, nunca presumido.</summary>
    [ObservableProperty] private FormaPagamento? _forma;

    /// <summary>Recebida nesta janela: sai da lista sem esperar a recarga.</summary>
    [ObservableProperty] private bool _recebida;

    private static readonly CultureInfo Brasil = new("pt-BR");
}

/// <summary>
/// A COBRANÇA NO BALCÃO (set/2026, item 3 da lista "o que falta para ficar profissional").
///
/// O <c>ElegibilidadeService</c> avisa desde a parcela 27 que o paciente deve — amarelo, com
/// a pessoa na frente, que é "a hora barata de combinar o acerto". Só que a única porta
/// para RECEBER ficava no Financeiro ("Quem me deve"), outro app, de outra pessoa: o
/// balcão via o aviso, combinava o acerto, e o dinheiro entrava no caixa horas depois, ou
/// não entrava. Alerta sem porta no mesmo app é pior que alerta nenhum (parcela 48).
///
/// A janela mora no SHELL porque tem três portas — o Novo atendimento, a ficha do paciente
/// e o que mais vier a mostrar o alerta — e três cópias divergiriam na primeira correção.
/// Ela NÃO grava dinheiro por conta própria: passa pelo <c>InadimplenciaService.ReceberAsync</c>,
/// que delega ao <c>FinanceiroService</c> — quem grava dinheiro continua sendo um só, com a
/// auditoria no mesmo SaveChanges. É a mesma porta que o Financeiro usa.
///
/// A PERMISSÃO é a de quem já põe dinheiro no caixa pelo balcão: receber a parcela vencida
/// de um pacote ou de uma sessão "a receber" é a continuação de <c>VenderPacote</c> e do
/// passo do caixa do Finalizar (<c>LancarAtendimento</c>, parcela 62) — ou o
/// <c>EditarFinanceiro</c> de sempre. Bit novo aqui seria a caixinha que o bit existente
/// já cobre, e o enum tem UM bit sobrando (set/2026).
/// </summary>
public sealed partial class CobrancaDoPacienteViewModel : ObservableObject
{
    public const Permissao QuemRecebe =
        Permissao.EditarFinanceiro | Permissao.LancarAtendimento | Permissao.VenderPacote;

    private readonly IServiceScopeFactory _escopos;
    private readonly IDialogoService _dialogo;
    private readonly int _pacienteId;
    private readonly string? _telefone;

    public string Paciente { get; }

    public ObservableCollection<LinhaContaVencida> Contas { get; } = [];

    public IReadOnlyList<FormaPagamento> Formas { get; } = Enum.GetValues<FormaPagamento>();

    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _ocupado;

    /// <summary>A leitura FALHOU — lista vazia por erro não é "nada a receber".</summary>
    [ObservableProperty] private bool _naoVerificado;

    /// <summary>Metade visível da permissão; quem impede é o <c>ExigirAlgum</c> no comando.</summary>
    public bool PodeReceber => SessaoUsuario.Atual.PodeAlgum(QuemRecebe);

    public bool TemTelefone => !string.IsNullOrWhiteSpace(_telefone);

    /// <summary>Algo foi recebido aqui: quem abriu a janela precisa reler o alerta.</summary>
    public bool Mudou { get; private set; }

    public CobrancaDoPacienteViewModel(
        IServiceScopeFactory escopos, IDialogoService dialogo,
        int pacienteId, string paciente, string? telefone)
    {
        _escopos = escopos;
        _dialogo = dialogo;
        _pacienteId = pacienteId;
        _telefone = telefone;
        Paciente = paciente;
    }

    /// <summary>O devedor cru da última leitura — a mensagem de cobrança é montada dele.</summary>
    private PacienteInadimplente? _devedor;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Mensagem = string.Empty;
            MensagemEhErro = false;
            NaoVerificado = false;

            using var escopo = _escopos.CreateScope();
            var devedor = await escopo.ServiceProvider
                .GetRequiredService<InadimplenciaService>()
                .DoPacienteAsync(_pacienteId, DateOnly.FromDateTime(DateTime.Today));
            _devedor = devedor;

            // Monta fora e publica de uma vez: entre o Clear() e o último Add não há await.
            var linhas = (devedor?.Detalhe ?? [])
                .OrderBy(c => c.Vencimento)
                .Select(c => new LinhaContaVencida
                {
                    LancamentoId = c.LancamentoId,
                    Descricao = c.Descricao,
                    Vencimento = c.Vencimento.ToString("dd/MM/yyyy"),
                    Atraso = c.DiasEmAtraso == 1 ? "vencida há 1 dia" : $"vencida há {c.DiasEmAtraso} dias",
                    Valor = c.Valor
                })
                .ToList();

            Contas.Clear();
            foreach (var l in linhas) Contas.Add(l);

            Resumo = devedor is null
                ? "Nenhuma conta vencida — o alerta deve sumir ao voltar à tela."
                : $"{devedor.Contas} conta(s) vencida(s), somando {devedor.Total.ToString("C2", new CultureInfo("pt-BR"))}"
                  + $" · a mais antiga de {devedor.VencimentoMaisAntigo:dd/MM/yyyy}.";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Balcão — contas vencidas do paciente não puderam ser lidas", ex);
            Contas.Clear();
            NaoVerificado = true;
            Resumo = string.Empty;
            Erro("Não foi possível ler as contas vencidas agora. A lista NÃO está vazia por "
                 + "não haver dívida — ela não pôde ser lida.");
        }
    }

    /// <summary>
    /// Registra o recebimento de UMA conta, pela forma escolhida na linha. Confirmação
    /// obrigatória mostrando o valor: dar baixa por engano apaga uma dívida real, e o
    /// erro só aparece no fim do mês (a regra do Financeiro, copiada, não reescrita).
    /// </summary>
    [RelayCommand]
    private async Task ReceberAsync(LinhaContaVencida? conta)
    {
        if (conta is null || conta.Recebida || Ocupado) return;

        try
        {
            SessaoUsuario.Atual.ExigirAlgum(QuemRecebe, "receber no balcão");

            if (conta.Forma is null)
            {
                Erro("Escolha como o paciente está pagando (dinheiro, PIX, cartão…) antes de receber.");
                return;
            }

            if (!_dialogo.Confirmar("Receber conta",
                    $"{Paciente} — {conta.Descricao}\n\nRegistrar o recebimento de {conta.ValorTexto} "
                    + $"em {RotulosEnum.De(conta.Forma.Value)}, hoje?"))
                return;

            Ocupado = true;
            using var escopo = _escopos.CreateScope();
            await escopo.ServiceProvider.GetRequiredService<InadimplenciaService>().ReceberAsync(
                conta.LancamentoId, DateOnly.FromDateTime(DateTime.Today), conta.Forma,
                SessaoUsuario.Atual.Operador);

            conta.Recebida = true;
            Mudou = true;
            Mensagem = $"Recebido: {conta.ValorTexto} ({conta.Descricao}).";
            MensagemEhErro = false;
            await CarregarAsync();
            // A recarga zera a mensagem: reescreve DEPOIS (a lição da parcela 68).
            Mensagem = $"Recebido: {conta.ValorTexto} ({conta.Descricao}).";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Balcão — recebimento não pôde ser registrado", ex);
            Erro($"Não foi possível registrar o recebimento: {ex.Message}");
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>
    /// Abre o WhatsApp com o lembrete pronto — o MESMO texto do Financeiro
    /// (<c>InadimplenciaService.MensagemDeCobranca</c>): lembrete, não ameaça, e sem dado
    /// clínico. Um clique por paciente, de propósito: o número é o WhatsApp da clínica.
    /// </summary>
    [RelayCommand]
    private void Cobrar()
    {
        if (_devedor is null) return;
        var erro = Whatsapp.Abrir(_telefone, Paciente, InadimplenciaService.MensagemDeCobranca(_devedor));
        if (erro is null)
        {
            Mensagem = $"WhatsApp aberto para {Paciente}.";
            MensagemEhErro = false;
        }
        else Erro(erro);
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}
