# WhatsApp nativo na Recepção: o roteiro da manhã

> **Para a direção, antes de a clínica abrir.** Isto põe no ar a tela nova do WhatsApp na
> Recepção. São cinco partes, nesta ordem: conferir, publicar o CRM, publicar os apps, ligar
> e conferir a tela. Cada passo diz o que fazer e o que conferir depois.
>
> Tempo total: cerca de 1 h 30, quase tudo esperando o GitHub compilar.
>
> ⚠️ **Fase 3 (decisão da direção, 24/10): não existe mais a moldura nem a escolha "Tela nova /
> Moldura do CRM".** A Recepção nova abre sempre a tela nativa, e o CRM novo não abre mais
> dentro de app nenhum. Por isso, **faça as partes 1 e 2 em sequência, fora do horário de
> atendimento**: entre o CRM novo no ar e a Recepção atualizada, a aba WhatsApp das Recepções
> antigas fica em branco. Nesse intervalo, e em qualquer emergência depois, **as secretárias
> usam o CRM no navegador** (o mesmo endereço de sempre, as mesmas conversas).

Os detalhes de cada peça estão em `docs/whatsapp-nativo-implementacao.md`. O CRM em produção
está descrito em `semdor-crm/docs/producao-out2026.md` (VPS 2) e em
`docs/crm-em-producao-out2026.md` (VPS 1 e os apps).

---

## 0. Antes de começar (5 min)

| Conferir | Como | Se falhar |
|---|---|---|
| A aba WhatsApp de hoje funciona | Abra a Recepção e clique em **WhatsApp**. O CRM tem de abrir **sem pedir senha**. | A tela nova entra pelo mesmo bilhete. Sem isso ela também não entra. Pare e veja a ponte (VPS 1). |
| O GitHub Actions está liberado | github.com → Settings → Billing. Em 21/10 ele estava bloqueado por pagamento. | Sem Actions, os apps não são publicados. O CRM pode ser montado no PC (parte 1). |
| Os PRs estão abertos | Abra os dois PRs (o do `semdor-crm` e o do `Clinica`, da branch `claude/whatsapp-system-integration-ankslw` para a `main`). Os textos estão prontos no **Anexo** no fim deste documento. Espere o CI ficar verde. | Não publique com CI vermelho. |

**O que vai junto com o CRM.** O pacote leva tudo o que está na `main` do CRM, não só o
PR 0. Veja o que está no ar hoje, na VPS 2:

```text
ssh -o BatchMode=yes clinica-admin@191.252.92.246 "sudo cat /opt/semdor-crm/installation/state.json"
```

O campo `current` começa com os 12 primeiros caracteres do commit no ar. No PC, dentro do
repositório `semdor-crm`:

```text
git fetch origin
git log --oneline <os 12 caracteres>..origin/main
```

A lista mostra o que ainda não está no ar. A documentação registra `79f823e` (21/10) como a
última versão publicada. Se as fases 1 a 3 (PRs #36, #37 e #38) e o @SemDor (#32, #33 e #35)
aparecerem na lista, eles também vão junto.

Nada liga sozinho: cada função nova depende da sua variável no `production.env`. Mesmo
assim, saiba o que vai junto.

---

## 1. Publicar o CRM na VPS 2 (30 a 40 min)

O PR 0 está na branch `claude/whatsapp-system-integration-ankslw` do `semdor-crm`. Ele traz:

- as rotas que a tela nova usa: a lista com o tick e a releitura com `?desde=`;
- o teste que trava o contrato;
- **uma mudança de comportamento**: quando um membro da equipe não tem perfil gravado no CRM,
  o administrador dele passa a ser quem é administrador no Clinica, e não o primeiro da lista.

### 1.1 Juntar na `main`

O pacote se monta **a partir da `main`** (é a regra de `producao-out2026.md` §5).

```text
gh pr create -R lucaszaous-creator/semdor-crm --base main --head claude/whatsapp-system-integration-ankslw --title "API para os apps: lista com tick, ?desde= e teste de contrato (PR 0 do WhatsApp nativo)" --body "Ver docs/api-para-os-apps.md"
```

Espere o CI **"Testes do CRM"** ficar verde no PR e junte:

```text
gh pr merge <número> -R lucaszaous-creator/semdor-crm --merge
```

✅ **Conferir:** a `main` do CRM tem o merge (`git log -1 origin/main` depois de `git fetch`).

### 1.2 Montar o pacote no PC (PowerShell, na pasta do `semdor-crm`)

```powershell
git checkout main; git pull
$commit = git rev-parse HEAD
$pasta = "$env:TEMP\crm-$($commit.Substring(0,12))"
dotnet publish src/Clinica.Crm/Clinica.Crm.csproj -c Release -p:UseAppHost=false -p:IsTransformWebConfigDisabled=true -p:DebugType=None -p:DebugSymbols=false -o "$pasta\pub"
python tools/crm-release.py pack --published "$pasta\pub" --destination "$pasta\pacote" --commit $commit
```

O `pack` imprime três valores. **Anote o `releaseId` e o `manifestSha256`**.

Há um atalho, se o Actions estiver liberado: o CI da `main` já monta o mesmo pacote. Ele fica
no artefato `semdor-crm-<commit>` (`gh run download`), e o resumo está em
`crm-pacote-resumo.json`.

### 1.3 Copiar e instalar na VPS 2

```powershell
scp -r "$pasta\pacote" clinica-admin@191.252.92.246:/tmp/crm-pacote
```

Depois, na VPS 2 (`ssh clinica-admin@191.252.92.246`). Use **só esta chave e este usuário**:
o fail2ban bane o IP de quem erra.

```text
sudo chmod -R a+rX /tmp/crm-pacote
python3 /opt/semdor-crm/crm-release.py verify --package /tmp/crm-pacote --manifest-sha256 <manifestSha256>
sudo systemctl stop semdor-crm
sudo -u semdor-crm python3 /opt/semdor-crm/crm-release.py stage --package /tmp/crm-pacote --installation /opt/semdor-crm/installation --manifest-sha256 <manifestSha256>
sudo -u semdor-crm python3 /opt/semdor-crm/crm-release.py select --installation /opt/semdor-crm/installation --configuration /etc/semdor-crm/production.env --release-id <releaseId> --backup-destination /var/backups/semdor-crm/selecao-$(date +%Y%m%d)-<7 primeiros do commit>
sudo systemctl start semdor-crm
```

A ordem importa:

- o serviço **parado** antes do `stage`, porque com ele rodando a resposta é "Instalação em uso";
- o `select` faz e confere o backup dos dados antes de trocar a versão.

Cada comando responde uma linha em JSON. Se aparecer `"error"`, pare e não ligue o serviço com
outra versão.

✅ **Conferir:**

1. `curl -s https://crm.clinicasemdormacae.com.br/health/ready` responde **JSON** com
   `"status":"ready"`. Não é o texto `ready`: um roteiro que compare o texto puro dá a versão
   por quebrada sem motivo (aconteceu em 21/10).
2. Teste **pela URL pública**. Direto na porta 8080, sem o Caddy, a resposta é 400.
3. O CRM no navegador abre, entra e mostra as conversas.
4. Na Recepção ainda sem a versão nova, a aba WhatsApp (a moldura) **fica em branco** até a
   parte 2: é o esperado (o CRM novo não abre em moldura). As secretárias usam o CRM no navegador.
5. Em **Equipe**, confira quem aparece como administrador. É a mudança de comportamento
   do PR 0.
6. Apague o pacote copiado: `rm -rf /tmp/crm-pacote`.

**Voltar atrás no CRM:** com o serviço parado, troque `select … --release-id <releaseId>` por
`rollback` com os mesmos `--installation`, `--configuration` e um `--backup-destination` novo.
Depois ligue o serviço. O `rollback` volta para a versão anterior e não mexe nos dados.

---

### 1.1 Publicar a ponte na VPS 1 (10 min)

A regra de quem atende o WhatsApp mudou (25/10, decisão da direção: **só as secretárias e o
gerente**): perfil Recepção ou Gerente, com o acesso de editar a agenda. Quem decide a entrada no
CRM é a **ponte**, então ela precisa ser publicada para a regra valer também no CRM web — sem
isso, a faturista a quem a direção deu "editar agenda" ainda entraria pelo navegador. O roteiro é
o de sempre (`docs/crm-em-producao-out2026.md` §5: `dotnet publish src/Clinica.Crm.Bridge`,
`releases/ponte-<commit>`, `current`, `systemctl restart clinica-crm-bridge`). Conferir: entre no
CRM web com um usuário de outro perfil que tenha "editar agenda" — a resposta é "Seu usuário não
tem acesso ao CRM".

## 2. Publicar os apps: Recepção e Gerente (40 min)

**O CRM primeiro, depois os apps.** O app novo com o CRM antigo não quebra, porque a tela
nova continua desligada até a parte 4. Mas não haveria o que conferir.

### 2.1 Juntar o PR do Clinica

O PR leva a branch `claude/whatsapp-system-integration-ankslw` (rodadas 1 e 2 juntas) para a
`main`. O CI (`build-exe`) compila os cinco apps e roda a suíte inteira, em cerca de 25 min.
Com tudo verde:

```text
gh pr merge <número> -R lucaszaous-creator/Clinica --merge
```

**Não há migration nesta entrega.** Para conferir, este comando não deve listar nada:

```text
git diff --stat origin/main~1 origin/main -- src/Clinica.Infrastructure/Migrations
```

### 2.2 Quem precisa de versão nova

| App | Precisa? | Por quê |
|---|---|---|
| **Recepção** | sim | Carrega a aba WhatsApp |
| **Gerente** | sim | Carrega todos os módulos, inclusive a aba, e é onde fica a escolha da tela |
| Financeiro e Consultório | não | Carregam a Recepção só como contexto, sem o item WhatsApp no menu |
| Faturamento | **não** | Não tem a aba. Não republique sem necessidade: ele está em produção |

### 2.3 As tags, uma por vez

A próxima versão é a última tag **daquele** app + 1. Em 24/10 as últimas eram
`recepcao-v1.2.73` e `gerente-v1.2.86`. Confira de novo antes de criar:

```text
git checkout main; git pull; git fetch --tags
git tag -l "recepcao-v*" --sort=-v:refname | head -1
git tag -l "gerente-v*" --sort=-v:refname | head -1
```

Envie **uma tag por `git push`**. Com mais de três tags no mesmo push, o GitHub não dispara
nada (aconteceu em out/2026).

```text
git tag recepcao-v1.2.74
git push origin recepcao-v1.2.74
git tag gerente-v1.2.87
git push origin gerente-v1.2.87
```

O atalho sem tag é Actions → "Release" → Run workflow, uma execução por app:

```text
gh workflow run "Release (instalador + auto-update)" -R lucaszaous-creator/Clinica --ref main -f app=recepcao -f version=1.2.74
```

✅ **Conferir:**

1. Em Actions, as duas execuções de "Release" ficam verdes. O job `sincronizar-canais` pode
   aparecer **cancelado** quando duas rodam juntas. Isso é normal: basta o último ter passado.
2. Em Releases, `recepcao-v1.2.74` e `gerente-v1.2.87` têm os arquivos.
3. Abra o Gerente na sua máquina. Ele se atualiza sozinho e reabre.

---

## 3. O que mudou na tela de Configurações

Gerente → Configurações → **Integrações** tem só o **Endereço do CRM**, como antes da tela nova.
O bloco "WhatsApp dentro do app" (a escolha entre a tela nova e a moldura) **saiu** na fase 3:
com o endereço salvo, a aba WhatsApp da Recepção abre a tela nativa. Sem endereço, a aba diz
como ligar.

---

## 4. Ligar e conferir (20 min)

1. Confira no Gerente que Configurações → Integrações → **Endereço do CRM** está preenchido.
2. Numa máquina da Recepção: feche o app e abra de novo (ele também se atualiza). Entre com o
   **usuário de uma secretária** que tenha acesso à agenda.
3. Clique em **WhatsApp**.

Para os testes de envio, use **o seu celular e um paciente de teste**. Nunca um paciente de
verdade: tudo o que sai, sai pelo número oficial da clínica.

| ✅ | O que conferir |
|---|---|
| ☐ | **Entra sem senha.** A lista aparece sem nenhuma tela de login do CRM. |
| ☐ | **A lista aparece** com as conversas, os filtros Novas, Minhas, Todas e Encerradas, e as contagens. |
| ☐ | **Envia.** Mande uma mensagem do seu celular para o número da clínica e responda pela tela. Com a janela de 24 h fechada, só aparece "Escolher modelo". |
| ☐ | **O tick muda** de enviada para entregue e depois para lida, sem recarregar (a tela relê a cada poucos segundos). |
| ☐ | **Remarcar** um horário do paciente de teste pela coluna da direita. A agenda do balcão mostra a mudança. Depois, desfaça. |
| ☐ | **Retenção.** O seletor Conversas / Retenção mostra os grupos. Quem não autorizou mensagens só tem "Já liguei". **Não envie convite a paciente real** neste teste. |
| ☐ | **O sino leva à conversa.** Um aviso de confirmação no sino abre a conversa certa. |
| ☐ | **Mídia.** Mande do seu celular uma foto, um áudio e um PDF: a foto aparece no balão (clique amplia), o áudio toca, o PDF abre e baixa. |
| ☐ | **Figurinha e retrato.** Mande uma figurinha do seu celular: ela aparece pequena e solta (sem balão), e a lista diz "Figurinha". Se o paciente de teste tem foto na ficha, o rosto da ficha aparece no avatar da conversa depois de a ficha estar conferida (a foto do perfil do WhatsApp a Meta não fornece). |
| ☐ | **Gravar áudio.** O microfone aparece só se o CRM tiver o conversor de gravações; o Windows pede permissão do microfone na primeira vez. Grave 3 s e envie ao seu celular. |
| ☐ | **@SemDor** (se estiver ligado na VPS 2): ✨ com o campo vazio sugere a resposta; com texto, corrige, e ↶ volta. "✨ Resumo" mostra o resumo. |
| ☐ | **Conversa ao lado.** Na Agenda do dia, o ícone do WhatsApp na linha do paciente de teste abre a conversa à direita; escreva algo, feche com Esc e abra de novo: o texto continua. |
| ☐ | **Trocar usuário.** "Trocar usuário" reabre o app. Entre com outra pessoa, responda à conversa de teste e confira que a mensagem sai **com o nome dela**, não com o da anterior. |

Se a tela mostrar "O CRM não respondeu" ou "não aceitou o acesso", veja o log do app
(`Diagnostico`) antes de mexer em qualquer coisa.

### 4.1 Ligar o pedido de confirmação automático (recomendado, 5 min)

Decisão D11 (25/10, `docs/whatsapp-nativo-implementacao.md` §14): **recomendado ligar**. É uma chave
do CRM na VPS 2, não um release: liga e desliga sem tocar nos apps.

1. No **CRM pelo navegador**, entre com um usuário da **gestão** (só a gestão vê a Automação).
2. Abra **Mais → Automação → Ajustar modelos e horário** e confira: o número oficial, o modelo
   **`semdor_lembrete_sessao`** (aprovado na Meta, com os botões Confirmar, Remarcar e Cancelar) e a
   **hora**. O padrão é **06:00 do dia da sessão** (`ReminderHour` 6); "24 h antes" é a opção 0 (a véspera).
   Salve.
3. Volte a **Mais → Automação** e ligue **Pedido de confirmação**. O aviso diz "sai às 06:00 do dia da sessão".
4. Confira no dia seguinte, com um paciente de teste marcado para o dia: às 06:00 o lembrete chega ao
   celular; tocando **Confirmar**, a agenda do balcão mostra "Confirmou" e o sino avisa; sem tocar em
   nada, depois de 3 h o sino da Recepção e o painel do Gerente mostram **"sem resposta"** — a mesma
   palavra do CRM (R2).

| | Ligado | Desligado (como hoje) |
|---|---|---|
| 🧑‍💻 UX | Some a maior parte dos ~5 cliques por paciente da rodada; a recepção cuida só de quem não respondeu ou não vem. A mensagem de remarcação passa a dizer "Antes da sessão chega o lembrete para você confirmar." | A recepção avisa um por um pelo **Avisar os que faltam** (o aviso conta quando o lembrete SAI pela conversa). A mensagem de remarcação não promete lembrete. |
| 🛠️ DX | Nada a construir: a automação, a ponte e o aviso "sem resposta" já existem. | — |
| 🤝 CX | **Custo:** cada lembrete é uma mensagem de modelo de **utilidade, cobrada pela Meta** — uma por sessão marcada para o dia. Lembrete de horário marcado é transacional, não marketing. | Sem custo de mensagem; o custo é o tempo da recepção. |

Desligar é o mesmo caminho: **Mais → Automação → Pedido de confirmação**. Nada no app precisa de
versão nova para isso: a frase do lembrete na remarcação acompanha a chave na próxima leitura da aba.

### Se algo der errado

- **A aba WhatsApp não abre ou o CRM não responde:** as secretárias seguem no **CRM pelo
  navegador** (as mesmas conversas, o mesmo número). Veja o log do app (`Diagnostico`) antes de
  mexer em qualquer coisa.
- **Defeito na tela nova:** a correção é um release novo da Recepção (e do Gerente). Não existe
  volta automática de versão dos apps; a reserva, até o release sair, é o CRM no navegador.

Na VPS 2, a linha `CRM_EMBED_ORIGINS` do `/etc/semdor-crm/production.env` pode sair: o CRM novo
não a lê mais (não faz mal se ficar).

---

## 5. O que depende da direção

Nada disso trava o que está acima. São decisões para a próxima rodada.

1. ~~**Degraus 30, 20 e 15 dias na retenção.**~~ **Decidido em 24/10: um número só basta.**
   Fica o "Recall: dias sem vir" do Gerente (padrão 60). Nada a construir.
2. **Os modelos aprovados na Meta: resolvido durante a noite.** A tela passou a conhecer os
   doze `semdor_*` (os mesmos rótulos do CRM) e convida com `semdor_recall` (ou
   `semdor_iniciar_atendimento` para o novo no BSV). Se um modelo for aprovado com outra
   quantidade de variáveis, a tela mostra "Variável n" em branco para a secretária preencher.
3. ~~**"Como se sentiu depois da 1ª sessão de BSV"**~~ **Decidido em 24/10: não vira fila nova.**
   "Novos no BSV" continua sendo quem **ainda não** fez a 1ª sessão.
4. ~~**O convite contava duas tentativas no Acompanhamento.**~~ **Decidido e feito em 24/10:**
   fica só a linha do app. O convite da retenção vai ao CRM com `ContactRecordedByApp`
   (campo novo no fim do `AppReply`, os dois contratos idênticos, hash novo nos dois testes), e o
   CRM não manda o aviso dele à ponte. Mensagem de texto livre e modelo fora da retenção
   continuam sendo registrados pelo CRM, como antes. Testes:
   `RelationshipContactTests.Convite_que_o_app_ja_registrou_nao_conta_duas_vezes` (CRM) e o
   teste do convite em `WhatsAppDaRecepcaoServiceTests` (app). **Vai junto na publicação do CRM**
   (parte 1); a ordem CRM → apps continua a mesma. Com o CRM antigo e o app novo, o campo é
   ignorado e o convite volta a contar duas vezes até o CRM ser publicado; nada quebra.
5. ~~**Cancelar pela conversa**~~ **Decidido em 24/10: fica assim.** Grava o motivo na auditoria
   da agenda e numa nota interna; nenhuma mensagem vai ao paciente sozinha (a secretária escreve).
6. ~~**O PARAR do "Não quer voltar"**~~ **Decidido em 24/10: fica assim.** O "Não quer voltar"
   revoga a autorização só da ficha conferida (é sobre o tratamento daquela pessoa); o PARAR que
   o paciente manda pelo CRM revoga todas as fichas com aquele telefone (é o dono do celular).
7. ~~**Teste das telas no Postgres.**~~ **Resolvido na madrugada.** A sessão do Dr. Caio dentro
   das férias caía em "hoje + 20" e ele só atende terça e quinta: em cinco dias de cada sete o
   gatilho da agenda protegida recusava. A semente passou a escolher a terça ou quinta das férias
   e a gravar a sessão antes do bloqueio. Conferido no Postgres com o relógio em cada um dos sete
   dias da semana (a semente antiga reprova num sábado, a nova passa nos sete), e o
   `TelasBlazorAbremTests` inteiro passou no Postgres (161 de 161, cada botão clicado). Nada para decidir.

### O que fica para as fases 2 e 3

- ~~**Fase 2**~~ **Feita em 24/10** (branch `wa-painel`, `docs/whatsapp-nativo-implementacao.md` §10): a
  conversa abre numa **gaveta à direita** da Agenda do dia, da Ficha, de Confirmações e do
  Acompanhamento, sem trocar de tela — a mesma tela de conversa da aba, no modo painel. Para conferir
  na clínica: na Agenda do dia, o ícone do WhatsApp na linha de um paciente abre a conversa dele à
  direita; Esc fecha. Continua com a direção o destino da tela Acompanhamento.
- **Fase 3: feita em 24/10 (decisão da direção).** A moldura saiu inteira: a tela
  `WhatsAppNoCrm`, o roteador da aba, o CSS, a semente, a escolha em Configurações e, no CRM, o
  `modo=recepcao`, o `modo-recepcao.js`, o `CRM_EMBED_ORIGINS` e os testes deles. A entrada por
  bilhete **ficou**: é por ela que a tela nova entra.

---

## 6. 🧑‍💻 UX · 🛠️ DX · 🤝 CX

- 🧑‍💻 **UX**
  - **Direção:** a escolha fica num bloco só, ao lado do endereço do CRM. Uma frase explica
    cada opção. Quando a escolha está bloqueada, o bloco diz por quê. A volta é um clique.
  - **Secretária:** nada muda até a direção ligar. Depois, a aba WhatsApp é uma tela da casa:
    a conversa no meio, a ficha, os horários, a confirmação de hoje e o caso do Acompanhamento
    ao lado, lidos do próprio app. Remarcar, confirmar e cancelar são os botões da agenda. O
    que pede ação aparece dentro da conversa e no sino. A retenção é o segundo modo da mesma
    lista: quem sumiu, um por um, com o convite preenchido para revisar. Sem senha, sem trocar
    de sistema. Fotos em `docs/mockups/whatsapp-nativo/tela-real/`.
  - **Saída do app:** "Sair" e "Trocar usuário" encerram a sessão da pessoa no CRM na hora.
    Sem isso, ela continuava valendo no servidor até vencer (2 h).
  - Fotografado em 1024 e 1366, com e sem endereço do CRM
    (`docs/mockups/whatsapp-nativo/tela-real/`).
- 🛠️ **DX**
  - **As frases e a regra da escolha** moram em `ConfiguracoesDaClinicaNaTela`
    (`PorQueSemEscolhaDoWhatsApp`, `FraseWhatsAppSalvo`), com teste.
  - **A chave** é `ParametrosService.ChaveWhatsAppNativo`. O teste fixa o padrão desligado,
    a gravação, a releitura e que só o valor `true` liga.
  - **A saída do CRM** tem um ponto só: o fechamento da `JanelaDoApp`. "Trocar usuário" reabre
    o processo, então passa por ali também. A peça é `Infrastructure/Crm/SaidaDoCrm`, testada
    em `ClienteDoCrmHttpTests`: o logout já chegou ao CRM quando ela volta, e o CRM fora do ar
    não lança.
  - **A espera tem teto de 2 s.** Sem ela, o aviso morria com o processo e a chamada não fazia
    o que promete.
- 🤝 **CX**
  - **Custo:** nenhum servidor novo e nenhuma migration. São dois apps republicados, mais o
    CRM.
  - **Risco:** controlado pela escolha. Ela nasce desligada, liga quando a direção quer e volta
    sem release. O CRM tem o próprio `rollback`.
  - **Confiança:** a direção liga antes de a clínica abrir e confere com o próprio celular.
    Paciente nenhum recebe teste.

## 7. O que NÃO foi feito, e por quê

O que tinha ficado "para depois" foi feito em 24/10, a pedido da direção: **remarcar direto na
conversa** (o formulário de remarcar da própria agenda, sem o cartão do horário no meio), **mídia**
(ver foto, ouvir áudio, ver vídeo, abrir e baixar documento, recebidos e enviados), **gravar áudio**,
**o @SemDor** (✨ sugerir ou corrigir, resumo, etiqueta na lista; o aviso de atenção no sino abre a
conversa na aba), **a fase 2** (a conversa ao lado da Agenda do dia, da Ficha, de Confirmações e do
Acompanhamento) e **a fase 3** (a moldura saiu inteira). Detalhes em
`docs/whatsapp-nativo-implementacao.md` §10 a §12. Em 25/10 entraram **as figurinhas** (pequenas e
soltas, como no WhatsApp) e **o retrato da ficha no avatar** da conversa (§13). Continua de fora:

- **Publicar, criar tag ou disparar workflow.** O roteiro é para a direção executar. Nenhum
  comando acima foi rodado.
- **O logout do Faturamento.** Ele não tem a aba WhatsApp nem sessão no CRM.
- **Microfone e @SemDor de verdade não foram testados aqui**: a chave da Anthropic e o conversor de
  gravações ficam na VPS 2, e o microfone é do Windows da clínica. Conferir na parte 4.
- **Ouvir a gravação antes de enviar, capa do vídeo e tamanho do documento antes de baixar**: o
  WhatsApp Web também não mostra o tamanho antes; os outros dois pedem peça nova (gravação em dois
  passos; baixar o vídeo inteiro para tirar a capa).
- **A gaveta ao lado cobre a tela em vez de empurrá-la**: empurrar exigiria que cada tela coubesse
  em ~900 px. Em 1024, a coluna de ações da Agenda do dia fica sob a gaveta; fecha-se com Esc.
- **A foto do perfil do WhatsApp**: a API oficial da Meta entrega o nome do perfil, nunca a foto.
  O avatar mostra o retrato da FICHA (o do balcão), quando a conversa está ligada a uma.
- **O destino da tela Acompanhamento** (funil e taxa de retorno) continua com a direção.

---

## Anexo: os textos dos dois PRs

Os dois saem da branch `claude/whatsapp-system-integration-ankslw` para a `main`. Abra o do
CRM primeiro (ele é publicado antes).

### PR do `semdor-crm`

**Título:** API para os apps da clínica e o fim do modo Recepção (WhatsApp nativo)

```markdown
## O que muda

A aba WhatsApp da Recepção vira uma tela nativa do app que fala com `/api/*`. Este PR faz a API
virar contrato para os apps e tira daqui a moldura (fase 3).

- `ApiParaOsApps.cs`: os records `App*` que o app lê e escreve, idênticos ao `ContratosDoCrm.cs`
  do Clinica (SHA-256 conferido nos dois repositórios). Campos novos sempre no fim, com padrão:
  `AppReply.ContactRecordedByApp`, `AppConversationSummary.Semdor`, `AppSession.RecordingEnabled`,
  `AppMessage.Sticker`.
- **Figurinha**: entra como `IMAGE` com a marca `Sticker`; a lista diz "Figurinha", o CRM web e o
  app a desenham pequena e solta, e o @SemDor lê "[figurinha]".
- `GET /api/conversations` com o tick da lista e `?desde=` (só o que mudou, inclusive a etiqueta
  nova do @SemDor, guardada fora da conversa).
- **@SemDor para os apps**: as rotas de estado, sugestão e resumo entram no contrato.
- **O convite da retenção não conta duas tentativas**: quando o app já gravou o contato no
  Acompanhamento, o CRM não manda o aviso dele à ponte.
- **Fase 3: o modo Recepção sai inteiro.** Saem o `?modo=recepcao`, o `modo-recepcao.js`, o
  `CRM_EMBED_ORIGINS`, os cookies `SameSite=None` da moldura e os testes deles; o CSP volta a
  `frame-ancestors 'none'`. **Fica** a entrada por bilhete (`POST /api/login/ticket`).
- **Mudança de comportamento:** sem perfil gravado, o administrador padrão é quem é administrador
  no Clinica, e não o primeiro da lista da ponte.

## UX · DX · CX

- 🧑‍💻 **UX:** o CRM web não muda para quem o usa no navegador. A secretária passa a ter a conversa,
  a ficha, a agenda, a mídia e o @SemDor na mesma janela da Recepção.
- 🛠️ **DX:** um arquivo de records e um teste que trava a forma nos dois repositórios; a moldura,
  com três peças e dois testes, deixa de existir.
- 🤝 **CX:** sem servidor novo. ⚠️ Publicar este CRM deixa a aba WhatsApp das Recepções ANTIGAS em
  branco até elas atualizarem: publique o CRM e os apps em sequência, fora do horário (roteiro da
  manhã). A linha `CRM_EMBED_ORIGINS` do `production.env` pode sair.

## Testes

`dotnet test SemDor.Crm.sln -c Debug`: 818 + 20 (ponte). `node --test tests/*.test.cjs`: 149.

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01HpPiBY5jRicdjB3mfhNSrv
```

### PR do `Clinica`

**Título:** WhatsApp nativo na Recepção: a tela, a mídia, o @SemDor, a conversa ao lado e o fim da moldura

```markdown
## O que muda

A aba WhatsApp da Recepção é uma tela da casa (Blazor), sobre a API do CRM. A moldura do CRM saiu.

- **A tela** (`ConversasDoWhatsApp`, `WhatsAppDaRecepcaoService`, `Formularios/WhatsApp/*`):
  conversas, janela de 24 h, os doze modelos `semdor_*`, conferir de quem é a ficha, retenção com
  convite, cadastrar com o telefone da conversa, "Ver caso" no Acompanhamento, remarcar direto no
  formulário da agenda, celular (≤ 760 px) com todas as ações.
- **Mídia**: foto (miniatura e ampliada), áudio, vídeo e documento recebidos e enviados, com abrir
  e baixar; **gravar áudio** (o microfone só aparece com o conversor de gravações no CRM; o WebView2
  libera o microfone só para a origem do app).
- **Figurinha e retrato**: a figurinha pequena e solta (`AppMessage.Sticker`), e o retrato da ficha
  ligada no avatar da lista, do cabeçalho e do cartão (a foto do perfil do WhatsApp a Meta não dá).
- **@SemDor**: ✨ sugere ou corrige a resposta (↶ volta), resumo da conversa, etiqueta de assunto na
  lista; o aviso de atenção no sino abre a conversa na aba.
- **Fase 2, a conversa ao lado**: uma gaveta à direita da Agenda do dia, da Ficha, de Confirmações
  e do Acompanhamento, com a mesma tela de conversa; o rascunho é da janela e sobrevive à gaveta.
- **Fase 3, a moldura sai inteira**: `WhatsAppNoCrm`, o roteador da aba, a escolha em Configurações,
  o parâmetro dela e o endereço com `?modo=recepcao`. Fica o bilhete de entrada.
- **Cliente do CRM** (`IClienteDoCrm`, `ClienteDoCrmHttp`): sessão por pessoa pelo bilhete, renova
  no 401 e no CSRF recusado, prazos próprios para mídia e @SemDor.
- Sem migration.

## As regras de tela de quem atende

1. **Uma porta por pergunta:** a mesma tela de conversa na aba e na gaveta ao lado; remarcar usa o
   formulário da agenda.
2. **Função que saiu, sai inteira:** a moldura saiu do app e do CRM, com testes e textos.
3. **O que pede ação vira aviso:** os selos vêm da lista do sino; o aviso do @SemDor abre a conversa.
4. **O telefone não é identidade:** duas fichas no telefone abrem "Qual ficha é esta pessoa?".
5. **Relacionamento é de quem trata:** a retenção só traz BSV, acupuntura e os dois.
6. **Responsivo:** fotos em 1366, 1024 e 480; o roteiro de fotos reprova rolagem lateral, botão
   que vaza do grupo e ação escondida no celular.

## UX · DX · CX

Ver `docs/whatsapp-nativo-implementacao.md` (§7 a §12) e `docs/whatsapp-nativo-roteiro-da-manha.md` §6.

## Testes

- Locais (Linux): build `--no-incremental` sem warning; `compilar-sombra` (13 projetos WPF);
  `verificar-suite`; `TelasBlazorAbremTests` e as famílias do WhatsApp, do CRM, do Acompanhamento,
  de Configurações, da mídia, do @SemDor e da conversa ao lado (596 de 596 na árvore juntada); as
  telas também no Postgres.
- Fotos: `tools/PreviaBlazor/fotos-whatsapp.js`, `fotos-semdor.js` e `fotos-conversa-ao-lado.js`.
- **Não rodou localmente:** a suíte inteira e os cinco apps no Windows (é este CI); o microfone real
  e o @SemDor real (VPS 2); o tablet e a web de leitura (não foram tocados).

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01HpPiBY5jRicdjB3mfhNSrv
```
