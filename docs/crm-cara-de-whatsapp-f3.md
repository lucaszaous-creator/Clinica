# CRM com cara de WhatsApp — F3: a v3 e a v4 na tela (24/11/2026)

A F3 liga na aba WhatsApp da Recepção o que a F2 (contrato do CRM, #379–#381) entregou, sobre a tela da F1 (#382). Inclui o
adendo do dono de 24/11: as mensagens automáticas ficam compactas.

## O que entrou

| Recurso | Na tela | Regra (Application, com teste) |
|---|---|---|
| Mensagens automáticas | O menu, o "encaminhado para…" e o lembrete viram **uma linha** ("Automático · Menu enviado · paciente escolheu Acupuntura") que abre inteira ao clique e se recolhe | `ConversasNaTela.EhAutomatica`, `ResumoDaAutomatica`, `OpcoesDoMenu`, `EscolhaNoMenu` |
| Formatação | \*negrito\* \_itálico\_ \~tachado\~ aparecem formatados, nunca com os símbolos | `ConversasNaTela.Formatar` |
| Etiquetas | Pílula de filtro por etiqueta; etiqueta na linha (uma, "+n"), na faixa e nos Dados do contato; editar pelo ⋮ | `EtiquetasDa`, `IdsComEtiqueta`, `TomDaEtiqueta` |
| Fixar | Botão direito → "Fixar no topo" (até 3, o motivo quando passa); alfinete na linha; fixadas primeiro em Tudo e Minhas | `FixadasPrimeiro`, `MotivoParaNaoFixar` |
| Rascunho | "Rascunho:" em verde na linha da conversa com texto não enviado | `PreviaDoRascunho` |
| "Fulana está escrevendo…" | No cabeçalho, no lugar do subtítulo; o pulso vai ao CRM a cada ~3 s de digitação | `QuemEscreve` |
| Reações | Emoji no balão (quantas, de quem); "Reagir…" no menu do balão, só na janela de 24 h e até 30 dias | `Reacoes`, `MotivoParaNaoReagir` |
| ★ importantes | No menu do balão; ★ no rodapé do balão; lista nos Dados do contato | — |
| Endereço, contato do médico e serviços | No 📎, só na janela de 24 h; o balão vira cartão (mapa desenhado, contato) | `MenuDoWhatsApp.DoClipe` |
| Enviar mais tarde | Relógio ao lado do enviar: horários sugeridos dentro da janela, ou dia e hora; a agendada aparece na conversa com "Cancelar" | `HorariosSugeridos`, `FimDaJanela`, `TextoDaAgendada`, `AgendadasAVista` |
| Pílulas do @SemDor | 2 a 3 respostas acima do campo quando o paciente espera; um clique põe no campo | — |
| "!" e "Tentar de novo" | O balão que falhou mostra o "!" vermelho e "Não enviada · Tentar de novo"; some quando a nova sai | `PedeTentarDeNovo` |
| Saudação | Botão no topo da lista (só a gestão, `GerenciarCampanhas`): interruptor e texto | — |
| Colar print e arrastar arquivo | Ctrl+V no campo e soltar na conversa viram o anexo do rascunho | — |
| Busca na conversa | ↑ ↓ pulam entre os achados | — |
| Dados do contato | Etiquetas, mídia e documentos, ★ importantes | — |

**Desligados por decisão do dono:** "digitando…" ao paciente (`PatientTyping`) e transcrição (`TranscriptionEnabled`): nada deles
aparece. Se um áudio já vier com `Transcript`, o texto aparece sob ele. **"Fora do horário" não existe** (saiu em out/2026): só a
saudação. **CRM antigo (404):** o recurso some (etiquetas, fixar, clipe) ou diz em uma frase que ainda não chegou (saudação).

## 🧑‍💻 UX

- A conversa da equipe fica à vista: o que a máquina mandou é uma linha com o rótulo "Automático", e o que o paciente escolheu já
  vem na mesma linha.
- O que vai ao paciente respeita a Meta na própria tela: fora da janela, o item vem apagado com o motivo em poucas palavras.
- Nada sai sozinho: sugestão e resposta rápida vão para o campo; agendar mostra o horário e tem Cancelar.
- Contraste medido: etiquetas 5,32–5,78:1; "!" e "Não enviada" (`#C62F35`) 5,44:1; demais cores as da F1.
- Fotos em 1366, 1024, 760 e 390 px: `Shared/Clinica/crm-ux/fotos-f3/`.

## 🛠️ DX

- Regras novas em `ConversasNaTela.Recursos.cs` e no `MenuDoWhatsApp`, com `RecursosDoWhatsAppNaTelaTests`.
- As chamadas novas do CRM em `WhatsAppDaRecepcaoService.Recursos.cs` (sem tocar no contrato nem no `IClienteDoCrm`).
- A tela ganhou `ConversasDoWhatsApp.Recursos.cs` e o `SaudacaoForm`; o JS da conversa ganhou colar/arrastar e o ↑↓ da busca.
- Tokens novos em `web-extras.css` (`--wa-etq-*`, `--wa-erro`). Os formulários e menus da aba ficam no azul de ação (`.wa-camada`).
- A prévia (`CrmDeMentira`) ganhou um menu automático fictício na conversa do novo contato, para a foto e o teste.

## 🤝 CX

- A recepção para de rolar o menu para achar o que o paciente disse; a direção vê etiquetas e fixadas como no WhatsApp Business.
- Publicar depois do CRM com a F2, com os apps Recepção e Gerente (decisão do dono).

## O que não foi feito

- Não testado num Windows de verdade (fotos no Chromium do Linux, prévia com relógio fixo).
- Colar print e arrastar só foram compilados e ligados: a prévia não simula a área de transferência nas fotos.
- "Tentar de novo" não aparece nas fotos: a semente não tem mensagem com falha (a regra tem teste).
- Gestão de etiquetas, endereço, contatos e serviços (criar e editar) não está nesta tela: é da tela de Configurações.
