# Ficha, atendimento, infusão e agenda — validação da PR 245

Data: 08/10/2026. Este registro consolida a revisão solicitada pelo proprietário, separando resultados concluídos de trabalho ainda em andamento. Não constitui aprovação clínica nem autorização de publicação.

## Resultados já concluídos

| Escopo | Resultado | Evidência |
|---|---|---|
| Suíte de domínio e serviços | **2.985 testes aprovados, zero falhas e zero ignorados** | `artifacts/fluxos-testes.log` |
| Infusão pelos ViewModels e persistência isolada | **32 verificações aprovadas**: modelos, rascunho, liberação, permissões, checagem, retificação, encerramento, cancelamento e PDFs | `artifacts/validacao-infusao-pr245.log`; [inventário detalhado](validacao-infusao-pr245.md) |
| Contratos da suíte web | **76 páginas, 97 formulários, zero divergências** | `artifacts/fluxos-suite-web.log` |
| Menus e ações | Seis ações preservadas, duas diretas, com teclado, clique fora e contexto da ponte em 900 e 1.366 px | `artifacts/fluxos-suite-web.log` |
| Tabelas e conteúdo longo | Histórico e guias com 14 colunas, seis ações e texto rico íntegros, sem arrastar lateralmente em **900, 1.366 e 1.920 px** | `artifacts/fluxos-suite-web.log` |
| Calendário denso | **197 sessões** acessíveis em 900 e 1.366 px; sete dias, abertura do dia completo e retorno preservam os registros | `artifacts/fluxos-suite-web.log` |
| Filtro de situação da semana | Sequência **197 → 50 → 50 → 197** conferida na execução central dos filtros, com recuperação do conjunto completo | Resultado da verificação central; complementar ao cenário denso da suíte web |
| Agenda da Recepção | **46 registros: 34 de hoje e 12 pendências anteriores**; 10 concluídos; segundo profissional com cinco concluídos, dez a atender e dois em atendimento | `artifacts/fluxos-recepcao-web.log` |
| Filtro por profissional | Nomes e contagens 23 + 23; seleção retorna 23 e “Todos” restaura 46. Combinação com situação conferida em 900 e 1.366 px | `artifacts/fluxos-recepcao-web.log` |
| Ficha e atendimento no WebView2 | **Concluído com código de saída 0**: ficha → sessão → PDF → correções → anexos → exames → ficha → prescrição de infusão cancelada sem gravar; 20 rotas em três larguras, enfermagem e gestos no mapa | `artifacts/fluxos-clinico-web.log` |
| PDF da sessão | PDF com duas páginas, paciente e evolução conferidos pelo verificador PyMuPDF na execução central | Verificação central do PDF gerado pelo cenário clínico |
| Buscas já existentes | Digitação sem Enter/blur em Documentos, Marcar horário e Prontuário; CPF fora das primeiras 50 fichas; resposta atrasada descartada; troca de paciente preserva o contexto correto | `artifacts/fluxos-recepcao-web.log` |
| Marcação | Navegação, busca, preenchimento e gravação pela interface; cancelamento sem gravar; permissões distintas para agendamento e lançamento com guia | `artifacts/fluxos-recepcao-web.log` |

Os cenários de agenda mantêm pendências anteriores visíveis. “A atender”, “Em atendimento” e “Concluídos” não são inferidos pela cor: os filtros usam as situações do host, e os estados continuam acompanhados de texto.

## Identidade visual e contraste

A referência Cielo orienta a distinção entre área de trabalho, controles e conteúdo; **não substitui a identidade da Clínica SemDor nem fornece uma paleta nova**. Os ajustes reaproveitam `cores-semdor.css`, os tokens existentes e a composição aprovada:

- Azul principal `#123A9E` e marinho `#071F5C` da SemDor; branco e cinzas frios claros nas superfícies.
- Cor de seleção `#EEF3FC`; borda de controle `#BAC9DD`, já existente em `identidade-visual.css`.
- Verde, âmbar e vermelho restritos aos estados funcionais; estados identificados também por rótulos.
- Campos, botões e filtros recebem limites visíveis; não se introduzem gradientes, roxo, ciano ou sombras coloridas.

Esta descrição registra a implementação de contraste; não afirma uma certificação completa de acessibilidade.

## Busca na agenda e parecer final

| Item | Estado nesta consolidação |
|---|---|
| Busca de paciente durante digitação na Agenda e no Meu dia | **Aprovada nos cenários WebView2**: nome sem acentos/caixa, foco preservado, combinação com situação/profissional, resultado vazio e limpar sem perder situação. Semana com 197 registros; Meu dia preserva comando Atender e ID. Evidências: `artifacts/fluxos-busca-agenda-web.log` e `artifacts/fluxos-recepcao-web.log`, ambos saída 0. |
| Resposta final do Jev | Consulta efetiva ao `jev-1.13.0`: `apto_para_homologacao_com_limites` (confiança 0,98) e `coerente_com_tokens_existentes` (1,0). Avaliou fontes CSS, classificador, documentos e logs. **Não recebeu imagens nem executou o aplicativo; não constitui aprovação visual de todas as telas.** Pedido/resposta locais: `artifacts/jev-validacao-fluxos/final-pedido.json` e `final-resposta.json`. |

## Limites e publicação

A conferência dos executáveis detectou o JavaScript principal dentro do bundle de arquivo único, ausente da pasta lida pelo WebView2. Os dois projetos web agora declaram `ExcludeFromSingleFile=true` para manter seus recursos físicos. `verificar-recursos-web-publicados.ps1` confere as referências locais dos índices antes de entregar cada pacote; a mesma verificação foi acrescentada ao build Windows antes do upload de cada artefato.

Os testes usam cenários determinísticos e dados sintéticos. O harness da infusão exercita ViewModels reais e persistência SQLite em memória, com capturas WPF; o cenário clínico WebView2 acima comprova separadamente a abertura e o cancelamento da prescrição pela ficha, sem confundi-lo com o ciclo completo de checagem no harness. Certificados sintéticos e geração de PDF não comprovam SafeID, certificado físico nem impressão na clínica.

Esta revisão não autoriza merge, release ou produção. Executáveis e checks devem ser vinculados ao commit final correspondente; artefatos de commits anteriores não comprovam estas alterações.
