> **Atualização da PR #205:** consulte a [implementação do Modelo A com capturas WPF reais e cobertura item a item](IMPLEMENTACAO-MODELO-A.md). Esta página preserva o diagnóstico e os protótipos históricos; as ampliações funcionais pendentes estão discriminadas no novo relatório.

# Mockups visuais — módulo Clínico e módulo Recepção

**Escopo corrigido: apenas aparência. O fluxo atual permanece.** Esta versão substitui a proposta anterior de reorganização do fluxo do dia. As imagens são capturas de mockups HTML no navegador, com dados fictícios; não são telas já implantadas no WPF.

## Módulo Recepção — Agenda do dia

![Mockup visual dentro do módulo Recepção](propostas/06-fluxo-recepcao.png)

A apresentação mantém o conteúdo e as ações da tela atual:

- Cabeçalho **Agenda do dia**, navegação de data, **Hoje** e **Atualizar**.
- Resumos de atendidos, em atendimento e falta/cancelamento.
- Filtros atuais por profissional.
- **Marcar atendimento** e **Novo horário**.
- Colunas **Horário**, **Paciente / atendimento**, **Profissional**, **Status** e **Ações**.
- Botões **Chegou**, **Concluir**, **Debitar pacote**, **Editar** e **⋯**, conforme o estado e as permissões já existentes. O cenário da imagem não contém pacote pendente, por isso não exibe “Debitar pacote”.
- O menu **⋯** conserva as ações atuais e seus destinos, incluindo ficha, termos, fechamento, convênio/cota, falta, cancelamento e grade quando disponíveis.

### O que muda visualmente

Título e botões deixam de disputar espaço; indicadores ficam alinhados; nomes e horários ganham hierarquia; ações mantêm alinhamento constante; a linha selecionada recebe destaque discreto. Os textos dos status permanecem visíveis. A paleta segue o Modelo A.

## Módulo Clínico — Meu dia

![Mockup visual dentro do módulo Clínico](propostas/07-fluxo-medico.png)

A apresentação mantém o conteúdo e as ações da tela atual:

- Cabeçalho **Meu dia**, profissional, navegação de data, **Hoje** e **Atualizar**.
- Botão de sessões sem evolução com o rótulo atual; sem pendências, continua **Prontuário em dia**.
- Colunas **Horário**, **Paciente**, **Status**, **Prontuário** e **Ações**.
- Prontuário **Escrito** ou **Pendente**, segundo o cálculo existente.
- **Atender** ou **Ver registro**, exatamente como os comandos atuais definem. A proposta não introduz “Retomar atendimento” nem “Chamar”.

### O que muda visualmente

A tabela recebe espaçamento mais confortável, tipografia consistente, alinhamento dos botões e maior distinção entre nome, modalidade, status e prontuário. A seleção e os estados usam a mesma linguagem visual da Recepção, mantendo a identidade do módulo Clínico.

## Limite desta revisão

| Item | Tratamento |
| --- | --- |
| Espaçamentos, alinhamento e hierarquia tipográfica | Ajuste visual proposto |
| Cores, bordas, fundo e destaque da seleção | Modelo A |
| Nomes, comandos, estados e permissões | Mantidos conforme a implementação atual |
| Etapas clínicas e administrativas | Sem alteração |
| Evoluções, encerramento, conclusão, regras de enfermagem | Sem alteração |
| Destinos dos botões e navegação funcional | Sem alteração |
| Novas colunas de pendência, painel lateral e novos filtros propostos anteriormente | Retirados destes mockups |

A navegação ao redor da tela representa o contexto de cada módulo; as abas continuam condicionadas aos módulos carregados e às permissões existentes. O texto “Módulo Clínico”/“Módulo Recepção” identifica claramente qual mockup está sendo mostrado. Os botões são ilustrativos e não gravam dados.

## Conferência

Os rótulos e ações foram conferidos em `FilaView.xaml`, `FilaViewModel.cs`, `MeuDiaView.xaml`, `MeuDiaViewModel.cs` e `OrganizacaoNavegacao.cs`. O Jev (`jev-1.13.0`, consulta real TypeSafe) avaliou a descrição revisada como aderente ao escopo exclusivamente visual; não realizou inspeção das imagens. Codex conferiu as capturas no navegador.

**Entrega desta revisão:** mockups e documentação. Nenhum arquivo de aplicação, regra ou banco foi alterado.

[Relatório geral](DIAGNOSTICO-AGENDA.md) · [Galeria](README.md)
