# Abertura da agenda e exclusão de modelos de infusão

## Mudanças

- Agenda e Pedidos da enfermagem reaproveitam sua interface no histórico recente da mesma sessão. Os dados são consultados novamente ao retornar; a reutilização não funciona como cache de resultados clínicos.
- Navegadores que saem do histórico são descartados. O fechamento da janela também libera os navegadores. Assinaturas de fechamento deixam de manter todas as agendas anteriores vivas.
- Agenda e pedidos enviam o estado assim que a leitura termina, sem esperar o timer de 250 ms. Uma resposta abandonada durante a troca de tela não impede a atualização da tela reaberta.
- A Grade sai da navegação. Seus atalhos antigos abrem a Visão geral com a data e o profissional solicitados.
- A Visão geral apresenta atendimentos, cancelamentos, faltas, profissionais e calendário. Não apresenta jornada/capacidade e usa uma consulta que não lê bloqueios nem calcula jornada.
- Modelos de infusão podem ser excluídos com confirmação ou cancelamento. O host valida acesso, confirmação e ID do modelo disponível; o serviço restringe a exclusão a modelos de infusão e registra auditoria. As prescrições existentes permanecem intactas.

## Evidência e limites

O teste `tools/validar-ocupacao-agenda -- --medir-abertura` alterna as duas telas 16 vezes, altera o banco SQLite em cada retorno e aguarda o novo conteúdo no DOM real. Exige apenas dois navegadores, verifica descarte por limite do histórico e fechamento da janela. Antes da correção, as mesmas 16 alternâncias criavam 16 navegadores.

Os tempos locais não medem a VPS. A primeira inicialização do WebView2 continua tendo custo. O preto prolongado relatado pelo usuário não foi reproduzido nesse ambiente; não há evidência para atribuí-lo exclusivamente à VPS ou declarar sua resolução definitiva. O navegador permanece visível, com fundo claro explícito: não foi adicionada uma camada para esconder sua inicialização ou erros.

A validação de agenda cobre filtros, datas, atalhos antigos, erros, permissões e tamanhos de notebook. O teste `tools/validar-agenda-documentos` exerce exclusão/cancelamento no WebView2 real, IDs inválidos, auditoria e preservação de rascunhos e prescrições, com SQLite fictício. Os testes de serviço verificam contagens sem acesso à tabela de bloqueios e restrição/auditoria da exclusão.

O Jev foi consultado sobre diagnóstico, reutilização/ciclo de vida, agenda e exclusão. As respostas estruturadas e as medições locais ficam em `docs/desempenho-global-2026-10-14` e `artifacts/ocupacao-agenda` no ambiente de trabalho. Nenhum acesso à homologação foi retomado e nenhuma publicação em produção faz parte desta entrega.
