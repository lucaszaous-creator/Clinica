# Reserva de horário sem ficha (nov/2026)

**Pedido do dono.** Os oito anúncios da Meta trazem muita gente que só pergunta. A clínica não quer cadastrar cada
uma nem pedir CPF pelo WhatsApp (LGPD). Até aqui a aba Agenda da conversa travava sem ficha, porque
`Agendamento.PacienteId` é obrigatório. A decisão do dono foi: *"Vamos com a B e os dados não se apagam."*

## As opções comparadas

| | A. `Agendamento.PacienteId` anulável | **B. Entidade própria (`ReservaDeHorario`)** ✅ | C. Ficha "provisória" |
|---|---|---|---|
| 🧑‍💻 UX | o horário aparece na agenda como os outros | a agenda e a conversa mostram "Reserva · sem ficha", e "Virar paciente" completa no balcão | a lista de pacientes se enche de gente que nunca veio |
| 🛠️ DX | faturamento, guias, BI, relatórios e repasse supõem paciente: dezenas de consultas a revisar, e um esquecido gera guia sem dono | uma tabela nova; a reserva entra só onde ocupa (busca de vagas, conflito, ponte) e fica fora do resto por construção | sem mudança de esquema, mas cada relatório precisa saber o que é "provisório" |
| 🤝 CX | risco alto no faturamento (o produto principal) | risco contido; o dado mínimo fica fora da base de pacientes | dado pessoal de quem não é paciente misturado ao prontuário (LGPD) |

## Como funciona

1. **Sem ficha, a agenda funciona** (passo 1, no ar). A ponte busca vagas com `PatientId = 0` e o CRM mostra as vagas,
   o calendário e "Enviar estes horários". Nos apps, a trava sai com a tela do Design SemDor.
2. **Reservar.** A recepção informa o **nome** dito na conversa e se é particular ou de qual convênio (sem carteirinha).
   O telefone é o da conversa.
   - A reserva **ocupa o horário**: `BuscaDeVagasService`, a busca da ponte e `AgendaService.ConflitosAsync` a
     consideram.
   - Marcar por cima é **sempre recusado** (`RecursoAgenda.Reserva`, `ImpedeMarcar`), com a frase "Desmarque a
     reserva ou use 'Virar paciente'".
   - Selo: "Reserva via WhatsApp — sem ficha" (detalhe e agenda) e "Reserva · sem ficha" (bloco da grade, cartão da
     conversa).
   - Limite de reservas abertas por telefone: `ReservaDeHorarioRegras.LimiteAbertasPorTelefone` = 2 (decisão do dono),
     conferido no servidor.
3. **Nada de ficha.** A reserva não cria `Paciente` nem `Agendamento`. Fica fora da busca de pacientes, do BI, dos
   relatórios, do faturamento e das guias.
4. **Virar paciente** (balcão).
   - **Paciente novo**: nome e telefone vêm da reserva; o cadastro usa a validação de sempre
     (`ValidacaoCadastroPaciente`, que exige CPF; no balcão o CPF é pedido).
   - **Já tem ficha**: a recepção escolhe a ficha.
   - Nos dois casos a reserva vira um `Agendamento` normal no **mesmo horário**: cobertura do convênio e trava de
     consulta valem como sempre.
   - Ficha, agendamento e reserva gravam numa **transação só**. Se uma parte falhar, nada fica.
   - É **idempotente**: repetir devolve o mesmo agendamento.
   - A conversa liga a ficha pelo `ConversaId` que a reserva guarda.
5. **Desmarcar e remarcar** valem pela conversa (ponte) e pelos apps.
   - Motivos: "A pessoa pediu", "Não respondeu", "A clínica precisou".
   - Remarcar mantém a mesma reserva.
   - Nada sai sozinho: a mensagem pronta fica com a recepção (`RascunhosDaConversa`, do Design).
6. **Lembrete da véspera** (decisão do dono, nov/2026). A reserva recebe o MESMO pedido de confirmação dos
   agendamentos, com o mesmo modelo, pelo número da conversa.
   - A ponte manda as reservas abertas em `ClinicSnapshot.Reservations` (campo novo no fim), com Id **negativo** e sem
     ficha. Ficam fora de `Appointments`, então não entram na ficha, no contexto nem na agenda do CRM.
   - A resposta volta pela mesma `/crm/decision` (Id negativo) e fica na reserva: `LembreteEnviadoEm`,
     `RespostaDoLembrete` (confirmou, pediu-cancelar, pediu-remarcar) e `RespondidaEm`.
   - Nada é cancelado sozinho: cancelar e remarcar viram pedido, que aparece no sino do CRM.
   - A resposta não muda a revisão que a recepção tem na mão. Remarcar a reserva esquece o lembrete do horário antigo.
   - Ao **virar paciente**, a resposta passa ao agendamento como a linha de confirmação de sempre
     (`ReservaDeHorarioRegras.ContatoDoLembrete`): o balcão lê "Confirmou" e o CRM não pede de novo.
   - A mensagem "agendamento feito" não sai para a reserva: só o lembrete foi decidido.
7. **Os dados não se apagam.** Desmarcada, não comparecida ou convertida, a linha fica com a situação. A base legal e
   as mitigações estão em `docs/conformidade-lgpd.md`, **pendentes de validação jurídica**.

## Onde mora

| Peça | Arquivo |
|---|---|
| Entidade, situação, selos e motivos | `Domain/Entities/ReservaDeHorario.cs` |
| Regras (validação, limite, o que recusa, trilha sem nome) | `Application/Modelos/ReservaDeHorarioRegras.cs` |
| Serviço (transação, trava, conversão) | `Infrastructure/ReservaDeHorarioService.cs` (`IReservaDeHorarioService`) |
| Ocupação na busca e no conflito | `BuscaDeVagasService.ComReservasAsync`, `AgendaService.ConflitosAsync`/`GarantirHorarioPermitidoAsync`, `ClinicBookingBridge.FreeSlots` |
| Ponte | `Clinica.Crm.Bridge/ClinicReservationBridge.cs`: `/crm/v1/reservations`, `/list`, `/cancel`, `/reschedule` (atrás de `CRM_BOOKING_ENABLED`) |
| Contrato | `ClinicReservationRequest`, `ClinicReservation`, `ClinicReservationQuery`, `ClinicReservationCancel`, `ClinicReservationReschedule` (idêntico no semdor-crm) |
| Banco | migration `ReservasDeHorarioSemFicha` (aditiva); `papel-ponte-crm.sql` com INSERT/UPDATE (sem DELETE) |

## Testes

- `ReservaDeHorarioTests`:
  - ocupa o horário e não cria ficha;
  - não reserva por cima de horário marcado nem de outra reserva;
  - só o mínimo vale, e há limite por telefone;
  - remarcar mantém a reserva;
  - desmarcar deixa o histórico;
  - virar paciente (novo, existente, falha sem deixar nada, idempotente);
  - a trilha não guarda nome nem telefone;
  - a grade do dia.
- `PonteClinicBookingTests`: reservar pela conversa ocupa a vaga, é idempotente, é só deste telefone e confere a
  revisão.
- `PontePapelRestritoTests`: as gravações da ponte batem com o papel (no CI, pelo interceptador).
- `TraducaoNoNpgsqlTests`: as consultas traduzem para o Postgres.

## O que NÃO foi feito, e por quê

- **Telas** (aba Agenda nos apps, grade da Recepção, "Virar paciente", aba do CRM web): ficam com o Design SemDor e com
  o CRM web, que dependem da aprovação do dono à proposta.
- **Eliminação a pedido do titular** (art. 18, VI): não há porta; está na lista de pendências da LGPD.

## 🧑‍💻 UX

- A recepção responde ao anúncio na hora: horários, reserva só com o nome, e o cadastro fica para o balcão.
- O horário reservado não some debaixo de outra marcação.

## 🛠️ DX

- Uma tabela e um serviço. A regra do que ocupa a agenda continua num lugar só (`ConflitosAsync` e busca de vagas).
- Faturamento e relatórios não mudaram.

## 🤝 CX

- Menos cadastro de quem não vem e nada de CPF pelo WhatsApp.
- O risco no faturamento é zero, porque nenhuma consulta dele mudou.
- Os dados ficam guardados por decisão do dono; a base legal ainda depende de validação jurídica.
