# Vídeo de entrega — Clínica SemDor

Material de produção de um vídeo de **90 segundos, 16:9**, para apresentar à
clínica o que foi construído. Tom de **entrega**, não de venda.

| Arquivo | O que é |
|---|---|
| `roteiro.md` | Roteiro cronometrado, cena a cena, com a locução e as notas de direção |
| `cenas-animadas.html` | As 7 cenas animadas em 1920×1080, para gravar |
| `after-effects/montar-projeto.jsx` | Script que monta o projeto no After Effects |
| `after-effects/verificar-montagem.js` | Roda o `.jsx` fora do AE, contra um mock da API |

---

## 1. O que você precisa

| Item | Para quê | Situação |
|---|---|---|
| Máquina Windows | Gravar e editar | já tem |
| **OBS Studio** ([obsproject.com](https://obsproject.com)) | Gravar as cenas | grátis |
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
4. Grave as cenas (passo 3 abaixo).
5. Rode o `.jsx` no After Effects e monte.
6. Ajuste, exporte.

---

## 3. Como gravar as cenas

### Caminho recomendado — OBS Browser Source

Resolução exata independentemente do seu monitor. É o único caminho que
garante 1920×1080 nativo mesmo num notebook de tela menor.

1. No OBS: **Fontes → + → Navegador**
2. Marque **Arquivo local** e aponte para `cenas-animadas.html`
3. **Largura 1920, Altura 1080**
4. Em **URL personalizada / parâmetros**, garanta que a URL termine com
   `?c=1&play=1`
5. **Configurações → Vídeo:** Base e Saída em `1920×1080`, **30 fps**
6. **Configurações → Saída → Gravação:** Qualidade `Indistinguível`,
   formato `mkv`, codificador `x264`
7. Comece a gravar, depois clique com o botão direito na fonte →
   **Atualizar cache da página atual**. A página recarrega e toca sozinha.
8. Espere ~95s e pare. Sobra no começo e no fim é o que você apara no AE.
9. **Arquivo → Remuxar gravações** para virar `.mp4`

### Caminho alternativo — navegador em tela cheia

Só vale se o seu monitor for 1080p ou maior. Abra
`cenas-animadas.html?c=1` no Chrome ou Edge, aperte **F11**, comece a gravar
com Captura de Tela no OBS e aperte **espaço** — vem uma contagem de 3
segundos antes de começar, para você sair do caminho.

### Atalhos do arquivo de cenas

| Tecla | Faz |
|---|---|
| `espaço` | toca / pausa |
| `R` | volta ao início |
| `←` `→` | cena anterior / próxima |
| `C` | liga o modo captura (esconde o painel e o cursor) |

E pela URL:

| Parâmetro | Faz |
|---|---|
| `?t=30` | abre direto no segundo 30 |
| `?play=1` | começa tocando |
| `?c=1` | já entra em modo captura |
| `?still=1` | **desliga o movimento** — tudo salta para o estado final |

O `still=1` é para conferir enquadramento (texto estourando, cartão colado na
borda). **Nunca grave com ele ligado**: o vídeo sai parado.

---

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
