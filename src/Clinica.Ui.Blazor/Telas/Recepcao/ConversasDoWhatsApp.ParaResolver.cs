using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Ui.Blazor.Telas;

/// <summary>
/// "PARA RESOLVER" NA COLUNA DO PACIENTE (R6, 25/10 — docs/whatsapp-nativo-implementacao.md §14). Os alertas da
/// Ficha do balcão (consulta a renovar, cota, carteirinha, guia glosada, pacote vencendo, conta em atraso, termo
/// pendente) chegam à conversa, para a recepção responder "a guia saiu?", "quantas sessões tenho?" sem trocar de tela.
///
/// As regras da leitura, e por quê:
/// <list type="bullet">
/// <item><b>Depois de a conversa estar desenhada</b>: são ~8 conferências ao banco; a conversa abre no tempo de
/// sempre e a seção aparece com o esqueleto até chegarem.</item>
/// <item><b>Uma vez por troca de paciente</b>: a releitura de 7 s e as escritas da conversa reabrem a conversa, mas
/// o paciente é o mesmo — a resposta guardada é reposta sem ir ao banco.</item>
/// <item><b>O mesmo bit da Ficha do balcão</b> (<see cref="Permissao.VerFichaPaciente"/>): o serviço de elegibilidade
/// não filtra por permissão, então sem o bit nada é lido nem desenhado.</item>
/// <item><b>Falhou, diz que não conferiu</b> (terceiro estado): vazio por erro não pode parecer "nada a resolver".</item>
/// </list>
/// Arquivo à parte da tela, como a mídia e as fotos, para as outras frentes que mexem nela não esbarrarem.
/// </summary>
public partial class ConversasDoWhatsApp
{
    /// <summary>A última resposta lida (de um paciente só): reposta enquanto o paciente da conversa não muda.</summary>
    private ParaResolverDoPaciente? _paraResolver;
    private int _geracaoParaResolver;

    private async Task ParaResolverDaConversaAsync()
    {
        if (_aberta is not { Paciente: { } paciente } aberta || !Pode(Permissao.VerFichaPaciente)) return;
        if (_paraResolver is { } guardado && guardado.PacienteId == paciente.Id && !guardado.Falhou)
        {
            if (!ReferenceEquals(aberta.Dados.ParaResolver, guardado)) _aberta = aberta with { Dados = aberta.Dados with { ParaResolver = guardado } };
            return;
        }
        var g = ++_geracaoParaResolver;
        // A conversa vai para a tela antes da leitura (a seção mostra o esqueleto enquanto isso).
        StateHasChanged();
        ParaResolverDoPaciente resposta;
        try
        {
            using var escopo = Escopo();
            resposta = await escopo.ServiceProvider.GetRequiredService<WhatsAppDaRecepcaoService>().ParaResolverAsync(paciente.Id);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Blazor — o \"Para resolver\" da conversa do WhatsApp não pôde ser lido", ex);
            resposta = new(paciente.Id, [], Falhou: true);
        }
        if (g != _geracaoParaResolver || Descartada || _aberta?.Paciente?.Id != paciente.Id) return;
        _paraResolver = resposta;
        _aberta = _aberta with { Dados = _aberta.Dados with { ParaResolver = resposta } };
    }
}
