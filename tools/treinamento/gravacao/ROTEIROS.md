# Como escrever o roteiro de uma videoaula

Cada tela dos cinco apps tem UMA videoaula, e o roteiro dela é `roteiros/<chave>.json`. O
GitHub abre a tela de verdade na prévia (moldura real, dados fictícios das sementes), executa
cada passo e o Remotion monta por cima o motion design: câmera que vai até a função,
destaque, cursor, painel com a função da vez e o índice da aula. **A legenda é quem ensina**
— não há narração.

## O que a aula precisa cobrir

**TODAS as funções da tela**, uma por passo com título: cada botão, aba interna, filtro,
recorte, campo de busca, ação de linha, menu "⋯", formulário que a tela abre, cartões de
número com o que eles contam, avisos e estados que a tela mostra. Quem assiste tem de sair
sabendo **para que serve** cada coisa e **o que fazer** com ela.

**Demonstre, não só aponte.** Sempre que for seguro, faça o gesto de verdade: clique no
filtro e mostre o resultado, abra o formulário e preencha dois ou três campos com dados
fictícios plausíveis, abra o menu "⋯" e mostre os itens, digite na busca. Depois desfaça
(feche o formulário, limpe a busca, volte o filtro para "Todos") num passo sem título.

Uma aula tem tipicamente 10 a 20 funções e dura de 2 a 4 minutos.

## O formato

```json
{
  "chave": "caixa",
  "titulo": "Caixa do mês",
  "descricao": "Uma frase: a pergunta que esta tela responde.",
  "url": { "paciente": 1, "secao": "Atendimento", "subaba": 0, "agendamento": 12 },
  "antes": [ { "alvo": "...", "acao": "clicar" } ],
  "inventario": [ { "alvo": "...", "acao": "clicar" } ],
  "passos": [
    { "titulo": "Lance uma entrada ou saída", "texto": "Novo lançamento registra um recebimento ou uma despesa: valor, data, categoria e forma de pagamento.", "alvo": "botao=Novo lançamento", "acao": "clicar" },
    { "titulo": "Preencha o lançamento", "texto": "Escolha se é entrada ou saída, o valor e a data. Pode entrar como previsto ou já realizado.", "alvo": "janela-campo=Valor", "acao": "digitar", "valor": "250,00" },
    { "alvo": "janela=Fechar", "acao": "clicar" }
  ]
}
```

- `chave`: a chave da tela (o nome do arquivo). Não muda.
- `titulo`: o nome da tela como a pessoa a conhece. `descricao`: uma frase, até ~160 caracteres.
- `url`: só para a tela do paciente (chaves `consultorio-paciente`, `consultorio-atendimento`
  etc.): o paciente (id das sementes) e, se preciso, a seção.
- `antes`: passos de preparação, executados SEM filmar, antes da aula começar (ex.: a ficha do
  balcão precisa de um paciente aberto).
- `inventario`: passos que só o inventário executa, para fotografar a tela num estado que o
  vídeo mostra no meio (ex.: depois de escolher o paciente).
- `passos`: a aula. Com `titulo` é uma **função** (vira capítulo e aparece no painel); sem
  `titulo` é um gesto de apoio (fechar, limpar, voltar o filtro).

### Cada passo

- `titulo`: imperativo e curto, até ~38 caracteres ("Registre a chegada", "Filtre pela
  situação", "Emita o recibo").
- `texto`: 1 ou 2 frases, até ~200 caracteres: **o que a função faz e o que a pessoa deve
  fazer / quando usar**. Português da clínica, sem jargão técnico. Nada de "clique aqui" sem
  dizer o efeito.
- `alvo`: o elemento (abaixo). Sem alvo, o passo só mostra a legenda sobre a tela inteira.
- `acao`: `mostrar` (padrão com alvo), `clicar`, `digitar` (com `valor`), `limpar`,
  `selecionar` (com `valor` = o texto da opção), `marcar` (caixa de seleção), `passar`
  (hover), `tecla` (com `valor`, ex. `Escape`), `rolar`, `esperar` (com `ms`).
- `ms` (opcional): tempo na tela; sem ele, o tempo de leitura é calculado pelo tamanho do texto.
- `depois` (opcional): espera depois do gesto, em ms (o padrão já dá tempo de ver o resultado).

### Os alvos (sempre pelo que a pessoa LÊ na tela)

| alvo | acha |
|---|---|
| `botao=Texto` / `botao-exato=Texto` | botão pelo nome acessível (texto ou `aria-label`); o primeiro que CONTÉM o texto / o exato |
| `aba=Texto` | aba interna da tela (`role=tab`) |
| `entrada=Texto` | campo pelo `aria-label`/rótulo OU pelo placeholder |
| `campo=Texto` / `placeholder=Texto` | campo só pelo rótulo / só pelo placeholder |
| `opcao=Nome` | linha de escolha (`role=option`) — ex.: o paciente na busca |
| `caixa=Texto` | caixa de seleção |
| `texto=Trecho` / `texto-exato=Texto` | um texto qualquer da tela (para destacar cartões, títulos de seção, avisos) |
| `titulo=Texto` | um título (h1–h4) |
| `linha=Trecho` | a linha de uma tabela/lista que contém o trecho |
| `item=Texto` | item de menu aberto (`role=menuitem`), ex. no "⋯" |
| `janela=Texto` / `janela-exato=Texto` | botão dentro do formulário/diálogo aberto (ex. `janela=Fechar`, `janela=Cancelar`) |
| `janela-campo=Texto` / `janela-texto=Trecho` / `janela-opcao=Nome` / `janela-caixa=Texto` | campo, texto, opção ou caixa dentro do formulário aberto |
| `janela-toda=` | o formulário/diálogo inteiro (para destacá-lo) |
| `aba-app=Texto` | aba da moldura (as abas do item composto, no topo) |
| `css=seletor` | último recurso, dentro da tela |

Tudo é procurado DENTRO da tela da aula (o palco), menos `item`, `aba-app`, `menu` e `janela*`.

**O nome tem de existir.** Tire-o do inventário (`<chave>.json`: `controles[].nome`,
`aria`, `placeholder`, `titulos`, `texto`) ou do `.razor` da tela (`aria-label`, texto do
botão, `title`). Roteiro com alvo que não existe REPROVA a aula na gravação.

## Cuidados

- Formulários (`Modal`) fecham por `janela=Fechar` (o "×" do topo); diálogos de confirmação
  têm `janela=Cancelar`. Para ação destrutiva (cancelar horário, excluir), MOSTRE o diálogo e
  saia por Cancelar — a legenda explica o que aconteceria.
- Não clique em nada que leve a OUTRA tela no meio da aula; se a função é navegar ("Abrir a
  ficha", "Marcar atendimento"), deixe-a para o último passo ou só a mostre.
- Não escreva frases que dependam da hora ("Boa tarde"), nem números que mudam com o dia.
- Cada aula roda numa prévia nova: o que um passo muda (uma chegada, um lançamento) vale
  para os passos seguintes da mesma aula, e só para ela.
- Dados das sementes (fictícios): pacientes como "Rosana Figueiredo Lemos", "Otávio Rezende
  Campos", "Ana Beatriz Souza" (id 1), profissionais "Dra. Helena", "Dr. Marcos", "Bianca".
