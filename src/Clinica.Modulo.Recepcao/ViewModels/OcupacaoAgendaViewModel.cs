using System.Collections.ObjectModel;
using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Recepcao.Modulo;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>Uma opção do combo de profissional; <c>Id</c> nulo é "toda a equipe".</summary>
public sealed record OpcaoProfissionalOcupacao(int? Id, string Nome);

/// <summary>Um atalho de "Outras operações": a chave do destino e o nome escrito.</summary>
public sealed record OperacaoAgenda(string Chave, string Nome);

/// <summary>Frases de uma medida de ocupação — as mesmas regras da versão web.</summary>
public static class TextoOcupacao
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    public static string Tempo(int minutos) => $"{minutos / 60}h{(minutos % 60 != 0 ? $" {minutos % 60}min" : "")}";
    public static string Percentual(decimal? v) => v is null ? "Sem cálculo" : $"{v.Value.ToString(PtBr)}%";
    public static double Fracao(decimal? v) => v is null ? 0 : Math.Clamp((double)v.Value / 100d, 0, 1);
}

/// <summary>Um horário na lista do modo DIA.</summary>
public sealed class HorarioOcupacaoLinha(HorarioOcupacao h)
{
    public HorarioOcupacao Horario { get; } = h;
    public string Inicio => h.Inicio.ToString("HH:mm");
    public string Ate => $"até {h.Fim:HH:mm}" + (h.Fim.Date != h.Inicio.Date ? " · dia seguinte" : "");
    public string Paciente => h.Paciente;
    public string Modalidade => h.Modalidade;
    public string Profissional => h.Profissional;
    public string Situacao => h.Situacao;
    /// <summary>Cancelado e falta continuam na lista, apagados — a regra da folha do dia.</summary>
    public bool Inativo => !h.Ocupa;
}

/// <summary>A prévia de um horário dentro do cartão do dia, na semana.</summary>
public sealed record PreviaOcupacao(string Hora, string Paciente, string Profissional);

/// <summary>Um dia no calendário da semana ou do mês.</summary>
public sealed class DiaOcupacaoCartao
{
    public required DateOnly Dia { get; init; }
    public required MedidaOcupacao Resumo { get; init; }
    public required string Rotulo { get; init; }
    public required bool Selecionado { get; init; }
    public required bool ModoSemana { get; init; }
    public required IReadOnlyList<PreviaOcupacao> Previas { get; init; }
    /// <summary>Célula vazia no começo do mês, para o dia 1 cair na coluna da semana certa.</summary>
    public bool Vazio { get; init; }

    public int Atendimentos => Resumo.Atendimentos;
    public string TotalTexto => Resumo.Atendimentos == 1 ? "atendimento" : "atendimentos";
    public double Fracao => TextoOcupacao.Fracao(Resumo.Percentual);
    public bool SemBase => Resumo.Percentual is null;
    public string Taxa => Resumo.Percentual is not null ? $"{TextoOcupacao.Percentual(Resumo.Percentual)} ocupado"
        : Resumo.SemJornada > 0 ? "Jornada incompleta" : "Sem jornada no dia";
    public string Mais => Resumo.Atendimentos > 3 ? $"+{Resumo.Atendimentos - 3} atendimentos" : "";
    public bool TemMais => Resumo.Atendimentos > 3;
    public bool SemHorarios => Resumo.Atendimentos == 0;
    public string Acessivel => $"{Dia.ToDateTime(TimeOnly.MinValue).ToString("dddd, d 'de' MMMM", CultureInfo.GetCultureInfo("pt-BR"))}, "
        + $"{Resumo.Atendimentos} atendimentos, {TextoOcupacao.Percentual(Resumo.Percentual)} de ocupação";
}

/// <summary>Uma linha da ocupação por profissional.</summary>
public sealed class ProfissionalOcupacaoLinha(ProfissionalOcupacao p)
{
    public ProfissionalOcupacao Profissional { get; } = p;
    public int Id => p.Id;
    public string Nome => p.Nome;
    public string Jornada => p.Jornada;
    public int Atendimentos => p.Resumo.Atendimentos;
    public string Percentual => p.Resumo.Percentual is null ? "—" : TextoOcupacao.Percentual(p.Resumo.Percentual);
    public double Fracao => TextoOcupacao.Fracao(p.Resumo.Percentual);
    public string Livres => p.Resumo.SemJornada > 0 ? "Jornada incompleta"
        : p.Resumo.MinutosDisponiveis > 0 ? $"{TextoOcupacao.Tempo(p.Resumo.MinutosLivres)} livres" : "Sem jornada no período";
    public string Acessivel => $"Filtrar {p.Nome}";
}

/// <summary>
/// O panorama da agenda — a aba "Visão geral" (out/2026, de volta ao WPF depois da versão
/// em navegador embutido). Somente leitura: as contagens e a ocupação vêm do
/// <see cref="IOcupacaoAgendaService"/>, e a tela só escolhe o período (dia, semana, mês),
/// o profissional e leva às telas que AGEM (marcar, grade, confirmações…).
///
/// Lê por escopo próprio em cada carga — a tela é de vida longa (checagem 37) — e descarta
/// a resposta que chegar fora de ordem (contador de geração, parcela 60).
/// </summary>
public sealed partial class OcupacaoAgendaViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly (string Valor, string Chave, string Nome)[] Rotas =
    [
        ("confirmacoes", ChavesSuite.ConfirmacoesAgenda, "Confirmar agendamentos"),
        ("retornos", "retornos-a-marcar", "Retornos solicitados"),
        ("conferir", "lancamentos", "Conferir atendimentos"),
        ("convenio", "consultas", "Consultas de convênio"),
        ("enfermagem", "consultorio-sessoes-enfermagem", "Sessões de enfermagem")
    ];

    private readonly IServiceProvider _servicos;
    private readonly int _usuario = SessaoUsuario.Atual.UsuarioId;
    private HashSet<int> _idsProfissionais = [];
    private int _geracao;
    private bool _ativa, _trocandoProfissional;

    public OcupacaoAgendaViewModel(IServiceProvider servicos)
    {
        _servicos = servicos;
        Profissionais.Add(new(null, "Toda a equipe"));
        _profissionalEscolhido = Profissionais[0];
    }

    public ObservableCollection<OpcaoProfissionalOcupacao> Profissionais { get; } = [];
    public ObservableCollection<OperacaoAgenda> Operacoes { get; } = [];
    public ObservableCollection<HorarioOcupacaoLinha> HorariosDoDia { get; } = [];
    public ObservableCollection<DiaOcupacaoCartao> Dias { get; } = [];
    public ObservableCollection<ProfissionalOcupacaoLinha> Equipe { get; } = [];

    [ObservableProperty] private DateOnly _dia = DateOnly.FromDateTime(DateTime.Today);
    /// <summary>"dia", "semana" ou "mes".</summary>
    [ObservableProperty] private string _modo = "semana";
    [ObservableProperty] private OpcaoProfissionalOcupacao? _profissionalEscolhido;
    [ObservableProperty] private OcupacaoAgenda? _dados;
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private string? _erro;
    [ObservableProperty] private OperacaoAgenda? _operacaoEscolhida;

    public bool Bloqueado => Carregando;
    public bool Livre => !Carregando;
    public bool TemErro => Erro is not null;
    public bool TemDados => Dados is not null && Erro is null;
    public bool PodeAgendar => SessaoUsuario.Atual.Pode(Permissao.EditarAgenda) && NavegacaoSuite.Existe(ModuloRecepcao.ChaveMarcarHorario);
    public bool AgendarHabilitado => Livre && TemDados;
    public bool TemOperacoes => Operacoes.Count > 0;
    public bool ModoDia => Modo == "dia";
    public bool ModoSemana => Modo == "semana";
    public bool ModoMes => Modo == "mes";
    public bool ModoCalendario => Modo != "dia";
    public bool Filtrando => ProfissionalEscolhido?.Id is not null;
    public string RotuloAtualizar => Carregando ? "Atualizando…" : "Atualizar";

    /// <summary>O DatePicker trabalha com DateTime; a tela guarda DateOnly.</summary>
    public DateTime? DataReferencia
    {
        get => Dia.ToDateTime(TimeOnly.MinValue);
        set
        {
            if (value is not { } d || Carregando) { OnPropertyChanged(); return; }
            var nova = DateOnly.FromDateTime(d);
            if (nova.Year is < 1901 or > 2199 || nova == Dia) { OnPropertyChanged(); return; }
            Dia = nova;
            _ = CarregarAsync();
        }
    }

    public string TituloPeriodo
    {
        get
        {
            if (Dados is not { } d) return Dia.ToDateTime(TimeOnly.MinValue).ToString("d 'de' MMMM", PtBr);
            var ini = d.Inicio.ToDateTime(TimeOnly.MinValue);
            var fim = d.Fim.ToDateTime(TimeOnly.MinValue);
            return Modo switch
            {
                "mes" => ini.ToString("MMMM 'de' yyyy", PtBr),
                "dia" => ini.ToString("dddd, d 'de' MMMM 'de' yyyy", PtBr),
                _ => $"{ini.ToString("d MMM", PtBr)} — {fim.ToString("d MMM yyyy", PtBr)}"
            };
        }
    }

    public string TituloCalendario => Modo switch
    {
        "dia" => "Atendimentos do dia", "semana" => "Distribuição na semana", _ => "Movimento do mês"
    };
    public string SubtituloCalendario => Modo == "dia" ? "Horários em ordem de atendimento."
        : "Selecione um dia para consultar todos os horários.";

    // Indicadores.
    private MedidaOcupacao? R => Dados?.Resumo;
    public int Atendimentos => R?.Atendimentos ?? 0;
    public string OcupacaoTexto => R?.Percentual is null ? "—" : TextoOcupacao.Percentual(R.Percentual);
    public double OcupacaoFracao => TextoOcupacao.Fracao(R?.Percentual);
    public string OcupacaoDetalhe => R is not { } r ? ""
        : r.SemJornada > 0 ? $"{r.SemJornada} sem jornada completa"
        : r.MinutosDisponiveis > 0 ? $"{TextoOcupacao.Tempo(r.MinutosOcupados)} de {TextoOcupacao.Tempo(r.MinutosDisponiveis)} disponíveis"
        : "Sem jornada disponível neste período";
    public string LivreTexto => R is not { } r ? "" : r.SemJornada > 0 ? "—" : TextoOcupacao.Tempo(r.MinutosLivres);
    public string LivreDetalhe => R?.SemJornada > 0 ? "Complete as jornadas para calcular" : "Somado entre os profissionais";
    public string BloqueadoTexto => R is { } r ? TextoOcupacao.Tempo(r.MinutosBloqueados) : "";
    public string BloqueadoDetalhe => R is { } r ? $"{r.Cancelados} cancelados · {r.Faltas} faltas" : "";

    public bool TemSemJornada => TemDados && R!.SemJornada > 0;
    public string TextoSemJornada
    {
        get
        {
            if (Dados is not { } d) return "";
            var incompletos = d.Profissionais.Where(p => p.Resumo.SemJornada > 0).Select(p => p.Nome).ToList();
            var texto = incompletos.Count > 0 ? $"Jornada incompleta: {string.Join(", ", incompletos)}. " : "";
            if (d.Horarios.Any(h => h.Ocupa && h.ProfissionalId is null)) texto += "Há atendimentos sem profissional vinculado. ";
            return texto + "Os atendimentos continuam visíveis; consulte abaixo as jornadas disponíveis.";
        }
    }
    public bool TemForaDaJornada => TemDados && R!.ForaDaJornada > 0;
    public string TextoForaDaJornada => R is { } r
        ? $"{r.ForaDaJornada} atendimento(s) fora da jornada ou sobre um bloqueio. Confira a grade de horários antes de agendar." : "";
    public bool SemHorariosNoDia => TemDados && ModoDia && HorariosDoDia.Count == 0;
    public bool SemEquipe => TemDados && Equipe.Count == 0;
    public string AtualizadoEm => Dados is { } d ? $"Atualizado às {d.AtualizadoEm:HH:mm}" : "";

    partial void OnCarregandoChanged(bool value)
    {
        foreach (var n in new[] { nameof(Bloqueado), nameof(Livre), nameof(AgendarHabilitado), nameof(RotuloAtualizar) })
            OnPropertyChanged(n);
    }
    partial void OnErroChanged(string? value) => AvisarDados();
    partial void OnDadosChanged(OcupacaoAgenda? value) => AvisarDados();
    partial void OnModoChanged(string value)
    {
        foreach (var n in new[] { nameof(ModoDia), nameof(ModoSemana), nameof(ModoMes), nameof(ModoCalendario),
            nameof(TituloCalendario), nameof(SubtituloCalendario), nameof(TituloPeriodo) })
            OnPropertyChanged(n);
    }
    partial void OnDiaChanged(DateOnly value) { OnPropertyChanged(nameof(DataReferencia)); OnPropertyChanged(nameof(TituloPeriodo)); }
    partial void OnProfissionalEscolhidoChanged(OpcaoProfissionalOcupacao? value)
    {
        OnPropertyChanged(nameof(Filtrando));
        if (_trocandoProfissional) return;
        // Combo cuja lista é refeita escreve NULO: nulo aqui é "toda a equipe", e não recarrega.
        if (value is null) return;
        if (value.Id is { } id && !_idsProfissionais.Contains(id)) return;
        _ = CarregarAsync();
    }
    partial void OnOperacaoEscolhidaChanged(OperacaoAgenda? value)
    {
        if (value is null) return;
        try
        {
            Exigir();
            // A rota sai da lista fixa `Rotas`, conferida por `Existe` ao montar o combo.
            var destino = value.Chave;
            NavegacaoSuite.Ir(destino);
        }
        catch (Exception ex) { Erro = ex.Message; }
        finally { OperacaoEscolhida = null; }
    }

    private void AvisarDados()
    {
        foreach (var n in new[] { nameof(TemErro), nameof(TemDados), nameof(AgendarHabilitado), nameof(TituloPeriodo),
            nameof(Atendimentos), nameof(OcupacaoTexto), nameof(OcupacaoFracao), nameof(OcupacaoDetalhe), nameof(LivreTexto),
            nameof(LivreDetalhe), nameof(BloqueadoTexto), nameof(BloqueadoDetalhe), nameof(TemSemJornada), nameof(TextoSemJornada),
            nameof(TemForaDaJornada), nameof(TextoForaDaJornada), nameof(SemHorariosNoDia), nameof(SemEquipe), nameof(AtualizadoEm) })
            OnPropertyChanged(n);
    }

    private void Exigir()
    {
        if (!SessaoUsuario.Atual.Autenticado || SessaoUsuario.Atual.UsuarioId != _usuario)
            throw new UnauthorizedAccessException("Abra a agenda novamente após entrar.");
        SessaoUsuario.Atual.Exigir(Permissao.VerAgenda, "consultar a ocupação");
    }

    // ==================== Ciclo ====================

    public void Iniciar()
    {
        _ativa = true;
        Operacoes.Clear();
        foreach (var (_, destino, nome) in Rotas)
            if (NavegacaoSuite.Existe(destino)) Operacoes.Add(new(destino, nome));
        OnPropertyChanged(nameof(TemOperacoes)); OnPropertyChanged(nameof(PodeAgendar));
        _ = CarregarAsync();
    }

    public void Parar() { _ativa = false; ++_geracao; }

    /// <summary>A batida do relógio da View: só relê quando não há leitura no ar.</summary>
    public void Bater() { if (!Carregando) _ = CarregarAsync(); }

    private (DateOnly Inicio, DateOnly Fim) Periodo()
    {
        if (Modo == "dia") return (Dia, Dia);
        if (Modo == "semana") { var segunda = Dia.AddDays(-(((int)Dia.DayOfWeek + 6) % 7)); return (segunda, segunda.AddDays(6)); }
        var primeiro = new DateOnly(Dia.Year, Dia.Month, 1);
        return (primeiro, primeiro.AddMonths(1).AddDays(-1));
    }

    private async Task CarregarAsync()
    {
        var g = ++_geracao; Carregando = true; Erro = null;
        var (inicio, fim) = Periodo(); var filtro = ProfissionalEscolhido?.Id;
        try
        {
            Exigir();
            using var scope = _servicos.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IOcupacaoAgendaService>();
            var resultado = await svc.ConsultarAsync(_usuario, inicio, fim, filtro);
            if (!_ativa || g != _geracao || SessaoUsuario.Atual.UsuarioId != _usuario) return;
            Dados = resultado;
            if (filtro is null) RefazerProfissionais(resultado);
            Montar(resultado);
        }
        catch (Exception ex)
        {
            if (_ativa && g == _geracao) { Dados = null; Montar(null); Erro = "Não foi possível consultar a ocupação. Tente atualizar."; }
            Clinica.Application.Diagnostico.Registrar("Ocupação da agenda", ex);
        }
        finally { if (g == _geracao) Carregando = false; }
    }

    private void RefazerProfissionais(OcupacaoAgenda resultado)
    {
        _idsProfissionais = resultado.Profissionais.Select(p => p.Id).ToHashSet();
        _trocandoProfissional = true;
        try
        {
            var escolhido = ProfissionalEscolhido?.Id;
            for (var i = Profissionais.Count - 1; i >= 1; i--) Profissionais.RemoveAt(i);
            foreach (var p in resultado.Profissionais) Profissionais.Add(new(p.Id, p.Nome));
            ProfissionalEscolhido = Profissionais.FirstOrDefault(p => p.Id == escolhido) ?? Profissionais[0];
        }
        finally { _trocandoProfissional = false; }
    }

    /// <summary>Monta as três listas da tela. Entre o Clear e o último Add não há await.</summary>
    private void Montar(OcupacaoAgenda? d)
    {
        HorariosDoDia.Clear(); Dias.Clear(); Equipe.Clear();
        if (d is not null)
        {
            foreach (var h in d.Horarios) HorariosDoDia.Add(new(h));
            if (Modo == "mes" && d.Dias.Count > 0)
            {
                var vazios = ((int)d.Dias[0].Dia.DayOfWeek + 6) % 7;
                for (var i = 0; i < vazios; i++)
                    Dias.Add(new() { Dia = d.Dias[0].Dia, Resumo = d.Dias[0].Resumo, Rotulo = "", Selecionado = false,
                        ModoSemana = false, Previas = [], Vazio = true });
            }
            foreach (var dia in d.Dias)
            {
                var inicioDia = dia.Dia.ToDateTime(TimeOnly.MinValue);
                // Distribuição visual: o horário que atravessa a meia-noite aparece nos dois dias.
                var previas = d.Horarios.Where(h => DateOnly.FromDateTime(h.Inicio) <= dia.Dia && h.Fim > inicioDia && h.Ocupa)
                    .Take(3).Select(h => new PreviaOcupacao(h.Inicio.ToString("HH:mm"), h.Paciente, h.Profissional)).ToList();
                Dias.Add(new()
                {
                    Dia = dia.Dia, Resumo = dia.Resumo, Selecionado = dia.Dia == Dia, ModoSemana = Modo == "semana",
                    Rotulo = Modo == "semana" ? inicioDia.ToString("ddd dd", PtBr) : inicioDia.ToString("dd", PtBr),
                    Previas = Modo == "semana" ? previas : []
                });
            }
            foreach (var p in d.Profissionais) Equipe.Add(new(p));
        }
        OnPropertyChanged(nameof(SemHorariosNoDia)); OnPropertyChanged(nameof(SemEquipe));
    }

    // ==================== Comandos ====================

    [RelayCommand] private Task Atualizar() { Exigir(); return Carregando ? Task.CompletedTask : CarregarAsync(); }

    [RelayCommand]
    private Task EscolherModo(string? modo)
    {
        Exigir();
        if (Carregando || modo is not ("dia" or "semana" or "mes")) { OnModoChanged(Modo); return Task.CompletedTask; }
        Modo = modo;
        return CarregarAsync();
    }

    [RelayCommand] private Task Anterior() => Andar(-1);
    [RelayCommand] private Task Proximo() => Andar(1);

    [RelayCommand]
    private Task Hoje()
    {
        Exigir();
        if (Carregando) return Task.CompletedTask;
        Dia = DateOnly.FromDateTime(DateTime.Today);
        return CarregarAsync();
    }

    private Task Andar(int passo)
    {
        Exigir();
        if (Carregando) return Task.CompletedTask;
        var destino = Modo == "mes" ? Dia.AddMonths(passo) : Dia.AddDays(passo * (Modo == "semana" ? 7 : 1));
        if (destino.Year is < 1901 or > 2199) return Task.CompletedTask;
        Dia = destino;
        return CarregarAsync();
    }

    [RelayCommand]
    private Task AbrirDia(DiaOcupacaoCartao? dia)
    {
        Exigir();
        if (Carregando || dia is null || dia.Vazio || Dados?.Dias.Any(x => x.Dia == dia.Dia) != true) return Task.CompletedTask;
        Dia = dia.Dia; Modo = "dia";
        return CarregarAsync();
    }

    [RelayCommand]
    private void FiltrarProfissional(ProfissionalOcupacaoLinha? linha)
    {
        Exigir();
        if (Carregando) return;
        ProfissionalEscolhido = linha is null ? Profissionais[0]
            : Profissionais.FirstOrDefault(p => p.Id == linha.Id) ?? ProfissionalEscolhido;
    }

    [RelayCommand]
    private void Agendar()
    {
        Exigir();
        if (Dados is null || Erro is not null) return;
        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "agendar");
        var ponte = _servicos.GetRequiredService<PreenchimentoNovoAtendimento>();
        ponte.Definir(new(true, Dia.ToDateTime(TimeOnly.MinValue), ProfissionalEscolhido?.Id, null));
        if (!NavegacaoSuite.Ir(ModuloRecepcao.ChaveMarcarHorario)) ponte.Consumir();
    }

    [RelayCommand]
    private void AbrirGrade()
    {
        Exigir();
        if (Dados is null || Erro is not null) return;
        var ponte = _servicos.GetRequiredService<PedidoAgenda>();
        ponte.AbrirComFiltro(Dia, ProfissionalEscolhido?.Id);
        if (!NavegacaoSuite.Ir(ChavesSuite.AgendaRecepcao)) ponte.Consumir();
    }
}
