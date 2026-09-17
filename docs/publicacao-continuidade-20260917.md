# Continuidade do portal: publicação de 17/09/2026

As melhorias das PRs clinica-site#8 e Clinica#185 foram instaladas na
homologação e em produção. O pacote de aplicação foi preservado:

- API: `0af336113d7dc97491e1882ac03b9defc0dda7a3`.
- Interface: `eea378e047f0a80c1f0e0552a3f12748a24252e3`.
- Pacote: `tablet-continuidade-0af336113d7d-eea378e047f0.tar.gz`.
- SHA-256: `1baf12211accb9a006d65b6878e50b093f43ab9d73c6284b636f6214d85aae0d`.

## Instalação e correção operacional

A primeira tentativa parou antes de conceder permissões ou trocar a versão:
`ArquivosAnexoPaciente` usa `AnexoPacienteId`, sem coluna `Id` própria. O
instalador consultava uma sequência de `Id` em todas as tabelas de inserção.
Foi corrigido para consultar somente colunas existentes. Uma extração anterior
pode ser reutilizada somente se manifesto, conjunto de arquivos e todos os hashes
forem idênticos ao pacote aprovado; links simbólicos são recusados.

O instalador corrigido foi aplicado separadamente, sem recompilar nem alterar o
pacote de API/interface. Os dois ambientes receberam 30 concessões restritas
(incluindo sequências); não houve migration, troca de credenciais ou alteração
da configuração do portal. Banco, túnel, proxies SafeID e o outro ambiente
mantiveram seus processos durante cada atualização.

Produção anterior: `tablet-release-970b553cb536-55d0727d86b8`.
Backup privado: `/var/backups/clinica-tablet-continuidade-20260917-071955`.
Homologação anterior: `tablet-safeid-diagnostico-20260916`.
Backup privado: `/var/backups/clinica-posto-hml-continuidade-20260917-071559`.
Os releases anteriores e os comandos de reversão das novas permissões foram
preservados. Não houve escrita clínica de teste em produção.

## Evidências

- 17 verificações contra a API real de homologação, usando somente pacientes
  fictícios: login, reentrada sem código, acesso anônimo, CSRF, agenda, ficha,
  pendências, anamnese com versões e conflitos, reenvio, medidas, problemas,
  exames, modelos, substituição/cancelamento de rascunhos e anexo maior que 1 MB
  com recuperação dos mesmos bytes e isolamento entre pacientes.
- A sessão fictícia de teste ficou concluída com uma evolução, um mapa e uma
  guia; repetir a conclusão devolveu o mesmo resultado sem duplicação.
- A auditoria somente de leitura confirmou vínculo das evoluções e guias nas
  duas sessões já concluídas pela homologação, disponíveis ao faturamento.
  A produção ainda não tinha conclusão pelo portal para conferir um caso real.
- Saúde e página profissional responderam HTTP 200 nos dois ambientes; rotas
  clínicas sem autenticação responderam 401. Respostas permanecem `no-store`,
  com CSP, HSTS e bloqueio de enquadramento. O JavaScript público de edição da
  ficha corresponde ao hash do arquivo instalado.
- O PDF SafeID já arquivado na homologação teve a integridade criptográfica
  verificada com OpenSSL CMS. Uma assinatura íntegra cobre o arquivo inteiro.
  Essa conferência de integridade não substitui uma validação externa completa
  de confiança, revogação ou carimbo do tempo.

Relatórios na VPS em `/home/clinica-admin/tablet-stage/`:

- `tablet-continuidade-0af336113d7d-eea378e047f0-hml.json`.
- `tablet-continuidade-0af336113d7d-eea378e047f0-aceite-hml.json`.
- `tablet-continuidade-0af336113d7d-eea378e047f0-producao.json`.
- `pdf-safeid-verificado.json`.
- `auditoria-portal-faturamento-20260916.json` (atualizado em 17/09).

## Aceite humano pendente

O responsável informou que médico/enfermagem não estavam disponíveis para testar.
Portanto, permanecem pendentes: a dupla assinatura com os certificados reais dos
dois titulares e o aceite assistido de um atendimento real pela recepção/faturista.
O aceite técnico do pacote não declara essas duas etapas concluídas.

Portal: https://portal.clinicasemdormacae.com.br/profissional/
