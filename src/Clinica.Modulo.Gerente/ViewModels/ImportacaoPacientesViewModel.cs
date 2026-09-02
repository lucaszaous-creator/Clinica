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

    /// <summary>O ZIP do Smart Clinic, quando a pessoa escolheu o pacote em vez de um CSV.
    /// No modo pacote as colunas são as do formato conhecido (fixadas em teste), então o
    /// passo 2 mostra só os convênios.</summary>
    private PacoteSmartClinic? _pacote;
    private PreviaSmartClinic? _previaPacote;
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
    /// <summary>Modo PACOTE (ZIP do Smart Clinic) × modo CSV avulso — os dois booleanos
    /// existem porque o XAML não tem conversor de inverso, e a tela mostra blocos diferentes.</summary>
    [ObservableProperty] private bool _modoPacote;
    [ObservableProperty] private bool _modoCsv;

    // ---- passo 2 ----
    public ObservableCollection<CampoMapa> Campos { get; } = [];
    public ObservableCollection<string> OpcoesColuna { get; } = [];
    public ObservableCollection<ConvenioMapa> Convenios { get; } = [];
    public ObservableCollection<OpcaoConvenio> OpcoesConvenio { get; } = [];
    [ObservableProperty] private bool _temConvenios;

    // ---- passo 3 ----
    public ObservableCollection<LinhaPrevia> Linhas { get; } = [];
    /// <summary>No modo pacote: o que MAIS entra além das fichas (prontuário por arquivo,
    /// agenda futura, histórico, autores) — uma linha por assunto.</summary>
    public ObservableCollection<string> ResumoPacote { get; } = [];
    /// <summary>A CONFERÊNCIA depois de importar o pacote: o ZIP relido contra o banco,
    /// arquivo por arquivo, com o que ficou de fora e o motivo.</summary>
    public ObservableCollection<string> Conferencia { get; } = [];
    [ObservableProperty] private bool _temConferencia;
    [ObservableProperty] private bool _conferenciaFechou;
    [ObservableProperty] private string _conferenciaTitulo = string.Empty;
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
        _previaPacote = null;
        Linhas.Clear();
        ResumoPacote.Clear();
        Conferencia.Clear();
        TemConferencia = false;
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
            _pacote = null;
            ModoPacote = false;
            ModoCsv = true;
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

    /// <summary>
    /// O ZIP inteiro do Smart Clinic: a carteira, o prontuário em texto, a agenda futura e o
    /// que não tem campo. As colunas do pacientes.csv são reconhecidas pelo formato medido
    /// (e fixado em teste); o que a direção decide é o destino de cada convênio.
    /// </summary>
    [RelayCommand]
    private async Task EscolherPacoteAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarPaciente, "importar pacientes");

            var caminho = ImpressaoPdf.Escolher(
                "Pacote do Smart Clinic (*.zip)|*.zip|Todos os arquivos|*.*",
                "Escolha o ZIP que o Smart Clinic entregou");
            if (caminho is null) return;

            Mensagem = null;
            TextoCarregando = "Lendo o pacote…";
            Carregando = true;

            var bytes = await File.ReadAllBytesAsync(caminho);
            var pacote = PacoteSmartClinic.Abrir(bytes);
            var pacientes = pacote.Tabela(PacoteSmartClinic.Pacientes)!;

            if (_catalogo.Count == 0)
            {
                using var escopo = _escopos.CreateScope();
                _catalogo = (await escopo.ServiceProvider.GetRequiredService<ConvenioCatalogoService>()
                        .ListarAsync())
                    .Where(c => c.Ativo)
                    .OrderBy(c => c.Nome)
                    .ToList();
            }

            _pacote = pacote;
            _tabela = pacientes;
            ModoPacote = true;
            ModoCsv = false;
            ArquivoNome = Path.GetFileName(caminho);
            var prontuario = PacoteSmartClinic.ArquivosDeProntuario.Where(pacote.Tem)
                .Sum(a => pacote.Tabela(a)!.Linhas.Count);
            ArquivoInfo = $"{pacote.Arquivos.Count} arquivo(s) · {pacientes.Linhas.Count} paciente(s) · "
                          + $"{prontuario} registro(s) de prontuário · "
                          + $"{pacote.Tabela(PacoteSmartClinic.Agenda)?.Linhas.Count ?? 0} horário(s) na agenda antiga"
                          + (pacote.Ignorados.Count > 0 ? $" · {pacote.Ignorados.Count} arquivo(s) ignorado(s)" : "");
            TemArquivo = true;

            MontarMapeamento(pacientes);
            InvalidarPrevia();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — pacote do Smart Clinic não pôde ser lido", ex);
            Mensagem = $"Não deu para ler o pacote: {ex.Message}";
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
        // A primeira opção é o padrão de quem não sabe: a ficha entra "a definir" e o
        // sistema acusa no próximo agendamento/atendimento — a decisão acontece com o
        // paciente na frente, ficha a ficha, e não 2.021 vezes antes de importar.
        OpcoesConvenio.Add(new OpcaoConvenio(
            "(definir depois — o sistema avisa no próximo atendimento)", ConvenioCadastro.ADefinir()));
        foreach (var c in _catalogo.Where(c => !string.Equals(c.Codigo, ConvenioCadastro.CodigoADefinir, StringComparison.OrdinalIgnoreCase)))
            OpcoesConvenio.Add(new OpcaoConvenio(c.Nome, c));

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
            var convenios = ConveniosEscolhidos();

            PreviaImportacao previa;
            PreviaSmartClinic? previaPacote = null;
            using (var escopo = _escopos.CreateScope())
            {
                if (_pacote is not null)
                {
                    previaPacote = await escopo.ServiceProvider.GetRequiredService<ImportacaoSmartClinicService>()
                        .PreverAsync(_pacote, convenios, DateOnly.FromDateTime(DateTime.Today));
                    previa = previaPacote.Pacientes;
                }
                else
                    previa = await escopo.ServiceProvider.GetRequiredService<ImportacaoPacientesService>()
                        .PreverAsync(_tabela, mapa, convenios);
            }
            if (geracao != _geracao) return;

            _previa = previa;
            _previaPacote = previaPacote;
            ResumoPacote.Clear();
            if (previaPacote is not null)
                foreach (var linha in LinhasDoResumo(previaPacote)) ResumoPacote.Add(linha);
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
            var avisos = previa.AvisosGerais.Concat(previaPacote?.Avisos ?? []).ToList();
            AvisosGerais = avisos.Count == 0 ? null : string.Join("\n", avisos);
            TemPrevia = true;
            var temTrabalho = previaPacote?.TemTrabalho ?? previa.TemTrabalho;
            PodeExecutar = PodeImportar && temTrabalho;
            RotuloImportar = !temTrabalho
                ? "Nada a importar"
                : previaPacote is null
                    ? $"Importar {previa.Criar + previa.Completar} ficha(s)"
                    : $"Importar o pacote ({previa.Criar + previa.Completar} ficha(s) · "
                      + $"{previaPacote.EvolucoesNovas} registro(s) · {previaPacote.Agenda.FuturosNovos} horário(s))";
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
        var temTrabalho = _previaPacote?.TemTrabalho ?? _previa?.TemTrabalho ?? false;
        if (_previa is null || !temTrabalho)
        {
            Mensagem = "Gere a prévia antes de importar.";
            MensagemEhErro = true;
            return;
        }
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarPaciente, "importar pacientes");

            // Ação em lote diz o tamanho do lote ANTES do clique (parcela 69).
            var extra = _previaPacote is null
                ? ""
                : $" Do prontuário antigo entram {_previaPacote.EvolucoesNovas} registro(s), e {_previaPacote.Agenda.FuturosNovos} "
                  + "horário(s) futuro(s) da agenda antiga.";
            var confirmou = _dialogo.ConfirmarPerigo("Importar pacientes",
                $"Vão ser criadas {_previa.Criar} ficha(s) nova(s) e completadas {_previa.Completar} já "
                + $"cadastrada(s). As {_previa.Problemas} linha(s) com problema ficam de fora.{extra}\n\n"
                + "Não há desfazer em lote: ficha importada por engano se remove uma a uma, pela ficha; registro "
                + "clínico importado não se apaga (cancela-se com motivo, como qualquer outro). Continuar?");
            if (!confirmou) return;

            Mensagem = null;
            TextoCarregando = "Gravando as fichas…";
            Carregando = true;

            string texto;
            bool teveErro;
            using (var escopo = _escopos.CreateScope())
            {
                if (_previaPacote is not null)
                {
                    // O progresso chega do serviço; o Progress<T> marshala para a thread da tela.
                    var progresso = new Progress<string>(p => TextoCarregando = p);
                    var r = await escopo.ServiceProvider.GetRequiredService<ImportacaoSmartClinicService>()
                        .ExecutarAsync(_previaPacote, SessaoUsuario.Atual.Operador, progresso);
                    texto = $"Importação concluída: {r.Pacientes.Criados} ficha(s) nova(s), {r.Pacientes.Completados} "
                            + $"completada(s), {r.Pacientes.Pulados} já existente(s) · {r.EvolucoesCriadas} registro(s) de "
                            + $"prontuário ({r.EvolucoesPuladas} já importado(s), {r.EvolucoesSemPaciente} sem ficha) · "
                            + $"{r.AgendamentosCriados} horário(s) futuro(s) ({r.AgendamentosPulados} já importado(s)).";
                    var erros = r.Pacientes.Erros.Concat(r.Erros).ToList();
                    if (erros.Count > 0)
                        texto += $"\n{erros.Count} erro(s):\n" + string.Join("\n", erros.Take(20)) + (erros.Count > 20 ? "\n…" : "");
                    if (r.Revinculados > 0)
                        texto += $"\n{r.Revinculados} registro(s) de rodadas anteriores ganharam o vínculo com a Equipe.";
                    teveErro = r.TeveErro;

                    // A CONFERÊNCIA: relê o mesmo ZIP contra o banco. É a prova de que tudo
                    // entrou — a mensagem acima diz o que se gravou; esta diz o que FALTA.
                    TextoCarregando = "Conferindo o que entrou…";
                    var releitura = await escopo.ServiceProvider.GetRequiredService<ImportacaoSmartClinicService>()
                        .PreverAsync(_pacote!, ConveniosEscolhidos(), DateOnly.FromDateTime(DateTime.Today));
                    var itens = ConferenciaSmartClinic.Montar(releitura);
                    Conferencia.Clear();
                    foreach (var i in itens)
                    {
                        Conferencia.Add((i.Completo ? "✓ " : "✗ ") + i.Resumo);
                        foreach (var m in i.ForaComMotivo) Conferencia.Add("      – " + m);
                    }
                    ConferenciaFechou = ConferenciaSmartClinic.Fechou(itens);
                    ConferenciaTitulo = ConferenciaFechou
                        ? "CONFERÊNCIA: fechou — tudo o que tinha de entrar está no sistema; o que ficou de fora está listado com o motivo."
                        : "CONFERÊNCIA: NÃO fechou — há registro que ainda não entrou. Leia os motivos e importe de novo.";
                    TemConferencia = true;
                }
                else
                {
                    var resultado = await escopo.ServiceProvider.GetRequiredService<ImportacaoPacientesService>()
                        .ExecutarAsync(_previa, SessaoUsuario.Atual.Operador);
                    texto = $"Importação concluída: {resultado.Criados} ficha(s) nova(s), "
                            + $"{resultado.Completados} completada(s), {resultado.Pulados} já existente(s).";
                    if (resultado.TeveErro)
                        texto += $"\n{resultado.Erros.Count} linha(s) não gravaram:\n" + string.Join("\n", resultado.Erros.Take(20))
                                 + (resultado.Erros.Count > 20 ? "\n…" : "");
                    teveErro = resultado.TeveErro;
                }
            }

            Mensagem = texto;
            // Meio sucesso não é sucesso (parcela 68): com erro a cor é a de aviso.
            MensagemEhErro = teveErro;

            // A prévia já foi consumida — o que sobrou de trabalho se descobre gerando outra.
            // A conferência fica na tela: é ela que responde "funcionou?".
            var conferencia = Conferencia.ToList();
            var fechou = ConferenciaFechou;
            var titulo = ConferenciaTitulo;
            InvalidarPrevia();
            if (conferencia.Count > 0)
            {
                foreach (var c in conferencia) Conferencia.Add(c);
                ConferenciaFechou = fechou;
                ConferenciaTitulo = titulo;
                TemConferencia = true;
            }
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

    private Dictionary<string, ConvenioCadastro> ConveniosEscolhidos() => Convenios
        .Where(c => c.Escolha?.Cadastro is not null)
        .ToDictionary(c => c.Texto, c => c.Escolha!.Cadastro!, StringComparer.OrdinalIgnoreCase);

    /// <summary>O resumo do pacote em linhas legíveis — uma por assunto, com os números.</summary>
    private static IEnumerable<string> LinhasDoResumo(PreviaSmartClinic p)
    {
        foreach (var a in p.Prontuario)
            yield return $"{a.Rotulo}: {a.Registros} registro(s) de {a.Pacientes} paciente(s) — {a.Novos} entram"
                         + (a.JaImportados > 0 ? $", {a.JaImportados} já importado(s)" : "")
                         + (a.SemPaciente > 0 ? $", {a.SemPaciente} de paciente que não está no pacote" : "")
                         + (a.Vazios > 0 ? $", {a.Vazios} vazio(s)" : "") + ".";
        yield return $"Agenda antiga: {p.Agenda.Futuros} horário(s) de hoje em diante viram horário aqui ({p.Agenda.FuturosNovos} novo(s)"
                     + (p.Agenda.FuturosJaImportados > 0 ? $", {p.Agenda.FuturosJaImportados} já importado(s)" : "") + "); "
                     + $"{p.Agenda.Passados} visita(s) passada(s) de {p.Agenda.PacientesComHistorico} paciente(s) ficam como histórico nas observações da ficha.";
        if (p.AutoresReconhecidos.Count > 0)
            yield return $"Autores do prontuário reconhecidos na Equipe: {string.Join(", ", p.AutoresReconhecidos)}.";
        if (p.Agenda.ProfissionaisReconhecidos.Count > 0)
            yield return $"Profissionais da agenda reconhecidos na Equipe: {string.Join(", ", p.Agenda.ProfissionaisReconhecidos)}.";
        yield return "Dados sem campo aqui (e-mail, RG, profissão, nome dos pais…) vão para as observações de cada ficha, com rótulo. "
                     + "Login e senha do sistema antigo não entram.";
    }
}
