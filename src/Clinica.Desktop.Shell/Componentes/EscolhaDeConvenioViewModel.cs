using System.Collections.ObjectModel;
using System.Linq;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Uma linha da lista de convênios — o nome, e o que ele significa para a guia.</summary>
public sealed record LinhaConvenio(ConvenioCadastro Entrada, string Nome, string Detalhe, bool GeraGuia);

/// <summary>
/// "Qual é o convênio deste paciente?" — a pergunta feita NO MEIO do lançamento
/// (parcela 92).
///
/// Por que ela existe
/// ------------------
/// A importação do sistema anterior (set/2026) trouxe 2.021 das 2.238 fichas sem convênio,
/// todas no código <see cref="ConvenioCadastro.CodigoADefinir"/> — que não gera guia. Até
/// aqui a única defesa era o alerta vermelho da elegibilidade, e ele não impede nada: a
/// sessão era lançada, o faturamento não via guia nenhuma, e a diferença aparecia no fim
/// do mês. Desde esta parcela o lançamento é RECUSADO
/// (<see cref="ConvenioNaoDefinidoException"/>) — e recusar sem oferecer a saída seria
/// trocar uma guia perdida por um balcão travado.
///
/// A decisão da direção sobre a importação sempre foi esta: <b>"a escolha acontece com o
/// paciente na frente, que é onde ela é possível"</b>. Só que o único lugar que a permitia
/// era a ficha — outra tela, outro caminho, com o paciente esperando. Esta janela põe a
/// escolha onde a pergunta nasce.
///
/// O que ela pergunta, e o que NÃO pergunta
/// ----------------------------------------
/// O convênio é obrigatório; carteirinha e validade são OPCIONAIS e vêm junto porque é o
/// mesmo instante e a mesma pessoa (a carteirinha é o número que vai NA guia, e a validade
/// vencida é guia recusada na hora). Em branco, cada uma preserva o que a ficha já tinha —
/// esta janela responde uma pergunta, não regrava a ficha.
///
/// O resto da ficha continua na ficha. Cada campo a mais aqui é um a menos de atenção na
/// escolha, que é o que a recepcionista veio fazer.
/// </summary>
public sealed partial class EscolhaDeConvenioViewModel : ObservableObject
{
    private readonly ConvenioCatalogoService _catalogo;
    private readonly PacienteService _pacientes;
    private readonly int _pacienteId;
    private readonly string? _operador;

    public EscolhaDeConvenioViewModel(
        ConvenioCatalogoService catalogo, PacienteService pacientes, Paciente paciente,
        string? operador = null)
    {
        ArgumentNullException.ThrowIfNull(paciente);

        _catalogo = catalogo;
        _pacientes = pacientes;
        _pacienteId = paciente.Id;
        _operador = operador;

        PacienteNome = paciente.Nome;
        Carteirinha = paciente.Carteirinha ?? string.Empty;
        ValidadeCarteirinha = paciente.ValidadeCarteirinha?.ToDateTime(TimeOnly.MinValue);

        // Botão apagado sem dizer por quê é o defeito da parcela 41. Quem não tem o bit
        // precisa saber que a saída existe, e que ela é chamar quem tem.
        if (!PodeEditarFicha)
            Mensagem = "Seu acesso não permite editar a ficha do paciente. Peça a quem tem "
                       + "essa permissão para escolher o convênio — ou peça o bit em Acessos.";

        _ = CarregarAsync();
    }

    public string PacienteNome { get; }

    public ObservableCollection<LinhaConvenio> Convenios { get; } = [];

    [ObservableProperty] private LinhaConvenio? _selecionado;

    [ObservableProperty] private string _carteirinha = string.Empty;

    [ObservableProperty] private DateTime? _validadeCarteirinha;

    [ObservableProperty] private bool _carregando;

    [ObservableProperty] private bool _gravando;

    [ObservableProperty] private string _mensagem = string.Empty;

    /// <summary>
    /// O convênio VINCULADO — nulo enquanto a gravação não aconteceu. Quem chama testa
    /// isto, e não o <c>DialogResult</c>: o que interessa à tela de trás não é a janela
    /// ter fechado, é a ficha ter mudado.
    /// </summary>
    public ConvenioCadastro? Vinculado { get; private set; }

    /// <summary>Dispara quando o vínculo foi GRAVADO — a janela fecha.</summary>
    public event Action? Vinculou;

    /// <summary>
    /// Metade VISÍVEL da permissão: vincular convênio ESCREVE na ficha, e o bit disso é
    /// <see cref="Permissao.EditarPaciente"/> — o mesmo que já governa cadastrar paciente
    /// e registrar autorização. Os três perfis que lançam atendimento (Recepção, Faturista
    /// e Gerente) o têm, então ninguém perde o que fazia ontem.
    /// </summary>
    public bool PodeEditarFicha => SessaoUsuario.Atual.Pode(Permissao.EditarPaciente);

    public bool PodeVincular =>
        Selecionado is not null && !Carregando && !Gravando && PodeEditarFicha;

    partial void OnSelecionadoChanged(LinhaConvenio? value) => OnPropertyChanged(nameof(PodeVincular));

    partial void OnCarregandoChanged(bool value) => OnPropertyChanged(nameof(PodeVincular));

    partial void OnGravandoChanged(bool value) => OnPropertyChanged(nameof(PodeVincular));

    private async Task CarregarAsync()
    {
        Carregando = true;
        try
        {
            var lista = await _catalogo.ListarAsync();

            // Monta fora e só então publica: entre o Clear() e o último Add não pode
            // haver await (a carga é disparada do construtor).
            var linhas = lista
                // Inativo some das escolhas novas, como em todo cadastro — o histórico de
                // quem já está nele é preservado pelo próprio código gravado na ficha.
                .Where(c => c.Ativo)
                // "A definir" é a PERGUNTA. Oferecê-lo como resposta devolveria uma tela
                // de sucesso e um lançamento recusado logo em seguida.
                .Where(c => !string.Equals(c.Codigo, ConvenioCadastro.CodigoADefinir,
                                           StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Nome)
                .Select(c => new LinhaConvenio(
                    c,
                    c.Nome,
                    // O que a escolha significa para a guia — dito na linha, porque é a
                    // única diferença que a recepcionista precisa enxergar aqui. O
                    // particular é escolha legítima (parcela 60) e some do faturamento de
                    // propósito; ele não pode parecer um convênio comum na lista.
                    c.GeraGuia
                        ? "Gera guia para o faturamento."
                        : "Não gera guia — é como se cadastra quem paga do bolso (particular).",
                    c.GeraGuia))
                .ToList();

            Convenios.Clear();
            foreach (var l in linhas) Convenios.Add(l);

            // Nada vem pré-selecionado: o primeiro da lista é alfabético, e um padrão que
            // ninguém escolheu é justamente o defeito que esta janela existe para corrigir.
            if (Convenios.Count == 0)
                Mensagem = "Nenhum convênio ativo no catálogo. A direção cadastra em "
                           + "Configurações → Convênios, no aplicativo do Gerente.";
        }
        catch (Exception ex)
        {
            Mensagem = "Não foi possível carregar os convênios: " + ex.Message;
            LogSuite.Registrar("Escolha do convênio do paciente — catálogo não pôde ser lido", ex);
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private async Task Vincular()
    {
        // A SEGUNDA barreira: o `IsEnabled` explica, o `Exigir` impede. Atalho de teclado
        // e corrida de carregamento passam pela primeira sem o clique dela.
        SessaoUsuario.Atual.Exigir(Permissao.EditarPaciente, "vincular o convênio ao paciente");

        // Guarda que FALA: o botão já nasce apagado sem seleção, mas o Enter passa direto
        // — e voltar calada é botão que não faz nada (parcela 41).
        if (Selecionado is null)
        {
            Mensagem = "Escolha o convênio do paciente.";
            return;
        }

        Gravando = true;
        try
        {
            await _pacientes.DefinirConvenioAsync(
                _pacienteId,
                Selecionado.Entrada.Codigo,
                Carteirinha,
                ValidadeCarteirinha is { } v ? DateOnly.FromDateTime(v) : null,
                _operador);

            Vinculado = Selecionado.Entrada;
            Vinculou?.Invoke();
        }
        catch (Exception ex)
        {
            // Fica na janela: quem está com o paciente na frente corrige e tenta de novo,
            // e fechar aqui devolveria a tela de trás sem dizer que nada foi gravado.
            Mensagem = "Não foi possível vincular o convênio: " + ex.Message;
            LogSuite.Registrar("Escolha do convênio do paciente — vínculo não pôde ser gravado", ex);
        }
        finally
        {
            Gravando = false;
        }
    }
}
