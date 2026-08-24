using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Clinico.Janelas;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>
/// Um alerta administrativo sobre o paciente que está na sala, com a urgência que veio do
/// <c>ElegibilidadeService</c> — nunca uma recalculada pela tela, que não tem como saber
/// se "cota esgotada" pesa mais do que "conta vencida".
/// </summary>
public sealed class LinhaAlertaClinico
{
    public required string Texto { get; init; }

    /// <summary>Vermelho na origem: atender assim provavelmente vira glosa.</summary>
    public required bool Grave { get; init; }
}

/// <summary>Uma sessão anterior, do jeito que o consultório precisa relê-la: inteira.</summary>

/// <summary>
/// A tela do ATENDIMENTO — onde a sessão é escrita enquanto o paciente ainda está na sala.
///
/// Por que não é a janela de evolução da recepção
/// ----------------------------------------------
/// A recepção escreve evolução de vez em quando, num diálogo modal aberto de dentro do
/// prontuário. O profissional escreve TODA sessão, e enquanto conversa com alguém. São
/// dois usos diferentes do mesmo dado, e a diferença aparece no leiaute: aqui as três
/// últimas sessões ficam ABERTAS ao lado do formulário, porque a primeira coisa que se faz
/// ao receber um paciente de tratamento é reler o que foi feito da última vez. Numa janela
/// modal isso não cabe — e a arquitetura da suíte não permitiria reaproveitá-la de outro
/// módulo de qualquer forma (nenhum módulo conhece os outros).
///
/// A EVA em par
/// ------------
/// Antes e depois, sempre. É a regra que o projeto inteiro aplica: uma medida solta não
/// diz se a sessão funcionou, e o campo "depois" é preenchido no fim do atendimento — por
/// isso a tela permite salvar com só o "antes" e volta a cobrar o par no resumo.
/// </summary>
public sealed partial class AtendimentoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly PacienteEmFoco _foco;

    /// <summary>Quantas sessões anteriores ficam abertas ao lado do formulário.</summary>
    private const int SessoesAnterioresVisiveis = 3;

    /// <summary>
    /// O mapa corporal da sessão — o mesmo componente do shell que a Recepção usa
    /// (parcela 36). É a ferramenta central da acupuntura, que é a especialidade da casa:
    /// um app para quem atende sem onde marcar o ponto seria um app para outra clínica.
    ///
    /// É RECRIADO a cada carga porque ele nasce amarrado a um paciente e a uma evolução —
    /// reaproveitar a instância entre pacientes traria os pontos de um para a sessão do
    /// outro, que é o pior defeito possível num prontuário.
    /// </summary>
    [ObservableProperty] private MapaCorporalViewModel? _mapa;

    public ObservableCollection<ResumoSessaoAnterior> Anteriores { get; } = [];

    /// <summary>
    /// O que os OUTROS módulos sabem sobre este paciente e que importa com ele na sala
    /// (parcela 36): carteirinha vencida e cota estourada vêm do Faturamento, conta
    /// vencida vem do Financeiro, guia glosada vem do Faturamento.
    ///
    /// É o sentido de VOLTA do compartilhamento. O `ElegibilidadeService` foi construído
    /// para o balcão — o único lugar onde o paciente está de corpo presente —, e o
    /// consultório é o segundo: quem está com ele por vinte minutos pode dizer "passe na
    /// recepção ao sair, sua autorização acabou", e é a coisa mais barata que a clínica
    /// faz para não glosar a sessão seguinte.
    ///
    /// É AVISO, nunca impedimento: a sessão clínica não se recusa por pendência
    /// administrativa.
    /// </summary>
    public ObservableCollection<LinhaAlertaClinico> Alertas { get; } = [];

    /// <summary>
    /// O que o PRONTUÁRIO avisa sobre este paciente: alergia e medicação de uso contínuo
    /// (parcela 37).
    ///
    /// Fica numa lista separada da administrativa de propósito. As duas são "avisos", e é
    /// só isso que têm em comum: carteirinha vencida se resolve no balcão depois, alergia
    /// se resolve ANTES de prescrever. Misturá-las faria a linha que impede um dano
    /// dividir espaço com a que lembra de uma cota — e a experiência do projeto com o
    /// <c>ElegibilidadeService</c> é clara: alerta que divide lugar com o resto é alerta
    /// que ninguém lê.
    ///
    /// Alergia dada por RESOLVIDA continua aqui: "resolvida" numa alergia é quase sempre
    /// "não reagiu da última vez", e o dia em que reagir é o dia em que o aviso teria
    /// valido. Só o descarte a cala.
    /// </summary>
    public ObservableCollection<LinhaAlertaClinico> AlertasClinicos { get; } = [];

    /// <summary>
    /// O termo do procedimento de hoje que ainda falta assinar (parcela 66, 2ª rodada).
    /// Null = nada pendente. É o que dá PORTA ao alerta que já chegava aqui.
    /// </summary>
    private SituacaoTermo? TermoPendente { get; set; }

    public bool TemTermoPendente => TermoPendente is not null;

    /// <summary>
    /// A metade VISÍVEL do acesso; a que IMPEDE está no comando. Quem atende recebe
    /// <see cref="Permissao.ColherAssinaturaPaciente"/> por padrão desde a parcela 66.
    /// </summary>
    /// <remarks>
    /// Não exige pendência: quem atende pode colher um termo AVULSO com o paciente na sala
    /// — foi para isso que a cliente pediu a porta aqui (o paciente que vem tirar dúvidas
    /// semanas antes do procedimento). Sem pendência, a janela pergunta qual termo é.
    /// </remarks>
    public bool PodeColherTermo
        => SessaoUsuario.Atual.Pode(Permissao.ColherAssinaturaPaciente);

    /// <summary>
    /// Abre a coleta do termo com o paciente já na sala — a MESMA janela do balcão
    /// (<c>AssinaturaPacienteWindow</c>, no shell). Copiá-la daria duas telas divergindo na
    /// primeira correção, e o que elas colhem é a prova de que o paciente consentiu.
    /// </summary>
    [RelayCommand]
    private async Task ColherTermoAsync()
    {
        // A barreira que IMPEDE, e ela DIZ por que recusou (a lição da parcela 41).
        //
        // ⚠️ INLINE, não snackbar. A convenção do projeto reserva o snackbar para a
        // confirmação passageira; recusa de permissão é o que exige CORREÇÃO — e ela some
        // em 4s, então quem olhou o paciente no meio tempo perde o motivo. A frase leva a
        // INSTRUÇÃO junto, como a mesma recusa já faz na Recepção: sem dizer o nome do
        // acesso, a pessoa não tem o que pedir à direção.
        if (!SessaoUsuario.Atual.Pode(Permissao.ColherAssinaturaPaciente))
        {
            Mensagem = "Você não tem permissão para colher a assinatura do paciente. "
                       + "Peça à direção o acesso \"Colher assinatura do paciente\".";
            MensagemEhErro = true;
            return;
        }

        if (PacienteId == 0)
        {
            Mensagem = "Escolha um paciente antes de colher o termo.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            // Modelo NULO quando não há pendência: a janela pergunta qual termo é. É a
            // porta que a cliente pediu — o paciente veio tirar dúvidas, e a assinatura se
            // colhe ali, sem esperar o dia do procedimento.
            var concluiu = Clinica.Desktop.Shell.Componentes.ColetaDeTermo.Abrir(
                _escopos, PacienteId, Paciente,
                TermoPendente?.ModeloId, TermoPendente?.DocumentoId,
                TermoPendente?.ProfissionalId);

            // Recarrega mesmo sem concluir: abrir a janela já EMITE o termo numerado, e a
            // tela precisa refletir isso.
            await CarregarAsync();

            if (concluiu) _snackbar.Sucesso("Termo do procedimento resolvido.");
        }
        catch (Exception ex)
        {
            _snackbar.Erro($"Não foi possível abrir o termo: {ex.Message}");
            Clinica.Application.Diagnostico.Registrar("Consultório — coleta do termo", ex);
        }
    }

    /// <summary>Evolução em edição. 0 = sessão nova.</summary>
    [ObservableProperty] private int _evolucaoId;

    [ObservableProperty] private string _paciente = string.Empty;
    [ObservableProperty] private bool _semPaciente = true;

    [ObservableProperty] private DateTime _data = DateTime.Today;

    [ObservableProperty] private int? _evaAntes;
    [ObservableProperty] private int? _evaDepois;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubjetivoPreenchido))]
    private string? _queixaPrincipal;

    // ---- O registro do ATENDIMENTO (parcela 73) ----
    //
    // Até aqui a sessão eram quatro caixas de texto e o par de EVA: o prontuário registrava
    // o que foi FEITO e não dizia por quê. Os três campos são OPCIONAIS, e a seção nasce
    // RECOLHIDA — a sessão curta de manutenção continua sendo queixa + conduta, e obrigar
    // quem faz vinte por dia a preencher anamnese faria a clínica escrever "idem" em todas.

    // ==================== As quatro abas da sessão (parcela 77) ====================
    //
    // A ficha era UMA coluna de rolagem com nove campos: quem escrevia perdia o começo de
    // vista ao chegar no fim, e a seção da parcela 73 tinha virado um Expander RECOLHIDO —
    // isto é, metade do registro clínico escondida atrás de um clique que ninguém dá.
    //
    // A divisão é o S-O-A-P, que é como todo prontuário do mundo se organiza e — não por
    // acaso — é a mesma forma das cinco etapas da COFEN do lado da enfermagem: o que a
    // pessoa DIZ, o que se ACHA nela, o que isso É, e o que se vai FAZER.
    //
    // ⚠️ Sub-aba ESCONDE campo, e esconder campo de prontuário é como se escreve menos sem
    // perceber. É por isso que cada aba anuncia se TEM conteúdo: o ponto no rótulo é visível
    // de qualquer aba, e é ele que denuncia o "Subjetivo" vazio de quem foi direto ao Plano.
    // Sem esse indicador a reorganização seria uma troca de leiaute que piora o registro.

    /// <summary>O que a pessoa DIZ — queixa e história da doença atual.</summary>
    public bool SubjetivoPreenchido =>
        !string.IsNullOrWhiteSpace(QueixaPrincipal)
        || !string.IsNullOrWhiteSpace(HistoriaDoencaAtual);

    /// <summary>O que se ACHOU nela — o exame físico. (Os sinais vitais são leitura.)</summary>
    public bool ObjetivoPreenchido => !string.IsNullOrWhiteSpace(ExameFisico);

    /// <summary>O que isso É — hipótese e CID.</summary>
    public bool AvaliacaoPreenchida =>
        !string.IsNullOrWhiteSpace(HipoteseDiagnostica)
        || !string.IsNullOrWhiteSpace(CidSessao);

    /// <summary>O que se vai FAZER — conduta, evolução, orientações e plano.</summary>
    public bool PlanoPreenchido =>
        !string.IsNullOrWhiteSpace(Conduta)
        || !string.IsNullOrWhiteSpace(TextoEvolucao)
        || !string.IsNullOrWhiteSpace(Orientacoes)
        || !string.IsNullOrWhiteSpace(PlanoTerapeutico)
        || RetornoSugeridoEm is not null
        || !string.IsNullOrWhiteSpace(RetornoSugeridoNota)
        || !string.IsNullOrWhiteSpace(Encaminhamento);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubjetivoPreenchido))]
    private string? _historiaDoencaAtual;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ObjetivoPreenchido))]
    private string? _exameFisico;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvaliacaoPreenchida))]
    private string? _hipoteseDiagnostica;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DescricaoCid))]
    [NotifyPropertyChangedFor(nameof(AvaliacaoPreenchida))]
    private string? _cidSessao;

    /// <summary>
    /// A descrição do CID digitado — a metade que PEGA o erro.
    ///
    /// "M54.4" no lugar de "M54.5" é tão plausível quanto o certo, e nada mais o denuncia
    /// (parcela 63). Código fora do catálogo desta clínica não é recusado: o campo aceita o
    /// que for digitado, e a frase diz que ele não foi reconhecido em vez de calar.
    /// </summary>
    public string? DescricaoCid
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CidSessao)) return null;

            return CatalogoCid.Descrever(CidSessao) is { } descricao
                ? descricao
                : "Código fora do catálogo desta clínica — confira antes de gravar.";
        }
    }

    /// <summary>Abre a busca do CID — atalho com conferência, nunca lista fechada.</summary>
    [RelayCommand]
    private void BuscarCid()
    {
        if (BuscaCidWindow.Perguntar(CidSessao) is { } escolhido)
            CidSessao = escolhido;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlanoPreenchido))]
    private string? _conduta;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlanoPreenchido))]
    private string? _textoEvolucao;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlanoPreenchido))]
    private string? _orientacoes;

    /// <summary>
    /// O PLANO: o que vem pela frente. Ver <c>Evolucao.PlanoTerapeutico</c> para por que ele
    /// não é a conduta nem a orientação.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlanoPreenchido))]
    private string? _planoTerapeutico;

    /// <summary>
    /// QUANDO reavaliar. `DateTime?` porque é o que o `DatePicker` amarra; o domínio guarda
    /// `DateOnly`, e a conversão acontece na borda.
    ///
    /// ⚠️ Não vira agendamento nenhum — ver <c>Evolucao.RetornoSugeridoEm</c>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlanoPreenchido))]
    private DateTime? _retornoSugeridoEm;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlanoPreenchido))]
    private string? _retornoSugeridoNota;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlanoPreenchido))]
    private string? _encaminhamento;

    /// <summary>
    /// Os sinais vitais que a ENFERMAGEM aferiu no dia desta sessão — leitura, nunca coleta.
    ///
    /// A clínica disse que todo paciente passa pela enfermagem: a PA e a temperatura são
    /// colhidas minutos antes, e até a parcela 76 quem prescreve escrevia a sessão sem elas
    /// na frente. Colher AQUI daria dois lugares para gravar a mesma aferição.
    ///
    /// ⚠️ Três estados, e os três são escritos: aferido (com a procedência ao lado), NÃO
    /// aferido naquele dia, e não foi possível conferir. Deixar em branco quando a leitura
    /// falha faria "ninguém mediu" e "o banco não respondeu" ficarem idênticos — e num campo
    /// de sinais vitais essa confusão é do tipo que muda conduta.
    /// </summary>
    [ObservableProperty] private string _sinaisVitaisTexto = string.Empty;

    /// <summary>"às 09:12, por Joana Técnica (COREN-SP 999999)". Vazio quando não há aferição.</summary>
    [ObservableProperty] private string _sinaisVitaisProcedencia = string.Empty;

    /// <summary>Há aferição de verdade — o que separa o número do recado de que não há número.</summary>
    [ObservableProperty] private bool _sinaisVitaisAferidos;

    /// <summary>
    /// O roteiro da sessão que se repete (parcela 63) — a MESMA janela da Recepção, no
    /// shell. Era aqui que ela fazia mais falta: quem escreve dez evoluções por dia é
    /// quem atende, e a sessão de acupuntura tem sempre a mesma forma.
    ///
    /// Aplicar COPIA e não grava: o Salvar desta tela continua sendo o que efetiva.
    /// </summary>
    [RelayCommand]
    private void AbrirModelos()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "usar modelos de evolução");

        var vm = new ModelosEvolucaoViewModel(
            _escopos,
            SessaoUsuario.Atual.ProfissionalId,
            new ModeloAplicado(
                QueixaPrincipal, Conduta, TextoEvolucao, Orientacoes,
                HistoriaDoencaAtual, ExameFisico, HipoteseDiagnostica,
                CidSessao, PlanoTerapeutico));

        var janela = new ModelosEvolucaoWindow(vm)
        {
            Owner = JanelaDona.Atual()
        };

        if (janela.ShowDialog() != true || janela.Escolhido is not { } m) return;

        // Preenche o que falta, nunca zera: um modelo com conduta e orientações não pode
        // apagar a queixa que o profissional acabou de digitar ouvindo o paciente.
        if (!string.IsNullOrWhiteSpace(m.QueixaPrincipal)) QueixaPrincipal = m.QueixaPrincipal;
        if (!string.IsNullOrWhiteSpace(m.Conduta)) Conduta = m.Conduta;
        if (!string.IsNullOrWhiteSpace(m.TextoEvolucao)) TextoEvolucao = m.TextoEvolucao;
        if (!string.IsNullOrWhiteSpace(m.Orientacoes)) Orientacoes = m.Orientacoes;

        // Esta tela edita os NOVE campos, então ela aplica os nove — é o que faz o roteiro
        // valer a pena depois das parcelas 73 e 75. Mesma regra do bloco acima: preenche o
        // que está vazio, nunca zera o que o profissional acabou de escrever.
        if (!string.IsNullOrWhiteSpace(m.HistoriaDoencaAtual)) HistoriaDoencaAtual = m.HistoriaDoencaAtual;
        if (!string.IsNullOrWhiteSpace(m.ExameFisico)) ExameFisico = m.ExameFisico;
        if (!string.IsNullOrWhiteSpace(m.HipoteseDiagnostica)) HipoteseDiagnostica = m.HipoteseDiagnostica;
        if (!string.IsNullOrWhiteSpace(m.CidSessao)) CidSessao = m.CidSessao;
        if (!string.IsNullOrWhiteSpace(m.PlanoTerapeutico)) PlanoTerapeutico = m.PlanoTerapeutico;
    }

    /// <summary>
    /// A FICHA DO ATENDIMENTO — o papel que o paciente leva embora (parcela 78).
    ///
    /// O buraco
    /// --------
    /// O relatório de evolução existe desde a parcela 3, com a marca da clínica, numeração
    /// por ano, código de conferência e assinatura ICP-Brasil — e os DOIS únicos chamadores
    /// dele estavam na RECEPÇÃO. Quem acabou de escrever a sessão não tinha por onde
    /// imprimi-la: precisava pedir ao balcão. É o defeito recorrente do projeto na variante
    /// "a porta está no módulo de quem não usa".
    ///
    /// ⚠️ E os dois chamadores emitem o histórico INTEIRO — sem período. Para entregar "o
    /// atendimento de hoje", um paciente de quarenta sessões recebia quarenta. Aqui o
    /// recorte é a DATA DESTA SESSÃO, que é o que a pergunta pede.
    ///
    /// Emitir é um FATO: o papel é numerado, fica na lista do paciente e não se apaga —
    /// cancela-se com motivo. Por isso a ficha sai depois de SALVAR, e a tela avisa quando
    /// há texto não gravado: imprimir o que ainda não está no prontuário entregaria ao
    /// paciente uma versão que o prontuário não tem.
    /// </summary>
    [RelayCommand]
    private async Task ImprimirFichaAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "imprimir a ficha do atendimento");

        if (PacienteId == 0) return;

        // ⚠️ A pergunta é `TemAlgoParaGravar`, NUNCA `!SessaoEmBranco` — confundir as duas
        // é o defeito da parcela 74: a sessão de acupuntura mais comum da casa (EVA 8→3,
        // seis pontos no mapa, nenhuma linha de texto) está "em branco" para efeito de
        // encerrar e tem MUITO o que gravar. Com a pergunta errada, ela sairia impressa
        // dizendo "EVA não medida" com o 8→3 na tela de quem imprimiu.
        if (TemAlgoParaGravar && EvolucaoId == 0)
        {
            Mensagem = "Salve a sessão antes de imprimir a ficha — o papel sai do que está "
                     + "gravado no prontuário, e o que você digitou ainda não está.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            var dia = DateOnly.FromDateTime(Data);

            byte[] pdf;
            string numero;
            using (var scope = _escopos.CreateScope())
            {
                var servicos = scope.ServiceProvider;

                var documento = await servicos.GetRequiredService<DocumentoClinicoService>()
                    .EmitirRelatorioEvolucaoAsync(
                        PacienteId, SessaoUsuario.Atual.ProfissionalId,
                        inicio: dia, fim: dia,
                        operador: SessaoUsuario.Atual.Operador);

                numero = documento.Numero;

                pdf = await servicos.GetRequiredService<DocumentosClinicosPdfService>()
                    .GerarAsync(
                        documento.Id,
                        await servicos.GetRequiredService<ParametrosService>()
                            .ObterPrestadorAsync());
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                pdf, ImpressaoPdf.NomeSeguro($"Ficha-do-atendimento-{numero.Replace('/', '-')}.pdf"));

            Mensagem = erro ?? $"Ficha {numero} emitida — ela fica na lista de documentos do paciente.";
            MensagemEhErro = erro is not null;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — ficha do atendimento não pôde ser emitida", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>De onde veio a sessão: chamada do dia, ou escolhida na busca.</summary>
    [ObservableProperty] private string _origem = string.Empty;

    [ObservableProperty] private string _resumoDor = string.Empty;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Metade VISÍVEL da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditarProntuario => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    public bool TemPaciente => !SemPaciente;

    /// <summary>
    /// Sem paciente na sala não há para quem emitir, e o botão diz isso apagado — a tela
    /// abre pela sidebar sem ninguém em foco, e botão aceso que não faz nada faz quem
    /// clica concluir que o sistema quebrou (parcela 41).
    /// </summary>
    public bool PodeEmitirDocumento => TemPaciente && PodeEditarProntuario;

    /// <summary>
    /// A ficha do atendimento pede <see cref="Permissao.VerProntuario"/> — ela IMPRIME o
    /// prontuário, não o escreve. O botão apagado tem de explicar as DUAS pré-condições
    /// que a guarda impede: sem isso, quem não tem o bit clica e leva a recusa depois.
    /// </summary>
    public bool PodeImprimirFicha =>
        TemPaciente && SessaoUsuario.Atual.Pode(Permissao.VerProntuario);

    partial void OnSemPacienteChanged(bool value)
    {
        OnPropertyChanged(nameof(TemPaciente));
        OnPropertyChanged(nameof(PodeEmitirDocumento));
        OnPropertyChanged(nameof(PodeImprimirFicha));
    }

    /// <summary>Valores da escala, para os dois seletores de dor.</summary>
    public IReadOnlyList<int> EscalaEva { get; } =
        Enumerable.Range(Evolucao.EvaMinima, Evolucao.EvaMaxima - Evolucao.EvaMinima + 1).ToList();

    /// <summary>
    /// ENFERMAGEM E INFUSÕES na coluna direita, em modo COMPACTO (parcela 72).
    ///
    /// Quem está escrevendo a conduta precisa saber o que a sala aferiu e o que foi
    /// administrado — e até aqui isso morava noutro módulo, no app de quem executa.
    /// Compacto porque a coluna tem ~350 px de altura útil: são três linhas por seção,
    /// escolhidas pelo chip, não duas listas rolando dentro de um vão.
    /// </summary>
    public LinhaDoTempoClinicaViewModel LinhaDoTempo { get; }

    public AtendimentoViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, PacienteEmFoco foco)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _foco = foco;

        LinhaDoTempo = new LinhaDoTempoClinicaViewModel(escopos)
        {
            Compacto = true,
            MostrarDocumentos = false,
            SecoesVisiveis =
            [
                Clinica.Domain.Prontuario.NaturezaRegistroClinico.EvolucaoEnfermagem,
                Clinica.Domain.Prontuario.NaturezaRegistroClinico.PrescricaoInterna
            ],
            SecaoInicial = Clinica.Domain.Prontuario.NaturezaRegistroClinico.EvolucaoEnfermagem
        };

        // O paciente do posto: quem veio da agenda já chega escolhido, e o profissional
        // não redigita o nome que acabou de clicar.
        if (_foco.Definido)
        {
            Paciente = _foco.Nome;
            SemPaciente = false;
            Origem = DescreverOrigem(_foco.AgendamentoId, _foco.DataDoHorario);
            _ = CarregarAsync();
        }
    }

    /// <summary>
    /// De onde a sessão veio — e é diferença que muda o registro, não decoração: chamada
    /// da agenda, a evolução nasce ligada ao horário e sai da lista de pendências do
    /// consultório; escolhida na busca, não.
    ///
    /// ⚠️ O cabeçalho dizia "da agenda de HOJE" para QUALQUER horário — e a dívida de
    /// prontuário e a Minha semana abrem horários de outros dias. Quem escrevia a
    /// evolução atrasada de terça lia "hoje" no topo, e a frase mentia sobre a data a
    /// que o registro ia ficar ligado.
    /// </summary>
    public static string DescreverOrigem(int? agendamentoId, DateOnly? dataDoHorario)
    {
        if (agendamentoId is null)
            return "Escolhido na busca — a evolução não fica ligada a nenhum horário.";

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        return dataDoHorario is { } data && data != hoje
            ? $"Chamado da agenda de {data:dd/MM/yyyy} — a evolução nasce ligada a esse horário."
            : "Chamado da agenda de hoje — a evolução nasce ligada a este horário.";
    }

    private int PacienteId => _foco.PacienteId ?? 0;

    /// <summary>Último paciente cujo acesso já foi registrado nesta tela (parcela 52).</summary>
    private int _acessoRegistradoDe;

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): a troca de paciente no posto
    /// dispara nova carga, e a resposta atrasada do paciente anterior chegando por último
    /// preencheria o formulário — e o MAPA — com a sessão dele sob o nome do novo, que é
    /// o pior defeito possível num prontuário. Quem começou primeiro perde.
    /// </summary>
    private int _geracaoCarga;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        if (PacienteId == 0)
        {
            SemPaciente = true;
            await LinhaDoTempo.CarregarAsync(0);
            return;
        }

        // O componente tem contador de geração próprio e filtro de acesso por natureza.
        _ = LinhaDoTempo.CarregarAsync(PacienteId);

        try
        {
            SemPaciente = false;
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;
            Anteriores.Clear();

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();

            // A trilha de LEITURA (parcela 52), registrada na troca de paciente.
            if (_acessoRegistradoDe != PacienteId)
            {
                _acessoRegistradoDe = PacienteId;
                await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(PacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.Atendimento);
            }

            var sessoes = await prontuario.DoPacienteAsync(PacienteId);

            // Chegou tarde: o posto já está em outro paciente.
            if (geracao != _geracaoCarga) return;

            // A sessão do horário chamado, quando ela já foi escrita: abrir o atendimento
            // de novo tem de CONTINUAR o registro, nunca criar um segundo para a mesma
            // sessão — dois registros do mesmo atendimento é o defeito que faz a clínica
            // desconfiar do prontuário inteiro.
            // MESMA definição que o cartão do Meu dia usa para dizer "já escrita"
            // (ConsultorioService.EvolucaoDoHorario), inclusive o caminho de baixo por
            // paciente + data. Sem ele, a evolução escrita pela RECEPÇÃO — que não conhece
            // o agendamento — não seria encontrada aqui: o cartão diria "Ver registro", o
            // formulário abriria EM BRANCO e o Salvar criaria uma SEGUNDA evolução do
            // mesmo atendimento, que é o defeito que faz a clínica desconfiar do
            // prontuário inteiro.
            Evolucao? doHorario = null;
            if (_foco.AgendamentoId is { } agendamentoId)
            {
                var diaDoHorario = _foco.DataDoHorario ?? DateOnly.FromDateTime(DateTime.Today);

                // As sessões IRMÃS do dia entram na conta: com duas sessões do mesmo
                // paciente no mesmo dia, a avulsa pertence a UMA delas — sem a lista,
                // abrir a segunda continuaria o texto da primeira.
                var doDia = await scope.ServiceProvider
                    .GetRequiredService<ConsultorioService>()
                    .SessoesDoPacienteNoDiaAsync(PacienteId, diaDoHorario);
                if (geracao != _geracaoCarga) return;

                doHorario = ConsultorioService.EvolucaoDoHorario(
                    sessoes, agendamentoId, PacienteId, diaDoHorario, doDia);
            }

            if (doHorario is not null) Preencher(doHorario);
            else Limpar(_foco.DataDoHorario);

            foreach (var e in sessoes.Where(e => e.Id != EvolucaoId).Take(SessoesAnterioresVisiveis))
                Anteriores.Add(ResumoSessaoAnterior.De(e));

            await CarregarAlertasAsync(scope.ServiceProvider, geracao);
            if (geracao != _geracaoCarga) return;

            // A data é a da SESSÃO que está na tela, nunca hoje: a dívida de prontuário e a
            // Minha semana abrem horários de dias passados, e a aferição de hoje ao lado da
            // sessão de terça diria que aquela PA foi medida nesta consulta.
            await CarregarSinaisVitaisAsync(
                scope.ServiceProvider,
                doHorario?.Data ?? _foco.DataDoHorario ?? DateOnly.FromDateTime(DateTime.Today),
                geracao);
            if (geracao != _geracaoCarga) return;

            // O mapa vem depois de resolvida a evolução do horário: ele precisa saber
            // se está editando uma sessão já escrita (e então carrega os pontos dela) ou
            // começando uma nova.
            var mapa = new MapaCorporalViewModel(
                _escopos, PacienteId, EvolucaoId == 0 ? null : EvolucaoId);
            await mapa.CarregarAsync();
            if (geracao != _geracaoCarga) return;
            Mapa = mapa;

            var dor = await prontuario.EvolucaoDaDorAsync(PacienteId);
            if (geracao != _geracaoCarga) return;
            ResumoDor = dor.SessoesComMedida == 0
                ? "Nenhuma sessão com o par EVA (antes e depois) ainda."
                : $"Começou em {dor.DorInicial}/10 e está em {dor.DorAtual}/10 — "
                  + $"{dor.SessoesComMedida} sessão(ões) medidas, alívio médio de "
                  + $"{dor.AlivioMedioPorSessao:0.#} por sessão.";
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar("Consultório — atendimento não pôde ser carregado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    /// <summary>
    /// Lê os sinais vitais da enfermagem para o dia desta sessão.
    ///
    /// Falha SOZINHA, pela mesma razão dos alertas: quem está na sala é o paciente, e uma
    /// leitura de enfermagem que não respondeu não pode impedir a consulta de abrir. Mas
    /// também não passa calada — vira o terceiro estado na tela, e vai para o log.
    /// </summary>
    private async Task CarregarSinaisVitaisAsync(
        IServiceProvider provedor, DateOnly dataDaSessao, int geracao)
    {
        try
        {
            var vitais = await provedor.GetRequiredService<ConsultorioService>()
                .SinaisVitaisDaSessaoAsync(PacienteId, dataDaSessao);

            if (geracao != _geracaoCarga) return;

            if (vitais is null)
            {
                SinaisVitaisAferidos = false;
                SinaisVitaisProcedencia = string.Empty;
                SinaisVitaisTexto = "Sem aferição da enfermagem neste dia.";
                return;
            }

            SinaisVitaisAferidos = true;
            SinaisVitaisTexto = vitais.Resumo;
            SinaisVitaisProcedencia = vitais.Procedencia;
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            SinaisVitaisAferidos = false;
            SinaisVitaisProcedencia = string.Empty;
            SinaisVitaisTexto = "Não foi possível conferir os sinais vitais da enfermagem.";
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — sinais vitais da enfermagem não puderam ser lidos", ex);
        }
    }

    /// <summary>
    /// Os alertas do paciente. Falham SOZINHOS: o atendimento não pode deixar de abrir
    /// porque a leitura administrativa quebrou — quem está na sala é o paciente, e a
    /// sessão acontece de qualquer forma.
    /// </summary>
    private async Task CarregarAlertasAsync(IServiceProvider servicos, int geracao)
    {
        // As listas são montadas LOCALMENTE e só publicadas no fim, sob a guarda de
        // geração. Entre o `Clear()` e o último `Add` não pode haver await (a lição da
        // parcela 62): com três leituras no meio, trocar de paciente enquanto a primeira
        // está no ar intercalava a alergia de um com a carteirinha do outro — e a lista
        // de alergia é a que menos pode falar do paciente errado.
        var clinicos = new List<LinhaAlertaClinico>();
        var administrativos = new List<LinhaAlertaClinico>();
        SituacaoTermo? pendente = null;

        // O prontuário falha SEPARADO do administrativo: uma consulta quebrada não pode
        // apagar a outra lista, e "sem alergia registrada" nunca pode ser o que a tela diz
        // quando na verdade não conseguiu ler.
        try
        {
            var problemas = servicos.GetRequiredService<ProblemaPacienteService>();

            foreach (var p in await problemas.AlertasAsync(PacienteId))
                clinicos.Add(new LinhaAlertaClinico
                {
                    Texto = p.Natureza == NaturezaProblema.Alergia
                        ? $"ALERGIA — {p.Rotulo}"
                              + (string.IsNullOrWhiteSpace(p.Observacoes)
                                  ? string.Empty : $": {p.Observacoes}")
                        : $"Uso contínuo — {p.Rotulo}"
                              + (string.IsNullOrWhiteSpace(p.Observacoes)
                                  ? string.Empty : $": {p.Observacoes}"),
                    // Alergia é vermelha; uso contínuo é amarelo. A urgência viaja com
                    // cada linha, como no ElegibilidadeService: pintar as duas da cor da
                    // pior faria a interação medicamentosa parecer contraindicação.
                    Grave = p.Natureza == NaturezaProblema.Alergia
                });
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — lista de problemas do paciente não pôde ser lida", ex);

            clinicos.Add(new LinhaAlertaClinico
            {
                Texto = "Não foi possível ler a lista de problemas deste paciente — ela "
                        + "está vazia por falha de leitura, não porque não haja alergia "
                        + "registrada.",
                Grave = false
            });
        }

        // ⚠️ A INTERCORRÊNCIA DA ENFERMAGEM chega aqui (parcela 72), na lista que JÁ existe
        // — zero pixel novo. O comentário da própria entidade afirmava, desde que ela
        // nasceu, que a marca "viaja para a tela de atendimento do médico"; ela não
        // viajava, e o estrago não era erro, era AUSÊNCIA — indistinguível de "não houve
        // intercorrência".
        //
        // ⚠️ Com JANELA DE 48 HORAS, e isso decide a utilidade da lista: alergia é ESTADO,
        // intercorrência é EVENTO DATADO, e a marca é `bool` — não há como descartá-la.
        // Sem janela, seis meses depois o paciente crônico teria uma náusea de março e um
        // extravasamento de abril acima da alergia real, e é assim que se ensina alguém a
        // fechar o alerta sem ler.
        try
        {
            var enfermagem = servicos.GetRequiredService<EvolucaoEnfermagemService>();
            var recentes = await enfermagem.DoPacienteAsync(PacienteId, limite: 30);

            // Retificada não alerta: quem corrigiu já disse que o registro anterior estava
            // errado, e a correção entra sozinha se ela também marcar intercorrência.
            var substituidas = recentes
                .Where(e => e.RetificaEvolucaoId is not null)
                .Select(e => e.RetificaEvolucaoId!.Value)
                .ToHashSet();

            var agora = DateTime.Now;
            foreach (var e in recentes
                         .Where(e => e.AlertaAgora(agora) && !substituidas.Contains(e.Id))
                         .OrderByDescending(e => e.Momento))
                clinicos.Add(new LinhaAlertaClinico
                {
                    Texto = $"INTERCORRÊNCIA na enfermagem em {e.Data:dd/MM} às "
                            + $"{e.Hora:HH\\:mm} — {e.Texto}"
                            + (string.IsNullOrWhiteSpace(e.AutorNome)
                                ? string.Empty : $" ({e.AutorNome})"),
                    // Vermelha, como a alergia: é o que aconteceu com este paciente há
                    // menos de dois dias, e quem vai atender agora precisa saber antes de
                    // decidir a conduta.
                    Grave = true
                });
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — intercorrências de enfermagem não puderam ser lidas", ex);

            clinicos.Add(new LinhaAlertaClinico
            {
                Texto = "Não foi possível ler as intercorrências de enfermagem deste "
                        + "paciente — a ausência delas aqui é falha de leitura, não "
                        + "garantia de que não houve nenhuma.",
                Grave = false
            });
        }

        try
        {
            var elegibilidade = servicos.GetRequiredService<ElegibilidadeService>();
            var resposta = await elegibilidade.ConferirAsync(
                PacienteId, DateOnly.FromDateTime(Data));

            // A urgência viaja COM cada alerta, e não num sinalizador da tela inteira:
            // carteirinha vencida (vermelho) e dívida do paciente (amarelo) chegam juntas
            // com frequência, e pintar as duas da cor da pior faria a segunda parecer
            // impedimento — que é justamente o que a parcela 27 decidiu que ela não é.
            foreach (var a in resposta.Alertas)
                administrativos.Add(new LinhaAlertaClinico
                {
                    Texto = a.Descricao,
                    Grave = a.Urgencia == NivelUrgencia.Vermelho
                });

            // A PORTA do termo (parcela 66, 2ª rodada). O alerta acima já dizia "falta o
            // termo assinado" com o paciente na sala — e não havia botão, menu nem item de
            // sidebar neste app para colher: o médico teria de descer ao balcão. Alerta sem
            // porta no mesmo app é pior que alerta nenhum, porque ensina a ignorá-lo
            // (parcela 48). O bit `ColherAssinaturaPaciente` já vai para os perfis
            // Profissional e Enfermagem desde a 66 — o modelo de permissão previu esta
            // porta antes de ela existir.
            var termos = servicos.GetRequiredService<TermoProcedimentoService>();
            var situacoes = await termos.SituacaoDoDiaAsync(
                PacienteId, DateOnly.FromDateTime(Data));

            pendente = situacoes.FirstOrDefault(s => s.Pendente);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — alertas do paciente não puderam ser lidos", ex);

            // Terceiro estado: a lista vazia por falha não pode se parecer com "nada a
            // avisar". A frase entra na própria lista, que é onde o profissional olha.
            administrativos.Add(new LinhaAlertaClinico
            {
                Texto = "Não foi possível conferir carteirinha, cota e pendências deste "
                        + "paciente — a lista está vazia por falha de leitura, não porque "
                        + "não haja nada.",
                Grave = false
            });
        }

        // Chegou tarde: o posto já está em outro paciente. Publicar aqui seria mostrar a
        // alergia de quem saiu ao lado do nome de quem entrou.
        if (geracao != _geracaoCarga) return;

        AlertasClinicos.Clear();
        foreach (var a in clinicos) AlertasClinicos.Add(a);

        Alertas.Clear();
        foreach (var a in administrativos) Alertas.Add(a);

        TermoPendente = pendente;
        OnPropertyChanged(nameof(TemTermoPendente));
        OnPropertyChanged(nameof(PodeColherTermo));
    }

    private void Preencher(Evolucao e)
    {
        EvolucaoId = e.Id;
        Data = e.Data.ToDateTime(TimeOnly.MinValue);
        EvaAntes = e.EvaAntes;
        EvaDepois = e.EvaDepois;
        QueixaPrincipal = e.QueixaPrincipal;
        HistoriaDoencaAtual = e.HistoriaDoencaAtual;
        ExameFisico = e.ExameFisico;
        HipoteseDiagnostica = e.HipoteseDiagnostica;
        CidSessao = e.CidSessao;
        Conduta = e.Conduta;
        TextoEvolucao = e.TextoEvolucao;
        Orientacoes = e.Orientacoes;
        PlanoTerapeutico = e.PlanoTerapeutico;
        RetornoSugeridoEm = e.RetornoSugeridoEm?.ToDateTime(TimeOnly.MinValue);
        RetornoSugeridoNota = e.RetornoSugeridoNota;
        Encaminhamento = e.Encaminhamento;

        // ⚠️ Nada mais precisa ser "aberto": as quatro abas mostram sozinhas quais têm
        // conteúdo, pelo ponto no rótulo. O Expander recolhido escondia metade do registro
        // clínico de quem voltava para reler — e a pessoa concluía que ele se perdeu.
    }

    /// <summary>
    /// Zera o formulário para uma sessão que ainda não tem evolução escrita.
    ///
    /// ⚠️ A DATA é a do HORÁRIO, não a de hoje — e é aqui que morava o defeito que
    /// quebrava o laço central do Consultório. O módulo existe para responder "o que eu
    /// atendi e ainda não escrevi"; escrever a evolução da sessão de terça gravava-a com a
    /// data de HOJE, e daí saíam dois estragos:
    ///
    /// 1. **O prontuário passa a dizer que a sessão foi hoje.** É registro clínico com a
    ///    data errada — a Lei 13.787/2018 pede o contrário disso, e o erro é
    ///    irrecuperável depois, porque nada guarda qual era a data verdadeira.
    /// 2. **A dívida nunca saía da lista.** `RegistrosPendentesAsync` lê as evoluções da
    ///    janela que termina ONTEM (a sessão de hoje não é cobrada, o paciente ainda está
    ///    na sala) — então a evolução datada de hoje ficava FORA do conjunto consultado, o
    ///    casamento não a encontrava, e a linha continuava lá. O médico salvava, lia
    ///    "Sessão registrada no prontuário", via a pendência de pé e escrevia de novo.
    ///
    /// Nulo — o caminho de quem entrou pela busca, sem horário em foco — continua sendo
    /// hoje, que é o melhor palpite disponível.
    /// </summary>
    private void Limpar(DateOnly? dataDoHorario = null)
    {
        EvolucaoId = 0;
        Data = dataDoHorario?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
        EvaAntes = null;
        EvaDepois = null;
        QueixaPrincipal = null;
        HistoriaDoencaAtual = null;
        ExameFisico = null;
        HipoteseDiagnostica = null;
        CidSessao = null;
        Conduta = null;
        TextoEvolucao = null;
        Orientacoes = null;
        PlanoTerapeutico = null;
        RetornoSugeridoEm = null;
        RetornoSugeridoNota = null;
        Encaminhamento = null;
    }

    /// <summary>
    /// Traz a conduta da última sessão para o formulário — sem gravar nada.
    ///
    /// É a mesma regra de "repetir a sessão anterior" do mapa corporal: o botão TRAZ para
    /// a tela, e só o Salvar efetiva. Tratamento de acupuntura repete protocolo por
    /// semanas, e redigitar a mesma conduta é como o registro vira "idem".
    /// </summary>
    [RelayCommand]
    private void RepetirUltima()
    {
        var ultima = Anteriores.FirstOrDefault();
        if (ultima is null)
        {
            Mensagem = "Não há sessão anterior para repetir.";
            MensagemEhErro = true;
            return;
        }

        if (ultima.Conduta != "—") Conduta = ultima.Conduta;
        if (ultima.Queixa != "—" && string.IsNullOrWhiteSpace(QueixaPrincipal))
            QueixaPrincipal = ultima.Queixa;

        Mensagem = $"Conduta da sessão de {ultima.Data} trazida para a tela. "
                   + "Nada foi gravado — confira e salve.";
        MensagemEhErro = false;
    }

    [RelayCommand]
    private Task SalvarAsync() => TentarSalvarAsync();

    /// <summary>
    /// Grava a sessão e DIZ se conseguiu.
    ///
    /// ⚠️ O <c>bool</c> existe por causa do "Finalizar atendimento" (parcela 74): encerrar
    /// é salvar a sessão E carimbar o fim, e a ORDEM entre os dois não é estilo — se a
    /// gravação falhar, o carimbo não pode acontecer, senão o balcão recebe o recado de
    /// que o médico terminou enquanto a evolução do paciente não existe em lugar nenhum.
    /// É a hierarquia da parcela 65 aplicada aqui: o fato que a clínica não pode perder
    /// vem primeiro, e o que veio depois nunca o desfaz.
    ///
    /// O comando continua existindo e continua sendo o Salvar de sempre — quem chama este
    /// método é só quem precisa saber o desfecho.
    /// </summary>
    public async Task<bool> TentarSalvarAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            if (PacienteId == 0)
                throw new InvalidOperationException("Escolha o paciente antes de escrever a sessão.");

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();

            var salva = await prontuario.SalvarAsync(new Evolucao
            {
                Id = EvolucaoId,
                PacienteId = PacienteId,
                ProfissionalId = SessaoUsuario.Atual.ProfissionalId,
                // O vínculo com o horário é o que faz a sessão sair da lista de
                // pendências do consultório depois de escrita.
                AgendamentoId = _foco.AgendamentoId,
                AtendimentoId = _foco.AtendimentoId,
                Data = DateOnly.FromDateTime(Data),
                EvaAntes = EvaAntes,
                EvaDepois = EvaDepois,
                QueixaPrincipal = QueixaPrincipal,
                HistoriaDoencaAtual = HistoriaDoencaAtual,
                ExameFisico = ExameFisico,
                HipoteseDiagnostica = HipoteseDiagnostica,
                CidSessao = CidSessao,
                Conduta = Conduta,
                TextoEvolucao = TextoEvolucao,
                Orientacoes = Orientacoes,
                PlanoTerapeutico = PlanoTerapeutico,
                RetornoSugeridoEm = RetornoSugeridoEm is { } r
                    ? DateOnly.FromDateTime(r)
                    : null,
                RetornoSugeridoNota = RetornoSugeridoNota,
                Encaminhamento = Encaminhamento
            }, SessaoUsuario.Atual.Operador);

            EvolucaoId = salva.Id;

            // O mapa é 1:1 com a evolução e só se grava DEPOIS dela: ele precisa do id da
            // sessão, e antes de a sessão existir não há a que pertencer. Os pontos
            // trazidos por "repetir" ou por protocolo viram prontuário só aqui — até este
            // ponto eram tela, e prontuário não é rascunho.
            if (Mapa is not null) await Mapa.SalvarAsync(salva.Id);

            _snackbar.Sucesso("Sessão registrada no prontuário.");

            // O aviso do par incompleto vem DEPOIS de gravar, e não impede: o "depois" é
            // medido ao fim do atendimento, e recusar a gravação por causa dele faria o
            // profissional escrever tudo de novo — ou desistir de medir.
            Mensagem = EvaAntes is not null && EvaDepois is null
                ? "Gravado. A EVA está só com a medida ANTES — sem o par não dá para dizer "
                  + "se a sessão aliviou. Volte aqui ao terminar para registrar o depois."
                : null;
            MensagemEhErro = false;

            await CarregarAsync();
            return true;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Consultório — sessão não pôde ser salva", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
            return false;
        }
    }

    /// <summary>
    /// A sessão está EM BRANCO — nada do que o profissional escreveria foi escrito.
    ///
    /// Serve ao "Finalizar atendimento": encerrar sem registro é legítimo (o médico pode
    /// escrever depois) e é exatamente a dívida que este app existe para cobrar, então a
    /// tela PERGUNTA em vez de impedir ou de calar. A EVA e o mapa não entram: eles são
    /// medida, não registro do que aconteceu.
    /// </summary>
    public bool SessaoEmBranco
        => string.IsNullOrWhiteSpace(QueixaPrincipal)
           && string.IsNullOrWhiteSpace(Conduta)
           && string.IsNullOrWhiteSpace(TextoEvolucao)
           && string.IsNullOrWhiteSpace(HistoriaDoencaAtual)
           && string.IsNullOrWhiteSpace(ExameFisico)
           && string.IsNullOrWhiteSpace(HipoteseDiagnostica)
           && string.IsNullOrWhiteSpace(Orientacoes)
           // ⚠️ O PLANO conta (parcela 75). Esquecê-lo aqui fazia o "Finalizar atendimento"
           // DESCARTAR em silêncio a sessão em que o profissional só ajustou o plano — o
           // mesmo defeito que esta parcela corrigiu horas antes, recometido por acrescentar
           // um campo e não reler quem lê a lista.
           && string.IsNullOrWhiteSpace(PlanoTerapeutico)
           // ⚠️ A MESMA lista do `temTexto` do serviço: a sessão que só decide o
           // retorno é uma sessão, e sem estas três linhas o "Finalizar" a descartaria
           // em SILÊNCIO — o defeito que a parcela 76 corrigiu com o plano.
           && RetornoSugeridoEm is null
           && string.IsNullOrWhiteSpace(RetornoSugeridoNota)
           && string.IsNullOrWhiteSpace(Encaminhamento);

    /// <summary>
    /// Há ALGUMA COISA a gravar — texto, EVA, CID ou pontos no mapa.
    ///
    /// ⚠️ É uma pergunta DIFERENTE de <see cref="SessaoEmBranco"/>, e confundir as duas foi
    /// um defeito real (parcela 74, 2ª rodada): aquela decide se a tela PERGUNTA "encerrar
    /// sem escrever a evolução?", e deixa a EVA e o mapa de fora com razão, porque eles são
    /// MEDIDA e não o registro do que aconteceu. Usá-la também para decidir se GRAVA fazia a
    /// sessão de acupuntura mais comum da casa — EVA antes 8, depois 3, seis pontos no mapa e
    /// nenhuma linha de texto — ser encerrada com <b>tudo descartado em silêncio</b>.
    ///
    /// O serviço aceita a sessão só com EVA desde sempre; era a tela que não a mandava.
    /// </summary>
    public bool TemAlgoParaGravar
        => !SessaoEmBranco
           || EvaAntes is not null
           || EvaDepois is not null
           || !string.IsNullOrWhiteSpace(CidSessao)
           || Mapa?.Pontos.Count > 0;

    /// <summary>
    /// Abre o mapa corporal em JANELA (parcela 37, rodada de leiaute).
    ///
    /// Ele morava numa aba de 530 px ao lado do formulário, e não cabia: as duas figuras
    /// são Canvas de 220×460 que NÃO esticam — é o que faz o clique virar fração — então
    /// sobrava barra de rolagem e os botões do rodapé saíam cortados pela borda da tela.
    /// A Recepção já abre o mapa numa janela de 960 de mínimo, pelo mesmo motivo.
    ///
    /// A janela NÃO grava. O mapa é 1:1 com a evolução e só se efetiva depois que a sessão
    /// existe — quem o grava continua sendo o Salvar daqui, com o id da evolução na mão.
    /// </summary>
    [RelayCommand]
    private void AbrirMapa()
    {
        // O botão apagado (TemPaciente) explica; esta guarda diz por quê quando o clique
        // chega mesmo assim — guarda que volta em silêncio é botão que não faz nada.
        if (Mapa is null)
        {
            Mensagem = "Escolha um paciente antes de abrir o mapa corporal: a tela abre "
                     + "no paciente que você está atendendo, ou use a busca.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            new MapaCorporalWindow(Mapa, $"Mapa corporal — {Paciente}")
            {
                Owner = JanelaDona.Atual()
            }.ShowDialog();

            // O resumo do rodapé muda com o que foi marcado lá dentro.
            OnPropertyChanged(nameof(Mapa));
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — mapa corporal não pôde ser aberto", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Emite receita, atestado, declaração de comparecimento ou pedido de exame para o
    /// paciente que está na sala.
    ///
    /// Abre a MESMA janela da Recepção — promovida ao shell na parcela 36, pelo mesmo
    /// motivo do mapa corporal. Quem prescreve é quem atende, e um app de consultório
    /// que não emite receita obriga o médico a pedir à recepcionista que digite o que
    /// ele acabou de decidir. O serviço por trás (<c>DocumentoClinicoService</c>) já
    /// exige o profissional que assina em receita, atestado e pedido de exame — é a única
    /// regra do projeto que IMPEDE em vez de avisar, e ela continua valendo daqui.
    /// </summary>
    [RelayCommand]
    private async Task EmitirDocumentoAsync()
    {
        if (PacienteId == 0)
        {
            Mensagem = "Escolha um paciente antes de emitir o documento.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "emitir documento clínico");

            var vm = new DocumentoEdicaoViewModel(_escopos, PacienteId);
            var janela = new DocumentoWindow(vm)
            {
                Owner = JanelaDona.Atual()
            };

            if (janela.ShowDialog() != true) return;

            _snackbar.Sucesso("Documento emitido e numerado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — documento não pôde ser emitido", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

}
