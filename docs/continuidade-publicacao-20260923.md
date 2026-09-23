# Continuidade da publicação — 23/09/2026

Registro do ponto de parada após o pedido para integrar as PRs, publicar na VPS e executar os releases. Não contém credenciais nem dados de pacientes.

## Concluído

- `Clinica` PR [#200](https://github.com/lucaszaous-creator/Clinica/pull/200) integrada em `main` pelo merge commit `c7e69b8bfc27701f500f922ffc24ce9f481b0fe3`. Os três checks da PR passaram: build Windows, verificador Linux e PostgreSQL.
- `clinica-site` PR [#20](https://github.com/lucaszaous-creator/clinica-site/pull/20) integrada em `main` pelo merge commit `03d8815a23a1733fc43de59be9bfafb26915d70a`. Os checks de interface, qualidade técnica e prontidão da PR passaram.
- Nenhuma release nova foi iniciada nesta rodada. Nenhuma alteração foi feita na VPS nem no arquivo local `known_hosts`.
- Após os merges, os workflows da `main` ainda estavam em execução às 00:30 UTC: [Clínica — Verificar (Linux)](https://github.com/lucaszaous-creator/Clinica/actions/runs/35802190158), [site — Portal de assinaturas](https://github.com/lucaszaous-creator/clinica-site/actions/runs/35802217550) e [site — Qualidade, SEO e implantação](https://github.com/lucaszaous-creator/clinica-site/actions/runs/35802217440). Conferir seus resultados atualizados antes de avançar.

## Bloqueio de acesso à VPS

O SSH com `StrictHostKeyChecking=yes` recusou `clinica-admin@177.153.66.251`: a chave ED25519 oferecida agora tem fingerprint `SHA256:Pm76VpwMO4/fCCLca2WNjSU6eG4jYBmOBpZtxBYAD44`, diferente da ED25519 anteriormente confiada (`SHA256:1hv8xfh8/rVWjBSsyWfQELzo/+FyLh5jGTUj5yMoI7U`). O aviso também identificou a entrada ECDSA antiga na linha 3 de `known_hosts`. Isso pode indicar troca legítima do servidor ou outro problema de identidade; ainda não há confirmação independente. O painel da Locaweb pediu novo login e o responsável informou que está fora de casa.

Ao voltar, abrir o console da VPS **pelo painel da Locaweb** e executar `ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub`. Comparar o fingerprint mostrado no console com o apresentado pelo SSH. Somente após confirmar a identidade, atualizar a entrada local de `known_hosts` e voltar a conectar com verificação estrita. Não usar `StrictHostKeyChecking=no`, não aceitar a chave apresentada pela própria conexão como única prova e não enviar senha ou chave privada pelo chat. Se os fingerprints diferirem, investigar com o provedor antes de acessar.

## Ordem segura para concluir

1. Conferir os workflows da `main` e a identidade SSH. Registrar a versão ativa dos serviços, a configuração e os backups sem expor segredos.
2. Preparar **um pacote exato dos dois commits da `main`** com hashes. O empacotador do portal está em `clinica-site/ferramentas/empacotar-portal.py`; o verificador é `clinica-site/ferramentas/verificar_pacote_portal.py`. Conferir que o pacote não inclui documentação, mapa de fonte, segredos ou arquivos antigos.
3. Atualizar o procedimento de implantação do tablet antes de executá-lo. `deploy/tablet/atualizar-posto.py` aceita apenas o contrato e a migration da continuidade anterior (`20260917185911_EnfermagemVinculadaEValidacaoInfusao`); ele **não aceita** as migrations da PR #200. `deploy/tablet/publicar-posto.py` está preso a nomes de releases e backup anteriores. Nenhum desses scripts deve ser usado sem adaptação, teste e revisão para esta versão.
4. Conferir a sequência das oito migrations novas da PR #200: catálogo de estoque, parcelamento de contas, conferência de materiais, baixas parciais, adoção gradual de materiais, modelos/fases de enfermagem, perfil Psicologia e habilitações do profissional. Fazer backup restaurável e ensaio na homologação com dados fictícios. Conferir permissões do papel PostgreSQL do portal, compatibilidade com a versão anterior e recuo antes de promover à produção. Preservar o fluxo de guias.
5. Publicar primeiro banco/API na VPS, depois o portal correspondente. Validar `/health`, login/CSRF, fila BSV, modelos, bloqueio de arquivos internos, permissões e serviço/túnel. Não criar atendimento fictício na produção. O workflow do site também pode publicar o site institucional em `publicado`; conferir separadamente o resultado e a versão servida pela VPS.
6. Só depois da implantação compatível, executar sequencialmente os cinco releases desktop pelo workflow `Clinica/.github/workflows/release.yml` na `main`. Cada canal tem versão própria e clientes instalados podem se atualizar automaticamente. Conferir a versão mais recente de cada canal imediatamente antes de disparar, aguardar `release` e `sincronizar-canais` verdes e verificar os assets da GitHub Release.

Versões seguintes calculadas às 00:30 UTC, **a confirmar novamente** antes do disparo:

| Aplicativo | Release atual | Próxima sugerida |
| --- | --- | --- |
| Faturamento | `v1.2.37` | `v1.2.38` |
| Recepção | `recepcao-v1.2.36` | `recepcao-v1.2.37` |
| Financeiro | `financeiro-v1.2.35` | `financeiro-v1.2.36` |
| Gerente | `gerente-v1.2.37` | `gerente-v1.2.38` |
| Consultório | `clinico-v1.2.37` | `clinico-v1.2.38` |

O merge da `main` **não** dispara automaticamente o workflow de release dos executáveis. A publicação do site institucional e a do portal clínico são percursos distintos; verificar ambos em vez de inferir que um confirma o outro.
