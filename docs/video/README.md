# Vídeo de entrega — Clínica SemDor

Material de produção de um vídeo de **90 segundos, 16:9**, para apresentar à
clínica o que foi construído. Tom de **entrega**, não de venda.

| Arquivo | O que é |
|---|---|
| `roteiro.md` | Roteiro cronometrado, cena a cena, com a locução e as notas de direção |
| `cenas-animadas.html` | As 7 cenas animadas em 1920×1080, para gravar |
| `after-effects/montar-projeto.jsx` | Script que monta o projeto no After Effects |
| `after-effects/verificar-montagem.js` | Roda o `.jsx` fora do AE, contra um mock da API |
| `renderizar.js` | **Gera o MP4 direto do HTML** — sem gravar tela |
| `marca/` | Logo branca corrigida (a do repositório tem defeito) |

---

## 1. O que você precisa

| Item | Para quê | Situação |
|---|---|---|
| Máquina Windows | Gravar e editar | já tem |
| After Effects | Montagem, legendas, trilha | assinatura Adobe |
| Locução | A voz do vídeo | você grava, ou geramos |
| Trilha sonora **licenciada** | Música de fundo | ~US$15/mês (Artlist, Epidemic) |

**Não é preciso** abrir o sistema, nem preparar base de demonstração, nem
instalar nada além do OBS. As telas do vídeo são reproduções fiéis do app,
feitas com o mesmo design system — e por isso não há dado de paciente
envolvido em nenhuma etapa.

> ⚠️ **Se você decidir gravar o app de verdade em vez disso**, use uma branch
> do Neon com dados fictícios (`docs/testar-sem-publicar.md` tem o roteiro).
> Gravar a base de produção põe prontuário e dado de paciente real num arquivo
> de marketing, e isso não tem desfazer depois de o vídeo circular.

---

## 2. Ordem de trabalho

A ordem importa, e a razão está no passo 1:

1. **Grave a locução primeiro.** O vídeo se ajusta à voz; a voz não se ajusta
   ao vídeo. Quem monta a imagem antes acaba acelerando a fala para caber, e
   locução apressada é a marca registrada de vídeo institucional ruim.
2. Meça a duração real de cada trecho e ajuste os tempos em `roteiro.md`.
3. Ajuste os mesmos tempos no `CENAS` de `cenas-animadas.html`.
4. Gere os MP4 (passo 3 abaixo).
5. Rode o `.jsx` no After Effects e monte.
6. Ajuste, exporte.

---

## 3. Gerar o vídeo

**Não é preciso gravar tela.** O MP4 sai daqui:

```bash
node docs/video/renderizar.js docs/video/cenas-animadas.html 1920 1080 90 entrega.mp4
node docs/video/renderizar.js docs/video/vertical/vertical-41s.html 1080 1920 41 vertical.mp4
```

Requer `puppeteer-core`, o Chromium do ambiente e `ffmpeg`. Saída em H.264,
`yuv420p`, `+faststart` — abre em celular, WhatsApp e navegador.

### Por que tempo virtual, e as quatro armadilhas

As animações são transições CSS, que correm no relógio de PAREDE. Capturar,
esperar e capturar de novo produziria quadros com espaçamento irregular: vídeo
trêmulo que nenhum ajuste de fps conserta. O renderizador congela o tempo do
navegador e o avança em passos exatos de 1/fps.

Isso tem quatro armadilhas, todas encontradas na prática e todas com a mesma
assinatura — a captura fica pendurada e a mensagem fala de *timeout*, nunca da
causa:

1. **Congelar antes de navegar trava o carregamento.** Carregue primeiro, com a
   peça pausada; só então congele.
2. **Sem mudança na tela não há quadro novo.** Nos trechos parados o compositor
   não entrega nada. Daí o marcador de 1 px que alterna a cada frame — a API
   própria para isto (`HeadlessExperimental.beginFrame`) foi removida do Chrome.
3. **`Page.captureScreenshot` precisa de `fromSurface:false`.**
4. **Imagem e fonte precisam ser decodificadas ANTES de congelar.** A
   decodificação é assíncrona e fica presa com o tempo pausado. Este só aparece
   quando há imagem no PRIMEIRO quadro — foi o que fez o 16:9 falhar assim que
   a logo entrou na abertura, enquanto o vertical (logo só no fecho) passava.

⚠️ **Não canalize a saída do render para `head`**: o pipe fecha, o processo
morre no meio e o MP4 some sem mensagem nenhuma.

### A peça precisa expor `irPara(t)` e `tocar()`

É o contrato. O renderizador **recusa** a peça que não os exponha, em vez de
seguir e produzir um vídeo torto — antes ele mexia direto na variável de
relógio, que tem nome diferente em cada arquivo, e no arquivo errado a peça
começava adiantada sem erro nenhum.

## 4. Montagem no After Effects

1. **Arquivo → Scripts → Executar arquivo de script…** → `montar-projeto.jsx`
2. Ele cria a comp `SemDor — MASTER 90s` com 7 espaços `[SUBSTITUIR]`,
   marcador em cada cena, as 22 legendas já cronometradas e os sólidos da marca
3. Importe a gravação para a pasta `03 · Gravações`
4. Corte a gravação nas 7 cenas e arraste cada trecho **com Alt** sobre o
   espaço correspondente — Alt substitui mantendo tempo e transições
5. Ponha a locução abaixo da guia `↓ LOCUÇÃO`
6. Ponha a trilha abaixo da guia `↓ TRILHA`, com volume por volta de −22 dB
   sob a voz

O script deixa **0,4s de cross-dissolve** em cada emenda. O HTML corta seco de
propósito: transição gravada no vídeo não se desfaz na montagem, e no AE ela é
um parâmetro.

### Se mexer no `.jsx`

Rode antes de abrir o After Effects:

```bash
node docs/video/after-effects/verificar-montagem.js \
     docs/video/after-effects/montar-projeto.jsx
```

Ele executa o script contra um mock da API do AE e confere o resultado —
ordem das camadas, sobreposição das emendas, marcadores, legendas. É a única
rede que existe fora do AE, e ela nasceu de dois defeitos reais: `mestre.markers`
no lugar de `markerProperty` (que só aparecia como *"linha 180, undefined não é
um objeto"*) e as vagas empilhadas ao contrário, que **não dava erro nenhum** e
transformava todo cross-dissolve em corte seco.

⚠️ **ExtendScript é ES3.** Sem `let`, sem `const`, sem arrow function, sem
template literal. O AE recusa, e a mensagem não diz o que houve.

### Exportação

`Composição → Adicionar à fila do Adobe Media Encoder` → **H.264**, 1920×1080,
30 fps, **VBR 2 passagens, alvo 12 Mbps / máximo 16**. Áudio AAC 320 kbps.

Se for para WhatsApp, exporte uma segunda versão a 8 Mbps — o app recomprime
qualquer coisa acima disso, e o que chega ao celular é a versão recomprimida.

---

## 5. Antes de publicar

- [ ] **Recontar os números.** O fecho diz 5 aplicativos, 84 serviços e 1.582
      testes. Confirmados em 15/08/2026; o último cresce a cada parcela. Os
      comandos estão no fim de `roteiro.md`.
- [ ] **Trilha licenciada.** Música do YouTube ou "gratuita" da internet gera
      reclamação de direitos autorais e derruba o vídeo depois de ele já ter
      circulado.
- [ ] **Nenhum dado real.** Todos os nomes nas cenas são fictícios; confirme
      que nenhum trecho de gravação de tela real entrou por engano.
- [ ] **Reler a Cena 6.** É a única que faz afirmação jurídica. Ela diz o que
      o código entrega e nada além — não acrescente "certificado SBIS/CFM" nem
      "PAdES-LT", que o sistema não tem. Garantia aparente é pior que ausência
      de garantia.

---

## 6. Se quiser mudar alguma coisa

**Trocar um texto de tela:** está no HTML, na seção da cena.

**Mudar a duração de uma cena:** mude `dur` no `CENAS` do HTML *e* o `dur` no
`CENAS` do `.jsx` *e* a tabela do `roteiro.md`. Os três são a mesma verdade
escrita em três lugares — não há como derivar um do outro sem uma etapa de
build, e uma etapa de build para três números seria pior do que a duplicação.

**Mudar quando uma animação dispara:** é o `t` do `cues` da cena, em segundos
*relativos ao início dela*. Foi feito relativo justamente para você poder
mexer numa cena sem reescrever os tempos das outras.

**Trocar uma cor:** os tokens estão no `:root` do HTML, espelhados de
`src/Clinica.Desktop/Styles/Tokens.xaml`. Se mudar lá, mude aqui — e no
`MARCA` do `.jsx`.
