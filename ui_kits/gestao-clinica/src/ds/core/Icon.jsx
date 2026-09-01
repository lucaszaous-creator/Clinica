import React from 'react';
import * as Lucide from 'lucide-react';

/* Ícones = lucide, sempre (novas-telas.md §6). Esta é a ÚNICA peça que muda entre a
   referência HTML (UMD `lucide` global) e a implementação React: lá o ícone é montado
   por `lucide.createIcons()` sobre um <i data-lucide>; aqui vem do pacote `lucide-react`,
   com os MESMOS nomes kebab-case, stroke 2. Nenhum outro componente do DS muda.

   Por que o pacote e não o CDN: sem o UMD carregado, o Icon da referência não desenha
   NADA e não quebra — a lupa da busca, os ícones da sidebar e o sino da topbar somem sem
   um único erro. É a causa nº 1 de "tela sem ícones". Com `import`, um ícone que falta
   quebra o build; e um NOME errado (que o import não pega, porque a resolução é por
   string) vira erro no console + um quadrado tracejado NA TELA — falha nunca é exibida
   como sucesso. `npm run verificar-icones` pega os dois casos antes de abrir a tela. */

const paraPascal = nome => String(nome || '')
  .split('-')
  .filter(Boolean)
  .map(p => p[0].toUpperCase() + p.slice(1))
  .join('');

/* `icons` traz os nomes CANÔNICOS; os apelidos que o lucide manteve por compatibilidade
   (check-circle, alert-triangle, more-vertical, loader-2 — todos usados pela referência)
   só existem como export nomeado. Procurar nos dois é o que faz o kebab-case da referência
   HTML valer aqui sem reescrever tela nenhuma. */
export function Icon({ name, size = 16, strokeWidth = 2, style }) {
  const chave = paraPascal(name);
  const Glifo = Lucide.icons[chave] || Lucide[chave];
  const base = {
    display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
    width: size, height: size, flexShrink: 0, ...style,
  };

  if (!Glifo) {
    if (typeof console !== 'undefined') {
      console.error(
        `Icon: "${name}" não existe no lucide-react — confira o nome em https://lucide.dev/icons. ` +
        'O ícone aparece como quadrado tracejado até ser corrigido (novas-telas.md §6).');
    }
    return <span aria-hidden="true" title={`ícone desconhecido: ${name}`}
      style={{ ...base, border: '1px dashed var(--danger)', borderRadius: 2, boxSizing: 'border-box' }} />;
  }

  return <span aria-hidden="true" style={base}>
    <Glifo size={size} strokeWidth={strokeWidth} absoluteStrokeWidth={false} />
  </span>;
}
