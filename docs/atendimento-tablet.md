# Atendimento profissional pelo tablet

Interface em `clinica-site/portal/profissional`, API em `Clinica.Assinaturas.Api`.
A sessão e o cadastramento do aparelho são os do portal presencial. O site
institucional não recebe prontuários nem hospeda esta API.

## Uso

1. Entrar com usuário individual vinculado a profissional ativo; abrir **Meu dia**.
2. Abrir o paciente, escrever ou copiar a evolução, revisar os dados e salvar.
   A cópia preserva o original e não copia valores de dor como se fossem atuais.
3. Em **Mapa corporal**, marcar com toque ou teclado, copiar outra sessão ou usar
   um modelo. **Guardar pontos como modelo** cria um modelo daquele paciente.
   Os pontos e observações são gravados junto da evolução, na mesma transação.
4. **Prescrições e documentos**: receita, pedido de exame, atestado ou infusão em
   texto livre. Infusão começa com SF 0,9% e 1h, editáveis pelo profissional.
   Cópias preservam posologia, quantidade e orientações dos registros antigos.
   A via é explícita; campos vazios de uma cópia não recebem padrões silenciosos.
   Itens suspensos aparecem no histórico, mas são excluídos da cópia com aviso.
   Vários itens ativos são transcritos no texto livre, com via conforme texto;
   o profissional revisa antes de emitir a nova folha.
5. Conferir PDF e autorizar assinatura SafeID. O retorno pede confirmação explícita.
6. **Concluir atendimento** salva evolução/mapa e usa o fechamento clínico existente:
   vincula evolução, atendimento e guias, grava fim e marca a sessão realizada.
   Repetir o mesmo envio devolve o mesmo recibo. Não inventa chegada nem início.

Salvar evolução e concluir são ações diferentes. Documentos não assinados são
identificados como tal. A tela não oferece baixa financeira, checagem da execução
de infusão nem assinatura em nome de outro profissional.

## Fronteira de segurança

- Toda leitura/escrita clínica exige vínculo ao profissional do agendamento e as
  permissões VerAgenda, VerProntuario e EditarProntuario. Prescrever e concluir
  exigem também Prescrever e LancarAtendimento, respectivamente.
- Usuário, vínculo ativo, bloqueio, versão de senha e modo equipe são relidos a
  cada requisição. Quinze minutos de inatividade exigem login; limite absoluto de
  duas horas. O navegador renova a atividade somente durante uso visível.
- O cliente não escolhe PacienteId, ProfissionalId ou Operador nas gravações.
  Mapa histórico e PDF são conferidos contra o paciente do agendamento autorizado.
- Cookies HttpOnly/Secure/SameSite Strict em produção; antiforgery em todos os
  POSTs; HTTPS, CSP restritiva, no-store, no-referrer, limites de corpo e requisições.
- Sem Analytics, logs de conteúdo clínico, armazenamento local ou modo offline.
  PDFs são renderizados dentro do diálogo protegido com PDF.js local, sem
  abrir uma aba avulsa. Fechar, sair ou trocar o acesso destrói o visualizador.
  Rascunhos ficam apenas na memória da página até salvar. Entrega ao paciente
  bloqueia o consultório; troca de aba/retorno exige revalidar a sessão.
- Escritas usam transação serializável e bloqueio por agendamento no PostgreSQL.
  Chave de idempotência é vinculada ao usuário, agendamento e hash do pedido.
  O recibo persiste apenas IDs, versão e situação, sem texto clínico.
- Edição antiga gera conflito; duas evoluções vigentes vinculadas exigem revisão
  no desktop. Nenhuma delas é escolhida silenciosamente.
- Auditoria registra autor e paciente nos acessos/gravações. Os controles do
  núcleo de prontuário, assinatura e imutabilidade continuam ativos.

## SafeID

O adaptador web reutiliza `ClienteSafeID`, `AssinadorSafeID` e os serviços de
assinatura existentes, sem modificar o núcleo congelado. A chave privada fica
no provedor; apenas o hash do conteúdo coberto sai para assinatura.

State aleatório, PKCE e código ficam no servidor por até cinco minutos. Uma
autorização ativa por sessão e uma assinatura por autorização. O callback GET
não assina: a operação exige a sessão original, CSRF e POST. Antes de assinar,
reconfere permissões, documento, cadastro, prestador, alergias na infusão e CPF
do certificado. Conteúdo alterado exige nova autorização. Reinício do serviço
descarta autorizações pendentes; documentos persistidos permanecem no prontuário.

Pré-requisitos de homologação/produção:

1. Aplicação SafeID com callback HTTPS exato cadastrado:
   `https://portal.clinicasemdormacae.com.br/safeid/retorno`.
2. Client ID/secret no arquivo protegido do serviço, nunca no site ou Git.
3. Saída TLS para os hosts oficiais do ambiente escolhido, por proxy local com
   allowlist. A unit atual permite somente localhost; preservar essa restrição.
   `HTTPS_PROXY` pode apontar ao proxy restrito. Não interceptar TLS nem registrar
   query string do callback (contém código temporário), headers ou corpos.
4. Ensaio acompanhado pelo titular do certificado: autorização, retorno ao tablet,
   assinatura, PDF arquivado e validação. Testes automatizados não consomem
   autorizações nem certificados reais. `Portal:SafeId:Habilitado` começa false.

## Implantação controlada

- Restaurar backup em homologação e aplicar a migration aditiva
  `20260916173014_AtendimentoClinicoTablet` com identidade de migração separada.
  Não iniciar o código novo antes da coluna AtividadeClinicaEm existir.
- Empacotar backend com `tools/empacotar-tablet.ps1` e frontend com
  `ferramentas/empacotar-portal.py`; registrar ambos os commits.
- Preservar socket Unix, tunnel, chaves Data Protection, no-store e ausência de
  telemetria. Feature clínica começa desabilitada em produção; configurar
  `Portal:AtendimentoHabilitado=true` só após ensaio dos privilégios e fluxos.
- O papel antigo do portal é insuficiente para escrita clínica. Na cópia restaurada,
  preparar grants por tabela/coluna para evolução/versões, mapa/pontos, prescrições/
  itens, assinaturas/arquivos, agenda/atendimento/códigos e recibos. Conferir também
  leituras de modelos, alergias, medicações, parâmetros, convênio e regras de
  fechamento. O fechamento mantém os efeitos do núcleo conforme o convênio.
  Remoção/substituição de pontos requer DELETE somente nas tabelas filhas editáveis;
  não conceder DELETE ao prontuário, documentos, assinaturas ou auditoria. Não usar
  superuser, credencial do desktop, GRANT ALL ou DDL no runtime.
- Validar em tablet físico retrato/paisagem, fonte ampliada, teclado, perda de rede,
  retorno do SafeID, dois profissionais, revogação e modo paciente. Confirmar o
  fechamento no desktop e que reenvio não duplicou guias.
- Recuo: desabilitar flags e voltar os dois artefatos. Preservar a migration aditiva,
  recibos, evoluções e PDFs; não apagar os registros clínicos produzidos.

## Testes

`dotnet test tests/Clinica.Tests` cobre escopo, permissões, inatividade, conflitos,
idempotência, mapa/modelos e conclusão com guias. A suíte roda em SQLite e no CI
também em PostgreSQL 16 com migrations reais e bancos descartáveis.

`dotnet test tests/Clinica.Assinaturas.Tests` usa a API real em demonstração:
CSRF, cookies, isolamento entre profissionais, versão, PDF, conclusão, revogação
e manutenção do portal de coleta. `clinica-site` testa interface responsiva e axe
com contrato fictício. Os testes não comprovam a integração SafeID em produção
nem substituem a homologação acompanhada no tablet físico.

Demonstração local: executar `npm ci` no clinica-site e depois
`tools/iniciar-demo-tablet.ps1 -Site <checkout-clinica-site>` no Clinica;
`/profissional/`, `medica.demo` / `TabletDemo#2026`. Somente dados fictícios.
