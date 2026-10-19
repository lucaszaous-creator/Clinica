# Abertura rápida dos cinco apps (out/2026)

A direção relatou 15–30 s entre o login e os dados da tela inicial, nos cinco apps. A causa foi
**medida**, não deduzida: `tests/Clinica.Tests/Desempenho` roda o caminho de abertura de cada app
(preparo do banco → login → tela inicial → o que roda por trás) contra um banco com latência
simulada por ida (`RedeSimulada`), contando idas, idas **em série** e conexões físicas novas.

## O que a medição mostrou

1. **Leituras em fila.** O Início da Recepção fazia 23 idas em série; a Mesa da direção, 26; o
   Caixa, duas; o Meu dia, três. A ~250 ms por ida até a VPS, é isso que vira segundos.
2. **Conexão física nova custa 2,7–4,7 s** (TCP + TLS + mTLS + autenticação). O pool aquecia 2
   conexões e a primeira tela pedia até 13 ao mesmo tempo — cada uma acima das aquecidas pagava a
   abertura.
3. **O que roda por trás disputava a abertura**: avisos do cabeçalho, conclusão automática,
   lembretes por e-mail, cópia de segurança, rodada de pendências — tudo disparado junto da tela.
4. **Antes do login**: os três catálogos em fila; no faturamento, `MigrateAsync` em toda abertura.

## O que mudou

| Antes | Agora |
|---|---|
| Leituras independentes em fila (Início, Mesa, Caixa, Meu dia, Acompanhamento, Painel da direção) | começam juntas, cada uma no seu escopo (`ILeituraParalela`); o que já chegou aparece, o resto mostra esqueleto |
| Paralelismo sem teto dentro de uma leitura paralela | 4 vagas de fora + 2 de dentro (`LeituraParalela`) = as conexões aquecidas |
| Pool com 2 conexões aquecidas | mínimo 4; a abertura aquece 6 enquanto a pessoa digita o login (as acima do mínimo fecham após 15 min ociosas — `max_connections = 60` na VPS) |
| Trabalho de fundo junto da tela inicial | espera a primeira tela ter os dados (`AberturaDoApp`), com teto de 20 s |
| Catálogos em fila; faturamento migrando sempre | catálogos em paralelo (`AberturaDoBanco.RecarregarCatalogosAsync`); migração só com pendência |

Medido a 150 ms por ida, pior caso (fundo rodando junto, pool aquecido):
Financeiro 3,2 s → 0,16 s; Consultório 3,5 s → 0,47 s; Recepção 6,3 s → 3,6 s; Gerente 4,7 s → 4,4 s
(a Mesa sozinha: 2,0 s). Na clínica o fundo agora espera a tela, então o caso real é o da "tela
sozinha": Recepção ~1 s, Gerente ~2 s.

`AberturaDosAppsTests.A_tela_inicial_nao_volta_a_fazer_idas_em_serie` **trava** o número de idas em
série de cada tela inicial: a próxima leitura posta em fila reprova no `dotnet test`.
Para medir de novo: `CLINICA_MEDIR_ABERTURA=arquivo.txt dotnet test --filter AberturaDosAppsTests`.

## UX · DX · CX

- 🧑‍💻 **UX**: os números da tela inicial aparecem primeiro e os blocos secundários entram com
  esqueleto no lugar — nunca um "vazio" que ainda não foi lido; o sino e as janelas de aviso
  chegam depois de a tela estar pronta.
- 🛠️ **DX**: a regra mora em três peças (`ILeituraParalela`, `AberturaDoApp`, `AberturaDoBanco`),
  sem paralelismo no mesmo `DbContext`; sem `ILeituraParalela` (testes) os serviços seguem em fila
  com o mesmo resultado. A medição roda o MESMO código do app.
- 🤝 **CX**: o dia começa sem a espera de 15–30 s; custo no servidor: até 6 conexões por app nos
  primeiros 15 min, 4 depois (eram 2).
