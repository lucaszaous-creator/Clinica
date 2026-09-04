using System.Collections.ObjectModel;
using Clinica.Application;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// LER UMA SESSÃO DO PRONTUÁRIO por inteiro, e IMPRIMI-LA para o paciente (set/2026 — os
/// dois pedidos do cliente, que são o mesmo pedido: <i>"ao abrir o prontuário não
/// conseguimos abrir o prontuário daquela sessão"</i> e <i>"imprimir sessão daquele
/// prontuário para entregar ao paciente"</i>).
///
/// Por que ela mora no SHELL
/// -------------------------
/// São TRÊS portas, em DOIS módulos que não se conhecem: a lista de sessões do Consultório,
/// o modal de leitura rápida da lista plana de Prontuários (também do Consultório) e a
/// lista do prontuário da Recepção. Três cópias de "abrir a sessão" divergiriam na primeira
/// correção, e a que ficasse para trás é a que abriria dado de saúde SEM registrar quem leu
/// (a lição das parcelas 60 e 75, e o argumento que já pôs aqui o mapa corporal e o
/// <see cref="ArquivosDaFicha"/>).
///
/// ⚠️ A tela da ENFERMAGEM não é porta desta janela, e é decisão: ela lista
/// <c>EvolucaoEnfermagem</c> — a passagem, com hora, sinais vitais e as cinco etapas da
/// COFEN —, e esta janela abre uma <c>Evolucao</c>, que é a sessão do prontuário médico.
/// São naturezas diferentes com ids POR TABELA (a lição da parcela 71): abrir uma pela
/// outra mostraria a sessão de outro paciente sem estourar nada.
///
/// O que ela NÃO faz, e é decisão
/// ------------------------------
/// ⚠️ Ela não EDITA. Ler a sessão pede <see cref="Permissao.VerProntuario"/> e escrever
/// pede <see cref="Permissao.EditarProntuario"/> — é o corte da parcela 49, e é o que
/// permite à técnica de enfermagem e ao faturista lerem o registro sem poderem mexer nele.
/// A porta de EDIÇÃO continua sendo a tela de Atendimento (Consultório) e a janela de
/// evolução (Recepção), cada uma com o bit de escrita.
///
/// ⚠️ As duas portas laterais são tratadas de formas DIFERENTES, e a diferença é onde a
/// janela de destino mora:
///
/// <list type="bullet">
///   <item>as CORREÇÕES (<c>VersoesEvolucaoWindow</c>) são do SHELL — a janela as abre por
///   dentro, e o botão vale em toda porta;</item>
///   <item>os ANEXOS moram no módulo Clínico e o shell não os alcança. O botão devolve a
///   INTENÇÃO (<see cref="PediuAnexos"/>) e quem age é a tela dona — o padrão da janela do
///   horário (parcela 58) —, e por isso ele só aparece quando a dona diz que sabe agir
///   (<c>ofereceAnexos</c>). Sem essa condição, abrir a sessão pela RECEPÇÃO — que não tem
///   janela de anexos — daria um botão que fecha a janela e não faz nada, que é o defeito
///   da parcela 41 construído de propósito.</item>
/// </list>
/// </summary>
public sealed partial class SessaoDoProntuarioViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _escopos;
    private readonly int _evolucaoId;
    private readonly bool _ofereceAnexos;

    /// <summary>Preenchidos pela carga; a impressão precisa dos dois.</summary>
    private int _pacienteId;
    private DateOnly? _dia;

    public ObservableCollection<BlocoDaSessao> Blocos { get; } = [];

    [ObservableProperty] private string _paciente;
    [ObservableProperty] private string _titulo = "Sessão do prontuário";
    [ObservableProperty] private string? _linhaDaSessao;

    /// <summary>A data formatada — o título da janela de correções a usa.</summary>
    [ObservableProperty] private string? _dataTexto;
    [ObservableProperty] private string? _procedencia;
    [ObservableProperty] private string? _avisoCancelamento;
    [ObservableProperty] private string? _anexosTexto;
    [ObservableProperty] private string? _correcoesTexto;

    [ObservableProperty] private bool _temAnexos;
    [ObservableProperty] private bool _retificada;
    [ObservableProperty] private bool _semBlocos;

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>A tela dona lê isto depois do <c>ShowDialog</c> e abre os anexos.</summary>
    public bool PediuAnexos { get; private set; }

    /// <summary>A janela observa para fechar quando a intenção foi registrada.</summary>
    public event Action? Fechar;

    /// <summary>
    /// O botão de anexos existe quando a sessão TEM arquivos <b>e</b> a tela dona sabe
    /// abri-los. As duas metades são obrigatórias: sem a primeira ele diria "nada a ver
    /// aqui" em quarenta sessões; sem a segunda ele fecharia a janela para nada.
    /// </summary>
    public bool MostraAnexos => _ofereceAnexos && TemAnexos;

    /// <summary>
    /// Metade VISÍVEL da barreira; quem impede é o <c>Exigir</c> dentro de
    /// <see cref="FichaDoAtendimento"/>. A ficha IMPRIME o prontuário — o bit é o de LER.
    ///
    /// ⚠️ Ela cobre também "a sessão carregou": botão aceso antes de haver dia e paciente
    /// emitiria a ficha do período errado, e botão que não faz nada é a parcela 41.
    /// </summary>
    public bool PodeImprimir =>
        _dia is not null && _pacienteId > 0
        && SessaoUsuario.Atual.Pode(Permissao.VerProntuario);

    /// <param name="ofereceAnexos">
    /// A tela dona sabe abrir a janela de anexos (ela mora no módulo Clínico). Parâmetro
    /// OBRIGATÓRIO: com valor padrão, a próxima porta nasceria oferecendo um botão que
    /// não faz nada sem ninguém ter decidido isso.
    /// </param>
    public SessaoDoProntuarioViewModel(
        IServiceScopeFactory escopos, int evolucaoId, string paciente, bool ofereceAnexos)
    {
        _escopos = escopos;
        _evolucaoId = evolucaoId;
        _ofereceAnexos = ofereceAnexos;
        Paciente = paciente;

        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        Carregando = true;
        try
        {
            // A porta de LEITURA de dado de saúde tem as duas barreiras: o botão de cada
            // lista já vem apagado sem o bit, e esta é a que impede.
            SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "abrir a sessão do prontuário");

            Evolucao? evolucao;
            int anexos;

            using (var escopo = _escopos.CreateScope())
            {
                var servicos = escopo.ServiceProvider;
                var repo = servicos.GetRequiredService<IClinicaRepositorio>();

                // SEQUENCIAL, nunca WhenAll: é o mesmo DbContext do escopo (parcela 74).
                evolucao = await repo.ObterEvolucaoAsync(_evolucaoId);

                if (evolucao is null)
                {
                    NaoVerificado = true;
                    Mensagem = "Esta sessão não foi encontrada no prontuário.";
                    MensagemEhErro = true;
                    return;
                }

                var contagem = await repo.ContagemDeAnexosAsync([_evolucaoId]);
                anexos = contagem.TryGetValue(_evolucaoId, out var q) ? q : 0;

                // Janela de dado de saúde deixa rastro — uma vez por abertura (ponto 4 do
                // compromisso). `RegistrarAsync` não lança e loga por dentro: banco lento
                // não pode impedir alguém de ler o prontuário do paciente que está na
                // frente, e a janela de silêncio de 30 min cobre a leitura repetida.
                await servicos.GetRequiredService<AcessoProntuarioService>()
                    .RegistrarAsync(evolucao.PacienteId, SessaoUsuario.Atual.Operador,
                        OrigemAcessoProntuario.ProntuarioClinico);
            }

            // ⚠️ As correções saem de `Versoes.Count` porque `ObterEvolucaoAsync` é a ÚNICA
            // leitura do repositório que as traz com `Include` — e isso está escrito ao lado
            // dela. Sem o Include a navegação viria VAZIA em produção enquanto o teste
            // passaria pelo relationship fixup do EF (a lição da parcela 68).
            var sessao = SessaoDoProntuario.De(evolucao, anexos, evolucao.Versoes.Count);

            _pacienteId = evolucao.PacienteId;
            _dia = evolucao.Data;

            Titulo = sessao.Titulo;
            DataTexto = sessao.Data;
            LinhaDaSessao = $"{sessao.Data} · {sessao.Eva} · {sessao.Profissional}";
            Procedencia = sessao.Procedencia;
            AvisoCancelamento = sessao.AvisoCancelamento;
            AnexosTexto = sessao.AnexosTexto;
            CorrecoesTexto = sessao.CorrecoesTexto;
            TemAnexos = sessao.TemAnexos;
            Retificada = sessao.Retificada;
            OnPropertyChanged(nameof(MostraAnexos));

            // Entre o `Clear()` e o último `Add` não pode haver `await` (parcela 62).
            Blocos.Clear();
            foreach (var b in sessao.Blocos) Blocos.Add(b);

            SemBlocos = Blocos.Count == 0;
            OnPropertyChanged(nameof(PodeImprimir));
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar(
                $"Suíte — a sessão {_evolucaoId} do prontuário não pôde ser aberta", ex);
            NaoVerificado = true;
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>
    /// A FICHA desta sessão — o papel que o paciente leva embora, recortado no DIA dela.
    ///
    /// ⚠️ O recorte é a data da SESSÃO, nunca hoje: a lista abre sessão de meses atrás, e
    /// recortar em hoje devolveria "não há registro no prontuário para relatar neste
    /// período" sobre uma sessão que está aberta na tela.
    ///
    /// Não há rascunho a perder — esta janela LÊ e não escreve.
    /// </summary>
    [RelayCommand]
    private async Task ImprimirAsync()
    {
        if (_dia is not { } dia)
        {
            // Guarda que DIZ por que não dá (parcela 41). Ela é sobre ESTADO, não sobre
            // parâmetro: o botão pode estar aceso um instante antes de a carga terminar.
            Mensagem = "Espere a sessão terminar de carregar para imprimir a ficha.";
            MensagemEhErro = true;
            return;
        }

        var r = await FichaDoAtendimento.EmitirAsync(
            _escopos, _pacienteId, dia,
            temRascunhoNaoGravado: false,
            contextoDoLog: "Suíte — sessão do prontuário");

        Mensagem = r.Frase;
        MensagemEhErro = r.EhErro;
    }

    [RelayCommand]
    private void VerAnexos()
    {
        PediuAnexos = true;
        Fechar?.Invoke();
    }

    /// <summary>
    /// O histórico de correções (parcela 52) — a rastreabilidade do art. 3º da Lei
    /// 13.787/2018 LIDA, e não só guardada.
    ///
    /// Esta a janela abre por DENTRO: <c>VersoesEvolucaoWindow</c> é do shell, alcançável
    /// de qualquer porta. Devolvê-la como intenção obrigaria cada tela dona a repetir a
    /// mesma abertura — três cópias de uma janela que já é compartilhada.
    /// </summary>
    [RelayCommand]
    private void VerCorrecoes()
    {
        try
        {
            new VersoesEvolucaoWindow
            {
                DataContext = new VersoesEvolucaoViewModel(
                    _escopos, _evolucaoId, $"{DataTexto} — {Paciente}"),
                Owner = JanelaDona.Atual()
            }.ShowDialog();
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar(
                $"Suíte — histórico de correções da sessão {_evolucaoId}", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
