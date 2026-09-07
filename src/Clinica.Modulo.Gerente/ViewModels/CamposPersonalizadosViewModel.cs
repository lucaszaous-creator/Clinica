using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Gerente.ViewModels;

/// <summary>Uma modalidade no combo — nulo = o campo vale para todas.</summary>
public sealed record OpcaoModalidadeCampo(string? Codigo, string Nome);

/// <summary>Uma linha da lista de campos cadastrados.</summary>
public sealed class LinhaCampoPersonalizado
{
    public required int Id { get; init; }
    public required string Rotulo { get; init; }
    public required string Tipo { get; init; }
    public required string Onde { get; init; }
    public required bool Ativo { get; init; }
    public string Situacao => Ativo ? "Ativo" : "Desativado";
}

/// <summary>
/// O cadastro dos CAMPOS PERSONALIZADOS do prontuário (set/2026) — ver
/// <see cref="CampoPersonalizadoProntuario"/>.
///
/// Mora no Gerente porque é decisão da DIREÇÃO: o que a clínica passa a registrar em toda
/// sessão é combinação da equipe, não escolha de quem está atendendo — dois profissionais
/// cadastrando campos parecidos dariam duas colunas que respondem à mesma pergunta e não
/// se comparam.
/// </summary>
public sealed partial class CamposPersonalizadosViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly IDialogoService _dialogo;

    public ObservableCollection<LinhaCampoPersonalizado> Campos { get; } = [];

    public IReadOnlyList<TipoCampoPersonalizado> Tipos { get; } =
        Enum.GetValues<TipoCampoPersonalizado>();

    public ObservableCollection<OpcaoModalidadeCampo> Modalidades { get; } = [];

    // ---- O formulário do campo em edição ----
    [ObservableProperty] private int _id;
    [ObservableProperty] private string? _rotulo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EhLista))]
    private TipoCampoPersonalizado _tipo = TipoCampoPersonalizado.Texto;

    [ObservableProperty] private string? _opcoes;
    [ObservableProperty] private string? _ajuda;
    [ObservableProperty] private OpcaoModalidadeCampo? _modalidade;
    [ObservableProperty] private bool _ativo = true;

    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;

    /// <summary>As opções só existem na lista — mostrá-las no resto é campo que não faz nada.</summary>
    public bool EhLista => Tipo == TipoCampoPersonalizado.Lista;

    /// <summary>Metade VISÍVEL da permissão; quem impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeConfigurar => SessaoUsuario.Atual.Pode(Permissao.GerenciarUsuarios);

    /// <summary>O teto dito na tela, em vez de descoberto no erro.</summary>
    public string LimiteTexto =>
        $"Até {CampoPersonalizadoService.MaximoAtivos} campos ativos — uma folha com campos "
        + "demais deixa de ser preenchida, e o que se perde não é o campo novo, são os do sistema.";

    public CamposPersonalizadosViewModel(IServiceScopeFactory escopos, IDialogoService dialogo)
    {
        _escopos = escopos;
        _dialogo = dialogo;
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            NaoVerificado = false;

            using var escopo = _escopos.CreateScope();
            var campos = await escopo.ServiceProvider
                .GetRequiredService<CampoPersonalizadoService>().TodosAsync();

            // Monta fora e publica de uma vez (a regra da parcela 62).
            var linhas = campos.Select(c => new LinhaCampoPersonalizado
            {
                Id = c.Id,
                Rotulo = c.Rotulo,
                Tipo = RotulosEnum.De(c.Tipo),
                Onde = string.IsNullOrWhiteSpace(c.ModalidadeCodigo)
                    ? "todas as modalidades"
                    : CatalogoModalidades.Nome(c.ModalidadeCodigo),
                Ativo = c.Ativo
            }).ToList();

            var modalidades = new List<OpcaoModalidadeCampo>
            {
                new(null, "(todas as modalidades)")
            };
            modalidades.AddRange(CatalogoModalidades.Ativas
                .Select(m => new OpcaoModalidadeCampo(m.Codigo, m.Nome)));

            Campos.Clear();
            foreach (var l in linhas) Campos.Add(l);

            Modalidades.Clear();
            foreach (var m in modalidades) Modalidades.Add(m);
            Modalidade ??= Modalidades[0];
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Gerente — campos personalizados não puderam ser lidos", ex);
            Campos.Clear();
            // Terceiro estado: lista vazia por falha não é "a clínica não cadastrou nenhum".
            NaoVerificado = true;
            Erro("Não foi possível ler os campos cadastrados.");
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>Traz o campo escolhido para o formulário — editar é objeto NOVO com o Id.</summary>
    [RelayCommand]
    private async Task EditarAsync(LinhaCampoPersonalizado? linha)
    {
        if (linha is null) return;

        try
        {
            using var escopo = _escopos.CreateScope();
            var campo = await escopo.ServiceProvider
                .GetRequiredService<CampoPersonalizadoService>().TodosAsync();
            if (campo.FirstOrDefault(c => c.Id == linha.Id) is not { } achado) return;

            Id = achado.Id;
            Rotulo = achado.Rotulo;
            Tipo = achado.Tipo;
            Opcoes = achado.Opcoes;
            Ajuda = achado.Ajuda;
            Ativo = achado.Ativo;
            Modalidade = Modalidades.FirstOrDefault(
                m => string.Equals(m.Codigo, achado.ModalidadeCodigo, StringComparison.OrdinalIgnoreCase))
                ?? Modalidades[0];
            Mensagem = string.Empty;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — campo não pôde ser aberto", ex);
            Erro(ex.Message);
        }
    }

    [RelayCommand]
    private void Novo()
    {
        Id = 0;
        Rotulo = null;
        Tipo = TipoCampoPersonalizado.Texto;
        Opcoes = null;
        Ajuda = null;
        Ativo = true;
        Modalidade = Modalidades.FirstOrDefault();
        Mensagem = string.Empty;
        MensagemEhErro = false;
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.GerenciarUsuarios, "cadastrar campo do prontuário");

            using var escopo = _escopos.CreateScope();
            await escopo.ServiceProvider.GetRequiredService<CampoPersonalizadoService>().SalvarAsync(
                new CampoPersonalizadoProntuario
                {
                    Id = Id,
                    Rotulo = Rotulo ?? string.Empty,
                    Tipo = Tipo,
                    Opcoes = Opcoes,
                    Ajuda = Ajuda,
                    ModalidadeCodigo = Modalidade?.Codigo,
                    Ativo = Ativo
                },
                SessaoUsuario.Atual.Operador);

            await CarregarAsync();
            // A carga zera a mensagem: escreve DEPOIS (a lição da parcela 68).
            Mensagem = Id == 0 ? "Campo cadastrado." : "Campo salvo.";
            MensagemEhErro = false;
            Novo();
            Mensagem = "Campo salvo. Ele já aparece na folha da sessão.";
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — campo não pôde ser salvo", ex);
            Erro(ex.Message);
        }
    }

    /// <summary>
    /// Liga/desliga o campo. Não há EXCLUIR — a definição é a procedência do valor, e
    /// apagá-la quebraria o vínculo pelo qual a clínica sabe que a coluna "Agulhas" de
    /// 2026 e a de 2027 são a MESMA pergunta.
    /// </summary>
    [RelayCommand]
    private async Task AlternarAsync(LinhaCampoPersonalizado? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.GerenciarUsuarios, "cadastrar campo do prontuário");

            if (linha.Ativo && !_dialogo.Confirmar(
                    "Desativar campo",
                    $"“{linha.Rotulo}” deixa de aparecer na folha da sessão.\n\n"
                    + "O que já foi registrado nele CONTINUA no prontuário — desativar não apaga nada."))
                return;

            using var escopo = _escopos.CreateScope();
            var servico = escopo.ServiceProvider.GetRequiredService<CampoPersonalizadoService>();
            var campo = (await servico.TodosAsync()).FirstOrDefault(c => c.Id == linha.Id);
            if (campo is null) return;

            campo.Ativo = !campo.Ativo;
            await servico.SalvarAsync(campo, SessaoUsuario.Atual.Operador);

            await CarregarAsync();
            Mensagem = campo.Ativo ? "Campo reativado." : "Campo desativado.";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Gerente — campo não pôde ser alternado", ex);
            Erro(ex.Message);
        }
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}
