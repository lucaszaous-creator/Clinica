import type { Acao, Contexto } from './paginas';
import './acoes-menu.css';

const h = (v: unknown) => String(v ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]!));
const atributos = (c: Contexto) => `data-escopo="${c.escopo}" data-contexto="${h(c.id)}"${c.tabela ? ` data-tabela="${h(c.tabela)}"` : ''}${c.linha ? ` data-linha="${h(c.linha)}"` : ''}`;
function botao(a: Acao, c: Contexto, classe: string, principal = false, menu = false) {
  return `<button type="button" class="botao ${a.estilo === 'perigo' ? 'perigo' : principal ? 'primario' : 'secundario'} ${classe}" ${menu ? 'role="menuitem" tabindex="-1"' : ''} data-comando="${h(a.chave)}" ${atributos(c)} ${a.habilitada === false || c.ocupado ? 'disabled' : ''}>${h(a.rotulo)}</button>`;
}

/** Mantém os mesmos comandos e contextos no DOM: o menu só muda a apresentação. */
export function menuAcoes(itens: Acao[], c: Contexto, titulo = 'Mais ações', classe = ''): string {
  const visiveis = itens.filter(a => a.visivel !== false);
  if (!visiveis.length) return '';
  const id = `acoes-${encodeURIComponent(JSON.stringify([c.escopo, c.id, c.tabela, c.linha, titulo, visiveis.map(a => a.chave)]))}`;
  const normais = visiveis.filter(a => a.estilo !== 'perigo');
  const perigosas = visiveis.filter(a => a.estilo === 'perigo');
  return `<span class="acoes-menu-grupo"><button type="button" class="botao secundario acoes-menu-abrir" data-abrir-acoes="${h(id)}" aria-haspopup="menu" aria-expanded="false" aria-controls="${h(id)}" ${c.ocupado ? 'disabled' : ''}>${h(titulo)}<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg></button><div id="${h(id)}" class="acoes-menu-painel" popover="auto" role="menu" aria-label="${h(titulo)}">${normais.map(a => botao(a, c, classe, false, true)).join('')}${normais.length && perigosas.length ? '<div class="acoes-menu-divisor" role="separator"></div>' : ''}${perigosas.map(a => botao(a, c, classe, false, true)).join('')}</div></span>`;
}

export function acoesCompactas(itens: Acao[], c: Contexto, classe = '', tituloMenu = 'Mais ações'): string {
  const visiveis = itens.filter(a => a.visivel !== false);
  if (visiveis.length <= 2) return visiveis.map((a, i) => botao(a, c, classe, a.estilo === 'primario' && !visiveis.slice(0, i).some(x => x.estilo === 'primario'))).join('');
  const principal = visiveis.find(a => a.estilo === 'primario' && a.habilitada !== false) ?? visiveis.find(a => a.estilo === 'primario');
  // Cancelar/Fechar fica à mão no formulário; na lista a segunda ação mantém a ordem do contrato.
  const secundaria = (c.escopo === 'dialogo' && !c.linha ? visiveis.find(a => /^(cancelar|fechar|voltar)$/i.test(a.rotulo)) : undefined)
    ?? visiveis.find(a => a !== principal && a.estilo !== 'perigo');
  const diretas = [principal, secundaria].filter((a): a is Acao => !!a);
  const restantes = visiveis.filter(a => !diretas.includes(a));
  return diretas.map(a => botao(a, c, classe, a === principal)).join('') + menuAcoes(restantes, c, tituloMenu, classe);
}

function acionador(painel: HTMLElement) {
  return document.querySelector<HTMLButtonElement>(`[data-abrir-acoes="${CSS.escape(painel.id)}"]`);
}
function fechar(painel: HTMLElement, devolverFoco = false) {
  if (painel.matches(':popover-open')) painel.hidePopover();
  const b = acionador(painel);
  b?.setAttribute('aria-expanded', 'false');
  if (devolverFoco) b?.focus({preventScroll: true});
}
function abrir(b: HTMLButtonElement, ultimo = false) {
  const painel = document.getElementById(b.dataset.abrirAcoes!);
  if (!painel || b.disabled) return;
  document.querySelectorAll<HTMLElement>('.acoes-menu-painel:popover-open').forEach(p => { if (p !== painel) fechar(p); });
  painel.showPopover();
  const r = b.getBoundingClientRect();
  const margem = 8;
  const espacoAbaixo = innerHeight - r.bottom - margem;
  const espacoAcima = r.top - margem;
  const abaixo = espacoAbaixo >= Math.min(painel.scrollHeight, 280) || espacoAbaixo >= espacoAcima;
  painel.style.maxHeight = `${Math.max(80, abaixo ? espacoAbaixo : espacoAcima)}px`;
  painel.style.left = `${Math.max(margem, Math.min(r.right - painel.offsetWidth, innerWidth - painel.offsetWidth - margem))}px`;
  painel.style.top = `${abaixo ? r.bottom + 4 : Math.max(margem, r.top - painel.offsetHeight - 4)}px`;
  b.setAttribute('aria-expanded', 'true');
  const itens = [...painel.querySelectorAll<HTMLButtonElement>('button:not(:disabled)')];
  itens[ultimo ? itens.length - 1 : 0]?.focus({preventScroll: true});
}

// Captura o Escape antes do diálogo: fechar o menu não pode descartar o formulário.
if (typeof document !== 'undefined') {
  document.addEventListener('click', e => {
    const alvo = e.target as Element;
    const b = alvo.closest<HTMLButtonElement>('[data-abrir-acoes]');
    if (b) {
      e.preventDefault(); e.stopPropagation();
      const painel = document.getElementById(b.dataset.abrirAcoes!);
      if (painel?.matches(':popover-open')) fechar(painel, true); else abrir(b);
      return;
    }
    const painel = alvo.closest<HTMLElement>('.acoes-menu-painel');
    if (painel && alvo.closest<HTMLButtonElement>('button[data-comando]:not(:disabled)')) fechar(painel);
  }, true);
  document.addEventListener('toggle', e => {
    const painel = e.target;
    if (painel instanceof HTMLElement && painel.classList.contains('acoes-menu-painel'))
      acionador(painel)?.setAttribute('aria-expanded', String(painel.matches(':popover-open')));
  }, true);
  document.addEventListener('keydown', e => {
    const alvo = e.target as Element;
    const b = alvo.closest<HTMLButtonElement>('[data-abrir-acoes]');
    if (b && ['ArrowDown', 'ArrowUp'].includes(e.key)) {
      e.preventDefault(); e.stopImmediatePropagation(); abrir(b, e.key === 'ArrowUp'); return;
    }
    const painel = alvo.closest<HTMLElement>('.acoes-menu-painel:popover-open');
    if (!painel) return;
    if (e.key === 'Escape') { e.preventDefault(); e.stopImmediatePropagation(); fechar(painel, true); return; }
    if (e.key === 'Tab') { fechar(painel, true); return; }
    if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(e.key)) return;
    e.preventDefault(); e.stopImmediatePropagation();
    const itens = [...painel.querySelectorAll<HTMLButtonElement>('button:not(:disabled)')];
    const i = itens.indexOf(document.activeElement as HTMLButtonElement);
    const proximo = e.key === 'Home' ? 0 : e.key === 'End' ? itens.length - 1 : e.key === 'ArrowUp' ? (i - 1 + itens.length) % itens.length : (i + 1) % itens.length;
    itens[proximo]?.focus();
  }, true);
  window.addEventListener('resize', () => document.querySelectorAll<HTMLElement>('.acoes-menu-painel:popover-open').forEach(p => fechar(p)));
  document.addEventListener('scroll', e => {
    if (e.target instanceof Element && e.target.closest('.acoes-menu-painel')) return;
    document.querySelectorAll<HTMLElement>('.acoes-menu-painel:popover-open').forEach(p => fechar(p));
  }, true);
}
