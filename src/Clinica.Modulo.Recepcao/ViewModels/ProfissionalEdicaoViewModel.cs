using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// Formulário do profissional. Só o nome é obrigatório — cadastro que exige demais na
/// primeira digitação é cadastro que a clínica adia, e sem profissional cadastrado a
/// agenda multiprofissional não existe.
/// </summary>
public sealed partial class ProfissionalEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int? _id;

    public ObservableCollection<EntradaEspecialidade> Especialidades { get; } = [];

    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private string? _nomeCurto;
    [ObservableProperty] private string? _registroConselho;

    /// <summary>
    /// CPF, para a assinatura digital. É ele que o sistema compara com o CPF de DENTRO do
    /// certificado ICP-Brasil na hora de assinar — sem ele, a assinatura é recusada, e sem
    /// este campo na tela não havia como preenchê-lo.
    /// </summary>
    [ObservableProperty] private string? _cpf;
    [ObservableProperty] private EntradaEspecialidade? _especialidade;
    [ObservableProperty] private string? _telefone;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private string? _cor;
    [ObservableProperty] private string _duracaoPadrao = string.Empty;

    // A jornada (set/2026): sete caixinhas e o par de horas, como texto "HH:mm". Nenhum
    // dia marcado = não declarado (qualquer dia) — a tela diz isso ao lado das caixas.
    [ObservableProperty] private bool _atendeSeg;
    [ObservableProperty] private bool _atendeTer;
    [ObservableProperty] private bool _atendeQua;
    [ObservableProperty] private bool _atendeQui;
    [ObservableProperty] private bool _atendeSex;
    [ObservableProperty] private bool _atendeSab;
    [ObservableProperty] private bool _atendeDom;
    [ObservableProperty] private string _atendeDas = string.Empty;
    [ObservableProperty] private string _atendeAte = string.Empty;
    [ObservableProperty] private string _ordem = "0";
    [ObservableProperty] private bool _ativo = true;
    [ObservableProperty] private string? _observacoes;

    [ObservableProperty] private string _titulo = "Novo profissional";
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    public event Action? Concluido;

    public ProfissionalEdicaoViewModel(IServiceScopeFactory escopos, int? id = null)
    {
        _escopos = escopos;
        _id = id;
        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        try
        {
            Especialidades.Clear();
            foreach (var e in CatalogoEspecialidades.Ativas) Especialidades.Add(e);

            if (_id is null) return;

            using var scope = _escopos.CreateScope();
            var equipe = scope.ServiceProvider.GetRequiredService<EquipeService>();
            var p = await equipe.ObterProfissionalAsync(_id.Value);
            if (p is null) return;

            Titulo = "Editar profissional";
            Nome = p.Nome;
            NomeCurto = p.NomeCurto;
            RegistroConselho = p.RegistroConselho;
            Cpf = p.Cpf;
            Especialidade = Especialidades.FirstOrDefault(e => e.Codigo == p.EspecialidadeCodigo);
            Telefone = p.Telefone;
            Email = p.Email;
            Cor = p.Cor;
            DuracaoPadrao = p.DuracaoPadraoMinutos?.ToString() ?? string.Empty;
            AtendeSeg = p.AtendeEm(DayOfWeek.Monday) && p.DiasDeAtendimento is not null;
            AtendeTer = p.AtendeEm(DayOfWeek.Tuesday) && p.DiasDeAtendimento is not null;
            AtendeQua = p.AtendeEm(DayOfWeek.Wednesday) && p.DiasDeAtendimento is not null;
            AtendeQui = p.AtendeEm(DayOfWeek.Thursday) && p.DiasDeAtendimento is not null;
            AtendeSex = p.AtendeEm(DayOfWeek.Friday) && p.DiasDeAtendimento is not null;
            AtendeSab = p.AtendeEm(DayOfWeek.Saturday) && p.DiasDeAtendimento is not null;
            AtendeDom = p.AtendeEm(DayOfWeek.Sunday) && p.DiasDeAtendimento is not null;
            AtendeDas = p.AtendeDas?.ToString("HH:mm") ?? string.Empty;
            AtendeAte = p.AtendeAte?.ToString("HH:mm") ?? string.Empty;
            Ordem = p.Ordem.ToString();
            Ativo = p.Ativo;
            Observacoes = p.Observacoes;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Recepção — profissional não pôde ser carregado", ex);
            Mensagem = $"Não foi possível carregar o profissional: {ex.Message}";
            MensagemEhErro = true;
        }
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = string.Empty;
        MensagemEhErro = false;

        if (string.IsNullOrWhiteSpace(Nome))
        {
            Erro("Informe o nome do profissional.");
            return;
        }

        int? duracao = null;
        if (!string.IsNullOrWhiteSpace(DuracaoPadrao))
        {
            if (!int.TryParse(DuracaoPadrao, out var minutos) || minutos <= 0)
            {
                Erro("A duração padrão precisa ser um número de minutos maior que zero.");
                return;
            }
            duracao = minutos;
        }

        if (!int.TryParse(Ordem, out var ordem)) ordem = 0;

        // O horário: os dois em branco é "não declarado"; um só é erro que a tela explica
        // antes de o serviço recusar. "HH:mm" — "8" e "8h" não são hora.
        TimeOnly? das = null, ate = null;
        if (!string.IsNullOrWhiteSpace(AtendeDas) || !string.IsNullOrWhiteSpace(AtendeAte))
        {
            if (!TimeOnly.TryParseExact(AtendeDas.Trim(), "HH:mm", out var d)
                || !TimeOnly.TryParseExact(AtendeAte.Trim(), "HH:mm", out var a))
            {
                Erro("Informe o horário de atendimento como início e fim, no formato 08:00 — ou deixe os dois em branco.");
                return;
            }
            if (a <= d)
            {
                Erro("O fim do horário de atendimento precisa ser depois do início.");
                return;
            }
            das = d;
            ate = a;
        }
        var dias = 0;
        if (AtendeSeg) dias |= Profissional.BitDe(DayOfWeek.Monday);
        if (AtendeTer) dias |= Profissional.BitDe(DayOfWeek.Tuesday);
        if (AtendeQua) dias |= Profissional.BitDe(DayOfWeek.Wednesday);
        if (AtendeQui) dias |= Profissional.BitDe(DayOfWeek.Thursday);
        if (AtendeSex) dias |= Profissional.BitDe(DayOfWeek.Friday);
        if (AtendeSab) dias |= Profissional.BitDe(DayOfWeek.Saturday);
        if (AtendeDom) dias |= Profissional.BitDe(DayOfWeek.Sunday);

        try
        {
            Salvando = true;
            using var scope = _escopos.CreateScope();
            var equipe = scope.ServiceProvider.GetRequiredService<EquipeService>();

            await equipe.SalvarProfissionalAsync(new Profissional
            {
                Id = _id ?? 0,
                Nome = Nome,
                NomeCurto = NomeCurto,
                RegistroConselho = RegistroConselho,
                Cpf = Cpf,
                EspecialidadeCodigo = Especialidade?.Codigo,
                Telefone = Telefone,
                Email = Email,
                Cor = Cor,
                DuracaoPadraoMinutos = duracao,
                DiasDeAtendimento = dias == 0 ? null : dias,
                AtendeDas = das,
                AtendeAte = ate,
                Ordem = ordem,
                Ativo = Ativo,
                Observacoes = Observacoes
            });

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — profissional não pôde ser salvo", ex);
            Erro(ex.Message);
        }
        finally
        {
            Salvando = false;
        }
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}
