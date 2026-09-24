using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

public sealed record FiltroProfissional(int? Id, string Nome);
public sealed record FiltroSala(int? Id, string Nome);

/// <summary>Projeção visual da agenda. Não grava agendamentos nem altera seus estados.</summary>
public sealed record BlocoAgendaVisual(double Topo, double Altura, string Rotulo,
    CartaoAgenda? Cartao = null, CelulaAgenda? Celula = null,
    string? Detalhe = null, bool Disponivel = false, int Raia = 0, int Raias = 1,
    CelulaAgenda? Encaixe = null)
{
    public bool TemCartao => Cartao is not null;
    public bool TemVao => Celula is not null;
    public bool Bloqueado => Celula is { Bloqueada: true } or { Expediente: true };
    public bool PodeSobrepor => Encaixe?.PodeSobrepor == true;
}
public sealed record ColunaAgendaVisual(string Nome, string Resumo, IReadOnlyList<BlocoAgendaVisual> Blocos);
public sealed record HoraAgendaVisual(double Topo, string Rotulo);

public sealed partial class AgendaViewModel
{
    public ObservableCollection<FiltroProfissional> FiltroProfissionais { get; } = [];
    public ObservableCollection<FiltroSala> FiltroSalas { get; } = [];
    public ObservableCollection<EntradaModalidade> ModalidadesPlanejamento { get; } = [];
    public ObservableCollection<Vaga> VagasPlanejamento { get; } = [];
    public ObservableCollection<ColunaAgendaVisual> ColunasPlanejamento { get; } = [];
    public ObservableCollection<HoraAgendaVisual> ReguaPlanejamento { get; } = [];
    [ObservableProperty] private FiltroProfissional? _filtroProfissional;
    [ObservableProperty] private FiltroSala? _filtroSala;
    [ObservableProperty] private EntradaModalidade? _modalidadePlanejamento;
    [ObservableProperty] private string _duracaoPlanejamento = "30";
    [ObservableProperty] private string _estadoVagas = "Selecione um profissional para consultar as vagas.";
    [ObservableProperty] private string _ultimaLeitura = "Disponibilidade ainda não consultada";
    [ObservableProperty] private bool _buscandoVagas;
    [ObservableProperty] private bool _disponibilidadeNaoVerificada = true;
    [ObservableProperty] private double _alturaPlanejamento = 1560;
    private IReadOnlyList<Profissional> _profissionaisPlanejamento = [];
    private IReadOnlyList<Sala> _salasPlanejamento = [];
    private IReadOnlyList<Agendamento> _horariosPlanejamento = [];
    private IReadOnlyList<Agendamento> _ocupacoesAnteriores = [];
    private string _ultimaConsultaConcluida = "nenhuma leitura concluída";
    private bool _preparandoFiltros;
    private bool _filtrosPreparados;
    private int _geracaoVagas;
    public int? ProfissionalEmFocoId => FiltroProfissional?.Id;
    public string ProfissionalEmFocoNome => FiltroProfissional?.Nome ?? "Todos os profissionais";
    public string TravaPlanejamento => ProfissionalDoFiltro is { } p
        ? p.AgendaProtegida ? "Trava ativa" : "Conflitos como aviso" : "Escolha um profissional";
    public string JornadaPlanejamento => ProfissionalDoFiltro?.DescricaoJornada ?? "Jornada não informada";
    public string ResumoPlanejamento => $"{ProfissionalEmFocoNome} · {DuracaoPlanejamento} minutos";
    public string SalaPlanejamento => FiltroSala?.Id is null
        ? "Sem sala selecionada: disponibilidade somente do profissional."
        : $"Profissional e {FiltroSala.Nome}. O paciente será conferido no agendamento.";
    private Profissional? ProfissionalDoFiltro => _profissionaisPlanejamento.FirstOrDefault(p => p.Id == ProfissionalEmFocoId);

    private void PrepararFiltros(IReadOnlyList<Profissional> profissionais, IReadOnlyList<Sala> salas)
    {
        _preparandoFiltros = true;
        try
        {
            _profissionaisPlanejamento = profissionais; _salasPlanejamento = salas;
            var escolhido = _filtrosPreparados ? ProfissionalEmFocoId : SessaoUsuario.Atual.ProfissionalId;
            var sala = FiltroSala?.Id;
            FiltroProfissionais.Clear(); FiltroProfissionais.Add(new(null, "Todos os profissionais"));
            foreach (var p in profissionais) FiltroProfissionais.Add(new(p.Id, p.Rotulo));
            FiltroProfissional = FiltroProfissionais.FirstOrDefault(p => p.Id == escolhido) ?? FiltroProfissionais[0];
            FiltroSalas.Clear(); FiltroSalas.Add(new(null, "Sem filtrar sala"));
            foreach (var s in salas) FiltroSalas.Add(new(s.Id, s.Nome));
            FiltroSala = FiltroSalas.FirstOrDefault(s => s.Id == sala) ?? FiltroSalas[0];
            if (!_filtrosPreparados)
            {
                foreach (var m in CatalogoModalidades.Ativas) ModalidadesPlanejamento.Add(m);
                ModalidadePlanejamento = ModalidadesPlanejamento.FirstOrDefault(m => m.Base == ModalidadeAtendimento.Consulta)
                    ?? ModalidadesPlanejamento.FirstOrDefault();
                DuracaoPlanejamento = (ProfissionalDoFiltro?.DuracaoPadraoMinutos ?? Agendamento.DuracaoPadraoMinutos).ToString();
            }
            _filtrosPreparados = true;
            NotificarContextoPlanejamento();
        }
        finally { _preparandoFiltros = false; }
    }

    private void NotificarContextoPlanejamento()
    {
        OnPropertyChanged(nameof(ProfissionalEmFocoId)); OnPropertyChanged(nameof(ProfissionalEmFocoNome));
        OnPropertyChanged(nameof(TravaPlanejamento)); OnPropertyChanged(nameof(JornadaPlanejamento));
        OnPropertyChanged(nameof(ResumoPlanejamento)); OnPropertyChanged(nameof(SalaPlanejamento));
    }
    private void InvalidarVagas()
    {
        ++_geracaoVagas; BuscandoVagas = false; VagasPlanejamento.Clear();
        EstadoVagas = "Use Próximas vagas para conferir este filtro.";
    }
    partial void OnDisponibilidadeNaoVerificadaChanged(bool value)
    {
        if (!value) return;
        InvalidarVagas();
        MontarPlanejamento();
    }
    partial void OnFiltroProfissionalChanged(FiltroProfissional? value)
    {
        if (_preparandoFiltros) return;
        InvalidarVagas(); NotificarContextoPlanejamento(); _ = CarregarAsync();
    }
    partial void OnFiltroSalaChanged(FiltroSala? value)
    {
        if (_preparandoFiltros) return;
        InvalidarVagas(); NotificarContextoPlanejamento(); _ = CarregarAsync();
    }
    partial void OnDuracaoPlanejamentoChanged(string value)
    {
        if (_preparandoFiltros) return;
        InvalidarVagas(); NotificarContextoPlanejamento(); MontarPlanejamento();
    }
    partial void OnModalidadePlanejamentoChanged(EntradaModalidade? value)
    {
        if (!_preparandoFiltros) InvalidarVagas();
    }

    private async Task ConsultarVagasPlanejamentoAsync()
    {
        InvalidarVagas();
        if (ProfissionalEmFocoId is not { } profissionalId)
        { EstadoVagas = "Selecione o profissional para consultar a disponibilidade."; return; }
        if (!int.TryParse(DuracaoPlanejamento, out var duracao) || duracao is < 1 or > 720)
        { EstadoVagas = "Informe a duração entre 1 e 720 minutos."; return; }
        var geracao = ++_geracaoVagas;
        var inicio = Dia.Date < DateTime.Now ? DateTime.Now : Dia.Date;
        BuscandoVagas = true; EstadoVagas = "Conferindo profissional, jornada e bloqueios…";
        try
        {
            using var scope = _escopos.CreateScope();
            var resultado = await scope.ServiceProvider.GetRequiredService<BuscaDeVagasService>()
                .ProximasComRecursosAsync(profissionalId, inicio, duracao, FiltroSala?.Id);
            if (geracao != _geracaoVagas) return;
            foreach (var vaga in resultado.Vagas) VagasPlanejamento.Add(vaga);
            EstadoVagas = resultado.Vazio ? "Nenhuma vaga encontrada nos próximos 60 dias."
                : $"Conferido às {DateTime.Now:HH:mm}. Ao agendar, os conflitos serão conferidos novamente."
                  + (resultado.JornadaPresumida ? " Jornada presumida: segunda a sábado, 7h às 20h." : "");
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoVagas) return;
            EstadoVagas = "Disponibilidade não verificada. Tente atualizar a busca.";
            Clinica.Application.Diagnostico.Registrar("Agenda — busca de vagas", ex);
        }
        finally { if (geracao == _geracaoVagas) BuscandoVagas = false; }
    }

    private async Task AbrirVagaPlanejamentoAsync(Vaga? vaga)
    {
        if (vaga is null || !VagasPlanejamento.Contains(vaga)) return;
        SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "marcar atendimento");
        if (IrParaNovoAtendimento(new PedidoNovoAtendimento(true, vaga.Inicio, ProfissionalEmFocoId,
                FiltroSala?.Id, DuracaoMinutos: int.Parse(DuracaoPlanejamento),
                ModalidadeCodigo: ModalidadePlanejamento?.Codigo))) return;
        await AbrirFormularioAsync(new AgendamentoEdicaoViewModel(_escopos)
        { Data = vaga.Inicio.Date, Hora = vaga.Inicio.ToString("HH:mm"),
          ProfissionalPreferidoId = ProfissionalEmFocoId, SalaPreferidaId = FiltroSala?.Id,
          Duracao = DuracaoPlanejamento, ModalidadeSelecionada = ModalidadePlanejamento });
    }

    /// <summary>Blocos contínuos com a duração real; sobreposições ocupam raias distintas.</summary>
    private void MontarPlanejamento()
    {
        ColunasPlanejamento.Clear(); ReguaPlanejamento.Clear();
        if (!BuscandoVagas) VagasPlanejamento.Clear();
        if (Faixas.Count == 0) return;
        var inicioMinuto = Faixas[0].Hora.Hour * 60 + Faixas[0].Hora.Minute;
        AlturaPlanejamento = Faixas.Count * 36d;
        foreach (var faixa in Faixas)
            ReguaPlanejamento.Add(new((faixa.Hora.Hour * 60 + faixa.Hora.Minute - inicioMinuto) * 1.2,
                faixa.Hora.ToString("HH:mm")));
        var duracaoValida = int.TryParse(DuracaoPlanejamento, out var duracao) && duracao is > 0 and <= 720;
        for (var indice = 0; indice < Colunas.Count; indice++)
        {
            var coluna = Colunas[indice]; var blocos = new List<BlocoAgendaVisual>();
            var inicio = coluna.Data.ToDateTime(TimeOnly.MinValue).AddMinutes(inicioMinuto);
            var profissional = coluna.Profissional ?? ProfissionalDoFiltro;
            var salaId = coluna.SalaId ?? FiltroSala?.Id;
            var sala = _salasPlanejamento.FirstOrDefault(s => s.Id == salaId);
            var vagas = coluna.Data >= DateOnly.FromDateTime(DateTime.Today)
                && profissional is { } p && duracaoValida && !DisponibilidadeNaoVerificada
                && (salaId is null || sala is not null)
                ? BuscaDeVagas.Calcular(inicio > DateTime.Now ? inicio : DateTime.Now, duracao, p,
                    _horariosPlanejamento, _bloqueios, quantidade: 100, diasMaximos: 1,
                    sala: sala)
                    .Where(v => DateOnly.FromDateTime(v.Inicio) == coluna.Data).ToList()
                : [];
            var disponiveis = vagas.Select(v => v.Inicio).ToHashSet();
            if (!BuscandoVagas && ProfissionalEmFocoId is not null && !(AgruparPorSala && !ModoSemana))
                foreach (var vaga in vagas.Take(Math.Max(0, 10 - VagasPlanejamento.Count))) VagasPlanejamento.Add(vaga);
            DateTime? proximoBloco = null;
            foreach (var faixa in Faixas)
            {
                var celula = faixa.Celulas[indice];
                var topo = (celula.Quando - inicio).TotalMinutes * 1.2;
                if (celula.Continuacao || celula.Cartoes.Any(c => !c.ForaDoDia)
                    || celula.Quando < proximoBloco) continue;
                var livre = disponiveis.Contains(celula.Quando);
                var minutosBloco = 30;
                if (livre) minutosBloco = duracao;
                else if (celula.Bloqueada || celula.Expediente)
                {
                    var seguintes = Faixas.Select(f => f.Celulas[indice]).Where(c => c.Quando > celula.Quando)
                        .TakeWhile(c => c.Bloqueio == celula.Bloqueio && c.ForaDoExpediente == celula.ForaDoExpediente
                            && !c.Continuacao && !c.Cartoes.Any(h => !h.ForaDoDia)).Count();
                    minutosBloco += seguintes * 30;
                }
                proximoBloco = celula.Quando.AddMinutes(minutosBloco);
                var salaOcupada = sala is not null && _horariosPlanejamento.Count(a => a.OcupaAgenda && a.SalaId == sala.Id
                    && a.ColideCom(celula.Quando, celula.Quando.AddMinutes(duracao))) >= sala.Capacidade;
                var rotulo = DisponibilidadeNaoVerificada ? "Não verificado"
                    : celula.Bloqueada ? celula.Bloqueio!
                    : celula.Expediente ? "Fora do expediente"
                    : celula.NoPassado ? "Horário passado"
                    : profissional is { } dono && !BuscaDeVagas.AtendeNoDia(dono, coluna.Data.DayOfWeek)
                        ? "Sem jornada para este dia"
                    : salaId is not null && sala is null ? "Sala indisponível"
                    : livre ? $"Disponível · {duracao} min"
                    : salaOcupada ? "Sala ocupada"
                    : profissional is null ? "Consultar horário" : $"Não cabe {duracao} min";
                blocos.Add(new(topo, Math.Max(2, minutosBloco * 1.2 - 2), rotulo, Celula: celula,
                    Detalhe: celula.ForaDoExpediente, Disponivel: livre));
            }
            var horarios = coluna.Horarios.OrderBy(h => h.DataHora).ThenBy(h => h.Fim).ToList();
            var finais = new List<DateTime>(); var cartoes = new List<(CartaoAgenda Cartao, int Raia)>();
            foreach (var horario in horarios)
            {
                var raia = finais.FindIndex(f => f <= horario.DataHora);
                if (raia < 0) { raia = finais.Count; finais.Add(horario.Fim); }
                else finais[raia] = horario.Fim;
                cartoes.Add((horario, raia));
            }
            foreach (var (cartao, raia) in cartoes)
            {
                var dono = _profissionaisPlanejamento.FirstOrDefault(p => p.Id == cartao.ProfissionalId)
                    ?? _horariosPlanejamento.FirstOrDefault(a => a.Id == cartao.AgendamentoId)?.Profissional;
                var encaixe = new CelulaAgenda { ProfissionalId = cartao.ProfissionalId,
                    SalaId = coluna.SalaId ?? FiltroSala?.Id, Quando = cartao.DataHora,
                    Cartoes = [cartao], Continuacao = false, NoPassado = cartao.DataHora < DateTime.Now,
                    AgendaProtegida = dono?.AgendaProtegida == true };
                blocos.Add(new((cartao.DataHora - inicio).TotalMinutes * 1.2,
                    Math.Max(2, (cartao.Fim - cartao.DataHora).TotalMinutes * 1.2 - 2),
                    cartao.Faixa, Cartao: cartao, Raia: raia, Raias: Math.Max(1, finais.Count), Encaixe: encaixe));
            }
            ColunasPlanejamento.Add(new(coluna.Nome, coluna.Resumo, blocos));
        }
        // O painel lateral segue os filtros do cabeçalho. A grade por sala calcula cada
        // recurso separadamente; juntar essas listas repetiria horários e perderia a sala.
        if (!BuscandoVagas && AgruparPorSala && !ModoSemana && !DisponibilidadeNaoVerificada
            && duracaoValida && ProfissionalDoFiltro is { } selecionado && Dia.Date >= DateTime.Today)
            foreach (var vaga in BuscaDeVagas.Calcular(Dia.Date > DateTime.Now ? Dia.Date : DateTime.Now,
                duracao, selecionado, _horariosPlanejamento, _bloqueios, diasMaximos: 1,
                sala: _salasPlanejamento.FirstOrDefault(s => s.Id == FiltroSala?.Id)))
                VagasPlanejamento.Add(vaga);
        if (!BuscandoVagas)
            EstadoVagas = ProfissionalEmFocoId is null ? "Escolha um profissional para consultar vagas."
                : DisponibilidadeNaoVerificada ? "Disponibilidade não verificada. Atualize a agenda."
                : VagasPlanejamento.Count == 0 ? "Nenhuma vaga neste período. Consulte mais dias."
                : "Horários do período aberto. Ao agendar, os conflitos serão conferidos novamente.";
    }
}
