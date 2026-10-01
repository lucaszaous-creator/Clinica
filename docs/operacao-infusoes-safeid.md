# Infusões e SafeID — operação e recuperação

Registro de 30/09/2026 (America/Sao_Paulo). Implementação: [conclusão automática](safeid-conclusao-automatica.md). Revisões: PR Clinica #231 e clinica-site #48, branch `codex/infusao-documento-unico`.

## O que mudou

- “Não” e “Não executável” exigem justificativa. Horários editados da execução são preservados na geração de novos PDFs.
- A prescrição com as duas assinaturas é o documento principal. A falta de uma folha secundária não cria pendência de arquivamento.
- A enfermeira confere a execução e autoriza no SafeID. O retorno válido aciona a conclusão no servidor; a confirmação do PDF, assinatura e recibo ocorre na mesma transação. Não há segundo botão de arquivamento depois do retorno.
- A conclusão automática exige `Portal__SafeId__ConclusaoAutomatica=true`; receitas e autorizações antigas preservam o fluxo anterior.
- Uma autorização pendente bloqueia somente o mesmo ato/documento. O bloqueio anterior por sessão foi removido: outras infusões continuam disponíveis.
- O diálogo identifica a prescrição, a assinatura médica ou de enfermagem, o estado e o prazo. “Retomar no SafeID” reutiliza a autorização ainda aguardando na sessão original, sem gerar outro state ou PKCE. O conteúdo e as permissões são revalidados.

## Publicação e evidências

O usuário pediu explicitamente a publicação em produção depois de informado de que os testes técnicos passaram e o retorno com certificado real ainda não foi concluído. Essa dispensa deve constar no relatório do instalador, sem registrar falsamente um aceite com certificado real.

- Última publicação: `/home/clinica-admin/tablet-stage/infusoes-safeid-ultima-publicacao.json` no VPS. Contém release, commits, SHA-256, backup, verificações e limitações.
- Pacote e relatórios de cada release: `/home/clinica-admin/tablet-stage/tablet-continuidade-<backend>-<interface>*`.
- Aplicação: `/opt/clinica-tablet/current`; homologação: `/opt/clinica-posto-hml/current`.
- Configuração: `/etc/clinica-tablet/portal.env`. Não copiar este arquivo para chamados: contém segredos. Backup da configuração antes de habilitar a flag fica privado na pasta stage, com o nome da release e sufixo `-portal-env-producao-backup`.
- Backup do instalador: diretório exato registrado no relatório, sob `/var/backups/clinica-tablet-continuidade-*`.
- Migração aditiva: `20261001015315_ConclusaoAutomaticaSafeId`, tabela `OperacoesAssinaturaTablet`. Não altera PDFs históricos nem apaga registros.

Testes: suíte anterior com 2.925 testes do sistema; regressão desta correção com 143 testes de atendimento e 29 testes HTTP, incluindo 10 cenários de retorno automático; testes de navegador para identificação, retomada, outra sessão, expiração e ausência do segundo botão. A API usa provedor fictício com CMS criptograficamente válido para verificar duas assinaturas no PDF. Isso não substitui um teste real do titular no SafeID.

Na homologação, o servidor recebeu HTTP 200 do provedor, mas o retorno de assinatura não aconteceu. O navegador do Codex deixou de responder à inspeção; o usuário abriu o portal no Chrome. Não há evidência suficiente para afirmar que todo o fluxo SafeID foi concluído no Chrome. Os parâmetros OAuth, validação de certificado e motor de assinatura foram preservados.

## Quando uma tentativa fica pendente

1. Anotar o número PRE, etapa (médico/enfermagem), horário e estado mostrado. Não colocar CPF, códigos OAuth, tokens ou URLs completas de autorização em chamados.
2. `aguardando`: não houve consumo da autorização. Na sessão original, usar “Retomar no SafeID”. Em outro acesso ou após reinício, concluir no acesso original ou aguardar a expiração de cinco minutos. “Atualizar situação” consulta sem assinar.
3. `assinando`: processamento no servidor. Consultar andamento; não repetir o ato remoto. Passado o limite de processamento, o estado consultado passa a `verificar`.
4. `concluido`: PDF, assinatura e recibo gravados. Conferir o PDF principal e a fila; não pedir nova assinatura por falta da antiga folha secundária.
5. `falha`/`expirado`: conferir o documento antes de iniciar novamente. `verificar`: resultado remoto incerto; conferir arquivo persistido e provedor antes de liberar qualquer repetição.

O bloqueio durável usa `Tipo:Documento` em `ChaveAtiva`; os segredos transitórios ficam somente na memória. Não apagar recibos nem limpar `ChaveAtiva` manualmente para contornar uma pendência. Em outra sessão, a consulta autorizada mostra o estado do documento, sem expor o identificador ou URL da tentativa original.

## Diagnóstico no servidor

`systemctl is-active clinica-tablet` confere o serviço. O endpoint `/api/clinico/safeid` exige sessão autenticada e informa `habilitado` e `conclusaoAutomatica`. A interface publicada deve corresponder aos hashes de `manifesto.json` da release.

Com acesso administrativo, consultar somente os metadados necessários:

```sql
SELECT "Tipo", "Documento", "Situacao", "AtualizadaEm", "ExpiraEm"
FROM "OperacoesAssinaturaTablet"
WHERE "ChaveAtiva" IS NOT NULL
ORDER BY "AtualizadaEm" DESC;
```

Os horários são Unix em milissegundos. Banco de produção `clinica`, PostgreSQL local porta 45432. Não expor logs brutos: eles podem conter parâmetros de autenticação. Registrar no chamado apenas horários, estados, referência autorizada do documento e erro sanitizado.

## Desativação e retorno à versão anterior

1. Conferir tentativas em andamento e resultados incertos antes de reiniciar. Um reinício perde os segredos transitórios. Não usar uma versão antiga para repetir uma tentativa incerta.
2. Para novas autorizações voltarem ao fluxo manual, definir somente `Portal__SafeId__ConclusaoAutomatica=false` e reiniciar `clinica-tablet` após a conferência. Não alterar ClientId, ClientSecret, retorno HTTPS, ambiente do provedor, certificados ou escopos.
3. A flag desativada não cancela uma intenção automática já aceita pelo processo em execução. Preservar e conferir essas intenções.
4. Se for necessário reverter a aplicação, usar a release anterior e a configuração salvas no relatório/backup. Conferir o serviço, login, fila e PDF após a troca. Manter a migração aditiva e os recibos; não restaurar o banco inteiro sobre atendimentos novos.
5. Registrar a reversão, o motivo e os documentos que precisam de conferência no relatório de operação.

## Caso PRE 2026/0017

Registro de produção id 43: execução encerrada e duas assinaturas com arquivos presentes. A pendência vinha da exigência da folha secundária. O PDF original foi preservado; seu SHA-256 é `971dbd0f92b951fe929687b1b98e2b0fa12ec43afb613ec12f8fa6f312ab09b5`. Um horário ausente em um PDF histórico já assinado não foi inserido por reescrita do documento; a correção vale para a geração de novos PDFs.
