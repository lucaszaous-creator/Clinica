using Clinica.Domain.Entities;
using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>Um paciente na carteira do profissional, já com a leitura da dor pronta.</summary>
public sealed class LinhaPacienteClinico
{
    public required int PacienteId { get; init; }
    public required string Nome { get; init; }
    public required string Sessoes { get; init; }
    public required string UltimaSessao { get; init; }
    public required string Dor { get; init; }
    public required string Ganho { get; init; }

    /// <summary>Sem vir há muito tempo — o destaque que faz a clínica ligar.</summary>
    public required bool Sumido { get; init; }

    /// <summary>Nunca teve o par EVA completo: não há o que dizer sobre a dor dele.</summary>
    public required bool SemMedida { get; init; }

    /// <summary>Dias sem vir a partir dos quais a linha ganha destaque.</summary>
    public const int DiasParaDestaque = 45;

    public static LinhaPacienteClinico De(PacienteDoProfissional p, DateOnly hoje)
    {
        var dias = p.DiasSemVir(hoje);

        return new LinhaPacienteClinico
        {
            PacienteId = p.PacienteId,
            Nome = p.Nome,
            Sessoes = p.Sessoes == 0 ? "—" : $"{p.Sessoes} sessão(ões)",
            UltimaSessao = p.UltimaSessao is { } d
                ? $"{d:dd/MM/yyyy}" + (dias is { } n and > 0 ? $" · há {n} dias" : " · hoje")
                : "sem sessão registrada",
            Dor = p.UltimaDor is { } atual ? $"{atual}/10" : "—",
            Ganho = p.GanhoAcumulado is { } g
                ? (g >= 0 ? $"−{g} desde o início" : $"+{-g} desde o início")
                : "—",
            Sumido = dias is { } sem && sem > DiasParaDestaque,
            SemMedida = p.UltimaDor is null
        };
    }
}

/// <summary>
/// A carteira do profissional: quem ele atende, quando veio pela última vez e como a dor
/// está.
///
/// É diferente da lista de pacientes da recepção, e a diferença não é cosmética. Lá a
/// lista é de CADASTRO — todo mundo, ordenado por nome, com telefone e convênio, para
/// achar quem ligou. Aqui é de TRATAMENTO — só quem este profissional atendeu, ordenado
/// por quem veio por último, com a leitura da dor ao lado. Uma responde "quem é essa
/// pessoa?", a outra "como está indo o tratamento dela?".
/// </summary>
public sealed partial class MeusPacientesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly PacienteEmFoco _foco;

    /// <summary>Todos os pacientes lidos; a lista visível é o filtro sobre eles.</summary>
    private readonly List<LinhaPacienteClinico> _todos = [];

    public ObservableCollection<LinhaPacienteClinico> Pacientes { get; } = [];

    [ObservableProperty] private string _termo = string.Empty;

    [ObservableProperty] private string _resumo = string.Empty;

    /// <summary>Mostrar só quem está sem vir há mais tempo que o destaque.</summary>
    [ObservableProperty] private bool _somenteSumidos;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Sem profissional vinculado ao login: a carteira é a da clínica.</summary>
    [ObservableProperty] private bool _semVinculo;

    public MeusPacientesViewModel(IServiceScopeFactory escopos, PacienteEmFoco foco)
    {
        _escopos = escopos;
        _foco = foco;
        _ = CarregarAsync();
    }

    partial void OnTermoChanged(string value) => Filtrar();

    partial void OnSomenteSumidosChanged(bool value) => Filtrar();

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 60): dois cliques no Atualizar largam
    /// duas leituras no ar, e num banco remoto a VELHA pode responder por último.
    /// </summary>
    private int _geracaoCarga;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;

        try
        {
            Carregando = true;
            NaoVerificado = false;
            Mensagem = null;
            MensagemEhErro = false;

            var profissionalId = SessaoUsuario.Atual.ProfissionalId;
            SemVinculo = profissionalId is null;

            var hoje = DateOnly.FromDateTime(DateTime.Today);

            using var scope = _escopos.CreateScope();
            var consultorio = scope.ServiceProvider.GetRequiredService<ConsultorioService>();

            var pacientes = await consultorio.MeusPacientesAsync(profissionalId);
            if (geracao != _geracaoCarga) return;

            // Clear e Adds JUNTOS, depois do await: entre um e outro a carteira ficaria
            // vazia na tela, e duas cargas intercaladas a deixariam duplicada (parcela 62).
            _todos.Clear();
            foreach (var p in pacientes) _todos.Add(LinhaPacienteClinico.De(p, hoje));

            Filtrar();
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar("Consultório — carteira de pacientes não pôde ser lida", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            // Só a carga VIGENTE apaga o "Carregando": a superada desligaria o indicador
            // enquanto a nova ainda está no ar.
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    /// <summary>
    /// O filtro roda em memória sobre o que já veio: a carteira tem teto e cabe na tela,
    /// e ir ao banco a cada tecla daria uma consulta por letra digitada.
    ///
    /// A lista filtrada DIZ que está filtrada, como no resto do projeto — "8 pacientes"
    /// sozinho faria o profissional concluir que atende oito pessoas.
    /// </summary>
    private void Filtrar()
    {
        Pacientes.Clear();

        var termo = Termo.Trim();
        foreach (var p in _todos)
        {
            if (SomenteSumidos && !p.Sumido) continue;
            if (termo.Length > 0 && !p.Nome.Contains(termo, StringComparison.OrdinalIgnoreCase)) continue;
            Pacientes.Add(p);
        }

        var filtrado = termo.Length > 0 || SomenteSumidos;
        Resumo = filtrado
            ? $"{Pacientes.Count} de {_todos.Count} paciente(s) na sua carteira."
            : $"{_todos.Count} paciente(s) na sua carteira, do que veio por último ao mais antigo.";
    }

    /// <summary>Leva ao atendimento com este paciente em foco (sem horário de origem).</summary>
    [RelayCommand]
    private void Atender(LinhaPacienteClinico? linha)
    {
        if (linha is null) return;

        _foco.Definir(linha.PacienteId, linha.Nome);
        NavegacaoSuite.Ir(ModuloClinico.ChaveAtendimento);
    }

    [RelayCommand]
    private void VerDor(LinhaPacienteClinico? linha)
    {
        if (linha is null) return;

        _foco.Definir(linha.PacienteId, linha.Nome);
        NavegacaoSuite.Ir(ModuloClinico.ChaveEvolucaoDor);
    }

    [RelayCommand]
    private void VerAvaliacoes(LinhaPacienteClinico? linha)
    {
        if (linha is null) return;

        _foco.Definir(linha.PacienteId, linha.Nome);
        NavegacaoSuite.Ir(ModuloClinico.ChaveAvaliacoes);
    }
}
