using System.Collections.ObjectModel;
using System.Globalization;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Controls;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>Uma linha do orçamento. O total da linha é calculado, nunca digitado.</summary>
public sealed partial class LinhaOrcamento : ObservableObject
{
    [ObservableProperty] private string _descricao = string.Empty;
    [ObservableProperty] private string _quantidade = "1";
    [ObservableProperty] private string? _valorUnitario;

    /// <summary>
    /// Total da linha, calculado. Guardar o total digitado daria duas verdades sobre o
    /// mesmo item — e a que o paciente somaria no papel seria a outra.
    /// </summary>
    public string Total
    {
        get
        {
            if (!Valores.TentarLerDecimal(Quantidade, out var qtd) || qtd <= 0) return "—";
            if (!Valores.TentarLerDecimal(ValorUnitario, out var unit) || unit < 0) return "—";
            return (qtd * unit).ToString("C2", new CultureInfo("pt-BR"));
        }
    }

    partial void OnQuantidadeChanged(string value) => OnPropertyChanged(nameof(Total));
    partial void OnValorUnitarioChanged(string? value) => OnPropertyChanged(nameof(Total));
}

/// <summary>
/// Orçamento livre (parcela 24).
///
/// Até aqui o orçamento **só nascia de um pacote vendido** — quem quisesse orçar um plano de
/// tratamento, sessões avulsas ou um combinado que ainda não virou pacote no catálogo não
/// tinha caminho, embora o <see cref="DocumentoFinanceiroService.EmitirAsync"/> já aceitasse
/// linhas quaisquer desde a parcela 4. Era capacidade sem porta: a mais silenciosa das
/// falhas, porque o teste passa e a clínica pega o bloquinho de papel.
///
/// As regras vêm do serviço e não se repetem aqui: valores **gravados na emissão** (mudar a
/// tabela amanhã não reescreve o que o paciente recebeu hoje), numeração por ano e tipo,
/// validade padrão, e cancelamento com motivo em vez de exclusão.
/// </summary>
public sealed partial class OrcamentoViewModel : ObservableObject
{
    private static readonly CultureInfo Brasil = new("pt-BR");

    private readonly IServiceScopeFactory _escopos;
    private readonly int _pacienteId;

    public ObservableCollection<LinhaOrcamento> Itens { get; } = [];

    [ObservableProperty] private string _paciente = string.Empty;

    /// <summary>
    /// Quem recebe o papel. Nasce com o nome do paciente, mas é editável: quem paga nem
    /// sempre é quem é atendido (pai, empresa, plano).
    /// </summary>
    [ObservableProperty] private string? _destinatario;

    [ObservableProperty] private string? _documentoDestinatario;
    [ObservableProperty] private DateTime _data = DateTime.Today;
    [ObservableProperty] private DateTime _validoAte = DateTime.Today.AddDays(30);
    [ObservableProperty] private string? _titulo;
    [ObservableProperty] private string? _observacoes;

    [ObservableProperty] private string _totalGeral = "—";
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _emitindo;

    public int DocumentoEmitidoId { get; private set; }
    public string? NumeroEmitido { get; private set; }

    public event Action? Concluido;

    public OrcamentoViewModel(IServiceScopeFactory escopos, int pacienteId, string nomePaciente)
    {
        _escopos = escopos;
        _pacienteId = pacienteId;
        _paciente = nomePaciente;
        _destinatario = nomePaciente;
        Itens.Add(new LinhaOrcamento());
        Recalcular();
    }

    [RelayCommand]
    private void AdicionarItem()
    {
        Itens.Add(new LinhaOrcamento());
        Recalcular();
    }

    [RelayCommand]
    private void RemoverItem(LinhaOrcamento? item)
    {
        if (item is null) return;
        Itens.Remove(item);
        // Nunca deixa a tela sem nenhuma linha: um formulário vazio não sugere o que fazer.
        if (Itens.Count == 0) Itens.Add(new LinhaOrcamento());
        Recalcular();
    }

    [RelayCommand]
    private void Recalcular()
    {
        var total = 0m;
        var algumValido = false;

        foreach (var i in Itens)
        {
            if (string.IsNullOrWhiteSpace(i.Descricao)) continue;
            if (!Valores.TentarLerDecimal(i.Quantidade, out var qtd) || qtd <= 0) continue;
            if (!Valores.TentarLerDecimal(i.ValorUnitario, out var unit) || unit < 0) continue;

            total += qtd * unit;
            algumValido = true;
        }

        // "—" e não "R$ 0,00": orçamento sem nenhuma linha preenchida não custa zero, ele
        // ainda não existe.
        TotalGeral = algumValido ? total.ToString("C2", Brasil) : "—";
    }

    [RelayCommand]
    private async Task EmitirAsync()
    {
        if (Emitindo) return;

        Mensagem = string.Empty;
        MensagemEhErro = false;

        var itens = new List<ItemDocumentoFinanceiro>();
        var ordem = 1;

        foreach (var i in Itens)
        {
            if (string.IsNullOrWhiteSpace(i.Descricao)) continue;

            if (!Valores.TentarLerDecimal(i.Quantidade, out var qtd) || qtd <= 0)
            {
                Erro($"Quantidade inválida em \"{i.Descricao}\" — informe um número maior que zero.");
                return;
            }
            if (!Valores.TentarLerDecimal(i.ValorUnitario, out var unit) || unit < 0)
            {
                Erro($"Valor inválido em \"{i.Descricao}\" (ex.: 180,00).");
                return;
            }

            itens.Add(new ItemDocumentoFinanceiro
            {
                Ordem = ordem++,
                Descricao = i.Descricao.Trim(),
                Quantidade = qtd,
                ValorUnitario = unit
            });
        }

        if (itens.Count == 0)
        {
            Erro("Um orçamento precisa de ao menos um item: diga o que está sendo orçado.");
            return;
        }

        if (ValidoAte.Date < Data.Date)
        {
            Erro("A validade é anterior à data de emissão — confira as datas.");
            return;
        }

        Emitindo = true;
        try
        {
            // A UNIÃO dos bits das portas (a regra da parcela 69): a central exige
            // `EditarFinanceiro` (quem só LÊ o caixa não propõe preço em nome da
            // clínica — é o que o catálogo de folhas declara) e a tela de Pacotes chega
            // com `VenderPacote`. `VerFinanceiro` aqui era a regra mais fraca das três
            // no único lugar que de fato grava — e um bit de LEITURA emitindo papel
            // com valor.
            SessaoUsuario.Atual.ExigirAlgum(
                Permissao.EditarFinanceiro | Permissao.VenderPacote, "emitir orçamento");

            DocumentoFinanceiro emitido;
            using (var scope = _escopos.CreateScope())
            {
                var servico = scope.ServiceProvider.GetRequiredService<DocumentoFinanceiroService>();

                emitido = await servico.EmitirAsync(new DocumentoFinanceiro
                {
                    Tipo = TipoDocumentoFinanceiro.Orcamento,
                    PacienteId = _pacienteId,
                    Destinatario = Destinatario ?? Paciente,
                    DocumentoDestinatario = DocumentoDestinatario,
                    Data = DateOnly.FromDateTime(Data),
                    ValidoAte = DateOnly.FromDateTime(ValidoAte),
                    Titulo = Titulo,
                    Observacoes = Observacoes,
                    Itens = itens
                }, SessaoUsuario.Atual.Operador);
            }

            DocumentoEmitidoId = emitido.Id;
            NumeroEmitido = emitido.Numero;

            await ImprimirAsync(emitido);
            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — orçamento não pôde ser emitido", ex);
            Erro(ex.Message);
        }
        finally
        {
            Emitindo = false;
        }
    }

    private async Task ImprimirAsync(DocumentoFinanceiro emitido)
    {
        try
        {
            byte[] pdf;
            using (var scope = _escopos.CreateScope())
            {
                var pdfs = scope.ServiceProvider.GetRequiredService<DocumentosFinanceirosPdfService>();
                var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
                pdf = await pdfs.GerarAsync(emitido.Id, await parametros.ObterPrestadorAsync());
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                pdf, ImpressaoPdf.NomeSeguro($"Orcamento-{emitido.Numero.Replace('/', '-')}.pdf"));

            if (erro is null) return;
            Erro(erro);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — orçamento não pôde ser impresso", ex);
            // O orçamento ESTÁ emitido e numerado: dizer só "falhou" faria a pessoa emitir
            // outro e ficar com dois números para a mesma proposta.
            Erro($"O orçamento {emitido.Numero} está emitido, mas o PDF não pôde ser gerado: {ex.Message}");
        }
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}
