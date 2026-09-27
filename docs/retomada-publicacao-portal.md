# Publicação direta do portal em produção, sem HML

**Estado registrado em:** 27/09/2026

**Decisão do responsável:** publicar sem aguardar HML nesta liberação.

**Situação:** pacote ainda não aplicado em produção; permanecem pré-requisitos de acesso administrativo verificado à VPS e pré-voo do pacote/base.

## Estado atual

As mudanças abaixo já estão na branch principal dos respectivos repositórios:

- `Clinica`: PRs #216, #217 e #218; commit `029938b6d8192268cfe73ce4f15577f0703fdf39`.
- `clinica-site`: PRs #38 e #39; commit `ae0f19ea3e1b37013730d9139645b6895f89ff56`.

Foi preparado um pacote conjunto para homologação, com SHA-256 `ac7455f3865fa59e9d15de157bb68ae916efbf2b6b5cc6dcd9d6767da07caff6`. A atualização em HML informou estado saudável e gerou o relatório:

`/home/clinica-admin/tablet-stage/tablet-continuidade-029938b6d819-ae0f19ea3e1b-hml.json`

Registro histórico: a atualização em HML informou estado saudável, mas **não houve aceite funcional de HML**. A primeira verificação recebeu HTTP 403 do Cloudflare porque o cliente de teste não enviava um `User-Agent` aceito. O verificador local foi ajustado; a nova tentativa não concluiu porque o executor e o arquivo temporário de acesso HML não estavam presentes na VPS. Por decisão do responsável, essa verificação fica dispensada como etapa de liberação deste pacote. Nenhum relatório de aceite deve ser fabricado ou tratado como aprovado.

**Produção ainda não foi atualizada.** A dispensa de HML não dispensa os pré-requisitos técnicos abaixo.

## Escopo sem acesso às máquinas dos operadores

Para esta publicação, a proteção de credenciais compartilhadas fica **desativada** pelo padrão compatível. Não distribuir chave nem senha, não alterar os computadores dos operadores e não migrar credenciais para `enc:v1:`. O atualizador verifica que a base não contém credenciais cifradas antes de aceitar essa rota. A proteção poderá ser planejada em outra liberação quando houver acesso às máquinas.

1. **Console root disponível:** o console da Locaweb foi aberto no navegador e respondeu a comandos simples; `clinica-tablet` informou estado ativo. As tentativas de confirmar o link `current` e o banco não produziram um pré-voo confiável. O caminho `/opt/clinica-tablet/current` não foi encontrado, portanto é necessário identificar a estrutura real antes da atualização. O arquivo `/etc/ssh/ssh_host_ed25519_key.pub` também não foi encontrado; nenhuma chave SSH foi aceita ou alterada.
2. **Pré-voo de produção pendente:** confirmar o caminho do release, a migration esperada, o estado de credenciais cifradas, SHA e conteúdo do pacote, backup restaurável, release anterior e plano de rollback antes de alterar o serviço.

O atualizador `deploy/tablet/atualizar-posto.py` permite uma dispensa explícita de HML via `--pular-hml motivo`. Sem essa opção, a exigência normal de relatórios HML do mesmo pacote continua ativa. A tentativa é registrada em diretório privado antes das alterações; o relatório final registra o resultado e o motivo. Essa opção não equivale a aceite funcional.

## Roteiro de publicação direta

1. Usar o console root já aberto na Locaweb e verificar a configuração de produção sem expor segredos. SSH só deve ser usado se o fingerprint puder ser confirmado por fonte confiável.
2. Confirmar que `CLINICA_CREDENCIAIS_CRIPTOGRAFIA_HABILITADA` não está ativa e que a base não possui credenciais `enc:v1:`; o instalador verifica os valores sem exibi-los.
3. Gerar o pacote com checkouts limpos nas revisões exatas de `Clinica` e `clinica-site`, usando `tools/empacotar-continuidade-tablet.ps1 -Site <caminho-do-checkout-do-site>`. Conferir manifesto, SHA-256, migration esperada e release ativo; se o pacote mudar, repetir o pré-voo. O site é privado, então o empacotamento deve usar o checkout local autorizado, sem token adicional de acesso entre repositórios.
4. Fazer backup privado e confirmar o rollback.
5. Publicar API e `clinica-site` juntos sem atualizar desktops. Usar `--pular-hml` somente nesta liberação autorizada e informar o motivo; não criar relatórios HML fictícios.
6. Verificar health, rotas protegidas, sessões, upload autenticado, fila de infusões e pendências em produção. Não criar ou alterar registros clínicos reais.
7. Registrar o relatório de produção e reverter se as verificações de saúde ou de proteção falharem.

A PR #218 inicia a contagem de novas pendências em 27/09/2026, hoje. Após a publicação, conferir que sessões anteriores a essa data continuam fora do indicador e que omissões a partir dela entram na contagem.

O fluxo detalhado de criptografia e os requisitos de proxy estão em [implantacao-seguranca-portal.md](implantacao-seguranca-portal.md). Este roteiro trata da publicação do código; não autoriza nem inclui exclusão de dados de produção.

## Critério para considerar concluído

Só marcar a publicação como concluída quando o console root permitir confirmar o estado do release e do banco, o modo compatível de credenciais estiver confirmado, o backup/rollback estiver confirmado e os smoke tests de produção tiverem passado. A dispensa de HML deve constar no relatório como dispensa, nunca como aprovação.
