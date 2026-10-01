using System.Collections.ObjectModel;
using Clinica.Application.Abstracoes;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

public sealed record EscolhaRecall(string Codigo, string Nome);

public sealed partial class AcompanhamentoViewModel(IServiceScopeFactory escopos) : ObservableObject, ICarregarAoAbrir
{
    private IReadOnlyList<LinhaAcompanhamento> _todos = [];
    private Guid _idempotencia = Guid.NewGuid();
    private bool _recallInicializado;
    public ObservableCollection<LinhaAcompanhamento> Pacientes { get; } = [];
    public ObservableCollection<ContatoAcompanhamento> Historico { get; } = [];
    public ObservableCollection<OpcaoAcompanhamento> Responsaveis { get; } = [];
    public ObservableCollection<OpcaoAcompanhamento> Profissionais { get; } = [];
    public ObservableCollection<OpcaoAcompanhamento> Motivos { get; } = [];
    public ObservableCollection<OpcaoAcompanhamento> ResponsaveisFiltro { get; } = [];
    public ObservableCollection<OpcaoAcompanhamento> MotivosFiltro { get; } = [];
    public ObservableCollection<OpcaoAcompanhamento> MotivosEdicao { get; } = [];
    public ObservableCollection<string> Convenios { get; } = [];
    public ObservableCollection<string> ProfissionaisFiltro { get; } = [];
    public IReadOnlyList<EscolhaRecall> Modalidades { get; } = new[] { new EscolhaRecall("", "Todas as modalidades") }
        .Concat(Enum.GetValues<ModalidadeAtendimento>().Select(m => new EscolhaRecall(m.ToString(), RotulosEnum.De(m)))).ToArray();
    public IReadOnlyList<EscolhaRecall> Etapas { get; } = Enum.GetValues<EtapaAcompanhamento>()
        .Select(e => new EscolhaRecall(e.ToString(), RegrasAcompanhamento.Rotulo(e))).ToArray();
    public IReadOnlyList<EscolhaRecall> Canais { get; } = new[] { new EscolhaRecall("", "Atualização administrativa") }
        .Concat(Enum.GetValues<CanalContato>().Select(c => new EscolhaRecall(c.ToString(), c.ToString()))).ToArray();
    public IReadOnlyList<string> EstadosFila { get; } = ["Pendentes", "Todas", "A contatar", "Sem resposta", "Retornar na data combinada", "Aguardando plano", "Pronto para agendar", "Cancelou", "Faltou", "Conferir comparecimento", "Agendado", "Sessão realizada", "Encerrado com justificativa"];
    public IReadOnlyList<string> Prazos { get; } = ["Todos os prazos", "Hoje", "Atrasados", "Próximos 7 dias"];
    public IReadOnlyList<string> Condicoes { get; } = ["Todas as condições", "A assumir", "Sem primeiro contato", "Com tentativas", "Pacote com saldo", "Sem telefone", "Contato não autorizado", "Cancelou ou faltou"];
    public bool Gestor => SessaoUsuario.Atual.Perfil == PerfilAcesso.Gerente;
    public bool PodeAgendar => SessaoUsuario.Atual.Pode(Permissao.EditarAgenda);
    public bool PodeInteragir => !Carregando;
    public bool ListaVisivel => Selecionado == null && !Configurando && !Filtrando;
    public bool DetalheVisivel => Selecionado != null && !Configurando && !Filtrando;
    public bool EhRecall => Aba == "Recall";
    public string Titulo => EhRecall ? "Recall de pacientes" : "Novos pacientes BSV";
    public string DescricaoFila => EhRecall ? "Encontre quem está sem retornar e organize o próximo contato." : "Acompanhe cada indicação até o agendamento da primeira sessão de bloqueio.";
    public string FiltrosAtivos => string.Join(" · ", new[] {
        Situacao != "Pendentes" ? Situacao : "Pendentes", Prazo != "Todos os prazos" ? "Contato: " + Prazo.ToLowerInvariant() : null,
        Condicao != "Todas as condições" ? Condicao : null, Convenio != "Todos os convênios" ? Convenio : null,
        Profissional != "Todos os profissionais" ? Profissional : null, ResponsavelFiltro > 0 ? Responsaveis.FirstOrDefault(x => x.Id == ResponsavelFiltro)?.Nome : null,
        MotivoFiltro > 0 ? Motivos.FirstOrDefault(x => x.Id == MotivoFiltro)?.Nome : null, SomenteMeus ? "Somente meus" : null,
        DiasMinimos != "0" ? "Sem retornar há " + DiasMinimos + " dias ou mais" : null,
        !string.IsNullOrWhiteSpace(DiasMaximos) ? "Até " + DiasMaximos + " dias" : null,
        TentativasMinimas != "0" ? TentativasMinimas + "+ tentativas" : null,
        ContatoDesde != null || ContatoAte != null ? "Período de contato aplicado" : null }.Where(x => x != null));
    public string ConfiguracaoTexto => ProfissionalBsv is > 0 ? "Indicações BSV: " + Profissionais.FirstOrDefault(x => x.Id == ProfissionalBsv)?.Nome : "Selecione o profissional que indicará os novos pacientes BSV.";
    public string UltimaAtualizacaoTexto => _ultimaAtualizacao is {} em ? $"Lista atualizada às {em:HH:mm}" : "Carregando pacientes…";
    private DateTime? _ultimaAtualizacao;
    public string Resumo => $"{Pacientes.Count} de {_todos.Count(x => x.Tipo == Tipo)} no filtro · {_todos.Count(x => x.Tipo == Tipo && x.Pendente)} pendentes · {_todos.Count(x => x.Tipo == Tipo && x.Atrasado)} atrasados · {_todos.Count(x => x.Tipo == Tipo && x.Situacao == "Agendado")} agendados · {_todos.Count(x => x.Tipo == Tipo && x.Situacao == "Sessão realizada")} realizados";
    public string HojeTexto => $"Meus contatos de hoje ({_todos.Count(x => x.Tipo == Tipo && x.Pendente && x.ResponsavelId == SessaoUsuario.Atual.UsuarioId && x.ProximoContato <= Hoje)})";
    public string AtrasadosTexto => $"Atrasados ({_todos.Count(x => x.Tipo == Tipo && x.Atrasado)})";
    public string AAssumirTexto => $"A assumir ({_todos.Count(x => x.Tipo == Tipo && x.Pendente && x.ResponsavelId == null)})";
    public string SemContatoTexto => $"Sem primeiro contato ({_todos.Count(x => x.Tipo == Tipo && x.Pendente && x.Tentativas == 0)})";
    public string CanceladosTexto => $"Cancelados / faltosos ({_todos.Count(x => x.Tipo == Tipo && x.Pendente && (x.Cancelou || x.Faltou))})";
    private TipoAcompanhamento Tipo => EhRecall ? TipoAcompanhamento.Recall : TipoAcompanhamento.NovoBsv;
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] private string _aba = "Recall";
    [ObservableProperty] private string _busca = "";
    [ObservableProperty] private string _modalidade = "";
    [ObservableProperty] private string _situacao = "Pendentes";
    [ObservableProperty] private string _prazo = "Todos os prazos";
    [ObservableProperty] private string _condicao = "Todas as condições";
    [ObservableProperty] private string _convenio = "Todos os convênios";
    [ObservableProperty] private string _profissional = "Todos os profissionais";
    [ObservableProperty] private int _responsavelFiltro;
    [ObservableProperty] private int _motivoFiltro;
    [ObservableProperty] private string _tentativasMinimas = "0";
    [ObservableProperty] private DateTime? _contatoDesde;
    [ObservableProperty] private DateTime? _contatoAte;
    [ObservableProperty] private bool _somenteMeus;
    [ObservableProperty] private string _diasMinimos = "0";
    [ObservableProperty] private string _diasMaximos = "";
    [ObservableProperty] private string _diasRecall = "60";
    [ObservableProperty] private string _mensagem = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PodeInteragir))] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private bool _configurando;
    [ObservableProperty] private bool _filtrando;
    [ObservableProperty] private LinhaAcompanhamento? _selecionado;
    public string ResponsavelContato => SessaoUsuario.Atual.Nome + " (usuário conectado)";
    [ObservableProperty] private DateTime? _proximoContato;
    [ObservableProperty] private string _etapaEdicao = nameof(EtapaAcompanhamento.AContatar);
    [ObservableProperty] private string _canalEdicao = "";
    [ObservableProperty] private string _observacao = "";
    [ObservableProperty] private bool _encerrar;
    [ObservableProperty] private bool _reabrir;
    [ObservableProperty] private int _motivoEdicao;
    [ObservableProperty] private int? _profissionalBsv;
    [ObservableProperty] private int _responsavelPadrao;
    [ObservableProperty] private string _novoMotivo = "";
    public ObservableCollection<string> VisaoGestao { get; } = [];
    partial void OnAbaChanged(string value) { OnPropertyChanged(nameof(EhRecall)); OnPropertyChanged(nameof(Titulo)); OnPropertyChanged(nameof(DescricaoFila)); Refiltrar(); }
    partial void OnProfissionalBsvChanged(int? value) => OnPropertyChanged(nameof(ConfiguracaoTexto));
    partial void OnBuscaChanged(string value) => Refiltrar();
    partial void OnModalidadeChanged(string value) => Refiltrar();
    partial void OnSituacaoChanged(string value) => Refiltrar();
    partial void OnPrazoChanged(string value) => Refiltrar();
    partial void OnCondicaoChanged(string value) => Refiltrar();
    partial void OnConvenioChanged(string value) => Refiltrar();
    partial void OnProfissionalChanged(string value) => Refiltrar();
    partial void OnResponsavelFiltroChanged(int value) => Refiltrar();
    partial void OnMotivoFiltroChanged(int value) => Refiltrar();
    partial void OnTentativasMinimasChanged(string value) => Refiltrar();
    partial void OnContatoDesdeChanged(DateTime? value) => Refiltrar();
    partial void OnContatoAteChanged(DateTime? value) => Refiltrar();
    partial void OnSomenteMeusChanged(bool value) => Refiltrar();
    partial void OnDiasMinimosChanged(string value) => Refiltrar();
    partial void OnDiasMaximosChanged(string value) => Refiltrar();
    partial void OnConfigurandoChanged(bool value) => Visibilidade();
    partial void OnFiltrandoChanged(bool value) => Visibilidade();
    partial void OnSelecionadoChanged(LinhaAcompanhamento? value) => Visibilidade();
    private void Visibilidade() { OnPropertyChanged(nameof(ListaVisivel)); OnPropertyChanged(nameof(DetalheVisivel)); }
    public Task CarregarAsync() => RecarregarAsync();
    [RelayCommand] public async Task RecarregarAsync()
    {
        if (Carregando || DetalheVisivel || Configurando || Filtrando) return;
        NaoVerificado = false;
        await Executar(async svc =>
        {
            var config = await svc.ConfiguracaoAsync(SessaoUsuario.Atual.UsuarioId);
            AtualizarOpcoes(Responsaveis, config.Responsaveis);
            AtualizarOpcoes(Profissionais, config.Profissionais);
            AtualizarOpcoes(Motivos, config.Motivos);
            AtualizarOpcoes(ResponsaveisFiltro, new[] { new OpcaoAcompanhamento(0, "Todas as responsáveis") }.Concat(config.Responsaveis));
            AtualizarOpcoes(MotivosFiltro, new[] { new OpcaoAcompanhamento(0, "Todos os motivos") }.Concat(config.Motivos));
            AtualizarOpcoes(MotivosEdicao, new[] { new OpcaoAcompanhamento(0, "Sem motivo especial") }.Concat(config.Motivos));
            ProfissionalBsv = config.ProfissionalBsvId > 0 ? config.ProfissionalBsvId : null; ResponsavelPadrao = config.ResponsavelPadraoId;
            OnPropertyChanged(nameof(ProfissionalBsv)); OnPropertyChanged(nameof(ConfiguracaoTexto));
            if (!_recallInicializado && EhRecall)
            {
                await svc.GerarRecallAsync(SessaoUsuario.Atual.UsuarioId, 60);
                _recallInicializado = true;
            }
            _todos = await svc.ListarAsync(SessaoUsuario.Atual.UsuarioId);
            var convenioAtual = Convenio; var profissionalAtual = Profissional;
            Convenios.Clear(); Convenios.Add("Todos os convênios"); foreach (var x in _todos.Select(x => x.Convenio).Distinct().Order()) Convenios.Add(x);
            ProfissionaisFiltro.Clear(); ProfissionaisFiltro.Add("Todos os profissionais"); foreach (var x in _todos.Select(x => x.Profissional).Where(x => x != "—").Distinct().Order()) ProfissionaisFiltro.Add(x);
            Convenio = Convenios.Contains(convenioAtual) ? convenioAtual : "Todos os convênios";
            Profissional = ProfissionaisFiltro.Contains(profissionalAtual) ? profissionalAtual : "Todos os profissionais";
            Refiltrar();
            _ultimaAtualizacao = DateTime.Now; OnPropertyChanged(nameof(UltimaAtualizacaoTexto));
        });
        NaoVerificado = _falhou;
    }
    private static void AtualizarOpcoes(ObservableCollection<OpcaoAcompanhamento> destino, IEnumerable<OpcaoAcompanhamento> origem)
    {
        var novas = origem.ToList();
        // Preserve the selected item while refreshing; Clear() can erase WPF SelectedValue.
        for (var i = 0; i < novas.Count; i++)
        {
            if (i < destino.Count && destino[i] == novas[i]) continue;
            var existente = destino.IndexOf(novas[i]);
            if (existente >= 0) destino.Move(existente, i); else destino.Insert(i, novas[i]);
        }
        while (destino.Count > novas.Count) destino.RemoveAt(destino.Count - 1);
    }
    private void Refiltrar()
    {
        if (!int.TryParse(DiasMinimos, out var minimo) || minimo < 0 || (!string.IsNullOrWhiteSpace(DiasMaximos) && (!int.TryParse(DiasMaximos, out var maximo) || maximo < minimo)))
        { Mensagem = "Confira o intervalo de dias sem retornar."; return; }
        var max = int.TryParse(DiasMaximos, out var fim) ? fim : int.MaxValue;
        if (!int.TryParse(TentativasMinimas, out var tentativas) || tentativas < 0 || ContatoDesde > ContatoAte)
        { Mensagem = "Confira a quantidade de tentativas e o período do último contato."; return; }
        var motivoNome = Motivos.FirstOrDefault(m => m.Id == MotivoFiltro)?.Nome;
        var lista = _todos.Where(x => x.Tipo == Tipo && Clinica.Desktop.Shell.Componentes.Busca.Casa(x.Paciente, Busca)
            && (string.IsNullOrEmpty(Modalidade) || x.Modalidade.ToString() == Modalidade)
            && (Situacao == "Todas" || Situacao == "Pendentes" && x.Pendente || x.Situacao == Situacao)
            && (Convenio == "Todos os convênios" || x.Convenio == Convenio)
            && (Profissional == "Todos os profissionais" || x.Profissional == Profissional)
            && (ResponsavelFiltro == 0 || x.ResponsavelId == ResponsavelFiltro)
            && (MotivoFiltro == 0 || x.Motivo == motivoNome)
            && x.Tentativas >= tentativas
            && (ContatoDesde == null || x.UltimoContato >= ContatoDesde.Value.Date)
            && (ContatoAte == null || x.UltimoContato < ContatoAte.Value.Date.AddDays(1))
            && (!SomenteMeus || x.ResponsavelId == SessaoUsuario.Atual.UsuarioId)
            && x.Dias >= minimo && x.Dias <= max
            && (Prazo switch { "Hoje" => x.Pendente && x.ProximoContato <= Hoje, "Atrasados" => x.Atrasado, "Próximos 7 dias" => x.Pendente && x.ProximoContato >= Hoje && x.ProximoContato <= Hoje.AddDays(7), _ => true })
            && (Condicao switch { "A assumir" => x.ResponsavelId == null, "Sem primeiro contato" => x.Tentativas == 0, "Com tentativas" => x.Tentativas > 0, "Pacote com saldo" => x.PacoteComSaldo, "Sem telefone" => string.IsNullOrWhiteSpace(x.Telefone), "Contato não autorizado" => !x.Consentimento, "Cancelou ou faltou" => x.Cancelou || x.Faltou, _ => true }));
        Pacientes.Clear(); foreach (var x in lista) Pacientes.Add(x);
        foreach (var nome in new[] { nameof(Resumo), nameof(HojeTexto), nameof(AtrasadosTexto), nameof(AAssumirTexto), nameof(SemContatoTexto), nameof(CanceladosTexto), nameof(FiltrosAtivos) }) OnPropertyChanged(nome);
        VisaoGestao.Clear();
        foreach (var g in _todos.Where(x => x.Tipo == Tipo).GroupBy(x => x.Responsavel))
            VisaoGestao.Add($"{g.Key}: {g.Count(x => x.Pendente)} pendentes · {g.Count(x => x.Atrasado)} atrasados · {g.Count(x => x.Situacao == "Agendado")} agendados · {g.Count(x => x.Situacao == "Sessão realizada")} realizados");
        foreach (var g in _todos.Where(x => x.Tipo == Tipo && x.Situacao == "Encerrado com justificativa").GroupBy(x => x.Motivo ?? "Sem motivo"))
            VisaoGestao.Add($"Encerramento — {g.Key}: {g.Count()}");
    }
    [RelayCommand] private void TrocarAba(string aba) { Aba = aba; LimparFiltros(); }
    [RelayCommand] private void LimparFiltros() { Busca = ""; Modalidade = ""; Situacao = "Pendentes"; Prazo = "Todos os prazos"; Condicao = "Todas as condições"; Convenio = "Todos os convênios"; Profissional = "Todos os profissionais"; ResponsavelFiltro = 0; MotivoFiltro = 0; TentativasMinimas = "0"; ContatoDesde = null; ContatoAte = null; SomenteMeus = false; DiasMinimos = "0"; DiasMaximos = ""; }
    [RelayCommand] private void Atalho(string atalho) { LimparFiltros(); if (atalho == "hoje") { SomenteMeus = true; Prazo = "Hoje"; } else if (atalho == "assumir") Condicao = "A assumir"; else if (atalho == "atrasados") Prazo = "Atrasados"; else if (atalho == "sem-contato") Condicao = "Sem primeiro contato"; else if (atalho == "cancelados") Condicao = "Cancelou ou faltou"; else if (atalho == "sem-resposta") Situacao = "Sem resposta"; }
    [RelayCommand] private async Task GerarAsync()
    {
        if (!int.TryParse(DiasRecall, out var dias) || dias is < 1 or > 3650) { Mensagem = "Informe de 1 a 3650 dias sem retornar."; return; }
        var quantidade = 0;
        await Executar(async svc => quantidade = await svc.GerarRecallAsync(SessaoUsuario.Atual.UsuarioId, dias,
            Enum.TryParse<ModalidadeAtendimento>(Modalidade, out var m) ? m : null));
        if (_falhou) return;
        _recallInicializado = true;
        var modalidade = Modalidade; LimparFiltros(); Modalidade = modalidade; DiasMinimos = dias.ToString();
        await VoltarAsync();
        if (!_falhou) Mensagem = $"{Pacientes.Count} paciente(s)/modalidade(s) sem retornar há {dias} dias ou mais. {quantidade} novo(s) acompanhamento(s).";
    }
    [RelayCommand] private async Task AbrirAsync(LinhaAcompanhamento? linha)
    {
        if (linha == null) return;
        await Executar(async svc =>
        {
            var historico = await svc.HistoricoAsync(SessaoUsuario.Atual.UsuarioId, linha.Id);
            Selecionado = linha; OnPropertyChanged(nameof(ResponsavelContato)); ProximoContato = (linha.ProximoContato < Hoje ? Hoje : linha.ProximoContato).ToDateTime(TimeOnly.MinValue);
            EtapaEdicao = linha.Etapa.ToString(); CanalEdicao = ""; Observacao = ""; Encerrar = false; Reabrir = false; MotivoEdicao = Motivos.FirstOrDefault(m => m.Nome == linha.Motivo)?.Id ?? 0;
            _idempotencia = Guid.NewGuid(); Historico.Clear(); foreach (var c in historico) Historico.Add(c);
        });
    }
    [RelayCommand] private async Task VoltarAsync() { Selecionado = null; Configurando = false; Filtrando = false; await RecarregarAsync(); }
    [RelayCommand] private void AbrirFiltros() => Filtrando = true;
    [RelayCommand] private async Task SalvarAsync()
    {
        if (Selecionado is not {} linha) return;
        await Executar(svc => svc.AtualizarAsync(SessaoUsuario.Atual.UsuarioId, linha.Id, new(_idempotencia, linha.Versao,
            SessaoUsuario.Atual.UsuarioId, ProximoContato is {} data ? DateOnly.FromDateTime(data) : null,
            Enum.Parse<EtapaAcompanhamento>(EtapaEdicao), Enum.TryParse<CanalContato>(CanalEdicao, out var canal) ? canal : null,
            Observacao, Encerrar, MotivoEdicao > 0 ? MotivoEdicao : null, Reabrir)));
        if (_falhou) return;
        await VoltarAsync(); Mensagem = "Acompanhamento salvo com histórico.";
    }
    [RelayCommand] private async Task WhatsAppAsync()
    {
        if (Selecionado is not {} linha) return;
        await Executar(async svc =>
        {
            await svc.ValidarContatoAsync(SessaoUsuario.Atual.UsuarioId, linha.Id);
            var erro = Whatsapp.Abrir(linha.Telefone, linha.Paciente, linha.Tipo == TipoAcompanhamento.Recall
                ? Whatsapp.Recall(linha.Paciente, linha.Dias) : $"Olá, {linha.Paciente}! Aqui é da clínica. Podemos conversar sobre o agendamento do seu atendimento?");
            if (erro != null) throw new InvalidOperationException(erro);
            CanalEdicao = nameof(CanalContato.WhatsApp); Mensagem = "WhatsApp aberto. Após o contato, registre o resultado e o próximo passo.";
        });
    }
    [RelayCommand] private async Task AssumirAsync()
    {
        if (Selecionado is not { Pendente: true } linha) return;
        LinhaAcompanhamento? atual = null;
        await Executar(async svc =>
        {
            await svc.AtualizarAsync(SessaoUsuario.Atual.UsuarioId, linha.Id, new(Guid.NewGuid(), linha.Versao,
                SessaoUsuario.Atual.UsuarioId, Hoje, linha.Etapa, null, "Responsável assumiu o acompanhamento para contato.",
                MotivoId: Motivos.FirstOrDefault(m => m.Nome == linha.Motivo)?.Id));
            atual = (await svc.ListarAsync(SessaoUsuario.Atual.UsuarioId)).Single(x => x.Id == linha.Id);
        });
        if (!_falhou && atual != null) await AbrirAsync(atual);
    }
    [RelayCommand] private void Agendar()
    {
        if (Selecionado is not {} linha) return;
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "agendar a sessão");
            using var scope = escopos.CreateScope();
            scope.ServiceProvider.GetRequiredService<PreenchimentoNovoAtendimento>().Definir(new(true,
                Hoje.ToDateTime(TimeOnly.MinValue), linha.ProfissionalId, null, linha.PacienteId, ModalidadeCodigo: linha.Modalidade.ToString()));
            if (!NavegacaoSuite.Ir(Modulo.ModuloRecepcao.ChaveMarcarHorario)) throw new InvalidOperationException("A agenda não está disponível para este acesso.");
            Selecionado = null;
        }
        catch (Exception ex) { Mensagem = ex.Message; }
    }
    [RelayCommand] private void Configurar() { if (Gestor) { ResponsavelPadrao = 0; Configurando = true; } }
    [RelayCommand] private async Task SalvarConfiguracaoAsync()
    {
        if (ProfissionalBsv is not > 0) { Mensagem = "Selecione o profissional das indicações de BSV antes de salvar."; return; }
        await Executar(svc => svc.ConfigurarAsync(SessaoUsuario.Atual.UsuarioId, ProfissionalBsv.Value, ResponsavelPadrao));
        if (!_falhou) { await VoltarAsync(); if (!_falhou) Mensagem = "Configuração salva. O profissional selecionado permanece vinculado às indicações de BSV."; }
    }
    [RelayCommand] private async Task AdicionarMotivoAsync()
    {
        await Executar(svc => svc.AdicionarMotivoAsync(SessaoUsuario.Atual.UsuarioId, NovoMotivo));
        if (!_falhou) { NovoMotivo = ""; Mensagem = "Motivo cadastrado. Ficará disponível ao voltar à lista."; }
    }
    private bool _falhou;
    private async Task Executar(Func<IAcompanhamentoPacienteService, Task> acao)
    {
        if (Carregando) return;
        Carregando = true; _falhou = false; Mensagem = "";
        try { using var scope = escopos.CreateScope(); await acao(scope.ServiceProvider.GetRequiredService<IAcompanhamentoPacienteService>()); }
        catch (Exception ex)
        {
            _falhou = true;
            Clinica.Application.Diagnostico.Registrar("Acompanhamento de pacientes — operação não concluída", ex);
            Mensagem = ex is InvalidOperationException or UnauthorizedAccessException ? ex.Message
                : "Não foi possível concluir a operação. Os dados anteriores foram preservados. Tente novamente; se persistir, informe o suporte. O detalhe foi registrado no diagnóstico.";
        }
        finally { Carregando = false; }
    }
}
