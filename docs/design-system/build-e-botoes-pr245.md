# Build e ações dos cinco módulos — PR 245

## Falhas corrigidas

- O build `37933373083`, job `113829306192`, parou no QA do Financeiro:
  o download da aula `pacotes` respondeu HTTP 500. A compilação havia passado.
  As duas aulas usadas pelo QA agora acompanham os testes e continuam sendo
  verificadas pelo SHA-256 do catálogo publicado. Reprodução, retomada,
  conclusão e reinício continuam obrigatórios. Não há fallback que aprove
  o teste sem reproduzir. O erro HTTP 500, ausência de arquivo/progresso
  indevido e recuperação seguinte têm teste separado.
- O QA da Recepção, chamado apenas com `--web`, antes encerrava após validar
  os contratos. Agora executa os fluxos e abre o WebView2.
- `Simular`, em Taxas, retornava silenciosamente com valor inválido e
  substituía parcelas inválidas por uma parcela. Agora explica o campo que
  precisa ser corrigido e só calcula com os dados válidos. O QA clica no
  botão, confere os dois avisos e o resultado de R$ 200,00, além de verificar
  que nenhum lançamento foi criado.
- O build passa a executar também os fluxos completos da Recepção,
  Faturamento, Gerente e ferramentas compartilhadas. O mapa e capturas são
  guardados como artefato mesmo quando um teste falha.
- `Editar meta` não carregava as observações salvas. Alterar o valor e salvar
  novamente podia apagá-las. A lista agora transporta as observações para o
  formulário; o teste cria, reabre, altera somente o valor, salva e confirma
  a preservação, além de cancelar edição e recusar exclusão.

## Mapeamento solicitado ao Jev

O inventário reproduzível extrai **986 ações** dos registros de **76 páginas
e 98 formulários**, incluindo ações de cabeçalho, seção e linha. As ações
parametrizadas têm identificadores distintos. O Jev real (`jev-1.13.0`)
classificou 677 grupos de comando/rótulo. Seis ações inicialmente ambíguas
foram novamente analisadas com trechos dos métodos C#.

[Mapa com rótulos, comandos, condições e resultados esperados](mapa-botoes-jev-pr245.json).

A interpretação do código prevalece sobre a classificação probabilística.
Por exemplo, **Cobrar** abre a mensagem no WhatsApp e não dá baixa na dívida;
essa distinção é descrita explicitamente no resultado esperado, sem tratar
a classificação geral como prova de pagamento. As respostas e os hashes
dos pedidos são preservados.

O Jev recebeu contratos e trechos de código, sem dados de pacientes. Ele não
operou o aplicativo. O mapa identifica ações; **não representa 986 cliques
com resultado individualmente comprovado**. A cobertura de execução abaixo
é de cenários concretos e não certifica todas as combinações de estado/perfil.

## Cenários executáveis

| Área | Resultado conferido | Teste |
|---|---|---|
| Financeiro | 15 rotas em três tamanhos; criar lançamento, conta, categoria e item; cancelamento; valor decimal; paciente correto; histórico; acesso revogado; simulação sem gravação | `tools/validar-design-financeiro --web` |
| Recepção | Cadastro e foto; agendar/remarcar/cancelar; recebimento parcial; cancelamento sem gravação; perfil restrito; 16 páginas em três tamanhos | `qa/recepcao-web --web` |
| Faturamento | Anotação; baixa individual/em lote; glosa; não conformidade/reabertura; TUSS; envio/retorno de lote; formulário obrigatório; persistência e validação | `tools/validar-faturamento-web --web` |
| Gerente | Busca/seleção/cancelamento; páginas em três tamanhos; usuário/permissões, meta e preço pelo controller; criação e cancelamento também pelos botões da interface | `tools/validar-suite-web --web` e `GerenteGravacoesQa` |
| Documentos | Conferência com código vazio/inexistente nas duas telas; modelo aplicado no conteúdo; botão Emitir e PDF gerado | `GerenteOperacoesUiQa` e clínico `--infusao-modelos` |
| Consultório | Medidas, resultados, alertas, escalas, evolução, mapa corporal, campos complementares, rascunho, histórico, impressão e ações por perfil | `tests/Clinica.Clinico.Web.Qa --web` |
| Modelos/infusão | Sugestões e seleção; modelo altera conteúdo; salvar e reabrir; preparo agrupado; copiar sem gravar; rascunho fora da fila; liberação para enfermagem | clínico `--infusao-modelos` |
| Enfermagem | Prescrição recebida com preparo/medicamentos; evolução vinculada à sessão correta com dois horários no dia; Ver abre registro completo; sessão médica contém a evolução | clínico `--infusao-modelos` |
| Seções clínicas | Exames/anexos na ficha e atendimento; seis seções de anamnese; sete opções de medidas; cinco de avaliações; seleção e conteúdo correspondente | clínico `--infusao-modelos` |
| Ferramentas | Menu/teclado/Escape; busca global/F5; avisos/lidos/privacidade; catálogo/filtro; vídeo, progresso e retomada; bloqueio de origem externa | Financeiro e Faturamento `--ferramentas-only` |

Os cenários usam SQLite em memória e dados sintéticos. Os testes de regras
somam 2.989 aprovações, sem falhas/ignorados, incluindo o novo cenário HTTP 500.
Os logs consolidados no mapa têm SHA-256 e
trecho final para diferenciar execução de simples registro de comandos.

## Limites que permanecem

O inventário ainda não dispõe de evidência individual de execução para cada
uma das 986 ações. Ações dependentes de certificado A1, tablet/câmera física,
envio de WhatsApp, cobrança Pix real e serviços externos não foram executadas
contra equipamentos, contas ou pacientes reais. Testes de contrato, testes
de regras e classificação pelo Jev não substituem esses resultados.

Para reproduzir o mapa: executar `tools/validar-suite-web`, depois
`python tools/qa-web/mapear_jev.py --executar --chave <arquivo-fora-do-repositorio>`
e repetir com `--detalhar`. `consolidar_mapa.py` consolida os resultados
existentes e logs, sem nova chamada à API. Sem `--executar`, o mapeador apenas
prepara pedidos ou reutiliza respostas já presentes.
