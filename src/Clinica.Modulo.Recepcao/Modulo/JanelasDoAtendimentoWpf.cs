using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Componentes.Cadastro;
using Clinica.Domain.Entities;
using Clinica.Recepcao.ViewModels;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.Modulo;

/// <summary>
/// O que a tela "Lançar e marcar atendimento" (Blazor, out/2026) pede à Recepção WPF — cada
/// método faz EXATAMENTE o que o <c>NovoAtendimentoViewModel</c> fazia: a mesma ponte
/// <see cref="PreenchimentoNovoAtendimento"/>, as mesmas janelas e ViewModels, o dono pela
/// <see cref="JanelaDona"/> e um escopo por operação. Nada foi reescrito: a janela de fechamento
/// carrega pacote, insumo e caixa, e uma segunda definição divergiria na primeira correção.
///
/// ⚠️ Tudo roda no despachante do WPF: a janela modal precisa da thread da interface, e a tela
/// Blazor pode chamar de outra (o cuidado de <see cref="JanelasDaAgendaWpf"/>).
/// </summary>
public sealed class JanelasDoAtendimentoWpf(
    IServiceScopeFactory escopos,
    IDialogoService dialogo,
    PreenchimentoNovoAtendimento ponte) : IJanelasDoAtendimento
{
    public bool Disponivel => true;

    public bool HaPedido(bool marcar) => ponte.Espiar(marcarParaDepois: marcar) is not null;

    public PedidoDoAtendimento? ConsumirPedido(bool marcar)
        => ponte.ConsumirPara(marcarParaDepois: marcar) is { } p
            ? new PedidoDoAtendimento(p.DataHora, p.ProfissionalId, p.SalaId, p.PacienteId, p.DuracaoMinutos, p.ModalidadeCodigo)
            : null;

    public Task<bool> EscolherConvenioAsync(int pacienteId)
        => NaInterface(() => VinculoDeConvenio.GarantirAsync(escopos, pacienteId, SessaoUsuario.Atual.Operador));

    public Task<bool> VenderPacoteAsync(int pacienteId) => NaInterface(async () =>
    {
        // A MESMA janela da tela Pacotes, com o paciente já escolhido (set/2026).
        var paciente = await ObterAsync(pacienteId);
        if (paciente is null) return false;
        var vm = new PacoteVendaViewModel(escopos, paciente);
        return new PacoteVendaWindow(vm) { Owner = JanelaDona.Atual() }.ShowDialog() == true;
    });

    public Task<bool> ReceberDividaAsync(int pacienteId) => NaInterface(async () =>
    {
        // A MESMA janela de cobrança da ficha — quem grava dinheiro continua sendo um só.
        var paciente = await ObterAsync(pacienteId);
        if (paciente is null) return false;
        var vm = new CobrancaDoPacienteViewModel(escopos, dialogo, paciente.Id, paciente.Nome, paciente.Telefone);
        new CobrancaDoPacienteWindow(vm) { Owner = JanelaDona.Atual() }.ShowDialog();
        return vm.Mudou;
    });

    public Task<int?> CadastrarPacienteAsync() => NaInterface(() =>
    {
        var vm = new CadastroPacienteViewModel(escopos);
        var janela = new CadastroPacienteWindow(vm) { Owner = JanelaDona.Atual() };
        return Task.FromResult(janela.ShowDialog() == true ? vm.PacienteId : null);
    });

    private async Task<Paciente?> ObterAsync(int pacienteId)
    {
        using var scope = escopos.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PacienteService>().ObterAsync(pacienteId);
    }

    private static Task<T> NaInterface<T>(Func<Task<T>> acao)
    {
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (despachante is null || despachante.CheckAccess()) return acao();
        return despachante.InvokeAsync(acao).Task.Unwrap();
    }
}
