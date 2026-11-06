// A gestão no navegador (nov/2026): o que o hospedeiro das telas pede ao navegador — baixar um
// arquivo, escolher um arquivo para enviar, copiar texto, abrir um endereço. Os bytes vêm e vão
// por STREAM (DotNetStreamReference / IJSStreamReference), nunca numa mensagem gigante do WebSocket.
window.gestaoWeb = {
  async baixar(nome, tipo, referencia, abrir) {
    const dados = await referencia.arrayBuffer();
    const url = URL.createObjectURL(new Blob([dados], { type: tipo }));
    try {
      if (abrir) {
        // Prévia de PDF: abre numa aba; se o navegador bloquear, cai no download.
        // Sem 'noopener' no terceiro argumento: com ele o window.open devolve null sempre. O
        // opener é cortado à mão logo depois (o blob é da mesma origem).
        const janela = window.open(url, '_blank');
        if (janela) { janela.opener = null; setTimeout(() => URL.revokeObjectURL(url), 60000); return; }
      }
      const a = document.createElement('a');
      a.href = url; a.download = nome; a.rel = 'noopener';
      document.body.appendChild(a); a.click(); a.remove();
    } finally {
      if (!abrir) setTimeout(() => URL.revokeObjectURL(url), 10000);
    }
  },

  escolher(aceitar, teto) {
    return new Promise(resolver => {
      const entrada = document.createElement('input');
      entrada.type = 'file';
      if (aceitar) entrada.accept = aceitar;
      let respondido = false;
      const responder = valor => { if (!respondido) { respondido = true; resolver(valor); } };
      entrada.addEventListener('change', () => {
        const arquivo = entrada.files && entrada.files[0];
        if (!arquivo) return responder(null);
        if (arquivo.size > teto) return responder({ erro: 'O arquivo passa do tamanho aceito (' + Math.round(teto / 1048576) + ' MB).' });
        responder({ nome: arquivo.name, conteudo: DotNet.createJSStreamReference(arquivo) });
      });
      entrada.addEventListener('cancel', () => responder(null));
      entrada.click();
    });
  },

  async copiar(texto) {
    try { await navigator.clipboard.writeText(texto); return true; }
    catch {
      const area = document.createElement('textarea');
      area.value = texto; area.setAttribute('readonly', ''); area.style.position = 'fixed'; area.style.opacity = '0';
      document.body.appendChild(area); area.select();
      let ok = false;
      try { ok = document.execCommand('copy'); } catch { ok = false; }
      area.remove();
      return ok;
    }
  },

  abrir(url) {
    if (!/^https:\/\//i.test(url)) return;
    window.open(url, '_blank', 'noopener,noreferrer');
  }
};
