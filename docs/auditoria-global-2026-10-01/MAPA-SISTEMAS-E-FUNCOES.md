# Mapa dos sistemas, funções e responsabilidades

> **Escopo vigente — portal web preservado:** por decisão do proprietário em 01/10/2026, não alterar o portal web nem reunir evolução e consentimentos nele. Recomendações sobre portal neste inventário são históricas e não autorizam implementação. Ver [regra obrigatória, imagens desktop e cobertura](ESCOPO-E-PROPOSTAS-VISUAIS.md).

Este mapa organiza o catálogo completo por finalidade. A existência de dois componentes com o mesmo assunto não demonstra, sozinha, redundância: uma fila geral, uma ficha individual e uma janela de edição podem ser três entradas legítimas para o mesmo registro. O problema aparece quando há regras distintas, perda de contexto, nomes incompatíveis ou repetição da mesma decisão.

## Sistemas e fronteiras

| Sistema | Quem o usa / finalidade | Entradas e organização encontradas | Lugar recomendado |
| --- | --- | --- | --- |
| Recepção desktop | Balcão, agenda, cadastro, cobrança e continuidade | Painel, lista do dia, grade, marcação/lançamento, pacientes, documentos, pagamentos, acompanhamento e configuração da equipe | Trabalho do dia e paciente como contexto principal |
| Clínico desktop | Profissional, atendimento e prontuário | Meu dia, semana, pendências, prontuários, workspace do paciente, prescrições, documentos, enfermagem | Sessão atual com próximos passos e ficha longitudinal separada |
| Financeiro desktop | Movimento financeiro e controles | Caixa, fechamento, contas, recebíveis, conciliação, extrato, taxas, repasses, resultado e estoque, além de contextos de recepção/clínico | Cobranças, dinheiro, conferência e gestão com vocabulário próprio |
| Faturamento desktop (`Clinica.Desktop`) | Ciclo das guias e operadoras | Host consolidado: pendências, consulta, faturados, NC, glosas, TISS, relatórios/configurações; acompanhamento adicional | Guia/lote como unidade de trabalho, com correção contextual |
| Gerente desktop | Visão transversal e administração | Compõe os módulos da suíte; indicadores, metas, preços, campanhas, acessos, auditoria, guarda, importação e configuração | Entrada por exceções e indicadores; cadastros e manutenção em Administração |
| `Clinica.Web` | Consulta web de informações autorizadas | `/dia`, `/painel`, `/pacientes`, `/paciente/{id}`, entrada/saída e permissões | Produto de consulta claramente identificado; continuidade para histórico completo |
| Portal profissional (`clinica-site/portal/profissional`) | Consultório e enfermagem em navegador/tablet | Meu dia, agenda da enfermagem, infusões, pendências, pacientes, documentos para assinar, modelos, termos, treinamento | Mesmos nomes e estados do domínio desktop, adaptados ao dispositivo |
| Portal de termos (`clinica-site/portal`) | Equipe prepara coleta; paciente lê/assina | Entrada da equipe, BSV de hoje, conferência, preparação, leitura, assinatura/recusa, resultado e retomada de arquivamento | Coleta de termos presencial; não apresentar como autosserviço amplo |
| Site institucional | Pessoa buscando informação e contato | Clínica, especialidades, condições, primeira consulta, convênios, atendimento, contato, privacidade, acessibilidade e mapa | Explicar e facilitar contato; declarar quando a marcação depende da equipe |
| `semdor-crm` | Conversas, relacionamento e operação da recepção | Meu dia, atendimentos, contatos, funil, tarefas, agenda/confirmacões, recall, envios clínicos, relatórios, auditoria, IA e configurações | Conversa com paciente corretamente identificado, tarefa e agenda conectadas |

Os cinco executáveis desktop compartilham módulos e componentes. A composição real normaliza rótulos, filtra permissões, deduplica chaves e agrupa abas. Por isso, o [mapa por perfil](NAVEGACAO-POR-PERFIL.md) é a referência para o menu derivado, enquanto o [catálogo](CATALOGO.md) mostra declarações de origem.

## Funções por domínio

Cada linha abaixo tem um destino sugerido e deve ser lida com as fichas dos arquivos correspondentes em [Revisão por superfície](REVISAO-POR-SUPERFICIE.md). Funções internas e serviços estão indexados em [funcoes.csv](funcoes.csv); a tabela não substitui esse índice.

| Domínio | Funções e superfícies identificadas | Melhor organização proposta / cuidado |
| --- | --- | --- |
| Entrada e sessão de usuário | Login, configuração inicial, troca de usuário/senha, perfis, pesquisa do shell, ajuda | Identidade visível, encerramento controlado e pesquisa que alcance os nomes atuais; A53–A54, A60 |
| Agenda e disponibilidade | Grade dia/semana, lista do dia, vagas, agendamento, detalhes, remarcação, série, cancelamento, faltas, bloqueios, lista de espera, horários por profissional/sala | Uma entidade de agendamento, com entradas contextuais. Não confundir agendar com registrar chegada ou lançar atendimento |
| Confirmações | Rodada, amanhã, e-mail, abrir WhatsApp, confirmação manual, resposta pela integração | Separar preparar contato, envio e resposta; o CRM e o desktop não devem afirmar estados contraditórios |
| Chegada e atendimento administrativo | Fila, próximo passo, novo atendimento, convênio, autorização, capa inicial, fechamento, lançamento, estorno | Próximo passo depende do estado e da permissão; preservar a sessão ao ir à cobrança/termo/documento |
| Cadastro do paciente | Busca, novo/editar, foto, contatos, origem, convênio, carteirinha, elegibilidade, autorizações | Um ID de paciente; seleção contextual com retorno. Não usar apenas telefone como prova de identidade |
| Ficha administrativa | Resumo, agenda futura, pendências, sessões/guias, cobranças, relacionamento, privacidade e termos | Remover aba repetida e nomear os recortes; A07, A16, A56–A57 |
| Sessão clínica | Iniciar, salvar evolução, vincular registro, consultar ficha/anexos, indicar BSV, concluir/reabrir, imprimir | Conclusão de sessão, salvamento de evolução e assinatura são estados distintos; resultado deve explicar o que falta |
| Histórico longitudinal | Evoluções, sessões anteriores, correções/versões, resumo, anamnese, linha do tempo, enfermagem | Uma consulta longitudinal com filtros por tipo; atalhos de sessão podem abrir já no recorte correto |
| Problemas e alertas | Diagnósticos, alergias/intolerâncias, antecedentes, medicamentos contínuos, resolver/reabrir/descartar | Contexto compartilhado, mas não fundir alergia resolvida com ausência de alergia. Confirmar com o fluxo assistencial |
| Medidas, dor e avaliações | Registro de medidas, curvas, EVA, escalas/instrumentos, cancelamento justificado e exportação | Evolução e medidas sob o paciente; preservar autoria, unidade, instrumento e momento |
| Exames | Pedido, detalhes, laudo, resultados, cancelamento e vínculo ao pedido | Pedido e resultado no mesmo caminho; preservar Anexar laudo já contextual existente. A41 e A61 tratam lacunas específicas |
| Arquivos | Anexos da sessão, anexos do paciente, resultado anexado, abrir/baixar/cancelar | Mostrar finalidade e vínculo; não tratar todo PDF como documento emitido ou exame |
| Documentos clínicos | Receita, atestado, comparecimento, pedido de exame, relatório, anamnese, edição/modelo, emissão, segunda via, assinatura, cancelamento | Resultado único da emissão com ações pelo mesmo ID. Central de documentos ampla e fila de assinatura explicitamente nomeada; A01–A06, A11–A14 |
| Consentimentos e termos | Modelos/exigências, termo avulso ou da sessão, identidade, leitura, declarações, assinatura, recusa, arquivamento, privacidade e revogação | Não substituir consentimento por Imprimir. Contextualizar coleta com paciente/sessão/tipo; A28, A44–A45 |
| Prescrição e infusão | Prescrição interna, avulsa/externa, revisão, impressão, checagens, não realizado/não executável, horários, execução, encerramento, assinatura médica/enfermagem e devolução | Separar prescrever, executar, concluir e assinar; uma folha e seu histórico de versões, sem pedir de novo o que já foi gravado |
| Processo de enfermagem | Agenda, consulta, evolução, passagem, procedimentos, folha, sinais, intercorrências, modelos e materiais | Unidade sessão/paciente; evitar navegar pelo histórico inteiro para imprimir uma passagem. A20 e A43 |
| Pacotes e particular | Catálogo, venda, consumo, preços, orçamento, pagamento, recibo | Catálogo administrativo separado de vender/consumir. Mesma referência de preço em Recepção e Financeiro; A05 e A08 |
| Guias e pendências | Consulta de validade, segundo código, semáforo, baixa individual/lote, observação, NC, reabertura, histórico e capa | Fila acionável com contexto de paciente/guia, sem confundir baixa de guia com entrada de dinheiro |
| TISS | Pré-validação, agrupamento por operadora, geração XML, registro de envio/protocolo, retorno manual/XML, glosas, recurso | Prévia de prontidão e estados por guia. Original arquivado separado de versão regenerada; A64–A68 |
| Glosas | Registro, motivo, prazo, explicação, reapresentação, recuperada e receita glosada | Um caso rastreável ligando guia, recurso e reflexo financeiro, com atalhos mantendo seleção; A32 e A37 |
| Caixa e recebimento | Lançamento, realizar/cancelar, recibo, Pix, dívidas, fechamento, recontagem, divergência e exportação | Datas e eventos explícitos; recebimento pelo balcão não precisa expor toda gestão financeira. A35–A37, A58, A62 |
| Contas e planejamento | A pagar/receber, fixas, recorrência, geração, baixa, adiamento, fluxo e orçamento/teto | Próxima obrigação com prazo editável; A27. Não confundir orçamento financeiro com proposta ao paciente |
| Banco e cartão | Importar OFX, cruzar/conferir/desfazer, adquirentes, taxas, previstos, depósitos confirmados | Um quadro de conferência com origem, data e diferença; manter desfazer e não inferir recebimento de código Pix |
| Produção, repasse e resultado | Produção, regras, apuração, receitas recebidas, custos, impostos, simulador, resultado e rentabilidade | Indicadores deixam visível sua base: produzido, faturado, recebido, competência ou caixa. Não juntar totais semanticamente distintos |
| Estoque | Itens, entrada/saída, inventário, extrato, mínimos/validade, compras, materiais de sessão e custo | Catálogo e operação de estoque com vínculo clínico preservado; não ser página inicial acidental do Financeiro, A10 |
| Direção | Painel, indicadores, metas, origem, retenção, campanhas, faturamento gerencial e custo de transação | Resumo leva ao detalhe com filtro; nomear a pergunta de cada painel, evitando números repetidos sem decisão |
| Administração | Dados da clínica/recebimento, convênios, modalidades, especialidades, TUSS, preços, regras, equipe, salas e usuários | Cadastros canônicos por entidade; gravação consistente. A21, A33–A34 |
| Integrações e continuidade | SafeID, armazenamento/publicação, e-mail, tablet, backup, importação, bridge CRM e sincronizações | Status e ação de recuperação por integração, versão/ambiente visíveis; A17–A19, A59 |
| Guarda e auditoria | Acessos ao prontuário, trilha de ações, exportação do titular, exportação do prontuário/clínica, anonimização | Tipos de exportação distintos com conteúdo/destinatário explícitos. Esta auditoria não avalia conformidade legal |
| Treinamento e suporte | Catálogo, vídeo, progresso, ajuda, erros/logs | Ajuda por tarefa e estado real; progresso com escopo explícito e diagnóstico recuperável; A55, A69–A70 |
| CRM — conversa | Assumir, responder, nota, transferir, aceitar/rejeitar transferência, presença, mídia/áudio, modelos, busca no histórico, prioridade, concluir/reabrir | Conversa como contexto persistente; abertura de canal não é envio. Estado da API e feedback após salvar independentes; A24, A47–A50 |
| CRM — contato e trabalho futuro | Contatos, etiquetas, origem, funil, tarefas, follow-up, Minha atenção, conclusão com retorno, recall | Diferenciar lembrete pessoal, tarefa atribuída e campanha; consolidar a visão de próximos passos sem apagar os tipos; A25 |
| CRM — agenda | Paciente contextual, consulta de vagas, criar/remarcar, conflitos, confirmação, histórico de pedidos, solução operacional | Identidade confirmada e agenda canônica; Registrar solução não significa Remarcar. A26 |
| CRM — operação | Saúde, incidentes, consumo, tarifas, canais, sincronização, falhas, envios do Clínico, relatórios e auditoria de conversas | Exceções acionáveis e paginação; distinguir simulação de custo de cobrança real. A23 e A51 |
| CRM — IA | Configuração/modo, base de conhecimento, aprendizado proposto, exemplos, testes/simulação, revisão, histórico, assumir humano | Estados dependem da instalação; não declarar automação disponível apenas porque a tela existe. Preservar revisão e identificação de ambiente |

## Site institucional: todas as páginas de conteúdo identificadas

Os nomes abaixo são arquivos fonte; o slug de URL pode ser diferente, como `acupuntura-medica.html` publicado como `/acupuntura/`. Links derivados devem usar os slugs do gerador, não o nome físico.

| Conteúdo | Função no percurso | Melhoria de organização sugerida |
| --- | --- | --- |
| `inicio.html` | Entrada e orientação | Ação de contato coerente com o cabeçalho e especialidade de interesse |
| `a-clinica.html`, `dr-gustavo-lacerda.html` | Instituição e profissional | Relacionar profissional e serviços sem repetir todo conteúdo |
| `especialidades.html` | Índice de atendimento | Uma entrada por especialidade com destino canônico |
| `clinica-da-dor.html`, `acupuntura-medica.html`, `bloqueio-simpatico-venoso.html` | Serviços e procedimentos | Diferenciar informação, indicação clínica e agendamento |
| `endocrinologia.html`, `geriatria.html`, `ginecologia.html`, `psiquiatria.html` | Especialidades | CTA que preserve a especialidade ao iniciar contato |
| `fibromialgia.html` | Informação sobre uma condição | Conduzir ao serviço pertinente sem prometer tratamento individual |
| `atendimento.html`, `primeira-consulta.html`, `para-pacientes.html` | Preparar a visita | Uma sequência clara: contato → confirmação → o que levar → chegada |
| `convenios.html` | Cobertura/particular | Explicitar que lista de convênios não substitui conferência individual |
| `contato.html` | Telefone, WhatsApp, endereço e mapa | Ação direta e consistente; A46 |
| `acessibilidade.html`, `politica-de-privacidade.html` | Informação de apoio | Orientações alcançáveis em todas as páginas; revisão editorial própria quando mudarem |
| `mapa-do-site.html`, `404.html` | Localização e recuperação | Voltar a conteúdo útil, contato e índice; verificar links no build final |

`modelos/base.html` e `modelos/rodape.html` são templates compartilhados. Não são páginas duplicadas nem devem contar como duas implementações de cada link que aparece no site.

## O que unificar e o que preservar

| Assunto | Unificar | Preservar separado |
| --- | --- | --- |
| Paciente | Identidade, cadastro e abertura contextual | Contato de telefone não confirmado, conta do usuário e responsável por atendimento |
| Agenda | Agendamento e seus estados entre apps | Vaga sugerida, reserva confirmada, chegada, sessão e lançamento financeiro |
| Documento | Resultado de emissão, ID, impressão, ações e histórico | Documento emitido, arquivo anexado, modelo, assinatura profissional e consentimento |
| Pendências | Visão de trabalho e abertura do registro | Pendência clínica, financeira, guia, assinatura e falha técnica |
| Relacionamento | Histórico e próximo responsável | Tentativa de contato, mensagem enviada/entregue, consentimento e conclusão clínica |
| Preços | Catálogo por modalidade/convênio/validade | Particular, convênio, valor de pacote e custo apurado |
| Modelos | Biblioteca e administração por tipo | Aplicação contextual de evolução, enfermagem, termo e prescrição |
| Configuração | Dono e local de edição de cada entidade | Operação diária e administração de uso ocasional |
| Métricas | Definições e filtros comparáveis | Produção, faturamento, recebimento, custo e simulação |

## Fronteiras que não podem ser inferidas

O CRM contém cópias de 445 fontes com caminhos também presentes em Clinica; 442 são idênticas na fotografia e três divergem. O README e o bloqueio de publicação do projeto bridge indicam sua condição histórica. Não se deve multiplicar o número de sistemas ativos a partir dessas cópias. Também não se deve declarar que a integração publicada está quebrada: faltou ligar o bridge efetivamente implantado ao código canônico nas bases disponíveis.

Protótipos, kits de interface, vídeos e repositórios auxiliares de acesso ao banco não são automaticamente novas aplicações da clínica. O manifesto conserva seus caminhos rastreados quando pertencem às três bases; artefatos locais não versionados são descritos na cobertura, sem presumir uso em produção.
