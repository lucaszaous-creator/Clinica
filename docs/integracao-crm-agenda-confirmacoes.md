# A ponte do SemDor CRM com o Clinica (out/2026)

O SemDor CRM (repositório `semdor-crm`) é a **mesa de trabalho das secretárias**. Pelo WhatsApp e
pelo SMS elas conversam com o paciente e, do mesmo lugar, consultam a agenda, marcam, remarcam e
cancelam. Também acompanham as confirmações (quem não responde vira ligação) e recuperam
pacientes. Não é ferramenta de marketing. Ele **não lê o banco da clínica**. Quem lê e grava
é esta ponte, `src/Clinica.Crm.Bridge`, compilada com as bibliotecas ATUAIS do Clinica — as
mesmas regras de agenda, cancelamento e consentimento dos cinco apps.

Até out/2026 a ponte só existia como cópia histórica dentro do CRM, com bibliotecas do Clinica
de meses atrás (sem `CanalContato.Sms` e sem a regra do "não vem"). A cópia continua lá para
regressão e tem a publicação bloqueada; **esta é a que se instala**.

## 1. O que ela expõe

| Rota | Para quê |
|---|---|
| `GET /crm/snapshot` | Agenda de ontem até +32 dias, catálogo de especialidades e a situação da confirmação de cada horário |
| `POST /crm/decision` | Resposta do paciente ao lembrete do WhatsApp: `confirm`, `cancel`, `reschedule` ou `notified` |
| `GET /crm/recall` | Candidatos a recall (sessões realizadas por modalidade e casos de acompanhamento), com consentimento |
| `POST /crm/recall/notice` | Registro do convite de recall enviado, com o canal (`whatsapp` ou `sms`) |
| `POST /crm/v1/appointments/slots` / `reschedule` | Vagas e remarcação assistida (remarcação só com `CRM_SCHEDULING_ENABLED=true`) |
| `POST /crm/v1/appointments/cancel` | Cancelamento pela secretária, com motivo, pelo mesmo `CancelarAsync` do balcão (suspende as guias). Idempotente pelo comando. Só com `CRM_SCHEDULING_ENABLED=true` |
| `POST /crm/v1/booking/catalog` · `calendar` · `slots` · `status` | Leitura para marcar: pacientes do telefone, profissionais, salas, modalidades, a agenda ocupada (sem o nome de outros pacientes) e as vagas pela `BuscaDeVagas` |
| `POST /crm/v1/booking/hold` · `confirm` · `release` | Reserva de 5 min → confirmação com o paciente → horário criado por `AgendaService.AgendarAsync`. Hold e confirm só com `CRM_BOOKING_ENABLED=true` |

Contratos: `src/Clinica.Crm.Bridge/ClinicContracts.cs`, idêntico ao `ClinicContracts.cs` do CRM.
Campo novo entra com valor padrão no fim do record, para os dois lados atualizarem em momentos
diferentes.

## 2. A regra única das respostas

O paciente responde pelo **link do SMS** (Clinica) ou pelo **WhatsApp** (CRM), e a agenda do
balcão precisa ler as duas do mesmo jeito. Por isso as duas pontas gravam o MESMO contato
(`ContatosCampanha`, origem `AGD:{agendamento}`) com o mesmo vocabulário de
`RodadaDeConfirmacao.Classificar`:

| Resposta | Gravado | O que o balcão vê |
|---|---|---|
| Confirmar | `Respondido` | "Confirmou" |
| Cancelar | `Respondido` + `RespostaRemarcar` | "Não vem — remarcar" (vermelho). O horário **continua** |
| Remarcar | `Respondido` + `RespostaRemarcar` | "Não vem — remarcar" |

⚠️ Antes desta versão a ponte gravava o cancelamento com texto livre, e o classificador lia todo
`Respondido` sem "pediu para remarcar" como **"Confirmou"** — o balcão veria confirmado quem
avisou que não vem. E o "REMARCAR" do WhatsApp nunca chegava à agenda.

**Cancelar sozinho** continua possível, como decisão da clínica: `CRM_CANCEL_ENABLED=true`. Aí
a ponte usa `AgendaService.CancelarAsync` (que suspende as guias pelo caminho de sempre) e o CRM
avisa o paciente de que o horário foi cancelado. Sem a chave, ele avisa que a equipe vai ligar
para remarcar — a mensagem diz o que a clínica fez, nunca o que ela não fez.

Confirmar **não** registra chegada, presença nem gera guia.

## 3. Como instala

1. Publicar `src/Clinica.Crm.Bridge` no servidor do banco (a VPS). Ela escuta só
   `127.0.0.1:5190`; o CRM chega por túnel ou na mesma máquina.
2. Variáveis: `ConnectionStrings__Clinica`, `CRM_BRIDGE_KEY` (32+ caracteres, a mesma
   `CRM_CLINIC_KEY` do CRM), opcionais `CRM_BOOKING_ENABLED` (marcar pelo CRM),
   `CRM_SCHEDULING_ENABLED` (remarcar e cancelar pela equipe) e `CRM_CANCEL_ENABLED` (o "não vou"
   do paciente cancela sozinho). As três chaves também precisam valer no CRM.
3. A ponte **não cria nem migra** banco: as migrations continuam sendo aplicadas pelos apps.

### 2.1 A reserva de marcação

O estado da reserva fica na trilha (`EventoAuditoria`, ação `CrmReservaV1`), com uma linha por
mudança. **Não há tabela nem migração.** A reserva **não trava a agenda do balcão**, porque aqui a
agenda avisa e não impede (set/2026). O que ela garante é outra coisa: a busca do CRM não oferece
o horário reservado por outra conversa, o paciente tem uma reserva por vez, e a confirmação
reconfere a vaga numa transação com `Agendamentos` travada. O CRM nunca grava por cima de um
horário tomado.

⚠️ A busca não usa `ProximasComRecursosAsync`: ela devolve as **10 primeiras** vagas, e com o
filtro "após 17h" as dez primeiras seriam todas de manhã. A secretária leria "nenhuma vaga" numa
semana livre. A ponte chama o mesmo `BuscaDeVagas.Calcular`, sem o teto, e filtra antes do corte.

## 4. Validação

`tests/Clinica.Tests/PonteCrm/` (32 testes) roda a ponte contra o banco do Clinica — inclusive
no Postgres com `CLINICA_TESTES_POSTGRES` — cobrindo agenda, idempotência, telefone errado,
paciente que já chegou, a regra do "não vem", cancelamento opcional, recall e o canal SMS, e
também a marcação (reserva, recibo repetido, vaga tomada no balcão, filtro de período), a
privacidade da agenda e o cancelamento pela equipe com motivo. A concorrência real se prova no
Postgres de homologação, antes de ligar as chaves.

**SMS:** o lembrete de 24 h e o SMS do CRM usam o mesmo provedor (Zenvia, Twilio ou API própria).
A Infobip não é usada.

## 5. UX · DX · CX

- 🧑‍💻 **UX** — a secretária marca, remarca, cancela e lê a agenda sem sair do CRM; a recepção vê
  a resposta do WhatsApp e a do SMS com o mesmo selo, no mesmo lugar.
- 🛠️ **DX** — a ponte vive ao lado das regras que ela usa; mudar agenda ou consentimento quebra o
  teste da ponte no mesmo `dotnet test`, em vez de divergir em silêncio noutro repositório.
- 🤝 **CX** — nenhum horário some sem alguém falar com o paciente; um canal a mais não cria uma
  segunda verdade sobre a mesma sessão.
