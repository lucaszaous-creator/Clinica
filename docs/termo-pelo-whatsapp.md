# O termo assinado pelo celular do paciente — o link no WhatsApp

> Pedido da cliente (ago/2026): *"se colocássemos um na web a gente envia para o paciente,
> ele lê, assina, envia e já cai no nosso banco — é meio o que acontece com a nossa
> assinatura SafeID. Então no atendimento o médico/enfermeiro/secretária abre o termo e
> envia via WhatsApp para o paciente assinar/ler enquanto espera."*

Este documento é a DECISÃO — o que o link faz, o que ele nunca faz, que evidência fica e
por quê. O código executa o que está aqui; quando os dois divergirem, é o código que está
errado.

> ✅ **O Worker está publicado e o circuito foi testado de ponta a ponta** (ago/2026): o
> link sai, o paciente assina no celular, a resposta volta ao desktop e o termo é concluído
> com a evidência gravada.

> ✅ **Desde a parcela 89 este caminho serve TAMBÉM ao termo de consentimento (LGPD)** — as
> quatro finalidades viraram declarações Sim/Não que o paciente responde e assina, e a
> resposta assinada é o que o sistema passa a consultar. Nada aqui mudou para isso: o
> circuito sempre foi genérico sobre `DocumentoClinico`, e o que faltava era o portão
> (`TipoDocumentoInfo.AssinadoPeloPaciente`). Ver `docs/termo-assinado-pelo-paciente.md`, §8.

## 1. O que muda e o que NÃO muda

A parcela 66 decidiu a coleta no balcão (traço na tela, testemunha, documento conferido) e
recusou o modelo "link no celular" com dois argumentos: a declaração de jejum assinada em
casa é afirmação sobre o futuro, e tablet é outro computador. **O pedido de agora não é
aquele cenário**: o paciente está NA CLÍNICA, na sala de espera, e assina no próprio
celular enquanto aguarda — no dia, presente, com a equipe a dez metros. Isso preserva a
validade "só no dia" do jejum e a possibilidade de conferir a identidade no check-in.

O que não muda:

- **A coleta no balcão continua existindo por inteiro** (mouse, monitor touch). O link é
  mais uma porta, não a substituta — paciente sem WhatsApp, celular sem bateria, ou a
  preferência da casa.
- **A assinatura continua SIMPLES** (MP 2.200-2, art. 10, §2º): o valor vem da EVIDÊNCIA,
  não de certificado. A analogia com o SafeID é de FORMA (mandar → ação remota → resultado
  volta), não de espécie.
- **O selo do termo não muda.** O hash cobre o que o paciente viu — corpo, declarações
  respondidas, traço — exatamente como na coleta local. Mexer na montagem do selo
  invalidaria a conferência dos termos já assinados.

## 2. A arquitetura — por que R2 + Worker, e nunca banco na borda

O caminho reaproveita a infraestrutura das receitas publicadas (parcela 53) e do Worker do
validador (parcela 68): o MESMO domínio da clínica (CNAME), o MESMO balde, o MESMO token de
26 caracteres/2^127 (`PublicacaoDocumento.GerarToken`).

```
desktop                          borda (Cloudflare)                celular do paciente
-------                          ------------------                -------------------
emite o termo
gera token, sobe t/xx/TOKEN.json ────────────────────────────────► GET dominio/t/TOKEN
grava ColetaRemotaTermo no banco    Worker lê o pedido no R2       lê o termo, responde
abre wa.me com o link               (binding, sem credencial       Sim/Não, assina no dedo
                                     de banco NENHUMA na borda)
fica aguardando (polling) ◄──────── Worker grava TOKEN.resposta.json ◄─ POST (write-once)
lê a resposta, mostra o traço,
técnica confere identidade,
Confirmar → ColherAsync (o MESMO
da coleta local) → apaga os
objetos do balde
```

**A decisão estrutural: o Worker NÃO acessa o banco.** Ele só enxerga um prefixo do balde
(`t/`). Vazamento do Worker expõe no máximo os pedidos em aberto — nunca uma credencial do
Postgres da clínica. É a razão de o desenho ser "desktop sobe JSON / Worker devolve JSON",
e não "Worker consulta o Neon".

**Write-once**: o Worker recusa segunda resposta para o mesmo token. A primeira assinatura
é A assinatura; sem isso, quem interceptasse o link depois poderia sobrescrever o traço.

## 3. Minimização — o que o link mostra e o que NUNCA mostra

O `pedido.json` carrega SÓ o que a leitura exige:

| Vai | NÃO vai, por decisão |
|---|---|
| Título e corpo do termo (o texto que ele assina) | CPF, RG, carteirinha |
| As declarações (pergunta + detalhe) | Sobrenome do paciente (só o primeiro nome, para ele se reconhecer) |
| Primeiro nome do paciente | Nascimento, telefone, convênio |
| Validade do link | Qualquer outro dado da ficha |

A mensagem do WhatsApp também é mínima — "seu termo para leitura e assinatura: {link}" —
porque notificação de celular aparece na tela bloqueada.

Exposição: mesma classe já aceita para as receitas publicadas — dado de saúde atrás de um
token inadivinhável, no domínio da clínica, com prazo curto. Aqui o prazo é **24 horas**
(fixo, não configurável: o link é para a sala de espera, não para a semana), e a limpeza é
tripla: ao concluir a coleta o desktop APAGA os dois objetos; ao expirar, a rotina de
limpeza apaga; e o Worker recusa servir pedido vencido mesmo que o objeto ainda exista.

## 4. A evidência — o que responde "quem assinou?"

| Coleta no balcão | Coleta pelo celular |
|---|---|
| Traço colhido na frente da técnica | Traço colhido no celular do paciente |
| Documento de identidade conferido no ato | Identidade conferida no check-in; o campo continua OBRIGATÓRIO e a técnica escreve o que conferiu |
| Testemunha = quem estava logado | Testemunha = quem enviou o link e concluiu a coleta |
| — | Link enviado ao TELEFONE DA FICHA (fica gravado) |
| — | IP, user-agent e hora da resposta (gravados na linha `ColetaRemotaTermo`) |

A evidência remota não é mais fraca — é DIFERENTE, e fica escrita como tal: a trilha de
auditoria da coleta registra o canal ("pelo celular do paciente, link WhatsApp"), e a
linha `ColetaRemotaTermo` guarda o telefone, o token, quem enviou e a resposta.

**O que o desenho recusa**: concluir sozinho. A resposta do celular NÃO sela o documento
por conta própria — ela volta para a janela da técnica, que vê o traço e as respostas,
confere a identidade e clica Confirmar. Selar sem ninguém olhar transformaria qualquer
resposta tecnicamente válida num termo assinado, e o papel deste fluxo é tirar o custo do
pad, não a pessoa do circuito.

## 5. O Worker no Cloudflare — UM arquivo, e por quê

⚠️ **Corrigido no primeiro teste da clínica (parcela 82).** O desenho original previa dois
Workers com rotas por caminho (`/r/*` e `/t/*`) — o que só existe com domínio PRÓPRIO. O
endereço público configurado no sistema é o hostname de UM worker no `workers.dev`, e no
`workers.dev` **o hostname É o worker**: não há rota por caminho entre dois. O link do
termo (`{endereço}/t/{token}`) caía no worker das receitas, que respondia "Documento não
encontrado".

O arquivo é um só — **`tools/worker-clinica.js`** — com as duas funções despachadas pelo
caminho (`/t/*` → termo; resto → receitas/validador). Ele é colado **no worker cujo
hostname está configurado** em Gerente → Configurações → Publicação, usando o binding
`BUCKET` que já existe lá. Um segundo worker criado por engano pode ser apagado.

Com domínio próprio no futuro, o mesmo arquivo atende com uma única rota `dominio/*` —
nada muda no app nem nos QRs já impressos.

## 6. O que fica para depois (dito, não prometido)

- **Envio automático** (WhatsApp Business API) — hoje é o wa.me de sempre: um clique, a
  mensagem pronta, quem envia é a pessoa. Prometer envio sem clique exigiria conta
  Business e custo por mensagem.
- **Assinar em casa, dias antes** — o consentimento longo até admitiria; o jejum nunca. Se
  a clínica pedir, é outra decisão, com a validade por modelo (parcela 67) fazendo o corte.
- **Fotos de documento pelo link** — não; o link não coleta dado, só devolve a assinatura
  do texto que mostrou.
