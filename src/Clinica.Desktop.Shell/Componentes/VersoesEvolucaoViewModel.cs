using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Uma versão anterior da sessão, como a janela a mostra.</summary>
public sealed class LinhaVersaoEvolucao
{
    public required string Titulo { get; init; }
    public required string Quando { get; init; }
    public required string Motivo { get; init; }
    public required string Queixa { get; init; }

    /// <summary>
    /// A ANAMNESE da versão, numa frase só (parcela 74, 2ª rodada).
    ///
    /// ⚠️ Os quatro campos passaram a ser CONGELADOS na versão anterior nesta mesma parcela —
    /// e congelá-los sem mostrá-los seria o defeito recorrente do projeto na variante mais
    /// cara: aqui o leitor que falta é uma PERÍCIA. A janela existe para responder "o que
    /// este registro dizia antes"; sem esta linha, ela responderia pela metade.
    ///
    /// Vazia quando a versão não tem nenhum dos quatro — o que é o caso de toda versão
    /// gravada ANTES desta parcela, e a ausência é a verdade: o sistema não os guardava.
    /// </summary>
    public required string Anamnese { get; init; }

    public required string Conduta { get; init; }
    public required string Evolucao { get; init; }
    public required string Orientacoes { get; init; }

    /// <summary>
    /// O PLANO da versão (parcela 75). Congelar sem mostrar é o defeito recorrente com uma
    /// PERÍCIA como leitor faltando — a mesma razão da Anamnese logo acima.
    /// </summary>
    public required string Plano { get; init; }
    public required string Eva { get; init; }

    /// <summary>A versão VIGENTE, desenhada em destaque no topo da lista.</summary>
    public required bool Vigente { get; init; }

    public static LinhaVersaoEvolucao De(VersaoEvolucao v) => new()
    {
        Titulo = $"Versão {v.Versao}",
        Quando = $"substituída em {v.SubstituidaEm:dd/MM/yyyy HH:mm}"
                 + (string.IsNullOrWhiteSpace(v.SubstituidaPor)
                     ? string.Empty
                     : $" por {v.SubstituidaPor}"),
        // Motivo é opcional de propósito (ver VersaoEvolucao.Motivo): a frase abaixo é o
        // que impede a ausência de parecer erro de gravação.
        Motivo = string.IsNullOrWhiteSpace(v.Motivo)
            ? "Correção sem motivo informado."
            : v.Motivo,
        Queixa = Texto(v.QueixaPrincipal),
        Anamnese = MontarAnamnese(v.HistoriaDoencaAtual, v.ExameFisico,
                            v.HipoteseDiagnostica, v.CidSessao),
        Conduta = Texto(v.Conduta),
        Evolucao = Texto(v.TextoEvolucao),
        Orientacoes = Texto(v.Orientacoes),
        Plano = Texto(v.PlanoTerapeutico),
        Eva = v.EvaAntes is null && v.EvaDepois is null
            ? "EVA não medida"
            : $"EVA {v.EvaAntes?.ToString() ?? "—"} → {v.EvaDepois?.ToString() ?? "—"}",
        Vigente = false
    };

    /// <summary>A sessão como ela está HOJE — o topo da linha do tempo.</summary>
    public static LinhaVersaoEvolucao Atual(Evolucao e) => new()
    {
        Titulo = "Versão atual",
        Quando = e.AtualizadoEm is { } q
            ? $"corrigida pela última vez em {q:dd/MM/yyyy HH:mm}"
            : $"registrada em {e.CriadoEm:dd/MM/yyyy HH:mm}",
        Motivo = "É o que está valendo no prontuário.",
        Queixa = Texto(e.QueixaPrincipal),
        Anamnese = MontarAnamnese(e.HistoriaDoencaAtual, e.ExameFisico,
                            e.HipoteseDiagnostica, e.CidSessao),
        Conduta = Texto(e.Conduta),
        Evolucao = Texto(e.TextoEvolucao),
        Orientacoes = Texto(e.Orientacoes),
        Plano = Texto(e.PlanoTerapeutico),
        Eva = e.TemParEva ? $"EVA {e.EvaAntes} → {e.EvaDepois}" : "EVA não medida",
        Vigente = true
    };

    /// <summary>
    /// Junta os quatro numa frase, PULANDO o que não existe — a mesma razão pela qual a
    /// linha do crachá é montada no modelo: frase feita de bindings concatenados não sabe
    /// pular, e sobraria "HDA:  · Exame:  ·" com vãos no meio.
    /// </summary>
    private static string MontarAnamnese(string? hda, string? exame, string? hipotese, string? cid)
    {
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(hda)) partes.Add($"HDA: {hda.Trim()}");
        if (!string.IsNullOrWhiteSpace(exame)) partes.Add($"Exame físico: {exame.Trim()}");
        if (!string.IsNullOrWhiteSpace(hipotese))
            partes.Add($"Hipótese: {hipotese.Trim()}"
                       + (string.IsNullOrWhiteSpace(cid) ? string.Empty : $" ({cid.Trim()})"));
        return string.Join("\n", partes);
    }

    private static string Texto(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? "—" : valor;
}

/// <summary>
/// O HISTÓRICO DE CORREÇÕES de uma sessão do prontuário (parcela 52).
///
/// A metade que faltava do ponto 4
/// -------------------------------
/// A Lei 13.787/2018 (art. 3º) exige que a retificação seja RASTREÁVEL. A parcela guardou
/// as versões e as levou para a exportação — e não havia onde LÊ-LAS na tela. Dado gravado
/// sem leitor é o defeito recorrente deste projeto; aqui o leitor que faltava é a própria
/// clínica respondendo <i>"o que estava escrito antes?"</i>, que é a pergunta de uma
/// perícia.
///
/// Mora no SHELL porque a sessão é lida em DOIS módulos — o Prontuário da Recepção e o do
/// Consultório —, e a alternativa seria duas janelas divergindo na primeira correção. É o
/// mesmo argumento do mapa corporal e da emissão de documento.
///
/// A janela é somente leitura, e não há como restaurar uma versão antiga: reverter
/// silenciosamente uma correção é exatamente o gesto que a rastreabilidade existe para
/// impedir. Quem quer voltar atrás corrige de novo, e a correção entra como versão nova.
/// </summary>
public sealed partial class VersoesEvolucaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _evolucaoId;

    public ObservableCollection<LinhaVersaoEvolucao> Versoes { get; } = [];

    [ObservableProperty] private string _titulo = "Correções desta sessão";
    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _semCorrecoes;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public VersoesEvolucaoViewModel(IServiceScopeFactory escopos, int evolucaoId, string sessao)
    {
        _escopos = escopos;
        _evolucaoId = evolucaoId;
        Titulo = $"Correções da sessão de {sessao}";
        _ = CarregarAsync();
    }

    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();

            var atual = await prontuario.ObterAsync(_evolucaoId);
            var anteriores = await prontuario.VersoesAsync(_evolucaoId);

            // O Clear() vem DEPOIS das leituras (parcela 62): entre ele e o último Add
            // não pode haver await, senão uma segunda carga limpa a lista pela metade.
            Versoes.Clear();
            if (atual is not null) Versoes.Add(LinhaVersaoEvolucao.Atual(atual));

            // Da mais NOVA para a mais velha: quem abre esta janela quer saber o que mudou
            // por último, e o serviço devolve em ordem crescente.
            foreach (var v in anteriores.OrderByDescending(v => v.Versao))
                Versoes.Add(LinhaVersaoEvolucao.De(v));

            SemCorrecoes = anteriores.Count == 0;
            Resumo = SemCorrecoes
                ? "Esta sessão nunca foi corrigida — o que está escrito é o registro original."
                : $"{anteriores.Count} correção(ões). Nenhuma versão é apagada: a Lei "
                  + "13.787/2018 exige que a retificação do prontuário seja rastreável.";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Suíte — histórico de correções da sessão", ex);
            Mensagem = $"Não foi possível ler o histórico: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }
}
