using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// Uma sessão do prontuário numa lista. Usada pela ficha do paciente E pela tela de
/// Prontuário — o <see cref="De"/> existe para as duas nunca divergirem no que mostram:
/// duas descrições da mesma sessão em telas diferentes é defeito que ninguém reporta e
/// todo mundo estranha.
/// </summary>
public sealed class LinhaEvolucao
{
    public required int EvolucaoId { get; init; }
    public required string Data { get; init; }
    public required string Profissional { get; init; }
    public required string Eva { get; init; }
    public required string Resumo { get; init; }
    public required bool Melhorou { get; init; }
    public required bool Piorou { get; init; }
    public required string Anexos { get; init; }

    public static LinhaEvolucao De(Evolucao e, int anexos) => new()
    {
        EvolucaoId = e.Id,
        Data = e.Data.ToString("dd/MM/yyyy"),
        Profissional = e.Profissional?.Rotulo ?? "—",
        Eva = e.TemParEva ? $"EVA {e.EvaAntes} → {e.EvaDepois}" : "EVA não medida",
        Resumo = Resumir(e),
        Melhorou = e.VariacaoEva > 0,
        Piorou = e.VariacaoEva < 0,
        Anexos = anexos == 0 ? string.Empty : $"{anexos} anexo(s)"
    };

    /// <summary>
    /// A primeira linha que existir, cortada. A queixa vem antes da conduta porque é o
    /// que identifica a sessão na lista: "lombalgia" acha o dia; "agulhamento" não.
    /// </summary>
    private static string Resumir(Evolucao e)
    {
        var texto = e.QueixaPrincipal ?? e.TextoEvolucao ?? e.Conduta ?? e.Orientacoes ?? string.Empty;
        texto = texto.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return texto.Length <= 120 ? texto : texto[..117] + "…";
    }
}

/// <summary>Situação de uma finalidade de consentimento LGPD.</summary>
public sealed class LinhaConsentimento
{
    public required FinalidadeConsentimento Finalidade { get; init; }
    public required string Rotulo { get; init; }
    public required string Situacao { get; init; }
    public required bool Vigente { get; init; }
    public required bool PodeRevogar { get; init; }

    /// <summary>Id do registro atual — necessário para revogar.</summary>
    public int? RegistroId { get; init; }
}

/// <summary>Um contato de campanha no histórico de CRM da ficha.</summary>
public sealed class LinhaContato
{
    public required string Tipo { get; init; }
    public required string Data { get; init; }
    public required string Situacao { get; init; }
    public required string Detalhe { get; init; }

    public static LinhaContato De(ContatoCampanha c) => new()
    {
        Tipo = c.Tipo switch
        {
            TipoContato.ConfirmacaoSessao => "Confirmação de sessão",
            TipoContato.Nps => "Pesquisa de satisfação (NPS)",
            TipoContato.Recall => "Recall",
            _ => c.Tipo.ToString()
        },
        Data = c.Referencia.ToString("dd/MM/yyyy"),
        Situacao = c.Status.ToString(),
        // A nota do NPS é o que a direção quer ver de relance; nos outros tipos, quem
        // disparou. Mostrar "—" seria desperdiçar a linha.
        Detalhe = c.Nota is { } nota
            ? $"nota {nota}/10"
            : c.EnviadoEm is { } enviado
                ? $"enviado em {enviado:dd/MM/yyyy} por {c.EnviadoPor ?? "?"}"
                : "ainda não enviado"
    };
}

/// <summary>Um alerta de elegibilidade na ficha.</summary>
public sealed class LinhaAlerta
{
    public required string Descricao { get; init; }
    public required bool EhVermelho { get; init; }
}

/// <summary>Um documento clínico emitido, na lista da ficha.</summary>
public sealed class LinhaDocumento
{
    public required int DocumentoId { get; init; }
    public required string Numero { get; init; }
    public required string Tipo { get; init; }
    public required string Data { get; init; }
    public required string Profissional { get; init; }
    public required string Codigo { get; init; }
    public required bool Cancelado { get; init; }
    public required string Situacao { get; init; }

    /// <summary>Nome sugerido ao salvar o PDF.</summary>
    public string NomeArquivo => $"{Tipo}-{Numero.Replace('/', '-')}.pdf";

    /// <summary>Cancelar duas vezes não existe — o botão desliga depois do primeiro.</summary>
    public bool PodeCancelar => !Cancelado;

    /// <summary>
    /// Mesma razão do <see cref="LinhaEvolucao.De"/>: a ficha e a tela de Prescrições
    /// listam o mesmo documento, e a situação ("Cancelado em…") não pode ser escrita
    /// duas vezes em lugares diferentes.
    /// </summary>
    public static LinhaDocumento De(DocumentoClinico d) => new()
    {
        DocumentoId = d.Id,
        Numero = d.Numero,
        Tipo = TipoDocumentoInfo.Rotular(d.Tipo),
        Data = d.Data.ToString("dd/MM/yyyy"),
        Profissional = d.Profissional?.Rotulo ?? "—",
        Codigo = d.CodigoVerificacao,
        Cancelado = d.Cancelado,
        Situacao = d.Cancelado ? $"Cancelado em {d.CanceladoEm:dd/MM/yyyy}" : "Válido"
    };
}

/// <summary>
/// Ficha 360º do paciente: cadastro, elegibilidade, consentimentos LGPD, prontuário com
/// a evolução da dor e o histórico de guias — tudo o que a recepção precisa saber com o
/// paciente na frente.
///
/// A ELEGIBILIDADE é o que esta tela tem de mais valioso: carteirinha vencida e cota
/// estourada hoje só aparecem na hora de faturar, quando a sessão já aconteceu. Aqui
/// elas aparecem antes, enquanto ainda dá para resolver.
/// </summary>
public sealed partial class FichaPacienteViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ISnackbarService _snackbar;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaEvolucao> Prontuario { get; } = [];
    public ObservableCollection<LinhaConsentimento> Consentimentos { get; } = [];
    public ObservableCollection<LinhaAlerta> Alertas { get; } = [];
    public ObservableCollection<LinhaDocumento> Documentos { get; } = [];

    [ObservableProperty] private int _pacienteId;
    [ObservableProperty] private bool _carregando;

    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private string _documento = "—";
    [ObservableProperty] private string _telefone = "—";
    [ObservableProperty] private string _nascimento = "—";
    [ObservableProperty] private string _convenio = "—";
    [ObservableProperty] private string _carteirinha = "—";
    [ObservableProperty] private string _observacoes = string.Empty;
    [ObservableProperty] private byte[]? _foto;

    [ObservableProperty] private string _totalSessoes = "—";

    // ---- Padrão de falta (parcela 28) ----

    /// <summary>
    /// Quantas vezes o paciente faltou. A agenda registra `Faltou` desde a parcela 1 e os
    /// indicadores calculam a taxa da CLÍNICA — a do paciente nunca foi lida por ninguém.
    /// Quem está no balcão decidindo se dá o horário das 18h de terça (o mais disputado)
    /// para alguém que faltou três vezes não tinha como saber.
    /// </summary>
    [ObservableProperty] private string _faltas = "—";

    /// <summary>Cancelamento avisado, contado SEPARADO: quem desmarcou deu chance de reocupar.</summary>
    [ObservableProperty] private string _cancelamentos = "—";

    /// <summary>Padrão de falta digno de atenção. Aviso, nunca impedimento.</summary>
    [ObservableProperty] private bool _faltaReincidente;

    [ObservableProperty] private string _avisoFaltas = string.Empty;
    [ObservableProperty] private string _guiasEmAberto = "—";
    [ObservableProperty] private string _ultimaSessao = "—";

    // ---- Evolução da dor (EVA) ----
    [ObservableProperty] private string _dorInicial = "—";
    [ObservableProperty] private string _dorAtual = "—";
    [ObservableProperty] private string _ganhoAcumulado = "—";
    [ObservableProperty] private string _alivioMedio = "—";
    [ObservableProperty] private string _resumoEva = string.Empty;
    [ObservableProperty] private bool _semMedidaEva;

    /// <summary>
    /// Terceiro estado da elegibilidade: a checagem NÃO rodou. Sem isto, uma consulta
    /// que falhou apareceria como "tudo certo" — falha exibida como sucesso.
    /// </summary>
    [ObservableProperty] private bool _elegibilidadeNaoVerificada;

    // ===== CRM =====

    /// <summary>De onde o paciente veio. "Não perguntado" é um estado legítimo.</summary>
    [ObservableProperty] private string _origem = "Não perguntado";

    [ObservableProperty] private bool _temPaciente;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Últimas conversas da clínica com este paciente (confirmação, NPS, recall).</summary>
    public ObservableCollection<LinhaContato> Contatos { get; } = [];

    /// <summary>Avisa a tela dona que o cadastro mudou (para recarregar a lista).</summary>
    public event Action? Alterou;

    /// <summary>
    /// Habilita os botões de escrita da tela. É a metade VISÍVEL da permissão: o
    /// botão apagado explica por que não dá; a guarda no comando é que impede.
    /// Só desabilitar seria enfeite — um atalho de teclado passaria direto.
    /// </summary>
    public bool PodeEditarProntuario => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    /// <summary>
    /// Anonimizar não tem volta, então a barreira é outra: o balcão exporta os dados do
    /// titular, mas quem apaga a identificação é quem responde pela clínica.
    /// </summary>
    public bool PodeAnonimizar => SessaoUsuario.Atual.Pode(Permissao.AnonimizarDados);

    public FichaPacienteViewModel(
        IServiceScopeFactory escopos, ISnackbarService snackbar, IDialogoService dialogo)
    {
        _escopos = escopos;
        _snackbar = snackbar;
        _dialogo = dialogo;
    }

    /// <summary>Carrega a ficha de um paciente (ou limpa, quando o id é nulo).</summary>
    public async Task AbrirAsync(int? pacienteId)
    {
        if (pacienteId is null)
        {
            TemPaciente = false;
            PacienteId = 0;
            return;
        }

        PacienteId = pacienteId.Value;
        TemPaciente = true;
        await CarregarAsync();
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        if (PacienteId == 0) return;

        try
        {
            Carregando = true;
            Mensagem = string.Empty;
            MensagemEhErro = false;

            using var scope = _escopos.CreateScope();
            var pacientes = scope.ServiceProvider.GetRequiredService<PacienteService>();

            var paciente = await pacientes.ObterComHistoricoAsync(PacienteId);
            if (paciente is null)
            {
                TemPaciente = false;
                return;
            }

            AplicarCadastro(paciente);
            Foto = await pacientes.ObterFotoAsync(PacienteId) ?? paciente.FotoMiniatura;

            await CarregarProntuarioAsync(scope);
            await CarregarConsentimentosAsync(scope);
            await CarregarDocumentosAsync(scope);
            await CarregarCrmAsync(scope);
            await CarregarElegibilidadeAsync(scope);
            await CarregarFaltasAsync(scope);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — ficha do paciente não pôde ser carregada", ex);
            Mensagem = $"Não foi possível carregar a ficha: {ex.Message}";
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    private void AplicarCadastro(Paciente p)
    {
        Nome = p.Nome;
        Documento = string.IsNullOrWhiteSpace(p.Documento) ? "—" : Cpf.Formatar(p.Documento);
        Telefone = string.IsNullOrWhiteSpace(p.Telefone) ? "—" : p.Telefone!;
        Nascimento = p.DataNascimento is { } n ? $"{n:dd/MM/yyyy} ({Idade(n)} anos)" : "—";
        Convenio = CatalogoConvenios.Nome(p.ConvenioCodigo ?? p.Convenio.ToString());
        Carteirinha = string.IsNullOrWhiteSpace(p.Carteirinha)
            ? "—"
            : p.ValidadeCarteirinha is { } v
                ? $"{p.Carteirinha} · vence {v:dd/MM/yyyy}"
                : p.Carteirinha!;
        Observacoes = p.Observacoes ?? string.Empty;

        // Origem em branco é "ninguém perguntou", e a ficha diz isso com todas as letras
        // em vez de deixar o campo vazio: campo vazio parece defeito, e a recepção só vai
        // colher o dado se souber que ele falta.
        Origem = p.Origem switch
        {
            null => "Não perguntado",
            OrigemPaciente.Indicacao when !string.IsNullOrWhiteSpace(p.IndicadoPor)
                => $"Indicação de {p.IndicadoPor}",
            OrigemPaciente.Indicacao => "Indicação",
            OrigemPaciente.Encaminhamento => "Encaminhamento médico",
            OrigemPaciente.RedesSociais => "Redes sociais",
            OrigemPaciente.Fachada => "Passou em frente",
            OrigemPaciente.Convenio => "Lista do convênio",
            var o => o.ToString()!
        };

        var codigos = p.Atendimentos.SelectMany(a => a.Codigos).ToList();
        TotalSessoes = p.Atendimentos.Count.ToString();
        GuiasEmAberto = codigos.Count(c => c.DataBaixa is null).ToString();
        UltimaSessao = p.Atendimentos.Count == 0
            ? "—"
            : p.Atendimentos.Max(a => a.Data).ToString("dd/MM/yyyy");
    }

    private static int Idade(DateOnly nascimento)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var idade = hoje.Year - nascimento.Year;
        if (nascimento > hoje.AddYears(-idade)) idade--;
        return idade;
    }

    private async Task CarregarProntuarioAsync(IServiceScope scope)
    {
        var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();
        var evolucoes = await prontuario.DoPacienteAsync(PacienteId);

        Prontuario.Clear();
        foreach (var e in evolucoes)
        {
            var anexos = await prontuario.AnexosAsync(e.Id);
            Prontuario.Add(LinhaEvolucao.De(e, anexos.Count));
        }

        AplicarEvolucaoDaDor(await prontuario.EvolucaoDaDorAsync(PacienteId));
    }

    private void AplicarEvolucaoDaDor(EvolucaoDaDor dor)
    {
        SemMedidaEva = dor.SessoesComMedida == 0;

        if (SemMedidaEva)
        {
            DorInicial = DorAtual = GanhoAcumulado = AlivioMedio = "—";
            ResumoEva = dor.SessoesRegistradas == 0
                ? "Nenhuma sessão registrada no prontuário ainda."
                : $"{dor.SessoesRegistradas} sessão(ões) registradas, nenhuma com o par EVA "
                  + "(antes e depois). Sem o par não dá para dizer se a dor melhorou.";
            return;
        }

        DorInicial = $"{dor.DorInicial}/10";
        DorAtual = $"{dor.DorAtual}/10";
        GanhoAcumulado = dor.GanhoAcumulado is { } ganho
            ? (ganho >= 0 ? $"−{ganho} pontos" : $"+{-ganho} pontos")
            : "—";
        AlivioMedio = $"{dor.AlivioMedioPorSessao:0.#} por sessão";
        ResumoEva = $"{dor.SessoesComMedida} de {dor.SessoesRegistradas} sessão(ões) com EVA medida.";
    }

    /// <summary>
    /// Padrão de falta do paciente. Isolado do resto: se falhar, a ficha continua
    /// mostrando tudo o mais — o histórico de falta é informação de apoio, não a razão
    /// de a tela existir.
    ///
    /// Cancelamento avisado aparece SEPARADO das faltas: quem desmarcou deu à clínica a
    /// chance de reocupar o horário, e misturar os dois esconderia exatamente o
    /// comportamento que o número existe para mostrar.
    /// </summary>
    private async Task CarregarFaltasAsync(IServiceScope scope)
    {
        try
        {
            var servico = scope.ServiceProvider.GetRequiredService<RelacionamentoService>();
            var h = await servico.FaltasDoPacienteAsync(PacienteId);

            Faltas = h.Faltas == 0
                ? "nenhuma"
                : h.TaxaPercentual is { } taxa
                    ? $"{h.Faltas} ({taxa:0.#}%)"
                    : h.Faltas.ToString();

            Cancelamentos = h.Cancelamentos == 0 ? "nenhum" : h.Cancelamentos.ToString();
            FaltaReincidente = h.Reincidente;

            AvisoFaltas = h.Reincidente
                ? $"Faltou {h.Faltas} vez(es)"
                  + (h.UltimaFalta is { } ultima ? $", a última em {ultima:dd/MM/yyyy}" : string.Empty)
                  + ". Vale confirmar a sessão na véspera antes de dar a ele um horário disputado."
                : string.Empty;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — histórico de falta não pôde ser lido", ex);
            // Terceiro estado: "—" é diferente de "nenhuma falta". Dizer que o paciente
            // nunca faltou por causa de uma consulta quebrada seria falha exibida como
            // sucesso — e neste caso a favor de quem falta.
            Faltas = "não verificado";
            Cancelamentos = "não verificado";
            FaltaReincidente = false;
            AvisoFaltas = string.Empty;
        }
    }

    private async Task CarregarConsentimentosAsync(IServiceScope scope)
    {
        var servico = scope.ServiceProvider.GetRequiredService<ConsentimentoService>();
        var situacao = await servico.SituacaoAsync(PacienteId);

        Consentimentos.Clear();
        foreach (var finalidade in ConsentimentoService.Finalidades)
        {
            situacao.TryGetValue(finalidade, out var atual);

            Consentimentos.Add(new LinhaConsentimento
            {
                Finalidade = finalidade,
                Rotulo = ConsentimentoService.Rotular(finalidade),
                Situacao = Descrever(atual),
                Vigente = atual?.Vigente == true,
                PodeRevogar = atual?.Vigente == true,
                RegistroId = atual?.Id
            });
        }
    }

    /// <summary>
    /// Fecha o ciclo do marketing: a campanha sai no Gerente e o resultado dela aparece
    /// aqui, no balcão, onde alguém vai atender essa pessoa e precisa saber o que já foi
    /// falado com ela. Falhar aqui não derruba a ficha — é histórico, não o conteúdo.
    /// </summary>
    private async Task CarregarCrmAsync(IServiceScope scope)
    {
        Contatos.Clear();
        try
        {
            var campanhas = scope.ServiceProvider.GetRequiredService<CampanhaService>();
            foreach (var c in await campanhas.DoPacienteAsync(PacienteId))
                Contatos.Add(LinhaContato.De(c));
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — histórico de campanhas não pôde ser lido", ex);
        }
    }

    private async Task CarregarDocumentosAsync(IServiceScope scope)
    {
        var servico = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();

        Documentos.Clear();
        foreach (var d in await servico.DoPacienteAsync(PacienteId))
            Documentos.Add(LinhaDocumento.De(d));
    }

    private static string Descrever(ConsentimentoLgpd? registro) => registro switch
    {
        null => "Nunca perguntado",
        { RevogadoEm: { } revogado } => $"Revogado em {revogado:dd/MM/yyyy}",
        { Concedido: true } r => $"Concedido em {r.RegistradoEm:dd/MM/yyyy}",
        var r => $"Recusado em {r.RegistradoEm:dd/MM/yyyy}"
    };

    /// <summary>
    /// Conferência de elegibilidade — isolada de propósito: se ela falhar, a ficha
    /// continua abrindo e a tela diz que NÃO conseguiu conferir.
    /// </summary>
    private async Task CarregarElegibilidadeAsync(IServiceScope scope)
    {
        try
        {
            var servico = scope.ServiceProvider.GetRequiredService<ElegibilidadeService>();
            var resultado = await servico.ConferirAsync(PacienteId, DateOnly.FromDateTime(DateTime.Today));

            Alertas.Clear();
            foreach (var a in resultado.Alertas)
                Alertas.Add(new LinhaAlerta
                {
                    Descricao = a.Descricao,
                    EhVermelho = a.Urgencia == NivelUrgencia.Vermelho
                });

            ElegibilidadeNaoVerificada = false;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — elegibilidade não pôde ser conferida", ex);
            Alertas.Clear();
            ElegibilidadeNaoVerificada = true;
        }
    }

    // ==================== Comandos ====================

    [RelayCommand]
    private async Task EditarAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

        if (PacienteId == 0) return;

        var vm = new PacienteEdicaoViewModel(_escopos, PacienteId);
        var janela = new Janelas.PacienteWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() != true) return;
        _snackbar.Sucesso("Cadastro atualizado.");
        await CarregarAsync();
        Alterou?.Invoke();
    }

    [RelayCommand]
    private async Task NovaEvolucaoAsync() => await AbrirEvolucaoAsync(null);

    [RelayCommand]
    private async Task EditarEvolucaoAsync(LinhaEvolucao? linha)
    {
        if (linha is null) return;
        await AbrirEvolucaoAsync(linha.EvolucaoId);
    }

    [RelayCommand]
    private async Task ExcluirEvolucaoAsync(LinhaEvolucao? linha)
    {
        if (linha is null) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");
        if (!_dialogo.ConfirmarPerigo("Excluir do prontuário",
                $"Apagar a sessão de {linha.Data}? Os anexos dela vão junto, e o prontuário "
                + "é documento clínico — a exclusão fica registrada na auditoria.")) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();
            await prontuario.ExcluirAsync(linha.EvolucaoId, SessaoUsuario.Atual.Operador);
            _snackbar.Info("Sessão excluída do prontuário.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — evolução não pôde ser excluída", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    private async Task AbrirEvolucaoAsync(int? evolucaoId)
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");
        if (PacienteId == 0) return;

        var vm = new EvolucaoEdicaoViewModel(_escopos, PacienteId, evolucaoId);
        var janela = new Janelas.EvolucaoWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() != true) return;
        _snackbar.Sucesso("Prontuário atualizado.");
        await CarregarAsync();
    }

    /// <summary>Registra que o paciente CONCEDEU o consentimento desta finalidade.</summary>
    [RelayCommand]
    private async Task ConcederAsync(LinhaConsentimento? linha)
        => await RegistrarConsentimentoAsync(linha, concedido: true);

    /// <summary>Registra que o paciente RECUSOU. Recusa também é fato a provar.</summary>
    [RelayCommand]
    private async Task RecusarAsync(LinhaConsentimento? linha)
        => await RegistrarConsentimentoAsync(linha, concedido: false);

    private async Task RegistrarConsentimentoAsync(LinhaConsentimento? linha, bool concedido)
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

        if (linha is null || PacienteId == 0) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<ConsentimentoService>();
            await servico.RegistrarAsync(
                PacienteId, linha.Finalidade, concedido, operador: SessaoUsuario.Atual.Operador);

            _snackbar.Sucesso(concedido ? "Consentimento registrado." : "Recusa registrada.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — consentimento não pôde ser registrado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    [RelayCommand]
    private async Task RevogarAsync(LinhaConsentimento? linha)
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

        if (linha?.RegistroId is not { } registroId) return;

        var motivo = _dialogo.PerguntarTexto(
            "Revogar consentimento",
            $"Por que \"{linha.Rotulo}\" está sendo revogado? O registro anterior NÃO é apagado — "
            + "ele continua provando o consentimento do período já tratado.");
        if (string.IsNullOrWhiteSpace(motivo)) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<ConsentimentoService>();
            await servico.RevogarAsync(registroId, SessaoUsuario.Atual.Operador, motivo);
            _snackbar.Info("Consentimento revogado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — consentimento não pôde ser revogado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    // ==================== Documentos clínicos ====================

    /// <summary>Abre a janela de emissão (receita, atestado, declaração, pedido de exame).</summary>
    [RelayCommand]
    private async Task NovoDocumentoAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

        if (PacienteId == 0) return;

        var vm = new DocumentoEdicaoViewModel(_escopos, PacienteId);
        var janela = new Clinica.Desktop.Shell.Componentes.DocumentoWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (janela.ShowDialog() != true)
        {
            // Mesmo quando o usuário fecha sem concluir, um documento pode ter sido
            // emitido e só a impressão ter falhado — a lista precisa refletir isso.
            await CarregarAsync();
            return;
        }

        _snackbar.Sucesso("Documento emitido.");
        await CarregarAsync();
    }

    /// <summary>Relatório de evolução da dor, montado do prontuário.</summary>
    [RelayCommand]
    private Task EmitirRelatorioAsync()
        => EmitirMontadoAsync(TipoDocumentoClinico.RelatorioEvolucao);

    /// <summary>Termo de consentimento LGPD em papel, para assinar.</summary>
    [RelayCommand]
    private Task EmitirTermoAsync()
        => EmitirMontadoAsync(TipoDocumentoClinico.Consentimento);

    /// <summary>Anamnese: preenchida com o que o prontuário sabe, em linhas com o resto.</summary>
    [RelayCommand]
    private Task EmitirAnamneseAsync()
        => EmitirMontadoAsync(TipoDocumentoClinico.Anamnese);

    private async Task EmitirMontadoAsync(TipoDocumentoClinico tipo)
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

        if (PacienteId == 0) return;

        try
        {
            DocumentoClinico emitido;
            using (var scope = _escopos.CreateScope())
            {
                var servico = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();
                var operador = SessaoUsuario.Atual.Operador;

                emitido = tipo switch
                {
                    TipoDocumentoClinico.RelatorioEvolucao =>
                        await servico.EmitirRelatorioEvolucaoAsync(PacienteId, operador: operador),
                    TipoDocumentoClinico.Consentimento =>
                        await servico.EmitirTermoConsentimentoAsync(PacienteId, operador: operador),
                    _ => await servico.EmitirAnamneseAsync(PacienteId, operador: operador)
                };
            }

            await CarregarAsync();
            await ImprimirAsync(
                emitido.Id,
                $"{TipoDocumentoInfo.Rotular(tipo)}-{emitido.Numero.Replace('/', '-')}.pdf",
                emitido.Numero);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — documento montado do prontuário não pôde ser emitido", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>Segunda via: reimprime exatamente o que foi emitido.</summary>
    [RelayCommand]
    private async Task ImprimirDocumentoAsync(LinhaDocumento? linha)
    {
        if (linha is null) return;
        await ImprimirAsync(linha.DocumentoId, linha.NomeArquivo, linha.Numero);
    }

    private async Task ImprimirAsync(int documentoId, string nomeArquivo, string numero)
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

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                pdf, ImpressaoPdf.NomeSeguro(nomeArquivo));

            if (erro is null)
            {
                Mensagem = string.Empty;
                MensagemEhErro = false;
                return;
            }

            Mensagem = erro;
            MensagemEhErro = true;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — documento clínico não pôde ser impresso", ex);
            Mensagem = $"O documento {numero} está emitido, mas o PDF não pôde ser gerado: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Cancela um documento. Ele NÃO some da lista: a via que o paciente levou continua
    /// no mundo, e o registro é o que prova que ela não vale mais.
    /// </summary>
    [RelayCommand]
    private async Task CancelarDocumentoAsync(LinhaDocumento? linha)
    {
        SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

        if (linha is null || linha.Cancelado) return;

        var motivo = _dialogo.PerguntarTexto(
            "Cancelar documento",
            $"Por que o(a) {linha.Tipo.ToLowerInvariant()} {linha.Numero} está sendo cancelado? "
            + "Ele continua na lista, marcado como cancelado — a via impressa não desaparece "
            + "por ser apagada do sistema.");
        if (string.IsNullOrWhiteSpace(motivo)) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();
            await servico.CancelarAsync(linha.DocumentoId, motivo, SessaoUsuario.Atual.Operador);

            _snackbar.Info("Documento cancelado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — documento clínico não pôde ser cancelado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    // ==================== Direitos do titular (LGPD) ====================

    /// <summary>
    /// Entrega ao paciente tudo o que a clínica guarda sobre ele (LGPD, art. 18).
    ///
    /// Sai em TEXTO, não em PDF diagramado: o direito é de receber os dados em formato
    /// legível e reutilizável — quem pede costuma querer levá-los a outro serviço, e um
    /// relatório bonito seria menos útil. É a mesma razão de os relatórios exportarem CSV.
    /// </summary>
    [RelayCommand]
    private async Task ExportarDadosAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "ver o prontuário");

        if (PacienteId == 0) return;

        try
        {
            string texto;
            using (var scope = _escopos.CreateScope())
            {
                var servico = scope.ServiceProvider.GetRequiredService<TitularDadosService>();
                texto = await servico.ExportarAsync(PacienteId, SessaoUsuario.Atual.Operador);
            }

            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                System.Text.Encoding.UTF8.GetBytes(texto),
                ImpressaoPdf.NomeSeguro($"dados-{Nome}.txt"),
                "Texto (*.txt)|*.txt", ".txt");

            if (erro is null)
            {
                Mensagem = string.Empty;
                MensagemEhErro = false;
                return;
            }

            Mensagem = erro;
            MensagemEhErro = true;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — dados do titular não puderam ser exportados", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Atende ao pedido de eliminação anonimizando o cadastro — e DIZ, antes de fazer,
    /// que o prontuário fica.
    ///
    /// Prometer "apagar tudo" e manter o histórico seria mentir para o paciente por
    /// escrito. A guarda do prontuário é obrigação legal do profissional de saúde
    /// (CFM 1.821/2007) e a própria LGPD a preserva (art. 16, II); o que se apaga é a
    /// identificação. A confirmação existe para que ninguém descubra isso depois.
    /// </summary>
    [RelayCommand]
    private async Task AnonimizarAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.AnonimizarDados, "anonimizar dados do titular");

        if (PacienteId == 0) return;

        var motivo = _dialogo.PerguntarTexto(
            "Anonimizar a pedido do titular",
            $"Registre o pedido de {Nome}: quando pediu, por qual canal e quem recebeu. "
            + "É o que prova que a clínica atendeu — e ela precisa poder provar.");
        if (string.IsNullOrWhiteSpace(motivo)) return;

        if (!_dialogo.ConfirmarPerigo(
                "Anonimizar cadastro",
                $"Nome, documento, telefone, carteirinha, nascimento e foto de {Nome} serão "
                + "apagados e NÃO voltam.\n\n"
                + "O prontuário FICA: sessões, evoluções, guias e documentos continuam "
                + "guardados, sem dono identificável. A guarda do prontuário é obrigação "
                + "legal do profissional de saúde, e a LGPD a preserva expressamente "
                + "(art. 16, II) — o sistema não pode prometer apagá-lo.\n\n"
                + "Confirmar?"))
            return;

        try
        {
            ResultadoAnonimizacao resultado;
            using (var scope = _escopos.CreateScope())
            {
                var servico = scope.ServiceProvider.GetRequiredService<TitularDadosService>();
                resultado = await servico.AnonimizarAsync(
                    PacienteId, motivo, SessaoUsuario.Atual.Operador);
            }

            await CarregarAsync();
            Alterou?.Invoke();

            // O que aconteceu vem escrito, item por item: "pronto" deixaria a clínica sem
            // saber o que continua guardado se o titular perguntar amanhã.
            _dialogo.Aviso(
                "Cadastro anonimizado",
                $"O cadastro passou a se chamar \"{resultado.NomeAnonimo}\".\n"
                + $"Consentimentos revogados: {resultado.ConsentimentosRevogados}.\n"
                + $"Foto removida: {(resultado.FotoRemovida ? "sim" : "não havia")}.\n"
                + (resultado.PreservouProntuario
                    ? $"Sessões mantidas sob guarda legal: {resultado.SessoesPreservadas}."
                    : "Não havia histórico clínico a guardar."));
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — cadastro não pôde ser anonimizado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>Abre o WhatsApp do paciente direto da ficha.</summary>
    [RelayCommand]
    private void AbrirWhatsapp()
    {
        if (PacienteId == 0 || Telefone == "—") return;

        var erro = Whatsapp.Abrir(
            Telefone, Nome,
            $"Olá, {Nome.Split(' ').FirstOrDefault() ?? Nome}!");

        if (erro is null) return;
        Mensagem = erro;
        MensagemEhErro = true;
    }
}
