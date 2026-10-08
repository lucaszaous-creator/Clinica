# Validação visual do Financeiro

Executar em Windows com o SDK .NET 8 e suporte WPF:

```powershell
dotnet run --project tools/validar-design-financeiro/Qa.csproj
```

O programa abre o shell e as views reais fora da área visível, com serviços da aplicação sobre SQLite em memória. Usa apenas registros fictícios; não inicia `SuiteApp`, lê configurações de conexão ou executa cobranças externas. Os comandos de confirmação e gravação dos diálogos não são acionados.

As capturas e os relatórios ficam em `artifacts/design-financeiro/capturas` (ignorado pelo Git). São imagens do WPF, não maquetes. A execução falha quando encontra uma exceção, comando sem resolução, logo ausente, tabela sem área útil ou conteúdo que não pode ser rolado.

Cobertura:

- 15 rotas do módulo, com janelas de 1440×900, 1100×720 e 900×600; abas internas e setas de rolagem.
- Busca e limpeza dos filtros do Caixa.
- 15 diálogos, alcance dos campos e visibilidade dos botões de confirmação/cancelamento.
- Séries do Caixa com 299, 300 e 301 lançamentos, valores conhecidos, filtros e troca de mês. Cargas incompletas não desenham séries parciais como se fossem completas.

Para uma verificação específica, usar `-- --dialogos` ou `-- --caixa`. A primeira verifica os diálogos; a segunda abre o Caixa em 900×600.

Usar `-- --web` para verificar a interface HTML/CSS empacotada no WebView2, com o mesmo banco sintético. Antes, executar `npm ci` e `npm run build` em `src/Clinica.Modulo.Financeiro/Web/frontend`. Esse caminho captura o navegador embutido em três tamanhos, exercita os filtros e a troca de mês pelo bridge, e confere restrições de navegação.

Os dados demonstrativos não cobrem todas as combinações de regras de negócio. Os testes de domínio e serviços permanecem na suíte `Clinica.Tests`.
