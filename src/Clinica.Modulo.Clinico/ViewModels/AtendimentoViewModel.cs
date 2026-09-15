using System.Collections.ObjectModel;
using System.ComponentModel;
using Clinica.Application.Abstracoes;
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

/// <summary>
/// Um papel que já saiu para este paciente NO DIA desta sessão — a lista curta da coluna
/// ENTREGAR AGORA, que responde "eu já dei a receita para ela?".
/// </summary>
public sealed record FolhaDeHoje(
    int DocumentoId, string Rotulo, string Numero, string Detalhe, bool Cancelado)
{
    /// <summary>O nome do arquivo da segunda via.</summary>
    public string NomeArquivo => $"{Rotulo}-{Numero.Replace('/', '-')}.pdf";

    public static FolhaDeHoje De(DocumentoClinico d) => new(
        d.Id,
        CentralDocumentosService.RotularClinico(d.Tipo),
        d.Numero,
        // Cancelada aparece MARCADA, nunca sumindo: documento não se apaga neste sistema,
        // e esconder o cancelado faria a coluna mentir sobre o que já foi entregue.
        d.Cancelado
            ? $"cancelada — {d.MotivoCancelamento}"
            : $"{d.CriadoEm:HH':'mm}" + (d.Profissional is null ? "" : $" · {d.Profissional.Rotulo}"),
        d.Cancelado);
}

/// <summary>Uma sessão anterior, do jeito que o consultório precisa relê-la: inteira.</summary>

/// <summary>
/// A tela do ATENDIMENTO — onde a sessão é escrita enquanto o paciente ainda está na sala.
///
/// Por que não é a janela de evolução da recepção
/// ----------------------------------------------
/// A recepção escreve evolução de vez em quando, num diálogo modal aberto de dentro do
/// prontuário. O profissional escreve TODA sessão, e enquanto conversa com alguém. São
/// dois usos diferentes do mesmo dado, e a diferença aparece no leiaute: aqui as sessões
/// passadas têm uma ABA inteira (set/2026 — eram uma faixa de 320 px ao lado do
/// formulário), porque a primeira coisa que se faz ao receber um paciente de tratamento é
/// reler o que foi feito da última vez. Numa janela modal isso não cabe — e a arquitetura
/// da suíte não permitiria reaproveitá-la de outro módulo de qualquer forma (nenhum módulo
/// conhece os outros).
///
/// A EVA em par
/// ------------
/// Antes e depois, sempre. É a regra que o projeto inteiro aplica: uma medida solta não
/// diz se a sessão funcionou, e o campo "depois" é preenchido no fim do atendimento — por
/// isso a tela permite salvar com só o "antes" e volta a cobrar o par no resumo.
/// </summary>
public sealed partial class AtendimentoViewModel : FolhaDaSessaoViewModel
{
    public sealed record RegistroParaVincular(int Id, string Rotulo);
    public ObservableCollection<RegistroParaVincular> RegistrosParaVincular { get; } = [];
    [ObservableProperty] private RegistroParaVincular? _registroSelecionadoParaVincular;
    public bool TemRegistrosParaVincular => RegistrosParaVincular.Count > 0;

    [RelayCommand]
    private async Task VincularRegistroAsync()
    {
        if (RegistroSelecionadoParaVincular is not { } registro || _foco.AgendamentoId is not { } horario) return;
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "vincular o registro à sessão");
            if (TemAlgoParaGravar)
                throw new InvalidOperationException("Salve o que escreveu antes de vincular outro registro. Nenhum texto será descartado.");
            using var scope = _escopos.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ProntuarioService>()
                .VincularAoHorarioAsync(registro.Id, horario, SessaoUsuario.Atual.Operador);
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    private readonly PacienteEmFoco _foco;

    /// <summary>
    /// Quantas sessões anteriores a aba "Sessões anteriores" traz.
    ///
    /// ⚠️ Eram TRÊS, e o número era a altura da coluna de 350 px, não uma decisão clínica.
    /// Com a aba (set/2026) a altura é a da tela, e cinco é o que se lê antes de escrever a
    /// de hoje. Não é "todas" de propósito: o prontuário inteiro — com busca no texto, os
    /// anexos e as correções — é a seção Histórico, e duas telas respondendo à mesma
    /// pergunta é o que faz alguém procurar a diferença que não existe.
    /// </summary>
    private const int SessoesAnterioresVisiveis = 5;

    /// <summary>
    /// A ÚLTIMA sessão em uma linha, na folha de hoje: data, par da EVA e retorno sugerido.
    ///
    /// ⚠️ É o que sobrou da coluna aberta quando ela virou ABA (set/2026). Reler a sessão
    /// passada passou a custar um clique; o que não pode custar clique nenhum é a resposta
    /// para "por que este paciente está aqui hoje". A composição mora na Application, onde
    /// o <c>dotnet test</c> alcança (a regra da <c>GradeSemana</c>).
    /// </summary>
    [ObservableProperty] private string _contextoDaUltimaSessao = string.Empty;

    /// <summary>
    /// A ÚNICA linha acima da folha (set/2026): a última sessão e o que a enfermagem
    /// aferiu hoje, montadas por <see cref="ContextoDaFolha"/>. Eram quatro linhas.
    /// </summary>
    public string LinhaDeContexto => ContextoDaFolha.Montar(
        ContextoDaUltimaSessao,
        SinaisVitaisAferidos ? SinaisVitaisTexto : null,
        SinaisVitaisProcedencia,
        _sinaisVitaisNaoConferidos);

    /// <summary>A leitura dos sinais vitais falhou — o terceiro estado, que a linha escreve.</summary>
    private bool _sinaisVitaisNaoConferidos;

    partial void OnContextoDaUltimaSessaoChanged(string value) => OnPropertyChanged(nameof(LinhaDeContexto));
    partial void OnSinaisVitaisTextoChanged(string value) => OnPropertyChanged(nameof(LinhaDeContexto));
    partial void OnSinaisVitaisProcedenciaChanged(string value) => OnPropertyChanged(nameof(LinhaDeContexto));
    partial void OnSinaisVitaisAferidosChanged(bool value) => OnPropertyChanged(nameof(LinhaDeContexto));

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
    /// O que o botão da faixa diz (set/2026). Ele era LITERAL no XAML — "Termo não
    /// assinado · colher" — e essa frase é falsa sobre quem já assinou pelo celular,
    /// no app de quem vai fazer o procedimento.
    /// </summary>
    public string TermoPendenteRotulo
        => TermoAConferir ? "Termo assinado · conferir" : "Termo não assinado · colher";

    /// <summary>
    /// O termo pendente da faixa é um que o paciente JÁ ASSINOU pelo celular (set/2026).
    ///
    /// ⚠️ É o que dá o PESO ao botão, e não só a frase: o vermelho é de quem não assinou
    /// nada — nas outras quatro portas este estado é âmbar, e deixar o Consultório gastando
    /// o vermelho num caso que se resolve com UM clique ensina a ignorá-lo no caso em que
    /// ele impede o procedimento.
    /// </summary>
    public bool TermoAConferir => TermoPendente?.AssinaturaRemotaAguardaConferencia == true;

    /// <summary>A dica da faixa — muda com o rótulo, pela mesma razão.</summary>
    public string TermoPendenteDica
        => TermoAConferir
            ? "O paciente já assinou este termo pelo celular. Abra, confira o documento "
              + "dele e conclua — não é preciso colher de novo."
            : "O procedimento de hoje exige termo assinado pelo paciente, e ele ainda não "
              + "assinou. Colha agora — ele está aqui.";

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
            // A SESSÃO vai junto (set/2026): aqui o horário está ABERTO na tela, então
            // não há o que perguntar — o termo nasce ligado a ele. O caminho de baixo é a
            // situação do dia, para o caso de a tela ter sido aberta sem horário (a
            // dívida de prontuário e a Minha semana abrem sessões de outros dias).
            var concluiu = await Clinica.Desktop.Shell.Componentes.ColetaDeTermo.AbrirAsync(
                _escopos, PacienteId, Paciente,
                TermoPendente?.ModeloId, TermoPendente?.DocumentoId,
                TermoPendente?.ProfissionalId,
                AgendamentoDaSessao ?? TermoPendente?.AgendamentoId);

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
        // O PONTO ÚNICO (set/2026). Tudo o que era igual nas três telas que emitem a
        // ficha mora agora no shell; o que continua sendo desta tela é o que só ela sabe:
        // a DATA da sessão que está aberta e se há RASCUNHO não gravado.
        //
        // ⚠️ A pergunta é `TemAlgoParaGravar`, NUNCA `!SessaoEmBranco` — confundir as duas
        // é o defeito da parcela 74: a sessão de acupuntura mais comum da casa (EVA 8→3,
        // seis pontos no mapa, nenhuma linha de texto) está "em branco" para efeito de
        // encerrar e tem MUITO o que gravar. Com a pergunta errada, ela sairia impressa
        // dizendo "EVA não medida" com o 8→3 na tela de quem imprimiu.
        var r = await FichaDoAtendimento.EmitirAsync(
            _escopos, PacienteId, DateOnly.FromDateTime(Data),
            temRascunhoNaoGravado: TemAlgoParaGravar && EvolucaoId == 0,
            contextoDoLog: "Consultório");

        Mensagem = r.Frase;
        MensagemEhErro = r.EhErro;
    }

    // ===================================================================================
    //  ENTREGAR AGORA — a coluna dos papéis (set/2026, tela 1 do mockup aprovado)
    // ===================================================================================
    //
    //  O que ela corrige: emitir era "sair do atendimento, achar a tela de documentos,
    //  escolher o paciente de novo e voltar" — para um ato que acontece com o paciente
    //  ainda na maca. E o documento abria EM BRANCO na frente de quem tinha acabado de
    //  escrever o CID, a hipótese e a hora em que aquela pessoa chegou.
    //
    //  ⚠️ A coluna é PERMANENTE, e isso contraria a régua do README ("o que a pessoa FAZ
    //  de vez em quando é botão, não painel aberto") de propósito: aqui o papel não é o
    //  que se faz de vez em quando, é o que a consulta ENTREGA — e a tela do paciente roda
    //  em modo IMERSIVO (a sidebar e a barra de cima recolhem), então em 1366 sobram ~1000
    //  px para a folha depois dos 340 dela.
    //
    //  ⚠️ Quem decide o que cada cartão DIZ é `FolhasParaEntregar`, na Application: "CID
    //  M54.5 da sua hipótese" é uma promessa que a janela tem de cumprir, e regra que
    //  decide o que a tela afirma mora onde o `dotnet test` alcança.

    /// <summary>Os papéis que esta sessão entrega, com o que cada um já traz preenchido.</summary>
    public ObservableCollection<FolhaParaEntregar> Entregar { get; } = [];

    /// <summary>O que já saiu para este paciente NO DIA desta sessão.</summary>
    public ObservableCollection<FolhaDeHoje> SaiuHoje { get; } = [];

    /// <summary>
    /// A região "já saiu hoje" tem o que mostrar.
    ///
    /// Existe como propriedade, e não como um conversor de contagem, porque a região
    /// inteira SOME enquanto nada saiu: cartão vazio dizendo "nenhum documento hoje"
    /// ocuparia 60 px da coluna para não informar nada.
    /// </summary>
    public bool TemFolhasDeHoje => SaiuHoje.Count > 0;

    /// <summary>Quando a sessão é — só para a janela escrever a procedência.</summary>
    private DateTime? _dataHoraDaSessao;

    /// <summary>Os carimbos da fila deste horário, que a declaração de comparecimento usa.</summary>
    private TimeOnly? _chegadaDaSessao;
    private TimeOnly? _saidaDaSessao;

    /// <summary>Quantas sessões o prontuário tem — o relatório de evolução precisa de base.</summary>
    private int _sessoesRegistradas;

    /// <summary>
    /// A última herança que gerou os cartões. Existe para NÃO remontar a coluna a cada
    /// tecla da hipótese: cinco records iguais reescritos em toda letra fazem a coluna
    /// piscar ao lado de quem está escrevendo.
    /// </summary>
    private HerancaDaSessao? _herancaMontada;

    /// <summary>O que a sessão tem AGORA para dar aos papéis.</summary>
    private HerancaDaSessao HerancaDeAgora() => new(
        PacienteId,
        Cid: CidSessao,
        Hipotese: HipoteseDiagnostica,
        Chegada: _chegadaDaSessao,
        Saida: _saidaDaSessao,
        SessoesRegistradas: _sessoesRegistradas,
        AgendamentoId: _foco.AgendamentoId,
        // Enquanto a sessão não foi salva não há evolução para apontar — e apontar para
        // uma que não existe é um vínculo para lugar nenhum.
        EvolucaoId: EvolucaoId == 0 ? null : EvolucaoId,
        DataHoraDaSessao: _dataHoraDaSessao);

    public bool PodePrescreverInfusao => SessaoUsuario.Atual.Pode(Permissao.Prescrever);

    [RelayCommand]
    private async Task PrescreverInfusaoAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.Prescrever, "prescrever infusão");
            if (!TemPaciente) throw new InvalidOperationException("Escolha um paciente antes de prescrever.");
            using var scope = _escopos.CreateScope();
            var dialogo = scope.ServiceProvider.GetRequiredService<IDialogoService>();
            var vm = new PrescricaoInternaEdicaoViewModel(
                _escopos, dialogo, PacienteId, Paciente, SessaoUsuario.Atual.ProfissionalId,
                _foco.AgendamentoId, evolucaoId: EvolucaoId == 0 ? null : EvolucaoId);
            new PrescricaoInternaWindow(vm) { Owner = JanelaDona.Atual() }.ShowDialog();
            // Recarrega somente a leitura da enfermagem. O texto em edição fica intacto.
            await LinhaDoTempo.CarregarAsync(PacienteId);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Infusão no atendimento", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Refaz a coluna — só quando o que ela DIZ mudou.
    ///
    /// A comparação é do record inteiro (igualdade estrutural): o CID que o profissional
    /// acabou de escrever muda o cartão do atestado, e o resto continua igual.
    /// </summary>
    private void MontarEntregar()
    {
        if (!TemPaciente)
        {
            _herancaMontada = null;
            Entregar.Clear();
            return;
        }

        var heranca = HerancaDeAgora();
        if (heranca == _herancaMontada) return;
        _herancaMontada = heranca;

        Entregar.Clear();
        foreach (var cartao in FolhasParaEntregar.Montar(heranca, SessaoUsuario.Atual.Efetivas))
            Entregar.Add(cartao);
    }

    /// <summary>
    /// Emite o papel do cartão clicado.
    ///
    /// Os quatro escritos abrem a JANELA QUE JÁ EXISTE, agora com a herança — ela resolve
    /// quem assina, os modelos, a regra do CID e a conferência de alergia. O relatório é
    /// MONTADO do prontuário e não passa por janela nenhuma: emitir é imprimir o que já
    /// está lá.
    /// </summary>
    [RelayCommand]
    private async Task EntregarFolhaAsync(FolhaParaEntregar? cartao)
    {
        // Guarda que FALA: o cartão já nasce apagado quando não dá, e um atalho ou uma
        // corrida de carregamento chegam aqui mesmo assim (parcela 41).
        if (cartao is null) return;
        if (PacienteId == 0)
        {
            Mensagem = "Escolha um paciente antes de emitir.";
            MensagemEhErro = true;
            return;
        }

        var folha = CentralDocumentosService.Folha(cartao.Chave);
        if (folha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(
                folha.PermissaoEmitir, $"emitir {folha.Rotulo.ToLowerInvariant()}");

            if (folha.MontadaDoProntuario) await EmitirMontadaAsync(folha);
            else await AbrirJanelaDeEmissaoAsync(folha);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                $"Consultório — folha '{cartao.Rotulo}' não pôde ser emitida", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    private async Task AbrirJanelaDeEmissaoAsync(FolhaCatalogo folha)
    {
        if (folha.TipoClinico is not { } tipo) return;

        var vm = new DocumentoEdicaoViewModel(_escopos, PacienteId, tipo, HerancaDeAgora());
        var janela = new DocumentoWindow(vm) { Owner = JanelaDona.Atual() };

        // Recarrega dos dois jeitos: fechar sem concluir não quer dizer que nada
        // aconteceu — o documento pode ter sido emitido e só a impressão ter falhado.
        var concluiu = janela.ShowDialog() == true;
        await CarregarSaiuHojeAsync();

        if (concluiu) _snackbar?.Sucesso($"{folha.Rotulo} emitido(a).");
    }

    /// <summary>
    /// O relatório de evolução: o sistema monta do prontuário e imprime. Não há janela —
    /// não há o que digitar.
    /// </summary>
    private async Task EmitirMontadaAsync(FolhaCatalogo folha)
    {
        DocumentoClinico emitido;
        using (var scope = _escopos.CreateScope())
        {
            var servico = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();
            var operador = SessaoUsuario.Atual.Operador;

            emitido = folha.TipoClinico == TipoDocumentoClinico.Anamnese
                ? await servico.EmitirAnamneseAsync(PacienteId, operador: operador)
                : await servico.EmitirRelatorioEvolucaoAsync(PacienteId, operador: operador);
        }

        await CarregarSaiuHojeAsync();
        await ImprimirDocumentoAsync(
            emitido.Id, $"{folha.Rotulo}-{emitido.Numero.Replace('/', '-')}.pdf");
    }

    /// <summary>
    /// Segunda via do que saiu hoje. O conteúdo foi gravado na EMISSÃO e não é remontado —
    /// a via que sai agora tem de ser idêntica à que o paciente levou.
    /// </summary>
    [RelayCommand]
    private async Task SegundaViaAsync(FolhaDeHoje? linha)
    {
        if (linha is null) return;
        await ImprimirDocumentoAsync(linha.DocumentoId, linha.NomeArquivo);
    }

    private async Task ImprimirDocumentoAsync(int documentoId, string nomeArquivo)
    {
        try
        {
            byte[] pdf;
            using (var scope = _escopos.CreateScope())
            {
                var pdfs = scope.ServiceProvider.GetRequiredService<DocumentosClinicosPdfService>();
                var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
                pdf = await pdfs.GerarAsync(documentoId, await parametros.ObterPrestadorAsync());
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(pdf, ImpressaoPdf.NomeSeguro(nomeArquivo));
            Mensagem = erro;
            MensagemEhErro = erro is not null;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — segunda via não pôde ser gerada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// O que já saiu para este paciente NO DIA desta sessão.
    ///
    /// Só o dia, e não a lista inteira: a pasta clínica é a aba "Prescrições e documentos",
    /// e repetir os quarenta papéis dele numa coluna de 340 px daria duas respostas para a
    /// mesma pergunta. Aqui a pergunta é outra — "eu já dei a receita para ela?".
    ///
    /// Falha SOZINHA: quem está na sala é o paciente, e uma leitura de documentos que não
    /// respondeu não pode impedir a consulta de abrir. Mas não passa calada — vai ao log.
    /// </summary>
    private async Task CarregarSaiuHojeAsync()
    {
        var doDia = DateOnly.FromDateTime(Data);

        // ⚠️ Descarte de resposta fora de ordem (parcela 60), e aqui ele não é otimização:
        // trocar de paciente enquanto esta leitura está no ar poria os PAPÉIS de quem já
        // saiu na coluna de quem está na sala — com o botão "2ª via" apontando para o
        // documento da outra pessoa. Um clique imprimiria o papel errado.
        var doPaciente = PacienteId;

        try
        {
            using var scope = _escopos.CreateScope();
            var documentos = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();

            var todos = await documentos.DoPacienteAsync(doPaciente);

            // Chegou tarde: o posto já está em outro paciente.
            if (doPaciente != PacienteId) return;

            // Entre o Clear() e o último Add não pode haver await (parcela 62).
            var linhas = todos
                .Where(d => d.Data == doDia)
                .Select(FolhaDeHoje.De)
                .ToList();

            SaiuHoje.Clear();
            foreach (var l in linhas) SaiuHoje.Add(l);
            OnPropertyChanged(nameof(TemFolhasDeHoje));
        }
        catch (Exception ex)
        {
            if (doPaciente != PacienteId) return;

            Clinica.Application.Diagnostico.Registrar(
                "Consultório — documentos do dia não puderam ser lidos", ex);
            SaiuHoje.Clear();
            OnPropertyChanged(nameof(TemFolhasDeHoje));
        }
    }

    /// <summary>De onde veio a sessão: chamada do dia, ou escolhida na busca.</summary>
    [ObservableProperty] private string _origem = string.Empty;

    [ObservableProperty] private string _resumoDor = string.Empty;

    /// <summary>
    /// A ficha do atendimento pede <see cref="Permissao.VerProntuario"/> — ela IMPRIME o
    /// prontuário, não o escreve. O botão apagado tem de explicar as DUAS pré-condições
    /// que a guarda impede: sem isso, quem não tem o bit clica e leva a recusa depois.
    /// </summary>
    public bool PodeImprimirFicha =>
        TemPaciente && SessaoUsuario.Atual.Pode(Permissao.VerProntuario);

    /// <summary>
    /// O <c>partial</c> de <c>SemPaciente</c> é gerado na BASE (a folha), então o que a
    /// tela reavalia chega por este gancho. Sem ele, os dois botões continuariam acesos
    /// sobre uma tela sem ninguém em foco — a parcela 41 pela porta de trás.
    /// </summary>
    protected override void AoMudarPresencaDePaciente()
    {
        OnPropertyChanged(nameof(PodeImprimirFicha));

        // Sem paciente não há papel a entregar — e cinco cartões acesos sobre uma tela
        // vazia é o botão que não faz nada, com moldura.
        MontarEntregar();
    }

    /// <summary>
    /// ENFERMAGEM E INFUSÕES — a aba "Enfermagem e infusões" (parcela 72, na aba desde
    /// set/2026).
    ///
    /// Quem está escrevendo a conduta precisa saber o que a sala aferiu e o que foi
    /// administrado — e até aqui isso morava noutro módulo, no app de quem executa.
    ///
    /// ⚠️ NÃO é mais compacto: o modo compacto corta em TRÊS linhas por seção, e ele
    /// existia porque a coluna da direita tinha ~350 px de altura útil. Numa aba inteira o
    /// corte esconderia a quarta aferição da tarde sem dizer que a escondeu — e o resumo
    /// diria "3 de 12", que é a tela pedindo desculpa por um limite que não precisa mais
    /// existir.
    /// </summary>
    public LinhaDoTempoClinicaViewModel LinhaDoTempo { get; }

    public AtendimentoViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, PacienteEmFoco foco)
        : base(escopos, snackbar)
    {
        _foco = foco;

        // A coluna ENTREGAR AGORA acompanha o que está sendo ESCRITO: o cartão do
        // atestado passa a dizer "CID M54.5 da sua hipótese" no instante em que o
        // profissional escreve o CID.
        //
        // ⚠️ Por `PropertyChanged`, e não por `partial void OnCidSessaoChanged`: os
        // `partial` são gerados na classe BASE (a folha da sessão), e uma derivada não
        // pode implementá-los — a armadilha da parcela 88.
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CidSessao) or nameof(HipoteseDiagnostica)
                or nameof(EvolucaoId))
                MontarEntregar();
        };

        LinhaDoTempo = new LinhaDoTempoClinicaViewModel(escopos)
        {
            Compacto = false,
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

    /// <summary>
    /// De quem é a sessão — no Consultório, sempre o paciente do POSTO. A base não o
    /// guarda: quem sabe é o foco, e ele muda quando o profissional chama o próximo.
    /// </summary>
    /// ⚠️ O <c>set</c> é vazio de propósito: a base o expõe para a janela do Prontuário,
    /// que RECEBE o paciente por parâmetro; aqui a única verdade é o foco, e aceitar uma
    /// atribuição faria a tela responder por um paciente que o posto não está atendendo.
    public override int PacienteId
    {
        get => _foco.PacienteId ?? 0;
        protected set { }
    }

    /// <summary>O horário chamado — é o vínculo que tira a sessão de "Sessões sem evolução".</summary>
    protected override int? AgendamentoDaSessao => _foco.AgendamentoId;

    /// <summary>O atendimento que o Finalizar acabou de criar, quando já existe.</summary>
    protected override int? AtendimentoDaSessao => _foco.AtendimentoId;

    protected override string ContextoDoLog => "Consultório";

    /// <summary>Depois de gravar, a tela relê tudo — inclusive as anteriores e a curva da dor.</summary>
    protected override Task DepoisDeSalvarAsync() => CarregarAsync();

    /// <summary>
    /// A modalidade do horário chamado: é ela que decide quais campos personalizados
    /// aparecem. Sem horário (quem entrou pela busca) valem os de TODAS as modalidades.
    /// </summary>
    protected override async Task<string?> ModalidadeDaSessaoAsync(IServiceScope scope)
    {
        if (_foco.AgendamentoId is not { } agendamentoId) return null;

        return (await scope.ServiceProvider.GetRequiredService<IClinicaRepositorio>()
            .ObterAgendamentoAsync(agendamentoId))?.ModalidadeCodigo;
    }

    /// <summary>Último paciente cujo acesso já foi registrado nesta tela (parcela 52).</summary>
    private int _acessoRegistradoDe;

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): a troca de paciente no posto
    /// dispara nova carga, e a resposta atrasada do paciente anterior chegando por último
    /// preencheria o formulário — e o MAPA — com a sessão dele sob o nome do novo, que é
    /// o pior defeito possível num prontuário. Quem começou primeiro perde.
    /// </summary>
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
            RegistrosParaVincular.Clear();
            RegistroSelecionadoParaVincular = null;
            OnPropertyChanged(nameof(TemRegistrosParaVincular));
            ContextoDaUltimaSessao = string.Empty;

            // O contexto que a coluna ENTREGAR AGORA herda. Zerado ANTES da leitura: o
            // horário do paciente anterior daria à declaração de comparecimento a hora de
            // quem já saiu (a lição da parcela 89 — limpar depois do await deixa o dado
            // de outra pessoa na tela).
            _dataHoraDaSessao = null;
            _chegadaDaSessao = null;
            _saidaDaSessao = null;
            _sessoesRegistradas = 0;

            // ⚠️ E o "JÁ SAIU HOJE" pela MESMA razão. O contador de geração de
            // `CarregarSaiuHojeAsync` impede a resposta VELHA de sobrescrever a nova; ele
            // não impede a lista velha de FICAR na tela enquanto a leitura corre — e ela
            // corre depois de toda esta carga, que num banco remoto são segundos. Nesse
            // intervalo o crachá já mostra quem entrou na sala e a coluna mostra os papéis
            // de quem saiu, com o "2ª via" apontando para o documento da outra pessoa: um
            // clique imprimiria o papel errado, que é exatamente o que o comentário
            // daquele método diz existir para evitar.
            SaiuHoje.Clear();
            OnPropertyChanged(nameof(TemFolhasDeHoje));

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

            // Os campos personalizados que valem para esta sessão. SEQUENCIAL, nunca
            // WhenAll: é o mesmo DbContext do escopo (a lição da parcela 74).
            await CarregarCamposPersonalizadosAsync(scope, geracao);
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

                // Os carimbos da FILA deste horário — é deles que sai "esteve aqui 14:00
                // → 14:40" na declaração de comparecimento, em vez de a recepcionista
                // digitar de cabeça a hora em que o paciente chegou.
                if (doDia.FirstOrDefault(x => x.Id == agendamentoId) is { } horario)
                {
                    _dataHoraDaSessao = horario.DataHora;
                    _chegadaDaSessao = horario.ChegadaEm is { } chegou
                        ? TimeOnly.FromDateTime(chegou)
                        : null;

                    // A saída só existe depois de a sessão ser encerrada. Nula é a
                    // resposta certa enquanto o paciente está na sala: escrever "agora"
                    // numa declaração seria afirmar um fato que ainda não aconteceu.
                    _saidaDaSessao = horario.FimAtendimentoEm is { } saiu
                        ? TimeOnly.FromDateTime(saiu)
                        : null;
                }
            }

            if (doHorario is null && _foco.AgendamentoId is not null && _foco.DataDoHorario is { } dia)
            {
                foreach (var e in sessoes.Where(e => e.CanceladaEm is null && e.AgendamentoId is null
                    && e.AtendimentoId is null && e.Data == dia))
                    RegistrosParaVincular.Add(new RegistroParaVincular(e.Id,
                        $"Registro #{e.Id} · {e.Data:dd/MM/yyyy} · {e.Profissional?.Nome ?? "sem profissional informado"}"));
                OnPropertyChanged(nameof(TemRegistrosParaVincular));
            }

            _sessoesRegistradas = sessoes.Count;

            if (doHorario is not null) Preencher(doHorario);
            else Limpar(_foco.DataDoHorario);

            foreach (var e in sessoes.Where(e => e.Id != EvolucaoId).Take(SessoesAnterioresVisiveis))
                Anteriores.Add(ResumoSessaoAnterior.De(e));

            ContextoDaUltimaSessao = ResumoSessaoAnterior.ContextoDaUltima(Anteriores.FirstOrDefault());

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
                _escopos, PacienteId, EvolucaoId == 0 ? null : EvolucaoId, DateOnly.FromDateTime(Data));
            await mapa.CarregarAsync();
            if (geracao != _geracaoCarga) return;
            Mapa = mapa;

            // A coluna de papéis, com o que a sessão já sabe. Depois do `Preencher`, que é
            // quem põe o CID e a hipótese na tela.
            MontarEntregar();

            // Sequencial, como todo o resto desta carga: leitura composta em paralelo
            // sobre o mesmo repositório é o defeito que o SQLite dos testes esconde
            // (parcela 74), e aqui não há nada a ganhar em antecipá-la.
            await CarregarSaiuHojeAsync();
            if (geracao != _geracaoCarga) return;

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

            _sinaisVitaisNaoConferidos = false;
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

            _sinaisVitaisNaoConferidos = true;
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

            pendente = PendenciasDeTermo.Primeira(situacoes);
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
        OnPropertyChanged(nameof(TermoAConferir));
        OnPropertyChanged(nameof(TermoPendenteRotulo));
        OnPropertyChanged(nameof(TermoPendenteDica));
        OnPropertyChanged(nameof(PodeColherTermo));
    }

}
