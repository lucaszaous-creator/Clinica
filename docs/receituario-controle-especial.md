# Receituário de Controle Especial (nov/2026)

**Por quê.** A receita 2026/0484 (claritromicina + amoxicilina) foi recusada na farmácia. Antimicrobiano (RDC
471/2021) e a lista C1 da Portaria 344 pedem o **Receituário de Controle Especial**: duas vias (farmácia e
paciente), emitente e comprador identificados. O modelo é o do ITI v2.0. A referência do dono está em
`Shared/Clinica/receitas/Receituario-Controle-Especial-…-24-11-2026.pdf`. Autorização da área congelada:
`docs/safeid-congelado.md`, 2ª autorização de 24/11/2026.

## O que a médica faz

1. Em qualquer porta de emissão, escolha o papel **Receituário de Controle Especial**:
   - Consultório (/profissional): "Nova prescrição" → Documento.
   - Gestão → Clínico → Prescrições/Documentos: chip do tipo, a mesma `DocumentoForm`.
   - App Clínico (desktop): a mesma `DocumentoForm`.
   - Ou escreva uma **Receita** comum: se ela tiver antimicrobiano ou C1, a folha avisa *"a farmácia exige o
     Receituário de Controle Especial"* e oferece **Emitir como Receituário de Controle Especial**. O texto é
     mantido.
2. **Emitente:** ninguém digita. Ele vem do profissional logado (nome, conselho, número e UF) e da clínica
   (endereço, cidade e UF, telefone). A data é a da emissão.
3. **Comprador:** é o PACIENTE (nome, RG, órgão emissor, endereço, cidade, UF e telefone). Se faltar algo na
   ficha, um modal pede só o que falta antes de numerar (no portal, antes de liberar a assinatura). O que se
   preenche grava na ficha, com auditoria `DadosCompradorCompletadosNaEmissao`: o registro guarda só os nomes dos
   campos, nunca os valores. O bloco **Fornecedor** fica em branco, para a farmácia.
4. **Assinar** com SafeID ou A1, como qualquer receita. O carimbo cai **dentro** do campo "ASSINATURA MÉDICO(A)"
   nas duas vias.
5. **Depois de assinado:**
   - **Enviar ao paciente pelo WhatsApp**, um clique: a mesma fila do CRM de todos os documentos. Fora da janela
     de 24 h, o CRM manda o modelo aprovado `semdor_documento` com o PDF no cabeçalho
     (`Receituario-Controle-Especial-<número>.pdf`). A trilha registra quem, o canal e para onde.
   - **Salvar em PDF:** o arquivo assinado, com o mesmo nome.

Metadados do ITI (receita e receituário): `MetadadosDePrescricaoIti` grava os OIDs do guia no dicionário Info
antes de assinar. A decisão e a ressalva de que o guia não diz o lugar estão em `docs/safeid-congelado.md`.
O emitente sai por sigla: o cadastro guarda os códigos TISS ("06" = CRM, "33" = RJ) e o papel mostra "CRM" e
"RJ" (`RegistroDoPrescritor`).

Limite: 15 linhas de cerca de 92 caracteres por via. Prescrição maior é recusada antes do número, com a frase
"Divida em dois receituários".

## Onde mora

| peça | arquivo |
|---|---|
| tipo novo (texto no banco; app antigo lê `Desconhecido`) | `Domain/Entities/AtoClinico.cs` (`ReceituarioControleEspecial`, `TipoDocumentoInfo.EhReceita`) |
| regras: termos, comprador, UF, cidade/UF da clínica, tamanho | `Application/Modelos/ReceituarioControleEspecialRegras.cs` |
| completar o comprador (só o vazio, com auditoria) | `PacienteService.CompletarCompradorAsync` |
| PDF das duas vias (PdfSharp, QR em vetor) | `Application/Servicos/ReceituarioControleEspecialPdf.cs` |
| campo da assinatura (x 392–552, y 182–226) | `ReceituarioControleEspecialPdf.AreaDaAssinatura()` |
| colunas novas do paciente (RG, órgão, cidade, UF) | migration `20261125004127_ReceituarioControleEspecialComprador` (aditiva) |
| folha Blazor: sugestão + modal do comprador | `DocumentoForm.razor`, `CompradorDoReceituarioForm.razor` |
| portal: tipo `controle-especial`, `/assinatura/comprador`, termos, `/documentos/{id}/enviar-whatsapp` | `Assinaturas.Api` + `clinica-site/portal/profissional` |

## Testes

- `ReceituarioControleEspecialTests` (18): sugestão por palavra inteira e sem acento; ordem das faltas; gravar só
  o vazio com auditoria sem o valor; UF inválida não grava; cidade/UF da clínica; tamanho; nome do arquivo; duas
  vias A4; o retângulo do carimbo dentro do campo, longe do logo, da linha e do rótulo; a escala que faz o nome
  longo e o emissor do SERPRO caberem; e uma assinatura com certificado de teste, sem PSC, que desenha o carimbo
  nas duas vias.
- Portal: `ferramentas/testar-portal-controle-especial.mjs` (lista, Salvar em PDF, envio, comprador antes de
  assinar, sugestão, 390/1366 px, WCAG).
- **Não testado:** assinatura real (cada uma é cobrada) e o envio real pelo WhatsApp. Essa é a prova de campo
  da clínica.

## 🧑‍💻 UX

- O papel certo aparece no momento da escrita, não no balcão da farmácia.
- Ninguém redigita o emitente.
- O comprador se completa uma vez, ali mesmo, e fica na ficha.
- O envio é um clique, com confirmação que lembra o custo.
- Em 390 px, o modal e a sugestão quebram linha sem rolagem lateral (fotos do teste do portal).

## 🛠️ DX

- Uma regra num lugar só (`ReceituarioControleEspecialRegras`), com teste, que o Blazor e o portal consultam. O
  portal lê os termos pela API e não copia a lista.
- O motor ganhou dois campos opcionais e nada mais. Documento que não os usa desenha byte a byte como antes.
- O PDF é PdfSharp, a mesma biblioteca do carimbo. O QR é vetor porque essa montagem do PdfSharp não lê o PNG do
  QRCoder, e o teste pegou isso.

## 🤝 CX

- A farmácia recebe o modelo do ITI, com as duas vias, assinado e com QR de conferência.
- A recusa da 2026/0484 não se repete.
- Custo: o WhatsApp oficial cobra por envio (a confirmação diz isso). Não há serviço externo novo: o dado do
  paciente segue o caminho que os documentos já usam.
- Risco: o PDF só fica válido na farmácia depois da primeira assinatura real. A conferência é com a médica, no
  primeiro receituário.
