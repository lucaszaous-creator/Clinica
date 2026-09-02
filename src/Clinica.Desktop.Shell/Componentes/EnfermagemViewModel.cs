using System.Collections.ObjectModel;
using Clinica.Application;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Medidas;
using Clinica.Domain.Prontuario;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// A TELA DA ENFERMAGEM (parcela 71; o Y da parcela 72) — onde ela acompanha e escreve.
///
/// Por que ela precisou existir
/// ----------------------------
/// A evolução de enfermagem nasceu com duas portas: a fila da sala de infusão e a ficha do
/// paciente. As duas resolvem o caso da INFUSÃO — e a clínica disse a frase que muda o
/// desenho: <b>"todo paciente precisa passar pela enfermagem"</b>. A maioria dessas
/// passagens não tem folha nenhuma (curativo, triagem, observação, pós-consulta), e a
/// enfermeira não tinha de onde alcançá-las: a sala só mostra as folhas do dia, e a ficha
/// exige saber o nome e passar pelo módulo da recepção.
///
/// ⚠️ É tela SEPARADA da sala de infusão, e isso é decisão: a sala responde <i>"o que
/// executar agora"</i>; esta responde <i>"quem eu atendi e o que escrevi"</i>. Terceira
/// pergunta, terceira tela — e juntá-las faria a fila do dia disputar espaço com a carteira
/// inteira da clínica.
///
/// A forma: LISTA → TELA DO PACIENTE
/// ---------------------------------
/// A lista tem a largura inteira e a evolução mora atrás de um clique. Grudar a lista à
/// esquerda da evolução seria a faixa lateral que o README proíbe — e que o cliente já
/// reprovou sete vezes.
///
/// ⚠️ A lista ABRE COM QUEM ESTÁ NA CLÍNICA HOJE (parcela 72). A primeira versão abria com
/// a carteira inteira em ordem de busca, o que obrigava a digitar o nome de quem o sistema
/// já sabe que ela vai receber às 9h — <i>"tela que abre vazia é tela inacabada"</i>
/// (parcela 37). A busca continua ali, como ATALHO para quem está fora do dia, e o botão
/// "Todos os pacientes" traz a carteira inteira quando é ela que se procura.
///
/// ⚠️ São DOIS modos sobre UMA coleção, e não duas listas: duas <c>ListBox</c> amarradas ao
/// mesmo <c>SelectedItem</c> se limpam mutuamente, porque a que não contém o item escolhido
/// devolve <c>null</c> pelo binding (a armadilha da parcela 37).
/// </summary>
public partial class EnfermagemViewModel : ObservableObject, ICarregarAoAbrir
{
    private readonly IServiceScopeFactory _escopos;
    private readonly IDialogoService _dialogo;

    /// <summary>Descarte de resposta fora de ordem: a lista troca de paciente a cada clique.</summary>
    private int _geracaoCarga;

    /// <summary>Idem para a montagem da lista, que muda de modo e a cada tecla da busca.</summary>
    private int _geracaoLista;

    /// <summary>
    /// Último paciente cujo acesso já entrou na trilha. Sem esta guarda o registro sairia
    /// também na recarga que roda DEPOIS de escrever — e a janela de silêncio de 30 min só
    /// cobriria a duplicata depois de ir ao banco perguntar.
    /// </summary>
    private int _acessoRegistradoDe;

    /// <summary>
    /// A busca é ATALHO, e por isso vem com <c>limite: null</c>: quando a enfermeira digita
    /// um nome, ela quer a carteira inteira, não os vinte primeiros.
    /// </summary>
    public SeletorPacienteViewModel Seletor { get; }

    /// <summary>
    /// A ÚNICA lista da tela. Ela é preenchida pela fila do dia ou pelo resultado da busca
    /// — nunca pelas duas ao mesmo tempo, e o cabeçalho diz qual das duas está no ar.
    /// </summary>
    public ObservableCollection<Paciente> Pacientes { get; } = new();

    /// <summary>
    /// O PRONTUÁRIO INTEIRO deste paciente (parcela 72) — o mesmo componente da ficha da
    /// Recepção e do Consultório, aberto no chip <b>Enfermagem</b>.
    ///
    /// ⚠️ Os outros chips MOSTRAM A CONTAGEM mesmo desmarcados, e é isso que faz a
    /// enfermeira descobrir que há 12 sessões médicas para ler. Chip pré-marcado sem
    /// número ao lado deixaria a entrega desligada justamente na tela de quem mais precisa
    /// dela: infundir sem saber a conduta da consulta de hoje é executar às cegas.
    ///
    /// Sem ações: quem corrige a evolução de enfermagem faz isso pela janela de escrita
    /// (que retifica com motivo, nunca apaga), e a sessão médica é do médico.
    /// </summary>
    public LinhaDoTempoClinicaViewModel LinhaDoTempo { get; }

    /// <summary>
    /// O PLANO DE CUIDADOS DE HOJE — a etapa 4 da COFEN (parcela 76).
    ///
    /// ⚠️ Ele saiu daqui e virou componente do shell na parcela 88, porque a seção
    /// <b>Atendimento de enfermagem</b> do módulo Clínico mostra o MESMO plano. Duas
    /// definições de "o que falta executar hoje" divergem na primeira correção, e a
    /// segunda é sempre a que ninguém lembra de ajustar — aqui isso custaria a regra do
    /// <i>se necessário</i>, que é o que impede o contador de apontar para nada.
    /// </summary>
    public PlanoDeCuidadosViewModel Plano { get; }

    [ObservableProperty] private bool _mostrandoLista = true;
    [ObservableProperty] private bool _carregandoLista;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>O que a lista está mostrando agora — a frase que evita o filtro esquecido.</summary>
    [ObservableProperty] private string _resumoDaLista = string.Empty;

    /// <summary>Modo "carteira inteira" ligado pelo chip, ou pela busca com termo.</summary>
    [ObservableProperty] private bool _mostrandoTodos;

    /// <summary>
    /// O irmão do chip acima. Os dois modos são EXCLUSIVOS, e o par existe para a régua
    /// dizer qual está no ar ANTES do clique — dois botões iguais não dizem em qual dos
    /// dois você está, e filtro esquecido respondendo "ninguém aqui" faz a clínica dar o
    /// dia por atendido (a lição da lista de espera, parcela 25).
    /// </summary>
    public bool MostrandoHoje => !MostrandoTodos;

    partial void OnMostrandoTodosChanged(bool value) => OnPropertyChanged(nameof(MostrandoHoje));

    /// <summary>
    /// A escolha da lista. UM clique abre a tela do paciente: quem escolhe alguém na
    /// carteira quer a evolução dele, não uma seleção que não faz nada.
    /// </summary>
    [ObservableProperty] private Paciente? _escolhido;

    partial void OnEscolhidoChanged(Paciente? value)
    {
        // O clique da linha e o botão "Atender" fazem A MESMA COISA. Dois resultados
        // diferentes para o mesmo alvo fariam a pessoa procurar a diferença entre eles —
        // e o botão existe porque ele NOMEIA a ação: linha que só responde ao clique não
        // anuncia o que o clique faz.
        if (value is not null) _ = AtenderAsync(value);
    }

    [ObservableProperty] private string _paciente = string.Empty;

    /// <summary>
    /// O CONTEXTO do paciente aberto, numa LINHA (parcela 72) — idade, convênio, alergia,
    /// medicação em uso, última aferição, peso.
    ///
    /// ⚠️ Linha, não faixa: contexto permanente vira moldura, e faixa permanente foi a 5ª
    /// reprovação do cliente (parcela 38). A primeira versão escrevia aqui só o nome do
    /// convênio — <b>menos</b> do que a linha da lista de trás já dizia.
    /// </summary>
    [ObservableProperty] private string _contexto = string.Empty;

    /// <summary>A alergia, separada, porque é a única que muda de cor.</summary>
    [ObservableProperty] private string? _alerta;

    /// <summary>Termo pendente do dia: o rótulo do botão, e o que o torna existente.</summary>
    [ObservableProperty] private string? _termoPendente;
    private int? _modeloTermoPendente;
    private int? _documentoTermoPendente;

    /// <summary>A folha de infusão do paciente HOJE — a ponte que faltava entre as duas telas.</summary>
    [ObservableProperty] private string? _folhaDeHoje;
    private int _folhaDeHojeId;

    private int _pacienteId;

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeRegistrar =>
        SessaoUsuario.Atual.Pode(Permissao.RegistrarEvolucaoEnfermagem);

    /// <summary>
    /// ⚠️ A LEITURA também tem barreira (parcela 72). A primeira versão desta tela conferia
    /// só o bit de ESCREVER: ela montava a carteira inteira (nome, CPF, telefone, convênio
    /// — conteúdo de <c>VerFichaPaciente</c>) e carregava a evolução de enfermagem sem
    /// perguntar por <c>VerProntuario</c> uma única vez. A ficha do paciente, no mesmo
    /// commit, fazia o contrário e certo: "nem ler nem desenhar" (art. 5º, II).
    /// </summary>
    public bool PodeVerFicha => SessaoUsuario.Atual.Pode(Permissao.VerFichaPaciente);

    public bool PodeVerProntuario => SessaoUsuario.Atual.Pode(Permissao.VerProntuario);

    /// <summary>Ela tem o bit desde a parcela 66, e as telas dela não tinham a porta.</summary>
    public bool PodeColherTermo =>
        SessaoUsuario.Atual.Pode(Permissao.ColherAssinaturaPaciente);

    public bool PodeAbrirFolha => SessaoUsuario.Atual.Pode(Permissao.ChecarPrescricao);

    /// <summary>
    /// Abre o registro escolhido na linha do tempo — só o ARQUIVO DA FICHA tem ação nesta
    /// tela, e o roteamento pela natureza é o que impede o clique de encostar no id de
    /// outra tabela. Falha alto para natureza desconhecida (a lição da ficha).
    /// </summary>
    private async Task AbrirRegistroAsync(Clinica.Application.Modelos.RegistroClinicoPaciente item)
    {
        if (item.Natureza != NaturezaRegistroClinico.ArquivoDaFicha)
            throw new NotSupportedException(
                $"A tela da Enfermagem não sabe abrir {CatalogoRegistroClinico.Rotular(item.Natureza)}.");
        try
        {
            var erro = await ArquivosDaFicha.AbrirAsync(_escopos, item.Id);
            Mensagem = erro;
            MensagemEhErro = erro is not null;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Enfermagem — arquivo da ficha não pôde ser aberto", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    public EnfermagemViewModel(IServiceScopeFactory escopos, IDialogoService dialogo)
    {
        _escopos = escopos;
        _dialogo = dialogo;

        Seletor = new SeletorPacienteViewModel(escopos, limite: null);

        LinhaDoTempo = new LinhaDoTempoClinicaViewModel(escopos)
        {
            SecaoInicial = NaturezaRegistroClinico.EvolucaoEnfermagem,
            // Os ARQUIVOS DA FICHA abrem daqui: a técnica recebe o laudo pelo WhatsApp e
            // relê o da semana passada com o paciente na frente. Abrir é LEITURA, e a
            // metade visível segue o bit de ler; cancelar fica com a ficha e o Consultório.
            NaturezasComAcao = [NaturezaRegistroClinico.ArquivoDaFicha],
            AcessoParaMexer = Permissao.VerProntuario,
            AoAbrir = AbrirRegistroAsync
        };

        Plano = new PlanoDeCuidadosViewModel(escopos, dialogo);

        // ⚠️ A MENSAGEM tem uma superfície só, e ela é da TELA. O componente escreve na
        // propriedade dele; quem a mostra é quem o hospeda — duas caixas de mensagem na
        // mesma tela seriam duas respostas para a mesma pergunta (a regra de "um estado
        // vazio por pergunta", parcela 37), e a de baixo apareceria longe do clique.
        Plano.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not (nameof(PlanoDeCuidadosViewModel.Mensagem)
                                       or nameof(PlanoDeCuidadosViewModel.MensagemEhErro))
                || Plano.Mensagem is null) return;

            Mensagem = Plano.Mensagem;
            MensagemEhErro = Plano.MensagemEhErro;
        };

        // Remontagem por busca CONCLUÍDA, nunca por CollectionChanged — que dispara uma
        // vez por linha inserida (a armadilha da parcela 37).
        Seletor.Atualizou += AplicarBusca;
    }

    /// <summary>
    /// O shell resolve só o DataContext — quem abre a lista é este contrato. Sem ele a
    /// tela nasceria vazia, e tela vazia se lê como sistema quebrado.
    /// </summary>
    public Task CarregarAsync() => CarregarListaAsync();

    // ---- A lista ----

    /// <summary>
    /// Monta a lista: a fila do dia (o padrão) ou a carteira inteira. É UMA coleção, e é
    /// isso que impede as duas de se limparem mutuamente pelo <c>SelectedItem</c>.
    /// </summary>
    private async Task CarregarListaAsync()
    {
        // ⚠️ A carteira é dado cadastral: sem `VerFichaPaciente` não se monta a lista, e a
        // tela DIZ por quê em vez de ficar em branco.
        if (!PodeVerFicha)
        {
            Pacientes.Clear();
            ResumoDaLista = "Nenhum paciente listado.";
            Mensagem = "O seu acesso não permite ver a ficha dos pacientes. "
                     + "Peça em Acessos a permissão \"Ver ficha do paciente\".";
            MensagemEhErro = true;
            return;
        }

        var geracao = ++_geracaoLista;
        CarregandoLista = true;

        try
        {
            if (MostrandoTodos || !string.IsNullOrWhiteSpace(Seletor.Termo))
            {
                await Seletor.BuscarAsync(imediato: true);
                return; // O `Atualizou` publica a lista — um caminho só de publicação.
            }

            using var scope = _escopos.CreateScope();
            var hoje = DateOnly.FromDateTime(DateTime.Today);

            var agenda = await scope.ServiceProvider
                .GetRequiredService<AgendaService>()
                .DoDiaAsync(hoje);

            if (geracao != _geracaoLista) return;

            // Cancelado não é passagem: quem desmarcou não passa pela enfermagem.
            var doDia = agenda
                .Where(a => a.Status != StatusAgendamento.Cancelado && a.Paciente is not null)
                .OrderBy(a => a.DataHora)
                .Select(a => a.Paciente!)
                .DistinctBy(p => p.Id)
                .ToList();

            // Entre o Clear() e o último Add não pode haver await (parcela 62).
            Pacientes.Clear();
            foreach (var p in doDia) Pacientes.Add(p);

            ResumoDaLista = doDia.Count == 0
                ? "Ninguém marcado para hoje — use a busca ou \"Todos os pacientes\"."
                : $"Na clínica hoje — {doDia.Count} paciente(s). "
                  + "Busque pelo nome para alcançar quem não está no dia.";
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoLista) return;
            Diagnostico.Registrar("Enfermagem — a lista do dia não pôde ser carregada", ex);
            ResumoDaLista = "Não foi possível ler a agenda de hoje.";
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            if (geracao == _geracaoLista) CarregandoLista = false;
        }
    }

    /// <summary>Publica o resultado da busca na MESMA coleção da fila do dia.</summary>
    private void AplicarBusca()
    {
        if (!PodeVerFicha) return;

        // ⚠️ A busca AVANÇA a geração da lista (parcela 72). Sem isto, uma carga da fila do
        // dia no ar quando a pessoa começa a digitar responderia DEPOIS e sobrescreveria o
        // resultado da busca — a lista mostraria o dia com o termo digitado no campo, que é
        // a tela mentindo de um jeito que não reproduz em banco local.
        ++_geracaoLista;

        var termo = Seletor.Termo;

        // Sem termo e fora do modo "todos", a lista volta a ser a do dia.
        if (!MostrandoTodos && string.IsNullOrWhiteSpace(termo))
        {
            _ = CarregarListaAsync();
            return;
        }

        var achados = Seletor.Resultados.ToList();

        Pacientes.Clear();
        foreach (var p in achados) Pacientes.Add(p);

        ResumoDaLista = string.IsNullOrWhiteSpace(termo)
            ? $"Todos os pacientes — {achados.Count}."
            : $"Busca “{termo}” — {achados.Count} paciente(s).";

        CarregandoLista = false;
    }

    [RelayCommand]
    private async Task TodosAsync()
    {
        MostrandoTodos = true;
        await CarregarListaAsync();
    }

    [RelayCommand]
    private async Task HojeAsync()
    {
        MostrandoTodos = false;
        Seletor.Termo = null;
        await CarregarListaAsync();
    }

    // ---- A tela do paciente ----

    /// <summary>
    /// ATENDER — abre o paciente na tela clínica, com a seção da enfermagem já aberta.
    ///
    /// O que ele resolve
    /// -----------------
    /// A clínica pediu <i>"ver todos os pacientes e clicar em ATENDER"</i>, e a palavra
    /// importa: é a mesma da fila de quem consulta, e passa a levar ao mesmo lugar — a
    /// tela do paciente, com o crachá, as alergias, o prontuário inteiro e o cronômetro
    /// da sessão. Até aqui a enfermagem escrevia numa janela modal de altura fixa; dois
    /// desenhos para o mesmo ato no mesmo sistema é o que faz alguém achar que abriu
    /// outro programa (a reprovação da parcela 47).
    ///
    /// ⚠️ O CAMINHO DE VOLTA É PARTE DA FEATURE, não um remendo. Esta tela é do SHELL e é
    /// publicada por DOIS módulos: no <c>Clinica.Recepcao.exe</c> o módulo Clínico não
    /// está carregado, e <c>NavegacaoSuite.Ir</c> devolveria <c>false</c> EM SILÊNCIO — o
    /// botão não faria nada (a regressão da parcela 37, 4ª rodada, que passou pelas três
    /// redes). Então se pergunta ANTES, com <c>Existe</c>, e o painel desta própria tela
    /// continua sendo a resposta onde não há posto clínico.
    ///
    /// ⚠️ E leva o HORÁRIO de hoje junto quando há um. É esse vínculo que faz a passagem
    /// nascer ligada à sessão — sem ele o registro é do paciente e de sessão nenhuma, e a
    /// ficha do atendimento sai sem ele.
    /// </summary>
    [RelayCommand]
    private async Task AtenderAsync(Paciente? paciente)
    {
        // Guarda sobre PARÂMETRO: nunca dispara vindo de botão de linha (a exceção
        // declarada da checagem 21).
        if (paciente is null) return;

        // Este executável não tem posto clínico (é o caso do Clinica.Recepcao.exe): o
        // painel desta própria tela é a resposta, e continua sendo uma tela completa.
        if (!NavegacaoSuite.Existe(ChavesSuite.AtendimentoEnfermagem))
        {
            await AbrirAsync(paciente);
            return;
        }

        // ⚠️ TUDO dentro do try. Este comando também é disparado pelo clique da LINHA, com
        // `_ = AtenderAsync(...)`: exceção que escape daqui não tem quem a observe — nem o
        // `DispatcherUnhandledException`, que é a rede do WPF e não a de Tasks. Ela viraria
        // um clique que não faz nada, sem uma linha no log.
        try
        {
            var foco = await EntregaDoPaciente.AoPostoAsync(
                _escopos, paciente.Id, paciente.Nome);

            // O foco é SINGLETON e é registrado pelo módulo Clínico. Sem ele não há para
            // onde entregar o paciente — e navegar assim abriria a tela clínica com o
            // paciente ANTERIOR, que é o pior desfecho possível num prontuário.
            if (foco && NavegacaoSuite.Ir(ChavesSuite.AtendimentoEnfermagem)) return;

            // `Existe` disse que sim e `Ir` disse que não: a permissão do destino pode ter
            // mudado entre um e outro. Guarda que volta em silêncio é botão que não faz
            // nada (parcela 41), então o painel atende.
            await AbrirAsync(paciente);
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Enfermagem — o paciente não pôde ser aberto", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Abre o PAINEL desta tela — a evolução do paciente, com a largura inteira.
    ///
    /// É a resposta onde não há posto clínico (o <c>Clinica.Recepcao.exe</c>), e continua
    /// sendo uma tela completa: o prontuário inteiro, o plano de cuidados, as duas portas
    /// do dia e a ficha do atendimento.
    /// </summary>
    private async Task AbrirAsync(Paciente? paciente)
    {
        // Guarda sobre PARÂMETRO: nunca dispara vindo de botão de linha (a exceção
        // declarada da checagem 21).
        if (paciente is null) return;

        _pacienteId = paciente.Id;
        Paciente = paciente.Nome;
        Contexto = MontarContextoBasico(paciente);
        Alerta = null;
        TermoPendente = null;
        FolhaDeHoje = null;
        MostrandoLista = false;
        Mensagem = null;

        await RecarregarAsync();
    }

    [RelayCommand]
    private void Voltar()
    {
        // ⚠️ Limpar a seleção: sem isto, escolher o MESMO paciente de novo não dispara o
        // OnEscolhidoChanged, e o clique não faz nada — o defeito da parcela 41.
        Escolhido = null;
        MostrandoLista = true;
        Mensagem = null;
        _ = LinhaDoTempo.CarregarAsync(0);
        _ = Plano.CarregarAsync(0);
        Alerta = null;
        TermoPendente = null;
        FolhaDeHoje = null;
        _pacienteId = 0;

        // ⚠️ O acesso é registrado na TROCA de paciente: voltar para a lista zera a marca,
        // senão reabrir o mesmo prontuário meia hora depois não deixaria rastro.
        _acessoRegistradoDe = 0;
    }

    /// <summary>
    /// Escreve a evolução do paciente aberto — a passagem AVULSA, que é o caso normal
    /// fora da infusão. A janela é a mesma da fila da sala: quatro montagens da mesma tela
    /// divergiriam na primeira correção.
    /// </summary>
    [RelayCommand]
    private async Task RegistrarAsync()
    {
        if (_pacienteId == 0)
        {
            // A guarda DIZ por que não dá, em vez de voltar calada (parcela 41).
            Mensagem = "Abra um paciente para escrever a evolução.";
            MensagemEhErro = true;
            return;
        }

        SessaoUsuario.Atual.Exigir(
            Permissao.RegistrarEvolucaoEnfermagem, "registrar evolução de enfermagem");

        EvolucaoEnfermagemWindow.Abrir(_escopos, _dialogo, _pacienteId, Paciente);

        await RecarregarAsync();
    }

    /// <summary>
    /// Colhe o termo do procedimento do dia.
    ///
    /// ⚠️ O botão só EXISTE quando há termo pendente. Ela tem
    /// <c>ColherAssinaturaPaciente</c> desde a parcela 66, com o argumento escrito de que
    /// prepara a sala — e as duas telas dela não tinham a porta. Alerta sem porta no mesmo
    /// app ensina a ignorar o alerta (parcela 48).
    /// </summary>
    [RelayCommand]
    private async Task ColherTermoAsync()
    {
        if (_pacienteId == 0 || _modeloTermoPendente is null) return;

        try
        {
            // Dentro do try: `Exigir` LANÇA, e fora dele a recusa sobe até a rede do
            // Dispatcher em vez de virar a frase que a tela já sabe mostrar.
            SessaoUsuario.Atual.Exigir(
                Permissao.ColherAssinaturaPaciente, "colher a assinatura do paciente");

            ColetaDeTermo.Abrir(
                _escopos, _pacienteId, Paciente,
                _modeloTermoPendente, _documentoTermoPendente);
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Enfermagem — termo não pôde ser colhido", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
            return;
        }

        // Recarrega de qualquer forma: abrir a janela já EMITE o termo numerado.
        await RecarregarAsync();
    }

    /// <summary>
    /// Abre a folha de infusão de hoje deste paciente. Até aqui as duas telas da enfermagem
    /// se ignoravam: da carteira não se chegava à folha.
    /// </summary>
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
            Diagnostico.Registrar("Enfermagem — folha de execução não pôde ser aberta", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
            return;
        }

        await RecarregarAsync();
    }

    private async Task RecarregarAsync()
    {
        if (_pacienteId == 0) return;

        // ⚠️ Dado de saúde: nem ler nem desenhar sem `VerProntuario` — e a tela diz por quê
        // em vez de devolver lista vazia, que se lê como "este paciente nunca passou aqui".
        if (!PodeVerProntuario)
        {
            await LinhaDoTempo.CarregarAsync(0);
            Mensagem = "O seu acesso não permite ler o prontuário. "
                     + "Peça em Acessos a permissão \"Ver prontuário\".";
            MensagemEhErro = true;
            return;
        }

        var geracao = ++_geracaoCarga;
        var pacienteId = _pacienteId;
        try
        {
            using var scope = _escopos.CreateScope();
            var servicos = scope.ServiceProvider;

            // ⚠️ A trilha de LEITURA (parcela 52), na TROCA de paciente — nunca a cada
            // carga, porque esta roda também depois de escrever. A origem é `SalaInfusao`,
            // e não `ProntuarioClinico`: a janela de silêncio é POR ORIGEM, e gravar a
            // origem errada FUNDE o acesso da enfermagem com o de quem abriu o prontuário
            // clínico de verdade — apagando exatamente a distinção que uma investigação
            // procura.
            if (_acessoRegistradoDe != pacienteId)
            {
                _acessoRegistradoDe = pacienteId;
                await servicos.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(pacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.SalaInfusao);
            }

            var lista = await servicos.GetRequiredService<EvolucaoEnfermagemService>()
                .DoPacienteAsync(pacienteId, limite: 100);

            if (geracao != _geracaoCarga) return;

            // O prontuário inteiro sai do componente compartilhado — ele tem contador de
            // geração próprio e o filtro de acesso por natureza.
            await LinhaDoTempo.CarregarAsync(pacienteId);
            if (geracao != _geracaoCarga) return;

            // A leitura acima também alimenta o CONTEXTO: a última aferição é o dado que a
            // comparação de daqui a vinte minutos usa, e o valor isolado quase não diz nada.
            await CarregarContextoAsync(servicos, pacienteId, lista, geracao);
            if (geracao != _geracaoCarga) return;

            await Plano.CarregarAsync(pacienteId);
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;
            Diagnostico.Registrar("Enfermagem — evolução do paciente não pôde ser carregada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// A FICHA DO ATENDIMENTO da enfermagem — o papel que o paciente leva embora
    /// (parcela 78).
    ///
    /// ⚠️ Até aqui a enfermagem não tinha papel NENHUM: a passagem só saía impressa dentro
    /// da folha de infusão, isto é, quando havia folha. A técnica que colhe sinais vitais,
    /// troca o curativo e registra a consulta de enfermagem completa — as cinco etapas da
    /// COFEN — não tinha o que entregar a ninguém.
    ///
    /// É o MESMO documento do médico, recortado no dia: o paciente veio uma vez, e o que
    /// aconteceu com ele é um fato só. Dois papéis obrigariam a clínica a entregar dois e o
    /// convênio a casar duas numerações.
    /// </summary>
    [RelayCommand]
    private async Task ImprimirFichaAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "imprimir a ficha do atendimento");

        if (_pacienteId == 0) return;

        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);

            byte[] pdf;
            string numero;
            using (var scope = _escopos.CreateScope())
            {
                var servicos = scope.ServiceProvider;

                var documento = await servicos.GetRequiredService<DocumentoClinicoService>()
                    .EmitirRelatorioEvolucaoAsync(
                        _pacienteId, SessaoUsuario.Atual.ProfissionalId,
                        inicio: hoje, fim: hoje,
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
            Diagnostico.Registrar("Enfermagem — ficha do atendimento não pôde ser emitida", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// A linha de contexto e as duas portas condicionais.
    ///
    /// ⚠️ Falha aqui NÃO derruba a evolução, que é o assunto da tela — mas também não passa
    /// calada: vai para o log e a linha diz que não foi conferida. Falha exibida como
    /// sucesso é o que faz a clínica acreditar que não há alergia registrada.
    /// </summary>
    private async Task CarregarContextoAsync(
        IServiceProvider servicos, int pacienteId,
        IReadOnlyList<EvolucaoEnfermagem> evolucoes, int geracao)
    {
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var partes = new List<string>();

            var conferencia = await servicos.GetRequiredService<PrescricaoService>()
                .ContextoAsync(pacienteId);
            if (geracao != _geracaoCarga) return;

            Alerta = conferencia.Alergias.Count == 0
                ? null
                : "⚠ ALERGIA: " + string.Join(" · ", conferencia.Alergias.Select(a => a.Rotulo));

            if (conferencia.MedicacoesEmUso.Count > 0)
                partes.Add("em uso: "
                    + string.Join(", ", conferencia.MedicacoesEmUso.Select(m => m.Rotulo)));

            // A ÚLTIMA aferição — o dado que a comparação de daqui a vinte minutos usa. O
            // valor isolado quase não diz nada; a diferença diz.
            var ultima = evolucoes.FirstOrDefault(e => !e.Cancelada && e.TemSinaisVitais);
            if (ultima?.SinaisVitaisResumidos is { } sinais)
                partes.Add($"{sinais} às {ultima.Hora:HH\\:mm} de {ultima.Data:dd/MM}");

            // Peso COM a data: ele é insumo da conferência da dose (mg/kg), e quem confere
            // a dose é quem administra.
            var resumo = await servicos.GetRequiredService<MedidaClinicaService>()
                .ResumoAsync(pacienteId);
            if (geracao != _geracaoCarga) return;

            if (resumo.Ultimas.FirstOrDefault(m => m.TipoCodigo == CatalogoMedidas.Peso)
                is { } peso)
                partes.Add($"peso {peso.Valor:0.#} kg ({peso.Data:dd/MM/yyyy})");

            // O termo do dia: a porta que a parcela 66 deu à enfermagem e ninguém abriu.
            var termos = await servicos.GetRequiredService<TermoProcedimentoService>()
                .SituacaoDoDiaAsync(pacienteId, hoje);
            if (geracao != _geracaoCarga) return;

            if (termos.FirstOrDefault(t => t.Pendente) is { } pendente)
            {
                TermoPendente = $"Colher: {pendente.NomeDoTermo}";
                _modeloTermoPendente = pendente.ModeloId;
                _documentoTermoPendente = pendente.DocumentoId;
                partes.Add($"termo pendente: {pendente.NomeDoTermo}");
            }
            else
            {
                TermoPendente = null;
                _modeloTermoPendente = null;
                _documentoTermoPendente = null;
            }

            // A folha de hoje — a ponte entre a carteira e a sala.
            var folhas = await servicos.GetRequiredService<ChecagemPrescricaoService>()
                .DoDiaAsync(hoje, incluirEncerradas: true);
            if (geracao != _geracaoCarga) return;

            if (folhas.FirstOrDefault(f => f.PacienteId == pacienteId) is { } folha)
            {
                _folhaDeHojeId = folha.Id;
                FolhaDeHoje = $"Folha {folha.Numero}";
                partes.Add($"infusão {folha.Numero} · {folha.Pendentes} item(ns) aguardando");
            }
            else
            {
                _folhaDeHojeId = 0;
                FolhaDeHoje = null;
            }

            Contexto = string.Join(" · ", new[] { Contexto }
                .Concat(partes)
                .Where(p => !string.IsNullOrWhiteSpace(p)));
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;
            Diagnostico.Registrar("Enfermagem — contexto clínico não pôde ser conferido", ex);
            Alerta = "Não foi possível conferir alergias e contexto clínico deste paciente. "
                   + "Confira no prontuário antes de administrar qualquer coisa.";
        }
    }

    /// <summary>Idade e convênio — o que se sabe sem ir ao banco de novo.</summary>
    private static string MontarContextoBasico(Paciente paciente)
    {
        var partes = new List<string>();

        if (paciente.DataNascimento is { } nascimento)
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var idade = hoje.Year - nascimento.Year;
            if (nascimento > hoje.AddYears(-idade)) idade--;
            partes.Add($"{idade} anos");
        }

        if (!string.IsNullOrWhiteSpace(paciente.ConvenioNome))
            partes.Add(paciente.ConvenioNome);

        return string.Join(" · ", partes);
    }
}
