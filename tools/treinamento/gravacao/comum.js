// O que o inventário e a gravação compartilham (out/2026): qual app mostra cada aula, a
// seção da tela do paciente de cada chave e a URL da página da aula na prévia.
const fs = require('fs');

// A ordem decide em qual app a aula é gravada quando a tela existe em vários: o app de
// quem usa aquela tela todo dia vem primeiro (o Gerente, que carrega tudo, por último).
const ORDEM_APPS = ['recepcao', 'consultorio', 'financeiro', 'faturamento', 'gerente', 'consultorio-enfermagem'];

// As chaves da tela do paciente (ModuloClinico.AbaDe/SubAbaDe): a seção e a aba de dentro.
const SECOES = {
  'consultorio-atendimento': ['Atendimento', 0],
  'consultorio-atendimento-enfermagem': ['Atendimento de enfermagem', 0],
  'consultorio-paciente': ['Ficha do paciente', 0],
  'consultorio-prontuario': ['Histórico', 0],
  'consultorio-exames-anexos': ['Exames e anexos', 0],
  'consultorio-evolucao-dor': ['Acompanhamento', 0],
  'consultorio-medidas': ['Acompanhamento', 1],
  'consultorio-avaliacoes': ['Acompanhamento', 2],
};

function lerNavegacao(caminho) {
  return JSON.parse(fs.readFileSync(caminho, 'utf8')).filter(a => !a.erro);
}

/** Uma aula por chave de tela FOLHA (com componente), no primeiro app da ORDEM que a mostra. */
function aulasDaNavegacao(apps) {
  const porId = Object.fromEntries(apps.map(a => [a.app, a]));
  const aulas = new Map();
  for (const id of ORDEM_APPS) {
    const app = porId[id];
    if (!app) continue;
    for (const chave of app.telasTreinamento) {
      if (aulas.has(chave)) continue;
      const item = app.itens.find(i => i.chave === chave);
      if (!item || !item.componente) continue;
      aulas.set(chave, { chave, app: id, nomeApp: app.nome, rotulo: item.rotulo, componente: item.componente });
    }
  }
  return [...aulas.values()];
}

function urlDaAula(base, aula, extra = {}) {
  const q = new URLSearchParams();
  const secao = SECOES[aula.chave];
  if (secao) {
    q.set('secao', extra.secao || secao[0]);
    q.set('subaba', String(extra.subaba ?? secao[1]));
    q.set('paciente', String(extra.paciente || 1));
    if (extra.agendamento) q.set('agendamento', String(extra.agendamento));
  }
  const s = q.toString();
  return `${base}/aula/${aula.app}/${aula.chave}${s ? '?' + s : ''}`;
}

module.exports = { ORDEM_APPS, SECOES, lerNavegacao, aulasDaNavegacao, urlDaAula };
