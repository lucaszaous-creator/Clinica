# Financeiro web

Frontend local em HTML, CSS e TypeScript. O host WPF/WebView2 continua responsável por sessão, permissões, persistência e diálogos de operações financeiras.

## Executar e compilar

```powershell
npm ci
npm run dev
npm run build
```

O build é escrito em `../wwwroot`. A logo, as fontes Inter e os SVG Lucide são locais; não há CDN ou API externa. As licenças de terceiros acompanham o build.

Para visualizar no navegador sem o host, abrir `http://127.0.0.1:5173/?demo=1`. O modo demonstração é identificado na interface e suas operações não gravam dados. Sem WebView2 e sem esse parâmetro, a interface mostra que está desconectada. Não há fallback para dados fictícios quando o host falha.

## Integração

Ao iniciar, o frontend envia `{ acao: 'pronto' }` por `window.chrome.webview.postMessage`. O host publica mensagens `tipo: 'estado'`; os tipos estão em `src/main.ts`. Os valores monetários são recebidos formatados em pt-BR. Os gráficos usam os caminhos SVG calculados no host, em viewBox `0 0 280 52`, exclusivamente quando `serieDisponivel` é verdadeiro.

As ações enviadas são `mes`, `filtrar`, `atualizar`, `novo`, `pix`, `exportar`, `historico`, `realizar`, `recibo`, `cancelar`, `navegar` e `sistema`. Os IDs de linha são strings e precisam ser validados pelo host contra os lançamentos da sessão. O menu contém apenas as rotas que o host publicou. As outras seções abrem as telas existentes do aplicativo; este frontend implementa o resumo financeiro e sua lista de movimentos.

O resultado principal é líquido: receita após deduções menos saídas realizadas. O projetado é bruto e soma movimentos realizados e previstos, conforme `ResumoCaixa.SaldoPrevisto`. Nenhum deles é apresentado como saldo bancário.

Seletores de QA: `resumo-financeiro`, `mes`, `filtro-lancamentos`, `tabela-lancamentos`, `valor-resultado` e `alternar-privacidade` em atributos `data-testid`.
