# Modelos clínicos: busca e texto formatado

O sistema e o portal permitem buscar modelos por parte do nome, conferir seu
conteúdo e separar a criação de um novo modelo da atualização de um existente.
Infusões usam a mesma biblioteca, com indicação, cuidados e parâmetros de cada
bloco. Não copiam paciente, autoria, execução ou assinatura para o modelo.

O texto permanece nas colunas existentes. Colunas opcionais armazenam somente
trechos de negrito e itálico; se um cliente antigo alterar o texto, formatação
incompatível é descartada. Documentos emitidos copiam os trechos, e o gerador de
PDF os aplica. O editor não armazena HTML ou RTF recebido da área de transferência.

## Validação

- Compilação Windows e `tools/compilar-sombra.py`.
- `tools/verificar-suite.py` e QA do controle WPF real, incluindo salvar/reabrir.
- Suíte SQLite: 2.744 testes aprovados na primeira rodada; os dois problemas
  encontrados em pedidos de exame foram corrigidos e a repetição dos 146 testes
  afetados passou integralmente.
- PostgreSQL 16 isolado: 18 testes de modelo/receituário passaram, com todas as
  migrations executadas. Dados fictícios não foram inseridos em produção.
- Outros dois testes PostgreSQL passaram: gravação/substituição de itens com
  papel restrito e presença das fontes Bold/Italic nos PDFs de receita e infusão.
- Portal: fluxos completos com contrato simulado, acessibilidade e tamanhos de
  320 a 1180 px. Busca além de 200 modelos e formatação testadas por teclado.

## Implantação coordenada

Esta alteração ainda não foi publicada. O portal atualizado requer esta API e a
migration aditiva `20260921194019_ModelosClinicosFormatados`. Aplicar com backup
verificado e executar `deploy/tablet/modelos-documento-permissoes.sql` como
administrador: o papel do portal precisa atualizar modelos e substituir itens.
Publicar API e portal juntos, e distribuir a versão Windows com o novo editor.
Em uma reversão, preservar as novas colunas e voltar apenas os binários/portal.

A auditoria de produção de 21/09 encontrou três modelos ativos, todos termos de
procedimento, e nenhum modelo de receita, exame ou atestado. O papel do portal
tinha INSERT em ModelosDocumento, mas não UPDATE. Esse inventário não comprova
tentativas anteriores de salvar e não foi alterado por testes.
