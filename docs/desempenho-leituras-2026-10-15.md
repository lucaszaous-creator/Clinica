# Desempenho das leituras nos cinco apps (15/10/2026)

Continuação da branch `codex/desempenho-suite-windows`. Pedido: telas e dados mais rápidos ao
trocar de tela nos cinco módulos, **sem mudar caminhos, regras ou resultados** — o sistema
está em produção.

## O limite físico, dito antes de tudo

A meta pedida foi "< 100 ms em toda leitura de tela". O diagnóstico anterior mediu, do posto
até a VPS, **186–288 ms de ping** e `SELECT 1` entre 214 e 879 ms numa conexão já aberta.
Com isso, **nenhuma leitura nova no banco cabe em 100 ms**: uma única ida já custa mais que a
meta. O que esta rodada faz é reduzir o número de idas por tela (e fazê-las juntas quando são
independentes), para que cada tela custe o mínimo possível de idas. Abaixo de ~250 ms por tela
só se chega reduzindo a latência da rede (250 ms entre dois pontos no Brasil é anormal — vale
investigar o link do posto, a rota até a VPS e o túnel/VPN) ou mostrando a tela com os dados
anteriores enquanto relê, o que é mudança de comportamento e não foi feito.

## O que mudou (nenhuma regra, nenhum resultado)

| Onde | Antes | Depois |
|---|---|---|
| Conexão (os cinco apps) | Pool sem mínimo e sem keepalive: após 5 min parado, a próxima tela pagava 2,7–4,7 s para abrir conexão | Pool com 2 conexões mantidas, keepalive de 30 s e aquecimento na abertura (`ConexaoRemota`) |
| Configurações | Uma ida por chave (a tela de Configurações lia ~28; o horário do paciente, ~10) | Uma leitura das chaves curtas por operação; invalidada em qualquer gravação |
| Códigos em aberto (Fila, Painel, Novo atendimento, Pendências) | Ficha inteira de cada paciente (foto, observações importadas), rastreada, lida 3× por carga | Ficha leve, sem rastreio, lida uma vez por carga |
| Perguntas sobre UM paciente | Inadimplência, guias pendentes, NCs, folha de infusão e sessões do dia traziam a clínica inteira e filtravam em memória | Filtradas no banco |
| Linha do tempo (ficha, atendimento, enfermagem) | 6 leituras em fila | 6 leituras simultâneas, um escopo cada |
| Ficha do paciente (Recepção) | ~11 blocos em fila (~35 idas) | Blocos simultâneos, um escopo cada |
| Elegibilidade (agendamento, check-in, ficha, Consultório) | 8 conferências em fila | Simultâneas; alertas na MESMA ordem (teste compara as duas) |
| Painel da direção (abertura do Gerente) | ~18 leituras em fila | Antecipadas e simultâneas (no máximo 6 por app) |
| Agenda, Painel, Sala de infusão | Leituras independentes em fila a cada batida | Simultâneas |
| Fila do dia | Relia a agenda do dia 3× por minuto | Uma leitura reaproveitada por pendências e termos |
| Conferência de choque | 4 idas por tecla da hora/duração | Agrupa as teclas (250 ms) |
| Conciliação, Taxas | Tabela de preços/tributos relida a cada linha | Uma leitura por operação |
| Meu dia, Minha semana, sessões sem evolução | Ficha inteira (com foto) de cada horário, crescendo todo dia | Leitura leve (nome do paciente) |
| Conclusão automática (todo posto, a cada minuto) | Montava a lista de pendências com 1–3 consultas por linha para usar só a contagem | Contagem no banco |
| Workspace do paciente (Consultório) | Capa carregada duas vezes ao abrir a ficha | Uma vez |
| Agenda (sobreposição) | Varria todo o histórico de agendamentos | Piso de 7 dias antes, indexável |

## Pedidos da enfermagem

- O cabeçalho confere a cada **15 s** (era 30) com uma marca de alteração (o último evento),
  e a página aberta **relê na hora** em que a marca muda, em vez de esperar o relógio de 1 min.
- A caixa mostra "Hoje/Amanhã", a quantidade em destaque, **"chegou há N min"** e a linha
  "atualiza sozinho · última leitura às HH:mm".

## Modelos de infusão

A exclusão (já existente nesta branch) passou a confirmar **no próprio modelo**, com botão
compacto e desabilitado enquanto exclui. Serviço, auditoria e preservação das prescrições não
mudaram.

## Verificação

`dotnet test` (SQLite), `tools/compilar-sombra.py`, `tools/verificar-suite.py`, testes novos
(`ConexaoRemotaTests`, `ElegibilidadeParalelaTests`, tradução no Npgsql das consultas
projetadas) e renderização das telas web com dados fictícios. A revisão do Jev não pôde ser
feita a partir deste ambiente (o proxy bloqueia `api.typesafe.ai`).

Não medido: tempos na rede da clínica. A medição deve ser feita no posto, comparando primeira
abertura e retorno de cada tela, antes e depois.
