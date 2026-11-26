using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>Um "o quê" da Agenda da conversa: a modalidade do catálogo e, na consulta, a especialidade.</summary>
public sealed record AtendimentoNaAgenda(string ModalidadeCodigo, string? EspecialidadeCodigo, string Rotulo);

/// <summary>Uma vaga da agenda, com quem atende — a lista da aba Agenda e o "Enviar estes horários".</summary>
public sealed record VagaNaConversa(DateTime Inicio, int ProfissionalId, string Profissional);

/// <summary>O que a aba Agenda mostra sobre a ficha: ligar sozinha (uma ficha só), escolher (duas ou mais) ou cadastrar (nenhuma).</summary>
public enum DecisaoDoVinculo { JaLigada, LigarSozinha, Escolher, Cadastrar }

/// <summary>
/// A AGENDA DENTRO DA CONVERSA (nov/2026, aprovada pelo dono a partir do protótipo): a secretária confere vagas, marca,
/// remarca e desmarca sem sair da conversa. Aqui mora só o que a tela AFIRMA — as opções de atendimento (todas as que
/// a equipe ativa atende), quem atende cada uma, a junção das vagas e as frases. A AGENDA continua sendo a da clínica:
/// as vagas vêm do <see cref="BuscaDeVagasService"/> e marcar/remarcar/desmarcar passam pelo <see cref="AgendaService"/>
/// (pelo mesmo formulário da Agenda, com as travas e os avisos do convênio). Nenhuma regra de agenda nova.
/// </summary>
public static class AgendaDaConversa
{
    private static readonly System.Globalization.CultureInfo PtBr = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Quantas "próximas vagas" aparecem no topo (e quantas o "Enviar estes horários" cola na mensagem).</summary>
    public const int ProximasNoTopo = 3;

    /// <summary>Quantas vagas se pedem a cada profissional: o bastante para o calendário do mês ter os pontos dos dias com vaga.</summary>
    public const int VagasPorProfissional = 120;

    /// <summary>
    /// As opções do "O quê", de TODAS as especialidades que a equipe ativa atende (resposta do dono: todas aparecem). Vêm
    /// das habilitações de cada profissional; quem não tem habilitação declarada atende tudo — então entram as
    /// modalidades ativas do catálogo. Em ordem alfabética, sem repetir o mesmo rótulo.
    /// </summary>
    public static IReadOnlyList<AtendimentoNaAgenda> Opcoes(IEnumerable<Profissional> ativos)
    {
        var lista = new List<AtendimentoNaAgenda>();
        var semHabilitacao = false;
        foreach (var p in ativos)
        {
            if (p.HabilitacoesAtendimento is not { } hs) { semHabilitacao = true; continue; }
            lista.AddRange(hs.Select(h => new AtendimentoNaAgenda(h.ModalidadeCodigo, string.IsNullOrWhiteSpace(h.EspecialidadeCodigo) ? null : h.EspecialidadeCodigo, Rotulo(h.ModalidadeCodigo, h.EspecialidadeCodigo))));
        }
        if (semHabilitacao)
            lista.AddRange(CatalogoModalidades.Ativas.Select(m => new AtendimentoNaAgenda(m.Codigo, null, m.Nome)));
        return lista.GroupBy(o => o.Rotulo, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .OrderBy(o => o.Rotulo, StringComparer.Create(PtBr, true)).ToList();
    }

    /// <summary>"Psiquiatria" (consulta com especialidade), "Acupuntura", "BSV" — o nome que a recepção fala.</summary>
    public static string Rotulo(string modalidadeCodigo, string? especialidadeCodigo)
        => string.IsNullOrWhiteSpace(especialidadeCodigo) ? CatalogoModalidades.Nome(modalidadeCodigo) : CatalogoEspecialidades.Nome(especialidadeCodigo);

    /// <summary>Quem atende a opção (a mesma habilitação que o <see cref="AgendaService"/> confere ao marcar).</summary>
    public static IReadOnlyList<Profissional> QuemAtende(AtendimentoNaAgenda opcao, IEnumerable<Profissional> ativos)
        => ativos.Where(p => p.Atende(opcao.ModalidadeCodigo, opcao.EspecialidadeCodigo))
            .OrderBy(p => p.Rotulo, StringComparer.Create(PtBr, true)).ToList();

    /// <summary>As vagas de todos os profissionais escolhidos numa lista só, pela hora (no empate, pelo nome).</summary>
    public static IReadOnlyList<VagaNaConversa> Juntar(IEnumerable<(Profissional Profissional, IReadOnlyList<Vaga> Vagas)> porProfissional)
        => porProfissional.SelectMany(x => x.Vagas.Select(v => new VagaNaConversa(v.Inicio, x.Profissional.Id, x.Profissional.Rotulo)))
            .OrderBy(v => v.Inicio).ThenBy(v => v.Profissional, StringComparer.Ordinal).ToList();

    /// <summary>
    /// As próximas vagas do topo e do "Enviar estes horários": horários DIFERENTES (com "qualquer um", três profissionais às
    /// 13:30 não são três opções para a paciente — é uma). No mesmo horário, fica a primeira pela ordem da lista.
    /// </summary>
    public static IReadOnlyList<VagaNaConversa> Proximas(IEnumerable<VagaNaConversa> vagas, int quantas = ProximasNoTopo)
        => vagas.GroupBy(v => v.Inicio).Select(g => g.First()).OrderBy(v => v.Inicio).Take(quantas).ToList();

    /// <summary>Os dias que têm vaga (o ponto verde do calendário).</summary>
    public static IReadOnlySet<DateOnly> DiasComVaga(IEnumerable<VagaNaConversa> vagas)
        => vagas.Select(v => DateOnly.FromDateTime(v.Inicio)).ToHashSet();

    /// <summary>As vagas de um dia, pela hora.</summary>
    public static IReadOnlyList<VagaNaConversa> DoDia(IEnumerable<VagaNaConversa> vagas, DateOnly dia)
        => vagas.Where(v => DateOnly.FromDateTime(v.Inicio) == dia).OrderBy(v => v.Inicio).ToList();

    /// <summary>
    /// "Maria, tenho estes horários de acupuntura com a Dra. Helena: sexta-feira, 27/11 às 08:30, … ou … Qual fica melhor?"
    /// — o "Enviar estes horários" do protótipo aprovado. Com profissional escolhido, ele vai na frase; com "qualquer um",
    /// cada horário leva o nome de quem atende. Nulo sem vaga (o botão nem aparece).
    /// </summary>
    public static string? HorariosLivres(string? nome, string atendimento, IReadOnlyList<VagaNaConversa> vagas, DateOnly hoje, bool profissionalFixo)
    {
        if (vagas.Count == 0) return null;
        var quem = ConversasNaTela.PrimeiroNomeOu(nome, "");
        // "Acupuntura (apenas)" é nome de catálogo: na mensagem à paciente vai só "acupuntura".
        atendimento = System.Text.RegularExpressions.Regex.Replace(atendimento, @"\s*\([^)]*\)", "").Trim();
        var itens = Proximas(vagas)
            .Select(v => MensagensDeContato.QuandoPorExtenso(v.Inicio, hoje) + (profissionalFixo ? "" : $" ({v.Profissional})")).ToList();
        var lista = itens.Count == 1 ? itens[0] : string.Join(", ", itens.Take(itens.Count - 1)) + " ou " + itens[^1];
        var com = profissionalFixo ? RascunhosDaConversa.ComQuem(vagas[0].Profissional) : "";
        return $"{(quem.Length == 0 ? "Olá! T" : quem + ", t")}enho estes horários de {atendimento.ToLowerInvariant()}{com}: {lista}. Qual fica melhor?";
    }

    /// <summary>
    /// A FICHA DA CONVERSA PELO TELEFONE (nov/2026, pedido do dono: "a ficha deve se auto vincular com os pacientes com o
    /// mesmo número cadastrado"), sem esquecer que o telefone não é identidade: UMA ficha com o número liga sozinha (com
    /// trilha, e com o "Não é esta pessoa? Trocar" à vista); duas ou mais (a família no mesmo número) a recepção escolhe;
    /// nenhuma, a recepção cadastra. O número já chega comparado com e sem o 55 e o 9º dígito (<see cref="ConversasNaTela.MesmoTelefone"/>).
    /// </summary>
    public static DecisaoDoVinculo Vinculo(int? fichaDaConversa, int candidatas)
        => fichaDaConversa is not null ? DecisaoDoVinculo.JaLigada
            : candidatas == 1 ? DecisaoDoVinculo.LigarSozinha
            : candidatas > 1 ? DecisaoDoVinculo.Escolher
            : DecisaoDoVinculo.Cadastrar;

    /// <summary>A linha da trilha do vínculo automático (auditoria da clínica; o CRM registra a dele).</summary>
    public static string TrilhaDoVinculo(string telefone, int pacienteId, string nome)
        => $"Ficha {nome} (nº {pacienteId}) ligada sozinha à conversa do {telefone}: era a única ficha com este número.";

    /// <summary>O aviso no painel depois do vínculo automático.</summary>
    public const string AvisoDoVinculoAutomatico = "Ligada pelo telefone: é a única ficha com este número.";
}
