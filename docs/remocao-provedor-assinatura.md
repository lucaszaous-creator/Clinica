# Assinatura exclusivamente com A1 — revisão de 01/10/2026

Alteração local nas branches `codex/assinatura-a1` e `codex/portal-a1`. Não houve
publicação, acesso ao banco de produção nem alteração de documentos da clínica.

## Resultado

O portal usa o A1 individual cadastrado em **Meu certificado**. O desktop importa
o arquivo `.pfx`/`.p12` para a operação. As senhas não são persistidas. A seleção
de certificado instalado no Windows e a autorização remota foram retiradas das
telas. O acesso ao aplicativo continua por usuário e senha.

Foram removidos clientes, opções, serviços, armazenamento de autorizações,
rotas de autorização/retorno, login remoto, configurações administrativas,
segredos de integração, proxy exclusivo, unit correspondente, coleção Postman
e testes exclusivos do provedor. O publicador antigo, preso a um release de
setembro, foi retirado; o atualizador com backup e recuo foi mantido e recebeu
a migration aditiva e os privilégios da tabela A1.

A conferência de endereço da receita passou a usar `assinatura/endereco`, sem
dependência do provedor. O portal não oferece escolha de método. Os materiais
de treinamento passam a demonstrar somente o A1, com dados e API fictícios.

## Preservação documental

Não se apagam PDFs, assinaturas, registros de autoria, auditoria ou histórico
para retirar uma integração. A leitura continua pelos serviços de arquivos
existentes, com as mesmas permissões. Ela não exige A1 cadastrado ou habilitado,
não recalcula o PDF e não aplica outra assinatura.

Foram preservados o leitor de CMS em BER/DER, a conferência de todas as
assinaturas e a revisão incremental que mantém os bytes da primeira assinatura.
Certificados públicos da cadeia de confiança também foram preservados. O nome
de uma autoridade emissora nesses certificados não constitui uma integração
com seu serviço de assinatura.

Os testes usam dois PDFs **fictícios**, produzidos antes da remoção. Com A1
desabilitado e sem credencial cadastrada, consultam documento médico, infusão e
execução pela API e pelos serviços compartilhados pelo desktop. Conferem
igualdade dos bytes originais e do arquivo armazenado, além da integridade de
uma ou duas assinaturas. Esses ensaios não representam um PDF real da cliente
emitido pelo serviço anterior.

Os documentos históricos de engenharia permanecem identificados como registros
antigos. Não são instruções atuais de ativação. Credenciais antigas eventualmente
existentes no banco ou nos backups não foram apagadas; não há código que as use.

## Revisão com Jev

Quatro chamadas reais ao modelo `jev-1.13.0`, em lotes: portal, desktop,
preservação documental e revisão final de migration/privilégios/concorrência.
Foram enviados fontes e testes sintéticos, sem prontuários, PFX ou senhas.
As 13 verificações retornaram `sem_indicio` de defeito concreto no cenário
perguntado. A confiança variou; isso não é garantia de ausência de falhas.
Hipóteses foram confrontadas com os testes locais.

Pedidos, hashes e respostas estão em `artifacts/remocao-provedor/*-resultado.json`
no ambiente local, fora do versionamento. A chave de acesso permaneceu em arquivo
privado e foi lida apenas em memória.

## Validação local

- Núcleo: **2.865 testes aprovados**; testes exclusivos da integração retirada
  foram removidos, mantendo os testes de integridade e compatibilidade CMS.
- API: **21 testes aprovados**, incluindo três cenários de acesso aos PDFs anteriores.
- Publicação: **17 testes aprovados** e sintaxe dos scripts Python conferida.
- Após o ajuste de nomes/comentários do motor CMS, **27 testes de assinatura
  foram repetidos e passaram**, como subconjunto do núcleo.
- Sete percursos de navegador aprovados: cadastro A1, concorrência, janelas,
  endereço da receita, consultório, posto e execução direta. As APIs desses
  percursos são simuladas; a assinatura criptográfica é coberta nos testes .NET.
- Desktop: compilação dos 11 projetos WPF; 219 XAML e 136 construtores
  verificados; seletor A1 conferido em 780 e 960 px, com início vazio, limpeza
  de senha e recusa de seleção anterior após importação inválida.
- API Release e preparador de homologação: compilação sem erros ou avisos.
- Treinamento do portal: 23 vídeos e 23 capas regenerados, com 69 capturas da
  interface atual. Codec, duração e dimensões conferidos; capas inspecionadas.
  Reprodução, busca, filtros, celular, fonte ampliada e WCAG passaram. A aula
  A1 informa expressamente que não executa assinatura real na demonstração.
- Pacote privado do portal reconstruído e conferido: 294 arquivos previstos,
  com hashes válidos e sem documentação interna ou os vídeos antigos retirados.
- Varredura de 1.617 arquivos textuais de código, testes, instalação e interface,
  sem referências à integração retirada. Documentação histórica e artefatos de
  auditoria foram preservados. O identificador `SafeIdivNode` do PDF.js refere-se
  à divisão inteira da biblioteca, sem relação com o provedor.

## Antes da ativação

Seguir [o procedimento A1](assinatura-a1.md), incluindo chaveiro persistente,
privilégios mínimos, certificado individual autorizado e consulta de revogação.
A configuração de rede do servidor deve permitir os endereços oficiais de
validação da autoridade escolhida; a restrição de rede não foi enfraquecida.

O pacote de continuidade agora exige a base na migration
`20260928211121_ChecagemNaoExecutavel` e aplica
`20261001140452_CertificadosA1Profissionais` com SQL idempotente. A base e o SQL
PostgreSQL não foram executados nesta sessão; os testes HTTP usam SQLite.

Na implantação, revisar as configurações efetivas já instaladas no servidor:
retirar os antigos arquivos de ambiente, dependências e serviços do provedor
após guardar o backup de implantação. Os scripts atuais não os reinstalam,
mas uma edição local não remove serviços que já estejam em execução na VPS.
Preservar o armazenamento de PDFs e os metadados clínicos durante essa etapa.

Ainda é necessário homologar o A1 real da cliente e conferir um PDF real já
arquivado, com autorização e acesso apropriados, antes de ativar em produção.

## Preparação de homologação em 01/10/2026

O pré-voo identificou uma correção clínica posterior à base da remoção. Foram
incorporados os campos incrementais de execução, a prescrição com duas assinaturas
e os horários individuais já disponíveis no ambiente instalado. Os testes seguintes
passaram: 2.873 do núcleo, 21 da API e 17 de configuração de publicação.
A compilação do aplicativo clínico e a verificação de 219 XAML também passaram.
O Jev revisou a integridade incremental e a preservação do formato antigo sem
apontar defeito concreto; isso não substitui o aceite do certificado real.

A guarda de publicação aceita também o identificador imutável da migration
histórica `20261001015315_ConclusaoAutomaticaSafeId`. Essa é a única referência
de compatibilidade adicionada ao atualizador; a integração permanece retirada.
Não se excluem migrations já aplicadas, tabelas históricas ou PDFs para remover
uma referência textual. A varredura de código considera essa exceção explícita.
