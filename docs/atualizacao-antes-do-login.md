# Atualização antes do login, "como nos jogos" (nov/2026)

**O pedido (dono).** Ao abrir, antes da tela de login, o app confere se há versão nova. Havendo, mostra
"Atualizando…" com barra de progresso, baixa, reinicia sozinho na versão nova e só então abre o login. Sem
versão nova, segue direto.

## Como ficou (apps da suíte: Recepção, Financeiro, Gerente, Consultório)

| Situação na abertura | O que a pessoa vê | O que acontece |
|---|---|---|
| Sem internet, GitHub fora, proxy | nada — o login abre | segue na versão atual |
| A procura não responde em 10 s | nada — o login abre | segue na versão atual |
| Sem versão nova | nada — o login abre | — |
| Versão **comum** | nada — o login abre | baixa por trás e instala **ao fechar**; a próxima abertura já entra atualizada |
| Versão **obrigatória** | a janela **"Atualizando para a versão X…"**, com a barra | baixa, instala e **reabre** na versão nova, e aí o login |
| Obrigatória que passa de **2 min** | a janela fecha e o login abre | a instalação fica para o fechamento |
| Obrigatória com download que falha | a janela fecha e o login abre | registrado no log |
| A mesma versão já falhou 2× em 12 h (PR #324) | nada — o login abre | instalação suspensa, com o motivo no log |

**Obrigatória** é a versão com a marca `<!-- clinica:obrigatoria -->` nas notas da release. O `release.yml`
(PR do repositório de atualizações) a escreve quando o disparo manual pede **ou** quando entrou migration
desde a última versão daquele app — o banco muda e a versão antiga pode não entender as colunas novas.

Regra em `Clinica.Atualizacao.AtualizacaoNaAbertura` (sem WPF nem Velopack, com 10 testes em
`AtualizacaoNaAberturaTests`); os passos de verdade em `AtualizadorSuite.PassosVelopack`; a janela em
`JanelaDeAtualizacao`.

## O PR #324 entra JUNTO (e pode ser fechado depois deste)

O #324 limita a 2 tentativas por versão a cada 12 h a instalação na abertura: quando o Velopack não consegue
trocar a pasta (arquivo em uso, antivírus), ele reabre o app ANTIGO, que acha a mesma novidade e manda
instalar de novo — a tela do Velopack que não saía (clínica, 26/10). A atualização antes do login é
exatamente o mesmo `ApplyUpdatesAndRestart` na abertura, agora com uma tela nossa por cima: sem o limite, o
laço voltaria com a janela "Atualizando…" no lugar da do Velopack. Por isso este PR **traz o #324 por
merge** (autoria preservada) e passa a decisão pela regra (`MotivoParaNaoInstalar` antes de mostrar a janela;
`RegistrarTentativa` antes de entregar ao Velopack). Também mantém o `SetAutoApplyOnStartup(false)` dos quatro
`Program.cs`, que impede o Velopack de reaplicar sozinho, antes do nosso código, um pacote que já falhou.
Mergeado este, o #324 pode ser fechado como incorporado.

## Sem flash preto

A janela "Atualizando…" é **WPF nativo**, não Blazor: ela existe antes do navegador da janela do app
(`JanelaDoApp`), e um WebView2 nela traria de volta a janela preta do primeiro instante (CLAUDE.md, 🕰️). O
fundo é o token `Brush.Superficie`, pintado no primeiro quadro. Ela só aparece para a versão obrigatória;
nas outras aberturas não há janela a mais — o login (o navegador único, criado escondido e mostrado depois
da primeira renderização) continua igual.

## O ponto de encaixe da animação de abertura (para o design)

- **Onde:** `JanelaDeAtualizacao.Animacao` — um `ContentControl` de **120×120** acima da frase da versão. O
  conteúdo nasce em `AnimacaoDeAbertura.Criar()`; hoje é o símbolo estático (`Assets/Marca/simbolo-cor.png`).
- **Tecnologia:** **WPF nativo** (XAML vetorial + `Storyboard`). Não HTML: a janela de abertura existe antes
  do WebView2.
- **Quanto pode durar:** ela não pode ADIAR nada — roda enquanto o trabalho acontece. Começa no primeiro
  quadro; é cortada a qualquer instante (a janela fecha quando o download termina); se tiver introdução,
  **até ~1 s**, e o resto em laço.
- **Próximo passo, quando a animação chegar:** a mesma janela pode virar a **abertura de toda partida**
  (logo animada do processo começar até a primeira renderização do login, ~1–3 s hoje, cobrindo também a
  conexão e as migrations). Isso pede um aviso da `JanelaDoApp` ("desenhou") para fechar a de abertura na
  hora certa — o evento já existe (`EstadoDoApp.Desenhou`); fica para o PR da animação.

## O que NÃO mudou

- **O faturamento** (`Clinica.Desktop`, `UpdateService`) continua com a atualização de antes (30 s na
  abertura, sem tela própria, sem o limite do #324): está em produção e o pedido do #324 já o deixava de fora.
  Levar a ele a mesma regra é um PR à parte, com homologação.
- A verificação a cada 30 min com o app aberto continua instalando só ao fechar.

## UX · DX · CX · UI

- 🧑‍💻 **UX** — no dia comum, nada muda: o login abre na hora. Só a versão que precisa (migration) segura a
  abertura, com barra e frase, e nunca mais que 2 min; sem internet não há espera.
- 🛠️ **DX** — a regra é pura e testada (10 testes) e decide "obrigatória" por uma marca escrita pelo próprio
  workflow; o limite do #324 mora num lugar só e vale para os dois caminhos de instalação.
- 🤝 **CX** — a versão com migration não convive mais com a antiga na mesma clínica por um dia inteiro; o
  pior caso (instalação travada) continua abrindo o app.
- 🎨 **UI** — janela de 440×300 sem borda do sistema, fundo e cores de token, símbolo da marca, barra de
  progresso; o lugar da animação já existe.
