using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>
/// Onde a clínica ESCREVE o termo que o paciente vai assinar, e diz qual procedimento o
/// exige (parcela 66).
///
/// ⚠️ O TEXTO é da clínica, não nosso. O sistema guarda, versiona por cópia e imprime — o
/// conteúdo de um termo de consentimento é responsabilidade técnica de quem faz o
/// procedimento, e um texto padrão embutido no código seria o sistema opinando sobre risco
/// clínico. Por isso não há termo de fábrica: a lista nasce vazia e a tela diz o que fazer.
///
/// Fica atrás de um BOTÃO em Configurações, e não como mais um cartão da tela: escrever o
/// termo do BSV acontece uma vez por procedimento novo, e painel aberto para ato raro é a
/// faixa lateral que o README proíbe.
/// </summary>
public sealed partial class ModelosTermoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    /// <summary>
    /// Descarte de resposta fora de ordem (a regra da parcela 60). Aqui ela vale porque
    /// salvar dispara recarga e a pessoa pode clicar duas vezes: a leitura velha chegando
    /// por último devolveria a lista de antes do que ela acabou de gravar.
    /// </summary>
    private int _geracaoCarga;

    public ModelosTermoViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;

        // As modalidades vêm do catálogo, não do enum cru: "BsvComAcupuntura" na tela é o
        // defeito da parcela 41, e o catálogo já resolve o nome das variantes cadastradas.
        Modalidades = Enum.GetValues<ModalidadeAtendimento>()
            .Select(m => new OpcaoModalidade(m, CatalogoModalidades.Nome(m.ToString())))
            .ToList();

        _ = CarregarAsync();
    }

    public ObservableCollection<ModeloTermoLinha> Modelos { get; } = [];

    public ObservableCollection<ExigenciaLinha> Exigencias { get; } = [];

    public IReadOnlyList<OpcaoModalidade> Modalidades { get; }

    [ObservableProperty] private bool _carregando;

    [ObservableProperty] private bool _naoVerificado;

    [ObservableProperty] private string _mensagem = string.Empty;

    [ObservableProperty] private bool _mensagemEhErro;

    // ---- Edição do modelo em foco ----

    [ObservableProperty] private ModeloTermoLinha? _selecionado;

    [ObservableProperty] private string _nome = string.Empty;

    [ObservableProperty] private string _tituloImpresso = string.Empty;

    [ObservableProperty] private string _corpo = string.Empty;

    public ObservableCollection<DeclaracaoModelo> Declaracoes { get; } = [];

    [ObservableProperty] private string _novaDeclaracao = string.Empty;

    // ---- Exigência nova ----

    [ObservableProperty] private OpcaoModalidade? _modalidadeEscolhida;

    public bool PodeConfigurar => SessaoUsuario.Atual.Pode(Permissao.ConfigurarFaturamento);

    partial void OnSelecionadoChanged(ModeloTermoLinha? value)
    {
        Nome = value?.Nome ?? string.Empty;
        TituloImpresso = value?.Titulo ?? string.Empty;
        Corpo = value?.Corpo ?? string.Empty;

        Declaracoes.Clear();
        if (value is null) return;
        foreach (var d in value.Declaracoes)
            Declaracoes.Add(new DeclaracaoModelo { Texto = d.Texto, Detalhe = d.Detalhe });
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        try
        {
            Carregando = true;
            NaoVerificado = false;

            using var scope = _escopos.CreateScope();
            var documentos = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();
            var termos = scope.ServiceProvider.GetRequiredService<TermoProcedimentoService>();

            var modelos = await documentos.ModelosAsync(TipoDocumentoClinico.TermoProcedimento);
            var exigencias = await termos.ExigenciasAsync();

            if (geracao != _geracaoCarga) return;

            // Monta em lista local e só então publica: entre o Clear() e o último Add não
            // pode haver await, senão duas cargas se intercalam na mesma coleção (a metade
            // que a parcela 60 não escreveu e a 62 achou).
            var linhas = modelos.Select(m => new ModeloTermoLinha(
                m.Id, m.Nome, m.Titulo, m.Corpo,
                m.Itens.OrderBy(i => i.Ordem)
                    .Select(i => new DeclaracaoModelo { Texto = i.Descricao, Detalhe = i.Detalhe })
                    .ToList())).ToList();

            var linhasExigencia = exigencias.Select(e => new ExigenciaLinha(
                e.Id,
                CatalogoModalidades.Nome(e.ModalidadeCodigo ?? e.Modalidade.ToString()),
                e.Modelo?.Nome ?? "(modelo removido)",
                e.Ativa)).ToList();

            // ⚠️ O id em foco é guardado ANTES do Clear.
            //
            // `Modelos.Clear()` faz a ListBox devolver NULL pelo binding de
            // `SelectedItem` — o mesmo que a prévia do Novo atendimento sofreu na parcela
            // 50 —, então `Selecionado` já é nulo quando a linha de baixo o consulta. Sem
            // isto, salvar um termo devolvia o foco para o PRIMEIRO da lista, e a gravação
            // seguinte ia para o modelo errado, em silêncio.
            var emFoco = Selecionado?.Id;

            Modelos.Clear();
            foreach (var l in linhas) Modelos.Add(l);

            Exigencias.Clear();
            foreach (var l in linhasExigencia) Exigencias.Add(l);

            Selecionado = Modelos.FirstOrDefault(m => m.Id == emFoco)
                          ?? Modelos.FirstOrDefault();
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;
            NaoVerificado = true;
            MensagemEhErro = true;
            Mensagem = "Não foi possível carregar os termos: " + ex.Message;
            Clinica.Application.Diagnostico.Registrar("Gerente — modelos de termo", ex);
        }
        finally
        {
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    [RelayCommand]
    private void Novo()
    {
        Selecionado = null;
        Nome = string.Empty;
        TituloImpresso = string.Empty;
        Corpo = string.Empty;
        Declaracoes.Clear();
        Mensagem = string.Empty;
    }

    [RelayCommand]
    private void AcrescentarDeclaracao()
    {
        // Guarda sobre VARIÁVEL de tela em que sair calado seria errado: o botão fica
        // apagado com o campo vazio, e quem usar o Enter precisa saber por que não entrou.
        if (string.IsNullOrWhiteSpace(NovaDeclaracao))
        {
            MensagemEhErro = true;
            Mensagem = "Escreva a declaração antes de acrescentá-la.";
            return;
        }

        Declaracoes.Add(new DeclaracaoModelo { Texto = NovaDeclaracao.Trim() });
        NovaDeclaracao = string.Empty;
        Mensagem = string.Empty;
    }

    /// <summary>
    /// Tira uma declaração do MODELO. Não toca em termo nenhum já assinado: aplicar um
    /// modelo COPIA, e é essa cópia que faz corrigir o roteiro hoje não reescrever o que o
    /// paciente assinou no mês passado.
    /// </summary>
    [RelayCommand]
    private void RemoverDeclaracao(DeclaracaoModelo? item)
    {
        if (item is null) return;
        Declaracoes.Remove(item);
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        if (!SessaoUsuario.Atual.Pode(Permissao.ConfigurarFaturamento))
        {
            MensagemEhErro = true;
            Mensagem = "Você não tem permissão para configurar os termos.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Nome))
        {
            MensagemEhErro = true;
            Mensagem = "Dê um nome ao termo (ex.: \"Termo do BSV\").";
            return;
        }

        if (string.IsNullOrWhiteSpace(Corpo))
        {
            MensagemEhErro = true;
            Mensagem = "Escreva o texto do termo — é o que o paciente vai ler e assinar.";
            return;
        }

        try
        {
            Carregando = true;

            using var scope = _escopos.CreateScope();
            var documentos = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();

            var ordem = 1;
            await documentos.SalvarModeloAsync(new ModeloDocumento
            {
                Id = Selecionado?.Id ?? 0,
                Tipo = TipoDocumentoClinico.TermoProcedimento,
                Nome = Nome.Trim(),
                Titulo = string.IsNullOrWhiteSpace(TituloImpresso) ? null : TituloImpresso.Trim(),
                Corpo = Corpo.Trim(),
                Ativo = true,
                Itens = Declaracoes
                    .Where(d => !string.IsNullOrWhiteSpace(d.Texto))
                    .Select(d => new ItemModelo
                    {
                        Ordem = ordem++,
                        Descricao = d.Texto.Trim(),
                        Detalhe = string.IsNullOrWhiteSpace(d.Detalhe) ? null : d.Detalhe!.Trim()
                    }).ToList()
            }, SessaoUsuario.Atual.Operador);

            MensagemEhErro = false;
            Mensagem = $"Termo \"{Nome.Trim()}\" salvo.";
            _snackbar.Sucesso("Termo salvo.");

            await CarregarAsync();
        }
        catch (Exception ex)
        {
            MensagemEhErro = true;
            Mensagem = ex.Message;
            Clinica.Application.Diagnostico.Registrar("Gerente — salvar modelo de termo", ex);
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>Amarra a modalidade escolhida ao termo em foco.</summary>
    [RelayCommand]
    private async Task ExigirAsync()
    {
        if (!SessaoUsuario.Atual.Pode(Permissao.ConfigurarFaturamento))
        {
            MensagemEhErro = true;
            Mensagem = "Você não tem permissão para configurar os termos.";
            return;
        }

        if (Selecionado is null)
        {
            MensagemEhErro = true;
            Mensagem = "Escolha (ou salve) o termo antes de amarrá-lo a um procedimento.";
            return;
        }

        if (ModalidadeEscolhida is null)
        {
            MensagemEhErro = true;
            Mensagem = "Escolha o procedimento que passa a exigir este termo.";
            return;
        }

        try
        {
            Carregando = true;

            using var scope = _escopos.CreateScope();
            var termos = scope.ServiceProvider.GetRequiredService<TermoProcedimentoService>();

            await termos.ExigirAsync(
                ModalidadeEscolhida.Modalidade, Selecionado.Id,
                operador: SessaoUsuario.Atual.Operador);

            MensagemEhErro = false;
            Mensagem = $"{ModalidadeEscolhida.Nome} passa a exigir \"{Selecionado.Nome}\".";
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            MensagemEhErro = true;
            Mensagem = ex.Message;
            Clinica.Application.Diagnostico.Registrar("Gerente — exigir termo", ex);
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>
    /// Liga ou desliga a exigência. Desligar em vez de apagar: a clínica que suspende a
    /// cobrança por um mês não perde a amarração nem o texto.
    /// </summary>
    [RelayCommand]
    private async Task AlternarExigenciaAsync(ExigenciaLinha? linha)
    {
        if (linha is null) return;

        if (!SessaoUsuario.Atual.Pode(Permissao.ConfigurarFaturamento))
        {
            MensagemEhErro = true;
            Mensagem = "Você não tem permissão para configurar os termos.";
            return;
        }

        if (linha.Ativa && !_dialogo.Confirmar(
                "Desligar a exigência",
                $"O balcão deixa de cobrar o termo \"{linha.Termo}\" em {linha.Modalidade}. "
                + "Os termos já assinados continuam guardados. Confirma?"))
            return;

        try
        {
            using var scope = _escopos.CreateScope();
            var termos = scope.ServiceProvider.GetRequiredService<TermoProcedimentoService>();

            await termos.AlternarAsync(linha.Id, !linha.Ativa, SessaoUsuario.Atual.Operador);
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            MensagemEhErro = true;
            Mensagem = ex.Message;
            Clinica.Application.Diagnostico.Registrar("Gerente — alternar exigência", ex);
        }
    }
}

/// <summary>Um modelo de termo na lista.</summary>
public sealed record ModeloTermoLinha(
    int Id, string Nome, string? Titulo, string? Corpo,
    IReadOnlyList<DeclaracaoModelo> Declaracoes)
{
    public string Resumo => Declaracoes.Count switch
    {
        0 => "sem declarações",
        1 => "1 declaração",
        var n => $"{n} declarações"
    };
}

/// <summary>Uma declaração sendo escrita no modelo.</summary>
public sealed partial class DeclaracaoModelo : ObservableObject
{
    [ObservableProperty] private string _texto = string.Empty;

    [ObservableProperty] private string? _detalhe;
}

/// <summary>Uma amarração procedimento → termo.</summary>
public sealed record ExigenciaLinha(int Id, string Modalidade, string Termo, bool Ativa)
{
    public string Situacao => Ativa ? "Exigido" : "Desligado";

    public string AcaoRotulo => Ativa ? "Desligar" : "Ligar";
}

/// <summary>Uma modalidade no seletor, já com o nome do CATÁLOGO.</summary>
public sealed record OpcaoModalidade(ModalidadeAtendimento Modalidade, string Nome);
