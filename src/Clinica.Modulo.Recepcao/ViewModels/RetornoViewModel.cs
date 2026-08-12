using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>Um paciente para chamar de volta, como o balcão precisa vê-lo.</summary>
public sealed partial class LinhaRetorno : ObservableObject
{
    public required int PacienteId { get; init; }
    public required string Paciente { get; init; }
    public required string NomeCru { get; init; }
    public required string? TelefoneCru { get; init; }
    public required int DiasSemVir { get; init; }
    public required string UltimaVez { get; init; }

    /// <summary>Id do contato de campanha, quando a rodada já o gerou. Null = ainda não gerado.</summary>
    public int? ContatoId { get; init; }

    public required bool TemConsentimento { get; init; }

    public bool TemTelefone => !string.IsNullOrWhiteSpace(TelefoneCru);

    /// <summary>Muda na tela assim que o WhatsApp abre, sem esperar recarregar a lista.</summary>
    [ObservableProperty] private string _situacao = "a chamar";

    [ObservableProperty] private bool _chamado;

    /// <summary>
    /// Só dá para chamar quem tem telefone E consentiu. Recall é MARKETING — ao contrário
    /// da confirmação da própria sessão, que é transacional —, e a LGPD exige a
    /// finalidade <c>ComunicacaoEMarketing</c> vigente.
    /// </summary>
    public bool PodeChamar => TemTelefone && TemConsentimento && ContatoId is not null;

    /// <summary>O que FALTA, escrito na linha. O botão apagado sem explicação é o defeito da parcela 41.</summary>
    public string Impedimento =>
        !TemTelefone ? "sem telefone no cadastro"
        : !TemConsentimento ? "sem consentimento de contato (LGPD)"
        : ContatoId is null ? "ainda fora da rodada — clique em Gerar rodada"
        : string.Empty;

    public bool TemImpedimento => !string.IsNullOrWhiteSpace(Impedimento);
}

/// <summary>
/// Chamar de volta quem parou de vir — DENTRO da Recepção (parcela 48).
///
/// Por que aqui
/// ------------
/// A capacidade existe desde a parcela 5 (<see cref="CampanhaService.GerarRecallAsync"/>)
/// e a leitura "quem parou de vir" desde a 32, e as duas moravam só no GERENTE. Só que
/// quem telefona para o paciente é o BALCÃO — é exatamente o argumento que trouxe a rodada
/// de confirmação para cá na parcela 26, aplicado à outra ponta do relacionamento.
///
/// Não é uma segunda implementação: chama o mesmo serviço, com a mesma chave de
/// idempotência (<c>REC:{paciente}:{aaaa-MM}</c>). Rodar aqui e no Gerente no mesmo mês
/// não manda duas mensagens para o mesmo paciente.
///
/// O que a tela mostra que a rodada sozinha não mostra
/// --------------------------------------------------
/// A lista traz TODO CANDIDATO, inclusive quem a rodada não pôde gerar — sem telefone, sem
/// consentimento. Eles aparecem CONTADOS e com o motivo escrito, em vez de sumirem: quem
/// sumiu da lista some também da cabeça de quem trabalha, e "12 pacientes" que na verdade
/// eram 30 faz a clínica concluir que quase ninguém está sumindo.
/// </summary>
public sealed partial class RetornoViewModel : ObservableObject, ICarregarAoAbrir
{
    private readonly IServiceScopeFactory _escopos;

    public ObservableCollection<LinhaRetorno> Pacientes { get; } = [];

    /// <summary>Tudo o que o banco devolveu; <see cref="Pacientes"/> é o recorte do filtro.</summary>
    private readonly List<LinhaRetorno> _todos = [];

    /// <summary>
    /// Dias sem vir para entrar na lista. É configurável na tela porque a resposta muda
    /// com a especialidade: 60 dias sem acupuntura é sumiço; 60 dias entre consultas de
    /// acompanhamento é o intervalo normal.
    /// </summary>
    [ObservableProperty] private int _diasSemVir = CampanhaService.DiasInatividadePadrao;

    // ---- Filtro (em memória, sobre o que já foi lido — os dias acima mudam a CARGA;
    // estes três só estreitam). "Esconder já chamados" é a fila de trabalho da manhã:
    // depois de dez ligações, o que interessa é quem ainda falta.
    [ObservableProperty] private string _filtroNomeRetorno = string.Empty;
    [ObservableProperty] private bool _esconderChamados;
    [ObservableProperty] private bool _soComTelefone;

    partial void OnFiltroNomeRetornoChanged(string value) => Refiltrar();
    partial void OnEsconderChamadosChanged(bool value) => Refiltrar();
    partial void OnSoComTelefoneChanged(bool value) => Refiltrar();

    public bool FiltroAtivo =>
        EsconderChamados || SoComTelefone || !string.IsNullOrWhiteSpace(FiltroNomeRetorno);

    [RelayCommand]
    private void LimparFiltro()
    {
        EsconderChamados = false;
        SoComTelefone = false;
        FiltroNomeRetorno = string.Empty;
    }

    /// <summary>O estado vazio muda de frase quando há filtro — vazio filtrado não é "ninguém sumiu".</summary>
    [ObservableProperty] private string _vazioDescricao =
        "Não há paciente sem vir há tanto tempo e sem horário marcado. Diminua os dias acima para olhar mais perto.";

    [ObservableProperty] private string _resumo = string.Empty;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _carregando;

    /// <summary>A leitura FALHOU — o terceiro estado, nunca lista vazia.</summary>
    [ObservableProperty] private bool _naoVerificado;

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeEditar => SessaoUsuario.Atual.Pode(Permissao.GerenciarCampanhas);

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): o campo "dias sem vir" dispara uma
    /// carga a cada mudança, e a resposta do valor antigo pode voltar por último — a lista
    /// mostraria os candidatos de 60 dias sob um filtro que diz 90. Só a carga mais nova
    /// escreve na tela.
    /// </summary>
    private int _geracaoCarga;

    public RetornoViewModel(IServiceScopeFactory escopos) => _escopos = escopos;

    public Task CarregarAsync() => RecarregarAsync();

    [RelayCommand]
    public async Task RecarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        Carregando = true;
        try
        {
            Mensagem = null;
            MensagemEhErro = false;
            NaoVerificado = false;

            using var scope = _escopos.CreateScope();
            var campanhas = scope.ServiceProvider.GetRequiredService<CampanhaService>();

            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var candidatos = await campanhas.CandidatosRecallAsync(hoje, DiasSemVir);

            // Os contatos JÁ gerados neste mês, para casar com o candidato e saber quem já
            // foi chamado. A rodada é por mês (a origem carrega aaaa-MM), então a janela
            // de leitura é o mês inteiro — ler só hoje diria "ninguém foi chamado" na
            // manhã seguinte à rodada.
            var inicioDoMes = new DateOnly(hoje.Year, hoje.Month, 1);
            var contatos = await campanhas.ContatosAsync(
                TipoContato.Recall, null, inicioDoMes, inicioDoMes.AddMonths(1).AddDays(-1));
            var porPaciente = contatos
                .GroupBy(c => c.PacienteId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.Id).First());

            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            _todos.Clear();
            foreach (var c in candidatos)
            {
                porPaciente.TryGetValue(c.PacienteId, out var contato);

                _todos.Add(new LinhaRetorno
                {
                    PacienteId = c.PacienteId,
                    Paciente = c.Nome,
                    NomeCru = c.Nome,
                    TelefoneCru = c.Telefone,
                    DiasSemVir = c.DiasSemVir,
                    UltimaVez = c.UltimoAtendimento is { } quando
                        ? $"última sessão em {quando:dd/MM/yyyy}"
                        : "sem sessão registrada",
                    ContatoId = contato?.Id,
                    TemConsentimento = c.TemConsentimento,
                    Chamado = contato?.EnviadoEm is not null,
                    Situacao = Descrever(contato)
                });
            }

            Refiltrar();
        }
        catch (Exception ex)
        {
            // Chegou tarde: outra carga mais nova já foi pedida.
            if (geracao != _geracaoCarga) return;

            Clinica.Application.Diagnostico.Registrar(
                "Recepção — lista de retorno não pôde ser lida", ex);
            NaoVerificado = true;
            Erro($"Não foi possível ler a lista: {ex.Message}");
        }
        finally
        {
            // A carga superada não apaga o "Carregando" da que ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    /// <summary>
    /// Aplica o filtro sobre o que já foi lido — em memória, sem ida ao banco (o padrão
    /// da tela de Consultas ao lado). Quem foi chamado AGORA (o clique no WhatsApp) não
    /// some da tela até a próxima recarga: sumir sob o dedo de quem acabou de clicar se
    /// lê como defeito.
    /// </summary>
    private void Refiltrar()
    {
        Pacientes.Clear();
        foreach (var p in _todos.Where(p =>
                     (!EsconderChamados || !p.Chamado)
                     && (!SoComTelefone || p.TemTelefone)
                     && Busca.Casa(p.NomeCru, FiltroNomeRetorno)))
            Pacientes.Add(p);

        OnPropertyChanged(nameof(FiltroAtivo));

        // O resumo DIZ que está filtrado e MANTÉM a janela dita: sem os dias, o número
        // perde a régua que o define. Chamados e travados continuam contando o TOTAL.
        var chamados = _todos.Count(p => p.Chamado);
        var travados = _todos.Count(p => p.TemImpedimento && !p.Chamado);

        Resumo = _todos.Count == 0
            ? $"Ninguém sem vir há {DiasSemVir} dias ou mais. A agenda está girando."
            : FiltroAtivo
                ? $"{Pacientes.Count} de {_todos.Count} paciente(s) sem vir há {DiasSemVir} "
                  + $"dias ou mais no filtro · {chamados} já chamado(s) no total."
                : $"{Pacientes.Count} paciente(s) sem vir há {DiasSemVir} dias ou mais · "
                  + $"{chamados} já chamado(s)"
                  + (travados > 0 ? $" · {travados} sem como chamar (veja o motivo na linha)." : ".");

        VazioDescricao = FiltroAtivo
            ? "Ninguém bate com o filtro — limpe-o para ver a lista inteira da janela."
            : "Não há paciente sem vir há tanto tempo e sem horário marcado. Diminua os dias acima para olhar mais perto.";
    }

    private static string Descrever(ContatoCampanha? contato) => contato switch
    {
        null => "a chamar",
        { Status: StatusContato.Respondido } => "respondeu",
        { Status: StatusContato.Dispensado } => "dispensado",
        { EnviadoEm: { } quando } => $"chamado em {quando:dd/MM HH:mm}",
        _ => "na rodada, a chamar"
    };

    /// <summary>
    /// Gera a rodada do mês. Idempotente por paciente e mês: clicar duas vezes não cria
    /// contato repetido, e rodar aqui depois de o Gerente ter rodado não manda a mensagem
    /// de novo.
    /// </summary>
    [RelayCommand]
    private async Task GerarAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.GerenciarCampanhas, "gerar a rodada de retorno");

            using var scope = _escopos.CreateScope();
            var campanhas = scope.ServiceProvider.GetRequiredService<CampanhaService>();

            var r = await campanhas.GerarRecallAsync(
                DateOnly.FromDateTime(DateTime.Today), DiasSemVir);

            // Quem NÃO entrou aparece contado, nunca omitido: sem consentimento e sem
            // telefone são coisas que a recepção resolve — no balcão, na próxima visita —,
            // e ela só as resolve se souber quantas são.
            var partes = new List<string>();
            if (r.Gerados > 0) partes.Add($"{r.Gerados} para chamar");
            if (r.JaExistiam > 0) partes.Add($"{r.JaExistiam} já estavam na rodada do mês");
            if (r.SemConsentimento > 0) partes.Add($"{r.SemConsentimento} sem consentimento (LGPD)");
            if (r.SemTelefone > 0) partes.Add($"{r.SemTelefone} sem telefone");

            Mensagem = partes.Count == 0
                ? "Nenhum paciente elegível para a rodada deste mês."
                : string.Join(" · ", partes) + ".";
            MensagemEhErro = false;

            await RecarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — rodada de retorno não pôde ser gerada", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>
    /// Abre o WhatsApp com a mensagem pronta e registra o envio. Um clique por paciente,
    /// pela mesma razão da confirmação: o número é o da clínica, e disparo em massa
    /// automatizado por ali termina com o número bloqueado.
    /// </summary>
    [RelayCommand]
    private async Task ChamarAsync(LinhaRetorno? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.GerenciarCampanhas, "chamar o paciente de volta");

            if (linha.ContatoId is not { } contatoId)
            {
                Erro("Este paciente ainda não está na rodada — clique em \"Gerar rodada\" primeiro.");
                return;
            }

            var erro = Whatsapp.Abrir(
                linha.TelefoneCru, linha.NomeCru,
                Whatsapp.Recall(linha.NomeCru, linha.DiasSemVir));

            if (erro is not null)
            {
                // Sem telefone não há o que registrar: marcar como chamado aqui faria a
                // rodada dizer que o paciente foi procurado quando ninguém falou com ele.
                Erro(erro);
                return;
            }

            using var scope = _escopos.CreateScope();
            var campanhas = scope.ServiceProvider.GetRequiredService<CampanhaService>();
            await campanhas.RegistrarEnvioAsync(contatoId, SessaoUsuario.Atual.Operador);

            linha.Chamado = true;
            linha.Situacao = $"chamado em {DateTime.Now:dd/MM HH:mm}";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — chamada de retorno não pôde ser registrada", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>O paciente respondeu — tira a linha da fila de cobrança do mês.</summary>
    [RelayCommand]
    private async Task RespondeuAsync(LinhaRetorno? linha)
    {
        if (linha?.ContatoId is not { } contatoId) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.GerenciarCampanhas, "registrar a resposta");

            using var scope = _escopos.CreateScope();
            var campanhas = scope.ServiceProvider.GetRequiredService<CampanhaService>();
            await campanhas.RegistrarRespostaAsync(contatoId, "respondeu ao retorno");

            linha.Situacao = "respondeu";
            linha.Chamado = true;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — resposta de retorno não pôde ser registrada", ex);
            Erro(ex.Message);
        }
    }

    partial void OnDiasSemVirChanged(int value) => _ = RecarregarAsync();

    private void Erro(string mensagem)
    {
        Mensagem = mensagem;
        MensagemEhErro = true;
    }
}
