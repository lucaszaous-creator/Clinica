using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Ui.Blazor.Telas;

/// <summary>
/// AS FOTOS DOS AVATARES da tela do WhatsApp (25/10/2026, pergunta da direção: "fotos de perfis
/// estão inclusas?"). A foto de perfil do WhatsApp NÃO existe para a clínica: a API oficial da Meta
/// (Cloud API) entrega o nome do perfil e nunca a foto. O que existe é o RETRATO DA FICHA, tirado
/// no balcão — e ele aparece no avatar da conversa quando a conversa está LIGADA a uma ficha
/// (<c>PatientId</c>, a ficha que a recepção conferiu: o telefone não é identidade, e a candidata
/// ainda não conferida não ganha rosto). Sem ficha ou sem retrato, ficam as iniciais.
///
/// - Lê só as miniaturas (~160 px) e só as que a tela ainda não tem: a releitura de 7 s não volta
///   ao banco. Uma ficha recém-ligada entra na batida seguinte.
/// - O retrato é da ficha: sem <see cref="Permissao.VerFichaPaciente"/>, iniciais.
/// - Falhou: as iniciais continuam e a falha vai ao log; a tela não muda por isso.
/// </summary>
public partial class ConversasDoWhatsApp
{
    /// <summary>A foto (o <c>data:</c>) de cada ficha já perguntada; nulo = perguntada e sem retrato (ou a pergunta falhou).</summary>
    private readonly Dictionary<int, string?> _fotos = new();
    private bool _buscandoFotos;

    /// <summary>Pede ao banco as miniaturas das fichas da lista, da Retenção e da conversa aberta que ainda não vieram.</summary>
    private void PedirFotos()
    {
        if (_painel is not { } painel || _buscandoFotos || Descartada || !Pode(Permissao.VerFichaPaciente)) return;
        var faltam = painel.Conversas.Select(c => c.PatientId)
            .Concat(painel.Retencao.Select(r => (int?)r.PacienteId))
            .Append(_aberta?.Paciente?.Id)
            .OfType<int>().Where(id => id > 0 && !_fotos.ContainsKey(id)).Distinct().ToList();
        if (faltam.Count == 0) return;
        // Marcadas ANTES de perguntar: a batida de 7 s seguinte não pergunta de novo pelas mesmas.
        foreach (var id in faltam) _fotos[id] = null;
        _buscandoFotos = true;
        _ = BuscarFotosAsync(faltam);
    }

    private async Task BuscarFotosAsync(IReadOnlyCollection<int> ids)
    {
        try
        {
            using var escopo = Escopo();
            var miniaturas = await escopo.ServiceProvider.GetRequiredService<PacienteService>().ObterMiniaturasAsync(ids);
            if (Descartada) return;
            foreach (var (id, bytes) in miniaturas)
                if (bytes.Length > 0) _fotos[id] = "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
            if (miniaturas.Count > 0) await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Blazor — fotos das fichas no WhatsApp", ex);
        }
        finally { _buscandoFotos = false; }
    }

    /// <summary>
    /// O avatar: o retrato da ficha quando há (as iniciais seguem no texto alternativo da lista, que é
    /// <c>aria-hidden</c>), senão as iniciais na cor da pessoa. <paramref name="extra"/> é o selo do canal.
    /// </summary>
    private RenderFragment Avatar(int? pacienteId, string iniciais, string tom, RenderFragment? extra = null) => b =>
    {
        var foto = pacienteId is int id ? _fotos.GetValueOrDefault(id) : null;
        b.OpenElement(0, "span");
        b.AddAttribute(1, "class", foto is null ? $"avatar tom-{tom}" : "avatar com-foto");
        b.AddAttribute(2, "aria-hidden", "true");
        if (foto is null) b.AddContent(3, iniciais);
        else
        {
            b.OpenElement(4, "img");
            b.AddAttribute(5, "src", foto);
            b.AddAttribute(6, "alt", "");
            b.CloseElement();
        }
        b.AddContent(7, extra);
        b.CloseElement();
    };
}
