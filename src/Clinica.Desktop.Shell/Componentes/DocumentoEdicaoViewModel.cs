using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Domain.Entities;
using Clinica.Desktop.Shell.Componentes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Uma linha do documento em edição (medicamento + posologia, exame + indicação).</summary>
public sealed partial class LinhaItemDocumento : ObservableObject
{
    [ObservableProperty] private string _descricao = string.Empty;
    [ObservableProperty] private string? _quantidade;
    [ObservableProperty] private string? _detalhe;
}

/// <summary>
/// Emissão de um documento ESCRITO pelo profissional: receita, atestado, declaração de
/// comparecimento e pedido de exame.
///
/// Os outros três da página 21 (relatório de evolução, termo de consentimento e
/// anamnese) não passam por aqui porque não são escritos: o sistema os monta do
/// prontuário, e a ficha do paciente os emite num clique. Pedir para alguém digitar o
/// que o banco já sabe é o caminho mais curto para o documento sair errado.
/// </summary>
public sealed partial class DocumentoEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _pacienteId;

    /// <summary>Os tipos que se escrevem à mão — os montados do prontuário ficam de fora.</summary>
    public IReadOnlyList<TipoDocumentoClinico> Tipos { get; } =
    [
        TipoDocumentoClinico.Receita,
        TipoDocumentoClinico.Atestado,
        TipoDocumentoClinico.Comparecimento,
        TipoDocumentoClinico.PedidoExame
    ];

    public ObservableCollection<Profissional> Profissionais { get; } = [];
    public ObservableCollection<LinhaItemDocumento> Itens { get; } = [];

    // ===================== Conferência clínica (parcela 40) =====================
    //
    // O sistema guarda as ALERGIAS do paciente desde a parcela 37 e a emissão de receita
    // nunca as consultou: a base sabia que a paciente é alérgica a dipirona, o
    // profissional escrevia "Dipirona 500mg" e o papel saía sem uma palavra.
    //
    // A conferência mora AQUI, no shell, e não na tela do consultório, porque este é o
    // único lugar por onde toda receita passa — nas duas portas (Recepção e Consultório).
    // Checagem de segurança que só existe em uma delas é o defeito de novo, com a
    // agravante de dar a impressão de estar coberto.

    /// <summary>Alergias e medicação contínua do paciente — contexto permanente da tela.</summary>
    public ObservableCollection<string> AlertasClinicos { get; } = [];

    /// <summary>O que a conferência achou ao comparar os itens escritos com as alergias.</summary>
    public ObservableCollection<string> ColisoesAlergia { get; } = [];

    public bool TemAlertasClinicos => AlertasClinicos.Count > 0;

    /// <summary>
    /// Um item escrito bate com alergia registrada. Não impede — exige que alguém diga
    /// que viu (a caixa de confirmação abaixo). É o segundo caso do projeto em que a tela
    /// cobra confirmação explícita; o primeiro é a divergência do fechamento de caixa.
    /// </summary>
    [ObservableProperty] private bool _colideComAlergia;

    /// <summary>O profissional marcou que viu o alerta e assume a prescrição.</summary>
    [ObservableProperty] private bool _alergiaConferida;

    partial void OnColideComAlergiaChanged(bool value)
    {
        // Some o alerta, some a confirmação: deixar a caixa marcada de uma conferência
        // anterior faria a próxima receita passar com um "eu vi" que ninguém deu.
        if (!value) AlergiaConferida = false;
        EmitirCommand.NotifyCanExecuteChanged();
    }

    partial void OnAlergiaConferidaChanged(bool value) => EmitirCommand.NotifyCanExecuteChanged();

    /// <summary>Enquanto houver colisão não confirmada, o botão de emitir fica apagado.</summary>
    private bool PodeEmitir() => !Emitindo && (!ColideComAlergia || AlergiaConferida);

    /// <summary>
    /// Relê o contexto clínico e reconfere os itens escritos.
    ///
    /// Falha aqui NÃO derruba a emissão nem a bloqueia: a conferência é uma ajuda, e um
    /// banco lento não pode impedir alguém de dar um atestado. Mas ela também não pode
    /// falhar em silêncio fingindo que está tudo certo — por isso o erro vai ao log e a
    /// tela avisa que a conferência não rodou.
    /// </summary>
    private async Task ConferirClinicamenteAsync()
    {
        try
        {
            var escritos = Itens
                .Where(i => !string.IsNullOrWhiteSpace(i.Descricao))
                .Select(i => $"{i.Descricao} {i.Detalhe}".Trim())
                .ToList();

            using var scope = _escopos.CreateScope();
            var prescricao = scope.ServiceProvider.GetRequiredService<PrescricaoService>();
            var conferencia = await prescricao.ConferirAsync(_pacienteId, escritos);

            AlertasClinicos.Clear();
            foreach (var alergia in conferencia.Alergias)
                AlertasClinicos.Add($"ALERGIA: {alergia.Descricao}");
            foreach (var medicacao in conferencia.MedicacoesEmUso)
                AlertasClinicos.Add($"Em uso contínuo: {medicacao.Descricao}");

            ColisoesAlergia.Clear();
            foreach (var a in conferencia.Alertas.Where(
                         a => a.Gravidade == GravidadePrescricao.Alergia))
                ColisoesAlergia.Add($"\u201C{a.Item}\u201D \u2014 {a.Motivo}");

            ColideComAlergia = conferencia.ExigeConfirmacao;
            OnPropertyChanged(nameof(TemAlertasClinicos));
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Documento clínico — conferência de alergia não pôde ser feita", ex);

            AlertasClinicos.Clear();
            AlertasClinicos.Add(
                "Não foi possível conferir as alergias deste paciente agora — confira na "
                + "lista de problemas antes de prescrever.");
            ColideComAlergia = false;
            OnPropertyChanged(nameof(TemAlertasClinicos));
        }
    }
    public ObservableCollection<ModeloDocumento> Modelos { get; } = [];

    [ObservableProperty] private TipoDocumentoClinico _tipoSelecionado = TipoDocumentoClinico.Receita;
    [ObservableProperty] private Profissional? _profissional;
    [ObservableProperty] private DateTime _data = DateTime.Today;
    [ObservableProperty] private string? _titulo;
    [ObservableProperty] private string? _corpo;
    [ObservableProperty] private string? _observacoes;

    [ObservableProperty] private string? _diasAfastamentoTexto;
    [ObservableProperty] private string? _cid;
    [ObservableProperty] private bool _cidAutorizado;

    [ObservableProperty] private DateTime? _periodoInicio;
    [ObservableProperty] private DateTime? _periodoFim;
    [ObservableProperty] private string? _horaChegadaTexto;
    [ObservableProperty] private string? _horaSaidaTexto;

    [ObservableProperty] private ModeloDocumento? _modeloSelecionado;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _emitindo;

    // Sem isto o botão continuaria apagado depois da emissão: `PodeEmitir` lê `Emitindo`,
    // e o gerador só reavalia o comando quando alguém avisa.
    partial void OnEmitindoChanged(bool value) => EmitirCommand.NotifyCanExecuteChanged();

    public string TituloJanela => $"Emitir {TipoDocumentoInfo.Rotular(TipoSelecionado).ToLowerInvariant()}";

    /// <summary>Receita e pedido de exame são listas; atestado e declaração, não.</summary>
    public bool MostraItens => TipoDocumentoInfo.ExigeItens(TipoSelecionado);

    public bool MostraAtestado => TipoSelecionado == TipoDocumentoClinico.Atestado;

    public bool MostraComparecimento => TipoSelecionado == TipoDocumentoClinico.Comparecimento;

    public string RotuloItens => TipoSelecionado == TipoDocumentoClinico.Receita
        ? "Medicamentos e orientações"
        : "Exames solicitados";

    public string RotuloDetalhe => TipoSelecionado == TipoDocumentoClinico.Receita
        ? "Posologia"
        : "Indicação clínica";

    /// <summary>
    /// O CID preenchido SEM autorização não sai impresso. A tela avisa antes de a
    /// secretária entregar o papel achando que o diagnóstico foi junto.
    /// </summary>
    public bool AvisaCidOmitido => !string.IsNullOrWhiteSpace(Cid) && !CidAutorizado;

    /// <summary>Id do documento emitido — a tela dona usa para reimprimir.</summary>
    public int DocumentoEmitidoId { get; private set; }

    public event Action? Concluido;

    public DocumentoEdicaoViewModel(
        IServiceScopeFactory escopos, int pacienteId,
        TipoDocumentoClinico tipoInicial = TipoDocumentoClinico.Receita)
    {
        _escopos = escopos;
        _pacienteId = pacienteId;
        _tipoSelecionado = tipoInicial;
        Itens.Add(new LinhaItemDocumento());
        _ = CarregarAsync();
    }

    partial void OnTipoSelecionadoChanged(TipoDocumentoClinico value)
    {
        OnPropertyChanged(nameof(TituloJanela));
        OnPropertyChanged(nameof(MostraItens));
        OnPropertyChanged(nameof(MostraAtestado));
        OnPropertyChanged(nameof(MostraComparecimento));
        OnPropertyChanged(nameof(RotuloItens));
        OnPropertyChanged(nameof(RotuloDetalhe));
        _ = CarregarModelosAsync();
    }

    partial void OnCidChanged(string? value) => OnPropertyChanged(nameof(AvisaCidOmitido));
    partial void OnCidAutorizadoChanged(bool value) => OnPropertyChanged(nameof(AvisaCidOmitido));

    private async Task CarregarAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var equipe = scope.ServiceProvider.GetRequiredService<EquipeService>();

            Profissionais.Clear();
            foreach (var p in await equipe.ProfissionaisAtivosAsync()) Profissionais.Add(p);
            Profissional = Profissionais.FirstOrDefault();

            await CarregarModelosAsync();

            // O contexto clínico vale com a receita ainda em branco: é o que se olha
            // ANTES de escrever, não uma validação de saída.
            await ConferirClinicamenteAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Documento clínico — tela de documento não pôde ser carregada", ex);
            Erro($"Não foi possível carregar a tela: {ex.Message}");
        }
    }

    private async Task CarregarModelosAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var documentos = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();

            Modelos.Clear();
            foreach (var m in await documentos.ModelosAsync(TipoSelecionado))
                Modelos.Add(m);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Documento clínico — modelos de documento não puderam ser lidos", ex);
        }
    }

    [RelayCommand]
    private void AdicionarItem() => Itens.Add(new LinhaItemDocumento());

    [RelayCommand]
    private void RemoverItem(LinhaItemDocumento? item)
    {
        if (item is null) return;
        Itens.Remove(item);
        if (Itens.Count == 0) Itens.Add(new LinhaItemDocumento());
    }

    /// <summary>Traz texto e linhas do modelo escolhido, substituindo o que estiver na tela.</summary>
    [RelayCommand]
    private void AplicarModelo()
    {
        if (ModeloSelecionado is not { } modelo) return;

        Titulo = modelo.Titulo;
        Corpo = modelo.Corpo;

        Itens.Clear();
        foreach (var i in modelo.Itens.OrderBy(i => i.Ordem).ThenBy(i => i.Id))
            Itens.Add(new LinhaItemDocumento
            {
                Descricao = i.Descricao,
                Quantidade = i.Quantidade,
                Detalhe = i.Detalhe
            });

        if (Itens.Count == 0) Itens.Add(new LinhaItemDocumento());
        Informar($"Modelo \"{modelo.Nome}\" aplicado.");
    }

    /// <summary>
    /// Guarda o que está na tela como modelo. Nome repetido sobrescreve: quem salva duas
    /// vezes com o mesmo nome está corrigindo o modelo, não criando um gêmeo.
    /// </summary>
    [RelayCommand]
    private async Task SalvarComoModeloAsync(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            Erro("Dê um nome ao modelo antes de guardá-lo.");
            return;
        }

        try
        {
            using var scope = _escopos.CreateScope();
            var documentos = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();

            var modelo = new ModeloDocumento
            {
                Tipo = TipoSelecionado,
                Nome = nome!,
                Titulo = Titulo,
                Corpo = Corpo
            };

            foreach (var i in Itens.Where(i => !string.IsNullOrWhiteSpace(i.Descricao)))
                modelo.Itens.Add(new ItemModelo
                {
                    Descricao = i.Descricao,
                    Quantidade = i.Quantidade,
                    Detalhe = i.Detalhe
                });

            await documentos.SalvarModeloAsync(modelo, SessaoUsuario.Atual.Operador);
            await CarregarModelosAsync();
            Informar($"Modelo \"{modelo.Nome}\" guardado.");
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Documento clínico — modelo de documento não pôde ser guardado", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>
    /// Apaga o modelo escolhido.
    ///
    /// Modelo NÃO é documento: ele não registra nada que aconteceu — é rascunho de apoio,
    /// e por isso se apaga mesmo (o documento emitido, esse, só se cancela com motivo).
    /// Sem esta porta, a lista só crescia: o modelo criado com o nome errado, ou o da
    /// conduta que a clínica deixou de usar, ficava no combo para sempre.
    ///
    /// Os documentos já emitidos a partir dele não mudam: a emissão COPIA o conteúdo.
    /// </summary>
    [RelayCommand]
    private async Task ExcluirModeloAsync()
    {
        if (ModeloSelecionado is not { } modelo) return;

        try
        {
            using var scope = _escopos.CreateScope();
            var dialogo = scope.ServiceProvider.GetRequiredService<IDialogoService>();

            // O botão fica ao lado do "Aplicar modelo": sem a pergunta, o clique errado
            // apaga o modelo que a clínica usa todo dia.
            if (!dialogo.ConfirmarPerigo("Apagar modelo",
                    $"Apagar o modelo \"{modelo.Nome}\"? "
                    + "Os documentos já emitidos com ele NÃO mudam — a emissão copia o conteúdo."))
                return;

            var documentos = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();
            await documentos.ExcluirModeloAsync(modelo.Id);

            ModeloSelecionado = null;
            await CarregarModelosAsync();
            Informar($"Modelo \"{modelo.Nome}\" apagado. Os documentos já emitidos com ele não mudam.");
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Documento clínico — modelo de documento não pôde ser apagado", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>Emite o documento, gera o PDF e abre para impressão.</summary>
    [RelayCommand(CanExecute = nameof(PodeEmitir))]
    private async Task EmitirAsync()
    {
        if (Emitindo) return;

        Mensagem = string.Empty;
        MensagemEhErro = false;

        // Reconfere com o que está escrito AGORA. A conferência da abertura viu uma
        // receita em branco, e quem digitou o alérgeno depois dela passaria direto.
        await ConferirClinicamenteAsync();

        if (ColideComAlergia && !AlergiaConferida)
        {
            Erro("Esta prescrição bate com uma alergia registrada do paciente. "
                 + "Confira o alerta e marque que você o viu antes de emitir.");
            return;
        }

        Emitindo = true;

        try
        {
            var dados = MontarDocumento();

            byte[] pdf;
            string numero;

            using (var scope = _escopos.CreateScope())
            {
                var documentos = scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>();
                var pdfs = scope.ServiceProvider.GetRequiredService<DocumentosClinicosPdfService>();
                var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();

                var emitido = await documentos.EmitirAsync(dados, SessaoUsuario.Atual.Operador);
                DocumentoEmitidoId = emitido.Id;
                numero = emitido.Numero;

                pdf = await pdfs.GerarAsync(emitido.Id, await parametros.ObterPrestadorAsync());
            }

            // O documento JÁ está emitido: uma falha daqui para a frente é de impressão,
            // não de emissão — e a tela precisa dizer isso, senão alguém emite de novo.
            var erro = await ImpressaoPdf.SalvarEAbrirAsync(
                pdf, ImpressaoPdf.NomeSeguro(
                    $"{TipoDocumentoInfo.Rotular(TipoSelecionado)}-{numero.Replace('/', '-')}.pdf"));

            if (erro is not null)
            {
                Erro($"{erro} O documento {numero} foi emitido e está na ficha do paciente.");
                return;
            }

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Documento clínico — documento clínico não pôde ser emitido", ex);
            Erro(ex.Message);
        }
        finally
        {
            Emitindo = false;
        }
    }

    private DocumentoClinico MontarDocumento()
    {
        var documento = new DocumentoClinico
        {
            Tipo = TipoSelecionado,
            PacienteId = _pacienteId,
            ProfissionalId = Profissional?.Id,
            Data = DateOnly.FromDateTime(Data),
            Titulo = Titulo,
            Corpo = Corpo,
            Observacoes = Observacoes
        };

        if (MostraItens)
            foreach (var i in Itens.Where(i => !string.IsNullOrWhiteSpace(i.Descricao)))
                documento.Itens.Add(new ItemDocumento
                {
                    Descricao = i.Descricao,
                    Quantidade = i.Quantidade,
                    Detalhe = i.Detalhe
                });

        if (MostraAtestado)
        {
            if (!string.IsNullOrWhiteSpace(DiasAfastamentoTexto))
            {
                if (!int.TryParse(DiasAfastamentoTexto, out var dias))
                    throw new InvalidOperationException("Os dias de afastamento devem ser um número.");
                documento.DiasAfastamento = dias;
            }

            documento.Cid = Cid;
            documento.CidAutorizado = CidAutorizado;
        }

        if (MostraAtestado || MostraComparecimento)
        {
            documento.PeriodoInicio = PeriodoInicio is { } de ? DateOnly.FromDateTime(de) : null;
            documento.PeriodoFim = PeriodoFim is { } ate ? DateOnly.FromDateTime(ate) : null;
        }

        if (MostraComparecimento)
        {
            documento.HoraChegada = LerHora(HoraChegadaTexto, "chegada");
            documento.HoraSaida = LerHora(HoraSaidaTexto, "saída");
        }

        return documento;
    }

    private static TimeOnly? LerHora(string? texto, string qual)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        if (TimeOnly.TryParse(texto, out var hora)) return hora;
        throw new InvalidOperationException($"A hora de {qual} não foi entendida — use 14:30.");
    }

    private void Informar(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = false;
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}
