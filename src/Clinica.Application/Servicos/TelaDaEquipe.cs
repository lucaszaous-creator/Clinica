using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// Uma linha das habilitações de atendimento do profissional: a modalidade e, quando ela é
/// CONSULTA, a especialidade — consultas são separadas por especialidade (a consulta de
/// psiquiatria e a de geriatria não são o mesmo ato para quem marca).
/// </summary>
public sealed record OpcaoDeAtendimento(string ModalidadeCodigo, string? EspecialidadeCodigo, string Rotulo, bool EhConsulta)
{
    public bool Casa(AtendimentoHabilitado h)
        => string.Equals(h.ModalidadeCodigo, ModalidadeCodigo, StringComparison.OrdinalIgnoreCase)
           && string.Equals(h.EspecialidadeCodigo, EspecialidadeCodigo, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Os números do topo da tela de Profissionais e salas.</summary>
public sealed record PainelDaEquipe(
    int Ativos, int Inativos, int ComJornada, int ComCpf,
    int SalasAtivas, int SalasInativas, int VagasSimultaneas,
    int AgendaFechada, int SessoesARemarcar);

/// <summary>
/// O que a tela de PROFISSIONAIS E SALAS afirma (out/2026, migração do EquipeViewModel e das
/// janelas de profissional, sala, jornada e bloqueio para Blazor). As decisões que moravam nos
/// ViewModels do WPF subiram para cá — a frase da jornada, o que conta como habilitação, a
/// regra da Psicologia, o que se grava quando o gerente NÃO mexeu nas habilitações — porque o
/// Razor não é alcançado pelo <c>dotnet test</c>, e o que decide o que a tela diz precisa ser.
/// </summary>
public static class TelaDaEquipe
{
    /// <summary>Os sete dias, na ordem em que a clínica lê a semana (segunda primeiro).</summary>
    public static readonly IReadOnlyList<(DayOfWeek Dia, string Curto, string Nome)> Semana =
    [
        (DayOfWeek.Monday, "seg", "Segunda"), (DayOfWeek.Tuesday, "ter", "Terça"),
        (DayOfWeek.Wednesday, "qua", "Quarta"), (DayOfWeek.Thursday, "qui", "Quinta"),
        (DayOfWeek.Friday, "sex", "Sexta"), (DayOfWeek.Saturday, "sáb", "Sábado"),
        (DayOfWeek.Sunday, "dom", "Domingo")
    ];

    // ==================== Profissional na lista ====================

    /// <summary>"CRM-RJ 52.1234" — ou a frase que diz que falta (os documentos saem sem registro).</summary>
    public static string Registro(Profissional p)
        => string.IsNullOrWhiteSpace(p.RegistroConselho) ? "sem registro no conselho" : p.RegistroConselho!.Trim();

    /// <summary>A duração das sessões dele. Vazia = o padrão da clínica, e a tela diz QUAL.</summary>
    public static string Duracao(Profissional p)
        => p.DuracaoPadraoMinutos is { } d
            ? $"{d} min por sessão"
            : $"padrão da clínica ({Agendamento.DuracaoPadraoMinutos} min)";

    /// <summary>Quem não cadastrou horário: não é erro (a agenda aceita qualquer dia e hora) — é o que a linha diz.</summary>
    public const string HorarioNaoCadastrado = "horário não cadastrado";

    /// <summary>O horário de atendimento como se lê — a MESMA frase da grade e da recusa (<see cref="Profissional.DescricaoJornada"/>).</summary>
    public static string Horario(Profissional p) => p.DescricaoJornada ?? HorarioNaoCadastrado;

    /// <summary>Nome antigo de <see cref="Horario"/> (a palavra "jornada" saiu da interface em out/2026).</summary>
    public static string Jornada(Profissional p) => Horario(p);

    /// <summary>
    /// A mini-semana da linha do profissional: os sete dias (segunda primeiro), cada um com as
    /// faixas dele ("08:00–12:00 · 14:00–18:00"), "não atende" ou "qualquer hora" (cadastro
    /// antigo com dias e sem horas). Vazia quando não há horário cadastrado — a linha diz isso.
    /// </summary>
    public static IReadOnlyList<(DayOfWeek Dia, string Curto, string Nome, bool Atende, string Faixas)> MiniSemana(Profissional p)
    {
        if (!p.JornadaDeclarada) return [];
        return Semana.Select(s =>
        {
            var f = p.FaixasEm(s.Dia);
            var texto = f is null ? "qualquer hora" : f.Count == 0 ? "não atende" : string.Join(" · ", f.Select(x => x.ToString()));
            return (s.Dia, s.Curto, s.Nome, f is not { Count: 0 }, texto);
        }).ToList();
    }

    /// <summary>
    /// O que o profissional pode receber na marcação. NULO nas habilitações é "não restringido"
    /// (o comportamento de sempre: marca-se qualquer modalidade); lista vazia é "o gerente tirou
    /// tudo" — e aí ninguém consegue marcar com ele, o que a tela precisa DIZER, senão a
    /// recepção descobre na recusa do agendamento. A Psicologia é regra do domínio
    /// (<see cref="Profissional.Atende"/>): só consulta de psicologia, configurado ou não.
    /// </summary>
    public static string Habilitacoes(Profissional p)
    {
        if (EhPsicologia(p.EspecialidadeCodigo)) return "só consulta de Psicologia";
        var h = p.HabilitacoesAtendimento;
        if (h is null) return "todas as modalidades (sem restrição)";
        return h.Count switch
        {
            0 => "nenhum atendimento — não pode ser marcado",
            1 => "1 atendimento habilitado",
            var n => $"{n} atendimentos habilitados"
        };
    }

    /// <summary>A habilitação deixa o profissional fora da marcação — vale um alerta na linha.</summary>
    public static bool SemNadaHabilitado(Profissional p)
        => !EhPsicologia(p.EspecialidadeCodigo) && p.HabilitacoesAtendimento is { Count: 0 };

    /// <summary>Tem CPF válido: é o que permite assinar com e-CPF (o sistema compara com o do certificado).</summary>
    public static bool AssinaDigitalmente(Profissional p) => Cpf.Valido(p.Cpf);

    /// <summary>Iniciais do avatar: primeira e última palavra que contam ("Dra. Helena Duarte" → "HD").</summary>
    public static string Iniciais(string? nome)
    {
        var tratamentos = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "dr", "dr.", "dra", "dra.", "de", "da", "do", "das", "dos", "e" };
        var partes = (nome ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !tratamentos.Contains(p)).ToList();
        if (partes.Count == 0) return "?";
        var primeira = char.ToUpperInvariant(partes[0][0]);
        return partes.Count == 1 ? primeira.ToString() : $"{primeira}{char.ToUpperInvariant(partes[^1][0])}";
    }

    /// <summary>
    /// A cor da agenda, normalizada ("#RRGGBB"), ou nulo quando não é uma cor que a agenda
    /// reconheça — e aí ela usa a cor padrão (a agenda nunca depende de a cor estar preenchida).
    /// </summary>
    public static string? CorDaAgenda(string? cor)
    {
        if (string.IsNullOrWhiteSpace(cor)) return null;
        var t = cor.Trim();
        if (!t.StartsWith('#')) t = "#" + t;
        return Regex.IsMatch(t, "^#[0-9a-fA-F]{6}$") ? t.ToUpperInvariant() : null;
    }

    /// <summary>A busca da lista: nome, nome curto, registro e especialidade, sem acento nem caixa.</summary>
    public static bool CasaBusca(Profissional p, string? termo, string? especialidadeNome = null)
    {
        if (string.IsNullOrWhiteSpace(termo)) return true;
        var t = SemAcento(termo.Trim());
        return new[] { p.Nome, p.NomeCurto, p.RegistroConselho, especialidadeNome }
            .Any(c => c is not null && SemAcento(c).Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    // ==================== Sala ====================

    /// <summary>A capacidade como frase: é ela que decide quando a agenda avisa choque de sala.</summary>
    public static string Capacidade(int capacidade)
        => capacidade <= 1 ? "1 atendimento por vez" : $"{capacidade} atendimentos ao mesmo tempo";

    // ==================== Ativar / desativar ====================

    /// <summary>
    /// O efeito de desativar, dito ANTES de a pessoa desmarcar a caixa. Desativar NÃO apaga nada
    /// nem desmarca ninguém: some dos seletores de marcação; os horários já marcados continuam na
    /// agenda, numa coluna marcada como de inativo (é o que impede a recepção de marcar outro
    /// paciente por cima sem ver quem precisa ser remarcado).
    /// </summary>
    public static string EfeitoDaSituacao(bool ativo, bool ehSala)
        => (ativo, ehSala) switch
        {
            (true, false) => "Ativo: aparece para marcar horário, na grade e nos relatórios.",
            (false, false) => "Inativo: sai dos seletores de marcação. Os horários já marcados continuam na agenda, numa coluna marcada como de inativo — remarque-os com os pacientes. Todo o histórico (agenda, prontuário, repasse) fica preservado.",
            (true, true) => "Ativa: pode ser escolhida ao marcar horário.",
            _ => "Inativa: sai dos seletores de marcação. Os horários já marcados nela continuam na agenda, numa coluna marcada como de inativa. O histórico fica preservado."
        };

    // ==================== Habilitações (gerente) ====================

    /// <summary>
    /// Todas as combinações que o gerente pode habilitar: cada modalidade ativa e, para as de
    /// CONSULTA, uma linha por especialidade ativa. É a MESMA lista que o serviço confere ao
    /// gravar (modalidade e especialidade ativas do catálogo).
    /// </summary>
    public static IReadOnlyList<OpcaoDeAtendimento> OpcoesDeAtendimento(
        IEnumerable<EntradaModalidade> modalidades, IEnumerable<EntradaEspecialidade> especialidades)
    {
        var esp = especialidades.Where(e => e.Ativo).OrderBy(e => e.Nome, StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), true)).ToList();
        var lista = new List<OpcaoDeAtendimento>();
        foreach (var m in modalidades.Where(m => m.Ativo))
        {
            if (m.Base == ModalidadeAtendimento.Consulta)
                lista.AddRange(esp.Select(e => new OpcaoDeAtendimento(m.Codigo, e.Codigo, $"{m.Nome} · {e.Nome}", true)));
            else
                lista.Add(new OpcaoDeAtendimento(m.Codigo, null, m.Nome, false));
        }
        return lista;
    }

    /// <summary>
    /// As especialidades do seletor: as ativas e, se o profissional tem uma que foi DESATIVADA
    /// no catálogo, ela também — marcada. Sem isto o seletor abriria em branco e o Salvar
    /// apagaria a especialidade de quem só veio corrigir o telefone (quem não edita, preserva).
    /// </summary>
    public static IReadOnlyList<(string Codigo, string Nome)> EspecialidadesParaEscolha(
        IEnumerable<EntradaEspecialidade> ativas, string? atual, string? nomeDaAtual = null)
    {
        var lista = ativas.Where(e => e.Ativo).Select(e => (e.Codigo, e.Nome)).ToList();
        if (!string.IsNullOrWhiteSpace(atual)
            && !lista.Any(e => string.Equals(e.Codigo, atual, StringComparison.OrdinalIgnoreCase)))
            lista.Add((atual!, $"{nomeDaAtual ?? atual} (inativa no catálogo)"));
        return lista;
    }

    public static bool EhPsicologia(string? especialidadeCodigo)
        => string.Equals(especialidadeCodigo, "Psicologia", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A regra da Psicologia (a mesma do serviço e do domínio): quem tem Psicologia como
    /// especialidade principal só atende consulta de Psicologia. As outras linhas ficam
    /// APAGADAS — e não somem: sumir faria a pessoa procurar a opção que o sistema recusa.
    /// </summary>
    public static bool Permitida(OpcaoDeAtendimento opcao, string? especialidadePrincipal)
        => !EhPsicologia(especialidadePrincipal)
           || (opcao.EhConsulta && EhPsicologia(opcao.EspecialidadeCodigo));

    /// <summary>
    /// Como a linha abre: sem configuração (nulo) tudo o que é permitido vem MARCADO — é o que
    /// "sem restrição" significa; com configuração, só o que foi habilitado.
    /// </summary>
    public static bool MarcadaNaAbertura(OpcaoDeAtendimento opcao, IReadOnlyList<AtendimentoHabilitado>? atuais,
        string? especialidadePrincipal)
        => Permitida(opcao, especialidadePrincipal) && (atuais is null || atuais.Any(opcao.Casa));

    /// <summary>
    /// O que vai para <see cref="Profissional.HabilitacoesAtendimentoJson"/>. Nulo PRESERVA (o
    /// serviço não toca a coluna): é o que se grava quando quem salva não é o gerente — só ele
    /// define habilitações — e quando o gerente NÃO mexeu num profissional que nunca teve
    /// restrição. Sem esta segunda metade, abrir e salvar a ficha para corrigir o telefone
    /// trocaria "sem restrição" (que alcança a modalidade cadastrada no mês que vem) por uma
    /// lista fixa das modalidades de hoje — a mudança de comportamento que ninguém pediu.
    /// </summary>
    public static string? HabilitacoesParaGravar(bool podeConfigurar, bool alterou,
        IReadOnlyList<AtendimentoHabilitado>? originais, IEnumerable<OpcaoDeAtendimento> marcadas)
    {
        if (!podeConfigurar) return null;
        if (!alterou && originais is null) return null;
        return JsonSerializer.Serialize(marcadas
            .Select(o => new AtendimentoHabilitado(o.ModalidadeCodigo, o.EspecialidadeCodigo))
            .Distinct().ToList());
    }

    // ==================== Agenda fechada (bloqueios) ====================

    /// <summary>De quem a agenda está fechada. Sem profissional e sem sala = a clínica inteira (feriado).</summary>
    public static string AlvoDoBloqueio(BloqueioAgenda b)
        => b.DaClinica ? "Clínica inteira"
            : b.Profissional?.Rotulo
              ?? (b.Sala?.Nome is { } sala ? $"Sala {sala}" : "Profissional ou sala removido");

    /// <summary>"12/10/2026 · 08:00 às 12:00" num dia só; "dd/MM/yyyy HH:mm a dd/MM/yyyy HH:mm" em vários.</summary>
    public static string PeriodoDoBloqueio(BloqueioAgenda b)
    {
        var pt = CultureInfo.GetCultureInfo("pt-BR");
        if (b.Inicio.Date == b.Fim.Date)
        {
            var diaInteiro = b.Inicio.TimeOfDay == TimeSpan.Zero && b.Fim.TimeOfDay >= new TimeSpan(23, 59, 0);
            return diaInteiro
                ? $"{b.Inicio.ToString("dd/MM/yyyy", pt)} · dia inteiro"
                : $"{b.Inicio.ToString("dd/MM/yyyy", pt)} · {b.Inicio:HH:mm} às {b.Fim:HH:mm}";
        }
        return $"{b.Inicio.ToString("dd/MM/yyyy HH:mm", pt)} a {b.Fim.ToString("dd/MM/yyyy HH:mm", pt)}";
    }

    /// <summary>
    /// Bloquear NÃO desmarca ninguém: quem já estava marcado dentro aparece CONTADO, e a frase
    /// manda remarcar — quem desmarca é quem fala com o paciente.
    /// </summary>
    public static string SituacaoDoBloqueio(int marcadosDentro)
        => marcadosDentro switch
        {
            0 => "nada marcado dentro",
            1 => "1 sessão marcada dentro — remarque",
            var n => $"{n} sessões marcadas dentro — remarque"
        };

    /// <summary>
    /// Lê o "em quantos dias empurrar". Zero e texto não são deslocamento; negativo é permitido
    /// (puxar para trás), como no serviço.
    /// </summary>
    public static bool LerDeslocamento(string? texto, out int dias)
        => int.TryParse(texto?.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out dias) && dias != 0;

    // ==================== Painel ====================

    public static PainelDaEquipe Painel(IReadOnlyCollection<Profissional> profissionais, IReadOnlyCollection<Sala> salas,
        IReadOnlyCollection<int> marcadosDentroDeCadaBloqueio)
    {
        var ativos = profissionais.Where(p => p.Ativo).ToList();
        return new PainelDaEquipe(
            Ativos: ativos.Count,
            Inativos: profissionais.Count - ativos.Count,
            ComJornada: ativos.Count(p => p.JornadaDeclarada),
            ComCpf: ativos.Count(AssinaDigitalmente),
            SalasAtivas: salas.Count(s => s.Ativa),
            SalasInativas: salas.Count(s => !s.Ativa),
            VagasSimultaneas: salas.Where(s => s.Ativa).Sum(s => Math.Max(1, s.Capacidade)),
            AgendaFechada: marcadosDentroDeCadaBloqueio.Count,
            SessoesARemarcar: marcadosDentroDeCadaBloqueio.Sum());
    }

    private static string SemAcento(string texto)
    {
        var normalizado = texto.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalizado.Length);
        foreach (var c in normalizado)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
}
