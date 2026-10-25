using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Componentes;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Ui.Blazor.Telas;

/// <summary>
/// A MÍDIA da tela do WhatsApp (24/10/2026, pedido da direção: "ver imagens, ouvir áudios e baixar
/// itens enviados dentro da tela"): trazer o arquivo de uma mensagem, guardá-lo para a releitura de
/// 7 s não o baixar de novo, mostrar a foto grande, baixar, abrir, e enviar o áudio gravado. Num
/// arquivo à parte da tela para a parte da conversa e a da mídia crescerem sem se atropelar; as
/// regras (rota, teto, rótulos, nome para salvar) são de <see cref="MidiaDaConversa"/>.
///
/// As regras da casa: escopo por operação; a mesma barreira da aba (<see cref="Permissao.EditarAgenda"/>)
/// em baixar, abrir e gravar; disco só pelo <c>Host</c>; e a recusa do CRM é frase na tela, não log —
/// só o CRM fora do ar vai ao <c>Diagnostico</c>.
/// </summary>
public partial class ConversasDoWhatsApp
{
    /// <summary>Quantos arquivos vêm do CRM ao mesmo tempo (uma conversa com dez fotos não abre dez conexões).</summary>
    private const int MidiasAoMesmoTempo = 3;

    private readonly MemoriaDeMidia _memoriaDeMidia = new();
    private readonly HashSet<string> _midiaCarregando = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _midiaFalhou = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _vagasDeMidia = new(MidiasAoMesmoTempo, MidiasAoMesmoTempo);
    private MidiaDaMensagem? _midiaAmpliada;
    private GravadorDeAudio? _gravador;

    /// <summary>O balão de uma mensagem com mídia (o que já veio, o carregando e a falha são desta tela).</summary>
    private RenderFragment Midia(MidiaDaMensagem m) => b =>
    {
        b.OpenComponent<MidiaNoBalao>(0);
        b.SetKey(m.Chave);
        b.AddAttribute(1, nameof(MidiaNoBalao.Midia), m);
        b.AddAttribute(2, nameof(MidiaNoBalao.Carregada), _memoriaDeMidia.Ler(m.Chave));
        b.AddAttribute(3, nameof(MidiaNoBalao.Carregando), _midiaCarregando.Contains(m.Chave));
        b.AddAttribute(4, nameof(MidiaNoBalao.Falha), _midiaFalhou.GetValueOrDefault(m.Chave));
        b.AddAttribute(5, nameof(MidiaNoBalao.AoCarregar), EventCallback.Factory.Create(this, () => CarregarMidiaAsync(m)));
        b.AddAttribute(6, nameof(MidiaNoBalao.AoAmpliar), EventCallback.Factory.Create(this, () => _midiaAmpliada = m));
        b.AddAttribute(7, nameof(MidiaNoBalao.AoBaixar), EventCallback.Factory.Create(this, () => BaixarMidiaAsync(m)));
        b.AddAttribute(8, nameof(MidiaNoBalao.AoAbrir), EventCallback.Factory.Create(this, () => AbrirMidiaAsync(m)));
        b.CloseComponent();
    };

    /// <summary>A foto ampliada (só enquanto a conversa dela está aberta).</summary>
    private RenderFragment FotoAmpliada() => b =>
    {
        if (_midiaAmpliada is not { } m || _aberta?.Conversa?.Id != m.ConversaId) return;
        b.OpenComponent<Clinica.Ui.Blazor.Formularios.MidiaAmpliadaForm>(0);
        b.AddAttribute(1, nameof(Clinica.Ui.Blazor.Formularios.MidiaAmpliadaForm.Midia), m);
        b.AddAttribute(2, nameof(Clinica.Ui.Blazor.Formularios.MidiaAmpliadaForm.Carregada), _memoriaDeMidia.Ler(m.Chave));
        b.AddAttribute(3, nameof(Clinica.Ui.Blazor.Formularios.MidiaAmpliadaForm.Ocupado), _midiaCarregando.Contains(m.Chave));
        b.AddAttribute(4, nameof(Clinica.Ui.Blazor.Formularios.MidiaAmpliadaForm.AoFechar), EventCallback.Factory.Create(this, () => _midiaAmpliada = null));
        b.AddAttribute(5, nameof(Clinica.Ui.Blazor.Formularios.MidiaAmpliadaForm.AoBaixar), EventCallback.Factory.Create(this, () => BaixarMidiaAsync(m)));
        b.CloseComponent();
    };

    private async Task CarregarMidiaAsync(MidiaDaMensagem m) => await TrazerMidiaAsync(m);

    /// <summary>
    /// O arquivo da mensagem: da memória, ou do CRM (e então guardado). Nulo quando falhou (a frase fica
    /// em <see cref="_midiaFalhou"/>, e o balão mostra "Tentar de novo") ou quando já está a caminho.
    /// </summary>
    private async Task<MidiaCarregada?> TrazerMidiaAsync(MidiaDaMensagem m)
    {
        if (_memoriaDeMidia.Ler(m.Chave) is { } pronta) return pronta;
        if (!_midiaCarregando.Add(m.Chave)) return null;
        _midiaFalhou.Remove(m.Chave);
        StateHasChanged();
        try
        {
            await _vagasDeMidia.WaitAsync();
            try
            {
                using var escopo = Escopo();
                var carregada = await escopo.ServiceProvider.GetRequiredService<WhatsAppDaRecepcaoService>().MidiaAsync(m);
                if (Descartada) return null;
                _memoriaDeMidia.Guardar(m.Chave, carregada);
                // A última mensagem da conversa cresce quando o arquivo chega (o esqueleto é menor que a
                // foto e o tocador): a tela, que já tinha rolado ao fim, rola de novo (foto 55).
                if (_aberta?.LinhaDoTempo.LastOrDefault(i => i.Tipo is TipoNaLinhaDoTempo.Recebida or TipoNaLinhaDoTempo.Enviada)?.Midia?.Chave == m.Chave)
                    _rolarAoFim = true;
                return carregada;
            }
            finally { _vagasDeMidia.Release(); }
        }
        catch (CrmIndisponivel ex)
        {
            Clinica.Application.Diagnostico.Registrar("Blazor — mídia do WhatsApp não veio do CRM", ex);
            _midiaFalhou[m.Chave] = MidiaDaConversa.Falha(m, ex.Message);
            return null;
        }
        catch (InvalidOperationException ex) // RecusaDoCrm: o CRM disse por quê
        {
            _midiaFalhou[m.Chave] = MidiaDaConversa.Falha(m, ex.Message);
            return null;
        }
        finally { _midiaCarregando.Remove(m.Chave); }
    }

    /// <summary>Baixar: pergunta onde salvar e grava (o disco é do Host). Desistir da janela não faz nada.</summary>
    private async Task BaixarMidiaAsync(MidiaDaMensagem m)
    {
        if (!Exigir(Permissao.EditarAgenda, "baixar um arquivo da conversa")) return;
        if (await TrazerMidiaAsync(m) is not { } carregada) { AvisarFalhaDaMidia(m); return; }
        var nome = MidiaDaConversa.NomeParaSalvar(m, carregada.Arquivo);
        var (filtro, extensao) = MidiaDaConversa.FiltroParaSalvar(nome);
        var caminho = await Host.PerguntarOndeSalvarAsync(nome, filtro, extensao);
        if (caminho is null || Descartada) return;
        if (await Host.GravarArquivoAsync(caminho, carregada.Arquivo.Conteudo) is { } erro) { Notificacoes.Erro(erro); return; }
        Notificacoes.Sucesso($"{m.Rotulo} salvo em {caminho}.");
    }

    /// <summary>Abrir (o documento): salva onde a pessoa escolher e abre no programa do Windows.</summary>
    private async Task AbrirMidiaAsync(MidiaDaMensagem m)
    {
        if (!Exigir(Permissao.EditarAgenda, "abrir um arquivo da conversa")) return;
        if (await TrazerMidiaAsync(m) is not { } carregada) { AvisarFalhaDaMidia(m); return; }
        var nome = MidiaDaConversa.NomeParaSalvar(m, carregada.Arquivo);
        var (filtro, extensao) = MidiaDaConversa.FiltroParaSalvar(nome);
        if (await Host.SalvarEAbrirAsync(carregada.Arquivo.Conteudo, nome, filtro, extensao) is { } erro && !Descartada) Notificacoes.Erro(erro);
    }

    // Sem o arquivo: a falha já está no balão; a que veio de outro clique ao mesmo tempo, ainda não.
    private void AvisarFalhaDaMidia(MidiaDaMensagem m)
    {
        if (_midiaFalhou.TryGetValue(m.Chave, out var falha)) Notificacoes.Erro(falha);
        else if (_midiaCarregando.Contains(m.Chave)) Notificacoes.Info("O arquivo ainda está chegando. Tente de novo em instantes.");
    }

    /// <summary>
    /// O áudio gravado: sobe como gravação (o CRM o converte para a nota de voz) e sai como resposta com o
    /// anexo — as MESMAS regras do Anexar (de outra pessoa, janela fechada e SMS o CRM recusa, e a frase
    /// aparece). Falso deixa o áudio no gravador; repetir reusa o anexo e o Id do envio.
    /// </summary>
    private async Task<bool> EnviarGravacaoAsync(ConversaDoWhatsApp c, GravadorDeAudio.Gravacao g)
    {
        // O gravador chama por Func (precisa da resposta), não por EventCallback: quem redesenha a
        // tela é este método — sem isto a mensagem enviada e a frase da recusa só apareciam na
        // releitura de 7 s (foto 55).
        try { return await SubirEEnviarGravacaoAsync(c, g); }
        finally { if (!Descartada) StateHasChanged(); }
    }

    private async Task<bool> SubirEEnviarGravacaoAsync(ConversaDoWhatsApp c, GravadorDeAudio.Gravacao g)
    {
        if (!Exigir(Permissao.EditarAgenda, "enviar um áudio") || c.Conversa is not { } conversa || _ocupado) return false;
        if (g.Anexo is null || g.ConversaDoAnexo != conversa.Id)
        {
            _ocupado = true;
            _recado = null;
            try
            {
                using var escopo = Escopo();
                using var fluxo = new MemoryStream(g.Bytes);
                g.Anexo = await escopo.ServiceProvider.GetRequiredService<WhatsAppDaRecepcaoService>()
                    .AnexarAsync(conversa, fluxo, MidiaDaConversa.NomeDaGravacao(g.Mime), g.Mime, gravacao: true);
                g.ConversaDoAnexo = conversa.Id;
            }
            catch (CrmIndisponivel ex)
            {
                Clinica.Application.Diagnostico.Registrar("Blazor — o CRM não recebeu o áudio do WhatsApp", ex);
                Recado($"O CRM não respondeu: {ex.Message} O áudio continua aqui; tente Enviar de novo.", "erro");
                return false;
            }
            catch (InvalidOperationException ex) // RecusaDoCrm
            {
                Recado(ex.Message, "erro");
                return false;
            }
            finally { _ocupado = false; }
            if (Descartada) return false;
        }
        var anexo = g.Anexo.Id;
        var nova = await GravarNoCrmAsync(s => s.ResponderAsync(conversa, "", anexo, g.IdDoEnvio), "Áudio enviado.", "enviar um áudio");
        return nova is not null;
    }
}
