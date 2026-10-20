// O MOTION DESIGN da videoaula (out/2026). Recebe a tela real gravada (o "bruto") e a linha
// do tempo do roteiro (gravar.js) e monta, quadro a quadro:
//  - a ABERTURA: marca, módulo, título da aula e a lista do que ela ensina;
//  - a JANELA do app flutuando sobre o fundo da marca, com uma CÂMERA que vai até cada
//    função (zoom e enquadramento) e volta ao todo para mostrar o resultado do clique;
//  - o DESTAQUE animado no elemento, o CURSOR desenhado e o toque do clique;
//  - o PAINEL lateral: a função da vez (número, título e o que fazer) e o índice da aula;
//  - o FECHAMENTO: o resumo do que foi visto.
// Nada aqui é captura de tela inventada: a imagem do app é a gravação da tela de verdade.
import React from 'react';
import { AbsoluteFill, Easing, Img, OffthreadVideo, Sequence, interpolate, spring, staticFile, useCurrentFrame } from 'remotion';
import tempos from './tempos.json';

const { fps: FPS, abertura: ABERTURA, fechamento: FECHAMENTO } = tempos;
const W = 1920, H = 1080;
const PAINEL_X = 64, PAINEL_W = 400;
const JAN_X = 500, JAN_W = 1356, BARRA = 38;
const CW = JAN_W, CH = Math.round(JAN_W * 9 / 16);
const JAN_H = CH + BARRA, JAN_Y = Math.round((H - JAN_H) / 2) + 6;
const FOCO = { x: CW / 2, y: CH * 0.46 };
const FONTE = 'Inter, "Segoe UI", system-ui, sans-serif';
const COR = { fundo1: '#061A4F', fundo2: '#0E3192', fundo3: '#2F56C4', ambar: '#FBBF24', ambarForte: '#F59E0B', claro: '#DCE5FF', suave: '#9DB0E6', verde: '#4ADE80' };
const suave = Easing.bezier(0.45, 0.05, 0.25, 1);
const lim = (v, a, b) => Math.max(a, Math.min(b, v));
const mix = (a, b, p) => a + (b - a) * p;

// ---------- câmera e cursor (no espaço da tela do app, em pixels do conteúdo) ----------
function escalaDaTela(tela) { return CW / tela.largura; }
function retangulo(r, k) { return { x: r.x * k, y: r.y * k, w: r.w * k, h: r.h * k }; }
const VISAO = { s: 1, x: FOCO.x, y: FOCO.y };
function enquadrar(c) {
  const s = c.s;
  return { s, x: lim(c.x, FOCO.x / s, CW - (CW - FOCO.x) / s), y: lim(c.y, FOCO.y / s, CH - (CH - FOCO.y) / s) };
}
function camDoPasso(p, k) {
  if (!p.rect) return VISAO;
  const r = retangulo(p.rect, k);
  const s = lim(Math.min(CW * 0.5 / Math.max(r.w, 1), CH * 0.42 / Math.max(r.h, 1)), 1, 1.65);
  return enquadrar({ s, x: r.x + r.w / 2, y: r.y + r.h / 2 });
}
function quadrosChave(passos, k) {
  const q = [{ t: -10, cam: VISAO }];
  for (const p of passos) {
    q.push({ t: p.inicio, cam: camDoPasso(p, k) });
    if (p.clique != null && ['clicar', 'marcar', 'selecionar'].includes(p.acao)) q.push({ t: p.clique + 0.45, cam: VISAO });
  }
  return q;
}
function camEm(q, t) {
  let i = 0;
  while (i + 1 < q.length && q[i + 1].t <= t) i++;
  if (i === 0) return q[0].cam;
  const de = q[i - 1].cam, para = q[i].cam;
  const p = suave(lim((t - q[i].t) / 1.0, 0, 1));
  return { s: mix(de.s, para.s, p), x: mix(de.x, para.x, p), y: mix(de.y, para.y, p) };
}
const naJanela = (cam, x, y) => ({ x: FOCO.x + (x - cam.x) * cam.s, y: FOCO.y + (y - cam.y) * cam.s });
function pontosDoCursor(passos, k) {
  const q = [{ t: -10, x: CW * 0.72, y: CH * 0.86 }];
  for (const p of passos) {
    if (!p.rect) continue;
    const r = retangulo(p.rect, k);
    q.push({ t: p.inicio + 0.15, x: r.x + Math.min(r.w / 2, 70), y: r.y + r.h * 0.6 });
  }
  return q;
}
function cursorEm(q, t) {
  let i = 0;
  while (i + 1 < q.length && q[i + 1].t <= t) i++;
  if (i === 0) return q[0];
  const de = q[i - 1], para = q[i];
  const p = suave(lim((t - para.t) / 0.85, 0, 1));
  return { x: mix(de.x, para.x, p), y: mix(de.y, para.y, p) };
}

// ---------- peças ----------
const Fundo = () => {
  const f = useCurrentFrame();
  const bolha = (cx, cy, r, cor, v, fase) => (
    <div style={{ position: 'absolute', left: cx + Math.sin(f / v + fase) * 60 - r, top: cy + Math.cos(f / (v * 1.3) + fase) * 40 - r, width: r * 2, height: r * 2, borderRadius: '50%', background: cor, filter: 'blur(90px)', opacity: 0.55 }} />
  );
  return (
    <AbsoluteFill style={{ background: `linear-gradient(135deg, ${COR.fundo1} 0%, ${COR.fundo2} 55%, ${COR.fundo3} 100%)`, overflow: 'hidden' }}>
      {bolha(300, 220, 260, '#3B82F6', 140, 0)}
      {bolha(1650, 900, 320, '#1D4ED8', 170, 2)}
      {bolha(1500, 160, 200, '#60A5FA', 120, 4)}
      <AbsoluteFill style={{ backgroundImage: 'radial-gradient(rgba(255,255,255,0.07) 1.4px, transparent 1.4px)', backgroundSize: '34px 34px' }} />
    </AbsoluteFill>
  );
};

const Marca = ({ largura = 200, style }) => <Img src={staticFile('marca-branca.png')} style={{ width: largura, ...style }} />;

const Cursor = ({ x, y, clique }) => (
  <div style={{ position: 'absolute', left: x - 4, top: y - 3, width: 34, height: 34, filter: 'drop-shadow(0 4px 6px rgba(0,0,0,0.35))', transform: `scale(${clique ? 0.88 : 1})` }}>
    <svg viewBox="0 0 26 26" width="34" height="34"><path d="M4 2 L4 21 L9.2 16.4 L12.6 24 L15.8 22.6 L12.5 15.2 L19.5 15.2 Z" fill="#fff" stroke="#0F172A" strokeWidth="1.5" strokeLinejoin="round" /></svg>
  </div>
);

// ---------- abertura ----------
const Abertura = ({ aula }) => {
  const f = useCurrentFrame();
  const sp = (atraso, cfg = {}) => spring({ frame: f - atraso * FPS, fps: FPS, config: { damping: 16, mass: 0.7, ...cfg } });
  const saida = interpolate(f, [(ABERTURA - 0.7) * FPS, ABERTURA * FPS], [0, 1], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp', easing: suave });
  const palavras = aula.titulo.split(' ');
  const funcoes = aula.funcoes.slice(0, 9);
  const resto = aula.funcoes.length - funcoes.length;
  return (
    <AbsoluteFill style={{ fontFamily: FONTE, color: '#fff', padding: '0 150px', justifyContent: 'center', opacity: 1 - saida, transform: `translateY(${-60 * saida}px)` }}>
      <div style={{ opacity: sp(0.1), transform: `scale(${0.8 + 0.2 * sp(0.1)})`, transformOrigin: 'left center' }}><Marca largura={230} /></div>
      <div style={{ marginTop: 46, fontSize: 22, letterSpacing: '0.22em', fontWeight: 700, color: COR.ambar, opacity: sp(0.45), transform: `translateX(${-30 * (1 - sp(0.45))}px)` }}>
        TREINAMENTO · {aula.categoria.toUpperCase()}
      </div>
      <div style={{ marginTop: 14, fontSize: 84, fontWeight: 800, lineHeight: 1.05, display: 'flex', flexWrap: 'wrap', gap: '0 22px', maxWidth: 1500 }}>
        {palavras.map((p, i) => {
          const v = sp(0.7 + i * 0.09, { damping: 14 });
          return <span key={i} style={{ display: 'inline-block', opacity: v, transform: `translateY(${50 * (1 - v)}px)` }}>{p}</span>;
        })}
      </div>
      <div style={{ marginTop: 24, fontSize: 28, lineHeight: 1.45, color: COR.claro, maxWidth: 1280, opacity: sp(1.4), transform: `translateY(${20 * (1 - sp(1.4))}px)` }}>{aula.descricao}</div>
      <div style={{ marginTop: 50, fontSize: 18, letterSpacing: '0.2em', fontWeight: 700, color: COR.suave, opacity: sp(2.0) }}>NESTA AULA</div>
      <div style={{ marginTop: 16, display: 'flex', flexWrap: 'wrap', gap: 12, maxWidth: 1620 }}>
        {funcoes.map((fn, i) => {
          const v = sp(2.2 + i * 0.13, { damping: 13 });
          return (
            <div key={i} style={{ opacity: v, transform: `scale(${0.85 + 0.15 * v})`, background: 'rgba(255,255,255,0.12)', border: '1px solid rgba(255,255,255,0.22)', borderRadius: 999, padding: '10px 20px', fontSize: 21, fontWeight: 600, display: 'flex', alignItems: 'center', gap: 10 }}>
              <span style={{ color: COR.ambar, fontWeight: 800 }}>{String(i + 1).padStart(2, '0')}</span>{fn}
            </div>
          );
        })}
        {resto > 0 && <div style={{ opacity: sp(2.2 + funcoes.length * 0.13), fontSize: 21, padding: '10px 6px', color: COR.claro }}>e mais {resto}</div>}
      </div>
    </AbsoluteFill>
  );
};

// ---------- fechamento ----------
const Fechamento = ({ aula }) => {
  const f = useCurrentFrame();
  const sp = (atraso, cfg = {}) => spring({ frame: f - atraso * FPS, fps: FPS, config: { damping: 15, mass: 0.7, ...cfg } });
  const traco = interpolate(f, [0.2 * FPS, 0.9 * FPS], [60, 0], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp', easing: suave });
  const n = aula.funcoes.length;
  const colunas = n > 6 ? 2 : 1;
  return (
    <AbsoluteFill style={{ fontFamily: FONTE, color: '#fff', padding: '0 170px', justifyContent: 'center' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 28 }}>
        <svg width="96" height="96" viewBox="0 0 96 96" style={{ transform: `scale(${sp(0)})` }}>
          <circle cx="48" cy="48" r="44" fill={COR.ambarForte} />
          <path d="M28 49 L42 63 L69 34" fill="none" stroke="#fff" strokeWidth="9" strokeLinecap="round" strokeLinejoin="round" strokeDasharray="60" strokeDashoffset={traco} />
        </svg>
        <div>
          <div style={{ fontSize: 22, letterSpacing: '0.2em', fontWeight: 700, color: COR.ambar, opacity: sp(0.3) }}>AULA CONCLUÍDA · {aula.categoria.toUpperCase()}</div>
          <div style={{ fontSize: 64, fontWeight: 800, marginTop: 6, opacity: sp(0.45), transform: `translateX(${-30 * (1 - sp(0.45))}px)` }}>{aula.titulo}</div>
        </div>
      </div>
      <div style={{ marginTop: 44, fontSize: 20, letterSpacing: '0.18em', fontWeight: 700, color: COR.suave, opacity: sp(0.8) }}>VOCÊ VIU COMO</div>
      <div style={{ marginTop: 14, display: 'grid', gridTemplateColumns: `repeat(${colunas}, 1fr)`, gap: '12px 60px', maxWidth: 1580 }}>
        {aula.funcoes.slice(0, 16).map((fn, i) => {
          const v = sp(1.0 + i * 0.1);
          return (
            <div key={i} style={{ opacity: v, transform: `translateY(${16 * (1 - v)}px)`, fontSize: 25, display: 'flex', alignItems: 'center', gap: 14, color: COR.claro }}>
              <span style={{ width: 30, height: 30, borderRadius: '50%', background: 'rgba(74,222,128,0.18)', color: COR.verde, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 18, fontWeight: 800, flexShrink: 0 }}>✓</span>
              <span style={{ whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{fn}</span>
            </div>
          );
        })}
      </div>
      <div style={{ position: 'absolute', left: 170, right: 170, bottom: 70, display: 'flex', alignItems: 'center', justifyContent: 'space-between', opacity: sp(1.6) }}>
        <Marca largura={170} />
        <div style={{ fontSize: 20, color: COR.suave }}>Reveja qualquer função pelos capítulos ao lado do vídeo · dados fictícios</div>
      </div>
    </AbsoluteFill>
  );
};

// ---------- a aula: janela, câmera, destaque, cursor e painel ----------
const Palco = ({ aula }) => {
  const f = useCurrentFrame();
  const t = f / FPS;
  const k = escalaDaTela(aula.tela);
  const passos = aula.passos;
  const cam = camEm(quadrosChave(passos, k), t);
  const cur = cursorEm(pontosDoCursor(passos, k), t);
  const curJ = naJanela(cam, cur.x, cur.y);

  const entra = spring({ frame: f + 14, fps: FPS, config: { damping: 18, mass: 0.8 } });
  const durQ = Math.round(aula.duracao * FPS);
  const sai = interpolate(f, [durQ - 0.7 * FPS, durQ], [0, 1], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp', easing: suave });

  // A função da vez (último passo COM título que já começou).
  let atual = -1;
  passos.forEach((p, i) => { if (p.titulo && p.inicio <= t + 0.05) atual = i; });
  const comTitulo = passos.filter(p => p.titulo);
  const nAtual = atual < 0 ? 0 : comTitulo.indexOf(passos[atual]) + 1;
  const passoAtual = atual < 0 ? null : passos[atual];
  const desde = passoAtual ? (t - passoAtual.inicio) * FPS : f;
  const tr = spring({ frame: desde, fps: FPS, config: { damping: 17, mass: 0.6 } });

  // Destaque: o elemento do passo (com ou sem título) que está em cena agora.
  let foco = null;
  for (const p of passos) {
    if (!p.rect) continue;
    const ini = p.inicio + 0.55, fim = p.clique != null ? p.clique + 0.25 : (p.fim ?? p.inicio + 3) - 0.2;
    if (t >= ini && t <= fim) foco = { p, a: lim((t - ini) / 0.35, 0, 1), saindo: lim((fim - t) / 0.25, 0, 1) };
  }
  let anel = null;
  if (foco) {
    const r = retangulo(foco.p.rect, k);
    const a = naJanela(cam, r.x, r.y), b = naJanela(cam, r.x + r.w, r.y + r.h);
    const pad = 8, vis = Math.min(foco.a, foco.saindo);
    const pulso = 0.5 + 0.5 * Math.sin(t * 5);
    anel = { x: a.x - pad, y: a.y - pad, w: b.x - a.x + 2 * pad, h: b.y - a.y + 2 * pad, vis, pulso, esc: 1.12 - 0.12 * foco.a };
  }
  // O toque do clique.
  const toques = passos.filter(p => p.clique != null && t >= p.clique && t <= p.clique + 0.7)
    .map(p => ({ p: (t - p.clique) / 0.7 }));
  const clicando = passos.some(p => p.clique != null && t >= p.clique - 0.05 && t <= p.clique + 0.15);
  const cursorVisivel = lim((t - 0.6) / 0.4, 0, 1) * (1 - sai);

  const titulo = passoAtual?.titulo;
  const texto = passoAtual?.texto;
  const janelaLista = 7;
  const iniLista = lim(nAtual - 3, 0, Math.max(0, comTitulo.length - janelaLista));

  return (
    <AbsoluteFill style={{ fontFamily: FONTE }}>
      {/* painel lateral */}
      <div style={{ position: 'absolute', left: PAINEL_X, top: 64, width: PAINEL_W, bottom: 56, color: '#fff', opacity: entra * (1 - sai), transform: `translateX(${-40 * (1 - entra)}px)`, display: 'flex', flexDirection: 'column' }}>
        <Marca largura={176} />
        <div style={{ marginTop: 26, display: 'inline-flex', alignSelf: 'flex-start', fontSize: 15, letterSpacing: '0.16em', fontWeight: 700, color: COR.fundo1, background: COR.ambar, borderRadius: 999, padding: '6px 14px' }}>{aula.categoria.toUpperCase()}</div>
        <div style={{ marginTop: 12, fontSize: 27, fontWeight: 700, lineHeight: 1.2 }}>{aula.titulo}</div>
        <div style={{ height: 1, background: 'rgba(255,255,255,0.18)', margin: '26px 0 26px' }} />
        <div style={{ minHeight: 330, opacity: tr, transform: `translateX(${-26 * (1 - tr)}px)` }}>
          {passoAtual ? (
            <>
              <div style={{ display: 'flex', alignItems: 'baseline', gap: 14 }}>
                <span style={{ fontSize: 64, fontWeight: 800, color: 'transparent', WebkitTextStroke: `2px ${COR.ambar}`, lineHeight: 1 }}>{String(nAtual).padStart(2, '0')}</span>
                <span style={{ fontSize: 15, letterSpacing: '0.18em', fontWeight: 700, color: COR.suave }}>FUNÇÃO {nAtual} DE {comTitulo.length}</span>
              </div>
              <div style={{ marginTop: 14, fontSize: 36, fontWeight: 800, lineHeight: 1.15 }}>{titulo}</div>
              <div style={{ marginTop: 16, fontSize: 22, lineHeight: 1.5, color: COR.claro }}>{texto}</div>
            </>
          ) : (
            <>
              <div style={{ fontSize: 15, letterSpacing: '0.18em', fontWeight: 700, color: COR.suave }}>A TELA</div>
              <div style={{ marginTop: 14, fontSize: 24, lineHeight: 1.5, color: COR.claro }}>{aula.descricao}</div>
            </>
          )}
        </div>
        <div style={{ marginTop: 'auto' }}>
          {comTitulo.slice(iniLista, iniLista + janelaLista).map((p, j) => {
            const i = iniLista + j + 1;
            const feito = i < nAtual, agora = i === nAtual;
            return (
              <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 12, height: 38, fontSize: 18, fontWeight: agora ? 700 : 500, color: agora ? '#fff' : feito ? COR.suave : 'rgba(220,229,255,0.55)' }}>
                <span style={{ width: 26, height: 26, borderRadius: '50%', flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 13, fontWeight: 800, background: agora ? COR.ambar : feito ? 'rgba(74,222,128,0.2)' : 'rgba(255,255,255,0.1)', color: agora ? COR.fundo1 : feito ? COR.verde : COR.suave }}>{feito ? '✓' : i}</span>
                <span style={{ whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{p.titulo}</span>
              </div>
            );
          })}
          <div style={{ marginTop: 14, height: 5, borderRadius: 3, background: 'rgba(255,255,255,0.14)', overflow: 'hidden' }}>
            <div style={{ width: `${lim(t / aula.duracao, 0, 1) * 100}%`, height: '100%', background: COR.ambar }} />
          </div>
        </div>
      </div>

      {/* janela do app */}
      <div style={{ position: 'absolute', left: JAN_X, top: JAN_Y, width: JAN_W, height: JAN_H, borderRadius: 16, overflow: 'hidden', background: '#fff',
        boxShadow: '0 40px 90px rgba(2,8,40,0.55), 0 0 0 1px rgba(255,255,255,0.15)',
        opacity: entra * (1 - sai), transform: `translateY(${50 * (1 - entra) + 30 * sai}px) scale(${0.93 + 0.07 * entra - 0.05 * sai})` }}>
        <div style={{ height: BARRA, background: '#E9EEF7', borderBottom: '1px solid #D5DDEB', display: 'flex', alignItems: 'center', padding: '0 16px', fontSize: 15, color: '#334155' }}>
          <span style={{ fontWeight: 600 }}>{aula.nomeApp} — Clínica SemDor</span>
          <span style={{ marginLeft: 'auto', display: 'flex', gap: 22, color: '#64748B', fontSize: 16 }}><span>—</span><span>☐</span><span>✕</span></span>
        </div>
        <div style={{ position: 'relative', width: CW, height: CH, overflow: 'hidden', background: '#F8FAFC' }}>
          <div style={{ position: 'absolute', left: 0, top: 0, width: CW, height: CH, transformOrigin: '0 0', transform: `translate(${FOCO.x - cam.x * cam.s}px, ${FOCO.y - cam.y * cam.s}px) scale(${cam.s})` }}>
            <OffthreadVideo src={staticFile(aula.video)} muted style={{ width: CW, height: CH }} />
          </div>
          {anel && (
            <div style={{ position: 'absolute', left: anel.x, top: anel.y, width: anel.w, height: anel.h, borderRadius: 12, opacity: anel.vis, transform: `scale(${anel.esc})`,
              boxShadow: `0 0 0 3px ${COR.ambarForte}, 0 0 ${18 + 14 * anel.pulso}px ${4 + 4 * anel.pulso}px rgba(251,191,36,0.55), 0 0 0 4000px rgba(6,26,79,${0.26 * anel.vis})` }} />
          )}
          {toques.map((q, i) => (
            <div key={i} style={{ position: 'absolute', left: curJ.x - 26, top: curJ.y - 26, width: 52, height: 52, borderRadius: '50%', border: `3px solid ${COR.ambarForte}`, opacity: 1 - q.p, transform: `scale(${0.4 + 1.6 * q.p})` }} />
          ))}
          <div style={{ opacity: cursorVisivel }}><Cursor x={curJ.x} y={curJ.y} clique={clicando} /></div>
        </div>
      </div>
    </AbsoluteFill>
  );
};

export const Aula = (aula) => {
  const durQ = Math.round(aula.duracao * FPS);
  return (
    <AbsoluteFill>
      <Fundo />
      <Sequence durationInFrames={Math.round(ABERTURA * FPS)}><Abertura aula={aula} /></Sequence>
      <Sequence from={Math.round(ABERTURA * FPS)} durationInFrames={durQ}><Palco aula={aula} /></Sequence>
      <Sequence from={Math.round(ABERTURA * FPS) + durQ}><Fechamento aula={aula} /></Sequence>
    </AbsoluteFill>
  );
};
