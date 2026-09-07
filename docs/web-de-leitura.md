# A web de leitura — o dia, o mês e a ficha, de fora da máquina

> **Somente leitura.** Lançar atendimento, marcar horário, escrever no prontuário, receber
> dinheiro e assinar documento continuam **só** nos cinco aplicativos. A web responde três
> perguntas e não muda nada — a única escrita que ela faz é a **trilha de acesso**, e ela é
> obrigatória (ponto 4 do compromisso de conformidade).

## O que ela responde

| Página | Pergunta | Exige |
|---|---|---|
| **O dia** (`/dia`) | quem está marcado, em que pé está cada um | `VerAgenda` |
| **O mês** (`/painel`) | os números do mês e o que está vencendo | `VerIndicadores` |
| **Pacientes** (`/pacientes`) | a ficha de alguém: contato, convênio, próximas sessões | `VerFichaPaciente` |
| — o **prontuário** dentro da ficha | as sessões escritas | `VerProntuario` **também** |

A régua é a MESMA do desktop (`AcessoWeb`, na Application, com teste). Uma segunda regra —
"na web todo mundo vê o resumo" — seria a permissão granular da parcela 49 desfeita por uma
porta nova, que é exatamente o que a parcela 60 achou nas cópias do faturamento.

## ⛔ Onde ela PODE rodar — leia antes de publicar

**A decisão de expor a web na internet é da direção, e ela não foi tomada.** O
`docs/banco-na-vps.md` recusou, por escrito, o desenho "API HTTPS no meio" com o argumento
de que ele é *o mais exposto* — porta 443 respondendo a qualquer IP, com código nosso
atrás. Publicar esta web na internet é tomar aquela decisão pela porta de trás.

O que este projeto entrega é o **software**; onde ele escuta é escolha de instalação, e as
duas que não contradizem a decisão do banco são:

1. **Na rede da clínica** — o servidor sobe numa máquina de lá e responde só a quem está
   dentro. É o caso de uso que motivou o pedido (a direção olhando o dia do celular, no
   Wi-Fi da clínica).
2. **Atrás de VPN** — o WireGuard/Tailscale que a parcela do banco descartou *para as
   máquinas do balcão* (porque exigia agente em cada uma) é barato para **um** servidor e
   **um** celular.

Se um dia a direção quiser a web aberta na internet, o que ela precisa ganhar ANTES está
escrito aqui para não ser esquecido: segundo fator no login, limite de tentativas por IP
(hoje o travamento é por USUÁRIO, no `AcessoService`), registro de origem do acesso na
trilha e um certificado público de verdade.

## O que já está no código, e por quê

- **HTTPS obrigatório fora de desenvolvimento** (`UseHsts` + `UseHttpsRedirection`): sem
  ele, o cookie de sessão e a ficha do paciente viajam em claro pela rede da clínica.
- **`Cache-Control: no-store`** em toda resposta — é o que impede a ficha de ficar no cache
  do navegador de um computador compartilhado, e o botão "voltar" depois do logout é
  exatamente onde isso apareceria.
- **CSP sem script e sem origem externa**: a página é HTML e CSS embutidos. Não há
  framework de front, e isso é decisão — são três páginas de leitura, e um SPA custaria
  build, dependências que se atualizam sozinhas e uma segunda cópia do design system para
  manter (a mesma razão do gráfico desenhado com os tokens, parcela 5).
- **Todo texto do banco é escapado** (`Paginas.T`): nome de paciente e observação de
  horário são texto que uma PESSOA digitou. A CSP é a segunda tranca, nunca a primeira.
- **Cookie de 2 horas**, `HttpOnly`, `SameSite=Strict`: a web mostra dado de saúde, e sessão
  que sobrevive ao dia é o celular esquecido em cima da mesa.
- **A permissão é fotografia do login**, como no `SessaoUsuario.Entrar` do desktop — reler o
  banco a cada requisição pagaria uma consulta por clique. O preço é o mesmo do desktop: uma
  permissão retirada passa a valer no próximo login, e o cookie dura 2 h.
- **Sem cookie, `Permissao.Nenhuma`** — diferença deliberada em relação ao desktop, onde
  "sem sessão autenticada, `Pode` libera" (lá o login é obrigatório e tela vazia parece
  defeito). Aqui a porta está na rede: liberar por omissão seria a web inteira aberta.
- **Antiforgery de verdade nos dois POST** (entrar e sair), com o token no formulário e a
  validação num lugar só. `SameSite=Strict` já barra a maior parte do CSRF — mas ele
  protege a sessão que EXISTE, e o login-CSRF acontece ANTES de haver cookie: é ele que
  planta a sessão de outra pessoa no navegador de quem lê prontuário. Token vencido (a aba
  aberta desde ontem) devolve "recarregue e tente de novo", nunca uma página de erro.
- **A busca não consulta com o campo vazio** — a regra do `SemBuscaInicial`: com o termo em
  branco a busca não filtra nada e traria o começo do alfabeto de 2.238 fichas.

## A trilha

Abrir o prontuário pela web grava `OrigemAcessoProntuario.WebDeLeitura` — origem PRÓPRIA,
porque "abriu no balcão às 14h" e "abriu de fora às 23h" são fatos de naturezas diferentes
sobre o mesmo paciente, e fundi-los apagaria justamente o padrão que uma investigação
procura. Falhar a trilha **não** impede a leitura (banco lento não pode travar quem está
com o paciente na frente), mas vai para o log.

## Como rodar

```bash
# A MESMA cadeia dos apps, com o certificado do mTLS (docs/banco-na-vps.md).
ConnectionStrings__Clinica="Host=...;Database=...;Username=...;Password=...;SSL Mode=VerifyFull;..." \
  dotnet run --project src/Clinica.Web
```

Sem a variável o site **não sobe**, e a mensagem diz por quê: subir apontando para lugar
nenhum daria uma tela de login que recusa todo mundo, e ninguém saberia o motivo.
