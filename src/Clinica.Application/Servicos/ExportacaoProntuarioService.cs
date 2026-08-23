using System.Globalization;
using System.Text;
using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;

namespace Clinica.Application.Servicos;

/// <summary>Um arquivo da exportação: o nome sugerido e o conteúdo já pronto.</summary>
public sealed record ArquivoExportado(string Nome, string Conteudo)
{
    public int Linhas => Conteudo.Count(c => c == '\n');
}

/// <summary>
/// A EXPORTAÇÃO DO PRONTUÁRIO EM FORMATO ABERTO (parcela 52).
///
/// Por que existe
/// --------------
/// A auditoria de fornecedor da cliente, ponto 8: <i>"a clínica não deveria ficar refém do
/// fornecedor — é importante saber como recuperar/exportar os prontuários caso o contrato
/// seja encerrado"</i>. O sistema tinha duas metades e faltava a terceira:
///
/// - exportação POR PACIENTE, em texto (<see cref="TitularDadosService"/>), que atende o
///   art. 18, II da LGPD — o direito do titular, não o da clínica;
/// - backup da base inteira em JSON (<c>BackupService</c>), completo e restaurável, mas
///   num formato que só ESTE sistema lê. Ele resolve "a base sumiu"; não resolve "vamos
///   trocar de fornecedor".
///
/// Esta é a terceira: a clínica inteira, em <b>CSV</b>, que qualquer sistema importa e
/// qualquer pessoa abre. Não é bonito e é de propósito: o que se pede aqui é que o dado
/// atravesse a fronteira do produto, e PDF diagramado não atravessa — é a mesma razão pela
/// qual a exportação do titular sai em texto e não em folha desenhada.
///
/// O que vai junto, e por que importa
/// ----------------------------------
/// <b>As sessões CANCELADAS e as VERSÕES anteriores.</b> Elas fazem parte do prontuário
/// sob guarda (Lei 13.787/2018, art. 6º), e uma exportação que as omitisse entregaria ao
/// próximo fornecedor uma versão higienizada do histórico — exatamente o que a exigência
/// de integridade e rastreabilidade do art. 3º existe para impedir. A coluna
/// <c>Situacao</c> diz qual é qual.
///
/// O que NÃO vai
/// -------------
/// <b>Os bytes dos anexos e das fotos.</b> Um CSV com laudos em base64 vira um arquivo de
/// centenas de megabytes que o Excel não abre e ninguém confere — e o backup JSON já os
/// carrega, íntegros. A exportação lista os anexos (nome, tipo, tamanho, sessão) para o
/// destino saber o que tem de vir junto; os arquivos em si saem pelo backup. Prometer o
/// contrário seria a "garantia aparente" que este projeto recusa desde a parcela 3.
/// </summary>
public sealed class ExportacaoProntuarioService
{
    private readonly IClinicaRepositorio _repo;

    /// <summary>Cultura fixa nas datas e números, pela razão do backup: o arquivo VIAJA.</summary>
    private static readonly CultureInfo Fixa = CultureInfo.InvariantCulture;

    public ExportacaoProntuarioService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>
    /// ⚠️ O ARQUIVO de cada natureza clínica (parcela 72) — a declaração que
    /// <c>ConjuntoClinicoTests</c> confere contra <see cref="CatalogoRegistroClinico"/>,
    /// natureza a natureza, gravando um registro de cada e exigindo que ele APAREÇA.
    ///
    /// O defeito que ela existe para impedir já foi cometido duas vezes neste arquivo: a
    /// folha de infusão e a lista de problemas — onde moram as ALERGIAS — ficaram de fora
    /// da primeira versão. Um prontuário exportado sem elas entrega ao próximo fornecedor
    /// um paciente "sem alergia nenhuma". Declaração sozinha pode mentir, e é por isso que
    /// o teste é COMPORTAMENTAL: ele lê o CSV.
    /// </summary>
    public static IReadOnlyDictionary<NaturezaRegistroClinico, string> ArquivoPorNatureza { get; } =
        new Dictionary<NaturezaRegistroClinico, string>
        {
            [NaturezaRegistroClinico.SessaoMedica] = "prontuario-sessoes.csv",
            [NaturezaRegistroClinico.EvolucaoEnfermagem] = "prontuario-enfermagem.csv",
            [NaturezaRegistroClinico.PrescricaoInterna] = "prontuario-prescricoes.csv",
            [NaturezaRegistroClinico.DocumentoClinico] = "prontuario-documentos.csv",
            [NaturezaRegistroClinico.AvaliacaoClinica] = "prontuario-avaliacoes.csv",
            [NaturezaRegistroClinico.MedidaClinica] = "prontuario-medidas.csv",
            [NaturezaRegistroClinico.ProblemaPaciente] = "prontuario-problemas.csv",
            [NaturezaRegistroClinico.Anexo] = "prontuario-anexos.csv",
            [NaturezaRegistroClinico.MapaCorporal] = "prontuario-mapa-corporal.csv"
        };

    /// <summary>
    /// Exporta o prontuário de todos os pacientes (ou de um só, quando
    /// <paramref name="pacienteId"/> vem preenchido).
    /// </summary>
    public async Task<IReadOnlyList<ArquivoExportado>> ExportarAsync(
        int? pacienteId = null, CancellationToken ct = default)
    {
        var pacientes = pacienteId is { } um
            ? [await _repo.ObterPacienteAsync(um, ct)
                ?? throw new InvalidOperationException("Paciente não encontrado.")]
            : await _repo.BuscarPacientesAsync(null, null, ct);

        var cadastro = Cabecalho("PacienteId", "Nome", "Documento", "Nascimento", "Sexo",
            "Telefone", "Convenio", "Carteirinha");
        // ⚠️ As colunas do ATENDIMENTO (parcela 73) entram AQUI e não numa planilha nova:
        // elas são da sessão, e separá-las obrigaria quem recebe o prontuário a casar duas
        // tabelas pelo id para ler uma consulta.
        var sessoes = Cabecalho("PacienteId", "Paciente", "SessaoId", "Data", "Situacao",
            "EvaAntes", "EvaDepois", "QueixaPrincipal", "HistoriaDoencaAtual", "ExameFisico",
            "HipoteseDiagnostica", "CID", "Conduta", "Evolucao", "Orientacoes",
            "CriadoPor", "MotivoCancelamento");
        var versoes = Cabecalho("PacienteId", "SessaoId", "Versao", "SubstituidaEm",
            "SubstituidaPor", "Motivo", "QueixaPrincipal", "Conduta", "Evolucao", "Orientacoes");
        var avaliacoes = Cabecalho("PacienteId", "Paciente", "Data", "Instrumento",
            "Pontuacao", "PontuacaoMaxima", "Unidade", "Faixa", "Situacao");
        var medidas = Cabecalho("PacienteId", "Paciente", "Data", "Tipo", "Valor",
            "ValorSecundario", "Unidade", "Faixa", "Situacao");
        var documentos = Cabecalho("PacienteId", "Paciente", "Numero", "Tipo", "Data",
            "CodigoVerificacao", "Assinado", "Situacao");
        var anexos = Cabecalho("PacienteId", "SessaoId", "NomeArquivo", "Tipo",
            "TamanhoBytes", "CriadoEm", "Situacao");
        // A folha de infusão e sua EXECUÇÃO. Ficaram de fora da primeira versão, o que
        // contrariava a regra 8 do CLAUDE.md — e a execução é a parte que uma auditoria
        // de enfermagem procura: o que entrou no paciente, a que horas e por quem.
        var prescricoes = Cabecalho("PacienteId", "Paciente", "Numero", "Data", "Hora",
            "Situacao", "Prescritor", "Indicacao", "CodigoVerificacao", "AssinadaEm",
            "EncerradaEm", "MotivoCancelamento");
        var itensPrescricao = Cabecalho("PacienteId", "PrescricaoNumero", "Ordem",
            "Item", "Dose", "Diluente", "Volume", "Via", "TempoInfusao", "SeNecessario",
            "Situacao", "MotivoSuspensao");
        var checagens = Cabecalho("PacienteId", "PrescricaoNumero", "ItemOrdem", "Item",
            "Situacao", "HoraRealizacao", "RegistradoEm", "Executante", "Conselho",
            "Justificativa", "Retificacao", "MotivoRetificacao");
        // A LISTA DE PROBLEMAS ficou fora da primeira versão — e é onde moram as
        // ALERGIAS: um prontuário exportado sem elas entregaria ao próximo fornecedor
        // um paciente "sem alergia nenhuma". Descartados INCLUÍDOS, pela regra de sempre.
        var problemas = Cabecalho("PacienteId", "Paciente", "Natureza", "Descricao", "CID",
            "Inicio", "Fim", "Situacao", "Observacoes", "MotivoDescarte");
        // O MAPA CORPORAL da sessão: os pontos são o registro gráfico do que foi feito.
        // Coordenadas normalizadas (0 a 1) sobre a figura, como foram gravadas.
        var pontosMapa = Cabecalho("PacienteId", "SessaoId", "Face", "Ordem", "Numero",
            "X", "Y", "Tecnica", "Observacao");
        // As RESPOSTAS item a item das escalas: o escore ia e o que o paciente respondeu
        // ficava — e é a resposta (o item 9 do PHQ-9, por exemplo) que carrega o alerta.
        var respostas = Cabecalho("PacienteId", "Paciente", "Data", "Instrumento",
            "ItemOrdem", "ItemCodigo", "Enunciado", "Valor", "OpcaoRotulo");
        // A EVOLUÇÃO DE ENFERMAGEM (parcela 71). Entra pela regra 8: entidade clínica nova
        // entra na exportação e na guarda, senão a clínica exporta um prontuário
        // incompleto. Canceladas e retificadas INCLUÍDAS e marcadas, como em toda lista
        // deste arquivo.
        var enfermagem = Cabecalho("PacienteId", "Paciente", "Data", "Hora", "RegistradoEm",
            "PrescricaoNumero", "Texto", "Intercorrencia", "PA", "FC", "FR", "Temperatura",
            "SpO2", "Dor", "Autor", "Conselho", "Retificacao", "MotivoRetificacao",
            "Situacao", "MotivoCancelamento",
            // O Processo de Enfermagem (parcela 73): o que é TEXTO fica na linha da
            // evolução; o diagnóstico e o cuidado são LISTAS e vão em planilha própria,
            // como os itens e as checagens da folha de infusão.
            "EhConsulta", "Historico", "ExameFisico", "Avaliacao");
        var diagnosticosEnf = Cabecalho("PacienteId", "Paciente", "Data", "Hora",
            "Ordem", "Codigo", "Titulo", "RelacionadoA", "EvidenciadoPor", "ResultadoEsperado");
        var cuidadosEnf = Cabecalho("PacienteId", "Paciente", "Data", "Hora",
            "Ordem", "Codigo", "Cuidado", "Frequencia");

        foreach (var p in pacientes)
        {
            ct.ThrowIfCancellationRequested();

            Linha(cadastro, p.Id, p.Nome, p.Documento, Data(p.DataNascimento),
                RotulosEnum.De(p.Sexo), p.Telefone, p.ConvenioNome, p.Carteirinha);

            // Canceladas INCLUÍDAS: ver o resumo da classe.
            foreach (var e in await _repo.EvolucoesDoPacienteAsync(p.Id, true, ct))
            {
                Linha(sessoes, p.Id, p.Nome, e.Id, Data(e.Data),
                    e.Cancelada ? "Cancelada" : "Vigente",
                    e.EvaAntes, e.EvaDepois, e.QueixaPrincipal,
                    e.HistoriaDoencaAtual, e.ExameFisico, e.HipoteseDiagnostica, e.CidSessao,
                    e.Conduta, e.TextoEvolucao, e.Orientacoes,
                    e.CriadoPor, e.MotivoCancelamento);

                foreach (var v in await _repo.VersoesDaEvolucaoAsync(e.Id, ct))
                    Linha(versoes, p.Id, e.Id, v.Versao,
                        v.SubstituidaEm.ToString("O", Fixa), v.SubstituidaPor, v.Motivo,
                        v.QueixaPrincipal, v.Conduta, v.TextoEvolucao, v.Orientacoes);

                foreach (var a in await _repo.AnexosDaEvolucaoAsync(e.Id, ct))
                    Linha(anexos, p.Id, e.Id, a.NomeArquivo, RotulosEnum.De(a.Tipo), a.Tamanho,
                        a.CriadoEm.ToString("O", Fixa), "Vigente");

                if (await _repo.ObterMapaDaEvolucaoAsync(e.Id, ct) is { } mapa)
                    foreach (var ponto in mapa.Pontos.OrderBy(x => x.Ordem))
                        Linha(pontosMapa, p.Id, e.Id, RotulosEnum.De(ponto.Face),
                            ponto.Ordem, ponto.Nome,
                            ponto.X.ToString("0.####", Fixa), ponto.Y.ToString("0.####", Fixa),
                            RotulosEnum.De(ponto.Tecnica), ponto.Observacao);
            }

            foreach (var a in await _repo.AvaliacoesDoPacienteAsync(
                         p.Id, null, incluirCanceladas: true, ct))
            {
                Linha(avaliacoes, p.Id, p.Nome, Data(a.Data), a.InstrumentoNome,
                    a.Pontuacao, a.PontuacaoMaxima, a.Unidade, a.FaixaNome,
                    a.Cancelada ? "Cancelada" : "Vigente");

                // As respostas não vêm na lista (o corte é para a rede da tela); aqui a
                // aplicação inteira é o ponto.
                if (await _repo.ObterAvaliacaoAsync(a.Id, ct) is { } aplicacao)
                    foreach (var r in aplicacao.Respostas.OrderBy(r => r.Ordem))
                        Linha(respostas, p.Id, p.Nome, Data(a.Data), a.InstrumentoNome,
                            r.Ordem, r.ItemCodigo, r.Enunciado, r.Valor, r.OpcaoRotulo);
            }

            foreach (var problema in await _repo.ProblemasDoPacienteAsync(p.Id, somenteAtivos: false, ct))
                Linha(problemas, p.Id, p.Nome, RotulosEnum.De(problema.Natureza),
                    problema.Descricao, problema.Cid, Data(problema.Inicio), Data(problema.Fim),
                    RotulosEnum.De(problema.Situacao), problema.Observacoes, problema.MotivoDescarte);

            foreach (var m in await _repo.MedidasDoPacienteAsync(
                         p.Id, null, incluirCanceladas: true, ct))
                Linha(medidas, p.Id, p.Nome, Data(m.Data), m.TipoNome,
                    m.Valor.ToString("0.##", Fixa),
                    m.ValorSecundario?.ToString("0.##", Fixa), m.Unidade, m.FaixaNome,
                    m.Cancelada ? "Cancelada" : "Vigente");

            foreach (var pr in await _repo.PrescricoesInternasDoPacienteAsync(p.Id, int.MaxValue, ct))
            {
                Linha(prescricoes, p.Id, p.Nome, pr.Numero, Data(pr.Data),
                    pr.Hora.ToString("HH\\:mm"), RotulosEnum.De(pr.Situacao),
                    pr.Profissional?.Rotulo, pr.Indicacao, pr.CodigoVerificacao,
                    pr.AssinadaEm?.ToString("O", Fixa), pr.EncerradaEm?.ToString("O", Fixa),
                    pr.MotivoCancelamento);

                foreach (var item in pr.Itens.OrderBy(i => i.Ordem))
                {
                    Linha(itensPrescricao, p.Id, pr.Numero, item.Ordem, item.Descricao,
                        item.Dose, item.Diluente, item.Volume, RotulosEnum.De(item.Via),
                        item.TempoInfusao, item.SeNecessario,
                        item.Suspenso ? "Suspenso" : "Vigente", item.MotivoSuspensao);

                    // TODAS as checagens, inclusive as retificadas: apagar e regravar é o
                    // gesto que a auditoria de enfermagem procura, e a folha mostra as duas.
                    foreach (var c in item.Checagens.OrderBy(c => c.RegistradoEm))
                        Linha(checagens, p.Id, pr.Numero, item.Ordem, item.Descricao,
                            RotulosEnum.De(c.Situacao), c.HoraRealizacao.ToString("HH\\:mm"),
                            c.RegistradoEm.ToString("O", Fixa), c.ExecutanteNome,
                            c.ExecutanteConselho, c.Justificativa,
                            c.RetificaChecagemId is null ? string.Empty : "retifica anterior",
                            c.MotivoRetificacao);
                }
            }

            foreach (var en in await _repo.EvolucoesEnfermagemDoPacienteAsync(p.Id, int.MaxValue, ct))
            {
                Linha(enfermagem, p.Id, p.Nome, Data(en.Data), en.Hora.ToString("HH\\:mm"),
                    en.RegistradoEm.ToString("O", Fixa),
                    en.Prescricao?.Numero, en.Texto, en.Intercorrencia ? "sim" : "não",
                    en.PressaoArterial, en.FrequenciaCardiaca, en.FrequenciaRespiratoria,
                    en.Temperatura, en.SaturacaoOxigenio, en.Dor,
                    en.AutorNome, en.AutorConselho,
                    en.EhRetificacao ? "retifica anterior" : string.Empty,
                    en.MotivoRetificacao,
                    en.Cancelada ? "Cancelada" : "Válida", en.MotivoCancelamento,
                    en.EhConsulta ? "sim" : "não",
                    en.Historico, en.ExameFisico, en.Avaliacao);

                foreach (var d in en.Diagnosticos.OrderBy(x => x.Ordem))
                    Linha(diagnosticosEnf, p.Id, p.Nome, Data(en.Data),
                        en.Hora.ToString("HH\\:mm"), d.Ordem, d.Codigo, d.Titulo,
                        d.RelacionadoA, d.EvidenciadoPor, d.ResultadoEsperado);

                foreach (var c in en.Cuidados.OrderBy(x => x.Ordem))
                    Linha(cuidadosEnf, p.Id, p.Nome, Data(en.Data),
                        en.Hora.ToString("HH\\:mm"), c.Ordem, c.Codigo, c.Descricao,
                        c.Frequencia);
            }

            foreach (var d in await _repo.DocumentosDoPacienteAsync(p.Id, ct))
                Linha(documentos, p.Id, p.Nome, d.Numero,
                    TipoDocumentoInfo.Rotular(d.Tipo), Data(d.Data),
                    d.CodigoVerificacao, d.AssinadoEletronicamente ? "sim" : "não",
                    d.Cancelado ? "Cancelado" : "Válido");
        }

        return
        [
            new("prontuario-pacientes.csv", cadastro.ToString()),
            new("prontuario-sessoes.csv", sessoes.ToString()),
            new("prontuario-sessoes-versoes.csv", versoes.ToString()),
            new("prontuario-avaliacoes.csv", avaliacoes.ToString()),
            new("prontuario-medidas.csv", medidas.ToString()),
            new("prontuario-documentos.csv", documentos.ToString()),
            new("prontuario-anexos.csv", anexos.ToString()),
            new("prontuario-prescricoes.csv", prescricoes.ToString()),
            new("prontuario-prescricoes-itens.csv", itensPrescricao.ToString()),
            new("prontuario-prescricoes-checagens.csv", checagens.ToString()),
            new("prontuario-problemas.csv", problemas.ToString()),
            new("prontuario-mapa-corporal.csv", pontosMapa.ToString()),
            new("prontuario-avaliacoes-respostas.csv", respostas.ToString()),
            new("prontuario-enfermagem.csv", enfermagem.ToString()),
            new("prontuario-enfermagem-diagnosticos.csv", diagnosticosEnf.ToString()),
            new("prontuario-enfermagem-cuidados.csv", cuidadosEnf.ToString()),
            new("LEIA-ME.txt", LeiaMe(pacientes.Count))
        ];
    }

    // Os enums saem ROTULADOS, nunca por ToString() (a lição da parcela 41). Este arquivo
    // vai para a cliente e para o próximo fornecedor: "PedidoExame" e "Feminino" cru são o
    // identificador do programador vazando para fora do produto. O CSV é para pessoa E
    // para máquina, e o rótulo serve às duas — o que a máquina precisa é de coluna estável,
    // e ela é.

    // ---- Montagem do CSV ----
    //
    // Separador PONTO E VÍRGULA, como manda a convenção do projeto: com vírgula o Excel
    // em português abre tudo numa coluna só. Quem grava o arquivo põe o BOM UTF-8.

    private static StringBuilder Cabecalho(params string[] colunas)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', colunas));
        return sb;
    }

    private static void Linha(StringBuilder destino, params object?[] valores)
        => destino.AppendLine(string.Join(';', valores.Select(Escapar)));

    private static string? Data(DateOnly? d) => d?.ToString("yyyy-MM-dd", Fixa);

    /// <summary>
    /// Aspas em volta e aspas dobradas por dentro, sempre que houver separador, aspa ou
    /// quebra de linha. Evolução é texto livre com ponto e vírgula e parágrafo dentro —
    /// sem isto, uma sessão desalinharia todas as colunas dali para baixo, e o arquivo
    /// pareceria bom até alguém tentar importá-lo.
    /// </summary>
    private static string Escapar(object? valor)
    {
        var texto = valor switch
        {
            null => string.Empty,
            bool b => b ? "sim" : "não",
            IFormattable f => f.ToString(null, Fixa),
            _ => valor.ToString() ?? string.Empty
        };

        if (!texto.Any(c => c is ';' or '"' or '\n' or '\r')) return texto;

        return '"' + texto.Replace("\"", "\"\"") + '"';
    }

    private static string LeiaMe(int pacientes) =>
        $"""
         EXPORTAÇÃO DO PRONTUÁRIO ELETRÔNICO
         Gerada em {DateTime.Now:dd/MM/yyyy HH:mm} — {pacientes} paciente(s).

         FORMATO
         Arquivos CSV, separados por PONTO E VÍRGULA, codificação UTF-8 com BOM.
         Datas em ISO (aaaa-mm-dd); números com PONTO decimal.
         Abrem no Excel, no LibreOffice e em qualquer sistema que importe CSV.

         ARQUIVOS
         - prontuario-pacientes.csv .......... cadastro
         - prontuario-sessoes.csv ............ evoluções (as sessões do prontuário)
         - prontuario-sessoes-versoes.csv .... o que cada sessão dizia antes de ser corrigida
         - prontuario-avaliacoes.csv ......... escalas aplicadas (PHQ-9, GAD-7, Katz…)
         - prontuario-medidas.csv ............ medidas seriadas (peso, PA, glicemia…)
         - prontuario-documentos.csv ......... documentos emitidos
         - prontuario-anexos.csv ............. LISTA dos arquivos anexados (ver abaixo)
         - prontuario-prescricoes.csv ........ folhas de infusão (prescrição interna)
         - prontuario-prescricoes-itens.csv .. o que foi prescrito em cada folha
         - prontuario-enfermagem............ a evolução de enfermagem: o que foi observado
                                             no paciente, com hora do fato, sinais vitais e
                                             quem escreveu (nome e COREN)
         - prontuario-prescricoes-checagens... o que a enfermagem executou, com horário
         - prontuario-problemas.csv .......... lista de problemas (diagnósticos, ALERGIAS)
         - prontuario-mapa-corporal.csv ...... pontos marcados no mapa corporal por sessão
         - prontuario-avaliacoes-respostas.csv resposta item a item de cada escala

         O QUE ESTÁ INCLUÍDO E QUE COSTUMA SURPREENDER
         As sessões CANCELADAS vêm juntas, marcadas na coluna Situacao, e o mesmo vale para
         avaliações e medidas. Elas fazem parte do prontuário sob guarda: a Lei 13.787/2018
         exige integridade e rastreabilidade, e uma exportação sem elas entregaria uma
         versão higienizada do histórico.

         O arquivo de VERSÕES é o histórico de correções: cada linha é o que a sessão dizia
         antes de alguém alterá-la, com a data, o autor e o motivo da correção.

         O QUE NÃO ESTÁ INCLUÍDO
         Os BYTES dos anexos e as fotos dos pacientes. O CSV lista os anexos (nome, tipo,
         tamanho e a que sessão pertencem) para o sistema de destino saber o que precisa
         receber; os arquivos em si saem no backup completo da base (Configurações →
         Backup), que os carrega íntegros.

         GUARDA LEGAL
         Prontuário deve ser guardado por, no mínimo, {GuardaProntuario.AnosDeGuarda} anos a
         partir do ÚLTIMO registro (Lei 13.787/2018, art. 6º). Quem recebe esta exportação
         assume essa obrigação junto com os dados.
         """;
}
