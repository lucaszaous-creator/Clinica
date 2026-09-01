using System.Collections.ObjectModel;
using System.IO;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>Uma linha do passo 2: qual coluna do arquivo alimenta este campo da ficha.</summary>
public sealed partial class CampoMapa : ObservableObject
{
    public const string NaoImportar = "(não importar)";

    public required CampoImportacao Campo { get; init; }
    public string Rotulo => CamposImportacao.Rotulo(Campo);
    public string Dica => CamposImportacao.Dica(Campo);
    public bool Obrigatorio => Campo == CampoImportacao.Nome;

    [ObservableProperty] private string _coluna = NaoImportar;
}

/// <summary>Um convênio cadastrado aqui, como opção do combo (ou "não definido").</summary>
public sealed record OpcaoConvenio(string Rotulo, ConvenioCadastro? Cadastro);

/// <summary>Um nome de convênio COMO ESTÁ NO ARQUIVO e para qual convênio daqui ele aponta.</summary>
public sealed partial class ConvenioMapa : ObservableObject
{
    public required string Texto { get; init; }
    public required int Linhas { get; init; }
    public string LinhasTexto => Linhas == 1 ? "1 linha" : $"{Linhas} linhas";

    [ObservableProperty] private OpcaoConvenio? _escolha;
}

/// <summary>
/// Importar pacientes do sistema anterior (set/2026 — a clínica migrou do Smart Clinic).
///
/// Três passos numa tela só, na ordem em que a pessoa decide: o ARQUIVO, o que cada
/// COLUNA significa (com a sugestão já marcada) e a PRÉVIA — que é o passo que importa:
/// nada é gravado antes de a direção ler, linha a linha, o que vai acontecer. A regra
/// que a tela realiza é a do serviço: ficha que já existe é COMPLETADA, nunca
/// sobrescrita; o mesmo arquivo importado duas vezes não duplica ninguém.
///
/// Serviços por ESCOPO (checagem 37): a tela vive o expediente inteiro e o
/// <c>DbContext</c> não.
/// </summary>
public sealed partial class ImportacaoPacientesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly IDialogoService _dialogo;

    private TabelaImportada? _tabela;
    /// <summary>Um rótulo ÚNICO por coluna do arquivo, na ordem dela — duas colunas "Telefone"
    /// no cabeçalho virariam uma só no combo, e a segunda ficaria inalcançável.</summary>
    private string[] _rotulosColunas = [];
    private PreviaImportacao? _previa;
    private IReadOnlyList<ConvenioCadastro> _catalogo = [];

    /// <summary>A metade visível da barreira: os botões apagam para quem não cadastra paciente.</summary>
    public bool PodeImportar => SessaoUsuario.Atual.Pode(Permissao.EditarPaciente);

    // ---- passo 1 ----
    [ObservableProperty] private string? _arquivoNome;
    [ObservableProperty] private string? _arquivoInfo;
    [ObservableProperty] private bool _temArquivo;

    // ---- passo 2 ----
    public ObservableCollection<CampoMapa> Campos { get; } = [];
    public ObservableCollection<string> OpcoesColuna { get; } = [];
    public ObservableCollection<ConvenioMapa> Convenios { get; } = [];
    public ObservableCollection<OpcaoConvenio> OpcoesConvenio { get; } = [];
    [ObservableProperty] private bool _temConvenios;

    // ---- passo 3 ----
    public ObservableCollection<LinhaPrevia> Linhas { get; } = [];
    [ObservableProperty] private bool _temPrevia;
    [ObservableProperty] private string _resumoPrevia = string.Empty;
    [ObservableProperty] private string? _avisosGerais;
    [ObservableProperty] private bool _podeExecutar;
    [ObservableProperty] private string _rotuloImportar = "Importar";

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private string _textoCarregando = "Lendo o arquivo…";
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public ImportacaoPacientesViewModel(IServiceScopeFactory escopos, IDialogoService dialogo)
    {
        _escopos = escopos;
        _dialogo = dialogo;

        foreach (var c in CamposImportacao.Todos)
        {
            var campo = new CampoMapa { Campo = c };
            campo.PropertyChanged += (_, _) =>
            {
                InvalidarPrevia();
                // Trocou a coluna do convênio: a lista de textos a mapear é OUTRA.
                if (campo.Campo == CampoImportacao.Convenio) RemontarConvenios();
            };
            Campos.Add(campo);
        }
    }

    /// <summary>Mexeu no mapeamento: a prévia que estava na tela não descreve mais o que
    /// vai acontecer, e ficar visível seria promessa velha.</summary>
    private void InvalidarPrevia()
    {
        if (!TemPrevia) return;
        _previa = null;
        Linhas.Clear();
        TemPrevia = false;
        PodeExecutar = false;
    }

    // ============================================================ passo 1 — arquivo

    [RelayCommand]
    private async Task EscolherArquivoAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarPaciente, "importar pacientes");

            var caminho = ImpressaoPdf.Escolher(
                "Planilha exportada (*.csv;*.txt)|*.csv;*.txt|Todos os arquivos|*.*",
                "Escolha o arquivo exportado do sistema anterior");
            if (caminho is null) return; // desistiu — silêncio é o certo aqui

            Mensagem = null;
            TextoCarregando = "Lendo o arquivo…";
            Carregando = true;

            var bytes = await File.ReadAllBytesAsync(caminho);
            var tabela = LeitorCsv.Ler(bytes);

            if (_catalogo.Count == 0)
            {
                using var escopo = _escopos.CreateScope();
                _catalogo = (await escopo.ServiceProvider.GetRequiredService<ConvenioCatalogoService>()
                        .ListarAsync())
                    .Where(c => c.Ativo)
                    .OrderBy(c => c.Nome)
                    .ToList();
            }

            _tabela = tabela;
            ArquivoNome = Path.GetFileName(caminho);
            ArquivoInfo = $"{tabela.Linhas.Count} linha(s) · {tabela.Colunas.Count} coluna(s) · "
                          + $"separador: {tabela.SeparadorRotulo} · codificação: {tabela.Codificacao}";
            TemArquivo = true;

            MontarMapeamento(tabela);
            InvalidarPrevia();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — arquivo de importação não pôde ser lido", ex);
            Mensagem = $"Não deu para ler o arquivo: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    private void MontarMapeamento(TabelaImportada tabela)
    {
        _rotulosColunas = RotulosUnicos(tabela.Colunas);

        // Sem await entre o Clear e o último Add (parcela 62).
        OpcoesColuna.Clear();
        OpcoesColuna.Add(CampoMapa.NaoImportar);
        foreach (var r in _rotulosColunas) OpcoesColuna.Add(r);

        var sugestao = SugestorDeMapeamento.Sugerir(tabela.Colunas);
        foreach (var campo in Campos)
            campo.Coluna = sugestao.ColunaDe(campo.Campo) is { } i
                ? _rotulosColunas[i]
                : CampoMapa.NaoImportar;

        OpcoesConvenio.Clear();
        OpcoesConvenio.Add(new OpcaoConvenio("(escolha um convênio)", null));
        foreach (var c in _catalogo) OpcoesConvenio.Add(new OpcaoConvenio(c.Nome, c));

        RemontarConvenios();
    }

    /// <summary>Coluna sem nome vira "(coluna N)" — o Excel exporta cabeçalho vazio no fim —
    /// e nome repetido ganha " (2)", " (3)"…</summary>
    private static string[] RotulosUnicos(IReadOnlyList<string> colunas)
    {
        var vistos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var saida = new string[colunas.Count];
        for (var i = 0; i < colunas.Count; i++)
        {
            var nome = string.IsNullOrWhiteSpace(colunas[i]) ? $"(coluna {i + 1})" : colunas[i];
            var vezes = vistos.TryGetValue(nome, out var n) ? n + 1 : 1;
            vistos[nome] = vezes;
            saida[i] = vezes == 1 ? nome : $"{nome} ({vezes})";
        }
        return saida;
    }

    private MapeamentoImportacao MapeamentoAtual()
    {
        var mapa = new MapeamentoImportacao();
        if (_tabela is null) return mapa;
        foreach (var campo in Campos)
        {
            if (campo.Coluna == CampoMapa.NaoImportar) continue;
            var i = Array.IndexOf(_rotulosColunas, campo.Coluna);
            if (i >= 0) mapa.Definir(campo.Campo, i);
        }
        return mapa;
    }

    /// <summary>Os textos de convênio do arquivo, cada um com a sugestão pelo nome.</summary>
    private void RemontarConvenios()
    {
        if (_tabela is null) return;
        var mapa = MapeamentoAtual();
        var textos = ImportacaoPacientesService.ConveniosDoArquivo(_tabela, mapa);
        if (!mapa.Tem(CampoImportacao.Convenio)) textos = [ImportacaoPacientesService.ConvenioEmBranco];

        var col = mapa.ColunaDe(CampoImportacao.Convenio);
        var contagem = textos.ToDictionary(t => t, t => col is null
            ? _tabela.Linhas.Count
            : _tabela.Linhas.Count(l => string.Equals(
                ImportacaoPacientesService.ChaveConvenio(l[col.Value]), t, StringComparison.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase);

        var anteriores = Convenios.ToDictionary(c => c.Texto, c => c.Escolha, StringComparer.OrdinalIgnoreCase);

        Convenios.Clear();
        foreach (var t in textos)
        {
            var linha = new ConvenioMapa { Texto = t, Linhas = contagem[t] };
            linha.Escolha = anteriores.TryGetValue(t, out var antes) && antes?.Cadastro is not null
                ? antes
                : Sugerir(t);
            linha.PropertyChanged += (_, _) => InvalidarPrevia();
            Convenios.Add(linha);
        }
        TemConvenios = Convenios.Count > 0;
    }

    private OpcaoConvenio Sugerir(string texto)
    {
        var alvo = SugestorDeMapeamento.Normalizar(texto);
        if (alvo.Length == 0) return OpcoesConvenio[0];
        var achado = OpcoesConvenio.Skip(1).FirstOrDefault(o =>
        {
            var n = SugestorDeMapeamento.Normalizar(o.Rotulo);
            return n == alvo || n.Contains(alvo) || alvo.Contains(n);
        });
        return achado ?? OpcoesConvenio[0];
    }

    // ============================================================ passo 3 — prévia

    private int _geracao;

    [RelayCommand]
    private async Task GerarPreviaAsync()
    {
        if (_tabela is null)
        {
            Mensagem = "Escolha o arquivo primeiro (passo 1).";
            MensagemEhErro = true;
            return;
        }
        var geracao = ++_geracao;
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarPaciente, "importar pacientes");
            Mensagem = null;
            TextoCarregando = "Montando a prévia…";
            Carregando = true;

            var mapa = MapeamentoAtual();
            var convenios = Convenios
                .Where(c => c.Escolha?.Cadastro is not null)
                .ToDictionary(c => c.Texto, c => c.Escolha!.Cadastro!, StringComparer.OrdinalIgnoreCase);

            PreviaImportacao previa;
            using (var escopo = _escopos.CreateScope())
            {
                previa = await escopo.ServiceProvider.GetRequiredService<ImportacaoPacientesService>()
                    .PreverAsync(_tabela, mapa, convenios);
            }
            if (geracao != _geracao) return;

            _previa = previa;
            Linhas.Clear();
            // Quem pede ação sobe: problema e aviso primeiro, depois o que entra liso.
            foreach (var l in previa.Linhas
                         .OrderByDescending(l => l.EhProblema)
                         .ThenByDescending(l => l.TemAvisos)
                         .ThenBy(l => l.Numero))
                Linhas.Add(l);

            ResumoPrevia = $"{previa.Criar} ficha(s) nova(s) · {previa.Completar} já cadastrada(s) a completar · "
                           + $"{previa.JaImportadas} já importada(s) · {previa.Problemas} linha(s) que não entram"
                           + (previa.ComAviso > 0 ? $" · {previa.ComAviso} com aviso" : "");
            AvisosGerais = previa.AvisosGerais.Count == 0 ? null : string.Join("\n", previa.AvisosGerais);
            TemPrevia = true;
            PodeExecutar = PodeImportar && previa.TemTrabalho;
            RotuloImportar = previa.TemTrabalho
                ? $"Importar {previa.Criar + previa.Completar} ficha(s)"
                : "Nada a importar";
        }
        catch (Exception ex)
        {
            if (geracao != _geracao) return;
            Clinica.Application.Diagnostico.Registrar("Gerente — prévia da importação falhou", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            if (geracao == _geracao) Carregando = false;
        }
    }

    // ============================================================ importar

    [RelayCommand]
    private async Task ImportarAsync()
    {
        if (_previa is null || !_previa.TemTrabalho)
        {
            Mensagem = "Gere a prévia antes de importar.";
            MensagemEhErro = true;
            return;
        }
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarPaciente, "importar pacientes");

            // Ação em lote diz o tamanho do lote ANTES do clique (parcela 69).
            var confirmou = _dialogo.ConfirmarPerigo("Importar pacientes",
                $"Vão ser criadas {_previa.Criar} ficha(s) nova(s) e completadas {_previa.Completar} já "
                + $"cadastrada(s). As {_previa.Problemas} linha(s) com problema ficam de fora.\n\n"
                + "Não há desfazer em lote: ficha importada por engano se remove uma a uma, pela ficha. "
                + "Continuar?");
            if (!confirmou) return;

            Mensagem = null;
            TextoCarregando = "Gravando as fichas…";
            Carregando = true;

            ResultadoImportacao resultado;
            using (var escopo = _escopos.CreateScope())
            {
                resultado = await escopo.ServiceProvider.GetRequiredService<ImportacaoPacientesService>()
                    .ExecutarAsync(_previa, SessaoUsuario.Atual.Operador);
            }

            var texto = $"Importação concluída: {resultado.Criados} ficha(s) nova(s), "
                        + $"{resultado.Completados} completada(s), {resultado.Pulados} já existente(s).";
            if (resultado.TeveErro)
                texto += $"\n{resultado.Erros.Count} linha(s) não gravaram:\n" + string.Join("\n", resultado.Erros.Take(20))
                         + (resultado.Erros.Count > 20 ? "\n…" : "");
            Mensagem = texto;
            // Meio sucesso não é sucesso (parcela 68): com erro a cor é a de aviso.
            MensagemEhErro = resultado.TeveErro;

            // A prévia já foi consumida — o que sobrou de trabalho se descobre gerando outra.
            InvalidarPrevia();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — importação de pacientes falhou", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }
}
