# Estoque clínico e contas — trabalho em andamento

Solicitação: profissionalizar o estoque de procedimentos e da rotina da clínica, perguntar pelos materiais usados ao concluir o atendimento e ampliar contas a pagar/receber. O usuário pediu interromper ao atingir 5% de uso disponível do Codex, deixando o trabalho em PR para continuar no dia seguinte. Não publicar release nem enviar esta etapa à main antes de concluir o escopo.

## Implementado nesta etapa

- Cadastro: código interno único, código de barras, grupo, uso em procedimentos/rotina/ambos, fabricante, apresentação, unidade de consumo, embalagem e fator de compra, mínimo/máximo, local, observações e exigência de lote/validade. Medicamentos exigem lote e validade. Não são criados produtos, quantidades, dosagens ou protocolos fictícios.
- Busca por produto, código de barras, fabricante, apresentação, grupo, uso e local. Unidade com histórico não pode ser alterada; código de item inativo não pode ser reutilizado.
- Compras por embalagem convertem quantidade e custo para a unidade de consumo. O histórico conserva quantidade, unidade, fator e custo informados. A conta usa o total da compra antes de arredondar o custo unitário convertido. Fornecedor e documento aparecem no estoque e na obrigação financeira.
- Saída manual pela tela de estoque registra consumo de rotina e exige setor e produto permitido. Consumo de procedimento continua vinculado ao atendimento e ao paciente. Ajuste de produtos rastreados pede lote/validade.
- Conferência de materiais no desktop clínico: pergunta por quantidade e lote; aceita explicitamente “sem consumo”; permite várias linhas do mesmo produto para lotes diferentes; busca não apaga quantidades preenchidas. Baixas antigas são apresentadas para conferência sem repetição.
- Conclusão clínica com materiais é atômica. Falta de saldo mantém o atendimento aberto; a evolução já salva permanece. A conferência identifica responsável e horário, é idempotente e impede baixa posterior repetida pela recepção. O faturamento continua sem campos monetários.
- Contas novas: contraparte, documento, competência e de 1 a 120 parcelas mensais. Distribuição exata dos centavos, datas a partir do vencimento original, transação única e chave de idempotência. Parcelamento da obrigação é independente do calendário de depósitos de cartão.
- Quatro migrations aditivas: CatalogoEstoqueClinico, ParcelamentoContasDocumentado, ConferenciaMateriaisProcedimento e BaixasParciaisObrigacoes. Defaults preservam catálogo e movimentos antigos.

- Portal/tablet: catálogo autorizado por profissional e atendimento, pergunta explícita sobre materiais e declaração de ausência de consumo. Reenvio conserva a chave de idempotência. A API exige conferência na conclusão manual. Publicar em conjunto com a PR do `clinica-site`; um portal antigo recebe mensagem para atualizar em vez de concluir sem conferência.
- Baixas parciais no Financeiro e na recepção: valor efetivamente pago permanece no caixa, restante vira previsão vinculada à mesma obrigação. Retenções previstas conservam os centavos. Histórico disponível em Contas e Caixa; estorno de pagamento desdobrado reabre o valor devido, cancela o recibo vigente e preserva todas as linhas.
- Leitura de quantidades fracionadas independente da cultura do Windows; vírgula e ponto indicam fração, sem separador de milhar. O CI detectou interpretação incorreta de vírgula em en-US; a leitura foi corrigida e o harness cobre as duas culturas.

## Escopo ainda aberto

1. Definir registro complementar de materiais da enfermagem posterior à conclusão médica, concluir os caminhos antigos/automáticos e mostrar pendências de conferência no Gerente. O serviço antigo de conclusão permanece compatível; rotinas automáticas não devem inventar uma declaração de ausência de materiais.
2. Permitir correção/estorno auditado de conferência e consumo sem reescrever o histórico. A conferência atual fecha o consumo da sessão; tentativas divergentes são recusadas.
3. Melhorar seleção de lotes disponíveis, saldo por lote e visibilidade do histórico de conferências no Gerente. Local de armazenamento é cadastro descritivo, não controle de múltiplos almoxarifados. Material permanente/instrumental ainda segue o controle de quantidade; patrimônio/esterilização não estão implementados.
4. Contas: acrescentar juros, desconto, multa, renegociação e apresentação consolidada do parcelamento contratual completo. Baixa parcial, saldo, histórico e estorno já existem nesta etapa. Cada obrigação parcelada mantém suas baixas; o saldo referencia o lançamento original sem reutilizar a chave de criação da parcela.
5. Relatórios e filtros de contas por contraparte/documento e consumo por setor; indicadores e pendências de conferência no Gerente.
6. Conferir o CI da atualização final (principalmente Postgres) e homologar os dois repositórios juntos contra API isolada e banco descartável. As verificações locais abaixo já passaram. Manter as branches e atualizar esta lista conforme o trabalho evoluir.

## Validação feita até aqui

- Primeira etapa (catálogo/contas): 2.802 testes SQLite passaram.
- Conferência clínica: 66 testes focados passaram, incluindo rollback de todas as baixas, não repetição, declaração sem consumo e conclusão que permanece aberta quando falta estoque.
- Compilação da solução Windows passou (0 erros; avisos preexistentes).
- Harness WPF em SQLite isolado verificou as telas de gestão e acrescentou cadastro, parcelamento e materiais em duas larguras cada; confirmou preservação das quantidades ao filtrar.
- Marco b32fda4: 2.809 testes SQLite e a mesma suíte Postgres no CI passaram. O CI Windows desse marco revelou a leitura da vírgula em en-US; correção incluída na atualização seguinte.
- Integração portal: 137 testes de tablet/fechamento passaram; interface com reenvio, consumo e ausência explícita de materiais, 320–1180 px, fonte 200% e WCAG aprovada.
- Baixas parciais: 74 testes focados de contas, tradução Npgsql e gestão passaram; solução Windows compilou sem erros.
- Verificação final local: 2.816 testes SQLite passaram; o teste adicional de cartão parcial também passou isoladamente (depósitos apenas do valor pago, taxas, saldo restante). Os 9 testes HTTP da API passaram, incluindo recusa de conclusão sem conferência e isolamento do catálogo por profissional.
- Espelho de tokens, verificador da suíte (189 XAML, 9 projetos, 132 construtores), compilação sombra dos 10 projetos WPF e harness de gestão passaram. A verificação cobre quantidades em pt-BR/en-US e as telas de histórico de Contas e Caixa.
- Portal: testes de materiais, interface do consultório, posto clínico e modelos passaram; pacote gerado localmente. Os testes de navegador usam contratos simulados; a API possui testes HTTP separados, sem substituir a homologação integrada pendente.
- Consultar os checks das PRs para o resultado remoto da atualização final; não considerar o CI anterior como validação das alterações posteriores.

## Continuidade

PR principal: https://github.com/lucaszaous-creator/Clinica/pull/200 (rascunho), branch `codex/estoque-clinico-contas`. Portal: https://github.com/lucaszaous-creator/clinica-site/pull/20 (rascunho), branch `codex/materiais-procedimentos`. Não houve merge, release ou alteração no banco da VPS nesta etapa. Ao retomar, ler esta lista e verificar os checks das duas PRs antes de continuar.
