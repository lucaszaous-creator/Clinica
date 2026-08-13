using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Uma linha da lista de modelos.</summary>
public sealed class LinhaModeloEvolucao
{
    public required int Id { get; init; }
    public required string Nome { get; init; }

    /// <summary>"Da clínica" ou "Meu" — quem lê a lista precisa saber de quem é o texto.</summary>
    public required string Dono { get; init; }

    public required bool DaClinica { get; init; }

    /// <summary>As primeiras palavras, para escolher sem abrir.</summary>
    public required string Previa { get; init; }

    public required string? QueixaPrincipal { get; init; }
    public required string? Conduta { get; init; }
    public required string? TextoEvolucao { get; init; }
    public required string? Orientacoes { get; init; }
}

/// <summary>
/// O que a janela devolve para a tela de evolução. Nulo = a pessoa fechou sem aplicar.
/// </summary>
public sealed record ModeloAplicado(
    string? QueixaPrincipal, string? Conduta, string? TextoEvolucao, string? Orientacoes);

/// <summary>
/// MODELOS DE EVOLUÇÃO (parcela 63) — o roteiro da sessão que se repete.
///
/// <c>ModeloDocumento</c> existe desde a parcela 3 e serve só aos papéis IMPRESSOS. A
/// evolução — que é o texto mais escrito do sistema, várias vezes por dia — não tinha
/// nada: a sessão de acupuntura tem sempre a mesma forma e era redigitada por inteiro.
///
/// Mora no SHELL porque a evolução é escrita em DOIS módulos (a Recepção, pela ficha do
/// paciente, e o Consultório, pela tela de atendimento) — o mesmo argumento que já pôs
/// aqui o mapa corporal e a emissão de documento na parcela 36. Duas janelas divergiriam
/// na primeira correção.
///
/// ⚠️ <b>Aplicar COPIA, nunca aponta.</b> É a regra do protocolo do mapa corporal e da
/// venda do pacote, e aqui ela é obrigação legal: referência viva faria corrigir uma
/// palavra do modelo hoje reescrever o prontuário da sessão da semana passada, que é
/// exatamente o que a Lei 13.787/2018 proíbe. Depois de aplicado, o texto é da SESSÃO.
///
/// É por isso — e só por isso — que o modelo <b>se apaga mesmo</b>, ao contrário de todo
/// o resto do prontuário: ele não registra o que aconteceu com ninguém, e as sessões
/// escritas com ele não mudam quando ele some.
/// </summary>
public sealed partial class ModelosEvolucaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int? _profissionalId;

    /// <summary>O que está escrito na sessão AGORA — a base do "salvar como modelo".</summary>
    private readonly ModeloAplicado _sessaoAtual;

    public ObservableCollection<LinhaModeloEvolucao> Modelos { get; } = [];

    [ObservableProperty] private LinhaModeloEvolucao? _selecionado;

    [ObservableProperty] private bool _carregando;

    /// <summary>
    /// A leitura FALHOU — o terceiro estado. Sem ele, "nenhum modelo cadastrado" e "não
    /// consegui ler os modelos" ficam idênticos, e a pessoa cria de novo um modelo que já
    /// existe.
    /// </summary>
    [ObservableProperty] private bool _naoVerificado;

    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Nome do modelo novo, no campo de salvar.</summary>
    [ObservableProperty] private string _nomeNovo = string.Empty;

    /// <summary>
    /// Salvar como modelo DA CLÍNICA em vez de pessoal.
    ///
    /// Nasce desmarcado: o atalho pessoal é o caso comum, e um modelo que aparece para os
    /// seis profissionais sem ninguém ter decidido isso é ruído na lista de todos.
    /// </summary>
    [ObservableProperty] private bool _paraAClinica;

    /// <summary>A sessão tem algum texto para virar modelo?</summary>
    public bool TemTextoParaSalvar =>
        !string.IsNullOrWhiteSpace(_sessaoAtual.QueixaPrincipal)
        || !string.IsNullOrWhiteSpace(_sessaoAtual.Conduta)
        || !string.IsNullOrWhiteSpace(_sessaoAtual.TextoEvolucao)
        || !string.IsNullOrWhiteSpace(_sessaoAtual.Orientacoes);

    /// <summary>
    /// Escrever no prontuário é o que este texto vai virar — então é o bit que manda.
    /// <c>VerProntuario</c> não basta: quem só lê não precisa de roteiro para escrever.
    /// </summary>
    public bool PodeMexer => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    /// <summary>O que a janela devolve. Nulo enquanto ninguém aplicou nada.</summary>
    public ModeloAplicado? Resultado { get; private set; }

    /// <summary>Dispara quando a pessoa aplica — a janela fecha.</summary>
    public event Action? Aplicou;

    public ModelosEvolucaoViewModel(
        IServiceScopeFactory escopos, int? profissionalId, ModeloAplicado sessaoAtual)
    {
        _escopos = escopos;
        _profissionalId = profissionalId;
        _sessaoAtual = sessaoAtual;

        _ = CarregarAsync();
    }

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 50): salvar e apagar recarregam, e duas
    /// cargas no ar se intercalariam dentro da mesma coleção.
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

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();

            var lista = await prontuario.ModelosAsync(_profissionalId);
            if (geracao != _geracaoCarga) return;

            // Monta em lista local e só ENTÃO publica: entre o Clear e o último Add não
            // pode haver await (a lição da parcela 62).
            var linhas = lista.Select(Montar).ToList();

            var escolhido = Selecionado?.Id;

            Modelos.Clear();
            foreach (var l in linhas) Modelos.Add(l);

            Selecionado = Modelos.FirstOrDefault(m => m.Id == escolhido) ?? Modelos.FirstOrDefault();
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;

            NaoVerificado = true;
            Clinica.Application.Diagnostico.Registrar(
                "Prontuário — modelos de evolução não puderam ser lidos", ex);
            Erro($"Não foi possível ler os modelos: {ex.Message}");
        }
        finally
        {
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    private static LinhaModeloEvolucao Montar(ModeloEvolucao m) => new()
    {
        Id = m.Id,
        Nome = m.Nome,
        DaClinica = m.DaClinica,
        Dono = m.DaClinica ? "Da clínica" : "Meu",
        Previa = Previa(m),
        QueixaPrincipal = m.QueixaPrincipal,
        Conduta = m.Conduta,
        TextoEvolucao = m.TextoEvolucao,
        Orientacoes = m.Orientacoes
    };

    /// <summary>
    /// As primeiras palavras do modelo, para escolher sem abrir. Vem da CONDUTA quando
    /// existe: é o campo que diferencia dois modelos da mesma clínica ("agulhamento
    /// lombar" × "auriculoterapia"), enquanto a queixa costuma ser parecida.
    /// </summary>
    private static string Previa(ModeloEvolucao m)
    {
        var texto = new[] { m.Conduta, m.TextoEvolucao, m.QueixaPrincipal, m.Orientacoes }
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t))?.Trim() ?? string.Empty;

        return texto.Length <= 120 ? texto : texto[..120] + "…";
    }

    // ==================== Aplicar ====================

    /// <summary>
    /// Copia o texto do modelo para a sessão.
    ///
    /// ⚠️ Não grava NADA — é a mesma regra de "repetir a sessão anterior" e "aplicar
    /// protocolo" do mapa corporal (parcela 3): o texto vai para a tela, e só o Salvar da
    /// sessão o efetiva. Quem abriu o modelo por engano fecha a evolução sem salvar e
    /// nada aconteceu.
    /// </summary>
    [RelayCommand]
    private void Aplicar()
    {
        if (Selecionado is not { } m) return;

        SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "aplicar modelo de evolução");

        Resultado = new ModeloAplicado(
            m.QueixaPrincipal, m.Conduta, m.TextoEvolucao, m.Orientacoes);

        Aplicou?.Invoke();
    }

    // ==================== Salvar o que está escrito ====================

    [RelayCommand]
    private async Task SalvarComoModeloAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "salvar modelo de evolução");

            // Guarda com VOZ (parcela 41): sair calado faria o botão parecer quebrado, e a
            // pessoa não tem como adivinhar que faltava escrever a sessão primeiro.
            if (!TemTextoParaSalvar)
            {
                Erro("A sessão está em branco — escreva a evolução primeiro e depois "
                     + "guarde-a como modelo.");
                return;
            }

            if (string.IsNullOrWhiteSpace(NomeNovo))
            {
                Erro("Dê um nome ao modelo (\"Sessão de acupuntura — lombar\", por exemplo).");
                return;
            }

            // Modelo da clínica é decisão da equipe, não atalho pessoal: ele passa a
            // aparecer para os seis profissionais.
            var dono = ParaAClinica ? (int?)null : _profissionalId;

            if (ParaAClinica && !SessaoUsuario.Atual.Pode(Permissao.EditarProntuario))
            {
                Erro("Você não pode criar modelo da clínica.");
                return;
            }

            using var scope = _escopos.CreateScope();
            var prontuario = scope.ServiceProvider.GetRequiredService<ProntuarioService>();

            await prontuario.SalvarModeloAsync(new ModeloEvolucao
            {
                Nome = NomeNovo.Trim(),
                ProfissionalId = dono,
                QueixaPrincipal = _sessaoAtual.QueixaPrincipal,
                Conduta = _sessaoAtual.Conduta,
                TextoEvolucao = _sessaoAtual.TextoEvolucao,
                Orientacoes = _sessaoAtual.Orientacoes
            }, SessaoUsuario.Atual.Operador);

            NomeNovo = string.Empty;
            Informar("Modelo guardado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Prontuário — modelo de evolução não pôde ser salvo", ex);
            Erro(ex.Message);
        }
    }

    // ==================== Apagar ====================

    /// <summary>
    /// Apaga o modelo — e aqui apagar é APAGAR mesmo.
    ///
    /// É a única exceção do prontuário à regra de que registro clínico não se apaga, e ela
    /// se sustenta em duas coisas: o modelo não registra o que aconteceu com nenhum
    /// paciente, e aplicar COPIA — então nenhuma sessão escrita com ele muda. É a mesma
    /// decisão da parcela 25 para o protocolo do mapa e o modelo de documento.
    /// </summary>
    [RelayCommand]
    private async Task ApagarAsync()
    {
        if (Selecionado is not { } m) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "apagar modelo de evolução");

            using var scope = _escopos.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ProntuarioService>()
                .RemoverModeloAsync(m.Id);

            Informar($"Modelo \"{m.Nome}\" apagado. As sessões escritas com ele não mudam.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Prontuário — modelo de evolução não pôde ser apagado", ex);
            Erro(ex.Message);
        }
    }

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }

    private void Informar(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = false;
    }
}
