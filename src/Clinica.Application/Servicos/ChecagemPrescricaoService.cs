using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Quem está executando. Vem do LOGIN, nunca de um campo digitado — é o vínculo com a
/// pessoa que dá valor à checagem, e um nome digitado não vincula ninguém.
/// </summary>
/// <param name="Nome">Copiado para a linha: o usuário pode ser renomeado ou desativado.</param>
/// <param name="Conselho">COREN, quando cadastrado.</param>
public sealed record IdentificacaoExecutante(int? UsuarioId, string Nome, string? Conselho = null);

/// <summary>
/// A CHECAGEM DE ENFERMAGEM — o coração da parcela 42.
///
/// O que é checar
/// --------------
/// No hospital, checar não é marcar uma caixinha: é dizer <i>"foi prescrito assim e foi
/// realizado assim"</i>, com o horário em que aconteceu, e responder por isso. Quando não
/// foi realizado, a técnica <b>"rodela"</b> — circula o horário — e escreve o porquê
/// ("o paciente não quis", "apresentou reação alérgica").
///
/// Onde mora a assinatura da enfermagem
/// ------------------------------------
/// <b>No papel.</b> A clínica decidiu que quem confere e assina a execução é a enfermeira,
/// na via impressa, depois que a folha sai da impressora — e por isso este serviço não
/// assina nada. O que ele grava é o REGISTRO do que foi feito: alimenta o prontuário, a
/// conferência do fim do dia e o circuito da alergia. A garantia de autoria continua sendo
/// a caneta dela sobre a folha, e a folha impressa diz isso com todas as letras.
///
/// Não é menos rigor, é rigor no lugar certo: exigir um segundo certificado ICP-Brasil aqui
/// obrigaria a clínica a comprar um e-CPF para a técnica e produziria, com muita cerimônia,
/// a mesma garantia que a assinatura manuscrita já dá.
///
/// As quatro regras que este serviço cobra
/// ---------------------------------------
/// 1. <b>Só se checa folha ASSINADA.</b> Administrar sobre rascunho é o buraco clássico.
/// 2. <b>Não realizado EXIGE justificativa.</b> Terceira recusa do projeto, junto da
///    divergência do fechamento de caixa e do descarte de problema. Um item que não entrou
///    no paciente sem uma linha dizendo por quê é a pior linha possível de um prontuário.
/// 3. <b>A hora é INFORMADA, e não pode ser no futuro.</b> Ver <see cref="ChecarAsync"/>.
/// 4. <b>Não se apaga: retifica-se.</b> Ver <see cref="RetificarAsync"/>.
///
/// E o circuito que ele fecha
/// --------------------------
/// O exemplo que a clínica deu ao pedir a feature foi <i>"o paciente apresentou reação
/// alérgica e não quis fazer a dipirona"</i>. Até aqui essa frase morreria num campo de
/// texto: a próxima receita sairia com dipirona de novo, porque a conferência da parcela
/// 40 lê a LISTA DE PROBLEMAS, e ninguém teria escrito lá. Por isso a não realização por
/// reação pode registrar a alergia <b>no mesmo SaveChanges</b> — ver
/// <see cref="ChecarAsync"/>. É o que transforma o que aconteceu hoje no alerta de amanhã.
/// </summary>
public sealed class ChecagemPrescricaoService
{
    private readonly IClinicaRepositorio _repo;

    /// <summary>
    /// Folga sobre o relógio ao aceitar o horário informado. Existe para o relógio da
    /// máquina da sala poder estar alguns minutos adiantado sem travar a técnica; não é
    /// permissão para checar antes de fazer.
    /// </summary>
    private static readonly TimeSpan FolgaDeRelogio = TimeSpan.FromMinutes(5);

    /// <summary>
    /// De onde sai "agora". Único ponto do projeto com esta costura, e ela é justificada
    /// por uma coisa só: a recusa de hora futura é regra de SEGURANÇA (ver
    /// <see cref="ChecarAsync"/>), e uma regra de segurança que não dá para testar é uma
    /// regra que apodrece sem ninguém notar. Com o relógio real o teste seria intestável
    /// perto da meia-noite — <c>TimeOnly.AddHours</c> dá a volta, e "duas horas depois das
    /// 23h" é 1h da manhã, que está no passado.
    ///
    /// O resto do serviço continua usando <c>DateTime.Now</c> direto, como o resto do
    /// projeto: trocar tudo por relógio injetado seria refatorar 40 arquivos por causa de
    /// um teste.
    /// </summary>
    private readonly Func<DateTime> _agora;

    public ChecagemPrescricaoService(IClinicaRepositorio repo, Func<DateTime>? agora = null)
    {
        _repo = repo;
        _agora = agora ?? (() => DateTime.Now);
    }

    // ---- Leitura ----

    /// <summary>As folhas assinadas do dia — a fila da sala de infusão.</summary>
    public Task<IReadOnlyList<PrescricaoInterna>> DoDiaAsync(
        DateOnly data, int? profissionalId = null, bool incluirEncerradas = false,
        CancellationToken ct = default)
        => _repo.PrescricoesInternasDoDiaAsync(data, profissionalId, incluirEncerradas, ct);

    /// <summary>
    /// As folhas encerradas que ainda devem a assinatura eletrônica da enfermagem, de
    /// QUALQUER dia — a fila que a sala não mostrava.
    ///
    /// A folha só fica assinável depois de encerrar, e encerrar era justamente o que a
    /// tirava da lista: encerradas nascem escondidas e a fila é travada em hoje. Quem
    /// recusasse assinar na hora só reencontrava a folha digitando o código do papel.
    /// </summary>
    public Task<IReadOnlyList<PrescricaoInterna>> AguardandoAssinaturaAsync(
        int? profissionalId = null, CancellationToken ct = default)
        => _repo.PrescricoesInternasAguardandoAssinaturaAsync(profissionalId, ct);

    /// <summary>Quantas folhas do dia ainda têm item esperando execução.</summary>
    public Task<int> PendentesDoDiaAsync(
        DateOnly data, int? profissionalId = null, CancellationToken ct = default)
        => _repo.PrescricoesInternasPendentesAsync(data, profissionalId, ct);

    // ---- Checagem ----

    /// <summary>
    /// Checa um item: o "chequezinho" (<see cref="SituacaoChecagem.Realizado"/>) ou a
    /// "rodela" (<see cref="SituacaoChecagem.NaoRealizado"/>).
    /// </summary>
    /// <param name="hora">
    /// A hora em que foi (ou seria) administrado, DIGITADA por quem executou — nunca o
    /// relógio. A técnica administra às 14h e consegue registrar às 14h20, entre um
    /// paciente e outro; carimbar <c>DateTime.Now</c> mentiria sobre o horário da
    /// administração, que é o dado clínico. O relógio fica em
    /// <see cref="ChecagemPrescricao.RegistradoEm"/>, ao lado, e a diferença entre os dois
    /// é o que uma auditoria de enfermagem procura.
    ///
    /// Hora no FUTURO é recusada, e essa é uma regra de segurança, não de formulário:
    /// checar antes de administrar ("pré-checagem") é justamente o hábito que faz um item
    /// aparecer como feito num paciente que saiu antes de recebê-lo.
    /// </param>
    /// <param name="alergiaObservada">
    /// Quando preenchido, registra uma ALERGIA na lista de problemas do paciente no mesmo
    /// SaveChanges da checagem. É o caminho do "não fez porque teve reação" virar alerta
    /// na próxima prescrição, em vez de morrer na justificativa.
    /// </param>
    public async Task<ChecagemPrescricao> ChecarAsync(
        int itemId,
        SituacaoChecagem situacao,
        TimeOnly hora,
        IdentificacaoExecutante executante,
        string? justificativa = null,
        string? alergiaObservada = null,
        CancellationToken ct = default)
    {
        var item = await ExigirItemChecavel(itemId, ct);

        if (item.ChecagemVigente is not null)
            throw new InvalidOperationException(
                "Este item já foi checado. Se o registro está errado, RETIFIQUE — a "
                + "checagem anterior fica na folha, com o motivo da correção.");

        var checagem = Montar(item, situacao, hora, executante, justificativa);
        item.Checagens.Add(checagem);

        await RegistrarAlergiaSePedido(item, alergiaObservada, executante, ct);

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = executante.Nome,
            Acao = situacao == SituacaoChecagem.Realizado
                ? "PrescricaoItemChecado"
                : "PrescricaoItemNaoRealizado",
            PacienteId = item.Prescricao?.PacienteId,
            Detalhe = Descrever(item, checagem)
        }, ct);

        await _repo.SalvarAsync(ct);
        return checagem;
    }

    /// <summary>
    /// Corrige uma checagem SEM apagá-la: grava outra apontando para a anterior, com o
    /// motivo da correção.
    ///
    /// Checagem que some leva junto a prova de que a conduta da época era razoável — mesma
    /// regra da não conformidade do faturamento e do documento clínico cancelado. E aqui
    /// pesa mais que nos outros dois: apagar e regravar uma checagem é exatamente o gesto
    /// que uma auditoria de enfermagem procura, e um sistema que o permitisse silenciosamente
    /// estaria oferecendo a ferramenta.
    /// </summary>
    public async Task<ChecagemPrescricao> RetificarAsync(
        int itemId,
        SituacaoChecagem situacao,
        TimeOnly hora,
        IdentificacaoExecutante executante,
        string motivoRetificacao,
        string? justificativa = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivoRetificacao))
            throw new InvalidOperationException(
                "Diga por que a checagem anterior estava errada. É essa frase que separa "
                + "uma correção de uma reescrita.");

        var item = await ExigirItemChecavel(itemId, ct);

        var anterior = item.ChecagemVigente
            ?? throw new InvalidOperationException(
                "Não há checagem para retificar neste item — use a checagem normal.");

        var checagem = Montar(item, situacao, hora, executante, justificativa);
        checagem.RetificaChecagemId = anterior.Id;
        checagem.MotivoRetificacao = motivoRetificacao.Trim();
        item.Checagens.Add(checagem);

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = executante.Nome,
            Acao = "PrescricaoChecagemRetificada",
            PacienteId = item.Prescricao?.PacienteId,
            Detalhe = $"{Descrever(item, checagem)} · corrige a checagem de "
                    + $"{anterior.HoraRealizacao:HH\\:mm} ({RotularSituacao(anterior.Situacao)}) "
                    + $"· motivo: {motivoRetificacao.Trim()}"
        }, ct);

        await _repo.SalvarAsync(ct);
        return checagem;
    }

    /// <summary>
    /// Encerra a execução: a folha sai da sala e não se checa mais.
    ///
    /// <b>Aqui NÃO há assinatura eletrônica</b>, e isso é decisão da clínica: quem confere e
    /// assina a execução é a enfermeira, <b>na via impressa</b>, depois que a folha sai da
    /// impressora. O que este método grava é o REGISTRO do que foi feito — útil no
    /// prontuário, na conferência do fim do dia e no circuito da alergia —, não o documento
    /// que responde por ele. O documento é o papel, e a folha impressa diz isso.
    ///
    /// A primeira versão exigia um segundo certificado ICP-Brasil aqui. Além de obrigar a
    /// clínica a comprar um e-CPF para a técnica, era uma cerimônia a mais para produzir uma
    /// garantia que o papel assinado já dava.
    ///
    /// Exige que todo item tenha destino (feito, não feito ou suspenso): encerrar com item
    /// pendente deixaria no prontuário uma folha que não diz se a droga entrou no paciente
    /// — que é a única pergunta que ela existe para responder. O "se necessário" fica de
    /// fora da conta, porque condição que não aconteceu não é trabalho por fazer.
    ///
    /// <b>Não há reabertura.</b> Se a folha foi encerrada cedo demais, a correção é
    /// retificar a checagem errada — o que continua possível — ou prescrever outra folha.
    /// </summary>
    public async Task<PrescricaoInterna> EncerrarAsync(
        int prescricaoId, IdentificacaoExecutante executante, CancellationToken ct = default)
    {
        var prescricao = await _repo.ObterPrescricaoInternaAsync(prescricaoId, ct)
            ?? throw new InvalidOperationException("Prescrição não encontrada.");

        if (prescricao.Situacao == SituacaoPrescricao.Encerrada)
            throw new InvalidOperationException(
                $"A execução da prescrição {prescricao.Numero} já foi encerrada.");

        if (!prescricao.PodeChecar)
            throw new InvalidOperationException(
                $"A prescrição {prescricao.Numero} não está em execução "
                + $"({RotulosEnum.De(prescricao.Situacao)}).");

        if (!prescricao.ExecucaoCompleta)
            throw new InvalidOperationException(
                $"Ainda há {prescricao.Pendentes} item(ns) sem checagem. Uma folha encerrada "
                + "com item em aberto não diz se a medicação entrou no paciente, que é a "
                + "única pergunta que ela existe para responder.");

        prescricao.Situacao = SituacaoPrescricao.Encerrada;
        prescricao.EncerradaEm = DateTime.Now;
        prescricao.AtualizadoEm = DateTime.Now;
        prescricao.AtualizadoPor = executante.Nome;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = executante.Nome,
            Acao = "PrescricaoExecucaoEncerrada",
            PacienteId = prescricao.PacienteId,
            Detalhe = $"{prescricao.Numero} · {prescricao.Realizados} realizados, "
                    + $"{prescricao.NaoRealizados} não realizados · encerrada por "
                    + $"{executante.Nome} "
                    + (prescricao.ExigeAssinaturaEletronicaDaExecucao
                        ? "(aguardando a assinatura eletrônica da enfermagem)"
                        : "(assinatura da enfermagem na via impressa)")
        }, ct);

        await _repo.SalvarAsync(ct);
        return prescricao;
    }

    // ---- Apoio ----

    private async Task<ItemPrescricaoInterna> ExigirItemChecavel(int itemId, CancellationToken ct)
    {
        var item = await _repo.ObterItemPrescricaoInternaAsync(itemId, ct)
            ?? throw new InvalidOperationException("Item não encontrado.");

        var prescricao = item.Prescricao
            ?? throw new InvalidOperationException("Item sem prescrição.");

        if (!prescricao.PodeChecar)
            throw new InvalidOperationException(prescricao.Situacao switch
            {
                SituacaoPrescricao.Rascunho =>
                    $"A prescrição {prescricao.Numero} ainda não foi assinada pelo "
                    + "profissional e não pode ser executada.",
                SituacaoPrescricao.Encerrada =>
                    $"A execução da prescrição {prescricao.Numero} já foi encerrada.",
                _ =>
                    $"A prescrição {prescricao.Numero} foi cancelada."
            });

        if (item.Suspenso)
            throw new InvalidOperationException(
                $"O item foi suspenso pelo prescritor ({item.MotivoSuspensao}) e não deve "
                + "ser administrado.");

        return item;
    }

    private ChecagemPrescricao Montar(
        ItemPrescricaoInterna item, SituacaoChecagem situacao, TimeOnly hora,
        IdentificacaoExecutante executante, string? justificativa)
    {
        if (string.IsNullOrWhiteSpace(executante.Nome))
            throw new InvalidOperationException(
                "A checagem precisa de quem executou. Sem isso ela é uma afirmação de "
                + "ninguém — e é justamente o vínculo com a pessoa que a torna útil.");

        var limpa = string.IsNullOrWhiteSpace(justificativa) ? null : justificativa.Trim();

        if (situacao == SituacaoChecagem.NaoRealizado && limpa is null)
            throw new InvalidOperationException(
                "Item não realizado exige justificativa. Sem ela, a folha registra que a "
                + "medicação não entrou no paciente e não diz por quê — que é a pior linha "
                + "possível de um prontuário.");

        ExigirHoraPlausivel(item, hora);

        return new ChecagemPrescricao
        {
            ItemPrescricaoInternaId = item.Id,
            Situacao = situacao,
            HoraRealizacao = hora,
            Justificativa = limpa,
            ExecutanteUsuarioId = executante.UsuarioId,
            ExecutanteNome = executante.Nome.Trim(),
            ExecutanteConselho = string.IsNullOrWhiteSpace(executante.Conselho)
                ? null : executante.Conselho.Trim(),
            RegistradoEm = _agora()
        };
    }

    /// <summary>
    /// Recusa hora no futuro. Só faz sentido conferir na folha de HOJE: numa folha de
    /// ontem qualquer horário do dia já passou, e numa folha lançada com atraso a
    /// comparação com o relógio de agora não diria nada.
    /// </summary>
    private void ExigirHoraPlausivel(ItemPrescricaoInterna item, TimeOnly hora)
    {
        var agora = _agora();
        var hoje = DateOnly.FromDateTime(agora);
        var data = item.Prescricao?.Data ?? hoje;
        if (data != hoje) return;

        var momento = data.ToDateTime(hora);
        if (momento <= agora + FolgaDeRelogio) return;

        throw new InvalidOperationException(
            $"A hora informada ({hora:HH\\:mm}) ainda não chegou. Checar antes de "
            + "administrar deixaria registrado como feito um item que o paciente pode não "
            + "receber — anote depois de administrar.");
    }

    /// <summary>
    /// Transforma a reação observada em ALERGIA na lista de problemas, na mesma transação.
    ///
    /// A alergia nasce ATIVA e sem CID: quem está com o paciente reagindo não vai
    /// classificar nada. O que importa é que a palavra ("dipirona") passe a existir onde a
    /// conferência da parcela 40 a procura.
    /// </summary>
    private async Task RegistrarAlergiaSePedido(
        ItemPrescricaoInterna item, string? alergia,
        IdentificacaoExecutante executante, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(alergia)) return;
        if (item.Prescricao is null) return;

        await _repo.AdicionarProblemaAsync(new ProblemaPaciente
        {
            PacienteId = item.Prescricao.PacienteId,
            Natureza = NaturezaProblema.Alergia,
            Descricao = alergia.Trim(),
            Situacao = SituacaoProblema.Ativo,
            Inicio = DateOnly.FromDateTime(DateTime.Today),
            Observacoes = $"Registrada na execução da prescrição {item.Prescricao.Numero}: "
                        + $"reação ao administrar {item.TextoCompleto}.",
            CriadoEm = DateTime.Now,
            CriadoPor = executante.Nome
        }, ct);
    }

    private static string Descrever(ItemPrescricaoInterna item, ChecagemPrescricao checagem)
    {
        var texto = $"{item.Prescricao?.Numero} · item {item.Ordem} ({item.TextoCompleto}) · "
                  + $"{RotularSituacao(checagem.Situacao)} às {checagem.HoraRealizacao:HH\\:mm} "
                  + $"por {checagem.ExecutanteNome}";

        return checagem.Justificativa is null ? texto : $"{texto} · {checagem.Justificativa}";
    }

    private static string RotularSituacao(SituacaoChecagem situacao)
        => situacao == SituacaoChecagem.Realizado ? "realizado" : "NÃO realizado";
}
