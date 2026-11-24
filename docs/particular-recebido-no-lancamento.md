# Particular recebido no lançamento (nov/2026)

O dono perguntou: *"Por que o sistema não está dando baixa no particular na hora que a secretária lança? No momento do
lançamento já deve ser entendido que entrou dinheiro."* A decisão dele foi esta: **todo atendimento particular
lançado conta como RECEBIDO na hora, sem forma de pagamento**.

## Por que não entrava
Quem lança o particular é quase sempre o médico: pelo Consultório, pelo posto/portal ou pela conclusão automática.
A pergunta "Pago agora / Fica a receber" só existia no fechamento da Recepção, que depois disso não roda. Em produção,
a tabela `Lancamentos` estava vazia.

## A regra (uma só, `ReceitaDoParticularService`)
A regra é chamada em `AgendaService.ConfirmarNucleoAsync`, o núcleo por onde passam TODAS as formas de lançar:
- a presença na Fila;
- o Concluir do Consultório;
- o posto/tablet;
- a conclusão automática;
- a recuperação de guias;
- o registro da Recepção.

A entrada nasce **no mesmo SaveChanges do atendimento**:

| Situação | O que acontece |
|---|---|
| Particular (ficha particular ou sessão marcada particular), com preço | Entrada **Realizada**, `DataPagamento` = dia da sessão, **sem forma**, presa ao atendimento e ao horário |
| Valor | A última entrada recebida do paciente (o combinado); senão a tabela do particular, como no fechamento |
| Sem preço | Nada é gravado (o sistema não inventa valor). A sessão segue na aba **Particulares** da Conciliação ("sem dinheiro registrado") para a recepção completar |
| Paciente com **pacote** de saldo | Nenhuma receita nova |
| Já há entrada no horário ou no atendimento (pago ao marcar, fechamento) | Nada (idempotente) |
| Ficha "a definir" | Não conta como particular |
| Convênio | Nada (o dinheiro entra pela conciliação da guia) |
| Estorno do atendimento | Cancela as entradas presas a ele (`EstornoAtendimentoService`, como já fazia) |

O **fechamento da Recepção** relê a proposta depois do registro e não oferece cobrar de novo. Se alguém ainda
tentar gravar uma segunda entrada, ela é recusada com um aviso: "já entrou no caixa quando foi lançada".

## Recuperação desde 10/10/2026
`ReceitaDoParticularService.RecuperarAsync(desde, ate, aplicar: false)` faz a recuperação.
- **Simulação por padrão:** lista cada sessão particular realizada, não estornada, sem entrada viva e sem pacote, com
  o valor que seria gravado e as sem preço. Usa a mesma definição da aba Particulares.
- **Com `aplicar: true`:** grava as que têm preço num único SaveChanges. Rodar de novo não grava em dobro.

## Banco do posto (`clinica-tablet`)
O posto passa a INSERIR em `Lancamentos`. O `deploy/tablet/atualizar-posto.py` concede:
- `SELECT` e `INSERT` em `"Lancamentos"`;
- `USAGE` e `SELECT` em `SEQUENCE "Lancamentos_Id_seq"`.

## UX · DX · CX
- 🧑‍💻 **UX**
  - Ninguém precisa lembrar de dar baixa: o médico conclui e a sessão já está no caixa.
  - A recepção não é perguntada de novo.
  - A sessão sem preço continua visível na Conciliação, com o motivo.
- 🛠️ **DX**
  - Uma peça nova (`ReceitaDoParticularService`), chamada num único ponto (o núcleo da confirmação).
  - Testes: `ParticularNoLancamentoTests`. Dois testes do `CircuitoParticularTests` foram atualizados para a regra nova.
- 🤝 **CX**
  - O caixa passa a refletir o atendimento do dia.
  - Risco: quem não pagou aparece como pago, por decisão do dono. A correção é no Financeiro (cancelar ou ajustar o lançamento).
  - O faturamento (app compartilhado) também cria a entrada ao lançar particular, que é o pedido ("por qualquer porta").

## O que não foi feito
- **Debitar o pacote no lançamento:** o pacote continua sendo debitado no fechamento da Recepção, como antes. Aqui,
  com pacote, só não nasce receita. O débito automático pede o Id do atendimento depois de salvar, e isso fica para
  uma próxima parcela.
- **Pendência "particular sem preço" nova:** não foi criada, porque a aba Particulares da Conciliação já é essa
  pendência visível. Criar outra seria uma segunda porta para a mesma pergunta.
- **Tela para rodar a recuperação:** fica para o operador. O método existe e foi testado; a aplicação em produção
  espera a conferência da lista.
- **A suíte contra o Postgres** não rodou localmente. A gravação usa colunas existentes, e não há migration.
