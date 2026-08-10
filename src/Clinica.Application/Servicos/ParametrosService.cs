using System.Text.Json;
using System.Text.Json.Serialization;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>Nome e código de um convênio disponível (para listas/combos de cadastro).</summary>
public sealed record ConvenioOpcao(Convenio Convenio, string Nome);

/// <summary>Valores efetivos dos parâmetros (defaults do código sobrepostos pela configuração salva).</summary>
public sealed class ParametrosSnapshot
{
    private readonly IReadOnlyDictionary<Convenio, ParametroConvenio> _map;
    public ParametrosSnapshot(IReadOnlyDictionary<Convenio, ParametroConvenio> map) => _map = map;

    public int? ValidadeConsultaDias(Convenio c)
        => _map.TryGetValue(c, out var p) ? p.ValidadeConsultaDias : ConvenioInfo.ValidadeConsultaDias(c);

    public int DiasSegundoCodigo(Convenio c)
        => _map.TryGetValue(c, out var p) ? p.DiasSegundoCodigo : 1;

    /// <summary>Nome exibido do convênio (custom salvo, ou o padrão do código).</summary>
    public string NomeExibicao(Convenio c)
        => _map.TryGetValue(c, out var p) && !string.IsNullOrWhiteSpace(p.Nome)
            ? p.Nome!
            : ConvenioInfo.NomeExibicao(c);

    /// <summary>Categoria-base do paciente (custom salva por app, ou o padrão do código).</summary>
    public Categoria CategoriaBase(Convenio c, bool possuiApp)
    {
        if (_map.TryGetValue(c, out var p))
        {
            var custom = possuiApp ? p.CategoriaComApp : p.CategoriaSemApp;
            if (custom is { } cat) return cat;
        }
        return CategoriaConvenio.Base(c, possuiApp);
    }

    public bool Ativo(Convenio c) => !_map.TryGetValue(c, out var p) || p.Ativo;

    /// <summary>Convênios ativos, com o nome exibido, em ordem alfabética (para combos de cadastro).</summary>
    public IReadOnlyList<ConvenioOpcao> ConveniosAtivos =>
        Enum.GetValues<Convenio>()
            .Where(Ativo)
            .Select(c => new ConvenioOpcao(c, NomeExibicao(c)))
            .OrderBy(o => o.Nome)
            .ToList();

    public IReadOnlyCollection<ParametroConvenio> Todos => (IReadOnlyCollection<ParametroConvenio>)_map.Values;
}

/// <summary>Lê e grava os parâmetros editáveis dos convênios.</summary>
public sealed class ParametrosService
{
    private readonly IClinicaRepositorio _repo;

    public ParametrosService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>Snapshot com os valores efetivos: defaults do código + overrides salvos.</summary>
    public async Task<ParametrosSnapshot> ObterAsync(CancellationToken ct = default)
    {
        var salvos = (await _repo.ParametrosAsync(ct)).ToDictionary(p => p.Convenio);

        var map = new Dictionary<Convenio, ParametroConvenio>();
        foreach (var c in Enum.GetValues<Convenio>())
        {
            map[c] = salvos.TryGetValue(c, out var s)
                ? PreencherDefaults(s)
                : new ParametroConvenio
                {
                    Convenio = c,
                    Nome = ConvenioInfo.NomeExibicao(c),
                    Ativo = true,
                    ValidadeConsultaDias = ConvenioInfo.ValidadeConsultaDias(c),
                    DiasSegundoCodigo = 1,
                    CategoriaComApp = CategoriaConvenio.Base(c, true),
                    CategoriaSemApp = CategoriaConvenio.Base(c, false)
                };
        }
        return new ParametrosSnapshot(map);
    }

    /// <summary>Completa campos nulos de uma linha salva com os padrões do código (para exibição/edição).</summary>
    private static ParametroConvenio PreencherDefaults(ParametroConvenio p)
    {
        p.Nome ??= ConvenioInfo.NomeExibicao(p.Convenio);
        p.CategoriaComApp ??= CategoriaConvenio.Base(p.Convenio, true);
        p.CategoriaSemApp ??= CategoriaConvenio.Base(p.Convenio, false);
        return p;
    }

    public async Task SalvarAsync(IEnumerable<ParametroConvenio> parametros, CancellationToken ct = default)
    {
        foreach (var p in parametros)
            await _repo.SalvarParametroAsync(p, ct);
        await _repo.SalvarAsync(ct);
    }

    // ---- Configurações globais (valem para todas as máquinas) ----

    public const string ChaveJanelaAlertaConsulta = "JanelaAlertaConsultaDias";
    public const int JanelaAlertaConsultaPadrao = 5;

    /// <summary>Dias de antecedência do alerta de consultas a vencer (global, salvo no banco).</summary>
    public async Task<int> ObterJanelaAlertaConsultaAsync(CancellationToken ct = default)
        => int.TryParse(await _repo.ObterConfiguracaoAsync(ChaveJanelaAlertaConsulta, ct), out var dias) && dias >= 0
            ? dias
            : JanelaAlertaConsultaPadrao;

    public async Task SalvarJanelaAlertaConsultaAsync(int dias, CancellationToken ct = default)
    {
        await _repo.SalvarConfiguracaoAsync(ChaveJanelaAlertaConsulta, Math.Max(0, dias).ToString(), ct);
        await _repo.SalvarAsync(ct);
    }

    // ---- Prazo de recurso de glosa (dias corridos a partir da glosa; contratual) ----

    public const string ChavePrazoRecursoGlosa = "PrazoRecursoGlosaDias";
    public const int PrazoRecursoGlosaPadrao = 30;

    /// <summary>Dias para recorrer de uma glosa (global, salvo no banco; padrão 30).</summary>
    public async Task<int> ObterPrazoRecursoGlosaAsync(CancellationToken ct = default)
        => int.TryParse(await _repo.ObterConfiguracaoAsync(ChavePrazoRecursoGlosa, ct), out var dias) && dias >= 1
            ? dias
            : PrazoRecursoGlosaPadrao;

    public async Task SalvarPrazoRecursoGlosaAsync(int dias, CancellationToken ct = default)
    {
        await _repo.SalvarConfiguracaoAsync(ChavePrazoRecursoGlosa, Math.Max(1, dias).ToString(), ct);
        await _repo.SalvarAsync(ct);
    }

    // ---- Rodada de pendências ("rodar as pendências" — prazo de decisão por guia) ----

    public const string ChaveIntervaloRodadaPendencias = "IntervaloRodadaPendenciasDias";
    public const int IntervaloRodadaPendenciasPadrao = 10;

    /// <summary>
    /// Prazo (em dias, desde a data prevista) para exigir decisão em cada guia pendente (padrão 10).
    /// Ao vencer sem baixa, a guia entra no bloqueio da rodada.
    /// </summary>
    public async Task<int> ObterIntervaloRodadaPendenciasAsync(CancellationToken ct = default)
        => int.TryParse(await _repo.ObterConfiguracaoAsync(ChaveIntervaloRodadaPendencias, ct), out var dias) && dias >= 1
            ? dias
            : IntervaloRodadaPendenciasPadrao;

    public async Task SalvarIntervaloRodadaPendenciasAsync(int dias, CancellationToken ct = default)
    {
        await _repo.SalvarConfiguracaoAsync(ChaveIntervaloRodadaPendencias, Math.Max(1, dias).ToString(), ct);
        await _repo.SalvarAsync(ct);
    }

    // ---- Publicação do documento assinado (parcela 53) ----

    public const string ChaveUrlPublicacao = "PublicacaoUrlBase";

    /// <summary>
    /// Domínio onde os documentos assinados ficam acessíveis (ex.:
    /// <c>https://receita.clinicasemdor.com.br</c>). Null = publicação DESLIGADA, e o
    /// sistema funciona como antes: o QR aponta para o validador do ITI.
    ///
    /// É o DOMÍNIO DA CLÍNICA, nunca o endereço do provedor — a URL fica selada dentro da
    /// assinatura, e trocar de armazenamento um dia tem de ser repontar o DNS, não matar o
    /// QR de toda receita já assinada.
    /// </summary>
    /// <remarks>
    /// <b>Correção da parcela 53.</b> A primeira versão deste comentário dizia que as
    /// credenciais do armazenamento iriam por variável de ambiente, "porque segredo em
    /// tabela de configuração é segredo que sai no backup". Estava errado por duas vias.
    ///
    /// Primeiro, contradiz o que o projeto já decidiu: o
    /// <c>ProvedorOpcoesSafeID</c> saiu de variável de ambiente na parcela 44 com o motivo
    /// escrito — variável de ambiente é ritual de instalação, e uma clínica não abre o
    /// Prompt de Comando em cada máquina. Aqui seria pior: quem assina documento é o
    /// Consultório E a Recepção, então a publicação funcionaria onde alguém digitou e
    /// falharia CALADA nas outras.
    ///
    /// Segundo, descrevia como seguro um padrão que o sistema não segue: o
    /// <c>client_secret</c> do SafeID já está gravado em claro nesta mesma tabela.
    ///
    /// As credenciais ficam no banco, com o ambiente podendo sobrepor — igual ao SafeID. O
    /// segredo dentro do backup é problema real e SEPARADO, que já existe hoje e merece
    /// decisão própria; resolvê-lo por acidente aqui só o esconderia.
    /// </remarks>
    public async Task<string?> ObterUrlPublicacaoAsync(CancellationToken ct = default)
    {
        var valor = await _repo.ObterConfiguracaoAsync(ChaveUrlPublicacao, ct);
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }

    public async Task SalvarUrlPublicacaoAsync(string? url, CancellationToken ct = default)
    {
        await _repo.SalvarConfiguracaoAsync(ChaveUrlPublicacao, url?.Trim() ?? string.Empty, ct);
        await _repo.SalvarAsync(ct);
    }

    public const string ChaveDiasPublicacao = "PublicacaoDiasNoAr";

    /// <summary>
    /// Por quantos dias o documento fica no ar. Ausente ou fora da faixa cai no padrão —
    /// ver <see cref="PublicacaoDocumento.DiasPublicadoValidos"/>.
    /// </summary>
    public async Task<int> ObterDiasPublicacaoAsync(CancellationToken ct = default)
        => PublicacaoDocumento.DiasPublicadoValidos(
            int.TryParse(await _repo.ObterConfiguracaoAsync(ChaveDiasPublicacao, ct), out var d)
                ? d
                : null);

    public async Task SalvarDiasPublicacaoAsync(int dias, CancellationToken ct = default)
    {
        await _repo.SalvarConfiguracaoAsync(
            ChaveDiasPublicacao,
            PublicacaoDocumento.DiasPublicadoValidos(dias).ToString(),
            ct);
        await _repo.SalvarAsync(ct);
    }

    // ---- Credenciais do armazenamento de objetos (parcela 53) ----
    //
    // Banco, com o ambiente podendo sobrepor — o precedente do SafeID. Ver o <remarks> de
    // ObterUrlPublicacaoAsync para por que NÃO é variável de ambiente apenas.

    public const string ChaveArmazenamentoEndpoint = "PublicacaoEndpoint";
    public const string ChaveArmazenamentoRegiao = "PublicacaoRegiao";
    public const string ChaveArmazenamentoBucket = "PublicacaoBucket";
    public const string ChaveArmazenamentoChave = "PublicacaoAccessKey";
    public const string ChaveArmazenamentoSegredo = "PublicacaoSecretKey";

    /// <summary>
    /// As credenciais do armazenamento S3-compatível, ou <c>null</c> em cada campo que a
    /// clínica ainda não preencheu.
    ///
    /// <b>Devolve os campos crus, sem julgar se o conjunto serve.</b> Quem decide se há
    /// configuração utilizável é <c>OpcoesArmazenamento.De</c>, num lugar só — espalhar a
    /// pergunta "está configurado?" faria a tela e o serviço discordarem sobre o mesmo
    /// banco.
    /// </summary>
    public async Task<(string? Endpoint, string? Regiao, string? Bucket, string? Chave, string? Segredo)>
        ObterCredenciaisArmazenamentoAsync(CancellationToken ct = default)
        => (await _repo.ObterConfiguracaoAsync(ChaveArmazenamentoEndpoint, ct),
            await _repo.ObterConfiguracaoAsync(ChaveArmazenamentoRegiao, ct),
            await _repo.ObterConfiguracaoAsync(ChaveArmazenamentoBucket, ct),
            await _repo.ObterConfiguracaoAsync(ChaveArmazenamentoChave, ct),
            await _repo.ObterConfiguracaoAsync(ChaveArmazenamentoSegredo, ct));

    public async Task SalvarCredenciaisArmazenamentoAsync(
        string? endpoint, string? regiao, string? bucket, string? chave, string? segredo,
        CancellationToken ct = default)
    {
        await _repo.SalvarConfiguracaoAsync(ChaveArmazenamentoEndpoint, (endpoint ?? string.Empty).Trim(), ct);
        await _repo.SalvarConfiguracaoAsync(ChaveArmazenamentoRegiao, (regiao ?? string.Empty).Trim(), ct);
        await _repo.SalvarConfiguracaoAsync(ChaveArmazenamentoBucket, (bucket ?? string.Empty).Trim(), ct);
        await _repo.SalvarConfiguracaoAsync(ChaveArmazenamentoChave, (chave ?? string.Empty).Trim(), ct);
        await _repo.SalvarConfiguracaoAsync(ChaveArmazenamentoSegredo, (segredo ?? string.Empty).Trim(), ct);
        await _repo.SalvarAsync(ct);
    }

    // ---- Política de backup (parcela 52) ----

    public const string ChavePastaBackup = "BackupPasta";
    public const string ChaveIntervaloBackup = "BackupIntervaloDias";
    public const string ChaveUltimoBackup = "BackupUltimoEm";
    public const string ChaveCopiasBackup = "BackupCopiasAGuardar";

    public const int IntervaloBackupPadrao = 7;
    public const int CopiasBackupPadrao = 8;

    /// <summary>
    /// Onde a cópia é gravada. Null = a clínica ainda não escolheu, e aí o backup só
    /// acontece por clique — que é o estado que a auditoria de fornecedor reprovou.
    /// </summary>
    public async Task<string?> ObterPastaBackupAsync(CancellationToken ct = default)
    {
        var valor = await _repo.ObterConfiguracaoAsync(ChavePastaBackup, ct);
        return string.IsNullOrWhiteSpace(valor) ? null : valor;
    }

    public async Task SalvarPastaBackupAsync(string? pasta, CancellationToken ct = default)
    {
        await _repo.SalvarConfiguracaoAsync(ChavePastaBackup, pasta?.Trim() ?? string.Empty, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>De quantos em quantos dias a cópia deve ser feita (padrão 7).</summary>
    public async Task<int> ObterIntervaloBackupAsync(CancellationToken ct = default)
        => int.TryParse(await _repo.ObterConfiguracaoAsync(ChaveIntervaloBackup, ct), out var d) && d >= 1
            ? d
            : IntervaloBackupPadrao;

    public async Task SalvarIntervaloBackupAsync(int dias, CancellationToken ct = default)
    {
        await _repo.SalvarConfiguracaoAsync(ChaveIntervaloBackup, Math.Max(1, dias).ToString(), ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// Quantas cópias ficam na pasta (padrão 8 — com o intervalo padrão, dois meses).
    ///
    /// Guardar só a última é o erro clássico: a corrupção que ninguém percebeu na
    /// sexta-feira é copiada por cima da única cópia boa no sábado.
    /// </summary>
    public async Task<int> ObterCopiasBackupAsync(CancellationToken ct = default)
        => int.TryParse(await _repo.ObterConfiguracaoAsync(ChaveCopiasBackup, ct), out var n) && n >= 1
            ? n
            : CopiasBackupPadrao;

    public async Task SalvarCopiasBackupAsync(int copias, CancellationToken ct = default)
    {
        await _repo.SalvarConfiguracaoAsync(ChaveCopiasBackup, Math.Max(1, copias).ToString(), ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>Quando a última cópia foi gravada com sucesso. Null = nunca.</summary>
    public async Task<DateTime?> ObterUltimoBackupAsync(CancellationToken ct = default)
        => DateTime.TryParse(
                await _repo.ObterConfiguracaoAsync(ChaveUltimoBackup, ct),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var d)
            ? d
            : null;

    /// <summary>
    /// Grava o momento da cópia. Formato "O" (ISO), pelo mesmo motivo do backup: data
    /// escrita na cultura da máquina vira 08/03 noutro computador.
    /// </summary>
    public async Task SalvarUltimoBackupAsync(DateTime quando, CancellationToken ct = default)
    {
        await _repo.SalvarConfiguracaoAsync(ChaveUltimoBackup, quando.ToString("O"), ct);
        await _repo.SalvarAsync(ct);
    }

    // O VALOR da chave continua "InicioRodadaPorAtendimento" de propósito: é o que já está gravado
    // nas instalações que ancoraram a carência. Renomeá-lo perderia a âncora e reiniciaria o período
    // de carência do zero. Só o nome em C# acompanhou a mudança do prazo para a data prevista.
    public const string ChaveInicioRodadaPrazo = "InicioRodadaPorAtendimento";

    /// <summary>
    /// Data em que a rodada passou a valer (definida na 1ª execução desta versão). Serve para dar
    /// carência ao backlog anterior — guias que já estavam pendentes antes dela só começam a contar
    /// o prazo a partir daqui, evitando um bloqueio em massa logo na primeira abertura.
    /// Null = ainda não ancorada.
    /// </summary>
    public async Task<DateOnly?> ObterInicioRodadaPrazoAsync(CancellationToken ct = default)
        => DateOnly.TryParse(await _repo.ObterConfiguracaoAsync(ChaveInicioRodadaPrazo, ct), out var d)
            ? d
            : null;

    public async Task SalvarInicioRodadaPrazoAsync(DateOnly data, CancellationToken ct = default)
    {
        await _repo.SalvarConfiguracaoAsync(ChaveInicioRodadaPrazo, data.ToString("yyyy-MM-dd"), ct);
        await _repo.SalvarAsync(ct);
    }

    public const string ChaveRodadaAplicaConsultas = "RodadaAplicaConsultas";
    public const string ChaveRodadaAplicaCarteirinhas = "RodadaAplicaCarteirinhas";

    /// <summary>A rodada também cobra consultas a renovar? (padrão: não — começa só pelas guias).</summary>
    public async Task<bool> ObterRodadaAplicaConsultasAsync(CancellationToken ct = default)
        => string.Equals(await _repo.ObterConfiguracaoAsync(ChaveRodadaAplicaConsultas, ct), "true",
            StringComparison.OrdinalIgnoreCase);

    /// <summary>A rodada também cobra carteirinhas a vencer? (padrão: não — começa só pelas guias).</summary>
    public async Task<bool> ObterRodadaAplicaCarteirinhasAsync(CancellationToken ct = default)
        => string.Equals(await _repo.ObterConfiguracaoAsync(ChaveRodadaAplicaCarteirinhas, ct), "true",
            StringComparison.OrdinalIgnoreCase);

    public async Task SalvarRodadaAplicaAsync(bool consultas, bool carteirinhas, CancellationToken ct = default)
    {
        await _repo.SalvarConfiguracaoAsync(ChaveRodadaAplicaConsultas, consultas ? "true" : "false", ct);
        await _repo.SalvarConfiguracaoAsync(ChaveRodadaAplicaCarteirinhas, carteirinhas ? "true" : "false", ct);
        await _repo.SalvarAsync(ct);
    }

    // ---- Imposto retido (parcela 9) ----

    public const string ChaveAliquotaImposto = "AliquotaImpostoRetido";

    /// <summary>
    /// Aliquota de imposto retido sobre o recebimento, em percentual (ISS, Simples).
    /// PADRAO ZERO de proposito: cada clinica tem o seu regime, e chutar uma aliquota
    /// produziria um liquido errado com aparencia de exato em toda a base. Zero significa
    /// "nao configurado", e nesse caso nenhum imposto e descontado.
    ///
    /// A cultura da leitura e invariante (ponto decimal), nao a do Windows: a mesma base
    /// e lida por maquinas com virgula e com ponto, e "2,5" lido como 25 multiplicaria o
    /// imposto por dez.
    /// </summary>
    public async Task<decimal> ObterAliquotaImpostoAsync(CancellationToken ct = default)
        => decimal.TryParse(
               await _repo.ObterConfiguracaoAsync(ChaveAliquotaImposto, ct),
               System.Globalization.NumberStyles.Number,
               System.Globalization.CultureInfo.InvariantCulture,
               out var aliquota) && aliquota is > 0m and <= 100m
            ? aliquota
            : 0m;

    public async Task SalvarAliquotaImpostoAsync(decimal aliquota, CancellationToken ct = default)
    {
        if (aliquota is < 0m or > 100m)
            throw new InvalidOperationException("A aliquota vai de 0 a 100.");

        await _repo.SalvarConfiguracaoAsync(
            ChaveAliquotaImposto,
            aliquota.ToString(System.Globalization.CultureInfo.InvariantCulture), ct);
        await _repo.SalvarAsync(ct);
    }

    // ---- Indicadores gerenciais (parcela 5) ----

    public const string ChaveJornadaDiariaMinutos = "JornadaDiariaMinutos";
    public const int JornadaDiariaPadraoMinutos = 480; // 8 h

    /// <summary>
    /// Jornada diária de um profissional, em minutos — o DENOMINADOR da taxa de
    /// ocupação. É configurável porque clínica que atende meio período veria 50% de
    /// ocupação com a agenda cheia, e o número mentiria para baixo todo mês.
    /// </summary>
    public async Task<int> ObterJornadaDiariaAsync(CancellationToken ct = default)
        => int.TryParse(await _repo.ObterConfiguracaoAsync(ChaveJornadaDiariaMinutos, ct), out var min) && min >= 1
            ? min
            : JornadaDiariaPadraoMinutos;

    public async Task SalvarJornadaDiariaAsync(int minutos, CancellationToken ct = default)
    {
        await _repo.SalvarConfiguracaoAsync(
            ChaveJornadaDiariaMinutos, Math.Clamp(minutos, 1, 24 * 60).ToString(), ct);
        await _repo.SalvarAsync(ct);
    }

    public const string ChaveDiasInatividadeRecall = "DiasInatividadeRecall";

    /// <summary>Dias sem vir a partir dos quais o paciente entra no recall (padrão 60).</summary>
    public async Task<int> ObterDiasInatividadeRecallAsync(CancellationToken ct = default)
        => int.TryParse(await _repo.ObterConfiguracaoAsync(ChaveDiasInatividadeRecall, ct), out var dias) && dias >= 1
            ? dias
            : CampanhaService.DiasInatividadePadrao;

    public async Task SalvarDiasInatividadeRecallAsync(int dias, CancellationToken ct = default)
    {
        await _repo.SalvarConfiguracaoAsync(ChaveDiasInatividadeRecall, Math.Max(1, dias).ToString(), ct);
        await _repo.SalvarAsync(ct);
    }

    // ---- Carimbo do tempo das assinaturas ICP-Brasil (parcela 42) ----

    public const string ChaveCarimbadoraDeTempo = "AssinaturaCarimbadoraDeTempoUrl";

    /// <summary>
    /// URL de uma ACT (autoridade de carimbo do tempo, RFC 3161), ou null.
    ///
    /// Nasce VAZIA de propósito. Sem ela a assinatura é PAdES-B: válida, com a data por
    /// conta do relógio de quem assinou — e o PDF escreve exatamente isso. Chutar uma ACT
    /// pública faria a clínica confiar num carimbo que ninguém contratou e que pode sair
    /// do ar sem aviso, transformando "assinar" numa operação que às vezes falha por
    /// motivo que ninguém entende.
    /// </summary>
    public async Task<Uri?> ObterCarimbadoraDeTempoAsync(CancellationToken ct = default)
    {
        var valor = await _repo.ObterConfiguracaoAsync(ChaveCarimbadoraDeTempo, ct);
        return Uri.TryCreate(valor, UriKind.Absolute, out var url) ? url : null;
    }

    public async Task SalvarCarimbadoraDeTempoAsync(string? url, CancellationToken ct = default)
    {
        await _repo.SalvarConfiguracaoAsync(
            ChaveCarimbadoraDeTempo, (url ?? string.Empty).Trim(), ct);
        await _repo.SalvarAsync(ct);
    }

    // ---- Credenciais da aplicação no SafeID (parcela 44) ----

    public const string ChaveSafeIDClientId = "SafeIDClientId";
    public const string ChaveSafeIDClientSecret = "SafeIDClientSecret";
    public const string ChaveSafeIDAmbiente = "SafeIDAmbiente";

    /// <summary>
    /// As credenciais da APLICAÇÃO no PSC, cadastradas uma vez pela direção e lidas por
    /// todas as máquinas.
    ///
    /// Por que no banco, e não numa variável de ambiente por máquina
    /// -------------------------------------------------------------
    /// Porque senão instalar o sistema numa máquina nova passaria por abrir o Prompt de
    /// Comando e digitar quatro <c>setx</c> — e uma clínica não faz isso. Configuração que
    /// depende de ritual de instalação é configuração que um dia falta, e o sintoma seria a
    /// médica sem conseguir assinar num consultório específico, sem ninguém saber por quê.
    ///
    /// O <c>client_secret</c> identifica a APLICAÇÃO, não a titular: sozinho ele não assina
    /// nada. Para assinar é preciso, ainda, a médica aprovar no celular (ou o PIN dela) e o
    /// CPF de dentro do certificado bater com o do cadastro — as duas barreiras que
    /// <c>TitularDoCertificado.Exigir</c> guarda. É por isso que ele pode morar aqui, ao
    /// lado da URL da ACT, e a connection string não pode: aquela É a chave de tudo.
    /// </summary>
    public async Task<(string? ClientId, string? ClientSecret, string? Ambiente)>
        ObterCredenciaisSafeIDAsync(CancellationToken ct = default)
        => (await _repo.ObterConfiguracaoAsync(ChaveSafeIDClientId, ct),
            await _repo.ObterConfiguracaoAsync(ChaveSafeIDClientSecret, ct),
            await _repo.ObterConfiguracaoAsync(ChaveSafeIDAmbiente, ct));

    public async Task SalvarCredenciaisSafeIDAsync(
        string? clientId, string? clientSecret, string? ambiente,
        CancellationToken ct = default)
    {
        await _repo.SalvarConfiguracaoAsync(ChaveSafeIDClientId, (clientId ?? string.Empty).Trim(), ct);
        await _repo.SalvarConfiguracaoAsync(ChaveSafeIDClientSecret, (clientSecret ?? string.Empty).Trim(), ct);
        await _repo.SalvarConfiguracaoAsync(ChaveSafeIDAmbiente, (ambiente ?? string.Empty).Trim(), ct);
        await _repo.SalvarAsync(ct);
    }

    // ---- Numeração sequencial de lote TISS (o padrão exige sequência, não timestamp) ----

    public const string ChaveProximoLoteTiss = "TissProximoNumeroLote";

    /// <summary>Próximo número de lote TISS (sequência global, começa em 1). Não consome o número.</summary>
    public async Task<int> ObterProximoNumeroLoteTissAsync(CancellationToken ct = default)
        => int.TryParse(await _repo.ObterConfiguracaoAsync(ChaveProximoLoteTiss, ct), out var n) && n >= 1 ? n : 1;

    /// <summary>Confirma que o número de lote foi usado (grava numeroUsado+1 como próximo).</summary>
    public async Task ConfirmarNumeroLoteTissAsync(int numeroUsado, CancellationToken ct = default)
    {
        await _repo.SalvarConfiguracaoAsync(ChaveProximoLoteTiss, (numeroUsado + 1).ToString(), ct);
        await _repo.SalvarAsync(ct);
    }

    // ---- Dados do prestador (clínica) — GLOBAIS, usados na capa e no TISS ----

    public const string ChavePrestador = "DadosPrestador";

    private static readonly JsonSerializerOptions OpcoesJson = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Já existe configuração do prestador salva no banco?</summary>
    public async Task<bool> PrestadorConfiguradoAsync(CancellationToken ct = default)
        => await _repo.ObterConfiguracaoAsync(ChavePrestador, ct) is not null;

    /// <summary>Dados do prestador salvos no banco (vazios se nunca configurados).</summary>
    public async Task<DadosPrestador> ObterPrestadorAsync(CancellationToken ct = default)
    {
        var json = await _repo.ObterConfiguracaoAsync(ChavePrestador, ct);
        if (string.IsNullOrWhiteSpace(json))
            return new DadosPrestador();

        try
        {
            return JsonSerializer.Deserialize<DadosPrestador>(json, OpcoesJson) ?? new DadosPrestador();
        }
        catch (Exception ex)
        {
            // Configuração corrompida não impede o uso — mas fica no log, senão o
            // prestador "some" da guia sem ninguém entender por quê.
            Diagnostico.Registrar("Parâmetros — dados do prestador corrompidos", ex);
            return new DadosPrestador();
        }
    }

    public async Task SalvarPrestadorAsync(DadosPrestador dados, CancellationToken ct = default)
    {
        await _repo.SalvarConfiguracaoAsync(ChavePrestador, JsonSerializer.Serialize(dados, OpcoesJson), ct);
        await _repo.SalvarAsync(ct);
    }
}
