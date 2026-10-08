# Frontend financeiro

HTML, CSS e TypeScript com Vite, Inter local e ícones SVG Lucide. A logo pertence à Clínica SemDor e é preservada. Recursos compilados em `../wwwroot`; licenças acompanham a distribuição.

## Comandos

`npm ci`, `npm run dev`, `npm run build`.

A prévia com `?demo=1` tem dados fictícios do resumo e não grava operações. Sem o host e sem demonstração explícita, a interface informa ausência de conexão. Para provar integração e paridade use `tools/validar-design-financeiro --web`, conforme README do diretório pai.

## Componentes e estado

- `main.ts`: shell visual, logo, navegação única, resumo, protocolo, foco e estado de diálogos.
- `paginas.ts`: campos tipados, ações, indicadores, tabelas, gráficos e seções de página.
- `style.css`: identidade do resumo e shell.
- `paginas.css`: identidade das demais páginas e formulários.

`estado.pagina` descreve a rota atual. `estado.dialogo` descreve o diálogo ativo. A serialização usa camelCase, conforme `ContratosFinanceiroWeb.cs`. Mensagens `pagina-campo`/`pagina-acao` incluem `contexto`, `chave`, `valor` quando aplicável, `tabela` e `linha` opcionais. Mensagens `dlg-campo`/`dlg-acao`/`dlg-fechar` incluem o ID do diálogo atual. O host resolve opções e linhas somente nas coleções autorizadas vigentes.

Todo texto de domínio passa por escape HTML. Gráficos numéricos usam pontos do host; a UI não calcula totais financeiros. Alterações de campos são descarregadas antes de salvar. Diálogos preservam foco, permitem teclado e mantêm ações no rodapé. Áreas excedentes oferecem controles de rolagem.

Nunca adicionar uma rota que encaminhe ao shell financeiro antigo. Login, componentes do sistema operacional e abertura de documentos são fronteiras externas; as páginas e formulários do módulo permanecem web.
