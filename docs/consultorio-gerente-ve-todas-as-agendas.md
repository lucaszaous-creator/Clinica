# Consultório: o Gerente vê e atende a agenda de qualquer profissional (24/11/2026)

**Decisão do dono.** Quem tem o perfil **Gerente** pode ver e atender (evolução, prescrição e documentos) a agenda de qualquer profissional pelo Consultório. O caso que motivou: a diretora entra pelo portal do profissional e só via a própria agenda.

## A régua, num lugar só

`Application/Modelos/AgendaDoConsultorio.cs`, testada em `AgendaDoConsultorioTests`:

- **`Filtro(perfil, próprio, escolha)`**: de quem é a agenda que a lista mostra. Quem não é Gerente fica sempre com a própria, e a escolha que vier na requisição é ignorada (a trava é do servidor). O Gerente escolhe:
  - `eu` (o padrão quando há vínculo);
  - `todos`;
  - o id de um profissional.
- **`PodeAtenderHorarioDe`**: abrir e registrar a sessão de outro profissional, só para o Gerente.
- **`Rotulo`**: "Agenda de Dr. X · registrado por Fulana", quando a sessão é de outro profissional.

## O que muda no portal (API)

- `GET /api/clinico/dia?data=&profissional=` (Meu dia) e `GET /api/posto/pendencias?profissional=` (Sessões sem evolução) passam a respeitar o seletor. Cada horário volta com o nome do profissional.
- `GET /api/clinico/acesso` devolve `qualquerAgenda`, `profissionais` (só para o Gerente) e `profissionalId`.
- Abrir e salvar o atendimento de qualquer horário vale para o Gerente (`AtendimentoTabletService.Horario`), assim como emitir pelo link do WhatsApp. O atendimento aberto traz `agendamento.agendaDeOutro` com o rótulo.

## O que NÃO muda (conformidade)

- **O registro fica no nome de quem fez login.** A evolução, a prescrição e o documento levam o `ProfissionalId` e o operador do login, como sempre (teste `AtendimentoTabletTests.Gerente_ve_e_atende_a_agenda_de_outro_e_o_registro_sai_no_nome_dela`).
- `PoliticaAtendimentoTablet.PodeAtender` continua exigindo `VerAgenda`, `VerProntuario` e `EditarProntuario`.
- A consulta ao atendimento continua auditada (`TabletClinicoConsulta`), e nada se apaga.
- A evolução que **outro** profissional já escreveu continua dele: o Gerente não a sobrescreve (trava que já existia).
- **SafeID e A1 não foram tocados.** Assinar continua valendo só para o documento de quem assina.

## 🧑‍💻 UX

A diretora escolhe "Eu", "Todos os profissionais" ou um profissional e atende como sempre. A tela do atendimento diz de quem é a agenda e quem está registrando. Para quem não é Gerente, nada muda na tela.

## 🛠️ DX

- Uma régua na Application, com teste. O portal só a chama.
- Uma peça nova (`AgendaDoConsultorio`) e um parâmetro opcional em três rotas.

## 🤝 CX

- Sem migration e sem dado novo.
- Risco: o perfil Gerente passa a enxergar o prontuário de pacientes de todos os profissionais pelo Consultório. Isso já valia pela Gestão, porque o Gerente tem todos os bits. O acesso continua registrado.
- **Publicar:**
  - a API do portal (`Clinica.Assinaturas.Api`);
  - o `clinica-site` (o seletor no portal);
  - os apps Gerente e Consultório, se a paridade Blazor entrar.

## O que não foi feito

As telas Blazor do Consultório (Meu dia, Minha semana, Sessões sem evolução) não ganharam o seletor nesta entrega. O portal, que é o que a diretora usa, ganhou. O "Semana" só existe nas telas Blazor; o portal não tem.
