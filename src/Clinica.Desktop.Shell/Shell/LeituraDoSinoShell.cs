using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell;

/// <summary>
/// O VISTO e o LIDO do sino, para quem está logado (out/2026 — pedido da clínica: o número do
/// sino nunca sumia). A regra mora em <see cref="MarcasDoSino"/> (Application, testada) e a
/// gravação em <see cref="LeituraDoSinoService"/> (por usuário, no banco); aqui só se aplica.
///
/// O número do sino passa a ser o que é NOVO: respostas e pedidos cuja chave este usuário
/// ainda não viu, mais os avisos da sessão não lidos. Abrir o sino vê tudo — e o número some.
/// A PENDÊNCIA ("12 para ligar") continua dita no cabeçalho de cada grupo, sem piscar.
/// </summary>
public sealed partial class ShellViewModel
{
    private MarcasDoSino _marcas = MarcasDoSino.Vazias;
    private bool _marcasCarregadas;
    // Primeiro uso deste usuário: o que já estava no sino entra como visto e lido em silêncio,
    // uma vez por lista — estrear com o dia inteiro piscando ensinaria a ignorar o sino.
    private bool _semearPedidos, _semearRespostas, _semearSemDor, _semearEspera;
    private List<string> _chavesPedidosNovos = [];
    private List<string> _chavesRespostas = [];
    private List<string> _chavesSemDor = [];
    private List<string> _chavesEspera = [];

    /// <summary>Respostas e pedidos que este usuário ainda não viu — a parte "nova" do número do sino.</summary>
    public int AvisosNaoVistos => _marcasCarregadas ? _marcas.NaoVistas(_chavesPedidosNovos.Concat(_chavesRespostas).Concat(_chavesSemDor).Concat(_chavesEspera)) : 0;

    /// <summary>Há algo no sino ainda não lido (o "Marcar todas como lidas" fica aceso).</summary>
    public bool TemNaoLidas => NaoLidas > 0;

    /// <summary>Quantos avisos do sino estão sem leitura — o número da aba "Não lidas".</summary>
    public int NaoLidas => PedidosNoSino.Count(p => PedidoNaoLido(p.Id)) + RespostasNoSino.Count(RespostaNaoLida) + AvisosDoSemDor.Count(a => AvisoDoSemDorNaoLido(a.Chave))
        + EsperasNoWhatsApp.Count(e => EsperaNaoLida(e.Chave));

    public bool PedidoNaoLido(int pedidoId)
        => _marcasCarregadas && _chavesPedidosNovos.Contains(LeituraDoSino.ChavePedido(pedidoId))
           && !_marcas.Lido(LeituraDoSino.ChavePedido(pedidoId));

    public bool RespostaNaoLida(RespostaNoSino r)
        => _marcasCarregadas && !_marcas.Lido(LeituraDoSino.ChaveResposta(r.AgendamentoId, r.Tipo));

    /// <summary>Um aviso do @SemDor sem leitura (a chave dele é a do CRM, "semdor:…").</summary>
    public bool AvisoDoSemDorNaoLido(string chave) => _marcasCarregadas && !_marcas.Lido(chave);

    /// <summary>Uma espera no WhatsApp sem leitura (a chave é a da espera, "wa-espera:…": a mensagem nova é outra chave).</summary>
    public bool EsperaNaoLida(string chave) => _marcasCarregadas && !_marcas.Lido(chave);

    private async Task CarregarMarcasAsync()
    {
        if (_sessaoClinica is not { Autenticado: true } sessao) return;
        try
        {
            using var scope = _servicos.CreateScope();
            var gravadas = await scope.ServiceProvider.GetRequiredService<LeituraDoSinoService>().ObterAsync(sessao.UsuarioId);
            _semearPedidos = _semearRespostas = _semearSemDor = _semearEspera = gravadas is null;
            _marcas = gravadas ?? MarcasDoSino.Vazias;
            _marcasCarregadas = true;
        }
        catch (Exception ex)
        {
            // Sem as marcas, nada conta como novo: um número que não se sabe é melhor ausente
            // do que inventado — contar tudo como novo encheria o sino de uma vez. O "!"
            // continua para a leitura dos avisos que falhar; as listas seguem no sino.
            Application.Diagnostico.Registrar("Sino — marcas de leitura do usuário", ex);
        }
        NotificarSino();
    }

    /// <summary>A lista de pedidos NOVOS mudou (chaves). No primeiro uso, ela entra como lida.</summary>
    private void PedidosNovosMudaram(IEnumerable<int> ids)
    {
        _chavesPedidosNovos = ids.Select(LeituraDoSino.ChavePedido).ToList();
        if (_semearPedidos && _marcasCarregadas) { _semearPedidos = false; Persistir(s => s.LerAsync(Usuario, _chavesPedidosNovos), m => m.LerTodas(_chavesPedidosNovos)); }
        NotificarSino();
    }

    /// <summary>As respostas no sino mudaram (chaves). No primeiro uso, entram como lidas.</summary>
    private void RespostasMudaram(IEnumerable<AvisoDeConfirmacao> avisos)
    {
        _chavesRespostas = avisos.Select(a => LeituraDoSino.ChaveResposta(a.AgendamentoId, a.Tipo)).ToList();
        if (_semearRespostas && _marcasCarregadas) { _semearRespostas = false; Persistir(s => s.LerAsync(Usuario, _chavesRespostas), m => m.LerTodas(_chavesRespostas)); }
        NotificarSino();
    }

    /// <summary>Os avisos do @SemDor no sino mudaram (chaves). No primeiro uso, entram como lidos.</summary>
    private void AvisosDoSemDorMudaram(IEnumerable<string> chaves)
    {
        _chavesSemDor = chaves.ToList();
        if (_semearSemDor && _marcasCarregadas) { _semearSemDor = false; Persistir(s => s.LerAsync(Usuario, _chavesSemDor), m => m.LerTodas(_chavesSemDor)); }
        NotificarSino();
    }

    /// <summary>Quem espera resposta no WhatsApp mudou (chaves). No primeiro uso, entra como lido.</summary>
    private void EsperasMudaram(IEnumerable<string> chaves)
    {
        _chavesEspera = chaves.ToList();
        if (_semearEspera && _marcasCarregadas) { _semearEspera = false; Persistir(s => s.LerAsync(Usuario, _chavesEspera), m => m.LerTodas(_chavesEspera)); }
        NotificarSino();
    }

    private IReadOnlyList<string> ChavesDoSino => _chavesPedidosNovos.Concat(_chavesRespostas).Concat(_chavesSemDor).Concat(_chavesEspera).Distinct().ToList();

    /// <summary>Abrir o sino: tudo o que está nele passa a VISTO — o número some.</summary>
    private void VerSino()
    {
        if (!_marcasCarregadas) return;
        var chaves = ChavesDoSino;
        if (_marcas.NaoVistas(chaves) == 0) return;
        Persistir(s => s.VerAsync(Usuario, chaves), m => m.Ver(chaves));
    }

    /// <summary>"Marcar todas como lidas": o destaque some de tudo o que está no sino.</summary>
    [RelayCommand]
    private void MarcarTodasComoLidas()
    {
        Snackbar?.MarcarLidos();
        if (!_marcasCarregadas) return;
        var chaves = ChavesDoSino;
        Persistir(s => s.LerAsync(Usuario, chaves), m => m.LerTodas(chaves));
    }

    /// <summary>Clicar num aviso: só ele vira lido.</summary>
    private void LerAviso(string chave)
    {
        if (!_marcasCarregadas || _marcas.Lido(chave)) return;
        Persistir(s => s.LerAsync(Usuario, [chave]), m => m.Ler(chave));
    }

    private int Usuario => _sessaoClinica?.UsuarioId ?? 0;

    /// <summary>
    /// Aplica NA HORA (o número e o destaque respondem ao clique) e grava por trás. A gravação
    /// devolve o que ficou no banco somado ao que outra máquina do mesmo usuário marcou. Falhar
    /// ao gravar não desfaz o que a pessoa vê: na próxima abertura do app o aviso volta como
    /// não visto — e isso fica no log.
    /// </summary>
    private void Persistir(Func<LeituraDoSinoService, Task<MarcasDoSino>> gravar, Func<MarcasDoSino, MarcasDoSino> local)
    {
        _marcas = local(_marcas);
        NotificarSino();
        if (_sessaoClinica is not { Autenticado: true }) return;
        var usuario = Usuario;
        _ = GravarAsync();

        async Task GravarAsync()
        {
            try
            {
                using var scope = _servicos.CreateScope();
                var gravadas = await gravar(scope.ServiceProvider.GetRequiredService<LeituraDoSinoService>());
                if (usuario != Usuario) return;
                _marcas = gravadas;
                NotificarSino();
            }
            catch (Exception ex)
            {
                Application.Diagnostico.Registrar("Sino — gravar o que o usuário viu/leu", ex);
            }
        }
    }

    private void NotificarSino()
    {
        OnPropertyChanged(nameof(TotalAvisos));
        OnPropertyChanged(nameof(ContadorAvisos));
        OnPropertyChanged(nameof(TemAvisos));
        OnPropertyChanged(nameof(AvisosNaoVistos));
        OnPropertyChanged(nameof(NaoLidas));
        OnPropertyChanged(nameof(TemNaoLidas));
    }
}
