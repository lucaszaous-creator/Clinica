using System.Collections.ObjectModel;
using Clinica.Application;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Medidas;
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

    public ObservableCollection<LinhaEvolucaoEnfermagem> Registros { get; } = new();

    [ObservableProperty] private bool _mostrandoLista = true;
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _carregandoLista;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>O que a lista está mostrando agora — a frase que evita o filtro esquecido.</summary>
    [ObservableProperty] private string _resumoDaLista = string.Empty;

    /// <summary>Modo "carteira inteira" ligado pelo botão, ou pela busca com termo.</summary>
    [ObservableProperty] private bool _mostrandoTodos;

    /// <summary>
    /// A escolha da lista. UM clique abre a tela do paciente: quem escolhe alguém na
    /// carteira quer a evolução dele, não uma seleção que não faz nada.
    /// </summary>
    [ObservableProperty] private Paciente? _escolhido;

    partial void OnEscolhidoChanged(Paciente? value)
    {
        if (value is not null) _ = AbrirAsync(value);
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

    public EnfermagemViewModel(IServiceScopeFactory escopos, IDialogoService dialogo)
    {
        _escopos = escopos;
        _dialogo = dialogo;

        Seletor = new SeletorPacienteViewModel(escopos, limite: null);

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

    /// <summary>Abre a tela DO PACIENTE — a evolução dele, com a largura inteira.</summary>
    [RelayCommand]
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
        Registros.Clear();
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

        SessaoUsuario.Atual.Exigir(
            Permissao.ColherAssinaturaPaciente, "colher a assinatura do paciente");

        ColetaDeTermo.Abrir(
            _escopos, _pacienteId, Paciente,
            _modeloTermoPendente, _documentoTermoPendente);

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

        SessaoUsuario.Atual.Exigir(
            Permissao.ChecarPrescricao, "abrir a folha de execução");

        var vm = new FolhaExecucaoViewModel(_escopos, _dialogo, _folhaDeHojeId);
        new FolhaExecucaoWindow(vm) { Owner = JanelaDona.Atual() }.ShowDialog();

        await RecarregarAsync();
    }

    private async Task RecarregarAsync()
    {
        if (_pacienteId == 0) return;

        // ⚠️ Dado de saúde: nem ler nem desenhar sem `VerProntuario` — e a tela diz por quê
        // em vez de devolver lista vazia, que se lê como "este paciente nunca passou aqui".
        if (!PodeVerProntuario)
        {
            Registros.Clear();
            Mensagem = "O seu acesso não permite ler o prontuário. "
                     + "Peça em Acessos a permissão \"Ver prontuário\".";
            MensagemEhErro = true;
            return;
        }

        var geracao = ++_geracaoCarga;
        var pacienteId = _pacienteId;
        Carregando = true;
        NaoVerificado = false;

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

            var substituidas = lista
                .Where(e => e.RetificaEvolucaoId is not null)
                .Select(e => e.RetificaEvolucaoId!.Value)
                .ToHashSet();

            // Entre o Clear() e o último Add não pode haver await (parcela 62).
            var linhas = lista
                .Select(e => LinhaEvolucaoEnfermagem.De(
                    e, substituidas.Contains(e.Id), mostrarData: true))
                .ToList();

            Registros.Clear();
            foreach (var l in linhas) Registros.Add(l);

            await CarregarContextoAsync(servicos, pacienteId, lista, geracao);
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;
            NaoVerificado = true;
            Diagnostico.Registrar("Enfermagem — evolução do paciente não pôde ser carregada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            if (geracao == _geracaoCarga) Carregando = false;
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
