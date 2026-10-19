using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que a TELA DO PACIENTE afirma — o rail de seções, quem vê qual seção, de onde o
/// paciente veio e para onde o "voltar" leva (out/2026, P0 da migração da tela do paciente
/// para Blazor).
///
/// ⚠️ Mora na Application, e não no <c>ModuloClinico</c> nem na ViewModel, por dois motivos
/// que se somam: (1) a tela Blazor (<c>Clinica.Ui.Blazor</c>, net8.0 puro) não alcança o
/// módulo WPF, e uma segunda cópia das listas divergiria na primeira seção nova — o índice
/// de navegação abriria a seção ERRADA sem quebrar build nenhum (o caso (b) da checagem 38);
/// (2) a ViewModel WPF não compila no projeto de teste, e regra que o <c>dotnet test</c> não
/// alcança apodrece calada. O WPF e o Blazor leem DAQUI.
/// </summary>
public static class TelaDoPacienteNaTela
{
    public const string Atendimento = "Atendimento";
    public const string AtendimentoDeEnfermagem = "Atendimento de enfermagem";
    public const string FichaDoPaciente = "Ficha do paciente";
    public const string Historico = "Histórico";
    public const string ExamesEAnexos = "Exames e anexos";
    public const string PrescricoesEDocumentos = "Prescrições e documentos";
    public const string Acompanhamento = "Acompanhamento";

    /// <summary>
    /// As seções, na ORDEM do rail. O índice de navegação é derivado daqui pelo NOME (a
    /// lição da parcela 75: índice escrito à mão abre a seção errada quando alguém insere uma
    /// no meio). A checagem 38 lê esta lista.
    /// </summary>
    public static readonly IReadOnlyList<string> SecoesDoPaciente =
    [
        "Atendimento",
        "Atendimento de enfermagem",
        "Ficha do paciente",
        "Histórico",
        "Exames e anexos",
        "Prescrições e documentos",
        "Acompanhamento"
    ];

    /// <summary>
    /// O grupo de cada seção, casado POR POSIÇÃO com <see cref="SecoesDoPaciente"/>: o que se
    /// faz nesta sessão e o que o paciente já tem.
    /// </summary>
    public static readonly IReadOnlyList<string> GruposDoPaciente =
    [
        "Sessão",
        "Sessão",
        "Paciente",
        "Paciente",
        "Paciente",
        "Paciente",
        "Paciente"
    ];

    /// <summary>Uma linha do rail: o rótulo que se lê e o grupo em que ela mora.</summary>
    public sealed record SecaoDoRail(string Rotulo, string Grupo);

    /// <summary>
    /// O rail, na ordem. Recusa listas de tamanho diferente e grupo partido em dois blocos:
    /// o rail é uma vista AGRUPADA, e grupo partido faz a vista reordenar as seções — o
    /// clique passa a abrir a tela do vizinho, sem erro.
    /// </summary>
    public static IReadOnlyList<SecaoDoRail> Rail()
    {
        if (SecoesDoPaciente.Count != GruposDoPaciente.Count)
            throw new InvalidOperationException(
                "TelaDoPacienteNaTela: SecoesDoPaciente e GruposDoPaciente têm tamanhos "
                + "diferentes. As duas são casadas por POSIÇÃO — uma seção sem grupo "
                + "sairia do rail sem quebrar nada.");

        var vistos = new List<string>();
        foreach (var grupo in GruposDoPaciente)
        {
            if (vistos.Count > 0 && vistos[^1] == grupo) continue;
            if (vistos.Contains(grupo))
                throw new InvalidOperationException(
                    $"TelaDoPacienteNaTela: o grupo “{grupo}” aparece em blocos separados de "
                    + "GruposDoPaciente. O rail é uma vista agrupada, e grupo partido faz a "
                    + "vista reordenar as seções — o clique passa a abrir a tela do vizinho.");
            vistos.Add(grupo);
        }

        return [.. SecoesDoPaciente.Select((nome, i) => new SecaoDoRail(nome, GruposDoPaciente[i]))];
    }

    /// <summary>O índice da seção pelo nome; nome desconhecido cai na primeira (o Atendimento).</summary>
    public static int IndiceDe(string secao)
    {
        for (var i = 0; i < SecoesDoPaciente.Count; i++)
            if (SecoesDoPaciente[i] == secao) return i;
        return 0;
    }

    /// <summary>
    /// Uma linha do rail para QUEM está logado.
    ///
    /// ⚠️ Esconder é COLAPSAR, nunca tirar da lista: o índice desta lista é a régua da
    /// navegação, e filtrá-la deslocaria os índices.
    /// </summary>
    public sealed record ItemDoRail(string Rotulo, string Grupo, bool Oculta)
    {
        /// <summary>O rótulo que o menu lê ("Atendimento" é a evolução da sessão).</summary>
        public string RotuloMenu => Rotulo switch
        {
            Atendimento => "Evolução da sessão",
            Historico => "Histórico clínico",
            _ => Rotulo
        };

        /// <summary>
        /// Ficha e Prescrições não ocupam linha no menu: a ficha abre pelo botão do cabeçalho
        /// e os documentos pelo bloco EMITIR — duas portas com o mesmo nome fazem a pessoa
        /// procurar a diferença que não existe.
        /// </summary>
        public bool OcultaNoMenu => Oculta || Rotulo is FichaDoPaciente or PrescricoesEDocumentos;
    }

    /// <summary>
    /// O rail de quem está logado. As regras são as que a ViewModel WPF aplicava (parcela 95
    /// e set/2026), agora num lugar só:
    /// <list type="bullet">
    /// <item>a seção de ESCRITA do outro lado some (<see cref="PerfisAcesso.SecoesDeEscritaDoPosto"/>);</item>
    /// <item>o perfil Enfermagem nunca vê o S-O-A-P;</item>
    /// <item>quem escreve como médico lê as passagens no histórico — não ganha a segunda seção;</item>
    /// <item>sem <c>VerProntuario</c>, só a Ficha (dado cadastral) fica à vista: o resto é saúde.</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<ItemDoRail> RailPara(Permissao efetivas, PerfilAcesso perfil)
    {
        var (medico, enfermagem) = PerfisAcesso.SecoesDeEscritaDoPosto(efetivas);
        if (perfil == PerfilAcesso.Enfermagem) medico = false;
        if (medico) enfermagem = false;
        var veProntuario = (efetivas & Permissao.VerProntuario) == Permissao.VerProntuario;

        return [.. Rail().Select(s => new ItemDoRail(
            s.Rotulo, s.Grupo,
            Oculta: (s.Rotulo == Atendimento && !medico)
                    || (s.Rotulo == AtendimentoDeEnfermagem && !enfermagem)
                    || (!veProntuario && s.Rotulo != FichaDoPaciente)))];
    }

    /// <summary>
    /// A seção que abre. A pedida, se estiver à vista; a passagem de enfermagem pedida por quem
    /// não a vê cai no Atendimento (a regra do WPF); e qualquer outra escondida cai na
    /// primeira seção visível — abrir numa seção escondida mostraria uma tela que o rail
    /// diz não existir.
    /// </summary>
    public static int SecaoInicial(int pedida, IReadOnlyList<ItemDoRail> rail)
    {
        if (pedida < 0 || pedida >= rail.Count) pedida = 0;
        if (!rail[pedida].Oculta) return pedida;

        var atendimento = IndiceDe(Atendimento);
        if (rail[pedida].Rotulo == AtendimentoDeEnfermagem && !rail[atendimento].Oculta) return atendimento;

        for (var i = 0; i < rail.Count; i++)
            if (!rail[i].Oculta) return i;
        return pedida;
    }

    /// <summary>
    /// De onde a sessão veio — e é diferença que muda o registro: chamada da agenda, a
    /// evolução nasce ligada ao horário; escolhida na busca, não. Diz a DATA quando não é
    /// hoje (a dívida de prontuário e a semana abrem horários de outros dias).
    /// </summary>
    public static string DescreverOrigem(int? agendamentoId, DateOnly? dataDoHorario, DateOnly hoje)
    {
        if (agendamentoId is null)
            return "Escolhido na busca — a evolução não fica ligada a nenhum horário.";

        return dataDoHorario is { } data && data != hoje
            ? $"Chamado da agenda de {data:dd/MM/yyyy} — a evolução nasce ligada a esse horário."
            : "Chamado da agenda de hoje — a evolução nasce ligada a este horário.";
    }

    /// <summary>
    /// Para onde o "voltar" leva: o Meu dia quando o paciente veio de um horário e a pessoa
    /// alcança o Meu dia; senão a lista de Pacientes, que exige o mesmo bit desta tela.
    /// Rótulo e destino saem da MESMA resposta — botão que anuncia um destino e leva a outro
    /// é pior do que o botão que não faz nada.
    /// </summary>
    public static bool VoltaParaMeuDia(int? agendamentoId, bool alcancaMeuDia)
        => agendamentoId is not null && alcancaMeuDia;

    public static string RotuloVoltar(bool voltaParaMeuDia) => voltaParaMeuDia ? "Meu dia" : "Pacientes";
}

/// <summary>
/// Em que pé está o horário do paciente aberto — a pílula e o cronômetro do cabeçalho
/// (out/2026, subido da <c>PacienteWorkspaceViewModel.DescreverSessao</c>).
/// </summary>
public sealed record SessaoDoAtendimentoNaTela(
    bool TemSessao,
    bool Encerrada,
    bool Concluida,
    bool EmAtendimento,
    bool PodeIniciar,
    string? Situacao,
    string? Cronometro)
{
    public static readonly SessaoDoAtendimentoNaTela Nenhuma = new(false, false, false, false, false, null, null);

    /// <summary>Reabrir só existe entre o encerramento e a conclusão (depois, é estorno no balcão).</summary>
    public bool PodeReabrir => Encerrada && !Concluida;

    public static SessaoDoAtendimentoNaTela De(Agendamento? horario, DateTime agora)
    {
        if (horario is null) return Nenhuma;

        var duracao = horario.DuracaoDoAtendimento(agora);
        var encerrada = horario.FimAtendimentoEm is not null;
        var concluida = horario.Status == StatusAgendamento.Realizado && encerrada;
        var emAtendimento = horario.InicioAtendimentoEm is not null && !encerrada
                            && horario.Status == StatusAgendamento.Agendado;
        var podeIniciar = horario.InicioAtendimentoEm is null && horario.Status == StatusAgendamento.Agendado;

        string situacao;
        if (concluida)
            situacao = "Sessão concluída"
                       + (horario.FimAtendimentoEm is { } fim ? $" às {fim:HH\\:mm}" : "")
                       + (duracao is null ? "" : $" · durou {duracao} min");
        else if (horario.Status == StatusAgendamento.Realizado)
            situacao = "Guias geradas · registro clínico em aberto";
        else if (encerrada)
            situacao = $"Conclusão pendente — encerrado às {horario.FimAtendimentoEm:HH\\:mm}"
                       + (duracao is null ? "" : $" · durou {duracao} min");
        else if (emAtendimento)
            // Sem o tempo: o cronômetro ao lado diz há quanto tempo; a pílula diz em que pé.
            situacao = "Em atendimento";
        else if (podeIniciar)
            situacao = $"Horário de {horario.DataHora:HH\\:mm} — o paciente ainda não entrou na sala";
        else
            situacao = horario.Status switch
            {
                StatusAgendamento.Faltou => "Horário marcado como falta",
                StatusAgendamento.Substituido => "Horário substituído por uma sessão lançada por fora",
                _ => "Horário cancelado"
            };

        return new(true, encerrada, concluida, emAtendimento, podeIniciar, situacao,
            CronometroDaSessao.De(horario, agora));
    }

    /// <summary>
    /// Finalizar existe para o horário em aberto, ou para o realizado ainda sem o carimbo de
    /// fim — e só para quem pode concluir (a decisão de acesso é de quem chama).
    /// </summary>
    public static bool PodeFinalizar(Agendamento? horario, bool podeConcluirComAcesso)
        => podeConcluirComAcesso && horario is { Status: StatusAgendamento.Agendado }
            or { Status: StatusAgendamento.Realizado, AtendimentoId: not null, FimAtendimentoEm: null };

    public static string RotuloFinalizar(Agendamento? horario)
        => horario?.Status == StatusAgendamento.Realizado
            ? "Salvar e finalizar"
            : horario?.FimAtendimentoEm is not null ? "Concluir sessão pendente" : "Salvar e finalizar";
}
