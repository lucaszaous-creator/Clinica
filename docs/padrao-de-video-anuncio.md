# Padrão de vídeo de anúncio (Meta: Reels, Stories e Feed)

> Aprovado pelo dono em 23/11/2026 ("simplesmente perfeito esses padrões de vídeo") a partir da rodada 3 da Clínica SemDor.
> Vale para todo vídeo de anúncio ou divulgação feito para os clientes: SemDor (Clinica), CDML e os próximos.
> A identidade (logo, cores, voz, rodapé) muda por cliente. A **linguagem** e o **processo** abaixo não mudam.

## 1. A linguagem (o que faz o vídeo "grudar")
Referência: os vídeos de criador de conteúdo e de SaaS de 2026 (ex.: Matheus Gobbi, Fitfolio), adaptados à marca.

1. **Legenda cinética palavra por palavra**, presa ao tempo exato da voz.
   - A frase vai em sans pesada pequena.
   - A **palavra-chave** vai em escrita à mão grande, na cor principal da marca, com sublinhado na cor de destaque.
2. **Rabiscos desenhados à mão que se desenham na tela**: setas, círculos e "passo 1, 2, 3" circulados, com check-list ✓. Eles apontam e anotam o que a voz diz.
3. **Figurinhas**: foto recortada com borda branca e sombra, entrando com mola (escala de 0,9 para 1,0, com leve rotação).
4. **Moldura de seleção** (caixa com alças, como numa interface) sobre a logo ou um detalhe, com rótulo em escrita à mão.
5. **Interface desenhada** (ex.: WhatsApp num celular) com **conversa fictícia**. Nunca captura real.
6. **Ritmo**: cortes de 0,6 a 1,7 s, gancho no primeiro quadro, e o mesmo fecho em todos os vídeos (logo + bordão + canal de contato).
7. **Fundo branco sempre**, com logo **só na versão colorida oficial**. Azul-marinho e cores genéricas de IA são proibidos.

## 2. A voz (locução)
- **ElevenLabs**, plano pago (o plano gratuito não libera vozes da biblioteca nem uso comercial).
- Voz brasileira da biblioteca, com sotaque nativo. Padrão SemDor: **Gabriel** (`WPgqgCVa3mD38JuyODU6`). A alternativa feminina é Isabela (`Pxxu743NJcLUqO1QxfD6`).
- **Roteiro comercial próprio**, escrito para ouvir (não é a legenda lida). Frases curtas, convite no fim.
- **Pronúncia da marca**: escrever no texto da voz como deve soar (ex.: "Clínica Sem Dor"), mantendo a grafia oficial na tela ("SemDor").
- Gerar com `/v1/text-to-speech/{voz}/with-timestamps`. O alinhamento por caractere vira um JSON de **tempos por palavra** (`palavra`, `inicio`, `fim`), que alimenta a legenda.
- Ajustes: `model_id eleven_multilingual_v2`, `language_code pt`, `stability ~0.42`, `similarity 0.8`, `style ~0.4`, `speed 1.0–1.07`. Saída `mp3_44100_128` (o Starter não libera 192 kbps).
- A voz é usada **inteira, sem corte**. Se não couber no vídeo, o vídeo cresce; a voz não é acelerada acima de 1,06x.
- A chave fica no cofre do OpenBot (`ELEVENLABS_API_KEY`). **Nunca** vai para o repositório nem para log.

## 3. O processo de produção (código + ffmpeg)
1. **Cenas em HTML/CSS/SVG** (`cenas.html`): cada cena é uma função do tempo, e os traços se desenham com `stroke-dashoffset`.
2. **Render quadro a quadro** com Playwright/Chromium (`render.cjs`) a 30 fps, em 1080×1920 (9:16) e 1080×1350 (4:5).
3. **Montagem com ffmpeg** (`fazer-videos.sh`): quadros → H.264 (yuv420p, `+faststart`) → mistura da voz → AAC 160 a 192 kbps.
4. **Loudness de rede**: `loudnorm` em duas passadas, de -14 a -16 LUFS, com pico a -1,5 dBTP.
5. **Checagem automática**: a legenda falha o render se alguma palavra não bater com o JSON da voz.
6. **Folha de contato** (`fps=1,tile`) para revisão visual antes do QA.
7. Trilha: só com licença registrada, baixa sob a voz (cerca de -18 dB, com ducking). Na dúvida, sem trilha, e escolhe-se uma da Biblioteca de sons da Meta ao publicar.

## 4. Imagens, fontes e licenças
- Fotos ou filmagens de banco com **licença comercial livre** (Pexels, Unsplash, Pixabay), cada uma registrada em `LICENCAS.md` com autor, link e uso.
- **Nunca** apresentar foto de banco como a equipe ou o local do cliente. Mostrar o aviso "Imagens ilustrativas" nas cenas com foto. Nada com cara de IA. Cidade só como palavra e grafismo, se não houver foto real dela.
- Fontes com licença OFL (ex.: Inter para texto, Caveat para a escrita à mão), registradas.
- Fotos e fontes **não** vão para o repositório: baixam-se de novo pela licença registrada.

## 5. Regras de saúde (inegociáveis)
- **CFM 2.336/2023**: sem promessa de cura ou resultado, sem antes e depois, sem preço como chamariz, sem figurinha de promoção ("% OFF", "BIG SALE"), sem número inventado. **Rodapé do responsável técnico em todo quadro**: nome + CRM.
- **Política de saúde da Meta**: não perguntar nem afirmar atributo de saúde de quem vê ("Você sofre de dor?").
- **LGPD**: nenhum dado real de paciente. Depoimento só com pessoa real e termo de imagem assinado.

## 6. Entrega
- Os vídeos em 9:16 e 4:5, Full HD, 30 fps, H.264 + AAC, já com a voz.
- PDF para o celular com os quadros-chave, os tempos e o texto da locução.
- A fonte que gera tudo e o `LICENCAS.md`.
- **QA de Design aprova antes** de chegar ao dono. Nada é publicado sem o OK dele.
- Para o cliente: um ZIP com nomes claros (ex.: `1-Reels-Stories-9x16-12s`). Pelo WhatsApp, enviar **como documento**.

## 7. Identidade por cliente (preencher no repositório de cada um)
| Item | SemDor (Clinica) | CDML |
|---|---|---|
| Logo | colorida oficial, símbolo azul forte + azul fraco com "Clínica SemDor" | a definir com o Chefe CDML |
| Cor principal / destaque | #153194 (royal) / âmbar | a definir |
| Voz | Gabriel (alternativa Isabela) | a definir |
| Pronúncia | "Clínica Sem Dor" | a definir |
| Bordão | "Cuidado da dor? Chama a SemDor." | a definir |
| Contato | WhatsApp (22) 99957-7446 | a definir |
| Responsável técnico | Dr. Gustavo Lacerda · CRM-RJ 52-990914 | a definir |
