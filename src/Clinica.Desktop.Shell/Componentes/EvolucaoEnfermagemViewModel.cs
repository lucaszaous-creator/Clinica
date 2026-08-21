using System.Collections.ObjectModel;
using System.Globalization;
using Clinica.Application;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>Uma linha da evolução de enfermagem, do jeito que ela se lê.</summary>
public sealed class LinhaEvolucaoEnfermagem
{
    public required int Id { get; init; }
    public required string Hora { get; init; }
    public required string Data { get; init; }
    public required string Texto { get; init; }
    public required string? SinaisVitais { get; init; }
    public required string Autor { get; init; }
    public required string Registro { get; init; }
    public required bool Intercorrencia { get; init; }
    public required bool Cancelada { get; init; }
    public required bool Substituida { get; init; }
    public required string? Marca { get; init; }
    public required string? Folha { get; init; }

    /// <summary>Vigente é o que se pode corrigir ou cancelar — o resto é histórico.</summary>
    public bool Vigente => !Cancelada && !Substituida;

    /// <summary>Destaque só no que está valendo: intercorrência já corrigida é histórico.</summary>
    public bool EmDestaque => Intercorrencia && Vigente;

    public static LinhaEvolucaoEnfermagem De(
        EvolucaoEnfermagem e, bool substituida, bool mostrarData)
    {
        var marca = e.Cancelada
            ? $"CANCELADA — {e.MotivoCancelamento}"
            : substituida
                ? "CORRIGIDA — vale o registro seguinte"
                : e.EhRetificacao
                    ? $"corrige o registro anterior — {e.MotivoRetificacao}"
                    : null;

        return new LinhaEvolucaoEnfermagem
        {
            Id = e.Id,
            Hora = e.Hora.ToString("HH\\:mm"),
            Data = mostrarData ? e.Data.ToString("dd/MM/yyyy") : string.Empty,
            Texto = e.Texto,
            SinaisVitais = e.SinaisVitaisResumidos,
            Autor = string.IsNullOrWhiteSpace(e.AutorConselho)
                ? e.AutorNome
                : $"{e.AutorNome} · {e.AutorConselho}",
            Registro = $"registrado {e.RegistradoEm:dd/MM/yyyy HH\\:mm}",
            Intercorrencia = e.Intercorrencia,
            Cancelada = e.Cancelada,
            Substituida = substituida,
            Marca = marca,
            Folha = e.Prescricao?.Numero
        };
    }
}

/// <summary>
/// A EVOLUÇÃO DE ENFERMAGEM (parcela 71) — a janela onde quem executa escreve o que
/// observou no paciente.
///
/// Por que é JANELA e não aba da folha de execução
/// -----------------------------------------------
/// Três razões, e a primeira sozinha decide:
/// 1. <b>A folha encerrada apaga a janela dela inteira</b> (<c>PodeMexer =&gt; PodeChecar
///    &amp;&amp; EmExecucao</c>). Um campo ali dentro nasceria morto exatamente no caso que
///    justifica a feature: a reação que aparece meia hora depois da última bomba.
/// 2. <b>A folha é modal.</b> Com a folha da cadeira 1 aberta, registrar uma reação na
///    cadeira 3 custaria fechar, achar, abrir, escrever — dez gestos com o paciente
///    reagindo. Ela não faz isso; ela chama a médica e escreve depois, que é o que
///    acontece hoje.
/// 3. <b>Dois campos de hora com significados diferentes na mesma tela</b>, num registro
///    cuja razão de existir é a hora ser a certa.
///
/// A janela abre da LINHA DA FILA da sala e também de dentro da folha, e nos dois casos
/// pergunta uma coisa só: <i>o que eu observei nesta passagem</i>.
/// </summary>
public partial class EvolucaoEnfermagemViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly IDialogoService _dialogo;
    private readonly int _pacienteId;
    private readonly int? _prescricaoId;
    private readonly int? _agendamentoId;

    /// <summary>
    /// Descarte de resposta fora de ordem (parcela 60): a janela recarrega a cada
    /// gravação, e a leitura velha não pode sobrescrever a nova.
    /// </summary>
    private int _geracaoCarga;

    /// <summary>Quando está corrigindo, o registro que vai ser substituído.</summary>
    private int? _retificando;

    public string Paciente { get; }

    /// <summary>Contexto numa LINHA de texto, nunca uma faixa: faixa permanente vira moldura.</summary>
    [ObservableProperty] private string _contexto = string.Empty;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public ObservableCollection<LinhaEvolucaoEnfermagem> Registros { get; } = new();

    // ---- O compositor ----

    [ObservableProperty] private string _hora = DateTime.Now.ToString("HH:mm");
    [ObservableProperty] private string _texto = string.Empty;
    [ObservableProperty] private bool _intercorrencia;

    [ObservableProperty] private string _sistolica = string.Empty;
    [ObservableProperty] private string _diastolica = string.Empty;
    [ObservableProperty] private string _cardiaca = string.Empty;
    [ObservableProperty] private string _respiratoria = string.Empty;
    [ObservableProperty] private string _temperatura = string.Empty;
    [ObservableProperty] private string _saturacao = string.Empty;
    [ObservableProperty] private string _dor = string.Empty;

    /// <summary>
    /// A reação observada vira ALERGIA na lista de problemas, no mesmo SaveChanges — e é
    /// aqui que o circuito se fecha para o caso que a checagem não cobre: o paciente que
    /// teve reação e mesmo assim completou a infusão.
    /// </summary>
    [ObservableProperty] private string _alergiaObservada = string.Empty;

    /// <summary>Está corrigindo um registro anterior — o rótulo do botão muda e o motivo é pedido.</summary>
    [ObservableProperty] private bool _corrigindo;

    [ObservableProperty] private string _rotuloDoBotao = "Registrar";

    /// <summary>Metade visível da permissão; a que impede é o <c>Exigir</c> no comando.</summary>
    public bool PodeRegistrar =>
        SessaoUsuario.Atual.Pode(Permissao.RegistrarEvolucaoEnfermagem);

    public EvolucaoEnfermagemViewModel(
        IServiceScopeFactory escopos, IDialogoService dialogo,
        int pacienteId, string paciente,
        int? prescricaoId = null, string? folha = null, int? agendamentoId = null)
    {
        _escopos = escopos;
        _dialogo = dialogo;
        _pacienteId = pacienteId;
        _prescricaoId = prescricaoId;
        _agendamentoId = agendamentoId;
        Paciente = paciente;

        Contexto = prescricaoId is null
            // ⚠️ A janela DIZ que não há folha, em vez de deixar a pessoa supor: é a
            // passagem avulsa (curativo, observação, triagem), e o registro é do paciente.
            ? "Registro do paciente — esta passagem não está ligada a uma folha de infusão."
            : $"Durante a folha de infusão {folha}.";

        _ = CarregarAsync();
    }

    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;
        Carregando = true;
        NaoVerificado = false;

        try
        {
            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<EvolucaoEnfermagemService>();

            // Dentro de uma folha, a lista é a DAQUELA sessão; solta, é a do paciente.
            var lista = _prescricaoId is { } id
                ? await servico.DaPrescricaoAsync(id)
                : await servico.DoPacienteAsync(_pacienteId, limite: 50);

            if (geracao != _geracaoCarga) return;

            var substituidas = lista
                .Where(e => e.RetificaEvolucaoId is not null)
                .Select(e => e.RetificaEvolucaoId!.Value)
                .ToHashSet();

            // ⚠️ Entre o Clear() e o último Add não pode haver await (parcela 62): monta-se
            // em lista local e só então publica.
            var linhas = lista
                .OrderByDescending(e => e.Data).ThenByDescending(e => e.Hora).ThenByDescending(e => e.Id)
                .Select(e => LinhaEvolucaoEnfermagem.De(
                    e, substituidas.Contains(e.Id), mostrarData: _prescricaoId is null))
                .ToList();

            Registros.Clear();
            foreach (var l in linhas) Registros.Add(l);
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;
            NaoVerificado = true;
            Diagnostico.Registrar("Enfermagem — evolução não pôde ser carregada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    [RelayCommand]
    private async Task RegistrarAsync()
    {
        try
        {
            // A segunda barreira: o IsEnabled explica, este impede (atalho de teclado passa
            // pelo primeiro).
            SessaoUsuario.Atual.Exigir(
                Permissao.RegistrarEvolucaoEnfermagem, "registrar evolução de enfermagem");

            if (!TimeOnly.TryParseExact(Hora.Trim(), "HH\\:mm", out var hora)
                && !TimeOnly.TryParse(Hora.Trim(), CultureInfo.GetCultureInfo("pt-BR"), out hora))
            {
                Mensagem = "Informe a hora do que foi observado, no formato 14:20.";
                MensagemEhErro = true;
                return;
            }

            var sinais = LerSinaisVitais();

            string? motivo = null;
            if (Corrigindo)
            {
                motivo = _dialogo.PerguntarTexto(
                    "Corrigir o registro",
                    "Por que o registro anterior estava errado? Ele não é apagado — fica na "
                    + "folha, marcado, com esta explicação ao lado.",
                    string.Empty);

                // Diálogo cancelado: sair calado é o certo (a exceção da checagem 21).
                if (string.IsNullOrWhiteSpace(motivo)) return;
            }

            using (var scope = _escopos.CreateScope())
            {
                var servico = scope.ServiceProvider.GetRequiredService<EvolucaoEnfermagemService>();
                var hoje = DateOnly.FromDateTime(DateTime.Today);

                if (Corrigindo && _retificando is { } alvo)
                    await servico.RetificarAsync(
                        alvo, hoje, hora, Texto, Executante(), motivo!, Intercorrencia, sinais);
                else
                    await servico.RegistrarAsync(
                        _pacienteId, hoje, hora, Texto, Executante(),
                        _prescricaoId, _agendamentoId, Intercorrencia, sinais,
                        string.IsNullOrWhiteSpace(AlergiaObservada) ? null : AlergiaObservada);
            }

            var alergia = !string.IsNullOrWhiteSpace(AlergiaObservada);
            LimparCompositor();
            await CarregarAsync();

            Mensagem = alergia
                ? "Registrado. A alergia entrou na lista de problemas do paciente e vai "
                  + "alertar na próxima prescrição."
                : "Registrado no prontuário do paciente.";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Enfermagem — evolução não pôde ser registrada", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Traz o registro para o compositor a fim de CORRIGI-LO. O anterior não é apagado:
    /// grava-se outro apontando para ele, com o motivo.
    /// </summary>
    [RelayCommand]
    private void Corrigir(LinhaEvolucaoEnfermagem? linha)
    {
        // Guarda sobre PARÂMETRO: nunca dispara vindo de botão de linha, e sair calado
        // aqui é o certo (a exceção declarada da checagem 21).
        if (linha is null) return;

        _retificando = linha.Id;
        Corrigindo = true;
        RotuloDoBotao = "Gravar correção";
        Hora = linha.Hora;
        Texto = linha.Texto;
        Intercorrencia = linha.Intercorrencia;
        Mensagem = $"Corrigindo o registro das {linha.Hora}. O original fica na folha, marcado.";
        MensagemEhErro = false;
    }

    [RelayCommand]
    private void CancelarCorrecao()
    {
        LimparCompositor();
        Mensagem = null;
    }

    /// <summary>
    /// Cancela o registro lançado no paciente ou na sessão ERRADA. A linha FICA, com o
    /// motivo — registro clínico não se apaga.
    /// </summary>
    [RelayCommand]
    private async Task CancelarRegistroAsync(LinhaEvolucaoEnfermagem? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(
                Permissao.RegistrarEvolucaoEnfermagem, "cancelar evolução de enfermagem");

            var motivo = _dialogo.PerguntarTexto(
                "Cancelar o registro",
                "Cancelar é para o registro lançado no paciente ou na sessão ERRADA — para "
                + "corrigir o texto, use Corrigir. Por que este registro está sendo "
                + "cancelado? Ele não é apagado: fica no prontuário, marcado.",
                string.Empty);

            if (string.IsNullOrWhiteSpace(motivo)) return;

            using (var scope = _escopos.CreateScope())
            {
                var servico = scope.ServiceProvider.GetRequiredService<EvolucaoEnfermagemService>();
                await servico.CancelarAsync(linha.Id, motivo, SessaoUsuario.Atual.Operador);
            }

            await CarregarAsync();
            Mensagem = "Registro cancelado. Ele continua no prontuário, com o motivo.";
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Enfermagem — registro não pôde ser cancelado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    // ---- Apoio ----

    private void LimparCompositor()
    {
        _retificando = null;
        Corrigindo = false;
        RotuloDoBotao = "Registrar";
        Texto = string.Empty;
        Intercorrencia = false;
        AlergiaObservada = string.Empty;
        Sistolica = Diastolica = Cardiaca = Respiratoria = string.Empty;
        Temperatura = Saturacao = Dor = string.Empty;
        Hora = DateTime.Now.ToString("HH:mm");
    }

    private SinaisVitais? LerSinaisVitais()
    {
        var sinais = new SinaisVitais(
            Inteiro(Sistolica), Inteiro(Diastolica), Inteiro(Cardiaca),
            Inteiro(Respiratoria), Decimal(Temperatura), Inteiro(Saturacao), Inteiro(Dor));

        return sinais == new SinaisVitais() ? null : sinais;
    }

    private static int? Inteiro(string texto)
        => int.TryParse(texto.Trim(), out var valor) ? valor : null;

    /// <summary>
    /// Aceita "36,4" e "36.4": a técnica digita com vírgula, e o teclado numérico do
    /// notebook manda ponto. Recusar um dos dois seria transformar o teclado da máquina
    /// num requisito de registro clínico.
    /// </summary>
    private static decimal? Decimal(string texto)
    {
        var limpo = texto.Trim().Replace('.', ',');
        return decimal.TryParse(limpo, NumberStyles.Number,
            CultureInfo.GetCultureInfo("pt-BR"), out var valor) ? valor : null;
    }

    /// <summary>
    /// Quem escreve é quem fez LOGIN, com o COREN do cadastro dele. ⚠️ Evolução de
    /// enfermagem sem registro no conselho não é evolução de enfermagem — o número é parte
    /// da assinatura profissional.
    /// </summary>
    private static IdentificacaoExecutante Executante() => new(
        UsuarioId: SessaoUsuario.Atual.Autenticado ? SessaoUsuario.Atual.UsuarioId : null,
        Nome: SessaoUsuario.Atual.Autenticado
            ? SessaoUsuario.Atual.Nome
            : SessaoUsuario.Atual.Operador,
        Conselho: SessaoUsuario.Atual.RegistroConselho);
}
