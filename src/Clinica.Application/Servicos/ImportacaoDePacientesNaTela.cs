using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Qual dos três arquivos a pessoa escolheu — cada um mostra blocos diferentes.</summary>
public enum ModoDaImportacao
{
    /// <summary>Nenhum arquivo escolhido ainda.</summary>
    Nenhum,
    /// <summary>Um CSV avulso de pacientes, de qualquer sistema: o passo 2 pergunta cada coluna.</summary>
    Csv,
    /// <summary>O ZIP do Smart Clinic: colunas do formato conhecido, só os convênios a decidir.</summary>
    Pacote,
    /// <summary>O ZIP DE ARQUIVOS (receitas, laudos): sem coluna nem convênio — o passo 2 some.</summary>
    Anexos
}

/// <summary>Um convênio cadastrado aqui, como opção da lista (o primeiro é "definir depois").</summary>
public sealed record OpcaoDeConvenioImportado(string Rotulo, ConvenioCadastro Cadastro);

/// <summary>Um nome de convênio COMO ESTÁ NO ARQUIVO e quantas linhas o trazem.</summary>
public sealed record ConvenioDoArquivo(string Texto, int Linhas)
{
    public string LinhasTexto => Linhas == 1 ? "1 linha" : $"{Linhas} linhas";
}

/// <summary>Uma linha da conferência, já com o sinal (✓/✗) ou o recuo do motivo.</summary>
public sealed record LinhaDeConferencia(string Texto, bool Completo, bool EhMotivo);

/// <summary>A conferência depois de importar: o título que diz se fechou, e as linhas.</summary>
public sealed record ConferenciaNaTela(string Titulo, bool Fechou, IReadOnlyList<LinhaDeConferencia> Linhas);

/// <summary>O que a prévia do ZIP de arquivos mostra: o resumo em linhas, a frase, os avisos e o botão.</summary>
public sealed record PreviaDosAnexosNaTela(
    IReadOnlyList<string> Resumo, string ResumoPrevia, string? Avisos, string RotuloImportar, bool TemTrabalho);

/// <summary>
/// O que a tela "Importar pacientes" do Gerente AFIRMA (out/2026, etapa 6 — a tela saiu do WPF
/// para o Blazor). Era o <c>ImportacaoPacientesViewModel</c>: os rótulos únicos das colunas, a
/// sugestão do mapeamento e dos convênios, a ordem da prévia, as frases do resumo, do botão,
/// da confirmação e do resultado, e a conferência. Nada aqui lê o banco nem o disco — a tela
/// lê o arquivo pelo hospedeiro e chama os MESMOS serviços da importação
/// (<see cref="ImportacaoPacientesService"/>, <see cref="ImportacaoSmartClinicService"/>,
/// <see cref="ImportacaoAnexosSmartClinicService"/>).
///
/// A regra que a tela realiza é a dos serviços: ficha que já existe é COMPLETADA, nunca
/// sobrescrita; o mesmo arquivo importado duas vezes não duplica ninguém; e nada é gravado
/// antes de a direção ler a prévia linha a linha.
/// </summary>
public static class ImportacaoDePacientesNaTela
{
    /// <summary>A opção "esta coluna não entra" da lista de colunas.</summary>
    public const string NaoImportar = "(não importar)";

    /// <summary>
    /// A primeira opção de convênio: o padrão de quem não sabe. A ficha entra "a definir" e o
    /// sistema acusa no próximo agendamento/atendimento — a decisão acontece com o paciente na
    /// frente, ficha a ficha, e não 2.021 vezes antes de importar.
    /// </summary>
    public const string DefinirDepois = "(definir depois — o sistema avisa no próximo atendimento)";

    /// <summary>Teto das linhas "Não entra" listadas no resumo do ZIP de arquivos: trezentas
    /// linhas iguais escondem as três que importam.</summary>
    public const int TetoDeLinhasDeFora = 60;

    // ============================================================ passo 1 — o arquivo

    /// <summary>
    /// Um rótulo ÚNICO por coluna, na ordem dela: coluna sem nome vira "(coluna N)" — o Excel
    /// exporta cabeçalho vazio no fim — e nome repetido ganha " (2)", " (3)"… Sem isso, duas
    /// colunas "Telefone" virariam uma só na lista, e a segunda ficaria inalcançável.
    /// </summary>
    public static IReadOnlyList<string> RotulosUnicos(IReadOnlyList<string> colunas)
    {
        var vistos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var saida = new string[colunas.Count];
        for (var i = 0; i < colunas.Count; i++)
        {
            var nome = string.IsNullOrWhiteSpace(colunas[i]) ? $"(coluna {i + 1})" : colunas[i];
            var vezes = vistos.TryGetValue(nome, out var n) ? n + 1 : 1;
            vistos[nome] = vezes;
            saida[i] = vezes == 1 ? nome : $"{nome} ({vezes})";
        }
        return saida;
    }

    /// <summary>A linha que descreve o CSV lido: linhas, colunas, separador e codificação.</summary>
    public static string InfoDoCsv(TabelaImportada tabela)
        => $"{tabela.Linhas.Count} linha(s) · {tabela.Colunas.Count} coluna(s) · "
           + $"separador: {tabela.SeparadorRotulo} · codificação: {tabela.Codificacao}";

    /// <summary>A linha que descreve o ZIP do Smart Clinic: arquivos, pacientes, prontuário e agenda.</summary>
    public static string InfoDoPacote(PacoteSmartClinic pacote)
    {
        var pacientes = pacote.Tabela(PacoteSmartClinic.Pacientes)?.Linhas.Count ?? 0;
        var prontuario = PacoteSmartClinic.ArquivosDeProntuario.Where(pacote.Tem)
            .Sum(a => pacote.Tabela(a)!.Linhas.Count);
        return $"{pacote.Arquivos.Count} arquivo(s) · {pacientes} paciente(s) · "
               + $"{prontuario} registro(s) de prontuário · "
               + $"{pacote.Tabela(PacoteSmartClinic.Agenda)?.Linhas.Count ?? 0} horário(s) na agenda antiga"
               + (pacote.Ignorados.Count > 0 ? $" · {pacote.Ignorados.Count} arquivo(s) ignorado(s)" : "");
    }

    /// <summary>A linha que descreve o ZIP de arquivos: quantos, o índice, o tamanho e os órfãos.</summary>
    public static string InfoDosAnexos(PacoteAnexosSmartClinic pacote)
        => $"{pacote.Arquivos} arquivo(s) · {pacote.Relacao.Linhas.Count} linha(s) no índice · "
           + $"{pacote.BytesTotais / (1024 * 1024)} MB"
           + (pacote.SemLinhaNoIndice.Count > 0 ? $" · {pacote.SemLinhaNoIndice.Count} sem linha no índice" : "");

    // ============================================================ passo 2 — colunas

    /// <summary>
    /// A sugestão inicial: para cada campo da ficha, o rótulo da coluna que o
    /// <see cref="SugestorDeMapeamento"/> achou — ou <see cref="NaoImportar"/>.
    /// </summary>
    public static IReadOnlyDictionary<CampoImportacao, string> SugestaoDeColunas(
        TabelaImportada tabela, IReadOnlyList<string> rotulos)
    {
        var sugestao = SugestorDeMapeamento.Sugerir(tabela.Colunas);
        var saida = new Dictionary<CampoImportacao, string>();
        foreach (var campo in CamposImportacao.Todos)
            saida[campo] = sugestao.ColunaDe(campo) is { } i && i < rotulos.Count ? rotulos[i] : NaoImportar;
        return saida;
    }

    /// <summary>O mapeamento a partir do que a pessoa escolheu em cada campo (rótulo da coluna).</summary>
    public static MapeamentoImportacao Mapeamento(
        IReadOnlyDictionary<CampoImportacao, string> escolhas, IReadOnlyList<string> rotulos)
    {
        var mapa = new MapeamentoImportacao();
        foreach (var (campo, coluna) in escolhas)
        {
            if (coluna == NaoImportar) continue;
            var i = IndiceDe(rotulos, coluna);
            if (i >= 0) mapa.Definir(campo, i);
        }
        return mapa;
    }

    private static int IndiceDe(IReadOnlyList<string> rotulos, string rotulo)
    {
        for (var i = 0; i < rotulos.Count; i++)
            if (rotulos[i] == rotulo) return i;
        return -1;
    }

    /// <summary>O campo que tem de estar mapeado (linha sem nome não entra).</summary>
    public static bool Obrigatorio(CampoImportacao campo) => campo == CampoImportacao.Nome;

    // ============================================================ passo 2 — convênios

    /// <summary>O catálogo que vira opção: só os ATIVOS, por nome.</summary>
    public static IReadOnlyList<ConvenioCadastro> CatalogoAtivo(IEnumerable<ConvenioCadastro> catalogo)
        => catalogo.Where(c => c.Ativo).OrderBy(c => c.Nome, StringComparer.CurrentCultureIgnoreCase).ToList();

    /// <summary>
    /// As opções da lista de convênio: "definir depois" PRIMEIRO (o padrão de quem não sabe) e
    /// depois o catálogo ativo — sem a linha "a definir" do catálogo, que é a mesma coisa da
    /// primeira opção e apareceria duas vezes.
    /// </summary>
    public static IReadOnlyList<OpcaoDeConvenioImportado> OpcoesDeConvenio(IReadOnlyList<ConvenioCadastro> catalogoAtivo)
    {
        var opcoes = new List<OpcaoDeConvenioImportado> { new(DefinirDepois, ConvenioCadastro.ADefinir()) };
        foreach (var c in catalogoAtivo.Where(c =>
                     !string.Equals(c.Codigo, ConvenioCadastro.CodigoADefinir, StringComparison.OrdinalIgnoreCase)))
            opcoes.Add(new OpcaoDeConvenioImportado(c.Nome, c));
        return opcoes;
    }

    /// <summary>
    /// Os textos de convênio do arquivo, cada um com quantas linhas o trazem. Sem a coluna de
    /// convênio mapeada, a lista é UMA linha — "(em branco)", com todas as linhas — para a
    /// direção ainda decidir para onde vai quem não tem convênio no arquivo.
    /// </summary>
    public static IReadOnlyList<ConvenioDoArquivo> ConveniosAMapear(TabelaImportada tabela, MapeamentoImportacao mapa)
    {
        var col = mapa.ColunaDe(CampoImportacao.Convenio);
        if (col is null) return [new ConvenioDoArquivo(ImportacaoPacientesService.ConvenioEmBranco, tabela.Linhas.Count)];

        var textos = ImportacaoPacientesService.ConveniosDoArquivo(tabela, mapa);
        var contagem = tabela.Linhas
            .GroupBy(l => ImportacaoPacientesService.ChaveConvenio(col.Value < l.Length ? l[col.Value] : null),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        return textos.Select(t => new ConvenioDoArquivo(t, contagem.TryGetValue(t, out var n) ? n : 0)).ToList();
    }

    /// <summary>
    /// A sugestão pelo NOME: o índice da opção cujo nome normalizado é igual ao texto, contém
    /// ou está contido nele; sem nada parecido, a primeira ("definir depois"). Nunca um
    /// índice numa lista vazia.
    /// </summary>
    public static int SugerirConvenio(string texto, IReadOnlyList<OpcaoDeConvenioImportado> opcoes)
    {
        if (opcoes.Count == 0) return 0;
        var alvo = SugestorDeMapeamento.Normalizar(texto);
        if (alvo.Length == 0) return 0;
        for (var i = 1; i < opcoes.Count; i++)
        {
            var n = SugestorDeMapeamento.Normalizar(opcoes[i].Rotulo);
            if (n.Length == 0) continue;
            if (n == alvo || n.Contains(alvo) || alvo.Contains(n)) return i;
        }
        return 0;
    }

    /// <summary>Texto do arquivo → convênio daqui, pelo índice escolhido em cada linha.</summary>
    public static Dictionary<string, ConvenioCadastro> ConveniosEscolhidos(
        IEnumerable<(string Texto, int Indice)> escolhas, IReadOnlyList<OpcaoDeConvenioImportado> opcoes)
    {
        var saida = new Dictionary<string, ConvenioCadastro>(StringComparer.OrdinalIgnoreCase);
        foreach (var (texto, indice) in escolhas)
            if (indice >= 0 && indice < opcoes.Count) saida[texto] = opcoes[indice].Cadastro;
        return saida;
    }

    // ============================================================ passo 3 — prévia

    /// <summary>Quem pede ação sobe: problema e aviso primeiro, depois o que entra liso.</summary>
    public static IReadOnlyList<LinhaPrevia> LinhasEmOrdem(PreviaImportacao previa)
        => previa.Linhas
            .OrderByDescending(l => l.EhProblema)
            .ThenByDescending(l => l.TemAvisos)
            .ThenBy(l => l.Numero)
            .ToList();

    /// <summary>O tom do selo de cada destino (a cor diz o que acontece com a linha).</summary>
    public static string TomDoDestino(DestinoLinha destino) => destino switch
    {
        DestinoLinha.Criar => "sucesso",
        DestinoLinha.Completar => "info",
        DestinoLinha.JaImportada => "aviso",
        _ => "perigo"
    };

    /// <summary>A frase de cima da prévia: quantas novas, a completar, já importadas, de fora e com aviso.</summary>
    public static string ResumoDaPrevia(PreviaImportacao previa)
        => $"{previa.Criar} ficha(s) nova(s) · {previa.Completar} já cadastrada(s) a completar · "
           + $"{previa.JaImportadas} já importada(s) · {previa.Problemas} linha(s) que não entram"
           + (previa.ComAviso > 0 ? $" · {previa.ComAviso} com aviso" : "");

    /// <summary>Os avisos gerais (da carteira e do pacote), um por linha; nulo quando não há.</summary>
    public static string? AvisosGerais(PreviaImportacao previa, PreviaSmartClinic? pacote)
    {
        var avisos = previa.AvisosGerais.Concat(pacote?.Avisos ?? []).ToList();
        return avisos.Count == 0 ? null : string.Join("\n", avisos);
    }

    /// <summary>Há o que gravar? No modo pacote, o prontuário e a agenda contam também.</summary>
    public static bool TemTrabalho(PreviaImportacao previa, PreviaSmartClinic? pacote)
        => pacote?.TemTrabalho ?? previa.TemTrabalho;

    /// <summary>O botão diz o tamanho do lote ANTES do clique (parcela 69).</summary>
    public static string RotuloImportar(PreviaImportacao previa, PreviaSmartClinic? pacote)
    {
        if (!TemTrabalho(previa, pacote)) return "Nada a importar";
        var fichas = previa.Criar + previa.Completar;
        return pacote is null
            ? $"Importar {fichas} ficha(s)"
            : $"Importar o pacote ({fichas} ficha(s) · {pacote.EvolucoesNovas} registro(s) · {pacote.Agenda.FuturosNovos} horário(s))";
    }

    /// <summary>O resumo do pacote em linhas legíveis — uma por assunto, com os números.</summary>
    public static IReadOnlyList<string> ResumoDoPacote(PreviaSmartClinic p)
    {
        var linhas = new List<string>();
        foreach (var a in p.Prontuario)
            linhas.Add($"{a.Rotulo}: {a.Registros} registro(s) de {a.Pacientes} paciente(s) — {a.Novos} entram"
                       + (a.JaImportados > 0 ? $", {a.JaImportados} já importado(s)" : "")
                       + (a.SemPaciente > 0 ? $", {a.SemPaciente} de paciente que não está no pacote" : "")
                       + (a.Vazios > 0 ? $", {a.Vazios} vazio(s)" : "") + ".");
        linhas.Add($"Agenda antiga: {p.Agenda.Futuros} horário(s) de hoje em diante viram horário aqui ({p.Agenda.FuturosNovos} novo(s)"
                   + (p.Agenda.FuturosJaImportados > 0 ? $", {p.Agenda.FuturosJaImportados} já importado(s)" : "") + "); "
                   + $"{p.Agenda.Passados} visita(s) passada(s) de {p.Agenda.PacientesComHistorico} paciente(s) ficam como histórico nas observações da ficha.");
        if (p.AutoresReconhecidos.Count > 0)
            linhas.Add($"Autores do prontuário reconhecidos na Equipe: {string.Join(", ", p.AutoresReconhecidos)}.");
        if (p.Agenda.ProfissionaisReconhecidos.Count > 0)
            linhas.Add($"Profissionais da agenda reconhecidos na Equipe: {string.Join(", ", p.Agenda.ProfissionaisReconhecidos)}.");
        linhas.Add("Dados sem campo aqui (e-mail, RG, profissão, nome dos pais…) vão para as observações de cada ficha, com rótulo. "
                   + "Login e senha do sistema antigo não entram.");
        return linhas;
    }

    /// <summary>
    /// A prévia do ZIP de arquivos: uma linha por assunto, e as linhas de fora uma a uma (até
    /// <see cref="TetoDeLinhasDeFora"/>), com a frase, os avisos e o rótulo do botão.
    /// </summary>
    public static PreviaDosAnexosNaTela PreviaDosAnexos(PreviaAnexosSmartClinic previa)
    {
        var resumo = new List<string>
        {
            $"Arquivos da ficha: {previa.Novos} entram em {previa.PacientesQueRecebem} ficha(s) "
            + $"({previa.BytesNovos / (1024 * 1024)} MB)"
            + (previa.JaImportados > 0 ? $" · {previa.JaImportados} já importado(s)" : "")
            + (previa.SemPaciente > 0 ? $" · {previa.SemPaciente} sem ficha" : "")
            + (previa.SemArquivo > 0 ? $" · {previa.SemArquivo} sem o arquivo no ZIP" : "")
            + (previa.Invalidos > 0 ? $" · {previa.Invalidos} inválido(s)" : "") + "."
        };
        foreach (var l in previa.Linhas.Where(l => l.EhProblema).Take(TetoDeLinhasDeFora))
            resumo.Add($"  • Não entra — linha {l.Numero}, {l.Paciente}, {l.Titulo}: {l.Detalhe}");
        if (previa.Problemas > TetoDeLinhasDeFora)
            resumo.Add($"  • … e mais {previa.Problemas - TetoDeLinhasDeFora} linha(s) de fora.");
        resumo.Add("Cada arquivo entra em \"Exames e anexos\" da ficha do paciente, com a data do documento e a procedência escrita.");

        return new PreviaDosAnexosNaTela(
            resumo,
            $"{previa.Novos} arquivo(s) entram · {previa.JaImportados} já importado(s) · {previa.Problemas} linha(s) que não entram",
            previa.Avisos.Count == 0 ? null : string.Join("\n", previa.Avisos),
            previa.TemTrabalho ? $"Importar {previa.Novos} arquivo(s)" : "Nada a importar",
            previa.TemTrabalho);
    }

    // ============================================================ importar

    /// <summary>A pergunta antes de gravar a carteira (e o pacote): o tamanho do lote e o que não se desfaz.</summary>
    public static string ConfirmacaoDaImportacao(PreviaImportacao previa, PreviaSmartClinic? pacote)
    {
        var extra = pacote is null
            ? ""
            : $" Do prontuário antigo entram {pacote.EvolucoesNovas} registro(s), e {pacote.Agenda.FuturosNovos} "
              + "horário(s) futuro(s) da agenda antiga.";
        return $"Vão ser criadas {previa.Criar} ficha(s) nova(s) e completadas {previa.Completar} já "
               + $"cadastrada(s). As {previa.Problemas} linha(s) com problema ficam de fora.{extra}\n\n"
               + "Não há desfazer em lote: ficha importada por engano se remove uma a uma, pela ficha; registro "
               + "clínico importado não se apaga (cancela-se com motivo, como qualquer outro). Continuar?";
    }

    /// <summary>A pergunta antes de gravar o ZIP de arquivos.</summary>
    public static string ConfirmacaoDosAnexos(PreviaAnexosSmartClinic previa)
        => $"Vão entrar {previa.Novos} arquivo(s) em {previa.PacientesQueRecebem} ficha(s), como arquivos da ficha "
           + $"(registro clínico). As {previa.Problemas} linha(s) com problema ficam de fora.\n\n"
           + "Não há desfazer em lote: arquivo importado por engano se cancela um a um, com motivo, "
           + "pela ficha. Continuar?";

    /// <summary>
    /// A mensagem depois de gravar o CSV avulso. Meio sucesso não é sucesso (parcela 68): com
    /// erro, <c>TeveErro</c> é verdadeiro e a tela pinta com a cor de aviso.
    /// </summary>
    public static (string Texto, bool TeveErro) ResultadoDoCsv(ResultadoImportacao r)
    {
        var texto = $"Importação concluída: {r.Criados} ficha(s) nova(s), "
                    + $"{r.Completados} completada(s), {r.Pulados} já existente(s).";
        if (r.TeveErro)
            texto += $"\n{r.Erros.Count} linha(s) não gravaram:\n" + string.Join("\n", r.Erros.Take(20))
                     + (r.Erros.Count > 20 ? "\n…" : "");
        return (texto, r.TeveErro);
    }

    /// <summary>A mensagem depois de gravar o pacote: fichas, prontuário, agenda, erros e revínculos.</summary>
    public static (string Texto, bool TeveErro) ResultadoDoPacote(ResultadoSmartClinic r)
    {
        var texto = $"Importação concluída: {r.Pacientes.Criados} ficha(s) nova(s), {r.Pacientes.Completados} "
                    + $"completada(s), {r.Pacientes.Pulados} já existente(s) · {r.EvolucoesCriadas} registro(s) de "
                    + $"prontuário ({r.EvolucoesPuladas} já importado(s), {r.EvolucoesSemPaciente} sem ficha) · "
                    + $"{r.AgendamentosCriados} horário(s) futuro(s) ({r.AgendamentosPulados} já importado(s)).";
        var erros = r.Pacientes.Erros.Concat(r.Erros).ToList();
        if (erros.Count > 0)
            texto += $"\n{erros.Count} erro(s):\n" + string.Join("\n", erros.Take(20)) + (erros.Count > 20 ? "\n…" : "");
        if (r.Revinculados > 0)
            texto += $"\n{r.Revinculados} registro(s) de rodadas anteriores ganharam o vínculo com a Equipe.";
        return (texto, r.TeveErro);
    }

    /// <summary>A mensagem depois de gravar o ZIP de arquivos.</summary>
    public static (string Texto, bool TeveErro) ResultadoDosAnexos(ResultadoAnexosSmartClinic r)
    {
        var texto = $"Importação concluída: {r.Criados} arquivo(s) gravado(s) na ficha, "
                    + $"{r.Pulados} já importado(s), {r.SemPaciente} sem ficha.";
        if (r.TeveErro)
            texto += $"\n{r.Erros.Count} erro(s):\n" + string.Join("\n", r.Erros.Take(20));
        return (texto, r.TeveErro);
    }

    // ============================================================ conferência

    /// <summary>
    /// A CONFERÊNCIA do pacote: o ZIP relido contra o banco. É a prova de que tudo entrou — a
    /// mensagem do resultado diz o que se gravou; esta diz o que FALTA.
    /// </summary>
    public static ConferenciaNaTela ConferenciaDoPacote(IReadOnlyList<ItemConferencia> itens)
    {
        var fechou = ConferenciaSmartClinic.Fechou(itens);
        return new ConferenciaNaTela(
            fechou
                ? "CONFERÊNCIA: fechou — tudo o que tinha de entrar está no sistema; o que ficou de fora está listado com o motivo."
                : "CONFERÊNCIA: NÃO fechou — há registro que ainda não entrou. Leia os motivos e importe de novo.",
            fechou, Linhas(itens));
    }

    /// <summary>A conferência do ZIP de arquivos — a mesma prova do pacote.</summary>
    public static ConferenciaNaTela ConferenciaDosAnexos(IReadOnlyList<ItemConferencia> itens)
    {
        var fechou = ConferenciaAnexosSmartClinic.Fechou(itens);
        return new ConferenciaNaTela(
            fechou
                ? "CONFERÊNCIA: fechou — todo arquivo do índice está no sistema; o que ficou de fora está listado com o motivo."
                : "CONFERÊNCIA: NÃO fechou — há arquivo que ainda não entrou. Leia os motivos e importe de novo.",
            fechou, Linhas(itens));
    }

    private static IReadOnlyList<LinhaDeConferencia> Linhas(IReadOnlyList<ItemConferencia> itens)
    {
        var linhas = new List<LinhaDeConferencia>();
        foreach (var i in itens)
        {
            linhas.Add(new LinhaDeConferencia((i.Completo ? "✓ " : "✗ ") + i.Resumo, i.Completo, false));
            foreach (var m in i.ForaComMotivo) linhas.Add(new LinhaDeConferencia("– " + m, i.Completo, true));
        }
        return linhas;
    }
}
