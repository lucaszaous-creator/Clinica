# CRM com cara de WhatsApp — F1b: corretor, acesso da direção, notificação e atalhos (24/11/2026)

O dono aprovou em 24/11/2026 as propostas v2, v3 e v4. Esta frente é a F1b, que mexe no host, no shell e na regra de acesso. A tela é a F1, do Design SemDor; o servidor do CRM e o contrato são a F2, do Arquiteto. O plano das frentes está em `Shared/Clinica/crm-ux/IMPLEMENTACAO.md`.

## O que entra

| # | O quê | Onde | Teste |
|---|---|---|---|
| 1 | **Corretor ortográfico em pt-BR**. O menu de contexto do navegador só aparece em campo editável, e só com as sugestões, "Adicionar ao dicionário", recortar, copiar e colar. Fora de campo continua sem menu. O idioma do ambiente WebView2 é `pt-BR`, o que vale para os menus, o `accept-language` e o dicionário. | `Ui.Blazor/Hospedagem/MenuDoCorretor.cs` (regra pura); `Desktop.Shell/Shell/NavegadorEmbutido.cs` (`PrepararAmbiente`, `ConfigurarNavegador`, `ContextMenuRequested`); `Desktop.Blazor/JanelaDeFormularioBlazor.cs` usa as mesmas duas | `MenuDoCorretorTests` |
| 2 | **Decisão 4**: a aba WhatsApp e a entrada no CRM dependem só do bit `EditarAgenda`, em qualquer perfil | `Domain/Entities/AcessoAoWhatsApp.cs`. As portas (menu, tela, conversa ao lado, sino, `ClinicTeamBridge.PodeEntrar`) já chamavam essa regra | `AcessoAoWhatsAppTests`, `PonteClinicTeamTests.Bit_de_agendar_em_qualquer_perfil_entra_no_CRM_e_sem_ele_nao`, `ConversaAoLadoTests` |
| 3 | **Notificação do Windows** a cada mensagem nova de paciente com o app em segundo plano, e o **número de não lidas no ícone** da barra de tarefas (Recepção e Gerente) | `Application/Servicos/VigiaDoWhatsApp.cs` (`Chegaram`), `NotificacaoDoWhatsApp.cs` (texto); `Desktop.Shell/Shell/AvisosDoWindows.cs` (Win32 + `TaskbarItemInfo.Overlay`), ligado em `EsperaNoWhatsAppShell.cs` e `JanelaDoApp.cs` | `NotificacaoDoWhatsAppTests` |
| 4 | **Atalhos** Ctrl+F (busca da tela), Ctrl+Alt+N ("novo" da tela) e Esc (fechar), entregues à tela que os declara | `Ui.Blazor/Ativos/js/janela.js` (`clinicaAtalhos.telaPede`), `Hospedagem/SinalDeVolta.cs` (`AtalhoDeTela.Buscar/Nova/Fechar`) | `AtalhosDaTelaTests` (o script e o enum casados) |

## O que o Design SemDor liga na tela

1. **Atalhos.** Duas coisas:
   - no elemento raiz da aba (`<div class="pagina wa …">`), o atributo `data-atalhos="buscar nova fechar"`;
   - no código da tela, `override AoAtalhoAsync`:
     - `AtalhoDeTela.Buscar` → abre a busca na conversa (`AlternarBusca` + foco), ou a busca da lista se não há conversa aberta;
     - `AtalhoDeTela.Nova` → `AbrirNovaConversa()`;
     - `AtalhoDeTela.Fechar` → fecha, nesta ordem e uma coisa por vez: popover, formulário da tela, busca na conversa, conversa.

   O Esc **não é engolido**. Ele continua chegando ao campo, por exemplo ao `TeclaAsync` do textarea, que hoje fecha o popover com Esc. Então o tratamento deve ficar num lugar só: ou o `Fechar`, ou o campo. Com pergunta (`Dialogos`) ou formulário avulso aberto, o `PalcoDeTelas` não repassa atalho nenhum.
2. **Corretor:** nada a ligar. O `<html lang="pt-BR">` já existe, e o textarea da conversa não desliga o `spellcheck` (o padrão do textarea é ligado). Se um campo novo da F1 levar `spellcheck="false"`, ele perde o corretor; isso só é certo em busca, login e telefone.
3. **Notificação e número no ícone:** nada a ligar. Vêm do shell, a partir da mesma leitura do número do menu.

## 🧑‍💻 UX

- A palavra sublinhada em vermelho agora tem conserto com o botão direito, e as sugestões são de português. O resto do app continua sem o menu do Edge ("Inspecionar", "Salvar como"…), que não é do sistema.
- O diretor que entra como Médico passa a ter a aba WhatsApp quando a direção marca "Marcar e remarcar" para ele em Acessos.
- Com o app minimizado ou atrás de outra janela:
  - a mensagem de paciente chega como notificação do Windows ("Maria mandou mensagem.");
  - o botão na barra de tarefas mostra a bolinha vermelha com o número de não lidas (o mesmo do menu);
  - clicar na notificação traz a janela e abre a conversa.
- A notificação diz **quem** escreveu, nunca **o quê**: a tela do balcão fica à vista de quem está na recepção, e a mensagem pode falar de saúde.

## 🛠️ DX

- Peças novas:
  - `MenuDoCorretor`, `NotificacaoDoWhatsApp`, `AvisosDoWindows`;
  - três valores no `AtalhoDeTela`;
  - um ouvinte de teclado no `janela.js`.
- As regras moram na Application/Ui.Blazor, puras e com teste. O shell só chama o Windows.
- **Por que Win32 e não o "toast" do Windows App SDK:** o toast pede `net8.0-windows10.0.x` nos cinco apps e o registro da identidade do app. O `NotifyIcon` do WinForms traria o `System.Windows.Forms` para dentro do shell WPF (nomes ambíguos), e o `compilar-sombra` não o compila. O `Shell_NotifyIcon` por P/Invoke não precisa de pacote nenhum e compila na sombra.
- **O ambiente do WebView2 é um só** (`NavegadorEmbutido.PrepararAmbiente`). A janela do app e a de formulário avulso dividem a pasta do perfil, e o WebView2 recusa abrir a mesma pasta com opções diferentes. Quem criar um terceiro navegador usa as mesmas duas funções.
- O atalho novo de tela vale só por **declaração** (`data-atalhos`): tela que não declara não recebe `Buscar`, `Nova` nem `Fechar`, então nenhuma tela existente muda.

## 🤝 CX

- Nenhum pacote, licença ou servidor novo. A notificação usa a leitura de 15 s que o vigia já fazia, sem chamada extra ao CRM.
- **Risco em produção, do acesso:** quem ganhou `EditarAgenda` em Acessos sem ser da recepção, como a faturista que agenda (parcela 58), passa a ver o WhatsApp também. Os perfis-padrão não mudam, porque só Recepção e Gerente nascem com o bit. Vale a direção conferir em Acessos quem tem "Marcar e remarcar".
- **Para valer no CRM web**, a ponte precisa ser publicada na VPS 1, porque a regra de quem entra no CRM é a dela.
- Publicar: apps **Recepção e Gerente** (aba WhatsApp, notificação, atalhos, corretor). O corretor e os atalhos também chegam ao Financeiro e ao Consultório, porque o navegador é o mesmo da suíte. O faturamento não muda.

## O que não foi feito

- **Não testado num Windows de verdade.** Ficaram sem teste a notificação, a bolinha no ícone, o menu do corretor e o idioma do dicionário. O Linux daqui não roda WPF nem WebView2. O que foi provado: a regra pura com teste, a compilação do C# WPF na sombra e o comportamento do script de atalhos num navegador.
- O WebView2 usa o corretor do Windows quando ele tem o idioma. Se a máquina do balcão não tiver o português instalado no Windows, a sugestão pode não aparecer. O conserto é instalar o pacote de idioma, não código.
- O nome do item "Adicionar ao dicionário" vem da documentação do WebView2: todo item do corretor se chama `spellCheck`. O submenu das opções do corretor fica de fora de propósito.
- A notificação fica de fora com o app **em primeiro plano**: quem olha a tela já vê a lista e o número.
- **Regra da notificação:** vale para toda conversa que a pessoa vê, não só as dela. É a mesma régua do número do menu. Restringir a "minhas e sem dono" é uma linha em `VigiaDoWhatsApp.QueChegaram`.
- A ligação dos atalhos na tela é do Design SemDor (seção acima).
