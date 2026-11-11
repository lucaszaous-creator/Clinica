# Baixa de guia da Saúde Petrobras com a guia ANS

**Pedido da direção (11/11/2026):** "Toda baixa de guia Petrobras abre o formulário com as
informações igual à que eu te enviei, as informações que a Petrobras solicita" — a tela "Guia de
Consulta" do Portal TISS da operadora. "Queremos a nossa guia padrão ANS." Depois, teste interno.

## O que mudou para quem dá baixa

Ao clicar em **Dar baixa** em **qualquer guia** de paciente da Saúde Petrobras — consulta, consulta
de especialidade e BSV —, o formulário de baixa abre com a guia de consulta no padrão ANS: os mesmos quatro blocos do portal, na mesma ordem e com
a mesma numeração de campo.

| Bloco | Campos |
|---|---|
| Dados do beneficiário | 1 Registro ANS · 4 Número da carteira · 5 Validade · 7 Nome · 6 Atendimento a RN · 26 Nome social |
| Dados do contratado | 9 Código na operadora (CNPJ) · 10 Nome do contratado · 11 CNES |
| Dados do profissional executante | 12 Nome · 13 Conselho · 14 Número no conselho · 15 UF · 16 CBO |
| Dados do atendimento / procedimento | 17 Indicação de acidente · 27 Cobertura especial · 28 Regime · 29 Saúde ocupacional · 18 Data · 20 Tipo de consulta · 19 Tabela · 21 Código do procedimento · 22 Valor · 23 Observação |
| Baixa no sistema (como já era) | Número da guia gerada · Data da baixa · Observação |

Os campos já vêm preenchidos com o que o sistema sabe:

| Campo | De onde vem | Se faltar |
|---|---|---|
| Carteira, validade, nome | Ficha do paciente | Fica em branco, com aviso |
| Registro ANS | Cadastro do convênio (ou Configurações do faturamento) | Em branco, com aviso |
| Código na operadora, nome do contratado, CNES | Configurações do faturamento (CNPJ só com dígitos; CNES 9999999 quando não há) | Em branco, com aviso |
| Profissional executante | O profissional do horário do atendimento | Em branco, com aviso |
| Conselho, número, UF, CBO | A última guia conferida do mesmo profissional; na primeira vez, o registro escrito no cadastro da equipe ("CRM-RJ 123456") — o CBO é escolhido uma vez | Em branco |
| Acidente, regime, tabela | Padrões da Petrobras: 9 Não acidentes, 01 Ambulatorial, 22 TUSS | — |
| Tipo de consulta | 1 Primeira; 2 Seguimento quando há consulta anterior pelo convênio (com a data no aviso) | — |
| Código do procedimento | Códigos TUSS de Configurações do faturamento | Em branco, com aviso |
| Valor | Tabela de preços do convênio | Em branco, com aviso |

Nada é palpite: o que o sistema não sabe fica **em branco** e aparece num aviso no topo. O botão
**Confirmar baixa** só acende com a guia completa; o que falta fica marcado em vermelho no campo.

## O que a baixa NÃO faz (para ninguém entender errado)

- **Não envia a guia à operadora.** A baixa continua sendo o registro do número que o Portal TISS
  gerou. Nenhum texto da tela diz "enviado". O envio pelo web service é passo posterior
  (`docs/tiss-webservice-petrobras.md`).
- **O BSV usa a mesma guia.** A direção informou (11/11/2026) que o BSV é lançado no portal na
  mesma tela "Guia de Consulta": o que muda é o bloco do atendimento (tabela, código do
  procedimento, valor). Por isso a baixa do BSV abre o mesmo formulário, com o código e o valor
  do BSV vindos de Configurações do faturamento e da tabela de preços. No XML do lote, a guia
  conferida sai como guia de consulta, uma por código — e não mais dentro de uma SP/SADT.
- **Não vale para sessão particular** de paciente Petrobras: ela não vai à operadora.

## As outras portas de baixa

A baixa tem três caminhos até `FaturamentoService.DarBaixaAsync`. Só um tem formulário por guia.

| Porta | Consulta da Petrobras |
|---|---|
| Dar baixa (painel de pendências) | Abre a guia ANS |
| Baixa em lote | A linha diz "baixa só pelo Dar baixa do painel (guia ANS)". Com número digitado nela, o lote **não é processado** e diz quais linhas limpar — nenhuma baixa é feita |
| Rodar pendências (painel e a janela bloqueante da abertura) | O número digitado nessa linha é recusado antes de gravar. **A decisão dela é a justificativa** (decisão da direção: "avisa sobre a pendência Petrobras com justificativa"): a rodada bloqueante continua cobrando, a linha diz onde a baixa é dada, e a justificativa é gravada como **anotação da pendência** — não como não conformidade —, então a guia continua no painel. A justificativa já anotada volta preenchida na rodada seguinte, e confirmá-la sem mudar não regrava nada |
| Rede final | Se algo escapar, o serviço recusa a guia com a frase que diz onde resolver, e as outras linhas seguem |

## As opções consideradas

A direção decidiu que a guia aparece **sempre**. O que restava escolher era onde.

| | A. Seção dentro do formulário de baixa (**esta**) | B. Segundo formulário antes da baixa | C. Montar a guia em silêncio |
|---|---|---|---|
| 🧑‍💻 UX | Uma janela, um Confirmar; a pessoa vê a guia e o número juntos, como no portal | Duas janelas e dois cliques a mais por guia; dá para fechar a segunda e ficar com a guia sem baixa | Nenhum campo a mais, mas ninguém confere o que vai gravado |
| 🛠️ DX | Um formulário, uma gravação, uma transação | Estado entre dois formulários; guia órfã se a baixa falhar | Menos tela, mesma regra |
| 🤝 CX | A guia gravada é a que alguém conferiu contra o portal | Idem, com mais atrito no dia | Descartada pela direção: a guia sairia com dado que ninguém viu |

Onde os dados moram:

| | Tabela própria 1:1 (**esta**) | Colunas novas na guia | JSON |
|---|---|---|---|
| 🧑‍💻 UX | Nenhuma diferença na tela | Idem | Idem |
| 🛠️ DX | ~25 colunas que só a Petrobras usa ficam fora de toda leitura de pendências; migration só cria tabela | 25 colunas nulas em toda guia de todo convênio | Sem tipo e sem teto por campo; o XSD tem teto por campo |
| 🤝 CX | Migration aditiva; o faturamento que roda não muda de forma | Idem, com tabela mais larga | Difícil auditar |

## 🧑‍💻 UX

- **Cliques:** os mesmos de hoje (Dar baixa → Confirmar baixa → confirmar). Campos a digitar: na
  primeira guia de cada profissional, o CBO (e conferir conselho, número e UF); nas seguintes,
  só o número da guia, como hoje — desde que os cadastros estejam preenchidos.
- **Espera:** uma leitura a mais na abertura (a proposta), antes de o formulário aparecer; ele
  não pisca da baixa comum para a baixa com guia.
- **Quando falha:** falta de dado é aviso no topo e campo em vermelho, nunca erro de sistema; o
  botão apagado diz por quê; a baixa nunca é gravada pela metade.
- **Regras de tela:** uma porta por pergunta (a guia mora na baixa, não ganhou tela nem menu);
  responsivo (três colunas que viram uma abaixo de 640 px); sem botão para função que não existe.
- **Design system:** componentes da casa (`Modal`, `Campo`, `Aviso`, `secao-form`); cor, raio e
  espaço só de token, na grade de 8 (24 entre blocos, 16 entre campos, 12 dentro do bloco).

## 🛠️ DX

- **Onde a regra mora** (Application, com teste): `GuiaAnsNaBaixa` (quem exige, validação,
  gravação, leitura do registro em texto livre), `GuiaAnsNaBaixaService` (a proposta),
  `GuiaAnsEmEdicao` (o que a tela edita, e a leitura do valor). O Razor só desenha.
- **Domínios:** `DominiosTiss` (Domain) tem as listas do XSD 4.01.00. `GuiaAnsNaBaixaTests`
  confere cada uma contra a enumeração do XSD: lista escrita de memória reprova.
- **Armadilhas do padrão que viraram teste:** UF é código IBGE ("33"), conselho tem zero ("06"),
  valor tem ponto ("139.00"), tag opcional vazia é omitida, o contratado leva uma identificação
  só. E "139.00" digitado não vira 13.900.
- **XML do lote:** guia com conferência sai completa e **válida no XSD oficial** (teste); guia sem
  conferência sai exatamente como saía. `TissExportService` não mudou para os outros convênios.
- **Banco:** tabela nova `GuiasTissConsulta`, chave = a guia do sistema; migration
  `20261111193730_GuiaTissConsultaNaBaixa`, só `CreateTable` e `CreateIndex`.
- **Estorno:** preserva a guia conferida; a baixa seguinte parte dela e a substitui.
- **Para estender a outro convênio:** `GuiaAnsNaBaixa.Exige` é o único lugar que
  decide; as portas em massa leem `LinhaDaRodada.PedeGuiaAns`.

## 🤝 CX

- **O que muda amanhã de manhã:** a baixa de consulta da Petrobras ganha a guia; a baixa em lote e
  a rodada deixam de aceitar número nessas guias. Os outros convênios não mudam em nada.
- **Risco em produção:** a recusa nova chega às telas de baixa (é o objetivo). A rodada bloqueante
  tem sempre saída (a justificativa) e não fabrica não conformidade. A janela WPF da rodada não
  foi alterada: o texto novo e a justificativa preenchida vêm da Application, que ela já lê —
  **mas ela não foi aberta para conferir que mostra a justificativa preenchida**.
- **Dado pessoal:** a guia gravada repete dados que o sistema já tem (carteira, nome, data) e
  acrescenta dados do profissional (conselho, CBO) e o valor da guia. Não leva dado clínico (nem
  CID, nem evolução). Nada sai do banco da clínica nesta etapa.
- **Dinheiro:** o valor gravado é o valor **faturado na guia**, como consta no portal. O sistema
  continua sem controlar recebimento.
- **Custo:** nenhum serviço novo.

## O que falta (em ordem)

1. **Teste interno pela clínica** com guias reais, comparando campo a campo com o portal.
2. Conselho, número, UF e CBO no **cadastro da equipe** (hoje vêm da última guia do profissional).
3. Conferir com a clínica o **código e a tabela do BSV** usados no portal, e cadastrá-los em
   Configurações do faturamento para o formulário já abrir preenchido.
4. Envio por web service (`tissLoteGuias`), depois de uma guia de teste aceita.

## O que não foi verificado

- O formulário foi aberto na prévia (`tools/PreviaBlazor`, dados fictícios) num navegador em 1366 de
  largura, com a guia da paciente Petrobras da semente: os blocos, os avisos, os campos em vermelho e
  o botão apagado foram vistos. **Não foi visto em 1024**, nem com o `foto.js` (pede Node, que não
  havia na máquina), nem o teclado (Tab, Ctrl+S), nem o app WPF de verdade.
- A suíte contra o Postgres não rodou; a migration foi gerada pelo `dotnet ef` e só cria tabela.
- A janela WPF da rodada não foi compilada (Windows); ela não foi editada.
- Se os valores que a clínica escolhe no portal coincidem sempre com os padrões propostos.
