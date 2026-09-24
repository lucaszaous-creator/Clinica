# Atendimentos do dia — a mesma operação para recepção e médicos

**Proposta visual da PR #205 · Modelo A · revisão com Jev.** As duas imagens são capturas de protótipos HTML no navegador, com dados fictícios. Esta reorganização ainda não foi implementada no aplicativo nem publicada em produção. Os botões de perfil servem apenas para comparar os protótipos; não propõem troca de permissões no sistema.

## Uma aba, com ações conforme o perfil

A aba será **Atendimentos do dia** nos dois perfis. A recepção começa vendo todos os profissionais; o médico começa vendo seus próprios atendimentos. A estrutura, a ordem das informações e os status serão os mesmos. O acesso a outros profissionais continuará sujeito à permissão existente.

| Área | Para que serve |
| --- | --- |
| **Agenda** | Consultar horários, dia/semana, jornada, bloqueios e vagas; marcar ou remarcar. |
| **Atendimentos do dia** | Acompanhar quem está marcado, quem chegou, quem está em atendimento e o que falta concluir. |
| **Ficha única do paciente** | Trabalhar no atendimento selecionado e consultar os dados autorizados, preservando o vínculo com o agendamento original. |

Os caminhos atuais “Agenda do dia”, “Dia” e “Meu dia”, quando tiverem essa mesma finalidade, devem levar à mesma tela, já com o contexto do usuário. “Fluxo do dia” nos primeiros protótipos passa a se chamar **Atendimentos do dia**.

## 1. O que a recepção verá

![Proposta de Atendimentos do dia — recepção](propostas/06-fluxo-recepcao.png)

- Lista de todos os atendimentos da data, filtrável por profissional, situação e paciente.
- Horário inicial/final, paciente, modalidade, profissional, situação e tempo de espera.
- Pendências operacionais objetivas: cadastro incompleto, termo pendente ou registro de enfermagem pendente quando aplicável. Sem expor o texto clínico.
- Ação adequada à linha: **Registrar chegada**, **Conferir cadastro**, **Conferir termo**, **Ver pendências** ou **Ver atendimento**.
- Painel lateral com o paciente selecionado e os fatos registrados. “Ver atendimento”, na recepção, mostra o andamento administrativo autorizado; não libera edição clínica.
- **Agendar** e **Ver horários e vagas** continuam visíveis. A edição abre o agendamento existente.

No exemplo, a recepção selecionou o paciente fictício 03: ele chegou às 09h42, aguarda há 23 minutos e precisa completar o endereço. Essa mesma chegada aparece para a médica.

## 2. O que o médico verá

![Proposta de Atendimentos do dia — médico](propostas/07-fluxo-medico.png)

- A mesma tabela e os mesmos estados, inicialmente filtrados pelo profissional conectado.
- **Atender** para abrir o atendimento; **Retomar atendimento** quando já estiver em andamento; **Ver registro** para consultar o registro existente.
- Acesso contextual à ficha única e aos documentos, sempre conforme as permissões.
- Indicação separada de evolução em rascunho/salva e de encerramento ou conclusão pendente.
- Retorno à lista preservando data, filtros e paciente selecionado.

No exemplo, a médica vê o paciente fictício 02 em atendimento e o paciente fictício 03 no local, com os mesmos horário e espera da recepção. O painel está no paciente 02 para mostrar como retomar uma consulta.

## 3. Como o fluxo funcionará

| Fato registrado | O que a lista deve mostrar | O que isso não confirma |
| --- | --- | --- |
| Horário criado | **Marcado** | Presença ou atendimento realizado. |
| Recepção registra chegada | **No local**, hora da chegada e espera | Evolução de enfermagem salva. |
| Chamada registrada, quando utilizada | **Chamado**, com hora | Atendimento iniciado automaticamente. |
| Profissional inicia o atendimento | **Em atendimento**, com hora | Evolução concluída ou sessão fechada. |
| Evolução é salva | Registro salvo na coluna de pendências/registro | Encerramento automático do atendimento. |
| Profissional encerra, mas falta fechamento | **Conclusão pendente**, com o que falta | Sessão concluída com guias. |
| Fechamento exigido é efetivamente concluído | **Concluído** | Recebimento financeiro por inferência. |
| Cancelamento ou falta registrados | **Cancelado** ou **Faltou**, mantendo a linha no histórico | Exclusão do agendamento original. |

Esses eventos vêm dos registros existentes e das ações autorizadas. O desenho não cria lançamentos paralelos nem avança etapas por uma troca de aba. Chamada, quando utilizada, deve reutilizar o fluxo existente e ter ação explícita autorizada; alguns controles de chamada hoje não estão expostos na tela médica.

## 4. E as duas evoluções de enfermagem?

Nas sessões que exigem enfermagem, a lista deve distinguir:

| Situação real dos registros | Indicação |
| --- | --- |
| Nenhuma etapa salva | **Faltam chegada e pós-aplicação** |
| Chegada salva | **Falta evolução pós-aplicação** |
| Ambas salvas | **Evoluções de enfermagem completas** |
| Não exigidas para a modalidade/sessão | Nenhuma cobrança indevida dessas etapas |

Salvar a chegada retorna à lista do dia e mantém a pendência da pós-aplicação na **mesma sessão**. Presença na recepção e evolução da chegada são fatos separados. A recepção acompanha apenas a pendência operacional; os detalhes e a edição ficam restritos a quem tem permissão. A trava de edição da enfermagem identifica o usuário e bloqueia outras edições de enfermagem daquela sessão, preservando o trabalho do médico.

## 5. Atualização e organização

- Mostrar a hora da última atualização e avisar quando a leitura falhar.
- Atualizar os estados entre os postos sem perder a seleção ou deslocar a linha durante uma ação.
- Revalidar no servidor antes de gravar e preservar o formulário se houver conflito.
- Salas só entram como informação quando configuradas e relevantes; não serão exigidas apenas por causa deste desenho.
- O resumo conta os horários da lista filtrada. No exemplo da recepção: 8 horários, sendo 3 no local, 2 em atendimento, 1 concluído, 1 marcado e 1 cancelado. Os cartões destacam parte dessas situações; o filtro inclui todas.
- Layout, logo, tipografia e paleta seguem o Modelo A. Situações têm texto explícito, além da diferenciação visual.

## Base da proposta e revisão

Foram conferidos os componentes atuais `FilaView`, `FilaViewModel`, `MeuDiaView`, `MeuDiaViewModel` e a definição compartilhada `StatusDaFila`. Hoje há dados e vocabulário compartilhados, mas apresentações e ações em componentes separados; a proposta é unificar a composição e manter as variações necessárias por permissão.

O Jev (`jev-1.13.0`, consulta real pela TypeSafe) avaliou o texto da proposta e trechos do código em três critérios: unificação da navegação, separação dos fatos de atendimento e preservação das permissões. Classificou os três como adequados. Isso apoia a revisão da proposta; não é teste da implementação nem inspeção visual realizada pelo Jev. As capturas foram conferidas por Codex.

**Limite deste material:** os filtros e ações são demonstrações visuais, sem conexão ao banco. Os dois perfis usam o mesmo conjunto fictício de agendamentos de 25/09/2026; as telas anteriores de disponibilidade usam outro cenário, em 24/09/2026.

[Voltar ao relatório geral da agenda](DIAGNOSTICO-AGENDA.md) · [Ver todas as imagens](README.md)
