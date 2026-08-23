using System.Globalization;
using System.Text;
using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;

namespace Clinica.Application.Servicos;

/// <summary>O que a anonimização fez, para a tela dizer exatamente o que aconteceu.</summary>
public sealed record ResultadoAnonimizacao(
    int PacienteId,
    string NomeAnonimo,
    int ConsentimentosRevogados,
    bool FotoRemovida,
    int SessoesPreservadas)
{
    /// <summary>Sobrou prontuário: ele fica, por obrigação legal, agora sem dono identificável.</summary>
    public bool PreservouProntuario => SessoesPreservadas > 0;
}

/// <summary>
/// Direitos do titular (LGPD, arts. 18 e 16) — o que faltava depois do consentimento.
///
/// A clínica sabia COLHER e REVOGAR consentimento desde a parcela 2. Faltavam os outros
/// dois pedidos que um paciente pode fazer e que a clínica é obrigada a atender: **me
/// dê tudo o que vocês têm sobre mim** (acesso/portabilidade) e **apaguem meus dados**
/// (eliminação).
///
/// A regra que manda aqui, e que o produto não pode fingir que não existe: **prontuário
/// não se apaga.** A guarda do prontuário é obrigação legal do profissional de saúde
/// (Resolução CFM 1.821/2007 — vinte anos a contar do último registro), e a própria LGPD
/// abre a exceção no art. 16, II: o dado necessário ao cumprimento de obrigação legal é
/// conservado mesmo depois do pedido de eliminação.
///
/// Então o que se faz é o que a lei permite e o paciente pediu: **anonimizar**. Nome,
/// documento, telefone, e-mail, endereço e foto saem; o histórico clínico fica, sem dono
/// identificável, cumprindo a guarda. E a tela DIZ isso — prometer apagar tudo e manter o
/// prontuário seria mentir para o paciente por escrito.
/// </summary>
public sealed class TitularDadosService
{
    private static readonly CultureInfo Brasil = new("pt-BR");

    private readonly IClinicaRepositorio _repo;
    private readonly ConsentimentoService _consentimentos;
    private readonly ProntuarioService _prontuarios;
    private readonly DocumentoClinicoService _documentos;

    public TitularDadosService(
        IClinicaRepositorio repo,
        ConsentimentoService consentimentos,
        ProntuarioService prontuarios,
        DocumentoClinicoService documentos)
    {
        _repo = repo;
        _consentimentos = consentimentos;
        _prontuarios = prontuarios;
        _documentos = documentos;
    }

    /// <summary>
    /// ⚠️ A SEÇÃO de cada natureza clínica (parcela 72) — a declaração que
    /// <c>ConjuntoClinicoTests</c> confere gravando um registro de cada e exigindo que
    /// ele apareça NO TEXTO.
    ///
    /// Ela nasce porque este documento cobria TRÊS naturezas de nove: quem recebe infusão
    /// semanal, faz PHQ-9 e é pesado toda consulta levava, como resposta ao art. 18, II,
    /// um papel que não dizia uma palavra sobre nada disso — e ele é a prova de que a
    /// clínica atendeu ao pedido.
    /// </summary>
    public static IReadOnlyDictionary<NaturezaRegistroClinico, string> SecaoPorNatureza { get; } =
        new Dictionary<NaturezaRegistroClinico, string>
        {
            [NaturezaRegistroClinico.SessaoMedica] = "== EVOLUÇÕES CLÍNICAS ==",
            [NaturezaRegistroClinico.EvolucaoEnfermagem] = "== EVOLUÇÕES DE ENFERMAGEM ==",
            [NaturezaRegistroClinico.PrescricaoInterna] = "== PRESCRIÇÕES DE INFUSÃO ==",
            [NaturezaRegistroClinico.DocumentoClinico] = "== DOCUMENTOS EMITIDOS ==",
            [NaturezaRegistroClinico.AvaliacaoClinica] = "== AVALIAÇÕES E ESCALAS ==",
            [NaturezaRegistroClinico.MedidaClinica] = "== MEDIDAS ==",
            [NaturezaRegistroClinico.ProblemaPaciente] = "== PROBLEMAS, DIAGNÓSTICOS E ALERGIAS ==",
            [NaturezaRegistroClinico.Anexo] = "== ANEXOS E MAPAS CORPORAIS ==",
            [NaturezaRegistroClinico.MapaCorporal] = "== ANEXOS E MAPAS CORPORAIS =="
        };

    /// <summary>
    /// Tudo o que a clínica guarda sobre o paciente, num texto que ele leva embora.
    ///
    /// Texto puro, e não PDF: o direito é de RECEBER os dados em formato legível — um
    /// relatório diagramado seria mais bonito e menos útil para quem quer levá-los a
    /// outro serviço. É o mesmo motivo de a exportação dos relatórios ser CSV.
    /// </summary>
    public async Task<string> ExportarAsync(
        int pacienteId, string? operador = null, CancellationToken ct = default)
    {
        var paciente = await _repo.ObterPacienteComHistoricoAsync(pacienteId, ct)
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        var texto = new StringBuilder();

        texto.AppendLine("DADOS PESSOAIS MANTIDOS PELA CLÍNICA");
        texto.AppendLine($"Emitido em {DateTime.Now.ToString("dd/MM/yyyy HH:mm", Brasil)}");
        texto.AppendLine("Documento entregue em atendimento ao art. 18 da LGPD (Lei 13.709/2018).");
        texto.AppendLine();

        texto.AppendLine("== CADASTRO ==");
        Linha(texto, "Nome", paciente.Nome);
        Linha(texto, "Documento", paciente.Documento);
        Linha(texto, "Nascimento", paciente.DataNascimento?.ToString("dd/MM/yyyy", Brasil));
        Linha(texto, "Sexo", paciente.Sexo.ToString());
        Linha(texto, "Telefone", paciente.Telefone);
        Linha(texto, "Convênio", paciente.Convenio.ToString());
        Linha(texto, "Carteirinha", paciente.Carteirinha);
        Linha(texto, "Validade da carteirinha",
            paciente.ValidadeCarteirinha?.ToString("dd/MM/yyyy", Brasil));
        Linha(texto, "Foto no cadastro", paciente.TemFoto ? "sim" : "não");
        texto.AppendLine();

        texto.AppendLine("== CONSENTIMENTOS ==");
        var consentimentos = await _consentimentos.HistoricoAsync(pacienteId, ct);
        if (consentimentos.Count == 0) texto.AppendLine("(nenhum registrado)");
        foreach (var c in consentimentos)
            texto.AppendLine(
                $"- {ConsentimentoService.Rotular(c.Finalidade)}: "
                + $"{(c.Concedido ? "concedido" : "recusado")} "
                + $"em {c.RegistradoEm.ToString("dd/MM/yyyy", Brasil)}"
                + (c.RevogadoEm is { } r ? $", revogado em {r.ToString("dd/MM/yyyy", Brasil)}" : string.Empty));
        texto.AppendLine();

        texto.AppendLine("== ATENDIMENTOS E GUIAS ==");
        if (paciente.Atendimentos.Count == 0) texto.AppendLine("(nenhum)");
        foreach (var a in paciente.Atendimentos.OrderBy(a => a.Data))
        {
            texto.AppendLine($"- {a.Data.ToString("dd/MM/yyyy", Brasil)} · {a.Modalidade}");
            foreach (var codigo in a.Codigos.OrderBy(c => c.Ordem))
                texto.AppendLine(
                    $"    guia {codigo.Ordem}: {codigo.Tipo} · {codigo.Status}"
                    + (string.IsNullOrWhiteSpace(codigo.NumeroGuiaReal)
                        ? string.Empty
                        : $" · nº {codigo.NumeroGuiaReal}"));
        }
        texto.AppendLine();

        texto.AppendLine("== EVOLUÇÕES CLÍNICAS ==");
        var evolucoes = await _prontuarios.DoPacienteAsync(pacienteId, ct);
        if (evolucoes.Count == 0) texto.AppendLine("(nenhuma)");
        foreach (var e in evolucoes.OrderBy(e => e.Data))
        {
            texto.AppendLine($"- {e.Data.ToString("dd/MM/yyyy", Brasil)}"
                             + (e.EvaAntes is { } antes ? $" · dor antes {antes}" : string.Empty)
                             + (e.EvaDepois is { } depois ? $" · dor depois {depois}" : string.Empty));
            Bloco(texto, "queixa", e.QueixaPrincipal);
            // O registro do atendimento (parcela 73). O art. 18, II é sobre TUDO o que a
            // clínica guarda — e a hipótese diagnóstica é justamente o que o titular leva
            // ao próximo serviço.
            Bloco(texto, "história da doença atual", e.HistoriaDoencaAtual);
            Bloco(texto, "exame físico", e.ExameFisico);
            Bloco(texto, "hipótese diagnóstica", e.HipoteseDiagnostica
                + (string.IsNullOrWhiteSpace(e.CidSessao) ? string.Empty : $" (CID {e.CidSessao})"));
            Bloco(texto, "conduta", e.Conduta);
            Bloco(texto, "evolução", e.TextoEvolucao);
            Bloco(texto, "orientações", e.Orientacoes);
        }
        texto.AppendLine();

        // A EVOLUÇÃO DE ENFERMAGEM (parcela 71). O art. 18 II é sobre TUDO o que a clínica
        // guarda do titular — e um paciente que só passa pela enfermagem receberia, sem
        // esta seção, um documento que diz que a clínica não tem registro clínico dele.
        texto.AppendLine("== EVOLUÇÕES DE ENFERMAGEM ==");
        var enfermagem = await _repo.EvolucoesEnfermagemDoPacienteAsync(pacienteId, int.MaxValue, ct);
        if (enfermagem.Count == 0) texto.AppendLine("(nenhuma)");
        foreach (var en in enfermagem.OrderBy(e => e.Data).ThenBy(e => e.Hora))
        {
            texto.AppendLine(
                $"- {en.Data.ToString("dd/MM/yyyy", Brasil)} às {en.Hora:HH\\:mm}"
                + (en.Intercorrencia ? " · INTERCORRÊNCIA" : string.Empty)
                + (en.Cancelada ? " (CANCELADA)" : string.Empty));
            if (en.SinaisVitaisResumidos is { } sinais) Bloco(texto, "sinais vitais", sinais);
            Bloco(texto, "observação", en.Texto);

            // O Processo de Enfermagem, quando o registro é uma CONSULTA (parcela 73).
            Bloco(texto, "histórico de enfermagem", en.Historico);
            Bloco(texto, "exame físico", en.ExameFisico);

            foreach (var d in en.Diagnosticos.OrderBy(x => x.Ordem))
            {
                texto.AppendLine($"    diagnóstico de enfermagem: {d.Redacao}");
                if (!string.IsNullOrWhiteSpace(d.ResultadoEsperado))
                    texto.AppendLine($"      resultado esperado: {d.ResultadoEsperado}");
            }

            foreach (var c in en.Cuidados.OrderBy(x => x.Ordem))
                texto.AppendLine($"    cuidado prescrito: {c.Redacao}");

            Bloco(texto, "avaliação de enfermagem", en.Avaliacao);
            Bloco(texto, "registrado por", en.AutorNome
                + (string.IsNullOrWhiteSpace(en.AutorConselho) ? "" : $" ({en.AutorConselho})"));
            if (en.Cancelada) Bloco(texto, "motivo do cancelamento", en.MotivoCancelamento);
        }
        texto.AppendLine();

        // ⚠️ AS SEIS SEÇÕES QUE FALTAVAM (parcela 72). O art. 18, II é sobre TUDO o que a
        // clínica guarda do titular, e este documento cobria três naturezas de nove: quem
        // recebe uma infusão semanal, faz PHQ-9 no consultório e é pesado toda consulta
        // levava um papel que não dizia uma palavra sobre nada disso — e ele é a prova de
        // que a clínica atendeu ao pedido. Agora a lista de naturezas é UMA
        // (CatalogoRegistroClinico), e a próxima entidade clínica entra por ela.
        texto.AppendLine("== PRESCRIÇÕES DE INFUSÃO ==");
        var infusoes = await _repo.PrescricoesInternasDoPacienteAsync(pacienteId, int.MaxValue, ct);
        if (infusoes.Count == 0) texto.AppendLine("(nenhuma)");
        foreach (var pr in infusoes.OrderBy(p => p.Data).ThenBy(p => p.Hora))
        {
            texto.AppendLine(
                $"- {pr.Data.ToString("dd/MM/yyyy", Brasil)} às {pr.Hora:HH\\:mm} · folha nº {pr.Numero}"
                + (pr.Cancelada ? " (CANCELADA)" : string.Empty));
            Bloco(texto, "indicação", pr.Indicacao);
            Bloco(texto, "prescrita por", pr.Profissional?.Rotulo);

            // A EXECUÇÃO junto: é o que entrou no paciente, a que horas e por quem — a
            // parte que o titular tem mais razão de querer levar embora.
            foreach (var item in pr.Itens.OrderBy(i => i.Ordem))
            {
                var checagem = item.ChecagemVigente;
                texto.AppendLine(
                    $"    {item.Ordem}. {item.TextoCompleto}"
                    + (item.Suspenso ? " (suspenso pelo prescritor)" : string.Empty)
                    + (checagem is null
                        ? " — sem registro de execução"
                        : $" — {(checagem.Situacao == SituacaoChecagem.Realizado ? "realizado" : "NÃO realizado")}"
                          + $" às {checagem.HoraRealizacao:HH\\:mm} por {checagem.ExecutanteNome}"
                          + (string.IsNullOrWhiteSpace(checagem.Justificativa)
                              ? string.Empty
                              : $" ({checagem.Justificativa})")));
            }
        }
        texto.AppendLine();

        texto.AppendLine("== AVALIAÇÕES E ESCALAS ==");
        var avaliacoes = await _repo.AvaliacoesDoPacienteAsync(
            pacienteId, null, incluirCanceladas: true, ct);
        if (avaliacoes.Count == 0) texto.AppendLine("(nenhuma)");
        foreach (var a in avaliacoes.OrderBy(a => a.Data))
            texto.AppendLine(
                $"- {a.Data.ToString("dd/MM/yyyy", Brasil)} · {a.InstrumentoNome}: "
                + $"{a.Pontuacao} de {a.PontuacaoMaxima}"
                + (string.IsNullOrWhiteSpace(a.FaixaNome) ? string.Empty : $" · {a.FaixaNome}")
                + (a.Cancelada ? " (CANCELADA)" : string.Empty));
        texto.AppendLine();

        texto.AppendLine("== MEDIDAS ==");
        var medidas = await _repo.MedidasDoPacienteAsync(
            pacienteId, null, incluirCanceladas: true, ct);
        if (medidas.Count == 0) texto.AppendLine("(nenhuma)");
        foreach (var m in medidas.OrderBy(m => m.Data))
            texto.AppendLine(
                $"- {m.Data.ToString("dd/MM/yyyy", Brasil)} · {m.TipoNome}: "
                + $"{m.Valor.ToString("0.##", Brasil)}"
                + (m.ValorSecundario is { } segundo ? $"/{segundo.ToString("0.##", Brasil)}" : string.Empty)
                + $" {m.Unidade}"
                + (string.IsNullOrWhiteSpace(m.FaixaNome) ? string.Empty : $" · {m.FaixaNome}")
                + (m.Cancelada ? " (CANCELADA)" : string.Empty));
        texto.AppendLine();

        // ⚠️ A LISTA DE PROBLEMAS é onde moram as ALERGIAS. Um documento de art. 18 sem
        // ela entrega ao titular — e ao próximo serviço que o atender — um paciente "sem
        // alergia nenhuma". É a mesma lacuna que a exportação do fornecedor já corrigiu.
        texto.AppendLine("== PROBLEMAS, DIAGNÓSTICOS E ALERGIAS ==");
        var problemas = await _repo.ProblemasDoPacienteAsync(pacienteId, somenteAtivos: false, ct);
        if (problemas.Count == 0) texto.AppendLine("(nenhum)");
        foreach (var pb in problemas.OrderBy(p => p.Inicio ?? DateOnly.FromDateTime(p.CriadoEm)))
            texto.AppendLine(
                $"- {RotulosEnum.De(pb.Natureza)}: {pb.Descricao}"
                + (string.IsNullOrWhiteSpace(pb.Cid) ? string.Empty : $" (CID {pb.Cid})")
                + $" · {RotulosEnum.De(pb.Situacao)}"
                + (pb.Inicio is { } inicio ? $" · desde {inicio.ToString("dd/MM/yyyy", Brasil)}" : string.Empty));
        texto.AppendLine();

        texto.AppendLine("== ANEXOS E MAPAS CORPORAIS ==");
        var comAnexo = 0;

        // ⚠️ Canceladas INCLUÍDAS. `_prontuarios.DoPacienteAsync` devolve só as vigentes,
        // e o laudo anexado a uma sessão cancelada continua sob guarda — a tela de guarda
        // o conta, e este documento não o listava. Duas respostas para "o que a clínica
        // tem deste paciente", e a que vai para a mão dele era a menor.
        var sessoesComCanceladas = await _repo.EvolucoesDoPacienteAsync(pacienteId, true, ct);

        foreach (var e in sessoesComCanceladas)
        {
            foreach (var a in await _repo.AnexosDaEvolucaoAsync(e.Id, ct))
            {
                comAnexo++;
                texto.AppendLine(
                    $"- {e.Data.ToString("dd/MM/yyyy", Brasil)} · anexo: {a.NomeArquivo} "
                    + $"({RotulosEnum.De(a.Tipo)}, {a.Tamanho} bytes)"
                    + (e.Cancelada ? " (da sessão CANCELADA, guardada)" : string.Empty));
            }

            if (await _repo.ObterMapaDaEvolucaoAsync(e.Id, ct) is { } mapa)
            {
                comAnexo++;
                texto.AppendLine(
                    $"- {e.Data.ToString("dd/MM/yyyy", Brasil)} · mapa corporal com "
                    + $"{mapa.Pontos.Count} ponto(s) marcado(s)"
                    + (e.Cancelada ? " (da sessão CANCELADA, guardado)" : string.Empty));
            }
        }
        if (comAnexo == 0) texto.AppendLine("(nenhum)");
        texto.AppendLine();

        texto.AppendLine("== DOCUMENTOS EMITIDOS ==");
        var documentos = await _documentos.DoPacienteAsync(pacienteId, ct);
        if (documentos.Count == 0) texto.AppendLine("(nenhum)");
        foreach (var d in documentos.OrderBy(d => d.Data))
            texto.AppendLine(
                $"- {d.Data.ToString("dd/MM/yyyy", Brasil)} · {TipoDocumentoInfo.Rotular(d.Tipo)}"
                + $" nº {d.Numero}"
                + (d.Cancelado ? " (CANCELADO)" : string.Empty));

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "DadosDoTitularExportados",
            Detalhe = $"Exportação LGPD dos dados de {paciente.Nome}",
            PacienteId = pacienteId
        }, ct);
        await _repo.SalvarAsync(ct);

        return texto.ToString();
    }

    /// <summary>
    /// Anonimiza o cadastro: o paciente deixa de ser identificável, e o histórico clínico
    /// permanece sob guarda legal.
    ///
    /// O que SAI: nome, documento, telefone, carteirinha, foto e observações do cadastro,
    /// mais os consentimentos vigentes (a base para falar com ele deixa de existir junto
    /// com o contato).
    ///
    /// O que FICA: atendimentos, guias, evoluções e documentos — a guarda do prontuário é
    /// obrigação legal do profissional de saúde, e a LGPD a preserva expressamente
    /// (art. 16, II). Eles ficam ligados a um cadastro sem nome.
    ///
    /// Não há volta: é isso que o paciente pediu. Por isso o motivo é obrigatório e a
    /// ação vai para a trilha de auditoria — a clínica precisa poder provar que atendeu
    /// ao pedido, e quando.
    /// </summary>
    public async Task<ResultadoAnonimizacao> AnonimizarAsync(
        int pacienteId, string motivo, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException(
                "Registre o pedido do titular (data, canal, quem pediu). É o que prova "
                + "que a clínica atendeu — e ela precisa poder provar.");

        var paciente = await _repo.ObterPacienteComHistoricoAsync(pacienteId, ct)
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        var nomeOriginal = paciente.Nome;
        var anonimo = $"Paciente anonimizado #{pacienteId}";
        var sessoes = paciente.Atendimentos.Count;
        var tinhaFoto = paciente.TemFoto;

        paciente.Nome = anonimo;
        paciente.Documento = null;
        paciente.Telefone = null;
        paciente.Carteirinha = null;
        paciente.DataNascimento = null;
        paciente.Observacoes = null;

        if (tinhaFoto) await _repo.RemoverFotoPacienteAsync(pacienteId, ct);

        // O consentimento cai junto: sem contato não há como falar com ele, e manter o
        // "sim" de quem pediu para sair seria guardar permissão de um contato que não
        // existe mais.
        var vigentes = await _consentimentos.SituacaoAsync(pacienteId, ct);
        var revogados = 0;
        foreach (var (finalidade, consentimento) in vigentes)
        {
            if (!consentimento.Concedido || consentimento.RevogadoEm is not null) continue;
            await _consentimentos.RevogarAsync(
                consentimento.Id, operador,
                "Anonimização a pedido do titular (LGPD)", ct);
            revogados++;
        }

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "TitularAnonimizado",
            // O nome original FICA na trilha: é o que liga o pedido ao registro, e a
            // auditoria é exatamente onde esse vínculo pode existir sem expor o dado na
            // operação do dia a dia.
            Detalhe = $"{nomeOriginal} anonimizado — {motivo.Trim()}",
            PacienteId = pacienteId
        }, ct);

        await _repo.SalvarAsync(ct);

        return new ResultadoAnonimizacao(pacienteId, anonimo, revogados, tinhaFoto, sessoes);
    }

    private static void Linha(StringBuilder texto, string rotulo, string? valor)
        => texto.AppendLine($"{rotulo}: {(string.IsNullOrWhiteSpace(valor) ? "—" : valor)}");

    private static void Bloco(StringBuilder texto, string rotulo, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return;
        texto.AppendLine($"    {rotulo}: {valor.Trim()}");
    }
}
