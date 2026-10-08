# Teste da nova interface — PR 245

Esta edição portátil contém Recepção, Clínico, Financeiro, Faturamento e Gerente. Extraia a pasta inteira e abra o executável do módulo desejado. Mantenha a pasta `WebSuite` e os demais arquivos ao lado do executável. Windows 64 bits e Microsoft Edge WebView2 Runtime são necessários; o runtime .NET acompanha os aplicativos.

No Explorer, use **Extrair tudo** antes de abrir. Executar diretamente dentro do ZIP pode
deixar os recursos locais inacessíveis e causar o erro de caminho `0x80070003`.

A revisão 1.0.245-test.20261008.5 reconstrói os principais espaços de trabalho com React, Mantine, FullCalendar, TanStack Table e Motion. Há fila compacta na Recepção, agendamento com conferência do paciente, ficha organizada em identificação/tratamento/alertas, histórico por sessão e composições próprias de Faturamento, Financeiro e Gestão. O calendário oferece Lista, Semana e Dia, com abertura dos horários que não cabem no resumo.

A composição retoma o Financeiro original, com superfícies neutras, cores pontuais e navegação no topo. As buscas e ações continuam ligadas aos serviços originais em C#. Confira nomes completos, troca de prontuário, valores decimais e os comandos de Mais ações. Evidências e escopo técnico: `docs/componentes-clinicos-pr245.md`.

As cores distinguem seleção, andamento, conclusão, pendências e interrupções. Menus, diálogos e calendário têm transições curtas; a preferência de movimento reduzido do Windows é respeitada. A atualização de resultados não remonta os campos durante a digitação.

## Banco de teste

Na primeira abertura, configure **um banco PostgreSQL separado para testes**. Use dados fictícios ou uma cópia preparada para homologação. As operações continuam sendo reais: salvar, receber, cancelar e assinar alteram o banco que você informar. A abertura também aplica as migrations pendentes nesse banco.

Esta edição não herda a conexão da instalação em uso. A configuração fica em `ClinicaSemDor-Teste-PR245` no perfil do Windows. A variável opcional de conexão é `CLINICA_TESTE_CONNECTION`; a variável habitual da instalação não é lida. A atualização automática está desativada nesta compilação. Não há instalação, publicação de release, alteração do canal de atualização nem envio à produção.

Não configure integrações reais de envio, assinatura externa, cobrança ou publicação durante testes que não devam produzir efeitos externos. Os serviços mantêm suas funções originais e usam as configurações do banco informado.

## O que conferir

- Menus no topo: passar o mouse, clicar, usar teclado e fechar com Escape.
- Recepção: cadastro e foto, agenda, fila, confirmação/cancelamento e recebimento parcial.
- Clínico: paciente, atendimento, prontuário, modelos, evolução, mapa corporal, enfermagem, documentos e infusão.
- Financeiro: períodos, lançamentos, contas, estoque, repasses, pacotes e relatórios.
- Faturamento: guias, baixa, glosas, não conformidades, lotes/XML TISS e relatórios.
- Gerente: indicadores, acessos, metas, preços, configurações e ferramentas do topo.
- Formulários: preenchimento, validação, cancelamento, confirmação, rolagem e permissões de cada perfil.

Os testes automatizados usam banco sintético. Eles não substituem sua homologação dos fluxos completos com os perfis, impressoras, câmera, monitor do paciente e integrações utilizados pela clínica. As evidências e os comandos de reprodução estão no repositório, em `docs/refatoracao-web-pr245.md`.

As correções desta revisão e suas evidências estão em `docs/ajustes-homologacao-pr245.md`.

## Revisão de ficha e agenda

A revisão .5 acrescenta Ver sessão em Sessões e guias da ficha; atualiza as listas ao entrar nas abas sem descartar rascunhos; protege a edição da anamnese e acrescenta filtros de situação combinados com profissional e data. Na Agenda e no Meu dia, digite o nome para localizar o paciente imediatamente, inclusive sem acentos. Limpar busca mantém os demais filtros. Contornos de campos, botões e itens clicáveis usam os tokens azuis SemDor. Use o roteiro em docs/validacao-fluxos-agenda-pr245.md para distinguir funções executadas, cobertura de serviços e limites de equipamentos/integrações.
