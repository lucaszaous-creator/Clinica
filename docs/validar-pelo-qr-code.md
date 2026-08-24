# Validar pelo QR Code direto no gov.br — o que falta e como ligar

## O que acontece hoje (e por quê)

O QR das nossas folhas carrega a **URL do PDF assinado**. Isso faz dois caminhos
funcionarem e um não:

| Gesto | Resultado |
|---|---|
| **Câmera do celular** no QR | O arquivo abre. ✔ |
| **"Colar URL"** no validador | O site baixa o PDF e valida. ✔ |
| **"Ler QR Code"** no validador | Lê o QR, **pede um código** e nada serve. ✘ |

O terceiro caminho falha porque ele não é um leitor de link: pelo **Guia de Orientações
aos Desenvolvedores** do VALIDAR (Capítulo IV), depois de ler o QR o validador chama a URL
com `?_format=application/validador-iti+json&_secretCode=<código>` e espera um **JSON**
com a URL do arquivo. Um balde S3 responde o PDF, não o JSON — e a tela fica esperando um
código que a folha não tem.

**Não precisa de cadastro no ITI.** O contrato é aberto: qualquer URL que responda o JSON
entra no fluxo. O que falta é uma camada fina na frente do balde que fale as duas línguas.

## O desenho

Um **Cloudflare Worker** no MESMO domínio do QR (o CNAME da clínica, decidido na
parcela 53 exatamente para isto):

- Navegador pede a URL → o Worker entrega o **PDF** do R2, como hoje.
- O validador pede com `_format=application/validador-iti+json` → o Worker confere o
  `_secretCode` contra o **código de conferência** do documento (que o app grava como
  metadado do objeto desde 16/08/2026) e responde o JSON do contrato.

O código que a tela "Insira o código" pede passa a ser o **código de conferência impresso
no rodapé da folha** — o farmacêutico já o tem na mão. Campo em branco também passa: o
guia define `_secretCode` como "0 a 64 caracteres", e a nossa barreira de acesso é o token
de 128 bits que já está na URL.

**Como a URL não muda, as receitas já assinadas e impressas passam a funcionar também** —
inclusive as publicadas antes do metadado (o Worker aceita objeto sem código).

## Roteiro de instalação (uma vez, ~15 minutos)

1. **Cloudflare → Workers & Pages → Create Worker.** Cole o conteúdo de
   `tools/worker-clinica.js` (o arquivo ÚNICO do domínio — receitas/validador e o termo
   pelo WhatsApp juntos; parcela 82).
2. **Settings → Bindings → R2 bucket**: vincule o balde da publicação com o nome
   `BUCKET` (exatamente assim — é o nome que o script usa).
3. **Domínio**: no Worker, *Settings → Domains & Routes → Add route*:
   `dominio-da-clinica/*` (o mesmo domínio gravado em Configurações → Publicação).
   Se o domínio hoje aponta direto para o R2 (custom domain do balde), a rota do Worker
   passa na frente — nada mais muda.
4. **Teste em três passos**:
   - abrir uma URL de receita no navegador → o PDF tem de abrir como sempre;
   - a mesma URL com `/?_format=application/validador-iti+json` no fim → tem de voltar
     o JSON;
   - no validador: **Ler QR Code** → apontar para o QR → digitar o código de
     conferência da folha (ou deixar em branco) → **Validar**.

## Depois de ligado: o rodapé

A folha imprime hoje *"o Ler QR Code de lá pede um código que esta folha não tem"* — que
é a verdade **enquanto o Worker não existe**. Ligado o Worker, essa frase muda para
apontar o código de conferência (uma linha em `DocumentosClinicosPdfService`), e sai numa
release normal. A ordem importa: mudar a frase antes de ligar o Worker imprimiria uma
promessa que o endereço ainda não cumpre.

## O que este desenho não faz

- **Não expõe nada novo**: o Worker lê o mesmo balde, no mesmo domínio; o metadado do
  código não aparece para quem baixa o arquivo.
- **Não toca na assinatura** nem nos PDFs — a URL selada nos arquivos continua a mesma.
- **Não substitui o validador**: quem responde "válido/adulterado" continua sendo o ITI.

## Nota — DocMDP (Capítulo VI do guia)

O guia recomenda que a 1ª assinatura declare `DocMDP` (P=2: permite preencher formulário
e **acrescentar assinaturas**) e avisa que, sem isso, um PDF com atualização incremental
"poderá" aparecer como **"Assinatura Indeterminada"** no validador. Afeta só a folha com
DUAS assinaturas (a prescrição de infusão) — que é documento **interno** (art. 13 da Lei
14.063/2020) e não vai à farmácia; receita e atestado têm uma assinatura e nenhuma
atualização incremental. Declarar DocMDP exige mexer em como o PDFsharp escreve a 1ª
assinatura — área congelada (`docs/safeid-congelado.md`); fica registrado como decisão
futura, com autorização expressa.
