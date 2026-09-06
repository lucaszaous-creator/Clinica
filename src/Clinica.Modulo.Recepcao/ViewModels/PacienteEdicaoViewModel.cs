using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Controls;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.ViewModels;

/// <summary>
/// Cadastro do paciente na Recepção — o "360º" da proposta.
///
/// É aqui que a foto é tirada: a webcam fica no balcão, não na mesa do faturamento.
/// A gravação da imagem acontece no SALVAR, nunca na captura — quem desiste do
/// cadastro não deixa retrato órfão no banco.
/// </summary>
public sealed partial class PacienteEdicaoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int? _id;

    /// <summary>Foto capturada nesta sessão, ainda não gravada.</summary>
    private byte[]? _fotoCheiaPendente;
    private byte[]? _fotoMiniaturaPendente;
    private bool _removerFoto;

    public ObservableCollection<EntradaConvenio> Convenios { get; } = [];
    public ObservableCollection<EntradaModalidade> Modalidades { get; } = [];

    public IReadOnlyList<Sexo> Sexos { get; } = [Sexo.Feminino, Sexo.Masculino];

    /// <summary>Origens oferecidas, com o vazio na frente ("nao perguntado").</summary>
    public IReadOnlyList<OrigemPaciente?> Origens { get; } =
        [null, .. Enum.GetValues<OrigemPaciente>().Cast<OrigemPaciente?>()];

    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private string? _documento;
    [ObservableProperty] private string? _telefone;

    /// <summary>
    /// E-mail do paciente (set/2026). É por ele que sai o lembrete AUTOMÁTICO da sessão,
    /// quando a clínica liga o servidor de saída em Configurações. Opcional: sem ele o
    /// paciente continua sendo avisado pelo WhatsApp de um clique, como sempre.
    /// </summary>
    [ObservableProperty] private string? _email;

    /// <summary>
    /// Endereço residencial. Exigido pelo art. 35 da Lei 5.991/1973 para a receita poder
    /// ser AVIADA — sem ele a farmácia pode recusar, e quem descobre é o paciente na fila.
    /// </summary>
    [ObservableProperty] private string? _endereco;
    [ObservableProperty] private DateTime? _dataNascimento;
    [ObservableProperty] private Sexo _sexoSelecionado = Sexo.Feminino;
    [ObservableProperty] private EntradaConvenio? _convenio;
    [ObservableProperty] private string? _carteirinha;
    [ObservableProperty] private DateTime? _validadeCarteirinha;
    [ObservableProperty] private EntradaModalidade? _modalidadePreferida;
    [ObservableProperty] private bool _possuiApp;
    [ObservableProperty] private string? _observacoes;

    // ===== CRM: de onde veio =====

    /// <summary>
    /// Null = ninguem perguntou. E por isso a lista comeca com a opcao vazia em vez de
    /// ja vir preenchida: origem chutada e pior que origem em branco — a direcao decide
    /// onde investir em cima desse numero.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EhIndicacao))]
    private OrigemPaciente? _origem;

    [ObservableProperty] private string? _indicadoPor;

    /// <summary>O campo "quem indicou" so vale para a origem Indicacao.</summary>
    public bool EhIndicacao => Origem == OrigemPaciente.Indicacao;

    [ObservableProperty] private string _titulo = "Novo paciente";
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private bool _mensagemEhErro;
    [ObservableProperty] private bool _salvando;

    /// <summary>Miniatura exibida no formulário (a pendente, se houve captura).</summary>
    [ObservableProperty] private byte[]? _miniatura;

    public bool TemFoto => Miniatura is { Length: > 0 };

    public event Action? Concluido;

    public PacienteEdicaoViewModel(IServiceScopeFactory escopos, int? id = null)
    {
        _escopos = escopos;
        _id = id;
        _ = CarregarAsync();
    }

    partial void OnMiniaturaChanged(byte[]? value) => OnPropertyChanged(nameof(TemFoto));

    private async Task CarregarAsync()
    {
        try
        {
            Convenios.Clear();
            foreach (var c in CatalogoConvenios.Ativos) Convenios.Add(c);
            Convenio = Convenios.FirstOrDefault();

            Modalidades.Clear();
            foreach (var m in CatalogoModalidades.Ativas) Modalidades.Add(m);
            ModalidadePreferida = Modalidades
                .FirstOrDefault(m => m.Base == ModalidadeAtendimento.AcupunturaComEletro)
                ?? Modalidades.FirstOrDefault();

            if (_id is null) return;

            using var scope = _escopos.CreateScope();
            var pacientes = scope.ServiceProvider.GetRequiredService<PacienteService>();
            var p = await pacientes.ObterComHistoricoAsync(_id.Value);
            if (p is null) return;

            Titulo = "Editar paciente";
            Nome = p.Nome;
            Documento = p.Documento;
            Telefone = p.Telefone;
            Email = p.Email;
            Endereco = p.Endereco;
            DataNascimento = p.DataNascimento?.ToDateTime(TimeOnly.MinValue);
            SexoSelecionado = p.Sexo;
            Convenio = Convenios.FirstOrDefault(c => c.Codigo == p.ConvenioCodigo)
                       ?? Convenios.FirstOrDefault(c => c.Familia == p.Convenio)
                       ?? Convenio;
            Carteirinha = p.Carteirinha;
            ValidadeCarteirinha = p.ValidadeCarteirinha?.ToDateTime(TimeOnly.MinValue);
            ModalidadePreferida = Modalidades.FirstOrDefault(m => m.Codigo == p.ModalidadePreferidaCodigo)
                                  ?? Modalidades.FirstOrDefault(m => m.Base == p.ModalidadePreferida)
                                  ?? ModalidadePreferida;
            PossuiApp = p.PossuiApp;
            Observacoes = p.Observacoes;
            Origem = p.Origem;
            IndicadoPor = p.IndicadoPor;
            Miniatura = p.FotoMiniatura;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — cadastro do paciente não pôde ser aberto", ex);
            Erro($"Não foi possível carregar o cadastro: {ex.Message}");
        }
    }

    /// <summary>
    /// Abre a webcam do balcão. A foto fica PENDENTE até o Salvar: capturar não é
    /// gravar, e cadastro abandonado não pode deixar retrato solto no banco.
    /// </summary>
    [RelayCommand]
    private void CapturarFoto()
    {
        var janela = new CapturaFotoWindow(Nome)
        {
            Owner = JanelaDona.Atual()
        };

        // Diálogo cancelado sai calado: é o caso normal, e a pessoa sabe que desistiu.
        if (janela.ShowDialog() != true) return;

        // Aqui, não. Confirmar a captura e a janela devolver quadro vazio é defeito da
        // webcam (driver que solta o dispositivo, quadro perdido no clique) — e sair em
        // silêncio faria a recepcionista concluir que a foto está guardada. Ela só
        // descobriria no Salvar, com o paciente já fora do balcão.
        if (janela.Conteudo is null || janela.Miniatura is null)
        {
            Erro("A câmera não devolveu a imagem. Tente capturar de novo.");
            return;
        }

        _fotoCheiaPendente = janela.Conteudo;
        _fotoMiniaturaPendente = janela.Miniatura;
        _removerFoto = false;
        Miniatura = janela.Miniatura;
        Mensagem = "Foto capturada — ela será gravada ao salvar o cadastro.";
        MensagemEhErro = false;
    }

    [RelayCommand]
    private void RemoverFoto()
    {
        _fotoCheiaPendente = null;
        _fotoMiniaturaPendente = null;
        _removerFoto = true;
        Miniatura = null;
        Mensagem = "A foto será apagada ao salvar o cadastro.";
        MensagemEhErro = false;
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Mensagem = string.Empty;
        MensagemEhErro = false;

        if (string.IsNullOrWhiteSpace(Nome))
        {
            Erro("Informe o nome do paciente.");
            return;
        }

        if (Convenio is null)
        {
            Erro("Escolha o convênio.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(Documento) && !Cpf.Valido(Documento))
        {
            Erro("CPF inválido. Confira os números (ou deixe em branco).");
            return;
        }

        // E-mail errado gravado é lembrete que nunca chega — e ninguém descobre, porque o
        // envio conta o endereço inválido como "sem e-mail" e segue.
        if (!string.IsNullOrWhiteSpace(Email) && !EnderecoDeEmail.Valido(Email))
        {
            Erro("E-mail inválido. Confira o endereço (ou deixe em branco).");
            return;
        }

        try
        {
            Salvando = true;
            using var scope = _escopos.CreateScope();
            var pacientes = scope.ServiceProvider.GetRequiredService<PacienteService>();

            var paciente = _id is null
                ? new Paciente()
                : await pacientes.ObterComHistoricoAsync(_id.Value)
                  ?? throw new InvalidOperationException("Paciente não encontrado.");

            paciente.Nome = Nome.Trim();
            paciente.Documento = Limpar(Documento);
            paciente.Telefone = Limpar(Telefone);
            paciente.Email = EnderecoDeEmail.Normalizar(Email);
            paciente.Endereco = Limpar(Endereco);
            paciente.DataNascimento = DataNascimento is { } nasc ? DateOnly.FromDateTime(nasc) : null;
            paciente.Sexo = SexoSelecionado;
            paciente.Convenio = Convenio.Familia;
            paciente.ConvenioCodigo = Convenio.Codigo;
            paciente.Carteirinha = Limpar(Carteirinha);
            paciente.ValidadeCarteirinha =
                ValidadeCarteirinha is { } validade ? DateOnly.FromDateTime(validade) : null;
            paciente.ModalidadePreferida = ModalidadePreferida?.Base ?? paciente.ModalidadePreferida;
            paciente.ModalidadePreferidaCodigo = ModalidadePreferida?.Codigo;
            paciente.PossuiApp = PossuiApp;
            paciente.Observacoes = Limpar(Observacoes);
            paciente.Origem = Origem;
            // Quem indicou so faz sentido com a origem "Indicacao": guardar o nome preso
            // a outra origem deixaria um dado orfao que ninguem sabe ler depois.
            paciente.IndicadoPor = Origem == OrigemPaciente.Indicacao ? Limpar(IndicadoPor) : null;

            if (_id is null)
                await pacientes.SalvarNovoAsync(paciente);
            else
                await pacientes.AtualizarAsync(paciente);

            await GravarFotoAsync(pacientes, paciente.Id);

            Concluido?.Invoke();
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — paciente não pôde ser salvo", ex);
            Erro(ex.Message);
        }
        finally
        {
            Salvando = false;
        }
    }

    /// <summary>
    /// Grava o retrato depois de o paciente existir — num cadastro novo o Id só nasce
    /// no Salvar, e a foto precisa dele.
    /// </summary>
    private async Task GravarFotoAsync(PacienteService pacientes, int pacienteId)
    {
        if (_fotoCheiaPendente is not null && _fotoMiniaturaPendente is not null)
        {
            await pacientes.DefinirFotoAsync(pacienteId, _fotoCheiaPendente, _fotoMiniaturaPendente);
            _fotoCheiaPendente = null;
            _fotoMiniaturaPendente = null;
            return;
        }

        if (_removerFoto)
        {
            await pacientes.RemoverFotoAsync(pacienteId);
            _removerFoto = false;
        }
    }

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private void Erro(string texto)
    {
        Mensagem = texto;
        MensagemEhErro = true;
    }
}
