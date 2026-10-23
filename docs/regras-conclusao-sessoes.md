# Enfermagem e conclusão das sessões

Em **Gerente Geral → Configurações → Enfermagem e conclusão das sessões**, a direção escolhe:

- As modalidades BSV que exigem evolução de enfermagem antes da conclusão automática. O padrão é BSV e BSV com acupuntura. A seleção é global, inclusive para o portal atualizado. Outras modalidades não permitem nova evolução de enfermagem; infusões continuam permitidas.
- O prazo inteiro de 1 a 720 horas, com sugestão de 24 horas. Salvar as regras ativa a conclusão automática. Enquanto o gerente não configurar, a tela informa que as sessões permanecerão abertas.

No computador e no portal, o médico usa somente **Salvar sessão**. Não há botão de finalizar nem pergunta Sim/Não nessa tela. Salvar não gera guias imediatamente. A enfermagem também salva sua evolução sem concluir o atendimento; o relógio é baseado na gravação médica.

O prazo começa na última gravação da evolução médica, somente para registros gravados após ativar a regra. Desligar e reativar cria uma nova data de ativação. Alterar o prazo enquanto habilitado mantém a data de ativação. Registros antigos não são encerrados em lote por simplesmente habilitar a opção.

Toda nova evolução de enfermagem precisa de sessão BSV. Fora da agenda, o sistema só associa automaticamente quando há exatamente uma sessão BSV válida do paciente na data informada; se houver zero ou mais de uma, pede a sessão correta. O portal oferece apenas sessões BSV para evolução/vínculo, inclusive concluídas para registro tardio. Retificações de registros históricos preservam seu vínculo e a trilha original. A lista de sessões para infusões permanece disponível para todas as modalidades.

Nas modalidades selecionadas, **o automático mantém a sessão aberta se faltar evolução de enfermagem vigente vinculada ao mesmo agendamento**. Registros avulsos, cancelados ou substituídos não liberam. O profissional vê o motivo no atendimento; o Gerente tem uma lista de pendências e lembrete no sino, no máximo uma vez por hora enquanto a quantidade permanecer igual. A lista mostra até 100 pendências mais antigas; a execução percorre todos os candidatos em lotes.

O automático exige uma evolução médica vigente e inequívoca, com conteúdo e médico/paciente correspondentes. Não conclui sessões canceladas nem sessões sem evolução médica. Preserva autoria, agendamento e atendimento originais e usa o mesmo núcleo que gera as guias na conclusão manual. A auditoria identifica `sistema:conclusao-automatica`; não representa assinatura digital, pagamento ou nova avaliação médica. A enfermagem continua sem permissão para finalizar o atendimento médico.

## Execução e publicação

O serviço verifica os prazos a cada minuto. Os módulos atualizados que usam o shell da suíte executam a verificação enquanto abertos e conectados. O serviço da API `Clinica.Assinaturas.Api` também executa a rotina: **a API precisa estar atualizada e ativa na VPS para funcionar com todos os computadores desligados**. Não basta publicar o site ou o instalador.

Configuração persistida em `Configuracoes`, chave `PoliticaConclusaoClinicaV1`, sem nova migration. Somente usuário ativo do perfil Gerente, com `GerenciarUsuarios`, pode alterá-la; alterações são auditadas. Configuração inválida interrompe a rotina sem concluir sessões. Falhas são registradas e reavaliadas no próximo ciclo.

A conclusão roda em transação serializável e usa o bloqueio de agendamento já adotado pelo tablet no PostgreSQL. Repetir a operação não cria outro atendimento ou outras guias. Antes de produção, executar testes SQLite/PostgreSQL, interface do portal e layout Windows; implantar API e interface primeiro em homologação. Habilitar a opção no Gerente somente após conferir o prazo e as modalidades desejadas.

## O texto escrito e não salvo (out/2026, "como no Excel")

Pedido da direção em 22/10/2026: o médico escreveu e não clicou em **Salvar sessão**, o texto não pode se perder, e a sessão encerra sozinha no prazo, gerando a guia.

- **Enquanto o médico escreve**, a folha guarda sozinha o que está na tela como **rascunho** (`RascunhoEvolucao`, uma linha por sessão, atualizada no lugar). Guarda quando a pessoa para de digitar por cerca de 2 s, ou a cada 20 s digitando sem parar, e ao sair da seção ou trocar de paciente. O rodapé diz "Rascunho guardado às 14:32" e o prazo; nada mais muda na tela.
- **Rascunho não é prontuário.** Ele não aparece no histórico, na ficha impressa nem na busca, e não cria versão a cada pausa. O registro clínico continua nascendo no **Salvar sessão**, que versiona (Lei 13.787) e, como hoje, conclui e gera as guias quando quem salva é o médico do horário.
- **Reabrir o paciente** (outro computador, app fechado ou travado) devolve o texto não salvo à folha, com o aviso "Recuperamos o que estava escrito… e ainda não foi salvo". O rascunho só vence o que está salvo quando é mais novo que a última gravação.
- **No prazo** (o mesmo da conclusão automática, contado da **última alteração** e só para rascunhos alterados depois de ativar a regra), a rotina **grava a evolução em nome de quem escreveu** e, havendo horário em aberto sem impedimento, **encerra a sessão e gera as guias** — na mesma transação. A auditoria registra `RascunhoSalvoPorPrazo` e `ConclusaoAutomaticaPorPrazo` com o operador `sistema:conclusao-automatica`. Não é assinatura nem nova avaliação.
- Enquanto há rascunho aberto, a sessão **não é encerrada por cima** com o texto salvo antes: espera o prazo do rascunho.
- O rascunho nunca se apaga: encerra com o desfecho (`SalvoPeloProfissional`, `SalvoPorPrazo`, `SemConteudo` ou `Superado`, quando a sessão foi salva depois da última alteração e ele não regrava nada).
- **O que não entra:** o mapa corporal continua sendo gravado só pelo Salvar.

### 🧑‍💻 UX
Quem escreve não vê nada acontecer além da hora no rodapé: sem "salvando…", sem o campo piscar, sem botão novo. O texto não se perde ao trocar de paciente, fechar o app ou o computador desligar (no máximo os últimos segundos).

### 🛠️ DX
Uma entidade e uma migration aditiva (`RascunhoAutomaticoDaEvolucao`); as regras (quando guardar, o que vai e volta, o rodapé) em `Application/Modelos/RascunhoAutomatico.cs`, com teste; a gravação em `Infrastructure/RascunhosDaSessao.cs` (uma linha, sem transação à mão); o prazo em `ConclusaoAutomaticaService`, dentro da estratégia de execução. Campo novo da folha entra também em `ConteudoDoRascunho`.

### 🤝 CX
Nenhum serviço novo e nenhum custo: uma gravação pequena por pausa na digitação. A sessão esquecida não fica aberta nem sem guia. A direção vê na auditoria o que foi salvo pelo sistema.
