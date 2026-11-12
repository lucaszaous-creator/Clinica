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
| Conselho, número, UF, CBO | Campo a campo, nesta ordem: 1º o **cadastro do profissional** ("Registro para guias de convênio"); 2º a última guia conferida dele; 3º o registro escrito em texto livre ("CRM-RJ 123456"), que nunca dá o CBO | Em branco, com aviso que diz onde cadastrar |
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
| Baixa em lote | A linha não tem campo de número: tem o botão **Abrir a guia**, que abre este mesmo formulário por cima do lote. Baixada ali, a linha fica marcada "Baixa registrada" e o que estava digitado nas outras linhas continua |
| Rodar pendências — painel | O botão **Abrir a guia** no lugar do número; a justificativa continua ao lado. Baixada pela guia, a linha fica marcada e sai da conta; fechar a rodada sem concluir não aplica o que ficou digitado |
| Rodar pendências — janela bloqueante da abertura (WPF) | O número digitado nessa linha é recusado antes de gravar. **A decisão dela é a justificativa** (decisão da direção: "avisa sobre a pendência Petrobras com justificativa"): a rodada bloqueante continua cobrando, a linha diz onde a baixa é dada, e a justificativa é gravada como **anotação da pendência** — não como não conformidade —, então a guia continua no painel. A justificativa já anotada volta preenchida na rodada seguinte, e confirmá-la sem mudar não regrava nada |
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
2. ~~Conselho, número, UF e CBO no cadastro da equipe~~ — feito (seção "Cadastro do profissional e
   Abrir a guia", abaixo). Falta a clínica **preencher** o cadastro de quem atende convênio.
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

## Cadastro do profissional e "Abrir a guia" (11/11/2026, segunda entrega)

**Decisão da direção, pela própria tela do portal:** conselho, número, UF e CBO são dados do
**profissional**, não de cada guia. E o pedido de testar tudo de uma vez: o lote e a rodada do
painel deixam de mandar a pessoa sair dali para dar baixa numa guia da Saúde Petrobras.

### O que mudou

- **Profissionais e salas → editar o profissional** ganhou a seção **Registro para guias de convênio
  (padrão ANS)**: Conselho e UF em lista (a tela mostra a sigla — CRM, RJ — e grava o código do
  padrão — 06, 33), Número em texto e CBO em lista por ordem de nome. Os quatro são **opcionais**.
- O **"Conselho e registro" em texto livre continua**: é o que sai escrito nos documentos clínicos.
  Nenhum dos dois é derivado do outro.
- Com o texto livre preenchido e os campos novos em branco, o formulário **propõe** o que leu
  ("Li de CRM-RJ 77.2468: conselho CRM, número 772468 e UF RJ") com o botão **Usar estes valores**.
  Não preenche sozinho, e só grava no Salvar.
- A guia da baixa busca o executante **campo a campo**: cadastro → última guia conferida → texto
  livre. Com o cadastro completo não há aviso; faltando algo, o aviso diz o que falta, de onde veio
  o substituto e onde cadastrar de vez.
- **Baixa em lote** e **Rodar pendências** (do painel): a linha da Saúde Petrobras tem o botão
  **Abrir a guia**. A janela bloqueante da abertura do app não mudou.

### Opções comparadas

| | A — campos no profissional + botão na linha (feito) | B — só a última guia como memória (como estava) | C — derivar tudo do texto livre |
|---|---|---|---|
| 🧑‍💻 UX | cadastra uma vez; a guia abre pronta; lote e rodada resolvem a Petrobras sem sair | a primeira guia de cada profissional é digitada; quem erra ali repete o erro nas seguintes | nada a cadastrar, mas o CBO nunca vem e "CREFITO-2 12345-F" se lê errado |
| 🛠️ DX | 4 colunas aditivas, uma regra pura com teste, o mesmo `BaixaGuiaForm` reaproveitado | nenhuma peça nova | um parser que cresce a cada formato de conselho |
| 🤝 CX | o dado fica onde a direção disse que ele mora, e é auditável na ficha | o dado mora espalhado nas guias | guia errada na operadora por causa de um texto que ninguém digitou para isso |

### 🧑‍💻 UX

- Quatro campos, uma seção, nenhum obrigatório: o cadastro de quem não atende convênio não muda.
- A proposta é uma frase com um botão, e some depois de aceita. Campo que aparecesse preenchido sem
  ninguém digitar seria campo que ninguém confere.
- O número é criticado a cada tecla ("só letras e números"); ponto e traço são aceitos na digitação
  e saem ao gravar, que é como a operadora o recebe.
- No lote e na rodada o botão ocupa o lugar do campo de número, na mesma coluna. A guia abre por
  cima; ao confirmar, a linha vira "Baixa registrada" e o que estava digitado nas outras linhas
  fica. O painel de trás relê na hora, então fechar depois é só fechar.
- O CBO é uma lista de 169 itens em ordem de nome (lista simples, como no formulário da guia): com
  ela aberta, digitar o começo do nome chega no item. A casa não tem lista com busca para domínios.

### 🛠️ DX

- `Profissional.ConselhoProfissional`, `NumeroConselho`, `UfConselho`, `Cbos` (códigos do
  `DominiosTiss`, com o teto do XSD no `ClinicaDbContext`). **A migration é uma só, gerada na
  integração** — esta entrega não tem migration.
- `RegistroDoProfissionalParaGuias` (Application, puro): o que é válido, a proposta do texto livre,
  as listas ordenadas e `ParaAGuia`, que decide a fonte de cada campo e escreve o aviso.
- `EquipeService.SalvarProfissionalAsync`: **nulo = "não sei", preserva; vazio = apaga** — a mesma
  convenção dos convênios não atendidos. A tela de Acessos e todo chamador que não conhece os
  campos continuam sem apagá-los.
- `LinhaDaRodada.BaixadaNoFormulario` tira a linha de toda conta (`SemDecisao`, `AplicarAsync`,
  `ImpedimentoDaGuiaAns`); `DecisoesDaRodada.Montar(itens, abreAGuia: true)` só muda a frase da
  linha nas telas que têm o botão. A janela WPF chama `Montar(itens)` e fica como estava.
- Testes: `RegistroParaGuiasTests` (regra e serviço), `GuiaAnsNaBaixaTests` (as três fontes e a
  mistura, no banco) e `RegistroParaGuiasBlazorTests` (formulário, lote, rodada e a recusa do botão).

### 🤝 CX

- A clínica precisa **preencher o cadastro** de quem atende convênio: sem ele a guia continua
  funcionando pelas fontes antigas, com o aviso apontando para a tela.
- Nenhum profissional é bloqueado, nenhuma guia já conferida muda, e nada é enviado à operadora.
- Risco em produção: quatro colunas novas anuláveis; o faturamento em produção não as lê.

### O que não foi verificado nesta entrega

- Teclado (Tab, Ctrl+S com a guia aberta por cima do lote) e o app WPF de verdade.
- A suíte contra o Postgres (a migration ainda não existe; os testes rodaram em SQLite).
- `GuiaAnsNaBaixa.LerRegistroConselho` lê "CREFITO-2 12345-F" com o número "2" (a região) — é o
  comportamento fixado no teste dele. A proposta mostra o que leu e a pessoa corrige antes de
  aceitar; o leitor não foi alterado nesta entrega.
