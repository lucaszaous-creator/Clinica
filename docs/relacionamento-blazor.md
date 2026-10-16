# O Acompanhamento de pacientes em Blazor (out/2026)

> Pedido da cliente, sobre a aba **Relacionamento → Acompanhamento**: *"como a usuária assume,
> atende, manda mensagem e vê o progresso?"*. Os dois desenhos e a comparação estão em
> `docs/mockups/relacionamento-dois-desenhos.html`; a cliente aprovou o **desenho A** (a fila com
> a próxima ação + a conversa do paciente por cima). O passo a passo de como se faz uma tela
> Blazor está em `docs/telas-blazor.md`; aqui fica o que foi decidido para ESTA tela, e a lista
> de paridade com a tela WPF que saiu.

## 1. O que mudou de lugar

| Antes (WPF, `Clinica.Modulo.Recepcao`) | Agora (Blazor, `Clinica.Ui.Blazor`) |
|---|---|
| `AcompanhamentoView` + `AcompanhamentoViewModel` (lista, painel ao lado, "Registrar contato…", filtros e configurar como quatro modos da mesma tela) | `Telas/Relacionamento/AcompanhamentoDePacientes` |
| Modo "Registrar contato…" e o painel de contato | `Formularios/Relacionamento/ConversaDoPacienteForm` (Modal) |
| Modo "Mais filtros" | `MaisFiltrosAcompanhamentoForm` |
| "sem vir há N dias ou mais — Buscar" | `GerarRecallForm` |
| Modo "Configurar" | `ConfigurarAcompanhamentoForm` |
| Expander "Visão da gestão — responsáveis e motivos" | `EquipeAcompanhamentoForm` (botão "Por responsável") |

A chave de navegação (`ChavesSuite.RetornoPacientes`, "retorno-pacientes") **não mudou** — o
composto "Relacionamento" da Recepção, a aba do Gerente e o item que o app do **Faturamento**
publica (`ModuloFaturamentoAplicativo`, que pega o item da Recepção) continuam apontando para
ela. A Recepção passou a referenciar `Clinica.Desktop.Blazor` + `Clinica.Ui.Blazor` e a chamar
`AddHospedagemBlazor()` (idempotente: o Gerente e o Faturamento carregam vários módulos Blazor
e ficam com UM navegador por janela).

O que a tela AFIRMA mora na Application, puro e com teste: `AcompanhamentoNaTela` (passo da
linha, faixa do funil, degrau, selo, filtro, o que cada desfecho grava) e
`ConversaDoAcompanhamento` (o pedido do "Registrar", a reabertura, o trilho, o vazio, os filtros
extras, a coluna do CSV e a visão por responsável).

## 2. As decisões, cada uma com a comparação

### 2.1 Como a tela "Marcar horário" alcança a agenda da Recepção

A tela Blazor é `net8.0` puro e não enxerga a Recepção (WPF). O "Marcar horário" da tela WPF
deixava o pedido na ponte `PreenchimentoNovoAtendimento` e navegava para a aba Marcar.

| Opção | 🧑‍💻 UX | 🛠️ DX | 🤝 CX |
|---|---|---|---|
| Navegar só pela chave (`Host.NavegarAsync`) | a aba Marcar abre VAZIA: a recepcionista redigita quem é o paciente | nada novo | o "um clique" do mockup vira três |
| Reescrever a marcação em Blazor | uma segunda tela de marcar, com outra cara | duas marcações divergiriam na primeira correção | caro e arriscado |
| **Interface pequena `IMarcacaoDeHorario`** em `Clinica.Ui.Blazor`, implementada PELA Recepção (`MarcacaoDaRecepcao`) — escolhida | a aba Marcar abre já com o paciente, o profissional que atendia e a modalidade | uma interface, uma implementação; a prévia e os testes caem em `MarcacaoIndisponivel` e a tela DIZ "não disponível" | igual ao WPF; no app do Faturamento (que não tem a agenda) a frase diz onde marcar |

`MarcacaoDaRecepcao` pergunta `NavegacaoSuite.Existe` ANTES de definir o pedido — pedido definido
com navegação recusada ficaria órfão e pré-preencheria a próxima abertura manual da tela.

### 2.2 "Mais filtros" e o que o `FiltroAcompanhamento` não carrega

A tela WPF filtrava por tentativas mínimas, período do último contato, dias sem vir e etapa — e
o filtro do desenho A não tinha esses campos. Em vez de alargar o record que outra frente usa,
eles moram em `FiltroExtraAcompanhamento` (mesmo arquivo de regra, ao lado), e o botão "Mais
filtros (N)" soma os dois.

### 2.3 O desfecho não vem pré-escolhido

O mockup mostra "Não respondeu" destacado. Na tela ele **não** vem marcado: registrar o desfecho
errado conta uma tentativa que não houve e empurra o próximo contato — e o clique em "Registrar"
sem olhar é exatamente o gesto que a casa já pagou caro (a parcela 65). O botão diz o desfecho
escolhido ("Registrar \"Não respondeu\"") e fica apagado até haver um.

### 2.4 O canal é pedido para "não respondeu", "pediu outra data" e "quer marcar"

O serviço conta como TENTATIVA só o contato com canal. Gravar "não respondeu" sem canal deixaria
o caso em "Sem resposta" com zero tentativas — e o botão da linha diria "Falar no WhatsApp" de
novo, como se ninguém tivesse tentado. "Aguardando o plano" (conversa com a operadora) e "Não quer
voltar" (que a gestão pode encerrar sem tentativa) aceitam "sem contato".

## 3. Lista de paridade (cada coisa da tela WPF → onde ficou)

| Na tela WPF | Na tela Blazor |
|---|---|
| Controle segmentado "Sumiram · N" / "Novos BSV · N" (`TrocarAba`) | Segmentado "Recall · N" / "Novos BSV · N" na barra (pendentes do escopo à vista) |
| Título e descrição por fila (`Titulo`, `DescricaoFila`) | Subtítulo único do cabeçalho; a fila escolhida está no segmentado e na coluna "Sem vir"/"Indicado há" |
| Chip "A assumir (N)" | Faixa **A assumir** do funil (clicável) |
| Chip "Meus contatos de hoje (N)" | Faixa **Para contatar hoje** + "Minha fila" (padrão) |
| Chip "Atrasados (N)" | Nota da faixa "Para contatar hoje" (N atrasados, em vermelho) + selo "Atrasado N dias" + ordem da lista (atrasados primeiro) |
| Chip "Sem primeiro contato (N)" | Etapa "A contatar" em Mais filtros + selo "A contatar" + degrau do trilho |
| Chip "Tratamento longo (N)" | "Tratamento longo" em Mais filtros + texto na linha ("tratamento longo (32 sessões)") |
| Atalhos "cancelados"/"sem-resposta" | "Cancelou ou faltou" e Etapa "Sem resposta" em Mais filtros |
| Busca por nome | Busca por **nome, telefone ou convênio** |
| Combo Situação (13 valores) | Funil (A assumir · Contatar hoje · Em conversa · Agendados · Voltaram) + "Encerrados (N)" + Etapa em Mais filtros. "Sessão realizada" = faixa Voltaram |
| Combo Modalidade | Mais filtros → Modalidade |
| Prazo: Hoje / Atrasados / Próximos 7 dias | Faixas do funil + ordem por próximo contato. **"Próximos 7 dias" saiu** como recorte próprio: a faixa "Em conversa" já é a lista ordenada pela data |
| Condição: Pacote com saldo / Sem telefone / Contato não autorizado / Cancelou ou faltou | Mais filtros (as quatro caixinhas) |
| Convênio, Profissional, Funcionária responsável, Motivo | Mais filtros |
| Tentativas mínimas, Último contato de/até, Dias sem vir mín./máx. | Mais filtros (`FiltroExtraAcompanhamento`) |
| "Somente meus pacientes" | "Minha fila" (meu + sem dono) ou Mais filtros → Responsável = você |
| "Limpar filtros" | "Limpar filtros" ao lado do resumo (só aparece quando há o que limpar) e "Limpar estes filtros" no formulário |
| `FiltrosAtivos` (frase dos filtros) | "Mais filtros (N)" + resumo "N de M na fila · filtrada" + vazio que diz "ninguém bate com o filtro" |
| `Resumo` (pendentes · atrasados · agendados · realizados) | O funil (os mesmos números, clicáveis) |
| "Lista atualizada às HH:mm" | Ao lado do resumo |
| Releitura a cada 60 s com a tela visível (timer da View) | Releitura ao **voltar** à aba (`AoVoltarAsync`, silenciosa) + Atualizar/F5. O timer saiu: a tela Blazor fica montada oculta no palco e não sabe se está à vista — um relógio leria o banco para uma tela que ninguém olha. A colisão entre duas mesas continua protegida pela `Versao` (a recusa diz "outra pessoa atualizou") |
| Botão "Atualizar" | Botão de ícone no cabeçalho + **F5** |
| "Exportar CSV" com as duas barreiras e a coluna "Pode receber mensagem" | Igual, mesmas 12 colunas, + **Ctrl+P** |
| "Configurar" (só Gerente): profissional BSV, motivos | `ConfigurarAcompanhamentoForm`: profissional BSV, **responsável padrão** (a tela WPF não tinha o campo e gravava zero por cima do configurado) e motivos |
| "Buscar" (sem vir há N dias) = `GerarRecallAsync` com a modalidade do combo; depois limpava os filtros e deixava modalidade + dias mínimos | "Gerar recall (N dias)" → `GerarRecallForm` (dias + modalidade); depois o mesmo: filtros limpos, modalidade e dias mínimos ligados, frase com novos/total |
| Recall gerado na 1ª abertura com os dias da configuração | Igual (`CarregarAsync`, uma vez por tela) |
| Expander "Entenda as situações e os próximos passos" | `<details>` no rodapé da lista |
| Expander "Visão da gestão" (só Gerente) | Botão "Por responsável" (só Gerente) → `EquipeAcompanhamentoForm` |
| Painel ao lado da linha: telefone, consentimento, mensagem prévia, contatos anteriores | A conversa: telefone, selo, trilho, **mensagem que vai sair**, ficha (convênio, sessões, pacote, autorização) e **histórico** |
| "Assumir acompanhamento" (gravava um contato com texto fixo) | **"Assumir" na própria linha**, um clique, sem texto (`AssumirAsync`) |
| "Abrir WhatsApp" / "Enviar pelo WhatsApp" (`ValidarContatoAsync` + wa.me) | "Falar no WhatsApp"/"Tentar de novo" na linha e "Abrir no WhatsApp" na conversa: valida no serviço, abre o wa.me e abre a conversa com o canal WhatsApp; se não abrir, copia o endereço e diz |
| — | "Copiar o telefone para ligar" (novo) |
| "Agendar sessão" / "Marcar horário" (`EditarAgenda`) | "Marcar horário" na linha (passo "Quer marcar") e na conversa, mesmas barreiras, via `IMarcacaoDeHorario`; "Quer marcar" grava e então abre a marcação |
| "Registrar contato…" → combo Resultado/etapa, Canal, Próximo contato, Motivo, Observação obrigatória (5 letras), Encerrar, Reabrir | "Como foi?": 5 cartões (Não respondeu · Pediu outra data · Quer marcar · Aguardando o plano · Não quer voltar) que já sabem a etapa e propõem a data; canal; motivo (legenda, preservado); observação **opcional** salvo para encerrar. Caso encerrado mostra a justificativa e o bloco **Reabrir** (justificativa + data) |
| "Responsável pelo contato: <usuário> (usuário conectado)" | "Registrado por <usuário> (usuário conectado)" no rodapé da conversa |
| Combo "Resultado / próxima etapa" livre (5 etapas) | Os desfechos cobrem as 5 etapas; "A contatar" volta pela **Reabertura** e pelo recall |
| Canal "Atualização administrativa" (sem canal) | "Sem contato" (só onde o desfecho admite) |
| Mensagens: "Acompanhamento salvo com histórico.", "Configuração salva…", "Motivo cadastrado…", "WhatsApp aberto…", contagem do recall | Notificação após gravar (frase com desfecho e próximo contato), confirmação inline no Configurar, frase do recall; recusas do serviço INLINE na conversa |
| Erro genérico "Não foi possível concluir…" com log | Igual (log em `Diagnostico`), e terceiro estado `FalhaDeLeitura` quando a fila não lê |
| Permissão da tela (`VerFichaPaciente` E `GerenciarCampanhas` OU `VerFaturamento`) | Igual nas duas barreiras; sem ela a tela DIZ por quê e não chama o serviço |
| Colunas: Paciente, Sem vir, Tratamento, Última sessão, Situação, Contato | Paciente (+ contexto), Sem vir/Indicado há, **Progresso**, Situação (selo), Responsável (avatar), **Próximo passo**. A última sessão e com quem ficaram no cabeçalho da conversa |

## 4. Desvios do mockup aprovado (e por quê)

1. **O desfecho não vem pré-escolhido** (2.3).
2. **"Aguardando o plano" é um quinto cartão** — o serviço tem a etapa e o mockup mostrava quatro;
   sem o cartão, a resposta da operadora não teria como ser registrada pela conversa.
3. **Canais incluem E-mail** — o enum tem o canal e a tela WPF oferecia; tirar seria perder capacidade.
4. **"Encerrados (N)"** ao lado do resumo: o funil do mockup não tem a faixa, e sem ela o caso
   encerrado (que se lê e se reabre) ficaria inalcançável.
5. **Cabeçalho com "Atualizar", "Por responsável" e "Configurar"** além de "Gerar recall" e
   "Exportar CSV" — vieram da tela WPF (2.2 da lista de paridade); os dois da gestão só aparecem
   para o perfil Gerente.
6. **"Gerar recall" só aparece na fila Recall** — na de BSV não faz sentido.
7. **O estilo da tela é ligado por `<link>` dentro da própria tela**, e não por `@import` no
   `clinica.css`, porque outra frente mexe naquele arquivo em paralelo. Quando as duas se
   juntarem, a linha pode subir para o `@import` (é uma linha).

## 5. Como se garantiu que nada se perdeu

1. A lista de paridade acima.
2. `ConversaDoAcompanhamentoTests` (13 testes) e `AcompanhamentoNaTelaTests` — as regras puras.
3. `RelacionamentoBlazorParidadeTests` clica a tela e confere o BANCO: "Assumir" na linha grava
   quem fez login; "Não respondeu" pela conversa grava Sem resposta, tentativa +1 e próximo
   contato em dois dias úteis; "Não respondeu" sem canal não grava nada e diz o que falta.
4. `TelasBlazorAbremTests` abre a tela com a semente `SementeAcompanhamento` (casos em todas as
   faixas, sem telefone, sem autorização, BSV, encerrado, atrasado, cancelou, agendado, voltou) e
   clica cada botão.
5. `tools/ValidarLayoutWindows --acompanhamento` passou a conferir pelo SERVIÇO o que era regra
   (a fila lê, o contato é assinado por quem fez login, a configuração não perde o médico, gerar o
   recall não esconde o caso novo, o recall da abertura acha a modalidade nova).
6. Fotos da prévia em 1024 e 1366, com a conversa, os filtros, o configurar e a visão por responsável abertos.

## 6. UX · DX · CX

- 🧑‍💻 **UX** — assumir é UM clique na linha; falar é WhatsApp → "Como foi?" → um clique
  registra (observação opcional); o progresso está no funil (da equipe, e é o filtro), no trilho
  de cada linha e no trilho da conversa. Filtros raros saíram da frente e o botão diz quantos
  estão ligados. A tela tem a cara do Financeiro e do Faturamento já aprovados.
- 🛠️ **DX** — o que decide (o passo, a faixa, o pedido do desfecho, o vazio) está na Application
  com teste; a tela é desenho. Uma interface pequena (`IMarcacaoDeHorario`) é a única ponte com a
  Recepção. A tela entra sozinha na prévia e nos testes (achada por reflexão), com a semente própria.
- 🤝 **CX** — os mesmos dados e o mesmo serviço, sem migração; nada do WPF se perdeu sem estar
  escrito acima; o contato registrado em segundos aumenta o registro real — e é ele que alimenta
  o "voltaram em 30 dias".
