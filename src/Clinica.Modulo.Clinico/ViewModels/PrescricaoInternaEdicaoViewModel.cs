using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Clinico.Janelas;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using Clinica.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace Clinica.Clinico.ViewModels;

/// <summary>
/// Uma linha da prescrição sendo escrita.
///
/// O profissional escreve a prescrição livremente. Cada bloco continua sendo um item
/// de checagem; diluente, volume, via e tempo ficam visíveis ao preparar a infusão.
/// </summary>
public sealed partial class LinhaItemPrescricao : ObservableObject
{
    public string InfusaoRotulo { get; set; } = string.Empty;
    [ObservableProperty] private string _descricao = string.Empty;
    [ObservableProperty] private string? _descricaoFormatada;
    partial void OnDescricaoChanged(string value) { DescricaoFormatada = null; OnPropertyChanged(nameof(DicaDose)); }
    public string DicaDose => System.Globalization.CultureInfo.GetCultureInfo("pt-BR").CompareInfo.IndexOf(Descricao,"lidocaina",System.Globalization.CompareOptions.IgnoreCase|System.Globalization.CompareOptions.IgnoreNonSpace)>=0 ? "Quantidade em mL" : "Quantidade e medida";
    [ObservableProperty] private string? _dose;
    [ObservableProperty] private string? _diluente = "SF 0,9%";
    [ObservableProperty] private string? _volume;
    [ObservableProperty] private ViaAdministracao _via = ViaAdministracao.Endovenosa;
    [ObservableProperty] private string? _tempoInfusao = "1 hora";
    [ObservableProperty] private string? _horaPrevista;
    [ObservableProperty] private bool _seNecessario;
    [ObservableProperty] private string? _observacoes;
    [ObservableProperty] private string? _observacoesFormatadas;

    public static LinhaItemPrescricao De(ItemPrescricaoInterna item) => new()
    {
        Descricao = item.Descricao, DescricaoFormatada = item.DescricaoFormatada,
        Dose = item.Dose,
        Diluente = item.Diluente,
        Volume = item.Volume,
        Via = item.Via,
        TempoInfusao = item.TempoInfusao,
        HoraPrevista = item.HoraPrevista?.ToString("HH\\:mm"),
        SeNecessario = item.SeNecessario,
        Observacoes = item.Observacoes, ObservacoesFormatadas = item.ObservacoesFormatadas
    };

    public ItemPrescricaoInterna Para() => new()
    {
        Descricao = Descricao, DescricaoFormatada = DescricaoFormatada,
        Dose = Dose,
        Diluente = Diluente,
        Volume = Volume,
        Via = Via,
        TempoInfusao = TempoInfusao,
        // Hora vazia é o normal (numa sessão de infusão a ordem basta) e hora ilegível não
        // trava o salvamento: vira "sem horário previsto", que é o que ela significa.
        HoraPrevista = TimeOnly.TryParse(HoraPrevista, out var hora) ? hora : null,
        SeNecessario = SeNecessario,
        Observacoes = Observacoes, ObservacoesFormatadas = ObservacoesFormatadas
    };
}

/// <summary>
/// Escrever e ASSINAR a prescrição de execução interna (parcela 42).
///
/// O fluxo tem dois passos porque a assinatura é irreversível
/// ----------------------------------------------------------
/// Salvar deixa em rascunho — editável, invisível para a sala de infusão. Assinar é o que
/// põe a folha na sala, e a partir daí ela não se edita mais: corrigir passa a ser
/// suspender o item e prescrever outro. Juntar os dois num botão só faria a médica assinar
/// sem perceber que assinou.
///
/// A conferência de alergia acontece ANTES do certificado
/// ------------------------------------------------------
/// Quando há coincidência com alergia registrada, a tela mostra os achados e pede uma
/// confirmação escrita — e só depois abre a escolha do certificado. A ordem importa:
/// confirmar depois de assinar é o mesmo que não confirmar.
/// </summary>
public sealed partial class PrescricaoInternaEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly IDialogoService _dialogo;
    private readonly int _pacienteId;
    private readonly int? _profissionalId;
    private readonly int? _agendamentoId;
    private readonly int? _evolucaoId;

    public ObservableCollection<GrupoInfusaoEdicao> Infusoes { get; } = [];
    public IReadOnlyList<LinhaItemPrescricao> Itens => Infusoes.SelectMany(g => { foreach (var i in g.Itens) i.InfusaoRotulo = g.Titulo; return g.Itens; }).ToArray();
    public string AlertasTexto => string.Join("\n", Alertas);
    [ObservableProperty] private IReadOnlyList<MedicamentoSugerido> _catalogoMedicamentos = [];
    public Task Inicializacao { get; }
    private bool _carregamentoFalhou;


    /// <summary>Alergias e medicação contínua do paciente — o que se olha ANTES de escrever.</summary>
    public ObservableCollection<string> Alertas { get; } = [];

    public IReadOnlyList<ViaAdministracao> Vias { get; } = Enum.GetValues<ViaAdministracao>();

    [ObservableProperty] private string _paciente = string.Empty;
    /// <summary>
    /// Número da série anual. Fica com o aviso até a primeira gravação, porque até lá a
    /// prescrição não existe — mostrar "—" sugeriria falha de carregamento.
    /// </summary>
    [ObservableProperty] private string _numero = "(será numerada ao salvar)";
    [ObservableProperty] private string? _indicacao;
    [ObservableProperty] private string? _indicacaoFormatada;
    [ObservableProperty] private string? _observacoes;
    [ObservableProperty] private string? _observacoesFormatadas;
    [ObservableProperty] private DateTime? _dataPrescricao = DateTime.Today;
    [ObservableProperty] private string _horaPrescricao = DateTime.Now.ToString("HH:mm");

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private string _textoOperacao = "Carregando prescrição…";
    [ObservableProperty] private bool _temAlertas;

    /// <summary>A folha já foi assinada nesta janela — quem abriu recarrega a lista.</summary>
    public bool Assinou { get; private set; }

    private int _prescricaoId;

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodePrescrever => SessaoUsuario.Atual.Pode(Permissao.Prescrever);

    /// <summary>
    /// A segunda barreira, e ela DIZ por que recusou em vez de voltar calada — guarda
    /// silenciosa é botão que não faz nada (a lição da parcela 41). Devolve false quando
    /// impede.
    /// </summary>
    private bool Exigir(Permissao permissao, string acao)
    {
        if (SessaoUsuario.Atual.Pode(permissao)) return true;

        Mensagem = $"Você não tem permissão para {acao}. Peça à direção o acesso "
                 + $"\"{PerfisAcesso.Rotular(permissao)}\".";
        MensagemEhErro = true;
        return false;
    }

    /// <summary>Sem item não se assina — e o botão diz isso antes do clique.</summary>
    [ObservableProperty] private bool _continuidadeAtiva;
    public string RotuloLiberacao => ContinuidadeAtiva ? "Liberar para enfermagem" : "Assinar e enviar à sala";
    partial void OnContinuidadeAtivaChanged(bool value) => OnPropertyChanged(nameof(RotuloLiberacao));

    public bool PodeAssinar => PodePrescrever && !Ocupado && Itens.Count > 0;
    public bool PodeCopiarUltimaPrescricao => PodePrescrever && !Ocupado && !_carregamentoFalhou;

    private bool TemConteudoEmEdicao => !string.IsNullOrWhiteSpace(Indicacao)
        || !string.IsNullOrWhiteSpace(Observacoes) || Infusoes.Count > 1
        || Infusoes.Any(g => g.Diluente != "SF 0,9%" || !string.IsNullOrWhiteSpace(g.Volume)
            || g.Via != ViaAdministracao.Endovenosa || !string.IsNullOrWhiteSpace(g.Tempo)
            || !string.IsNullOrWhiteSpace(g.Horario))
        || Itens.Any(i => !string.IsNullOrWhiteSpace(i.Descricao) || !string.IsNullOrWhiteSpace(i.Dose)
            || !string.IsNullOrWhiteSpace(i.Observacoes) || i.SeNecessario);

    public async Task AplicarCopiaDaUltimaAsync(ModeloInfusao modelo)
    {
        if (_carregamentoFalhou || !Exigir(Permissao.Prescrever, "copiar a última prescrição")) return;
        // Converte e valida tudo antes de tocar no conteúdo atual.
        var grupos = GrupoInfusaoEdicao.Carregar(modelo.Itens.Select(ModeloInfusao.Para),
            modelo.DiluicaoUnica, modelo.DiluenteGlobal, modelo.VolumeTotal);
        if (grupos.Count == 0)
        {
            Mensagem = "A última prescrição emitida não possui itens ativos para copiar.";
            MensagemEhErro = false;
            return;
        }
        if (TemConteudoEmEdicao && !await DialogosDaSessao.ConfirmarPerigoAsync(_dialogo, "Copiar última prescrição",
            "Substituir o conteúdo em edição pela última prescrição emitida deste paciente?")) return;
        Indicacao = modelo.Indicacao; IndicacaoFormatada = modelo.IndicacaoFormatada;
        Observacoes = modelo.Observacoes; ObservacoesFormatadas = modelo.ObservacoesFormatadas;
        Infusoes.Clear();
        foreach (var grupo in grupos) Infusoes.Add(grupo);
        Renumerar();
        Mensagem = "Última prescrição copiada. Revise os medicamentos, preparos e horários antes de salvar e liberar. Nada foi gravado.";
        MensagemEhErro = false;
    }

    [RelayCommand]
    private async Task CopiarUltimaPrescricaoAsync()
    {
        if (!PodeCopiarUltimaPrescricao || !Exigir(Permissao.Prescrever, "copiar a última prescrição")) return;
        try
        {
            Ocupado = true; TextoOperacao = "Carregando última prescrição…";
            using var scope = _escopos.CreateScope();
            var modelo = await scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>()
                .UltimaComoModeloAsync(_pacienteId);
            if (modelo is not null) await AplicarCopiaDaUltimaAsync(modelo);
            else { Mensagem = "Não há prescrição de infusão emitida deste paciente para copiar."; MensagemEhErro = false; }
        }
        catch (Exception ex) { Mensagem = ex.Message; MensagemEhErro = true; }
        finally { Ocupado = false; }
    }

    /// <param name="prescricaoId">
    /// Rascunho existente a REABRIR. Nulo cria uma prescrição nova.
    ///
    /// Sem este parâmetro, salvar um rascunho e fechar a janela deixava a prescrição
    /// inalcançável: a lista só oferecia "Abrir", que leva à folha de EXECUÇÃO (a tela da
    /// enfermagem), e não havia caminho de volta para a edição. <c>PrescricaoInterna
    /// .PodeEditar</c> existe no domínio desde a parcela 42 dizendo que rascunho se edita —
    /// e nenhuma tela oferecia isso.
    /// </param>
    public PrescricaoInternaEdicaoViewModel(
        IServiceScopeFactory escopos, IDialogoService dialogo,
        int pacienteId, string paciente, int? profissionalId, int? agendamentoId = null,
        int? prescricaoId = null, int? evolucaoId = null)
    {
        _escopos = escopos;
        _dialogo = dialogo;
        _pacienteId = pacienteId;
        _profissionalId = profissionalId;
        _agendamentoId = agendamentoId;
        _evolucaoId = evolucaoId;
        _prescricaoId = prescricaoId ?? 0;
        Paciente = paciente;

        Infusoes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(PodeAssinar));

        // Só a prescrição NOVA nasce com uma linha em branco: reabrir um rascunho e ganhar
        // um item vazio no fim faria a folha impressa sair com uma linha fantasma se
        // ninguém reparasse.
        if (_prescricaoId == 0) CriarInfusao();

        Inicializacao = InicializarAsync();
    }

    public ObservableCollection<ModeloDocumento> Modelos {get;}=[];
    [ObservableProperty] private string _buscaModeloWeb=string.Empty;
    public IReadOnlyList<ModeloDocumento> ModelosDisponiveisWeb => Modelos.Where(m=>System.Globalization.CultureInfo.GetCultureInfo("pt-BR").CompareInfo.IndexOf(m.Nome,BuscaModeloWeb.Trim(),System.Globalization.CompareOptions.IgnoreCase|System.Globalization.CompareOptions.IgnoreNonSpace)>=0).ToArray();
    partial void OnBuscaModeloWebChanged(string value)=>OnPropertyChanged(nameof(ModelosDisponiveisWeb));

    [ObservableProperty] private ModeloDocumento? _modeloSelecionado;
    public string PreviaModelo => ModeloSelecionado?.Corpo??"Escolha um modelo para conferir o conteúdo.";
    partial void OnModeloSelecionadoChanged(ModeloDocumento? value)=>OnPropertyChanged(nameof(PreviaModelo));
    [RelayCommand]
    private async Task UsarModeloWebAsync(GrupoInfusaoEdicao? grupo)
    {
        if (grupo is null || !Infusoes.Contains(grupo)) return;
        if (ModeloSelecionado is null || !Modelos.Contains(ModeloSelecionado))
        { Mensagem = "Selecione um modelo antes de aplicá-lo."; MensagemEhErro = true; return; }
        await AplicarModeloAsync(grupo, ModeloSelecionado);
    }
    [RelayCommand]
    private async Task SalvarModeloWebAsync(GrupoInfusaoEdicao? grupo)
    {
        if (grupo is null || !Infusoes.Contains(grupo)) return;
        var nome = await DialogosDaSessao.PerguntarTextoAsync(_dialogo, "Salvar infusão como modelo", "Nome do modelo. Guarda o preparo e os medicamentos, sem dados do paciente ou horário.");
        if (nome is null) return;
        await SalvarModeloAsync(grupo, nome.Trim());
    }
    private async Task CarregarModelosAsync() {
        try {
            using var scope=_escopos.CreateScope();
            var modelos=await scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>().ModelosAsync();
            Modelos.Clear();foreach(var m in modelos.Where(m=>m.Ativo&&m.ParaInfusao))Modelos.Add(m);
        } catch(Exception ex){Mensagem="Não foi possível carregar os modelos: "+ex.Message;MensagemEhErro=true;}
    }
    private async Task InicializarAsync()
    {
        await PrepararAsync();
        await CarregarModelosAsync();
        await RecarregarMedicamentosAsync();
    }
    [RelayCommand]
    private async Task RecarregarMedicamentosAsync()
    {
        try
        {
            using var scope=_escopos.CreateScope();
            CatalogoMedicamentos=BuscaMedicamentos.Catalogo(await scope.ServiceProvider.GetRequiredService<MedicamentoCatalogoService>().ListarAsync());
        }
        catch(Exception ex) { Mensagem="A busca de medicamentos não pôde carregar. Você pode escrever livremente. "+ex.Message;MensagemEhErro=true; }
    }
    public async Task AplicarModeloAsync(GrupoInfusaoEdicao grupo, ModeloDocumento selecionado)
    {
        if(Ocupado||!Infusoes.Contains(grupo)||!Exigir(Permissao.Prescrever,"usar modelos"))return;
        try
        {
            var modelo=ModeloInfusao.Ler(selecionado.ConfiguracaoInfusao);
            var novos=GrupoInfusaoEdicao.Carregar(modelo.Itens.Select(ModeloInfusao.Para),modelo.DiluicaoUnica,modelo.DiluenteGlobal,modelo.VolumeTotal);
            if(novos.Count==0)throw new InvalidOperationException("Modelo sem medicamentos.");
            if(grupo.Itens.Any(i=>!string.IsNullOrWhiteSpace(i.Descricao)) && !await DialogosDaSessao.ConfirmarPerigoAsync(_dialogo, "Usar modelo","Substituir os medicamentos e o preparo desta infusão?"))return;
            var indice=Infusoes.IndexOf(grupo);Infusoes.RemoveAt(indice);
            foreach(var g in novos)Infusoes.Insert(indice++,g);
            Renumerar();Mensagem="Modelo aplicado. Revise o preparo e as doses antes de liberar.";MensagemEhErro=false;
        }catch(Exception ex){Mensagem=ex.Message;MensagemEhErro=true;}
    }
    public async Task<bool> SalvarModeloAsync(GrupoInfusaoEdicao grupo,string nome)
    {
        if(Ocupado||!Infusoes.Contains(grupo)||!Exigir(Permissao.Prescrever,"salvar modelos"))return false;
        try
        {
            Ocupado=true;TextoOperacao="Salvando modelo…";
            var itens=grupo.Preparar().ToArray();
            foreach(var i in itens)i.GrupoInfusao=1;
            // O modelo guarda a composição, sem indicação, paciente, horário ou observação clínica.
            foreach(var i in itens){i.HoraPrevista=null;i.Observacoes=null;i.ObservacoesFormatadas=null;}
            var config=new ModeloInfusao(null,null,itens.Select(ModeloInfusao.De).ToArray()).Guardar();
            using var scope=_escopos.CreateScope();
            await scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>().SalvarModeloAsync(new() {
                Nome=nome,Tipo=TipoDocumentoClinico.Receita,ParaInfusao=true,ConfiguracaoInfusao=config,
                Corpo=string.Join("\n",itens.Select(i=>i.Descricao)),Ativo=true
            },SessaoUsuario.Atual.Operador,substituirPorNome:false);
            await CarregarModelosAsync();Mensagem="Modelo salvo para reutilizar na clínica.";MensagemEhErro=false;return true;
        }catch(Exception ex){Mensagem=ex.Message;MensagemEhErro=true;return false;}finally{Ocupado=false;}
    }

    partial void OnOcupadoChanged(bool value)
    {
        OnPropertyChanged(nameof(PodeAssinar));
        OnPropertyChanged(nameof(PodeCopiarUltimaPrescricao));
    }

    /// <summary>
    /// Carrega o contexto clínico do paciente e, quando se está REABRINDO, o rascunho.
    /// Prescrição nova não é criada aqui — ver o comentário adiante.
    /// </summary>
    private async Task PrepararAsync()
    {
        try
        {
            Ocupado = true;

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>();

            ContinuidadeAtiva = await ContinuidadeSemAssinatura.HabilitadaAsync(scope.ServiceProvider.GetRequiredService<IClinicaRepositorio>());
            // A prescrição NOVA não é criada aqui, e isso é decisão (parcela 45): a
            // criação assinala um NÚMERO da série anual (PRE 2026/0001) e grava a linha.
            // Fazer isso na abertura da janela significava que abrir e desistir deixava
            // um rascunho vazio numerado na lista da clínica — e, agora que o rascunho
            // pode ser reaberto, esses órfãos passariam a ser visíveis e clicáveis.
            // Quem cria é o primeiro salvamento, que é quando existe algo para numerar.
            if (_prescricaoId == 0)
            {
                await CarregarContextoAsync(servico);
                return;
            }

            var prescricao = await servico.ObterAsync(_prescricaoId)
                ?? throw new InvalidOperationException("Prescrição não encontrada.");

            // Assinada não se edita: os bytes já foram selados, e deixar a tela abrir para
            // edição faria a médica digitar por nada e descobrir na hora de salvar.
            if (!prescricao.PodeEditar)
                throw new InvalidOperationException(
                    $"A prescrição {prescricao.Numero} já foi assinada e não pode mais ser "
                    + "editada. Cancele e emita outra, se for o caso.");

            _prescricaoId = prescricao.Id;
            Numero = prescricao.Numero;
            DataPrescricao = prescricao.Data.ToDateTime(TimeOnly.MinValue);
            HoraPrescricao = prescricao.Hora.ToString("HH:mm");

            Indicacao=prescricao.Indicacao;IndicacaoFormatada=prescricao.IndicacaoFormatada;
            Observacoes=prescricao.Observacoes;ObservacoesFormatadas=prescricao.ObservacoesFormatadas;
            Infusoes.Clear();
            foreach(var g in GrupoInfusaoEdicao.Carregar(prescricao.Itens,prescricao.DiluicaoUnica,prescricao.DiluenteGlobal,prescricao.VolumeTotal))Infusoes.Add(g);
            if(Infusoes.Count==0)CriarInfusao();
            Renumerar();

            await CarregarContextoAsync(servico);
        }
        catch (Exception ex)
        {
            _carregamentoFalhou=true;
            Application.Diagnostico.Registrar(
                "Consultório — rascunho de prescrição não pôde ser criado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>Alergias e medicação contínua do paciente — o que a tela avisa antes de prescrever.</summary>
    private async Task CarregarContextoAsync(PrescricaoInternaService servico)
    {
        var contexto = await servico.ContextoAsync(_pacienteId);

        Alertas.Clear();
        if (ProblemaPacienteService.ResumirAlergias(contexto.Alergias) is { } alergias)
            Alertas.Add(alergias);
        foreach (var medicacao in contexto.MedicacoesEmUso)
            Alertas.Add($"Em uso contínuo: {medicacao.Descricao}");

        TemAlertas = Alertas.Count > 0;
    }

    private void Renumerar()
    {
        for(var n=0;n<Infusoes.Count;n++)Infusoes[n].Numero=n+1;
        OnPropertyChanged(nameof(PodeAssinar));
    }
    [RelayCommand]
    private void CriarInfusao()
    {
        if(Infusoes.Count>=50){Mensagem="Limite de 50 infusões por prescrição.";MensagemEhErro=true;return;}
        var grupo=new GrupoInfusaoEdicao();grupo.Itens.Add(new LinhaItemPrescricao());Infusoes.Add(grupo);Renumerar();
    }
    [RelayCommand]
    private async Task RemoverInfusaoAsync(GrupoInfusaoEdicao? grupo)
    {
        if(grupo is null)return;
        if(grupo.Itens.Any(i=>!string.IsNullOrWhiteSpace(i.Descricao))&&!await DialogosDaSessao.ConfirmarPerigoAsync(_dialogo, "Remover infusão","Remover esta infusão do rascunho em edição?"))return;
        Infusoes.Remove(grupo);Renumerar();
    }
    [RelayCommand]
    private void AcrescentarItem(GrupoInfusaoEdicao? grupo)
    {
        if(grupo is null)return;grupo.Itens.Add(new LinhaItemPrescricao());OnPropertyChanged(nameof(PodeAssinar));
    }
    [RelayCommand]
    private void RemoverItem(LinhaItemPrescricao? linha)
    {
        if(linha is null)return;
        Infusoes.FirstOrDefault(g=>g.Itens.Contains(linha))?.Itens.Remove(linha);OnPropertyChanged(nameof(PodeAssinar));
    }

    [RelayCommand]
    private async Task SalvarRascunhoAsync()
    {
        // A barreira que IMPEDE. `PodePrescrever` é só a metade VISÍVEL, e as duas são
        // obrigatórias: atalho de teclado e corrida de carregamento chegam aqui sem passar
        // pelo `IsEnabled`. Numa tela que gera prescrição médica, só desabilitar é enfeite.
        if (!Exigir(Permissao.Prescrever, "salvar a prescrição de infusão")) return;

        if (await GravarAsync() is null) return;
        Mensagem = "Rascunho salvo. Use Liberar para enfermagem para enviar à sala.";
        MensagemEhErro = false;
    }

    /// <summary>
    /// Confere, pede confirmação se houver alergia, escolhe o certificado e assina.
    /// </summary>
    [RelayCommand]
    private async Task AssinarAsync()
    {
        if (!Exigir(Permissao.Prescrever, "assinar a prescrição de infusão")) return;

        // Guardas que DIZEM por que não dá. O botão já nasce apagado nos dois casos, mas
        // atalho de teclado e corrida de carregamento chegam aqui — e guarda que volta em
        // silêncio é botão que não faz nada.
        if (Itens.Count == 0)
        {
            Mensagem = "Acrescente ao menos um item antes de assinar: uma folha assinada em "
                     + "branco vira espaço para alguém escrever uma linha depois.";
            MensagemEhErro = true;
            return;
        }

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.Prescrever, "prescrever");

            if (await GravarAsync() is null) return;

            TextoOperacao = "Assinando e arquivando prescrição…";
            Ocupado = true;

            var confirmouAlergia = false;

            using (var scope = _escopos.CreateScope())
            {
                var servico = scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>();
                var conferencia = await servico.ConferirParaAssinaturaAsync(_prescricaoId);

                if (conferencia.ExigeConfirmacao)
                {
                    var achados = conferencia.Alertas
                        .Where(a => a.Gravidade == GravidadePrescricao.Alergia)
                        .Select(a => $"• {a.Item}\n  {a.Motivo}");

                    confirmouAlergia = await DialogosDaSessao.ConfirmarPerigoAsync(_dialogo,
                        "Alergia registrada",
                        "Há item prescrito que bate com alergia registrada deste paciente:\n\n"
                        + string.Join("\n\n", achados)
                        + "\n\nO sistema não impede — quem decide é quem assina. Confirma "
                        + "que viu os alertas e quer prosseguir?");

                    if (!confirmouAlergia)
                    {
                        Mensagem = "Assinatura cancelada. Corrija os itens ou revise a lista "
                                 + "de problemas do paciente.";
                        MensagemEhErro = false;
                        return;
                    }
                }
            }

            if (ContinuidadeAtiva)
            {
                using var scope = _escopos.CreateScope();
                await scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>()
                    .LiberarSemAssinaturaAsync(_prescricaoId, SessaoUsuario.Atual.UsuarioId, confirmouAlergia);
                Assinou = false;
                Fechar?.Invoke();
                return;
            }
            using var certificado = await EscolherCertificadoWindow.PerguntarAsync(
                $"Prescrição {Numero} — {Paciente}",
                System.Windows.Application.Current?.MainWindow, _escopos);

            if (certificado is null)
            {
                // Diálogo cancelado: sair calado é o certo aqui, e é a exceção que a
                // regra do "botão que não faz nada" prevê para guarda sobre variável local.
                return;
            }

            using (var scope = _escopos.CreateScope())
            {
                var assinaturas = scope.ServiceProvider
                    .GetRequiredService<AssinaturaDePrescricaoService>();

                // UsuarioId é 0 quando não há login (o shell só chega aqui autenticado,
                // mas `SessaoUsuario` libera sem sessão de propósito). Gravar 0 numa chave
                // estrangeira quebraria a inserção — nulo é o que "sem login" significa.
                await assinaturas.AssinarPrescricaoAsync(
                    _prescricaoId, certificado, confirmouAlergia,
                    SessaoUsuario.Atual.Autenticado ? SessaoUsuario.Atual.UsuarioId : null,
                    SessaoUsuario.Atual.Operador);
            }

            Assinou = true;
            Fechar?.Invoke();
        }
        catch (Exception ex)
        {
            Application.Diagnostico.Registrar(
                "Consultório — prescrição não pôde ser assinada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>Grava o rascunho. Devolve null quando recusou — o chamador não segue.</summary>
    private async Task<PrescricaoInterna?> GravarAsync()
    {
        if (_carregamentoFalhou) { Mensagem="Reabra a prescrição: o carregamento anterior falhou.";MensagemEhErro=true;return null; }
        try
        {
            if(Infusoes.Count==0)throw new InvalidOperationException("Crie ao menos uma infusão.");
            var itens=Infusoes.SelectMany(g=>g.Preparar()).ToArray();
            TextoOperacao = "Salvando prescrição de infusão…";
            Ocupado = true;
            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>();

            // Primeira gravação: é AQUI que a prescrição passa a existir e ganha número.
            if (_prescricaoId == 0)
            {
                var criada = await servico.CriarAsync(
                    _pacienteId, _profissionalId, _agendamentoId,
                    evolucaoId: _evolucaoId,
                    operador: SessaoUsuario.Atual.Operador);

                _prescricaoId = criada.Id;
                Numero = criada.Numero;
            }

            if (DataPrescricao is not { } dataPrescricao || !TimeOnly.TryParse(HoraPrescricao, out var horaPrescricao))
                throw new InvalidOperationException("Informe a data e a hora da prescrição.");
            var salva = await servico.SalvarRascunhoAsync(
                _prescricaoId, Indicacao, Observacoes, itens,
                SessaoUsuario.Atual.Operador,
                exigeAssinaturaEletronicaDaExecucao: false,indicacaoFormatada:IndicacaoFormatada,observacoesFormatadas:ObservacoesFormatadas,
                dataPrescricao:DateOnly.FromDateTime(dataPrescricao),horaPrescricao:horaPrescricao,diluicaoUnica:false,diluenteGlobal:null,volumeTotal:null);

            Mensagem = null;
            MensagemEhErro = false;
            return salva;
        }
        catch (Exception ex)
        {
            Application.Diagnostico.Registrar(
                "Consultório — rascunho de prescrição não pôde ser salvo", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
            return null;
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>A janela liga isto ao próprio fechamento — o VM não conhece WPF.</summary>
    public Action? Fechar { get; set; }
}
