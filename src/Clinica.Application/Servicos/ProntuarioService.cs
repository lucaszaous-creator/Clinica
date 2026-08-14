using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Prontuário: a evolução clínica de cada sessão, com a escala de dor EVA medida antes
/// e depois.
///
/// A regra que dá sentido ao resto: a EVA vale em PAR. Uma medida solta não diz se o
/// tratamento funcionou, e por isso só as sessões com o par completo entram na
/// <see cref="EvolucaoDaDorAsync"/> — deixar uma medida pela metade puxar o gráfico
/// faria a linha oscilar por falta de dado, não por dor.
/// </summary>
public sealed class ProntuarioService
{
    private readonly IClinicaRepositorio _repo;

    /// <summary>Teto de um anexo, em bytes (10 MB). Foto de celular passa longe disso.</summary>
    public const int TamanhoMaximoAnexo = 10 * 1024 * 1024;

    public ProntuarioService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>Prontuário do paciente, da sessão mais recente para a mais antiga.</summary>
    public Task<IReadOnlyList<Evolucao>> DoPacienteAsync(int pacienteId, CancellationToken ct = default)
        => _repo.EvolucoesDoPacienteAsync(pacienteId, ct);

    public Task<Evolucao?> ObterAsync(int evolucaoId, CancellationToken ct = default)
        => _repo.ObterEvolucaoAsync(evolucaoId, ct);

    /// <summary>
    /// Registra (ou atualiza) a evolução de uma sessão. Grava auditoria no mesmo
    /// SaveChanges: prontuário é documento clínico, e alteração sem rastro não presta.
    /// </summary>
    /// <param name="motivoDaCorrecao">
    /// Por que a sessão está sendo corrigida. Só vale ao ALTERAR uma já existente, e é
    /// opcional de propósito — ver <see cref="VersaoEvolucao.Motivo"/>: a evolução é
    /// escrita em várias passadas durante o atendimento, e exigir justificativa a cada
    /// Salvar produziria trinta "ajuste" por dia, que é rastro com aparência de controle
    /// e nenhum conteúdo. O que a lei exige — recuperar o que estava escrito — a versão
    /// entrega com ou sem ele.
    /// </param>
    public async Task<Evolucao> SalvarAsync(
        Evolucao dados, string? operador = null, string? motivoDaCorrecao = null,
        CancellationToken ct = default)
    {
        if (await _repo.ObterPacienteAsync(dados.PacienteId, ct) is null)
            throw new InvalidOperationException("Paciente não encontrado.");

        if (!Evolucao.EvaValida(dados.EvaAntes) || !Evolucao.EvaValida(dados.EvaDepois))
            throw new InvalidOperationException(
                $"A escala de dor vai de {Evolucao.EvaMinima} a {Evolucao.EvaMaxima}.");

        // Evolução vazia é ruído no prontuário: alguma coisa tem de ter sido registrada.
        var temTexto = !string.IsNullOrWhiteSpace(dados.QueixaPrincipal)
                       || !string.IsNullOrWhiteSpace(dados.Conduta)
                       || !string.IsNullOrWhiteSpace(dados.TextoEvolucao)
                       || !string.IsNullOrWhiteSpace(dados.Orientacoes);
        if (!temTexto && dados.EvaAntes is null && dados.EvaDepois is null)
            throw new InvalidOperationException(
                "Registre ao menos a dor (EVA) ou um dos campos da evolução.");

        Evolucao destino;
        var novo = dados.Id == 0;
        if (novo)
        {
            destino = new Evolucao
            {
                PacienteId = dados.PacienteId,
                CriadoEm = DateTime.Now,
                CriadoPor = operador
            };
            await _repo.AdicionarEvolucaoAsync(destino, ct);
        }
        else
        {
            destino = await _repo.ObterEvolucaoAsync(dados.Id, ct)
                ?? throw new InvalidOperationException("Evolução não encontrada.");

            if (destino.Cancelada)
                throw new InvalidOperationException(
                    "Esta sessão foi cancelada e não se edita. Registre uma sessão nova — "
                    + "editar a cancelada faria o prontuário desdizer o cancelamento.");

            // A METADE QUE FALTAVA (parcela 52): antes de sobrescrever, guarda o que o
            // registro dizia. Sem isto a auditoria gravava "EvolucaoAlterada" e o texto
            // anterior sumia — trilha que diz QUE mudou sem dizer O QUE mudou não responde
            // a única pergunta que se faz a um prontuário eletrônico numa perícia.
            GuardarVersao(destino, operador, motivoDaCorrecao);

            destino.AtualizadoEm = DateTime.Now;
        }

        destino.ProfissionalId = dados.ProfissionalId;
        destino.AtendimentoId = dados.AtendimentoId;
        destino.AgendamentoId = dados.AgendamentoId;
        destino.Data = dados.Data;
        destino.EvaAntes = dados.EvaAntes;
        destino.EvaDepois = dados.EvaDepois;
        destino.QueixaPrincipal = Limpar(dados.QueixaPrincipal);
        destino.Conduta = Limpar(dados.Conduta);
        destino.TextoEvolucao = Limpar(dados.TextoEvolucao);
        destino.Orientacoes = Limpar(dados.Orientacoes);

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = novo ? "EvolucaoRegistrada" : "EvolucaoAlterada",
            Detalhe = $"Sessão de {destino.Data:dd/MM/yyyy}"
                      + (destino.TemParEva ? $" — EVA {destino.EvaAntes}→{destino.EvaDepois}" : string.Empty)
                      // A trilha diz que existe versão anterior guardada, e qual. Sem esta
                      // frase, quem lê a auditoria não sabe que dá para recuperar o texto.
                      + (novo ? string.Empty : $" — versão anterior guardada (v{destino.Versoes.Count})")
                      + (string.IsNullOrWhiteSpace(motivoDaCorrecao)
                          ? string.Empty
                          : $" — motivo: {motivoDaCorrecao.Trim()}"),
            PacienteId = destino.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return destino;
    }

    /// <summary>
    /// Congela o conteúdo ATUAL da sessão como versão anterior, antes de ele ser
    /// sobrescrito pela correção.
    ///
    /// A numeração sai da contagem do que já existe, e não de um contador guardado na
    /// evolução: um campo a mais seria uma segunda verdade sobre a mesma coisa, e as duas
    /// divergiriam no primeiro caminho que gravasse a versão sem incrementar o contador.
    /// </summary>
    private static void GuardarVersao(Evolucao atual, string? operador, string? motivo)
        => atual.Versoes.Add(new VersaoEvolucao
        {
            EvolucaoId = atual.Id,
            Versao = atual.Versoes.Count + 1,
            Data = atual.Data,
            EvaAntes = atual.EvaAntes,
            EvaDepois = atual.EvaDepois,
            QueixaPrincipal = atual.QueixaPrincipal,
            Conduta = atual.Conduta,
            TextoEvolucao = atual.TextoEvolucao,
            Orientacoes = atual.Orientacoes,
            ProfissionalId = atual.ProfissionalId,
            SubstituidaEm = DateTime.Now,
            SubstituidaPor = operador,
            Motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim()
        });

    /// <summary>Quantas correções cada sessão teve — para a lista marcar quais foram mexidas.</summary>
    public Task<IReadOnlyDictionary<int, int>> ContagemDeVersoesAsync(
        IReadOnlyCollection<int> evolucaoIds, CancellationToken ct = default)
        => _repo.ContagemDeVersoesAsync(evolucaoIds, ct);

    /// <summary>
    /// O que esta sessão já disse antes das correções, da versão mais antiga para a mais
    /// nova (parcela 52).
    ///
    /// Guardar a versão e não ter por onde lê-la seria o defeito recorrente do projeto na
    /// variante mais cara: aqui o leitor que falta é uma perícia.
    /// </summary>
    public Task<IReadOnlyList<VersaoEvolucao>> VersoesAsync(
        int evolucaoId, CancellationToken ct = default)
        => _repo.VersoesDaEvolucaoAsync(evolucaoId, ct);

    /// <summary>
    /// CANCELA uma sessão do prontuário. Não apaga (parcela 52).
    ///
    /// Até aqui este método chamava <c>Remove()</c> no banco, e levava os anexos junto.
    /// Isso contradizia frontalmente a Lei 13.787/2018 (art. 3º — integridade e
    /// autenticidade) e o prazo de guarda de 20 anos do art. 6º: não há como garantir
    /// retenção com um botão que destrói o registro. Contradizia também o resto deste
    /// projeto, que já cancelava com motivo no documento clínico, na não conformidade do
    /// faturamento e no descarte de problema — a regra existia e não tinha sido aplicada
    /// justamente onde mais importa.
    ///
    /// A sessão cancelada sai das listas e das contas, mas continua no prontuário,
    /// marcada e legível. O caso real que motivava a exclusão — a sessão lançada no
    /// paciente errado — fica melhor resolvido assim: some do tratamento de quem não a
    /// teve e continua provando o que aconteceu.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b><c>operador</c> não tem valor padrão, e isso é a correção de um defeito real.</b>
    /// Enquanto ele foi opcional, <c>CancelarAsync(id, operador)</c> COMPILAVA — e era
    /// exatamente o que a ficha do paciente fazia: o login entrava como <c>motivo</c>, o
    /// operador ficava nulo e a auditoria assinava "?". Assinatura que aceita a chamada
    /// errada é assinatura que vai receber a chamada errada; sem o padrão, o compilador
    /// passa a ser a rede.
    /// </remarks>
    public async Task CancelarAsync(
        int evolucaoId, string motivo, string? operador, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException(
                "Diga por que a sessão está sendo cancelada. Sem o motivo, quem ler o "
                + "prontuário amanhã não sabe se houve engano de paciente ou de digitação — "
                + "e cancelar sem justificativa é apagar com uma etapa a mais.");

        var evolucao = await _repo.ObterEvolucaoAsync(evolucaoId, ct)
            ?? throw new InvalidOperationException("Evolução não encontrada.");

        if (evolucao.Cancelada)
            throw new InvalidOperationException("Esta sessão já está cancelada.");

        evolucao.CanceladaEm = DateTime.Now;
        evolucao.MotivoCancelamento = motivo.Trim();
        evolucao.CanceladaPor = operador;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "EvolucaoCancelada",
            Detalhe = $"Sessão de {evolucao.Data:dd/MM/yyyy} — {motivo.Trim()}",
            PacienteId = evolucao.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
    }

    // ==================== Modelos de evolução (parcela 63) ====================

    /// <summary>
    /// Os modelos que este profissional enxerga: os DELE mais os da clínica.
    ///
    /// A sessão de acupuntura tem sempre a mesma forma, e era redigitada por inteiro toda
    /// vez — <c>ModeloDocumento</c> existia desde a parcela 3 e servia só aos papéis
    /// impressos, enquanto a evolução, que é o texto mais escrito do sistema, não tinha
    /// nada.
    /// </summary>
    public Task<IReadOnlyList<ModeloEvolucao>> ModelosAsync(
        int? profissionalId = null, CancellationToken ct = default)
        => _repo.ModelosEvolucaoAsync(profissionalId, ct);

    /// <summary>
    /// Grava um modelo. Nome repetido para o MESMO dono sobrescreve em vez de duplicar —
    /// é o que quem clica "salvar como modelo" pela segunda vez espera, e é a mesma regra
    /// do modelo de documento.
    /// </summary>
    public async Task<ModeloEvolucao> SalvarModeloAsync(
        ModeloEvolucao modelo, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(modelo.Nome))
            throw new InvalidOperationException("Dê um nome ao modelo.");

        // Modelo vazio, aplicado, apagaria o que já estava escrito na sessão — e o
        // profissional não teria como saber que foi ele. Recusar na gravação é barato.
        if (!modelo.TemConteudo)
            throw new InvalidOperationException(
                "O modelo não tem nenhuma linha preenchida. Escreva ao menos um campo — "
                + "modelo vazio, aplicado, limparia a sessão em vez de preenchê-la.");

        modelo.Nome = modelo.Nome.Trim();

        var existente = await _repo.ObterModeloEvolucaoPorNomeAsync(
            modelo.ProfissionalId, modelo.Nome, ct);

        if (existente is not null && existente.Id != modelo.Id)
        {
            existente.QueixaPrincipal = modelo.QueixaPrincipal;
            existente.Conduta = modelo.Conduta;
            existente.TextoEvolucao = modelo.TextoEvolucao;
            existente.Orientacoes = modelo.Orientacoes;
            existente.Ordem = modelo.Ordem;
            existente.Ativo = true;
            existente.AtualizadoEm = DateTime.Now;

            await _repo.SalvarAsync(ct);
            return existente;
        }

        if (modelo.Id == 0)
        {
            modelo.CriadoPor = operador;
            await _repo.AdicionarModeloEvolucaoAsync(modelo, ct);
        }
        else
        {
            modelo.AtualizadoEm = DateTime.Now;
        }

        await _repo.SalvarAsync(ct);
        return modelo;
    }

    /// <summary>
    /// Apaga um modelo — e apagar aqui é APAGAR mesmo, ao contrário de tudo o que é
    /// prontuário neste sistema.
    ///
    /// A diferença é o que ele é: não registra o que aconteceu com nenhum paciente, é
    /// rascunho de apoio. E como aplicar COPIA o texto para a sessão, nenhuma evolução
    /// escrita com ele muda quando ele some. É a mesma decisão da parcela 25 para o
    /// protocolo do mapa corporal e o modelo de documento.
    /// </summary>
    public async Task RemoverModeloAsync(int modeloId, CancellationToken ct = default)
    {
        await _repo.RemoverModeloEvolucaoAsync(modeloId, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// Como a dor andou ao longo do tratamento. Só entram as sessões com o par EVA
    /// completo, em ordem cronológica (o prontuário vem do mais novo para o mais velho;
    /// o gráfico precisa do contrário).
    /// </summary>
    public async Task<EvolucaoDaDor> EvolucaoDaDorAsync(int pacienteId, CancellationToken ct = default)
    {
        var evolucoes = await _repo.EvolucoesDoPacienteAsync(pacienteId, ct);

        var pontos = evolucoes
            .Where(e => e.TemParEva)
            .OrderBy(e => e.Data).ThenBy(e => e.Id)
            .Select(e => new PontoEva(e.Data, e.EvaAntes!.Value, e.EvaDepois!.Value))
            .ToList();

        return new EvolucaoDaDor(pontos, evolucoes.Count);
    }

    // ---------------- Anexos ----------------

    public Task<IReadOnlyList<AnexoResumo>> AnexosAsync(int evolucaoId, CancellationToken ct = default)
        => _repo.AnexosDaEvolucaoAsync(evolucaoId, ct);

    /// <summary>
    /// Quantos anexos tem cada sessão, em UMA consulta (parcela 37).
    ///
    /// A lista do prontuário desenha um clipe por sessão, e perguntar sessão a sessão dá
    /// uma ida ao banco por linha — num prontuário de quarenta sessões, quarenta viagens a
    /// um banco remoto para desenhar quarenta números. Sessão sem anexo não aparece no
    /// dicionário; quem lê trata a ausência como zero.
    /// </summary>
    public Task<IReadOnlyDictionary<int, int>> ContagemDeAnexosAsync(
        IReadOnlyCollection<int> evolucaoIds, CancellationToken ct = default)
        => _repo.ContagemDeAnexosAsync(evolucaoIds, ct);

    /// <summary>Bytes de um anexo — a única leitura que traz o arquivo do banco.</summary>
    public Task<byte[]?> ConteudoAnexoAsync(int anexoId, CancellationToken ct = default)
        => _repo.ConteudoDoAnexoAsync(anexoId, ct);

    public async Task<AnexoProntuario> AnexarAsync(
        int evolucaoId, string nomeArquivo, byte[] conteudo,
        TipoAnexo tipo = TipoAnexo.Documento, string? tipoConteudo = null,
        string? descricao = null, string? operador = null, CancellationToken ct = default)
    {
        var evolucao = await _repo.ObterEvolucaoAsync(evolucaoId, ct)
            ?? throw new InvalidOperationException("Evolução não encontrada.");

        if (conteudo.Length == 0)
            throw new InvalidOperationException("O arquivo está vazio.");

        // O limite protege o banco remoto: um anexo gigante trava a sincronização de
        // todo mundo, e o erro apareceria longe da causa.
        if (conteudo.Length > TamanhoMaximoAnexo)
            throw new InvalidOperationException(
                $"O arquivo tem {conteudo.Length / (1024 * 1024)} MB e o limite é "
                + $"{TamanhoMaximoAnexo / (1024 * 1024)} MB.");

        var anexo = new AnexoProntuario
        {
            EvolucaoId = evolucaoId,
            NomeArquivo = nomeArquivo.Trim(),
            Tipo = tipo,
            TipoConteudo = tipoConteudo,
            Conteudo = conteudo,
            Tamanho = conteudo.Length,
            Descricao = Limpar(descricao),
            CriadoEm = DateTime.Now,
            CriadoPor = operador
        };

        await _repo.AdicionarAnexoAsync(anexo, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "AnexoProntuario",
            Detalhe = $"{anexo.NomeArquivo} na sessão de {evolucao.Data:dd/MM/yyyy}",
            PacienteId = evolucao.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return anexo;
    }

    /// <summary>
    /// RETIRA um anexo do prontuário sem apagá-lo (parcela 52).
    ///
    /// O laudo que sustentou uma conduta continua sendo parte da prova de que a conduta
    /// era razoável — mesmo depois de alguém concluir que o arquivo estava errado, e
    /// especialmente nesse caso. Como o resto do prontuário, ele sai da lista e fica.
    /// </summary>
    public async Task CancelarAnexoAsync(
        int anexoId, string motivo, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException(
                "Diga por que o anexo está sendo retirado do prontuário.");

        var anexo = await _repo.ObterAnexoAsync(anexoId, ct)
            ?? throw new InvalidOperationException("Anexo não encontrado.");

        if (anexo.Cancelado)
            throw new InvalidOperationException("Este anexo já foi retirado.");

        anexo.CanceladoEm = DateTime.Now;
        anexo.MotivoCancelamento = motivo.Trim();
        anexo.CanceladoPor = operador;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "AnexoRetirado",
            Detalhe = $"{anexo.NomeArquivo} — {motivo.Trim()}",
            PacienteId = anexo.Evolucao?.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
    }

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
