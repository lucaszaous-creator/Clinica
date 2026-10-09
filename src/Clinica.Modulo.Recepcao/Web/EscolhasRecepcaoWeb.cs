using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using Clinica.Recepcao.Janelas;
using Clinica.Recepcao.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Recepcao.Web;

/// <summary>Escolha contextual; os atos continuam na AgendaViewModel e em seus serviços.</summary>
public sealed class DetalheHorarioWebViewModel
{
    public CartaoAgenda Cartao { get; }
    public AcaoHorario Acao { get; private set; }
    public event Action? Confirmado;
    public IRelayCommand RemarcarCommand { get; }
    public IRelayCommand ConfirmarCommand { get; }
    public IRelayCommand ComprovanteCommand { get; }
    public IRelayCommand QuemChamarCommand { get; }
    public IRelayCommand FaltaCommand { get; }
    public IRelayCommand CancelarCommand { get; }
    public IRelayCommand CancelarSerieCommand { get; }

    public DetalheHorarioWebViewModel(CartaoAgenda cartao)
    {
        Cartao = cartao;
        RemarcarCommand = Criar(AcaoHorario.Remarcar, () => (cartao.EmAberto || cartao.ForaDoDia) && cartao.PodeEditarAgenda);
        ConfirmarCommand = Criar(AcaoHorario.Confirmar, () => cartao.EmAberto && cartao.TemTelefone);
        ComprovanteCommand = Criar(AcaoHorario.Comprovante, () => cartao.EmAberto);
        QuemChamarCommand = Criar(AcaoHorario.QuemChamar, () => cartao.EmAberto && cartao.PodeEditarAgenda);
        FaltaCommand = Criar(AcaoHorario.Falta, () => cartao.EmAberto && cartao.PodeEditarAgenda);
        CancelarCommand = Criar(AcaoHorario.Cancelar, () => cartao.EmAberto && cartao.PodeEditarAgenda);
        CancelarSerieCommand = Criar(AcaoHorario.CancelarSerie, () => cartao.EhSerie && cartao.PodeEditarAgenda);
    }

    private IRelayCommand Criar(AcaoHorario acao, Func<bool> permitido) => new RelayCommand(() =>
    {
        SessaoUsuario.Atual.Exigir(Permissao.VerAgenda, "consultar o horário");
        if (!permitido()) throw new InvalidOperationException("Esta ação não está disponível para o horário.");
        Acao = acao;
        Confirmado?.Invoke();
    }, permitido);
}

/// <summary>Seleciona apenas uma das vagas retornadas pela busca atual; não cria agendamento.</summary>
public sealed class ProximasVagasWebViewModel
{
    public ResultadoBuscaDeVagas Resultado { get; }
    public string Criterio => Resultado.Criterio;
    public IReadOnlyList<Vaga> Vagas => Resultado.Vagas;
    public bool Vazio => Resultado.Vazio;
    public string AvisoVazio => Vazio
        ? "Nenhuma vaga livre com esta duração nos próximos 60 dias. Confira a jornada e os bloqueios ou escolha data e hora no formulário."
        : string.Empty;
    public Vaga? Escolhida { get; private set; }
    public event Action? Confirmado;
    public IRelayCommand<Vaga> EscolherCommand { get; }

    public ProximasVagasWebViewModel(ResultadoBuscaDeVagas resultado)
    {
        Resultado = resultado;
        EscolherCommand = new RelayCommand<Vaga>(vaga =>
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarAgenda, "escolher uma vaga");
            if (vaga is null || !Vagas.Contains(vaga))
                throw new InvalidOperationException("A vaga não pertence à busca atual.");
            Escolhida = vaga;
            Confirmado?.Invoke();
        });
    }
}
