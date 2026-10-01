# Jornadas atuais e caminhos propostos

Os percursos abaixo são reconstruídos do código. As setas representam mudanças de contexto/etapa, **não uma medição de cliques nem um teste de usabilidade**. Permissão, sessão existente, tipo de documento e configuração podem abreviar ou ampliar o caminho. Os achados referenciados contêm links de origem.

## Emitir um documento e entregar ao paciente

**Hoje, dependendo da entrada:** Documentos → escolher paciente → escolher tipo/prévia → abrir editor → emitir/persistir → eventualmente selecionar certificado → salvar/abrir PDF externo. Na lista de documentos, quando `PodeAssinar` é verdadeiro, Assinar ganha a posição principal e 2ª via fica escondida; a impressão continua no menu `⋯`. Cancelar o certificado pode manter a janela de emissão e orientar reemissão, apesar de existir assinatura posterior do mesmo registro.

**Proposta:** entrar pelo paciente ou sessão → compor/revisar → emitir uma vez → resultado persistente **Documento nº … emitido**. A partir desse estado, imprimir, assinar, compartilhar quando permitido ou fechar operam sobre o mesmo ID. Falha de uma dessas ações não volta a transformar Emitir no próximo passo. Nova emissão exige escolha explícita para um novo registro.

| Ação no resultado | Significado proposto |
| --- | --- |
| Imprimir | Abrir a via emitida ou preparar impressão; mostrar claramente a etapa externa quando depender de leitor PDF |
| Assinar este documento | Assinatura profissional do registro existente, quando cabível |
| Retomar assinatura | Consultar/retomar a mesma operação pendente, sem criar outro documento |
| Ver documento | Visualizar o artefato e seu estado atual |
| Fechar | Encerrar o editor preservando a emissão |
| Corrigir conteúdo | Explicar o mecanismo permitido de correção/cancelamento e nova versão, com histórico |

**Não há proposta de substituir toda assinatura por impressão.** Consentimento do paciente, assinatura profissional e entrega de papel respondem a necessidades diferentes. Ver A01–A04, A11–A14 e A28–A29.

## Percursos operacionais

| Jornada | Caminho atual / ponto de atrito | Caminho proposto | Evidência ou restrição |
| --- | --- | --- | --- |
| 01. Assinar documento já emitido | Central → documento → Assinar; mensagem do editor sugere cancelar e emitir outro | Retomar pelo mesmo ID a partir do editor e da central | A01–A02; teste de falha pós-persistência necessário |
| 02. Imprimir sem assinar agora | Documento sem assinatura → `⋯` → segunda via | Imprimir visível conforme tipo e política do documento | A03; assinatura continua disponível |
| 03. Salvar/abrir PDF | Emitir e imprimir → escolher arquivo → leitor externo | Nomear a ação conforme efeito e distinguir cancelamento de salvamento concluído | A04 |
| 04. Recibo no balcão | Central orienta Caixa, mas Recepção tem Pagamentos → Recibo | Abrir cobrança/pagamento autorizado do mesmo paciente | A05; não ampliar acesso ao Caixa inteiro |
| 05. Fechamento em PDF | Emissão usa período mantido em outra aba | Período explícito na própria ação de gerar fechamento | A06 |
| 06. Cadastrar durante busca | Busca vazia → instrução Pacientes → Novo paciente → retornar e buscar | Cadastrar com contexto → selecionar automaticamente → retomar tarefa | A40; conferir duplicidade antes de criar |
| 07. Marcar horário | Agenda/lista oferecem marcação contextual; permissões podem levar à grade | Preservar os atalhos existentes e padronizar o nome Agendar | Matriz por perfil e catálogo; não afirmar que toda marcação exige todos os passos |
| 08. Preparar retorno indicado | Pedido clínico → Retornos a marcar → marcar horário | Manter vínculo pedido/agendamento e expor retorno na ficha e no CRM autorizado | A25; existem pontes, não foi constatada ausência total de integração |
| 09. Confirmar consulta | Rodada manual/e-mail/WhatsApp/CRM | Uma visualização de estados por horário, diferenciando envio e resposta | A31 aplica ao registro de campanha, não a todo provedor |
| 10. Cancelar e reaproveitar vaga | Detalhe do horário → cancelar → lista de espera/Quem chamar | Após cancelamento, oferecer lista de espera compatível com o horário | Catálogo de agenda; oportunidade de continuidade a validar em execução |
| 11. Atender paciente | Meu dia → sessão → evolução → salvar/concluir | Próximo passo contextual com estado salvo, conclusão e pendências visíveis | Catálogo clínico; não colapsar salvar e concluir sem regra explícita |
| 12. Consultar histórico | Sessão → histórico/ficha → procurar seção | Atalhos para a seção do mesmo paciente, com retorno à sessão | A41 e mapa de workspace |
| 13. Ver sessão antiga na web | Ficha web → últimas 20 sessões → fim | Carregar anteriores/pesquisar por período | A52 |
| 14. Registrar laudo | Pedido → Anexar laudo já vinculado; entrada genérica também existe | Preservar via contextual; diferenciar não há pedido de todos respondidos | A61; o vínculo automático existente é uma solução a manter |
| 15. Colher termo | Selecionar termo/sessão → conferir identidade → coletar → arquivar | Entrada da sessão pré-seleciona contexto; ainda exige leitura, identidade e manifestação do paciente | A28, A44–A45 |
| 16. Retomar arquivamento de assinatura | Histórico do portal pode oferecer Retomar arquivamento | Dar visibilidade à operação pendente e conservar o mesmo documento | Já há porta no portal; verificar ponta a ponta sem duplicar coleta |
| 17. Executar infusão | Agenda/ficha/fila → folha → checar → encerrar → assinatura(s) | Uma linha do tempo de estados e ação seguinte por papel, com retomada | A20; conferir os quatro commits locais do portal antes de implementar |
| 18. Imprimir enfermagem | Selecionar sessão → ler todas as páginas históricas → filtrar sessão → imprimir | Consulta específica por sessão → folha → imprimir | A43 |
| 19. Cobrar dívida paga anteriormente | Inadimplência → Recebi → confirmar → grava hoje | Confirmar recebimento com data efetiva | A36 |
| 20. Receber sem cobrança criada | Pagamentos vazio → instrução para fechamento ou venda | Conferir sessões/Ver pacotes do paciente → origem da cobrança → retornar | A62 |
| 21. Cobrar por Pix presencial | Gerar → copiar texto → transferir para celular | QR e copia e cola do mesmo payload → conferir recebimento separadamente | A58; geração não confirma pagamento |
| 22. Devolver receita glosada já recebida | Conciliação → aviso → Caixa → montar saída | Registrar devolução com vínculo e campos conhecidos | A37; original permanece preservado |
| 23. Conferir retenção | Reter? → erro registrado apenas em log | Prévia, erro por linha e Tentar novamente | A35 |
| 24. Adiar conta | Adiar 7d | Escolher nova data com atalhos Hoje/7 dias e motivo quando aplicável | A27; oportunidade, não ausência comprovada de todo outro caminho |
| 25. Resolver glosa do painel | Linha → Abrir glosas → lista geral | Abrir a glosa/guia exata, com período e retorno | A32 |
| 26. Corrigir antes de exportar TISS | Avisos de riscos/dados → cancelar → procurar cadastro → voltar | Prévia de prontidão com ações por registro | A68 |
| 27. Registrar XML de retorno parcial | Importar → aviso de ausentes → revisão → confirmar todas as linhas | Pendentes sem resposta destacados e decisão explícita | A64 |
| 28. Recuperar XML exportado | XML/2ª via → regenerar com parâmetros atuais | Baixar original; gerar versão corrigida como ação diferente | A65 |
| 29. Resolver rodada vencida | Abertura → diálogo bloqueante → decidir todas → continuar | Fila obrigatória retomável; acesso às consultas necessárias | A67; alteração de regra exige definição do responsável do processo |
| 30. Editar convênio | Abrir → editar → Fechar → Salvar na tela anterior | Salvar/Cancelar por entidade ou revisão de alterações explícita | A33–A34 |
| 31. Chamar paciente de volta | Lista de retenção → WhatsApp → nada registrado nessa ação | Preparar mensagem → registrar tentativa e próximo passo no acompanhamento | A38; não transformar abertura em entrega |
| 32. Recuperar NPS não feito | NPS de ontem | Ontem + período de recuperação e prévia de já gerados | A39 |
| 33. Encerrar conversa e continuar fila | Concluir → filtro muda para concluídos | Confirmar resultado → manter fila → próximo elegível | A47 |
| 34. Concluir com falha no refresh | POST concluído → diálogo fecha → refresh falha → erro no diálogo fechado | Sucesso da mutação e atualização pendente visíveis separadamente | A48 |
| 35. Remarcar pelo CRM | Contexto de paciente → vagas → confirmar mudança; Operação tem Registrar solução separado | Remarcação canônica + evidência da mudança + fechamento do pedido operacional | A26; registrar nota não altera horário por si |
| 36. Usar modelo WhatsApp | Mais ações → Modelos | Promover modelo como ação principal quando o estado exigir | A49 |
| 37. Investigar fila extensa no CRM | Resumo total → lista limitada | Paginação por fila com total e filtros preservados | A51 |
| 38. Site até marcação | Página → atendimento/contato → WhatsApp → triagem CRM → agenda confirmada | CTA com canal e especialidade preservados; confirmação de agendamento explícita | A46; não é autoagendamento na página pública |
| 39. Testar publicação de documentos | Enviar teste → abrir URL → painel do provedor → apagar objeto | Teste com resultado e limpeza do próprio objeto na mesma área | A59 |
| 40. Pedir suporte | Ler instruções → localizar logs → anotar horário | Referência do erro + diagnóstico revisável + versão/ambiente | A69–A70 |

## Transferência de contexto recomendada

Uma navegação entre áreas deve levar **somente os identificadores necessários e autorizados**, não copiar registros ou dados clínicos para URLs públicas. O destino precisa reler o registro e validar permissão/versão.

| Origem → destino | Contexto a preservar | Ao voltar |
| --- | --- | --- |
| Fila → paciente/sessão | PacienteId, AgendamentoId, data e filtro de origem | Mesma data/filtro/posição |
| Documento → impressão/assinatura | DocumentoId e operação pendente | Mesmo registro e estado atualizado |
| Pedido → resultado | PedidoId e PacienteId | Pedido com resultado vinculado |
| Guia → glosa/caixa | CodigoId/GuiaId, período, vínculo financeiro | Mesma seleção, com situação recalculada |
| CRM → agenda | ConversationId e paciente confirmado, oferta/versão | Histórico do pedido e resposta efetiva da agenda |
| Busca → cadastro | Termo de busca e fluxo chamador | Novo ID selecionado, sem nova criação automática |
| Termos → sessão | PacienteId, sessão e coleta ativa | Status recebido/arquivado/recusado, sem afirmar liberação clínica automática |

## Verificação presencial ainda necessária

Para cada caminho escolhido para correção, usar paciente fictício e o perfil real da tarefa. Conferir abertura, pré-preenchimento, salvar/cancelar, falha de rede, permissão negada, repetição após sucesso parcial e retorno à origem. Na impressão, conferir impressora/leitor PDF e cancelamento. Na assinatura, conferir ida/retorno do provedor, expiração e retomada do mesmo registro. Na integração CRM, confirmar a mudança no sistema canônico, e não apenas a mensagem de sucesso local.

Esses ensaios não foram executados em produção nesta auditoria. São a próxima etapa de validação das correções propostas; não condicionam a disponibilidade do mapa estático e dos achados já documentados.
