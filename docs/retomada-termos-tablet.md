# Retomada — BSV/TCLE assinados no tablet

## Atualização de 15/09/2026 — implementação para homologação

O checkpoint de 14/09 abaixo foi retomado nas duas branches `codex/termos-tablet`.
API, interface, migração aditiva e recuperação estão implementadas. **Portal
instalado na VPS em https://portal.clinicasemdormacae.com.br/, com migração aplicada
após backup e homologação na cópia PostgreSQL restaurada.**
O usuário confirmou tablet Android para a enfermeira.

Entregue nesta retomada:

- Interface em `clinica-site/portal/`: entrada, agenda/busca, conferência, leitura,
  respostas obrigatórias, rubrica por termo, finalização e reentrada da equipe.
- Recusa com semântica própria, expiração/abandono de preparações, retomada de
  arquivamento sem nova rubrica e proteção contra submissões concorrentes.
- Migração `20260915102346_ColetaDeTermosNoTablet` com três tabelas aditivas e
  triggers para preservar via, conteúdo do documento, declarações e rubrica.
- PDF somente com assinatura do paciente; operadora identificada sem linha para
  assinatura profissional. Recuperação dos mesmos bytes pelo serviço do prontuário.
- CI por componente, scripts de demonstração/pacote e exemplos de instalação.

| Evidência local | Resultado |
| --- | --- |
| Suíte Clinica.Tests / SQLite | 2.548 aprovados, incluindo 19 cenários do tablet |
| Fronteira HTTP real de demonstração | 1 percurso aprovado: autenticação, CSRF, modo paciente e revogação |
| Tokens e verificar-suite | Aprovados |
| Compilação-sombra | Dez projetos WPF aprovados |
| Modelo EF | Snapshot sem mudanças pendentes |
| Interface com contrato simulado | Alergias, rubrica após edição, rotação, reentrada, escaping e fonte 200% aprovados |
| Navegador contra API real | Dois termos fictícios arquivados; PDFs reabertos idênticos; zero erros de página |
| PDF | Todas as seis páginas conferidas visualmente |

O job `testes-postgres` aplica todas as migrações em banco descartável e repete a
suíte; consultar o resultado do commit no CI. SQLite não comprova os triggers.
O CI PostgreSQL do commit `488f8c4` passou. O percurso HTTP na cópia da VPS também
passou com privilégios restritos, dois PDFs arquivados e reabertura idêntica.
Faltam a confirmação do login pelo responsável, a homologação no Android físico
e a confirmação da versão dos leitores desktop em uso. Seguir o
[roteiro de instalação, permissões, backup e piloto](operacao-termos-tablet.md).

---

## Registro histórico de 14/09/2026

O texto abaixo descreve o checkpoint inicial, anterior à implementação acima.

## Checkpoint solicitado pelo responsável

Branch dos dois repositórios: `codex/termos-tablet`.
Este checkpoint preserva trabalho em andamento contra queda de energia. **Não é
uma versão pronta para pacientes reais ou implantação.** Nada deste fluxo foi
publicado na VPS, e nenhuma migration foi executada no banco clínico.

- Backend: [Clinica / branch de trabalho](https://github.com/lucaszaous-creator/Clinica/tree/codex/termos-tablet).
- Interface: [clinica-site / branch de trabalho](https://github.com/lucaszaous-creator/clinica-site/tree/codex/termos-tablet).
- Especificação aceita: [plano completo](plano-assinatura-bsv-tcle-tablet.md).

## Decisões do usuário que devem ser preservadas

1. Somente o paciente assina, desenhando uma rubrica no tablet com dedo ou caneta.
2. A assinatura pertence ao documento e à versão apresentada. A via assinada deve
   ficar no prontuário do paciente; agendamento/sessão clínica é opcional.
3. Alergia exige resposta explícita, sem opção preselecionada. Sem resposta, bloquear
   a rubrica e o envio, tanto na interface quanto na API. Responder que tem alergia
   registra um alerta para avaliação clínica; não é ausência de resposta.
4. Enfermeira seleciona o paciente, confere identidade e entrega o tablet. O paciente
   não pode acessar outros pacientes, a lista do dia ou funções da equipe.
5. A operadora fica identificada na coleta; não é signatária do documento.
6. Preservar os textos dos modelos BSV/TCLE existentes. TCLE contínuo e termo diário
   mantêm as regras clínicas existentes, separadas do vínculo opcional ao horário.
7. Os dois repositórios usam a mesma VPS. O institucional permanece isolado; o portal
   terá serviço próprio. Evitar ciclos amplos/repetidos de testes e CI duplicada.

## Implementado neste checkpoint

- `Clinica.Assinaturas.Api`: projeto ASP.NET Core separado, endpoints de entrada,
  agenda reduzida, busca, termos do paciente, preparação, recebimento e via privada.
- Sessão do tablet persistida no servidor, com modo equipe/paciente e nova entrada
  da equipe para sair do acesso restrito. Rechecagem de usuário, permissões e senha.
- Cadastro do dispositivo por código configurado, cookies protegidos, antiforgery,
  limites de requisição/tentativas e cabeçalhos de privacidade.
- Novas entidades e mapeamento EF: `SessaoTablet`, `ColetaTablet` e
  `ViaAssinadaPaciente`. O banco ainda não recebeu essas estruturas.
- Cópia do conteúdo apresentado, pergunta explícita sobre alergias, validação de
  respostas e PNG limitado ao formato do canvas, com rejeição de traço vazio.
- Submissão durável e idempotência; worker para gerar PDF e arquivar com evidências
  em transação, preservando submissão em caso de falha.
- Leitura da via arquivada pelo serviço de PDF existente, com conferência de hash.
- Alerta de alergia positiva integrado ao cálculo das declarações que exigem atenção.
- Ambiente de demonstração SQLite com pacientes e credenciais exclusivamente fictícios.

## Verificação já feita

`dotnet build src/Clinica.Assinaturas.Api/Clinica.Assinaturas.Api.csproj`

Resultado: compilação aprovada, **zero avisos e zero erros**, com SDK 8.0.425.
Não confundir compilação com validação funcional. Testes do novo fluxo, migration,
segurança em HTTP e interface ainda não foram concluídos.

## Próximos passos, na ordem

1. Revisar os contratos e o ciclo de vida das coletas: recusa precisa refletir a
   semântica existente de recusa do paciente; permitir retomada operacional segura
   de falhas; tratar coletas preparadas abandonadas após nova entrada/expiração.
2. Gerar e revisar migration aditiva e snapshot do EF. Acrescentar proteção da via
   imutável no PostgreSQL, inclusive contra atualizações por outra versão do desktop.
   Garantir que os leitores desktop usem a versão que recupera os bytes arquivados.
3. Implementar a interface em `clinica-site/portal/`: entrada, dia, conferência,
   leitura, respostas obrigatórias, rubrica em canvas, confirmação, finalização e
   retorno autenticado à equipe. Servir pela API na mesma origem.
4. Implementar testes focados: identidade/documento, alergia ausente/nula/vazia,
   resposta positiva/negativa, PNG falso/em branco, autorização/CSRF/modo paciente,
   duplicação, concorrência, queda antes/depois de PDF, recuperação e via idêntica.
   Validar transações/migration no PostgreSQL além dos testes SQLite.
5. Executar um percurso completo com dados fictícios: dois termos rubricados,
   PDF final conferido e recuperação pela mesma função usada no prontuário.
   Conferir toque/rotação/texto ampliado no navegador e no tablet da clínica.
6. Acrescentar documentação de operação, testes, permissões mínimas, backup,
   restauração, configuração e implantação protegida por feature flag. Configurar
   CI por componente afetado, sem disparar a matriz institucional por mudança do portal.
7. Revisar as duas branches, abrir PRs vinculados e preparar o piloto assistido.
   A implantação exige modelos aprovados configurados, HTTPS válido, capacidade
   medida na VPS e restauração conferida, conforme o plano aceito.

## Como compilar e preparar a demonstração

Usar SDK .NET 8 atualizado. O SDK instalado durante este trabalho ficou fora do
repositório, em `../dotnet/dotnet.exe` no workspace do responsável.

```powershell
dotnet build src/Clinica.Assinaturas.Api/Clinica.Assinaturas.Api.csproj
```

A demonstração exige `ASPNETCORE_ENVIRONMENT=Development`, `Portal__Demo=true` e
`Portal__Interface` apontando para a futura pasta `portal` do checkout de
`clinica-site`. Ela escuta apenas `http://127.0.0.1:18120`, usa banco SQLite próprio
e recusa uma connection string clínica. A interface ainda não existe neste checkpoint.

Credencial de demonstração incluída no código: `demo` / `TabletDemo#2026`.
É uma conta fictícia local, sem acesso à clínica. Não reutilizar em produção.

## Hospedagem existente

VPS Locaweb `vps69933`, compartilhada pelos projetos. Não alterar PostgreSQL clínico,
SSH, firewall ou serviços existentes como efeito incidental desta implementação.
O histórico do institucional, DNS, Cloudflare Tunnel e HTTPS está no README do
`clinica-site`. O novo portal ainda não tem subdomínio configurado ou serviço instalado.

Este commit de checkpoint usa `[skip ci]` porque preserva uma implementação
incompleta. A entrega deve executar os testes focados e a CI apropriada.
