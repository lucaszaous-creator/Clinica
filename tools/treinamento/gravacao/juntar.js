// Junta o que as máquinas da matriz gravaram (out/2026). Uso:
//   node juntar.js <pasta-com-as-partes> <pasta-saida>
// Cada parte tem catalogo.json, resumo.json e os MP4. Sai UM catálogo (o formato do
// Clinica.Desktop.Shell/Treinamento/catalogo.json), com os vídeos renomeados pelo SHA-256 —
// é por esse nome que o app os baixa da release.
const fs = require('fs');
const path = require('path');
const [, , entrada, saida] = process.argv;
fs.mkdirSync(path.join(saida, 'release'), { recursive: true });
const catalogo = [], resumo = [];
for (const parte of fs.readdirSync(entrada)) {
  const dir = path.join(entrada, parte);
  if (!fs.statSync(dir).isDirectory()) continue;
  const cat = path.join(dir, 'catalogo.json');
  if (fs.existsSync(cat)) for (const a of JSON.parse(fs.readFileSync(cat, 'utf8'))) {
    catalogo.push(a);
    fs.copyFileSync(path.join(dir, a.Id + '.mp4'), path.join(saida, 'release', a.Sha256 + '.mp4'));
  }
  const res = path.join(dir, 'resumo.json');
  if (fs.existsSync(res)) resumo.push(...JSON.parse(fs.readFileSync(res, 'utf8')));
}
catalogo.sort((a, b) => a.Categoria.localeCompare(b.Categoria) || a.Titulo.localeCompare(b.Titulo));
fs.writeFileSync(path.join(saida, 'catalogo.json'), JSON.stringify(catalogo, null, 2) + '\n');
fs.writeFileSync(path.join(saida, 'resumo.json'), JSON.stringify(resumo, null, 2));
const falhas = resumo.filter(r => r.erro);
console.log(`${catalogo.length} aula(s) no catálogo; ${falhas.length} com erro.`);
for (const f of falhas) console.log('  ERRO', f.chave, '—', f.erro);
