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

namespace Clinica.Desktop.Shell.Componentes.Cadastro;

/// <summary>
/// Cadastro do paciente na Recepção — o "360º" da proposta.
///
/// É aqui que a foto é tirada: a webcam fica no balcão, não na mesa do faturamento.
/// A gravação da imagem acontece no SALVAR, nunca na captura — quem desiste do
/// cadastro não deixa retrato órfão no banco.
/// </summary>
public sealed partial class CadastroPacienteViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private int? _id;
    public int? PacienteId => _id;
    public bool PodeEditar => SessaoUsuario.Atual.Pode(Permissao.EditarPaciente);
    public bool PodeAjustarCategoria => PodeEditar && SessaoUsuario.Atual.Pode(Permissao.VerFaturamento);
    public Array Categorias => Enum.GetValues(typeof(Categoria));
    [ObservableProperty] private Categoria _categoria;
    private bool _categoriaManual;
    private bool _carregandoCategoria;
    private void AtualizarCategoriaAutomatica()
    {
        if (_categoriaManual || Convenio is null) return;
        _carregandoCategoria = true;
        Categoria = CategoriaConvenio.Base(Convenio.Familia, PossuiApp);
        _carregandoCategoria = false;
    }
    partial void OnConvenioChanged(OpcaoDeConvenio? value) => AtualizarCategoriaAutomatica();
    partial void OnPossuiAppChanged(bool value) => AtualizarCategoriaAutomatica();
    partial void OnCategoriaChanged(Categoria value) { if (!_carregandoCategoria) _categoriaManual = true; }

    /// <summary>
    /// O que a escolha do convênio SIGNIFICA, ao lado do campo — "Sem guia: o paciente paga
    /// a sessão…", "Gera guia para o faturamento.".
    ///
    /// Sem nada escolhido ela é o CONVITE, e não um vazio: é a linha que diz à
    /// recepcionista que existe uma opção para quem não tem plano. O combo antes não dizia
    /// nada, e "Particular" era um nome entre operadoras.
    /// </summary>
    public string ExplicacaoDoConvenio => Convenio?.Explicacao
        ?? "Escolha o convênio do paciente — ou \"Particular\", se ele paga do bolso.";

    /// <summary>Foto capturada nesta sessão, ainda não gravada.</summary>
    private byte[]? _fotoCheiaPendente;
    private byte[]? _fotoMiniaturaPendente;
    private bool _removerFoto;

    /// <summary>
    /// Convênio ou particular, na ordem e com a frase de <see cref="OpcoesDeConvenio"/>
    /// (set/2026): operadoras primeiro, o Particular depois e o "a definir" por último.
    ///
    /// ⚠️ Era <c>CatalogoConvenios.Ativos</c> cru — ordem ALFABÉTICA — e o primeiro da lista
    /// vinha PRÉ-SELECIONADO. Numa base que importou a carteira do sistema anterior, o
    /// primeiro é "A definir (importado sem convênio)": todo paciente cadastrado no balcão
    /// nascia com um convênio que afirma ter vindo de importação, que não gera guia e que
    /// RECUSA o lançamento da sessão (parcela 92). Padrão que depende da ordem alfabética
    /// não é decisão.
    /// </summary>
    public ObservableCollection<OpcaoDeConvenio> Convenios { get; } = [];
    public ObservableCollection<EntradaModalidade> Modalidades { get; } = [];

    public IReadOnlyList<Sexo> Sexos { get; } = [Sexo.Feminino, Sexo.Masculino];

    /// <summary>Origens oferecidas, com o vazio na frente ("nao perguntado").</summary>
    public IReadOnlyList<OrigemPaciente?> Origens { get; } =
        [null, .. Enum.GetValues<OrigemPaciente>().Cast<OrigemPaciente?>()];

    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private string? _documento;
    [ObservableProperty] private string? _erroDocumento;
    [ObservableProperty] private string? _erroEndereco;

    partial void OnDocumentoChanged(string? value) => ErroDocumento = null;
    partial void OnEnderecoChanged(string? value) => ErroEndereco = null;
    [ObservableProperty] private string? _telefone;

    /// <summary>
    /// E-mail do paciente (set/2026). É por ele que sai o lembrete AUTOMÁTICO da sessão,
    /// quando a clínica liga o servidor de saída em Configurações. Opcional: sem ele o
    /// paciente continua sendo avisado pelo WhatsApp de um clique, como sempre.
    /// </summary>
    [ObservableProperty] private string? _email;

    /// <summary>
    /// Endereço residencial, opcional no cadastro e na edição.
    /// </summary>
    [ObservableProperty] private string? _endereco;
    [ObservableProperty] private DateTime? _dataNascimento;
    [ObservableProperty] private Sexo _sexoSelecionado = Sexo.Feminino;
    /// <summary>
    /// O convênio escolhido. Nasce NULO no cadastro novo: a pergunta "convênio ou
    /// particular?" é respondida por quem está com o paciente na frente, e o Salvar já
    /// recusa sem resposta ("Escolha o convênio"). Na EDIÇÃO vem o que a ficha tem.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExplicacaoDoConvenio))]
    private OpcaoDeConvenio? _convenio;
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
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodePreencher), nameof(PodeFechar))]
    private bool _salvando;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodePreencher))]
    private bool _carregando = true;
    private bool _carregado;
    public bool PodePreencher => PodeEditar && _carregado && !Carregando && !Salvando;
    public bool PodeFechar => !Salvando;

    /// <summary>Miniatura exibida no formulário (a pendente, se houve captura).</summary>
    [ObservableProperty] private byte[]? _miniatura;

    public bool TemFoto => Miniatura is { Length: > 0 };

    public event Action? Concluido;

    public CadastroPacienteViewModel(IServiceScopeFactory escopos, int? id = null)
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
            foreach (var c in OpcoesDeConvenio.Montar(CatalogoConvenios.Ativos, incluirADefinir: true))
                Convenios.Add(c);

            // ⚠️ NADA pré-selecionado. O "a definir" fica na LISTA (a ficha importada precisa
            // continuar mostrando o convênio que ela tem, e "ainda não sei" é resposta
            // legítima no balcão) — o que deixou de existir é ele ser o PADRÃO de quem foi
            // cadastrado hoje, por acidente da ordem alfabética.

            Modalidades.Clear();
            foreach (var m in CatalogoModalidades.Ativas) Modalidades.Add(m);
            ModalidadePreferida = Modalidades
                .FirstOrDefault(m => m.Base == ModalidadeAtendimento.AcupunturaComEletro)
                ?? Modalidades.FirstOrDefault();

            if (_id is null) { _carregado = true; return; }

            using var scope = _escopos.CreateScope();
            var pacientes = scope.ServiceProvider.GetRequiredService<PacienteService>();
            var p = await pacientes.ObterAsync(_id.Value);
            if (p is null) throw new InvalidOperationException("Paciente não encontrado. Reabra a lista antes de editar.");

            Titulo = "Editar paciente";
            Nome = p.Nome;
            Documento = p.Documento;
            Telefone = p.Telefone;
            Email = p.Email;
            Endereco = p.Endereco;
            DataNascimento = p.DataNascimento?.ToDateTime(TimeOnly.MinValue);
            SexoSelecionado = p.Sexo;
            // O CÓDIGO vence; a família é o caminho de baixo, para a ficha antiga cujo
            // código é o próprio nome do enum. Sem achar nenhum dos dois o combo fica
            // vazio, e o Salvar cobra a escolha — que é melhor do que marcar uma operadora
            // qualquer na ficha de quem talvez seja particular.
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
            _carregandoCategoria = true;
            Categoria = p.Categoria;
            _categoriaManual = p.Categoria != CategoriaConvenio.Base(p.Convenio, p.PossuiApp);
            _carregandoCategoria = false;
            _carregado = true;
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — cadastro do paciente não pôde ser aberto", ex);
            Erro($"Não foi possível carregar o cadastro: {ex.Message}");
        }
        finally { Carregando = false; }
    }

    /// <summary>
    /// Abre a webcam do balcão. A foto fica PENDENTE até o Salvar: capturar não é
    /// gravar, e cadastro abandonado não pode deixar retrato solto no banco.
    /// </summary>
    [RelayCommand]
    private void CapturarFoto()
    {
        if (!PodePreencher) return;
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
        if (!PodePreencher) return;
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
        if (Salvando) return;
        if (Carregando || !_carregado) { Erro("Aguarde o carregamento do cadastro. Se houve falha, feche e abra novamente."); return; }
        if (!PodeEditar) { Erro("Seu acesso não permite editar o cadastro de pacientes."); return; }
        Mensagem = string.Empty;
        MensagemEhErro = false;

        if (string.IsNullOrWhiteSpace(Nome))
        {
            Erro("Informe o nome do paciente.");
            return;
        }

        // A pergunta que o cadastro passou a FAZER (set/2026): ela já era recusada aqui, e
        // a recusa nunca aparecia porque o combo vinha pré-selecionado com o primeiro da
        // ordem alfabética. A frase diz as duas saídas, porque "escolha o convênio" não
        // responde a quem não tem convênio nenhum.
        if (Convenio is null)
        {
            Erro("Escolha o convênio — ou \"Particular\", se o paciente paga do bolso.");
            return;
        }

        ErroDocumento = ValidacaoCadastroPaciente.ErroCpf(Documento);
        ErroEndereco = ValidacaoCadastroPaciente.ErroEndereco(Endereco);
        if (ErroDocumento is not null || ErroEndereco is not null)
        {
            Erro("Confira os campos sinalizados. Os dados preenchidos foram mantidos.");
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
            SessaoUsuario.Atual.Exigir(Permissao.EditarPaciente, "salvar cadastro do paciente");
            using var scope = _escopos.CreateScope();
            var pacientes = scope.ServiceProvider.GetRequiredService<PacienteService>();

            var paciente = _id is null
                ? new Paciente()
                : await pacientes.ObterAsync(_id.Value)
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

            if (_categoriaManual) paciente.Categoria = Categoria;
            if (_id is null)
            {
                await pacientes.SalvarNovoAsync(paciente, _categoriaManual);
                _id = paciente.Id; // Uma falha ao gravar a foto não pode criar outra ficha na tentativa seguinte.
            }
            else
                await pacientes.AtualizarAsync(paciente, _categoriaManual);

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
