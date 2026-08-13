# O quanto estamos atrás da My Smart Clinic

> Levantamento da distância entre o nosso produto e o **My Smart Clinic**
> (`mysmartclinic.com.br`) — o sistema que a nossa cliente usa hoje. Feito em **ago/2026**.
>
> A pergunta que ele responde é a que foi feita: *"o quão atrás estamos?"* — e a resposta
> honesta tem duas metades, porque **não estamos atrás na mesma direção em que estamos na
> frente**.

## Como este levantamento foi feito, e o que ele NÃO viu

Do lado **deles**: páginas públicas do produto, base de conhecimento (`tawk.help`), página
de planos e preços e material de divulgação. ⚠️ **O acesso direto ao site foi bloqueado
pelo proxy de saída deste ambiente**, então o que está aqui vem de resultados de busca e
de trechos das páginas, não da leitura integral delas. Consequência prática: **a lista
deles pode estar incompleta, e um ou outro detalhe de plano pode ter mudado.** Antes de
usar isto em conversa comercial, vale abrir o site e conferir as linhas marcadas com ⚠️.

Do lado **nosso**: o código, não a memória nem `features-por-modulo.md`. Toda linha da
tabela "estamos atrás" foi conferida com busca no `src/` — e é por isso que algumas dizem
"zero ocorrências" em vez de "não me lembro de ter feito".

## O placar, em uma frase

**Não estamos atrás em profundidade — estamos atrás em ALCANCE.** Eles são um SaaS de
nuvem que abre no celular, deixa o paciente marcar sozinho, dispara mensagem sem ninguém
clicar e tem IA transcrevendo consulta. Nós somos uma suíte **Windows instalada em cada
máquina** que, por dentro, faz coisas que eles não fazem — TISS completo, assinatura
ICP-Brasil própria, sala de infusão com checagem de enfermagem, financeiro tributário.

| Eixo | Situação |
|---|---|
| **Alcance** (onde o sistema abre) | 🔴 **Muito atrás** — 1 gap, estrutural |
| **Autosserviço do paciente** | 🔴 **Muito atrás** — 4 gaps, 3 deles fora de escopo por decisão da cliente |
| **Comunicação automática** | 🟠 **Atrás** — 1 gap, o de melhor relação impacto/custo |
| **IA** | 🔴 **Muito atrás** — zero, e é o que eles usam como argumento de venda |
| **Fiscal (NFS-e)** | 🟠 **Atrás** — 1 gap conhecido |
| **Mídia no prontuário** | 🟡 **Pouco atrás** — imagem e PDF sim; vídeo e áudio não |
| **Núcleo clínico e de gestão** | 🟢 **Empatados** |
| **Convênio / TISS / conformidade / financeiro profundo** | 🟢 **Muito à frente** |

**São 8 lacunas reais.** Três delas a própria cliente pôs fora de escopo em jul/2026
(telemedicina, portal do paciente e — por tabela — o SmartDocs). **Restam 5 que valem
decisão**, e duas delas custam pouco.

---

## 1. A distância que não é feature: desktop × nuvem

É a maior de todas e a única que não se fecha escrevendo uma tela.

| | My Smart Clinic | Nós |
|---|---|---|
| Onde abre | Navegador em Mac, Windows, Linux, Android e iOS ⚠️ | **Só Windows** (`net8.0-windows`, WPF) |
| Como chega | Login numa URL | **Cinco instaladores**, um por posto, com auto-update Velopack |
| Fora da clínica | Abre de casa, do celular | **Não abre** |
| App de celular | Sim, com prontuário e agenda | **Não existe** |

A cliente sente isso todo dia mesmo sem saber nomear: **a médica não vê a agenda do dia
no celular a caminho da clínica, e a direção não olha o painel no fim de semana.** Um
sistema que só existe dentro do prédio é lido como "sistema antigo" antes de qualquer
comparação de funcionalidade — e essa impressão contamina a avaliação de tudo o que vem
depois.

**Isto não se resolve por parcela.** O caminho honesto é um dos três, e nenhum é barato:
(a) uma aplicação web nova sobre o MESMO banco (a arquitetura permite — `Clinica.Domain`,
`Clinica.Application` e `Clinica.Infrastructure` não têm uma linha de WPF); (b) uma web
enxuta só de **leitura** (agenda do dia, painel, ficha), que cobre 80% do incômodo por uma
fração do custo; (c) assumir o desktop como posição e vender profundidade. **A (b) é a que
eu recomendaria começar**, e ela é a única coisa deste documento que muda a percepção do
produto por inteiro.

---

## 2. Onde estamos atrás, item a item

| # | O que eles têm | O que temos | Distância | Custo estimado |
|---|---|---|---|---|
| 1 | **Acesso web e mobile** (5 plataformas) | Só Windows, instalado | 🔴 Estrutural | Alto — projeto, não parcela |
| 2 | **Agendamento online pelo paciente** — link público da agenda, compartilhado por WhatsApp/site/redes; o paciente escolhe dia e hora sozinho | Nada. `AgendaService` só é alcançado pelo balcão | 🔴 Grande | Alto (exige web) |
| 3 | **Área/Portal do paciente** — ele acessa os próprios documentos e agendamentos | ❌ Fora de escopo (jul/2026) | 🔴 Grande | Alto (é produto) |
| 4 | **Telemedicina** — link seguro de teleconsulta, computador ou celular | ❌ Fora de escopo (jul/2026). Zero ocorrências de `telemedicin`/`webrtc` no `src/` | 🔴 Grande | Alto (é produto) |
| 5 | **IA na consulta** — transcreve, ajuda a anamnese, sugere hipóteses; assistente que resume a agenda do dia em áudio; SmartCRM com IA. Eles vendem "40% mais rápido" | **Zero.** Nenhuma ocorrência de `openai`, `anthropic` ou `transcri` no `src/` | 🔴 Grande | **Médio** — é integração de API, não produto novo |
| 6 | **Lembrete e confirmação automáticos** por WhatsApp, SMS e e-mail, com modelo e variáveis (`{paciente}`, `{horario}`, `{profissional}`) ⚠️ WhatsApp no plano Premium | A **rodada** é automática (acha quem confirmar, escreve a mensagem, aplica a LGPD, não repete ninguém), mas o **disparo é um clique por paciente** via `wa.me`. **Não há SMS e não há e-mail**: zero `SmtpClient`/`SendMailAsync` no `src/` | 🟠 Média | **Baixo/médio** — e-mail é barato; WhatsApp automático exige a API oficial da Meta (custo por mensagem) |
| 7 | **Nota fiscal (NFS-e)** emitida e amarrada ao financeiro | Não existe. Já catalogado como pendente, dependendo de integração municipal | 🟠 Média | Médio — depende do município |
| 8 | **Mídia rica no prontuário** — imagens, **vídeos** e **notas de áudio** | Anexo genérico (`AnexoProntuario`) com MIME e tipo `Imagem`/`Documento`/`Outro`, **teto de 10 MB, gravado no próprio banco**. Vídeo não cabe; áudio não tem porta | 🟡 Pequena | Baixo (áudio) / médio (vídeo — pede armazenamento externo, que **já temos**: `ArmazenamentoS3`) |

### Dois esclarecimentos que evitam conclusão errada

**O SmartDocs (paciente assina no próprio celular) não é a nossa assinatura.** Eles mandam
o documento para o PACIENTE assinar; nós assinamos com o e-CPF do PROFISSIONAL, em
ICP-Brasil qualificada, que é o que dá validade jurídica ao receituário perante a farmácia.
São coisas diferentes e a nossa é a mais forte — mas **a deles resolve o termo de
consentimento sem papel, e a nossa não**.

**A integração com a Memed é vantagem deles no comercial e não na técnica.** Eles dão
acesso Memed gratuito ao perfil médico ⚠️; nós fazemos a assinatura por dentro, com QR
para o validador de saúde do ITI. A ressalva que pesa é operacional e já está registrada:
**sem o e-CPF da clínica, a nossa assinatura não opera em produção.** Enquanto o
certificado não for comprado, na prática eles prescrevem com assinatura e nós não —
independentemente de quem tem a melhor engenharia. **Cobrar esse certificado é a ação de
maior retorno imediato deste documento inteiro.**

---

## 3. Onde estamos empatados

Nenhuma vantagem para nenhum lado; entram aqui para ninguém gastar parcela reconstruindo
o que já existe.

Agenda multiprofissional com recorrência/série · fila de check-in · prontuário
personalizável · cadastro de pacientes com CRM · financeiro com receitas, despesas,
relatórios e inadimplência · estoque · permissões por perfil · relatórios e BI ·
comunicação com o paciente (nós temos NPS e recall com regra de consentimento; eles têm
marketing de captação, que é outra coisa).

---

## 4. Onde estamos na frente — e é mais do que parece

Isto **não** é consolo: é o que sustenta o preço e é a razão de o produto existir. Nada
disto apareceu no material deles.

| O que temos | Eles |
|---|---|
| **TISS 4.01 completo** — motor de regras por convênio, lote → envio → retorno → glosa → recurso, XML com epílogo validado por hash, guia em PDF no leiaute ANS, importação do demonstrativo da operadora, radar de prevenção de glosa, cota de sessões | Não anunciado ⚠️ |
| **O 2º código** — data prevista +24h, semáforo, rodada bloqueante com prazo por guia e não conformidade justificada. É o defeito que dá nome ao produto | Não existe em sistema genérico |
| **Assinatura ICP-Brasil própria** — PAdES-B, PKCS#7 SHA-256, carimbo RFC 3161 opcional, CPF lido de dentro do certificado e conferido contra o profissional, QR do validador de saúde do ITI, publicação do PDF por link | Eles terceirizam (Memed/SmartDocs) |
| **Sala de infusão** — prescrição interna, checagem de enfermagem com horário informado, rodela com justificativa, retificação em linha nova, e a reação alérgica que volta para a lista de problemas e recusa a próxima prescrição | Não anunciado |
| **Escalas e medidas clínicas** — EVA, PHQ-9, GAD-7, Oswestry, Katz, FINDRISC, medidas seriadas com IMC derivado e faixas publicadas | Não anunciado |
| **Mapa corporal com protocolo reutilizável** | Não anunciado |
| **Financeiro profundo** — taxa de cartão com vigência, recebíveis de cartão por depósito, regime tributário por tributo com base presumida, retenção na fonte por convênio, rentabilidade por convênio (líquido por guia), custo de transação efetivo × tabela, fechamento de caixa em espécie, conciliação bancária OFX, metas e orçamento | Eles têm "financeiro e relatórios" |
| **Conformidade LGPD/13.787 auditada** — trilha de **leitura** do prontuário por porta de acesso, versionamento da evolução, guarda de 20 anos, exportação, anonimização, política de backup com rotação, auditoria imutável | Eles citam LGPD; nós temos o mapa dos dez pontos conferido em `conformidade-lgpd.md` |

**Traduzindo para a conversa comercial:** para uma clínica genérica, eles ganham. Para uma
**clínica de acupuntura que fatura convênio**, o que eles não fazem é justamente onde o
dinheiro da nossa cliente entra e vaza.

---

## 5. O que eu faria, em ordem

Ordenado por **impacto sentido pela cliente ÷ custo**, não por dificuldade técnica.

1. **Comprar o e-CPF.** Custo baixo, ação comercial e não técnica. Hoje é a única razão de
   a receita assinada — que está pronta, testada e publicável — não operar. Enquanto isso
   não acontece, perdemos uma comparação que já ganhamos.
2. **Envio automático de lembrete e confirmação.** Começar por **e-mail** (barato, não
   depende de terceiro) e decidir o WhatsApp oficial como item comercial, com o custo por
   mensagem na mesa. Fecha o gap nº 6, que é o que a recepcionista percebe todo dia.
3. **Web de LEITURA** — agenda do dia, painel da direção e ficha do paciente no navegador,
   sobre o mesmo banco. Não é o produto web inteiro; é a fração que mata a frase "não abre
   no meu celular", que é a maior perda de percepção do produto.
4. **Áudio e vídeo no prontuário.** O `ArmazenamentoS3` já existe (parcela 53) e resolve o
   teto de 10 MB. Custo baixo para uma diferença visível na tela do médico.
5. **IA na consulta.** Antes de construir: decidir. É integração de API, não produto novo,
   mas **dado de saúde saindo para serviço externo é transferência internacional pelo art.
   33** — o ponto 10 do nosso compromisso de conformidade. Não dá para fazer "só para
   testar" numa clínica que nos está auditando.
6. **NFS-e** — reavaliar quando houver um município definido.
7. **Telemedicina, portal e autosserviço do paciente** — continuam fora de escopo. Se a
   cliente voltar a pedir, entram como **produto**, com preço próprio; nenhum deles cabe
   num app WPF de balcão.

## 6. A ação que não é de código

Duas coisas deste levantamento não se resolvem programando, e as duas já estavam
registradas em `features-por-modulo.md`:

- **Telemedicina e Portal do paciente ainda aparecem na ARTE da sidebar dos mockups.**
  Enquanto estiverem lá, a cliente compara com a My Smart Clinic e vê que prometemos
  exatamente o que eles entregam e nós não. Precisam sair do deck.
- **"Confirmação automática por WhatsApp" (feature 02) precisa ser dita por extenso.** Nós
  automatizamos a rodada; eles automatizam o disparo. Se a cliente entendeu "automática"
  como "sozinho, sem ninguém clicar", ela está comparando a nossa promessa com a entrega
  deles — e nesse recorte perdemos sem precisar perder.
