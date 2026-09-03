using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// O PONTO ÚNICO por onde passa "este paciente pode ser lançado?" quando a resposta
/// depende de escolher o convênio (parcela 92).
///
/// Por que é um ponto único, e não um trecho na tela do Novo atendimento
/// ---------------------------------------------------------------------
/// É a mesma razão do <see cref="ColetaDeTermo"/>: são VÁRIAS portas — o lançamento
/// avulso, o lançamento sobre o horário do dia, a marcação com "guia no agendamento"
/// ligado — e cada uma que montasse a janela por conta própria (escopo, ViewModel, dono
/// da janela, cópia do resultado de volta na ficha em memória) divergiria da outra na
/// primeira correção.
///
/// A recusa de verdade mora no serviço (<c>AtendimentoService.MontarAsync</c>), e é ela
/// que garante que nenhuma porta escape. Isto aqui é a metade VISÍVEL: perguntar antes,
/// para que a recusa não seja o que a pessoa descobre.
/// </summary>
public static class VinculoDeConvenio
{
    /// <summary>
    /// Garante que o paciente tenha convênio ESCOLHIDO — perguntando, quando não tem.
    ///
    /// Quando a ficha já tem convênio (o caso normal), devolve <c>true</c> sem abrir nada
    /// e sem ir ao banco. Quando não tem, abre a janela de escolha; escolhido e gravado, a
    /// ficha em memória recebe o convênio novo — a tela de trás mostra o crachá certo e a
    /// chamada seguinte ao serviço passa — e devolve <c>true</c>.
    /// </summary>
    /// <param name="paciente">
    /// A ficha que a tela tem em mãos. É ATUALIZADA no lugar quando o vínculo acontece:
    /// ela veio de outro escopo (o <c>DbContext</c> dela já morreu), então recarregá-la
    /// seria uma ida ao banco para saber o que a gravação acabou de decidir.
    /// </param>
    /// <returns><c>false</c> quando a pessoa desistiu — e aí o lançamento não segue.</returns>
    public static bool Garantir(
        IServiceScopeFactory escopos, Paciente paciente, string? operador = null)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(paciente);

        if (!paciente.ConvenioADefinir) return true;

        using var scope = escopos.CreateScope();
        return Perguntar(scope.ServiceProvider, paciente, operador);
    }

    /// <summary>
    /// A mesma garantia para quem só tem o ID em mãos — o cartão da Fila, que carrega o
    /// paciente pelo nome e pelo id, nunca pela ficha.
    ///
    /// Lê a ficha ANTES de decidir: a alternativa seria a tela guardar o convênio no
    /// cartão e conferir por lá, e aí a fila carregada às 8h decidiria sobre um convênio
    /// que a outra máquina do balcão já resolveu às 9h.
    /// </summary>
    public static async Task<bool> GarantirAsync(
        IServiceScopeFactory escopos, int pacienteId, string? operador = null)
    {
        ArgumentNullException.ThrowIfNull(escopos);

        using var scope = escopos.CreateScope();
        var servicos = scope.ServiceProvider;

        var paciente = await servicos.GetRequiredService<PacienteService>().ObterAsync(pacienteId);

        // Ficha que sumiu não é problema DESTA conferência: quem for lançar em seguida
        // dará o erro certo ("Paciente não encontrado"), e inventar um aqui trocaria a
        // mensagem verdadeira por uma sobre convênio.
        if (paciente is null || !paciente.ConvenioADefinir) return true;

        return Perguntar(servicos, paciente, operador);
    }

    /// <summary>O miolo: pergunta, grava (é a ViewModel que grava) e devolve o veredito.</summary>
    private static bool Perguntar(IServiceProvider servicos, Paciente paciente, string? operador)
    {
        var vm = new EscolhaDeConvenioViewModel(
            servicos.GetRequiredService<ConvenioCatalogoService>(),
            servicos.GetRequiredService<PacienteService>(),
            paciente,
            operador);

        var janela = new EscolhaDeConvenioWindow(vm) { Owner = JanelaDona.Atual() };

        // O DialogResult diz que a janela fechou; o `Vinculado` diz que a FICHA mudou.
        // Só o segundo libera o lançamento — fechar no "X" com um convênio destacado na
        // lista não vinculou coisa nenhuma.
        if (janela.ShowDialog() != true || vm.Vinculado is not { } escolhido)
            return false;

        paciente.ConvenioCodigo = escolhido.Codigo;
        paciente.Convenio = escolhido.Familia;
        paciente.Categoria = Clinica.Domain.Regras.CategoriaConvenio.Base(
            escolhido.Familia, paciente.PossuiApp);

        // Espelha a regra do serviço: em branco PRESERVA o que a ficha já tinha.
        if (!string.IsNullOrWhiteSpace(vm.Carteirinha))
            paciente.Carteirinha = vm.Carteirinha.Trim();
        if (vm.ValidadeCarteirinha is { } validade)
            paciente.ValidadeCarteirinha = DateOnly.FromDateTime(validade);

        return true;
    }
}
