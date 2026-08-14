# O que os erros desta rodada ensinam

> Retrospectiva das parcelas **66 (5ª rodada)** e **67** — a tela do paciente e os termos do
> BSV. Só os **erros**: o que quebrou, quem pegou, e o que fazer diferente da próxima vez.
>
> O `CLAUDE.md` guarda as lições em ordem cronológica, uma entrada por parcela. Este
> documento é o corte transversal: **por FAMÍLIA**, porque a mesma família volta com roupa
> diferente, e é olhando o conjunto que se percebe qual pergunta faltou.

## O placar, e o que ele diz

| Quem achou | O quê | O que isso significa |
|---|---|---|
| As três redes locais | ~9 deslizes de código | Chave de design system inventada, janela mais alta que o monitor do balcão, `FontSize` numérico, membro não estático, enum inexistente no teste. Funcionaram — é o caso normal e não é notícia |
| O CI (compilador de marcação) | 1 | Sete minutos por um `;assembly=` que faltava |
| **A revisão adversarial do próprio diff** | **8 confirmados** | Com build verde, **1546 testes verdes** e as três redes verdes |
| O cliente, em produção | 1 | O PDF saindo em duas páginas |

⚠️ **A linha que importa é a terceira.** O diff estava pronto para o merge — CI verde,
testes verdes, redes verdes — e tinha oito defeitos reais, **três deles dentro da ferramenta
que existe para achar defeito**. Nenhum quebrava nada. Verde não é sinônimo de correto; é
sinônimo de "nada que sabemos conferir reclamou".

---

## Família 1 — A rede que existe para pegar defeito é onde o defeito mais se esconde

Três dos oito achados estavam no `verificar-suite.py`, e um deles estava lá **desde que a
checagem nasceu**.

**Os casos:**

- **A dispensa da checagem 18 valia para o ARQUIVO INTEIRO.** Um `DropIndex` inofensivo
  declarado dava passe livre a um `DropColumn` acrescentado depois na mesma migration — e a
  ferramenta ainda imprimia, como justificativa dele, a frase que falava do índice e afirmava
  *"nenhuma linha se perde"*.
- **`AlterColumn` nunca disparou a checagem 18.** A busca era `.AlterColumn(` e o EF gera
  `migrationBuilder.AlterColumn<string>(`. A operação **mais** destrutiva da lista era letra
  morta desde o primeiro dia.
- **O autoteste da saída consciente reimplementava a lógica** em vez de chamar a função da
  checagem — ficaria verde exatamente quando ela quebrasse.
- (E, no começo da rodada) **a checagem 33 cobria um só sentido de um erro simétrico**: ela
  nasceu pegando o `;assembly=` que SOBRA e não via o que FALTA. O CI reprovou o PR.

**Por que nenhuma rede pegou:** a rede não se examina. As três redes locais examinam o
código do produto; nada examina as redes.

**A regra que sai daí — quatro perguntas para toda checagem nova:**

1. **O autoteste CHAMA o código que roda?** Autoteste que reimplementa a lógica é verde
   justamente quando a checagem quebra — a cópia dentro dele não quebra junto. É o defeito
   recorrente do projeto aplicado à ferramenta de achá-lo.
2. **Como a ferramenta que gera o código escreve isto de verdade?** Não como você
   escreveria. O EF emite genéricos; procurar `Op(` sem `Op<` é procurar o que nunca existe.
3. **Este erro tem dois sentidos?** Se tem, cubra os dois no mesmo commit. O sentido que
   ficar de fora é o que a próxima pessoa vai cometer.
4. **O que esta checagem NÃO vê?** Meça o ruído antes de decidir não alargá-la — em duas
   ocasiões o alargamento custou zero falso positivo e revelou o próprio defeito.

---

## Família 2 — Exceção declarada delimita, ou dispensa o vizinho

A saída consciente da checagem 18 nasceu **certa na intenção e errada no alcance**: a marca
dizia "esta migration tem uma razão", e não "esta OPERAÇÃO tem uma razão".

O caminho de dano é o realista: **a migration marcada é justamente a que a próxima pessoa
copia como modelo.** Ela copia a marca junto, acrescenta a operação dela, e a ferramenta
carimba com uma justificativa escrita sobre outra coisa.

> **Garantia falsa no log do CI é pior do que checagem nenhuma.** Sem a checagem, alguém
> confere; com a garantia falsa, ninguém confere.

**A regra:** exceção declarada **nomeia o que dispensa**
(`MIGRATION-NAO-ADITIVA-CONSCIENTE(DropIndex): razão`), e o que não estiver na lista continua
erro. E a razão **é** a exceção, não um interruptor: marca sem razão escrita não vale.

Corolário que já valia e ganhou um caso: **exceção nunca fica silenciosa.** Ela vira aviso em
toda execução, inclusive no CI — exceção que some da saída é exceção que ninguém revisa.

---

## Família 3 — Âncora em campo EDITÁVEL não é âncora

O botão que cria os termos do BSV guardava-se contra o segundo clique comparando o **nome do
modelo**. E o nome é justamente o que o desenho **manda mudar**: a marca `(rascunho —
revisar)` mora nele para ser apagada quando o responsável técnico aprovar o texto.

Renomeado, o segundo clique criava outro par de rascunhos e ligava mais quatro exigências —
que a chave alargada da mesma parcela deixa **conviver**. O BSV passaria a cobrar quatro
papéis, dois deles não revisados, e assinar um par não zeraria o outro.

**A pergunta certa não era "este texto já existe?", era "o BSV já está configurado?"** — a
guarda passou a olhar a **exigência**, que é o fato, não o rótulo.

**A regra:** idempotência se ancora no que o sistema **não deixa o usuário editar**. Se a
única âncora disponível é editável, a pergunta está errada — procure o fato que a operação
cria, não o texto que ela escreve.

---

## Família 4 — "Vazio" não é nulo, e `??` não dispara para string vazia

A lista do Gerente escrevia **"Acupuntura + eletroacupuntura"** em toda exigência de família.

A cadeia inteira, porque ela é instrutiva:

```
Nome(codigo ?? familia.ToString())
  → NormalizarCodigo grava STRING VAZIA, nunca null   (NULL não é único no PostgreSQL)
  → o ?? não dispara
  → o catálogo não acha ""
  → Enum.TryParse("") falha
  → cai no literal de fallback: AcupunturaComEletro
```

Cada passo é razoável sozinho. O defeito mora nas junções, e **nenhuma delas erra alto**: não
há exceção, não há log, não há teste vermelho — há um nome errado na tela.

**A regra:** quando um campo tem "ausente" e "vazio" como estados distintos, `??` cobre um só.
E quando existe um par código+família, o ponto único é `Nome(codigo, familia)` — o código
vence, a família é o caminho de baixo. O projeto já tinha esse desenho no convênio
(`CatalogoConvenios.Nome`); a modalidade não o tinha, e cada tela improvisava.

---

## Família 5 — Um lado no plural, o outro no singular

`ExigenciaTermoProcedimento` tinha chave única `(Modalidade, ModalidadeCodigo)`: **uma**
exigência por procedimento. Só que o `Resolver` percorre as exigências e devolve uma
**lista**, e o comentário dele dizia, com todas as letras, que era *"o que permite dois
procedimentos no mesmo dia exigirem dois termos sem um cobrir o outro"*.

A **leitura** sempre soube devolver vários. Quem não deixava era a **escrita**.

E o erro não aparecia: amarrar o segundo termo trocava o primeiro em silêncio, e a exigência
antiga simplesmente sumia da lista.

**A regra:** quando um lado do sistema fala no plural e o outro no singular, **o que está
errado é quase sempre o singular** — o plural custou trabalho para existir, e ninguém o
escreve por acidente.

---

## Família 6 — O texto que sai impresso fala com quem o lê

Dois defeitos no rascunho dos termos, os dois no conteúdo e não no código — e os dois foram
**refutados pelos juízes** antes de eu confirmar que estavam certos.

- **O `Detalhe` sai impresso na via que o paciente assina**, e o meu trazia *"Confira com o
  paciente quantas horas"* — instrução de balcão num documento que fala do leitor na terceira
  pessoa.
- **Duas declarações admitiam "Não" legítimo.** Havia um *"Estou acompanhado(a) para voltar
  para casa — se a orientação da clínica exigir"*: num dia comum, metade dos pacientes
  responderia "Não" e acenderia **alerta vermelho**. E o próximo alerta a ser ignorado seria
  o do jejum.

**As regras, que agora estão em teste:**

- Declaração é redigida para que **"Não" seja um SINAL**. Se o "Não" é normal, a declaração
  dilui o alerta e não deve existir — a clínica acrescenta a linha quando a exigência for
  real.
- Declaração **afirmativa**, nunca negativa: "não tive febre" respondido com "Não" é dupla
  negação, que o paciente lê errado e a equipe também.
- Todo campo que **sai impresso** fala com o destinatário do papel.

Isto é a regra mais antiga do projeto — *"alerta que dispara para todo mundo é alerta que
ninguém lê"* — aplicada ao lugar onde ela não parece uma regra de software.

---

## Família 7 — Falha no meio de N gravações sem transação

O botão faz **seis gravações** independentes contra um banco remoto, sem nada que as amarre.
Caindo a rede entre a segunda e a terceira, metade ficava configurada — e a guarda de
"já criado" recusava o retry inteiro. Resultado: a declaração de jejum nunca existia, o BSV
nunca cobrava termo, **em silêncio**, com a tela dizendo que o trabalho estava feito.

Agravante que a revisão apontou e que vale por si: o `catch` **não recarregava a lista**,
então a tela continuava dizendo "Nenhum termo escrito" — exatamente o convite a clicar de
novo sem saber que metade já existe.

**As regras:**

- Operação de N passos sem transação nasce **resumível**: o segundo clique completa o que
  faltou, em vez de recusar tudo por causa da metade que ficou.
- **Recarregue depois da FALHA, não só depois do êxito.** A tela precisa mostrar o que ficou
  gravado; senão ela mente sobre o estado do sistema justamente quando ele está inconsistente.

---

## Família 8 — Só o compilador de marcação pega

`clr-namespace:X` **sem** `;assembly=` quando o tipo mora noutro projeto: `MC3074`, sete
minutos de CI.

Nenhuma rede local pega, pela razão de sempre: o XML é bem-formado, o `compilar-sombra`
**não lê o corpo** do XAML e o C# compila.

O que torna este caso didático é que a **checagem 33 já existia para esta família** — ela
nasceu na parcela 60 pegando o `;assembly=` que **sobra** (tela movida entre projetos), e
este é o mesmo erro pelo avesso. Seis parcelas até alguém tentar o outro lado.

**A regra** (a mesma da Família 1, ponto 3, e ela merece a repetição): **ao escrever uma rede
para um erro que tem dois sentidos, cubra os dois no mesmo commit.**

---

## Família 9 — O método de revisão também erra, e erra calado

Duas falhas do processo, não do produto — e as duas produzem o mesmo desfecho perigoso:
**parecem "nada encontrado"**.

**(a) O script do workflow devolveu lista vazia por defeito.** Numa rodada anterior, a fase de
verificação passou *promises* para `parallel()`, que espera *thunks*; ela morreu e o workflow
devolveu `{confirmados: [], descartados: []}` — indistinguível de "revisei e está limpo". Os
achados estavam no `journal.jsonl` o tempo todo.

> **Resultado vazio de workflow é para ser investigado no journal, nunca lido como aprovação.**

**(b) O veredito de refutação não é palavra final.** Nesta rodada, com a verificação
funcionando:

- **três** dos oito achados confirmados também tinham sido **refutados** por outra lente;
- **dois** achados dados como refutados eram **verdadeiros** (os da Família 6), e só foram
  corrigidos porque eu os reli em vez de confiar no veredito.

O que salvou os dois casos foi a **diversidade de lentes**, não a quantidade de céticos: o
mesmo defeito visto por ângulos diferentes sobrevive ao ângulo que erra.

**As regras:**

- Ao montar a revisão, prefira **lentes diferentes** a mais céticos idênticos.
- **Releia os refutados** quando o assunto for conteúdo (texto que vai ao paciente, redação de
  alerta, rótulo impresso) — é onde o "advogado do código" tem menos com o que trabalhar,
  porque o código está tecnicamente correto e o defeito é de significado.
- Achado sobre a própria ferramenta merece verificação **empírica**, não argumento: os dois
  buracos da checagem 18 foram confirmados reintroduzindo o defeito e vendo a ferramenta
  passar.

---

## Família 10 — A tela que se contradiz

O cabeçalho da tela de termos afirmava *"O termo vale POR SESSÃO: ele é pedido a cada vez"* —
sobra da 1ª versão da parcela 66, antes de a validade virar escolha por procedimento. A
legenda da caixinha, na mesma tela, dizia o contrário e estava certa.

O dano não é cosmético: quem lê no topo que o consentimento longo será pedido em toda sessão
conclui que isso é inviável e **desliga a exigência** — a garantia que a parcela existe para
dar.

**A regra:** ao mudar uma regra de negócio, **procure o texto da tela que a explicava.** Ele
não compila, não tem teste, e continua afirmando a regra antiga com toda a autoridade de
estar escrito na interface.

---

## O denominador comum

Tirando os deslizes de código que as redes pegaram, **quase todos** os defeitos desta rodada
têm a mesma assinatura: **nada falha.** Não há exceção, não há teste vermelho, não há build
quebrado. O sistema faz uma coisa levemente diferente da que diz fazer, e quem descobre é
quem usa.

É a mesma assinatura do defeito que dá nome ao produto — a guia obtida +24h depois que
ninguém lembra. Ele não avisa; ele só não acontece.

As perguntas que teriam pego a maioria, e que valem para a próxima parcela:

1. **O que esta checagem não vê?** (Famílias 1, 8)
2. **Esta exceção delimita o que dispensa?** (Família 2)
3. **Esta âncora é editável?** (Família 3)
4. **Este campo tem "vazio" e "ausente" como estados diferentes?** (Família 4)
5. **A escrita permite tudo o que a leitura devolve?** (Família 5)
6. **Este texto sai impresso? Para quem ele fala?** (Família 6)
7. **Se cair a rede no meio, o segundo clique conserta ou trava?** (Família 7)
8. **Que texto de tela explicava a regra que eu acabei de mudar?** (Família 10)
