using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>O paciente que o cadastro acabou de gravar: quem nasceu (ou foi editado) e o nome dele.</summary>
public sealed record PacienteCadastrado(int Id, string Nome);

/// <summary>
/// O retrato que a captura devolveu (JPEG): a foto cheia (até 640 px, tabela própria) e a
/// miniatura (até 160 px, na linha do paciente). Ainda NÃO gravado — quem grava é o Salvar do
/// cadastro, e cadastro abandonado não deixa retrato órfão no banco.
/// </summary>
public sealed record FotoDoPaciente(byte[] Cheia, byte[] Miniatura);

/// <summary>
/// Os formulários do PACIENTE que se abrem NO MEIO de outro fluxo e cuja resposta o fluxo
/// espera (out/2026 — eram as janelas WPF do shell <c>CadastroPacienteWindow</c> e
/// <c>EscolhaDeConvenioWindow</c>, pedidas por ponte; as duas saíram): o cadastro (novo ou edição) e a
/// escolha do convênio da ficha "a definir" (parcela 92).
///
/// Por que um serviço que ESPERA, e não o padrão "a tela é dona do aberto/fechado"
/// ---------------------------------------------------------------------------------
/// A escolha do convênio é perguntada no MEIO de um salvar — o formulário do horário, o
/// Lançar do balcão, o Finalizar do médico, o Concluir da lista do dia, a conciliação — e o
/// passo seguinte depende da resposta. Cada uma dessas portas guardando o próprio estado de
/// "aberto" e quebrando o fluxo em dois métodos seria a segunda definição da pergunta em
/// cinco lugares. É o desenho do <see cref="Dialogos"/>: quem pergunta só espera; quem
/// DESENHA é a <c>CamadaDoPaciente</c>, ao lado da camada de diálogos (na <c>Raiz</c> e no
/// <c>PalcoDeTelas</c>).
///
/// Um por navegador (Scoped). Sem camada montada (um teste que renderiza um formulário
/// solto) <see cref="Disponivel"/> é falso e as perguntas respondem "não abriu" na hora —
/// esperar uma resposta que ninguém vai desenhar travaria o fluxo para sempre.
/// </summary>
public sealed class FormulariosDoPaciente(IServiceScopeFactory escopos)
{
    private readonly Queue<FormularioDoPacienteAberto> _fila = new();
    private int _camadas;

    /// <summary>O que a tela diz quando não há quem desenhe o formulário (botão que não abre nada em silêncio é a parcela 41).</summary>
    public const string Indisponivel = "O formulário do paciente não pôde ser aberto nesta tela — feche-a e abra de novo.";

    /// <summary>Há quem desenhe os formulários (a camada está montada).</summary>
    public bool Disponivel => _camadas > 0;

    /// <summary>O formulário no ar, ou null.</summary>
    public FormularioDoPacienteAberto? Atual => _fila.Count > 0 ? _fila.Peek() : null;

    /// <summary>A camada se redesenha quando abre ou fecha um formulário.</summary>
    public event Action? Mudou;

    /// <summary>
    /// Garante que o paciente tenha convênio ESCOLHIDO — perguntando, quando não tem (era o
    /// <c>VinculoDeConvenio.GarantirAsync</c> do shell WPF, que saiu em out/2026). Lê a ficha ANTES de decidir: a
    /// lista carregada às 8h não decide sobre um convênio que a outra máquina resolveu às 9h.
    /// Verdadeiro = a ficha tem convênio (já tinha, ou escolhido e gravado agora); falso = a
    /// pessoa desistiu, ou não há onde perguntar.
    /// </summary>
    public async Task<bool> EscolherConvenioAsync(int pacienteId)
    {
        Paciente? paciente;
        using (var escopo = escopos.CreateScope())
            paciente = await escopo.ServiceProvider.GetRequiredService<PacienteService>().ObterAsync(pacienteId);

        // Ficha que sumiu não é problema DESTA conferência: quem for lançar em seguida dá o
        // erro certo ("Paciente não encontrado"), e inventar um aqui trocaria a mensagem
        // verdadeira por uma sobre convênio.
        if (paciente is null || !paciente.ConvenioADefinir) return true;
        if (!Disponivel) return false;

        var resposta = await Abrir(new FormularioDoPacienteAberto(TipoFormularioDoPaciente.Convenio, pacienteId, paciente));
        return resposta is not null;
    }

    /// <summary>
    /// O cadastro de um paciente NOVO. Nulo = desistiu (ou não há onde abrir). <paramref name="telefone"/>
    /// já preenche o campo (out/2026, WhatsApp nativo: "Cadastrar com este telefone" — quem escreveu pelo
    /// WhatsApp não precisa ditar o número que a conversa já tem); a recepção pode mudá-lo antes de gravar.
    /// </summary>
    public Task<PacienteCadastrado?> CadastrarAsync(string? telefone = null)
        => Disponivel
            ? Abrir(new FormularioDoPacienteAberto(TipoFormularioDoPaciente.Cadastro, null, null, telefone))
            : Task.FromResult<PacienteCadastrado?>(null);

    /// <summary>A edição do cadastro. Verdadeiro = gravou.</summary>
    public async Task<bool> EditarCadastroAsync(int pacienteId)
        => Disponivel
           && await Abrir(new FormularioDoPacienteAberto(TipoFormularioDoPaciente.Cadastro, pacienteId, null)) is not null;

    private Task<PacienteCadastrado?> Abrir(FormularioDoPacienteAberto f)
    {
        _fila.Enqueue(f);
        Mudou?.Invoke();
        return f.Resposta.Task;
    }

    /// <summary>Chamado pela camada: o formulário terminou (com o paciente gravado) ou a pessoa desistiu (null).</summary>
    public void Responder(FormularioDoPacienteAberto f, PacienteCadastrado? resultado)
    {
        if (_fila.Count == 0 || !ReferenceEquals(_fila.Peek(), f)) return;
        _fila.Dequeue();
        Mudou?.Invoke();
        f.Resposta.TrySetResult(resultado);
    }

    /// <summary>
    /// A pessoa trocou de tela pelo menu com um formulário aberto: todos desistem — a regra
    /// do <see cref="Dialogos.DesistirDeTodos"/>. O formulário da tela que saiu, respondido
    /// por cima da próxima, agiria sobre a tela que já não está à vista; desistir é seguro
    /// porque nada grava sem o clique de quem preenche.
    /// </summary>
    public void DesistirDeTodos()
    {
        if (_fila.Count == 0) return;
        var abertos = _fila.ToList();
        _fila.Clear();
        Mudou?.Invoke();
        foreach (var f in abertos) f.Resposta.TrySetResult(null);
    }

    /// <summary>A camada se anuncia ao montar e ao sair: é isso que faz <see cref="Disponivel"/> dizer a verdade.</summary>
    internal void CamadaMontada() => _camadas++;

    internal void CamadaDesmontada()
    {
        _camadas = Math.Max(0, _camadas - 1);
        if (_camadas == 0) DesistirDeTodos();
    }
}

public enum TipoFormularioDoPaciente { Cadastro, Convenio }

/// <summary>Um formulário pedido: qual, de quem (nulo = paciente novo) e a ficha já lida (o convênio).</summary>
public sealed class FormularioDoPacienteAberto(TipoFormularioDoPaciente tipo, int? pacienteId, Paciente? paciente, string? telefoneInicial = null)
{
    public TipoFormularioDoPaciente Tipo { get; } = tipo;
    public int? PacienteId { get; } = pacienteId;
    public Paciente? Paciente { get; } = paciente;
    /// <summary>O telefone que o cadastro NOVO já abre preenchido (nulo = campo vazio, como sempre).</summary>
    public string? TelefoneInicial { get; } = telefoneInicial;

    internal TaskCompletionSource<PacienteCadastrado?> Resposta { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
