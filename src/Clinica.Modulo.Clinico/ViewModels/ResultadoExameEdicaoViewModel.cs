using System.Collections.ObjectModel;
using System.IO;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>Uma opção do combo "responde ao pedido": nulo = resultado avulso.</summary>
public sealed record OpcaoPedidoExame(int? DocumentoId, string Rotulo)
{
    public override string ToString() => Rotulo;
}

/// <summary>
/// O registro de UM resultado de exame (ago/2026) — em diálogo, pelo molde da colheita de
/// medida: registrar é ato pontual, e ato pontual não merece painel permanente.
///
/// O VALOR é texto livre por desenho (a entidade explica: "não reagente", "&lt; 0,01" são
/// resultados reais), então aqui não há conversão numérica nenhuma — quem valida
/// completude e plausibilidade de DATA é o serviço, e a recusa volta inline.
///
/// O combo "responde ao pedido" (set/2026) é o que faz a tela de Exames dizer
/// "Resultado disponível" por FATO. Ele mora AQUI, na janela única de registro, para as
/// duas portas (a tela de Exames e a seção Exames e anexos) amarrarem pela MESMA regra —
/// vínculo que só existe numa porta é o defeito recorrente do projeto. E a falha ao
/// CARREGAR os pedidos não impede registrar: banco lento não pode segurar um laudo na
/// mão da técnica — vira aviso escrito, nunca silêncio.
/// </summary>
public sealed partial class ResultadoExameEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _pacienteId;
    private readonly int? _pedidoPreSelecionado;

    /// <summary>Gravou — é por isso que a janela fecha.</summary>
    public bool Registrado { get; private set; }

    [ObservableProperty] private string _paciente = string.Empty;

    [ObservableProperty] private DateTime _dataNova = DateTime.Today;
    [ObservableProperty] private string? _nomeNovo;
    [ObservableProperty] private string? _valorNovo;
    [ObservableProperty] private string? _unidadeNova;
    [ObservableProperty] private string? _referenciaNova;
    [ObservableProperty] private string? _laboratorioNovo;
    [ObservableProperty] private string? _observacoesNovas;

    public ObservableCollection<OpcaoPedidoExame> PedidosDoPaciente { get; } = [];
    [ObservableProperty] private OpcaoPedidoExame? _pedidoEscolhido;

    /// <summary>Terceiro estado do combo: "não deu para conferir" ≠ "não há pedido".</summary>
    [ObservableProperty] private string? _avisoPedidos;

    // ===== O LAUDO EM ARQUIVO =====
    // O PDF que chega do laboratório é o que o profissional quer ver; o valor
    // estruturado acima é o que se compara depois. Os dois são opcionais entre si —
    // o serviço recusa só o registro SEM conteúdo nenhum.
    private byte[]? _laudo;
    private string? _laudoNome;
    private string? _laudoTipo;

    /// <summary>"laudo.pdf · 1,2 MB" — nulo quando nenhum arquivo foi escolhido.</summary>
    [ObservableProperty] private string? _laudoEscolhido;

    [ObservableProperty] private bool _salvando;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    public ResultadoExameEdicaoViewModel(
        IServiceScopeFactory escopos, int pacienteId, string paciente,
        int? pedidoDocumentoId = null)
    {
        _escopos = escopos;
        _pacienteId = pacienteId;
        _pedidoPreSelecionado = pedidoDocumentoId;
        Paciente = paciente;

        _ = CarregarPedidosAsync();
    }

    private async Task CarregarPedidosAsync()
    {
        try
        {
            using var scope = _escopos.CreateScope();
            var repo = scope.ServiceProvider
                .GetRequiredService<Clinica.Application.Abstracoes.IClinicaRepositorio>();
            var pedidos = await repo.PedidosDeExameAsync(pacienteId: _pacienteId);

            PedidosDoPaciente.Clear();
            PedidosDoPaciente.Add(new OpcaoPedidoExame(null, "— resultado avulso (sem pedido) —"));
            foreach (var p in pedidos.Where(p => !p.Cancelado))
            {
                // O que já tem resultado continua na lista, DITO: um pedido de vários
                // exames recebe vários laudos, e escondê-lo impediria o segundo.
                var rotulo = p.ResultadosVinculados > 0
                    ? $"{p.RotuloDoCombo} (já tem {p.ResultadosVinculados})"
                    : p.RotuloDoCombo;
                PedidosDoPaciente.Add(new OpcaoPedidoExame(p.DocumentoId, rotulo));
            }

            PedidoEscolhido = _pedidoPreSelecionado is { } alvo
                ? PedidosDoPaciente.FirstOrDefault(o => o.DocumentoId == alvo)
                    ?? PedidosDoPaciente[0]
                : PedidosDoPaciente[0];
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — pedidos do paciente não puderam ser listados", ex);
            AvisoPedidos = "Não deu para listar os pedidos deste paciente — dá para "
                + "registrar mesmo assim, e o vínculo pode ser feito depois.";
        }
    }

    /// <summary>Escolhe o arquivo do laudo. Lê os bytes AGORA — o arquivo pode sair da
    /// pasta antes de a pessoa clicar em Registrar.</summary>
    [RelayCommand]
    private async Task EscolherLaudoAsync()
    {
        try
        {
            var dialogo = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Anexar o laudo do exame",
                Filter = "Laudos (*.pdf;*.jpg;*.jpeg;*.png)|*.pdf;*.jpg;*.jpeg;*.png"
                         + "|Todos os arquivos (*.*)|*.*"
            };
            if (dialogo.ShowDialog() != true) return;

            var bytes = await File.ReadAllBytesAsync(dialogo.FileName);
            var extensao = Path.GetExtension(dialogo.FileName).ToLowerInvariant();

            _laudo = bytes;
            _laudoNome = Path.GetFileName(dialogo.FileName);
            _laudoTipo = extensao switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => null
            };
            LaudoEscolhido = $"{_laudoNome} · {Peso(bytes.Length)}";
            Mensagem = null;
            MensagemEhErro = false;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — o arquivo do laudo não pôde ser lido", ex);
            Mensagem = "Não deu para ler o arquivo escolhido.";
            MensagemEhErro = true;
        }
    }

    /// <summary>Tira o arquivo escolhido — antes de gravar, escolher errado é só um clique.</summary>
    [RelayCommand]
    private void RemoverLaudo()
    {
        _laudo = null;
        _laudoNome = null;
        _laudoTipo = null;
        LaudoEscolhido = null;
    }

    private static string Peso(int bytes) => bytes >= 1024 * 1024
        ? $"{bytes / 1024d / 1024d:0.#} MB"
        : $"{Math.Max(1, bytes / 1024)} KB";

    [RelayCommand]
    private async Task RegistrarAsync()
    {
        try
        {
            Salvando = true;
            Mensagem = null;
            MensagemEhErro = false;

            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            using var scope = _escopos.CreateScope();
            var servico = scope.ServiceProvider.GetRequiredService<ResultadoExameService>();

            await servico.RegistrarAsync(new ResultadoExame
            {
                PacienteId = _pacienteId,
                PedidoDocumentoId = PedidoEscolhido?.DocumentoId,
                Data = DateOnly.FromDateTime(DataNova),
                Nome = NomeNovo ?? string.Empty,
                Valor = ValorNovo ?? string.Empty,
                Unidade = UnidadeNova,
                Referencia = ReferenciaNova,
                Laboratorio = LaboratorioNovo,
                Observacoes = ObservacoesNovas
            }, SessaoUsuario.Atual.Operador,
               laudo: _laudo, nomeDoArquivo: _laudoNome, tipoDoArquivo: _laudoTipo);

            Registrado = true;
        }
        catch (Exception ex)
        {
            Registrado = false;
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — resultado de exame não pôde ser registrado", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            // `Salvando` volta a false por ÚLTIMO: é a mudança que a janela observa para
            // fechar, e ela precisa encontrar `Registrado` já definido.
            Salvando = false;
        }
    }
}
