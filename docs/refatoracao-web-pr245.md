# Refatoração visual da suíte — PR 245

A interface local em HTML/CSS/TypeScript foi aplicada aos módulos Recepção, Clínico, Faturamento e Gerente, mantendo o Financeiro como referência. C# continua responsável por autenticação, permissões, regras, persistência, assinaturas e exportação. Não foi criado um backend alternativo no navegador.

## Superfícies e composição

A composição dos cinco módulos registra 76 páginas e 97 formulários distintos por chave/tipo. Os destinos autorizados permanecem alcançáveis pelo topo; formulários, seletores e confirmações são apresentados no mesmo host web. Entrada, primeiro acesso, senha obrigatória, configuração de conexão, avisos e assinatura em monitor do paciente também receberam interface local.

Cada módulo fornece registros explícitos de propriedades, opções e comandos. O host valida a sessão e o contexto em cada mensagem; os IDs de linhas e opções resolvem somente objetos nas coleções atuais. Campos de senha não são devolvidos ao HTML. Operações inválidas bloqueiam a confirmação até a correção. Cancelar fecha o formulário com resultado negativo.

- Recepção: 16 páginas e 16 formulários próprios, agenda visual, cadastros e foto, ficha administrativa, fila, recebimentos, documentos e pacotes.
- Clínico: 20 páginas e 17 formulários próprios, mais componentes clínicos compartilhados; evolução formatada, modelos, avaliações, anexos, mapa corporal, enfermagem, infusão e leituras históricas.
- Faturamento: oito páginas e 13 formulários próprios, incluindo baixa em lote, glosas, não conformidades, TISS, rodada obrigatória e contingência offline somente para consulta.
- Gerente: 16 páginas, gráficos, cadastros, metas, preços, configurações e composição dos demais módulos.
- Financeiro: 15 páginas existentes, navegação superior, atalhos de período e abertura de contas fixas, validades, regras e catálogo; os mesmos registros financeiros também são usados no Gerente.

As contagens por módulo se sobrepõem nas superfícies compartilhadas e não devem ser somadas. Arquivos WPF legados permanecem como referência e suporte às ferramentas antigas; os hosts dos aplicativos migrados apresentam os fluxos registrados na web. Seletores de arquivos e abertura de documentos exportados continuam nativos.

## Verificação de paridade

A comparação determinística entre 199 XAML e os registros efetivos identificou 1.418 vínculos de comando ou entrada. A documentação de diferenças e aliases está em `qa/recepcao-cobertura/justificativas.md`; não se confunde correspondência nominal com equivalência funcional. Campos de leitura e comportamentos por eventos receberam verificações adicionais nos fluxos clínicos, de assinatura, entrada e ferramentas do topo.

As correções incluíram conteúdo formatado das versões e sessões, observações do mapa, busca por modelo, alertas de infusão, indicação de modalidades de enfermagem, acesso ao catálogo, filtros/paginação e atalhos de teclado. O limite de 20.000 caracteres da transcrição de infusão externa foi conservado.

## Evidências locais

- `Clinica.Tests`: 2.958 testes aprovados, zero falhas/ignorados, SQLite; quatro testes específicos de treinamento repetidos após o isolamento da edição de teste.
- `Clinica.Assinaturas.Tests`: 21 testes aprovados em Debug. A tentativa de compilar a solução inteira em Release encontrou a dependência SQLite indisponível nesse projeto de testes: o repositório exclui SQLite de Infrastructure em Release. A validação dos executáveis usa os cinco projetos de aplicativo em Release, separadamente dos testes, como no CI.
- Compilação-sombra: C# dos 11 projetos WPF aprovado; verificação da suíte: 225 XAML e 10 projetos; espelho de 33 cores aprovado.
- Recepção: fluxos reais de cadastro/foto, agenda e pagamento parcial, cancelamento e acesso negado; 48 capturas de rotas e duas da agenda, incluindo geometria dos cartões de 30 minutos.
- Clínico: persistência de medida/exame/alerta/avaliação/sessão, rascunhos, leitura de versões, mapa por ponteiro/teclado e restauração ao cancelar; 60 capturas de rotas e uma do mapa.
- Faturamento: gravações reais de anotação/baixa/glosa/NC, lote/XML, validações e contingência; entrada e assinatura real do canvas no banco sintético. Usuário, permissões individuais, meta e preço do Gerente gravados e cancelamento conferido.
- Gerente e Financeiro composto: 31 páginas em três dimensões, sem erro visual, vazamento horizontal ou sobreposição das ferramentas do topo.
- Ferramentas compartilhadas: menu/teclado, pesquisa, avisos e leitura, treinamento autorizado, vídeo local validado por SHA, reprodução, retomada, progresso, conclusão e reinício. Destino externo e aula não catalogada rejeitados.
- Financeiro original: ferramentas de treinamento/avisos integradas ao cabeçalho, com vídeo, retomada e privacidade; regressão das 15 páginas em três dimensões, 25 contratos de formulário/prompt e operações reais de gravação/cancelamento, permissões e contexto.

Os harnesses reproduzíveis são `tools/validar-suite-web`, `tools/validar-faturamento-web`, `tools/validar-design-financeiro`, `qa/recepcao-web` e `tests/Clinica.Clinico.Web.Qa`. As capturas e os logs ficam em `artifacts/`, fora do versionamento, sempre com dados fictícios.

## Compilação para homologação

Os cinco projetos de aplicativo compilaram em Release com `--no-incremental`, sem erros. Os avisos existentes de campos observáveis do MVVM Toolkit continuam registrados nos logs de compilação.

`tools/gerar-executaveis-teste.ps1` gera cinco aplicativos portáteis Windows x64, com runtime .NET e recursos locais. A propriedade `ClinicaTesteLocal=true` ativa a configuração separada `ClinicaSemDor-Teste-PR245`, a variável `CLINICA_TESTE_CONNECTION` e desativa os atualizadores. O script não instala, não publica, não cria tag e não altera o canal de atualização. Instruções para o usuário: `docs/teste-pr245.md`.

Os cinco pacotes foram gerados e tiveram a abertura conferida até a entrada, sem configurar banco: todas as janelas identificaram a edição de teste PR 245. Os 147 arquivos distribuídos foram conferidos pelo manifesto SHA-256, incluindo os recursos HTML/CSS/JavaScript e fontes locais. A abertura dos executáveis não substitui os testes de navegação e persistência dos harnesses descritos acima.

## Limites da validação

Os testes de persistência locais usaram SQLite sintético. A integração com PostgreSQL é verificada pelo CI quando disponível e pela homologação em banco separado. Não houve envio real de WhatsApp, cobrança/Pix, assinatura paga SafeID ou publicação de documentos. Câmera física, impressora e segundo monitor físico precisam da homologação local do usuário. Nenhum teste aqui certifica todas as combinações de dados, perfis ou integrações externas.

Não houve nova auditoria do Jev nesta etapa de implementação. Os três agentes trabalharam nos módulos e seus testes; a comparação de contratos e as verificações do repositório são determinísticas. A PR permanece em rascunho, sem merge e sem publicação em produção.

A revisão posterior dos relatos de homologação, incluindo a consulta pontual efetiva
ao Jev, contraste global, buscas e agenda densa, está documentada em
`docs/ajustes-homologacao-pr245.md`.
