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
>
> ⚠️ A **15** não vem de parcela nenhuma: saiu de uma pergunta de arquitetura, sem uma linha
> de código escrita. Está aqui porque a assinatura dos achados é a mesma do denominador comum
> — **nada falha** —, e porque um deles é a Família 6 com roupa nova.
>
> ⚠️ E as **16 a 23** não vêm de código nenhum: são a **virada do banco para a VPS**, feita ao
> vivo, com a clínica trabalhando do outro lado. Estão aqui porque em campo **não existem as
> três redes** — a única rede é o critério que o roteiro escreve —, e porque metade delas é
> sobre critério que não conseguia reprovar. A assinatura é a mesma: nada falhou.

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

---

## Família 15 — A premissa que só vale enquanto houver UM tipo de cliente

> **De onde veio:** a pergunta *"e se a clínica tivesse um site em PHP, com API, para o
> agendamento cair direto no sistema?"*. Não se escreveu código nenhum — o que segue é a
> leitura do que já existe. Nada disto está errado hoje; **tudo passa a estar** no dia em que
> um segundo tipo de cliente existir. É a família mais barata de achar, porque a rede que a
> pegou foi uma pergunta de arquitetura, que roda antes de haver o que consertar.

Seis achados, todos conferidos no código:

- **`SessaoUsuario.Atual` é singleton de PROCESSO, e sem autenticação libera tudo.**
  `Efetivas => Autenticado ? Permissoes : PerfisAcesso.Todas`
  (`SessaoUsuario.cs:90`). É decisão documentada e certa no balcão — *"tela vazia parece
  defeito, e no app real o login é obrigatório"*. Num processo web é **um usuário para o
  servidor inteiro**, com permissão total por padrão.
- **A mensagem que ajuda no balcão VAZA em outra porta.** `PacienteService.cs:158` recusa CPF
  repetido dizendo *"já está cadastrado para Maria Silva. Abra a ficha dela"* — e é essa frase
  que transforma um erro em instrução (parcela 57). Devolvida a um desconhecido pela internet,
  a mesma frase é um oráculo de CPF → nome. **É a Família 6 com roupa nova**: o texto fala com
  quem o lê, e aqui o leitor mudou sem ninguém reescrever o texto.
- **Os catálogos são cache estático de processo.** `CatalogoConvenios` guarda um dicionário
  trocado por `RecarregarCacheAsync`. Com um processo, chamar no arranque basta. Com dois, a
  clínica cadastra a operadora no desktop e o outro fica com o catálogo velho — sem erro, sem
  log, sem teste vermelho.
- **`GarantirSemChoqueAsync` é check-then-act, sem transação** (`AgendaService.cs:259`):
  confere o choque e depois insere. Hoje o serializador é **humano** — há uma recepcionista
  por vez. O próprio `DependencyInjection.cs:184-187` já avisa que não há transação explícita
  em ponto nenhum do projeto, e que quem introduzir uma precisa passar pelo
  `Database.CreateExecutionStrategy` por causa do retry.
- **"Livre" quer dizer "ninguém marcou", não "a clínica trabalha".** Não existe escala do
  profissional; a janela 07:00–20:00 é constante da camada **WPF**
  (`AgendaViewModel.cs:219-220`) e a grade se ESTICA para caber o que houver no dia. Quem
  completa a informação que falta é a pessoa que olha a tela.
- **`encaixe: true` fura tudo, inclusive bloqueio de feriado** (`AgendaService.cs:263`). É
  válvula deliberada para uma decisão humana — *"a clínica DECIDE atender por cima"*. Como
  parâmetro de uma chamada programática, é só um furo.

**Por que nenhuma rede pega:** não há o que pegar. Cada um destes é a decisão **certa** para o
contexto em que nasceu — um processo, um humano autenticado, uma pessoa por vez. O contexto
não está escrito na assinatura do método, e é ele que expira.

**A regra:** antes de acrescentar um segundo tipo de cliente — processo web, integração, app
novo —, a pergunta é *o que neste código só é verdade porque existe **um** usuário humano,
autenticado, por processo?* A resposta não aparece em teste nenhum, porque o teste também roda
num processo só, com um usuário só.

**E um corolário sobre a decisão de arquitetura, não sobre o código.** O `docs/banco-na-vps.md`
descartou *"API HTTPS no meio"* com razões corretas: 234 métodos no `IClinicaRepositorio`, 179
`SalvarAsync` por change tracking do EF, `xmin` que não atravessa HTTP. Aquilo respondia
*"trocar a camada de dados dos cinco apps de desktop"*; não responde *"cinco endpoints para um
site"*. É a lição da parcela 51 fora do lugar onde foi escrita: **quando uma decisão exclui um
caminho, o motivo tem prazo de validade — releia-o antes de citá-lo.** Decisão citada pela
conclusão, e não pela razão, vira regra que ninguém sabe mais por que existe.

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

## Adendo II — a virada do banco para a VPS, e a rede que não existe em campo

> A clínica saiu da Neon (banco gerenciado, cobrança por uso, servidor fora do país) para uma
> VPS brasileira com PostgreSQL fechado por **mTLS** — certificado de cliente em cada máquina,
> nada exposto. O desenho está em `docs/banco-na-vps.md` e o roteiro da noite em
> `docs/virada.md`. Aqui ficam só os **erros do caminho**.
>
> A diferença de fundo em relação a tudo o que está acima: **em campo não existem as três
> redes.** Não há `compilar-sombra`, não há `verificar-suite`, não há os 1546 testes. A única
> rede é o **critério que o roteiro escreve**.

### O placar

| Quem achou | O quê | O que isso significa |
|---|---|---|
| O roteiro (a única rede que existe em campo) | 2 | A versão do Postgres da Neon conferida **antes** de copiar; a contagem tabela a tabela no fim. Foram as duas únicas coisas que funcionaram sozinhas — e as duas eram passos com critério objetivo |
| Uma conferência escrita **depois** do fato | 1 | Máquinas instaladas continuavam gravando na Neon. Nada acusou, e o roteiro dava o passo por feito |
| Eu, refazendo o diagnóstico do zero | 1 | O `subir-pg18.sh` nunca tinha tido efeito — e horas de diagnóstico partiram dessa premissa |
| O cliente, na tela | 2 | Senha recusada (`28P01`); painel dizendo "usuário ou senha incorretos" sendo outra coisa |
| **Ninguém ainda** | **1** | A janela da virada: o que foi gravado na Neon depois da foto final |

⚠️ **A linha que importa é a segunda.** A migração "não acusou nada", as 20 máquinas foram
instaladas sem um erro, e a clínica continuou gravando no banco antigo. Nenhum passo falhou —
a mesma assinatura do denominador comum, agora sem nenhuma ferramenta para pegá-la.

---

## Família 16 — "Rodou" não é "teve efeito", e "não acusou nada" não é prova

Três casos, e os três produzem a mesma sensação de trabalho concluído.

**Os casos:**

- **O `subir-pg18.sh` rodou e não subiu nada.** A Neon serve PostgreSQL 18 e o Ubuntu traz o
  16; o script existia para acertar isso. Ele foi executado, não teve saída conferida, e por
  **horas** eu diagnostiquei erros partindo de "o cluster é 18". O `pg_lsclusters` mostrou 16.
  A virada acabou acontecendo no 16 — que é suportado até 2028 e o app não distingue —, mas
  todo raciocínio no meio partiu de uma premissa falsa.
- **As máquinas foram instaladas e a clínica continuou gravando na Neon.** O `.bat` rodou
  certo em todas, a variável de ambiente estava correta na máquina **e** no processo. A causa
  é de uma linha: **programa aberto não relê variável de ambiente.** O app já estava aberto
  desde antes da configuração.
- **"Fiz a migração e não acusou nada."** Ausência de reclamação lida como confirmação.

**Por que nenhuma rede pegou:** não há rede — e o roteiro, que é a rede em campo, tinha o
passo certo ("fechar e reabrir o app → conferir que abriu **com os dados**") com um critério
**incapaz de reprovar**: a Neon também tem os dados. O app abre igual nos dois bancos. O
critério existia, era objetivo, e passava do mesmo jeito nos dois desfechos.

O que resolveu foi uma conferência escrita depois (`onde-esta.sh`): contar as **duas** bases e
ver **qual delas cresce**. Essa só passa de um jeito.

**A regra:** todo passo de campo precisa de um critério que consiga **REPROVAR**. Antes de
escrevê-lo, pergunte: **o que eu veria se este passo NÃO tivesse funcionado?** Se a resposta
for "a mesma coisa", não é critério — é decoração. E o corolário para script de operação:
**script que não imprime a prova do que fez é script que ninguém conferiu.**

---

## Família 17 — O ambiente herdado decide o que você não disse

**Os casos:**

- **Seis tempos-limite silenciosos ao ler a Neon** — de dentro da VPS, que **hospeda o banco
  de destino**. O `psql` montou a conexão com pedaços da linha de comando e pedaços do
  ambiente, e o ambiente carregava a porta do cluster **local** (45432) e endereços IPv6 que
  não roteiam dali. Nada mentiu: ele fez exatamente o que o ambiente mandou.
- **`pg_restore: unsupported version (1.16) in file header`** — o wrapper do Ubuntu escolhe o
  binário pela versão do **cluster padrão**, não pela do arquivo que está sendo lido. Um dump
  em formato 18 chegou ao `pg_restore` 16.

**Por que nenhuma rede pegou:** as duas máquinas do problema são a mesma máquina. Numa VPS que
hospeda o banco de destino, **toda variável de cliente já aponta para o lado errado** — e é o
único lugar onde ninguém pensa nisso, justamente porque "é o servidor do banco".

**A regra:** conexão de migração **dita todos os campos** — `unset` explícito do que vaza
(`PGPORT`, `PGHOST`, `PGHOSTADDR`, `PGUSER`, `PGDATABASE`, `PGPASSWORD`) e host, porta,
usuário, base e senha declarados na chamada. É mais curto do que descobrir qual deles vazou.
E **quando há duas versões do mesmo binário instaladas, use o caminho completo** — o wrapper
resolve pela versão do cluster, não pela do arquivo.

---

## Família 18 — `set -e` transforma aviso conhecido em fracasso, e apaga a prova

Na migração definitiva, o dump trazia no cabeçalho um `SET transaction_timeout` que o
PostgreSQL 16 não conhece. Erro **conhecido e inofensivo** — o restore tinha funcionado. Só
que o `set -e` matou o script ali: sem `ANALYZE`, e principalmente **sem imprimir as
contagens**, que eram a única coisa capaz de provar que estava tudo certo.

Quem estava na clínica, à noite, no meio de uma virada, viu um erro em vermelho e nenhum
número. É o pior desfecho possível de um passo que deu certo.

Agravante: o diagnóstico inicial partiu do cluster errado (Família 16), então a mensagem foi
lida contra a premissa errada.

**A regra:** em roteiro de campo, **quem decide o desfecho é a CONFERÊNCIA no fim, não o
código de saída de cada comando.** Etapa cujo erro é conhecido e inofensivo roda com o erro
explicitamente tolerado, e a prova fica com a contagem. **Script que morre antes de imprimir a
prova deixa quem está na clínica sem saber se refaz tudo.** (A doença oposta é igualmente
ruim: `|| true` em cima do passo que importa.)

---

## Família 19 — Mensagem de erro que aponta a causa errada custa mais que o silêncio

Dois casos, um nosso e um de fora.

- **`28P01: password authentication failed`** na primeira máquina. A senha estava certa: ela
  fora gerada com `openssl rand -base64 24`, e os `/`, `+` e `=` do base64 quebram a
  *connection string*. O gerador estava correto; o **formato de quem consome** é que não
  aceita o alfabeto dele. Conserto: `openssl rand -hex`.
- **O painel (Cockpit) dizendo "usuário ou senha incorretos"** através do túnel SSH. As
  credenciais estavam certas — era **recusa de origem**. A providência natural diante daquela
  frase é trocar a senha de root da VPS: o dano teria sido causado pela mensagem, não pelo
  defeito.

**A regra, de que o projeto já tinha metade:** a parcela 41 fixou que *guarda que volta em
silêncio é botão que não faz nada*. O degrau seguinte é este — **a guarda fala, e fala outra
coisa**. A mensagem nomeia a **checagem que falhou**, quase nunca a causa; quando ela não bate
com o que você sabe, **leia o log do sistema antes de agir sobre o que ela diz**.
E do nosso lado: **valor gerado tem de caber no formato de quem o consome** — senha, número de
guia, nome de `SharedSizeGroup` (parcela 50). É a mesma família.

---

## Família 20 — Instrução não é controle

Pedi **duas vezes** que dado de paciente não fosse colado no chat. Foi colado nas duas. E com
razão: a pessoa estava diagnosticando, e era o que a tela mostrava.

A terceira tentativa não foi um terceiro aviso — foi **reescrever a ferramenta**. O
`orfaos.sh` passou a comparar **carimbos de hora e contagens** em vez de linhas: a saída dele
**não tem como** conter nome de paciente.

**Por que nenhuma rede pegou:** o compromisso de conformidade do `CLAUDE.md` protege o
**produto**. Aqui o caminho de vazamento era a **ferramenta de operar o produto**, que não
estava na lista de ninguém. Pior: leitura por `psql` **não deixa rastro nenhum** — o
`AcessoProntuarioService` cobre as telas do sistema, não o terminal do servidor.

**A regra:** **quando a proteção depende de alguém lembrar, ela já falhou.** É a mesma frase
do `PoliticaBackupService` (backup que depende de clicar existe no manual, não no disco) e da
crítica do número da guia (validar na tela cobre uma porta de quatro). Ferramenta de operação
que toca base clínica **nasce sem poder imprimir dado de paciente**: ela responde *quantos* e
*quando*; quem precisa do nome abre a tela do sistema, que registra o acesso.

> **Nota que vale por si:** senhas circularam no chat durante a instalação. O estrago foi
> nenhum, e não por sorte — **o desenho tem duas fechaduras**: sem o certificado de cliente, a
> senha do banco não abre nada. Foi o mTLS que transformou um vazamento em um aborrecimento.
> É o argumento mais forte a favor dele, e ele não aparece em auditoria nenhuma.

---

## Família 21 — "Automatizado" é medida de quem executa, não de quem escreve

Foram **três** versões do kit de instalação até ele ser aceito, e as três eram "automáticas"
pela minha medida:

1. gerar certificado e instruções por máquina → *"porra, fazer isso em 20 máquinas?"*;
2. um `.bat` por máquina, pedindo a lista de nomes → *"não tenho os 20 nomes, são aleatórios,
   não quero essa info"*;
3. **pilha de certificados anônimos** → aceito.

O critério só apareceu quando foi dito como **número**: *"só aceito se eu tiver que mandar só
1 comando na VPS"*.

O desenho que saiu daí merece registro, porque foi a restrição que o produziu: o `.bat` pega
**o primeiro `.pfx` livre da pasta**, move-o para `usados\` e escreve uma linha no
`registro.txt`. Ninguém digita nome de máquina, e **a planilha de revogação se escreve
sozinha** — o registro é subproduto da instalação, não uma tarefa ao lado dela.

**A regra:** antes de entregar roteiro de operação, **conte**: quantos comandos, quantos campos
a preencher, quantas decisões por máquina. Se o número não for o que a pessoa aceita, o
roteiro não está pronto — e **esse número se pergunta, não se estima**.

---

## Família 22 — A branch que ficou aberta enquanto a `main` andou

A PR da virada teria mostrado **289 arquivos e 41.774 deleções**: a `main` avançara 7 PRs, e o
diff — correto como diff — apagaria o trabalho de todas elas. Depois de trazer a `main` para
dentro da branch (sem um conflito), foram 8 arquivos e 696 inserções.

**A regra:** antes de abrir PR de branch que ficou dias parada, rode
`git diff origin/main..HEAD --stat` e **leia o número de DELEÇÕES antes do de arquivos**. É a
única linha que denuncia isso, e ela aparece antes de qualquer revisor.

---

## Família 23 — A virada não é um instante: é uma JANELA, e ela é por máquina

O roteiro (`docs/virada.md`) supunha um momento: todos fora do sistema → foto final → viradas.
No mundo real a troca aconteceu **máquina a máquina**, ao longo de horas, com notebooks
remotos — e, pela Família 16, cada máquina só virou de fato quando o app foi **fechado e
reaberto**. Tudo o que foi lançado na Neon entre a foto e a virada de cada máquina ficou lá.

Resultado: **dois bancos que divergiram do mesmo instantâneo**, com os **IDs colidindo** — o
atendimento 152 da Neon e o 152 da VPS são registros diferentes.

**Como NÃO se conserta: por SQL.** Três razões, e a segunda é a do produto:

1. os IDs colidem, então não há `INSERT` que preserve as duas verdades;
2. `AtendimentoService.LancarAsync` é **ponto único** — ele gera as guias pelas regras do
   convênio, debita o pacote, baixa o insumo e lança no caixa. Um `INSERT` cria um atendimento
   **sem a guia**, que é exatamente o defeito que o produto existe para impedir;
3. é **prontuário**: estado inconsistente custa mais que redigitar três linhas.

O conserto é **relançar pelo app**, e a conta é pequena de propósito — a janela é de horas.

**A regra para a próxima virada:** decida entre as duas honestamente. Ou **para todo mundo de
verdade** (inclusive remoto) e vira num movimento só; ou **aceita-se a janela**, e aí o
relançamento é **parte do roteiro**, com a consulta que NOMEIA os registros perdidos (a que
compara carimbos dos dois lados) escrita **antes** — não depois, com a clínica já trabalhando
nos dois bancos.

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

⚠️ **E a virada do banco fecha o argumento pelo outro lado.** Ali não havia verde nenhum para
se enganar com ele — não havia teste, build nem checagem —, e o desfecho foi o mesmo: nenhum
comando falhou, as 20 máquinas instalaram sem um erro, e a clínica passou dois dias gravando
no banco antigo. Ou seja: **o problema nunca foi confiar no verde. É aceitar como resposta
qualquer coisa que passaria dos dois jeitos** — e o verde é só a forma mais confortável disso.

> A pergunta que unifica o documento inteiro: **o que eu veria se isto NÃO tivesse
> funcionado?** Se a resposta for "exatamente o que estou vendo", não sei se funcionou — sei
> que nada reclamou.

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
13. **O que aqui só é verdade porque há UM usuário humano, autenticado, por processo?**
    (Família 15)

E as que a virada acrescentou, para todo roteiro que sai da máquina de quem programa:

14. **O que eu veria se este passo NÃO tivesse funcionado?** Se for a mesma coisa, o critério
    não reprova nada. (Famílias 16, 18)
15. **Que variável desta máquina já aponta para o outro lado?** (Família 17)
16. **Esta proteção depende de alguém lembrar?** (Família 20)
17. **Quantos comandos e quantos campos sobram para quem executa — e eu perguntei o número?**
    (Família 21)
18. **Isto acontece num instante ou numa janela? Quem grava durante ela?** (Família 23)

A da **Família 15** é a única da lista que se faz **antes de existir código**: as demais
precisam de um diff ou de um roteiro em execução para ter onde morder. É o que a torna a mais
barata — basta uma mudança de contexto anunciada.
