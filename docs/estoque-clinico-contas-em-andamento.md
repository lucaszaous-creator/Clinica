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
- Três migrations aditivas: CatalogoEstoqueClinico, ParcelamentoContasDocumentado e ConferenciaMateriaisProcedimento. Defaults preservam catálogo e movimentos antigos.

## Escopo ainda aberto

1. Levar a conferência de materiais ao tablet/portal profissional e fechar todos os caminhos de conclusão; definir registro complementar de materiais da enfermagem posterior à conclusão médica. O serviço antigo de conclusão permanece compatível; nem toda chamada exige a nova conferência ainda.
2. Permitir correção/estorno auditado de conferência e consumo sem reescrever o histórico. A conferência atual fecha o consumo da sessão; tentativas divergentes são recusadas.
3. Melhorar seleção de lotes disponíveis, saldo por lote e visibilidade do histórico de conferências no Gerente. Local de armazenamento é cadastro descritivo, não controle de múltiplos almoxarifados. Material permanente/instrumental ainda segue o controle de quantidade; patrimônio/esterilização não estão implementados.
4. Contas: baixa parcial, saldo remanescente, histórico da obrigação, juros/desconto/multa e estorno auditado, mantendo caixa, recebíveis, recebimento na recepção e conciliação consistentes. Não representar pagamento parcial somente alterando o valor sem rastrear a obrigação e as baixas.
5. Relatórios e filtros de contas por contraparte/documento e consumo por setor; indicadores e pendências de conferência no Gerente.
6. Executar validação final SQLite/Postgres, API, WPF e portais; salvar PR e registrar pendências concretas no limite pedido. Manter a branch e atualizar esta lista conforme o trabalho evoluir.

## Validação feita até aqui

- Primeira etapa (catálogo/contas): 2.802 testes SQLite passaram.
- Conferência clínica: 66 testes focados passaram, incluindo rollback de todas as baixas, não repetição, declaração sem consumo e conclusão que permanece aberta quando falta estoque.
- Compilação da solução Windows passou (0 erros; avisos preexistentes).
- Harness WPF em SQLite isolado verificou as telas de gestão e acrescentou cadastro, parcelamento e materiais em duas larguras cada; confirmou preservação das quantidades ao filtrar.
- Ainda atualizar com os resultados da suíte completa após todas as alterações, verificadores estáticos e CI/Postgres.
