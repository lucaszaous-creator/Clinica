using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Hospedagem;

namespace Clinica.Gestao.Web.Hospedagem;

// AS PONTES DAS TELAS NO PORTAL (nov/2026) — docs/gestao-web.md, "O que o portal não fazia".
//
// As telas pedem ao HOSPEDEIRO o que só ele sabe: navegar levando o contexto (o vão clicado, o
// paciente, o dia), entregar um PDF, perguntar um motivo. Nos apps quem responde é o módulo WPF;
// o portal respondia só a três delas, e nas outras valia o "indisponível" da prévia — a aba Marcar
// abria vazia, "ver na agenda" não abria o dia, a 2ª via do documento dizia "é do aplicativo".
//
// Tudo aqui é POR CONEXÃO (Scoped = circuito): o pedido de uma pessoa nunca preenche a tela de
// outra. E nada reescreve regra: cada ponte chama o MESMO serviço da Application que o app chama.

/// <summary>
/// Os pedidos que uma tela deixa para a outra NESTA conexão — o papel do
/// <c>PreenchimentoNovoAtendimento</c> e do <c>PedidoAgenda</c> da Recepção WPF. Ler LIMPA: pedido
/// órfão pré-preencheria a abertura de amanhã com o clique de ontem.
/// </summary>
public sealed class PedidosDaGestao
{
    private readonly object _trava = new();
    private PedidoDoAtendimento? _marcacao;
    private PedidoDeDiaDaAgenda? _dia;

    public void DefinirMarcacao(PedidoDoAtendimento pedido) { lock (_trava) _marcacao = pedido; }

    public bool HaMarcacao { get { lock (_trava) return _marcacao is not null; } }

    public PedidoDoAtendimento? ConsumirMarcacao()
    {
        lock (_trava) { var p = _marcacao; _marcacao = null; return p; }
    }

    public void DefinirDia(PedidoDeDiaDaAgenda pedido) { lock (_trava) _dia = pedido; }

    public PedidoDeDiaDaAgenda? ConsumirDia()
    {
        lock (_trava) { var p = _dia; _dia = null; return p; }
    }

    /// <summary>Outra tela desta conexão mudou um aviso da enfermagem.</summary>
    public event Action? PedidosDaEnfermagemMudaram;

    public void SinalizarPedidosDaEnfermagem() => PedidosDaEnfermagemMudaram?.Invoke();
}

/// <summary>As chaves de tela que as pontes conhecem (as mesmas do <c>catalogo-gestao.json</c>).</summary>
public static class ChavesDaGestao
{
    public const string Marcar = "marcar-horario";
    public const string AgendaDoDia = "fila";
    public const string Grade = "agenda-recepcao";
    public const string TelaDoPaciente = "consultorio-paciente";
    public const string ExamesDoPaciente = "consultorio-exames-anexos";
    public const string Atendimento = "consultorio-atendimento";
    public const string AtendimentoDeEnfermagem = "consultorio-atendimento-enfermagem";
}

/// <summary>
/// A marcação com o CONTEXTO: define o pedido e navega para a aba Marcar. Pergunta antes se a aba
/// existe para este acesso, e desfaz o pedido se a navegação recusar — nada fica pendurado.
/// </summary>
public sealed class MarcacaoComPedido(NavegacaoDaGestao navegacao, PedidosDaGestao pedidos)
{
    public bool Disponivel => navegacao.Conhece(ChavesDaGestao.Marcar);

    public bool Marcar(PedidoDoAtendimento pedido)
    {
        if (!Disponivel) return false;
        pedidos.DefinirMarcacao(pedido);
        if (navegacao.Navegar(ChavesDaGestao.Marcar)) return true;
        pedidos.ConsumirMarcacao();
        return false;
    }

    /// <summary>Hora 00:00 = "só o dia"; dia passado vira hoje (não se marca para trás).</summary>
    public static DateTime SoODia(DateOnly dia)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        return (dia < hoje ? hoje : dia).ToDateTime(TimeOnly.MinValue);
    }
}

/// <summary>O Lançar/Marcar lê aqui o pedido que outra tela deixou. Só a aba MARCAR recebe pedido no portal.</summary>
public sealed class JanelasDoAtendimentoDaGestao(PedidosDaGestao pedidos) : IJanelasDoAtendimento
{
    public bool Disponivel => true;
    public bool HaPedido(bool marcar) => marcar && pedidos.HaMarcacao;
    public PedidoDoAtendimento? ConsumirPedido(bool marcar) => marcar ? pedidos.ConsumirMarcacao() : null;
}

/// <summary>"Marcar atendimento" da agenda do dia: a aba Marcar, com o dia da lista e o profissional do filtro.</summary>
public sealed class JanelasDaAgendaDaGestao(MarcacaoComPedido marcacao) : IJanelasDaAgenda
{
    public bool Disponivel => marcacao.Disponivel;

    public Task<bool> MarcarNovoHorarioAsync(DateOnly dia, int? profissionalId)
        => Task.FromResult(marcacao.Marcar(new PedidoDoAtendimento(dia.ToDateTime(TimeOnly.MinValue), profissionalId, null)));
}

/// <summary>Marcar um horário para um paciente (retornos, acompanhamento): a aba Marcar, já com ele.</summary>
public sealed class MarcacaoDaGestao(MarcacaoComPedido marcacao) : IMarcacaoDeHorario
{
    public Task<bool> MarcarAsync(int pacienteId, int? profissionalId, string? modalidadeCodigo)
        => MarcarAsync(pacienteId, profissionalId, modalidadeCodigo, DateOnly.FromDateTime(DateTime.Today));

    public Task<bool> MarcarAsync(int pacienteId, int? profissionalId, string? modalidadeCodigo, DateOnly dia)
        => Task.FromResult(marcacao.Marcar(new PedidoDoAtendimento(
            MarcacaoComPedido.SoODia(dia), profissionalId, null, pacienteId, ModalidadeCodigo: modalidadeCodigo)));
}

/// <summary>A navegação da Visão geral e da Grade: marcar no vão (com sala, duração e modalidade) e abrir o dia.</summary>
public sealed class AgendaDaVisaoGeralDaGestao(
    NavegacaoDaGestao navegacao, MarcacaoComPedido marcacao, PedidosDaGestao pedidos, PedidoDaAgendaDoDia pedidoDoDia)
    : IAgendaDaVisaoGeral
{
    public bool MarcacaoDisponivel => marcacao.Disponivel;

    public Task<bool> MarcarAsync(DateTime dataHora, int? profissionalId)
        => Task.FromResult(marcacao.Marcar(new PedidoDoAtendimento(dataHora, profissionalId, null)));

    public Task<bool> MarcarAsync(DateTime dataHora, int? profissionalId, int? salaId, int? duracaoMinutos, string? modalidadeCodigo)
        => Task.FromResult(marcacao.Marcar(new PedidoDoAtendimento(
            dataHora, profissionalId, salaId, DuracaoMinutos: duracaoMinutos, ModalidadeCodigo: modalidadeCodigo)));

    public PedidoDeDiaDaAgenda? ConsumirPedidoDeDia() => pedidos.ConsumirDia();

    public Task<bool> AbrirDiaAsync(DateOnly dia, int? profissionalId)
    {
        if (!navegacao.Conhece(ChavesDaGestao.AgendaDoDia)) return Task.FromResult(false);
        pedidoDoDia.Definir(dia, profissionalId);
        if (navegacao.Navegar(ChavesDaGestao.AgendaDoDia)) return Task.FromResult(true);
        pedidoDoDia.Consumir();
        return Task.FromResult(false);
    }
}

/// <summary>
/// O balcão no portal: "ver na agenda" abre a Grade no dia, e o Início pergunta se o destino existe
/// para este acesso. O sino é do shell do app (a moldura da web não o tem): não há aviso a abrir.
/// </summary>
public sealed class BalcaoDaGestao(NavegacaoDaGestao navegacao, PedidosDaGestao pedidos) : IBalcaoDaRecepcao
{
    public Task<bool> AbrirDiaNaAgendaAsync(DateOnly dia)
    {
        if (!navegacao.Conhece(ChavesDaGestao.Grade)) return Task.FromResult(false);
        pedidos.DefinirDia(new PedidoDeDiaDaAgenda(dia, AplicarFiltro: true, ProfissionalId: null));
        if (navegacao.Navegar(ChavesDaGestao.Grade)) return Task.FromResult(true);
        pedidos.ConsumirDia();
        return Task.FromResult(false);
    }

    public bool DestinoExiste(string chave) => navegacao.Conhece(chave);

    public event Action? PedidosMudaram
    {
        add => pedidos.PedidosDaEnfermagemMudaram += value;
        remove => pedidos.PedidosDaEnfermagemMudaram -= value;
    }

    public void SinalizarMudancaDosPedidos() => pedidos.SinalizarPedidosDaEnfermagem();

    public int? ConsumirAberturaDePedido() => null;
}

/// <summary>A folha do médico: o motivo pelo qual a conclusão automática ainda não concluiu o horário.</summary>
public sealed class JanelasDoAtendimentoClinicoDaGestao(IServiceScopeFactory escopos) : IJanelasDoAtendimentoClinico
{
    public bool Disponivel => true;

    public async Task<string?> PendenciaDeConclusaoAutomaticaAsync(int agendamentoId)
    {
        using var escopo = escopos.CreateScope();
        return (await escopo.ServiceProvider.GetRequiredService<ConclusaoAutomaticaService>()
            .PendenciasAsync(ConclusaoAutomaticaService.Agora, agendamentoId)).FirstOrDefault()?.Motivo;
    }
}

/// <summary>"Detalhes" do pedido de exame: o paciente na seção Exames e anexos.</summary>
public sealed class JanelasDosExamesDaGestao(NavegacaoDaGestao navegacao, PedidoDaTelaDoPaciente pedido) : IJanelasDosExames
{
    public Task<bool> AbrirExamesDoPacienteAsync(PedidoDeExameLinha linha)
    {
        if (!navegacao.Conhece(ChavesDaGestao.ExamesDoPaciente)) return Task.FromResult(false);
        pedido.Definir(new PacienteNoPosto(linha.PacienteId, linha.Paciente, null, null, null), TelaDoPacienteNaTela.ExamesEAnexos);
        return Task.FromResult(navegacao.Navegar(ChavesDaGestao.ExamesDoPaciente));
    }
}

/// <summary>
/// A lista de prontuários: "Novo prontuário" abre a tela de atendimento de quem clicou. A
/// assinatura da anamnese é a área congelada — a gestão web não assina, e DIZ isso.
/// </summary>
public sealed class JanelasDosProntuariosDaGestao(NavegacaoDaGestao navegacao) : IJanelasDosProntuarios
{
    public Task<ResultadoDaAssinatura?> AssinarAnamneseAsync(LinhaProntuario linha)
        => Task.FromResult<ResultadoDaAssinatura?>(new ResultadoDaAssinatura(AssinaturaForaDaGestao.Frase, Erro: true));

    public Task<bool> AbrirAtendimentoAsync()
        => Task.FromResult(navegacao.Navegar(PostoDoConsultorioNaTela.AtendimentoDeEnfermagem(SessaoUsuario.Atual)
            ? ChavesDaGestao.AtendimentoDeEnfermagem
            : ChavesDaGestao.Atendimento));
}

/// <summary>
/// Os atos do documento clínico já emitido, no portal: a 2ª via (o PDF abre no navegador, com a
/// trilha de acesso ao prontuário) e o cancelamento com motivo obrigatório. Enviar e a nova versão
/// são formulários da própria tela.
///
/// ⛔ Assinar, renovar o link e tirar do ar NÃO são oferecidos aqui: passam pela área congelada
/// (<c>docs/safeid-congelado.md</c> — assinatura e <c>PublicacaoDocumentoService</c>), que não ganha
/// porta nova sem autorização expressa e homologação. Eles continuam no aplicativo, e o menu da
/// linha não os mostra — botão que não faz nada é o defeito que esta ponte veio tirar.
/// </summary>
public sealed class DocumentosDaGestao(
    IServiceScopeFactory escopos, IHostDaTela host, Dialogos dialogos, Notificacoes notificacoes, NavegacaoDaGestao navegacao)
    : IDocumentosDaRecepcao
{
    public const string SoNoAplicativo = "Esta ação é feita no aplicativo instalado da clínica (assinatura e link do documento).";

    public bool Disponivel => true;

    public bool CaixaNesteAplicativo => navegacao.Conhece("caixa");

    public AtosDoDocumento Atos(DocumentoEmitido doc)
    {
        var pode = SessaoUsuario.Atual.Pode(CentralDocumentosService.AcessoParaEmitir(doc.Tipo));
        return new AtosDoDocumento(
            NovaVersao: TipoDocumentoInfo.PermiteNovaVersao(doc.Tipo) && !doc.Cancelado && doc.PacienteId is not null && pode,
            Assinar: false,
            Enviar: EnvioAoPacienteService.PodeEnviar(doc.Tipo, doc.Assinado, doc.Cancelado) && pode,
            RenovarLink: false,
            TirarDoAr: false,
            Cancelar: !doc.Cancelado && pode);
    }

    public Task<ResultadoDoAto> ExecutarAsync(AtoDoDocumento ato, DocumentoEmitido doc) => ato switch
    {
        AtoDoDocumento.SegundaVia => SegundaViaAsync(doc),
        AtoDoDocumento.Cancelar => CancelarAsync(doc),
        AtoDoDocumento.NovaVersao => Task.FromResult(new ResultadoDoAto(Frase: "A nova versão do laudo abre na própria tela de Documentos.", EhErro: true)),
        AtoDoDocumento.Enviar => Task.FromResult(new ResultadoDoAto(Frase: "O envio ao paciente é feito pelo formulário da própria tela.", EhErro: true)),
        // Recusa explícita, e não o braço padrão: cair no `_` CANCELARIA o documento.
        _ => Task.FromResult(new ResultadoDoAto(Frase: SoNoAplicativo, EhErro: true))
    };

    private async Task<ResultadoDoAto> SegundaViaAsync(DocumentoEmitido doc)
    {
        try
        {
            // ExigirAlgum: o acesso para VER é uma união de bits (o termo que a recepção colhe).
            SessaoUsuario.Atual.ExigirAlgum(CentralDocumentosService.AcessoParaVer(doc.Tipo), $"abrir {doc.Rotulo.ToLowerInvariant()}");

            byte[] pdf;
            using (var escopo = escopos.CreateScope())
            {
                var sp = escopo.ServiceProvider;
                pdf = await sp.GetRequiredService<DocumentosClinicosPdfService>()
                    .GerarAsync(doc.DocumentoId, await sp.GetRequiredService<ParametrosService>().ObterPrestadorAsync());
                // Em fila, no MESMO escopo: é o mesmo DbContext.
                if (doc.PacienteId is { } id)
                    await sp.GetRequiredService<AcessoProntuarioService>()
                        .RegistrarAsync(id, SessaoUsuario.Atual.Operador, OrigemAcessoProntuario.Documento);
            }

            var erro = await host.AbrirPreviaAsync(pdf, $"{doc.Rotulo}-{doc.Numero.Replace('/', '-')}.pdf");
            return new ResultadoDoAto(Frase: erro, EhErro: erro is not null);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar($"Gestão web — documento {doc.Numero}: segunda via não pôde ser gerada", ex);
            // O documento ESTÁ emitido: dizer só "falhou" faria a pessoa emitir de novo.
            return new ResultadoDoAto(Frase: $"O documento {doc.Numero} está emitido, mas o PDF não pôde ser gerado: {ex.Message}", EhErro: true);
        }
    }

    private async Task<ResultadoDoAto> CancelarAsync(DocumentoEmitido doc)
    {
        if (doc.Cancelado) return new ResultadoDoAto(Frase: $"{doc.Numero} já está cancelado.", EhErro: true);
        try
        {
            // Cancelar é do mesmo peso de emitir, e cobra o mesmo bit do TIPO.
            SessaoUsuario.Atual.Exigir(CentralDocumentosService.AcessoParaEmitir(doc.Tipo), "cancelar documento clínico");

            var motivo = await dialogos.PerguntarTextoAsync("Cancelar documento",
                $"Por que o(a) {doc.Rotulo.ToLowerInvariant()} {doc.Numero} está sendo cancelado? "
                + "Ele continua na lista, marcado como cancelado — a via impressa não desaparece por ser apagada do sistema.");
            // Motivo OBRIGATÓRIO: nulo é desistência, e o vazio não é resposta.
            if (string.IsNullOrWhiteSpace(motivo)) return new ResultadoDoAto(Silencioso: true);

            using var escopo = escopos.CreateScope();
            await escopo.ServiceProvider.GetRequiredService<DocumentoClinicoService>()
                .CancelarAsync(doc.DocumentoId, motivo, SessaoUsuario.Atual.Operador);

            notificacoes.Info("Documento cancelado.");
            return new ResultadoDoAto(Mudou: true);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar($"Gestão web — documento {doc.Numero}: não pôde ser cancelado", ex);
            return new ResultadoDoAto(Frase: ex.Message, EhErro: true);
        }
    }
}

/// <summary>
/// "Outra máquina gravou nesta tabela", no portal: o MESMO <see cref="OuvinteDeMudancas"/> dos apps.
///
/// ⚠️ O aviso chega numa thread do Npgsql, FORA de qualquer conexão — e no servidor a sessão é a do
/// fluxo (<c>SessaoUsuario.UsarNesteFluxo</c>): sem ela, a releitura que o aviso dispara rodaria
/// como "ninguém" e seria recusada. Por isso cada assinatura guarda o contexto de execução de QUEM
/// assinou (a tela, dentro da atividade da conexão dela) e o aviso roda nele. Por conexão, e
/// descartado com ela: nenhuma tela fechada fica pendurada no ouvinte do processo.
/// </summary>
public sealed class AvisosDoBancoDaGestao : IAvisosDoBanco, IDisposable
{
    private readonly object _trava = new();
    private readonly Dictionary<Action<string>, Action<string>> _assinaturas = [];

    public event Action<string>? Mudou
    {
        add
        {
            if (value is null) return;
            var contexto = ExecutionContext.Capture();
            Action<string> noContexto = contexto is null
                ? value
                : tabela => ExecutionContext.Run(contexto, _ => value(tabela), null);
            lock (_trava)
            {
                if (_assinaturas.ContainsKey(value)) return;
                _assinaturas[value] = noContexto;
            }
            OuvinteDeMudancas.Mudou += noContexto;
        }
        remove
        {
            if (value is null) return;
            Action<string>? noContexto;
            lock (_trava)
            {
                if (!_assinaturas.Remove(value, out noContexto)) return;
            }
            OuvinteDeMudancas.Mudou -= noContexto;
        }
    }

    public void Dispose()
    {
        List<Action<string>> todas;
        lock (_trava)
        {
            todas = [.. _assinaturas.Values];
            _assinaturas.Clear();
        }
        foreach (var a in todas) OuvinteDeMudancas.Mudou -= a;
    }
}

/// <summary>Liga a escuta do banco na subida do portal (uma vez por processo; falha nunca derruba nada).</summary>
public sealed class EscutaDoBancoDaGestao(IConfiguration configuracao) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (configuracao.GetConnectionString("Clinica") is { Length: > 0 } cadeia) OuvinteDeMudancas.Iniciar(cadeia);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
