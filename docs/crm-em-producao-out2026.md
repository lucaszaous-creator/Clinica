# O CRM em produção — o diário da instalação e do que veio depois (out/2026)

Este documento conta o que foi feito entre 17 e 18/10/2026 para pôr o **SemDor CRM** no ar.
Vai do primeiro acesso às duas VPS até a última versão dos cinco apps. O roteiro genérico continua em
[`vps-do-crm.md`](vps-do-crm.md). Aqui fica **o que aconteceu de fato**: os endereços, as
decisões, os erros que custaram rodadas e o estado em que tudo ficou. O lado do CRM está
espelhado em `semdor-crm/docs/producao-out2026.md`.

> **Nenhum segredo neste arquivo.** A chave da ponte, a senha do papel `ponte_crm`, a senha do
> `.pfx`, a do webhook e a chave da Brevo nunca foram exibidas nem escritas em lugar nenhum
> além dos arquivos de configuração das VPS. Quem precisar delas lê no servidor, com `sudo`.

---

## 1. O mapa

| Peça | Onde | Detalhe |
|---|---|---|
| **VPS 1 — clínica** | Locaweb `vps69933`, IP `177.153.66.251` | Postgres da clínica (porta 45432, mTLS) e a **ponte do CRM** (`Clinica.Crm.Bridge`) |
| **VPS 2 — CRM** | Locaweb `vps72441`, IP `191.252.92.246` | O CRM web (`semdor-crm`), Caddy e HTTPS |
| **Túnel** | WireGuard `10.8.0.1` (VPS 1) ↔ `10.8.0.2` (VPS 2) | A ponte escuta **só** em `10.8.0.1:5190` e exige a chave Bearer |
| **Tela das secretárias** | `https://crm.clinicasemdormacae.com.br` | Caddy → `127.0.0.1:8080` |
| **Entrada de fora** (webhooks, mídia) | `https://avisos.clinicasemdormacae.com.br` | Caddy → `127.0.0.1:8081` (todas as outras rotas dão 404 ali) |
| **Link de confirmação do SMS** | `https://confirma.clinicasemdormacae.com.br` | Worker da Cloudflare (`tools/worker-clinica.js`), com a logo |
| **DNS** | Cloudflare | `crm` e `avisos` com registro A **em "DNS only" (nuvem cinza)**: com o proxy laranja, o Let's Encrypt do Caddy não emite |
| **SMS** | Brevo (SMS transacional) | Provedor `generic` do CRM e da ponte; IPs das duas VPS liberados na Brevo |

## 2. Acesso às VPS — como entrar e o que NÃO fazer

- **SSH só como `clinica-admin`, com a chave desta máquina.** O `root` está bloqueado
  (`PermitRootLogin no`). Na VPS 2, a chave foi posta pelo cloud-init na criação.
- **fail2ban está ativo nas duas.** Repetir tentativas com usuário ou chave errados bane o IP
  de quem tenta, e o desbloqueio exige o painel da Locaweb. Use sempre `ssh -o BatchMode=yes`
  em scripts: falha na hora, em vez de ficar pedindo senha.
- **`sudo` sem senha foi TEMPORÁRIO.** Para instalar sem pedir senha a cada passo, a VPS 1
  recebeu `/etc/sudoers.d/90-instalacao-crm`, **removido em 18/10/2026**. Desde então o `sudo`
  pede a senha do `clinica-admin`, que é do grupo `sudo`. Na VPS 2, o `sudo` sem senha vem do
  `90-cloud-init-users`, criado pela Locaweb com a máquina, e continua lá; se a direção quiser a
  senha também ali, é esse o arquivo a ajustar. Os arquivos temporários da instalação foram
  apagados do `/tmp` das duas.
- **Configuração e segredos**:
  - VPS 1: o arquivo de ambiente do serviço `clinica-crm-bridge`, com `CRM_BRIDGE_KEY`, a
    cadeia do papel `ponte_crm` e as chaves de etapa.
  - VPS 2: `/etc/semdor-crm/production.env` (root:semdor-crm, 640), `accounts.json` e
    `chaves.pfx` em `/etc/semdor-crm/`.

## 3. A ordem da instalação (17/10)

1. **Túnel WireGuard** entre as duas VPS (`wg0`); a ponte recusa subir fora do IP do túnel.
2. **Papel restrito no Postgres**: `deploy/ponte-crm/papel-ponte-crm.sql` cria o `ponte_crm`. Ele
   grava **só** nas tabelas que a ponte grava (lista medida pelo `PontePapelRestritoTests`) e lê
   as três colunas de `Usuarios` que o login precisa.
3. **Ponte na VPS 1**: self-contained linux-x64 em `/opt/clinica-crm-bridge/releases/<id>`,
   link `current` e serviço systemd `clinica-crm-bridge`. Backups `*.antigo-20261017` do serviço
   e do env anteriores.
4. **CRM na VPS 2**: usuário de serviço `semdor-crm`, instalação versionada em
   `/opt/semdor-crm/installation`, dados em `/var/lib/semdor-crm`, Caddy e certificados.
5. **DNS**: registros A de `crm` e `avisos` para `191.252.92.246`, em "DNS only".
6. **SMS**: a Brevo foi escolhida. O SMS Token só serve OTP e a iAgente ficou presa em aprovação
   de conta. Teste isolado entregue no celular da equipe **antes** de ligar qualquer envio,
   conforme a seção 6 do roteiro.

### Os erros que custaram rodadas, e o que ensinam

- **HTTP 400 no CRM sem os cabeçalhos do proxy**: é o esperado. O CRM exige
  `X-Forwarded-Proto` do Caddy (`CRM_TRUSTED_PROXIES=127.0.0.1`). Teste pela URL pública,
  nunca direto na 8080.
- **`crm-release.py stage` recusou com "Instalação em uso"**: o serviço precisa estar
  **parado** para preparar a versão. A ordem certa é parar → `stage` → `select` (com
  `--backup-destination`) → iniciar.
- **Brevo devolveu 401 "unrecognised IP"**: a Brevo exige liberar o IP de quem chama. Os
  **dois** IPs foram liberados: a VPS 2 manda o SMS do CRM e a VPS 1 manda o lembrete da ponte.
- **Eventos "rejected" na Brevo**: os créditos de SMS tinham acabado. A clínica recarregou.
- **iPhone pôs o SMS em "Promoções"**: o filtro do iOS olha o TEXTO. As duas medidas adotadas
  foram texto **transacional** (o que a clínica fez, sem oferta) e pedir ao paciente para
  responder ou salvar o contato.
- **Conversa de teste sem o 55**: o CRM passou a normalizar o telefone brasileiro na
  criação da conversa (`Store.BrazilianPhone`).

## 4. A ligação por etapas (seção 6 do roteiro)

Nada enviou mensagem a paciente antes do SMS de teste chegar ao celular da equipe. Depois a
direção aprovou ligar a agenda e o envio ("mete marcha"). Hoje estão ligadas:

| Onde | Chave | Estado |
|---|---|---|
| Ponte (VPS 1) | `CRM_BOOKING_ENABLED`, `CRM_SCHEDULING_ENABLED`, `CRM_CANCEL_ENABLED` | `true` |
| CRM (VPS 2) | `CRM_BOOKING_ENABLED`, `CRM_SCHEDULING_ENABLED`, `CRM_SEND_ENABLED` | `true` |
| CRM | lembrete automático, recall automático, IA, `CRM_CLINICAL_DISPATCH_ENABLED` | **desligados** |

**O CRM nunca manda nada sozinho a um paciente.** As mensagens prontas (aviso, relacionamento)
só **preenchem** o campo de resposta, e a secretária revisa e envia.

## 5. Como publicar de novo

**Ponte (VPS 1)**:

1. `dotnet publish src/Clinica.Crm.Bridge -c Release -r linux-x64 --self-contained true`.
2. Copie a pasta para `/opt/clinica-crm-bridge/releases/ponte-<commit>`, dono `root`.
3. Aponte `current` para ela e rode `systemctl restart clinica-crm-bridge`.

Voltar atrás é apontar `current` para a pasta anterior. **Mexeu em fluxo que grava? Rode
`PontePapelRestritoTests` no Postgres antes do push**, senão a clínica descobre pelo 42501.

**CRM (VPS 2)**: ver `semdor-crm/docs/producao-out2026.md` §5 (`crm-release.py`).

**Os cinco apps**: `gh workflow run release.yml --ref main -f app=<app> -f version=<x.y.z>`,
um por app (faturamento, recepcao, financeiro, gerente, clinico). Os instalados se
atualizam sozinhos. A tarefa `sincronizar-canais` de uma execução pode aparecer como
**cancelada** quando outra a substitui; o que conta é o job `release` de cada uma e os arquivos
da release.

Versões publicadas em 18/10: faturamento e Recepção **1.2.55**, Financeiro **1.2.50**, Gerente
**1.2.63** e Consultório **1.2.64**.

## 6. O desenvolvimento (PRs deste repositório)

| PR | O que entregou |
|---|---|
| **#257** | Ponte do CRM para a VPS separada: túnel WireGuard, escuta só no IP do túnel (`RedeDaPonte`), relógio de SMS no servidor e transação sob a estratégia de repetição (`TransacaoDaPonte`) |
| **#258** | A abertura do app resiste a falha passageira de rede |
| **#259** | A ponte dá ao CRM o login e a senha do sistema: as secretárias entram com o usuário do Clinica |
| **#260** | O recall pela ponte **sem a trava de consentimento**, a pedido da direção (`ClinicRecallBridge`). ⚠️ **Revertido na fase 3 Recepção×CRM (23/10/2026)**: a trava voltou, com a autorização colhida no termo do BSV — `docs/fase3-recepcao-crm.md`. ⚠️ **Desde 25/10/2026** a trava bloqueia só quem RECUSOU ("Não" no termo, PARAR, revogação); sem registro conta como autorizado (`PacientesComMensagensLiberadasAsync`, §6 do mesmo documento) |
| **#261** | **Busca do paciente** por telefone, nome ou documento (`ClinicPatientBridge`, `/crm/v1/patients/lookup`). **Resposta ao lembrete** no snapshot da agenda (`ClinicAgendaBridge.AnswerOf`: confirmou / não vem). **Relacionamento só de quem trata**: BSV, acupuntura e os dois (`ClinicRelationshipBridge.Tratamento`) |
| **#262** | A resposta do paciente vira **aviso** no sino da Recepção ("Respostas dos pacientes") e no painel do Gerente (`PacientesNaoVem`, `LembretesSemResposta`). `AvisosDeConfirmacao` é a regra única, com 17 testes. A tela de Confirmações abre no dia da sessão. A **agenda do dia** ganhou no topo as pílulas **Não vêm · Sem resposta · Confirmaram**, que filtram a lista |

Contrato: `ClinicContracts.cs` é **idêntico** nos dois repositórios. Os campos novos entraram no
fim dos records, com valor padrão: `Answer`, `AnsweredAt` e `NotifiedAt` em `ClinicAppointment`,
mais `ClinicPatientLookup`, `ClinicPatientVisit` e `ClinicPatientCard`.

### As regras de tela que saíram desta rodada

Estão em [`CLAUDE.md`](../CLAUDE.md), na seção "⛔ Regras de tela para quem atende", e valem
também no CRM: uma porta por pergunta; função que saiu sai inteira; o que pede ação vira aviso;
o telefone não é identidade; Relacionamento é de quem trata; responsivo e sem resquício.

⚠️ **25/10/2026, telefone com 0 na frente:** a ponte mandava ao CRM o telefone da ficha como estava
gravado, e a ficha com "022998999334" (0 do interurbano) ia para a Meta assim — a mensagem não era
entregue. `ClinicAgendaBridge.Phone` agora tira o 0 e o código de operadora
(`Telefone.SemPrefixoDeDiscagem`) antes de pôr o 55. As conversas que o CRM já abriu com o número
errado continuam lá; as novas vão para 55 + DDD + número. Vale depois de publicar a ponte (VPS 1).

## 6.1 Incidente de 18/10: os apps não abriam ("o banco não respondeu a tempo")

**Sintoma**: a Recepção e o Gerente ficavam parados na abertura, com *"Exception while reading
from stream"* e depois *"O banco de dados não respondeu a tempo"*.

**Causa**: o app tem uma trava no Postgres (`pg_advisory_lock`, chave `727411`) para só um
computador atualizar o banco por vez. Uma conexão do posto caiu segurando essa trava, e a
sessão ficou "idle" no servidor. Todo app que abria depois ficava esperando por ela, sem prazo.
**Não foi a publicação do dia**: a versão 1.2.55 não tinha atualização de banco nenhuma.

**Como destravar à mão** (pede a senha do `clinica-admin`; feche antes os apps do posto):

```text
ssh -t clinica-admin@177.153.66.251 sudo -u postgres psql -d clinica -c "select pid, granted, client_addr, state from pg_locks join pg_stat_activity using(pid) where locktype='advisory' and objid=727411"
ssh -t clinica-admin@177.153.66.251 sudo -u postgres psql -d clinica -c "select pg_terminate_backend(pid) from pg_locks where locktype='advisory' and objid=727411"
```

**Correção no código** (PR #264): sem atualização pendente, a abertura nem pede a trava; com
atualização pendente, espera no máximo 60 s e segue sem ela, registrando no log.

## 7. 🧑‍💻 UX · 🛠️ DX · 🤝 CX

- 🧑‍💻 **UX**:
  - A secretária trabalha do CRM sem trocar de sistema.
  - Ela marca, remarca e cancela pela ponte, com as regras do Clinica.
  - Vê quem confirmou, quem não vem e quem não respondeu no sino e no topo da agenda do dia.
  - Do mesmo lugar, manda a mensagem já redigida.
  - No Clinica, a mesma informação chega ao sino da Recepção e ao painel do Gerente.
- 🛠️ **DX**:
  - Cada regra mora num lugar só: a resposta é classificada na ponte e o aviso em
    `AvisosDeConfirmacao` / `ReceptionNotices`, com o mesmo horizonte (o fim do próximo dia útil).
  - As publicações são versionadas e voltam atrás com um comando.
  - O papel do banco é restrito e medido por teste.
- 🤝 **CX**:
  - Dado de saúde fica no Brasil, nas VPS da Locaweb.
  - A borda não toca o banco.
  - O SMS sai sem acento, para caber em uma parte e custar menos.
  - Nada é enviado sem configuração nem sem a secretária revisar.
  - O custo novo é a Brevo, por SMS, e a VPS 2.

## 8. O que NÃO foi feito, e fica para depois

- **Respostas por SMS dentro do CRM**: a Brevo precisa de um adaptador de webhook em
  `avisos.` para a resposta do paciente entrar na conversa.
- **Respostas salvas com variáveis** (`{nome}`, `{data}`, `{hora}`) e **concluir a conversa
  com o desfecho**.
- **Horário de silêncio do SMS**, para nada sair de madrugada.
- **Cópia de segurança do `chaves.pfx`** fora da VPS 2.
- `crm-release.py` exige `CRM_AGENTS_FILE` mesmo com o login da clínica ligado. Funciona, mas
  é um arquivo a mais para manter.
