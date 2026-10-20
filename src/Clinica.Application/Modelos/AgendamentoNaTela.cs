using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// O que o FORMULÁRIO DO HORÁRIO da agenda (out/2026 — era o <c>AgendamentoEdicaoViewModel</c>
/// do WPF, que saiu; hoje o <c>AgendamentoForm</c> Blazor) AFIRMA e DECIDE: o título por porta, a leitura da
/// hora e da duração, a ordem das recusas do Salvar, o aviso de horário sem dono na edição e a
/// capa do que o paciente já tem no dia. Mora aqui, e não no Razor, pela regra da casa: o que a
/// tela afirma precisa morar onde o <c>dotnet test</c> alcança.
/// </summary>
public static class AgendamentoNaTela
{
    public const string TituloNovo = "Novo horário";
    public const string TituloRemarcar = "Remarcar horário";

    /// <summary>
    /// O título vem da PORTA: horário novo usa o título de quem abriu ("Chamar Fulano da lista
    /// de espera") ou "Novo horário"; horário que já existe usa o da edição ("Editar o horário",
    /// pela lista do dia) ou "Remarcar horário", pela grade. Rótulo fixo mente numa das duas
    /// portas — a lição de <c>RotulosDeAbrir</c>.
    /// </summary>
    public static string Titulo(bool edicao, string? tituloDaPorta, string? tituloDaEdicao)
        => edicao
            ? (string.IsNullOrWhiteSpace(tituloDaEdicao) ? TituloRemarcar : tituloDaEdicao!)
            : (string.IsNullOrWhiteSpace(tituloDaPorta) ? TituloNovo : tituloDaPorta!);

    /// <summary>A hora digitada ("HH:mm") — a MESMA leitura do formulário WPF (<c>TimeOnly.TryParse</c>).</summary>
    public static bool TentarLerHora(string? hora, out TimeOnly lida)
        => TimeOnly.TryParse(hora?.Trim(), out lida);

    /// <summary>Dia + hora digitada; falso quando a hora não é legível.</summary>
    public static bool TentarMontarDataHora(DateOnly data, string? hora, out DateTime dataHora)
    {
        dataHora = default;
        if (!TentarLerHora(hora, out var h)) return false;
        dataHora = data.ToDateTime(h);
        return true;
    }

    /// <summary>A duração digitada: vazio ou inválido = nulo (o padrão do profissional).</summary>
    public static int? DuracaoInformada(string? duracao) => AtendimentoDoBalcaoNaTela.DuracaoInformada(duracao?.Trim());

    public const string DicaDuracao = "Vazio = padrão do profissional.";

    public const string RecusaSemPaciente = "Escolha o paciente.";
    public const string RecusaSemModalidade = "Escolha a modalidade.";
    public const string RecusaSemEspecialidade = "Consulta precisa de especialidade.";
    public const string RecusaHora = "Horário inválido. Use HH:mm (ex.: 14:30).";
    public const string RecusaDuracao = "A duração precisa ser um número de minutos maior que zero.";

    /// <summary>
    /// A metade VISÍVEL da recusa do <c>AgendaService.AgendarAsync</c> (parcela 95): horário
    /// MARCADO precisa de dono. A frase é a do formulário WPF.
    /// </summary>
    public const string RecusaSemProfissional =
        "Escolha quem vai atender: sem profissional o horário não aparece na agenda de ninguém e fica fora do repasse.";

    /// <summary>
    /// As recusas do Salvar, na ORDEM do formulário WPF — a primeira que falhar é a que a
    /// pessoa lê. Nulo = pode gravar. O encaixe e a EDIÇÃO passam sem profissional: o encaixe é
    /// o paciente no balcão, e mover um horário não escolhe quem atende (os importados podem não
    /// ter ninguém).
    /// </summary>
    public static string? RecusaAoSalvar(bool temPaciente, bool temModalidade, bool ehConsulta,
        bool temEspecialidade, DateOnly data, string? hora, string? duracao,
        bool edicao, bool encaixe, bool temProfissional)
    {
        if (!temPaciente) return RecusaSemPaciente;
        if (!temModalidade) return RecusaSemModalidade;
        if (ehConsulta && !temEspecialidade) return RecusaSemEspecialidade;
        if (!TentarMontarDataHora(data, hora, out _)) return RecusaHora;
        if (!string.IsNullOrWhiteSpace(duracao)
            && (!int.TryParse(duracao.Trim(), out var minutos) || minutos <= 0))
            return RecusaDuracao;
        if (!encaixe && !edicao && !temProfissional) return RecusaSemProfissional;
        return null;
    }

    /// <summary>
    /// EDITANDO um horário que ficou sem dono: AVISO (marcar recusa; editar não pode, os
    /// importados nasceram assim). Só DEPOIS de o horário ter sido lido — antes disso o aviso
    /// piscaria em toda edição, inclusive nos horários que TÊM dono.
    /// </summary>
    public static bool AvisarSemProfissional(bool edicao, bool horarioCarregado, bool temProfissional)
        => edicao && horarioCarregado && !temProfissional;

    public const string AvisoSemProfissionalNaEdicao =
        "Sem profissional este horário não aparece na agenda de quem atende nem entra no repasse.";

    /// <summary>
    /// A permissão do Salvar. Com a chave "guia no agendamento" ligada, CRIAR horário cria o
    /// atendimento e as guias — o ato que <c>LancarAtendimento</c> nomeia; editar não cria
    /// atendimento e fica só com <c>EditarAgenda</c>. Os bits somados são um E.
    /// </summary>
    public static Permissao PermissaoParaSalvar(bool edicao, bool chaveGuiaNoAgendamento)
        => !edicao && chaveGuiaNoAgendamento
            ? Permissao.EditarAgenda | Permissao.LancarAtendimento
            : Permissao.EditarAgenda;

    public static string AtoDoSalvar(bool edicao, bool chaveGuiaNoAgendamento)
        => !edicao && chaveGuiaNoAgendamento ? "marcar atendimento gerando as guias" : "mexer na agenda";

    /// <summary>A recusa da ficha sem convênio com a chave ligada (parcela 92) — a frase do WPF.</summary>
    public static string RecusaSemConvenio(string paciente)
        => $"{paciente} está sem convênio, e com \"guia no agendamento\" ligada marcar já gera as guias. "
           + "Escolha o convênio do paciente para marcar — quem paga do bolso entra no convênio particular.";

    /// <summary>
    /// A CAPA do que o paciente já tem no dia escolhido (parcela 70), pelo MESMO ponto único do
    /// Novo atendimento (<c>CapasDoDiaAsync</c>). O verbo é MARCAR: esta é a porta de marcação.
    /// </summary>
    public static string? AvisoJaLancado(IReadOnlyList<CapaDoDia> capas, DateOnly data)
        => capas.Count == 0
            ? null
            : $"Já lançado em {data:dd/MM}: "
              + string.Join(" · ", capas.Select(c => $"nº {c.Numero} ({c.Modalidade}, {c.Lancamento} — {c.ResumoGuias})"))
              + ". Marcar de novo cria OUTRO atendimento e outro jogo de guias.";

    public const string ElegibilidadeNaoConferida = "Não foi possível conferir carteirinha e cota agora.";

    /// <summary>
    /// A conferência de choque não respondeu. Nada impede marcar (set/2026), mas a lista de
    /// avisos não pode se passar por "horário livre": é o terceiro estado, escrito.
    /// </summary>
    public const string ConflitosNaoConferidos =
        "Não foi possível conferir quem já está neste horário agora — nada impede marcar, mas os avisos podem estar incompletos.";

    /// <summary>
    /// O rótulo do profissional no combo. O que está INATIVO só entra na lista quando já é o
    /// dono do horário em edição — "quem não edita, PRESERVA": tirá-lo do combo faria o Salvar
    /// gravar o horário sem dono.
    /// </summary>
    public static string RotuloProfissional(Profissional p) => p.Ativo ? p.Rotulo : $"{p.Rotulo} (inativo)";

    public static string RotuloSala(Sala s) => s.Ativa ? s.Nome : $"{s.Nome} (inativa)";

    /// <summary>O que dizer depois de gravar: o fato das GUIAS, quando o serviço o escreveu.</summary>
    public static string Desfecho(IReadOnlyList<string> avisosDoSalvamento)
        => avisosDoSalvamento.Count == 0
            ? "Agenda atualizada."
            : $"Agenda atualizada — {string.Join(" ", avisosDoSalvamento)}";
}

/// <summary>
/// O formulário de ENTRADA NA LISTA DE ESPERA (out/2026 — o <c>ListaEsperaEdicaoViewModel</c>
/// do WPF, agora <c>ListaEsperaForm</c> Blazor): os turnos oferecidos e as frases da tela.
/// </summary>
public static class ListaEsperaNaTela
{
    public const string Titulo = "Entrar na lista de espera";

    public const string Subtitulo =
        "Quanto mais preferências você registrar, mais fácil achar quem serve para o horário que vagar.";

    public const string DicaProfissional = "Em branco = qualquer um (chama mais rápido).";

    public const string RecusaSemPaciente = "Escolha o paciente.";

    /// <summary>Os turnos, na ordem do formulário, com o rótulo de <see cref="RotulosEnum"/> (nunca o enum cru).</summary>
    public static IReadOnlyList<(PeriodoPreferido Valor, string Nome)> Periodos { get; } =
    [
        (PeriodoPreferido.Qualquer, RotulosEnum.De(PeriodoPreferido.Qualquer)),
        (PeriodoPreferido.Manha, RotulosEnum.De(PeriodoPreferido.Manha)),
        (PeriodoPreferido.Tarde, RotulosEnum.De(PeriodoPreferido.Tarde))
    ];

    public const string Desfecho = "Paciente entrou na lista de espera.";
}
