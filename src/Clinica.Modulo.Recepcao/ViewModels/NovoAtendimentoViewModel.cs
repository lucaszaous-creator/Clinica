using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// Um código recém-gerado, e a informação que o balcão precisa dar ao paciente: esta guia
/// já está liberada para o faturamento cuidar hoje, ou só a partir de quando?
///
/// O 1º código nasce faturável na hora; o 2º só a partir de +24h — que é o defeito que dá
/// nome ao produto, e é justamente o que esta linha existe para tornar visível no momento
/// em que a guia nasce.
/// </summary>
public sealed record CodigoLancado(CodigoFaturamento Codigo, bool Liberada, string? Impedimento)
{
    public bool Baixado => Codigo.Baixado;

    // Os rótulos saem resolvidos daqui, e não de conversor de XAML: os conversores
    // `EnumDescricao` e `CodigoEspecialidade` são do design system do FATURAMENTO e não
    // existem na suíte. Duplicá-los para uma tela seria pagar o débito do design system
    // uma terceira vez; o padrão daqui é o `LinhaPendencia` do Gerente — resolver no VM.
    public string TipoRotulo => RotularTipo(Codigo.Tipo);

    public string EspecialidadeRotulo => Codigo.EspecialidadeCodigo is { } cod
        ? CatalogoEspecialidades.Nome(cod)
        : "—";

    public string OrdemRotulo => Codigo.Ordem == OrdemCodigo.Segundo ? "2º" : "1º";

    public string FaturarEm => Codigo.DataPrevistaFaturamento.ToString("dd/MM/yyyy");

    public string ComoObter => Codigo.FormaObtencao switch
    {
        FormaObtencao.NaoAplica => "—",
        FormaObtencao.App => "Pelo app (QR Code)",
        FormaObtencao.Sistema => "Pelo sistema",
        FormaObtencao.Ligacao => "Ligar para o paciente",
        _ => Codigo.FormaObtencao.ToString()
    };

    /// <summary>
    /// O que o balcão diz ao paciente. Três estados, e o do meio é o motivo de o produto
    /// existir: a guia que só libera depois de amanhã é a que se esquece.
    /// </summary>
    public string Situacao => Baixado
        ? "Já baixada pelo faturamento"
        : Impedimento ?? "Liberada — o faturamento já pode dar baixa";

    public bool TemImpedimento => Impedimento is not null;

    internal static string RotularTipo(TipoCodigo t) => t switch
    {
        TipoCodigo.ConsultaEspecialidade => "Consulta de especialidade",
        TipoCodigo.Eletroacupuntura => "Eletroacupuntura",
        TipoCodigo.Bsv => "BSV",
        TipoCodigo.Acupuntura => "Acupuntura",
        TipoCodigo.Consulta => "Consulta",
        _ => t.ToString()
    };
}

/// <summary>
/// Lança um atendimento AVULSO — o paciente que não estava na agenda. O motor de regras
/// gera os códigos do convênio na hora, inclusive o 2º código de +24h.
///
/// Veio do app de FATURAMENTO na parcela 46. O balcão já criava atendimento pelo caminho da
/// AGENDA (Fila → Finalizar → <c>FechamentoSessaoService</c> →
/// <c>AgendaService.ConfirmarPresencaAsync</c> → <c>AtendimentoService.LancarAsync</c>); o
/// que faltava aqui era o avulso, e ele morava no posto do faturamento — longe de quem
/// recebe o paciente que chegou sem horário marcado.
///
/// <b>O circuito com o faturamento é o MESMO</b>: os dois caminhos desembocam em
/// <see cref="AtendimentoService.LancarAsync"/>, que é ponto único, e é ele que grava
/// <c>Atendimento</c> + <c>CodigoFaturamento</c> pelas regras do convênio. Não existe
/// atendimento que nasça sem guia, e não existe guia que o faturamento não veja — a ligação
/// é chave estrangeira no mesmo banco, não sincronização.
///
/// O que NÃO veio junto foi a BAIXA. Ela é o ato do faturamento, tem as quatro portas de lá
/// (tela de baixa, baixa em lote, rodada de pendências e fila do Gerente) e o perfil que usa
/// esta tela não tem o bit — um botão que nasce apagado para quem usa a tela é o defeito da
/// parcela 41. A lista de códigos gerados fica como CONFIRMAÇÃO: é onde o balcão vê, na
/// hora, que a guia nasceu e quando ela libera.
/// </summary>
public partial class NovoAtendimentoViewModel : ObservableObject
{
    /// <summary>Metade VISÍVEL da permissão: lançar atendimento CRIA as guias pela regra do convênio.</summary>
    public bool PodeLancar => SessaoUsuario.Atual.Pode(Permissao.LancarAtendimento);

    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Busca de paciente compartilhada (mesmo limite e mesmo comportamento das outras telas).</summary>
    public SeletorPacienteViewModel Seletor { get; }

    /// <summary>Atalho para o paciente escolhido no seletor.</summary>
    public Paciente? PacienteSelecionado => Seletor.Selecionado;

    public ObservableCollection<CodigoLancado> CodigosGerados { get; } = new();
    public ObservableCollection<string> Avisos { get; } = new();

    /// <summary>Placar das baixas do atendimento recém-lançado ("1 de 2 guias baixadas…").</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemResumoBaixas))]
    private string? _resumoBaixas;

    public bool TemResumoBaixas => !string.IsNullOrWhiteSpace(ResumoBaixas);

    /// <summary>Modalidades ativas do catálogo (embutidas + variantes criadas pela clínica).</summary>
    public ObservableCollection<EntradaModalidade> Modalidades { get; } = new();

    /// <summary>Especialidades ativas do catálogo (para a consulta avulsa).</summary>
    public ObservableCollection<EntradaEspecialidade> Especialidades { get; } = new();

    /// <summary>Opções de qual código sai primeiro (hoje) numa modalidade dupla. Vazio nas simples.</summary>
    public ObservableCollection<TipoCodigo> OpcoesPrimeiroCodigo { get; } = new();

    [ObservableProperty] private DateTime _data = DateTime.Today;
    [ObservableProperty] private EntradaModalidade? _modalidadeSelecionada;
    [ObservableProperty] private EntradaEspecialidade? _especialidadeSelecionada;
    [ObservableProperty] private TipoCodigo? _primeiroCodigo;
    [ObservableProperty] private string? _observacoes;
    [ObservableProperty] private bool _lancado;
    [ObservableProperty] private string? _numeroAtendimento;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMensagem))]
    private string? _mensagem;

    public bool TemMensagem => !string.IsNullOrWhiteSpace(Mensagem);

    /// <summary>Aviso de guias pendentes do paciente selecionado (para a secretária cobrar na hora). Nulo = sem pendências.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAvisoPendencias))]
    private string? _avisoPendencias;

    /// <summary>Há aviso de pendências a exibir?</summary>
    public bool TemAvisoPendencias => !string.IsNullOrWhiteSpace(AvisoPendencias);

    /// <summary>Aviso de carteirinha vencida do paciente selecionado. Separado da mensagem de erro.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAvisoCarteirinha))]
    private string? _avisoCarteirinha;

    public bool TemAvisoCarteirinha => !string.IsNullOrWhiteSpace(AvisoCarteirinha);

    /// <summary>
    /// Consulta renovável vencida ou a vencer na data do atendimento. Fica ao lado dos
    /// outros avisos em vez de junto deles: carteirinha, cota e consulta chegam juntas e
    /// se resolvem em lugares diferentes.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAvisoConsulta))]
    private string? _avisoConsulta;

    public bool TemAvisoConsulta => !string.IsNullOrWhiteSpace(AvisoConsulta);

    /// <summary>A consulta já venceu — o convênio recusa o que for faturado sem consulta vigente.</summary>
    [ObservableProperty] private bool _consultaVencida;

    /// <summary>
    /// Já há paciente escolhido? Alterna a busca pelo resumo do paciente na tela.
    ///
    /// O par com <see cref="SemPaciente"/> substitui o conversor `BoolInvertidoParaVisibilidade`
    /// do faturamento, que a suíte não tem — é o mesmo par que a tela de Prescrições da
    /// Recepção já usa.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SemPaciente))]
    private bool _pacienteEscolhido;

    /// <summary>Ninguém escolhido ainda — mostra a busca.</summary>
    public bool SemPaciente => !PacienteEscolhido;

    /// <summary>Nome do convênio do paciente selecionado (resolvido pelo catálogo).</summary>
    [ObservableProperty] private string? _convenioPaciente;

    /// <summary>Categoria do paciente (semáforo do cadastro), como TEXTO — o conversor de cor é do faturamento.</summary>
    [ObservableProperty] private string? _categoriaPaciente;

    /// <summary>Cota de sessões: "Senha 12345 · 7 de 10 usadas — restam 3". Nulo = sem autorização vigente.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemSaldoAutorizacao))]
    private string? _saldoAutorizacao;

    public bool TemSaldoAutorizacao => !string.IsNullOrWhiteSpace(SaldoAutorizacao);

    /// <summary>Cota esgotada ou autorização vencida: lançar agora é candidato à glosa 2006.</summary>
    [ObservableProperty] private bool _autorizacaoCritica;

    /// <summary>Resta uma sessão: hora de pedir a renovação da senha.</summary>
    [ObservableProperty] private bool _autorizacaoNaUltima;

    [ObservableProperty] private bool _ocupado;

    private int _ultimoAtendimentoId;

    /// <summary>Comportamento (base) da modalidade selecionada — o que o motor de regras usa.</summary>
    private ModalidadeAtendimento Modalidade =>
        ModalidadeSelecionada?.Base ?? ModalidadeAtendimento.AcupunturaComEletro;

    /// <summary>Modalidade dupla (gera 1º hoje + 2º em +24h): permite escolher qual código sai primeiro.</summary>
    public bool ModalidadeDupla =>
        Modalidade is ModalidadeAtendimento.AcupunturaComEletro or ModalidadeAtendimento.BsvComAcupuntura;

    /// <summary>Consulta avulsa: pede a especialidade (discriminada nos relatórios).</summary>
    public bool ModalidadeConsulta => Modalidade == ModalidadeAtendimento.Consulta;

    public NovoAtendimentoViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        Seletor = new SeletorPacienteViewModel(scopeFactory);
        Seletor.SelecaoMudou += AoTrocarPaciente;
        AtualizarOpcoesPrimeiroCodigo();
    }

    partial void OnModalidadeSelecionadaChanged(EntradaModalidade? value)
    {
        AtualizarOpcoesPrimeiroCodigo();
        if (Modalidade != ModalidadeAtendimento.Consulta)
            EspecialidadeSelecionada = null;
        OnPropertyChanged(nameof(ModalidadeDupla));
        OnPropertyChanged(nameof(ModalidadeConsulta));
    }

    /// <summary>Preenche as opções de "qual código primeiro" conforme a modalidade e escolhe o padrão.</summary>
    private void AtualizarOpcoesPrimeiroCodigo()
    {
        OpcoesPrimeiroCodigo.Clear();
        switch (Modalidade)
        {
            case ModalidadeAtendimento.AcupunturaComEletro:
                OpcoesPrimeiroCodigo.Add(TipoCodigo.Acupuntura);
                OpcoesPrimeiroCodigo.Add(TipoCodigo.Eletroacupuntura);
                break;
            case ModalidadeAtendimento.BsvComAcupuntura:
                OpcoesPrimeiroCodigo.Add(TipoCodigo.Bsv);
                OpcoesPrimeiroCodigo.Add(TipoCodigo.Acupuntura);
                break;
        }
        PrimeiroCodigo = OpcoesPrimeiroCodigo.Count > 0 ? OpcoesPrimeiroCodigo[0] : null;
    }

    public async Task CarregarAsync()
    {
        CarregarCatalogos();
        await Seletor.BuscarAsync(imediato: true);
    }

    /// <summary>Recarrega as opções de modalidade/especialidade do cache (reflete o que foi salvo em Configurações).</summary>
    private void CarregarCatalogos()
    {
        var modalidadeAtual = ModalidadeSelecionada?.Codigo;
        Modalidades.Clear();
        foreach (var m in CatalogoModalidades.Ativas)
            Modalidades.Add(m);
        ModalidadeSelecionada = Modalidades.FirstOrDefault(m => m.Codigo == modalidadeAtual)
            ?? Modalidades.FirstOrDefault(m => m.Base == ModalidadeAtendimento.AcupunturaComEletro)
            ?? Modalidades.FirstOrDefault();

        var especialidadeAtual = EspecialidadeSelecionada?.Codigo;
        Especialidades.Clear();
        foreach (var e in CatalogoEspecialidades.Ativas)
            Especialidades.Add(e);
        EspecialidadeSelecionada = Especialidades.FirstOrDefault(e => e.Codigo == especialidadeAtual);
    }

    // Pré-preenche a modalidade com a habitual do paciente (definida no cadastro)
    // e avisa carteirinha vencida ANTES de gerar uma guia que o convênio vai recusar.
    private void AoTrocarPaciente(Paciente? value)
    {
        OnPropertyChanged(nameof(PacienteSelecionado));
        AvisoPendencias = null;
        AvisoCarteirinha = null;
        AvisoConsulta = null;
        ConsultaVencida = false;
        SaldoAutorizacao = null;
        AutorizacaoCritica = false;
        AutorizacaoNaUltima = false;
        PacienteEscolhido = value is not null;
        ConvenioPaciente = value is null
            ? null
            : CatalogoConvenios.Nome(value.ConvenioCodigo ?? value.Convenio.ToString());
        CategoriaPaciente = value?.Categoria.ToString();
        if (value is null) return;

        // Pré-seleciona a modalidade habitual do paciente: primeiro pelo código salvo, senão pela base.
        ModalidadeSelecionada = Modalidades.FirstOrDefault(m => m.Codigo == value.ModalidadePreferidaCodigo)
            ?? Modalidades.FirstOrDefault(m => m.Base == value.ModalidadePreferida)
            ?? ModalidadeSelecionada;
        AvisoCarteirinha = value.CarteirinhaVencida
            ? $"A carteirinha de {value.Nome} venceu em {value.ValidadeCarteirinha:dd/MM/yyyy} — o convênio pode recusar a guia."
            : null;

        _ = VerificarPendenciasAsync(value.Id);
        _ = VerificarAutorizacaoAsync(value.Id);
        _ = VerificarConsultaAsync(value.Id);
    }

    /// <summary>
    /// Consulta renovável do paciente na data do atendimento.
    ///
    /// Ela existia em dois lugares — a aba Consultas e o painel de pendências — e em
    /// nenhum deles a secretária está com o paciente na frente. Lançar o atendimento é o
    /// último momento barato: a consulta vencida faz o convênio recusar o que acabou de
    /// ser gerado aqui.
    /// </summary>
    private async Task VerificarConsultaAsync(int pacienteId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var consultas = scope.ServiceProvider.GetRequiredService<ConsultaService>();
            var situacao = await consultas.DoPacienteAsync(pacienteId, DateOnly.FromDateTime(Data));

            // A seleção pode ter mudado enquanto a consulta rodava.
            if (PacienteSelecionado?.Id != pacienteId) return;

            AvisoConsulta = situacao?.AvisoRenovacao;
            ConsultaVencida = situacao?.Vencida ?? false;
        }
        catch (Exception ex)
        {
            // Aviso é auxiliar: nunca impede o lançamento. Mas também não pode sumir
            // calado, senão a tela diria "não há consulta a renovar" sem ter olhado.
            LogSuite.Registrar("Novo atendimento — consulta renovável não pôde ser lida", ex);
            AvisoConsulta = "Não foi possível conferir a consulta renovável deste paciente.";
            ConsultaVencida = false;
        }
    }

    /// <summary>
    /// Mostra a cota de sessões antes de lançar. É o aviso que evita a glosa 2006
    /// ("quantidade executada acima da autorizada") — o sistema registrava essa glosa
    /// depois do prejuízo e não avisava antes.
    /// </summary>
    private async Task VerificarAutorizacaoAsync(int pacienteId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var autorizacoes = scope.ServiceProvider.GetRequiredService<AutorizacaoService>();
            var saldo = await autorizacoes.VigenteAsync(pacienteId, DateOnly.FromDateTime(Data));

            // A seleção pode ter mudado enquanto a consulta rodava.
            if (PacienteSelecionado?.Id != pacienteId) return;

            if (saldo is null)
            {
                SaldoAutorizacao = null;
                AutorizacaoCritica = false;
                AutorizacaoNaUltima = false;
                return;
            }

            var senha = string.IsNullOrWhiteSpace(saldo.Autorizacao.Numero)
                ? "Autorização"
                : $"Senha {saldo.Autorizacao.Numero}";
            SaldoAutorizacao = saldo.Vencida
                ? $"{senha}: venceu em {saldo.Autorizacao.DataValidade:dd/MM/yyyy} — peça uma nova antes de lançar."
                : $"{senha}: {saldo.Resumo} (válida até {saldo.Autorizacao.DataValidade:dd/MM/yyyy}).";
            AutorizacaoCritica = saldo.Vencida || saldo.Esgotada;
            AutorizacaoNaUltima = !AutorizacaoCritica && saldo.NaUltima;
        }
        catch (Exception ex)
        {
            // Aviso é auxiliar: nunca pode impedir o lançamento do atendimento.
            LogSuite.Registrar("Novo atendimento — cota de sessões não pôde ser lida", ex);
            SaldoAutorizacao = null;
            AutorizacaoCritica = false;
            AutorizacaoNaUltima = false;
        }
    }

    /// <summary>Volta para a busca, para trocar o paciente escolhido.</summary>
    [RelayCommand]
    private void TrocarPaciente() => Seletor.Limpar();

    /// <summary>Zera a tela para lançar outro atendimento, sem sair da seção.</summary>
    [RelayCommand]
    private void NovoLancamento()
    {
        Lancado = false;
        NumeroAtendimento = null;
        _ultimoAtendimentoId = 0;
        CodigosGerados.Clear();
        Avisos.Clear();
        ResumoBaixas = null;
        Observacoes = null;
        Mensagem = null;
        Data = DateTime.Today;
        Seletor.Limpar();
        Seletor.Termo = null;
    }

    /// <summary>
    /// Avisa se o paciente selecionado tem guias pendentes de baixa de atendimentos anteriores —
    /// oportunidade de a secretária cobrar a guia em aberto no mesmo instante do novo atendimento.
    /// </summary>
    private async Task VerificarPendenciasAsync(int pacienteId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var pendencias = scope.ServiceProvider.GetRequiredService<PendenciaService>();
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var lista = await pendencias.PendenciasDoPacienteAsync(pacienteId, hoje);
            var ncs = await pendencias.NaoConformidadesDoPacienteAsync(pacienteId, hoje);

            // A seleção pode ter mudado enquanto a consulta rodava.
            if (PacienteSelecionado?.Id != pacienteId) return;
            if (lista.Count == 0 && ncs.Count == 0) { AvisoPendencias = null; return; }

            var partes = new List<string>();
            if (lista.Count > 0)
            {
                var itens = string.Join("; ", lista.Take(3).Select(p =>
                {
                    var ordinal = p.Ordem == OrdemCodigo.Segundo ? "2ª" : "1ª";
                    return $"{ordinal} guia de {CodigoLancado.RotularTipo(p.Tipo).ToLowerInvariant()} de {p.DataPrevista:dd/MM}";
                }));
                if (lista.Count > 3) itens += $"; +{lista.Count - 3}";
                partes.Add($"{lista.Count} guia(s) pendente(s) de baixa — cobre a guia agora! ({itens}.)");
            }
            // Não conformidade: o paciente voltou, então ela será reaberta ao lançar o atendimento.
            if (ncs.Count > 0)
                partes.Add($"{ncs.Count} não conformidade(s) — serão reabertas ao lançar (o paciente voltou); cobre a(s) guia(s).");

            AvisoPendencias = "Este paciente tem " + string.Join(" ", partes);
        }
        catch (Exception ex)
        {
            // Aviso é auxiliar: uma falha aqui nunca pode impedir o lançamento do atendimento.
            LogSuite.Registrar("Novo atendimento — pendências do paciente não puderam ser lidas", ex);
            AvisoPendencias = null;
        }
    }

    [RelayCommand]
    private async Task Lancar()
    {
        SessaoUsuario.Atual.Exigir(Permissao.LancarAtendimento, "lançar atendimento");

        if (Seletor.Selecionado is not { } paciente)
        {
            Mensagem = "Selecione o paciente.";
            return;
        }
        if (ModalidadeSelecionada is null)
        {
            Mensagem = "Selecione a modalidade.";
            return;
        }
        if (ModalidadeConsulta && EspecialidadeSelecionada is null)
        {
            Mensagem = "Informe a especialidade da consulta.";
            return;
        }

        // Guarda contra duplo clique: dois lançamentos gerariam códigos duplicados.
        if (Ocupado) return;
        Ocupado = true;
        try
        {
            CodigosGerados.Clear();
            Avisos.Clear();
            Mensagem = null;

            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AtendimentoService>();
            var resultado = await service.LancarAsync(
                paciente.Id, DateOnly.FromDateTime(Data), Modalidade, Observacoes,
                registrarNaAgenda: true, primeiroCodigo: ModalidadeDupla ? PrimeiroCodigo : null,
                modalidadeCodigo: ModalidadeSelecionada.Codigo,
                especialidadeConsultaCodigo: ModalidadeConsulta ? EspecialidadeSelecionada?.Codigo : null);

            MontarCodigos(resultado.Atendimento.Codigos);
            foreach (var a in resultado.Avisos)
                Avisos.Add(a);

            _ultimoAtendimentoId = resultado.Atendimento.Id;
            NumeroAtendimento = resultado.Atendimento.Numero;
            Lancado = true;
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível lançar o atendimento: {ex.Message}";
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>
    /// Monta as linhas do resultado marcando o que já dá para baixar hoje. O 1º código
    /// nasce faturável na hora; o 2º só a partir da data prevista (+24h).
    /// </summary>
    private void MontarCodigos(IEnumerable<CodigoFaturamento> codigos)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        CodigosGerados.Clear();

        foreach (var c in codigos.OrderBy(c => c.DataPrevistaFaturamento).ThenBy(c => c.Ordem))
        {
            var liberada = c.EstaPendente(hoje);
            var impedimento = c.Baixado || liberada || c.Status == StatusCodigo.NaoAplicavel
                ? null
                : $"Libera em {c.DataPrevistaFaturamento:dd/MM/yyyy}";
            CodigosGerados.Add(new CodigoLancado(c, liberada, impedimento));
        }

        AtualizarResumoBaixas(hoje);
    }

    private void AtualizarResumoBaixas(DateOnly hoje)
    {
        var faturaveis = CodigosGerados.Where(l => l.Codigo.Status != StatusCodigo.NaoAplicavel).ToList();
        if (faturaveis.Count == 0) { ResumoBaixas = null; return; }

        var baixadas = faturaveis.Count(l => l.Baixado);
        var proxima = faturaveis
            .Where(l => !l.Baixado && l.Codigo.DataPrevistaFaturamento > hoje)
            .OrderBy(l => l.Codigo.DataPrevistaFaturamento)
            .FirstOrDefault();

        ResumoBaixas = $"{baixadas} de {faturaveis.Count} guia(s) já baixada(s) pelo faturamento"
            + (proxima is null
                ? baixadas == faturaveis.Count ? " — nada pendente deste atendimento." : "."
                : $" — a próxima libera em {proxima.Codigo.DataPrevistaFaturamento:dd/MM/yyyy} e entra no painel de pendências do faturamento.");
    }

    /// <summary>Gera a capa de faturamento (PDF) do atendimento recém-lançado e abre o arquivo.</summary>
    [RelayCommand]
    private async Task GerarCapa()
    {
        if (_ultimoAtendimentoId == 0) return;

        byte[] pdf;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var capa = scope.ServiceProvider.GetRequiredService<CapaFaturamentoService>();
            var prestador = await scope.ServiceProvider.GetRequiredService<ParametrosService>().ObterPrestadorAsync();
            pdf = await capa.GerarPdfAsync(_ultimoAtendimentoId, prestador);
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível gerar a capa: {ex.Message}";
            return;
        }

        // Salvar cancelado devolve null, e sair calado é o certo: diálogo que a pessoa
        // fechou de propósito não é falha a reportar.
        await ImpressaoPdf.SalvarEAbrirAsync(
            pdf, $"Capa-INICIAL-{NumeroAtendimento ?? _ultimoAtendimentoId.ToString()}-{Data:yyyy-MM-dd}.pdf");
    }
}
