using System.Collections.ObjectModel;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Clinico.Janelas;
using Clinica.Clinico.Modulo;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.ViewModels;

/// <summary>
/// A tela de EXAMES do handoff (set/2026): os pedidos de exame com a situação derivada
/// de FATO — "Aguardando resultado" é o pedido sem resultado vigente amarrado,
/// "Resultado disponível" é o que tem. Sem o vínculo (a coluna
/// <see cref="ResultadoExame.PedidoDocumentoId"/>) a coluna de situação seria chute com
/// cara de registro.
///
/// O "Agendado" do mockup ficou de fora DE PROPÓSITO: não há fato de agendamento de
/// exame no domínio, e situação sem fato é a garantia aparente que o projeto recusa.
/// </summary>
public sealed partial class ExamesViewModel : ObservableObject, ICarregarAoAbrir
{
    /// <summary>A janela da lista. 90 dias cobrem o ciclo real de um laudo; o resumo DIZ o recorte.</summary>
    public const int JanelaDias = 90;

    private readonly IServiceScopeFactory _escopos;
    private readonly PacienteEmFoco _foco;

    // Toda carga async disparada por clique descarta resposta fora de ordem (parcela 60).
    private int _geracaoCarga;

    public ObservableCollection<PedidoDeExameLinha> Pedidos { get; } = [];

    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private bool _naoVerificado;
    [ObservableProperty] private string? _resumo;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _mensagemEhErro;

    /// <summary>Lista da clínica inteira (sem vínculo de profissional) — e o motivo escrito.</summary>
    [ObservableProperty] private bool _listaDaClinica;
    [ObservableProperty] private string? _motivoDaLista;

    // As metades VISÍVEIS das barreiras; quem impede é o Exigir de cada comando.
    public bool PodeEmitirPedido
        => SessaoUsuario.Atual.Pode(CentralDocumentosService.AcessoParaEmitir(
            TipoDocumentoClinico.PedidoExame));
    public bool PodeRegistrarResultado
        => SessaoUsuario.Atual.Pode(Permissao.EditarProntuario);

    public ExamesViewModel(IServiceScopeFactory escopos, PacienteEmFoco foco)
    {
        _escopos = escopos;
        _foco = foco;
    }

    [RelayCommand]
    public async Task CarregarAsync()
    {
        var geracao = ++_geracaoCarga;
        Carregando = true;

        try
        {
            var profissionalId = PostoClinico.ProfissionalDaLista();
            var hoje = DateOnly.FromDateTime(DateTime.Today);

            IReadOnlyList<PedidoDeExameLinha> pedidos;
            using (var scope = _escopos.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IClinicaRepositorio>();
                pedidos = await repo.PedidosDeExameAsync(
                    hoje.AddDays(-JanelaDias), hoje, profissionalId);
            }

            if (geracao != _geracaoCarga) return;

            ListaDaClinica = profissionalId is null;
            MotivoDaLista = PostoClinico.MotivoDaListaAmpla();

            // Entre o Clear e o último Add não há await (parcela 62).
            Pedidos.Clear();
            foreach (var p in pedidos) Pedidos.Add(p);

            var aguardando = pedidos.Count(p =>
                p.Situacao == SituacaoPedidoExame.AguardandoResultado);
            Resumo = pedidos.Count == 0
                ? $"Nenhum pedido nos últimos {JanelaDias} dias"
                : $"{pedidos.Count} pedido(s) nos últimos {JanelaDias} dias · {aguardando} aguardando resultado";

            NaoVerificado = false;
        }
        catch (Exception ex)
        {
            if (geracao != _geracaoCarga) return;
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — pedidos de exame não puderam ser carregados", ex);
            NaoVerificado = true;
        }
        finally
        {
            if (geracao == _geracaoCarga) Carregando = false;
        }
    }

    /// <summary>
    /// Emite um pedido novo. A tela não tem paciente em foco, então a primeira pergunta
    /// é QUEM — e a emissão é a MESMA janela de documento de todas as outras portas.
    /// </summary>
    [RelayCommand]
    private async Task NovoPedidoAsync()
    {
        try
        {
            SessaoUsuario.Atual.Exigir(
                CentralDocumentosService.AcessoParaEmitir(TipoDocumentoClinico.PedidoExame),
                "emitir pedido de exame");

            var paciente = EscolherPacienteWindow.Perguntar(
                "Novo pedido de exame — para quem?", JanelaDona.Atual(), _escopos);
            if (paciente is null) return;

            var vm = new DocumentoEdicaoViewModel(
                _escopos, paciente.Id, TipoDocumentoClinico.PedidoExame);
            var janela = new DocumentoWindow(vm) { Owner = JanelaDona.Atual() };

            var concluiu = janela.ShowDialog() == true;
            await CarregarAsync();
            if (concluiu)
            {
                Mensagem = $"Pedido de exame emitido para {paciente.Nome}.";
                MensagemEhErro = false;
            }
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — pedido de exame não pôde ser emitido", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>Registra o laudo que chegou, já amarrado NESTE pedido.</summary>
    [RelayCommand]
    private async Task RegistrarResultadoAsync(PedidoDeExameLinha? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.EditarProntuario, "escrever no prontuário");

            var vm = new ResultadoExameEdicaoViewModel(
                _escopos, linha.PacienteId, linha.Paciente, linha.DocumentoId);
            var janela = new RegistrarResultadoExameWindow(vm) { Owner = JanelaDona.Atual() };
            janela.ShowDialog();

            await CarregarAsync();
            if (vm.Registrado)
            {
                Mensagem = $"Resultado registrado para {linha.Paciente}.";
                MensagemEhErro = false;
            }
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Consultório — resultado não pôde ser registrado da tela de Exames", ex);
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }

    /// <summary>
    /// Abre o paciente na seção "Exames e anexos" — é lá que moram os resultados, os
    /// laudos anexados e o registro avulso. "Ver resultados" e "Detalhes" levam ao MESMO
    /// lugar de propósito: uma resposta, uma tela.
    /// </summary>
    [RelayCommand]
    private void Abrir(PedidoDeExameLinha? linha)
    {
        if (linha is null) return;

        try
        {
            SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "abrir os exames do paciente");

            _foco.Definir(linha.PacienteId, linha.Paciente);
            if (!NavegacaoSuite.Ir(ModuloClinico.ChaveExamesDoPaciente))
            {
                // Ir devolve false EM SILÊNCIO quando a chave não está no menu — a
                // regressão da parcela 37; aqui ela vira frase, nunca clique morto.
                Mensagem = "Não deu para abrir a seção de exames do paciente.";
                MensagemEhErro = true;
            }
        }
        catch (Exception ex)
        {
            Mensagem = ex.Message;
            MensagemEhErro = true;
        }
    }
}
