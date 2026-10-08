# Orientações para agentes

Leia também `CLAUDE.md` para as regras de negócio, arquitetura, testes e publicação da suíte. Código, comentários, interface e commits são em português (pt-BR).

## Padrão de interface iniciado pelo Financeiro

A referência de implementação é `src/Clinica.Modulo.Financeiro/Web/`. Este padrão deve orientar futuras migrações de outros módulos quando solicitadas pelo proprietário.

- A apresentação é HTML/CSS/TypeScript local, empacotada com o aplicativo e exibida em WebView2. O C# mantém autenticação, permissões, regras, serviços, auditoria e persistência. Não reproduzir cálculos financeiros ou regras clínicas no navegador.
- Migrar o fluxo inteiro de um módulo: todas as rotas, abas, filtros, tabelas, detalhes, formulários, seletores, confirmações, histórico, edição e estados de erro. Uma página web que encaminha suas funções para telas WPF antigas não é uma migração concluída.
- Uma janela e uma navegação consistentes. As rotas internas trocam o conteúdo web; formulários e confirmações usam diálogos web. Seletores de arquivo do sistema operacional e abertura de documentos exportados podem continuar nativos.
- Preservar a logo original da Clínica SemDor. Usar Inter local, ícones SVG Lucide locais e os tokens CSS do Financeiro. Não usar emoji nem glifos dependentes da fonte do Windows como ícones de interface.
- Aparência: superfícies brancas, fundo neutro muito claro, bordas discretas, sombras leves e cantos arredondados. Azul da clínica para a ação principal; verde/vermelho com significado, sempre acompanhado de texto. Títulos, indicadores, tabelas e formulários têm hierarquia e espaçamento consistentes.
- A navegação fica no topo, com subopções ao passar o mouse sobre cada grupo, conforme pedido explícito do proprietário em 08/10/2026. Não usar navegação lateral. Manter também abertura por clique e teclado, fechamento por Escape, estado ativo e rótulos acessíveis. Todos os destinos autorizados precisam permanecer alcançáveis; evitar menus redundantes.
- Componentes compartilhados de página, campo, indicador, tabela, ação e diálogo mantêm consistência. Cada tela precisa de conteúdo e ações adequados à sua tarefa, sem perder funções existentes para caber num componente genérico.
- Notebook é cenário principal: campos legíveis, foco visível, navegação por teclado, rótulos associados e botões de rolagem nas áreas com conteúdo excedente. Tabelas podem rolar dentro de sua região; a página não deve vazar horizontalmente. Diálogos precisam manter cancelar/salvar acessíveis.
- O contrato com C# é explícito e limitado às rotas, campos e ações registrados. Validar autorização no host em cada operação, resolver IDs na lista/contexto atual e rejeitar nomes arbitrários de propriedade ou método enviados pelo cliente. Nunca expor credenciais, conexão de banco, objetos .NET ou execução de código ao HTML.
- Revalidar a sessão, impedir dupla gravação, sinalizar carregamento/erro/resultado e preservar o formulário ao falhar. Cancelar um diálogo nunca equivale a confirmar. Navegação e respostas atrasadas não podem aplicar dados ao contexto errado.
- Todos os recursos visuais são locais, sem CDN. O modo de demonstração é explícito, contém somente dados fictícios e nunca substitui silenciosamente uma falha da aplicação real.
- A apresentação dos cinco módulos usa React + TypeScript + CSS desde a revisão da PR 245. Componentes compartilhados ficam em `Clinica.Desktop.Shell/Web/frontend/src/*react*`; o resumo financeiro tem JSX próprio. Preservar as chaves por contexto/campo/registro e o contrato `data-*` da ponte. Não voltar a substituir o HTML inteiro a cada resposta. Controles especializados mantêm adaptadores explícitos; consulte `docs/react-pr245.md`. Compilar o frontend compartilhado antes do Financeiro. Movimentos são curtos, respeitam `prefers-reduced-motion` e não reiniciam ao atualizar resultados de busca.

## Evidência de entrega

Antes de publicar uma migração, conferir a paridade com o inventário de rotas e ações existente. Exercitar navegação e operações com WebView2 real e banco sintético, incluindo criação/edição/cancelamento, validações, acesso negado, estados vazios e tamanhos de notebook. Capturas de uma única página ou testes que apenas comprovam compilação não demonstram que um módulo inteiro foi migrado.

Manter as verificações determinísticas do repositório, CI e PR antes de integrar à `main`. Publicar apenas os canais autorizados e conferir o instalador e o índice de atualização. Relatar separadamente revisão de código, inspeção visual, testes executados e publicação; não atribuir aprovação a um agente ou ao Jev sem resposta efetiva e escopo documentado.
