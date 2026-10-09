# Verificação visual no Windows

Execute na raiz do repositório:

```powershell
dotnet run --project tools/ValidarLayoutWindows
```

As telas com filtros React precisam do runtime WebView2 e dos assets locais compilados
em `src/Clinica.Desktop.Shell/WebClinica/frontend` (`npm ci` e `npm run build`).
O CI prepara o runtime antes de executar qualquer verificação de layout.
O harness aguarda os painéis web carregarem antes das capturas e exige vagas clicáveis
na grade original, sem precisar abrir uma aba adicional.

A rolagem é exercitada com o template compartilhado: quatro setas vetoriais com clique
e repetição, clique no trilho, arraste nas duas orientações, roda do mouse e tecla PageDown
devem mover o conteúdo. O thumb permanece visível também em regiões de rolagem pequenas.

Use `--retornos` para conferir somente “Retornos a marcar”, com dados fictícios,
nos perfis Recepção e Gerente, em 880, 1024, 1366 e 1920 pixels. A verificação
exige os botões Marcar horário e WhatsApp visíveis, sem cortes ou rolagem horizontal.

Use `--a1` para capturar a escolha do arquivo A1 com o formulário expandido em 780 e
960 pixels, verificar ações visíveis e limpeza da senha ao fechar. Nenhuma assinatura
é realizada e os certificados da máquina não são exibidos nas capturas.

`--completo` percorre todas as telas e subabas publicadas pelos quatro módulos que usam o shell.
O faturamento tem janela e recursos próprios e precisa de conferência separada.

O programa abre janelas fora da tela com a aplicação WPF básica, os recursos reais e SQLite
em memória. Não executa o bootstrap, não lê a conexão salva e não usa pacientes reais.

Redimensiona cada tela entre 1920, 1366, 880, 960 e 1024 pixels lógicos. Confere controles
fora da largura e colunas que exigem rolagem horizontal. Nas listas React confere a área útil
do navegador e o acesso às ações ao rolar. Na grade, busca um nome ausente, preserva vagas
e bloqueios e restaura os cartões ao limpar. Grava imagens e erros de binding em `artifacts/layout-windows`.
Os dados incluem nomes longos e uma agenda preenchida. A aprovação destas verificações
não substitui a inspeção visual, a interação com os menus nem os testes de regras clínicas.
