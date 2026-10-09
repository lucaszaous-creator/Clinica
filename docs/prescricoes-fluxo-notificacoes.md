# Prescrições, troca de paciente e notificação

Base: `main` em `74cbf48b` (PR 246 já integrada). Escopo solicitado: reconstruir as telas clínicas **Receitas e documentos** e **Prescrição de infusão**, melhorar seleção/troca de paciente e corrigir a notificação encoberta pelo conteúdo web.

## Interface e fluxo

- React/TypeScript, HTML e CSS locais com Inter, SVG e cores da clínica. A identidade e navegação do shell permanecem as do projeto.
- Busca ampla por nome/CPF, resultados legíveis, identificação persistente e botão **Trocar paciente**. **Manter paciente** cancela a troca; o contexto anterior só muda ao escolher uma pessoa.
- Criação de documentos em cartões do catálogo existente. Infusão com **Nova prescrição** e **Copiar última**. Histórico com busca, tipo/situação, contagem, colunas alinhadas, ações e estados de carregamento, erro e vazio.
- Setas de rolagem mantidas com espaço reservado. Layout conferido em 1420, 1100, 900 e 650 px, sem vazamento horizontal.
- O host só aceita IDs da lista/contexto atual. Resultado de busca anterior não pode selecionar um paciente depois de uma nova digitação. Erro tardio de consulta cancelada também é descartado.
- Na ficha de paciente, o seletor interno fica indisponível; não troca silenciosamente o contexto das demais seções.

## Paridade e limites

Os seis tipos vêm de `CentralDocumentosService.Catalogo`, com as permissões de cada folha. Foram preservados imprimir, assinar, enviar, renovar link, retirar link e cancelar documento; editar/abrir/imprimir/registro histórico/cancelar infusão; nova prescrição e cópia da última. Menus exibem as ações habilitadas pelo estado e acesso atual.

Esta entrega reconstrói as duas telas de seleção/criação/histórico. **Não é migração integral do módulo clínico**: emissão e assinatura de documentos continuam nos componentes existentes; PDF e confirmações continuam nos serviços da suíte. O editor de infusão já usa React e continua em sua janela. A abertura desse editor revelou reentrada no evento nativo WebView2: comandos agora saem dessa pilha antes de abrir um diálogo, mantendo o bloqueio contra duplo clique e a revalidação da sessão.

## Notificação

O Border WPF ficava atrás do HWND do WebView2. O novo `SnackbarPopup` usa uma janela própria sem topmost global nem ativação, acompanha o shell ativo e fecha ao minimizar/desativar. Mensagem, duração de quatro segundos, histórico e contador usam o mesmo serviço.

## Evidências

Capturas das views reais e banco SQLite exclusivamente fictício:

- [Receitas e documentos](evidencias/prescricoes-2026-10-09/documentos.png)
- [Prescrição de infusão](evidencias/prescricoes-2026-10-09/infusoes.png)
- [Busca de paciente](evidencias/prescricoes-2026-10-09/busca-paciente.png)
- [Notificação integral acima do WebView2](evidencias/prescricoes-2026-10-09/notificacao.png)

Validação local em Windows:

- `npm run build`: TypeScript e Vite aprovados; recursos gerados empacotados no aplicativo.
- `tools/validar-prescricoes`: WebView2 real, busca/seleção/troca/cancelamento da troca, filtros, menus, layout, contexto obsoleto, busca atrasada, erro real de leitura e recuperação, vazio, acesso negado, abertura do editor de infusão no paciente correto e fechamento sem gravar.
- `tools/validar-notificacao-web`: captura da composição real do Windows, aviso integral sobre WebView2, foco, ausência de topmost, movimento, redimensionamento, minimizar/desativar/restaurar, expiração e histórico.
- `tools/validar-agenda-documentos`: regressão das agendas médico/recepção e editor de infusão aprovada após a mudança no host compartilhado.
- Testes de documentos, prescrições, busca e contexto: **295 aprovados**, zero falhas.
- `verificar-suite.py`: **226 XAML, 10 projetos e 136 construtores** aprovados.
- `compilar-sombra.py`: C# dos **11 projetos WPF** aprovado.

Um agente auxiliar implementou e validou a notificação, depois revisou contexto, busca e paridade; os dois achados concretos foram corrigidos (erro tardio da busca e descrição de link vencido). A revisão React incluiu foco, rótulos, estados, limpeza de listeners e recursos locais.

O **Jev 1.13.0 aprovou interface, fluxo e notificação** com base em código e logs sintéticos: [respostas e hashes](evidencias/prescricoes-2026-10-09/jev.json). Essa aprovação é de revisão de código/evidências; a inspeção visual e execução da interface foram locais, não feitas pelo Jev.

Nenhuma alteração de esquema, envio ao paciente, assinatura real ou publicação de release faz parte desta entrega. O novo QA de prescrições foi incluído no build Windows da PR. O QA de composição da notificação requer desktop interativo e permanece executado localmente.
