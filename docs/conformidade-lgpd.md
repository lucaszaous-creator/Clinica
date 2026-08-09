# Conformidade do prontuário eletrônico — LGPD e Lei 13.787/2018

Parcela 52. Este documento responde, ponto a ponto, a **auditoria de fornecedor** que a
clínica enviou: dez itens que ela verificaria antes de contratar um prontuário eletrônico.

A base legal é a que ela mesma apontou: **LGPD (Lei 13.709/2018)** e **Lei 13.787/2018**,
que trata especificamente da digitalização, guarda, armazenamento e manuseio de prontuários
e exige preservar **integridade, autenticidade e confidencialidade**.

Ele é escrito para ser LIDO POR QUEM AUDITA, e não por quem programa. Onde o sistema não
atende, está escrito que não atende — fornecedor que marca dez de dez na primeira leitura
está respondendo o que o cliente quer ouvir, não o que o sistema faz.

## Placar

| # | Exigência | Estado |
|---|---|---|
| 1 | Acesso individualizado por usuário | ✅ Atende |
| 2 | Controle de acesso por perfil | ✅ Atende |
| 3 | Registro de auditoria (quem acessou, quando, o que fez) | ✅ Atende (desde a parcela 52) |
| 4 | Proteção contra alteração indevida do prontuário | ✅ Atende (desde a parcela 52) |
| 5 | Segurança do armazenamento e da transmissão | ✅ Atende — com uma parte que é do subprocessador |
| 6 | Backup e recuperação | ✅ Atende (desde a parcela 52) |
| 7 | Prazo de guarda adequado (20 anos) | ✅ Atende (desde a parcela 52) |
| 8 | Exportação do prontuário | ✅ Atende (desde a parcela 52) |
| 9 | Política para incidentes de segurança | ⚠️ Parcial — o sistema detecta e registra; o **procedimento** é da clínica |
| 10 | Contrato de tratamento de dados | ⚠️ Pendente — **não é código**, e há um ponto que exige decisão |

---

## 1. Acesso individualizado por usuário ✅

Cada pessoa tem login próprio (`UsuarioSistema`). Não existe senha compartilhada, e os
**cinco aplicativos** exigem autenticação.

| O quê | Como |
|---|---|
| Senha guardada | **PBKDF2-HMAC-SHA256, 210.000 iterações**, sal de 128 bits por usuário. Nunca em claro, nem no banco nem no log. |
| Força da senha | Recusada na criação e na troca (`HashSenha.Criticar`). |
| Tentativa e erro | Trava a conta após N tentativas; o travamento vai para a trilha. |
| Enumeração de usuários | Mensagem única para login inexistente **e** senha errada — não dá para descobrir quem tem conta. |
| Trocar a própria senha | Exige a senha atual, para estação deixada aberta não virar troca de senha alheia. |
| Primeira senha | Nasce com troca obrigatória. |
| Sair sem fechar o app | "Trocar usuário" nos cinco aplicativos. |

**Por que "Trocar usuário" está nesta lista:** no balcão, duas pessoas dividem a mesma
máquina. Sem saída, a segunda trabalha com o login da primeira, e a trilha do item 3 passa
a assinar o nome errado. Login sem saída desfaz em silêncio a razão de o login existir.

## 2. Controle de acesso por perfil ✅

Perfis (`PerfilAcesso`) com **permissões granulares** (mais de 24 bits), concedidas ou
negadas **por pessoa** sem alterar o perfil dos demais. A permissão efetiva é resolvida na
leitura — `padrão do perfil + extras − negadas` —, e **negada vence extra**.

O corte entre recepção e clínica é o da própria LGPD, e responde ao exemplo que a clínica
deu (*"a recepcionista não precisa ter o mesmo acesso clínico que o médico"*):

| Bit | Alcança | Natureza do dado |
|---|---|---|
| `VerFichaPaciente` / `EditarPaciente` | cadastro, contato, convênio | dado pessoal |
| `VerProntuario` / `EditarProntuario` | evolução, avaliações, medidas | **dado sensível de saúde** (art. 5º, II) |

Antes da parcela 49 esses quatro eram dois: quem precisava do cadastro para marcar horário
lia a evolução inteira de todo mundo. A separação **tirou** esse acesso, de propósito.

Toda ação que grava tem **duas barreiras**: o botão desabilitado (que explica) e a
verificação no comando (que impede) — só desabilitar é enfeite, porque atalho de teclado
passa direto.

Tabela completa por perfil: [`docs/permissoes-por-perfil.md`](permissoes-por-perfil.md).

## 3. Registro de auditoria ✅

> *"Esse é um dos pontos que eu mais valorizaria: conseguir saber quem acessou determinado
> prontuário, quando acessou e, idealmente, o que realizou."*

São três perguntas, e até a parcela 52 o sistema respondia **só a terceira**. A trilha
gravava 55 tipos de ação e todas eram **escrita**: abrir o prontuário de alguém e ler tudo
não deixava rastro nenhum.

Isso importa porque o acesso indevido clássico numa clínica é **leitura** — a funcionária
que abre o prontuário da vizinha, do ex-marido, de alguém conhecido. A permissão do item 2
limita *quem pode* abrir; ela não responde *quem abriu*, e numa clínica pequena quase todo
mundo tem permissão legítima sobre quase todo mundo.

**O que passou a ser registrado:**

| Pergunta dela | Resposta do sistema |
|---|---|
| Quem acessou | login de quem abriu (`AcessoProntuarioService`) |
| Quando acessou | data e hora, e **por qual porta** — ficha, prontuário clínico, atendimento, documento ou exportação |
| O que realizou | as 55 ações de escrita, gravadas **na mesma transação** do ato |

Duas decisões que valem explicar a quem audita:

- **Janela de silêncio de 30 minutos.** Um atendimento de vinte minutos entre quatro abas
  do mesmo paciente geraria quinze linhas idênticas. Trilha que ninguém consegue ler é
  trilha que ninguém lê. A janela agrupa **um atendimento**, não um turno: quem abre o
  mesmo prontuário de manhã e à tarde fez dois acessos, e fundi-los esconderia justamente o
  padrão que uma investigação procura.
- **A trilha é somente leitura.** Não há exclusão nem edição, nem no código nem na tela.
  Registro de auditoria que se pode apagar não é auditoria, é rascunho.

A ação é gravada **no mesmo `SaveChanges`** do ato que a originou: não existe baixa,
alteração ou cancelamento que aconteça sem a linha correspondente.

## 4. Proteção contra alteração indevida ✅

> *"Uma evolução médica já registrada não deveria simplesmente poder ser apagada ou
> modificada silenciosamente."*

Até a parcela 52 ela podia ser **as duas coisas**, e este era o ponto mais grave de toda a
auditoria — o único em que o sistema fazia ativamente o que a lei proíbe.

**O que havia:**

- exclusão **física** em quatro caminhos: evolução (levando os anexos junto), anexo,
  avaliação clínica e medida;
- alteração **por cima**: a evolução era sobrescrita no lugar, e a trilha gravava
  `"EvolucaoAlterada — Sessão de 12/03/2026"`. O texto anterior desaparecia.

Trilha que registra *que* mudou sem registrar *o que* mudou não responde a única pergunta
que se faz a um prontuário eletrônico numa perícia.

**O que há agora:**

| Antes | Agora |
|---|---|
| `ExcluirAsync` apagava a sessão | `CancelarAsync` **exige motivo escrito**; a sessão sai do prontuário que se lê e **continua guardada**, marcada |
| Anexo, avaliação e medida idem | idem, com motivo |
| Correção sobrescrevia o texto | cada correção **congela a versão anterior** (`VersaoEvolucao`), com data, autor e motivo |
| Métodos de exclusão no repositório | **removidos da interface** — enquanto existirem, alguma tela futura vai chamá-los |

O último item é o que sustenta os outros. Há um teste automatizado que falha se alguém
reintroduzir um método de exclusão de registro clínico.

**Por que uma tabela de versões, e não uma linha nova por correção:** a evolução é escrita
em várias passadas durante o atendimento. Gravar uma linha nova a cada Salvar criaria seis
"sessões" no prontuário para uma consulta que houve uma vez, e o prontuário passaria a
mentir sobre quantas vezes o paciente veio. A sessão continua sendo uma; o que ela já foi
fica ao lado, recuperável e exportável.

## 5. Segurança do armazenamento e da transmissão ✅

| Camada | Medida |
|---|---|
| Transmissão app ↔ banco | TLS com **`SslMode.VerifyFull`**: criptografa **e valida** o certificado do servidor, conferindo o nome do host |
| Autenticação no banco | SCRAM, com *channel binding* negociado pelo Npgsql |
| Credencial na máquina | criptografada com **DPAPI** (chave do usuário do Windows), em `%APPDATA%` |
| Senhas de usuário | PBKDF2, ver item 1 |
| Armazenamento em repouso | criptografia do provedor (Neon) — ver a ressalva abaixo |
| Concorrência | controle otimista (`xmin`): duas pessoas não sobrescrevem uma à outra em silêncio |

**Mudança da parcela 52:** o modo TLS era `Require`, que criptografa e **não valida** o
certificado. Isso protege contra quem observa o tráfego e não contra quem se apresenta como
sendo o banco — e é prontuário que passa por esse cano. `VerifyFull` fecha essa porta e não
exige configuração da clínica.

⚠️ **A parte que não é nossa.** A criptografia em repouso é do provedor de banco, não do
nosso código. Ela é adequada, mas é **declaração de subprocessador** e pertence ao contrato
do item 10 — não a uma promessa nossa.

## 6. Backup e recuperação ✅

> *"Não adianta impedir invasão se uma pane puder fazer desaparecer dez anos de
> prontuários."*

O sistema tinha a **ferramenta** desde a parcela 34 e não tinha a **política**: era um botão
em Configurações. Backup que depende de alguém lembrar de clicar toda semana existe no
manual e não no disco.

| Item | Como funciona |
|---|---|
| Escopo | **base inteira** — todas as tabelas, inclusive anexos e PDFs assinados (bytes inclusos) |
| Frequência | automática, configurável (padrão **7 dias**), disparada na abertura do Gerente |
| Destino | pasta escolhida pela clínica (rede ou nuvem sincronizada) |
| Redundância | **várias cópias**, com rotação (padrão: as **8 mais recentes**) |
| Conferência | o backup traz **manifesto** e há função de conferir o arquivo sem restaurar |
| Restauração | testada, e **recusa base que não esteja vazia** |
| Falha | não impede o app de abrir; vira aviso na tela e registro no log |

**Por que várias cópias e não uma:** guardar só a última é o erro clássico — a corrupção que
ninguém percebeu na sexta é copiada por cima da única cópia boa no sábado.

**Por que na abertura do Gerente e não num agendador:** o sistema é desktop e não tem
serviço residente; inventar um daria mais uma peça para quebrar em silêncio.

⚠️ **O que a clínica precisa fazer, e o sistema não tem como conferir:** a pasta de destino
deve ficar **fora da máquina** — unidade de rede ou nuvem sincronizada. Cópia gravada no
mesmo computador não é redundância contra incêndio, furto ou ransomware. Nenhum caminho de
arquivo diz onde ele fisicamente está, então isto é orientação, não promessa do código.

## 7. Prazo de guarda ✅

**20 anos a partir do último registro** (Lei 13.787/2018, art. 6º), e a contagem é do
registro **mais recente** de qualquer natureza — sessão, avaliação, medida ou documento
emitido —, nunca do primeiro. Contar do cadastro faria o prontuário de quem ainda se trata
ficar elegível a eliminação com sessões recentes dentro dele.

O prazo é **constante em código, não configuração**: é prazo legal, não política da clínica.
Deixá-lo editável permitiria baixá-lo numa tela sem ninguém perceber.

**A garantia tem três metades, e as três são desta parcela:**

1. o sistema **não apaga** registro clínico (item 4);
2. o sistema **calcula** o prazo e mostra a situação por paciente e da clínica inteira;
3. o dado **sai** em formato aberto se a clínica trocar de fornecedor (item 8).

⚠️ **O sistema não elimina nada ao vencer o prazo.** Vencido, o prontuário fica *elegível* —
a decisão de eliminar é da clínica, com a comissão de revisão que o art. 7º prevê, e é
irreversível. Eliminar automaticamente seria a pior leitura possível desta lei: o prazo é
**piso de guarda**, não agendamento de destruição.

⚠️ **Duas ressalvas honestas sobre guardar "só no sistema":**

- **Assinatura digital e o tempo.** Os documentos assinados com ICP-Brasil usam PAdES-B.
  O arquivo continua íntegro por 20 anos, mas **deixa de se validar sozinho** quando o
  certificado de quem assinou expira (tipicamente 1–3 anos), porque não embutimos
  CRL/OCSP (LTV/PAdES-LT). O documento não perde valor por isso — a prova de integridade
  segue no arquivo e na trilha —, mas um validador público passará a dizer "não foi
  possível verificar a cadeia". **Implementar LTV está na lista do que falta.**
- **Substituir o papel.** Para o prontuário eletrônico *substituir* o arquivo físico (isto
  é, a clínica poder descartar o papel), a norma pede certificação **SBIS/CFM (S-RES,
  nível NGS2)**. Isso é certificação de produto, não configuração — e este sistema **não a
  tem**. Enquanto não houver, a orientação é: o sistema guarda **e** o papel também.

## 8. Exportação do prontuário ✅

> *"A clínica não deveria ficar refém do fornecedor."*

Três saídas, para três perguntas diferentes:

| Pergunta | Saída | Formato |
|---|---|---|
| "Quero meus dados" (paciente, art. 18, II) | dados do titular | texto legível |
| "A base sumiu" | backup completo | JSON restaurável, **com os bytes** dos anexos |
| "Vamos trocar de fornecedor" | exportação do prontuário | **CSV**, que qualquer sistema importa |

A exportação em CSV leva cadastro, sessões, **versões anteriores**, avaliações, medidas,
documentos e a lista de anexos, e vai acompanhada de um `LEIA-ME.txt` que explica o formato
e a obrigação de guarda que segue com os dados.

Duas escolhas que a auditoria deve conhecer:

- **As sessões canceladas vão junto, marcadas.** Elas fazem parte do prontuário sob guarda,
  e omiti-las entregaria ao próximo fornecedor uma versão higienizada do histórico —
  exatamente o que a exigência de integridade existe para impedir.
- **Os bytes dos anexos não vão no CSV.** Um arquivo com laudos em base64 vira centenas de
  megabytes que ninguém abre nem confere. O CSV **lista** os anexos para o destino saber o
  que precisa receber; os arquivos saem íntegros no backup completo. Prometer o contrário
  seria entregar uma garantia aparente.

## 9. Política para incidentes ⚠️ Parcial

O que o **sistema** faz hoje:

- registra tentativas de login que travam a conta (`LoginTravado`);
- registra todo acesso a prontuário e toda ação de escrita, o que permite **investigar** o
  alcance de um incidente — quem viu o quê e quando;
- registra falhas técnicas em log mensal rotacionado, com botão para abrir a pasta.

O que **falta**, e não é código:

- procedimento escrito de **detecção, registro e resposta**;
- responsável nomeado e prazo interno de escalonamento;
- modelo de comunicação à **ANPD** e aos titulares, exigida quando o incidente puder
  acarretar risco ou dano relevante (LGPD, art. 48).

Isto é documento a redigir com a clínica, não funcionalidade a programar. O sistema entrega
a **capacidade de investigar**, que é a parte que depende de software; quem decide comunicar
é a controladora.

## 10. Contrato de tratamento de dados ⚠️ Pendente — e há uma decisão a tomar

A estrutura é a que a clínica descreveu:

| Papel | Quem |
|---|---|
| **Controladora** | a clínica — decide as finalidades do tratamento |
| **Operador** | o fornecedor do software |
| **Suboperador** | o provedor de banco de dados (Neon) |

Falta redigir o contrato com responsabilidades sobre tratamento, confidencialidade,
segurança, subcontratados, incidentes, backup, término e devolução/eliminação dos dados.

⚠️ **O ponto que não estava na lista dela e é o mais sério deste item: transferência
internacional.**

O banco de dados fica **fora do Brasil**. Prontuário de paciente brasileiro hospedado no
exterior é **transferência internacional de dados** (LGPD, art. 33) e exige base legal
específica — as **cláusulas-padrão contratuais** aprovadas pela ANPD em 2024, ou outra
hipótese do artigo.

Há dois caminhos, e a escolha é da clínica:

1. **Manter onde está** e cobrir contratualmente, com as cláusulas-padrão da ANPD e o
   registro do suboperador.
2. **Migrar a base para região brasileira**, o que simplifica a conformidade e elimina a
   discussão. Tecnicamente é uma mudança de string de conexão mais a migração dos dados; não
   afeta o código.

Recomendamos avaliar a opção 2 antes de assinar contrato, porque ela **remove** um requisito
em vez de administrá-lo.

---

## O que falta, em uma lista

Para não deixar dúvida sobre o que este documento não cobre:

| Item | Natureza | Quem resolve |
|---|---|---|
| LTV / PAdES-LT na assinatura | código | fornecedor |
| Certificação SBIS/CFM (S-RES) | certificação de produto | fornecedor + entidade certificadora |
| Procedimento de resposta a incidentes | documento | clínica, com apoio do fornecedor |
| Contrato de tratamento (DPA) | contrato | clínica + fornecedor |
| Base legal para transferência internacional | contrato **ou** migração de região | clínica decide |
| Destino do backup fora da máquina | configuração | clínica |

## Onde isso está no código

| O quê | Onde |
|---|---|
| Prazo de guarda (a regra) | `Domain/GuardaProntuario.cs` |
| Situação da guarda por paciente e da clínica | `Application/Servicos/GuardaProntuarioService.cs` |
| Cancelamento com motivo (sessão, anexo) | `Application/Servicos/ProntuarioService.cs` |
| Versões anteriores da evolução | `Domain/Entities/Prontuario.cs` (`VersaoEvolucao`) |
| Trilha de LEITURA do prontuário | `Application/Servicos/AcessoProntuarioService.cs` |
| Trilha de escrita e sua tela | `Application/Servicos/AuditoriaService.cs` |
| Exportação em formato aberto | `Application/Servicos/ExportacaoProntuarioService.cs` |
| Backup, conferência e restauração | `Infrastructure/BackupService.cs` |
| Política de backup (prazo, destino, rotação) | `Infrastructure/PoliticaBackupService.cs` |
| Senha, travamento e perfis | `Domain/HashSenha.cs`, `Application/Servicos/AcessoService.cs` |
| TLS e credencial local | `*/Configuracao/ConexaoStore.cs` |
| Testes destas garantias | `tests/Clinica.Tests/ConformidadeProntuarioTests.cs` |

## Fontes

- [LGPD — Lei 13.709/2018](https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709.htm)
- [Lei 13.787/2018 — digitalização e guarda de prontuários](https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13787.htm)
- [ANPD — cláusulas-padrão contratuais para transferência internacional](https://www.gov.br/anpd/pt-br/assuntos/noticias/anpd-aprova-clausulas-contratuais-padrao-para-transferencia-internacional-de-dados)
- [CFM — Resolução 1.821/2007 (guarda de prontuários e SBIS/CFM)](https://sistemas.cfm.org.br/normas/visualizar/resolucoes/BR/2007/1821)
- [SBIS/CFM — certificação de S-RES](https://sbis.org.br/certificacao)
- [OWASP — Password Storage Cheat Sheet (parâmetros do PBKDF2)](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)
