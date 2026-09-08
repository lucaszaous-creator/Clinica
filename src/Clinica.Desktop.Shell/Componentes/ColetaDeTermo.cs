using System.Linq;
using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// O PONTO ÚNICO por onde toda coleta de termo passa (parcela 66, 3ª rodada).
///
/// São QUATRO portas — a ficha do paciente, a fila do balcão, o atendimento do Consultório
/// e a central de documentos —, e cada uma tinha começado a montar a janela por conta
/// própria: escopo, ViewModel, dono da janela, recarga. Quatro montagens divergem na
/// primeira correção, e o que elas colhem é a prova de que o paciente consentiu.
///
/// Aqui mora o que é igual nas quatro: escolher o modelo quando ele não vem decidido,
/// resolver os serviços do escopo, abrir a janela com o dono certo e dizer se concluiu.
/// </summary>
public static class ColetaDeTermo
{
    /// <summary>
    /// Abre a coleta. Quando <paramref name="modeloId"/> é nulo, pergunta ANTES qual termo
    /// — é o caminho da porta AVULSA, em que não há procedimento marcado dizendo qual é.
    /// </summary>
    /// <returns>
    /// <c>true</c> quando o paciente assinou ou recusou. <c>false</c> quando ninguém
    /// escolheu termo nenhum, ou a janela foi fechada sem resolver.
    ///
    /// ⚠️ Quem chama deve RECARREGAR de qualquer forma, e não só no <c>true</c>: abrir a
    /// janela já EMITE o termo numerado, então mesmo o "fechei sem assinar" mudou o que a
    /// tela de trás mostra.
    /// </returns>
    /// <param name="agendamentoId">
    /// A SESSÃO a que o termo se refere (set/2026). Quem sabe, passa: no consultório é o
    /// horário aberto na tela, no balcão é o cartão da fila. Quem não sabe deixa nulo — e
    /// aí esta porta RESOLVE, perguntando só quando há dúvida.
    /// </param>
    public static async Task<bool> AbrirAsync(
        IServiceScopeFactory escopos,
        int pacienteId,
        string pacienteNome,
        int? modeloId = null,
        int? documentoId = null,
        int? profissionalId = null,
        int? agendamentoId = null)
    {
        ArgumentNullException.ThrowIfNull(escopos);

        using var scope = escopos.CreateScope();
        var servicos = scope.ServiceProvider;

        if (modeloId is null)
        {
            var escolha = new EscolherTermoViewModel(
                servicos.GetRequiredService<TermoProcedimentoService>(), pacienteNome);

            var seletor = new EscolherTermoWindow(escolha) { Owner = Dono() };
            if (seletor.ShowDialog() != true || escolha.Escolhido is not { } modelo)
                return false;

            modeloId = modelo.Id;
        }

        var (sessaoId, sessaoRotulo, desistiu) = await ResolverSessaoAsync(
            servicos, pacienteId, pacienteNome, modeloId.Value, documentoId, agendamentoId);

        if (desistiu) return false;

        var vm = new AssinaturaPacienteViewModel(
            servicos.GetRequiredService<DocumentoClinicoService>(),
            servicos.GetRequiredService<AssinaturaDoPacienteService>(),
            servicos.GetRequiredService<IDialogoService>(),
            pacienteId,
            modeloId.Value,
            pacienteNome,
            documentoId,
            profissionalId,
            servicos.GetRequiredService<AcessoProntuarioService>(),
            servicos.GetRequiredService<ParametrosService>(),
            servicos.GetRequiredService<ProblemaPacienteService>(),
            servicos.GetRequiredService<ColetaRemotaTermoService>(),
            sessaoId)
        {
            SessaoDoTermo = sessaoRotulo
        };

        new AssinaturaPacienteWindow(vm) { Owner = Dono() }.ShowDialog();

        return vm.Concluido;
    }

    /// <summary>
    /// A qual SESSÃO este termo pertence — resolvido aqui, no ponto único, e não em cada
    /// porta (set/2026).
    ///
    /// A regra tem três degraus, e o do meio é o que a direção pediu:
    /// <list type="number">
    /// <item>quem CHAMOU já sabe (consultório, fila) — nada é perguntado;</item>
    /// <item>UMA sessão hoje e nenhuma outra à frente — amarra sozinho, e a janela DIZ a
    /// que amarrou. Perguntar aqui seria pedir à pessoa uma informação que o sistema
    /// tem;</item>
    /// <item>duas sessões, ou sessões à frente — abre a janela de escolha, que é
    /// literalmente o pedido do balcão.</item>
    /// </list>
    ///
    /// ⚠️ Documento JÁ EMITIDO não pergunta nada: a procedência foi gravada na emissão, e
    /// regravá-la porque a tela foi reaberta de outro lugar reescreveria um registro
    /// clínico por causa de um caminho de navegação.
    ///
    /// ⚠️ Falha de leitura NÃO impede colher. O termo assinado vale mais que a procedência
    /// dele: sem a agenda, ele nasce avulso e a assinatura acontece — travar a coleta
    /// porque uma consulta ao banco não respondeu produziria o desfecho pior, o
    /// procedimento sem termo nenhum.
    /// </summary>
    private static async Task<(int? Sessao, string? Rotulo, bool Desistiu)> ResolverSessaoAsync(
        IServiceProvider servicos,
        int pacienteId,
        string pacienteNome,
        int modeloId,
        int? documentoId,
        int? agendamentoId)
    {
        if (documentoId is not null) return (null, null, false);
        if (agendamentoId is { } jaSabe) return (jaSabe, null, false);

        var termos = servicos.GetRequiredService<TermoProcedimentoService>();

        IReadOnlyList<SessaoParaTermo> sessoes;
        try
        {
            sessoes = await termos.SessoesParaTermoAsync(
                pacienteId, DateOnly.FromDateTime(DateTime.Today));
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar(
                "Coleta de termo — a agenda do paciente não pôde ser lida", ex);
            return (null, null, false);
        }

        if (sessoes.Count == 0) return (null, null, false);

        if (sessoes.Count == 1 && sessoes[0].Hoje)
            return (sessoes[0].AgendamentoId, Descrever(sessoes[0]), false);

        var escolha = new EscolherSessaoDoTermoViewModel(
            termos, pacienteId, pacienteNome, await NomeDoModeloAsync(termos, modeloId));

        var janela = new EscolherSessaoDoTermoWindow(escolha) { Owner = Dono() };
        if (janela.ShowDialog() != true) return (null, null, true);

        var escolhida = escolha.Escolhida;
        var rotulo = escolhida is { } id
            ? sessoes.FirstOrDefault(s => s.AgendamentoId == id) is { } achada
                ? Descrever(achada)
                : null
            : null;

        return (escolhida, rotulo, false);
    }

    private static string Descrever(SessaoParaTermo sessao)
        => $"{sessao.Rotulo}  ·  {sessao.Detalhe}";

    private static async Task<string> NomeDoModeloAsync(
        TermoProcedimentoService termos, int modeloId)
    {
        try
        {
            var modelos = await termos.ModelosDisponiveisAsync();
            return modelos.FirstOrDefault(m => m.Id == modeloId)?.Nome ?? "Termo";
        }
        catch
        {
            // O nome do termo é enfeite do cabeçalho da janela; falhar aqui não pode
            // impedir a escolha da sessão.
            return "Termo";
        }
    }

    /// <summary>
    /// Colhe o TERMO LGPD (parcela 89) — o consentimento que o paciente assina.
    ///
    /// ⚠️ É a MESMA janela do termo de procedimento, e isso não é economia: ela é o único
    /// lugar que sabe colher o traço com a evidência que o sustenta (documento de
    /// identidade conferido, testemunha logada, selo do conteúdo que estava na tela) e que
    /// oferece a segunda tela e o envio pelo WhatsApp. Uma janela própria para o LGPD
    /// divergiria dela na primeira correção — e a metade que ficasse para trás seria a que
    /// ninguém releria.
    ///
    /// O que muda é só a AUSÊNCIA de modelo: este termo é montado das quatro finalidades,
    /// não copiado de um texto escrito pela clínica.
    /// </summary>
    /// <param name="documentoId">
    /// Um termo já emitido e ainda não assinado, quando existe. Nulo = emite um novo.
    /// </param>
    public static bool AbrirConsentimentoLgpd(
        IServiceScopeFactory escopos, int pacienteId, string pacienteNome,
        int? documentoId = null)
    {
        ArgumentNullException.ThrowIfNull(escopos);

        using var scope = escopos.CreateScope();
        var servicos = scope.ServiceProvider;

        var vm = new AssinaturaPacienteViewModel(
            servicos.GetRequiredService<DocumentoClinicoService>(),
            servicos.GetRequiredService<AssinaturaDoPacienteService>(),
            servicos.GetRequiredService<IDialogoService>(),
            pacienteId,
            modeloId: null,
            pacienteNome,
            documentoId,
            profissionalId: null,
            servicos.GetRequiredService<AcessoProntuarioService>(),
            servicos.GetRequiredService<ParametrosService>(),
            servicos.GetRequiredService<ProblemaPacienteService>(),
            servicos.GetRequiredService<ColetaRemotaTermoService>());

        new AssinaturaPacienteWindow(vm) { Owner = Dono() }.ShowDialog();

        return vm.Concluido;
    }

    /// <summary>
    /// A janela ATIVA, não a principal: com um modal já aberto, a próxima nasceria ATRÁS
    /// dele e quem clicou concluiria que o botão não fez nada (a lição da parcela 58).
    /// </summary>
    private static Window? Dono()
        => System.Windows.Application.Current?.Windows.OfType<Window>()
               .FirstOrDefault(w => w.IsActive)
           ?? System.Windows.Application.Current?.MainWindow;
}
