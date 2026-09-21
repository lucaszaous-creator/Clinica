# Verificação visual no Windows

Execute na raiz do repositório:

```powershell
dotnet run --project tools/ValidarLayoutWindows
```

Use `--retornos` para conferir somente “Retornos a marcar”, com dados fictícios,
nos perfis Recepção e Gerente, em 880, 1024, 1366 e 1920 pixels. A verificação
exige os botões Marcar horário e WhatsApp visíveis, sem cortes ou rolagem horizontal.

`--completo` percorre todas as telas e subabas publicadas pelos quatro módulos que usam o shell.
O faturamento tem janela e recursos próprios e precisa de conferência separada.

O programa abre janelas fora da tela com a aplicação WPF básica, os recursos reais e SQLite
em memória. Não executa o bootstrap, não lê a conexão salva e não usa pacientes reais.

Redimensiona cada tela entre 1920, 1366, 880, 960 e 1024 pixels lógicos. Confere controles
fora da largura, colunas que exigem rolagem horizontal, espaço perdido na agenda e ações
cortadas pela célula. Grava imagens e erros de binding em `artifacts/layout-windows`.
Os dados incluem nomes longos e uma agenda preenchida. A aprovação destas verificações
não substitui a inspeção visual, a interação com os menus nem os testes de regras clínicas.
