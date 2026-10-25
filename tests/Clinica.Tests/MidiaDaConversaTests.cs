using System.Buffers.Binary;
using System.Text;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A mídia do WhatsApp na tela (24/10/2026): o que <see cref="MidiaDaConversa"/> decide — a rota no
/// CRM por direção, o tipo, o que vira <c>data:</c>, a duração lida do arquivo, os rótulos, o nome
/// para salvar — e a <see cref="MemoriaDeMidia"/> que segura a releitura de 7 s.
/// </summary>
public sealed class MidiaDaConversaTests
{
    private static readonly DateTimeOffset Quando = new(2026, 10, 23, 13, 12, 0, TimeSpan.Zero);

    private static AppMessage Msg(string direcao, string tipo, string texto = "", string? anexo = null, string? nome = null, string id = "m1")
        => new(id, direcao, texto, tipo, direcao == "out" ? "ana" : null, Quando, direcao == "in" ? "received" : "seen", AttachmentId: anexo, FileName: nome);

    // ---------------------------------------------------------------- rota e tipo

    [Fact]
    public void A_recebida_vem_da_rota_da_mensagem_e_a_enviada_da_rota_do_anexo()
    {
        Assert.Equal("/api/conversations/c%2F1/media/wamid.9?download=true",
            MidiaDaConversa.Rota("c/1", Msg("in", "IMAGE", id: "wamid.9")));
        Assert.Equal("/api/attachments/abc123?download=true", MidiaDaConversa.Rota("c1", Msg("out", "AUDIO", anexo: "abc123")));
    }

    [Fact]
    public void Figurinha_e_imagem_que_se_chama_figurinha_em_toda_frase()
    {
        var m = MidiaDaConversa.Da("c1", Msg("in", "IMAGE") with { Sticker = true })!;
        Assert.Equal((TipoDeMidia.Imagem, true, true, "Figurinha"), (m.Tipo, m.Figurinha, m.CarregaSozinha, m.Rotulo));
        var webp = new ArquivoDaMidia([1, 2, 3], "image/webp", null);
        Assert.StartsWith("data:image/webp;base64,", MidiaDaConversa.Endereco(m.Tipo, webp));
        Assert.Equal("Figurinha recebida · 3 bytes", MidiaDaConversa.Descricao(m, MidiaDaConversa.Preparar(m, webp)));
        Assert.StartsWith("figurinha-", MidiaDaConversa.NomeParaSalvar(m, webp));
        Assert.Equal("Não foi possível carregar a figurinha.", MidiaDaConversa.Falha(m, ""));
        // A foto comum continua foto, e a marca numa mensagem que não é imagem não muda nada.
        var foto = MidiaDaConversa.Da("c1", Msg("in", "IMAGE"))!;
        Assert.Equal((false, "Foto", "Não foi possível carregar a foto."), (foto.Figurinha, foto.Rotulo, MidiaDaConversa.Falha(foto, "")));
        Assert.False(MidiaDaConversa.Da("c1", Msg("in", "AUDIO") with { Sticker = true })!.Figurinha);
    }

    [Fact]
    public void Sem_arquivo_nao_ha_rota()
    {
        Assert.Null(MidiaDaConversa.Rota("c1", Msg("in", "TEXT", "oi")));
        // A saída automática com tipo de mídia e sem anexo: o CRM não a serve pela rota da recebida.
        Assert.Null(MidiaDaConversa.Rota("c1", Msg("out", "IMAGE")));
        Assert.Null(MidiaDaConversa.Da("c1", Msg("out", "TEMPLATE", "Olá")));
    }

    [Theory]
    [InlineData("IMAGE", TipoDeMidia.Imagem, "Foto")]
    [InlineData("audio", TipoDeMidia.Audio, "Áudio")]
    [InlineData("VIDEO", TipoDeMidia.Video, "Vídeo")]
    [InlineData("DOCUMENT", TipoDeMidia.Documento, "Documento")]
    public void O_tipo_do_CRM_vira_o_tipo_e_o_rotulo(string tipo, TipoDeMidia esperado, string rotulo)
    {
        var m = MidiaDaConversa.Da("c1", Msg("in", tipo))!;
        Assert.Equal(esperado, m.Tipo);
        Assert.Equal(rotulo, m.Rotulo);
        Assert.Equal("c1/m1", m.Chave);
        Assert.Equal(m.Tipo is TipoDeMidia.Imagem or TipoDeMidia.Audio, m.CarregaSozinha);
    }

    [Fact]
    public void Saida_com_anexo_sem_tipo_de_midia_e_documento()
    {
        var m = MidiaDaConversa.Da("c1", Msg("out", "TEMPLATE", "Segue o documento", anexo: "a1", nome: "receita.pdf"))!;
        Assert.Equal(TipoDeMidia.Documento, m.Tipo);
        Assert.Equal("receita.pdf", m.Nome);
    }

    // ---------------------------------------------------------------- o que se mostra

    [Fact]
    public void So_vira_endereco_o_arquivo_do_tipo_que_a_mensagem_diz()
    {
        var png = new ArquivoDaMidia([1, 2, 3], "image/png", null);
        Assert.Equal("data:image/png;base64,AQID", MidiaDaConversa.Endereco(TipoDeMidia.Imagem, png));
        Assert.Null(MidiaDaConversa.Endereco(TipoDeMidia.Imagem, new ArquivoDaMidia([1], "application/pdf", null)));
        Assert.Equal("data:audio/ogg;base64,AQ==", MidiaDaConversa.Endereco(TipoDeMidia.Audio, new ArquivoDaMidia([1], "audio/ogg; codecs=opus", null)));
        Assert.Null(MidiaDaConversa.Endereco(TipoDeMidia.Documento, new ArquivoDaMidia([1], "application/pdf", null)));
        Assert.Null(MidiaDaConversa.Endereco(TipoDeMidia.Imagem, new ArquivoDaMidia([], "image/png", null)));
    }

    [Theory]
    [InlineData(0.0, "0:00")]
    [InlineData(42.4, "0:42")]
    [InlineData(65.0, "1:05")]
    [InlineData(725.0, "12:05")]
    [InlineData(3723.0, "1:02:03")]
    public void O_tempo_se_le_como_no_WhatsApp(double segundos, string esperado)
        => Assert.Equal(esperado, MidiaDaConversa.Tempo(segundos));

    [Fact]
    public void Sem_duracao_o_audio_e_so_Audio()
    {
        Assert.Equal("Áudio", MidiaDaConversa.RotuloDoAudio(null));
        Assert.Equal("Áudio", MidiaDaConversa.RotuloDoAudio(double.NaN));
        Assert.Equal("Áudio 0:42", MidiaDaConversa.RotuloDoAudio(42));
        Assert.Equal("", MidiaDaConversa.Tempo(-1));
    }

    [Theory]
    [InlineData(1L, "1 byte")]
    [InlineData(820L, "820 bytes")]
    [InlineData(12_400L, "12 KB")]
    [InlineData(1_468_006L, "1,4 MB")]
    [InlineData(16L * 1024 * 1024, "16 MB")]
    public void O_tamanho_e_em_pt_BR(long bytes, string esperado) => Assert.Equal(esperado, MidiaDaConversa.Tamanho(bytes));

    [Fact]
    public void A_descricao_diz_o_que_e_quem_mandou_e_o_tamanho()
    {
        var foto = MidiaDaConversa.Da("c1", Msg("in", "IMAGE"))!;
        Assert.Equal("Foto recebida", MidiaDaConversa.Descricao(foto, null));
        var pdf = MidiaDaConversa.Da("c1", Msg("out", "DOCUMENT", anexo: "a1", nome: "orientacoes.pdf"))!;
        Assert.Equal("PDF enviado · 12 KB", MidiaDaConversa.Descricao(pdf, new MidiaCarregada(new ArquivoDaMidia(new byte[12_400], "application/pdf", null), null, null)));
        var audio = MidiaDaConversa.Da("c1", Msg("in", "AUDIO"))!;
        Assert.Equal("Áudio recebido · 0:06 · 2 KB", MidiaDaConversa.Descricao(audio, new MidiaCarregada(new ArquivoDaMidia(new byte[2048], "audio/ogg", null), "data:", 6.2)));
    }

    [Theory]
    [InlineData("Pedido médico.pdf", null, "PDF")]
    [InlineData("planilha.xlsx", null, "XLSX")]
    [InlineData(null, "application/pdf", "PDF")]
    [InlineData("sem-extensao", "application/octet-stream", "DOC")]
    [InlineData("estranho.extensaolonga", null, "DOC")]
    public void A_sigla_do_documento(string? nome, string? mime, string esperado) => Assert.Equal(esperado, MidiaDaConversa.Sigla(nome, mime));

    [Fact]
    public void A_previa_da_lista_troca_o_tipo_cru_pela_palavra()
    {
        Assert.Equal("Áudio", MidiaDaConversa.PreviaDaLista("AUDIO"));
        Assert.Equal("Foto", MidiaDaConversa.PreviaDaLista(" IMAGE "));
        // O paciente que escreveu "Video" é texto, não tipo.
        Assert.Equal("Video", MidiaDaConversa.PreviaDaLista("Video"));
        Assert.Equal("Bom dia", MidiaDaConversa.PreviaDaLista("Bom dia"));
    }

    [Fact]
    public void A_falha_diz_o_que_nao_carregou_e_por_que()
    {
        Assert.Equal("Não foi possível carregar o vídeo: O CRM não respondeu.", MidiaDaConversa.Falha(TipoDeMidia.Video, "O CRM não respondeu."));
        Assert.Equal("Não foi possível carregar a foto.", MidiaDaConversa.Falha(TipoDeMidia.Imagem, " "));
    }

    // ---------------------------------------------------------------- salvar

    [Fact]
    public void O_nome_para_salvar_e_o_do_CRM_limpo_ou_o_rotulo_com_a_hora()
    {
        var doc = MidiaDaConversa.Da("c1", Msg("in", "DOCUMENT", nome: "exame.pdf"))!;
        Assert.Equal("Pedido-medico.pdf", MidiaDaConversa.NomeParaSalvar(doc, new ArquivoDaMidia([1], "application/pdf", "\"Pedido/medico.pdf\"")));
        Assert.Equal("exame.pdf", MidiaDaConversa.NomeParaSalvar(doc, new ArquivoDaMidia([1], "application/pdf", null)));

        var foto = MidiaDaConversa.Da("c1", Msg("in", "IMAGE"))!;
        var hora = Quando.ToLocalTime().ToString("yyyy-MM-dd-HHmm");
        Assert.Equal($"foto-{hora}.jpg", MidiaDaConversa.NomeParaSalvar(foto, new ArquivoDaMidia([1], "image/jpeg", null)));
        Assert.Equal("gravacao.ogg", MidiaDaConversa.NomeParaSalvar(foto, new ArquivoDaMidia([1], "audio/ogg", "gravacao")));
        Assert.Equal($"foto-{hora}.bin", MidiaDaConversa.NomeParaSalvar(foto, new ArquivoDaMidia([1], "application/x-nada", null)));
    }

    [Fact]
    public void O_filtro_da_janela_de_salvar_segue_a_extensao()
    {
        Assert.Equal(("Arquivo PDF (*.pdf)|*.pdf", ".pdf"), MidiaDaConversa.FiltroParaSalvar("exame.pdf"));
        Assert.Equal(("Todos os arquivos (*.*)|*.*", ""), MidiaDaConversa.FiltroParaSalvar("sem-extensao"));
    }

    [Theory]
    [InlineData("audio/webm;codecs=opus", "gravacao.webm")]
    [InlineData("audio/ogg", "gravacao.ogg")]
    [InlineData("audio/mp4", "gravacao.m4a")]
    [InlineData("", "gravacao.webm")]
    public void O_nome_da_gravacao_segue_o_formato_do_navegador(string mime, string esperado)
        => Assert.Equal(esperado, MidiaDaConversa.NomeDaGravacao(mime));

    // ---------------------------------------------------------------- duração

    [Fact]
    public void A_duracao_do_WAV_vem_do_tamanho_dos_dados()
    {
        var d = MidiaDaConversa.Duracao(PreviaBlazor.MidiaDeExemplo.NotaDeVoz.Conteudo, "audio/wav");
        Assert.NotNull(d);
        Assert.Equal(6.5, d!.Value, 2);
    }

    [Fact]
    public void A_duracao_da_nota_de_voz_OGG_Opus_vem_da_ultima_pagina_menos_o_pre_skip()
    {
        var ogg = Ogg(preSkip: 312, amostrasNoFim: 48000L * 42 + 312);
        Assert.Equal(42.0, MidiaDaConversa.Duracao(ogg, "audio/ogg")!.Value, 3);
    }

    [Fact]
    public void Arquivo_que_nao_se_le_nao_tem_duracao()
    {
        Assert.Null(MidiaDaConversa.Duracao([1, 2, 3], "audio/mpeg"));
        Assert.Null(MidiaDaConversa.Duracao("OggS"u8.ToArray(), "audio/ogg"));
        Assert.Null(MidiaDaConversa.Duracao("RIFF\0\0\0\0WAVEfmt "u8.ToArray(), "audio/wav"));
    }

    /// <summary>Um OGG mínimo: a página do OpusHead e a última página com a posição (granule).</summary>
    private static byte[] Ogg(int preSkip, long amostrasNoFim)
    {
        static byte[] Pagina(long granulo, byte[] pacote)
        {
            var p = new byte[27 + 1 + pacote.Length];
            "OggS"u8.CopyTo(p);
            BinaryPrimitives.WriteInt64LittleEndian(p.AsSpan(6), granulo);
            p[26] = 1;
            p[27] = (byte)pacote.Length;
            pacote.CopyTo(p, 28);
            return p;
        }
        var cabecalho = new byte[19];
        "OpusHead"u8.CopyTo(cabecalho);
        cabecalho[8] = 1; cabecalho[9] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(cabecalho.AsSpan(10), (ushort)preSkip);
        BinaryPrimitives.WriteUInt32LittleEndian(cabecalho.AsSpan(12), 48000);
        return [.. Pagina(0, cabecalho), .. Pagina(0, Encoding.ASCII.GetBytes("OpusTags")), .. Pagina(amostrasNoFim, [1, 2, 3])];
    }

    // ---------------------------------------------------------------- a linha do tempo

    [Fact]
    public void Na_linha_do_tempo_a_midia_sobe_ao_balao_e_o_texto_e_so_a_legenda()
    {
        var conversa = new AppConversation("c1", "Roberto", "5522", "whatsapp", "s", "Recepção", "ana", false, 1, Quando, Quando, null, "",
        [
            Msg("in", "IMAGE", id: "a"),
            Msg("in", "AUDIO", id: "b") with { At = Quando.AddMinutes(1) },
            Msg("out", "DOCUMENT", "Seguem as orientações", anexo: "x1", nome: "orientacoes.pdf", id: "c") with { At = Quando.AddMinutes(2) },
            Msg("in", "TEXT", "Obrigado", id: "d") with { At = Quando.AddMinutes(3) },
        ], [], [], [], null);

        var baloes = ConversasNaTela.LinhaDoTempo(conversa, 4, Quando.AddMinutes(5))
            .Where(i => i.Tipo is TipoNaLinhaDoTempo.Recebida or TipoNaLinhaDoTempo.Enviada).ToList();

        Assert.Equal([TipoDeMidia.Imagem, TipoDeMidia.Audio, TipoDeMidia.Documento], baloes.Take(3).Select(b => b.Midia!.Tipo));
        Assert.Null(baloes[3].Midia);
        Assert.Equal("", baloes[0].Texto); // sem "Mensagem sem texto" debaixo da foto
        Assert.Equal("Seguem as orientações", baloes[2].Texto);
        Assert.All(baloes, b => Assert.Null(b.Anexo)); // o cartão do arquivo já mostra o nome
        Assert.Equal("c1/c", baloes[2].Midia!.Chave);
    }

    // ---------------------------------------------------------------- a memória

    private static MidiaCarregada De(int bytes) => new(new ArquivoDaMidia(new byte[bytes], "image/png", null), null, null);

    [Fact]
    public void A_memoria_descarta_a_usada_ha_mais_tempo_quando_enche()
    {
        var memoria = new MemoriaDeMidia(teto: 300);
        memoria.Guardar("a", De(100));
        memoria.Guardar("b", De(100));
        memoria.Guardar("c", De(100));
        Assert.NotNull(memoria.Ler("a")); // "a" passa a ser a mais recente
        memoria.Guardar("d", De(100));

        Assert.Null(memoria.Ler("b"));
        Assert.NotNull(memoria.Ler("a"));
        Assert.NotNull(memoria.Ler("c"));
        Assert.NotNull(memoria.Ler("d"));
        Assert.Equal(300, memoria.Total);
    }

    [Fact]
    public void A_memoria_conta_o_endereco_e_nao_guarda_o_que_passa_do_teto()
    {
        var memoria = new MemoriaDeMidia(teto: 100);
        memoria.Guardar("a", new MidiaCarregada(new ArquivoDaMidia(new byte[10], "image/png", null), new string('x', 20), null));
        Assert.Equal(50, memoria.Total);
        memoria.Guardar("grande", De(101));
        Assert.Null(memoria.Ler("grande"));
        Assert.Equal(1, memoria.Quantidade);
        memoria.Guardar("a", De(30)); // trocar não conta duas vezes
        Assert.Equal(30, memoria.Total);
    }

    [Fact]
    public void O_teto_da_memoria_cabe_o_maior_arquivo_que_o_CRM_serve_com_o_endereco()
        => Assert.True(MidiaDaConversa.Teto + (MidiaDaConversa.Teto + 2) / 3 * 4 * 2L <= MidiaDaConversa.TetoDaMemoria);
}
