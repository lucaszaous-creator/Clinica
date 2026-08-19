using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>Uma origem na tabela, já formatada para a tela.</summary>
public sealed class LinhaOrigemTela
{
    public required string Rotulo { get; init; }
    public required int NaBase { get; init; }
    public required int Estreias { get; init; }

    /// <summary>Fração da base. Nula sem base — 0% de zero pacientes seria alvo batido falso.</summary>
    public required string Fracao { get; init; }

    /// <summary>
    /// A linha "não perguntado" vem MARCADA: quando ela encabeça a tabela, o achado do
    /// relatório é o balcão ter parado de perguntar — e a direção precisa ler isso como
    /// tarefa (cobrar a pergunta no cadastro), não como categoria de marketing.
    /// </summary>
    public required bool EhSemResposta { get; init; }
}

/// <summary>Quem indicou, na lista do ranking.</summary>
public sealed class LinhaIndicador
{
    public required string Nome { get; init; }
    public required string Quantos { get; init; }
}

/// <summary>
/// De onde vêm os pacientes (parcela 69).
///
/// O cadastro pergunta a origem a todo paciente novo desde que o campo existe, e a
/// resposta era lida numa ficha por vez. Esta tela é o leitor agregado que faltava — a
/// pergunta da direção é "vale manter o anúncio?", e ela se responde com a base inteira,
/// não com um paciente aberto.
///
/// Quem calcula é o <see cref="OrigemPacientesService"/>, dono da leitura; a tela só
/// formata. "Estreia" = primeiro atendimento no período — a definição está escrita no
/// subtítulo, porque um número cuja definição só existe no código é um número que cada
/// leitor interpreta de um jeito.
/// </summary>
public sealed partial class OrigensViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;

    public ObservableCollection<LinhaOrigemTela> Linhas { get; } = [];
    public ObservableCollection<LinhaIndicador> Indicadores { get; } = [];

    public IReadOnlyList<string> Periodos { get; } = PeriodoGerencial.Opcoes;

    /// <summary>
    /// "Este ano" por padrão, e não "Este mês" como nas telas de produção: a pergunta
    /// desta tela ("vale manter o anúncio?") se decide olhando meses, e um mês de estreias
    /// tem meia dúzia de linhas — ruído com cara de tendência.
    /// </summary>
    [ObservableProperty] private string _periodo = PeriodoGerencial.EsteAno;

    [ObservableProperty] private string _resumo = "—";
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;

    public OrigensViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        _ = CarregarAsync();
    }

    partial void OnPeriodoChanged(string value) => _ = CarregarAsync();

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): trocar o período duas vezes rápido
    /// num banco remoto deixaria os números de um intervalo sob o rótulo do outro.
    /// </summary>
    private int _geracaoCarga;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        try
        {
            Carregando = true;
            NaoVerificado = false;

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var (inicio, fim) = PeriodoGerencial.Intervalo(Periodo, hoje);

            using var escopo = _escopos.CreateScope();
            var resumo = await escopo.ServiceProvider
                .GetRequiredService<OrigemPacientesService>().ResumoAsync(inicio, fim);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            // Monta e só ENTÃO publica: entre o Clear e o último Add não pode haver await
            // (parcela 62) — aqui não há await no meio, e a regra fica anotada para a
            // próxima pessoa que for acrescentar uma leitura.
            Linhas.Clear();
            foreach (var l in resumo.Linhas)
                Linhas.Add(new LinhaOrigemTela
                {
                    Rotulo = l.Rotulo,
                    NaBase = l.TotalNaBase,
                    Estreias = l.EstreiasNoPeriodo,
                    Fracao = resumo.TotalPacientes > 0
                        ? $"{(double)l.TotalNaBase / resumo.TotalPacientes:P0}"
                        : "—",
                    EhSemResposta = l.Origem is null
                });

            Indicadores.Clear();
            foreach (var i in resumo.QuemMaisIndica)
                Indicadores.Add(new LinhaIndicador
                {
                    Nome = i.Nome,
                    Quantos = i.Indicados == 1 ? "1 paciente" : $"{i.Indicados} pacientes"
                });

            Resumo = resumo.TotalPacientes == 0
                ? "Nenhum paciente cadastrado ainda."
                : $"{resumo.TotalPacientes} paciente(s) na base · {resumo.SemResposta} sem a pergunta respondida"
                  + $" · estreias de {resumo.Inicio:dd/MM/yyyy} a {resumo.Fim:dd/MM/yyyy}";
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar(
                "Gerente — origem dos pacientes não pôde ser lida", ex);
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }
}
