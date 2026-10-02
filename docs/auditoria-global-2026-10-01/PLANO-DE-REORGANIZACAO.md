# Proposta de reorganização e ordem de trabalho

> **Escopo vigente — portal web preservado:** por decisão do proprietário em 01/10/2026, não alterar o portal web nem reunir evolução e consentimentos nele. Recomendações sobre portal neste inventário são históricas e não autorizam implementação. Ver [regra obrigatória, imagens desktop e cobertura](ESCOPO-E-PROPOSTAS-VISUAIS.md).

O objetivo é reduzir decisões repetidas e deslocamentos sem perder funções. A proposta não é uma autorização para apagar telas ou alterar regras clínicas/financeiras automaticamente. Cada mudança deve manter as permissões e o histórico das entidades afetadas.

## Estrutura de informação proposta

| Área | Conteúdo principal | O que fica contextual |
| --- | --- | --- |
| Meu trabalho | Agenda do papel, fila e pendências atribuídas | Retomar última tarefa autorizada, alertas com destino exato |
| Pacientes | Busca/cadastro e ficha longitudinal | Sessão, documentos, exames, termos, cobranças e próximos contatos |
| Atendimento | Sessão clínica/enfermagem e estado atual | Salvar, concluir, prescrever, registrar materiais, imprimir e assinar |
| Recebimentos | Cobranças, recebimento e recibo | Caixa completo apenas para os perfis autorizados |
| Faturamento | Guias, lotes, retornos, glosas e recursos | Correção de cadastro pela pendência, mantendo seleção |
| Financeiro | Caixa, contas, banco/cartão, fechamento, resultado e repasse | Configurações de taxa/categoria por acesso secundário |
| Relacionamento | Conversas, acompanhamento, tarefas e campanhas | Paciente confirmado, agendamento e tentativas de contato |
| Gestão | Indicadores, metas, exceções e auditoria | Abrir lista que explica o indicador, com seus filtros |
| Administração | Equipe/acessos, catálogos, modelos, integrações, guarda/importação/backup | Atalhos administrativos a partir da operação quando autorizados |
| Ajuda | Instruções por tarefa, treinamento e diagnóstico | Ajuda contextual do estado que o usuário está enfrentando |

Essa é uma taxonomia comum, não uma obrigação de mostrar dez áreas em todos os executáveis. Cada perfil recebe seu recorte. Uma pessoa do balcão não precisa enxergar administração ou toda a contabilidade para receber uma cobrança.

## Etapa 1 — corrigir afirmações de estado e retomada

Tratar A01–A04, A31, A48 e A64–A66. São casos em que o rótulo, a recuperação ou o estado de conclusão podem divergir do que efetivamente aconteceu. A entrega deve conservar IDs e distinguir:

- Documento criado, PDF salvo, impressão iniciada e assinatura concluída.
- Mensagem preparada, envio confirmado manualmente, aceitação do provedor e entrega.
- Guia sem retorno, aceita e glosada.
- XML original e versão regenerada.
- Validação realizada, incompleta ou indisponível.

**Validação:** falhar depois da persistência não deve repetir a mutação. Ausência de resposta não deve virar confirmação positiva. Alterar cadastro não modifica o original arquivado.

## Etapa 2 — fechar caminhos interrompidos

Tratar A05–A06, A32, A35–A43, A51–A52, A58–A59, A61–A62 e A68. Usar o padrão **origem → registro específico → ação → retorno ao recorte anterior**. Estados vazios devem dizer qual dado falta e oferecer a continuação permitida. Paginação precisa permitir alcançar o conjunto autorizado, sem exigir filtros artificiais.

**Validação:** cada atalho abre a entidade esperada; o destino volta com seleção e período preservados; perfis sem acesso recebem orientação útil sem ampliação automática de privilégio.

## Etapa 3 — consolidar nomes, menus e bibliotecas

Tratar A07–A14, A16, A20–A25, A29, A33–A34, A44–A46, A49–A50, A53–A54, A56–A57, A63 e A70. Escolher um nome por assunto e aliases para busca. Definir o proprietário de cada catálogo. Reutilizar componentes de ações do documento e estado das filas onde o significado for igual.

**Validação:** matriz de perfis antes/depois, com cada destino antigo mapeado ao destino canônico. Toda função preservada tem entrada alcançável. Links antigos podem redirecionar; componentes sem referência só são removidos depois de verificar inicialização por recursos/reflexão.

## Etapa 4 — sustentação e continuidade

Tratar A15, A17–A19, A27–A28, A30, A55, A60 e A67–A69 conforme dependências. Ligar ambiente publicado a commit/pacote, localizar o bridge canônico e explicitar fontes históricas. Rever progresso de treinamento, troca de usuário, rodada bloqueante e diagnóstico.

**Validação:** a documentação da instalação reproduz as integrações; histórico não é confundido com backend ativo; mudanças de regra têm responsável e aceite operacional. Prioridade dentro da etapa depende de frequência e impacto observados na clínica, que não foram medidos aqui.

## Padrões de interface recomendados

| Situação | Padrão proposto |
| --- | --- |
| Ação que salva | Verbo + objeto, estado salvando e resultado persistente |
| Sucesso parcial | O que já foi salvo + o que falta + Retomar, mantendo o mesmo ID |
| Ação externa | Indicar que abrir aplicativo/provedor não confirma conclusão |
| Ação indisponível | Explicar se falta seleção, campo, permissão, configuração ou estado |
| Resultado vazio | Distinguir sem dados, sem correspondência, recorte sem itens e falha de leitura |
| Filtros e paginação | Total do recorte, posição e Limpar filtros; independência entre filas |
| Edição de cadastro | Momento de gravação consistente e descarte compreensível |
| Ação irreversível do produto | Consequência e entidade explícitas; histórico e permissão apropriados |
| Busca | Sem acentos, aliases atuais/anteriores e destinos autorizados |
| Indicador | Unidade, período, origem e lista explicativa |
| Menu secundário | Ações ocasionais; o próximo passo necessário permanece evidente |

## O que não deve ser removido na simplificação

Manter a conferência de identidade, autorização por ação, motivo de cancelamento/correção, controle de versão e retomada idempotente onde existem. Manter a distinção entre consentimento e assinatura profissional. Preservar ações de desfazer/reabrir quando o domínio permite. Preservar as advertências de ambiente fictício, integração não habilitada e recebimento ainda não confirmado.

Não eliminar uma fila especializada só por repetir itens de um resumo: ela pode ser o destino canônico de trabalho. Não somar páginas montadas com o mesmo DOM no CRM como se fossem cadastros duplicados. Não apagar os serviços copiados do CRM sem antes resolver o destino do bridge e suas dependências.

## Registro de resolução por achado

Usar os IDs A01–A70 nos tickets/PRs. Cada resolução deve registrar: responsável, escopo, destino canônico, comportamento anterior/depois, perfis afetados, teste de sucesso e falha, versão publicada e evidência de aceite. Registrar também uma decisão de manter o comportamento, com motivo, quando a melhoria proposta não fizer sentido na operação real.

Não há estimativa de horas, redução percentual ou número de cliques neste plano: essas medidas precisam de observação do uso e dos cenários representativos. O valor imediato desta entrega é tornar o conjunto rastreável e permitir mudanças pequenas com consequência conhecida.
