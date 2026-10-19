using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Ui.Blazor.Componentes.SecoesDoPaciente.Enfermagem;

/// <summary>Um DIAGNÓSTICO de enfermagem sendo escrito — as etapas 2 e 3 do Processo.</summary>
public sealed class DiagnosticoEmEdicao
{
    /// <summary>Do catálogo, quando veio de lá. Nulo = escrito à mão, e isso é legítimo.</summary>
    public string? Codigo { get; init; }
    public string Titulo { get; set; } = string.Empty;
    public string RelacionadoA { get; set; } = string.Empty;
    public string EvidenciadoPor { get; set; } = string.Empty;
    public string ResultadoEsperado { get; set; } = string.Empty;

    public DiagnosticoEnfermagem Colher() => new()
    {
        Codigo = Codigo,
        Titulo = Titulo.Trim(),
        RelacionadoA = string.IsNullOrWhiteSpace(RelacionadoA) ? null : RelacionadoA.Trim(),
        EvidenciadoPor = string.IsNullOrWhiteSpace(EvidenciadoPor) ? null : EvidenciadoPor.Trim(),
        ResultadoEsperado = string.IsNullOrWhiteSpace(ResultadoEsperado) ? null : ResultadoEsperado.Trim()
    };
}

/// <summary>Um CUIDADO prescrito — a etapa 4. A frequência é parte do cuidado, não detalhe.</summary>
public sealed class CuidadoEmEdicao
{
    public string? Codigo { get; init; }
    public string Descricao { get; set; } = string.Empty;
    public string Frequencia { get; set; } = string.Empty;

    /// <summary>"Se necessário": SOS não conta como pendência — é a regra que esta caixinha sustenta.</summary>
    public bool SeNecessario { get; set; }

    /// <summary>Veio SUGERIDO por um diagnóstico do catálogo: proposta que não se distingue da decisão ninguém confere.</summary>
    public bool Sugerido { get; init; }

    public CuidadoEnfermagem Colher() => new()
    {
        Codigo = Codigo,
        Descricao = Descricao.Trim(),
        Frequencia = string.IsNullOrWhiteSpace(Frequencia) ? null : Frequencia.Trim(),
        SeNecessario = SeNecessario
    };
}

/// <summary>
/// O COMPOSITOR DA PASSAGEM DE ENFERMAGEM e a lista das passagens do paciente — o estado que as
/// portas Blazor compartilham (out/2026: substitui o <c>EvolucaoEnfermagemViewModel</c> WPF na
/// seção "Atendimento de enfermagem" e na tela Enfermagem; a janela da sala de infusão continua
/// com o WPF).
///
/// ⚠️ UM objeto para o compositor (<see cref="FolhaDaPassagem"/>), a lista
/// (<see cref="ListaDePassagens"/>) e a consulta COFEN (o formulário das cinco etapas): é isso
/// que faz "Corrigir" na lista trazer o registro para a folha da aba ao lado, e a consulta em
/// janela escrever no MESMO registro que o Registrar grava (a regra da parcela 49 — duas cópias
/// dariam duas verdades sobre o mesmo plano).
///
/// As regras caras — a hora INFORMADA, a hora futura recusada, a retificação que preserva a data
/// do fato, a alergia no mesmo SaveChanges — moram no <see cref="EvolucaoEnfermagemService"/>; o
/// que a tela afirma e lê, em <see cref="PassagemDeEnfermagemNaTela"/> (com teste).
///
/// Quem hospeda relê o que depende da passagem ao <see cref="Gravou"/> (o plano de cuidados, a
/// linha do tempo, a última aferição) e mostra a <see cref="Mensagem"/> numa superfície só.
/// </summary>
public sealed class EstadoDaPassagem
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _pacienteId;
    private readonly int? _prescricaoId;
    private readonly string? _folha;
    private int? _agendamentoId;

    /// <summary>Descarte de resposta fora de ordem: a lista recarrega a cada gravação.</summary>
    private int _geracaoCarga;
    private int _geracaoModalidade;

    /// <summary>Quando está corrigindo, o registro substituído e a DATA DO FATO dele.</summary>
    private int? _retificando;
    private DateOnly? _dataCorrigida;

    private bool _descartado;

    public EstadoDaPassagem(IServiceScopeFactory escopos, int pacienteId, string paciente,
        int? prescricaoId = null, string? folha = null, int? agendamentoId = null, DateOnly? dataDoAtendimento = null)
    {
        _escopos = escopos;
        _pacienteId = pacienteId;
        _prescricaoId = prescricaoId;
        _folha = folha;
        _agendamentoId = agendamentoId;
        Paciente = paciente;
        DataDoAtendimento = dataDoAtendimento ?? DateOnly.FromDateTime(DateTime.Today);
        Contexto = PassagemDeEnfermagemNaTela.DescreverContexto(prescricaoId, folha, agendamentoId);
        Hora = DateTime.Now.ToString("HH:mm");
    }

    /// <summary>Algo mudou por fora de um campo (carga, gravação, correção): quem desenha redesenha.</summary>
    public event Action? Mudou;

    /// <summary>A passagem foi GRAVADA (registrada, corrigida, cancelada ou vinculada).</summary>
    public event Action? Gravou;

    public int PacienteId => _pacienteId;
    public string Paciente { get; }
    public int? AgendamentoAberto => _agendamentoId;

    /// <summary>A que a passagem fica ligada — numa LINHA de texto, nunca uma faixa.</summary>
    public string Contexto { get; private set; }

    public bool Carregando { get; private set; }
    public bool Carregou { get; private set; }
    public bool NaoVerificado { get; private set; }
    public string? Mensagem { get; private set; }
    public bool MensagemEhErro { get; private set; }
    public IReadOnlyList<LinhaDaPassagem> Registros { get; private set; } = [];

    // ==================== O compositor ====================

    public DateOnly? DataDoAtendimento { get; private set; }
    public string Hora { get; set; }
    public string Texto { get; set; } = string.Empty;
    public bool Intercorrencia { get; set; }

    /// <summary>A PA num campo só ("120/80") — quem separa é <see cref="PressaoArterialTexto"/>.</summary>
    public string PressaoArterial { get; set; } = string.Empty;
    public string Cardiaca { get; set; } = string.Empty;
    public string Respiratoria { get; set; } = string.Empty;
    public string Temperatura { get; set; } = string.Empty;
    public string Saturacao { get; set; } = string.Empty;

    /// <summary>
    /// A dor aferida. A tela não a pede (o WPF também não), mas ela é PRESERVADA na correção:
    /// quem não edita, preserva — senão corrigir uma vírgula apagaria a dor registrada.
    /// </summary>
    public string Dor { get; set; } = string.Empty;

    public string AcessoLocal { get; set; } = string.Empty;
    public string AcessoCalibre { get; set; } = string.Empty;
    public DateOnly? AcessoPuncionadoEm { get; set; }

    /// <summary>O AGENTE (dipirona), nunca a reação — é esta palavra que a conferência procura.</summary>
    public string AlergiaObservada { get; set; } = string.Empty;

    /// <summary>As cinco etapas da COFEN estão valendo. Desligado, o registro é a anotação de passagem.</summary>
    public bool ConsultaCompleta { get; private set; }
    public string Historico { get; set; } = string.Empty;
    public string ExameFisico { get; set; } = string.Empty;
    public string Avaliacao { get; set; } = string.Empty;
    public List<DiagnosticoEmEdicao> Diagnosticos { get; } = [];
    public List<CuidadoEmEdicao> Cuidados { get; } = [];

    public bool Corrigindo { get; private set; }
    public string RotuloDoBotao => Corrigindo ? PassagemDeEnfermagemNaTela.RotuloCorrigir : PassagemDeEnfermagemNaTela.RotuloRegistrar;

    /// <summary>
    /// A data informada tem UMA sessão BSV do paciente (a evolução de enfermagem é exclusiva de BSV
    /// e se amarra à sessão). Sem ela o Registrar nasce apagado, e o aviso diz por quê.
    /// </summary>
    public bool SessaoBsvDisponivel { get; private set; }
    public string? AvisoModalidadeEnfermagem { get; private set; }

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> dentro de cada ato.</summary>
    public static bool PodeRegistrar => SessaoUsuario.Atual.Pode(Permissao.RegistrarEvolucaoEnfermagem);

    public bool PodeRegistrarNova => PodeRegistrar && (Corrigindo || SessaoBsvDisponivel);

    public string? EtapasEmFalta => PassagemDeEnfermagemNaTela.EtapasEmFalta(
        ConsultaCompleta, Historico,
        [.. Diagnosticos.Select(d => (d.Titulo, (string?)d.ResultadoEsperado))],
        Cuidados.Count, Avaliacao);

    public bool ConsultaTemConteudo => PassagemDeEnfermagemNaTela.ConsultaTemConteudo(
        Historico, ExameFisico, Avaliacao, Diagnosticos.Select(d => (string?)d.Titulo), Cuidados.Select(c => (string?)c.Descricao));

    /// <summary>Nada desta passagem foi digitado — a pergunta do "imprimir com rascunho?".</summary>
    public bool CompositorEmBranco => PassagemDeEnfermagemNaTela.CompositorEmBranco(
        Texto, AlergiaObservada, LerSinais(), LerAcesso(), Historico, ExameFisico, Avaliacao,
        Diagnosticos.Select(d => (string?)d.Titulo), Cuidados.Select(c => (string?)c.Descricao));

    /// <summary>
    /// A chave com que a seção guarda este estado na tela do paciente (ITelaDoPaciente.Guardado):
    /// a passagem em edição sobrevive à troca de seção no rail, e o "Finalizar" do cabeçalho a acha.
    /// </summary>
    public const string Chave = "enfermagem.passagem";

    /// <summary>
    /// O contrato de IPassagemAberta: nada escrito na passagem ainda não registrada. É o
    /// <see cref="CompositorEmBranco"/> — olha TUDO o que grava (a PA sozinha não é branco),
    /// nunca só o texto: senão o Finalizar encerraria calado sobre os sinais vitais digitados.
    /// </summary>
    public bool PassagemEmBranco => CompositorEmBranco;

    /// <summary>A tela saiu: carga que chega depois não escreve em nada.</summary>
    public void Descartar() => _descartado = true;

    private void Avisar()
    {
        if (!_descartado) Mudou?.Invoke();
    }

    private void Falar(string? texto, bool erro)
    {
        Mensagem = texto;
        MensagemEhErro = erro;
        Avisar();
    }

    // ==================== Leitura ====================

    /// <summary>
    /// Confere a sessão BSV da data e lê as passagens (da folha, quando ligada a uma; senão as
    /// 50 últimas do paciente). Falha vira o terceiro estado — "nenhuma passagem" e "não consegui
    /// ler as passagens" dizem coisas opostas a quem vai pendurar a próxima bomba.
    /// </summary>
    public async Task CarregarAsync()
    {
        if (_pacienteId == 0 && _prescricaoId is null)
        {
            Registros = [];
            Carregando = false;
            Carregou = true;
            Avisar();
            return;
        }

        var geracao = ++_geracaoCarga;
        Carregando = true;
        NaoVerificado = false;
        Avisar();

        try
        {
            await VerificarModalidadeAsync();

            IReadOnlyList<EvolucaoEnfermagem> lista;
            using (var escopo = _escopos.CreateScope())
            {
                var servico = escopo.ServiceProvider.GetRequiredService<EvolucaoEnfermagemService>();
                lista = _prescricaoId is { } id
                    ? await servico.DaPrescricaoAsync(id)
                    : await servico.DoPacienteAsync(_pacienteId, limite: 50);
            }

            if (geracao != _geracaoCarga || _descartado) return;
            Registros = PassagemDeEnfermagemNaTela.Linhas(lista, mostrarData: _prescricaoId is null, _agendamentoId);
            Carregou = true;
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga || _descartado) return;
            NaoVerificado = true;
            Carregou = true;
            Clinica.Application.Diagnostico.Registrar("Enfermagem — evolução não pôde ser carregada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            if (geracao == _geracaoCarga) Carregando = false;
            Avisar();
        }
    }

    /// <summary>
    /// A data tem UMA sessão BSV? A recusa do serviço vira o AVISO da folha (é regra, não falha:
    /// não vai para o log); exceção que não é recusa vai.
    /// </summary>
    public async Task VerificarModalidadeAsync()
    {
        var geracao = ++_geracaoModalidade;
        SessaoBsvDisponivel = false;
        if (_pacienteId == 0) { Avisar(); return; }

        try
        {
            var data = DataDoAtendimento ?? throw new InvalidOperationException("Informe a data da sessão BSV.");
            using var escopo = _escopos.CreateScope();
            await escopo.ServiceProvider.GetRequiredService<EvolucaoEnfermagemService>()
                .ResolverSessaoBsvAsync(_pacienteId, data, _agendamentoId);
            if (geracao != _geracaoModalidade || _descartado) return;
            SessaoBsvDisponivel = true;
            AvisoModalidadeEnfermagem = null;
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoModalidade || _descartado) return;
            AvisoModalidadeEnfermagem = ex.Message;
            if (ex is not InvalidOperationException) // recusa de regra é aviso, não defeito
                Clinica.Application.Diagnostico.Registrar("Conferência da sessão BSV para enfermagem", ex);
        }
        Avisar();
    }

    /// <summary>A pessoa trocou a data do atendimento: reconfere a sessão BSV dela.</summary>
    public Task TrocarDataAsync(DateOnly? data)
    {
        DataDoAtendimento = data;
        return VerificarModalidadeAsync();
    }

    /// <summary>
    /// Amarra a passagem ao horário em curso quando quem monta o compositor descobre isso DEPOIS
    /// (a tela Enfermagem lê o dia depois de abrir o paciente). Só quando MUDA — e então relê,
    /// senão o selo "DESTA SESSÃO" nunca acenderia.
    /// </summary>
    public Task FixarHorarioAsync(int? agendamentoId)
    {
        if (_agendamentoId == agendamentoId) return Task.CompletedTask;
        _agendamentoId = agendamentoId;
        Contexto = PassagemDeEnfermagemNaTela.DescreverContexto(_prescricaoId, _folha, agendamentoId);
        return CarregarAsync();
    }

    // ==================== Escrita ====================

    /// <summary>
    /// Grava a passagem (ou a correção). Duas barreiras: o botão nasce apagado sem o bit, e
    /// este <c>Exigir</c> impede. Cancelar a pergunta do motivo da correção é desistir, nunca
    /// gravar com motivo em branco (checagem 39).
    /// </summary>
    public async Task RegistrarAsync(Dialogos dialogos)
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.RegistrarEvolucaoEnfermagem, "registrar evolução de enfermagem");

            if (PassagemDeEnfermagemNaTela.LerHora(Hora) is not { } hora)
            {
                Falar(PassagemDeEnfermagemNaTela.HoraInvalida, true);
                return;
            }

            var sinais = LerSinais();
            var acesso = LerAcesso();

            string? motivo = null;
            if (Corrigindo)
            {
                motivo = await dialogos.PerguntarTextoAsync(
                    PassagemDeEnfermagemNaTela.PerguntaCorrigirTitulo, PassagemDeEnfermagemNaTela.PerguntaCorrigir);
                if (string.IsNullOrWhiteSpace(motivo)) return;
            }

            var dia = DataDoAtendimento
                ?? throw new InvalidOperationException("Informe a data em que ocorreu o atendimento de enfermagem.");

            using (var escopo = _escopos.CreateScope())
            {
                var servico = escopo.ServiceProvider.GetRequiredService<EvolucaoEnfermagemService>();
                if (Corrigindo && _retificando is { } alvo)
                    // ⚠️ A data é a DO FATO, nunca a de hoje: corrigir na segunda o registro do
                    // sábado não pode mover o fato de dia. E o PROCESSO vai junto.
                    await servico.RetificarAsync(
                        alvo, _dataCorrigida ?? dia, hora, Texto, Executante(), motivo!,
                        Intercorrencia, sinais, ColherProcesso(), acesso);
                else
                    await servico.RegistrarAsync(
                        _pacienteId, dia, hora, Texto, Executante(),
                        _prescricaoId, _agendamentoId, Intercorrencia, sinais,
                        string.IsNullOrWhiteSpace(AlergiaObservada) ? null : AlergiaObservada,
                        ColherProcesso(), acesso);
            }

            var alergia = !string.IsNullOrWhiteSpace(AlergiaObservada);
            LimparCompositor();
            await CarregarAsync();
            Falar(PassagemDeEnfermagemNaTela.Registrado(alergia), false);
            // Depois da frase: o hospedeiro relê e só escreve quando a leitura FALHA.
            if (!_descartado) Gravou?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Enfermagem — evolução não pôde ser registrada", ex);
            Falar(ex.Message, true);
        }
    }

    /// <summary>
    /// Traz o registro INTEIRO para o compositor a fim de corrigi-lo (sinais como números, o
    /// acesso, as cinco etapas, o "se necessário" de cada cuidado). O anterior não é apagado.
    /// </summary>
    public async Task CorrigirAsync(LinhaDaPassagem linha)
    {
        try
        {
            EvolucaoEnfermagem? original;
            using (var escopo = _escopos.CreateScope())
                original = await escopo.ServiceProvider.GetRequiredService<IClinicaRepositorio>()
                    .ObterEvolucaoEnfermagemAsync(linha.Id);
            if (_descartado) return;
            if (original is null)
            {
                Falar(PassagemDeEnfermagemNaTela.RegistroNaoEncontrado, true);
                return;
            }

            _retificando = original.Id;
            _dataCorrigida = original.Data;
            Corrigindo = true;

            Hora = original.Hora.ToString("HH\\:mm");
            Texto = original.Texto;
            Intercorrencia = original.Intercorrencia;
            PressaoArterial = PressaoArterialTexto.Juntar(Num(original.PressaoSistolica), Num(original.PressaoDiastolica));
            Cardiaca = Num(original.FrequenciaCardiaca);
            Respiratoria = Num(original.FrequenciaRespiratoria);
            Temperatura = original.Temperatura?.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) ?? string.Empty;
            Saturacao = Num(original.SaturacaoOxigenio);
            Dor = Num(original.Dor);

            AcessoLocal = original.AcessoLocal ?? string.Empty;
            AcessoCalibre = original.AcessoCalibre ?? string.Empty;
            AcessoPuncionadoEm = original.AcessoPuncionadoEm;

            Historico = original.Historico ?? string.Empty;
            ExameFisico = original.ExameFisico ?? string.Empty;
            Avaliacao = original.Avaliacao ?? string.Empty;

            Diagnosticos.Clear();
            foreach (var d in original.Diagnosticos.OrderBy(d => d.Ordem))
                Diagnosticos.Add(new DiagnosticoEmEdicao
                {
                    Codigo = d.Codigo, Titulo = d.Titulo,
                    RelacionadoA = d.RelacionadoA ?? string.Empty,
                    EvidenciadoPor = d.EvidenciadoPor ?? string.Empty,
                    ResultadoEsperado = d.ResultadoEsperado ?? string.Empty
                });

            Cuidados.Clear();
            foreach (var c in original.Cuidados.OrderBy(c => c.Ordem))
                Cuidados.Add(new CuidadoEmEdicao
                {
                    Codigo = c.Codigo, Descricao = c.Descricao,
                    Frequencia = c.Frequencia ?? string.Empty,
                    // Sem esta linha, retificar DESLIGARIA o "se necessário" em silêncio.
                    SeNecessario = c.SeNecessario
                });

            // Por CÓDIGO: carregar não pergunta "voltar para anotação?" (pergunta sem gesto se fecha sem ler).
            ConsultaCompleta = original.EhConsulta;

            Falar(PassagemDeEnfermagemNaTela.Corrigindo(original.Data, original.Hora), false);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Enfermagem — registro não pôde ser aberto para correção", ex);
            Falar(ex.Message, true);
        }
    }

    public void CancelarCorrecao()
    {
        LimparCompositor();
        Falar(null, false);
    }

    /// <summary>Cancela o registro lançado no paciente ou na sessão ERRADA — a linha FICA, com o motivo.</summary>
    public async Task CancelarRegistroAsync(LinhaDaPassagem linha, Dialogos dialogos)
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.RegistrarEvolucaoEnfermagem, "cancelar evolução de enfermagem");

            var motivo = await dialogos.PerguntarTextoAsync(
                PassagemDeEnfermagemNaTela.PerguntaCancelarTitulo, PassagemDeEnfermagemNaTela.PerguntaCancelar);
            if (string.IsNullOrWhiteSpace(motivo)) return;

            using (var escopo = _escopos.CreateScope())
                await escopo.ServiceProvider.GetRequiredService<EvolucaoEnfermagemService>()
                    .CancelarAsync(linha.Id, motivo, SessaoUsuario.Atual.Operador);

            await CarregarAsync();
            Falar(PassagemDeEnfermagemNaTela.Cancelado, false);
            if (!_descartado) Gravou?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Enfermagem — registro não pôde ser cancelado", ex);
            Falar(ex.Message, true);
        }
    }

    /// <summary>
    /// O primeiro passo de "Vincular à sessão": confere o registro e devolve as sessões do dia
    /// DELE que podem recebê-lo. Nulo = não dá (e a frase já foi dita).
    /// </summary>
    public async Task<IReadOnlyList<SessaoParaVincular>?> SessoesParaVincularAsync(LinhaDaPassagem linha)
    {
        if (!linha.PodeVincular) return null;
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.RegistrarEvolucaoEnfermagem, "vincular a evolução de enfermagem");
            using var escopo = _escopos.CreateScope();
            var repo = escopo.ServiceProvider.GetRequiredService<IClinicaRepositorio>();
            var registro = await repo.ObterEvolucaoEnfermagemAsync(linha.Id)
                ?? throw new InvalidOperationException("Registro não encontrado.");
            if (registro.PacienteId != _pacienteId)
                throw new InvalidOperationException("O registro pertence a outro paciente.");
            var sessoes = PassagemDeEnfermagemNaTela.SessoesParaVincular(
                await repo.AgendamentosDoPacienteNoDiaAsync(_pacienteId, registro.Data));
            if (sessoes.Count == 0) throw new InvalidOperationException(PassagemDeEnfermagemNaTela.SemSessaoParaVincular);
            return sessoes;
        }
        catch (Exception ex)
        {
            Falar(ex.Message, true);
            return null;
        }
    }

    /// <summary>O segundo passo: grava o vínculo com o motivo. A conclusão e as guias ficam como estão.</summary>
    public async Task VincularAsync(int evolucaoId, int agendamentoId, string motivo)
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.RegistrarEvolucaoEnfermagem, "vincular a evolução de enfermagem");
            using (var escopo = _escopos.CreateScope())
                await escopo.ServiceProvider.GetRequiredService<EvolucaoEnfermagemService>()
                    .VincularSessaoAsync(evolucaoId, agendamentoId, SessaoUsuario.Atual.UsuarioId, motivo);
            await CarregarAsync();
            Falar(PassagemDeEnfermagemNaTela.Vinculado, false);
            if (!_descartado) Gravou?.Invoke();
        }
        catch (Exception ex)
        {
            Falar(ex.Message, true);
        }
    }

    // ==================== A consulta de enfermagem (COFEN 358/2009) ====================

    /// <summary>Marca a consulta (a porta a abre em janela). Desmarcar passa por <see cref="DesmarcarConsultaAsync"/>.</summary>
    public void MarcarConsulta()
    {
        ConsultaCompleta = true;
        Avisar();
    }

    /// <summary>
    /// ⚠️ DESMARCAR DESCARTA AS CINCO ETAPAS na gravação, e esse custo não se vê na tela (a
    /// consulta mora numa janela). Por isso a pergunta — e só quando há o que perder.
    /// Verdadeiro = desmarcou.
    /// </summary>
    public async Task<bool> DesmarcarConsultaAsync(Dialogos dialogos)
    {
        if (ConsultaTemConteudo)
        {
            var seguir = await dialogos.ConfirmarAsync(
                PassagemDeEnfermagemNaTela.ConfirmarAnotacaoTitulo, PassagemDeEnfermagemNaTela.ConfirmarAnotacao, perigoso: true);
            if (!seguir) { Avisar(); return false; }
        }
        ConsultaCompleta = false;
        Avisar();
        return true;
    }

    /// <summary>Traz o diagnóstico do catálogo E os cuidados que ele costuma pedir — marcados como sugeridos.</summary>
    public void AdicionarDiagnostico(string codigo)
    {
        if (CatalogoEnfermagem.Diagnostico(codigo) is not { } escolhido) return;
        if (Diagnosticos.Any(d => d.Codigo == escolhido.Codigo)) return;

        Diagnosticos.Add(new DiagnosticoEmEdicao
        {
            Codigo = escolhido.Codigo, Titulo = escolhido.Titulo, ResultadoEsperado = escolhido.ResultadoEsperado
        });
        foreach (var c in CatalogoEnfermagem.CuidadosDe(escolhido.Codigo))
            if (!Cuidados.Any(x => x.Codigo == c.Codigo))
                Cuidados.Add(new CuidadoEmEdicao { Codigo = c.Codigo, Descricao = c.Descricao, Frequencia = c.FrequenciaSugerida, Sugerido = true });
        Avisar();
    }

    public void AdicionarCuidado(string codigo)
    {
        if (CatalogoEnfermagem.Cuidado(codigo) is not { } escolhido) return;
        if (Cuidados.Any(c => c.Codigo == escolhido.Codigo)) return;
        Cuidados.Add(new CuidadoEmEdicao { Codigo = escolhido.Codigo, Descricao = escolhido.Descricao, Frequencia = escolhido.FrequenciaSugerida });
        Avisar();
    }

    public bool DiagnosticoNoPlano(string codigo) => Diagnosticos.Any(d => d.Codigo == codigo);
    public bool CuidadoNoPlano(string codigo) => Cuidados.Any(c => c.Codigo == codigo);

    public void NovoDiagnostico() { Diagnosticos.Add(new DiagnosticoEmEdicao()); Avisar(); }
    public void RemoverDiagnostico(DiagnosticoEmEdicao d) { Diagnosticos.Remove(d); Avisar(); }
    public void NovoCuidado() { Cuidados.Add(new CuidadoEmEdicao()); Avisar(); }
    public void RemoverCuidado(CuidadoEmEdicao c) { Cuidados.Remove(c); Avisar(); }

    // ==================== Apoio ====================

    private SinaisVitais? LerSinais() => PassagemDeEnfermagemNaTela.LerSinaisVitais(
        PressaoArterial, Cardiaca, Respiratoria, Temperatura, Saturacao, Dor);

    private AcessoVenoso? LerAcesso() => PassagemDeEnfermagemNaTela.LerAcesso(AcessoLocal, AcessoCalibre, AcessoPuncionadoEm);

    /// <summary>As cinco etapas, ou nulo no modo anotação — é o que faz o registro curto continuar curto.</summary>
    private ProcessoDeEnfermagem? ColherProcesso() => !ConsultaCompleta
        ? null
        : new ProcessoDeEnfermagem(
            Historico, ExameFisico, Avaliacao,
            [.. Diagnosticos.Where(d => !string.IsNullOrWhiteSpace(d.Titulo)).Select(d => d.Colher())],
            [.. Cuidados.Where(c => !string.IsNullOrWhiteSpace(c.Descricao)).Select(c => c.Colher())]);

    private void LimparCompositor()
    {
        // As etapas saem JUNTO: sem isto o próximo registro nasceria com os diagnósticos do anterior.
        Historico = ExameFisico = Avaliacao = string.Empty;
        Diagnosticos.Clear();
        Cuidados.Clear();
        ConsultaCompleta = false;
        _retificando = null;
        _dataCorrigida = null;
        Corrigindo = false;
        Texto = string.Empty;
        Intercorrencia = false;
        AlergiaObservada = string.Empty;
        PressaoArterial = Cardiaca = Respiratoria = Temperatura = Saturacao = Dor = string.Empty;
        AcessoLocal = AcessoCalibre = string.Empty;
        AcessoPuncionadoEm = null;
        Hora = DateTime.Now.ToString("HH:mm");
    }

    private static string Num(int? valor) => valor?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>Quem escreve é quem fez LOGIN, com o COREN do cadastro dele (copiado no ato).</summary>
    public static IdentificacaoExecutante Executante() => new(
        UsuarioId: SessaoUsuario.Atual.Autenticado ? SessaoUsuario.Atual.UsuarioId : null,
        Nome: SessaoUsuario.Atual.Autenticado ? SessaoUsuario.Atual.Nome : SessaoUsuario.Atual.Operador,
        Conselho: SessaoUsuario.Atual.RegistroConselho);
}
