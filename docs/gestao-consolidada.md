# Gestão consolidada — cinco módulos

Esta entrega conecta pagamentos do balcão, caixa, extrato, compras e estoque ao
painel do Gerente. Todos usam os mesmos lançamentos financeiros e movimentos de
estoque. O Gerente acompanha os resultados e abre a tela responsável por resolver
cada pendência; cada equipe conserva suas funções e permissões.

## Responsabilidades

| Módulo | Operação | Relação com a gestão |
| --- | --- | --- |
| Gerente | Painel da direção, indicadores, metas, rentabilidade, acessos e auditoria | Recebimentos brutos, resultado após deduções registradas, inadimplência, depósitos, conferência bancária e alertas de estoque; acesso às telas dos demais módulos conforme permissão |
| Recepção | Cadastro, agenda, fila, fechamento, pacotes e **Pagamentos na recepção** | Recebe cobranças particulares existentes por paciente, informa data e forma de pagamento, registra cartão e emite segunda via do mesmo recibo |
| Clínico | Prontuário, prescrição, evolução e conclusão assistencial | Produz o atendimento que fundamenta cobrança e consumo; não recebe permissão financeira por esta entrega |
| Faturamento | Guias, códigos, lotes, baixas, glosas e pendências dos convênios | Continua independente; guia e lançamento financeiro permanecem entidades distintas e vinculadas |
| Financeiro | Caixa, contas, taxas, recebíveis, extrato, fechamento diário, compras e estoque | Confere o pagamento real, controla despesas e depósitos, concilia e resolve as pendências apontadas pelo Gerente |

A permissão existente de venda de pacotes agora explicita também receber cobranças
particulares e emitir recibos. Ela não concede leitura do caixa geral nem edição
de despesas à recepção. Um perfil personalizado pode retirar essa permissão.

## Pagamento até a conferência

1. Concluir a sessão na recepção e escolher **pago** ou **a receber**, com valor e
   vencimento. O pacote mantém seu fluxo de venda e suas cobranças próprias.
2. Em **Pagamentos na recepção**, selecionar o paciente e receber a cobrança em
   aberto. O sistema atualiza o lançamento existente: não cria uma segunda receita.
3. Informar a data efetiva e a forma utilizada. Para cartão, informar adquirente,
   bandeira e parcelas, com taxa e prazo previamente cadastrados no Financeiro.
   A taxa é copiada para o pagamento; renegociar o catálogo não reescreve o histórico.
4. Emitir o recibo após receber. Repetir a emissão devolve o recibo vigente do mesmo
   lançamento. Cobrança prevista não gera comprovante de recebimento.
5. O Financeiro confere a gaveta e os depósitos. Caixa diário conferido precisa ser
   reaberto com motivo antes de incluir ou cancelar um pagamento em dinheiro.
6. Importar o OFX, conferir a proposta e confirmar. A operação revalida os dados no
   momento da gravação, inclusive quando outra estação recebeu ou conciliou antes.

Caixa e Contas também solicitam data e forma de pagamento ao dar baixa. Cobrança de
outro paciente, valor alterado ou recebimento já realizado são recusados no balcão.

## Significado dos números

| Número | Critério |
| --- | --- |
| Recebido bruto no mês | Lançamentos realizados pela data do pagamento; competência original é preservada |
| Resultado líquido do mês | Bruto recebido menos taxas, impostos registrados e saídas realizadas; acompanha a convenção de caixa do resultado mensal existente |
| Recebível de cartão | Venda registrada que ainda espera depósito da adquirente |
| Depósito de cartão | Bruto menos taxa de adquirente; tributos provisionados separadamente não diminuem esse depósito |
| Crédito da operadora | Valor transferido após as retenções registradas na receita do convênio |
| Conciliado | Pagamento conferido contra uma transação do extrato, identificado por banco/agência/conta e FITID |
| Custo de insumos | Custo da baixa na data do consumo; compra posterior não altera a sessão passada |

Venda recebida em cartão e dinheiro depositado no banco são etapas diferentes.
O painel não apresenta recebimento registrado como se já tivesse sido conciliado.
Falha de leitura do dinheiro do mês aparece como **não verificado**, não como zero.
Despesas de tributos não devem duplicar deduções já consideradas no resultado.

O OFX aceita depósito correspondente a uma venda ou ao lote completo de vendas da
mesma adquirente e data. Um lote só é sugerido quando existe uma correspondência
inequívoca. A confirmação e sua reversão abrangem o lote inteiro. Arquivo sem
FITID continua visível, mas não é conciliado usando uma identidade inventada.

## Compra, consumo e inventário

- Na entrada de estoque, marcar **Gerar conta da compra** quando houver despesa a
  registrar; informar fornecedor, custo, vencimento e se já foi paga. Entrada e
  conta são gravadas na mesma transação e ficam vinculadas. Se uma falha, ambas
  são desfeitas. Para doação ou conta já registrada, manter a opção desmarcada.
- Informar lote e validade na entrada. Os alertas consideram a quantidade restante
  de cada lote, excluindo lotes já consumidos. A saída permite escolher o lote.
- Consumo não pode gerar saldo negativo nem usar lote vencido. Registrar perda com
  motivo ou selecionar um lote válido. Ajuste de inventário mantém o fluxo próprio.
- Itens movimentados são inativados, não excluídos. Sua unidade não pode ser trocada
  depois de movimentar, pois isso mudaria o significado das quantidades históricas.
- Registrar movimentos em ordem cronológica. Acertos posteriores devem ter a data
  do acerto, para preservar custos já atribuídos a atendimentos anteriores.
- O custo médio considera o saldo e as entradas de cada momento. Custo ausente
  aparece como custo parcial, e a média e a comparação de sessões ficam suspensas;
  não se presume que um insumo sem preço foi gratuito.

## Implantação e dados existentes

A migration `20260922003512_ConsolidacaoGestao` acrescenta o vínculo compra/conta e
os campos de conta bancária, data do extrato e confirmação anterior do depósito.
Não apaga tabelas, movimentos ou valores existentes. A atualização deve abranger
os cinco executáveis: versões antigas não aplicam as novas proteções de gravação.

Antes do uso operacional, validar em cópia do banco: cobranças em aberto, taxas e
prazos reais das maquininhas, saldo físico e lotes, contas a pagar e último caixa
conferido. Guardar backup verificado e atualizar as estações na mesma janela. Esta
entrega prepara código e pacotes; a execução em produção é uma etapa separada.

Limites relevantes para a configuração:

- O modelo atual de cartão prevê um crédito integral por venda no prazo cadastrado.
  Cartão parcelado só representa corretamente contratos com liquidação integral
  nesse prazo. Agenda de depósitos mensais por parcela e antecipações parciais
  exigem uma evolução específica; não se deve tratar o total como disponível no
  primeiro vencimento de um contrato sem antecipação.
- Débito automático em maquininha, webhook Pix, estorno bancário e reembolso não
  são executados pelo registro de pagamento; ele registra a operação confirmada
  pela equipe. Cancelamento de lançamento não transfere dinheiro ao paciente.
- Consumos antigos sem lote são distribuídos por validade e ordem de entrada na
  reconstituição. Conferir o inventário físico antes de usar esse histórico como
  rastreabilidade de lote. Valores antigos sem custo não são completados por chute.
- A conciliação sugere correspondências, sem buscar combinações arbitrárias de
  vendas nem inventar tarifas para fazer o extrato bater. Diferenças ficam pendentes.

## Verificação reproduzível

```powershell
python tokens/verificar-espelho.py
python tools/verificar-suite.py
python tools/compilar-sombra.py
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj -c Release
dotnet build Clinica.sln -c Release
dotnet run --project tools/ValidarLayoutWindows -c Release -- --gestao
```

Para gerar um ZIP local com os cinco módulos e manifesto SHA-256, execute
`powershell -File tools/empacotar-gestao.ps1` com o SDK .NET 8 disponível no PATH.

O workflow Linux executa também a suíte contra PostgreSQL 16 com todas as migrations
em bancos descartáveis. Os testes de concorrência usam duas conexões independentes.
O workflow Windows verifica as telas e gera os cinco pacotes de executáveis.

O repositório `clinica-site` mantém site e portal separados da administração;
consulte seu documento `docs/integracao-gestao.md` para a responsabilidade de cada parte.
