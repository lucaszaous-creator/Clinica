# Revisão final da agenda antes do merge — PR #205

Revisão da implementação do Modelo A a partir de `3fcdbfc`, em conjunto com Jev/TypeSafe. O fluxo de chegada, atendimento, evolução, conclusão, assinatura e faturamento permanece nos serviços e comandos existentes.

## Falhas corrigidas

| Achado | Evidência | Correção |
|---|---|---|
| Na visão por sala, o profissional selecionado não entrava no cálculo da coluna | Teste nativo falhou: sala A deveria oferecer 09h, enquanto sala B estava ocupada por outro médico | Cada coluna combina o profissional selecionado com sua própria sala. O painel lateral segue os filtros do cabeçalho, sem repetir a mesma vaga por sala |
| O atalho de marcar outro paciente no mesmo horário havia desaparecido | Teste nativo falhou ao procurar o comando numa ocupação futura com trava desligada | Botão `+` restaurado no bloco ocupado; abre o formulário existente. Não aparece com trava ativa e exige a permissão atual de edição |
| A leitura de bloqueios liberava a disponibilidade antes de terminar a consulta da semana; mudar um filtro nessa janela podia recalcular com dados parciais | Conferência da sequência de `await`; teste com interceptor SQL que pausa depois da consulta de bloqueios | A disponibilidade só é liberada na publicação conjunta dos horários e colunas. Marcar a leitura como não verificada remove imediatamente as vagas e seus rótulos de disponível |

## Participação do Jev

Duas consultas reais à TypeSafe retornaram o modelo `jev-1.13.0`. Foram enviados trechos de código e cenários fictícios. Na primeira, os sinais sobre sala e atalho tinham baixa confiança; a confirmação veio dos testes nativos. Na segunda, o modelo avaliou as correções de recursos, trava e ordem de publicação. As respostas não substituem a execução dos testes.

## Verificação executável

`tools/ValidarLayoutWindows` cobre:

- Duas salas e dois profissionais, com ocupação cruzada, para conferir a vaga de cada recurso.
- Ausência de horários duplicados no painel lateral.
- Atalho de sobreposição presente com trava desligada e ausente com trava ativa.
- Leitura da semana pausada depois dos bloqueios: mudar a duração não oferece disponibilidade parcial.
- Estado não verificado removendo as vagas, inclusive quando a duração não muda.
- Contexto preservado entre dia/semana e abertura do formulário com profissional, data, horário e duração.
- Contagem dos agendamentos inalterada ao consultar/escolher vaga.
- Layout entre 880 e 1.920 pixels e bindings WPF sem erros.

Antes da correção, a execução falhou nos dois primeiros cenários de interface. Depois, a rotina terminou com `TELAS CONFERIDAS` e log de bindings vazio. Na rodada local final: **2.854 testes aprovados**, compilação de sombra dos **11 projetos** aprovada e verificador estático aprovado (**217 XAML, 10 projetos e 136 construtores**). O CI da revisão deve estar aprovado no commit efetivamente incorporado.

## Escopo e evidências anteriores

- [Implementação visual, fotos e cobertura das 12 recomendações](IMPLEMENTACAO-MODELO-A.md).
- [Revisão anterior da navegação, ficha, cadastro, recebimentos e Faturamento](../../revisao-pr205.md).
- A revisão não acrescenta migrations, regras de conclusão clínica ou automações de atendimento.
- As ampliações funcionais já discriminadas na matriz da implementação, como jornadas diferentes por dia e múltiplos intervalos, permanecem registradas como pendentes. Esta revisão não declara essas propostas implementadas.
- Merge na `main`, resultados do CI e distribuição aos computadores são etapas distintas. O estado final do merge deve ser conferido na [PR #205](https://github.com/lucaszaous-creator/Clinica/pull/205).
