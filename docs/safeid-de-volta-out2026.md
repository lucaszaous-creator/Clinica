# SafeID de volta para receitas e documentos — 27/10/2026

Pedido da clínica, autorizado expressamente pela direção em 27/10/2026: o SafeID (Safeweb,
certificado A3 em nuvem) volta a assinar **receitas e documentos clínicos, no sistema e no
portal**. A **infusão** (prescrição, folha de execução, enfermagem) continua **só com o A1**.
A1 e SafeID convivem: o médico escolhe na hora de assinar. Mesmas credenciais Safeweb de antes.

Decisão do dono: sem homologação prévia — a médica testa a emissão real direto em produção.
Isso contraria o roteiro de `docs/safeid-congelado.md` (homologar antes) e fica registrado aqui.

## O que voltou

- **Motor** (`Application/Assinatura/SafeID/`): restaurado tal e qual estava antes de `9b002a4d`
  (01/10/2026), com o `SafeIDTests` de então. `CertificadoAssinatura.AssinadorRemoto` voltou e o
  `AssinaturaDigitalService` o usa quando o certificado é de nuvem. `BuscaNoSafeID` (novo) é o
  antigo `BuscarNaNuvemAsync` do `EscolherCertificadoViewModel` WPF, movido sem mudar a sequência.
- **Credenciais**: `ParametrosService.Obter/SalvarCredenciaisSafeIDAsync` (as chaves de banco de
  antes, `SafeIDClientId`/`SafeIDClientSecret`/`SafeIDAmbiente`), com a tela em Configurações →
  Integrações → "Assinatura pelo SafeID". O ambiente (`SafeID__ClientId` e afins) vence o banco.
- **Sistema (Blazor)**: o seletor de certificado ganha "Usar SafeID" quando aberto para documento
  (`IAssinaturaNaTela.EscolherCertificadoParaDocumentoAsync`) e a clínica tem credenciais. A
  infusão abre o seletor pelo método de sempre e não vê a opção.
- **Documentos assináveis de novo**: a lista "emitido só para impressão" (`EmissaoSemCertificado`,
  02/10/2026) saiu. Assinar continua opcional: a via impressa vale como antes.
  - Central de Documentos: ⋯ → "Assinar com o e-CPF…".
  - Emissão: botão "Emitir e assinar" ao lado de "Emitir e imprimir". Desistir no seletor
    entrega a via de sempre; falhar diz que o documento já foi emitido e manda assinar pela central.
- **Portal** (`Clinica.Assinaturas.Api` + `clinica-site`): o contrato HTTP de antes de 01/10
  (`/api/clinico/safeid`, `.../{tipo}/{documento}/safeid`, `/safeid/retorno`,
  `/api/clinico/safeid/{id}` e `/concluir`), ligado por `Portal:SafeId:Habilitado`, só para
  `documento`; infusão e execução recusadas antes de qualquer chamada à Safeweb.

## Para ligar

**Sistema:** publicar os apps que assinam documentos (Consultório, Recepção e Gerente) e
conferir em Configurações → Integrações se o client_id está lá (as credenciais antigas não foram
apagadas em 01/10). Se o segredo foi gravado cifrado por outra máquina, digitar de novo.

**Portal (VPS, uma vez, com sudo):**
1. `deploy/tablet/homologacao/safeid-proxy.py` → `/opt/clinica-safeid-proxy/safeid-proxy.py`;
   `deploy/tablet/clinica-safeid.service` → `/etc/systemd/system/`;
   `systemctl enable --now clinica-safeid` (127.0.0.1:18744, túnel só para os hosts da Safeweb).
2. `/etc/clinica-tablet/safeid.env` (root:root, 0600): `Portal__SafeId__ClientId`,
   `Portal__SafeId__ClientSecret`, `Portal__SafeId__Retorno=https://portal.clinicasemdormacae.com.br/safeid/retorno`,
   `Portal__SafeId__Homologacao=false`.
3. `deploy/tablet/clinica-tablet-safeid.conf` → `/etc/systemd/system/clinica-tablet.service.d/safeid.conf`
   (`Wants`, não `Requires`: o proxy caído não derruba o portal nem o A1). Antes, conferir com
   `systemctl cat clinica-tablet` se sobrou o drop-in antigo `posto.conf` (set/2026).
4. No `portal.env`: `Portal__SafeId__Habilitado=true`, `HTTPS_PROXY=http://127.0.0.1:18744`,
   `NO_PROXY=localhost,127.0.0.1`. A unit continua com `IPAddressDeny=any`.
5. `systemctl daemon-reload && systemctl restart clinica-tablet`, e publicar o pacote com o
   `clinica-site` novo (a página sem a rota do SafeID mostra só o A1, então pode ir antes).

As autorizações do portal ficam em memória (5 minutos): reiniciar o portal pede nova autorização.

## UX · DX · CX

- 🧑‍💻 **UX** — o médico vê o SafeID onde já assinava (o seletor, a central, a emissão), com o
  QR Code no celular; quem não tem SafeID continua no A1 sem mudança. A espera da confirmação
  (até 3 min) tem Cancelar. Na infusão nada muda.
- 🛠️ **DX** — o motor voltou idêntico ao que a Safeweb aceitou em ago/2026 (sete rodadas em
  produção), com os testes de então. Peças novas: `BuscaNoSafeID`/`FabricaDoClienteSafeID`, um
  método na porta da assinatura e o bloco de Configurações. A regra do que pode ser assinado mora
  em `AssinaturaDeDocumentoNaTela.PodeAssinar`, com teste.
- 🤝 **CX** — cada tentativa é cobrada pelo PSC e não houve homologação: o primeiro teste da
  médica é real. Os testes da casa usam PSC simulado e certificado autoassinado — verde aqui
  não prova a Safeweb. Rollback do sistema: release anterior dos apps; do portal:
  `Portal__SafeId__Habilitado=false` e restart.
