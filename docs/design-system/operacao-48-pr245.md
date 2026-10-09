# Operação diária e 48 pacientes — PR 245

## Mudanças

O padrão aprovado permanece: superfícies brancas, campos com contorno legível, azul SemDor nas interações, ação principal preenchida e auxiliares com texto/ícone/contorno azul. Não houve substituição de regras C# por regras no navegador.

- Secretaria: chegada, falta e cancelamento acessíveis na linha. Falta/cancelamento pedem confirmação com paciente e horário; desistir não grava. Em notebook, as ações ficam lado a lado abaixo da identidade e situação.
- Os totais dos filtros por profissional incluem todas as linhas exibidas, inclusive faltas e cancelamentos. “Em sala” exclui sessões com fim registrado e pendências de dias anteriores. Concluídos e conclusão pendente continuam distintos na secretaria e no Meu dia.
- Acompanhamento: Salvar é o botão principal; Voltar à lista é direto. Filtros, detalhe e configuração têm retornos próprios. Falhas mantêm rascunho e mensagem de erro, sem anunciar sucesso após uma recarga malsucedida.
- Confirmações: resumo atualiza após operações, confirmação repetida fica indisponível e o nome do profissional é carregado com o horário. Confirmar o horário não registra chegada. A confirmação aberta dentro da agenda também publica o aviso local e desabilita respostas já confirmadas.
- Faturamento: removida a seção Carteirinhas a vencer/vencidas das apresentações Web e WPF. Guias/códigos, recursos e consultas continuam acessíveis. Cadastro da carteirinha e conferência de elegibilidade permanecem.
- Salvar acompanhamento e confirmar horário alimentam a central de avisos existente; falhas de ações explícitas orientam conferir a tela. Os avisos não incluem nome do paciente nem conteúdo clínico. Abrir marca como lido e mantém histórico. Não há aviso periódico nem envio de WhatsApp/e-mails reais nos testes.

## Matriz de evidências

| Pedido | Evidência reproduzível |
| --- | --- |
| 48 pacientes na secretaria | `qa/recepcao-web --agenda48`: SQLite isolado com exatamente 48 pacientes e 48 horários; sete estados/filtros, 1366/900 px; chegada, falta, cancelamento e desistência; leitura após navegação; nenhuma duplicação |
| Totais e Meu dia | Mesmo cenário confere 48/24/24 nos profissionais, 8 em sala, 8 concluídos e 8 conclusões pendentes; falta/cancelamento alterados na recepção aparecem no Meu dia |
| Confirmações e recall | `qa/recepcao-web --acompanhamento`: outro banco com 48 pacientes, 24 confirmações elegíveis e 48 recalls; geração idempotente, nomes de profissionais, confirmação, resumo, filtros, histórico, erro/rascunho e permissão negada |
| Acompanhamento e avisos | WebView2 em 1440/1044/900 px, Salvar azul e Voltar direto; operação real → aviso local, contador e histórico da central existente |
| Buscas e marcar horário | `qa/recepcao-web --buscas`: busca digitada, resposta fora de ordem, identidade escolhida, salvar/cancelar agendamento, confirmação acessível ao rolar, chegada; grade densa e filtros |
| Clínico | `tests/Clinica.Clinico.Web.Qa --web`: atendimento, gravação explícita, permissões, documentos, campos complementares e infusão; `tools/VerificarInfusao` cobre o ciclo com persistência |
| Faturamento | `tools/validar-faturamento-web --web`: ausência de Carteirinhas com seções mantidas, baixa, glosa, NC, parâmetros e TISS; três larguras |
| Financeiro | `tools/validar-design-financeiro --web`: aplicativo independente, navegação, busca, formulários, persistência, cancelamento e acesso revogado |
| Gerente e integração | `tools/validar-suite-web --web`: janela real da suíte, páginas, formulários e buscas integradas |
| Contraste dos cinco módulos | `tools/validar-suite-web --contraste`: 76 contratos de página em 1366/900 px, medidas sRGB dos campos, placeholders, rótulos, botões, indicadores e estados renderizados; texto ≥4,5:1, contorno ≥3:1, sem overflow |
| Serviço de confirmação | `CampanhaServiceTests.Confirmacao_ReabrirFilaCarregaProfissionalDoHorarioESuportaHorarioSemProfissional`: consulta com tracker limpo, com e sem profissional |

O inventário de contraste usa um menu de QA para abrir todos os contratos, inclusive telas internas. A navegação autorizada e os atos de gravação são verificados separadamente pelos QA de cada aplicativo. Os cenários sintéticos não provam todas as combinações de dados, permissões, zoom ou dispositivos da clínica.

## Reprodução no Windows

A infusão recebe a pasta de saída: `dotnet run --project tools/VerificarInfusao -c Release -- artifacts/infusao-desktop`.

Os comandos da matriz são executados com `dotnet run --project <projeto> -c Release -- <argumento>`. A suíte de serviços usa `dotnet test tests/Clinica.Tests/Clinica.Tests.csproj`. Antes do push, executar também `python tools/compilar-sombra.py` e `python tools/verificar-suite.py`, sem compilações WPF simultâneas.

Os novos cenários de secretaria, acompanhamento e contraste estão no workflow Windows da PR. Os logs locais ficam em `artifacts/meta-*.log`; medições detalhadas em `artifacts/contraste-modulos/medicoes.json` e capturas em `artifacts/agenda48` e `artifacts/acompanhamento-confirmacoes`.

## Jev

A revisão adicional recebe somente alterações de código e resultados sintéticos. O Jev analisa evidências; não opera o aplicativo nem inspeciona capturas. As três consultas classificaram os quatro quesitos (secretaria, acompanhamento, notificações e módulos) como `coerente_com_evidencias`. Resultado e escopo: [operacao-48-jev-pr245.json](operacao-48-jev-pr245.json).

## Capturas verificadas

- [Secretaria: ações diretas e filtros com 48 pacientes](evidencias-pr245/secretaria-48-900.png).
- [Acompanhamento: Salvar e Voltar diretos](evidencias-pr245/recall-48-900.png).
- [Confirmações: profissional e situação](evidencias-pr245/confirmacoes-48-900.png).
- [Avisos das operações reais](evidencias-pr245/avisos-operacionais-48.png).
- [Faturamento sem a seção de carteirinhas](evidencias-pr245/faturamento-sem-carteirinhas-900.png).

## Resultado local

2.986 testes de serviços aprovados, sem falhas ou ignorados. QA funcionais dos cinco módulos, cenários de 48 pacientes, 34 verificações de infusão e inventário de contraste passaram. A execução da busca da recepção atravessou a meia-noite; foi repetida após a mudança do dia, com aprovação, mantendo a regra que só permite registrar chegada no dia do horário.
