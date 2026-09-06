# O quanto estamos atrás da My Smart Clinic

> Levantamento da distância entre o nosso produto e o **My Smart Clinic**
> (`mysmartclinic.com.br`) — o sistema que a nossa cliente usa hoje. **Ago/2026.**
>
> A base é a **página de planos e preços deles**, lida item a item, e o **nosso código** —
> não `features-por-modulo.md` e não a memória. Toda linha de "estamos atrás" foi conferida
> com busca no `src/`, e é por isso que algumas dizem "zero ocorrências".

## Fora de escopo por decisão nossa

**Notas Fiscais, Portal do paciente e Telemedicina/Teleconsulta não entram.** Não são
lacunas: são itens que a cliente decidiu não querer (jul/2026, reafirmado ago/2026). Ficam
listados só onde ajudam a ler o preço deles.

⚠️ **Duas linhas ficaram sem decisão e são suas, não minhas** — o **Agendamento online** e o
**SmartDocs** costumam ser confundidos com "portal do paciente" e **não são a mesma coisa**.
Estão marcadas com ⚠️ ao longo do documento.

---

## O que eles cobram

| Plano | Assinatura | Com 2 profissionais e os 3 adicionais |
|---|---|---|
| **PRO** | R$ 189/mês (anual) | R$ 475/mês |
| **PREMIUM** | R$ 259/mês (anual) | R$ 545/mês |
| **ULTRA** | R$ 389/mês (anual) | **R$ 438/mês** |

Cada plano inclui **1 assento de profissional de saúde**; do segundo em diante são
**+R$ 49/mês** cada. Usuários administrativos (secretaria, financeiro) são ilimitados.
Os adicionais — **SmartDocs, Notas Fiscais e SmartCRM** — custam **+R$ 79/mês cada** no PRO
e no PREMIUM, e vêm **inclusos no ULTRA**.

**A leitura comercial que a tabela deles entrega de graça:** quem quer os três adicionais
paga **menos** no ULTRA (R$ 438) do que no PRO (R$ 475) ou no PREMIUM (R$ 545). O ULTRA é o
plano desenhado para ser escolhido — e é onde eles põem o **Estoque**, que nós entregamos
por padrão.

Para dimensionar: uma clínica com **cinco profissionais** no ULTRA paga **R$ 585/mês**, ou
**R$ 7.020/ano**.

---

## O placar

**Não estamos atrás na mesma direção em que estamos na frente.** Retirados os três itens
fora de escopo, sobram **12 diferenças**, e a maioria é pequena. Nenhuma delas está no
núcleo do que a nossa cliente faz.

| Bloco | Situação |
|---|---|
| **Comunicação automática** (WhatsApp sem clique, SMS, e-mail) | 🟠 **Atrás no WhatsApp sem clique e no SMS.** O lembrete da sessão por **e-mail** sai sozinho desde set/2026 (`LembreteEmailService`) |
| **IA no prontuário** | 🔴 Atrás — zero |
| **Alcance** (web/celular) | 🔴 Atrás — estrutural, e não está na lista deles porque para eles é o básico |
| **Integrações** (agendas externas, RD Station) | 🟠 Atrás |
| **Prontuário: campos personalizados, vídeo, foto antes/depois** | 🟡 Pouco atrás |
| **Chat interno** | 🟡 Pouco atrás |
| **Núcleo clínico, agenda, pacientes, relatórios** | 🟢 Empatados |
| **Financeiro, estoque, TISS, assinatura, conformidade** | 🟢 **Muito à frente** |

---

## 1. Item a item, a lista deles

### Plano PRO — o que está na base

| O que eles listam | Nós | Distância |
|---|---|---|
| Gestão de pacientes | `PacientesView` + ficha 360º com abas | 🟢 Empate |
| Multiagenda | Agenda multiprofissional, por sala, semana, série, encaixe, lista de espera | 🟢 **À frente** |
| Lembretes e confirmação por WhatsApp | A **rodada** é automática (acha quem confirmar, escreve, aplica a LGPD, não repete ninguém); pelo WhatsApp o **disparo é um clique por paciente** no `wa.me`; por **e-mail o disparo é automático** desde set/2026 (abertura da Recepção e do Gerente) | 🟡 Pouco atrás |
| Prontuário personalizado | Modelo de evolução e modelo de documento (**texto**). **Não há campo personalizado** — zero `CampoPersonalizado` no `src/` | 🟡 Pouco atrás |
| Prescrição eletrônica com Memed | Assinatura **ICP-Brasil própria** (PAdES-B, QR do validador do ITI) | 🟢 **À frente** ⚠️ travada pelo e-CPF |
| Financeiro e orçamentos | Caixa, contas, fluxo, fechamento, tributos, recebíveis, conciliação OFX, orçamento | 🟢 **Muito à frente** |
| **Marketing por SMS, e-mail e WhatsApp** | **Zero SMS.** E-mail: só o LEMBRETE transacional da sessão (set/2026, `EnviadorSmtp`) — **marketing por e-mail continua não existindo** (exige consentimento e disparo em massa, que o envio um a um não é). WhatsApp por clique | 🟠 Atrás |
| Relatórios | BI com gráficos, CSV, metas, rentabilidade por convênio | 🟢 **À frente** |
| Usuários administrativos ilimitados | Não cobramos por assento | 🟢 Empate (ou melhor) |
| **Integração de agendas externas** | Não existe — zero `iCalendar`/`CalDav`/Google Calendar | 🔴 Atrás |
| Armazenamento de fotos, vídeos e documentos | Anexo com MIME, **teto de 10 MB gravado no próprio banco**. Foto e PDF sim; **vídeo não cabe** | 🟡 Pouco atrás |
| **Comparativo antes e depois** | Fazemos com **número** (EVA antes/depois da sessão, no relatório de evolução), **não com foto** | 🟡 Pouco atrás |
| Simulações e consulta interativa | Não temos | ⚪ É recurso de **estética**; não serve acupuntura |
| **Chat de comunicação interna** | Não existe | 🟡 Pouco atrás |

### Plano PREMIUM — o que eles põem acima da base

| O que eles listam | Nós | Distância |
|---|---|---|
| Teleconsulta | — | ❌ **Fora de escopo** |
| ⚠️ **Agendamento online** | Não existe. **Não é o portal do paciente**: é um link público da agenda para o paciente escolher dia e hora sozinho | ⚠️ **Decisão sua** |
| **Confirmação automática via WhatsApp** | Pelo WhatsApp o disparo continua sendo um clique por paciente; por e-mail sai sozinho (set/2026) | 🟠 **Atrás no canal que eles cobram** |
| **Inteligência Artificial no prontuário** | **Zero** — nenhum `openai`, `anthropic` ou `transcri` no `src/` | 🔴 Atrás |
| Contratos e Termos | Termo de consentimento clínico + consentimento LGPD com histórico. Não há contrato genérico assinável | 🟡 Pouco atrás |
| Integração com RD Station | Não existe | 🟠 Atrás (baixa prioridade) |

### Plano ULTRA — os adicionais

| O que eles listam | Nós | Distância |
|---|---|---|
| **Estoque** (+R$ 79 ou plano de R$ 389) | Estoque com saldo por movimento, lote com validade, alerta de mínimo, acerto de inventário, custo por sessão | 🟢 **À frente — e de graça** |
| SmartCRM (+R$ 79) | CRM com origem, indicação e contatos + campanhas de NPS e recall com regra de consentimento. Não é o mesmo funil de vendas | 🟡 Pouco atrás |
| ⚠️ SmartDocs (+R$ 79) | O paciente assinar no próprio celular não existe aqui. **Não é a nossa assinatura**: a nossa é do PROFISSIONAL (e-CPF, ICP-Brasil); a deles é do PACIENTE | ⚠️ **Decisão sua** |
| Notas Fiscais (+R$ 79) | — | ❌ **Fora de escopo** |

---

## 2. A diferença que não está na lista deles

**Eles são web; nós somos Windows instalado.** Isso não aparece como feature na página de
preços porque, para um SaaS, é o chão — e é justamente por isso que pesa: o cliente não lê
"acesso web" como vantagem deles, lê "não abre no meu celular" como defeito nosso.

Na prática: a médica não vê a agenda do dia a caminho da clínica, e a direção não olha o
painel no fim de semana. **Não se resolve por parcela.** O caminho barato é uma **web só de
LEITURA** sobre o mesmo banco — agenda do dia, painel e ficha —, que mata a frase sem
construir o produto web inteiro. As três camadas de baixo (`Domain`, `Application`,
`Infrastructure`) não têm uma linha de WPF, então o banco e as regras já servem.

---

## 3. Onde estamos muito à frente

Nada disto aparece na página deles, em nenhum dos três planos. **É o que sustenta o nosso
preço.**

- **TISS 4.01 completo** — motor de regras por convênio, lote → envio → retorno → glosa →
  recurso, XML validado por hash, guia em PDF no leiaute ANS, importação do demonstrativo
  da operadora, radar de prevenção de glosa, cota de sessões.
- **O 2º código** — data prevista +24h, semáforo, rodada bloqueante com prazo por guia e
  não conformidade justificada. É o defeito que dá nome ao produto, e nenhum sistema
  genérico o conhece.
- **Assinatura ICP-Brasil própria** — PAdES-B, PKCS#7 SHA-256, carimbo RFC 3161, CPF lido
  de dentro do certificado e conferido contra o profissional, QR do validador de saúde do
  ITI, publicação do PDF por link. Eles terceirizam para a Memed.
- **Sala de infusão** — prescrição interna, checagem de enfermagem com horário informado,
  rodela com justificativa, retificação em linha nova, e a reação alérgica que volta para a
  lista de problemas e recusa a próxima prescrição.
- **Escalas e medidas clínicas** — EVA, PHQ-9, GAD-7, Oswestry, Katz, FINDRISC, medidas
  seriadas com IMC derivado.
- **Mapa corporal com protocolo reutilizável.**
- **Financeiro profundo** — taxa de cartão com vigência, recebíveis por depósito, regime
  tributário por tributo com base presumida, retenção na fonte por convênio, rentabilidade
  por convênio (líquido por guia), custo de transação efetivo × tabela, fechamento de caixa
  em espécie, conciliação bancária OFX, metas e orçamento.
- **Conformidade LGPD/13.787 auditada** — trilha de **leitura** do prontuário por porta,
  versionamento da evolução, guarda de 20 anos, exportação, anonimização, política de
  backup com rotação, auditoria imutável.
- **Estoque** — que eles cobram R$ 79/mês ou empurram para o plano de R$ 389.

**Traduzindo:** para uma clínica genérica, eles ganham. Para uma **clínica de acupuntura que
fatura convênio**, o que eles não fazem é exatamente onde o dinheiro da nossa cliente entra
e vaza.

---

## 4. O que eu faria, em ordem

Ordenado por **impacto sentido ÷ custo**, não por dificuldade.

1. **Comprar o e-CPF.** Não é código. A receita assinada está pronta, testada e publicável,
   e não opera sem ele. Hoje eles prescrevem com assinatura e nós não — perdendo em campo
   uma comparação que ganhamos na engenharia.
2. **Disparo automático de lembrete e confirmação.** É o gap que a recepcionista sente todo
   dia. **A metade do e-mail está feita (set/2026)** — `LembreteEmailService`, na abertura
   da Recepção e do Gerente, com o servidor cadastrado em Configurações; barato e sem
   terceiro. O WhatsApp sem clique exige a **API oficial da Meta** (conta Business, custo por
   mensagem) e é decisão comercial, não técnica. O marketing por e-mail NÃO veio junto: é
   outro ato (exige consentimento) e outro volume.
3. **SMS** — mesmo motor do item 2, provedor diferente. Só depois de decidir se a clínica
   quer pagar por mensagem.
4. **Vídeo e áudio no prontuário.** O `ArmazenamentoS3` já existe (parcela 53) e resolve o
   teto de 10 MB do banco. Custo baixo, diferença visível na tela do médico.
5. **Campos personalizados no prontuário.** É o único item de "prontuário personalizado" que
   não temos, e é o que uma clínica de outra especialidade pede na primeira semana.
6. **Foto antes/depois.** Depende do item 4. Para acupuntura vale menos que para estética,
   mas é barato depois que o armazenamento estiver de pé.
7. **Web de leitura** — agenda, painel e ficha no navegador. Maior impacto de percepção do
   documento inteiro, e o maior custo entre os itens viáveis.
8. **IA no prontuário.** Antes de construir, **decidir**: é integração de API, não produto
   novo, mas **dado de saúde saindo para serviço externo é transferência internacional pelo
   art. 33** — o ponto 10 do nosso compromisso de conformidade. Não dá para fazer "só para
   testar" numa clínica que nos está auditando.
9. **Chat interno, integração de agendas externas, RD Station** — os três só se o cliente
   pedir. Nenhum deles muda uma decisão de compra sozinho.

### As duas decisões que são suas

- ⚠️ **Agendamento online** (link público da agenda). Se ficar de fora junto do portal,
  tudo bem — mas precisa ser escolha, e não confusão com o portal: é o item da lista deles
  que mais aparece em comparação de concorrente, e é o único que reduz trabalho do balcão
  sem depender do balcão.
- ⚠️ **SmartDocs** (o paciente assina no celular). Resolve o **termo de consentimento sem
  papel**, que é um documento que a nossa cliente emite e arquiva à mão hoje. Não é
  telemedicina nem portal.

---

## 5. A ação que não é de código

- **Telemedicina e Portal do paciente ainda estão na ARTE da sidebar dos nossos mockups.**
  Enquanto estiverem lá, prometemos por escrito exatamente os dois itens que decidimos não
  fazer — e que eles entregam. Precisam sair do deck.
- **"Confirmação automática por WhatsApp" (feature 02) precisa ser dita por extenso.** Nós
  automatizamos a rodada e, desde set/2026, o disparo por E-MAIL; pelo WhatsApp o disparo é
  um clique, e eles automatizam esse — e cobram isso no PREMIUM. Se a cliente
  entendeu "automática" como "sozinho, sem ninguém clicar", ela está comparando a nossa
  promessa com a entrega deles — e nesse recorte perdemos sem precisar perder.
- **O Estoque é argumento de venda e ninguém está usando.** Eles cobram R$ 79/mês por ele,
  ou empurram para o plano de R$ 389. Está na nossa base desde a parcela 4.
