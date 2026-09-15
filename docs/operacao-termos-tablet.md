# Operação — assinaturas de TCLE e BSV no tablet

## Estado em 15/09/2026

API, interface, migração aditiva, recuperação e testes implementados nas branches
`codex/termos-tablet` dos dois repositórios. **Portal ainda não instalado na VPS;
nenhuma migração desta entrega aplicada ao banco clínico.** Aparelho escolhido:
tablet Android; falta homologação no aparelho físico.

- [Interface e testes](https://github.com/lucaszaous-creator/clinica-site/blob/codex/termos-tablet/docs/portal-assinaturas.md).
- [Plano aceito](plano-assinatura-bsv-tcle-tablet.md).
- [Evidências e checkpoint](retomada-termos-tablet.md).

## Fluxo assistencial

O paciente lê e rubrica individualmente cada documento. A enfermeira confere
identidade e opera o tablet; não assina como profissional. A via pertence ao
prontuário e à versão apresentada, independentemente de existir agendamento.
Assinatura não confirma atendimento, alta, sessão, guias ou faturamento.
Alergia exige resposta explícita. Respostas que exigem atenção ficam registradas
para avaliação clínica. A coleta não libera procedimento.

TCLE contínuo segue cobertura pelo modelo; termo diário usa a exigência ativa
`SoValeNoDiaDoProcedimento` e data de São Paulo. Configurar os dois modelos
existentes aprovados, sem editar seus textos ou duplicar cadastros.

## Demonstração e pacote

SDK .NET 8 e Python no PATH; em Windows usar PowerShell 7. Node e `npm ci` no
frontend são necessários somente para testes, não para executar o portal.

```powershell
./tools/iniciar-demo-tablet.ps1 -Site ../clinica-site-termos-tablet
```

Cria banco fictício novo, remove a conexão clínica do processo e escuta só em
`http://127.0.0.1:18120`. Credencial fictícia: `demo` / `TabletDemo#2026`.
Interromper com Ctrl+C; bancos ficam em `artifacts/`. Não expor esse modo na internet.

Depois de verificar e fazer commit nos dois checkouts:

```powershell
dotnet tool install dotnet-ef --version 8.0.11 --tool-path .tools
./tools/empacotar-tablet.ps1 -Site ../clinica-site-termos-tablet
```

Se `.tools/dotnet-ef` já existir, não repetir sua instalação. O pacote contém API
Linux x64 autocontida, interface, exemplos, SQL da nova migração e manifesto com
SHA-256 dos arquivos e os dois commits. Não inclui banco, senhas ou chaves; o
script não conecta à VPS nem aplica migrações.

## Instalação e piloto

Usar o console administrativo autorizado da VPS. A conta SSH `clinica-admin`
exige senha para sudo; não inserir essa senha no chat, repositórios ou artefatos.
A inspeção foi somente de leitura: `vps69933`, cerca de 1,35 GiB disponíveis e
48 GiB livres, PostgreSQL 16 e `cloudflared-site.service` ativos. Essa amostra não
substitui medição sob carga.

1. Obter **backup consistente**, restaurar em PostgreSQL 16 isolado com ferramentas
   compatíveis com a versão do dump e ensaiar migração/fluxo nessa cópia. Registrar
   resultado sem dados pessoais. Conferir recuperação das vias e rubricas.
2. Atualizar os leitores desktop para versão com `DocumentosClinicosPdfService`
   desta entrega, que devolve os bytes arquivados. Versões anteriores podem
   reconstruir uma impressão diferente.
3. Conferir `__EFMigrationsHistory`. O SQL pressupõe a base em
   `20260910120000_AssinaturaRemotaGuardadaAntesDaConferencia` e acrescenta
   `20260915102346_ColetaDeTermosNoTablet`. Havendo diferenças, levantar antes as
   migrações pendentes. A API não executa Migrate automaticamente em produção.
4. Criar usuário Linux sem login `clinica-tablet`. Instalar em
   `/opt/clinica-tablet/releases/<identificador>` e apontar o symlink `current`
   para a versão. Código/interface pertencem ao root, legíveis pelo serviço.
   Conferir permissão executável do binário Linux e os hashes do pacote.
5. Copiar `deploy/tablet/portal.env.example` para `/etc/clinica-tablet/portal.env`,
   dono root, modo 0600. Preencher conexão restrita, dois IDs positivos/distintos
   de modelos ativos de termo de procedimento, código aleatório de cadastro e
   diretório de chaves. Manter `Portal__Habilitado=false` até concluir dependências.
6. Criar o grupo `clinica-tablet-proxy` e instalar `clinica-tablet.service`.
   Ela escuta somente no socket Unix `/run/clinica-tablet/portal.sock`,
   tem limite inicial de 512 MiB e não acessa home, backups, certificados ou Docker.
   Conferir suporte às restrições systemd e dependências nativas de ICU/fontconfig;
   medir memória, CPU e tempo do PDF antes de ampliar o piloto.
7. Preparar **portal.clinicasemdormacae.com.br** no túnel existente, tipo Unix,
   serviço `unix:/run/clinica-tablet/portal.sock`, preservando
   `X-Forwarded-Proto: https`. O drop-in `cloudflared-portal.conf` concede ao
   conector somente o grupo de acesso ao socket. As chaves continuam em diretório
   0700 do serviço; o bloqueio TCP do conector para o banco permanece.
   A API recusa HTTP em produção. Desativar cache e log de corpos/query strings
   nessa rota. Preservar a origem/socket institucional e não abrir a porta pública.
8. Com backup/restauração, modelos, permissões, desktop e HTTPS conferidos, definir
   a flag como true e iniciar serviço. `/health` pelo hostname retorna
   `status=ok`, `contrato=1`: isso verifica processo/contrato; banco e PDF precisam
   do percurso autenticado no ambiente de homologação.
9. Ensaiar no **Android físico com Chrome atualizado**: dedo/caneta, retrato,
   paisagem, fonte ampliada, retorno do aplicativo, duplo toque no envio, perda de
   conexão e reentrada da equipe. Usar fictícios em homologação; depois acompanhar
   a enfermeira no piloto autorizado. Não cadastrar fictícios no banco clínico
   como efeito de teste automático.

## Permissões a conferir na cópia restaurada

Papel PostgreSQL próprio, sem superuser, criação de banco/papel/objetos. Não
reutilizar credencial do desktop ou administrador. Conceder CONNECT no banco e
USAGE no schema. O runtime usa os seguintes objetos:

| Acesso | Objetos |
| --- | --- |
| SELECT | `Pacientes`, `Agendamentos`, `Profissionais`, `ModelosDocumento`, `ItensModelo`, `ExigenciasTermo`, `Configuracoes`, `Usuarios` |
| UPDATE de login | Somente `Usuarios.TentativasFalhas`, `BloqueadoAte`, `UltimoAcessoEm` |
| SELECT/INSERT/UPDATE | `DocumentosClinicos`, `ItensDocumento`, `SessoesTablet`, `ColetasTablet` |
| SELECT/INSERT | `TracosAssinatura`, `ViasAssinadasPaciente` |
| INSERT e SELECT de Id (RETURNING) | `Auditoria` |
| USAGE | Sequências dos objetos em que existe INSERT |

Não conceder DELETE ou DDL ao serviço. Os triggers protegem via, conteúdo do
documento, declarações e rubrica arquivados inclusive contra UPDATE fora da API.
Ensaiar esses privilégios com o runtime na cópia restaurada; grants e papéis
existentes são configuração de cada instalação.

Na VPS conferida, PostgreSQL 16 escuta na porta 45432 e autentica conexões locais
por `peer`. Usar o papel `clinica-tablet`, igual ao usuário Linux, pelo socket
`/var/run/postgresql`, sem copiar senha do desktop nem alterar `pg_hba.conf`.
Os modelos existentes são 3 (TCLE contínuo) e 4 (termo diário BSV); conferir
novamente esses IDs no banco de destino antes de preencher a configuração.

Conta da enfermeira: ativa, senha já trocada, permissões `ColherAssinaturaPaciente`
e `VerAgenda`. Alteração de senha, bloqueio, desativação ou retirada de permissão
invalidam o próximo acesso. Código de cadastro do tablet não substitui login.

## Contrato HTTP 1

Token antiforgery obtido em `/api/sessao`, enviado em `X-CSRF-TOKEN` em todo POST.
Cookies HttpOnly/Secure/SameSite Strict. Sessão equipe dura até duas horas; entrega
restringe acesso ao paciente por uma hora. Cadastro do dispositivo dura 30 dias
e trocar o código de cadastro o invalida. Não persistir tokens na interface.

| Endpoint | Escopo/resultado |
| --- | --- |
| `GET /api/sessao` | Estado, CSRF e expiração; não lista pacientes |
| `POST /api/entrar` | Credenciais e, no primeiro uso, código de dispositivo |
| `POST /api/sair` | Revoga sessão; encerra preparações abandonadas |
| `GET /api/dia`, `GET /api/pacientes?q=` | Equipe: agenda reduzida e busca limitada |
| `GET /api/pacientes/{id}` | Equipe: identidade, modelos e histórico |
| `POST /api/preparar` | Equipe: paciente, nascimento, identidade e modelos |
| `GET /api/coletas` | Paciente: somente coletas da própria sessão |
| `POST /api/coletas/{id}/assinar` | Rubrica, respostas, hash e idempotência; 202 é recebimento durável |
| `POST /api/encerrar` | Encerra leitura ou registra recusa nos termos não enviados |
| `POST /api/coletas/{id}/retomar` | Equipe: retoma falha com a mesma submissão |
| `GET /api/documentos/{id}/via` | Equipe: PDF arquivado, hash conferido, acesso auditado |

## Recuperação

| Estado | Conduta |
| --- | --- |
| `preparado` | Paciente lê/responde; rubrica não enviada existe só na página |
| `recebido` | Submissão persistida; aguardar worker, inclusive após reinício |
| `finalizando` | Trabalho transacional; rollback retorna a recebido |
| `arquivado` | Via imutável disponível no prontuário/histórico |
| `falha` | Após cinco tentativas; equipe usa Retomar arquivamento após corrigir causa |
| `recusado` | Recusa registrada com motivo |
| `encerrado` / `expirado` | Preparação encerrada; documento original permanece auditável |

Nova entrada da equipe encerra só preparações abandonadas. Submissões recebidas
e vias permanecem. Com conexão interrompida, conferir estado antes de repetir;
mesma chave/conteúdo é idempotente. Não editar o banco para converter falha em
arquivado. Divergência de conteúdo/hash exige investigação e preservação da via.

A interface não tem modo offline. Recarregar ou alternar o aplicativo pode apagar
uma rubrica ainda não enviada, exigindo reler/rubricar; rubrica recebida fica no
servidor. O paciente pode chamar a equipe ou recusar sem assinar.

## Backup, observação e reversão

Backup completo inclui três novas tabelas, rubricas, documentos, itens e auditoria;
não omitir bytea. Preservar chaves em `/var/lib/clinica-tablet/chaves` e configuração
em armazenamento restrito, separado dos pacotes. Ensaiar restauração e comparar
SHA-256 das vias. Se as chaves não forem recuperadas, recadastrar tablets.

Observar reinícios, memória, falhas de login, tempo de arquivamento e coletas
recebido/falha. Logs operacionais não devem conter nomes, respostas, rubricas,
cookies ou credenciais; a trilha assistencial fica no banco protegido.

Para suspender, retirar rota do portal e parar serviço. Guardar configuração,
chaves, schema e submissões; corrigir/reiniciar permite ao worker retomar.
Reverter aplicação/interface para versão compatível, sem apagar tabelas nem
executar Down: a migração bloqueia remoção de evidências. Manter leitores desktop
compatíveis com as vias arquivadas.
