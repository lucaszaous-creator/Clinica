# Financeiro web completo

A apresentação do executável Financeiro é HTML/CSS/TypeScript local em WebView2. As 15 rotas financeiras, seus detalhes, filtros, operações e formulários permanecem na mesma interface. C# mantém autenticação, permissões, cálculos, serviços e persistência.

## Executar

Na pasta `Web/frontend`, execute `npm ci` e `npm run build`. Na raiz, `dotnet run --project src/Clinica.Financeiro` usa a configuração e autenticação normais. **Não use esse comando para testar contra a clínica**: para testes isolados, use o harness abaixo.

O instalador inclui WebView2 como pré-requisito e todos os assets em `Web/wwwroot`; não há servidor web ou CDN. Falha no componente visual mostra instrução de reparo. A navegação financeira não abre o shell ou formulários WPF antigos. Os demais executáveis ainda podem usar suas views nativas, preservadas durante esta migração.

`npm run dev` com `?demo=1` mostra uma demonstração visual fictícia do resumo. Essa demonstração não prova integração nem paridade do módulo; a evidência de integração vem do WebView2 real com banco sintético.

## Rotas

Caixa, contas a pagar/receber, inadimplência, plano de contas, fluxo de caixa, resultado e teto de despesas, fechamento, recebíveis de cartão, conciliação de receitas, extrato bancário, produção, pacotes, estoque, repasses e taxas/tributos. As antigas abas são seções acessíveis na própria página, com atalhos de seção, formulários e ações correspondentes.

## Arquitetura

- `FinanceiroWebView`: origem local restrita, sessão, fila de mensagens, estado e ciclo de vida do navegador.
- `FinanceiroPaginasController` e registro: DTOs explícitos e propriedades/comandos permitidos para cada rota. Contexto de navegação e IDs opacos impedem usar ações de uma tela anterior ou registros fora da lista atual.
- `DialogosFinanceiroController` e catálogo: formulários com os ViewModels existentes, campos permitidos, validação, pilha de diálogos filhos e conclusão assíncrona. Cada diálogo possui um ID próprio.
- `DialogosDaSessao` no shell: apresentador assíncrono restrito ao fluxo de execução. Mantém a apresentação nativa dos outros aplicativos e utiliza o apresentador web nas operações do Financeiro.
- `frontend/src/paginas.ts`: componentes compartilhados de campos, indicadores, tabelas, gráficos, ações e páginas; `main.ts` mantém o resumo visual, navegação e protocolo; CSS e fontes locais.

A página envia `pagina-campo`/`pagina-acao` com contexto e chaves registradas. Diálogos usam `dlg-campo`, `dlg-acao` e `dlg-fechar` com ID vigente. Nenhum nome arbitrário de método ou propriedade recebido do navegador é refletido no modelo. Confirmações, erros e cancelamento continuam explícitos. O canal de um diálogo filho permanece disponível enquanto o comando pai aguarda sua resposta.

Seletores de arquivo do Windows e documentos exportados podem abrir fora da interface; isso não encaminha a operação para uma tela financeira antiga.

## Verificação sem produção

```powershell
dotnet run --project tools/validar-design-financeiro -c Release -- --web
```

O harness usa SQLite em memória, serviços reais e os assets distribuídos no WebView2. Confere rotas em três dimensões, formulários, persistência/cancelamento e contexto/permissões. Capturas ficam em `artifacts/design-financeiro/capturas`. Requer o WebView2 Runtime.

O resultado do resumo é líquido do mês, não saldo bancário. Gráficos usam séries reais do host; ausência de dado não vira ponto zero inventado. Valores e rótulos acessíveis acompanham os gráficos.

O padrão reutilizável e a definição de migração completa estão no `AGENTS.md` da raiz.
