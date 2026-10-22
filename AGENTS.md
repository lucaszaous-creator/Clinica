# Orientações para agentes

As regras deste repositório estão no **`CLAUDE.md`** — leia-o primeiro, qualquer que seja o agente:
comandos, onde os testes rodam (no CI do PR), como publicar, as regras de UX · DX · CX, a lista de
conferência na escrita, os congelamentos e a arquitetura. O conhecimento por área (regras de negócio
e convenções de implementação) está em `docs/agentes/`, com índice no topo de cada arquivo.

Código, comentários, interface e commits são em português (pt-BR).

## Telas

Tela nova é **Blazor** (`src/Clinica.Ui.Blazor`), hospedada pelo WPF num único navegador por janela —
o guia é `docs/telas-blazor.md`. O padrão antigo de páginas em HTML/TypeScript conversando por
mensagem com o C# (a pasta `Modulo.Financeiro/Web`, que não existe mais) foi abandonado em out/2026,
quando o Financeiro inteiro virou Blazor: não o reintroduza.

## Evidência de entrega

Antes de integrar uma mudança de tela à `main`, mostre que ela FUNCIONA, não só que compila:

- a tela aberta e cada botão clicado (`TelasBlazorAbremTests`, que roda no CI do PR) e, quando a
  mudança tem comportamento próprio, um teste bUnit da tela sobre o serviço de verdade;
- a prévia (`tools/PreviaBlazor`) ou o app com banco sintético para o que só um navegador mostra
  (leiaute, teclas, arrastar), em tamanho de notebook;
- criação, edição, cancelamento, validação, acesso negado e estado vazio, quando a tela os tem.

Mantenha as verificações do repositório e do CI verdes antes de integrar à `main`. Publique só os
apps afetados e só quando o dono pedir, e confira que o release terminou. Relate separadamente o
que foi revisado, o que foi visto no navegador, os testes que rodaram (e onde) e o que foi publicado;
não atribua aprovação a ninguém sem resposta efetiva e escopo documentado.
