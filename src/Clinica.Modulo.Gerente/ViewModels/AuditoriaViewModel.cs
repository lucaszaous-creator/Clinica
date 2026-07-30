using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>Um evento da trilha, como aparece na lista.</summary>
public sealed class LinhaAuditoria
{
    public required string Quando { get; init; }
    public required string Acao { get; init; }
    public required string Operador { get; init; }
    public required string Detalhe { get; init; }
    public required string Referencias { get; init; }

    public static LinhaAuditoria De(EventoAuditoria e) => new()
    {
        // Com HORA: a trilha responde "quem fez isso?", e num balcão em que duas pessoas
        // dividem a máquina a hora é o que separa um turno do outro.
        Quando = e.DataHora.ToString("dd/MM/yyyy HH:mm"),
        Acao = e.Acao,
        Operador = string.IsNullOrWhiteSpace(e.Operador) ? "(não informado)" : e.Operador,
        Detalhe = e.Detalhe ?? "—",
        Referencias = string.Join(" · ", new[]
            {
                e.CodigoId is { } c ? $"guia #{c}" : null,
                e.PacienteId is { } p ? $"paciente #{p}" : null,
                e.LoteTissId is { } l ? $"lote #{l}" : null
            }.Where(x => x is not null))
    };
}

/// <summary>Uma linha do resumo (ação ou operador, com a contagem).</summary>
public sealed record ContagemAuditoria(string Rotulo, string ValorRotulo, double Fracao);

/// <summary>
/// A trilha de auditoria (parcela 21).
///
/// `EventoAuditoria` era gravado por praticamente tudo o que mexe em dinheiro ou permissão —
/// baixa de guia, estorno, glosa, lote, lançamento, conta, tributo, preço de convênio,
/// criação de usuário, troca de senha — e **nenhuma tela o lia**. Quarta ocorrência do mesmo
/// defeito no projeto, e a mais grave: as outras três eram dinheiro que ninguém via; esta é
/// a resposta para "quem fez isso?", que é o que se pergunta justamente quando algo deu
/// errado.
///
/// A tela é **somente leitura, e isso é decisão**: um registro de auditoria que pode ser
/// editado ou apagado não é auditoria, é rascunho. Não há botão de excluir aqui, e não deve
/// haver.
///
/// O filtro vai todo para o SQL, com o corte no banco. Esta é a tabela que mais cresce no
/// sistema — toda ação de dinheiro escreve nela.
/// </summary>
public sealed partial class AuditoriaViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    public ObservableCollection<LinhaAuditoria> Eventos { get; } = [];
    public ObservableCollection<ContagemAuditoria> PorAcao { get; } = [];
    public ObservableCollection<ContagemAuditoria> PorOperador { get; } = [];

    /// <summary>Ações já registradas, para o filtro não exigir que se saiba escrevê-las.</summary>
    public ObservableCollection<string> Acoes { get; } = [];

    public IReadOnlyList<int> Limites { get; } = [100, 300, 1000];

    [ObservableProperty] private string? _acao;
    [ObservableProperty] private string? _operador;
    [ObservableProperty] private string? _termo;
    [ObservableProperty] private DateTime? _inicio = DateTime.Today.AddDays(-30);
    [ObservableProperty] private DateTime? _fim = DateTime.Today;
    [ObservableProperty] private int _limite = 300;

    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private bool _noLimite;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public AuditoriaViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        _ = CarregarAsync();
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var auditoria = scope.ServiceProvider.GetRequiredService<AuditoriaService>();

            var filtro = new FiltroAuditoria
            {
                Acao = Acao,
                Operador = Operador,
                Termo = Termo,
                Inicio = Inicio is { } i ? DateOnly.FromDateTime(i) : null,
                Fim = Fim is { } f ? DateOnly.FromDateTime(f) : null,
                Limite = Limite
            };

            if (Acoes.Count == 0)
                foreach (var a in await auditoria.AcoesConhecidasAsync())
                    Acoes.Add(a);

            var eventos = await auditoria.ConsultarAsync(filtro);

            Eventos.Clear();
            foreach (var e in eventos) Eventos.Add(LinhaAuditoria.De(e));

            var r = await auditoria.ResumoAsync(filtro);

            PorAcao.Clear();
            foreach (var (acao, vezes) in r.PorAcao.Take(12))
                PorAcao.Add(new ContagemAuditoria(
                    acao, $"{vezes}x", r.Eventos > 0 ? (double)vezes / r.Eventos : 0d));

            PorOperador.Clear();
            foreach (var (op, vezes) in r.PorOperador.Take(12))
                PorOperador.Add(new ContagemAuditoria(
                    op, $"{vezes}x", r.Eventos > 0 ? (double)vezes / r.Eventos : 0d));

            // Bateu no limite = o resultado está CORTADO, e a tela precisa dizer. Um
            // "300 eventos" que na verdade são 300 dos 4.000 faria a direção concluir que
            // o período teve pouca movimentação.
            NoLimite = eventos.Count >= Limite;
            Resumo = r.Vazio
                ? "Nenhum evento no filtro atual."
                : NoLimite
                    ? $"{r.Eventos} evento(s) — LIMITE ATINGIDO, há mais no período. Estreite o filtro ou aumente o limite."
                    : $"{r.Eventos} evento(s) no filtro atual.";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — trilha de auditoria não pôde ser lida", ex);
            Mensagem = $"Não foi possível ler a trilha: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    [RelayCommand]
    private void Limpar()
    {
        Acao = Operador = Termo = null;
        Inicio = DateTime.Today.AddDays(-30);
        Fim = DateTime.Today;
        Limite = 300;
        _ = CarregarAsync();
    }

    /// <summary>
    /// Exporta a trilha filtrada em CSV. É o que se anexa quando a pergunta sai da clínica —
    /// contestação de convênio, dúvida do contador, conferência de quem mexeu no quê.
    /// </summary>
    [RelayCommand]
    private async Task ExportarAsync()
    {
        try
        {
            var linhas = Eventos
                .Select(e => (IReadOnlyList<string>)new[]
                {
                    e.Quando, e.Acao, e.Operador, e.Detalhe, e.Referencias
                })
                .ToList();

            var csv = ExportacaoCsv.Montar(
                ["Quando", "Acao", "Operador", "Detalhe", "Referencias"], linhas);

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                csv, $"auditoria-{DateTime.Today:yyyy-MM-dd}.csv",
                "CSV (*.csv)|*.csv", ".csv");

            Mensagem = erro;
            MensagemEhErro = erro is not null;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — trilha não pôde ser exportada", ex);
            Mensagem = $"Não foi possível exportar: {ex.Message}";
            MensagemEhErro = true;
        }
    }
}
