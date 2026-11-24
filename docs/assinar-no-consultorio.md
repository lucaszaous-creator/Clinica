# Assinar no Consultório a partir da gestão web (nov/2026)

**O problema:** a médica emitiu receitas pela gestão web e não conseguiu assinar. A gestão web não assina (a
assinatura digital é área congelada, `AssinaturaForaDaGestao`), mas mostrava "Emitir e assinar" e "⋯ → Assinar
com o e-CPF…", que falhavam com uma frase. No celular, parecia que "a aba de assinar não abre".

## O que muda
- `IAssinaturaNaTela.EnderecoParaAssinarFora(documento, paciente)` é um membro padrão que devolve **nulo**: o app do
  computador assina ali mesmo. A gestão web devolve `/profissional/?posto&assinar=<doc>&paciente=<id>`.
- **Na gestão web:**
  - "Emitir e assinar" vira **"Emitir e assinar no Consultório"**: emite e abre o Consultório na mesma aba, sem pop-up;
  - na central de Documentos, "⋯ → Assinar com o e-CPF…" vira **"Assinar no Consultório"**.
- **O Consultório** (clinica-site, PR irmão) lê `?assinar=&paciente=`. Abre a ficha do paciente na aba Documentos,
  com o documento destacado e o diálogo de assinar aberto, onde se escolhe A1 ou SafeID.

## Documento já emitido e sem atendimento
**Já funcionava**, e por isso não houve mudança no servidor. A ficha do Consultório assina com agenda 0, e
`AtendimentoTabletService.ExigirDocumentoAsync` aceita esse caso quando a médica é a autora e o documento é dos
tipos assináveis.

A tela **Documentos para assinar** (menu → Prontuário) já lista TODOS os documentos próprios sem assinatura, com ou
sem atendimento.

## Área congelada
Nada em `Assinatura/`, SafeID, `PublicacaoDocumentoService`, hash, PDF/PAdES ou token mudou. Só mudaram a tela e a
navegação: as mesmas rotas, os mesmos serviços, na mesma ordem. Nenhuma assinatura real foi feita nos testes.

## UX · DX · CX
- 🧑‍💻 **UX:** não existe mais botão que não assina. Um toque leva ao documento pronto para assinar no Consultório,
  e os pendentes ficam em "Documentos para assinar".
- 🛠️ **DX:** um membro padrão na interface da hospedagem; os aplicativos do computador não mudam. O teste
  `DocumentoFormBlazorTests.Na_gestao_web_emitir_e_assinar_leva_ao_Consultorio_com_o_documento` cobre o caso.
- 🤝 **CX:** sem custo de assinatura nos testes e sem tocar no motor que já funciona em produção.

## O que não foi feito
- **Assinar a anamnese pela gestão (Prontuários do posto):** continua sem botão que assina ali. A anamnese não é um
  dos tipos que o Consultório assina pela ficha, e mudar isso tocaria a área congelada.
