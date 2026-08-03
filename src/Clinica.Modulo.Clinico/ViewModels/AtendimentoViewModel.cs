using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Clinico.Janelas;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
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
public sealed class LinhaSessaoAnterior
{
    public required int EvolucaoId { get; init; }
    public required string Data { get; init; }
    public required string Eva { get; init; }
    public required string Queixa { get; init; }
    public required string Conduta { get; init; }
    public required string Evolucao { get; init; }

    public static LinhaSessaoAnterior De(Evolucao e) => new()
    {
        EvolucaoId = e.Id,
        Data = e.Data.ToString("dd/MM/yyyy"),
        Eva = e.TemParEva ? $"EVA {e.EvaAntes} → {e.EvaDepois}" : "EVA não medida",
        Queixa = string.IsNullOrWhiteSpace(e.QueixaPrincipal) ? "—" : e.QueixaPrincipal!,
        Conduta = string.IsNullOrWhiteSpace(e.Conduta) ? "—" : e.Conduta!,
        Evolucao = string.IsNullOrWhiteSpace(e.TextoEvolucao) ? "—" : e.TextoEvolucao!
    };
}

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
    /// O painel do consultório, que já abre com a agenda do dia e a carteira — e não com
    /// uma caixa de busca vazia sobre uma coluna em branco.
    /// </summary>
    public SeletorClinicoViewModel Seletor { get; }

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

    public ObservableCollection<LinhaSessaoAnterior> Anteriores { get; } = [];

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

    /// <summary>Evolução em edição. 0 = sessão nova.</summary>
    [ObservableProperty] private int _evolucaoId;

    [ObservableProperty] private string _paciente = string.Empty;
    [ObservableProperty] private bool _semPaciente = true;

    [ObservableProperty] private DateTime _data = DateTime.Today;

    [ObservableProperty] private int? _evaAntes;
    [ObservableProperty] private int? _evaDepois;

    [ObservableProperty] private string? _queixaPrincipal;
    [ObservableProperty] private string? _conduta;
    [ObservableProperty] private string? _textoEvolucao;
    [ObservableProperty] private string? _orientacoes;

    /// <summary>De onde veio a sessão: chamada do dia, ou escolhida na busca.</summary>
    [ObservableProperty] private string _origem = string.Empty;

    [ObservableProperty] private string _resumoDor = string.Empty;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Metade VISÍVEL da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditarProntuario => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    /// <summary>Valores da escala, para os dois seletores de dor.</summary>
    public IReadOnlyList<int> EscalaEva { get; } =
        Enumerable.Range(Evolucao.EvaMinima, Evolucao.EvaMaxima - Evolucao.EvaMinima + 1).ToList();

    public AtendimentoViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, PacienteEmFoco foco)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _foco = foco;

        Seletor = new SeletorClinicoViewModel(escopos, foco);
        Seletor.Escolhido += AoTrocarPaciente;

        // O paciente do posto: quem veio da agenda já chega escolhido, e o profissional
        // não redigita o nome que acabou de clicar.
        if (_foco.Definido)
        {
            Paciente = _foco.Nome;
            SemPaciente = false;
            Origem = DescreverOrigem(_foco.AgendamentoId);
            _ = CarregarAsync();
        }
    }

    private void AoTrocarPaciente(ItemPacienteClinico item)
    {
        // O painel já gravou o contexto do posto — inclusive DERRUBANDO o horário de
        // origem quando a escolha veio da busca. Sem isso, a evolução do novo paciente
        // nasceria amarrada à sessão do anterior.
        Paciente = item.Nome;
        SemPaciente = false;
        Origem = DescreverOrigem(item.AgendamentoId);
        _ = CarregarAsync();
    }

    /// <summary>
    /// De onde a sessão veio — e é diferença que muda o registro, não decoração: chamada
    /// da agenda, a evolução nasce ligada ao horário e sai da lista de pendências do
    /// consultório; escolhida na busca, não.
    /// </summary>
    private static string DescreverOrigem(int? agendamentoId)
        => agendamentoId is null
            ? "Escolhido na busca — a evolução não fica ligada a nenhum horário."
            : "Chamado da agenda de hoje — a evolução nasce ligada a este horário.";

    private int PacienteId => _foco.PacienteId ?? 0;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        if (PacienteId == 0)
        {
            SemPaciente = true;
            return;
        }

        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;
            Anteriores.Clear();
            Seletor.Sincronizar();

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();

            var sessoes = await prontuario.DoPacienteAsync(PacienteId);

            // A sessão do horário chamado, quando ela já foi escrita: abrir o atendimento
            // de novo tem de CONTINUAR o registro, nunca criar um segundo para a mesma
            // sessão — dois registros do mesmo atendimento é o defeito que faz a clínica
            // desconfiar do prontuário inteiro.
            var doHorario = _foco.AgendamentoId is { } agendamentoId
                ? sessoes.FirstOrDefault(e => e.AgendamentoId == agendamentoId)
                : null;

            if (doHorario is not null) Preencher(doHorario);
            else Limpar();

            foreach (var e in sessoes.Where(e => e.Id != EvolucaoId).Take(SessoesAnterioresVisiveis))
                Anteriores.Add(LinhaSessaoAnterior.De(e));

            await CarregarAlertasAsync(scope.ServiceProvider);

            // O mapa vem depois de resolvida a evolução do horário: ele precisa saber
            // se está editando uma sessão já escrita (e então carrega os pontos dela) ou
            // começando uma nova.
            var mapa = new MapaCorporalViewModel(
                _escopos, PacienteId, EvolucaoId == 0 ? null : EvolucaoId);
            await mapa.CarregarAsync();
            Mapa = mapa;

            var dor = await prontuario.EvolucaoDaDorAsync(PacienteId);
            ResumoDor = dor.SessoesComMedida == 0
                ? "Nenhuma sessão com o par EVA (antes e depois) ainda."
                : $"Começou em {dor.DorInicial}/10 e está em {dor.DorAtual}/10 — "
                  + $"{dor.SessoesComMedida} sessão(ões) medidas, alívio médio de "
                  + $"{dor.AlivioMedioPorSessao:0.#} por sessão.";
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar("Consultório — atendimento não pôde ser carregado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>
    /// Os alertas do paciente. Falham SOZINHOS: o atendimento não pode deixar de abrir
    /// porque a leitura administrativa quebrou — quem está na sala é o paciente, e a
    /// sessão acontece de qualquer forma.
    /// </summary>
    private async Task CarregarAlertasAsync(IServiceProvider servicos)
    {
        Alertas.Clear();
        AlertasClinicos.Clear();

        // O prontuário falha SEPARADO do administrativo: uma consulta quebrada não pode
        // apagar a outra lista, e "sem alergia registrada" nunca pode ser o que a tela diz
        // quando na verdade não conseguiu ler.
        try
        {
            var problemas = servicos.GetRequiredService<ProblemaPacienteService>();

            foreach (var p in await problemas.AlertasAsync(PacienteId))
                AlertasClinicos.Add(new LinhaAlertaClinico
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

            AlertasClinicos.Add(new LinhaAlertaClinico
            {
                Texto = "Não foi possível ler a lista de problemas deste paciente — ela "
                        + "está vazia por falha de leitura, não porque não haja alergia "
                        + "registrada.",
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
                Alertas.Add(new LinhaAlertaClinico
                {
                    Texto = a.Descricao,
                    Grave = a.Urgencia == NivelUrgencia.Vermelho
                });
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — alertas do paciente não puderam ser lidos", ex);

            // Terceiro estado: a lista vazia por falha não pode se parecer com "nada a
            // avisar". A frase entra na própria lista, que é onde o profissional olha.
            Alertas.Add(new LinhaAlertaClinico
            {
                Texto = "Não foi possível conferir carteirinha, cota e pendências deste "
                        + "paciente — a lista está vazia por falha de leitura, não porque "
                        + "não haja nada.",
                Grave = false
            });
        }
    }

    private void Preencher(Evolucao e)
    {
        EvolucaoId = e.Id;
        Data = e.Data.ToDateTime(TimeOnly.MinValue);
        EvaAntes = e.EvaAntes;
        EvaDepois = e.EvaDepois;
        QueixaPrincipal = e.QueixaPrincipal;
        Conduta = e.Conduta;
        TextoEvolucao = e.TextoEvolucao;
        Orientacoes = e.Orientacoes;
    }

    private void Limpar()
    {
        EvolucaoId = 0;
        Data = DateTime.Today;
        EvaAntes = null;
        EvaDepois = null;
        QueixaPrincipal = null;
        Conduta = null;
        TextoEvolucao = null;
        Orientacoes = null;
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
    private async Task SalvarAsync()
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
                Conduta = Conduta,
                TextoEvolucao = TextoEvolucao,
                Orientacoes = Orientacoes
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
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Consultório — sessão não pôde ser salva", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

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
        if (Mapa is null) return;

        try
        {
            new MapaCorporalWindow(Mapa, $"Mapa corporal — {Paciente}")
            {
                Owner = System.Windows.Application.Current?.MainWindow
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
        if (PacienteId == 0) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "emitir documento clínico");

            var vm = new DocumentoEdicaoViewModel(_escopos, PacienteId);
            var janela = new DocumentoWindow(vm)
            {
                Owner = System.Windows.Application.Current?.MainWindow
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

    /// <summary>Abre a curva de dor deste paciente sem perder o foco do posto.</summary>
    [RelayCommand]
    private void VerEvolucaoDaDor() => NavegacaoSuite.Ir(ModuloClinico.ChaveEvolucaoDor);

    /// <summary>Abre as escalas deste paciente.</summary>
    [RelayCommand]
    private void VerAvaliacoes() => NavegacaoSuite.Ir(ModuloClinico.ChaveAvaliacoes);
}
