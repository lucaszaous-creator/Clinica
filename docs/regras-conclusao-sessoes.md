# Enfermagem e conclusão das sessões

Em **Gerente Geral → Configurações → Enfermagem e conclusão das sessões**, a direção escolhe:

- As modalidades BSV que perguntam se houve enfermagem na conclusão manual. O padrão é BSV e BSV com acupuntura. A seleção é global, inclusive para o portal atualizado. Outras modalidades não permitem nova evolução de enfermagem; infusões continuam permitidas.
- Se haverá conclusão automática e o prazo inteiro de 1 a 720 horas. A opção vem desligada, com sugestão de 24 horas.

O prazo começa na última gravação da evolução médica, somente para registros gravados após ativar a regra. Desligar e reativar cria uma nova data de ativação. Alterar o prazo enquanto habilitado mantém a data de ativação. Registros antigos não são encerrados em lote por simplesmente habilitar a opção.

Toda nova evolução de enfermagem precisa de sessão BSV. Fora da agenda, o sistema só associa automaticamente quando há exatamente uma sessão BSV válida do paciente na data informada; se houver zero ou mais de uma, pede a sessão correta. O portal oferece apenas sessões BSV para evolução/vínculo, inclusive concluídas para registro tardio. Retificações de registros históricos preservam seu vínculo e a trilha original. A lista de sessões para infusões permanece disponível para todas as modalidades.

Nas modalidades selecionadas, **o automático mantém a sessão aberta se faltar evolução de enfermagem vigente vinculada ao mesmo agendamento**. Registros avulsos, cancelados ou substituídos não liberam. O profissional vê o motivo no atendimento; o Gerente tem uma lista de pendências e lembrete no sino, no máximo uma vez por hora enquanto a quantidade permanecer igual. A lista mostra até 100 pendências mais antigas; a execução percorre todos os candidatos em lotes.

O automático exige uma evolução médica vigente e inequívoca, com conteúdo e médico/paciente correspondentes. Não conclui sessões canceladas nem sessões sem evolução médica. Preserva autoria, agendamento e atendimento originais e usa o mesmo núcleo que gera as guias na conclusão manual. A auditoria identifica `sistema:conclusao-automatica`; não representa assinatura digital, pagamento ou nova avaliação médica. A enfermagem continua sem permissão para finalizar o atendimento médico.

## Execução e publicação

O serviço verifica os prazos a cada minuto. Os módulos atualizados que usam o shell da suíte executam a verificação enquanto abertos e conectados. O serviço da API `Clinica.Assinaturas.Api` também executa a rotina: **a API precisa estar atualizada e ativa na VPS para funcionar com todos os computadores desligados**. Não basta publicar o site ou o instalador.

Configuração persistida em `Configuracoes`, chave `PoliticaConclusaoClinicaV1`, sem nova migration. Somente usuário ativo do perfil Gerente, com `GerenciarUsuarios`, pode alterá-la; alterações são auditadas. Configuração inválida interrompe a rotina sem concluir sessões. Falhas são registradas e reavaliadas no próximo ciclo.

A conclusão roda em transação serializável e usa o bloqueio de agendamento já adotado pelo tablet no PostgreSQL. Repetir a operação não cria outro atendimento ou outras guias. Antes de produção, executar testes SQLite/PostgreSQL, interface do portal e layout Windows; implantar API e interface primeiro em homologação. Habilitar a opção no Gerente somente após conferir o prazo e as modalidades desejadas.
