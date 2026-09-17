# Continuidade do portal profissional

O portal grava na mesma base do sistema Clínica, usando os serviços de domínio
existentes. Não há uma segunda base de produção nem uma fila de sincronização.
Uma conclusão clínica reaproveita o atendimento e as guias já lançados pela
recepção; presença confirmada não equivale a evolução concluída.

## Funcionalidades

- Protocolo persistente, guias, datas previstas/baixas e situação da recepção.
  O resumo distingue consumo de pacote, conta a receber e recebimento registrado;
  não apresenta valores nem permite movimentar o caixa. Convênio a definir impede
  concluir, mas permite salvar a evolução. Carteirinha ausente/vencida gera aviso.
- Anamnese com versões; medidas com cancelamento justificado; problemas, alergias
  e medicações com edição e mudança de situação. Versões divergentes retornam 409.
- Resultados de exames, evolução de enfermagem e retificação com identificação
  profissional. Planos estruturados com diagnósticos/cuidados continuam sendo
  retificados no módulo clínico para preservar o processo completo.
- Anexos PDF, JPEG e PNG até 5 MiB, com validação de formato, nome e conteúdo.
  Consultas são autenticadas, auditadas e não armazenadas no cache do navegador.
- Modelos de documentos e correção/cancelamento de rascunhos próprios. Correção
  gera nova via e preserva a anterior cancelada; assinados nunca são reescritos.
- Central de pendências: sessões/recepção dos últimos 90 dias, documentos próprios
  sem assinatura de todas as datas e acesso à fila de enfermagem. Paginação de 50.

## Infusão: médico e enfermagem

O núcleo existente mantém a assinatura do prescritor e acrescenta a assinatura
do executante no PDF da prescrição, com as duas áreas visuais lado a lado.
A folha de checagens é um segundo PDF. O portal exibe nomes, conselhos, datas e
acesso à via arquivada com ambas as assinaturas. Falha ao arquivar a assinatura
da folha de checagens permanece visível na fila, mesmo se a prescrição conjunta
já estiver arquivada. Não há repetição automática de autorização SafeID.

O núcleo congelado do SafeID não foi alterado nesta entrega. Os testes de duas
assinaturas usam certificados fictícios. O aceite assistido com os certificados
reais do médico e da enfermagem continua necessário antes de declarar esse
percurso real homologado.

## Segurança e persistência

Todas as escritas passam pelo vínculo profissional, permissão específica,
escopo do paciente, transação e recibo de idempotência. O token CSRF é obrigatório.
Históricos preservam versões e autoria. A API não confia nas permissões da tela.
O limite HTTP global permanece 1 MB; apenas envio de anexo admite 7,1 MB para
acomodar a codificação base64 do arquivo de até 5 MiB.

O papel PostgreSQL do portal recebe somente as operações necessárias nas tabelas
clínicas existentes. Em Lancamentos, recebe leitura de quatro colunas operacionais
(AtendimentoId, CodigoFaturamentoId, Tipo e Status), sem valores nem escrita.
Não há migration nova, alteração de senha, mudança no túnel ou privilégio de DDL.

## Verificação e aceite

| Percurso | Evidência automatizada | Aceite de operação |
|---|---|---|
| Concluir após presença já confirmada | Mesmo atendimento/códigos, sem duplicação | Recepção conferir protocolo real no desktop |
| Guia aberta/baixada e cobrança | Resumo acompanha registros compartilhados | Faturista conferir um caso autorizado |
| Escritas clínicas | Permissões, versões, reenvio, histórico e HTTP/CSRF | Conferir ficha na homologação |
| Documento assinado | Correção bloqueada; dupla assinatura e PDF existentes | Médico e enfermeiro autorizarem os próprios certificados |
| Anexos | Formato/limite, acesso por paciente, no-store e arquivo >1 MB | Abrir arquivo fictício na homologação |
| Interface | Navegador, acessibilidade, 320/820/1180 px e fonte 200% | Uso assistido em tablet da clínica |

As evidências locais incluem 116 testes de domínio/portal/PDF e oito HTTP,
além dos três percursos de interface. Isso não comprova recebimento financeiro
nem substitui a aprovação dos titulares das assinaturas reais.

## Atualização

1. Com ambos os checkouts limpos, executar `tools/empacotar-continuidade-tablet.ps1
   -Site CAMINHO_CLINICA_SITE`. O pacote inclui hashes e revisões dos dois repos.
2. Enviar o pacote para `/home/clinica-admin/tablet-stage/` e conferir seu SHA-256.
3. Executar como root `atualizar-posto.py hml PACOTE SHA RELEASE_ANTERIOR`.
   O instalador confere ambiente, migration existente, backup e escopo de acesso.
4. Validar a API real em homologação, guardar o relatório de aceite do mesmo SHA.
5. Executar `atualizar-posto.py producao PACOTE SHA RELEASE_ANTERIOR`.
   A promoção exige os relatórios de saúde e aceite da homologação do mesmo pacote.

O instalador salva backup privado, concessões novas e comandos para revertê-las;
troca o link da versão atomicamente e verifica saúde, proteção das rotas, página
e processos dos outros serviços. Se essas verificações falham, retorna à versão
anterior e revoga somente as concessões adicionadas por esta atualização.
Não se criam atendimentos fictícios em produção. A aceitação de produção começa
por consultas e exige um caso real autorizado para comprovar o percurso operacional.
