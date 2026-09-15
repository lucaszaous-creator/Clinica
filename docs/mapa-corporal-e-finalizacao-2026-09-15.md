# Mapa corporal e conclusão da sessão — 15/09/2026

## Uso pela equipe

O mapa corporal compartilhado pelo módulo Clínico e pelo Gerente Geral passou a oferecer, logo no início da tela:

- **Copiar mapa:** escolha uma sessão anterior do mesmo paciente e copie os pontos, técnicas e observações. A sessão de origem permanece intacta. A lista considera os últimos 30 mapas disponíveis, inclusive quando existem sessões intermediárias sem mapa.
- **Usar modelo:** aplique um modelo do paciente ou compartilhado pela clínica e ajuste o que mudou nesta sessão.
- **Salvar e gerenciar modelos:** marque os pontos, informe um nome e salve como modelo. É possível cadastrar um modelo novo diretamente no mapa ou partir de uma sessão anterior. As observações também são preservadas.

As figuras ocupam a coluna esquerda; técnicas, pontos e observações ficam à direita. O profissional pode selecionar um ponto para editar seu nome, técnica e observação, desfazer alterações ou limpar o mapa. O desenho mantém as coordenadas dos mapas existentes.

**Usar mapa nesta sessão** devolve o rascunho ao editor clínico. **Descartar alterações**, fechar a janela ou pressionar Escape restaura o mapa anterior à abertura. Modelos explicitamente salvos ou excluídos são cadastros independentes do rascunho da sessão.

O mapa e a evolução são gravados juntos ao salvar o registro. Uma sessão preenchida somente com pontos ou observações do mapa também pode ser salva, sem criar texto clínico fictício. Se a gravação falhar, não fica uma evolução nova sem seu mapa.

## Finalizar, vincular e gerar guias

Salvar a evolução mantém o registro clínico. Para concluir o horário, o profissional usa **Finalizar sessão e gerar guias** no atendimento. A ação salva o conteúdo, conclui o agendamento original e vincula a evolução ao atendimento gerado, aplicando as regras existentes de convênio e modalidade.

O botão passou a funcionar mesmo quando o profissional não acionou o cronômetro. Nesse caso, a conclusão é registrada sem inventar uma hora de início. Horários anteriores ainda pendentes também podem ser retomados pelo fluxo original.

Quando o atendimento e suas guias já existem, **Finalizar registro clínico** preserva esses registros. A repetição da conclusão reutiliza o resultado existente e não cria guias duplicadas.

Esta entrega incorpora a correção do PR #181: retomada do horário original, tratamento explícito de vínculos ambíguos e registro das etapas do fechamento. Um horário antigo marcado como realizado, mas sem atendimento associado, continua exigindo conferência; a aplicação não cria atendimento retroativo silenciosamente.

## Conferência da VPS em 15/09/2026

A conexão atual foi identificada pela configuração de ambiente da instalação. A configuração salva anteriormente nesta máquina apontava para outra base, desatualizada. A conferência abaixo usou a **VPS**, em uma transação PostgreSQL explicitamente somente leitura, encerrada com rollback. Não foram executadas migrations, alterações de registros ou conclusões de sessões na produção.

Na janela de 90 dias até a data da consulta:

| Constatação | Resultado |
|---|---:|
| Evoluções não importadas | 10; todas com pelo menos um vínculo explícito a agendamento ou atendimento |
| Evoluções importadas | 801; sem esses vínculos explícitos |
| Evolução associada a horário ainda agendado | 1 |
| Horários realizados sem atendimento associado | 6 |
| Mapas corporais existentes | 2 |
| Modelos ativos | 0 |

A pendência recente é o **agendamento 814, de 14/09/2026 às 13:30, evolução 4992**. O horário estava agendado, sem início ou fim clínico e sem atendimento associado. Esse caso foi reproduzido com dados fictícios na verificação do botão real de finalização e passou com a correção.

Os seis horários realizados sem atendimento associado são **118 (07/08), 124, 131, 135 e 137 (10/08), 160 (12/08)**. Não foi encontrado atendimento para o mesmo paciente e dia nesses casos. Eles permanecem para conferência do registro original.

Também havia 569 horários realizados sem evolução explicitamente vinculada, entre 578 realizados no período. Essa contagem não identifica, por si só, uma falha de vínculo: o histórico inclui as 801 evoluções importadas e horários sem evolução registrada. Não foram inferidas associações por proximidade de data nem criadas guias a partir dessa contagem.

A VPS já registrava a migration `20260915140519_PrescricaoInfusaoTextoLivre`, mas ainda não a migration de etapas de fechamento do PR #181. A correção no código e os testes não significam que a atualização esteja instalada na cliente; as pendências acima não foram alteradas por esta entrega.

## Banco e compatibilidade

Além da tabela aditiva de etapas do fechamento, a migration `20260915151605_ModelosCorpoObservacoesCompletas` amplia as observações dos modelos corporais de 500 para 1.000 caracteres, igualando o limite do mapa. O `Down` mantém a capacidade ampliada para preservar textos já gravados.

Nenhum modelo clínico de pontos foi predefinido pelo sistema: a equipe cadastra seus próprios modelos e decide quais compartilhar.

## Verificação

Com .NET 8 e Python disponíveis, na raiz do repositório:

```text
dotnet build Clinica.sln --no-incremental
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj
python tokens/verificar-espelho.py
python tools/verificar-suite.py
python tools/compilar-sombra.py
dotnet run --project tools/verificar-editor-clinico/Qa.csproj
```

A suíte local passou com **2.586 testes, nenhuma falha ou teste ignorado**. A compilação Windows terminou sem erros, com 44 avisos existentes. A compilação-sombra cobre os dez projetos WPF.

A ferramenta de conferência usa SQLite na memória e controles WPF reais, em janela oculta. Exercita cadastro de modelo com pontos, cópia, desfazer, descarte ao fechar e finalização pelo comando do editor sem iniciar o cronômetro. Confere no banco de teste os vínculos entre agendamento, evolução, mapa, atendimento e guias. Também mantém a conferência das prescrições e infusões da entrega anterior.

Os testes de serviço cobrem cópia sem modificar a origem, exclusão de mapas de outros pacientes ou sessões futuras/canceladas, busca além de sessões sem mapa, gravação atômica de evolução e mapa e repetição da conclusão sem duplicar guias. O CI executa a suíte também em PostgreSQL 16, com migrations em banco descartável.

As imagens demonstrativas e os logs ficam em `artifacts/qa-prescricao`; os resultados locais da consulta à VPS ficam em `artifacts/auditoria-sessoes`. Ambos são ignorados pelo Git. A consulta não registra nomes de pacientes nem credenciais no relatório.
