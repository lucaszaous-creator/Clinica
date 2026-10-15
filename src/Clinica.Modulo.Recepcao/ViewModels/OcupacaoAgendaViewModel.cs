using System.Collections.ObjectModel;
using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Recepcao.Modulo;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>Uma opção do combo de profissional; <c>Id</c> nulo é "toda a equipe".</summary>
public sealed record OpcaoProfissionalOcupacao(int? Id, string Nome);


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

/// <summary>
/// Um item da coluna do dia na SEMANA (out/2026, mockup aprovado): um atendimento — com o
/// traço na cor da família da modalidade, riscado quando cancelado ou falta — ou um VÃO
/// livre, que é um botão "+ 14:00 livre" que marca ali.
/// </summary>
public sealed class ItemSemanaVisao
{
    public required DateTime Inicio { get; init; }
    public string Hora => Inicio.ToString("HH:mm");
    public HorarioOcupacaoLinha? Horario { get; init; }
    public VaoLivre? Vao { get; init; }
    public bool EhVao => Vao is not null;
    public string Paciente => Horario?.Paciente ?? "";
    public bool Inativo => Horario?.Inativo ?? false;
    public ModalidadeAtendimento Familia => Horario?.Horario.Familia ?? ModalidadeAtendimento.AcupunturaSimples;
    public string Dica => Vao?.Dica ?? $"{Horario?.Paciente} · {Horario?.Modalidade} · {Horario?.Profissional} · {Horario?.Situacao}";
    public string RotuloVao => Vao?.Rotulo ?? "";
}

/// <summary>Uma coluna da semana — o dia com os atendimentos e os vãos em ordem de hora.</summary>
public sealed class ColunaSemanaVisao
{
    public required DateOnly Dia { get; init; }
    public required string Titulo { get; init; }
    public required string Subtitulo { get; init; }
    public required bool EhHoje { get; init; }
    public string? Fechado { get; init; }
    public bool EstaFechado => Fechado is not null;
    public required IReadOnlyList<ItemSemanaVisao> Itens { get; init; }
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
    // ⚠️ AQUI MORAVA O COMBO "Outras operações" (out/2026): confirmações, retornos,
    // conferência, consultas de convênio e sessões de enfermagem — cinco telas que já são
    // ABAS da sidebar ("Operações da agenda" e a Agenda do Consultório). Um segundo menu
    // dentro da tela é um segundo caminho para cada uma delas, e saiu.

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
    public ObservableCollection<HorarioOcupacaoLinha> HorariosDoDia { get; } = [];
    public ObservableCollection<DiaOcupacaoCartao> Dias { get; } = [];
    public ObservableCollection<ProfissionalOcupacaoLinha> Equipe { get; } = [];
    public ObservableCollection<ColunaSemanaVisao> Semana { get; } = [];

    /// <summary>"12 a 17 de outubro" — o período do navegador, como no mockup.</summary>
    public string PeriodoCurto
    {
        get
        {
            if (Dados is not { } d) return TituloPeriodo;
            if (Modo == "dia") return d.Inicio.ToDateTime(TimeOnly.MinValue).ToString("ddd, d 'de' MMMM", PtBr);
            if (Modo == "mes") return d.Inicio.ToDateTime(TimeOnly.MinValue).ToString("MMMM 'de' yyyy", PtBr);
            var ultimo = Semana.Count > 0 ? Semana[^1].Dia : d.Fim;
            return d.Inicio.Month == ultimo.Month
                ? $"{d.Inicio.Day} a {ultimo.Day} de {ultimo.ToDateTime(TimeOnly.MinValue).ToString("MMMM", PtBr)}"
                : $"{d.Inicio.ToDateTime(TimeOnly.MinValue).ToString("d MMM", PtBr)} a {ultimo.ToDateTime(TimeOnly.MinValue).ToString("d MMM", PtBr)}";
        }
    }
    public int ColunasDaSemana => Math.Max(1, Semana.Count);
    public bool TemVagasPossiveis => Filtrando;
    public string DicaDaSemana => !Filtrando ? "Escolha um profissional para ver os horários livres"
        : Dados?.SemJornadaParaVagas == true ? "Cadastre a jornada do profissional para ver os horários livres"
        : "Clique numa vaga para marcar ali";

    [ObservableProperty] private DateOnly _dia = DateOnly.FromDateTime(DateTime.Today);
    /// <summary>"dia", "semana" ou "mes".</summary>
    [ObservableProperty] private string _modo = "semana";
    [ObservableProperty] private OpcaoProfissionalOcupacao? _profissionalEscolhido;
    [ObservableProperty] private OcupacaoAgenda? _dados;
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private string? _erro;

    public bool Bloqueado => Carregando;
    public bool Livre => !Carregando;
    public bool TemErro => Erro is not null;
    public bool TemDados => Dados is not null && Erro is null;
    public bool PodeAgendar => SessaoUsuario.Atual.Pode(Permissao.EditarAgenda) && NavegacaoSuite.Existe(ModuloRecepcao.ChaveMarcarHorario);
    public bool AgendarHabilitado => Livre && TemDados;
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
        OnPropertyChanged(nameof(Filtrando)); OnPropertyChanged(nameof(TemVagasPossiveis)); OnPropertyChanged(nameof(DicaDaSemana));
        if (_trocandoProfissional) return;
        // Combo cuja lista é refeita escreve NULO: nulo aqui é "toda a equipe", e não recarrega.
        if (value is null) return;
        if (value.Id is { } id && !_idsProfissionais.Contains(id)) return;
        _ = CarregarAsync();
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
        OnPropertyChanged(nameof(PodeAgendar));
        ConsumirPedidoDeDia();
        _ = CarregarAsync();
    }

    /// <summary>
    /// A ponte <see cref="PedidoAgenda"/>: a ficha do paciente ("próximos horários") e os
    /// atalhos antigos da grade pedem para a agenda abrir num DIA. A grade saiu da navegação
    /// (out/2026) e o shell redireciona a chave dela para cá — sem consumir aqui, o pedido
    /// ficava ÓRFÃO e a tela abria na semana de sempre, ignorando o clique.
    /// </summary>
    private void ConsumirPedidoDeDia()
    {
        try
        {
            var pedido = _servicos.GetService<PedidoAgenda>();
            if (pedido?.Consumir() is not { } dia) return;
            if (pedido.AplicarFiltro && pedido.ProfissionalId is { } id && _idsProfissionais.Contains(id))
            {
                _trocandoProfissional = true;
                try { ProfissionalEscolhido = Profissionais.FirstOrDefault(p => p.Id == id) ?? ProfissionalEscolhido; }
                finally { _trocandoProfissional = false; }
            }
            Dia = dia;
            Modo = "dia";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Visão geral — pedido de dia da agenda não pôde ser lido", ex);
        }
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
        HorariosDoDia.Clear(); Dias.Clear(); Equipe.Clear(); Semana.Clear();
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
            if (Modo == "semana") MontarSemana(d);
        }
        OnPropertyChanged(nameof(PeriodoCurto)); OnPropertyChanged(nameof(ColunasDaSemana)); OnPropertyChanged(nameof(DicaDaSemana));
        OnPropertyChanged(nameof(SemHorariosNoDia)); OnPropertyChanged(nameof(SemEquipe));
    }

    private static readonly string[] Semanas = ["Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb"];

    /// <summary>
    /// A semana em colunas — cada dia com TODOS os atendimentos e os vãos livres, em ordem de
    /// hora. O domingo só entra quando tem alguma coisa: a clínica não abre, e uma coluna
    /// vazia toda semana gastaria um sétimo da largura para dizer isso.
    /// </summary>
    private void MontarSemana(OcupacaoAgenda d)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        foreach (var dia in d.Dias)
        {
            var inicioDia = dia.Dia.ToDateTime(TimeOnly.MinValue);
            var fimDia = inicioDia.AddDays(1);
            var horarios = d.Horarios.Where(h => h.Inicio >= inicioDia && h.Inicio < fimDia)
                .Select(h => new ItemSemanaVisao { Inicio = h.Inicio, Horario = new(h) });
            var vaos = d.Vagas.Where(v => v.Dia == dia.Dia).Select(v => new ItemSemanaVisao { Inicio = v.Inicio, Vao = v });
            var itens = horarios.Concat(vaos).OrderBy(i => i.Inicio).ThenBy(i => i.EhVao).ToList();
            var fechado = d.Fechados.FirstOrDefault(f => f.Dia == dia.Dia)?.Motivo;
            if (dia.Dia.DayOfWeek == DayOfWeek.Sunday && itens.Count == 0 && fechado is null) continue;

            var nVagas = itens.Count(i => i.EhVao);
            var atend = dia.Resumo.Atendimentos;
            var sub = fechado is not null ? fechado.Replace("Agenda fechada · ", "", StringComparison.Ordinal)
                : $"{atend} {(atend == 1 ? "atendimento" : "atendimentos")}"
                  + (Filtrando && Dados?.SemJornadaParaVagas != true ? (nVagas == 0 ? " · sem vaga" : $" · {nVagas} {(nVagas == 1 ? "vaga" : "vagas")}") : "");
            Semana.Add(new ColunaSemanaVisao
            {
                Dia = dia.Dia,
                Titulo = $"{Semanas[(int)dia.Dia.DayOfWeek]} {dia.Dia.Day}",
                Subtitulo = dia.Dia == hoje ? $"hoje · {sub}" : sub,
                EhHoje = dia.Dia == hoje,
                Fechado = fechado,
                Itens = itens
            });
        }
    }

    /// <summary>
    /// Clique no vão livre: o Marcar já com o dia, a HORA do vão e o profissional filtrado —
    /// redigitar a hora que se acabou de apontar é onde ela sai errada (parcela 58).
    /// </summary>
    [RelayCommand]
    private void MarcarNoVao(ItemSemanaVisao? item)
    {
        Exigir();
        if (item?.Vao is not { } vao || Dados is null || Erro is not null) return;
        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "agendar");
        var ponte = _servicos.GetRequiredService<PreenchimentoNovoAtendimento>();
        ponte.Definir(new(true, vao.Inicio, ProfissionalEscolhido?.Id, null));
        if (!NavegacaoSuite.Ir(ModuloRecepcao.ChaveMarcarHorario)) ponte.Consumir();
    }

    /// <summary>Clique num atendimento da semana: a janela do horário, como na lista do dia.</summary>
    [RelayCommand]
    private Task AbrirItemSemana(ItemSemanaVisao? item) => item?.Horario is { } h ? AbrirHorarioAsync(h) : Task.CompletedTask;

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

    // ==================== A janela do horário e "Mais ações" ====================
    //
    // A GRADE saiu da navegação (out/2026, mockup aprovado: "a visão geral já não contempla
    // a semana do profissional?"), e com ela iam junto a janela do horário e cinco ações
    // de agenda que só ela oferecia. O dono delas continua sendo o AgendaViewModel — sem
    // ser desenhado — para nenhuma ganhar uma segunda cópia (permissão, aviso e recarga
    // num lugar só). Cada chamada abre escopo próprio: a tela é de vida longa.

    /// <summary>Clique numa linha do modo DIA: a janela do horário (remarcar, reabrir, comprovante…).</summary>
    [RelayCommand]
    private async Task AbrirHorarioAsync(HorarioOcupacaoLinha? linha)
    {
        Exigir();
        if (linha is null || Carregando) return;
        await ComAgendaAsync("abrir o horário", async agenda =>
        {
            if (!await agenda.AbrirJanelaDoHorarioAsync(DateOnly.FromDateTime(linha.Horario.Inicio), linha.Horario.Id))
                Avisar("Este horário não está mais neste dia — ele pode ter sido remarcado em outra máquina.");
        });
    }

    public bool PodeMexerNaAgenda => SessaoUsuario.Atual.Pode(Permissao.EditarAgenda);

    [RelayCommand]
    private Task FecharAgendaAsync() => AcaoDeAgendaAsync("fechar a agenda", a => a.FecharAgendaCommand.ExecuteAsync(null));

    [RelayCommand]
    private Task HorariosETravasAsync() => AcaoDeAgendaAsync("configurar horários e travas", a => a.ConfigurarHorariosCommand.ExecuteAsync(null));

    [RelayCommand]
    private Task ListaDeEsperaAsync() => AcaoDeAgendaAsync("abrir a lista de espera", a => a.AbrirEsperaCommand.ExecuteAsync(null));

    [RelayCommand]
    private Task ConciliarAgendaAsync() => AcaoDeAgendaAsync("conciliar a agenda", a => a.ConciliarAgendaCommand.ExecuteAsync(null));

    [RelayCommand]
    private Task ImprimirFolhaAsync() => AcaoDeAgendaAsync("imprimir a folha do dia", a => a.ImprimirAgendaCommand.ExecuteAsync(null));

    private Task AcaoDeAgendaAsync(string ato, Func<AgendaViewModel, Task> acao)
    {
        Exigir();
        if (Carregando) return Task.CompletedTask;
        return ComAgendaAsync(ato, async agenda =>
        {
            await agenda.PosicionarAsync(Dia, ProfissionalEscolhido?.Id);
            await acao(agenda);
        });
    }

    private async Task ComAgendaAsync(string ato, Func<AgendaViewModel, Task> acao)
    {
        Mensagem = string.Empty;
        try
        {
            using var scope = _servicos.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaViewModel>();
            await acao(agenda);
            if (!string.IsNullOrWhiteSpace(agenda.Mensagem)) Avisar(agenda.Mensagem, agenda.MensagemEhErro);
            if (_ativa) await CarregarAsync();
        }
        catch (UnauthorizedAccessException ex) { Avisar(ex.Message, erro: true); }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar($"Visão geral — {ato}", ex);
            Avisar($"Não foi possível {ato}: {ex.Message}", erro: true);
        }
    }

    /// <summary>Recado de uma ação — perto do cabeçalho, como a mensagem das outras telas.</summary>
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    private void Avisar(string texto, bool erro = false) { Mensagem = texto; MensagemEhErro = erro; }
}
