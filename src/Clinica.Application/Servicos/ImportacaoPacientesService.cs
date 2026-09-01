using System.Globalization;
using System.Text;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Importação de pacientes do sistema anterior (set/2026 — a clínica migrou do Smart
/// Clinic e recebeu a exportação da carteira). Dois passos, e a separação é o desenho:
/// <see cref="PreverAsync"/> lê o arquivo e diz, linha a linha, o que VAI acontecer, sem
/// gravar nada; <see cref="ExecutarAsync"/> grava exatamente o que a prévia mostrou.
/// Importar às cegas e conferir depois é como se cria a carteira com 300 fichas
/// duplicadas — e duplicata de paciente é a que parte o histórico em dois (parcela 57).
///
/// As regras que não são óbvias:
/// <list type="bullet">
/// <item><b>Idempotente pela chave</b> (<see cref="Paciente.ChaveImportacao"/> =
/// <c>IMPORT:{sistema}:{id de lá}</c>): o mesmo arquivo importado duas vezes — a segunda
/// exportação, a linha corrigida, a queda de conexão no meio — pula quem já entrou.</item>
/// <item><b>Quem já existe é COMPLETADO, nunca sobrescrito</b>: a ficha da base é a que a
/// clínica vem corrigindo no balcão; o arquivo preenche só o que está VAZIO nela. O
/// convênio da ficha é o que fatura, e não se troca por importação.</item>
/// <item><b>A criação passa pelo <see cref="PacienteService"/></b> — a validação do CPF e
/// a recusa de duplicata moram lá, e uma segunda cópia da regra divergiria na primeira
/// correção.</item>
/// <item><b>Convênio do arquivo é TEXTO, e cada texto aponta para UM convênio cadastrado
/// aqui</b>, escolhido pela direção na tela. Texto sem destino é problema da linha, não
/// palpite: "Unimed" no arquivo pode ser Padrão ou Intercâmbio, e a diferença é regra de
/// faturamento.</item>
/// <item><b>Nada apaga nada</b>: não há caminho de desfazer em lote. A ficha importada por
/// engano se remove uma a uma pela ficha (só se vazia), como qualquer outra.</item>
/// </list>
/// </summary>
public sealed class ImportacaoPacientesService
{
    public const string SistemaSmartClinic = "smartclinic";

    /// <summary>Rótulo da chave do mapa de convênios para a célula em branco.</summary>
    public const string ConvenioEmBranco = "(em branco)";

    private readonly IClinicaRepositorio _repo;
    private readonly PacienteService _pacientes;

    public ImportacaoPacientesService(IClinicaRepositorio repo, PacienteService pacientes)
    {
        _repo = repo;
        _pacientes = pacientes;
    }

    public static string Chave(string sistema, string idOrigem)
        => $"IMPORT:{sistema}:{idOrigem.Trim()}";

    /// <summary>Os textos DISTINTOS da coluna de convênio, na ordem em que aparecem — é a
    /// lista que a direção mapeia. Vazio quando a coluna não foi mapeada.</summary>
    public static IReadOnlyList<string> ConveniosDoArquivo(TabelaImportada tabela, MapeamentoImportacao mapa)
    {
        if (mapa.ColunaDe(CampoImportacao.Convenio) is not { } col) return [];
        return tabela.Linhas
            .Select(l => ChaveConvenio(l[col]))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string ChaveConvenio(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? ConvenioEmBranco : texto.Trim();

    /// <summary>
    /// Lê o arquivo com o mapeamento e responde, linha a linha, o que a execução faria.
    /// NÃO grava nada.
    /// </summary>
    public async Task<PreviaImportacao> PreverAsync(
        TabelaImportada tabela,
        MapeamentoImportacao mapa,
        IReadOnlyDictionary<string, ConvenioCadastro> convenios,
        string sistema = SistemaSmartClinic,
        CancellationToken ct = default)
    {
        if (mapa.ColunaDe(CampoImportacao.Nome) is null)
            throw new ArgumentException("Diga qual coluna do arquivo tem o NOME do paciente.");
        if (string.IsNullOrWhiteSpace(sistema))
            throw new ArgumentException("Informe o sistema de origem.");

        var fichas = await _repo.FichasResumidasAsync(ct);
        var porChave = fichas.Where(f => f.ChaveImportacao is not null)
            .ToDictionary(f => f.ChaveImportacao!, StringComparer.Ordinal);
        var porCpf = new Dictionary<string, FichaResumida>(StringComparer.Ordinal);
        foreach (var f in fichas)
        {
            var d = Cpf.Normalizar(f.Documento);
            if (d.Length == 11) porCpf.TryAdd(d, f);
        }
        var porNome = fichas
            .GroupBy(f => SugestorDeMapeamento.Normalizar(f.Nome))
            .ToDictionary(g => g.Key, g => g.ToList());

        var avisosGerais = new List<string>();
        if (!mapa.Tem(CampoImportacao.IdOrigem))
            avisosGerais.Add("Sem a coluna de ID do sistema anterior, importar o mesmo arquivo de novo "
                             + "só reconhece quem tem CPF — as outras fichas seriam criadas de novo.");
        if (!mapa.Tem(CampoImportacao.Cpf))
            avisosGerais.Add("Sem a coluna de CPF, quem já está cadastrado aqui só é reconhecido "
                             + "por nome e data de nascimento.");
        if (!mapa.Tem(CampoImportacao.Convenio))
            avisosGerais.Add("Sem a coluna de convênio, toda ficha precisa de um convênio escolhido "
                             + "no passo 2 (a linha \"(em branco)\").");

        var chavesNoArquivo = new Dictionary<string, int>(StringComparer.Ordinal);
        var cpfsNoArquivo = new Dictionary<string, int>(StringComparer.Ordinal);
        var linhas = new List<LinhaPrevia>();

        for (var i = 0; i < tabela.Linhas.Count; i++)
        {
            var numero = i + 2; // 1 é o cabeçalho — é o número que a pessoa vê no Excel
            var l = tabela.Linhas[i];
            string? Campo(CampoImportacao c) =>
                mapa.ColunaDe(c) is { } col && col < l.Length && !string.IsNullOrWhiteSpace(l[col])
                    ? l[col].Trim() : null;

            var nome = Campo(CampoImportacao.Nome);
            var cpfBruto = Campo(CampoImportacao.Cpf);
            var cpfFormatado = cpfBruto is null ? null : Cpf.Formatar(cpfBruto);
            var convenioTexto = mapa.Tem(CampoImportacao.Convenio) ? ChaveConvenio(Campo(CampoImportacao.Convenio)) : ConvenioEmBranco;
            var avisos = new List<string>();

            LinhaPrevia Problema(string detalhe) =>
                new(numero, nome ?? "(sem nome)", cpfFormatado, convenioTexto, DestinoLinha.Problema, detalhe, avisos);

            if (nome is null)
            {
                linhas.Add(Problema("Linha sem nome."));
                continue;
            }
            if (nome.Length > 200) nome = nome[..200];

            // ---- chave de idempotência ----
            string? chave = null;
            var id = Campo(CampoImportacao.IdOrigem);
            if (id is not null)
            {
                chave = Chave(sistema, id);
                if (chavesNoArquivo.TryGetValue(chave, out var outra))
                {
                    linhas.Add(Problema($"O ID \"{id}\" se repete no arquivo (já apareceu na linha {outra})."));
                    continue;
                }
                chavesNoArquivo[chave] = numero;

                if (porChave.TryGetValue(chave, out var ja))
                {
                    linhas.Add(new LinhaPrevia(numero, nome, cpfFormatado, convenioTexto,
                        DestinoLinha.JaImportada,
                        $"Já entrou numa importação anterior como \"{ja.Nome}\" (ficha #{ja.Id}).", avisos)
                    { PacienteExistenteId = ja.Id, Chave = chave });
                    continue;
                }
            }

            // ---- CPF ----
            string? cpf = null;
            if (cpfBruto is not null)
            {
                if (!Cpf.Valido(cpfBruto))
                {
                    linhas.Add(Problema($"CPF inválido: \"{cpfBruto}\". Corrija no arquivo ou apague a célula."));
                    continue;
                }
                cpf = Cpf.Normalizar(cpfBruto);
                if (cpfsNoArquivo.TryGetValue(cpf, out var outra))
                {
                    linhas.Add(Problema($"CPF repetido no arquivo (já apareceu na linha {outra})."));
                    continue;
                }
                cpfsNoArquivo[cpf] = numero;
            }

            // ---- convênio ----
            if (!convenios.TryGetValue(convenioTexto, out var cadastro) || cadastro is null)
            {
                linhas.Add(Problema(convenioTexto == ConvenioEmBranco
                    ? "Convênio em branco no arquivo — escolha no passo 2 qual convênio recebe essas linhas."
                    : $"Convênio \"{convenioTexto}\" ainda não aponta para um convênio cadastrado aqui (passo 2)."));
                continue;
            }

            // ---- os demais campos ----
            var nascimento = LerData(Campo(CampoImportacao.DataNascimento), "data de nascimento", avisos, nascimento: true);
            var validade = LerData(Campo(CampoImportacao.ValidadeCarteirinha), "validade da carteirinha", avisos);
            var sexo = LerSexo(Campo(CampoImportacao.Sexo), mapa.Tem(CampoImportacao.Sexo), avisos);
            var telefone = LerTelefone(Campo(CampoImportacao.Telefone));
            var endereco = Cortar(Campo(CampoImportacao.Endereco), 300);
            var carteirinha = Cortar(Campo(CampoImportacao.Carteirinha), 40);
            var observacoes = Campo(CampoImportacao.Observacoes);
            var (origem, indicadoPor, observacaoOrigem) = LerOrigem(Campo(CampoImportacao.Origem));
            if (observacaoOrigem is not null)
                observacoes = string.IsNullOrWhiteSpace(observacoes) ? observacaoOrigem : $"{observacoes}\n{observacaoOrigem}";

            // ---- já existe? ----
            FichaResumida? existente = null;
            if (cpf is not null && porCpf.TryGetValue(cpf, out var porDoc))
            {
                existente = porDoc;
            }
            else if (porNome.TryGetValue(SugestorDeMapeamento.Normalizar(nome), out var homonimos))
            {
                // Nome igual E nascimento igual é a mesma pessoa; nome igual sozinho é
                // homônimo até prova em contrário — cria e AVISA, porque fundir duas
                // pessoas num prontuário é pior do que duas fichas para conferir.
                var mesma = nascimento is not null
                    ? homonimos.FirstOrDefault(h => h.DataNascimento == nascimento)
                    : null;
                if (mesma is not null)
                {
                    existente = mesma;
                    avisos.Add("Reconhecida pelo nome e pela data de nascimento (não há CPF para comparar).");
                }
                else
                    avisos.Add($"Há ficha com o mesmo nome (#{homonimos[0].Id}) — confira depois se não é a mesma pessoa.");
            }

            if (existente is not null)
            {
                if (existente.ChaveImportacao is not null && chave is not null && existente.ChaveImportacao != chave)
                    avisos.Add("A ficha já veio de outra importação; a chave dela é mantida.");

                var ficha = new Paciente
                {
                    Nome = nome, Documento = cpf, Telefone = telefone, DataNascimento = nascimento,
                    Endereco = endereco, Carteirinha = carteirinha, ValidadeCarteirinha = validade,
                    Convenio = cadastro.Familia, ConvenioCodigo = cadastro.Codigo, Sexo = sexo,
                    Observacoes = observacoes, Origem = origem, IndicadoPor = indicadoPor,
                    ChaveImportacao = chave
                };
                linhas.Add(new LinhaPrevia(numero, nome, cpfFormatado, convenioTexto, DestinoLinha.Completar,
                    $"Já cadastrada como \"{existente.Nome}\" (ficha #{existente.Id}): só os campos vazios dela serão preenchidos; o convênio da ficha é mantido.",
                    avisos)
                { Ficha = ficha, PacienteExistenteId = existente.Id, Chave = chave });
                continue;
            }

            var nova = new Paciente
            {
                Nome = nome, Documento = cpf, Telefone = telefone, DataNascimento = nascimento,
                Endereco = endereco, Carteirinha = carteirinha, ValidadeCarteirinha = validade,
                Convenio = cadastro.Familia, ConvenioCodigo = cadastro.Codigo, Sexo = sexo,
                Observacoes = observacoes, Origem = origem, IndicadoPor = indicadoPor,
                ChaveImportacao = chave
            };
            linhas.Add(new LinhaPrevia(numero, nome, cpfFormatado, convenioTexto, DestinoLinha.Criar,
                $"Ficha nova · {cadastro.Nome}", avisos)
            { Ficha = nova, Chave = chave });
        }

        return new PreviaImportacao(sistema, linhas, avisosGerais);
    }

    /// <summary>
    /// Grava o que a prévia mostrou. Linha a linha, pelo <see cref="PacienteService"/>:
    /// uma linha que falhe (o CPF entrou na base por outra máquina entre a prévia e o
    /// clique) vira ERRO na lista e as demais seguem — e a chave de importação garante
    /// que rodar de novo só pega o que faltou.
    ///
    /// ⚠️ A trilha de cada ficha é gravada DEPOIS do ato, num segundo Salvar — e isso é uma
    /// exceção declarada à regra 7 do compromisso ("auditoria no MESMO SaveChanges"). A
    /// primeira versão acrescentava o <see cref="EventoAuditoria"/> ao contexto ANTES de
    /// chamar o <see cref="PacienteService"/>; quando ele RECUSAVA a ficha (CPF que entrou
    /// por outra porta), a linha "PacienteImportado" ficava pendurada no contexto e saía
    /// gravada junto da ficha SEGUINTE — trilha afirmando uma importação que não houve, que
    /// é a garantia aparente na tabela que existe para responder "quem fez isso?". Com a
    /// ordem invertida a linha só existe quando a ficha existe, e leva o id dela.
    /// </summary>
    public async Task<ResultadoImportacao> ExecutarAsync(
        PreviaImportacao previa, string operador, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(operador))
            throw new ArgumentException("Informe quem está importando.");

        // Reconfere as chaves NO INSTANTE de gravar: a prévia pode ter ficado aberta
        // enquanto outra máquina importava o mesmo arquivo.
        var chavesJa = (await _repo.FichasResumidasAsync(ct))
            .Where(f => f.ChaveImportacao is not null)
            .Select(f => f.ChaveImportacao!)
            .ToHashSet(StringComparer.Ordinal);

        int criados = 0, completados = 0, pulados = previa.JaImportadas;
        var erros = new List<string>();

        foreach (var linha in previa.Linhas)
        {
            ct.ThrowIfCancellationRequested();
            if (linha.Ficha is null) continue;
            if (linha.Chave is not null && chavesJa.Contains(linha.Chave))
            {
                pulados++;
                continue;
            }

            try
            {
                if (linha.EhCriar)
                {
                    var nova = await _pacientes.SalvarNovoAsync(linha.Ficha, ct: ct);
                    await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
                    {
                        Operador = operador,
                        Acao = "PacienteImportado",
                        PacienteId = nova.Id,
                        Detalhe = $"{nova.Nome}"
                                  + (nova.Documento is null ? "" : $" · CPF {Cpf.Formatar(nova.Documento)}")
                                  + (linha.Chave is null ? "" : $" · {linha.Chave}")
                                  + $" · linha {linha.Numero} do arquivo"
                    }, ct);
                    await _repo.SalvarAsync(ct);
                    criados++;
                }
                else if (linha.EhCompletar && linha.PacienteExistenteId is { } id)
                {
                    var atual = await _repo.ObterPacienteAsync(id, ct)
                        ?? throw new InvalidOperationException($"A ficha #{id} não existe mais.");
                    // A ficha é RASTREADA: se o serviço recusar depois de ela ter sido
                    // mexida, o que foi mexido sairia gravado no Salvar da linha seguinte.
                    // Daí o retrato de antes, para devolver no catch.
                    var antes = Retrato(atual);
                    var preenchidos = Completar(atual, linha.Ficha);
                    if (preenchidos.Count == 0)
                    {
                        pulados++;
                        continue;
                    }
                    try
                    {
                        // categoriaManual: a categoria da ficha existente não se recalcula
                        // por uma importação que nem toca o convênio dela.
                        await _pacientes.AtualizarAsync(atual, categoriaManual: true, ct);
                    }
                    catch
                    {
                        Restaurar(atual, antes);
                        throw;
                    }
                    await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
                    {
                        Operador = operador,
                        Acao = "PacienteCompletadoPorImportacao",
                        PacienteId = id,
                        Detalhe = $"{atual.Nome} · preenchidos: {string.Join(", ", preenchidos)}"
                                  + (linha.Chave is null ? "" : $" · {linha.Chave}")
                                  + $" · linha {linha.Numero} do arquivo"
                    }, ct);
                    await _repo.SalvarAsync(ct);
                    completados++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                erros.Add($"Linha {linha.Numero} ({linha.Nome}): {ex.Message}");
            }
            if (linha.Chave is not null) chavesJa.Add(linha.Chave);
        }

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = operador,
            Acao = "ImportacaoPacientes",
            Detalhe = $"Sistema {previa.Sistema}: {criados} ficha(s) nova(s), {completados} completada(s), "
                      + $"{pulados} já existente(s), {previa.Problemas} linha(s) com problema, {erros.Count} erro(s) na gravação."
        }, ct);
        await _repo.SalvarAsync(ct);

        return new ResultadoImportacao(criados, completados, pulados, erros);
    }

    /// <summary>
    /// Preenche na ficha da base SÓ o que está vazio. Devolve o nome dos campos
    /// preenchidos — vazio quer dizer "nada a fazer", e a linha é pulada sem trilha.
    /// </summary>
    public static IReadOnlyList<string> Completar(Paciente atual, Paciente doArquivo)
    {
        var feitos = new List<string>();

        if (string.IsNullOrWhiteSpace(atual.Documento) && doArquivo.Documento is not null)
        { atual.Documento = doArquivo.Documento; feitos.Add("CPF"); }
        if (string.IsNullOrWhiteSpace(atual.Telefone) && doArquivo.Telefone is not null)
        { atual.Telefone = doArquivo.Telefone; feitos.Add("telefone"); }
        if (atual.DataNascimento is null && doArquivo.DataNascimento is not null)
        { atual.DataNascimento = doArquivo.DataNascimento; feitos.Add("nascimento"); }
        if (string.IsNullOrWhiteSpace(atual.Endereco) && doArquivo.Endereco is not null)
        { atual.Endereco = doArquivo.Endereco; feitos.Add("endereço"); }
        if (string.IsNullOrWhiteSpace(atual.Carteirinha) && doArquivo.Carteirinha is not null)
        { atual.Carteirinha = doArquivo.Carteirinha; feitos.Add("carteirinha"); }
        if (atual.ValidadeCarteirinha is null && doArquivo.ValidadeCarteirinha is not null)
        { atual.ValidadeCarteirinha = doArquivo.ValidadeCarteirinha; feitos.Add("validade"); }
        if (atual.Origem is null && doArquivo.Origem is not null)
        {
            atual.Origem = doArquivo.Origem;
            atual.IndicadoPor ??= doArquivo.IndicadoPor;
            feitos.Add("origem");
        }
        if (string.IsNullOrWhiteSpace(atual.Observacoes) && doArquivo.Observacoes is not null)
        { atual.Observacoes = doArquivo.Observacoes; feitos.Add("observações"); }
        if (atual.ChaveImportacao is null && doArquivo.ChaveImportacao is not null)
        { atual.ChaveImportacao = doArquivo.ChaveImportacao; feitos.Add("chave de importação"); }

        return feitos;
    }

    private static Paciente Retrato(Paciente p) => new()
    {
        Documento = p.Documento, Telefone = p.Telefone, DataNascimento = p.DataNascimento,
        Endereco = p.Endereco, Carteirinha = p.Carteirinha, ValidadeCarteirinha = p.ValidadeCarteirinha,
        Origem = p.Origem, IndicadoPor = p.IndicadoPor, Observacoes = p.Observacoes,
        ChaveImportacao = p.ChaveImportacao
    };

    private static void Restaurar(Paciente p, Paciente antes)
    {
        p.Documento = antes.Documento; p.Telefone = antes.Telefone; p.DataNascimento = antes.DataNascimento;
        p.Endereco = antes.Endereco; p.Carteirinha = antes.Carteirinha; p.ValidadeCarteirinha = antes.ValidadeCarteirinha;
        p.Origem = antes.Origem; p.IndicadoPor = antes.IndicadoPor; p.Observacoes = antes.Observacoes;
        p.ChaveImportacao = antes.ChaveImportacao;
    }

    // ---------------------------------------------------------------- leitura dos campos

    private static readonly string[] FormatosData =
        ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "dd/MM/yy", "d/M/yy"];

    /// <summary>Data em qualquer forma comum; o que não se lê vira AVISO e o campo fica vazio —
    /// recusar a linha por uma data mal digitada deixaria a pessoa fora do sistema.</summary>
    public static DateOnly? LerData(string? texto, string rotulo, List<string> avisos, bool nascimento = false)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var soData = texto.Trim().Split(' ', 'T')[0];
        if (!DateOnly.TryParseExact(soData, FormatosData, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            avisos.Add($"{rotulo} \"{texto}\" não foi entendida e ficou em branco.");
            return null;
        }
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        if (nascimento && (d > hoje || d.Year < 1900))
        {
            avisos.Add($"{rotulo} \"{texto}\" é impossível e ficou em branco.");
            return null;
        }
        return d;
    }

    public static Sexo LerSexo(string? texto, bool colunaMapeada, List<string> avisos)
    {
        var t = SugestorDeMapeamento.Normalizar(texto ?? string.Empty);
        if (t.StartsWith('f') || t == "mulher") return Sexo.Feminino;
        if (t.StartsWith('m') || t.StartsWith('h')) return Sexo.Masculino;
        avisos.Add(colunaMapeada && t.Length > 0
            ? $"Sexo \"{texto}\" não foi entendido — confira na ficha."
            : "Sexo não informado no arquivo — confira na ficha.");
        return Sexo.Masculino;
    }

    public static string? LerTelefone(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        // O arquivo pode trazer dois números na mesma célula; fica o primeiro.
        var primeiro = texto.Split('/', ';', '|')[0].Trim();
        return Cortar(Telefone.Formatar(primeiro), 30);
    }

    /// <summary>Texto livre do sistema anterior para o enum de origem. O que não casa vira
    /// <see cref="OrigemPaciente.Outro"/> com o texto preservado nas observações — o
    /// relatório de origem conta a resposta, e a frase original não se perde.</summary>
    public static (OrigemPaciente? Origem, string? IndicadoPor, string? Observacao) LerOrigem(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return (null, null, null);
        var t = SugestorDeMapeamento.Normalizar(texto);
        if (t.Contains("indic"))
        {
            var partes = texto.Split(':', '-', '–');
            var quem = partes.Length > 1 ? string.Join(" ", partes.Skip(1)).Trim() : null;
            return (OrigemPaciente.Indicacao, string.IsNullOrWhiteSpace(quem) ? null : quem, null);
        }
        if (t.Contains("encaminh") || t.Contains("medic")) return (OrigemPaciente.Encaminhamento, null, null);
        if (t.Contains("instagram") || t.Contains("facebook") || t.Contains("rede") || t.Contains("social"))
            return (OrigemPaciente.RedesSociais, null, null);
        if (t.Contains("google") || t.Contains("internet") || t.Contains("site") || t.Contains("busca"))
            return (OrigemPaciente.Internet, null, null);
        if (t.Contains("fachada") || t.Contains("placa") || t.Contains("passou") || t.Contains("vizinh"))
            return (OrigemPaciente.Fachada, null, null);
        if (t.Contains("conven") || t.Contains("plano") || t.Contains("operadora"))
            return (OrigemPaciente.Convenio, null, null);
        if (t.Contains("campanha") || t.Contains("promo") || t.Contains("recall"))
            return (OrigemPaciente.Campanha, null, null);
        return (OrigemPaciente.Outro, null, $"Origem informada no sistema anterior: {texto.Trim()}");
    }

    private static string? Cortar(string? texto, int max)
        => texto is null ? null : texto.Length <= max ? texto : texto[..max];
}
