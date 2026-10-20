using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Application.Modelos;

/// <summary>
/// Uma linha da prescrição sendo escrita (subiu do <c>LinhaItemPrescricao</c> WPF, out/2026, sem
/// mudança de regra). O profissional escreve a prescrição livremente; cada bloco continua sendo
/// um item de checagem, e diluente, volume, via e tempo ficam visíveis ao preparar a infusão.
/// </summary>
public sealed class LinhaItemPrescricao
{
    private string _descricao = string.Empty;

    public string Descricao
    {
        get => _descricao;
        set { if (_descricao == value) return; _descricao = value; DescricaoFormatada = null; }
    }

    public string? DescricaoFormatada { get; set; }

    public string DicaDose => System.Globalization.CultureInfo.GetCultureInfo("pt-BR").CompareInfo.IndexOf(Descricao, "lidocaina",
        System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0
        ? "Quantidade em mL" : "Quantidade e medida";

    public string? Dose { get; set; }
    public string? Diluente { get; set; } = "SF 0,9%";
    public string? Volume { get; set; }
    public ViaAdministracao Via { get; set; } = ViaAdministracao.Endovenosa;
    public string? TempoInfusao { get; set; } = "1 hora";
    public string? HoraPrevista { get; set; }
    public bool SeNecessario { get; set; }
    public string? Observacoes { get; set; }
    public string? ObservacoesFormatadas { get; set; }

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

/// <summary>Uma infusão (preparo + medicamentos) em edição — subiu do <c>GrupoInfusaoEdicao</c> WPF.</summary>
public sealed class GrupoInfusaoEdicao
{
    public int Numero { get; set; }
    public string? Diluente { get; set; } = "SF 0,9%";
    public string? Volume { get; set; }
    public ViaAdministracao Via { get; set; } = ViaAdministracao.Endovenosa;
    public string? Tempo { get; set; }
    public string? Horario { get; set; }
    public string Titulo => $"Infusão {Numero}";

    /// <summary>"Salvar esta infusão como modelo" aberto NESTA infusão — cada uma tem o seu formulário.</summary>
    public bool SalvandoComoModelo { get; set; }
    public string NomeDoModelo { get; set; } = string.Empty;

    public List<LinhaItemPrescricao> Itens { get; } = [];

    public IReadOnlyList<ItemPrescricaoInterna> Preparar()
    {
        if (!string.IsNullOrWhiteSpace(Horario) && !TimeOnly.TryParseExact(Horario, "HH:mm", out _))
            throw new InvalidOperationException($"Confira o horário da infusão {Numero} (HH:mm).");
        var itens = Itens.Where(i => !string.IsNullOrWhiteSpace(i.Descricao)).Select(i => i.Para()).ToArray();
        if (itens.Length == 0) throw new InvalidOperationException($"Adicione medicamentos à infusão {Numero} ou remova a infusão vazia.");
        foreach (var i in itens)
        {
            i.GrupoInfusao = Numero; i.Diluente = Diluente;
            i.Volume = decimal.TryParse(Volume, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), out _) ? Volume?.Trim() + " mL" : Volume;
            i.Via = Via; i.TempoInfusao = Tempo;
            i.HoraPrevista = TimeOnly.TryParseExact(Horario, "HH:mm", out var h) ? h : null;
        }
        return itens;
    }

    public static IReadOnlyList<GrupoInfusaoEdicao> Carregar(IEnumerable<ItemPrescricaoInterna> itens, bool diluicaoUnica, string? diluente, string? volume)
    {
        var lista = itens.OrderBy(i => i.Ordem).ToArray();
        GruposInfusao.Validar(lista, diluicaoUnica);
        if (diluicaoUnica && lista.Select(i => (i.Via, i.TempoInfusao, i.HoraPrevista)).Distinct().Count() > 1)
            throw new InvalidOperationException("A prescrição antiga combina diluição única com vias, tempos ou horários diferentes. Revise os preparos em uma nova prescrição; a via anterior permanece preservada.");
        // Sem diluição única explícita, cada item antigo conserva seu próprio preparo.
        var grupos = new List<List<ItemPrescricaoInterna>>();
        string? anterior = null;
        foreach (var i in lista)
        {
            var chave = i.GrupoInfusao is { } n ? $"grupo:{n}" : !diluicaoUnica ? $"item:{grupos.Count}" : System.Text.Json.JsonSerializer.Serialize(new { Diluente = diluente, Volume = volume, i.Via, i.TempoInfusao, i.HoraPrevista });
            if (chave != anterior) { grupos.Add([]); anterior = chave; }
            grupos[^1].Add(i);
        }
        return grupos.Select((itensDoGrupo, n) =>
        {
            var primeiro = itensDoGrupo.First();
            var g = new GrupoInfusaoEdicao { Numero = n + 1, Diluente = diluicaoUnica ? diluente : primeiro.Diluente, Volume = diluicaoUnica ? volume : primeiro.Volume, Via = primeiro.Via, Tempo = primeiro.TempoInfusao, Horario = primeiro.HoraPrevista?.ToString("HH:mm") };
            foreach (var i in itensDoGrupo) g.Itens.Add(LinhaItemPrescricao.De(i));
            return g;
        }).ToArray();
    }
}

/// <summary>
/// ESCREVER E ASSINAR A PRESCRIÇÃO DE INFUSÃO (parcela 42) — o estado do editor (out/2026: era o
/// <c>PrescricaoInternaEdicaoViewModel</c> WPF, movido para cá sem mudar o que é gravado, chamado
/// ou perguntado). As perguntas ("substituir o que já foi escrito?") chegam por delegado, e o
/// formulário Blazor responde com o diálogo HTML.
///
/// O fluxo tem dois passos porque a assinatura é irreversível: salvar deixa em rascunho, assinar
/// põe a folha na sala. A conferência de alergia acontece ANTES do certificado — a ordem mora em
/// <see cref="AssinaturaDaPrescricaoNaTela.LiberarDoEditorAsync"/>.
/// </summary>
public sealed class PrescricaoDeInfusaoEmEdicao
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _pacienteId;
    private readonly int? _profissionalId;
    private readonly int? _agendamentoId;
    private readonly int? _evolucaoId;
    private int _prescricaoId;
    private bool _carregamentoFalhou;

    /// <param name="prescricaoId">Rascunho existente a REABRIR. Nulo cria uma prescrição nova.</param>
    public PrescricaoDeInfusaoEmEdicao(
        IServiceScopeFactory escopos, int pacienteId, string paciente, int? profissionalId,
        int? agendamentoId = null, int? prescricaoId = null, int? evolucaoId = null)
    {
        _escopos = escopos;
        _pacienteId = pacienteId;
        _profissionalId = profissionalId;
        _agendamentoId = agendamentoId;
        _evolucaoId = evolucaoId;
        _prescricaoId = prescricaoId ?? 0;
        Paciente = paciente;

        // Só a prescrição NOVA nasce com uma linha em branco: reabrir um rascunho e ganhar um
        // item vazio no fim faria a folha impressa sair com uma linha fantasma.
        if (_prescricaoId == 0) CriarInfusao();
    }

    public List<GrupoInfusaoEdicao> Infusoes { get; } = [];
    public IReadOnlyList<LinhaItemPrescricao> Itens => Infusoes.SelectMany(g => g.Itens).ToArray();
    public IReadOnlyList<MedicamentoSugerido> CatalogoMedicamentos { get; private set; } = [];

    /// <summary>Alergias e medicação contínua do paciente — o que se olha ANTES de escrever.</summary>
    public List<string> Alertas { get; } = [];
    public bool TemAlertas => Alertas.Count > 0;

    public IReadOnlyList<ViaAdministracao> Vias { get; } = Enum.GetValues<ViaAdministracao>();

    public string Paciente { get; }

    /// <summary>Número da série anual — fica com o aviso até a primeira gravação.</summary>
    public string Numero { get; private set; } = "(será numerada ao salvar)";
    public string? Indicacao { get; set; }
    public string? IndicacaoFormatada { get; set; }
    public string? Observacoes { get; set; }
    public string? ObservacoesFormatadas { get; set; }
    public DateTime? DataPrescricao { get; set; } = DateTime.Today;
    public string HoraPrescricao { get; set; } = DateTime.Now.ToString("HH:mm");

    public string? Mensagem { get; private set; }
    public bool MensagemEhErro { get; private set; }
    public bool Ocupado { get; private set; }
    public string TextoOperacao { get; private set; } = "Carregando prescrição…";

    /// <summary>A folha já foi assinada aqui — quem abriu recarrega a lista.</summary>
    public bool Assinou { get; private set; }

    /// <summary>A folha foi liberada (assinada ou sem assinatura): o formulário fecha.</summary>
    public bool Liberou { get; private set; }

    public bool ContinuidadeAtiva { get; private set; }
    public string RotuloLiberacao => ContinuidadeAtiva ? "Liberar para enfermagem" : "Assinar e enviar à sala";

    public List<ModeloDocumento> Modelos { get; } = [];
    public string BuscaModelo { get; set; } = string.Empty;
    public int? ModeloEmExclusao { get; set; }

    /// <summary>Metade visível da permissão; a que impede é o <see cref="Exigir"/> no comando.</summary>
    public static bool PodePrescrever => SessaoUsuario.Atual.Pode(Permissao.Prescrever);
    public bool PodeAssinar => PodePrescrever && !Ocupado && Itens.Count > 0;
    public bool PodeCopiarUltimaPrescricao => PodePrescrever && !Ocupado && !_carregamentoFalhou;

    /// <summary>Algo mudou por fora de um campo: quem desenha redesenha.</summary>
    public event Action? Mudou;
    private void Avisar() => Mudou?.Invoke();

    private void Falar(string? mensagem, bool erro) { Mensagem = mensagem; MensagemEhErro = erro; Avisar(); }

    /// <summary>
    /// A segunda barreira, e ela DIZ por que recusou em vez de voltar calada. Devolve false
    /// quando impede.
    /// </summary>
    private bool Exigir(Permissao permissao, string acao)
    {
        if (SessaoUsuario.Atual.Pode(permissao)) return true;
        Falar($"Você não tem permissão para {acao}. Peça à direção o acesso "
              + $"\"{PerfisAcesso.Rotular(permissao)}\".", true);
        return false;
    }

    private bool TemConteudoEmEdicao => !string.IsNullOrWhiteSpace(Indicacao)
        || !string.IsNullOrWhiteSpace(Observacoes) || Infusoes.Count > 1
        || Infusoes.Any(g => g.Diluente != "SF 0,9%" || !string.IsNullOrWhiteSpace(g.Volume)
            || g.Via != ViaAdministracao.Endovenosa || !string.IsNullOrWhiteSpace(g.Tempo)
            || !string.IsNullOrWhiteSpace(g.Horario))
        || Itens.Any(i => !string.IsNullOrWhiteSpace(i.Descricao) || !string.IsNullOrWhiteSpace(i.Dose)
            || !string.IsNullOrWhiteSpace(i.Observacoes) || i.SeNecessario);

    // ==================== Carga ====================

    /// <summary>Carrega o contexto clínico, o rascunho (quando reabre), os modelos e o catálogo — na ordem do WPF.</summary>
    public async Task InicializarAsync()
    {
        await PrepararAsync();
        await CarregarModelosAsync();
        await RecarregarMedicamentosAsync();
    }

    private async Task PrepararAsync()
    {
        try
        {
            Ocupado = true; Avisar();

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>();

            ContinuidadeAtiva = await ContinuidadeSemAssinatura.HabilitadaAsync(scope.ServiceProvider.GetRequiredService<IClinicaRepositorio>());
            // A prescrição NOVA não é criada aqui (parcela 45): a criação assinala um NÚMERO, e
            // abrir e desistir deixaria um rascunho vazio numerado na lista da clínica.
            if (_prescricaoId == 0)
            {
                await CarregarContextoAsync(servico);
                return;
            }

            var prescricao = await servico.ObterAsync(_prescricaoId)
                ?? throw new InvalidOperationException("Prescrição não encontrada.");

            // Assinada não se edita: os bytes já foram selados.
            if (!prescricao.PodeEditar)
                throw new InvalidOperationException(
                    $"A prescrição {prescricao.Numero} já foi assinada e não pode mais ser "
                    + "editada. Cancele e emita outra, se for o caso.");

            _prescricaoId = prescricao.Id;
            Numero = prescricao.Numero;
            DataPrescricao = prescricao.Data.ToDateTime(TimeOnly.MinValue);
            HoraPrescricao = prescricao.Hora.ToString("HH:mm");

            Indicacao = prescricao.Indicacao; IndicacaoFormatada = prescricao.IndicacaoFormatada;
            Observacoes = prescricao.Observacoes; ObservacoesFormatadas = prescricao.ObservacoesFormatadas;
            Infusoes.Clear();
            foreach (var g in GrupoInfusaoEdicao.Carregar(prescricao.Itens, prescricao.DiluicaoUnica, prescricao.DiluenteGlobal, prescricao.VolumeTotal)) Infusoes.Add(g);
            if (Infusoes.Count == 0) CriarInfusao();
            Renumerar();

            await CarregarContextoAsync(servico);
        }
        catch (Exception ex)
        {
            _carregamentoFalhou = true;
            Diagnostico.Registrar("Consultório — rascunho de prescrição não pôde ser criado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Ocupado = false; Avisar();
        }
    }

    private async Task CarregarContextoAsync(PrescricaoInternaService servico)
    {
        var contexto = await servico.ContextoAsync(_pacienteId);

        Alertas.Clear();
        if (ProblemaPacienteService.ResumirAlergias(contexto.Alergias) is { } alergias)
            Alertas.Add(alergias);
        foreach (var medicacao in contexto.MedicacoesEmUso)
            Alertas.Add($"Em uso contínuo: {medicacao.Descricao}");
    }

    private async Task CarregarModelosAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var modelos = await scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>().ModelosAsync();
            Modelos.Clear(); foreach (var m in modelos.Where(m => m.Ativo && m.ParaInfusao)) Modelos.Add(m);
            if (ModeloEmExclusao is int id && Modelos.All(m => m.Id != id)) ModeloEmExclusao = null;
        }
        catch (Exception ex) { Mensagem = "Não foi possível carregar os modelos: " + ex.Message; MensagemEhErro = true; }
        Avisar();
    }

    public async Task RecarregarMedicamentosAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();
            CatalogoMedicamentos = BuscaMedicamentos.Catalogo(await scope.ServiceProvider.GetRequiredService<MedicamentoCatalogoService>().ListarAsync());
        }
        catch (Exception ex) { Mensagem = "A busca de medicamentos não pôde carregar. Você pode escrever livremente. " + ex.Message; MensagemEhErro = true; }
        Avisar();
    }

    // ==================== Copiar a última / modelos ====================

    public async Task AplicarCopiaDaUltimaAsync(ModeloInfusao modelo, Func<string, string, Task<bool>> confirmarPerigo)
    {
        if (_carregamentoFalhou || !Exigir(Permissao.Prescrever, "copiar a última prescrição")) return;
        // Converte e valida tudo antes de tocar no conteúdo atual.
        var grupos = GrupoInfusaoEdicao.Carregar(modelo.Itens.Select(ModeloInfusao.Para),
            modelo.DiluicaoUnica, modelo.DiluenteGlobal, modelo.VolumeTotal);
        if (grupos.Count == 0)
        {
            Falar("A última prescrição emitida não possui itens ativos para copiar.", false);
            return;
        }
        if (TemConteudoEmEdicao && !await confirmarPerigo("Copiar última prescrição",
            "Substituir o conteúdo em edição pela última prescrição emitida deste paciente?")) return;
        Indicacao = modelo.Indicacao; IndicacaoFormatada = modelo.IndicacaoFormatada;
        Observacoes = modelo.Observacoes; ObservacoesFormatadas = modelo.ObservacoesFormatadas;
        Infusoes.Clear();
        foreach (var grupo in grupos) Infusoes.Add(grupo);
        Renumerar();
        Falar("Última prescrição copiada. Revise os medicamentos, preparos e horários antes de salvar e liberar. Nada foi gravado.", false);
    }

    public async Task CopiarUltimaPrescricaoAsync(Func<string, string, Task<bool>> confirmarPerigo)
    {
        if (!PodeCopiarUltimaPrescricao || !Exigir(Permissao.Prescrever, "copiar a última prescrição")) return;
        try
        {
            Ocupado = true; TextoOperacao = "Carregando última prescrição…"; Avisar();
            ModeloInfusao? modelo;
            using (var scope = _escopos.CreateScope())
                modelo = await scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>()
                    .UltimaComoModeloAsync(_pacienteId);
            Ocupado = false;
            if (modelo is not null) await AplicarCopiaDaUltimaAsync(modelo, confirmarPerigo);
            else Falar("Não há prescrição de infusão emitida deste paciente para copiar.", false);
        }
        catch (Exception ex) { Falar(ex.Message, true); }
        finally { Ocupado = false; Avisar(); }
    }

    public IEnumerable<ModeloDocumento> ModelosVisiveis => Modelos.Where(CasaComBusca);

    private bool CasaComBusca(ModeloDocumento m) =>
        string.IsNullOrWhiteSpace(BuscaModelo)
        || System.Globalization.CultureInfo.GetCultureInfo("pt-BR").CompareInfo.IndexOf(
               m.Nome, BuscaModelo.Trim(),
               System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;

    public string RotuloUsarModelo => $"Usar modelo salvo ({Modelos.Count})";
    public bool NenhumModeloVisivel => !Modelos.Any(CasaComBusca);
    public string TextoSemModelos => Modelos.Count == 0
        ? "Nenhum modelo cadastrado. Você pode salvar esta infusão como modelo."
        : "Nenhum modelo encontrado.";

    public async Task AplicarModeloAsync(GrupoInfusaoEdicao grupo, ModeloDocumento selecionado, Func<string, string, Task<bool>> confirmarPerigo)
    {
        if (Ocupado || !Infusoes.Contains(grupo) || !Exigir(Permissao.Prescrever, "usar modelos")) return;
        try
        {
            var modelo = ModeloInfusao.Ler(selecionado.ConfiguracaoInfusao);
            var novos = GrupoInfusaoEdicao.Carregar(modelo.Itens.Select(ModeloInfusao.Para), modelo.DiluicaoUnica, modelo.DiluenteGlobal, modelo.VolumeTotal);
            if (novos.Count == 0) throw new InvalidOperationException("Modelo sem medicamentos.");
            if (grupo.Itens.Any(i => !string.IsNullOrWhiteSpace(i.Descricao)) && !await confirmarPerigo("Usar modelo", "Substituir os medicamentos e o preparo desta infusão?")) return;
            var indice = Infusoes.IndexOf(grupo); Infusoes.RemoveAt(indice);
            foreach (var g in novos) Infusoes.Insert(indice++, g);
            Renumerar(); Falar("Modelo aplicado. Revise o preparo e as doses antes de liberar.", false);
        }
        catch (Exception ex) { Falar(ex.Message, true); }
    }

    public async Task<bool> SalvarModeloAsync(GrupoInfusaoEdicao grupo, string nome)
    {
        if (Ocupado || !Infusoes.Contains(grupo) || !Exigir(Permissao.Prescrever, "salvar modelos")) return false;
        try
        {
            Ocupado = true; TextoOperacao = "Salvando modelo…"; Avisar();
            var itens = grupo.Preparar().ToArray();
            foreach (var i in itens) i.GrupoInfusao = 1;
            // O modelo guarda a composição, sem indicação, paciente, horário ou observação clínica.
            foreach (var i in itens) { i.HoraPrevista = null; i.Observacoes = null; i.ObservacoesFormatadas = null; }
            var config = new ModeloInfusao(null, null, itens.Select(ModeloInfusao.De).ToArray()).Guardar();
            using (var scope = _escopos.CreateScope())
                await scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>().SalvarModeloAsync(new()
                {
                    Nome = nome, Tipo = TipoDocumentoClinico.Receita, ParaInfusao = true, ConfiguracaoInfusao = config,
                    Corpo = string.Join("\n", itens.Select(i => i.Descricao)), Ativo = true
                }, SessaoUsuario.Atual.Operador, substituirPorNome: false);
            Ocupado = false;
            await CarregarModelosAsync(); Falar("Modelo salvo para reutilizar na clínica.", false); return true;
        }
        catch (Exception ex) { Falar(ex.Message, true); return false; }
        finally { Ocupado = false; Avisar(); }
    }

    public async Task SalvarComoModeloAsync(GrupoInfusaoEdicao grupo)
    {
        var nome = grupo.NomeDoModelo.Trim();
        if (nome.Length == 0)
        {
            Falar("Dê um nome ao modelo para encontrá-lo depois.", true);
            return;
        }
        if (await SalvarModeloAsync(grupo, nome))
        {
            grupo.SalvandoComoModelo = false;
            grupo.NomeDoModelo = string.Empty;
            Avisar();
        }
    }

    public void PedirExclusaoModelo(ModeloDocumento modelo)
    {
        if (Ocupado) return;
        if (!Exigir(Permissao.Prescrever, "excluir modelos de infusão")) return;
        ModeloEmExclusao = modelo.Id; Avisar();
    }

    public void ManterModelo() { ModeloEmExclusao = null; Avisar(); }

    public async Task ConfirmarExclusaoModeloAsync(ModeloDocumento modelo)
    {
        try
        {
            if (await ExcluirModeloAsync(modelo.Id)) { ModeloEmExclusao = null; Avisar(); }
        }
        catch (Exception ex) { Falar(ex.Message, true); }
    }

    public async Task<bool> ExcluirModeloAsync(int modeloId)
    {
        if (Ocupado || !PodeCopiarUltimaPrescricao || !Exigir(Permissao.Prescrever, "excluir modelos de infusão")) return false;
        if (!Modelos.Any(m => m.Id == modeloId && m.ParaInfusao))
            throw new InvalidOperationException("Escolha um modelo de infusão disponível nesta lista.");
        try
        {
            Ocupado = true; TextoOperacao = "Excluindo modelo…"; Avisar();
            using (var scope = _escopos.CreateScope())
                await scope.ServiceProvider.GetRequiredService<DocumentoClinicoService>()
                    .ExcluirModeloInfusaoAsync(modeloId, SessaoUsuario.Atual.Operador);
            Ocupado = false;
            await CarregarModelosAsync();
            Falar("Modelo excluído. As prescrições já criadas foram mantidas.", false);
            return true;
        }
        catch (Exception ex) { Falar(ex.Message, true); return false; }
        finally { Ocupado = false; Avisar(); }
    }

    // ==================== A estrutura ====================

    private void Renumerar()
    {
        for (var n = 0; n < Infusoes.Count; n++) Infusoes[n].Numero = n + 1;
        Avisar();
    }

    public void CriarInfusao()
    {
        if (Infusoes.Count >= 50) { Falar("Limite de 50 infusões por prescrição.", true); return; }
        var grupo = new GrupoInfusaoEdicao(); grupo.Itens.Add(new LinhaItemPrescricao()); Infusoes.Add(grupo); Renumerar();
    }

    public async Task RemoverInfusaoAsync(GrupoInfusaoEdicao grupo, Func<string, string, Task<bool>> confirmarPerigo)
    {
        if (grupo.Itens.Any(i => !string.IsNullOrWhiteSpace(i.Descricao)) && !await confirmarPerigo("Remover infusão", "Remover esta infusão do rascunho em edição?")) return;
        Infusoes.Remove(grupo); Renumerar();
    }

    public void AcrescentarItem(GrupoInfusaoEdicao grupo) { grupo.Itens.Add(new LinhaItemPrescricao()); Avisar(); }

    public void RemoverItem(LinhaItemPrescricao linha)
    {
        Infusoes.FirstOrDefault(g => g.Itens.Contains(linha))?.Itens.Remove(linha); Avisar();
    }

    // ==================== Gravar / assinar ====================

    public async Task SalvarRascunhoAsync()
    {
        // A barreira que IMPEDE — `PodePrescrever` é só a metade VISÍVEL.
        if (!Exigir(Permissao.Prescrever, "salvar a prescrição de infusão")) return;

        if (await GravarAsync() is null) return;
        Falar("Rascunho salvo. Use Liberar para enfermagem para enviar à sala.", false);
    }

    /// <summary>
    /// Confere, pede confirmação se houver alergia, escolhe o certificado e assina — a ORDEM
    /// mora em <see cref="AssinaturaDaPrescricaoNaTela.LiberarDoEditorAsync"/>; aqui ficam as
    /// guardas e as frases do WPF.
    /// </summary>
    public async Task AssinarAsync(
        Func<string, string, Task<bool>> confirmarPerigo,
        Func<string, Task<Clinica.Application.Assinatura.CertificadoAssinatura?>> escolher)
    {
        if (!Exigir(Permissao.Prescrever, "assinar a prescrição de infusão")) return;

        if (Itens.Count == 0)
        {
            Falar("Acrescente ao menos um item antes de assinar: uma folha assinada em "
                  + "branco vira espaço para alguém escrever uma linha depois.", true);
            return;
        }

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.Prescrever, "prescrever");

            if (await GravarAsync() is null) return;

            TextoOperacao = "Assinando e arquivando prescrição…";
            Ocupado = true; Avisar();

            var desfecho = await AssinaturaDaPrescricaoNaTela.LiberarDoEditorAsync(
                _escopos, _prescricaoId, ContinuidadeAtiva,
                AssinaturaDaPrescricaoNaTela.AssuntoDoEditor(Numero, Paciente),
                confirmarPerigo,
                escolher,
                SessaoUsuario.Atual.UsuarioId,
                SessaoUsuario.Atual.Autenticado ? SessaoUsuario.Atual.UsuarioId : null,
                SessaoUsuario.Atual.Operador);

            switch (desfecho)
            {
                case DesfechoDaLiberacao.CanceladaNaAlergia:
                    Falar(AssinaturaDaPrescricaoNaTela.CanceladaNaAlergia, false);
                    break;
                case DesfechoDaLiberacao.LiberadaSemAssinatura:
                    Assinou = false;
                    Liberou = true;
                    break;
                case DesfechoDaLiberacao.Assinada:
                    Assinou = true;
                    Liberou = true;
                    break;
                // Desistiu no seletor: sair calado é o certo.
            }
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Consultório — prescrição não pôde ser assinada", ex);
            Falar(ex.Message, true);
        }
        finally
        {
            Ocupado = false; Avisar();
        }
    }

    /// <summary>Grava o rascunho. Devolve null quando recusou — o chamador não segue.</summary>
    private async Task<PrescricaoInterna?> GravarAsync()
    {
        if (_carregamentoFalhou) { Falar("Reabra a prescrição: o carregamento anterior falhou.", true); return null; }
        try
        {
            if (Infusoes.Count == 0) throw new InvalidOperationException("Crie ao menos uma infusão.");
            var itens = Infusoes.SelectMany(g => g.Preparar()).ToArray();
            TextoOperacao = "Salvando prescrição de infusão…";
            Ocupado = true; Avisar();
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
                exigeAssinaturaEletronicaDaExecucao: false, indicacaoFormatada: IndicacaoFormatada, observacoesFormatadas: ObservacoesFormatadas,
                dataPrescricao: DateOnly.FromDateTime(dataPrescricao), horaPrescricao: horaPrescricao, diluicaoUnica: false, diluenteGlobal: null, volumeTotal: null);

            Mensagem = null;
            MensagemEhErro = false;
            return salva;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Consultório — rascunho de prescrição não pôde ser salvo", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
            return null;
        }
        finally
        {
            Ocupado = false; Avisar();
        }
    }
}
