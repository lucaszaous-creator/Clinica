using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>Uma guia da PRÉVIA: o que vai nascer, antes de nascer.</summary>
public sealed record LinhaPreviaGuia(
    string Tipo, string Ordem, string FaturarEm, string ComoObter,
    string Especialidade, bool EhSegundo, string Nota);

/// <summary>O que uma modalidade vai gerar — a frase escrita na linha dela.</summary>
public sealed record ConsequenciaDaModalidade(string QuantasGuias, string Quando, bool TemSegundoCodigo)
{
    /// <summary>Antes de a leitura voltar: "…" e não vazio (vazio se lê como "não gera nada").</summary>
    public static readonly ConsequenciaDaModalidade Calculando = new("…", "calculando…", false);

    /// <summary>A leitura falhou: número velho na tela seria falha exibida como sucesso.</summary>
    public static readonly ConsequenciaDaModalidade NaoCalculada = new("—", "não foi possível calcular", false);

    /// <summary>O convênio não tem regra para esta modalidade.</summary>
    public static readonly ConsequenciaDaModalidade SemPrevia = new("—", string.Empty, false);
}

/// <summary>Um código recém-gerado e o que o balcão diz ao paciente sobre ele.</summary>
public sealed record CodigoGeradoNaTela(
    string Tipo, string Especialidade, string Ordem, string FaturarEm, string ComoObter,
    string Situacao, bool Baixado, bool TemImpedimento);

/// <summary>
/// As regras da tela "Lançar e marcar atendimento" (Blazor, out/2026) que o
/// <c>NovoAtendimentoViewModel</c> WPF decidia: títulos por modo, o rótulo do botão que
/// promete o número de guias, a prévia do motor de regras traduzida em linhas, as frases dos
/// avisos (horário do dia, já lançado, carteirinha, cota, pendências), a pergunta de
/// duplicidade e o resultado do lançamento. Subiram para cá pela regra da casa — o que a
/// tela AFIRMA mora onde o <c>dotnet test</c> alcança. As frases são as MESMAS do WPF.
/// </summary>
public static class AtendimentoDoBalcaoNaTela
{
    public const string Travessao = "—";

    // ==================== o modo: lançar × marcar ====================

    public static string Titulo(bool marcar) => marcar ? "Marcar horário" : "Lançar atendimento";

    public static string Subtitulo(bool marcar) => marcar
        ? "O paciente não está aqui. O horário entra na agenda de quem atende — e quem precisar voltar ganha outro horário pela agenda."
        : "O paciente está no balcão. Entra como encaixe com o check-in feito, e a guia nasce agora. Quem já está na agenda entra pela Fila.";

    /// <summary>"FOI feito" mentiria sobre uma sessão marcada para a semana que vem.</summary>
    public static string TituloPassoModalidade(bool marcar) => marcar ? "O que foi pedido" : "O que foi feito";

    public static string TituloPassoQuando(bool marcar) => marcar ? "Quando e com quem" : "Quem atendeu";

    public static string RotuloOutro(bool marcar) => marcar ? "Marcar outro" : "Lançar outro";

    public static string TituloResumo(bool marcar) => marcar ? "O que vai ser marcado" : "O que vai ser lançado";

    /// <summary>Depois de gravar, o resumo fala no passado: "vai ser marcado" sobre o que já foi mente.</summary>
    public static string TituloResumo(bool marcar, bool concluido) => concluido
        ? (marcar ? "O que foi marcado" : "O que foi lançado")
        : TituloResumo(marcar);

    /// <summary>
    /// A hora digitada/escolhida vale? No MARCAR a tela abre SEM hora (out/2026): pré-preencher
    /// com o relógio do computador marcava "15:11" de quem não escolheu nada, e o "Marcar outro"
    /// herdava a hora do horário que acabou de ser marcado — o próximo paciente nascia em choque
    /// com o anterior.
    /// </summary>
    public static bool HoraValida(string? hora) => TimeOnly.TryParse(hora, out _);

    /// <summary>A recusa de hora em branco/inválida, no verbo do modo.</summary>
    public static string RecusaSemHora(bool marcar) => marcar
        ? "Escolha o horário — numa das pílulas ou digitando em \u201COutro horário\u201D."
        : "Informe a hora da sessão no formato HH:mm.";

    /// <summary>
    /// A linha "Guias" do resumo quando a guia NÃO nasce neste clique (marcar com a chave
    /// desligada): contar "1 guia · tudo hoje" ali afirmava uma guia que a marcação não cria.
    /// </summary>
    public const string GuiasNoFinalizar = "nascem quando o profissional finalizar";

    /// <summary>No marcar a hora é "Outro horário" (as pílulas são o caminho normal); no lançar é a hora.</summary>
    public static string RotuloHora(bool marcar) => marcar ? "Outro horário" : "Hora";

    /// <summary>O que acontece com as guias no modo marcar, escrito ANTES do clique.</summary>
    public static string NotaGuiaNaMarcacao(bool chaveLigada) => chaveLigada
        ? "As guias nascem JÁ NA MARCAÇÃO e vão para o faturamento — dá para efetivar no "
          + "portal antes da sessão. Cancelamento e falta suspendem as guias sozinhos."
        : "A guia nasce no dia da sessão, quando o profissional finalizar o atendimento.";

    /// <summary>
    /// A prévia só se mostra onde a guia NASCE neste clique: sempre no lançar; no marcar, só
    /// com a chave "guia no agendamento" ligada.
    /// </summary>
    public static bool MostraPrevia(bool marcar, bool chaveLigada) => !marcar || chaveLigada;

    /// <summary>
    /// O atendimento (e a exigência de convênio escolhido, parcela 92) só nasce neste clique
    /// no lançar, e no marcar com a chave ligada.
    /// </summary>
    public static bool NasceAtendimento(bool marcar, bool chaveLigada) => !marcar || chaveLigada;

    // ==================== quando ====================

    /// <summary>"qui, 22/10 às 14:30" — a mesma redação do botão e do resumo.</summary>
    public static string QuandoPorExtenso(DateOnly data, string? hora)
    {
        var dia = RodadaDeConfirmacao.TituloDoDia(data, DateOnly.MinValue);
        return TimeOnly.TryParse(hora, out var h) ? $"{dia} às {h:HH:mm}" : dia;
    }

    /// <summary>Duração digitada válida (minutos &gt; 0), ou nulo = a padrão do profissional.</summary>
    public static int? DuracaoInformada(string? texto)
        => int.TryParse(texto, out var m) && m > 0 ? m : null;

    public static int DuracaoEfetiva(string? texto, Profissional? profissional)
        => DuracaoInformada(texto)
           ?? (profissional?.DuracaoPadraoMinutos is > 0 ? profissional.DuracaoPadraoMinutos.Value
               : Agendamento.DuracaoPadraoMinutos);

    // ==================== o botão ====================

    /// <summary>
    /// O rótulo do botão DIZ o que vai acontecer: "Lançar e gerar 2 guias", "Lançar no
    /// horário das 09h00…", "Marcar qui, 22/10 às 14:30 e gerar 2 guias". Botão que promete o
    /// número é o que faz alguém perceber, antes de clicar, que escolheu a modalidade errada.
    /// </summary>
    public static string RotuloDoBotao(bool marcar, bool chaveLigada, int? totalGuias,
        string quandoPorExtenso, string? horaDoHorarioDoDia)
    {
        if (marcar)
        {
            return !chaveLigada
                ? $"Marcar {quandoPorExtenso}"
                : totalGuias switch
                {
                    null => $"Marcar {quandoPorExtenso} e gerar as guias",
                    0 => $"Marcar {quandoPorExtenso}",
                    1 => $"Marcar {quandoPorExtenso} e gerar 1 guia",
                    var t => $"Marcar {quandoPorExtenso} e gerar {t} guias"
                };
        }

        var sobre = horaDoHorarioDoDia is null ? "" : $" no horário das {horaDoHorarioDoDia}";
        return totalGuias switch
        {
            null => $"Lançar{sobre} e gerar as guias",
            0 => $"Lançar atendimento{sobre}",
            1 => $"Lançar{sobre} e gerar 1 guia",
            var t => $"Lançar{sobre} e gerar {t} guias"
        };
    }

    // ==================== modalidades ====================

    /// <summary>Modalidade dupla (1º hoje + 2º em +24h): deixa escolher qual código sai primeiro.</summary>
    public static bool EhDupla(ModalidadeAtendimento m)
        => m is ModalidadeAtendimento.AcupunturaComEletro or ModalidadeAtendimento.BsvComAcupuntura;

    /// <summary>As opções de "qual código libera primeiro" — vazio nas simples.</summary>
    public static IReadOnlyList<TipoCodigo> OpcoesPrimeiroCodigo(ModalidadeAtendimento m) => m switch
    {
        ModalidadeAtendimento.AcupunturaComEletro => [TipoCodigo.Acupuntura, TipoCodigo.Eletroacupuntura],
        ModalidadeAtendimento.BsvComAcupuntura => [TipoCodigo.Bsv, TipoCodigo.Acupuntura],
        _ => []
    };

    /// <summary>
    /// O primeiro código depois de trocar a modalidade: mantém o escolhido quando ele continua
    /// valendo (trocar a escolha do usuário sem ele pedir é o que fazia a prévia "não
    /// atualizar"), senão o primeiro da lista, ou nulo nas simples.
    /// </summary>
    public static TipoCodigo? PrimeiroCodigoApos(ModalidadeAtendimento m, TipoCodigo? escolhido)
    {
        var opcoes = OpcoesPrimeiroCodigo(m);
        if (opcoes.Count == 0) return null;
        return escolhido is { } atual && opcoes.Contains(atual) ? atual : opcoes[0];
    }

    /// <summary>
    /// As modalidades que a tela oferece: todas as ativas, ou só as que o profissional
    /// escolhido atende (consulta entra quando ele atende alguma especialidade).
    /// </summary>
    public static IReadOnlyList<EntradaModalidade> Modalidades(
        IReadOnlyList<EntradaModalidade> ativas, IReadOnlyList<EntradaEspecialidade> especialidades, Profissional? profissional)
        => ativas.Where(m => profissional is null
            || (m.Base == ModalidadeAtendimento.Consulta
                ? especialidades.Any(e => profissional.Atende(m.Codigo, e.Codigo))
                : profissional.Atende(m.Codigo))).ToList();

    /// <summary>As especialidades da consulta que o profissional escolhido atende.</summary>
    public static IReadOnlyList<EntradaEspecialidade> Especialidades(
        IReadOnlyList<EntradaEspecialidade> ativas, bool ehConsulta, string? modalidadeCodigo, Profissional? profissional)
        => ativas.Where(e => !ehConsulta || profissional is null || modalidadeCodigo is null
            || profissional.Atende(modalidadeCodigo, e.Codigo)).ToList();

    /// <summary>A classe da FAMÍLIA (o traço colorido da linha), a mesma da agenda do dia.</summary>
    public static string ClasseDaFamilia(ModalidadeAtendimento m) => m switch
    {
        ModalidadeAtendimento.AcupunturaComEletro => "familia-eletro",
        ModalidadeAtendimento.BsvApenas => "familia-bsv",
        ModalidadeAtendimento.BsvComAcupuntura => "familia-bsv-acupuntura",
        ModalidadeAtendimento.Consulta => "familia-consulta",
        _ => "familia-acupuntura"
    };

    /// <summary>"2 guias · a 2ª libera 09/08" — o que a regra do convênio gera nesta modalidade.</summary>
    public static ConsequenciaDaModalidade Consequencia(PreviaLancamento? previa, bool marcar = false)
    {
        if (previa is null) return ConsequenciaDaModalidade.SemPrevia;
        var total = previa.GuiasHoje + previa.GuiasDepois;
        // No MARCAR a sessão pode ser na semana que vem: "tudo hoje" mentia sobre a data.
        return new ConsequenciaDaModalidade(
            total == 1 ? "1 guia" : $"{total} guias",
            previa.LiberaEm is { } quando ? $"a 2ª libera {quando:dd/MM}" : marcar ? "tudo no dia da sessão" : "tudo hoje",
            previa.GuiasDepois > 0);
    }

    // ==================== a prévia ====================

    public static string RotularTipo(TipoCodigo t) => t switch
    {
        TipoCodigo.ConsultaEspecialidade => "Consulta de especialidade",
        TipoCodigo.Eletroacupuntura => "Eletroacupuntura",
        TipoCodigo.Bsv => "BSV",
        TipoCodigo.Acupuntura => "Acupuntura",
        TipoCodigo.Consulta => "Consulta",
        _ => t.ToString()
    };

    public static string RotularForma(FormaObtencao f) => f switch
    {
        FormaObtencao.NaoAplica => Travessao,
        FormaObtencao.App => "Pelo app (QR Code)",
        FormaObtencao.Sistema => "Pelo sistema",
        FormaObtencao.Ligacao => "Ligar para o paciente",
        _ => f.ToString()
    };

    public static int TotalDeGuias(PreviaLancamento previa) => previa.GuiasHoje + previa.GuiasDepois;

    /// <summary>As guias da prévia em linhas — o 2º código com a nota de que é ele que se esquece.</summary>
    public static IReadOnlyList<LinhaPreviaGuia> LinhasDaPrevia(PreviaLancamento previa)
        => previa.Codigos
            .Where(c => c.Status != StatusCodigo.NaoAplicavel)
            .OrderBy(c => c.DataPrevistaFaturamento).ThenBy(c => c.Ordem)
            .Select(c =>
            {
                var ehSegundo = c.Ordem == OrdemCodigo.Segundo;
                return new LinhaPreviaGuia(
                    RotularTipo(c.Tipo), ehSegundo ? "2º" : "1º",
                    c.DataPrevistaFaturamento.ToString("dd/MM/yyyy"), RotularForma(c.FormaObtencao),
                    c.EspecialidadeCodigo is { } cod ? CatalogoEspecialidades.Nome(cod) : Travessao,
                    ehSegundo,
                    ehSegundo
                        ? "É esta que se esquece — ela entra no painel de pendências do faturamento."
                        : "Já nasce faturável na data do atendimento.");
            }).ToList();

    public static string ResumoPrevia(PreviaLancamento previa)
    {
        var total = TotalDeGuias(previa);
        return previa.LiberaEm is { } quando
            ? $"{total} guia(s) — a 2ª só libera em {quando:dd/MM/yyyy}, e é ela que costuma se perder."
            : $"{total} guia(s), todas faturáveis na data do atendimento.";
    }

    /// <summary>A linha curta da coluna direita — "2 guias · a 2ª libera 05/09".</summary>
    public static string ResumoCurtoPrevia(PreviaLancamento previa)
    {
        var total = TotalDeGuias(previa);
        return total == 1
            ? "1 guia · hoje"
            : previa.LiberaEm is { } libera ? $"{total} guias · a 2ª libera {libera:dd/MM}" : $"{total} guias · hoje";
    }

    public const string PreviaFalhou = "Não foi possível calcular a prévia das guias — o lançamento continua liberado.";

    // ==================== avisos ====================

    /// <summary>
    /// O horário em aberto que o paciente já tem no dia (set/2026) — ou o terceiro estado
    /// quando a leitura falhou (sem ela a tela voltaria ao encaixe, que é a duplicata que o
    /// aviso existe para impedir).
    /// </summary>
    public static string? AvisoHorarioDoDia(HorarioEmAberto? horario, int outros, bool naoVerificado,
        bool marcar, DateOnly data)
    {
        if (horario is null)
            return naoVerificado
                ? $"Não foi possível conferir se o paciente já tem horário em {data:dd/MM}. Se tiver, "
                  + "lance pela Fila (Chegou → Entrou → Concluir) para não criar um segundo cartão."
                : null;

        var mais = outros > 0 ? $" (e mais {outros})" : "";
        return marcar
            ? $"Já tem horário em {data:dd/MM}: {horario.Descricao}{mais}. Marcar de novo cria um SEGUNDO "
              + "horário no mesmo dia."
            : $"Já tem horário em {data:dd/MM}: {horario.Descricao}{mais}. Ao lançar, o sistema usa ESSE "
              + "horário — a modalidade escolhida aqui passa a valer para ele, o check-in é carimbado e "
              + "a guia nasce nele. Nenhum encaixe é criado.";
    }

    /// <summary>A capa do que o paciente já tem lançado no dia (parcela 70) — o aviso ANTES da pergunta.</summary>
    public static string? AvisoJaLancado(IReadOnlyList<CapaDoDia> capas, DateOnly data)
        => AvisoJaLancado(capas, data, marcar: false, criaAtendimento: true);

    /// <summary>
    /// O mesmo aviso no verbo do MODO (out/2026): na tela de marcar, "Lançar de novo cria OUTRO
    /// atendimento" falava de um gesto que ela não faz — e, com a chave desligada, marcar não
    /// cria atendimento nem guia nenhuma: cria um segundo HORÁRIO no mesmo dia.
    /// </summary>
    public static string? AvisoJaLancado(IReadOnlyList<CapaDoDia> capas, DateOnly data, bool marcar, bool criaAtendimento)
    {
        if (capas.Count == 0) return null;
        var lista = string.Join(" · ", capas.Select(c => $"nº {c.Numero} ({c.Modalidade}, {c.Lancamento} — {c.ResumoGuias})"));
        if (!marcar)
            return $"Já lançado em {data:dd/MM}: {lista}. Lançar de novo cria OUTRO atendimento e outro jogo de guias.";
        return $"Já tem atendimento em {data:dd/MM}: {lista}. "
               + (criaAtendimento
                   ? "Marcar de novo cria OUTRO atendimento e outro jogo de guias."
                   : "Marcar de novo cria um SEGUNDO horário no mesmo dia.");
    }

    /// <summary>A pergunta INFORMADA do clique (parcela 70): pergunta, nunca recusa.</summary>
    public static string PerguntaDeRepetido(string paciente, IReadOnlyList<CapaDoDia> capas, DateOnly dia, bool marcar)
        => PerguntaDeRepetido(paciente, capas, dia, marcar, criaAtendimento: true);

    /// <summary>A pergunta no verbo do modo — marcar sem a chave cria um HORÁRIO, não guias.</summary>
    public static string PerguntaDeRepetido(string paciente, IReadOnlyList<CapaDoDia> capas, DateOnly dia, bool marcar, bool criaAtendimento)
    {
        if (marcar && !criaAtendimento)
        {
            var itens = string.Join("\n", capas.Take(4).Select(c =>
                $"  • Nº {c.Numero} — {c.Modalidade}, {c.Lancamento} — {c.ResumoGuias}"));
            if (capas.Count > 4) itens += $"\n  • … e mais {capas.Count - 4} atendimento(s)";
            return $"{paciente} já tem atendimento em {dia:dd/MM/yyyy}:\n\n{itens}\n\n"
                   + "Marcar de novo cria um SEGUNDO horário no mesmo dia.\n\nMarcar mesmo assim?";
        }
        var linhas = string.Join("\n", capas.Take(4).Select(c =>
            $"  • Nº {c.Numero} — {c.Modalidade}, {c.Lancamento} — {c.ResumoGuias}"));
        if (capas.Count > 4) linhas += $"\n  • … e mais {capas.Count - 4} atendimento(s)";
        var verbo = marcar ? "Marcar" : "Lançar";
        return $"{paciente} já tem atendimento em {dia:dd/MM/yyyy}:\n\n{linhas}\n\n"
               + $"{verbo} de novo cria OUTRO atendimento — e outro jogo de guias — para o mesmo dia.\n\n"
               + $"{verbo} mesmo assim?";
    }

    public static string? AvisoCarteirinha(Paciente p)
        => p.CarteirinhaVencida
            ? $"A carteirinha de {p.Nome} venceu em {p.ValidadeCarteirinha:dd/MM/yyyy} — o convênio pode recusar a guia."
            : null;

    /// <summary>A cota de sessões — o aviso que evita a glosa 2006 antes do prejuízo.</summary>
    public static string? SaldoAutorizacao(SaldoAutorizacao? saldo)
    {
        if (saldo is null) return null;
        var senha = string.IsNullOrWhiteSpace(saldo.Autorizacao.Numero) ? "Autorização" : $"Senha {saldo.Autorizacao.Numero}";
        return saldo.Vencida
            ? $"{senha}: venceu em {saldo.Autorizacao.DataValidade:dd/MM/yyyy} — peça uma nova antes de lançar."
            : $"{senha}: {saldo.Resumo} (válida até {saldo.Autorizacao.DataValidade:dd/MM/yyyy}).";
    }

    /// <summary>Cota esgotada ou vencida (vermelho) — ou na última sessão (âmbar).</summary>
    public static bool AutorizacaoCritica(SaldoAutorizacao? saldo) => saldo is { } s && (s.Vencida || s.Esgotada);

    public static bool AutorizacaoNaUltima(SaldoAutorizacao? saldo) => saldo is { } s && !AutorizacaoCritica(s) && s.NaUltima;

    /// <summary>Guias pendentes de atendimentos anteriores e NCs — para cobrar a guia na hora.</summary>
    public static string? AvisoPendencias(IReadOnlyList<PendenciaCodigo> pendentes, int naoConformidades)
    {
        if (pendentes.Count == 0 && naoConformidades == 0) return null;
        var partes = new List<string>();
        if (pendentes.Count > 0)
        {
            var itens = string.Join("; ", pendentes.Take(3).Select(p =>
            {
                var ordinal = p.Ordem == OrdemCodigo.Segundo ? "2ª" : "1ª";
                return $"{ordinal} guia de {RotularTipo(p.Tipo).ToLowerInvariant()} de {p.DataPrevista:dd/MM}";
            }));
            if (pendentes.Count > 3) itens += $"; +{pendentes.Count - 3}";
            partes.Add($"{pendentes.Count} guia(s) pendente(s) de baixa — cobre a guia agora! ({itens}.)");
        }
        if (naoConformidades > 0)
            partes.Add($"{naoConformidades} não conformidade(s) — serão reabertas ao lançar (o paciente voltou); cobre a(s) guia(s).");
        return "Este paciente tem " + string.Join(" ", partes);
    }

    /// <summary>
    /// Os alertas de elegibilidade que a tela JÁ diz por conta própria (carteirinha, cota,
    /// consulta) ficam de fora da lista — o mesmo aviso duas vezes se lê como dois problemas.
    /// </summary>
    public static bool JaDitoPelaTela(ImpedimentoElegibilidade motivo) => motivo is
        ImpedimentoElegibilidade.CarteirinhaVencida or ImpedimentoElegibilidade.CotaEsgotada
        or ImpedimentoElegibilidade.CotaQuaseNoFim or ImpedimentoElegibilidade.SemAutorizacaoVigente
        or ImpedimentoElegibilidade.ConsultaVencida or ImpedimentoElegibilidade.ConsultaARenovar;

    public const string SemConvenio =
        "Sem convênio vinculado — o atendimento não gera guia e não pode ser lançado. Escolha o convênio do paciente (particular, para quem paga do bolso).";

    public static string RecusaSemConvenio(string paciente)
        => $"{paciente} está sem convênio, e sem convênio o atendimento não gera guia. Escolha o convênio para lançar — "
           + "quem paga do bolso entra no convênio particular.";

    public const string SemProfissionalNaMarcacao =
        "Sem profissional: o horário não aparece no quadro de ninguém e fica fora do repasse. Escolha em \"Quem atende\" — o sistema não sorteia.";

    public const string RecusaSemProfissional =
        "Escolha quem vai atender: o horário marcado precisa de dono para aparecer na agenda do profissional e entrar no repasse.";

    // ==================== resultado ====================

    /// <summary>As linhas do resultado: o 1º nasce faturável; o 2º só a partir da data prevista.</summary>
    public static IReadOnlyList<CodigoGeradoNaTela> CodigosGerados(IEnumerable<CodigoFaturamento> codigos, DateOnly hoje)
        => codigos.OrderBy(c => c.DataPrevistaFaturamento).ThenBy(c => c.Ordem).Select(c =>
        {
            var liberada = c.EstaPendente(hoje);
            var impedimento = c.Baixado || liberada || c.Status == StatusCodigo.NaoAplicavel
                ? null : $"Libera em {c.DataPrevistaFaturamento:dd/MM/yyyy}";
            return new CodigoGeradoNaTela(
                RotularTipo(c.Tipo),
                c.EspecialidadeCodigo is { } cod ? CatalogoEspecialidades.Nome(cod) : Travessao,
                c.Ordem == OrdemCodigo.Segundo ? "2º" : "1º",
                c.DataPrevistaFaturamento.ToString("dd/MM/yyyy"),
                RotularForma(c.FormaObtencao),
                c.Baixado ? "Já baixada pelo faturamento" : impedimento ?? "Liberada — o faturamento já pode dar baixa",
                c.Baixado, impedimento is not null);
        }).ToList();

    /// <summary>"1 de 2 guia(s) já baixada(s)… — a próxima libera em…". Nulo sem guia faturável.</summary>
    public static string? ResumoBaixas(IEnumerable<CodigoFaturamento> codigos, DateOnly hoje)
    {
        var faturaveis = codigos.Where(c => c.Status != StatusCodigo.NaoAplicavel).ToList();
        if (faturaveis.Count == 0) return null;
        var baixadas = faturaveis.Count(c => c.Baixado);
        var proxima = faturaveis.Where(c => !c.Baixado && c.DataPrevistaFaturamento > hoje)
            .OrderBy(c => c.DataPrevistaFaturamento).FirstOrDefault();
        return $"{baixadas} de {faturaveis.Count} guia(s) já baixada(s) pelo faturamento"
               + (proxima is null
                   ? baixadas == faturaveis.Count ? " — nada pendente deste atendimento." : "."
                   : $" — a próxima libera em {proxima.DataPrevistaFaturamento:dd/MM/yyyy} e entra no painel de pendências do faturamento.");
    }

    /// <summary>O desfecho quando não há pacote/caixa/insumo a decidir.</summary>
    public static string DesfechoSemDecisao(int guiasGeradas) => guiasGeradas == 0
        ? "Atendimento registrado — sem guia a faturar."
        : $"Atendimento registrado — {guiasGeradas} guia(s) no faturamento.";

    /// <summary>O desfecho da marcação, pelo que nasceu junto do horário.</summary>
    public static string DesfechoDaMarcacao(DateTime dataHora, int? guias) => guias switch
    {
        null => $"Horário marcado para {dataHora:dd/MM/yyyy 'às' HH:mm}. A guia nasce quando o profissional finalizar o atendimento.",
        0 => "Horário marcado. Este convênio não gera guia (particular).",
        var g => $"Horário marcado — {g} guia(s) já no faturamento; dá para efetivar no portal antes da sessão."
    };

    /// <summary>Nome de arquivo sem os caracteres que o Windows recusa (o nome do paciente vem do banco).</summary>
    public static string NomeDoArquivo(string nome)
    {
        var invalidos = new HashSet<char>("\\/:*?\"<>|");
        var limpo = new string(nome.Select(c => invalidos.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim();
        return limpo.Length == 0 ? "documento.pdf" : limpo;
    }
}
