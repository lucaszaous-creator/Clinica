# Estoque clínico e contas — PR #200 em andamento

## Comportamento atual

A adoção do estoque é gradual. Salvar evolução, concluir atendimento e gerar guias seguem as regras clínicas existentes, sem perguntar por materiais e sem depender de cadastro ou saldo de estoque. O registro de materiais é uma operação posterior no mesmo atendimento; nunca reabre a sessão nem gera outro conjunto de guias.

Em **Estoque → Materiais dos atendimentos**, a gerência escolhe:

- **Desativado** (padrão): consultório e portal seguem a rotina existente.
- **Somente gestão**: gestão/Financeiro registram o consumo dos atendimentos concluídos.
- **Gestão e consultório**: libera também o botão opcional **Registrar materiais**, após a conclusão, no sistema e no portal do médico responsável.

Só a gerência com permissão de edição financeira pode mudar a configuração, com auditoria. O serviço relê usuário, permissão e ativação antes de gravar. A enfermagem ainda não possui porta própria para complementar um registro já feito; continua no escopo aberto abaixo.

Atendimentos concluídos antes da ativação não passam a ser cobrados retroativamente. Desativar e reativar inicia outro período; registros já realizados são preservados e continuam disponíveis ao reativar. A lista da gestão usa período de até 93 dias.

### Estados dos materiais

- **Não informado**: nenhum registro. Deixar campos vazios ou fechar a janela não declara ausência de consumo.
- **Sem consumo declarado**: confirmação explícita de que não houve consumo.
- **Baixa pendente**: o relato foi salvo, mas saldo, lote, validade, item inativo ou movimento futuro impedem a baixa. Nenhum item do conjunto é baixado parcialmente.
- **Baixa registrada**: o conjunto foi baixado uma única vez. Reenvios idênticos não repetem movimentos.

A gestão pode corrigir o inventário e usar **Tentar baixa novamente** sobre o relato preservado. O relato original não é alterado nessa operação. A baixa tardia entra no razão na data do registro, ligada ao atendimento original e com a data da sessão na observação; não tenta inserir movimento antes dos movimentos já existentes. Pedidos com dados diferentes de um registro anterior são recusados até existir correção auditada.

O fechamento da recepção deixou de sugerir automaticamente consumo da última sessão. Chamadas legadas explícitas de insumos permanecem idempotentes e não desfazem o atendimento quando a baixa falha.

## Catálogo e financeiro já presentes na PR

- Código único, código de barras, grupo, uso (procedimento/rotina/ambos), fabricante, apresentação, embalagem/conversão, mínimo/máximo, local descritivo e lote/validade. Medicamentos exigem lote e validade; rotina exige setor. Sem catálogo clínico fictício.
- Compras por embalagem preservam quantidade e custo informados, documento/fornecedor e valor total da conta.
- Contas com contraparte, documento, competência e 1–120 parcelas mensais com centavos exatos.
- Baixas parciais no Financeiro e recepção: só o valor pago entra no caixa; restante continua previsto na mesma obrigação. Histórico e estorno preservam os lançamentos. Recebíveis de cartão incidem apenas sobre o pago.
- Quantidades fracionadas funcionam em pt-BR/en-US; filtro de materiais não apaga quantidades preenchidas.

## Banco e compatibilidade

As quatro migrations anteriores permanecem. A nova `20260922155443_AdocaoGradualMateriais` acrescenta relato JSON, data da baixa e motivo da pendência. Conferências anteriores são marcadas como já baixadas, sem gerar movimentos novos. A configuração reutiliza ConfiguracaoGlobal, sem ativação automática.

O portal usa GET/POST `/api/clinico/atendimentos/{id}/materiais` separados de `/salvar`. A API mantém o campo legado `consumo` no contrato de salvamento, mas não grava esse consumo; responde com aviso para registrar separadamente. Um portal antigo ainda pode apresentar sua pergunta local: publicar API e portal compatíveis em conjunto.

## Escopo ainda aberto da PR

1. Registro complementar pela enfermagem e correção/estorno auditado de relatos/baixas. O registro atual é único por atendimento; não editar diretamente no banco.
2. Seleção assistida de lotes e histórico detalhado. Local ainda descritivo; múltiplos almoxarifados, patrimônio e esterilização não implementados.
3. Juros, desconto, multa, renegociação e visão consolidada do parcelamento contratual.
4. Relatórios por contraparte/documento/setor e indicadores do painel do Gerente. Acompanhamento de materiais já disponível na tela de Estoque.
5. Homologação integrada dos dois repositórios antes da publicação final. Testes de navegador com contrato simulado e testes HTTP da API não substituem esse aceite.

## Validação da adoção gradual

- Solução completa Windows: 0 erros (40 avisos já existentes).
- Suíte SQLite: 2.828 testes aprovados. API HTTP: 9 testes aprovados.
- PostgreSQL isolado: 159 testes de catálogo, fechamento e tablet aprovados; migrations executadas no banco descartável.
- Verificador: 189 XAML, 9 projetos e 132 construtores; compilação sombra dos 10 projetos e espelho de tokens aprovados.
- Harness WPF de gestão: controles, permissões, materiais desativados/pendentes e layout em 960/1366 px aprovados, sem erros de binding.
- Portal: materiais, consultório, posto, modelos e portal de termos aprovados; pacote gerado.

As evidências locais ficam em `artifacts/`: `testes-materiais.log`, `testes-adocao.log`, `testes-finais.log`, `testes-postgres-materiais.log`, `build-isolado.log`, `verificar-suite.log`, `sombra.log` e `layout-windows/`. Consultar os resultados finais e os checks do commit atual; os checks de commits anteriores não validam alterações posteriores.

Testes cobrem conclusão sem estoque, envio legado, isolamento por profissional, revogação, adesão da gerência, ausência de cobrança retroativa, reativação, relato persistido, insuficiência sem baixa parcial e reprocessamento sem duplicar atendimento/guias. O teste HTTP cobre o novo endpoint, CSRF e reenvio. O navegador cobre a ação opcional, abandono sem declaração, resposta perdida, recarga de pendência, responsividade 320–1180 px, fonte 200% e acessibilidade. O harness WPF usa SQLite descartável e o tema real.

## Continuidade

PR principal: https://github.com/lucaszaous-creator/Clinica/pull/200 — branch `codex/estoque-clinico-contas`.
Portal: https://github.com/lucaszaous-creator/clinica-site/pull/20 — branch `codex/materiais-procedimentos`.
As PRs permanecem em rascunho pelo escopo aberto. Esta revisão não faz merge nem release e não altera o banco de produção. O PostgreSQL de teste fica em um cluster isolado, com dados sintéticos.
