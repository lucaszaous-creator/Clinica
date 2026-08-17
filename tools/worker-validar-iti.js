/**
 * O Worker que faz o botão "Ler QR Code" do validador gov.br funcionar com as nossas
 * receitas — pronto para colar no Cloudflare. Roteiro de instalação em
 * docs/validar-pelo-qr-code.md.
 *
 * O CONTRATO (Guia de Orientações aos Desenvolvedores do VALIDAR, Capítulo IV)
 * ----------------------------------------------------------------------------
 * Depois de ler o QR, o validador NÃO baixa o arquivo: ele chama a URL do QR assim,
 *
 *     GET <url-do-qr>/?_format=application/validador-iti+json&_secretCode=<código>
 *
 * e espera um JSON com a URL do PDF assinado:
 *
 *     { "version": "1.0.0", "prescription": { "signatureFiles": [ { "url": "…" } ] } }
 *
 * Respostas de erro previstas pelo guia: 401 (código errado) e 404 (não existe).
 *
 * O <código> é o que a tela "Insira o código" pede. Aqui ele é o CÓDIGO DE CONFERÊNCIA
 * impresso no rodapé de toda folha ("Conferência AB3K7-9XQ2M"), que o app grava como
 * metadado do objeto na publicação — o Worker confere sem banco nenhum.
 *
 * Por que aceitar também o código EM BRANCO: o guia define _secretCode como "variável de
 * 0 a 64 caracteres" — vazio é contrato válido. E a nossa barreira de acesso não é o
 * código, é o TOKEN de 128 bits que já está na URL: quem leu o QR já tem o endereço do
 * arquivo, então recusar o campo vazio só criaria atrito sem proteger nada. O código é
 * conferido QUANDO informado, porque errar a digitação e validar mesmo assim ensinaria
 * que o campo não faz nada.
 *
 * O que este Worker NÃO muda: quem abre a URL num navegador continua recebendo o PDF,
 * como sempre — o QR das folhas já impressas continua funcionando na câmera. É o mesmo
 * endereço com duas respostas, e é por isso que dá para ligar isto DEPOIS, sem reimprimir
 * nem reassinar nada: a URL está selada dentro dos PDFs assinados e não muda nunca.
 */

const FORMATO_VALIDADOR = "application/validador-iti+json";

/** Compara códigos ignorando caixa, hífen e espaço — "ab3k7 9xq2m" confere com "AB3K7-9XQ2M". */
const normalizar = (texto) => (texto ?? "").replace(/[^0-9a-z]/gi, "").toUpperCase();

export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    // O validador acrescenta "/?" ao endereço do QR, então o caminho chega com uma barra
    // sobrando no fim: /r/AB/TOKEN.pdf/ . Sem esta limpeza, nada seria encontrado.
    const chave = url.pathname.replace(/\/+$/, "").replace(/^\/+/, "");

    if (request.method === "OPTIONS")
      return new Response(null, { headers: cors() });

    // ---- O contrato do validador ----
    if (url.searchParams.get("_format") === FORMATO_VALIDADOR) {
      const objeto = await env.BUCKET.head(chave);
      if (!objeto) return new Response(null, { status: 404, headers: cors() });

      const esperado = normalizar(objeto.customMetadata?.codigo);
      const informado = normalizar(url.searchParams.get("_secretCode"));

      // Código informado e errado: 401, como o guia manda. Vazio passa (ver cabeçalho).
      // Objeto antigo sem metadado também passa: ele foi publicado antes de o app gravar
      // o código, e recusá-lo deixaria toda receita já impressa fora do fluxo.
      if (informado.length > 0 && esperado.length > 0 && informado !== esperado)
        return new Response(null, { status: 401, headers: cors() });

      return new Response(
        JSON.stringify({
          version: "1.0.0",
          prescription: { signatureFiles: [{ url: `${url.origin}/${chave}` }] },
        }),
        { headers: { "content-type": FORMATO_VALIDADOR, ...cors() } });
    }

    // ---- Fora do contrato: entrega o PDF, como o domínio sempre entregou ----
    const objeto = await env.BUCKET.get(chave);
    if (!objeto)
      return new Response("Documento não encontrado ou fora do ar.", {
        status: 404, headers: cors() });

    return new Response(objeto.body, {
      headers: {
        "content-type": objeto.httpMetadata?.contentType ?? "application/pdf",
        "content-disposition": 'inline; filename="documento.pdf"',
        // O validador busca o PDF a partir da página dele (outra origem): sem CORS a
        // busca falha DEPOIS do código certo, que é o pior lugar para falhar.
        ...cors(),
      },
    });
  },
};

const cors = () => ({
  "access-control-allow-origin": "*",
  "access-control-allow-methods": "GET, HEAD, OPTIONS",
});
