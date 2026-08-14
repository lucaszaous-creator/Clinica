using System.IO;
using System.Text;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>Uma linha de "quem abriu este prontuário".</summary>
public sealed class LinhaAcessoProntuario
{
    public required string Quando { get; init; }
    public required string Operador { get; init; }
    public required string Porta { get; init; }

    public static LinhaAcessoProntuario De(EventoAuditoria e) => new()
    {
        Quando = e.DataHora.ToString("dd/MM/yyyy HH:mm"),
        Operador = e.Operador,
        // O Detalhe é o rótulo humano gravado pelo serviço ("Prontuário clínico aberto");
        // a Acao ("ProntuarioAcessado:Documento") é o identificador — e identificador na
        // tela é o defeito da parcela 41.
        Porta = string.IsNullOrWhiteSpace(e.Detalhe) ? "Prontuário acessado" : e.Detalhe!
    };
}

/// <summary>
/// A GUARDA DO PRONTUÁRIO — a tela que responde à auditoria de fornecedor (parcela 52).
///
/// Por que ela existe
/// ------------------
/// A cliente perguntou, por escrito: <i>"como você garante retenção e recuperação desses
/// dados durante 20 anos?"</i> (ponto 7) e <i>"como recuperar/exportar os prontuários caso
/// o contrato seja encerrado?"</i> (ponto 8).
///
/// O motor das duas respostas ficou pronto e testado na mesma parcela — e sem tela. Numa
/// auditoria, <b>garantia que não se consegue demonstrar vale o mesmo que garantia que não
/// existe</b>: quem contrata não lê o código-fonte, olha a tela. Era o defeito recorrente
/// do projeto na sua variante mais cara, porque aqui ele vira promessa a um cliente que
/// está auditando.
///
/// Uma tela, uma pergunta
/// ----------------------
/// A pergunta é <b>"o que está guardado, e até quando"</b>. A exportação está aqui porque é
/// o que se FAZ com o que está guardado — não é uma segunda pergunta, é a ação desta. O
/// resto (backup, permissões, trilha) tem tela própria.
///
/// O que esta tela NÃO tem, de propósito
/// -------------------------------------
/// <b>Botão de eliminar prontuário vencido.</b> O prazo é PISO de guarda, não agendamento
/// de destruição: vencido, o prontuário fica elegível, e a decisão é da clínica com a
/// comissão de revisão que o art. 7º da Lei 13.787/2018 prevê. Um botão aqui transformaria
/// uma decisão colegiada e irreversível em dois cliques de quem estivesse com a tela aberta.
/// </summary>
public sealed partial class GuardaProntuarioViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    /// <summary>Escolher de quem ver a guarda. É o componente padrão — não se reescreve busca.</summary>
    public SeletorPacienteViewModel Paciente { get; }

    // ---- Retrato da clínica ----
    [ObservableProperty] private string _anosDeGuarda = $"{GuardaProntuario.AnosDeGuarda} anos";
    [ObservableProperty] private string _pacientes = "—";
    [ObservableProperty] private string _comRegistro = "—";
    [ObservableProperty] private string _registrosGuardados = "—";
    [ObservableProperty] private string _elegiveis = "—";
    [ObservableProperty] private string _maisAntigo = "—";
    [ObservableProperty] private string _resumoClinica = string.Empty;

    // ---- Situação de um paciente ----
    [ObservableProperty] private bool _temPaciente;
    [ObservableProperty] private string _nomePaciente = string.Empty;
    [ObservableProperty] private string _situacaoPaciente = string.Empty;
    [ObservableProperty] private string _detalhePaciente = string.Empty;

    /// <summary>
    /// Quem abriu ESTE prontuário, do mais recente para o mais antigo — a pergunta da
    /// cliente ao pé da letra ("quem acessou, quando acessou"). O serviço tinha a leitura
    /// própria (<c>AcessoProntuarioService.DoPacienteAsync</c>) desde a parcela 52 e
    /// nenhuma tela a chamava: investigar acesso indevido exigia adivinhar o nome da ação
    /// no filtro da Auditoria.
    /// </summary>
    public System.Collections.ObjectModel.ObservableCollection<LinhaAcessoProntuario> Acessos { get; } = [];

    [ObservableProperty] private string _resumoAcessos = string.Empty;

    /// <summary>Há trilha para listar — o cartão some quando não há (vazio já tem frase).</summary>
    [ObservableProperty] private bool _temAcessos;

    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, "nenhum prontuário elegível" por erro
    /// de consulta fica idêntico à resposta correta, que é justamente essa.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>
    /// Metade VISÍVEL da permissão. Exportar o prontuário da clínica inteira é um ato de
    /// direção, não de balcão — a outra metade é o <c>Exigir</c> dentro dos comandos.
    /// </summary>
    public bool PodeExportar => SessaoUsuario.Atual.Pode(Permissao.VerAuditoria);

    public GuardaProntuarioViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
        Paciente = new SeletorPacienteViewModel(escopos);

        Paciente.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SeletorPacienteViewModel.Selecionado))
                _ = CarregarPacienteAsync();
        };
    }

    /// <summary>
    /// O retrato da clínica. É uma varredura paciente a paciente e pode demorar numa base
    /// grande — por isso NÃO roda no construtor: quem abre a tela vê a explicação e decide
    /// quando pagar a consulta. Tela que trava por dez segundos ao abrir é tela que a
    /// direção deixa de abrir.
    /// </summary>
    [RelayCommand]
    public async Task CarregarClinicaAsync()
    {
        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var resumo = await scope.ServiceProvider
                .GetRequiredService<GuardaProntuarioService>()
                .ResumoAsync(DateOnly.FromDateTime(DateTime.Today));

            Pacientes = resumo.Pacientes.ToString("N0");
            ComRegistro = resumo.ComRegistro.ToString("N0");
            RegistrosGuardados = resumo.TotalDeRegistros.ToString("N0");
            Elegiveis = resumo.JaCumpriramPrazo.ToString("N0");
            MaisAntigo = resumo.RegistroMaisAntigo?.ToString("dd/MM/yyyy") ?? "—";

            // "0 elegíveis" é a resposta NORMAL numa clínica com menos de 20 anos de casa.
            // Sem esta frase, o zero se lê como consulta quebrada.
            ResumoClinica = resumo.NadaElegivel
                ? "Nenhum prontuário cumpriu os 20 anos ainda — o que é o esperado, e "
                  + "significa que TODOS continuam sob guarda obrigatória."
                : $"{resumo.JaCumpriramPrazo} prontuário(s) já cumpriram o prazo mínimo e são "
                  + "elegíveis a eliminação. O sistema não elimina nada: a decisão é da "
                  + "clínica, com a comissão de revisão prevista no art. 7º da Lei 13.787/2018.";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — guarda do prontuário", ex);
            NaoVerificado = true;
            Mensagem = $"Não foi possível ler a guarda: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>
    /// Número da carga mais recente pedida — descarte de resposta fora de ordem (parcela 50).
    ///
    /// Aqui ele não é zelo: o seletor é uma BUSCA, e quem investiga um acesso indevido
    /// troca de paciente várias vezes seguidas. São duas idas ao banco em sequência (a
    /// guarda e a trilha de leitura), e num banco remoto a leitura VELHA pode responder
    /// depois da nova — o resultado é a guarda de um paciente escrita sob o nome de outro,
    /// ou a trilha de acesso da Maria listada na ficha do João. Numa tela de conformidade
    /// isso é pior do que não ter a tela: a resposta errada tem a mesma cara da certa.
    /// </summary>
    private int _geracaoPaciente;

    private async Task CarregarPacienteAsync()
    {
        var geracao = ++_geracaoPaciente;

        if (Paciente.Selecionado is not { } escolhido)
        {
            TemPaciente = false;
            Acessos.Clear();
            TemAcessos = false;
            return;
        }

        try
        {
            Mensagem = null;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var situacao = await scope.ServiceProvider
                .GetRequiredService<GuardaProntuarioService>()
                .DoPacienteAsync(escolhido.Id);

            // Chegou tarde: outra pessoa já foi escolhida.
            if (geracao != _geracaoPaciente) return;

            TemPaciente = true;
            NomePaciente = situacao.PacienteNome;
            SituacaoPaciente = situacao.Descrever(DateOnly.FromDateTime(DateTime.Today));

            // As canceladas aparecem CONTADAS à parte: elas estão sob guarda, e escondê-las
            // aqui contradiria a razão de terem deixado de ser apagadas.
            DetalhePaciente =
                $"{situacao.Sessoes} sessão(ões) vigente(s)"
                + (situacao.SessoesCanceladas > 0
                    ? $" · {situacao.SessoesCanceladas} cancelada(s), guardada(s)"
                    : string.Empty)
                + $" · {situacao.Avaliacoes} avaliação(ões)"
                + $" · {situacao.Medidas} medida(s)"
                + $" · {situacao.Documentos} documento(s)"
                // A folha de infusão entrou na guarda na parcela 52 e ficou de fora DESTA
                // frase: a tela contava cinco naturezas de registro e o prazo de 20 anos
                // era calculado sobre seis. Contagem incompleta numa tela de conformidade
                // é pior do que contagem nenhuma — ela parece conferida.
                + $" · {situacao.Prescricoes} prescrição(ões)"
                + (situacao.UltimoRegistro is { } data
                    ? $" — último registro em {data:dd/MM/yyyy}"
                      + (situacao.Origem is { } o ? $" ({o})" : string.Empty)
                    : " — sem registro no prontuário");

            // A trilha de LEITURA do mesmo paciente, na mesma tela: guarda e acesso são
            // as duas metades da pergunta da auditoria.
            var acessos = await scope.ServiceProvider
                .GetRequiredService<AcessoProntuarioService>()
                .DoPacienteAsync(escolhido.Id, limite: 100);

            // Chegou tarde: outra pessoa já foi escolhida. Sem esta segunda guarda a
            // trilha de leitura de um paciente entraria na ficha de outro — e a lista
            // publicada aqui é justamente a prova que a auditoria pede.
            if (geracao != _geracaoPaciente) return;

            // Entre o Clear() e o último Add não pode haver await (parcela 62): monta-se
            // em lista local e só então publica.
            var linhas = acessos.Select(LinhaAcessoProntuario.De).ToList();

            Acessos.Clear();
            foreach (var linha in linhas) Acessos.Add(linha);

            TemAcessos = Acessos.Count > 0;
            ResumoAcessos = Acessos.Count == 0
                ? "Nenhum acesso registrado — a trilha de leitura existe desde a versão da "
                  + "parcela 52; aberturas anteriores a ela não deixaram rastro."
                : $"{Acessos.Count} acesso(s) registrados"
                  + (Acessos.Count == 100 ? " (mostrando os 100 mais recentes)" : string.Empty)
                  + ".";
        }
        catch (Exception ex)
        {
            // Chegou tarde: a falha de uma leitura superada não pode apagar a tela da
            // pessoa que está escolhida agora.
            if (geracao != _geracaoPaciente) return;

            Clinica.Application.Diagnostico.Registrar("Gerente — guarda de um paciente", ex);
            Mensagem = $"Não foi possível ler a guarda deste paciente: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    /// <summary>Exporta o prontuário de TODA a clínica.</summary>
    [RelayCommand]
    private Task ExportarTudoAsync() => ExportarAsync(null);

    /// <summary>Exporta o prontuário só do paciente escolhido.</summary>
    [RelayCommand]
    private Task ExportarPacienteAsync() => ExportarAsync(Paciente.Selecionado?.Id);

    /// <summary>
    /// Grava os CSV numa pasta escolhida.
    ///
    /// Vários arquivos numa PASTA, e não um zip: o destino costuma ser um pendrive
    /// entregue a outro fornecedor, e pasta se confere abrindo — zip alguém esquece de
    /// abrir e descobre corrompido meses depois.
    /// </summary>
    private async Task ExportarAsync(int? pacienteId)
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.VerAuditoria, "exportar o prontuário");

            // Guarda que DIZ por que não dá (a lição da parcela 41): botão que volta calado
            // faz a pessoa concluir que o sistema quebrou.
            if (pacienteId is null
                && !_dialogo.ConfirmarPerigo("Exportar o prontuário da clínica",
                    "Vai sair o prontuário de TODOS os pacientes, com dado de saúde, em "
                    + "arquivos que qualquer pessoa abre. Grave em local controlado e "
                    + "entregue só a quem tem direito de recebê-lo."))
                return;

            var pasta = ImpressaoPdf.EscolherPasta("Onde gravar a exportação do prontuário");
            if (pasta is null) return;

            Carregando = true;
            Mensagem = "Montando a exportação…";
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var arquivos = await scope.ServiceProvider
                .GetRequiredService<ExportacaoProntuarioService>()
                .ExportarAsync(pacienteId);

            // A trilha vem ANTES do arquivo, e a ordem é a regra — não arrumação.
            //
            // A exportação é um ACESSO ao prontuário, e dos maiores: leva tudo de uma vez.
            // O ponto 7 do compromisso de conformidade diz que ação que possa acontecer sem
            // a linha correspondente é ação sem trilha, e aqui o "ato" é gravar arquivo em
            // disco — não cabe no mesmo SaveChanges. O que resta é a ORDEM: registrando
            // primeiro, uma falha no registro impede a exportação; registrando depois, o
            // pendrive sai da clínica com o prontuário de todo mundo e sem uma linha
            // dizendo quem o levou. Trilha sem exportação é ruído; exportação sem trilha é
            // o buraco que uma investigação procura.
            var acessos = scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>();
            if (pacienteId is { } um)
            {
                await acessos.RegistrarAsync(um, SessaoUsuario.Atual.Operador,
                    OrigemAcessoProntuario.ExportacaoTitular);
            }
            else
            {
                var todos = await scope.ServiceProvider.GetRequiredService<PacienteService>()
                    .BuscarAsync(termo: null);
                await acessos.RegistrarExportacaoDaClinicaAsync(
                    todos.Select(p => p.Id).ToList(), SessaoUsuario.Atual.Operador);
            }

            var destino = Path.Combine(
                pasta, $"prontuario-{DateTime.Now:yyyy-MM-dd-HHmm}");
            Directory.CreateDirectory(destino);

            foreach (var arquivo in arquivos)
                // BOM UTF-8, pela convenção do projeto: sem ele o Excel lê como ANSI e
                // "Ocupação" vira "OcupaÃ§Ã£o".
                await File.WriteAllTextAsync(
                    Path.Combine(destino, arquivo.Nome), arquivo.Conteudo, new UTF8Encoding(true));

            Mensagem = $"Exportação gravada em {destino} — {arquivos.Count} arquivo(s).";
            _snackbar.Sucesso("Prontuário exportado.");
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — exportação do prontuário", ex);
            Mensagem = $"Não foi possível exportar: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }
}
