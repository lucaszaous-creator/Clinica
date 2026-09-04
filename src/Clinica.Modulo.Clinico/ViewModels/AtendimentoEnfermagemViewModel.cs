using Clinica.Application;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>
/// O ATENDIMENTO DE ENFERMAGEM — a seção onde quem executa escreve, dentro da tela do
/// paciente (parcela 88).
///
/// O pedido, e o que ele revelou
/// -----------------------------
/// A clínica pediu duas coisas: <i>"os enfermeiros podem ver TODOS os pacientes e clicar
/// em ATENDER, em vez de ver só os pacientes dele"</i> e <i>"quando clicado em atender,
/// seções de texto livre para escrever sobre o atendimento e campos de evolução"</i>.
///
/// ⚠️ A primeira metade tinha um mecanismo, e ele é do tipo que este projeto documenta há
/// oitenta parcelas: <b>a enfermagem não tem agenda própria</b>. Os horários pertencem a
/// quem consulta; a técnica passa por todos eles. Como "Meu dia" e "Meus pacientes"
/// filtram por <c>ProfissionalId</c>, vincular a enfermeira a um <c>Profissional</c> —
/// que é OBRIGATÓRIO desde a parcela 72, porque sem ele não há COREN para assinar o
/// registro — fazia as duas telas dela abrirem VAZIAS. Ou seja: ela ficava sem carteira
/// exatamente por estar cadastrada certo, e tela vazia se lê como sistema quebrado.
///
/// A segunda metade era a mais visível: ela escrevia numa <b>janela modal</b> de altura
/// fixa, enquanto quem consulta tem uma tela inteira com cabeçalho, cronômetro, sub-abas
/// e o prontuário ao lado. Dois desenhos para o mesmo ato no mesmo sistema é o que faz
/// alguém achar que abriu outro programa (a reprovação da parcela 47).
///
/// O desenho: X · Y · XY, aplicado à ESCRITA
/// -----------------------------------------
/// A parcela 72 fixou a frase que decide: <b>XY é a LEITURA; X e Y são as ESCRITAS</b>.
/// Então o paciente é UM (o mesmo crachá, o mesmo rail, as mesmas seções de leitura) e a
/// seção de escrita é que muda:
/// <list type="bullet">
///   <item><b>Atendimento</b> — a sessão de quem consulta, em S-O-A-P.</item>
///   <item><b>Atendimento de enfermagem</b> — esta, nas cinco etapas da COFEN 358/2009.</item>
/// </list>
/// As duas ficam VISÍVEIS para quem lê o prontuário, e é de propósito: quem infunde
/// precisa da conduta da consulta de hoje, e quem consulta precisa da pressão que a
/// técnica aferiu vinte minutos antes. Quem escreve em cada uma é que é separado, e o
/// separador é o bit — não a tela.
///
/// ⚠️ O COMPOSITOR NÃO FOI REESCRITO
/// ---------------------------------
/// <see cref="Passagem"/> é o MESMO <see cref="EvolucaoEnfermagemViewModel"/> da janela da
/// sala de infusão e da ficha da Recepção. Todas as regras caras moram lá — a hora
/// INFORMADA, a hora futura recusada, a retificação que preserva a data do fato, o
/// processo de enfermagem que a correção não pode descartar, a alergia que entra na lista
/// de problemas no mesmo <c>SaveChanges</c>. Reescrevê-las aqui daria uma segunda
/// definição do registro clínico mais delicado do sistema, e a segunda é sempre a que
/// ninguém lembra de ajustar.
///
/// O que esta ViewModel acrescenta é o que é da TELA: o plano de cuidados do dia (a etapa
/// 4, que só se marca com o paciente na frente), as duas portas do dia — a folha de
/// infusão e o termo pendente —, a leitura da conduta médica ao lado, e a trilha de acesso.
/// </summary>
public sealed partial class AtendimentoEnfermagemViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly IDialogoService _dialogo;
    private readonly PacienteEmFoco _foco;

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 60). A seção recarrega a cada registro
    /// gravado e a cada porta aberta; num banco remoto a leitura VELHA pode responder por
    /// último e desenhar a folha de outro dia.
    /// </summary>
    private int _geracaoCarga;

    /// <summary>
    /// Último paciente cujo acesso já entrou na trilha (parcela 52). Sem esta guarda o
    /// registro sairia também nas recargas que rodam DEPOIS de escrever, e a janela de
    /// silêncio de 30 min só cobriria a duplicata depois de ir ao banco perguntar.
    /// </summary>
    private int _acessoRegistradoDe;

    /// <summary>
    /// O COMPOSITOR e a lista de passagens — o mesmo da janela da sala. Ver o ⚠️ acima.
    /// </summary>
    public EvolucaoEnfermagemViewModel Passagem { get; }

    /// <summary>O plano de cuidados de HOJE — a etapa 4, no componente compartilhado.</summary>
    public PlanoDeCuidadosViewModel Plano { get; }

    /// <summary>
    /// A CONDUTA que veio do outro lado — a sessão médica e a folha de infusão do paciente.
    ///
    /// ⚠️ É a imagem espelhada do que a tela do médico faz: lá o painel compacto mostra a
    /// enfermagem e a infusão; aqui mostra a sessão médica e a infusão. É a mesma regra
    /// nas duas pontas — <i>infundir sem saber a conduta da consulta de hoje é executar às
    /// cegas</i> —, e é ela que faz o prontuário se ler como um só.
    /// </summary>
    public LinhaDoTempoClinicaViewModel LinhaDoTempo { get; }

    /// <summary>
    /// O nome do paciente aberto. Ele NÃO é desenhado aqui — o crachá da tela do paciente
    /// já o mostra, uma vez, acima das seções —, e é lido em C# pelas duas portas que
    /// precisam nomeá-lo (a coleta do termo e a ficha do atendimento).
    /// </summary>
    [ObservableProperty] private string _paciente = string.Empty;

    /// <summary>
    /// Há aviso a mostrar. ⚠️ Existe para o gatilho da região não comparar um binding com
    /// <c>{x:Null}</c>: condição de <c>MultiDataTrigger</c> é resolvida em RUNTIME, e uma
    /// que não case deixa a superfície visível e VAZIA — sem erro, sem log, e só na tela
    /// montada (a categoria de defeito da parcela 50).
    /// </summary>
    public bool TemAviso => AvisoDeLeitura is not null || PortasNaoVerificadas;

    partial void OnPortasNaoVerificadasChanged(bool value)
        => OnPropertyChanged(nameof(TemAviso));

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    // ==================== As duas portas do dia ====================
    //
    // ⚠️ Elas existem aqui porque "Atender" passou a ser o caminho da enfermeira até o
    // paciente, e o painel antigo (o da tela Enfermagem, que continua servindo à
    // Recepção) tinha as duas. Feature que some numa reforma de leiaute é capacidade
    // tirada de quem a usava ontem — a regra 3 do bloco do faturamento.

    /// <summary>Termo pendente do dia: o rótulo do botão, e o que o torna existente.</summary>
    [ObservableProperty] private string? _termoPendente;
    private int? _modeloTermoPendente;
    private int? _documentoTermoPendente;

    /// <summary>A folha de infusão deste paciente HOJE, quando há uma.</summary>
    [ObservableProperty] private string? _folhaDeHoje;
    private int _folhaDeHojeId;

    /// <summary>
    /// A leitura das duas portas FALHOU — o terceiro estado. "Não há termo pendente" e
    /// "não consegui conferir" dizem coisas opostas para quem vai preparar a sala.
    /// </summary>
    [ObservableProperty] private bool _portasNaoVerificadas;

    // ==================== Permissões ====================

    /// <summary>Metade visível; a que impede é o <c>Exigir</c> dentro de cada comando.</summary>
    public bool PodeRegistrar =>
        SessaoUsuario.Atual.Pode(Permissao.RegistrarEvolucaoEnfermagem);

    public bool PodeVerProntuario => SessaoUsuario.Atual.Pode(Permissao.VerProntuario);

    public bool PodeColherTermo =>
        SessaoUsuario.Atual.Pode(Permissao.ColherAssinaturaPaciente);

    public bool PodeAbrirFolha => SessaoUsuario.Atual.Pode(Permissao.ChecarPrescricao);

    /// <summary>
    /// A seção é do lado Y, e quem não escreve por ele vê a passagem em modo LEITURA — o
    /// que é legítimo e é o XY: o médico precisa ler a pressão que a técnica aferiu.
    /// A frase existe para a tela DIZER isso, em vez de mostrar campos que não gravam.
    /// </summary>
    public string? AvisoDeLeitura => PodeRegistrar
        ? null
        : "Você está lendo o registro da enfermagem. Escrever aqui exige a permissão "
          + "“Registrar evolução de enfermagem”, que a direção concede em Acessos.";

    public AtendimentoEnfermagemViewModel(
        IServiceScopeFactory escopos, IDialogoService dialogo, PacienteEmFoco foco)
    {
        _escopos = escopos;
        _dialogo = dialogo;
        _foco = foco;

        // ⚠️ O compositor nasce AMARRADO ao horário quando a pessoa veio da agenda
        // (`AgendamentoId`): é esse vínculo que põe a passagem na ficha DESTA sessão. Sem
        // paciente ele nasce inerte — o workspace constrói as nove seções antes de saber
        // quem é, e ir ao banco perguntar pelo paciente zero é uma ida a mais por
        // navegação, num banco remoto.
        Passagem = new EvolucaoEnfermagemViewModel(
            escopos, dialogo,
            pacienteId: foco.PacienteId ?? 0,
            paciente: foco.Nome,
            agendamentoId: foco.AgendamentoId);

        Plano = new PlanoDeCuidadosViewModel(escopos, dialogo);

        LinhaDoTempo = new LinhaDoTempoClinicaViewModel(escopos)
        {
            Compacto = true,
            MostrarDocumentos = false,
            SecoesVisiveis =
            [
                NaturezaRegistroClinico.SessaoMedica,
                NaturezaRegistroClinico.PrescricaoInterna
            ],
            SecaoInicial = NaturezaRegistroClinico.SessaoMedica
        };

        // ⚠️ UMA superfície de mensagem, e ela é da SEÇÃO. Os componentes escrevem na
        // propriedade deles; quem a mostra é quem os hospeda — duas caixas de mensagem na
        // mesma tela são duas respostas para a mesma pergunta, e a de baixo aparece longe
        // do clique (a lição da parcela 79).
        Plano.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not (nameof(PlanoDeCuidadosViewModel.Mensagem)
                                       or nameof(PlanoDeCuidadosViewModel.MensagemEhErro))
                || Plano.Mensagem is null) return;

            Mensagem = Plano.Mensagem;
            MensagemEhErro = Plano.MensagemEhErro;
        };

        // O compositor pela mesma razão: ele escreve "Registrado no prontuário do
        // paciente" e as recusas de hora e de permissão, e essa frase precisa aparecer AO
        // LADO do botão que a produziu.
        Passagem.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not (nameof(EvolucaoEnfermagemViewModel.Mensagem)
                                       or nameof(EvolucaoEnfermagemViewModel.MensagemEhErro))
                || Passagem.Mensagem is null) return;

            Mensagem = Passagem.Mensagem;
            MensagemEhErro = Passagem.MensagemEhErro;
        };

        if (_foco.Definido)
        {
            Paciente = _foco.Nome;
            _ = CarregarAsync();
        }
    }

    private int PacienteId => _foco.PacienteId ?? 0;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        if (PacienteId == 0)
        {
            await LinhaDoTempo.CarregarAsync(0);
            await Plano.CarregarAsync(0);
            return;
        }

        // ⚠️ Dado de saúde: nem ler nem desenhar sem `VerProntuario`, e a seção DIZ por
        // quê em vez de devolver lista vazia — que se lê como "este paciente nunca passou
        // pela enfermagem".
        if (!PodeVerProntuario)
        {
            await LinhaDoTempo.CarregarAsync(0);
            await Plano.CarregarAsync(0);
            Mensagem = "O seu acesso não permite ler o prontuário. Peça em Acessos a "
                     + "permissão “Ver prontuário”.";
            MensagemEhErro = true;
            return;
        }

        var geracao = ++_geracaoCarga;
        var pacienteId = PacienteId;

        // Os dois componentes têm contador de geração próprio.
        _ = LinhaDoTempo.CarregarAsync(pacienteId);
        _ = Plano.CarregarAsync(pacienteId);

        try
        {
            using var escopo = _escopos.CreateScope();
            var servicos = escopo.ServiceProvider;

            // ⚠️ A trilha de LEITURA na TROCA de paciente, nunca a cada carga — esta roda
            // também depois de gravar. A origem é `SalaInfusao`, e não `ProntuarioClinico`:
            // a janela de silêncio é POR ORIGEM, e gravar a origem errada FUNDE o acesso da
            // enfermagem com o de quem abriu o prontuário clínico de verdade, apagando
            // exatamente a distinção que uma investigação procura.
            if (_acessoRegistradoDe != pacienteId)
            {
                _acessoRegistradoDe = pacienteId;
                await servicos.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(pacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.SalaInfusao);
            }

            if (geracao != _geracaoCarga) return;

            await CarregarPortasAsync(servicos, pacienteId, geracao);
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;
            Diagnostico.Registrar(
                "Consultório — atendimento de enfermagem não pôde ser carregado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// As duas portas do dia: a folha de infusão e o termo pendente.
    ///
    /// ⚠️ Falha aqui NÃO derruba a seção — o assunto dela é a passagem, e banco lento não
    /// pode impedir alguém de registrar o que observou. Mas também não passa calada: vai
    /// para o log e a tela mostra o terceiro estado. Falha exibida como sucesso é o que faz
    /// a clínica acreditar que não há termo pendente quando há.
    /// </summary>
    private async Task CarregarPortasAsync(
        IServiceProvider servicos, int pacienteId, int geracao)
    {
        try
        {
            PortasNaoVerificadas = false;
            var hoje = DateOnly.FromDateTime(DateTime.Today);

            var termos = await servicos.GetRequiredService<TermoProcedimentoService>()
                .SituacaoDoDiaAsync(pacienteId, hoje);
            if (geracao != _geracaoCarga) return;

            if (termos.FirstOrDefault(t => t.Pendente) is { } pendente)
            {
                TermoPendente = $"Colher: {pendente.NomeDoTermo}";
                _modeloTermoPendente = pendente.ModeloId;
                _documentoTermoPendente = pendente.DocumentoId;
            }
            else
            {
                TermoPendente = null;
                _modeloTermoPendente = null;
                _documentoTermoPendente = null;
            }

            var folhas = await servicos.GetRequiredService<ChecagemPrescricaoService>()
                .DoDiaAsync(hoje, incluirEncerradas: true);
            if (geracao != _geracaoCarga) return;

            if (folhas.FirstOrDefault(f => f.PacienteId == pacienteId) is { } folha)
            {
                _folhaDeHojeId = folha.Id;
                FolhaDeHoje = $"Folha {folha.Numero} · {folha.Pendentes} item(ns)";
            }
            else
            {
                _folhaDeHojeId = 0;
                FolhaDeHoje = null;
            }
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;
            PortasNaoVerificadas = true;
            Diagnostico.Registrar(
                "Consultório — folha e termo do dia não puderam ser conferidos", ex);
        }
    }

    /// <summary>
    /// Colhe o termo do procedimento do dia. O botão só EXISTE quando há termo pendente:
    /// botão que não existe não gasta tela para dizer que não há nada.
    /// </summary>
    [RelayCommand]
    private async Task ColherTermoAsync()
    {
        if (PacienteId == 0 || _modeloTermoPendente is null) return;

        try
        {
            // Dentro do try: `Exigir` LANÇA, e fora dele a recusa sobe até a rede do
            // Dispatcher em vez de virar a frase que a tela já sabe mostrar.
            SessaoUsuario.Atual.Exigir(
                Permissao.ColherAssinaturaPaciente, "colher a assinatura do paciente");

            ColetaDeTermo.Abrir(
                _escopos, PacienteId, Paciente,
                _modeloTermoPendente, _documentoTermoPendente);
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Consultório — termo não pôde ser colhido", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
            return;
        }

        // Recarrega de qualquer forma: abrir a janela já EMITE o termo numerado.
        await CarregarAsync();
    }

    /// <summary>Abre a folha de infusão de hoje deste paciente — a ponte com a sala.</summary>
    [RelayCommand]
    private async Task AbrirFolhaAsync()
    {
        if (_folhaDeHojeId == 0) return;

        try
        {
            SessaoUsuario.Atual.Exigir(
                Permissao.ChecarPrescricao, "abrir a folha de execução");

            var vm = new FolhaExecucaoViewModel(_escopos, _dialogo, _folhaDeHojeId);
            new FolhaExecucaoWindow(vm) { Owner = JanelaDona.Atual() }.ShowDialog();
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Consultório — folha de execução não pôde ser aberta", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
            return;
        }

        await CarregarAsync();
    }

    /// <summary>
    /// A FICHA DO ATENDIMENTO da enfermagem — o papel que o paciente leva embora
    /// (parcela 78), recortado em HOJE, pelo PONTO ÚNICO do shell (set/2026).
    ///
    /// ⚠️ A guarda do RASCUNHO chegou com a extração, e ela faltava aqui: esta tela TEM
    /// compositor, e sem a pergunta a técnica escrevia a passagem, clicava em imprimir e
    /// entregava ao paciente um papel sem o que ela acabara de escrever. A cópia do médico
    /// tinha a guarda; esta, não — a divergência que a extração existe para acabar.
    ///
    /// O recorte é HOJE, e não a data do horário em foco, porque é com `hoje` que
    /// `EvolucaoEnfermagemService.RegistrarAsync` grava a passagem nova: recortar noutro
    /// dia devolveria "não há registro para relatar" sobre o que acabou de ser escrito.
    /// </summary>
    [RelayCommand]
    private async Task ImprimirFichaAsync()
    {
        var r = await FichaDoAtendimento.EmitirAsync(
            _escopos, PacienteId, DateOnly.FromDateTime(DateTime.Today),
            temRascunhoNaoGravado: !PassagemEmBranco,
            contextoDoLog: "Consultório — enfermagem");

        Mensagem = r.Frase;
        MensagemEhErro = r.EhErro;
    }

    /// <summary>
    /// Há passagem escrita para gravar — a pergunta que o FINALIZAR do posto faz antes de
    /// perguntar se a sessão está em branco.
    ///
    /// ⚠️ Ela existe porque o encerramento do atendimento olhava SÓ a sessão médica: a
    /// técnica que escreveu uma consulta de enfermagem inteira e clicou em Finalizar
    /// ouvia <i>"você não escreveu nada desta sessão"</i>. Perguntar sobre o registro que
    /// a pessoa acabou de digitar é o jeito mais rápido de ensinar alguém a fechar diálogo
    /// sem ler (a causa raiz do incidente da parcela 65).
    /// </summary>
    public bool PassagemEmBranco => Passagem.CompositorEmBranco;
}
