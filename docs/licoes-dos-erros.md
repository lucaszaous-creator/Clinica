# O que os erros desta rodada ensinam

> Retrospectiva das parcelas **66 (5ª rodada)** e **67** — a tela do paciente e os termos do
> BSV. Só os **erros**: o que quebrou, quem pegou, e o que fazer diferente da próxima vez.
>
> O `CLAUDE.md` guarda as lições em ordem cronológica, uma entrada por parcela. Este
> documento é o corte transversal: **por FAMÍLIA**, porque a mesma família volta com roupa
> diferente, e é olhando o conjunto que se percebe qual pergunta faltou.
>
> As famílias **1 a 10** saem das parcelas 66 e 67. As **11 a 14** vêm do adendo da parcela
> **65** — o defeito que o cliente achou em produção no dia seguinte ao merge, e o único
> desta lista que nenhuma rede, nenhum teste e nenhuma revisão pegou.

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

## Adendo — a parcela 65, e o defeito que o CLIENTE achou no dia seguinte

> As dez famílias acima saem das parcelas 66 e 67. Esta seção é **anterior** a elas na
> cronologia e **posterior** no aprendizado: o defeito da parcela 65 foi encontrado pelo
> cliente, em produção, no dia seguinte ao merge — e as famílias que ele revela não
> aparecem em nenhuma das dez.

**O caso, em uma linha:** a parcela 60 unificou a esteira do atendimento e, ao unificá-la,
pendurou a criação da **guia** na CONFIRMAÇÃO da janela de fechamento (pacote/insumo/caixa).
Quem fechasse aquela janela ficava com o horário na agenda, o paciente marcado como presente
e **nenhuma guia** — e, como o encaixe já tinha sido criado, a tela parecia ter funcionado.

### O placar

| Quem achou | O quê |
|---|---|
| As três redes locais | nada |
| O CI (build dos cinco `.exe`) | nada |
| A revisão do diff | nada |
| **O cliente, em produção** | **o defeito inteiro** |

**1512 testes verdes**, `compilar-sombra` verde, `verificar-suite` verde, CI verde. É o
denominador comum deste documento na forma mais pura: *nada falhou*.

### A evidência, que é o dado mais valioso da rodada

```
Id   DataHora           Paciente        Status     ChegadaEm         CriadoPor
161  12/08/2026 16:10   ELLEN GLAUCE…   Agendado   16:10:31.418591   flavia@
162  12/08/2026 16:10   ELLEN GLAUCE…   Agendado   16:10:37.338511   flavia@
163  12/08/2026 16:11   ELLEN GLAUCE…   Agendado   16:11:42.203364   flavia@
```

A mesma paciente lançada **três vezes em 71 segundos**, os três com check-in carimbado e
`AtendimentoId` nulo. Zero guias, três cartões na fila para uma sessão.

---

## Família 11 — Ao juntar dois fluxos, o fato IRREVERSÍVEL não pode depender do passo OPCIONAL

A parcela 60 estava certa no diagnóstico (duas portas faziam coisas diferentes) e errada num
detalhe que ninguém pesou: **em que momento cada um dos quatro fatos passa a existir.**

Concluir a sessão são quatro fatos — a guia nasce, o pacote debita, o insumo sai, o dinheiro
entra. O `ConcluirAsync` já tinha a hierarquia certa **entre** eles: só o atendimento derruba
a operação, os outros três viram aviso. O que faltou foi estender essa hierarquia ao
**momento**: os quatro passaram a nascer no mesmo clique, e esse clique era o do passo
opcional.

Invertida a ordem, o desenho se resolve sozinho: a guia nasce no registro e os outros três
viram passo seguinte, que é exatamente o peso que eles já tinham.

**A regra:** ao unificar fluxos, liste os fatos que o ato produz, marque qual deles é
**irreversível** ou **externo** (a guia vai à operadora; o pacote e o caixa se resolvem por
outra tela) e garanta que ele aconteça **primeiro** e sem depender de confirmação posterior.

---

## Família 12 — Teste que exercita só o caminho canônico não vê o caminho abandonado

Os 1512 testes cobriam a esteira inteira, e **todos** chamavam `ConcluirAsync`. Nenhum
exercitava o que a recepcionista fez: **fechar a janela**. O caminho abandonado não tinha
teste porque não parece um caminho — parece desistência.

> O caminho que ninguém questiona é o que os testes exercitam.

Agravante da mesma família: **dois testes existentes fixavam o MECANISMO em vez da
GARANTIA.** Eles afirmavam que a segunda tentativa *estourava* (`ThrowAsync`) — um detalhe de
implementação. A correção certa (reaproveitar o atendimento em vez de recusar) os deixou
vermelhos, e um teste vermelho por causa da correção é um convite a desfazer a correção. A
garantia real — *uma sessão, uma guia, um débito* — vale nos dois desenhos, e é ela que o
teste devia cobrar desde o começo.

**As regras:**

- Para todo fluxo com uma saída de desistência (fechar, cancelar, Esc), escreva o teste do
  **estado em que o sistema fica** quando a pessoa desiste no meio.
- Teste afirma a **garantia**, não o mecanismo. `ThrowAsync` é mecanismo; "não duplicou nada"
  é garantia.

---

## Família 13 — Mensagem inline numa tela que a pessoa já dá por concluída não chega

A frase existia, era clara e estava correta: *"O horário foi marcado e o paciente está na
Fila, em 'Na recepção'. Conclua por lá para gerar a guia."* Inline, na tela do lançamento.

**Ninguém tenta três vezes em 71 segundos se a mensagem chegou.** As três linhas do banco não
são só a prova do defeito — são a **medida** de que o canal estava errado.

O que a torna invisível é o momento: ela aparece depois de a pessoa ter concluído a tarefa na
cabeça dela. A atenção já saiu da tela; o que resta é procurar o resultado (a guia) e não
achar.

**A regra** — e ela completa a do canal de feedback que o projeto já tinha (*inline para
formulário, snackbar para confirmação passageira*): **quando a mensagem contradiz a conclusão
que a pessoa já tirou, ela precisa de um canal que interrompa** (diálogo), ou o desenho
precisa mudar para a mensagem não ser necessária. Aqui foi o segundo, que é sempre melhor: a
guia passou a nascer sozinha, e a frase deixou de existir.

Corolário de diagnóstico: **repetição rápida da mesma ação é um dado, não ruído.** Três
tentativas em 71 segundos localizam o defeito com mais precisão do que qualquer log.

---

## Família 14 — Quando o custo de um erro muda, a guarda muda de lugar

Antes desta parcela, o segundo clique no lançamento custava **um horário a limpar**. Depois,
com a guia nascendo no clique, ele passou a custar **um jogo de guias duplicado indo para a
operadora** — que só aparece semanas depois, no retorno.

A mesma ação, o mesmo botão, um custo diferente. Por isso a correção não foi só inverter a
ordem: veio junto a pergunta antes de criar o encaixe ("este paciente já tem atendimento
hoje") e a **idempotência por agendamento** no serviço, para que dois cliques no Finalizar
não virem duas guias.

E ela é **pergunta, não recusa**: sessão de manhã e consulta à tarde é caso legítimo, e
recusar travaria o balcão sem contorno — a mesma escolha que o formato do número da guia já
tinha feito.

**A regra:** toda vez que uma mudança torna um efeito mais caro ou mais difícil de desfazer,
**releia as guardas que protegiam o efeito antigo.** Elas foram dimensionadas para o custo
anterior.

---

## E uma da Família 10, achada ao escrever este documento

O comentário de classe do `FilaViewModel` ainda afirma que Finalizar *"gera o atendimento com
os códigos **e o retorno do 2º código**"*. A parcela 58 removeu essa criação — o retorno
sugerido era o agendamento fantasma que punha na agenda dos médicos uma pessoa que não tinha
hora marcada, e o comentário sobreviveu à remoção.

É a Família 10 fora da tela: prosa que continua afirmando a regra antiga, com a autoridade de
estar escrita ao lado do código. **Ao mudar uma regra, o `grep` pela regra antiga vale tanto
quanto o teste** — e ele alcança comentário, que nenhuma rede lê.

---

## O denominador comum

Tirando os deslizes de código que as redes pegaram, **quase todos** os defeitos desta rodada
têm a mesma assinatura: **nada falha.** Não há exceção, não há teste vermelho, não há build
quebrado. O sistema faz uma coisa levemente diferente da que diz fazer, e quem descobre é
quem usa.

É a mesma assinatura do defeito que dá nome ao produto — a guia obtida +24h depois que
ninguém lembra. Ele não avisa; ele só não acontece.

⚠️ E a parcela 65 fecha o argumento pelo pior lado: ali o sistema **deixou de fazer a única
coisa que ele existe para fazer** — gerar a guia — e mesmo assim tudo ficou verde. Não é que
as redes tenham falhado; é que **nenhuma delas pergunta se o produto cumpriu o propósito
dele**. Elas conferem que o código compila, que a regra calcula certo e que a tela monta.
Que a guia chegue ao faturamento quando alguém atende um paciente é uma afirmação sobre o
FLUXO, e fluxo se testa de ponta a ponta ou não se testa.

As perguntas que teriam pego a maioria, e que valem para a próxima parcela:

1. **O que esta checagem não vê?** (Famílias 1, 8)
2. **Esta exceção delimita o que dispensa?** (Família 2)
3. **Esta âncora é editável?** (Família 3)
4. **Este campo tem "vazio" e "ausente" como estados diferentes?** (Família 4)
5. **A escrita permite tudo o que a leitura devolve?** (Família 5)
6. **Este texto sai impresso? Para quem ele fala?** (Família 6)
7. **Se cair a rede no meio, o segundo clique conserta ou trava?** (Família 7)
8. **Que texto de tela explicava a regra que eu acabei de mudar?** (Famílias 10, 65)
9. **Quais fatos este ato produz, e qual deles é irreversível?** Ele acontece primeiro?
   (Família 11)
10. **O que sobra no sistema se a pessoa fechar esta janela no meio?** Existe teste disso?
    (Família 12)
11. **Esta mensagem chega depois de a pessoa já ter dado a tarefa por concluída?**
    (Família 13)
12. **Esta mudança tornou algum erro mais caro? Que guarda foi dimensionada para o custo
    antigo?** (Família 14)
