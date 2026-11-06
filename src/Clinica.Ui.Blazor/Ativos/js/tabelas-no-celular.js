// Tabelas no celular (nov/2026). No celular, cada linha de `table.lista` vira um cartão, e
// cada célula precisa dizer DE QUE COLUNA ela é ("Vencimento", "Valor"…), porque o cabeçalho
// da tabela some. Este script copia o texto de cada <th> para o `data-rotulo` das células da
// mesma coluna; o CSS (clinica.css, seção "Celular") o mostra antes do valor.
//
// Por que script e não Razor: são mais de 60 tabelas, e escrever o rótulo à mão em cada <td>
// seria a segunda cópia do cabeçalho em cada uma — a que a próxima coluna nova esquece.
// Aqui o cabeçalho é a fonte única, e tabela nova entra sozinha.
//
// Os `role` devolvem a semântica de tabela que o `display:block` do cartão tira dos leitores
// de tela. O observador não olha atributos, então gravar `data-rotulo` não o redispara.
(() => {
    function rotular(tabela) {
        const ths = tabela.querySelectorAll(':scope > thead > tr:last-child > th');
        if (!ths.length) return;
        const nomes = [];
        ths.forEach(th => {
            const nome = (th.textContent || '').replace(/\s+/g, ' ').trim();
            for (let i = 0; i < (th.colSpan || 1); i++) nomes.push(i === 0 ? nome : '');
        });
        if (tabela.getAttribute('role') !== 'table') tabela.setAttribute('role', 'table');
        tabela.querySelectorAll(':scope > thead > tr, :scope > tbody > tr').forEach(tr => {
            if (tr.getAttribute('role') !== 'row') tr.setAttribute('role', 'row');
            let coluna = 0;
            for (const celula of tr.children) {
                if (celula.tagName === 'TH') {
                    if (celula.getAttribute('role') !== 'columnheader') celula.setAttribute('role', 'columnheader');
                } else {
                    const rotulo = nomes[coluna] || '';
                    if (celula.getAttribute('data-rotulo') !== rotulo) celula.setAttribute('data-rotulo', rotulo);
                    if (celula.getAttribute('role') !== 'cell') celula.setAttribute('role', 'cell');
                }
                coluna += celula.colSpan || 1;
            }
        });
    }

    let agendado = false;
    function varrer() {
        agendado = false;
        document.querySelectorAll('table.lista').forEach(rotular);
    }
    new MutationObserver(() => {
        if (agendado) return;
        agendado = true;
        requestAnimationFrame(varrer);
    }).observe(document.documentElement, { childList: true, subtree: true, characterData: true });
    varrer();
})();
