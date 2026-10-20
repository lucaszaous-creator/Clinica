# A área da assinatura em Blazor (out/2026), só a camada de tela

**Autorização:** a direção autorizou em **19/10/2026** converter para Blazor as janelas da área
congelada do SafeID/assinatura. Ela fará os testes de assinatura em homologação, que são
cobrados por tentativa. A autorização cobre **só a camada de tela**: ver
[`safeid-congelado.md`](safeid-congelado.md), seção "Autorização expressa de 19/10/2026".

**O que NÃO mudou (continua congelado):**
- `Assinatura/` e `SafeID/`;
- `AssinaturaDeDocumentoClinicoService`, `AssinaturaDePrescricaoService` e `PublicacaoDocumentoService`;
- hash, geração do PDF/PAdES, certificado (`CertificadoA1`, `ConfiancaCertificadoA1`) e token;
- quais bytes são assinados.

A tela nova chama **os mesmos serviços, com os mesmos argumentos, na mesma ordem** que os
ViewModels WPF chamavam.

---

## 1. O que mudou

| Era (WPF) | Agora (Blazor) | Onde mora a regra |
|---|---|---|
| `EscolherCertificadoWindow` + VM | `Formularios/Assinatura/EscolherCertificadoForm` | `CarregamentoDoA1` (Application) |
| `AcoesDoDocumento.AssinarAsync` (corpo) | `IAssinaturaNaTela.AssinarDocumentoAsync` | `AssinaturaDeDocumentoNaTela` |
| `AssinaturaPacienteWindow` + VM | `Formularios/Termo/AssinaturaDoPacienteForm` + `js/assinatura-paciente.js` | `ColetaDoTermoEmAndamento` |
| `EscolherTermoWindow`, `EscolherSessaoDoTermoWindow` | `Formularios/Termo/EscolherTermoForm`, `EscolherSessaoDoTermoForm` | `ColetaDeTermoNaTela` |
| `PrescricaoInternaWindow` + `PrescricaoInternaEdicaoViewModel` | `Formularios/Infusao/PrescricaoDeInfusaoForm` | `PrescricaoDeInfusaoEmEdicao` |
| `FolhaExecucaoWindow` + VM | `Formularios/Infusao/FolhaDeExecucaoForm` | `FolhaDeExecucaoNaTela` |
| `InfusaoExternaWindow` | `Formularios/Infusao/InfusaoExternaForm` | `InfusaoExternaNaTela` |
| `EvolucaoEnfermagemWindow` e as views hospedadas | `Formularios/Infusao/EvolucaoDaFolhaForm` (reusa `FolhaDaPassagem`) | os serviços de enfermagem de sempre |

**A ordem das chamadas que era protocolo** foi movida linha a linha para
`Application/Modelos/AssinaturaNaTela.cs`, sem mudar argumento, ordem nem escopo:
- `AssinaturaDeDocumentoNaTela.AssinarAsync`: escolher o certificado → (desistiu: nada) → `AssinaturaDeDocumentoClinicoService.AssinarAsync(documento, certificado, usuarioId, operador)`.
- `AssinaturaDaPrescricaoNaTela.LiberarDoEditorAsync` (o editor):
  - conferir, num escopo;
  - confirmar a alergia, ANTES do certificado;
  - com continuidade, `LiberarSemAssinaturaAsync` noutro escopo;
  - sem ela, escolher → `AssinarPrescricaoAsync`, noutro escopo.
- `ValidarComoMedicoAsync`: a folha da infusão externa, num escopo só, como no WPF.
- `AssinarExecucaoAsync`: a 2ª assinatura, a da enfermagem.

`AssinaturaNaTelaTests` fixa essa sequência com os serviços reais sobre SQLite.

**Os formulários abrem na própria página** (`JanelasNaTela` + `CamadaDeJanelas`), nunca numa
janela do sistema. As pontes `IJanelasDaSalaDeInfusao`, `IJanelasDaEnfermagem` e
`IJanelasDaPrescricaoDeInfusao` passaram a ter implementação Blazor. As implementações WPF
foram removidas.

**O traço do paciente** é desenhado num `<canvas>` com pointer events. Ele sai no mesmo
formato que o `InkCanvas` produzia:
- PNG com fundo transparente;
- escala 2× (192 DPI);
- caneta preta de 2,2 px CSS com curva suavizada;
- largura e altura em DIP.

O PDF usa só os bytes e a proporção.

### A janela de transição

O código que ainda é WPF e pede um destes formulários não tem página onde abri-lo. Ele usa a
`JanelaDeFormularioBlazor` (`Desktop.Blazor`): uma janela modal com um navegador próprio, que
nasce escondido e só aparece depois da primeira renderização. Ela serve três casos:
- a ficha do balcão (`FichaPacienteViewModel`): assinar documento, colher termo, anotar enfermagem;
- a ponte WPF dos Prontuários (`JanelasDosProntuariosWpf`): assinar a anamnese;
- o ato "Assinar" da ponte WPF de documentos (`DocumentosNoAplicativoWpf`).

É uma porta provisória. Quando essas telas virarem Blazor, ela perde os chamadores e sai.

---

## 2. Roteiro de homologação (o que a clínica precisa testar)

Faça em **homologação**, com e-CPF A1 de teste e **uma tentativa por caso**. Em cada caso,
confira o PDF no validador do ITI (`assinaturadigital.iti.gov.br`) e se o carimbo aparece no
rodapé.

1. **Documento pela tela Documentos (Blazor)**: Recepção → Documentos → um termo emitido →
   "⋯" → "Assinar com o e-CPF…".
   - Escolha o `.pfx`, digite a senha e clique em Carregar: o titular e o CPF aparecem.
   - Clique em Assinar: o PDF salvo abre e a linha mostra "Assinado".
   - Receita, atestado, comparecimento, pedido de exame, relatório, anamnese e laudo não
     oferecem a ação: são emitidos para impressão. A lista é a mesma de antes.
2. **O mesmo pela ficha do balcão (WPF → janela de transição)**: ficha do paciente → aba
   Documentos → Assinar.
   - Deve abrir a janela "Assinar com certificado A1", sem tela preta.
   - Fechar pelo X desiste sem assinar.
3. **Anamnese pelos Prontuários**: Consultório → Prontuários → uma anamnese "A assinar" →
   Assinar. Ela também passa pela janela de transição.
4. **Editor da prescrição de infusão** (Consultório → paciente → Atendimento → Prescrever infusão):
   - Sem alergia: Assinar abre o seletor e o PDF sai com o carimbo do médico.
   - Com alergia na lista de problemas e um item que bate: a confirmação aparece **antes** do
     seletor. "Não" cancela sem pedir o certificado.
   - Com "continuidade sem A1" ligada (Configurações): "Liberar para enfermagem" libera sem
     pedir certificado e sem carimbo.
5. **Folha de execução, 2ª assinatura**: Sala de infusão → a folha → checar os itens →
   Encerrar → "Assinar execução".
   - O certificado da **enfermeira** assina; o do médico é recusado.
   - O PDF tem os dois carimbos lado a lado.
6. **Folha da infusão externa, validação do médico**: registrar uma infusão externa pela
   enfermagem → o médico abre a folha → "Validar e assinar". O caminho da alergia é o mesmo do
   item 4.
7. **Erros do A1**, cada um com a frase na tela e o botão Assinar apagado:
   - senha errada;
   - A1 de outra pessoa (CPF diferente do profissional do login);
   - A1 vencido;
   - arquivo que não é `.pfx`;
   - arquivo maior que 128 KB.
8. **Termo do paciente**:
   - **Na tela**: Recepção → a fila do dia → "⋯" → Colher termo. O paciente assina no canvas
     (mouse, toque ou caneta). Confira no PDF se o traço aparece nítido e na proporção certa.
   - **Pelo WhatsApp**: "Enviar pelo celular" → o paciente assina no celular → o traço volta à
     janela → Confirmar.
   - **Recusa**: "Paciente recusou", com o motivo.
   - **Alergia**: "Registrar alergia" dentro da coleta grava na lista de problemas.

---

## 3. O que não foi verificado

- **Nenhuma assinatura real** (e-CPF ICP-Brasil, PSC) foi feita: os testes usam um certificado
  autoassinado em memória. É exatamente o que o roteiro da §2 cobre.
- **O canvas não foi testado num navegador real com toque ou caneta.** O formato do PNG foi
  conferido no código, contra o `InkCanvas`, e não renderizado. O bUnit roda o JS de mentira.
- **A janela de transição não foi aberta** num Windows com WebView2. O build compila, mas o
  "sem tela preta" e o X que desiste não foram vistos rodando.
- O `tools/compilar-sombra.py` não roda neste Windows (falta o targeting pack de Linux). O
  `dotnet build Clinica.sln` compila os projetos WPF de verdade.

---

## UX · DX · CX

- 🧑‍💻 **UX:**
  - Os formulários abrem dentro da tela, com o visual das demais telas Blazor, sem janela por
    cima nem tela preta.
  - Exceção: a janela de transição, que ainda é uma janela.
  - As frases e as guardas são as mesmas de antes. Quem já usava encontra os mesmos passos.
- 🛠️ **DX:**
  - Cerca de 8 mil linhas de XAML/ViewModel/ferramenta saíram. As regras foram para a
    Application, com teste: `AssinaturaNaTelaTests`, `PrescricaoDeInfusaoEmEdicaoTests` (porte
    do `tools/VerificarInfusao`) e `ColetaDeTermoNaTelaTests`.
  - A ordem das chamadas de assinatura tem um lugar só, onde o `dotnet test` alcança.
  - Peça nova: `JanelasNaTela` (formulários na página) e a janela de transição, que é
    provisória.
- 🤝 **CX:**
  - Sem migration e sem mudança no que é assinado.
  - O risco está no caminho da tela: o seletor do A1, o canvas e a janela de transição.
  - Por isso a release só depois do roteiro da §2 em homologação, uma tentativa por caso.
