using Clinica.Application.Abstracoes;
using Clinica.Application.Assinatura;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Application.Modelos;

/// <summary>
/// Um item da folha, do ponto de vista de quem executa (subiu do <c>FolhaExecucaoViewModel</c>
/// WPF em out/2026, sem mudança: a folha virou formulário Blazor, e o que a linha AFIRMA mora
/// onde o <c>dotnet test</c> alcança).
///
/// O <see cref="Marca"/> é o "chequezinho" ou a "rodela" do papel — a linguagem visual da
/// enfermagem, que quem lê esta tela lê em centenas de folhas de hospital.
/// </summary>
public sealed class LinhaExecucaoItem
{
    public required int ItemId { get; init; }
    public required int Ordem { get; init; }
    public required string Descricao { get; init; }
    public required string Detalhe { get; init; }
    public required string Situacao { get; init; }
    public required string Marca { get; init; }
    public required string? Justificativa { get; init; }
    public required string? Executante { get; init; }
    public required bool Pendente { get; init; }
    public required bool Realizado { get; init; }
    public required bool NaoRealizado { get; init; }
    public required bool Suspenso { get; init; }
    public required bool SeNecessario { get; init; }

    /// <summary>
    /// A coincidência com ALERGIA registrada deste paciente, escrita na LINHA — não no topo da
    /// folha (parcela 72): o alerta do cabeçalho não diz QUAL das seis linhas é a dipirona.
    /// </summary>
    public required string? AlertaAlergia { get; init; }

    public bool TemAlertaAlergia => !string.IsNullOrWhiteSpace(AlertaAlergia);

    /// <summary>Já tem checagem: o caminho é retificar, não checar de novo.</summary>
    public bool Checado => Realizado || NaoRealizado;

    public static LinhaExecucaoItem De(ItemPrescricaoInterna item, string? alertaAlergia = null)
    {
        var checagem = item.ChecagemVigente;
        var situacao = item.Situacao;

        var detalhe = string.Join("  ·  ", new[]
        {
            item.GrupoInfusao.HasValue ? GruposInfusao.Rotulo(item) : null,
            RotulosEnum.De(item.Via),
            item.TempoInfusao,
            item.HoraPrevista is { } h ? $"previsto {h:HH\\:mm}" : null,
            item.SeNecessario ? "se necessário (SOS)" : null,
            item.Observacoes
        }.Where(p => !string.IsNullOrWhiteSpace(p)));

        var marca = situacao switch
        {
            SituacaoItemPrescricao.Realizado => $"✓ {(checagem!.DataRealizacao ?? DateOnly.FromDateTime(checagem.RegistradoEm)):dd/MM/yyyy} às {checagem.HoraRealizacao:HH\\:mm}",
            SituacaoItemPrescricao.NaoRealizado or SituacaoItemPrescricao.NaoExecutavel => $"○ {(checagem!.DataRealizacao ?? DateOnly.FromDateTime(checagem.RegistradoEm)):dd/MM/yyyy} às {checagem.HoraRealizacao:HH\\:mm}",
            SituacaoItemPrescricao.Suspenso => "suspenso",
            _ => "—"
        };

        return new LinhaExecucaoItem
        {
            ItemId = item.Id,
            Ordem = item.Ordem,
            Descricao = item.GrupoInfusao.HasValue ? string.Join(" · ", new[] { item.Descricao, item.Dose }.Where(s => !string.IsNullOrWhiteSpace(s))) : item.TextoCompleto,
            Detalhe = detalhe,
            Situacao = RotulosEnum.De(situacao),
            Marca = marca,
            Justificativa = item.Suspenso
                ? $"Suspenso pelo prescritor: {item.MotivoSuspensao}"
                : checagem?.Justificativa,
            Executante = checagem is null
                ? null
                : string.Join(" · ", new[] { checagem.ExecutanteNome, checagem.ExecutanteConselho }
                    .Where(p => !string.IsNullOrWhiteSpace(p))),
            Pendente = situacao == SituacaoItemPrescricao.Pendente,
            Realizado = situacao == SituacaoItemPrescricao.Realizado,
            NaoRealizado = situacao is SituacaoItemPrescricao.NaoRealizado or SituacaoItemPrescricao.NaoExecutavel,
            Suspenso = item.Suspenso,
            SeNecessario = item.SeNecessario,
            AlertaAlergia = alertaAlergia
        };
    }
}

/// <summary>
/// A FOLHA DE EXECUÇÃO — o estado que a tela desenha (out/2026: era o
/// <c>FolhaExecucaoViewModel</c> WPF, sem os comandos de tela). A CARGA, as duas barreiras
/// visíveis e as frases moram aqui; os comandos que perguntam (justificativa, motivo, alergia,
/// certificado) moram no formulário Blazor, que chama os MESMOS serviços com os MESMOS
/// argumentos. A ordem das assinaturas mora em <see cref="AssinaturaDaPrescricaoNaTela"/>.
///
/// As três coisas que a tela cobra continuam as de sempre: a hora é DIGITADA (vem preenchida só
/// como sugestão), "não realizado" pede justificativa antes de gravar, e a reação alérgica vira
/// ALERGIA no prontuário se quem checou confirmar.
/// </summary>
public sealed class FolhaDeExecucaoNaTela
{
    private readonly int _prescricaoId;

    /// <summary>Último paciente cujo acesso já entrou na trilha — a folha recarrega a cada checagem.</summary>
    private int _acessoRegistradoDe;
    private int? _arquivoConferido;

    public FolhaDeExecucaoNaTela(int prescricaoId) => _prescricaoId = prescricaoId;

    public int PrescricaoId => _prescricaoId;

    /// <summary>O paciente da folha, para abrir a evolução de enfermagem.</summary>
    public int PacienteId { get; private set; }

    public IReadOnlyList<LinhaExecucaoItem> Itens { get; private set; } = [];
    public IReadOnlyList<string> Alertas { get; private set; } = [];

    public string Numero { get; private set; } = string.Empty;
    public string Paciente { get; private set; } = string.Empty;
    public string Cabecalho { get; private set; } = string.Empty;
    public string? DiluicaoTotal { get; private set; }
    public string Resumo { get; private set; } = string.Empty;
    public bool NaoVerificado { get; private set; }
    public bool Carregou { get; private set; }
    public bool TemAlertas => Alertas.Count > 0;

    /// <summary>Medicação de uso contínuo do paciente, já montada em uma linha.</summary>
    public string? MedicacaoEmUso { get; private set; }
    public bool EmExecucao { get; private set; }
    public bool ExecucaoCompleta { get; private set; }

    /// <summary>Já houve checagem — o registro de execução tem o que mostrar.</summary>
    public bool TemRegistroExecucao { get; private set; }
    public bool ExibirRegistroSeparado { get; private set; }
    public string? AvisoPdfHistorico { get; private set; }

    /// <summary>A folha nasceu pedindo a 2ª assinatura (a eletrônica da enfermagem).</summary>
    public bool ExigeAssinaturaEletronica { get; private set; }
    public bool ContinuidadeAtiva { get; private set; }

    /// <summary>Encerrada, pedindo a 2ª assinatura, e ela ainda não foi colhida.</summary>
    public bool AguardaAssinaturaExecucao { get; private set; }

    /// <summary>Estado da 2ª assinatura, por extenso — vazio esconde a linha.</summary>
    public string? SituacaoAssinaturaExecucao { get; private set; }
    public bool OrigemEnfermagem { get; private set; }
    public bool PodeValidarMedico { get; private set; }
    public bool PodeDevolverMedico { get; private set; }
    public bool PodeRevisarDevolucao { get; private set; }
    public string? MotivoDevolucao { get; private set; }
    public bool PodeCorrigirHorarios { get; private set; }
    public bool PodeCancelarInfusao { get; private set; }
    public DateOnly DataPrescricao { get; private set; }
    public TimeOnly HoraPrescricao { get; private set; }
    public DateOnly DataExecucao { get; private set; }
    public TimeOnly HoraExecucao { get; private set; }

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public static bool PodeChecar => SessaoUsuario.Atual.Pode(Permissao.ChecarPrescricao) && TemConselho;

    /// <summary>
    /// ⚠️ A METADE VISÍVEL da recusa do COREN (parcela 72): o serviço RECUSA checar sem o registro
    /// no conselho; aqui o botão apaga e a frase aparece na ABERTURA, não no clique.
    /// </summary>
    public static bool TemConselho => !string.IsNullOrWhiteSpace(SessaoUsuario.Atual.RegistroConselho);

    /// <summary>A frase que aparece na ABERTURA da folha, não no clique.</summary>
    public static string? AvisoDoConselho => TemConselho
        ? null
        : "O seu login não tem o registro no conselho (COREN/CRM), e a checagem fica "
          + "bloqueada: o número é copiado para o prontuário no momento em que você checa. "
          + "Peça à direção para preencher o registro na ficha do profissional (Equipe) e "
          + "vincular essa ficha ao seu login (Acessos).";

    /// <summary>Bit próprio da evolução de enfermagem — a folha ENCERRADA continua aceitando registro.</summary>
    public static bool PodeRegistrarEnfermagem =>
        SessaoUsuario.Atual.Pode(Permissao.RegistrarEvolucaoEnfermagem) && TemConselho;

    /// <summary>Só folha em execução se checa.</summary>
    public bool PodeMexer => PodeChecar && EmExecucao;

    /// <summary>Encerrar exige tudo checado, e o botão diz isso antes do clique.</summary>
    public bool PodeEncerrar => PodeMexer && ExecucaoCompleta;

    /// <summary>Suspender é ato de QUEM PRESCREVE — bit <see cref="Permissao.Prescrever"/>.</summary>
    public bool PodeSuspender => SessaoUsuario.Atual.Pode(Permissao.Prescrever) && EmExecucao;

    /// <summary>Assinar a execução é ato de quem EXECUTA — o mesmo bit da checagem.</summary>
    public bool PodeAssinarExecucao => PodeChecar && AguardaAssinaturaExecucao;

    // ===== As frases (as MESMAS do WPF) =====

    public const string ItemSemChecagem = "Ainda há item sem checagem. Uma folha encerrada com item em aberto "
                                          + "não diz se a medicação entrou no paciente.";

    public string ConfirmacaoDeEncerrar => $"Encerrar a execução da prescrição {Numero}? Depois disso a folha não "
                                           + "pode mais ser checada.\n\n"
                                           + (ContinuidadeAtiva ? "O registro será salvo sem exigir assinatura digital ou impressão." : ExigeAssinaturaEletronica
                                               ? "Após encerrar, assine eletronicamente a prescrição para arquivar a execução."
                                               : "Lembre de assinar a via impressa — é ela que responde pela "
                                                 + "execução.");

    public string EncerradaFrase => ContinuidadeAtiva ? "✓ Execução salva sem assinatura digital. Impressão disponível." : ExigeAssinaturaEletronica
        ? "✓ Execução encerrada. Falta a assinatura eletrônica da enfermagem — o "
          + "botão \"Assinar execução\" fica nesta folha."
        : "✓ Execução encerrada. Confira e assine a via impressa.";

    public const string SemLoginParaAssinar = "Entre com o seu usuário para assinar a execução: a assinatura "
                                              + "é conferida contra o CPF de quem está logado.";

    /// <summary>
    /// Lê a folha. Falha vira o terceiro estado (<see cref="NaoVerificado"/>) e a frase — nunca
    /// uma folha vazia que se leria como "esta prescrição não tem itens".
    /// </summary>
    /// <returns>Nulo quando leu; senão, a frase do erro.</returns>
    public async Task<string?> CarregarAsync(IServiceScopeFactory escopos)
    {
        try
        {
            NaoVerificado = false;

            using var scope = escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<PrescricaoInternaService>();

            var prescricao = await servico.ObterAsync(_prescricaoId)
                ?? throw new InvalidOperationException("Prescrição não encontrada.");

            // Trilha de LEITURA (parcela 52): registrada na troca de prescrição (a folha recarrega
            // a cada checagem, e o paciente é o mesmo).
            if (_acessoRegistradoDe != prescricao.PacienteId)
            {
                _acessoRegistradoDe = prescricao.PacienteId;
                await scope.ServiceProvider.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(prescricao.PacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.Documento);
            }

            Numero = prescricao.Numero;
            OrigemEnfermagem = prescricao.OrigemEnfermagem;
            var checagemExterna = prescricao.Itens.Select(i => i.ChecagemVigente).FirstOrDefault(c => c is not null);
            DataPrescricao = prescricao.Data;
            HoraPrescricao = prescricao.Hora;
            DataExecucao = checagemExterna?.DataRealizacao ?? prescricao.Data;
            HoraExecucao = checagemExterna?.HoraRealizacao ?? prescricao.Hora;
            PodeCorrigirHorarios = prescricao.OrigemEnfermagem && prescricao.RegistradaPorUsuarioId == SessaoUsuario.Atual.UsuarioId
                && prescricao.Assinaturas.Count == 0 && prescricao.AssinadaEm is null && prescricao.LiberadaSemAssinaturaEm is null;
            PodeCancelarInfusao = !prescricao.Cancelada
                && ((SessaoUsuario.Atual.Pode(Permissao.Prescrever) && prescricao.ProfissionalId == SessaoUsuario.Atual.ProfissionalId)
                    || (prescricao.OrigemEnfermagem && prescricao.RegistradaPorUsuarioId == SessaoUsuario.Atual.UsuarioId
                        && SessaoUsuario.Atual.Pode(Permissao.ChecarPrescricao)))
                && (prescricao.OrigemEnfermagem || ((prescricao.Situacao is SituacaoPrescricao.Rascunho or SituacaoPrescricao.Assinada or SituacaoPrescricao.Liberada)
                    && !prescricao.Itens.Any(i => i.ChecagemVigente is not null)));
            PodeValidarMedico = prescricao.AguardaValidacaoMedica
                && (prescricao.ModoSemAssinatura || prescricao.AssinaturaDaExecucao?.ArquivoId is not null)
                && prescricao.ProfissionalId == SessaoUsuario.Atual.ProfissionalId
                && SessaoUsuario.Atual.Perfil != PerfilAcesso.Enfermagem
                && SessaoUsuario.Atual.Pode(Permissao.Prescrever);
            PodeDevolverMedico = PodeValidarMedico;
            PodeRevisarDevolucao = prescricao.DevolvidaEm is not null && prescricao.Retificacao is null
                && prescricao.RegistradaPorUsuarioId == SessaoUsuario.Atual.UsuarioId
                && SessaoUsuario.Atual.Pode(Permissao.ChecarPrescricao);
            MotivoDevolucao = prescricao.MotivoDevolucao;
            PacienteId = prescricao.PacienteId;
            Paciente = prescricao.Paciente?.Nome ?? "—";
            Cabecalho = $"{(prescricao.DevolvidaEm is not null ? "Devolvida à enfermagem" : prescricao.AguardaValidacaoMedica ? "Aguarda validação médica" : RotulosEnum.De(prescricao.Situacao))} · "
                      + $"{prescricao.Data:dd/MM/yyyy} às {prescricao.Hora:HH\\:mm} · "
                      + $"{(prescricao.OrigemEnfermagem ? "médico responsável" : "prescrita por")} {prescricao.Profissional?.Nome ?? "—"}";
            DiluicaoTotal = prescricao.DiluicaoUnica ? $"Diluente: {prescricao.DiluenteGlobal ?? "Não informado"} · Volume total: {prescricao.VolumeTotal ?? "Não informado"} (toda a infusão)" : null;
            Resumo = $"{prescricao.Realizados} realizados · {prescricao.NaoRealizados} não "
                   + $"realizados · {prescricao.Pendentes} aguardando";

            EmExecucao = prescricao.PodeChecar;
            ExecucaoCompleta = prescricao.ExecucaoCompleta;
            TemRegistroExecucao = prescricao.Realizados + prescricao.NaoRealizados > 0;
            ExibirRegistroSeparado = prescricao.AssinaturaDaExecucao is { ArquivoRegistroId: not null } antiga
                && antiga.ArquivoRegistroId != antiga.ArquivoId;
            // PDFs anteriores aos campos reservados não podem receber dados retroativamente.
            if (!prescricao.OrigemEnfermagem && prescricao.AssinaturaDaExecucao?.ArquivoId is { } arquivoId
                && _arquivoConferido != arquivoId)
            {
                var arquivo = await scope.ServiceProvider.GetRequiredService<IClinicaRepositorio>()
                    .ObterArquivoAssinadoAsync(arquivoId);
                AvisoPdfHistorico = arquivo is not null && !CamposExecucaoPdf.TemCampos(arquivo.Conteudo)
                    ? "PDF histórico: as assinaturas foram feitas sem incorporar os dados de execução. "
                      + "Consulte os horários, resultados e justificativas nesta tela. A impressão preserva o PDF originalmente assinado."
                    : null;
                _arquivoConferido = arquivo is not null ? arquivoId : null;
            }

            ContinuidadeAtiva = await ContinuidadeSemAssinatura.HabilitadaAsync(scope.ServiceProvider.GetRequiredService<IClinicaRepositorio>());
            ExigeAssinaturaEletronica = prescricao.ExigeAssinaturaEletronicaDaExecucao && !ContinuidadeAtiva;
            AguardaAssinaturaExecucao = prescricao.AguardaAssinaturaDaExecucao && !ContinuidadeAtiva;
            SituacaoAssinaturaExecucao = prescricao.AssinaturaDaExecucao is { } daExecucao
                ? $"Execução assinada eletronicamente por {daExecucao.NomeAssinante} "
                  + $"em {daExecucao.AssinadoEm:dd/MM/yyyy HH\\:mm}."
                : prescricao.ModoSemAssinatura ? "Registro salvo sem assinatura digital. Impressão disponível." : prescricao.AguardaAssinaturaDaExecucao
                    ? "Esta folha pede a assinatura eletrônica da enfermagem — falta colhê-la."
                    : null;

            // ⚠️ CONFERIR, não só o CONTEXTO (parcela 72): casa item a item pelo TextoCompleto.
            var conferencia = await servico.ConferirAsync(prescricao);

            var alertaPorItem = conferencia.Alertas
                .Where(a => a.Gravidade == GravidadePrescricao.Alergia)
                .GroupBy(a => a.Item)
                .ToDictionary(g => g.Key, g => string.Join(" · ", g.Select(a => a.Motivo)));

            Itens = prescricao.Itens.OrderBy(i => i.Ordem).ThenBy(i => i.Id)
                .Select(item => LinhaExecucaoItem.De(item, alertaPorItem.GetValueOrDefault(item.TextoCompleto)))
                .ToList();

            var alertas = new List<string>();
            if (ProblemaPacienteService.ResumirAlergias(conferencia.Alergias) is { } alergias)
                alertas.Add(alergias);
            Alertas = alertas;

            // A medicação de uso contínuo é CONTEXTO, não alerta.
            MedicacaoEmUso = conferencia.MedicacoesEmUso.Count == 0
                ? null
                : "Em uso contínuo: "
                  + string.Join(" · ", conferencia.MedicacoesEmUso.Select(m => m.Rotulo));
            Carregou = true;
            return null;
        }
        catch (Exception ex)
        {
            NaoVerificado = true;
            Diagnostico.Registrar("Consultório — folha de execução não pôde ser carregada", ex);
            return ex.Message;
        }
    }

    /// <summary>
    /// Quem está executando sai do LOGIN, nunca de um campo digitado: é o vínculo com a pessoa
    /// que dá valor à checagem.
    /// </summary>
    public static IdentificacaoExecutante Executante() => new(
        UsuarioId: SessaoUsuario.Atual.Autenticado ? SessaoUsuario.Atual.UsuarioId : null,
        Nome: SessaoUsuario.Atual.Autenticado
            ? SessaoUsuario.Atual.Nome
            : SessaoUsuario.Atual.Operador,
        Conselho: SessaoUsuario.Atual.RegistroConselho);

    /// <summary>
    /// Quando a justificativa fala em reação/alergia, a tela oferece registrar a alergia — a
    /// oferta é DISPARADA pela palavra, mas quem decide é quem checou.
    /// </summary>
    public static bool JustificativaFalaEmReacao(string justificativa)
    {
        var texto = justificativa.ToLowerInvariant();
        return texto.Contains("alerg") || texto.Contains("reaç")
            || texto.Contains("reac") || texto.Contains("anafil");
    }

    /// <summary>A conferência da hora digitada — a frase do WPF.</summary>
    public static string? RecusaDaHora(DateOnly? data, string hora, out TimeOnly lida)
    {
        lida = default;
        if (data is null) return "Informe a data em que a execução ocorreu.";
        if (!TimeOnly.TryParse(hora, out lida))
            return $"Hora inválida (\"{hora}\"). Escreva no formato 14:30 — é o horário "
                   + "em que o item foi administrado, não o de agora.";
        return null;
    }
}
