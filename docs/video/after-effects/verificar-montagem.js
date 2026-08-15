/**
 * verificar-montagem.js — roda montar-projeto.jsx contra um mock da API do
 * After Effects, fora do After Effects.
 *
 *   node docs/video/after-effects/verificar-montagem.js \
 *        docs/video/after-effects/montar-projeto.jsx
 *
 * POR QUE ISTO EXISTE
 * O .jsx só roda dentro do AE, que é Windows/Mac e não existe no CI. Sem esta
 * rede, a única forma de descobrir um erro é abrir o AE e ver o alerta — foi
 * o que aconteceu: `mestre.markers` não existe (o certo é `markerProperty`,
 * porque só Layer tem `.marker`), e a mensagem que chegou foi "linha 180,
 * undefined não é um objeto".
 *
 * A REGRA DO MOCK: expor EXATAMENTE a superfície que o AE expõe, nunca uma
 * conveniente. É por CompItem aqui ter `markerProperty` e NÃO ter `markers`
 * que este arquivo reproduz o defeito real em vez de passar por cima dele.
 * Mock generoso é pior que mock nenhum: ele fica verde e dá a impressão de
 * cobertura.
 *
 * E ele não confere só "rodou sem erro" — confere o RESULTADO, porque o
 * segundo defeito da mesma rodada não lançava exceção nenhuma: as vagas eram
 * empilhadas ao contrário, e como addSolid insere no topo, cada cena entrava
 * POR BAIXO da anterior. O cross-dissolve virava corte seco, e isso só
 * apareceria no vídeo montado.
 *
 * O que ele NÃO cobre: semântica de verdade do AE (nome de fonte que não
 * existe na máquina, versão sem markerProperty, render). Para isso não há
 * substituto para abrir o programa.
 */
const fs = require('fs');

let LOG = { comps: [], solids: [], layers: [], marcadores: [], alertas: [], removidos: [] };

class Prop {
  constructor(nome){ this.nome = nome; this.keys = []; this._value = null; this._filhos = {}; }
  property(n){
    if (!this._filhos[n]) this._filhos[n] = new Prop(n);
    return this._filhos[n];
  }
  setValueAtTime(t, v){
    if (typeof t !== 'number' || isNaN(t)) throw new Error('setValueAtTime: tempo inválido ' + t);
    this.keys.push([t, v]);
  }
  setValue(v){ this._value = v; }
  get value(){
    if (this.nome === 'Source Text') return new TextDocument();
    return this._value;
  }
}

class TextDocument {
  constructor(){
    this.fontSize = 0; this._font = ''; this.applyFill = true; this.applyStroke = false;
    this._fillColor = null; this._strokeColor = null; this.strokeWidth = 0;
    this.justification = null; this.strokeOverFill = false;
  }
  set font(v){
    // O AE recusa nome de fonte inexistente. Aqui só registramos.
    this._font = v;
  }
  get font(){ return this._font; }
  set fillColor(v){
    if (!this.applyFill) throw new Error('fillColor com applyFill desligado');
    this._fillColor = v;
  }
  get fillColor(){ return this._fillColor; }
  set strokeColor(v){
    if (!this.applyStroke) throw new Error('strokeColor com applyStroke desligado');
    this._strokeColor = v;
  }
  get strokeColor(){ return this._strokeColor; }
}

class Layer {
  constructor(nome, comp, tipo){
    this.name = nome; this._comp = comp; this.tipo = tipo;
    this._props = {}; this.label = 0; this.enabled = true;
    this._inPoint = 0; this._outPoint = comp.duration;
    this.index = comp._layers.length + 1;
  }
  property(n){ if (!this._props[n]) this._props[n] = new Prop(n); return this._props[n]; }
  set inPoint(v){
    if (v > this._outPoint) throw new Error('inPoint ' + v + ' depois do outPoint ' + this._outPoint);
    this._inPoint = v;
  }
  get inPoint(){ return this._inPoint; }
  set outPoint(v){
    if (v < this._inPoint) throw new Error('outPoint ' + v + ' antes do inPoint ' + this._inPoint);
    if (v > this._comp.duration + 1e-9) throw new Error('outPoint ' + v + ' além da comp');
    this._outPoint = v;
  }
  get outPoint(){ return this._outPoint; }
}

class FootageItem { constructor(nome){ this.name = nome; this.parentFolder = null; } }

class CompItem {
  constructor(nome, w, h, pa, dur, fps){
    this.name = nome; this.width = w; this.height = h; this.duration = dur; this.frameRate = fps;
    this.parentFolder = null; this.bgColor = null;
    this._layers = [];                     // topo→base, como no painel do AE
    this.markerProperty = new Prop('Marker');
    const self = this;
    this.layers = {
      addSolid(cor, nome, w2, h2, pa2, dur2){
        if (!Array.isArray(cor) || cor.length !== 3) throw new Error('addSolid: cor inválida');
        const l = new Layer(nome, self, 'solid');
        l.source = new FootageItem(nome);
        self._layers.unshift(l);           // novo entra no TOPO
        LOG.solids.push(nome);
        return l;
      },
      addText(txt){
        const l = new Layer(String(txt), self, 'text');
        self._layers.unshift(l);
        return l;
      },
      addNull(dur2){
        const l = new Layer('Null', self, 'null');
        self._layers.unshift(l);
        return l;
      }
    };
    LOG.comps.push(nome);
  }
  openInViewer(){}
  remove(){ LOG.removidos.push(this.name); }
}

class FolderItem { constructor(nome){ this.name = nome; this.parentFolder = null; } }

const project = {
  items: {
    addFolder(n){ return new FolderItem(n); },
    addComp(n, w, h, pa, dur, fps){
      if (!(dur > 0)) throw new Error('addComp: duração inválida ' + dur);
      return new CompItem(n, w, h, pa, dur, fps);
    }
  }
};

global.app = {
  project,
  newProject(){ return project; },
  beginUndoGroup(){}, endUndoGroup(){}
};
global.MarkerValue = class { constructor(c){ if (typeof c !== 'string') throw new Error('MarkerValue exige string'); this.comment = c; } };
global.ParagraphJustification = { CENTER_JUSTIFY: 'center', LEFT_JUSTIFY: 'left' };
global.alert = (m) => LOG.alertas.push(m);

// ---- executa o script de verdade ------------------------------------------
const caminho = process.argv[2];
const codigo = fs.readFileSync(caminho, 'utf8');
let master = null;
const origAddComp = project.items.addComp;
project.items.addComp = function(n, w, h, pa, dur, fps){
  const c = origAddComp.call(this, n, w, h, pa, dur, fps);
  if (n.indexOf('MASTER') >= 0) master = c;
  return c;
};

try {
  eval(codigo);
} catch (e) {
  console.log('ERRO AO EXECUTAR: ' + e.message);
  process.exit(1);
}

// ---- asserções -------------------------------------------------------------
const falhas = [];
if (!master) falhas.push('comp mestre não foi criada');

if (master) {
  if (Math.abs(master.duration - 90) > 1e-9) falhas.push('duração ' + master.duration + ' ≠ 90');

  const vagas = master._layers.filter(l => l.name.indexOf('[SUBSTITUIR]') === 0);
  if (vagas.length !== 7) falhas.push('esperava 7 vagas, achei ' + vagas.length);

  // A ordem é o que faz o cross-dissolve: topo→base deve ir da cena 7 à 1,
  // para cada cena que entra ficar ACIMA da anterior.
  const ordem = vagas.map(l => l.name.match(/Cena (\d)/)[1]).join('');
  if (ordem !== '7654321') falhas.push('ordem das vagas topo→base é ' + ordem + ', esperava 7654321');

  // Cada vaga precisa sobrepor a seguinte, senão não há o que dissolver.
  const porCena = {};
  vagas.forEach(l => { porCena[l.name.match(/Cena (\d)/)[1]] = l; });
  for (let n = 1; n <= 6; n++) {
    const fimA = porCena[n].outPoint, iniB = porCena[n + 1].inPoint;
    if (!(fimA > iniB + 1e-9)) falhas.push('cena ' + n + ' não sobrepõe a ' + (n + 1));
  }

  const marcas = master.markerProperty.keys.length;
  if (marcas !== 7) falhas.push('esperava 7 marcadores, achei ' + marcas);

  const legs = master._layers.filter(l => l.name.indexOf('LEG ') === 0);
  if (legs.length !== 22) falhas.push('esperava 22 legendas, achei ' + legs.length);

  // Legenda tem de ficar ACIMA de toda vaga, senão some atrás da gravação.
  const piorLeg = Math.max(...legs.map(l => master._layers.indexOf(l)));
  const melhorVaga = Math.min(...vagas.map(l => master._layers.indexOf(l)));
  if (piorLeg > melhorVaga) falhas.push('há legenda abaixo de uma vaga de gravação');

  legs.forEach(l => {
    if (l.outPoint > master.duration + 1e-9) falhas.push(l.name + ' passa do fim da comp');
  });
}

console.log('comps criadas: ' + LOG.comps.length + ' | removidas: ' + LOG.removidos.length);
console.log('sólidos: ' + LOG.solids.length);
if (falhas.length) { console.log('\nFALHAS:'); falhas.forEach(f => console.log(' ✗ ' + f)); process.exit(1); }
console.log('\nTUDO OK — ' + (master ? master._layers.length : 0) + ' camadas na mestre');
