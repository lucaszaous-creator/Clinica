# Retomada da publicação do portal

**Estado registrado em:** 26/09/2026

**Situação:** publicação em produção adiada até que seja possível acessar e atualizar todas as máquinas desktop envolvidas.

## Estado atual

As mudanças abaixo já estão na branch principal dos respectivos repositórios:

- `Clinica`: PRs #216, #217 e #218; commit `029938b6d8192268cfe73ce4f15577f0703fdf39`.
- `clinica-site`: PRs #38 e #39; commit `ae0f19ea3e1b37013730d9139645b6895f89ff56`.

Foi preparado um pacote conjunto para homologação, com SHA-256 `ac7455f3865fa59e9d15de157bb68ae916efbf2b6b5cc6dcd9d6767da07caff6`. A atualização em HML informou estado saudável e gerou o relatório:

`/home/clinica-admin/tablet-stage/tablet-continuidade-029938b6d819-ae0f19ea3e1b-hml.json`

Isso confirma a atualização saudável, mas **não confirma o aceite funcional de HML**. A primeira verificação recebeu HTTP 403 do Cloudflare porque o cliente de teste não enviava um `User-Agent` aceito. O verificador local foi ajustado para enviar um agente de navegador; a nova tentativa não pôde concluir porque o script executor e o arquivo temporário de acesso HML não estavam presentes na VPS. Portanto, ainda não existe relatório final de aceite funcional para esse pacote.

**Produção não foi atualizada.** Não iniciar a publicação de produção usando apenas o relatório de saúde de HML.

## Por que a publicação foi adiada

A aplicação usa configurações de credenciais compartilhadas entre a API e os postos desktop. Não há acesso a todas as máquinas de uso para confirmar versões, distribuir a mesma chave de criptografia com segurança e coordenar a atualização. Clientes antigos podem não conseguir ler credenciais cifradas no novo formato. O adiamento evita uma troca parcial que deixe integrações indisponíveis.

## Roteiro para retomar

1. **Agendar a janela e inventariar os postos.** Identificar cada máquina que usa o aplicativo desktop, quem consegue acessá-la e a versão instalada. Confirmar o acesso aos titulares SafeID necessários ao aceite assistido.
2. **Preparar a atualização coordenada.** Garantir que todos os postos que leem ou editam credenciais SMTP, SafeID ou S3 receberão a versão compatível. Fechar os clientes antigos durante a troca.
3. **Preparar a chave fora do repositório.** Gerar `CLINICA_CREDENCIAIS_CHAVE` com 32 bytes aleatórios codificados em Base64 e instalar o mesmo valor na API e em todos os postos aplicáveis. Guardar em cofre/armazenamento protegido, restringir permissões e não registrar o valor em documentação, comandos compartilhados, logs ou pacotes. Preservar a chave separada dos backups do banco.
4. **Refazer a verificação funcional de HML para o pacote exato.** Disponibilizar novamente, por procedimento protegido, o executor e as credenciais temporárias necessários. Confirmar o SHA-256 do pacote antes de executar. O teste precisa gerar um relatório de aceite funcional vinculado ao mesmo pacote e complementar o relatório de saúde existente. Se o pacote mudar, repetir os testes para o novo SHA.
5. **Completar os testes de HML.** Validar login, expiração e renovação de sessão, duas abas, upload autenticado e recusa anônima, encaminhamento de IP pelo `CF-Connecting-IP`, e-mail, SafeID e S3 com valores fictícios. Conferir que os dados sensíveis no banco permanecem cifrados sem exibir seus valores. Testar também os fluxos de portal e desktop afetados pelas PRs.
6. **Rever o corte de pendências de evolução.** A PR #218 tinha data efetiva configurada para 27/09/2026. Confirmar que essa data ainda corresponde à regra desejada antes de publicar; se a implantação ocorrer depois ou a regra mudar, ajustar a configuração e repetir as verificações pertinentes.
7. **Preparar backup e retorno.** Confirmar backup restaurável e procedimento de rollback para API, site, banco/configurações e clientes desktop. Manter a chave necessária para restaurar credenciais cifradas, protegida e separada do backup.
8. **Publicar em conjunto.** Depois dos aceites, atualizar API e `clinica-site` como uma única mudança coordenada e atualizar os postos necessários. Não publicar um dos repositórios isoladamente.
9. **Verificar produção.** Executar smoke tests dos fluxos críticos, observar logs e integrações e registrar o resultado. Fazer o aceite assistido de SafeID com seus titulares. Se uma verificação crítica falhar, aplicar o rollback planejado.

O fluxo detalhado de criptografia e os requisitos de proxy estão em [implantacao-seguranca-portal.md](implantacao-seguranca-portal.md). Este roteiro trata da publicação do código; não autoriza nem inclui exclusão de dados de produção.

## Critério para considerar concluído

Só marcar a publicação como concluída quando todas as máquinas e integrações aplicáveis tiverem sido consideradas, os relatórios de saúde e aceite funcional de HML corresponderem ao mesmo pacote, a chave estiver configurada de modo consistente e protegido, o aceite assistido tiver sido realizado e os smoke tests de produção tiverem passado.
