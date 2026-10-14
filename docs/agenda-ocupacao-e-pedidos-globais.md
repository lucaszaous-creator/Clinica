# Agenda e pedidos da enfermagem

A agenda da recepção abre em **Visão geral**, com dia, semana, mês e filtro por profissional. Atendimentos e grade de horários permanecem acessíveis. Marcar, confirmar, consultar retornos e conferir atendimentos estão reunidos em **Operações da agenda**.

## Onde a recepção encontra a solicitação

- Atalho **Pedidos da enfermagem** no cabeçalho, ao lado do sino, em qualquer tela. Com pedidos novos, fica azul e apresenta a contagem. Sem novos, continua acessível em aparência discreta.
- Sino com contador e cartão que abre a mesma caixa.
- Entrada **Pedidos da enfermagem** no menu Gestão.

A caixa inicia com pedidos abertos de todas as datas. Cada pedido informa dia, período, modalidade, quantidade, autora e observação. Abrir o sino não registra leitura. Abrir um pedido registra **Visto pela recepção**, compartilhado com a equipe, e atualiza o contador local. Alterações feitas em outros dispositivos são consultadas a cada 30 segundos enquanto a janela está aberta. Uma falha mostra indicação de indisponibilidade; não transforma os pedidos em zero.

O botão **Ver este dia na agenda** abre a data indicada e remove filtros de profissional e sala que poderiam esconder os atendimentos. Encerrar o aviso é opcional e não confirma agendamento. Não há atribuição de responsável, reserva automática ou nova permissão para a técnica agendar/editar.

## O que a ocupação representa

Percentual do tempo ocupado dentro das jornadas cadastradas dos profissionais, descontando bloqueios globais e do profissional. Intervalos simultâneos são unidos para não duplicar minutos. Cancelados e faltas não ocupam a jornada. Bloqueios exclusivos de sala não representam indisponibilidade de toda a equipe.

Jornada incompleta ou atendimento sem profissional impede apresentar um percentual global como completo. A tela identifica os profissionais sem jornada e preserva a leitura dos atendimentos. Tempo livre somado não representa vagas garantidas nem capacidade física de salas/cadeiras. Atendimentos fora da jornada ou sobre bloqueios são sinalizados.

## Verificação em 14/10/2026

- Apresentação React: TypeScript e build aprovados.
- Regras: 2.979 testes aprovados, incluindo 55 de ocupação, planejamento e tradução de consultas para PostgreSQL.
- WebView2 real e SQLite sintético: dia/semana/mês, clique no dia, filtros, pré-preenchimento ao agendar, preservação do profissional na grade, atalho do pedido para a data correta, erros/recuperação, jornada incompleta, estado vazio e restrição da enfermagem.
- Avisos globais: chegada fora da agenda, sino sem marcar leitura, atualização automática após leitura e indicação de falha. Cabeçalho conferido em 880, 1280 e 1600 pixels; conteúdo em 620, 900 e 1100 pixels sem vazamento horizontal.
- Inspeção visual das capturas reais do cabeçalho, dia, semana, mês e largura compacta. Dados exclusivamente fictícios. Capturas locais em `artifacts/ocupacao-agenda/`.
- Espelho de 33 cores aprovado; verificações de XAML e recursos realizadas.

Jev 1.13.0 recebeu o fluxo, os contratos e a apresentação em código, sem dados de pacientes reais nem imagens rasterizadas. Considerou adequadas a hierarquia, a semântica da ocupação, a descoberta dos pedidos e a divisão de responsabilidades. Sua seleção de maior ajuste teve baixa confiança (0,24): explicitar quem está sem jornada. A indicação nominal foi incorporada, assim como os dias da semana nas células compactas. Essa avaliação não substitui os testes executados e não constitui aprovação clínica.

Esta alteração não inclui migração de banco nem publicação no canal da clínica. CI e publicação têm estados próprios, informados no PR.
