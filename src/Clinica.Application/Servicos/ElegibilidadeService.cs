using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Responde "o que eu preciso saber com este paciente na minha frente?" — no BALCÃO,
/// antes da sessão.
///
/// Nasceu respondendo só à elegibilidade do convênio: carteirinha vencida e cota
/// estourada só apareciam na hora de faturar, quando o serviço já tinha sido prestado e o
/// prejuízo já era certo. Este serviço juntou num lugar só o que existia espalhado
/// (<see cref="Paciente.CarteirinhaVencida"/>, <see cref="AutorizacaoService"/>,
/// <see cref="ConsentimentoService"/>) e entregou à recepção enquanto ainda dava para
/// resolver.
///
/// A parcela 27 alargou o contrato de propósito, e o critério é um só: **entra aqui o que
/// se resolve com o paciente presente e fica caro depois**. Por isso chegaram dois avisos
/// que vêm dos OUTROS módulos — a conta vencida (Financeiro) e a guia glosada
/// (Faturamento). Os dois estavam gravados havia parcelas e nenhum chegava ao balcão, que
/// é o único lugar onde a pessoa que pode resolvê-los está de corpo presente.
///
/// Ele NUNCA impede o atendimento: quem decide é a clínica. Ele informa.
/// </summary>
public sealed class ElegibilidadeService
{
    private readonly IClinicaRepositorio _repo;
    private readonly AutorizacaoService _autorizacoes;
    private readonly ConsentimentoService _consentimentos;
    private readonly ConsultaService _consultas;
    private readonly InadimplenciaService? _inadimplencia;
    private readonly PacoteService? _pacotes;
    private readonly TermoProcedimentoService? _termos;

    /// <summary>Dias de antecedência para começar a avisar da carteirinha.</summary>
    public const int JanelaAvisoCarteirinhaDias = 30;

    /// <summary>
    /// A partir de quantos dias de atraso o balcão é avisado. Não é zero de propósito: a
    /// conta que venceu ontem quase sempre está em trânsito (boleto compensando, PIX de
    /// domingo), e um alerta que dispara para todo mundo é um alerta que ninguém lê.
    /// </summary>
    public const int AtrasoMinimoParaAvisarDias = 5;

    /// <summary>
    /// Dias de antecedência para avisar que o pacote vai vencer COM SALDO. Menor que a
    /// janela da carteirinha de propósito: renovar pacote é decisão de compra, e um mês de
    /// antecedência transformaria o aviso em ruído de fundo em toda sessão do tratamento.
    /// </summary>
    public const int JanelaAvisoPacoteDias = 10;

    public ElegibilidadeService(
        IClinicaRepositorio repo,
        AutorizacaoService autorizacoes,
        ConsentimentoService consentimentos,
        ConsultaService consultas,
        InadimplenciaService? inadimplencia = null,
        PacoteService? pacotes = null,
        TermoProcedimentoService? termos = null)
    {
        _repo = repo;
        _autorizacoes = autorizacoes;
        _consentimentos = consentimentos;
        // Obrigatório, ao contrário do Financeiro abaixo: a consulta renovável é do mesmo
        // faturamento que este serviço já confere, sem nada a montar por fora. Deixá-la
        // opcional faria a conferência sumir em silêncio onde ninguém passasse o serviço
        // — a tela diria "está tudo certo" sobre o que não olhou.
        _consultas = consultas;
        // Opcional para o serviço continuar construível sem o Financeiro montado — é a
        // mesma escolha do ParametrosService no GlosaService. Sem ele, a conferência
        // financeira simplesmente não roda; ela não é a razão de existir desta classe.
        _inadimplencia = inadimplencia;
        // Opcional pela mesma razão do Financeiro acima: o pacote é do módulo Financeiro,
        // e este serviço tem de continuar construível sem ele montado.
        _pacotes = pacotes;
        // Opcional, e aqui a razão é outra: a clínica que não cadastrou nenhuma exigência
        // de termo não deve pagar uma leitura por check-in. Sem ele, a conferência não
        // roda — e como ela só produz alerta quando há exigência ATIVA, não rodar e não
        // haver exigência dão a mesma tela.
        _termos = termos;
    }

    public async Task<Elegibilidade> ConferirAsync(
        int pacienteId, DateOnly referencia, CancellationToken ct = default)
    {
        var paciente = await _repo.ObterPacienteAsync(pacienteId, ct)
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        var alertas = new List<AlertaElegibilidade>();

        ConferirCarteirinha(paciente, referencia, alertas);
        await ConferirConsultaAsync(pacienteId, referencia, alertas, ct);
        await ConferirCotaAsync(pacienteId, referencia, alertas, ct);
        await ConferirConsentimentoAsync(pacienteId, alertas, ct);
        await ConferirDebitoAsync(pacienteId, referencia, alertas, ct);
        await ConferirGlosaAsync(pacienteId, referencia, alertas, ct);
        await ConferirPacoteAsync(pacienteId, referencia, alertas, ct);
        await ConferirTermoAsync(pacienteId, referencia, alertas, ct);

        return new Elegibilidade(pacienteId, paciente.Nome, alertas);
    }

    /// <summary>
    /// Termo de procedimento pendente, ou assinado com declaração negada (parcela 66).
    ///
    /// Mora AQUI, e não na tela da fila, pela razão de sempre: daqui o aviso chega sozinho
    /// ao agendamento, ao check-in, à ficha e ao Consultório. Foi o que a parcela 48 fez
    /// com o pacote, e a alternativa — escrever a conferência na tela que o cliente
    /// apontou — deixaria as outras três portas passando.
    /// </summary>
    private async Task ConferirTermoAsync(
        int pacienteId, DateOnly referencia, List<AlertaElegibilidade> alertas,
        CancellationToken ct)
    {
        // Opcional pela mesma razão do Financeiro e do pacote: este serviço tem de
        // continuar construível sem o módulo que trouxe a feature.
        if (_termos is null) return;

        var situacoes = await _termos.SituacaoDoDiaAsync(pacienteId, referencia, ct);

        foreach (var s in situacoes.Where(s => s.Pendente))
            alertas.Add(new AlertaElegibilidade(
                ImpedimentoElegibilidade.TermoProcedimentoPendente,
                NivelUrgencia.Vermelho,
                $"Falta o termo \"{s.NomeDoTermo}\" assinado pelo paciente."));

        // A recusa NÃO vira alerta de pendência: ela já foi decidida, e repeti-la todo dia
        // como se faltasse assinar mandaria o balcão apresentar de novo um termo que a
        // pessoa recusou na frente dele. Quem precisa saber dela é quem faz o
        // procedimento, e isso está na tela do termo.

        foreach (var s in situacoes.Where(s => s.TemDeclaracaoNegada))
            alertas.Add(new AlertaElegibilidade(
                ImpedimentoElegibilidade.DeclaracaoDoTermoNegada,
                NivelUrgencia.Vermelho,
                $"No termo \"{s.NomeDoTermo}\" o paciente respondeu NÃO em: "
                + $"{string.Join("; ", s.DeclaracoesNegadas)}."));
    }

    private static void ConferirCarteirinha(
        Paciente paciente, DateOnly referencia, List<AlertaElegibilidade> alertas)
    {
        if (paciente.ValidadeCarteirinha is not { } validade) return;

        if (validade < referencia)
        {
            alertas.Add(new AlertaElegibilidade(
                ImpedimentoElegibilidade.CarteirinhaVencida,
                NivelUrgencia.Vermelho,
                $"Carteirinha vencida em {validade:dd/MM/yyyy} — o convênio recusa a guia."));
            return;
        }

        var dias = validade.DayNumber - referencia.DayNumber;
        if (dias <= JanelaAvisoCarteirinhaDias)
            alertas.Add(new AlertaElegibilidade(
                ImpedimentoElegibilidade.CarteirinhaAVencer,
                NivelUrgencia.Amarelo,
                $"Carteirinha vence em {dias} dia(s) ({validade:dd/MM/yyyy})."));
    }

    /// <summary>
    /// Consulta renovável vencida ou a vencer.
    ///
    /// A consulta é o papel que cobre laudo, receita e dúvida por 22 ou 30 dias conforme o
    /// convênio, e ela era lida em exatamente dois lugares — a aba Consultas e o painel de
    /// pendências —, nenhum deles aberto no momento em que o paciente está no balcão.
    /// Renovar com ele presente é uma assinatura; descobrir a consulta vencida na hora de
    /// faturar é ligar para quem já foi embora.
    ///
    /// Só entra aqui quem JÁ TEM consulta emitida (<c>ARenovar</c>, não
    /// <c>PrecisaRenovar</c>) — ver o comentário do modelo.
    /// </summary>
    private async Task ConferirConsultaAsync(
        int pacienteId, DateOnly referencia,
        List<AlertaElegibilidade> alertas, CancellationToken ct)
    {
        var situacao = await _consultas.DoPacienteAsync(pacienteId, referencia, ct);
        if (situacao?.AvisoRenovacao is not { } aviso) return;

        alertas.Add(new AlertaElegibilidade(
            situacao.Vencida
                ? ImpedimentoElegibilidade.ConsultaVencida
                : ImpedimentoElegibilidade.ConsultaARenovar,
            // Vencida é vermelho pela regra deste serviço: o convênio recusa o que for
            // faturado sem consulta vigente. A que ainda vai vencer é amarelo — dá tempo,
            // e pintar as duas de vermelho faria a urgente deixar de se distinguir.
            situacao.Vencida ? NivelUrgencia.Vermelho : NivelUrgencia.Amarelo,
            aviso));
    }

    private async Task ConferirCotaAsync(
        int pacienteId, DateOnly referencia,
        List<AlertaElegibilidade> alertas, CancellationToken ct)
    {
        var vigente = await _autorizacoes.VigenteAsync(pacienteId, referencia, ct);

        if (vigente is null)
        {
            // Só avisa se ESTE paciente já teve senha registrada alguma vez — aí a
            // ausência de uma vigente é notícia (venceu, acabou). Numa clínica que não
            // controla senha, avisar sempre seria um alerta que dispara para todo mundo
            // — e alerta que sempre aparece é alerta que ninguém lê.
            var historico = await _autorizacoes.DoPacienteAsync(pacienteId, ct);
            if (historico.Count == 0) return;

            alertas.Add(new AlertaElegibilidade(
                ImpedimentoElegibilidade.SemAutorizacaoVigente,
                NivelUrgencia.Amarelo,
                "Nenhuma autorização vigente — a anterior venceu ou foi encerrada."));
            return;
        }

        if (vigente.Esgotada)
        {
            alertas.Add(new AlertaElegibilidade(
                ImpedimentoElegibilidade.CotaEsgotada,
                NivelUrgencia.Vermelho,
                $"Cota esgotada ({vigente.Resumo}) — a próxima sessão vira glosa 2006."));
            return;
        }

        if (vigente.NaUltima)
            alertas.Add(new AlertaElegibilidade(
                ImpedimentoElegibilidade.CotaQuaseNoFim,
                NivelUrgencia.Amarelo,
                $"Última sessão autorizada ({vigente.Resumo}) — peça a renovação da senha."));
    }

    private async Task ConferirConsentimentoAsync(
        int pacienteId, List<AlertaElegibilidade> alertas, CancellationToken ct)
    {
        if (await _consentimentos.VigenteAsync(
                pacienteId, FinalidadeConsentimento.TratamentoDeDados, ct))
            return;

        alertas.Add(new AlertaElegibilidade(
            ImpedimentoElegibilidade.SemConsentimentoLgpd,
            NivelUrgencia.Amarelo,
            "Sem consentimento LGPD de tratamento de dados — colha no balcão."));
    }

    /// <summary>
    /// O paciente deve à clínica (parcela 27) — o Financeiro respondendo ao balcão.
    ///
    /// A dívida existia desde a parcela 12 e virou tela na 23, mas só no Financeiro e no
    /// Gerente: o paciente entrava, era atendido e saía, e ninguém no balcão sabia. O
    /// momento em que ele está na recepção é o único em que cobrar não custa nada — uma
    /// frase. Depois custa telefonema, mensagem e constrangimento.
    ///
    /// É AVISO, como todo o resto deste serviço: ninguém deixa de ser atendido por dever.
    /// E a mensagem não traz nada do que foi feito na sessão — a conversa é sobre a conta.
    /// </summary>
    private async Task ConferirDebitoAsync(
        int pacienteId, DateOnly referencia,
        List<AlertaElegibilidade> alertas, CancellationToken ct)
    {
        if (_inadimplencia is null) return;

        var devedor = await _inadimplencia.DoPacienteAsync(pacienteId, referencia, ct);
        if (devedor is null || devedor.DiasMaiorAtraso < AtrasoMinimoParaAvisarDias) return;

        alertas.Add(new AlertaElegibilidade(
            ImpedimentoElegibilidade.PacienteEmDebito,
            // Amarelo, nunca vermelho: vermelho neste serviço significa "a guia vai ser
            // recusada", que é problema do convênio. Dívida é assunto de conversa.
            NivelUrgencia.Amarelo,
            $"{devedor.Contas} conta(s) em aberto — {devedor.Total:C}, a mais antiga "
            + $"vencida há {devedor.DiasMaiorAtraso} dia(s). O paciente está aqui: é a "
            + "hora barata de combinar o acerto."));
    }

    /// <summary>
    /// Guia deste paciente glosada pelo convênio (parcela 27) — o Faturamento respondendo
    /// ao balcão.
    ///
    /// Boa parte das glosas é documental: falta assinatura do paciente, falta a via da
    /// guia, falta o número da carteirinha atualizado. Todas essas se resolvem em trinta
    /// segundos com a pessoa presente, e nenhuma se resolve depois sem telefonar,
    /// remarcar ou perder o prazo de recurso — que é o que vinha acontecendo, porque a
    /// glosa só era vista na tela do faturamento, por quem não atende ninguém.
    /// </summary>
    private async Task ConferirGlosaAsync(
        int pacienteId, DateOnly referencia,
        List<AlertaElegibilidade> alertas, CancellationToken ct)
    {
        var glosadas = await _repo.CodigosGlosadosDoPacienteAsync(pacienteId, ct);
        if (glosadas.Count == 0) return;

        // A mais urgente manda no alerta: a que tem menos prazo de recurso restante. As
        // outras entram na contagem — uma linha por guia encheria o balcão de texto no
        // momento em que ele tem menos tempo para ler.
        var maisUrgente = glosadas
            .OrderBy(c => c.DataLimiteRecurso ?? DateOnly.MaxValue)
            .First();

        var dias = maisUrgente.DiasParaFimRecurso(referencia);
        var prazo = dias switch
        {
            null => "sem prazo de recurso registrado",
            < 0 => $"prazo de recurso VENCIDO há {-dias.Value} dia(s)",
            0 => "o prazo de recurso vence HOJE",
            _ => $"restam {dias} dia(s) para recorrer"
        };

        var motivo = string.IsNullOrWhiteSpace(maisUrgente.MotivoGlosa)
            ? maisUrgente.MotivoGlosaCodigo
            : maisUrgente.MotivoGlosa;

        var quantas = glosadas.Count == 1
            ? "Guia glosada pelo convênio"
            : $"{glosadas.Count} guias glosadas pelo convênio (a mais urgente)";

        alertas.Add(new AlertaElegibilidade(
            ImpedimentoElegibilidade.GuiaGlosada,
            // Vermelho só quando ainda dá para agir e o tempo está acabando; passado o
            // prazo o aviso perde a função de pedir pressa e vira informação.
            dias is >= 0 and <= 7 ? NivelUrgencia.Vermelho : NivelUrgencia.Amarelo,
            $"{quantas}: {(string.IsNullOrWhiteSpace(motivo) ? "sem motivo registrado" : motivo)} — {prazo}. "
            + "Se faltou assinatura ou documento, resolva agora, com o paciente aqui."));
    }

    /// <summary>
    /// O pacote de sessões do paciente (parcela 48) — o Financeiro respondendo ao balcão.
    ///
    /// <b>Não confundir com a cota do convênio.</b> As duas contam sessões e não têm nada a
    /// ver uma com a outra: a cota evita GLOSA, o pacote evita ATENDER DE GRAÇA. É por isso
    /// que são dois alertas, e não um — quem lê no balcão precisa saber com quem falar, o
    /// convênio ou o paciente.
    ///
    /// O que faltava não era a capacidade: <c>PacoteService.DoPacienteAsync</c> existe
    /// desde a parcela 4. Faltava ela chegar ao momento da decisão. O pacote só aparecia no
    /// <c>FechamentoSessaoService</c>, que roda no **Finalizar** — o último passo, quando a
    /// sessão já aconteceu e não há mais o que combinar. Marcar a 11ª sessão de um pacote
    /// de 10 era descoberto depois de ela ser prestada.
    ///
    /// <b>Só avisa quem TEM ou TEVE pacote.</b> Metade da clínica é de convênio e nunca
    /// comprou nada; avisar "sem pacote" para essa metade é o alerta que dispara para todo
    /// mundo — e alerta que sempre aparece é alerta que ninguém lê. É a mesma regra que a
    /// cota já seguia logo acima.
    ///
    /// <b>Amarelo, nunca vermelho.</b> Vermelho neste serviço significa "a guia vai ser
    /// recusada", que é problema do convênio. Pacote acabado é assunto de conversa e de
    /// venda — como a dívida.
    /// </summary>
    private async Task ConferirPacoteAsync(
        int pacienteId, DateOnly referencia,
        List<AlertaElegibilidade> alertas, CancellationToken ct)
    {
        if (_pacotes is null) return;

        var pacotes = await _pacotes.DoPacienteAsync(pacienteId, referencia, ct);
        if (pacotes.Count == 0) return;

        // Cancelado fica de fora: ele não acabou nem venceu — foi desfeito, e o motivo já
        // está registrado. Cobrar a renovação de um pacote que a clínica cancelou seria
        // pedir ao balcão que explicasse a própria decisão ao paciente.
        var relevantes = pacotes
            .Where(p => p.Situacao != StatusPacote.Cancelado)
            .ToList();
        if (relevantes.Count == 0) return;

        // Havendo um ATIVO, é dele que se fala: o paciente tem sessão comprada para hoje, e
        // o esgotado do mês passado não é notícia. Sem ativo nenhum é que a ausência vira
        // aviso — e aí manda o mais recente, que é o que ele lembra ter comprado.
        var ativo = relevantes
            .Where(p => p.Ativo)
            .OrderBy(p => p.ValidoAte ?? DateOnly.MaxValue)
            .FirstOrDefault();

        if (ativo is null)
        {
            var ultimo = relevantes.OrderByDescending(p => p.DataCompra).First();
            var porque = ultimo.Situacao == StatusPacote.Expirado
                ? $"venceu em {ultimo.ValidoAte:dd/MM/yyyy} com saldo sobrando"
                : "as sessões contratadas acabaram";

            alertas.Add(new AlertaElegibilidade(
                ImpedimentoElegibilidade.PacoteEsgotado,
                NivelUrgencia.Amarelo,
                $"Pacote \"{ultimo.Nome}\" sem saldo — {porque}. "
                + "Esta sessão não debita de pacote nenhum: combine a renovação ou a "
                + "cobrança agora, com o paciente aqui."));
            return;
        }

        // Última sessão do pacote ativo.
        if (ativo.SaldoSessoes is 1)
        {
            alertas.Add(new AlertaElegibilidade(
                ImpedimentoElegibilidade.PacoteNoFim,
                NivelUrgencia.Amarelo,
                $"Pacote \"{ativo.Nome}\": esta é a ÚLTIMA sessão ({ativo.SaldoRotulo}). "
                + "É a hora barata de vender a renovação."));
            return;
        }

        // Ou vence com saldo sobrando — o dinheiro que o paciente perde, e que a clínica
        // devolve em forma de reclamação se ninguém avisar.
        if (ativo.ValidoAte is { } ate)
        {
            var dias = ate.DayNumber - referencia.DayNumber;
            if (dias >= 0 && dias <= JanelaAvisoPacoteDias)
                alertas.Add(new AlertaElegibilidade(
                    ImpedimentoElegibilidade.PacoteNoFim,
                    NivelUrgencia.Amarelo,
                    $"Pacote \"{ativo.Nome}\" vence em {dias} dia(s) ({ate:dd/MM/yyyy}) "
                    + $"com {ativo.SaldoRotulo} por usar — dá para remarcar o que falta."));
        }
    }
}
