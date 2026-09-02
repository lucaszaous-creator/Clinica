using System.Globalization;
using System.Text;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Importa o ZIP DE ARQUIVOS do Smart Clinic para os ARQUIVOS DA FICHA de cada paciente
/// (set/2026 — ver <see cref="PacoteAnexosSmartClinic"/>).
///
/// As regras, na ordem em que doem quando faltam:
///
/// - <b>Cada arquivo acha a ficha pelo id do paciente no sistema anterior</b> — a chave
///   <c>IMPORT:smartclinic:{id_paciente}</c> que a importação da carteira gravou. Quando o
///   id não está no sistema (a duplicata fundida guarda um id só; a ficha que já existia
///   por outro caminho), o NOME resolve — e só quando é ÚNICO no cadastro: casar "Maria
///   Silva" com uma de duas seria pôr a receita na ficha errada, calado.
/// - <b>Idempotente pela chave</b> <c>IMPORT:smartclinic:arquivo:{id_arquivo}</c> (índice
///   único): o mesmo ZIP importado duas vezes não duplica arquivo.
/// - <b>A montagem é a MESMA do anexo pela tela</b> (<see cref="AnexoPacienteService.Montar"/>)
///   — teto, título, data —, e a gravação é em LOTES com uma linha de trilha por lote: 756
///   linhas de auditoria enterrariam a trilha que existe para ser lida.
/// - <b>A data é a do documento, como o sistema anterior a gravou.</b> Ilegível ou futura,
///   entra a de hoje e a observação DIZ — perder o arquivo por causa da data seria a
///   escolha errada, e inventar uma data plausível sem dizer seria a garantia aparente.
/// </summary>
public sealed class ImportacaoAnexosSmartClinicService
{
    private const string Sistema = "smartclinic";
    private const int TamanhoDoLote = 40;

    private readonly IClinicaRepositorio _repo;

    public ImportacaoAnexosSmartClinicService(IClinicaRepositorio repo) => _repo = repo;

    public static string ChaveDe(string idArquivo) => $"IMPORT:{Sistema}:arquivo:{idArquivo.Trim()}";

    private static string ChaveDePaciente(string idPaciente) => $"IMPORT:{Sistema}:{idPaciente.Trim()}";

    // ================================================================== prévia

    public async Task<PreviaAnexosSmartClinic> PreverAsync(
        PacoteAnexosSmartClinic pacote, CancellationToken ct = default)
    {
        var fichas = await _repo.FichasResumidasAsync(ct);
        var porChave = fichas.Where(f => f.ChaveImportacao is not null)
            .GroupBy(f => f.ChaveImportacao!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);
        // O nome só resolve quando é ÚNICO; o homônimo fica como "sem paciente", com o motivo.
        var porNome = fichas
            .GroupBy(f => NormalizarNome(f.Nome), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(f => f.Id).Distinct().ToList(), StringComparer.Ordinal);
        var chaves = await _repo.ChavesDeImportacaoDeAnexosPacienteAsync(ct);

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var linhas = new List<LinhaAnexoPrevia>();
        var pacientes = new HashSet<int>();
        int novos = 0, ja = 0, semPaciente = 0, semArquivo = 0, invalidos = 0;
        long bytesNovos = 0;

        var indice = RegistroCsv.Indice(pacote.Relacao);
        var numero = 1;
        foreach (var linha in pacote.Relacao.Linhas)
        {
            ct.ThrowIfCancellationRequested();
            var r = new RegistroCsv(indice, linha);
            var idArquivo = r["id_arquivo"];
            var nome = r["nome_paciente"] ?? "(sem nome)";
            var idPaciente = r["id_paciente"];
            var tituloCru = r["titulo"];
            var dataCrua = r["data"];

            var titulo = string.IsNullOrWhiteSpace(tituloCru)
                ? $"Arquivo {idArquivo ?? numero.ToString(CultureInfo.InvariantCulture)}"
                : tituloCru.Trim();
            var (data, dataDetalhe) = LerData(dataCrua, hoje);

            LinhaAnexoPrevia Linha(DestinoAnexo destino, string? detalhe, int? pacienteId, string? nomeNoZip)
                => new(numero, idArquivo ?? string.Empty, nome, titulo, data, destino,
                    string.Join(" · ", new[] { detalhe, dataDetalhe }.Where(d => !string.IsNullOrWhiteSpace(d))) is { Length: > 0 } d ? d : null,
                    pacienteId, nomeNoZip, dataCrua);

            if (string.IsNullOrWhiteSpace(idArquivo))
            {
                invalidos++;
                linhas.Add(Linha(DestinoAnexo.Invalido, "linha sem id_arquivo", null, null));
                numero++; continue;
            }
            if (chaves.Contains(ChaveDe(idArquivo)))
            {
                ja++;
                linhas.Add(Linha(DestinoAnexo.JaImportado, "já está no sistema", null, null));
                numero++; continue;
            }

            var arquivo = pacote.Arquivo(idArquivo);
            if (arquivo is null)
            {
                semArquivo++;
                linhas.Add(Linha(DestinoAnexo.SemArquivo, "o índice cita um arquivo que não está no ZIP", null, null));
                numero++; continue;
            }
            if (arquivo.Value.Bytes.Length == 0 || arquivo.Value.Bytes.Length > ProntuarioService.TamanhoMaximoAnexo)
            {
                invalidos++;
                linhas.Add(Linha(DestinoAnexo.Invalido,
                    arquivo.Value.Bytes.Length == 0
                        ? "arquivo vazio no ZIP"
                        : $"arquivo passa de {ProntuarioService.TamanhoMaximoAnexo / (1024 * 1024)} MB",
                    null, arquivo.Value.Nome));
                numero++; continue;
            }

            var pacienteId = ResolverPaciente(idPaciente, nome, porChave, porNome, out var motivo);
            if (pacienteId is null)
            {
                semPaciente++;
                linhas.Add(Linha(DestinoAnexo.SemPaciente, motivo, null, arquivo.Value.Nome));
                numero++; continue;
            }

            novos++;
            bytesNovos += arquivo.Value.Bytes.Length;
            pacientes.Add(pacienteId.Value);
            linhas.Add(Linha(DestinoAnexo.Novo, null, pacienteId, arquivo.Value.Nome));
            numero++;
        }

        var avisos = new List<string>();
        if (semPaciente > 0)
            avisos.Add($"{semPaciente} arquivo(s) de paciente sem ficha aqui: importe o pacote de pacientes do "
                       + "Smart Clinic antes, e importe este ZIP de novo — o que já entrou não duplica.");
        if (pacote.SemLinhaNoIndice.Count > 0)
            avisos.Add($"{pacote.SemLinhaNoIndice.Count} arquivo(s) no ZIP sem linha no índice (não há como saber de "
                       + $"quem são; ficam de fora): {string.Join(", ", pacote.SemLinhaNoIndice.Take(5))}"
                       + (pacote.SemLinhaNoIndice.Count > 5 ? ", …" : ""));
        if (linhas.Any(l => l.Destino == DestinoAnexo.Novo && l.Detalhe is not null))
            avisos.Add($"{linhas.Count(l => l.Destino == DestinoAnexo.Novo && l.Detalhe is not null)} arquivo(s) com data "
                       + "ilegível ou futura no índice entram com a data de hoje, e a observação diz.");

        return new PreviaAnexosSmartClinic(
            linhas, novos, ja, semPaciente, semArquivo, invalidos, pacientes.Count, bytesNovos, avisos);
    }

    // ================================================================== execução

    /// <summary>Grava o que a prévia mostrou, em lotes; cada lote leva uma linha de trilha no MESMO SaveChanges.</summary>
    public async Task<ResultadoAnexosSmartClinic> ExecutarAsync(
        PreviaAnexosSmartClinic previa, PacoteAnexosSmartClinic pacote, string operador,
        IProgress<string>? progresso = null, CancellationToken ct = default)
    {
        var chaves = (await _repo.ChavesDeImportacaoDeAnexosPacienteAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var erros = new List<string>();
        int criados = 0, pulados = 0, semPaciente = 0, noLote = 0;
        var loteDesde = 0;
        var candidatos = previa.Linhas.Where(l => l.Destino == DestinoAnexo.Novo).ToList();

        foreach (var l in candidatos)
        {
            ct.ThrowIfCancellationRequested();
            var chave = ChaveDe(l.IdArquivo);
            if (chaves.Contains(chave)) { pulados++; continue; }
            if (l.PacienteId is not { } pacienteId) { semPaciente++; continue; }
            var arquivo = pacote.Arquivo(l.IdArquivo);
            if (arquivo is null) { semPaciente++; continue; }

            var (anexo, bytes) = AnexoPacienteService.Montar(
                pacienteId, l.Data, l.Titulo,
                NomeDoArquivo(l.Titulo, arquivo.Value.Nome), arquivo.Value.Bytes,
                TipoConteudoDe(arquivo.Value.Nome),
                "Importado do Smart Clinic"
                + (string.IsNullOrWhiteSpace(l.DataOriginal) ? "" : $" · registrado lá em {l.DataOriginal}")
                + (l.Detalhe is null ? "" : $" · {l.Detalhe}"),
                operador, chave);
            await _repo.AdicionarAnexoPacienteAsync(anexo, ct);
            await _repo.AdicionarArquivoAnexoPacienteAsync(bytes, ct);
            chaves.Add(chave);
            noLote++;

            if (noLote >= TamanhoDoLote)
            {
                await FecharLoteAsync(operador, noLote, loteDesde, erros, ct);
                criados += noLote; loteDesde = criados; noLote = 0;
                progresso?.Report($"Arquivos: {criados} de {candidatos.Count} gravados…");
            }
        }
        if (noLote > 0)
        {
            await FecharLoteAsync(operador, noLote, loteDesde, erros, ct);
            criados += noLote;
        }

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = operador,
            Acao = "ImportacaoAnexosSmartClinic",
            Detalhe = $"Arquivos da ficha: {criados} importado(s), {pulados} já importado(s), {semPaciente} sem ficha."
        }, ct);
        await _repo.SalvarAsync(ct);

        return new ResultadoAnexosSmartClinic(criados, pulados, semPaciente, erros);
    }

    private async Task FecharLoteAsync(string operador, int quantos, int desde, List<string> erros, CancellationToken ct)
    {
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = operador,
            Acao = "AnexoFichaImportado",
            Detalhe = $"{quantos} arquivo(s) do Smart Clinic gravado(s) na ficha (lote a partir do nº {desde + 1})"
        }, ct);
        try
        {
            await _repo.SalvarAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Um lote que falha derruba o lote inteiro (é uma transação) e a mensagem diz
            // qual; a chave de importação faz a segunda rodada retomar de onde parou.
            erros.Add($"Lote de arquivos a partir do nº {desde + 1}: {ex.Message}");
            throw;
        }
    }

    // ================================================================== ajudantes

    private static int? ResolverPaciente(
        string? idPaciente, string nome,
        IReadOnlyDictionary<string, int> porChave, IReadOnlyDictionary<string, List<int>> porNome,
        out string? motivo)
    {
        motivo = null;
        if (!string.IsNullOrWhiteSpace(idPaciente) && porChave.TryGetValue(ChaveDePaciente(idPaciente), out var id))
            return id;

        var chaveNome = NormalizarNome(nome);
        if (chaveNome.Length > 0 && porNome.TryGetValue(chaveNome, out var ids))
        {
            if (ids.Count == 1) return ids[0];
            motivo = $"há {ids.Count} fichas com este nome — não dá para saber qual; o id de lá não está no sistema";
            return null;
        }
        motivo = "ficha não encontrada (nem pelo id do sistema anterior, nem pelo nome)";
        return null;
    }

    /// <summary>"Maria  da SILVA" e "MARIA DA SILVA" são a mesma pessoa digitada por duas recepcionistas.</summary>
    public static string NormalizarNome(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return string.Empty;
        var semAcento = nome.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(semAcento.Length);
        foreach (var c in semAcento)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return string.Join(' ', sb.ToString().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// A data como o sistema anterior a gravou ("2024-10-26 10:53:19"). Ilegível ou futura
    /// vira HOJE com o motivo escrito — o arquivo entra; a data não pode ser o que o impede.
    /// </summary>
    public static (DateOnly Data, string? Detalhe) LerData(string? texto, DateOnly hoje)
    {
        if (string.IsNullOrWhiteSpace(texto)) return (hoje, "sem data no índice; usada a de hoje");
        var t = texto.Trim();
        string[] formatos = ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd", "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy"];
        if (!DateTime.TryParseExact(t, formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return (hoje, $"data ilegível no índice ({t}); usada a de hoje");
        var data = DateOnly.FromDateTime(d);
        if (data > hoje) return (hoje, $"data futura no índice ({t}); usada a de hoje");
        if (data.Year < 1900) return (hoje, $"data implausível no índice ({t}); usada a de hoje");
        return (data, null);
    }

    /// <summary>"Receita #164001527" + "abc.pdf" → "receita-164001527.pdf": o nome que a pessoa reconhece, com a extensão de verdade.</summary>
    public static string NomeDoArquivo(string titulo, string nomeNoZip)
    {
        var extensao = Path.GetExtension(nomeNoZip);
        var sb = new StringBuilder();
        foreach (var c in NormalizarNome(titulo).ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        var slug = string.Join('-', sb.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (slug.Length == 0) slug = Path.GetFileNameWithoutExtension(nomeNoZip);
        if (slug.Length > 80) slug = slug[..80];
        return slug + (string.IsNullOrEmpty(extensao) ? ".pdf" : extensao.ToLowerInvariant());
    }

    private static string? TipoConteudoDe(string nome) => Path.GetExtension(nome).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        _ => null
    };
}
