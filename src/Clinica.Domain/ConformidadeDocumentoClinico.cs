using Clinica.Domain.Entities;

namespace Clinica.Domain;

/// <summary>Peso de uma exigência legal — é o que decide a cor e a frase na tela.</summary>
public enum PesoExigencia
{
    /// <summary>
    /// Sem isto o documento NÃO cumpre a lei: a receita não pode ser aviada na farmácia,
    /// ou o documento eletrônico não vale. Não impede emitir (ver o comentário da classe).
    /// </summary>
    Impeditiva,

    /// <summary>Falta recomendada: o documento vale, mas a via fica pior do que podia.</summary>
    Recomendada
}

/// <summary>
/// Uma exigência que falta, já escrita como a tela mostra.
/// </summary>
/// <param name="Descricao">O que falta, na voz de quem vai resolver.</param>
/// <param name="Fundamento">De onde a exigência vem — a norma, escrita por extenso.</param>
public sealed record ExigenciaLegal(string Descricao, string Fundamento, PesoExigencia Peso)
{
    public bool Impeditiva => Peso == PesoExigencia.Impeditiva;

    public string Frase => $"{Descricao} ({Fundamento})";
}

/// <summary>
/// O que a lei exige de cada documento clínico, conferido ANTES de o papel sair.
///
/// Por que isto existe
/// -------------------
/// O sistema imprime receita desde a parcela 3, e nunca soube dizer se o que imprimia
/// podia ser aviado. O art. 35 da Lei 5.991/1973 (com a redação da Lei 14.063/2020) é
/// explícito sobre o que a receita precisa TER — nome e endereço residencial do
/// paciente, modo de usar, data, assinatura, endereço do consultório e o número de
/// inscrição do prescritor no conselho — e a clínica descobria a falta na farmácia, com
/// o paciente na fila. Descobrir o requisito errando é o que faz a pessoa desistir do
/// sistema; é a mesma razão do <c>FolhaCatalogo.Exigencia</c> da central de documentos.
///
/// Avisa, não impede — e a exceção
/// -------------------------------
/// Vale a regra da casa: o sistema avisa e quem decide é quem assina. O cadastro pode
/// estar incompleto por motivo legítimo, e travar a emissão deixaria o paciente sem o
/// papel numa sexta-feira à noite por causa de um campo de endereço.
///
/// A exceção está na assinatura ELETRÔNICA, e ela é da lei, não nossa: pelo art. 13 da
/// Lei 14.063/2020, atestado médico e receituário de controle especial em meio
/// eletrônico <b>só valem</b> com assinatura eletrônica QUALIFICADA (ICP-Brasil). Um
/// atestado eletrônico sem certificado não é um atestado com defeito — é um arquivo sem
/// valor. Por isso, quando o documento vai ser assinado eletronicamente, esta conferência
/// é chamada de novo pelo serviço de assinatura, e lá a falta impede mesmo.
///
/// Papel continua valendo
/// ----------------------
/// Nada disto diz que a receita em papel deixou de valer: impressa e assinada à caneta
/// ela é válida, sempre foi, e é o que a clínica faz hoje. A conferência é sobre o
/// CONTEÚDO, que a lei exige nas duas formas.
/// </summary>
public static class ConformidadeDocumentoClinico
{
    /// <summary>Fundamento citado nas faltas de conteúdo da receita.</summary>
    public const string Art35 = "art. 35 da Lei 5.991/1973";

    /// <summary>Fundamento da assinatura qualificada obrigatória.</summary>
    public const string Art13 = "art. 13 da Lei 14.063/2020";

    /// <summary>
    /// Os tipos que, em meio eletrônico, a lei só aceita com assinatura QUALIFICADA.
    ///
    /// O receituário de controle especial também está no art. 13 e não aparece aqui
    /// porque o sistema não emite receita de controle especial: ela depende do SNCR da
    /// ANVISA (ver <c>docs/prescricao-eletronica-conformidade.md</c>). Listá-la daria a
    /// entender que o sistema a cobre.
    /// </summary>
    public static bool ExigeAssinaturaQualificada(TipoDocumentoClinico tipo)
        => tipo == TipoDocumentoClinico.Atestado;

    /// <summary>
    /// O que falta para este documento cumprir a lei.
    ///
    /// Lista vazia = está conforme. A ordem é a da conversa: primeiro o que impede, e
    /// dentro disso o que quem está na tela consegue resolver agora.
    /// </summary>
    /// <param name="assinaturaEletronica">
    /// O documento vai ser assinado eletronicamente. Muda o resultado porque a via em
    /// papel resolve na caneta o que o arquivo só resolve com certificado.
    /// </param>
    public static IReadOnlyList<ExigenciaLegal> Conferir(
        DocumentoClinico documento, bool assinaturaEletronica = false)
    {
        ArgumentNullException.ThrowIfNull(documento);

        var faltas = new List<ExigenciaLegal>();

        // ---- Vale para qualquer documento que alguém assina ----

        // Sem prescritor identificado não há o que conferir na farmácia nem no RH: o
        // documento sai em nome da clínica e de ninguém.
        if (documento.Profissional is null && documento.ProfissionalId is null)
            faltas.Add(new ExigenciaLegal(
                "Escolha o profissional que assina o documento",
                Art35, PesoExigencia.Impeditiva));
        else if (string.IsNullOrWhiteSpace(documento.Profissional?.RegistroConselho))
            faltas.Add(new ExigenciaLegal(
                "O profissional está sem registro no conselho (CRM/CREFITO) — cadastre em Equipe",
                Art35, PesoExigencia.Impeditiva));

        if (string.IsNullOrWhiteSpace(documento.Paciente?.Nome))
            faltas.Add(new ExigenciaLegal(
                "O paciente está sem nome no cadastro", Art35, PesoExigencia.Impeditiva));

        // ---- Só a receita ----

        if (documento.Tipo == TipoDocumentoClinico.Receita)
        {
            // A exigência que mais volta da farmácia, e a que o sistema nem tinha onde
            // guardar antes desta parcela.
            if (string.IsNullOrWhiteSpace(documento.Paciente?.Endereco))
                faltas.Add(new ExigenciaLegal(
                    "O paciente está sem endereço residencial — a receita não pode ser aviada sem ele",
                    Art35, PesoExigencia.Impeditiva));

            if (documento.Itens.Count == 0)
                faltas.Add(new ExigenciaLegal(
                    "A receita está sem nenhum item prescrito", Art35, PesoExigencia.Impeditiva));

            // "Modo de usar" é literal no art. 35. Um medicamento sem posologia obriga o
            // farmacêutico a adivinhar a dose, que é exatamente o que a lei impede.
            var semPosologia = documento.Itens
                .Where(i => string.IsNullOrWhiteSpace(i.Detalhe))
                .Select(i => i.Descricao)
                .ToList();

            if (semPosologia.Count > 0)
                faltas.Add(new ExigenciaLegal(
                    semPosologia.Count == 1
                        ? $"“{semPosologia[0]}” está sem o modo de usar (posologia)"
                        : $"{semPosologia.Count} itens estão sem o modo de usar (posologia)",
                    Art35, PesoExigencia.Impeditiva));
        }

        // ---- Só em meio eletrônico ----

        if (assinaturaEletronica) return Ordenar(faltas);

        if (ExigeAssinaturaQualificada(documento.Tipo))
            faltas.Add(new ExigenciaLegal(
                $"{TipoDocumentoInfo.Rotular(documento.Tipo)} em arquivo só vale assinado com "
                + "certificado ICP-Brasil — imprima e assine à caneta, ou assine digitalmente",
                Art13, PesoExigencia.Recomendada));

        return Ordenar(faltas);
    }

    /// <summary>
    /// O documento cumpre o que a lei exige dele? Atalho para tela e teste.
    /// </summary>
    public static bool Conforme(DocumentoClinico documento, bool assinaturaEletronica = false)
        => !Conferir(documento, assinaturaEletronica).Any(f => f.Impeditiva);

    private static IReadOnlyList<ExigenciaLegal> Ordenar(List<ExigenciaLegal> faltas)
        => faltas.OrderBy(f => f.Peso).ToList();
}
