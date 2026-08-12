using System.Collections.ObjectModel;
using System.IO;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Financeiro.ViewModels;

/// <summary>Uma linha do extrato na tela, com o que o sistema propõe para ela.</summary>
public sealed partial class LinhaExtratoBanco : ObservableObject
{
    public required string IdBancario { get; init; }
    public required string Data { get; init; }
    public required string Valor { get; init; }
    public required string Descricao { get; init; }
    public required bool Entrada { get; init; }

    /// <summary>"Casada", "Escolha qual", "Só no banco", "Já conferida".</summary>
    public required string Situacao { get; init; }

    /// <summary>Casada ou já conferida — nada a fazer nesta linha.</summary>
    public required bool Resolvida { get; init; }

    /// <summary>Já conciliada antes: dá para DESFAZER, não para conciliar de novo.</summary>
    public required bool JaConciliada { get; init; }

    public ObservableCollection<LancamentoFinanceiro> Candidatos { get; init; } = [];

    /// <summary>
    /// Qual lançamento casar. Vem pré-escolhido quando há UM só — a proposta do sistema —,
    /// e em branco na ambígua, porque escolher por ela seria decidir sem saber.
    /// </summary>
    [ObservableProperty] private LancamentoFinanceiro? _escolhido;

    /// <summary>Há o que confirmar nesta linha.</summary>
    public bool PodeConciliar => !JaConciliada && Candidatos.Count > 0;

    /// <summary>Mostra o seletor só quando há mais de uma opção — senão é ruído.</summary>
    public bool Escolher => !JaConciliada && Candidatos.Count > 1;
}

/// <summary>
/// EXTRATO DO BANCO (parcela 63) — a conciliação bancária por OFX.
///
/// Até aqui o extrato era conferido A OLHO contra a tela de recebíveis: o internet banking
/// de um lado, o sistema do outro. Era o último ponto do financeiro em que fechar o mês
/// dependia de alguém não se distrair.
///
/// ⚠️ Não confundir com a aba "Conciliação" ao lado: aquela casa GUIA com receita
/// (faturamento → financeiro); esta casa o que o sistema diz que entrou com o que o BANCO
/// diz que entrou. São duas conferências diferentes e as duas precisam existir.
///
/// A tela PROPÕE e a pessoa confirma — nada é conciliado sozinho, nem a linha que casa
/// perfeitamente. É a regra do fechamento da sessão e da proposta de preço por convênio:
/// o sistema faz o trabalho, o clique continua sendo de quem responde pelo caixa.
/// </summary>
public sealed partial class ExtratoBancoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;

    /// <summary>O conteúdo do arquivo lido, para recruzar depois de cada confirmação.</summary>
    private string? _conteudo;

    public ObservableCollection<LinhaExtratoBanco> Linhas { get; } = [];

    /// <summary>
    /// O que o sistema tem como recebido/pago e o banco não mostra. É a metade que
    /// ninguém pede e onde mora o erro caro: a venda marcada como recebida que nunca caiu.
    /// </summary>
    public ObservableCollection<LancamentoFinanceiro> SoNoSistema { get; } = [];

    [ObservableProperty] private string _arquivo = string.Empty;
    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Mexer no financeiro é o que conciliar é.</summary>
    public bool PodeConciliar => SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro);

    /// <summary>Já há extrato carregado — antes disso a tela é só o botão de abrir.</summary>
    public bool TemExtrato => Linhas.Count > 0;

    public ExtratoBancoViewModel(IServiceScopeFactory escopos, ISnackbarService snackbar)
    {
        _escopos = escopos;
        _snackbar = snackbar;
    }

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): cada confirmação recruza o
    /// arquivo, e duas cargas no ar se intercalariam dentro das mesmas coleções.
    /// </summary>
    private int _geracaoCarga;

    [RelayCommand]
    private async Task AbrirArquivoAsync()
    {
        var dialogo = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Extrato do banco (OFX)",
            Filter = "Extrato OFX (*.ofx)|*.ofx|Todos os arquivos (*.*)|*.*"
        };

        // Diálogo cancelado sai calado: é o caso normal, e a pessoa sabe que desistiu.
        if (dialogo.ShowDialog() != true) return;

        try
        {
            // Latin-1 e não UTF-8: o OFX 1.x dos bancos brasileiros costuma vir em
            // ISO-8859-1, e lê-lo como UTF-8 estraga os acentos do MEMO — que é
            // justamente o texto pelo qual a pessoa reconhece a transação.
            _conteudo = await File.ReadAllTextAsync(
                dialogo.FileName, System.Text.Encoding.Latin1);

            Arquivo = Path.GetFileName(dialogo.FileName);
            await CruzarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — extrato OFX não pôde ser lido", ex);
            Erro($"Não foi possível ler o arquivo: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CruzarAsync()
    {
        if (_conteudo is null) return;

        var geracao = ++_geracaoCarga;

        try
        {
            Carregando = true;
            NaoVerificado = false;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<ConciliacaoBancariaService>();

            var r = await servico.CruzarAsync(_conteudo);
            if (geracao != _geracaoCarga) return;

            // Monta em listas locais e só ENTÃO publica (a lição da parcela 62).
            var linhas = r.Linhas.Select(Montar).ToList();

            Linhas.Clear();
            foreach (var l in linhas) Linhas.Add(l);

            SoNoSistema.Clear();
            foreach (var l in r.SoNoSistema) SoNoSistema.Add(l);

            Resumo = r.Linhas.Count == 0
                ? "Este arquivo não tem nenhuma transação. Confira se é o extrato OFX que "
                  + "o banco exporta — não o PDF nem o CSV."
                : $"{r.Linhas.Count} linha(s) no extrato · {r.Casadas} já casam · "
                  + $"{r.Pendentes} pedem atenção · {r.SoNoSistema.Count} no sistema sem "
                  + $"correspondência no banco"
                  + (r.Inicio is { } de && r.Fim is { } ate
                      ? $" · período de {de:dd/MM/yyyy} a {ate:dd/MM/yyyy}"
                      : string.Empty);

            OnPropertyChanged(nameof(TemExtrato));
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — extrato não pôde ser cruzado", ex);
            Erro($"Não foi possível cruzar o extrato: {ex.Message}");
        }
        finally
        {
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    private static LinhaExtratoBanco Montar(LinhaConciliada l)
    {
        var linha = new LinhaExtratoBanco
        {
            IdBancario = l.Extrato.Id,
            Data = l.Extrato.Data.ToString("dd/MM/yyyy"),
            Valor = l.Extrato.ValorAbsoluto.ToString("C2"),
            Descricao = l.Extrato.Descricao,
            Entrada = l.Extrato.Entrada,
            JaConciliada = l.Situacao == SituacaoConciliacao.JaConciliada,
            Resolvida = l.Situacao is SituacaoConciliacao.Casada
                                   or SituacaoConciliacao.JaConciliada,
            Situacao = l.Situacao switch
            {
                SituacaoConciliacao.Casada => "Casada — confirme",
                SituacaoConciliacao.Ambigua => $"{l.Candidatos.Count} candidatos — escolha qual",
                SituacaoConciliacao.JaConciliada => "Já conferida",
                _ => "Não há lançamento correspondente no sistema"
            }
        };

        foreach (var c in l.Candidatos) linha.Candidatos.Add(c);

        // A proposta vem pré-escolhida SÓ quando não há dúvida. Na ambígua o campo fica em
        // branco: apontar um dos dois "porque é o primeiro" seria decidir por quem não
        // sabe, e o clique de confirmar viraria automático.
        linha.Escolhido = l.Unico;

        return linha;
    }

    [RelayCommand]
    private async Task ConciliarAsync(LinhaExtratoBanco? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "conciliar com o extrato");

            // Guarda com VOZ (a lição da parcela 41): na linha ambígua, sair calado faria
            // o botão parecer quebrado — a pessoa não adivinha que faltou escolher.
            if (linha.Escolhido is not { } lancamento)
            {
                Erro("Escolha primeiro com qual lançamento esta linha do extrato casa.");
                return;
            }

            using var scope = _escopos.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ConciliacaoBancariaService>()
                .ConciliarAsync(lancamento.Id, linha.IdBancario, SessaoUsuario.Atual.Operador);

            _snackbar.Sucesso($"{linha.Valor} conferido contra o extrato.");
            await CruzarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — linha do extrato não pôde ser conciliada", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>
    /// Desfaz a conciliação de uma linha. Conciliar é humano e errar também: casada a
    /// linha errada, sem isto a única saída seria mexer no banco.
    /// </summary>
    [RelayCommand]
    private async Task DesfazerAsync(LinhaExtratoBanco? linha)
    {
        if (linha?.Candidatos.FirstOrDefault() is not { } lancamento) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarFinanceiro, "desfazer a conciliação");

            using var scope = _escopos.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ConciliacaoBancariaService>()
                .DesfazerAsync(lancamento.Id, SessaoUsuario.Atual.Operador);

            _snackbar.Sucesso("Conciliação desfeita. A linha voltou para a lista.");
            await CruzarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Financeiro — conciliação não pôde ser desfeita", ex);
            Erro(ex.Message);
        }
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}
