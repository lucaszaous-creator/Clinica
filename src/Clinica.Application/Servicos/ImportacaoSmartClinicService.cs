using System.Globalization;
using System.Text;
using System.Text.Json;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// A importação do PACOTE do Smart Clinic (set/2026) — a carteira, o prontuário em texto,
/// os horários futuros da agenda antiga e o que não tem campo aqui. A decisão da direção
/// foi "não perder NADA", e cada arquivo tem um destino escrito:
/// <list type="bullet">
/// <item><b>pacientes.csv</b> → a ficha, pelo <see cref="ImportacaoPacientesService"/>
/// (idempotente pela chave; quem existe é completado). As colunas sem campo (e-mail, RG,
/// profissão, nome da mãe…) vão para as OBSERVAÇÕES da ficha, com rótulo — e o histórico
/// de visitas da agenda antiga também.</item>
/// <item><b>pos_operatorio, ficha_soap, prescricao, ficha_clinica, consulta_multi,
/// prontuario_personalizado, ficha_pre_consulta, ficha_pedi</b> → uma <see cref="Evolucao"/>
/// por registro, com a data de lá, o texto convertido de HTML, o autor como o sistema
/// antigo o gravou (<c>CriadoPor</c>) e, quando o nome casa com um profissional daqui,
/// o vínculo. É registro clínico como qualquer outro: não se apaga, entra na guarda de
/// 20 anos e na exportação.</item>
/// <item><b>agenda.csv</b> → os horários de HOJE em diante viram <see cref="Agendamento"/>
/// (a clínica troca de sistema com a agenda das próximas semanas já marcada). Os passados
/// NÃO viram sessão: marcá-los como "realizado" sem evolução ligada inundaria a dívida de
/// prontuário do médico com milhares de sessões antigas, e criar atendimento inventaria
/// guia. Eles ficam como histórico legível na ficha ("Visitas no sistema anterior").</item>
/// <item><b>contratante*.csv, exame.csv</b> → não são dado de paciente (a clínica, seus
/// usuários com senha, o catálogo de nomes de exame): não entram, e a prévia diz isso.</item>
/// </list>
/// Tudo idempotente: cada registro carrega <c>IMPORT:smartclinic:{arquivo}:{id}</c>, e o
/// pacote importado duas vezes só grava o que faltou.
/// </summary>
public sealed class ImportacaoSmartClinicService
{
    public const string Sistema = ImportacaoPacientesService.SistemaSmartClinic;

    /// <summary>Gravações por SaveChanges na execução do prontuário: 4.287 registros num
    /// banco remoto, um a um, seriam minutos; em lotes são segundos.</summary>
    public const int TamanhoDoLote = 200;

    private readonly IClinicaRepositorio _repo;
    private readonly ImportacaoPacientesService _pacientes;

    public ImportacaoSmartClinicService(IClinicaRepositorio repo, ImportacaoPacientesService pacientes)
    {
        _repo = repo;
        _pacientes = pacientes;
    }

    public static string ChaveDe(string arquivo, string id)
        => $"IMPORT:{Sistema}:{Path.GetFileNameWithoutExtension(arquivo)}:{id.Trim()}";

    // ================================================================== prévia

    public async Task<PreviaSmartClinic> PreverAsync(
        PacoteSmartClinic pacote,
        IReadOnlyDictionary<string, ConvenioCadastro> convenios,
        DateOnly hoje,
        CancellationToken ct = default)
    {
        var pacientes = pacote.Tabela(PacoteSmartClinic.Pacientes)
            ?? throw new ArgumentException("O pacote não tem pacientes.csv.");
        var mapa = SugestorDeMapeamento.Sugerir(pacientes.Colunas);
        if (mapa.ColunaDe(CampoImportacao.IdOrigem) is null || mapa.ColunaDe(CampoImportacao.Nome) is null)
            throw new ArgumentException("O pacientes.csv não tem as colunas id_paciente e nome que o Smart Clinic exporta.");

        var previaPacientes = await _pacientes.PreverAsync(
            pacientes, mapa, convenios, Sistema, ct, colunasSemCampoNasObservacoes: true);

        var avisos = new List<string>();
        foreach (var (arquivo, motivo) in pacote.Ignorados)
            avisos.Add($"{arquivo} não foi lido: {motivo}");

        var idsConhecidos = new HashSet<string>(StringComparer.Ordinal);
        var colId = mapa.ColunaDe(CampoImportacao.IdOrigem)!.Value;
        foreach (var l in pacientes.Linhas)
            if (colId < l.Length && !string.IsNullOrWhiteSpace(l[colId])) idsConhecidos.Add(l[colId].Trim());

        var profissionais = await _repo.ProfissionaisAsync(ct);
        var autoresReconhecidos = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
        var autoresSemCadastro = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);

        // ---- prontuário ----
        var chavesEvolucoes = await _repo.ChavesDeImportacaoDeEvolucoesAsync(ct);
        var planos = new List<EvolucaoPlanejada>();
        var resumos = new List<ResumoArquivoClinico>();
        foreach (var arquivo in PacoteSmartClinic.ArquivosDeProntuario)
        {
            var tabela = pacote.Tabela(arquivo);
            if (tabela is null) continue;

            int novos = 0, ja = 0, semPaciente = 0, vazios = 0;
            var pacientesDoArquivo = new HashSet<string>(StringComparer.Ordinal);
            var indice = RegistroCsv.Indice(tabela);
            foreach (var linha in tabela.Linhas)
            {
                var r = new RegistroCsv(indice, linha);
                var id = linha.Length > 0 ? linha[0].Trim() : string.Empty;
                var idPaciente = r["id_paciente"];
                if (id.Length == 0 || idPaciente is null) { vazios++; continue; }
                pacientesDoArquivo.Add(idPaciente);

                var plano = ComposicaoSmartClinic.Compor(arquivo, r);
                if (plano is null || !ProntuarioService.TemRegistro(plano)) { vazios++; continue; }

                var autor = ComposicaoSmartClinic.Autor(r);
                if (autor is not null)
                {
                    var profissional = Casar(profissionais, autor.Nome);
                    if (profissional is not null) { plano.ProfissionalId = profissional.Id; autoresReconhecidos.Add(autor.Nome); }
                    else autoresSemCadastro.Add(autor.Nome);
                    plano.CriadoPor = autor.Rotulo;
                }
                else plano.CriadoPor = "Smart Clinic";

                var chave = ChaveDe(arquivo, id);
                plano.ChaveImportacao = chave;
                if (chavesEvolucoes.Contains(chave)) { ja++; continue; }
                if (!idsConhecidos.Contains(idPaciente)) { semPaciente++; continue; }

                planos.Add(new EvolucaoPlanejada(arquivo, chave, idPaciente, plano, autor));
                novos++;
            }
            resumos.Add(new ResumoArquivoClinico(
                arquivo, ComposicaoSmartClinic.Rotulo(arquivo), tabela.Linhas.Count,
                pacientesDoArquivo.Count, novos, ja, semPaciente, vazios));
        }

        // ---- agenda ----
        var agenda = pacote.Tabela(PacoteSmartClinic.Agenda);
        var planosAgenda = new List<AgendamentoPlanejado>();
        var historicoPorPaciente = new Dictionary<string, List<(DateTime Quando, string Texto)>>(StringComparer.Ordinal);
        int futuros = 0, futurosNovos = 0, futurosJa = 0, passados = 0;
        var profReconhecidos = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
        var profSemCadastro = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
        if (agenda is not null)
        {
            var chavesAgenda = await _repo.ChavesDeImportacaoDeAgendamentosAsync(ct);
            var indice = RegistroCsv.Indice(agenda);
            foreach (var linha in agenda.Linhas)
            {
                var r = new RegistroCsv(indice, linha);
                var id = linha.Length > 0 ? linha[0].Trim() : string.Empty;
                var idPaciente = r["id_paciente"];
                var inicio = LerDataHora(r["inicio"]);
                if (id.Length == 0 || idPaciente is null || inicio is null) continue;

                var procedimento = r["procedimento"] ?? "Horário";
                var profissionalNome = r["profissional"];

                if (DateOnly.FromDateTime(inicio.Value) < hoje)
                {
                    passados++;
                    if (!historicoPorPaciente.TryGetValue(idPaciente, out var lista))
                        historicoPorPaciente[idPaciente] = lista = [];
                    lista.Add((inicio.Value,
                        $"{inicio.Value:dd/MM/yyyy HH:mm} · {procedimento}"
                        + (profissionalNome is null ? "" : $" · {profissionalNome}")
                        + (r["cirurgia"] is { } cir ? $" · {cir.Trim()}" : "")));
                    continue;
                }

                futuros++;
                var chave = ChaveDe(PacoteSmartClinic.Agenda, id);
                if (chavesAgenda.Contains(chave)) { futurosJa++; continue; }
                if (!idsConhecidos.Contains(idPaciente)) continue;

                Profissional? profissional = null;
                if (profissionalNome is not null)
                {
                    profissional = Casar(profissionais, profissionalNome);
                    if (profissional is not null) profReconhecidos.Add(profissionalNome);
                    else profSemCadastro.Add(profissionalNome);
                }
                var fim = LerDataHora(r["fim"]);
                var duracao = fim is { } f && f > inicio ? (int)(f - inicio.Value).TotalMinutes : (int?)null;

                planosAgenda.Add(new AgendamentoPlanejado(chave, idPaciente, new Agendamento
                {
                    DataHora = inicio.Value,
                    ModalidadePrevista = ModalidadeAtendimento.Consulta,
                    ModalidadeCodigo = ModalidadeAtendimento.Consulta.ToString(),
                    Status = StatusAgendamento.Agendado,
                    Origem = OrigemAgendamento.Manual,
                    ProfissionalId = profissional?.Id,
                    DuracaoMinutos = duracao,
                    Observacoes = Cortar($"Importado do Smart Clinic · {procedimento}"
                                         + (profissional is null && profissionalNome is not null ? $" · {profissionalNome}" : ""), 500),
                    ChaveImportacao = chave
                }, profissionalNome));
                futurosNovos++;
            }
        }

        // O histórico de visitas entra nas observações da ficha (Criar ou Completar).
        foreach (var l in previaPacientes.Linhas)
        {
            if (l.Ficha is null || l.Chave is null) continue;
            var idAntigo = l.Chave[(l.Chave.LastIndexOf(':') + 1)..];
            if (!historicoPorPaciente.TryGetValue(idAntigo, out var visitas)) continue;
            var bloco = $"— Visitas no sistema anterior ({visitas.Count}) —\n"
                        + string.Join("\n", visitas.OrderBy(v => v.Quando).Select(v => v.Texto));
            l.Ficha.Observacoes = string.IsNullOrWhiteSpace(l.Ficha.Observacoes)
                ? bloco : $"{l.Ficha.Observacoes}\n\n{bloco}";
        }

        if (profSemCadastro.Count > 0)
            avisos.Add("Profissionais da agenda antiga sem cadastro aqui (os horários entram sem profissional, com o nome "
                       + $"na observação): {string.Join(", ", profSemCadastro)}. Cadastre em Equipe e importe de novo para vincular.");
        if (autoresSemCadastro.Count > 0)
            avisos.Add("Autores do prontuário antigo sem cadastro aqui (o registro guarda o nome e o conselho, sem vínculo): "
                       + string.Join(", ", autoresSemCadastro) + ".");
        if (pacote.Tem(PacoteSmartClinic.Usuarios) || pacote.Tem(PacoteSmartClinic.Exames))
            avisos.Add("contratante.csv, contratante_usuario.csv e exame.csv não são dado de paciente (a clínica, os usuários "
                       + "com senha e o catálogo de nomes de exame) e não entram — usuários se criam em Acessos.");

        return new PreviaSmartClinic(
            previaPacientes, resumos,
            new ResumoAgendaAnterior(futuros, futurosNovos, futurosJa, passados, historicoPorPaciente.Count,
                profReconhecidos.ToList(), profSemCadastro.ToList()),
            autoresReconhecidos.ToList(), autoresSemCadastro.ToList(), avisos)
        {
            Evolucoes = planos,
            Agendamentos = planosAgenda
        };
    }

    // ================================================================== execução

    /// <summary>
    /// Grava o que a prévia mostrou: primeiro as fichas (é delas que sai o id de cada
    /// paciente), depois o prontuário em lotes, depois a agenda futura. Cada lote leva uma
    /// linha de auditoria no MESMO SaveChanges; a procedência de cada registro está nele
    /// (a chave e o autor), e uma linha de trilha por evolução — milhares — enterraria a
    /// trilha que existe para ser lida.
    /// </summary>
    public async Task<ResultadoSmartClinic> ExecutarAsync(
        PreviaSmartClinic previa, string operador, IProgress<string>? progresso = null,
        CancellationToken ct = default)
    {
        progresso?.Report("Gravando as fichas…");
        var pacientes = await _pacientes.ExecutarAsync(previa.Pacientes, operador, ct);

        // id antigo → id daqui, por LINHA do arquivo: é o que cobre também a duplicata
        // fundida (dois ids antigos, uma ficha) e a ficha que já existia.
        var idPorAntigo = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var l in previa.Pacientes.Linhas)
        {
            if (l.Chave is null) continue;
            var idAntigo = l.Chave[(l.Chave.LastIndexOf(':') + 1)..];
            if (pacientes.IdPorLinha.TryGetValue(l.Numero, out var id)) idPorAntigo[idAntigo] = id;
        }

        var erros = new List<string>();

        // A Equipe pode ser cadastrada DEPOIS: o que entrou sem vínculo numa rodada
        // anterior ganha o vínculo agora, pelo nome que o registro guarda.
        progresso?.Report("Vinculando a Equipe aos registros importados…");
        var revinculados = await RevincularAsync(ct);

        // ---- prontuário, em lotes ----
        var chavesEvolucoes = (await _repo.ChavesDeImportacaoDeEvolucoesAsync(ct)).ToHashSet(StringComparer.Ordinal);
        int criadas = 0, puladas = 0, semPaciente = 0, noLote = 0;
        var loteDesde = 0;
        foreach (var plano in previa.Evolucoes)
        {
            ct.ThrowIfCancellationRequested();
            if (chavesEvolucoes.Contains(plano.Chave)) { puladas++; continue; }
            if (!idPorAntigo.TryGetValue(plano.IdPacienteAntigo, out var pacienteId)) { semPaciente++; continue; }

            plano.Evolucao.PacienteId = pacienteId;
            await _repo.AdicionarEvolucaoAsync(plano.Evolucao, ct);
            chavesEvolucoes.Add(plano.Chave);
            noLote++;
            if (noLote >= TamanhoDoLote)
            {
                await FecharLoteAsync(operador, "ProntuarioImportado", noLote, loteDesde, criadas, erros, ct);
                criadas += noLote; loteDesde = criadas; noLote = 0;
                progresso?.Report($"Prontuário: {criadas} de {previa.Evolucoes.Count} registros gravados…");
            }
        }
        if (noLote > 0)
        {
            await FecharLoteAsync(operador, "ProntuarioImportado", noLote, loteDesde, criadas, erros, ct);
            criadas += noLote;
        }

        // ---- agenda futura ----
        progresso?.Report("Gravando a agenda das próximas semanas…");
        var chavesAgenda = (await _repo.ChavesDeImportacaoDeAgendamentosAsync(ct)).ToHashSet(StringComparer.Ordinal);
        int agCriados = 0, agPulados = 0, agSemPaciente = 0;
        noLote = 0;
        foreach (var plano in previa.Agendamentos)
        {
            ct.ThrowIfCancellationRequested();
            if (chavesAgenda.Contains(plano.Chave)) { agPulados++; continue; }
            if (!idPorAntigo.TryGetValue(plano.IdPacienteAntigo, out var pacienteId)) { agSemPaciente++; continue; }

            plano.Agendamento.PacienteId = pacienteId;
            plano.Agendamento.CriadoPor = Cortar(operador, 80);
            plano.Agendamento.CriadoEm = DateTime.Now;
            await _repo.AdicionarAgendamentoAsync(plano.Agendamento, ct);
            chavesAgenda.Add(plano.Chave);
            noLote++;
            if (noLote >= TamanhoDoLote)
            {
                await FecharLoteAsync(operador, "AgendaImportada", noLote, agCriados, agCriados, erros, ct);
                agCriados += noLote; noLote = 0;
            }
        }
        if (noLote > 0)
        {
            await FecharLoteAsync(operador, "AgendaImportada", noLote, agCriados, agCriados, erros, ct);
            agCriados += noLote;
        }

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = operador,
            Acao = "ImportacaoSmartClinic",
            Detalhe = $"Fichas: {pacientes.Criados} nova(s), {pacientes.Completados} completada(s), {pacientes.Pulados} já existente(s). "
                      + $"Prontuário: {criadas} registro(s) importado(s), {puladas} já importado(s), {semPaciente} sem ficha. "
                      + $"Agenda futura: {agCriados} horário(s), {agPulados} já importado(s). {erros.Count} erro(s)."
        }, ct);
        await _repo.SalvarAsync(ct);

        return new ResultadoSmartClinic(pacientes, criadas, puladas, semPaciente, agCriados, agPulados, agSemPaciente, erros)
        {
            Revinculados = revinculados
        };
    }

    /// <summary>
    /// Registros importados sem vínculo × Equipe de hoje. A evolução guarda o autor em
    /// <c>CriadoPor</c> ("Nome (CRM 123/RJ)"); o horário guarda o nome no fim da observação.
    /// Quem casar agora ganha o <c>ProfissionalId</c> — em lote, um UPDATE por profissional.
    /// </summary>
    public async Task<int> RevincularAsync(CancellationToken ct = default)
    {
        var profissionais = await _repo.ProfissionaisAsync(ct);
        if (profissionais.Count == 0) return 0;
        var total = 0;

        var evolucoes = await _repo.EvolucoesImportadasSemProfissionalAsync(ct);
        foreach (var grupo in evolucoes
                     .Select(e => (e.Id, Profissional: Casar(profissionais, NomeDoAutor(e.Texto))))
                     .Where(x => x.Profissional is not null)
                     .GroupBy(x => x.Profissional!.Id))
        {
            var ids = grupo.Select(x => x.Id).ToList();
            await _repo.VincularProfissionalEmEvolucoesAsync(ids, grupo.Key, ct);
            total += ids.Count;
        }

        var agendamentos = await _repo.AgendamentosImportadosSemProfissionalAsync(ct);
        foreach (var grupo in agendamentos
                     .Select(a => (a.Id, Profissional: Casar(profissionais, NomeDoProfissionalNaObservacao(a.Texto))))
                     .Where(x => x.Profissional is not null)
                     .GroupBy(x => x.Profissional!.Id))
        {
            var ids = grupo.Select(x => x.Id).ToList();
            await _repo.VincularProfissionalEmAgendamentosAsync(ids, grupo.Key, ct);
            total += ids.Count;
        }
        return total;
    }

    /// <summary>"Ana Autora (CRM 123456/RJ)" → "Ana Autora"; "Smart Clinic" (sem autor) → vazio.</summary>
    public static string NomeDoAutor(string? criadoPor)
    {
        if (string.IsNullOrWhiteSpace(criadoPor) || criadoPor == "Smart Clinic") return string.Empty;
        var i = criadoPor.IndexOf(" (", StringComparison.Ordinal);
        return (i > 0 ? criadoPor[..i] : criadoPor).Trim();
    }

    /// <summary>"Importado do Smart Clinic · Consulta · Nome" → "Nome"; sem o terceiro trecho → vazio.</summary>
    public static string NomeDoProfissionalNaObservacao(string? observacoes)
    {
        if (string.IsNullOrWhiteSpace(observacoes)) return string.Empty;
        var partes = observacoes.Split(" · ");
        return partes.Length >= 3 ? partes[^1].Trim() : string.Empty;
    }

    private async Task FecharLoteAsync(
        string operador, string acao, int quantos, int desde, int ate, List<string> erros, CancellationToken ct)
    {
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = operador,
            Acao = acao,
            Detalhe = $"{quantos} registro(s) do Smart Clinic gravado(s) (lote a partir do nº {desde + 1})"
        }, ct);
        try
        {
            await _repo.SalvarAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Um lote que falha derruba o lote inteiro (é uma transação) e a mensagem diz
            // qual; a chave de importação faz a segunda rodada retomar de onde parou.
            erros.Add($"Lote de {acao} a partir do nº {desde + 1}: {ex.Message}");
            throw;
        }
    }

    // ================================================================== ajudantes

    /// <summary>Nome do sistema antigo × cadastro daqui: igual depois de normalizar, ou um
    /// contido no outro ("Dr. Fulano" × "Fulano de Tal") com pelo menos seis letras.</summary>
    public static Profissional? Casar(IReadOnlyList<Profissional> profissionais, string nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return null;
        var alvo = SugestorDeMapeamento.Normalizar(nome.Replace("Dr.", "").Replace("Dra.", "").Replace("Dr ", "").Replace("Dra ", ""));
        if (alvo.Length < 2) return null;
        var exato = profissionais.FirstOrDefault(p => SugestorDeMapeamento.Normalizar(p.Nome) == alvo
                                                      || (p.NomeCurto is not null && SugestorDeMapeamento.Normalizar(p.NomeCurto) == alvo));
        if (exato is not null) return exato;
        if (alvo.Length < 6) return null;
        return profissionais.FirstOrDefault(p =>
        {
            var n = SugestorDeMapeamento.Normalizar(p.Nome);
            return n.Length >= 6 && (n.Contains(alvo) || alvo.Contains(n));
        });
    }

    public static DateTime? LerDataHora(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = texto.Trim();
        if (t.All(c => c == '0' || c == '-' || c == ':' || c == ' ')) return null;
        string[] formatos = ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy"];
        return DateTime.TryParseExact(t, formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? DateTime.SpecifyKind(d, DateTimeKind.Unspecified)
            : null;
    }

    private static string? Cortar(string? texto, int max)
        => texto is null ? null : texto.Length <= max ? texto : texto[..max];
}

/// <summary>
/// Como cada arquivo de prontuário do Smart Clinic vira uma <see cref="Evolucao"/>. Puro,
/// para o teste alcançar: recebe a linha, devolve a evolução SEM paciente (o id daqui é
/// resolvido na execução). O HTML vira texto; nada é cortado — as colunas longas são
/// <c>text</c> desde a migration desta parcela.
/// </summary>
public static class ComposicaoSmartClinic
{
    /// <summary>Colunas que são estrutura do sistema antigo, não conteúdo — nunca viram texto.</summary>
    private static readonly HashSet<string> Estruturais = new(StringComparer.OrdinalIgnoreCase)
    {
        "id_contratante_usuario", "id_paciente", "id_contratante", "fl_status", "data", "data_criacao",
        "data_prontuario", "id_agenda", "data_visualizacao", "dados_medicos", "id_migracao",
        "url_certificado_digital", "created_at", "data_sistema", "estrutura", "versao", "historico",
        "galeria", "rec_esp", "campos_concatenados", "id_ficha_personalizada"
    };

    public static string Rotulo(string arquivo) => arquivo switch
    {
        PacoteSmartClinic.PosOperatorio => "Evoluções (pós-operatório)",
        PacoteSmartClinic.FichaSoap => "Fichas S-O-A-P",
        PacoteSmartClinic.Prescricao => "Prescrições",
        PacoteSmartClinic.FichaClinica => "Anamneses (ficha clínica)",
        PacoteSmartClinic.ConsultaMulti => "Consultas (formulário)",
        PacoteSmartClinic.ProntuarioPersonalizado => "Fichas personalizadas",
        PacoteSmartClinic.FichaPreConsulta => "Pré-consultas",
        PacoteSmartClinic.FichaPedi => "Fichas pediátricas/obstétricas",
        _ => arquivo
    };

    public static Evolucao? Compor(string arquivo, RegistroCsv r)
    {
        var data = ImportacaoSmartClinicService.LerDataHora(r["data_prontuario"])
                   ?? ImportacaoSmartClinicService.LerDataHora(r["data"]);
        if (data is null) return null;
        var criadoEm = ImportacaoSmartClinicService.LerDataHora(r["data_criacao"]) ?? data.Value;

        var e = new Evolucao { Data = DateOnly.FromDateTime(data.Value), CriadoEm = criadoEm };
        switch (arquivo)
        {
            case PacoteSmartClinic.PosOperatorio:
                e.TextoEvolucao = HtmlParaTexto.Converter(r["anamnese"]);
                e.ExameFisico = HtmlParaTexto.Converter(r["exame_fisico"]);
                e.Conduta = HtmlParaTexto.Converter(r["conduta"]);
                e.RetornoSugeridoNota = Cortar(HtmlParaTexto.Converter(r["retorno"]), 300);
                break;
            case PacoteSmartClinic.FichaSoap:
                e.HistoriaDoencaAtual = HtmlParaTexto.Converter(r["subjetivo"]);
                e.ExameFisico = Juntar(HtmlParaTexto.Converter(r["objetivo"]), HtmlParaTexto.Converter(r["exame"]));
                var avaliacao = HtmlParaTexto.Converter(r["avaliacao"]);
                if (avaliacao is { Length: <= 1000 }) e.HipoteseDiagnostica = avaliacao;
                else if (avaliacao is not null) e.TextoEvolucao = $"Avaliação:\n{avaliacao}";
                e.Conduta = HtmlParaTexto.Converter(r["plano"]);
                break;
            case PacoteSmartClinic.Prescricao:
                var prescricao = HtmlParaTexto.Converter(r["prescricao"]);
                e.Conduta = prescricao is null ? null : $"Prescrição registrada no Smart Clinic:\n{prescricao}";
                break;
            case PacoteSmartClinic.FichaClinica:
                ComporFichaClinica(r, e);
                break;
            case PacoteSmartClinic.ConsultaMulti:
                e.TextoEvolucao = Juntar(r["titulo"] is { } t ? $"{t} (formulário do Smart Clinic)" : null,
                    TextoDoFormulario(r["conteudo"]));
                break;
            case PacoteSmartClinic.ProntuarioPersonalizado:
                e.TextoEvolucao = Juntar(r["titulo"], r["campos_concatenados"]?.Replace("\r\n", "\n").Trim()
                                                      ?? HtmlParaTexto.Converter(r["prontuario"]));
                break;
            case PacoteSmartClinic.FichaPreConsulta:
                e.ExameFisico = SinaisVitais(r);
                e.TextoEvolucao = Juntar(r["resultado"], HtmlParaTexto.Converter(r["obs"]));
                break;
            default:
                e.TextoEvolucao = ColunasComoTexto(r, ignorar: new HashSet<string>());
                break;
        }

        var status = r["fl_status"];
        if (status is not null && !string.Equals(status, "PR", StringComparison.OrdinalIgnoreCase))
            e.TextoEvolucao = Juntar(e.TextoEvolucao, $"Situação no sistema anterior: {status}");
        return e;
    }

    /// <summary>O autor, de <c>dados_medicos</c> (JSON com nome e conselho).</summary>
    public static AutorAnterior? Autor(RegistroCsv r)
    {
        var json = r["dados_medicos"];
        if (json is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(Sanear(json));
            var raiz = doc.RootElement;
            var nome = Texto(raiz, "nome");
            if (string.IsNullOrWhiteSpace(nome)) return null;
            var sigla = Texto(raiz, "sigla_conselho_profissional");
            var numero = Texto(raiz, "numero_conselho_profissional");
            var uf = Texto(raiz, "sigla_estado");
            var conselho = string.IsNullOrWhiteSpace(sigla) || string.IsNullOrWhiteSpace(numero)
                ? null
                : $"{sigla} {numero}" + (string.IsNullOrWhiteSpace(uf) ? "" : $"/{uf}");
            return new AutorAnterior(nome.Trim(), conselho);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>As respostas do formulário (consulta_multi.conteudo): "rótulo: resposta"
    /// por item respondido. Nulo quando o JSON não se lê — e aí a prévia conta o registro
    /// como vazio em vez de inventar texto.</summary>
    public static string? TextoDoFormulario(string? json)
    {
        if (json is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(Sanear(json));
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var linhas = new List<string>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var rotulo = Texto(item, "label");
                if (!item.TryGetProperty("userData", out var respostas) || respostas.ValueKind != JsonValueKind.Array) continue;
                var valor = string.Join(" ", respostas.EnumerateArray()
                    .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString())
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v!.Trim()));
                if (valor.Length == 0) continue;
                linhas.Add(string.IsNullOrWhiteSpace(rotulo) ? valor : $"{HtmlParaTexto.Converter(rotulo)}: {valor}");
            }
            return linhas.Count == 0 ? null : string.Join("\n", linhas);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void ComporFichaClinica(RegistroCsv r, Evolucao e)
    {
        var marcados = new List<string>();
        var textos = new List<string>();
        foreach (var coluna in r.Colunas)
        {
            if (Estruturais.Contains(coluna) || coluna.StartsWith("id_", StringComparison.OrdinalIgnoreCase)) continue;
            var valor = r[coluna];
            if (valor is null) continue;
            if (coluna.StartsWith("fl_", StringComparison.OrdinalIgnoreCase))
            {
                if (valor == "1") marcados.Add(RotuloDeFlag(coluna));
                continue;
            }
            if (coluna is "achados" or "tratamento") continue;
            var t = HtmlParaTexto.Converter(valor);
            if (t is not null) textos.Add($"{RotuloDeFlag(coluna)}: {t}");
        }
        var partes = new List<string?>();
        if (marcados.Count > 0) partes.Add("Marcados na ficha: " + string.Join("; ", marcados) + ".");
        partes.AddRange(textos);
        e.HistoriaDoencaAtual = Juntar(partes.ToArray());
        e.ExameFisico = HtmlParaTexto.Converter(r["achados"]);
        e.Conduta = HtmlParaTexto.Converter(r["tratamento"]);
    }

    private static string? SinaisVitais(RegistroCsv r)
    {
        var partes = new List<string>();
        if (r["pasis"] is { } s && r["padia"] is { } d) partes.Add($"PA {s}/{d}");
        else if (r["pasis"] is { } s2) partes.Add($"PA sistólica {s2}");
        if (r["freq_cardio"] is { } fc) partes.Add($"FC {fc}");
        if (r["freq_respi"] is { } fr) partes.Add($"FR {fr}");
        if (r["temperatura"] is { } t) partes.Add($"Temp {t}");
        if (r["saturacao"] is { } sat) partes.Add($"SpO2 {sat}");
        if (r["glicemia"] is { } g) partes.Add($"Glicemia {g}");
        if (r["peso"] is { } p) partes.Add($"Peso {p}");
        if (r["altura"] is { } a) partes.Add($"Altura {a}");
        return partes.Count == 0 ? null : string.Join(" · ", partes);
    }

    private static string? ColunasComoTexto(RegistroCsv r, IReadOnlySet<string> ignorar)
    {
        var linhas = new List<string>();
        foreach (var coluna in r.Colunas)
        {
            if (Estruturais.Contains(coluna) || ignorar.Contains(coluna) || coluna.StartsWith("id_", StringComparison.OrdinalIgnoreCase)) continue;
            var v = HtmlParaTexto.Converter(r[coluna]);
            if (v is not null) linhas.Add($"{RotuloDeFlag(coluna)}: {v}");
        }
        return linhas.Count == 0 ? null : string.Join("\n", linhas);
    }

    private static readonly Dictionary<string, string> Palavras = new(StringComparer.OrdinalIgnoreCase)
    {
        ["has"] = "HAS", ["avc"] = "AVC", ["aas"] = "AAS", ["aco"] = "ACO", ["cir"] = "cirurgia", ["mat"] = "material",
        ["peq"] = "pequena", ["med"] = "média", ["gde"] = "grande", ["lipo"] = "lipoaspiração", ["qtd"] = "quantidade",
        ["palpebra"] = "pálpebra", ["palpebras"] = "pálpebras", ["cabeca"] = "cabeça", ["pescoco"] = "pescoço",
        ["hernia"] = "hérnia", ["braco"] = "braço", ["abdomen"] = "abdômen", ["nausea"] = "náusea", ["vomito"] = "vômito",
        ["ulcera"] = "úlcera", ["calculos"] = "cálculos", ["secrecao"] = "secreção", ["infeccao"] = "infecção",
        ["hipertensao"] = "hipertensão", ["gluteo"] = "glúteo", ["protese"] = "prótese", ["ginkgobiloba"] = "Ginkgo biloba",
        ["polivitaminicos"] = "polivitamínicos", ["antihipertensivo"] = "anti-hipertensivo", ["hormonios"] = "hormônios",
        ["reposicao"] = "reposição", ["coagulacao"] = "coagulação", ["depressao"] = "depressão", ["doencas"] = "doenças",
        ["lesao"] = "lesão", ["hematopeietico"] = "hematopoiético", ["pelo"] = "pele e", ["cesarias"] = "cesáreas",
        ["gestacoes"] = "gestações", ["queixa"] = "queixa", ["outra"] = "outra", ["melitus"] = "mellitus", ["diabete"] = "diabetes",
        ["desc"] = "", ["fl"] = "", ["familiar"] = "(familiar)", ["tireopatia"] = "tireopatia", ["tosse"] = "tosse/",
    };

    /// <summary>"fl_cir_lipo_peq" → "Cirurgia lipoaspiração pequena"; "desc_alergias" → "Alergias".</summary>
    public static string RotuloDeFlag(string coluna)
    {
        var palavras = coluna.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => Palavras.TryGetValue(p, out var t) ? t : p)
            .Where(p => p.Length > 0)
            .ToList();
        var texto = string.Join(" ", palavras).Replace("/ ", "/").Trim();
        return texto.Length == 0 ? coluna : char.ToUpperInvariant(texto[0]) + texto[1..];
    }

    private static string? Juntar(params string?[] partes)
    {
        var validas = partes.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()).ToList();
        return validas.Count == 0 ? null : string.Join("\n\n", validas);
    }

    private static string? Texto(JsonElement el, string nome)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(nome, out var v)
            ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString()
            : null;

    /// <summary>O Smart Clinic grava quebras de linha CRUAS dentro das strings do JSON, o
    /// que a norma proíbe e o leitor recusa. Dentro de aspas, os controles viram escapes.</summary>
    public static string Sanear(string json)
    {
        var sb = new StringBuilder(json.Length + 16);
        var dentro = false;
        var escapado = false;
        foreach (var ch in json)
        {
            if (dentro)
            {
                if (escapado) { sb.Append(ch); escapado = false; continue; }
                if (ch == '\\') { sb.Append(ch); escapado = true; continue; }
                if (ch == '"') { dentro = false; sb.Append(ch); continue; }
                if (ch < ' ')
                {
                    sb.Append(ch switch { '\n' => "\\n", '\r' => "", '\t' => "\\t", _ => " " });
                    continue;
                }
                sb.Append(ch);
                continue;
            }
            if (ch == '"') dentro = true;
            sb.Append(ch);
        }
        return sb.ToString();
    }

    private static string? Cortar(string? texto, int max)
        => texto is null ? null : texto.Length <= max ? texto : texto[..max];
}
