using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Faturamento.Web;

/// <summary>Estado de formulário independente de Window, preservado até confirmação explícita.</summary>
public abstract class FormularioFaturamento : ObservableObject
{
    public string Titulo { get; protected set; } = "";
    public string Descricao { get; protected set; } = "";
    private string? _mensagem;
    public string? Mensagem { get => _mensagem; protected set => SetProperty(ref _mensagem, value); }
    public bool MensagemEhErro => !string.IsNullOrEmpty(Mensagem);
    public virtual bool PodeFechar => true;
    public event Action? Concluido;
    public IRelayCommand ConfirmarCommand { get; }
    protected FormularioFaturamento() => ConfirmarCommand = new RelayCommand(() =>
    {
        Mensagem = Validar();
        if (Mensagem is null) Concluido?.Invoke();
    });
    protected virtual string? Validar() => null;
    public Task<bool?> AbrirAsync() => DialogosDaSessao.AbrirAsync(GetType().Name, this,
        () => throw new InvalidOperationException("Este formulário requer a apresentação web do Faturamento."));
}

public sealed class ObservacaoFaturamento : FormularioFaturamento
{
    public string Texto { get; set; }
    public string? Observacao => string.IsNullOrWhiteSpace(Texto) ? null : Texto.Trim();
    public ObservacaoFaturamento(PendenciaCodigo p)
    {
        Titulo = "Observação — " + p.PacienteNome; Texto = p.ObservacaoPendencia ?? "";
        Descricao = p.ObservacaoPendenciaEm is { } em ? $"Anotado em {em:dd/MM/yyyy HH:mm}. Apagar o texto remove a observação." : "Por que esta guia ainda não foi baixada?";
    }
}
public sealed class NaoConformidadeFaturamento : FormularioFaturamento
{
    public string Justificativa { get; set; } = "";
    public NaoConformidadeFaturamento(PendenciaCodigo p) { Titulo = "Não conformidade — " + p.PacienteNome; Descricao = "A guia sai das pendências ativas; o motivo fica registrado."; }
    protected override string? Validar() => string.IsNullOrWhiteSpace(Justificativa) ? "Informe uma justificativa para registrar a não conformidade." : null;
}
public sealed class LinhaDecisaoFaturamento
{
    public int CodigoId { get; init; }
    public string Descricao { get; init; } = "";
    public string Situacao { get; init; } = "";
    public FormatoNumeroGuia Formato { get; init; }
    public string NumeroGuia { get; set; } = "";
    public string Justificativa { get; set; } = "";
    public string? Dica => RegraNumeroGuia.Dica(Formato);
    public LinhaDecisaoFaturamento(PendenciaCodigo p)
    {
        CodigoId = p.CodigoId; Descricao = $"{p.PacienteNome} — {RotulosEnum.De(p.Tipo)} ({RotulosEnum.De(p.Ordem)})";
        Formato = CatalogoConvenios.FormatoDoNumeroDaGuia(p.ConvenioCodigo ?? p.Convenio.ToString());
        Situacao = $"{CatalogoConvenios.Nome(p.ConvenioCodigo, p.Convenio)} · " + (p.DiasEmAtraso > 0 ? $"atrasada há {p.DiasEmAtraso} dias" : "vence hoje");
    }
}
public class BaixaLoteFaturamento : FormularioFaturamento
{
    public DateTime Data { get; set; } = DateTime.Today;
    public DateOnly DataBaixa => DateOnly.FromDateTime(Data);
    public List<LinhaDecisaoFaturamento> Linhas { get; }
    public BaixaLoteFaturamento(IEnumerable<PendenciaCodigo> itens)
    { Titulo = "Baixa em lote"; Descricao = "Informe o número de cada guia. Linhas em branco são ignoradas; nenhum valor de pagamento é registrado."; Linhas = itens.Select(p => new LinhaDecisaoFaturamento(p)).ToList(); }
    protected override string? Validar()
    {
        var erros = Linhas.Select(l => (l, erro: RegraNumeroGuia.Criticar(l.NumeroGuia, l.Formato))).Where(x => x.erro is not null).Select(x => x.l.Descricao + ": " + x.erro).ToList();
        return erros.Count == 0 ? null : "Corrija os números antes de continuar; nada foi gravado.\n" + string.Join("\n", erros);
    }
}
public sealed class RodadaFaturamento : BaixaLoteFaturamento
{
    public override bool PodeFechar { get; }
    public RodadaFaturamento(IEnumerable<PendenciaCodigo> itens, RodadaPendenciasStatus status, bool bloqueante) : base(itens)
    {
        Titulo = "Rodar pendências"; PodeFechar = !bloqueante;
        Descricao = bloqueante ? $"Prazo de {status.PrazoDias} dias vencido: decida todas as guias para continuar." : "Dê baixa no que puder e justifique o restante como não conformidade.";
        if (status.AplicaConsultas && status.ConsultasParaRevisar > 0) Descricao += $" Reveja {status.ConsultasParaRevisar} consultas a renovar no painel.";
        if (status.AplicaCarteirinhas && status.CarteirinhasParaRevisar > 0) Descricao += $" Reveja {status.CarteirinhasParaRevisar} carteirinhas no painel.";
    }
    protected override string? Validar() => !PodeFechar && Linhas.Any(l => string.IsNullOrWhiteSpace(l.NumeroGuia) && string.IsNullOrWhiteSpace(l.Justificativa))
        ? "Decida todas as guias: informe o número para baixa ou a justificativa de não conformidade." : base.Validar();
}
public sealed class EnvioFaturamento : FormularioFaturamento
{
    public DateTime Data { get; set; } = DateTime.Today;
    public DateOnly DataEnvio => DateOnly.FromDateTime(Data);
    public string? Protocolo { get; set; }
    public EnvioFaturamento(int numero) { Titulo = $"Marcar lote nº {numero} como enviado"; Descricao = "Registre a data e o protocolo devolvido pela operadora."; }
}
public sealed class GlosaFaturamento : FormularioFaturamento
{
    public DateTime Data { get; set; } = DateTime.Today;
    public DateOnly DataGlosa => DateOnly.FromDateTime(Data);
    public string? Motivo { get; set; }
    private MotivoGlosa? _selecionado;
    public MotivoGlosa? Selecionado { get => _selecionado; set { SetProperty(ref _selecionado, value); OnPropertyChanged(nameof(Significado)); OnPropertyChanged(nameof(ComoEvitar)); OnPropertyChanged(nameof(Lastro)); } }
    public IReadOnlyList<MotivoGlosa> Motivos => MotivosGlosa.Todos;
    public string? MotivoCodigo => Selecionado?.Codigo;
    public string? Significado => Selecionado?.Significado;
    public string? ComoEvitar => Selecionado?.ComoEvitar;
    public string? Lastro => Selecionado?.Lastro;
    public IAsyncRelayCommand AbrirGuiaCommand { get; }
    public GlosaFaturamento(string descricao, int prazo)
    { Titulo = "Registrar glosa"; Descricao = descricao + $". Prazo de recurso: {prazo} dias."; AbrirGuiaCommand = new AsyncRelayCommand(async () => { await new GuiaGlosasFaturamento(MotivoCodigo).AbrirAsync(); }); }
}
public sealed class GuiaGlosasFaturamento : FormularioFaturamento
{
    private string _busca = "";
    public string Busca { get => _busca; set { if (SetProperty(ref _busca, value)) Filtrar(); } }
    public ObservableCollection<MotivoGlosa> Motivos { get; } = [];
    private MotivoGlosa? _selecionado;
    public MotivoGlosa? Selecionado { get => _selecionado; set { SetProperty(ref _selecionado, value); OnPropertyChanged(nameof(Significado)); OnPropertyChanged(nameof(ComoEvitar)); OnPropertyChanged(nameof(Lastro)); OnPropertyChanged(nameof(Recurso)); } }
    public string? Significado => Selecionado?.Significado;
    public string? ComoEvitar => Selecionado?.ComoEvitar;
    public string? Lastro => Selecionado?.Lastro;
    public string Recurso => Selecionado is null ? "Nenhum motivo encontrado" : Selecionado.ValeRecorrer ? "Costuma valer o recurso" : "Recurso raramente prospera: corrija a causa ou trate como particular.";
    public GuiaGlosasFaturamento(string? codigo = null) { Titulo = "Por que as guias são glosadas"; Filtrar(); Selecionado = Motivos.FirstOrDefault(m => m.Codigo == codigo) ?? Motivos.FirstOrDefault(); }
    private void Filtrar()
    {
        var codigo = Selecionado?.Codigo; var filtrados = MotivosGlosa.Todos.Where(m => string.IsNullOrWhiteSpace(Busca) || new[] { m.Codigo, m.Descricao, m.Significado, m.ComoEvitar }.Any(s => s.Contains(Busca, StringComparison.OrdinalIgnoreCase))).ToList();
        Motivos.Clear(); foreach (var m in filtrados) Motivos.Add(m); Selecionado = Motivos.FirstOrDefault(m => m.Codigo == codigo) ?? Motivos.FirstOrDefault();
    }
}
public sealed class RetornoFaturamento : FormularioFaturamento
{
    public DateTime Data { get; set; } = DateTime.Today;
    public DateOnly DataRetorno => DateOnly.FromDateTime(Data);
    public string? Observacao { get; set; }
    public ObservableCollection<Clinica.Desktop.Alertas.RetornoLinha> Linhas { get; } = [];
    public IReadOnlyList<RetornoGuiaDecisao> Decisoes => Linhas.Select(l => new RetornoGuiaDecisao(l.CodigoId, l.Glosada, l.Motivo?.Codigo, l.Complemento)).ToList();
    public RetornoFaturamento(int numero, IEnumerable<CodigoFaturamento> codigos)
    {
        Titulo = $"Registrar retorno do lote nº {numero}";
        foreach (var c in codigos) Linhas.Add(new() { CodigoId = c.Id, Paciente = c.Atendimento?.Paciente?.Nome ?? "(desconhecido)", Tipo = RotulosEnum.De(c.Tipo), NumeroGuia = c.NumeroGuiaReal, Glosada = c.GlosaEmAberto, Motivo=MotivosGlosa.Todos.FirstOrDefault(m=>m.Codigo==c.MotivoGlosaCodigo), Complemento=c.MotivoGlosa });
    }
    public void AplicarImportacao(ResultadoImportacaoRetorno resultado)
    {
        if (resultado.DataDemonstrativo is { } data) Data = data.ToDateTime(TimeOnly.MinValue);
        foreach (var d in resultado.Decisoes) if (Linhas.FirstOrDefault(l => l.CodigoId == d.CodigoId) is { } l)
        { l.Glosada = d.Glosada; l.Motivo = MotivosGlosa.Todos.FirstOrDefault(m => m.Codigo == d.MotivoCodigo); l.Complemento = d.MotivoTexto; }
    }
}
public sealed class HistoricoFaturamento : FormularioFaturamento
{
    public sealed record Linha(string Quando, string Operador, string Acao, string Detalhe);
    public ObservableCollection<Linha> Linhas { get; } = [];
    private readonly IServiceScopeFactory _escopos;
    private readonly int _id;
    public HistoricoFaturamento(IServiceScopeFactory escopos, CodigoFaturamento codigo)
    { _escopos = escopos; _id = codigo.Id; Titulo = "Histórico da guia " + codigo.NumeroGuiaReal; Descricao = $"{codigo.Atendimento?.Paciente?.Nome} · {codigo.Descricao} · prevista para {codigo.DataPrevistaFaturamento:dd/MM/yyyy}"; }
    public async Task CarregarAsync()
    {
        SessaoUsuario.Atual.Exigir(Permissao.VerAuditoria, "ver histórico da guia");
        using var escopo = _escopos.CreateScope();
        var eventos = await escopo.ServiceProvider.GetRequiredService<AuditoriaService>().DaGuiaAsync(_id);
        Linhas.Clear(); foreach (var e in eventos.OrderByDescending(e => e.DataHora)) Linhas.Add(new(e.DataHora.ToString("dd/MM/yyyy HH:mm"), e.Operador, e.Acao, e.Detalhe ?? "—"));
    }
}

public sealed class AvisoPendenciasFaturamento : FormularioFaturamento
{
    public IReadOnlyList<PendenciaCodigo> Linhas { get; }
    public IRelayCommand VerPainelCommand { get; }
    public AvisoPendenciasFaturamento(IReadOnlyList<PendenciaCodigo> pendencias)
    {
        Linhas=pendencias; Titulo="Aviso de pendências";
        var atrasadas=pendencias.Count(p=>p.DiasEmAtraso>0);
        Descricao=$"{pendencias.Count} pendência(s) para faturar; {atrasadas} atrasada(s). Dê baixa assim que possível para não perder o faturamento.";
        VerPainelCommand=new RelayCommand(()=>{ ConfirmarCommand.Execute(null); Clinica.Desktop.Shell.Modulos.NavegacaoSuite.Ir("faturamento-pendencias"); });
    }
}
