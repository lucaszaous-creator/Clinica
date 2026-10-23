using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Ui.Blazor.Componentes.SecoesDoPaciente;

/// <summary>
/// A FOLHA DO MÉDICO de um paciente aberto (out/2026): o que a <c>AtendimentoViewModel</c> /
/// <c>FolhaDaSessaoViewModel</c> do WPF carregavam e gravavam, sem WPF.
///
/// ⚠️ Ela é guardada pela TELA do paciente (<see cref="ITelaDoPaciente.Guardado{T}"/>), e não
/// pela seção: trocar de seção no rail DESMONTA a seção, e o rascunho de quem estava
/// escrevendo não pode ir junto. É também por ela que o "Salvar e finalizar" do cabeçalho
/// grava o que está escrito (<see cref="IFolhaAberta"/>) — com a seção à vista ou não.
///
/// As regras (o que é "em branco", o que é "há algo a gravar", o modelo, a reutilização, a
/// cópia) moram no <see cref="RascunhoDaSessao"/> e no <see cref="MapaDaSessao"/>, na
/// Application, com teste; aqui ficam as LEITURAS e a GRAVAÇÃO, na ordem do WPF.
/// </summary>
public sealed class FolhaDoAtendimento(IServiceScopeFactory escopos, ContextoDoPaciente contexto) : IFolhaAberta
{
    /// <summary>Quantas sessões anteriores a coluna traz (o prontuário inteiro é a seção Histórico).</summary>
    public const int SessoesAnterioresVisiveis = 5;

    /// <summary>A chave pela qual a tela guarda a folha (<see cref="ITelaDoPaciente.Guardado{T}"/>).</summary>
    public const string Chave = "folha-do-atendimento";

    public ContextoDoPaciente Contexto { get; } = contexto;

    public RascunhoDaSessao Rascunho { get; } = new();

    /// <summary>O mapa da sessão — nulo até ler o horário (e se a leitura dele falhar).</summary>
    public MapaDaSessao? Mapa { get; private set; }

    public List<ResumoSessaoAnterior> Anteriores { get; } = [];
    public List<AlertaDaFolha> AlertasClinicos { get; } = [];
    public List<AlertaDaFolha> Alertas { get; } = [];
    public List<PapelDeHoje> SaiuHoje { get; } = [];

    /// <summary>Registros do dia sem vínculo com horário (o "Vincular registro existente").</summary>
    public List<(int Id, string Rotulo)> RegistrosParaVincular { get; } = [];

    public SituacaoTermo? TermoPendente { get; private set; }

    public bool PodeIndicarBsv { get; private set; }
    public bool BsvIndicado { get; set; }

    public string LinhaDeContexto { get; private set; } = string.Empty;

    public bool Carregando { get; private set; }
    public bool NaoVerificado { get; private set; }
    public bool Carregada { get; private set; }
    public string? Mensagem { get; set; }
    public bool MensagemEhErro { get; set; }

    /// <summary>"Última gravação às 14h37" — a gravação NO PRONTUÁRIO (cada Salvar versiona).</summary>
    public string UltimaGravacao { get; private set; } = string.Empty;

    /// <summary>Quando o rascunho automático foi guardado pela última vez (nulo = nada pendente).</summary>
    public DateTime? RascunhoGuardadoEm { get; private set; }

    /// <summary>A linha do rodapé sobre o rascunho, ou nulo quando não há rascunho pendente.</summary>
    public string? RodapeDoRascunho => RascunhoGuardadoEm is { } em ? RascunhoAutomatico.Rodape(em, _horasDoPrazo) : null;

    /// <summary>Algo mudou (carga terminou, sessão gravada): a seção à vista se redesenha.</summary>
    public event Action? Mudou;

    /// <summary>O rascunho foi guardado: só o rodapé muda (o campo de texto NÃO é redesenhado).</summary>
    public event Action? RascunhoGuardado;

    private IReadOnlyList<CampoPersonalizadoProntuario> _definicoes = [];
    private CopiaEvolucaoPaciente? _ultimaParaCopiar;
    private int? _atendimentoId = contexto.AtendimentoId;
    private DateTime? _dataHoraDaSessao;
    private TimeOnly? _chegada;
    private TimeOnly? _saida;
    private bool _acessoRegistrado;
    private int _geracao;

    // O rascunho automático: o último conteúdo guardado, o da conferência anterior (para saber
    // se a pessoa PAROU de digitar), quando guardou, e as travas de gravação no ar.
    private string? _guardado;
    private string? _conferenciaAnterior;
    private DateTime? _ultimaGuarda;
    private int? _horasDoPrazo;
    private bool _guardando;
    private bool _salvando;

    private ChaveDoRascunho ChaveDoRascunho => new(Contexto.PacienteId, SessaoUsuario.Atual.ProfissionalId,
        Contexto.AgendamentoId, Contexto.DiaDaSessao);

    private string ConteudoAtual() => ConteudoDoRascunho.De(Rascunho).Serializar();

    /// <summary>
    /// O RASCUNHO AUTOMÁTICO (out/2026, "como no Excel"). Chamado pela seção a cada
    /// <see cref="RascunhoAutomatico.Intervalo"/> e ao sair dela (<paramref name="forcar"/>):
    /// guarda o que está na tela quando mudou e a pessoa parou de digitar (ou passou o teto),
    /// numa gravação pequena e fora da tela — sem "salvando…", sem redesenhar o campo. Falha
    /// vai ao log e tenta de novo na próxima conferência; nunca interrompe quem escreve.
    /// </summary>
    public async Task ConferirRascunhoAsync(bool forcar = false)
    {
        if (!Carregada || Carregando || _salvando || _guardando || Contexto.PacienteId == 0 || !PodeEditarProntuario)
            return;
        var atual = ConteudoAtual();
        var deve = forcar
            ? atual != _guardado
            : RascunhoAutomatico.DeveGuardar(atual, _guardado, _conferenciaAnterior, DateTime.Now, _ultimaGuarda);
        _conferenciaAnterior = atual;
        if (!deve) return;

        _guardando = true;
        try
        {
            using var escopo = escopos.CreateScope();
            if (escopo.ServiceProvider.GetService<IRascunhosDaSessao>() is not { } rascunhos) return;
            var em = await rascunhos.GuardarAsync(ChaveDoRascunho, atual,
                Rascunho.EvolucaoId == 0 ? null : Rascunho.EvolucaoId, SessaoUsuario.Atual.Operador);
            _guardado = atual;
            _ultimaGuarda = DateTime.Now;
            RascunhoGuardadoEm = em;
            RascunhoGuardado?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Consultório — rascunho automático não pôde ser guardado; tenta de novo", ex);
        }
        finally { _guardando = false; }
    }

    public int EvolucaoId => Rascunho.EvolucaoId;
    public bool SessaoEmBranco => Rascunho.SessaoEmBranco;
    public bool TemAlgoParaGravar => Rascunho.TemAlgoParaGravar(Mapa?.TemConteudo == true);
    public bool TemUltimaParaCopiar => _ultimaParaCopiar is not null;

    /// <summary>Metade VISÍVEL do "escrever no prontuário": a enfermagem registra pela passagem dela.</summary>
    public static bool PodeEditarProntuario
        => SessaoUsuario.Atual.Perfil != PerfilAcesso.Enfermagem && SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    /// <summary>O que a sessão tem AGORA para dar aos papéis (a coluna EMITIR).</summary>
    public HerancaDaSessao Heranca() => new(
        Contexto.PacienteId,
        Cid: Rascunho.CidSessao,
        Hipotese: Rascunho.HipoteseDiagnostica,
        Chegada: _chegada,
        Saida: _saida,
        AgendamentoId: Contexto.AgendamentoId,
        // Sem sessão salva não há evolução para apontar — vínculo para lugar nenhum.
        EvolucaoId: Rascunho.EvolucaoId == 0 ? null : Rascunho.EvolucaoId,
        DataHoraDaSessao: _dataHoraDaSessao);

    public void AtendimentoNasceu(int atendimentoId) => _atendimentoId = atendimentoId;

    /// <summary>Copia a ÚLTIMA evolução salva (lida na carga) para a folha; nulo quando não há.</summary>
    public string? CopiarUltima() => _ultimaParaCopiar is { } m ? Rascunho.Copiar(m) : null;

    private void Avisar() => Mudou?.Invoke();

    /// <summary>
    /// Lê o horário, a sessão dele (para CONTINUAR o registro, nunca abrir um segundo), as
    /// anteriores, os avisos, os sinais vitais da enfermagem e o que já saiu hoje. Descarte de
    /// resposta fora de ordem pelo contador; publicação depois do último await.
    /// </summary>
    public async Task CarregarAsync()
    {
        var geracao = ++_geracao;
        var ctx = Contexto;
        Carregando = true;
        NaoVerificado = false;
        Mensagem = null;
        MensagemEhErro = false;
        // Zerado ANTES da leitura: a lista do paciente anterior não pode ficar na tela com o
        // "2ª via" apontando para o documento de outra pessoa enquanto esta carga corre.
        SaiuHoje.Clear();
        Avisar();

        try
        {
            using var escopo = escopos.CreateScope();
            var p = escopo.ServiceProvider;
            var prontuario = p.GetRequiredService<ProntuarioService>();

            var podeIndicarBsv = false;
            var bsvIndicado = false;
            if (ctx.AgendamentoId is { } horarioBsv
                && SessaoUsuario.Atual.Pode(Permissao.Prescrever | Permissao.EditarProntuario))
            {
                var bsv = await p.GetRequiredService<IAcompanhamentoPacienteService>()
                    .EstadoBsvAsync(SessaoUsuario.Atual.UsuarioId, horarioBsv);
                podeIndicarBsv = bsv.PodeIndicar;
                bsvIndicado = bsv.Indicado;
            }

            // A trilha de LEITURA (ponto 4 do compromisso): uma vez por paciente.
            if (!_acessoRegistrado)
            {
                _acessoRegistrado = true;
                await p.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(ctx.PacienteId, SessaoUsuario.Atual.Operador, OrigemAcessoProntuario.Atendimento);
            }

            var sessoes = await prontuario.DoPacienteAsync(ctx.PacienteId);
            if (geracao != _geracao) return;

            // Os campos personalizados — a MODALIDADE do horário decide quais aparecem; sem
            // horário, os de todas. Falhar aqui não impede escrever, mas vai ao log.
            IReadOnlyList<CampoPersonalizadoProntuario> definicoes = [];
            try
            {
                string? modalidade = null;
                if (ctx.AgendamentoId is { } idModalidade)
                    modalidade = (await p.GetRequiredService<IClinicaRepositorio>().ObterAgendamentoAsync(idModalidade))?.ModalidadeCodigo;
                definicoes = await p.GetRequiredService<CampoPersonalizadoService>().DaSessaoAsync(modalidade);
            }
            catch (Exception ex)
            {
                Clinica.Application.Diagnostico.Registrar("Consultório — campos personalizados não puderam ser lidos", ex);
            }
            if (geracao != _geracao) return;

            Evolucao? doHorario = null;
            IReadOnlyList<Agendamento> doDia = [];
            if (ctx.AgendamentoId is { } agendamentoId)
            {
                var diaDoHorario = ctx.DiaDaSessao;
                doDia = await p.GetRequiredService<ConsultorioService>().SessoesDoPacienteNoDiaAsync(ctx.PacienteId, diaDoHorario);
                if (geracao != _geracao) return;
                // A MESMA definição do cartão do Meu dia ("já escrita"), com as sessões irmãs.
                doHorario = ConsultorioService.EvolucaoDoHorario(sessoes, agendamentoId, ctx.PacienteId, diaDoHorario, doDia);
            }

            // A sessão que ESTA folha já gravou continua aberta na releitura, com ou sem horário.
            // Aberto pela busca (sem horário), o "relê tudo" de depois do Salvar caía no Limpar:
            // o texto sumia da tela com o rodapé dizendo "registrada", e o próximo Salvar criava
            // uma SEGUNDA evolução em vez de corrigir a primeira (a clínica, 22/10/2026).
            if (doHorario is null && Rascunho.EvolucaoId != 0)
                doHorario = sessoes.FirstOrDefault(e => e.Id == Rascunho.EvolucaoId && e.CanceladaEm is null);

            var vincular = new List<(int, string)>();
            if (doHorario is null && ctx.AgendamentoId is not null && ctx.DataDoHorario is { } dia)
                vincular.AddRange(sessoes
                    .Where(e => e.CanceladaEm is null && e.AgendamentoId is null && e.AtendimentoId is null && e.Data == dia)
                    .Select(e => (e.Id,
                        $"Registro #{e.Id} · {e.Data:dd/MM/yyyy} · {e.Profissional?.Nome ?? "sem profissional informado"}")));

            // O mapa da sessão aberta (1:1 com a evolução): falhar não impede escrever.
            MapaDaSessao? mapa = new();
            if (doHorario is not null)
            {
                try
                {
                    if (await p.GetRequiredService<MapaCorporalService>().DaEvolucaoAsync(doHorario.Id) is { } gravado)
                        mapa.Substituir(gravado.Pontos.OrderBy(x => x.Ordem).ThenBy(x => x.Id), gravado.Observacoes,
                            gravado.ProtocoloOrigemId, guardarParaDesfazer: false);
                }
                catch (Exception ex)
                {
                    Clinica.Application.Diagnostico.Registrar("Consultório — mapa corporal da sessão não pôde ser lido", ex);
                    mapa = null;
                }
            }
            // O texto escrito e não salvo desta sessão (o rascunho automático) e o prazo que o
            // rodapé promete. Falhar aqui não impede escrever: a folha abre com o que está salvo.
            RascunhoAberto? aberto = null;
            int? horasDoPrazo = null;
            try
            {
                if (PodeEditarProntuario && p.GetService<IRascunhosDaSessao>() is { } rascunhos)
                    aberto = await rascunhos.AbertoAsync(ChaveDoRascunho);
                var regra = await p.GetRequiredService<PoliticaConclusaoService>().ObterAsync();
                horasDoPrazo = regra.Automatica ? regra.Horas : null;
            }
            catch (Exception ex)
            {
                Clinica.Application.Diagnostico.Registrar("Consultório — rascunho automático não pôde ser lido", ex);
            }
            if (geracao != _geracao) return;

            // ===== Publicação (sem await até o fim do bloco) =====
            _definicoes = definicoes;
            _horasDoPrazo = horasDoPrazo;
            Rascunho.Campos.Clear();
            Rascunho.Campos.AddRange(definicoes.Select(CampoDaSessaoNaTela.De));
            if (doHorario is not null) Rascunho.Preencher(doHorario);
            else
            {
                Rascunho.Limpar(ctx.DataDoHorario, DateOnly.FromDateTime(DateTime.Today));
                UltimaGravacao = string.Empty;
            }
            // O rascunho só vence o que está salvo quando é MAIS NOVO que a última gravação.
            RascunhoGuardadoEm = null;
            var recuperado = false;
            if (aberto is not null && ConteudoDoRascunho.Ler(aberto.Conteudo) is { } conteudo
                && (doHorario is null || aberto.AtualizadoEm > (doHorario.AtualizadoEm ?? doHorario.CriadoEm)))
            {
                conteudo.Aplicar(Rascunho);
                if (Rascunho.EvolucaoId == 0 && aberto.EvolucaoId is { } continua) Rascunho.EvolucaoId = continua;
                RascunhoGuardadoEm = aberto.AtualizadoEm;
                recuperado = true;
            }
            _guardado = ConteudoAtual();
            _conferenciaAnterior = _guardado;
            if (recuperado)
            {
                Mensagem = RascunhoAutomatico.Recuperado(aberto!.AtualizadoEm);
                MensagemEhErro = false;
            }
            Mapa = mapa;

            PodeIndicarBsv = podeIndicarBsv;
            BsvIndicado = bsvIndicado;
            RegistrosParaVincular.Clear();
            RegistrosParaVincular.AddRange(vincular);
            _ultimaParaCopiar = CopiaEvolucaoPaciente.Ultima(sessoes, ctx.PacienteId, Rascunho.EvolucaoId, Rascunho.Data);

            Anteriores.Clear();
            Anteriores.AddRange(sessoes.Where(e => e.Id != Rascunho.EvolucaoId).Take(SessoesAnterioresVisiveis)
                .Select(ResumoSessaoAnterior.De));

            _dataHoraDaSessao = null;
            _chegada = null;
            _saida = null;
            if (ctx.AgendamentoId is { } idHorario && doDia.FirstOrDefault(x => x.Id == idHorario) is { } horario)
            {
                _dataHoraDaSessao = horario.DataHora;
                _chegada = horario.ChegadaEm is { } chegou ? TimeOnly.FromDateTime(chegou) : null;
                // A saída só existe depois de encerrar: escrever "agora" numa declaração
                // afirmaria um fato que ainda não aconteceu.
                _saida = horario.FimAtendimentoEm is { } saiu ? TimeOnly.FromDateTime(saiu) : null;
                if (horario.AtendimentoId is { } nasceu) _atendimentoId = nasceu;
            }

            // Dois grupos independentes, cada um no SEU escopo (o apoio abre outro DbContext).
            var dataDaSessao = doHorario?.Data ?? ctx.DiaDaSessao;
            await Task.WhenAll(
                CarregarAlertasAsync(p, dataDaSessao, geracao),
                CarregarApoioAsync(ctx.PacienteId, dataDaSessao, geracao));
            if (geracao != _geracao) return;
            Carregada = true;
        }
        catch (Exception ex)
        {
            if (geracao != _geracao) return;
            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar("Consultório — atendimento não pôde ser carregado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            if (geracao == _geracao) Carregando = false;
            Avisar();
        }
    }

    /// <summary>Sinais vitais da enfermagem (com procedência) e o que já saiu hoje — falham SOZINHOS.</summary>
    private async Task CarregarApoioAsync(int pacienteId, DateOnly dataDaSessao, int geracao)
    {
        using var escopo = escopos.CreateScope();
        var p = escopo.ServiceProvider;

        string? vitais = null, procedencia = null;
        var vitaisFalhou = false;
        try
        {
            if (await p.GetRequiredService<ConsultorioService>().SinaisVitaisDaSessaoAsync(pacienteId, dataDaSessao) is { } s)
            {
                vitais = s.Resumo;
                procedencia = s.Procedencia;
            }
        }
        catch (Exception ex)
        {
            vitaisFalhou = true;
            Clinica.Application.Diagnostico.Registrar("Consultório — sinais vitais da enfermagem não puderam ser lidos", ex);
        }
        if (geracao != _geracao) return;
        LinhaDeContexto = ContextoDaFolha.Montar(
            ResumoSessaoAnterior.ContextoDaUltima(Anteriores.FirstOrDefault()), vitais, procedencia, vitaisFalhou);

        await CarregarSaiuHojeAsync(p, geracao);
    }

    /// <summary>O que já saiu para este paciente NO DIA desta sessão (a lista curta da coluna EMITIR).</summary>
    public async Task RelerSaiuHojeAsync()
    {
        using var escopo = escopos.CreateScope();
        await CarregarSaiuHojeAsync(escopo.ServiceProvider, _geracao);
        Avisar();
    }

    private async Task CarregarSaiuHojeAsync(IServiceProvider p, int geracao)
    {
        var doDia = Rascunho.Data;
        try
        {
            var todos = await p.GetRequiredService<DocumentoClinicoService>().DoPacienteAsync(Contexto.PacienteId);
            if (geracao != _geracao || doDia != Rascunho.Data) return;
            var linhas = todos.Where(d => d.Data == doDia).Select(PapelDeHoje.De).ToList();
            SaiuHoje.Clear();
            SaiuHoje.AddRange(linhas);
        }
        catch (Exception ex)
        {
            if (geracao != _geracao) return;
            Clinica.Application.Diagnostico.Registrar("Consultório — documentos do dia não puderam ser lidos", ex);
            SaiuHoje.Clear();
        }
    }

    /// <summary>Os avisos: o prontuário falha SEPARADO do administrativo, e cada falha DIZ que falhou.</summary>
    private async Task CarregarAlertasAsync(IServiceProvider p, DateOnly dataDaSessao, int geracao)
    {
        var pacienteId = Contexto.PacienteId;
        var clinicos = new List<AlertaDaFolha>();
        var administrativos = new List<AlertaDaFolha>();
        SituacaoTermo? pendente = null;

        try
        {
            clinicos.AddRange(AlertasDaFolha.DosProblemas(await p.GetRequiredService<ProblemaPacienteService>().AlertasAsync(pacienteId)));
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Consultório — lista de problemas do paciente não pôde ser lida", ex);
            clinicos.Add(new AlertaDaFolha(AlertasDaFolha.FalhaProblemas, false));
        }

        try
        {
            if (geracao != _geracao) return;
            var recentes = await p.GetRequiredService<EvolucaoEnfermagemService>().DoPacienteAsync(pacienteId, limite: 30);
            clinicos.AddRange(AlertasDaFolha.DasIntercorrencias(recentes, DateTime.Now));
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Consultório — intercorrências de enfermagem não puderam ser lidas", ex);
            clinicos.Add(new AlertaDaFolha(AlertasDaFolha.FalhaIntercorrencias, false));
        }

        try
        {
            if (geracao != _geracao) return;
            var resposta = await p.GetRequiredService<ElegibilidadeService>().ConferirAsync(pacienteId, dataDaSessao);
            administrativos.AddRange(resposta.Alertas.Select(a => new AlertaDaFolha(a.Descricao, a.Urgencia == NivelUrgencia.Vermelho)));
            var situacoes = resposta.TermosDoDia
                            ?? await p.GetRequiredService<TermoProcedimentoService>().SituacaoDoDiaAsync(pacienteId, dataDaSessao);
            pendente = PendenciasDeTermo.Primeira(situacoes);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Consultório — alertas do paciente não puderam ser lidos", ex);
            administrativos.Add(new AlertaDaFolha(AlertasDaFolha.FalhaAdministrativos, false));
        }

        // Chegou tarde: publicar seria mostrar a alergia de quem saiu ao lado do nome de quem entrou.
        if (geracao != _geracao) return;
        AlertasClinicos.Clear();
        AlertasClinicos.AddRange(clinicos);
        Alertas.Clear();
        Alertas.AddRange(administrativos);
        TermoPendente = pendente;
    }

    /// <summary>Relê só os avisos (depois de colher o termo — abrir a coleta já EMITE o termo numerado).</summary>
    public async Task RelerAvisosAsync()
    {
        using var escopo = escopos.CreateScope();
        await CarregarAlertasAsync(escopo.ServiceProvider, Rascunho.Data, _geracao);
        Avisar();
    }

    /// <summary>
    /// Grava a sessão e DIZ se conseguiu. O <c>bool</c> existe por causa do Finalizar: se a
    /// gravação falhar, o carimbo de fim não pode acontecer. O perfil Profissional conclui o
    /// horário junto (as guias aplicáveis), como no WPF — falhar a conclusão NÃO desfaz a
    /// evolução, e a frase diz isso.
    /// </summary>
    public async Task<bool> TentarSalvarAsync()
    {
        // Enquanto grava, o rascunho automático espera: guardar no meio criaria um rascunho
        // novo com o texto que acabou de ir para o prontuário.
        _salvando = true;
        try
        {
            if (!PodeEditarProntuario)
                throw new UnauthorizedAccessException("Use Atendimento de enfermagem para registrar sua evolução BSV.");
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");
            if (Contexto.PacienteId == 0)
                throw new InvalidOperationException("Escolha o paciente antes de escrever a sessão.");

            Evolucao salva;
            using (var escopo = escopos.CreateScope())
            {
                salva = await escopo.ServiceProvider.GetRequiredService<ProntuarioService>().SalvarAsync(
                    Rascunho.ParaGravacao(Contexto.PacienteId, SessaoUsuario.Atual.ProfissionalId,
                        Contexto.AgendamentoId, _atendimentoId, _definicoes),
                    SessaoUsuario.Atual.Operador, mapa: Mapa?.ParaGravacao());
            }

            Rascunho.EvolucaoId = salva.Id;
            UltimaGravacao = $"Última gravação às {DateTime.Now:HH':'mm}";
            // Foi para o prontuário: o rascunho encerra. Se falhar, o rascunho fica mais VELHO que
            // a gravação, e a rotina do prazo o encerra sem regravar (RascunhoEvolucao.Superado).
            try
            {
                using var escopoRascunho = escopos.CreateScope();
                if (escopoRascunho.ServiceProvider.GetService<IRascunhosDaSessao>() is { } rascunhos)
                    await rascunhos.EncerrarAsync(ChaveDoRascunho, salva.Id);
                RascunhoGuardadoEm = null;
            }
            catch (Exception ex)
            {
                Clinica.Application.Diagnostico.Registrar("Consultório — rascunho não encerrou depois do Salvar", ex);
            }
            Mensagem = Rascunho.FraseDepoisDeGravar();
            MensagemEhErro = false;

            if (salva.AgendamentoId is { } horario && SessaoUsuario.Atual.Perfil == PerfilAcesso.Profissional)
            {
                try
                {
                    using var escopoConclusao = escopos.CreateScope();
                    var fim = await escopoConclusao.ServiceProvider.GetRequiredService<AgendaService>()
                        .ConcluirAtendimentoClinicoAsync(horario, SessaoUsuario.Atual.Operador,
                            usuarioId: SessaoUsuario.Atual.UsuarioId, permitirEnfermagemPosterior: true);
                    _atendimentoId = fim.Atendimento.Id;
                    var quantidade = fim.Atendimento.Codigos.Count(c => c.Status != StatusCodigo.NaoAplicavel);
                    Mensagem = $"Sessão salva e concluída; {quantidade} guia(s) aplicável(is)."
                               + " A enfermagem pode registrar depois na mesma sessão."
                               + (fim.Avisos.Count > 0 ? " " + string.Join(" · ", fim.Avisos) : string.Empty);
                }
                catch (Exception ex)
                {
                    Clinica.Application.Diagnostico.Registrar("Evolução salva; conclusão e guias pendentes", ex);
                    Mensagem = "A evolução foi salva, mas a conclusão e as guias ficaram pendentes: " + ex.Message;
                    MensagemEhErro = true;
                    Avisar();
                    return false;
                }
            }

            // Depois de gravar, relê tudo — inclusive as anteriores e a curva da dor —, sem
            // perder a frase do desfecho (a carga começa zerando a mensagem).
            var frase = Mensagem;
            var erro = MensagemEhErro;
            await CarregarAsync();
            if (Mensagem is null)
            {
                Mensagem = frase;
                MensagemEhErro = erro;
            }
            Avisar();
            return true;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Consultório — sessão não pôde ser salva", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
            Avisar();
            return false;
        }
        finally { _salvando = false; }
    }
}
