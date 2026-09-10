using System.Collections.ObjectModel;
using System.Globalization;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>Uma linha da tabela do período, já escrita para a tela.</summary>
public sealed class LinhaDocumentoEmitido
{
    public required string Numero { get; init; }
    public required string Folha { get; init; }
    public required string Para { get; init; }

    /// <summary>Quem assinou, ou "balcão" quando o papel não tem assinatura.</summary>
    public required string QuemAssinou { get; init; }

    public required string Data { get; init; }
    public required bool Assinado { get; init; }
    public required bool Cancelado { get; init; }
}

/// <summary>Uma cancelada, com o motivo que a lei manda existir.</summary>
public sealed class LinhaCancelada
{
    public required string Numero { get; init; }
    public required string Folha { get; init; }
    public required string Motivo { get; init; }
    public required string Contexto { get; init; }
}

/// <summary>
/// DOCUMENTOS EMITIDOS — a tela da direção (set/2026, tela 4 do mockup aprovado
/// "documentos nas quatro telas").
///
/// Por que ela existe
/// ------------------
/// A direção não emite receita. Até aqui a única tela de documentos da suíte era a do
/// BALCÃO — cartões de emitir, seletor de paciente, régua de folhas —, e quem só queria
/// saber quantos papéis saíram, quem assinou e quantos foram cancelados atravessava uma
/// tela inteira de operação para chegar a uma lista.
///
/// É a mesma divisão do resto do sistema: <b>o balcão opera, a direção lê o agregado</b>.
/// Por isso aqui não há um único botão de emitir — nem de cancelar, nem de segunda via.
/// Quem é gerente E atende emite pelo Consultório, e o subtítulo diz isso.
///
/// ⚠️ Ela não CALCULA nada: os números saem de <see cref="PainelDeDocumentos"/>, na
/// Application, e as folhas do <see cref="CentralDocumentosService"/> — o serviço dono da
/// leitura. Recontar aqui daria dois números para a mesma pergunta, e o segundo seria o
/// que ninguém lembraria de corrigir.
/// </summary>
public sealed partial class DocumentosEmitidosViewModel : ObservableObject
{
    private static readonly CultureInfo Brasil = new("pt-BR");

    private readonly IServiceScopeFactory _escopos;

    public ObservableCollection<LinhaDocumentoEmitido> Linhas { get; } = [];
    public ObservableCollection<QuemAssina> PorQuemAssina { get; } = [];
    public ObservableCollection<LinhaCancelada> Canceladas { get; } = [];

    public IReadOnlyList<string> Periodos { get; } = PeriodoGerencial.Opcoes;

    [ObservableProperty] private string _periodo = PeriodoGerencial.EsteMes;

    [ObservableProperty] private string _resumo = "—";
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;

    // ---- Os quatro números, já escritos ----
    [ObservableProperty] private string _emitidos = "—";
    [ObservableProperty] private string _assinados = "—";
    [ObservableProperty] private string _cancelados = "—";
    [ObservableProperty] private string _noAr = "—";
    [ObservableProperty] private string _maisEmitido = "—";

    /// <summary>Houve cancelamento no período — a caixa deles some quando não houve.</summary>
    public bool TemCanceladas => Canceladas.Count > 0;

    /// <summary>O título da caixa de cancelados diz QUANTOS, e ele muda com o período.</summary>
    [ObservableProperty] private string _tituloCanceladas = "CANCELADOS NO PERÍODO";

    // ---- Conferir pelo código ----
    [ObservableProperty] private string? _codigo;

    /// <summary>O que o código achou, já escrito. Nulo = ninguém conferiu nada ainda.</summary>
    [ObservableProperty] private string? _conferido;

    /// <summary>Documento cancelado ou código que não existe — os dois pedem destaque.</summary>
    [ObservableProperty] private bool _conferidoCancelado;

    /// <summary>
    /// Os acessos de quem está logado. A lista passa pelo MESMO filtro do balcão: o
    /// relatório de evolução carrega dado de saúde, e a tela da direção não é exceção à
    /// permissão granular da parcela 49 só por ser agregada — a coluna "PARA" nomeia o
    /// paciente de cada papel.
    /// </summary>
    private static Permissao Acessos => SessaoUsuario.Atual.Efetivas;

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): trocar o período duas vezes rápido
    /// num banco remoto deixaria os números de um intervalo sob o rótulo do outro.
    /// </summary>
    private int _geracaoCarga;

    public DocumentosEmitidosViewModel(IServiceScopeFactory escopos)
    {
        _escopos = escopos;
        _ = CarregarAsync();
    }

    partial void OnPeriodoChanged(string value) => _ = CarregarAsync();

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
            var central = escopo.ServiceProvider.GetRequiredService<CentralDocumentosService>();

            var folhas = await central.EmitidasAsync(inicio, fim, acessos: Acessos);

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            var painel = PainelDeDocumentos.Montar(folhas, hoje);

            Emitidos = painel.Emitidos.ToString(Brasil);
            Assinados = painel.Assinados.ToString(Brasil);
            Cancelados = painel.Cancelados.ToString(Brasil);
            NoAr = painel.NoAr.ToString(Brasil);

            // Vazio vira travessão: "— 0%" seria um número onde não há pergunta a
            // responder.
            MaisEmitido = string.IsNullOrEmpty(painel.MaisEmitido) ? "—" : painel.MaisEmitido;

            // Entre o Clear() e o último Add não pode haver await (parcela 62). Aqui as
            // três listas saem de dados já em memória.
            Linhas.Clear();
            foreach (var f in folhas) Linhas.Add(Montar(f));

            PorQuemAssina.Clear();
            foreach (var q in painel.PorQuemAssina) PorQuemAssina.Add(q);

            Canceladas.Clear();
            foreach (var c in painel.Canceladas)
                Canceladas.Add(new LinhaCancelada
                {
                    Numero = c.Numero,
                    Folha = c.FolhaRotulo,
                    Motivo = string.IsNullOrWhiteSpace(c.MotivoCancelamento)
                        ? "(sem motivo registrado)"
                        : c.MotivoCancelamento!,
                    Contexto = string.Join(" · ", new[]
                    {
                        c.Profissional,
                        c.Data.ToString("dd/MM/yyyy", Brasil)
                    }.Where(x => !string.IsNullOrWhiteSpace(x)))
                });

            OnPropertyChanged(nameof(TemCanceladas));
            TituloCanceladas = $"CANCELADOS NO PERÍODO · {painel.Cancelados}";

            Resumo = painel.Vazio
                ? $"Nenhum papel emitido de {inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}."
                : $"{painel.Emitidos} folha(s) de {inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}.";
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar(
                "Gerente — documentos emitidos não puderam ser lidos", ex);
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    private static LinhaDocumentoEmitido Montar(FolhaEmitida f) => new()
    {
        Numero = f.Numero,
        Folha = f.FolhaRotulo,
        Para = f.Paciente ?? "—",
        QuemAssinou = string.IsNullOrWhiteSpace(f.Profissional) ? "balcão" : f.Profissional!,
        Data = f.Data.ToString("dd/MM", Brasil),
        Assinado = f.Assinado,
        // Cancelada aparece MARCADA, nunca sumindo: documento não se apaga neste sistema,
        // e é justamente a cancelada que a direção veio olhar.
        Cancelado = f.Cancelado
    };

    /// <summary>
    /// Confere o papel pelo código impresso nele — o ato de quem RECEBE um documento de
    /// fora (a empresa que ligou, a escola, o convênio).
    ///
    /// É a mesma conferência do balcão, e ela está aqui pela mesma razão que a lista: a
    /// direção também recebe papel na mão. Registra a leitura na trilha, como toda porta
    /// que mostra de quem é o documento.
    /// </summary>
    [RelayCommand]
    private async Task ConferirAsync()
    {
        Conferido = null;
        ConferidoCancelado = false;

        var codigo = Codigo?.Trim();
        if (string.IsNullOrWhiteSpace(codigo))
        {
            Conferido = "Digite o código impresso no rodapé do documento.";
            ConferidoCancelado = true;
            return;
        }

        try
        {
            using var escopo = _escopos.CreateScope();
            var documentos = escopo.ServiceProvider.GetRequiredService<DocumentoClinicoService>();

            var achado = await documentos.PorCodigoAsync(codigo);

            if (achado is null)
            {
                // "Não confere" é RESPOSTA, não erro de sistema — e é a resposta que a
                // conferência existe para dar.
                Conferido = $"Nenhum documento com o código \"{codigo}\". Confira a "
                            + "digitação; se estiver certa, o papel não saiu deste sistema.";
                ConferidoCancelado = true;
                return;
            }

            // Conferir MOSTRA de quem é o documento e de que tipo ele é — acesso a dado de
            // saúde por uma porta própria, e por isso na trilha (ponto 4 do compromisso).
            await escopo.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                .RegistrarAsync(achado.PacienteId, SessaoUsuario.Atual.Operador,
                    OrigemAcessoProntuario.Documento);

            ConferidoCancelado = achado.Cancelado;

            var quem = achado.Paciente?.Nome ?? "(paciente removido)";
            var situacao = achado.Cancelado
                ? $"CANCELADO em {achado.CanceladoEm:dd/MM/yyyy} — {achado.MotivoCancelamento}"
                : "válido";

            Conferido = $"{CentralDocumentosService.RotularClinico(achado.Tipo)} {achado.Numero} · "
                        + $"{quem} · emitido em {achado.Data.ToString("dd/MM/yyyy", Brasil)} · {situacao}";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Gerente — documento não pôde ser conferido pelo código", ex);
            Conferido = $"Não foi possível conferir o código: {ex.Message}";
            ConferidoCancelado = true;
        }
    }
}
