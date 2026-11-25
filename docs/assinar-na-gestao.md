# Assinar na gestão web (nov/2026)

**O problema:** a médica emitia receitas pela gestão web (`/gestao`) e não conseguia assinar. A gestão não assinava,
porque a área é congelada. O botão "Emitir e assinar" falhava, e no celular parecia que "a aba de assinar não abre".

**Decisão do dono (24/11/2026):** *"Autorizo, na Gestão"*, registrada em `docs/safeid-congelado.md`. A assinatura
vem para a gestão; o motor não muda.

## Como funciona
- `AssinaturaNaGestao` (o `IAssinaturaNaTela` da gestão) leva o pedido ao navegador. O
  `wwwroot/assinatura-na-gestao.js` fala com **as mesmas rotas da API do portal** que o Consultório usa
  (`safeid.js`, `a1.js`), na mesma ordem, com a sessão do portal (mesma origem) e o CSRF de `/api/sessao`:
  - **SafeID:** a página vai à Safeweb **na mesma aba** (sem pop-up, que o Safari do iPhone bloqueia). A volta
    passa pela API e pelo Consultório, que a devolve para `/gestao/?assinatura=`. Lá aparece a confirmação
    explícita **"Assinar e arquivar"**.
  - **A1:** a senha do certificado é pedida no diálogo. O A1 precisa estar cadastrado em "Meu certificado", no
    Consultório.
  - **Antes de autorizar:** "Conferir o PDF", as alergias do cadastro, o endereço da receita (completável ali
    mesmo) e a caixa "Conferi o PDF e as alergias".
- **Documento já emitido, com ou sem atendimento:** assina pela ficha (agenda 0), como o Consultório.
- **Onde se assina na gestão:**
  - "Emitir e assinar" (ao emitir);
  - Clínico → Prescrições → "O que já saiu", com **"Assinar" à vista** na linha e no "⋯";
  - a aba **"Para assinar · N"** da mesma tela: os documentos PRÓPRIOS sem assinatura. É a mesma leitura do
    "Documentos para assinar" do Consultório (`Clinica.Domain.Regras.DocumentosParaAssinar`).
- **Quem vê a aba e o "Assinar":** quem tem o bit de prescrever e um profissional vinculado. A Recepção →
  Documentos continua como era.
- **"⋯" no celular:** o menu saiu de dentro da tabela. No cartão do celular ele ficava sob o fundo que o fecha, e
  o toque do iPhone caía no fundo. Agora o menu é irmão do fundo e, na tela estreita, abre como uma folha no
  rodapé, com alvos de 44 px. Foi testado com toque em 390 px.
- **Saiu:** o "Assinar no Consultório" do PR anterior (#393 no Clinica e #65 no clinica-site).

## O prazo clínico de 15 minutos (correção de 25/11/2026)

**O defeito:** na primeira noite, a médica tocou em assinar na gestão (Clínico → Prescrições) e recebeu "A assinatura digital não está habilitada neste portal". O SafeID estava ligado.

**A causa:** a API só abre o prontuário e a assinatura com **uso humano nos últimos 15 minutos** (`AtendimentoTabletService.AutorizarAsync`, `SessoesTablet.AtividadeClinicaEm`). Esse prazo só se renova pelo ping de toque ou tecla, `POST /api/clinico/atividade`.
- O Consultório faz esse ping; a gestão não fazia.
- Depois de 15 minutos na gestão, toda rota clínica respondia 401.
- A tela trocava qualquer falha (`.catch(() => null)`) por "não habilitada".
- Em produção, às 21:08, as sessões abertas da médica tinham a última atividade clínica entre 55 min e 1h44 antes.

**A correção** (só a camada de tela, `wwwroot/assinatura-na-gestao.js`):
- A gestão faz o **mesmo ping**, pela mesma regra: só depois de toque ou tecla, no máximo a cada 45 s, e antes de cada ação de assinar.
- Nada afrouxa. Depois de 15 minutos **sem uso**, o ping também é recusado e é preciso entrar de novo, como no Consultório.
- A recusa diz o motivo:
  - 401 com a sessão do portal válida: o prazo de 15 minutos;
  - sem sessão: "sua sessão do portal terminou";
  - 403: modo paciente ou sem permissão;
  - outro erro: o código.
- "Não habilitada" só aparece quando o SafeID e o A1 respondem **desligado**.

**Testes:**
- `SafeIdTabletTests.Prazo_clinico_vencido_…`: reproduz o 401 com a sessão válida e mostra que o ping não reabre o prazo;
- `AssinaturaNaGestaoTests`: impede a volta do mascaramento;
- os 7 cenários do JS conferidos num navegador com `fetch` falso.

Nenhuma assinatura real foi feita.

## UX · DX · CX
- 🧑‍💻 **UX:**
  - A médica assina onde emite, no celular ou no computador, sem trocar de tela.
  - Os pendentes ficam numa aba com contador.
  - O "⋯" responde ao toque.
- 🛠️ **DX:**
  - O motor continua intocado; a gestão é só tela e navegação.
  - A regra de "o que falta assinar" mora num lugar só, com teste e tradução para o Postgres.
  - Testes: `AssinaturaNaGestaoTests` (navegador falso; nenhuma assinatura real), `DocumentosParaAssinarTests` e
    `TraducaoNoNpgsqlTests`.
- 🤝 **CX:**
  - Uma assinatura real de teste, feita pela médica depois do deploy. Cada assinatura é cobrada pelo PSC.
  - Nenhuma mudança em como o documento é assinado e arquivado.

## O que não foi feito
- **Infusão** (prescrição e folha de execução) **na gestão:** segue no Consultório, só com o A1.
- **Anamnese:** segue no aplicativo do computador.
- **Cadastrar o A1 pela gestão:** segue em "Meu certificado", no Consultório.
- **Prazo clínico de 15 minutos sem uso:** continua valendo na gestão. Quem fica parado mais que isso entra de novo com a senha. Afrouxar o prazo seria mudar a regra de segurança, e isso não foi pedido.
