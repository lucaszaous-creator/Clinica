# Todos os documentos com o fluxo do Receituário de Controle Especial (nov/2026)

**Pedido do dono:** a médica assinou o Receituário de Controle Especial 2026/0486 e gostou principalmente de o PDF
abrir sozinho depois de assinar. *"Antes de fechar a PR, queremos fazer o mesmo modus operandi do Receituário de
Controle Especial aos outros documentos. Desde a emissão até o salvamento do arquivo que já abre em PDF
automaticamente sem precisar 'imprimir'."*

**Correção do dono no item da assinatura:** *"A assinatura mantém no mesmo lugar para os outros documentos, só o
receituário que precisou ficar em um lugar especial."* Por isso:
- o motor de assinatura e o carimbo visível ficam **intocados** fora do receituário;
- `docs/safeid-congelado.md` não muda.

## Documentos cobertos

Receita, Receituário de Controle Especial, Atestado, Declaração de comparecimento, Pedido de exame, Laudo médico,
Relatório clínico (evolução), Anamnese e Termo/Consentimento. São os papéis emitidos pela `DocumentoForm` (Consultório,
gestão, app Clínico) e pelo Consultório do portal.

| etapa | o que acontece |
|---|---|
| emissão | Quem emite é o logado (`EmitenteDoDocumento`). Se faltar na ficha um dado que o papel exige (`DadosDoPacienteNoDocumento`), abre o modal "complete aqui", que grava com auditoria e segue. |
| assinatura | "Emitir e assinar" com SafeID/A1 de quem emite. O carimbo fica no mesmo lugar de sempre; só o receituário tem o campo próprio. |
| depois de assinar | O PDF assinado **abre sozinho**: no app, sem perguntar onde salvar; na gestão, numa aba aberta no clique; no portal, no visualizador. Depois vêm "Salvar em PDF" e "Enviar ao paciente pelo WhatsApp" (`semdor_documento`). |
| emitir sem assinar | O PDF também **abre sozinho**. "Imprimir" é opção dentro dele, nunca um passo. |
| publicação (QR) e metadados ITI | Valem os de sempre. Publicam-se Receita, Receituário, Atestado, Pedido de exame e Comparecimento. Os metadados de prescrição vão na receita e no receituário. |

**O que o papel exige da ficha (`DadosDoPacienteNoDocumento`):**
- Receituário: o comprador inteiro (RG, órgão emissor, endereço, cidade, UF, telefone).
- Receita: o endereço residencial.
- Os demais: nada além do nome, que toda ficha tem.

Na receita, a pergunta de texto solta do endereço saiu e virou o mesmo modal. Saíram junto `CompletarEnderecoAsync` e
os textos dela.

**Sem QR por decisão anterior:** Laudo, Relatório, Anamnese e Termo continuam **sem QR**. `PublicacaoDocumentoTests`
fixa que "prontuário não vira URL". Publicar o laudo, que o paciente leva à perícia, é decisão do dono, a confirmar.

## Onde mora

- Regra dos dados: `Application/Modelos/DadosDoPacienteNoDocumento.cs`.
- Gravação: `PacienteService.CompletarParaDocumentoAsync`.
- Abrir sem perguntar: `IHostDaTela.AbrirPdfAsync`, que usa a pasta das prévias no app e uma aba no navegador.
- Depois de assinar no app: `AssinaturaNaTela`.
- Depois de assinar na gestão: `assinatura-na-gestao.js` (a aba nasce no clique, então o bloqueador de janelas não a
  barra; o PDF entra como blob).
- Folha: `DocumentoForm` (emitir abre o PDF; o rodapé pós-emissão tem "Salvar em PDF" e "Enviar ao paciente pelo
  WhatsApp") e o modal genérico `CompradorDoReceituarioForm` (parâmetro `Tipo`).
- Portal:
  - a API devolve `faltasPaciente` e `motivoPaciente`;
  - `/assinatura/paciente` grava para qualquer papel;
  - `/comprador` e os campos antigos ficam para a página que ainda estiver aberta;
  - `clinico.js`/`safeid.js` abrem o visualizador ao emitir e ao assinar.

## Testes

- `DadosDoPacienteNoDocumentoTests`: o que cada papel pede; a receita não grava o que não pediu; o aviso da folha.
- `DocumentoFormBlazorTests`: a receita sem endereço abre o modal; cancelar não numera; gravar emite e guarda.
- `ReceituarioLivreTests`: os casos do endereço passaram para o caminho genérico.
- Portal: `testar-portal-pdf-abre-sozinho.mjs` cobre emitir sem assinar e assinar (o PDF abre) e o endereço pela regra
  de todos os papéis, num botão só. Os mocks do posto e do SafeID ganharam a rota do PDF.
- Não testado aqui: assinatura real, envio real e o bloqueador de janelas de cada navegador na gestão. Se a aba for
  barrada, o PDF baixa.

## 🧑‍💻 UX

- Emitiu ou assinou, o PDF está aberto. Não há "imprimir" nem "salvar como" no caminho.
- Guardar e mandar ficam a um clique, no rodapé.
- O dado que falta é pedido no mesmo modal em qualquer papel e em qualquer porta.

## 🛠️ DX

- Uma regra (Application) para o que cada papel exige, usada pela folha e pelo portal.
- Um método do hospedeiro para "abrir sem perguntar".
- Nenhuma mudança no motor de assinatura.

## 🤝 CX

- Menos cliques por documento e o mesmo jeito em todos os papéis.
- O carimbo dos outros documentos fica igual (decisão do dono).
- Risco: na gestão, um navegador muito restritivo pode barrar a aba; aí o PDF baixa, sem perder o documento.
