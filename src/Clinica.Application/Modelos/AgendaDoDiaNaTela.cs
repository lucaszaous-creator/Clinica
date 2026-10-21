using System.Globalization;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>
/// Tudo o que a AGENDA DO DIA do balcão leu numa carga (<see cref="LeituraAgendaDoDia"/>).
/// A tela guarda isto e remonta as linhas a cada minuto SEM voltar ao banco — o atraso e a
/// espera correm com o relógio, o resto não muda até a próxima leitura.
/// </summary>
public sealed record DadosDaAgendaDoDia(
    DateOnly Dia,
    IReadOnlyList<Agendamento> Horarios,
    IReadOnlySet<int> ComPendencia,
    IReadOnlyDictionary<int, IReadOnlyList<SituacaoTermo>> Termos,
    IReadOnlyDictionary<int, RespostaDeConfirmacao> Confirmacoes,
    IReadOnlyDictionary<int, (int Usadas, int Contratadas)> Pacotes,
    IReadOnlySet<int> JaFechados)
{
    public static DadosDaAgendaDoDia Vazio(DateOnly dia) => new(dia, [], new HashSet<int>(),
        new Dictionary<int, IReadOnlyList<SituacaoTermo>>(), new Dictionary<int, RespostaDeConfirmacao>(),
        new Dictionary<int, (int, int)>(), new HashSet<int>());
}

/// <summary>O botão sólido da linha — um passo só por horário.</summary>
public enum PassoDaAgenda
{
    Nenhum,

    /// <summary>"Chegou" — o check-in do balcão; só no dia da sessão.</summary>
    Chegou,

    /// <summary>"Concluir" — o caminho de EXCEÇÃO (o profissional não finalizou pelo consultório).</summary>
    Concluir,

    /// <summary>"Debitar pacote"/"Fechar sessão" — a sessão concluída com dinheiro por fechar.</summary>
    FecharSessao
}

/// <summary>Os itens do menu "⋯" da linha, na ordem em que aparecem.</summary>
public enum AcaoDaLinhaAgenda
{
    Ficha,
    FecharSessao,
    ConferirConvenio,
    Separador,
    MarcarFalta,
    Cancelar,
    AbrirHorario
}

/// <summary>Quais bits a pessoa tem — a metade VISÍVEL das ações; a guarda é da tela.</summary>
public sealed record PermissoesDaAgendaDoDia(
    bool MoverFila, bool ConcluirSessao, bool FecharSessao, bool EditarHorario,
    bool VerFicha, bool FaltaOuCancelamento)
{
    /// <summary>Lê da sessão as MESMAS contas do <c>FilaViewModel</c> WPF.</summary>
    public static PermissoesDaAgendaDoDia Da(SessaoUsuario s) => new(
        MoverFila: s.PodeAlgum(Permissao.EditarAgenda | Permissao.MovimentarFila),
        ConcluirSessao: s.Pode(Permissao.EditarAgenda | Permissao.LancarAtendimento),
        FecharSessao: s.Pode(Permissao.EditarAgenda),
        EditarHorario: s.Pode(Permissao.EditarAgenda),
        VerFicha: s.Pode(Permissao.VerFichaPaciente),
        FaltaOuCancelamento: s.Pode(Permissao.EditarAgenda));
}

/// <summary>Uma linha da agenda do dia, já com tudo o que a tela desenha.</summary>
public sealed record LinhaAgendaDoDia
{
    public required int AgendamentoId { get; init; }
    public required int PacienteId { get; init; }
    public required string Paciente { get; init; }
    public required DateTime DataHora { get; init; }

    /// <summary>"09:00–10:00" no dia; "12/10 09:00" na conclusão pendente de outro dia.</summary>
    public required string Horario { get; init; }
    public required string Modalidade { get; init; }
    public required ModalidadeAtendimento Familia { get; init; }
    public int? ProfissionalId { get; init; }
    public required string Profissional { get; init; }

    /// <summary>A sala, ou vazio — nunca o travessão (a clínica não usa salas).</summary>
    public required string Sala { get; init; }
    public string Convenio { get; init; } = string.Empty;
    public string? Observacoes { get; init; }
    public byte[]? Foto { get; init; }
    public required StatusAgendamento Situacao { get; init; }
    public required EtapaFila Etapa { get; init; }
    public required bool EhEncaixe { get; init; }
    public required bool EhRetornoDoSegundoCodigo { get; init; }
    public required string Lancamento { get; init; }
    public required bool EhHoje { get; init; }

    /// <summary>A palavra da coluna STATUS (o vocabulário do Meu dia — <see cref="StatusDaFila"/>).</summary>
    public required string Status { get; init; }

    /// <summary>A hora do fato sob a palavra ("chegou às 14:40 · espera 12 min").</summary>
    public required string StatusDetalhe { get; init; }

    /// <summary>"Confirmou", "Não confirmou", "Não vem — remarcar" — só em AGUARDANDO.</summary>
    public required string Confirmacao { get; init; }
    public required IReadOnlyList<SeloFila> Selos { get; init; }
    public int? PacoteUsadas { get; init; }
    public int? PacoteContratadas { get; init; }
    public required bool TemGuiaPendente { get; init; }
    public required bool FechamentoPendente { get; init; }

    public bool TemSala => !string.IsNullOrWhiteSpace(Sala);
    public bool TemPacote => PacoteUsadas is not null && PacoteContratadas is not null;

    /// <summary>Cancelado, falta ou substituído: a linha FICA, apagada (a regra da folha do dia).</summary>
    public bool ForaDaFila => StatusDaFila.ForaDaFila(Situacao);

    /// <summary>Só horário em aberto aceita falta, cancelamento e o "Editar".</summary>
    public bool EmAberto => Etapa is not (EtapaFila.Finalizado or EtapaFila.ForaDaFila);

    /// <summary>
    /// O paciente pediu pelo WhatsApp para cancelar (out/2026): a linha mostra "Aceitar
    /// cancelamento" e a recepção cancela ali mesmo, sem trocar de tela.
    /// </summary>
    public bool PediuCancelar => EmAberto && Confirmacao == RodadaDeConfirmacao.SeloPediuCancelar;

    public bool PodeFechar => Etapa == EtapaFila.Finalizado;

    /// <summary>O grupo da COR — o mesmo do filtro de situação e do traço da linha.</summary>
    public string Grupo => SituacaoVisualAgenda.Grupo(Situacao, Etapa);

    /// <summary>Modalidade · convênio · pacote N/M — a linha sob o nome.</summary>
    public string Contexto => string.Join(" · ", new[]
    {
        Modalidade,
        Convenio.Length == 0 ? null : Convenio,
        TemPacote ? $"pacote {PacoteUsadas}/{PacoteContratadas}" : null
    }.Where(l => !string.IsNullOrEmpty(l)));

    /// <summary>O passo seguinte — um só por linha.</summary>
    public PassoDaAgenda Passo => Etapa switch
    {
        // "Chegou no local" é só do dia da sessão; de "No local" o balcão não tem passo:
        // quem age em seguida é quem atende.
        EtapaFila.Aguardando when EhHoje => PassoDaAgenda.Chegou,
        EtapaFila.EmAtendimento => PassoDaAgenda.Concluir,
        EtapaFila.Finalizado when FechamentoPendente => PassoDaAgenda.FecharSessao,
        _ => PassoDaAgenda.Nenhum
    };

    /// <summary>O rótulo do botão do passo.</summary>
    public string RotuloDoPasso => Passo switch
    {
        PassoDaAgenda.Chegou => "Chegou",
        PassoDaAgenda.Concluir => "Concluir",
        // Com pacote é o que se debita; o particular sem pacote fecha o CAIXA da sessão —
        // "Debitar pacote" diria a ele algo que não vai acontecer.
        PassoDaAgenda.FecharSessao => TemPacote ? "Debitar pacote" : "Fechar sessão",
        _ => string.Empty
    };

    /// <summary>A dica da linha: o que não coube nela e alguém pode precisar.</summary>
    public string Detalhe => string.Join("\n", new[]
    {
        $"{Horario} · {Modalidade}",
        TemSala ? $"{Profissional} · sala {Sala}" : Profissional,
        TemPacote ? $"Pacote {PacoteUsadas}/{PacoteContratadas} (o que vence primeiro é o que a sessão debita)" : null,
        EhEncaixe ? "Encaixe — marcado por cima de um horário ocupado" : null,
        Confirmacao switch
        {
            "Confirmou" => "Confirmou a presença na rodada de confirmação",
            RodadaDeConfirmacao.SeloNaoVem => "Respondeu na rodada que NÃO vem — remarque e libere o horário",
            RodadaDeConfirmacao.SeloPediuCancelar => "Pediu pelo WhatsApp para cancelar — aceite o cancelamento nesta linha",
            "Não confirmou" => "Avisado na rodada de confirmação, sem resposta",
            _ => null
        },
        EhRetornoDoSegundoCodigo ? "Legado: retorno do 2º código — guia não é atendimento, pode apagar" : null,
        string.IsNullOrWhiteSpace(Observacoes) ? null : $"Obs.: {Observacoes}",
        Lancamento
    }.Where(l => l is not null));
}

/// <summary>O recorte da lista: busca, modalidade, situação e profissional.</summary>
public sealed record FiltroAgendaDoDia(
    string Busca = "",
    string? Modalidade = null,
    string Situacao = "todos",
    int? ProfissionalId = null,
    string Resposta = "")
{
    /// <summary>Busca, modalidade ou situação — o profissional não conta (é contexto, não filtro esquecido).</summary>
    public bool TemFiltros => !string.IsNullOrWhiteSpace(Busca)
                              || !string.IsNullOrEmpty(Modalidade) && Modalidade != AgendaDoDiaNaTela.TodasModalidades
                              || Situacao != "todos"
                              || !string.IsNullOrEmpty(Resposta);
}

/// <summary>O que a tela mostra depois do recorte.</summary>
public sealed record RecorteAgendaDoDia(
    IReadOnlyList<LinhaAgendaDoDia> Visiveis,
    IReadOnlyDictionary<string, int> Contagens,
    IReadOnlyList<string> Modalidades,
    int Total,
    string Resumo,
    bool NenhumCorresponde,
    IReadOnlyDictionary<string, int>? Respostas = null);

/// <summary>Uma opção do combo de profissional, com quantos horários VIVOS ela recorta.</summary>
public sealed record ProfissionalDaAgenda(int? Id, string Nome, int Quantidade)
{
    public string Rotulo => $"{Nome} · {Quantidade}";
}

/// <summary>
/// A AGENDA DO DIA do balcão, o que a tela AFIRMA (out/2026 — a tela virou Blazor). É a
/// mesma regra que o <c>FilaViewModel</c>/<c>FiltroListaAgenda</c> WPF tinham escrita dentro
/// do ViewModel e do code-behind, onde o <c>dotnet test</c> não alcançava: a linha (status,
/// passo, selos, confirmação, pendência de fechamento), o filtro (busca sem acento, modalidade,
/// as oito pílulas de situação com contagem ANTES do clique, o profissional) e as frases de
/// desfecho.
/// </summary>
public static class AgendaDoDiaNaTela
{
    public const string TodasModalidades = "Todas as modalidades";

    private static readonly CompareInfo Comparador = new CultureInfo("pt-BR").CompareInfo;

    /// <summary>As OITO pílulas de situação, na ordem da agenda em produção.</summary>
    public static readonly IReadOnlyList<(string Chave, string Rotulo)> Situacoes =
    [
        ("todos", "Todos"), ("pendente", "Pendentes"), ("no-local", "No local"),
        ("em-atendimento", "Em atendimento"), ("atendido", "Atendidos"),
        ("cancelado", "Cancelados"), ("faltou", "Faltas"), ("substituido", "Substituídos")
    ];

    /// <summary>
    /// As respostas ao LEMBRETE, para a secretária correr atrás assim que abre o dia: quem
    /// avisou que não vem (remarcar e liberar o horário), quem foi avisado e não respondeu
    /// (ligar) e quem confirmou. A palavra é a do selo da linha — <c>SeloDaLista</c> — e só
    /// conta quem ainda VEM (o selo só existe em AGUARDANDO), então o número nunca inclui
    /// quem já chegou.
    /// </summary>
    public static readonly IReadOnlyList<(string Chave, string Rotulo, string Selo)> Respostas =
    [
        ("nao-vem", "Não vêm", RodadaDeConfirmacao.SeloNaoVem),
        ("cancelar", "Pediram cancelamento", RodadaDeConfirmacao.SeloPediuCancelar),
        ("sem-resposta", "Sem resposta", RodadaDeConfirmacao.SeloSemResposta),
        ("confirmou", "Confirmaram", RodadaDeConfirmacao.SeloConfirmou)
    ];

    /// <summary>Monta as linhas do dia, na ordem da HORA. Cancelado e falta ENTRAM (apagados).</summary>
    public static IReadOnlyList<LinhaAgendaDoDia> Montar(DadosDaAgendaDoDia d, DateTime agora)
        => d.Horarios.OrderBy(a => a.DataHora).Select(a => Linha(a, d, agora)).ToList();

    private static LinhaAgendaDoDia Linha(Agendamento a, DadosDaAgendaDoDia d, DateTime agora)
    {
        var vivo = a.Etapa != EtapaFila.ForaDaFila;

        // O selo de confirmação só fala em AGUARDANDO — depois do check-in é ruído.
        var confirmacao = a.Etapa == EtapaFila.Aguardando && d.Confirmacoes.TryGetValue(a.Id, out var resposta)
            ? RodadaDeConfirmacao.SeloDaLista(resposta)
            : string.Empty;

        // Os avisos do PACIENTE não entram na linha cancelada: selo numa linha apagada é ruído.
        var termos = vivo && d.Termos.TryGetValue(a.PacienteId, out var t) ? t : [];
        var termoPendente = termos.Any(x => x.PendenteSemAssinatura);
        var termoAConferir = termos.Any(x => x.AssinaturaRemotaAguardaConferencia);
        var guia = vivo && d.ComPendencia.Contains(a.PacienteId);
        (int Usadas, int Contratadas)? pacote = vivo && d.Pacotes.TryGetValue(a.PacienteId, out var p) ? p : null;

        var espera = a.EsperaMinutos(agora);
        var chamada = a.ChamadoHaMinutos(agora);
        var atraso = a.AtrasoMinutos(agora);
        var ehHoje = a.DataHora.Date == agora.Date;

        var status = a.Status == StatusAgendamento.Agendado
                     && (a.FimAtendimentoEm is not null || (a.InicioAtendimentoEm is not null && a.DataHora.Date < agora.Date))
            ? "Conclusão pendente"
            : StatusDaFila.Palavra(a.Status, a.Etapa);

        return new LinhaAgendaDoDia
        {
            AgendamentoId = a.Id,
            PacienteId = a.PacienteId,
            Paciente = a.Paciente?.Nome ?? "(paciente removido)",
            DataHora = a.DataHora,
            Horario = DateOnly.FromDateTime(a.DataHora) == d.Dia
                ? $"{a.DataHora:HH\\:mm}–{a.FimPrevisto:HH\\:mm}"
                : $"{a.DataHora:dd'/'MM HH\\:mm}",
            // Nome do CATÁLOGO, nunca o enum — e com a especialidade quando é consulta.
            Modalidade = CatalogoModalidades.NomeComEspecialidade(
                a.ModalidadeCodigo, a.ModalidadePrevista, a.EspecialidadeConsultaCodigo),
            Familia = a.ModalidadePrevista,
            ProfissionalId = a.ProfissionalId,
            // "sem profissional" é aviso: horário sem dono some do Meu dia e do repasse.
            Profissional = a.Profissional?.Rotulo ?? "sem profissional",
            Sala = a.Sala?.Nome ?? string.Empty,
            Convenio = a.Paciente?.ConvenioNome ?? string.Empty,
            Observacoes = a.Observacoes,
            Foto = a.Paciente?.FotoMiniatura,
            Situacao = a.Status,
            Etapa = a.Etapa,
            EhEncaixe = a.Encaixe,
            EhRetornoDoSegundoCodigo = a.Origem == OrigemAgendamento.RetornoSugerido,
            Lancamento = DescreverLancamento(a),
            EhHoje = ehHoje,
            Status = status,
            StatusDetalhe = StatusDaFila.Detalhe(a.Status, a.Etapa, a.ChegadaEm, espera, chamada,
                a.InicioAtendimentoEm, a.FimAtendimentoEm),
            Confirmacao = confirmacao,
            Selos = SelosDaFila.Montar(a.Etapa, termoPendente, termoAConferir, guia,
                pacote?.Usadas, pacote?.Contratadas, atraso, a.FimAtendimentoEm),
            PacoteUsadas = pacote?.Usadas,
            PacoteContratadas = pacote?.Contratadas,
            TemGuiaPendente = guia,
            // Só a sessão concluída, só enquanto o fechamento não aconteceu, e só quando há
            // PACOTE — ou quando é PARTICULAR (o caixa É o único dinheiro daquela sessão).
            FechamentoPendente = a.Etapa == EtapaFila.Finalizado
                                 && a.AtendimentoId is { } atendimentoId
                                 && !d.JaFechados.Contains(atendimentoId)
                                 && (d.Pacotes.ContainsKey(a.PacienteId) || EhParticular(a.Paciente))
        };
    }

    /// <summary>O convênio da ficha NÃO gera guia (parcela 60) — do cache do catálogo, sem banco.</summary>
    public static bool EhParticular(Paciente? paciente)
        => paciente is not null && !CatalogoConvenios.GeraGuia(paciente.ConvenioCodigo ?? paciente.Convenio.ToString());

    /// <summary>"Marcado por Ana em 08/08/2026 14:32" — ou a frase que assume a lacuna.</summary>
    public static string DescreverLancamento(Agendamento a)
    {
        if (string.IsNullOrWhiteSpace(a.CriadoPor))
            return "Marcado antes de o sistema passar a registrar quem lança — sem autoria.";
        return a.CriadoEm is { } quando
            ? $"Marcado por {a.CriadoPor} em {quando:dd'/'MM'/'yyyy HH\\:mm}"
            : $"Marcado por {a.CriadoPor}";
    }

    /// <summary>
    /// O tom do selo de confirmação: verde para quem confirmou, VERMELHO para quem disse que
    /// não vem (pede um gesto agora — ligar, remarcar e liberar o horário), âmbar para quem foi
    /// avisado e não respondeu. Vazio sem rodada: sem rodada não há o que afirmar.
    /// </summary>
    public static string TomDaConfirmacao(string confirmacao) => confirmacao switch
    {
        "Confirmou" => "sucesso",
        RodadaDeConfirmacao.SeloNaoVem or RodadaDeConfirmacao.SeloPediuCancelar => "perigo",
        "Não confirmou" => "aviso",
        _ => string.Empty
    };

    /// <summary>O tom do selo da fila em palavra de CSS.</summary>
    public static string Tom(TomDoSelo tom) => tom switch
    {
        TomDoSelo.Erro => "perigo",
        TomDoSelo.Aviso => "aviso",
        _ => "sucesso"
    };

    /// <summary>A família da modalidade em palavra de CSS — a mesma cor da grade.</summary>
    public static string ClasseDaFamilia(ModalidadeAtendimento familia) => familia switch
    {
        ModalidadeAtendimento.AcupunturaComEletro => "eletro",
        ModalidadeAtendimento.BsvApenas => "bsv",
        ModalidadeAtendimento.BsvComAcupuntura => "bsv-acupuntura",
        ModalidadeAtendimento.Consulta => "consulta",
        _ => "acupuntura"
    };

    /// <summary>As iniciais do avatar ("Ana Beatriz Souza" → "AS").</summary>
    public static string Iniciais(string nome)
    {
        var partes = (nome ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length > 2 || p == p.ToUpperInvariant()).ToList();
        if (partes.Count == 0) return "?";
        var primeira = char.ToUpperInvariant(partes[0][0]);
        return partes.Count == 1 ? primeira.ToString() : $"{primeira}{char.ToUpperInvariant(partes[^1][0])}";
    }

    /// <summary>
    /// O combo de profissional: "Todos" + quem tem horário VIVO no dia, em ordem de nome. Ele
    /// só aparece com DOIS ou mais — combo de um nome só é ruído.
    /// </summary>
    public static IReadOnlyList<ProfissionalDaAgenda> Profissionais(IReadOnlyList<LinhaAgendaDoDia> linhas)
    {
        var vivos = linhas.Where(l => !l.ForaDaFila).ToList();
        var lista = new List<ProfissionalDaAgenda> { new(null, "Todos os profissionais", vivos.Count) };
        lista.AddRange(vivos.Where(l => l.ProfissionalId is not null)
            .GroupBy(l => l.ProfissionalId!.Value)
            .Select(g => new ProfissionalDaAgenda(g.Key, g.First().Profissional, g.Count()))
            .OrderBy(p => p.Nome, StringComparer.Create(new CultureInfo("pt-BR"), false)));
        return lista;
    }

    /// <summary>
    /// O filtro vigente sobrevive à recarga só enquanto o profissional continua no dia —
    /// apontando para quem saiu, ele esconderia a lista inteira em silêncio.
    /// </summary>
    public static FiltroAgendaDoDia Ajustar(FiltroAgendaDoDia filtro, IReadOnlyList<ProfissionalDaAgenda> profissionais)
        => filtro.ProfissionalId is { } id && profissionais.All(p => p.Id != id)
            ? filtro with { ProfissionalId = null }
            : filtro;

    /// <summary>
    /// Aplica o recorte. As contagens das pílulas são sobre o que a BUSCA, a MODALIDADE e o
    /// PROFISSIONAL deixaram — nunca sobre o recorte da própria pílula: a distribuição tem de
    /// estar visível ANTES do clique.
    /// </summary>
    public static RecorteAgendaDoDia Recortar(IReadOnlyList<LinhaAgendaDoDia> linhas, FiltroAgendaDoDia filtro)
    {
        // Horário sem profissional só aparece em "Todos": ele não é de ninguém.
        var doProfissional = filtro.ProfissionalId is { } soDele
            ? linhas.Where(l => l.ProfissionalId == soDele).ToList()
            : linhas.ToList();

        var modalidadeAtiva = !string.IsNullOrEmpty(filtro.Modalidade) && filtro.Modalidade != TodasModalidades;
        var termos = (filtro.Busca ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        bool Passa(LinhaAgendaDoDia l) =>
            (!modalidadeAtiva || l.Modalidade == filtro.Modalidade)
            && termos.All(t => Comparador.IndexOf(l.Paciente, t,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);

        var pesquisadas = doProfissional.Where(Passa).ToList();
        var contagens = Situacoes.ToDictionary(
            s => s.Chave,
            s => s.Chave == "todos" ? pesquisadas.Count : pesquisadas.Count(l => l.Grupo == s.Chave));
        // As respostas contam sobre o mesmo recorte das situações — nunca sobre a própria
        // escolha de resposta, para a distribuição estar visível antes do clique.
        var respostas = Respostas.ToDictionary(r => r.Chave, r => pesquisadas.Count(l => l.Confirmacao == r.Selo));
        var seloEscolhido = Respostas.FirstOrDefault(r => r.Chave == filtro.Resposta).Selo;
        var visiveis = pesquisadas
            .Where(l => filtro.Situacao == "todos" || l.Grupo == filtro.Situacao)
            .Where(l => seloEscolhido is null || l.Confirmacao == seloEscolhido)
            .ToList();

        var modalidades = doProfissional.Select(l => l.Modalidade).Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct().OrderBy(m => m, StringComparer.Create(new CultureInfo("pt-BR"), false)).ToList();
        // A escolha atual fica na lista mesmo sem horário dela: sumir com a opção escolhida
        // faria o combo trocar o filtro sozinho.
        if (modalidadeAtiva && !modalidades.Contains(filtro.Modalidade!)) modalidades.Add(filtro.Modalidade!);
        modalidades.Insert(0, TodasModalidades);

        return new RecorteAgendaDoDia(
            visiveis, contagens, modalidades, doProfissional.Count,
            $"{visiveis.Count} de {doProfissional.Count} horário{(doProfissional.Count == 1 ? "" : "s")} no dia",
            NenhumCorresponde: doProfissional.Count > 0 && visiveis.Count == 0 && filtro.TemFiltros,
            Respostas: respostas);
    }

    /// <summary>
    /// Os itens do "⋯" da linha — ESTADO e PERMISSÃO juntos (os atos pedem bits diferentes;
    /// sem esta metade o item aparecia aceso e a recusa só chegava depois do clique). Sem
    /// separador órfão no começo, no fim ou repetido.
    /// </summary>
    public static IReadOnlyList<AcaoDaLinhaAgenda> Menu(LinhaAgendaDoDia l, PermissoesDaAgendaDoDia p)
    {
        var itens = new List<AcaoDaLinhaAgenda>();
        void Item(AcaoDaLinhaAgenda a, bool visivel) { if (visivel) itens.Add(a); }
        void Separar() { if (itens.Count > 0 && itens[^1] != AcaoDaLinhaAgenda.Separador) itens.Add(AcaoDaLinhaAgenda.Separador); }

        Item(AcaoDaLinhaAgenda.Ficha, p.VerFicha);
        // A porta do dinheiro depois que o médico concluiu: insumo e caixa não são anunciados
        // como pendência, e sem o item eles não teriam por onde ser lançados.
        Item(AcaoDaLinhaAgenda.FecharSessao, l.PodeFechar && p.FecharSessao);
        // Só para quem AINDA VEM: depois da sessão a mesma informação não evita mais nada.
        Item(AcaoDaLinhaAgenda.ConferirConvenio, l.EmAberto && p.VerFicha);
        Separar();
        Item(AcaoDaLinhaAgenda.MarcarFalta, l.EmAberto && p.FaltaOuCancelamento);
        Item(AcaoDaLinhaAgenda.Cancelar, l.EmAberto && p.FaltaOuCancelamento);
        Separar();
        // Abrir é leitura — e para a linha cancelada é a ÚNICA ação que sobra, com razão.
        Item(AcaoDaLinhaAgenda.AbrirHorario, true);
        while (itens.Count > 0 && itens[^1] == AcaoDaLinhaAgenda.Separador) itens.RemoveAt(itens.Count - 1);
        return itens;
    }

    /// <summary>O rótulo de cada item do "⋯".</summary>
    public static string Rotulo(AcaoDaLinhaAgenda a, LinhaAgendaDoDia l) => a switch
    {
        AcaoDaLinhaAgenda.Ficha => "Ficha do paciente…",
        AcaoDaLinhaAgenda.FecharSessao => "Fechar sessão (pacote, insumo, caixa)…",
        AcaoDaLinhaAgenda.ConferirConvenio => "Conferir convênio e cota…",
        AcaoDaLinhaAgenda.MarcarFalta => "Marcar falta",
        AcaoDaLinhaAgenda.Cancelar => l.PediuCancelar ? "Aceitar o cancelamento pedido" : "Cancelar o horário",
        AcaoDaLinhaAgenda.AbrirHorario => l.ForaDaFila ? "Abrir o horário (reabrir ou remarcar)…" : "Abrir o horário…",
        _ => string.Empty
    };

    /// <summary>A metade visível do botão do passo — o bit de CADA passo.</summary>
    public static bool PodeDarOPasso(LinhaAgendaDoDia l, PermissoesDaAgendaDoDia p) => l.Passo switch
    {
        PassoDaAgenda.Chegou => p.MoverFila,
        PassoDaAgenda.Concluir => p.ConcluirSessao,
        PassoDaAgenda.FecharSessao => p.FecharSessao,
        _ => false
    };

    /// <summary>
    /// A frase do que ACONTECEU no fechamento — uma só para as duas portas (Concluir e Fechar
    /// sessão). <paramref name="guias"/> nulo quando a sessão já estava concluída: dizer "0
    /// guia(s)" ali seria falso — as guias existem, só não nasceram neste clique.
    /// </summary>
    public static string FraseDoFechamento(string paciente, ResultadoFechamento resultado, int? guias = null)
    {
        var partes = new List<string>();
        if (guias is { } quantas) partes.Add($"{quantas} guia(s)");
        if (resultado.Consumo is not null) partes.Add("1 sessão do pacote");
        if (resultado.Movimentos.Count > 0) partes.Add($"{resultado.Movimentos.Count} insumo(s)");
        if (resultado.Lancamento is not null) partes.Add("entrada no caixa");
        return partes.Count == 0
            ? $"Sessão de {paciente} fechada — nada a debitar nem a lançar."
            : $"Sessão de {paciente} fechada — {string.Join(" · ", partes)}.";
    }

    /// <summary>A conclusão sem decisão (convênio sem pacote e sem insumo): a guia já está no faturamento.</summary>
    public static string FraseDaConclusao(string paciente, int guias) => guias == 0
        ? $"Sessão de {paciente} concluída — sem guia a faturar."
        : $"Sessão de {paciente} concluída — {guias} guia(s) no faturamento.";

    /// <summary>O que a conferência de convênio e cota diz — os alertas, ou que não há nada a resolver.</summary>
    public static string FraseDaElegibilidade(bool guiaPendente, IEnumerable<string> alertas, bool falhou)
    {
        var recados = new List<string>();
        if (guiaPendente)
            recados.Add("Tem GUIA PENDENTE de baixa — aproveite que ele está aqui e peça o documento; "
                        + "depois a cobrança vira telefonema.");
        recados.AddRange(alertas);
        if (falhou)
            recados.Add("Não foi possível conferir carteirinha e cota agora — confira na ficha do paciente.");
        return recados.Count > 0
            ? string.Join("\n\n", recados)
            : "Nada a resolver: carteirinha em dia, cota disponível, sem guia pendente e sem conta vencida.";
    }
}
