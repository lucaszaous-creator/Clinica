using System.Globalization;
using System.Text;
using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Gravidade do que a conferência achou.</summary>
public enum GravidadePrescricao
{
    /// <summary>Contexto que o profissional deve ver antes de decidir.</summary>
    Informativo,

    /// <summary>
    /// Bate com uma ALERGIA registrada. Não impede — exige que alguém diga que viu.
    /// </summary>
    Alergia
}

/// <summary>
/// Um achado da conferência: o item prescrito, e a linha do prontuário que ele cutuca.
/// </summary>
/// <param name="Item">A linha da receita como o profissional a escreveu.</param>
/// <param name="Motivo">Frase pronta para a tela.</param>
public sealed record AlertaPrescricao(
    GravidadePrescricao Gravidade,
    string Item,
    string Motivo,
    int? ProblemaId = null);

/// <summary>
/// O que a conferência de uma prescrição devolve.
/// </summary>
public sealed record ConferenciaPrescricao(
    IReadOnlyList<AlertaPrescricao> Alertas,
    IReadOnlyList<ProblemaPaciente> Alergias,
    IReadOnlyList<ProblemaPaciente> MedicacoesEmUso)
{
    /// <summary>
    /// Há coincidência com alergia registrada. É o que faz a emissão pedir confirmação
    /// escrita em vez de sair calada.
    /// </summary>
    public bool ExigeConfirmacao => Alertas.Any(a => a.Gravidade == GravidadePrescricao.Alergia);

    public static ConferenciaPrescricao Vazia { get; } = new([], [], []);
}

/// <summary>
/// Conferência clínica da prescrição (parcela 40).
///
/// O buraco que este serviço fecha
/// -------------------------------
/// Desde a parcela 37 o sistema GUARDA as alergias do paciente
/// (<see cref="NaturezaProblema.Alergia"/>, com a regra de alertar mesmo quando dadas por
/// resolvidas) — e a emissão de receita nunca as consultou. Ou seja: a base sabia que a
/// paciente é alérgica a dipirona, o profissional escrevia "Dipirona 500mg" e o sistema
/// imprimia sem dizer uma palavra. É o mesmo defeito recorrente do projeto — dado gravado
/// sem leitor —, só que aqui ele não custa uma guia: custa uma anafilaxia.
///
/// O que este serviço NÃO é
/// ------------------------
/// <b>Não é checador de interação medicamentosa.</b> Isso exige base farmacológica
/// licenciada (Micromedex, Vidal, ANVISA/CMED estruturada), atualizada continuamente, e
/// não se improvisa com uma lista em código: uma checagem caseira de interações erraria
/// nos dois sentidos e — pior — passaria a impressão de estar cobrindo o assunto. O que
/// ele faz é comparar o que está sendo prescrito com <b>o que a própria clínica anotou
/// sobre este paciente</b>, que é informação que o sistema já tem e estava jogando fora.
///
/// A comparação é TEXTUAL, e a tela diz isso
/// -----------------------------------------
/// Não há código de medicamento no `ItemDocumento` — a receita é texto livre, por desenho
/// (ver o comentário da entidade). Então o casamento é por palavra normalizada: sem
/// acento, sem caixa, e só para termos com <see cref="TamanhoMinimoTermo"/> caracteres ou
/// mais. Esse piso não é detalhe: sem ele, uma alergia registrada como "AAS" acenderia em
/// qualquer palavra que contivesse essas letras, e alerta que dispara à toa é alerta que a
/// pessoa aprende a fechar sem ler — o que mataria o valor do alerta verdadeiro.
///
/// Pelo mesmo motivo a busca é por PALAVRA INTEIRA e não por trecho: "sal" dentro de
/// "salbutamol" não é uma coincidência clínica, é uma coincidência de letras.
///
/// <b>Avisa, e exige confirmação — não impede.</b> O registro de alergia pode estar
/// errado, pode ser intolerância leve, pode haver dessensibilização; quem decide é quem
/// assina. Mas prescrever um alérgeno registrado não pode acontecer <i>sem alguém
/// perceber</i>, e é por isso que este é o segundo caso do projeto em que a tela cobra uma
/// confirmação explícita — o primeiro é a divergência do fechamento de caixa.
/// </summary>
public sealed class PrescricaoService
{
    private readonly IClinicaRepositorio _repo;

    /// <summary>
    /// Piso do termo comparável. Três letras ou menos gera ruído demais ("ovo" dentro de
    /// "novopren"), e o alerta que dispara à toa treina a pessoa a ignorá-lo.
    /// </summary>
    public const int TamanhoMinimoTermo = 4;

    public PrescricaoService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>
    /// O contexto clínico do paciente para a tela de prescrição: alergias e medicação de
    /// uso contínuo. É o que se olha ANTES de escrever, e continua valendo com a receita
    /// em branco — por isso é separado da conferência dos itens.
    /// </summary>
    public async Task<ConferenciaPrescricao> ContextoAsync(
        int pacienteId, CancellationToken ct = default)
        => await ConferirAsync(pacienteId, [], ct);

    /// <summary>
    /// Confere os itens escritos contra o que o prontuário sabe do paciente.
    /// </summary>
    /// <param name="itens">
    /// As linhas da receita como foram escritas (descrição e, se houver, a posologia —
    /// passe as duas concatenadas quando o alérgeno puder estar no detalhe).
    /// </param>
    public async Task<ConferenciaPrescricao> ConferirAsync(
        int pacienteId, IEnumerable<string> itens, CancellationToken ct = default)
    {
        var problemas = await _repo.ProblemasDoPacienteAsync(pacienteId, somenteAtivos: false, ct);

        // Descartado é registro desdito: não alerta, não contextualiza. Alergia RESOLVIDA
        // continua alertando — "resolvida" numa alergia é quase sempre "não reagiu da
        // última vez", e o dia em que reagir é o dia em que o aviso teria valido.
        var alergias = problemas
            .Where(p => p.Natureza == NaturezaProblema.Alergia
                        && p.Situacao != SituacaoProblema.Descartado)
            .ToList();

        var medicacoes = problemas
            .Where(p => p.Natureza == NaturezaProblema.MedicacaoContinua
                        && p.Situacao == SituacaoProblema.Ativo)
            .ToList();

        var alertas = new List<AlertaPrescricao>();

        foreach (var item in itens)
        {
            if (string.IsNullOrWhiteSpace(item)) continue;

            var palavrasDoItem = Palavras(item);

            foreach (var alergia in alergias)
            {
                var termo = TermoQueCasa(alergia.Descricao, palavrasDoItem);
                if (termo is null) continue;

                var ressalva = alergia.Situacao == SituacaoProblema.Resolvido
                    ? " (registrada como resolvida — confira antes de prescrever)"
                    : string.Empty;

                alertas.Add(new AlertaPrescricao(
                    GravidadePrescricao.Alergia,
                    item.Trim(),
                    $"O paciente tem ALERGIA registrada a \"{alergia.Descricao}\"{ressalva}.",
                    alergia.Id));
            }
        }

        // As medicações em uso entram como contexto SEM casar com item nenhum: o valor
        // delas é justamente o que o profissional ainda não escreveu. Casá-las produziria
        // o alerta inútil "você prescreveu o que ele já toma" — que é o caso normal da
        // renovação de receita.
        foreach (var medicacao in medicacoes)
        {
            alertas.Add(new AlertaPrescricao(
                GravidadePrescricao.Informativo,
                string.Empty,
                $"Em uso contínuo: {medicacao.Descricao}.",
                medicacao.Id));
        }

        return new ConferenciaPrescricao(alertas, alergias, medicacoes);
    }

    /// <summary>
    /// Devolve o termo da alergia que aparece no item, ou <c>null</c>.
    ///
    /// Cada palavra da descrição da alergia vale como termo: "alergia a dipirona" casa por
    /// "dipirona", e não pela frase inteira — ninguém escreve a receita com as palavras
    /// que usou para anotar a alergia.
    /// </summary>
    private static string? TermoQueCasa(string descricaoDaAlergia, HashSet<string> palavrasDoItem)
        => Palavras(descricaoDaAlergia)
            .FirstOrDefault(termo => palavrasDoItem.Contains(termo));

    /// <summary>
    /// As palavras comparáveis de um texto: sem acento, minúsculas, sem pontuação, e só as
    /// que têm tamanho suficiente para não gerar coincidência boba.
    ///
    /// Palavras vazias do português ("para", "com", "uso") entram no piso de tamanho e
    /// passariam — por isso a lista de dispensáveis existe: sem ela, uma alergia anotada
    /// como "alergia a penicilina" casaria com QUALQUER receita, pela palavra "alergia"
    /// nunca, mas por "para"/"uso" sim, e o alerta perderia todo o sentido.
    /// </summary>
    private static HashSet<string> Palavras(string texto)
        => Normalizar(texto)
            .Split(SeparadoresDePalavra, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length >= TamanhoMinimoTermo)
            .Where(p => !Dispensaveis.Contains(p))
            .ToHashSet();

    private static readonly char[] SeparadoresDePalavra =
        [' ', ',', ';', '.', ':', '/', '\\', '(', ')', '[', ']', '-', '+', '\t', '\n', '\r'];

    /// <summary>
    /// Palavras que passam do piso de tamanho e não distinguem nada. Deliberadamente
    /// curta: cada palavra aqui é uma que o sistema deixa de comparar, e uma lista
    /// entusiasmada acabaria engolindo o nome de um princípio ativo.
    /// </summary>
    private static readonly HashSet<string> Dispensaveis =
    [
        "alergia", "alergias", "alergico", "alergica", "intolerancia", "intolerante",
        "para", "pela", "pelo", "como", "quando", "onde", "esse", "essa", "isso",
        "uso", "usar", "tomar", "todos", "todas", "dias", "dose", "doses", "vezes",
        "comprimido", "comprimidos", "capsula", "capsulas", "frasco", "frascos",
        "caixa", "caixas", "ampola", "ampolas", "gotas", "creme", "pomada", "xarope",
        "solucao", "injetavel", "oral", "topico", "noite", "manha", "tarde", "hora",
        "horas", "cada", "sendo", "conforme", "necessario", "continuo", "contida"
    ];

    /// <summary>
    /// Minúsculas e sem acento. Sem isto, "Dipirona" não casaria com "dipirona" e
    /// "penicilina" não casaria com "penicilína" digitado com acento por engano.
    /// </summary>
    private static string Normalizar(string texto)
    {
        var decomposto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var limpo = new StringBuilder(decomposto.Length);

        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                limpo.Append(c);
        }

        return limpo.ToString().Normalize(NormalizationForm.FormC);
    }
}
