# A gestão no navegador e a entrada única do portal (nov/2026)

> Pedido da cliente: *"que a gerente tenha acesso remoto … com o usuário de gerente ter todo o
> sistema de gestão que hoje a gente tem no módulo de gerente"*. Decisão da direção: um login só
> no portal, **sem o código do tablet**, que leva cada perfil às suas funções; o gerente encontra
> **Gestão · Financeiro (com o Faturamento) · Recepção · Clínico**; e o acesso endurecido.

## O que é

| Peça | Onde | O que faz |
|---|---|---|
| Entrada do portal | `Clinica.Assinaturas.Api` (`/api/entrar…`) + `clinica-site` | senha → autenticador pessoal (TOTP) → sessão; aparelho confiável por 30 dias; destinos por perfil |
| Segundo fator | `SegundoFatorPortalService`, `Totp`, migration `SegundoFatorDoPortal` | cadastro por QR, códigos de recuperação, travamento, trilha |
| Destino | `AcessoAoPortal` (Application) | gerente → gestão; quem atende ou colhe assinatura → posto |
| Gestão web | `src/Clinica.Gestao.Web` (Blazor Server, `/gestao`) | as MESMAS telas e a MESMA moldura dos apps |
| Catálogo | `tools/gerar-catalogo-gestao-web.py` → `catalogo-gestao.json` | o menu lido dos módulos do Gerente Geral; checagem 50 do `verificar-suite` |
| Sessão por conexão | `SessaoUsuario` (modo servidor) + `AtividadeComSessao` | cada conexão com a sua sessão; sem login, nega |

## Como a gestão reconhece quem entrou

1. A pessoa entra pelo portal (senha + código do celular). A API grava dois cookies `__Host-`,
   `HttpOnly`, `Secure`, `SameSite=Strict`: a sessão (token aleatório; o banco guarda só o
   SHA-256) e o aparelho (cifrado pela proteção de dados da API).
2. `/gestao` é o MESMO endereço: os cookies chegam sem CORS. A porta da gestão
   (`Program.cs`) abre o cookie do aparelho com o MESMO anel de chaves (em leitura) e confere a
   sessão no banco (`SessaoDoPortalNaGestao`): modo equipe, não vencida nem revogada, do mesmo
   aparelho, senha não trocada desde a entrada, usuário ativo e destravado, **perfil Gerente**.
3. A página recebe só o id da sessão (o resumo, nunca o token). A conexão Blazor reconfere ao
   abrir e **a cada minuto**: sair do portal, trocar a senha ou perder o perfil derruba a gestão
   aberta em até um minuto.
4. Cada atividade da conexão roda com `SessaoUsuario.Atual` = a sessão DESTA conexão. Sem
   sessão, a sessão do processo é a que **nega tudo** (`AtivarModoServidor`) — no desktop, sem
   login, libera; num servidor isso seria uma porta aberta.
5. Os "pedidos entre telas" (abrir a ficha de um paciente em outra tela) são singleton no app;
   na gestão são **por conexão**, para o pedido de uma pessoa nunca aparecer na tela de outra.

## Endurecimento

| Camada | O que está no código |
|---|---|
| Entrada | **segundo fator opcional, com convite a cada login** até a pessoa ativar (decisão da direção, ver abaixo); TOTP pessoal (RFC 6238, um uso por código), 10 falhas (código ou recuperação) travam; limite por aparelho/origem; entrada pendente em cookie cifrado de 5 min; confiança no aparelho por pessoa, presa à versão da senha |
| Navegador | CSP sem origem externa e sem script em linha; `frame-ancestors 'none'`; HSTS; `nosniff`; `no-store`; COOP/CORP same-origin; **nenhum CORS** (nem cabeçalho `Access-Control-*`) |
| WebSocket | o `/_blazor` só é aceito com `Origin` = `Gestao:Origem` (contra sequestro de WebSocket entre sites); mensagem de entrada limitada a 64 KB; arquivos sobem e descem por stream |
| Servidor | só `/gestao` (o resto é 404); socket Unix que só o cloudflared alcança; systemd com usuário próprio, sem saída de rede, memória no teto; limite de 300 req/min por origem |
| Banco | papel `clinica-gestao` só com DML (sem DDL, sem migration, dono de nada); auditoria só-acréscimo; **raízes do prontuário sem DELETE** para este papel (`deploy/gestao/papel-gestao.sql`); o papel do portal (`clinica-tablet`) recebe o mínimo nas tabelas novas |
| CSV | neutralização de fórmula (`CsvParaExcel`) — vale para os apps também |

**Sobre RLS.** A autorização aqui é por PERMISSÃO (bits do perfil), não por "dono da linha":
uma política por linha repetiria, no banco, a régua que a aplicação já aplica — e para o gerente
(que tem todas as permissões) liberaria tudo de qualquer forma. A segunda tranca que faz sentido no
banco foi aplicada em nível de papel: privilégio mínimo por serviço, nada de DDL, auditoria
só-acréscimo e as raízes do prontuário sem DELETE para o papel web. Se um dia existir dado por
profissional que o gerente NÃO deva ver, aí uma política RLS por `ProfissionalId` passa a valer.

## O que fica de fora, e a tela diz

- **Assinatura digital** (área congelada, `docs/safeid-congelado.md`): a gestão web não assina —
  `AssinaturaForaDaGestao` responde "no aplicativo do computador ou no Consultório do portal".
- **Escolher uma pasta** (backup, exportação do prontuário em pasta): "só no aplicativo".
- **Sino e videoaulas**: o vigia dos pedidos e respostas é do shell do app; a moldura da web os esconde.

## Implantação (VPS)

Ordem — cada passo só depois do anterior conferido:

1. **Publicar um app da suíte** (Gerente) com a migration `SegundoFatorDoPortal` — os apps aplicam
   migration na abertura; o portal e a gestão não migram.
2. **Papel do banco:** `psql -d <banco> -v desktop=<papel-dono> -f deploy/gestao/papel-gestao.sql`.
3. **Chaves em leitura:** `groupadd clinica-portal-chaves`; `chgrp -R clinica-portal-chaves
   /var/lib/clinica-tablet/chaves`; `chmod 0750 /var/lib/clinica-tablet/chaves`; `chmod 0640` nos
   XML; `/var/lib/clinica-tablet` com `0710` (atravessar, não listar) — e `StateDirectoryMode=0710`
   na unidade da API.
4. **Usuário e unidade:** `useradd -r clinica-gestao`; `groupadd clinica-gestao-proxy`; publicar
   `dotnet publish src/Clinica.Gestao.Web -c Release -r linux-x64 --self-contained` em
   `/opt/clinica-gestao/<release>/app` com o link `current`; `gestao.env` a partir do exemplo;
   `deploy/gestao/clinica-gestao.service`.
5. **Túnel:** a regra de `/gestao` ANTES da do portal (`deploy/gestao/cloudflared-gestao.md`).
6. **Portal:** publicar a API e o `clinica-site` juntos (o login novo depende dos dois) pelo
   `atualizar-posto.py`, que concede ao papel do portal as tabelas do segundo fator.
7. **Conferir em produção:** entrar como enfermagem (cadastro do autenticador → posto), como
   gerente (→ gestão, as quatro áreas), sair (a gestão volta ao login em até 1 min), e a
   recepção que digita `/gestao` volta ao login.

**Depois da primeira instalação**, o merge na `main` publica o portal e a gestão sozinho:
`docs/deploy-automatico.md`.

**O segundo fator é OPCIONAL, com convite (decisão da direção, nov/2026).** Para não travar quem
não tem familiaridade técnica (e não virar chamado de suporte), quem ainda não ativou entra só com a
senha, e o portal mostra, **a cada login**, o convite "Ativar agora / Agora não" até a pessoa ativar.
A ativação é feita por dentro, já logado (`/api/conta/segundo-fator/iniciar` e `/ativar`). Quem
ativou passa a precisar do código do celular (ou do aparelho confiável). Quem perder o celular: a
direção usa "Zerar o autenticador" na tela de Acessos, e a pessoa volta a entrar só com a senha.

⚠️ **O risco aceito:** enquanto a pessoa não ativa, a conta dela vale o que vale a senha. Para a
GESTÃO (o sistema inteiro pela internet) existe a chave `Gestao__ExigirSegundoFator=true`, desligada
por padrão: ligada, a gestão só abre para quem já ativou. Recomendação: ligar assim que o gerente ativar.

**Achados da auditoria de segurança (metodologia security-audit da Cloudflare, escopo: autenticação
do portal), nov/2026:** (1) *médio* — o primeiro cadastro do autenticador exigia só a senha. Corrigido com um código de
liberação da direção e, depois, **revertido por decisão da direção** em favor do segundo fator
opcional com convite (o risco aceito está descrito acima). (2) *corrigido* — `AcessoAoWhatsApp.Pode` liberava a sessão fechada
do servidor. Rejeitados com a razão no relatório: força bruta do TOTP em paralelo (token de
concorrência), troca do cookie pendente, enumeração de usuários, aparelho confiável após troca de
senha, XSS pelo SVG do QR.

## UX · DX · CX

- 🧑‍💻 **UX** — Um login para todos; cada um cai no que faz. A gerente tem as telas que já conhece,
  no computador e no celular (barra das áreas embaixo, abas que deslizam). A enfermagem ganha um
  passo só no primeiro acesso de cada aparelho (marcar "confiar" no tablet da sala).
- 🛠️ **DX** — Nenhuma tela reescrita: o catálogo é gerado dos módulos, e tela nova no app chega à
  web pela checagem 50. As peças novas têm teste: `TotpTests` (vetores da RFC), `SegundoFatorPortalServiceTests`,
  `SegundoFatorHttpTests` (fluxo completo pela API), `SessaoPorConexaoTests`, `Clinica.Gestao.Web.Tests`
  (a porta e o catálogo), `CsvParaExcelTests`.
- 🤝 **CX** — Sem máquina nova (mesma VPS). O sistema de gestão fica na internet atrás de senha +
  segundo fator, com trilha, e o banco recusa apagar prontuário pelo papel web. O custo é a
  mudança no login da equipe, que precisa de aviso.
