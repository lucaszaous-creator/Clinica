# UI kit — Suíte de gestão da clínica (web)

Recriação em React das **18 telas** da suíte de gestão da Clínica SemDor, nos 4 módulos por
papel (Recepção, Clínico, Gerente geral, Faturamento) + os 3 modais clínicos. É a
implementação de referência do handoff `Clínica SemDor — Design System`; o produto real
continua sendo a suíte WPF/.NET em `src/`.

```bash
npm install
npm run dev                # http://localhost:5273
npm run build && npm run preview
npm run verificar-icones   # nome de ícone que não existe no lucide falha aqui
npm run capturar           # abre CADA tela num Chromium, confere e salva PNG
```

O seletor **Módulo**, no rodapé da sidebar, troca de papel em qualquer tela; `?modulo=` e
`?tela=` abrem direto numa delas (`/?modulo=faturamento&tela=glosas`) — é o equivalente aos
quatro `.html` da referência, e é por aí que `npm run capturar` percorre a suíte.

## As três regras que não podem cair

O handoff chama estas de **fidelidade obrigatória**, porque as três somem sem quebrar nada:

1. **Ícones = lucide, sempre.** Aqui vêm do pacote `lucide-react` (versão 0.462.0, a mesma
   do UMD da referência), com os mesmos nomes kebab-case. `src/ds/core/Icon.jsx` é o ÚNICO
   arquivo que muda entre a referência HTML e esta implementação — nenhum outro componente
   do DS foi tocado (novas-telas.md §6).
2. **Topbar SEMPRE com busca em pílula (lupa dentro do campo) + sino + avatar.** Vem pronta
   do componente `Topbar`; usar o componente é o que garante isso.
3. **TODO item de sidebar tem ícone.** `groups[].items[].icon` é obrigatório em `App.jsx`.

Nenhuma das três é conferida lendo o código: `npm run capturar` abre a tela montada e
reprova quando a lupa, o sino, o avatar ou o ícone de um item somem — é o §8.7/§8.8 do
`novas-telas.md` virado comando.

## Estrutura

| pasta | o que é |
|---|---|
| `src/ds/` | os 36 componentes do design system (core, data, feedback, navigation, charts, clinic) + `index.js` |
| `src/telas/` | as 18 telas e os 3 modais clínicos |
| `src/dados.js` | massa fictícia em pt-BR — trocar por dados reais numa implementação de produção |
| `tools/` | `verificar-icones.mjs` e `capturar-telas.mjs` (capturas em `tools/capturas/`, fora do git) |

**Os tokens não moram aqui.** `src/main.jsx` importa o `styles.css` da RAIZ do repositório,
que por sua vez importa `tokens/*.css` — os mesmos arquivos que espelham
`src/Clinica.Desktop/Styles/Tokens.xaml` (conferido no CI por `tokens/verificar-espelho.py`).
Uma segunda cópia dos tokens divergiria na primeira correção de cor.

## Tela nova

`novas-telas.md` §1 e §7: componente em `src/telas/`, registrado em `TELAS` no `App.jsx`,
mais o item (com `icon`) no `groups` do módulo dono. Depois, `npm run capturar` e o
checklist §8 — nesta ordem, com a tela aberta na frente.

## Notas de ambiente

- A fonte é **Inter** (substituta web do Segoe UI do app WPF), carregada do Google Fonts por
  `tokens/fonts.css`. Sem rede, a pilha cai para `system-ui` e o texto fica um pouco mais
  largo — algumas células de tabela quebram em duas linhas. Não é defeito de leiaute.
- `npm run capturar` usa o Playwright: o `npm install` baixa um Chromium (~150 MB) na
  primeira vez. Numa máquina que já tem um, `PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1 npm install`
  pula o download — o script usa `/opt/pw-browsers/chromium` quando esse caminho existe.
- O bundle carrega o conjunto lucide inteiro porque o `Icon` resolve o glifo por NOME, em
  tempo de execução, como na referência. É um kit de UI, não o app de produção.
