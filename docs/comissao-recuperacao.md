# Comissão de recuperação de pacientes (out/2026)

> Pedido da clínica: *"Atendente assume paciente que sumiu > paciente retorna > paciente conclui
> atendimento com médico/psicólogo/psiquiatra > gera guia > atendente recebe comissão. Só recebe
> comissão após o atendimento ser concluído o fluxo completo, e é preciso termos um relatório para
> recuperação de pacientes como se fosse as comissões dos médicos, assim conseguiremos calcular e
> validar."* E: *"o fluxo no acompanhamento, tanto para a recepção quanto para o faturamento, mas
> os valores e a aba de verificação devem ficar no módulo gerente."*

## 1. O fluxo, e onde cada passo mora

| Passo | Onde | Quem |
|---|---|---|
| O paciente some (recall) ou é indicado (BSV) | fila do **Acompanhamento** (Relacionamento) | o sistema / o médico |
| **Assume** o caso | a mesma fila — botão "Assumir" na linha | recepção **ou** faturamento |
| Fala com o paciente (WhatsApp, telefone) | a conversa do paciente, na fila | quem assumiu |
| O paciente volta e **conclui** a sessão | agenda → atendimento realizado | balcão / profissional |
| A **guia** nasce | motor de regras (`AtendimentoService`) | automático |
| **Valida** a comissão (valor copiado) | Gerente → *Campanhas, origem e comissão* → **Comissão de recuperação** | gestão (`EditarFinanceiro`) |

Quem atende **não vê valor nenhum**: a fila do Acompanhamento não mostra dinheiro. O valor e a
validação ficam na gestão.

## 2. O que conta como recuperação (a regra, em `ComissaoRecuperacaoService.ApurarAsync`)

- A sessão de retorno é a **primeira sessão realizada** da modalidade do caso depois da abertura
  do ciclo (novo recall ou indicação). A segunda já é tratamento, não recuperação.
- Alguém **assumiu** o caso antes dessa sessão: um registro feito pela própria atendente
  (assumir ou registrar contato). O responsável **padrão** que a configuração atribui na
  indicação de BSV **não conta** — isso é a fila distribuindo, não a atendente assumindo.
- Recebe quem assumiu **por último** antes do retorno. Quem assumiu e não registrou contato
  aparece marcado ("só assumiu, sem contato") — a gestão decide.
- Sessão **estornada** sem decisão sai da verificação; validada e estornada depois aparece em
  vermelho ("sessão estornada depois") para a gestão desfazer.
- Particular (sem guia) aparece marcado "particular, sem guia" — a gestão decide se paga.

## 3. Valor, validação e o que fica gravado

- **Valor** (`RegraComissaoRecuperacao`): geral, por fila (recall / novos BSV) e por modalidade,
  com **vigência**. Vale o mais específico vigente na **data do retorno**. Reajuste é valor novo.
- **Validar** copia o valor e a descrição da regra (`ComissaoRecuperacao`), com quem validou e
  quando, e grava a trilha no mesmo `SaveChanges`. **Quem recebe não valida a própria comissão.**
- **Recusar** exige justificativa. **Desfazer** (validação ou recusa) exige motivo, não apaga — a
  linha fica marcada como cancelada e a recuperação volta à verificação.
- Uma decisão viva por sessão de retorno (índice único filtrado em `CanceladaEm IS NULL`).
- A tela **calcula e valida**; não lança o pagamento no caixa (decisão: o pagamento da comissão é
  combinado à parte). Se um dia for pedido, o caminho é o do repasse (saída prevista no caixa).

## 4. Instalação no servidor

A migration roda na abertura do app. A chave de `ComissoesRecuperacao` para `Acompanhamentos`
só nasce onde o papel do desktop pode referenciar a tabela — em produção ela foi criada pelo
script do posto e o desktop não pode, então a chave fica para o dono: rode
`deploy/tablet/comissao-recuperacao-chave.sql` como `postgres` **depois** de o app ter aberto
uma vez com esta versão (é idempotente). Sem ela tudo funciona; ela só impede, no banco, uma
comissão apontando para um acompanhamento inexistente — e acompanhamento não se apaga.

## 5. Comparação das opções

| Opção | 🧑‍💻 UX | 🛠️ DX | 🤝 CX |
|---|---|---|---|
| Marcar "recuperado" à mão na fila | um clique a mais por paciente, e a pessoa marca o próprio crédito | um campo novo para manter em sincronia com a agenda | comissão sem prova: ninguém confere |
| **Apurar dos fatos que já existem** (escolhida) | a atendente só assume e fala — nada a mais; a gestão vê tudo pronto para conferir | uma leitura (casos + contatos + sessão + guia); nada é marcado no caminho, então não sai de sincronia | o número vem com a conta (quem, quando, qual sessão, qual regra) — dá para validar e discutir |
| Pagar automático ao gerar a guia | zero trabalho | simples | paga o que ninguém conferiu: a sessão pode ser estornada, a volta pode não ter sido por contato |

## 6. 🧑‍💻 UX · 🛠️ DX · 🤝 CX

- 🧑‍💻 **UX** — para quem atende, nada muda além do botão "Assumir" na linha (desenho A do
  Relacionamento). Para a gestão, uma tela com três abas — *Verificação* (o que falta conferir,
  com validar em lote), *Por atendente* (a folha do mês) e *Valores* —, os quatro números no topo
  e o CSV do mês com a conta de cada linha.
- 🛠️ **DX** — duas tabelas novas (migration aditiva gerada pelo `dotnet ef`), um serviço
  (`ComissaoRecuperacaoService`) e as regras puras em `ComissaoRecuperacaoRegras`, com teste. A
  tela é Blazor, achada sozinha pela prévia e pelos testes que abrem a tela e clicam cada botão.
  Testes: `ComissaoRecuperacaoTests` (o fluxo real — fila, lançamento pelo motor, apuração,
  validação — inclusive contra Postgres) e `ComissaoRecuperacaoBlazorTests` (clica Validar e
  confere o banco).
- 🤝 **CX** — a clínica passa a medir e pagar a recuperação com prova; o valor validado não muda
  com reajuste; nada é apagado (a trilha responde quem validou, recusou ou desfez). Custo: nenhuma
  máquina nova, nenhuma integração; uma migration aditiva aplicada na abertura.
