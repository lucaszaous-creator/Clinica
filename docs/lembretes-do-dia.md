# Pedido de confirmação às 06:00 do dia, e mensagem ao marcar (out/2026)

> ⛔ **O e-mail saiu deste fluxo (out/2026, decisão da direção).** A clínica não colhe e-mail no
> cadastro, e o e-mail ficou **só para o documento** que a equipe envia ao paciente
> (`EmailDoDocumento`, `EnvioAoPacienteService`). Saíram juntos o `LembreteEmailService`, o relógio
> `RelogioDoEmailNoServidor` da ponte (`LembreteEmail__Ligado`), o envio na abertura da Recepção e
> do Gerente, o botão "Enviar e-mails" das Confirmações e a opção "e-mail só para quem não tem
> celular" do SMS. A coluna **E-mail** abaixo é histórica: a confirmação de sessão é WhatsApp (CRM)
> e SMS.

Decisão da direção: o paciente recebe o pedido de confirmação **às 06:00 do dia da sessão**, por
**WhatsApp e por e-mail**. Também recebe uma mensagem **assim que a sessão é marcada**. O objetivo
é diminuir as faltas.

## Quem manda o quê

| Mensagem | WhatsApp | E-mail |
|---|---|---|
| Pedido de confirmação no dia, a partir das 06:00 | CRM (`ReminderHour = 6`, modelo `semdor_lembrete_sessao` com os botões Confirmar/Remarcar/Cancelar) | Clinica (`LembreteEmailService.EnviarLembretesDoDiaAsync`) |
| "Sua sessão foi agendada" | CRM (`BookingEnabled`, modelo `semdor_agendamento_confirmado`) | Clinica (`EnviarConfirmacoesDeAgendamentoAsync`) |

O **e-mail sai da clínica**, pelo servidor que ela já configurou em Gerente → Configurações →
Lembretes por e-mail. O CRM não recebe nenhuma credencial de e-mail.

O **relógio** é o do servidor (`RelogioDoEmailNoServidor`). Ele fica na ponte do CRM, na VPS 1, e
liga com `LembreteEmail__Ligado=true`. A abertura da Recepção e do Gerente continua rodando a
mesma rotina como caminho de baixo.

## As regras

- **A véspera deixou de ser lembrada por e-mail.** O pedido é do dia, como no WhatsApp.
  - O SMS da véspera é outro canal e continua como está configurado.
  - O WhatsApp do dia não trata o "avisado" pelo SMS como resposta: o paciente avisado pelo SMS
    também recebe o WhatsApp.
- **Os dois canais saem.**
  - O status do contato é um só para os dois canais, e o WhatsApp das 06:00 tira o contato de
    "pendente". Por isso, quem impede o segundo e-mail é a **trilha**: a ação `EmailLembreteDoDia`
    com a chave `AGD:{horário}`.
  - Quem já respondeu, ou foi dispensado, não recebe.
- **"Sua sessão foi agendada"** sai só para o horário marcado há no máximo 2 horas, ainda
  "Agendado" e que começa em mais de 1 hora.
  - A hora usada é a da marcação, `Agendamento.CriadoEm`, que a ponte manda ao CRM em
    `ClinicAppointment.CreatedAt`.
  - Sai uma vez por horário: remarcar não repete. A trilha é a ação `EmailAgendamentoConfirmado`.
  - A janela de 2 horas é o que impede de ligar o recurso e mandar a mensagem para a agenda
    inteira.
- **Marcado hoje e já avisado** não recebe o pedido de confirmação no mesmo dia pelo WhatsApp: a
  mensagem de agendamento acabou de dizer o horário.

## Como ligar em produção

1. VPS 1, ponte:
   - Publicar esta versão da ponte (`deploy-vps/vps1-ponte-*.sh`).
   - Acrescentar `LembreteEmail__Ligado=true` ao ambiente do serviço `clinica-crm-bridge`.
   - O servidor de e-mail é o que já está gravado no banco (Gerente).
2. CRM, em Configurações → Fluxo de atendimento, **depois de a Meta aprovar os modelos**:
   - "Confirmação no dia da sessão": ligar, modelo `semdor_lembrete_sessao`, "a partir das 06:00".
   - "Mensagem ao marcar a sessão": ligar, modelo `semdor_agendamento_confirmado`.
   - Ligar antes da aprovação faz o envio ser recusado ("Use um modelo aprovado"), e o pedido
     daquele horário não é repetido.

## UX · DX · CX

- 🧑‍💻 **UX:**
  - A recepção não faz nada: as mensagens saem sozinhas.
  - A resposta do WhatsApp continua chegando como "Confirmou" ou "Não vem — remarcar" na agenda
    do balcão.
  - O e-mail não leva dado clínico.
- 🛠️ **DX:**
  - A regra do e-mail mora em `LembreteEmailService`, com teste (`LembreteEmailTests`).
  - A do WhatsApp mora em `Store.PedeConfirmacaoAgora` / `AvisaMarcacaoAgora`, no CRM, com teste.
  - A idempotência do e-mail é pela trilha: não há migration.
  - O contrato ganhou só um campo no fim, com padrão nulo.
- 🤝 **CX:**
  - Menos faltas: o pedido chega na manhã da sessão, quando o paciente ainda pode avisar.
  - O e-mail não tem custo extra; o WhatsApp é cobrado por conversa pela Meta.
  - Com o SMS da véspera ligado, o paciente recebe três avisos: o SMS na véspera, e o WhatsApp e
    o e-mail no dia. Desligar o SMS é decisão da clínica.

## O que NÃO foi feito

- **Horário marcado para daqui a mais de 32 dias não recebe a mensagem de agendamento pelo
  WhatsApp.** O retrato da agenda que a ponte manda ao CRM cobre 32 dias. O e-mail não tem essa
  limitação.
- **A hora das 06:00 do e-mail é fixa** (`HoraDoLembreteDoDia`). A do WhatsApp se escolhe no CRM.
