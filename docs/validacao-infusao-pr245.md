# Validação funcional da infusão — PR 245

Data: 08/10/2026. Escopo solicitado pelo proprietário: confirmar o fluxo existente de infusão, sem alterar regras clínicas nem a identidade visual.

## Inventário e evidências

| Etapa | Implementação | Verificação determinística |
|---|---|---|
| Selecionar paciente e iniciar prescrição | PrescricaoInfusaoViewModel; diálogo web PrescricaoInterna | QA Recepção: busca por digitação/CPF e troca de paciente na rota de infusão |
| Medicamentos, grupos e modelos | PrescricaoInternaEdicaoViewModel; GrupoInfusaoEdicao | VerificarInfusao: seleção não impõe dose; modelo conserva preparo/dose e exclui horário/notas do paciente; dois grupos persistem e reabrem |
| Salvar e reabrir rascunho | SalvarRascunhoCommand; PrescricaoInternaService | VerificarInfusao: rascunho não entra na fila da enfermagem; reabertura conserva os grupos |
| Liberar no regime de continuidade | AssinarCommand; LiberarSemAssinaturaAsync | VerificarInfusao: estado Liberada, assinatura inexistente continua nula e folha chega à fila |
| Assinatura digital e alergias | AssinaturaDePrescricaoService; PrescricaoInternaService | PrescricaoInternaTests, PrescricaoInternaPdfTests: alergia exige confirmação, certificado incompatível recusado, assinatura/arquivo/hash conferidos |
| Checagem de enfermagem | FolhaExecucaoViewModel | VerificarInfusao ampliado: perfil prescritor não pode checar; enfermeira vinculada ao conselho pode; realizado/não realizado/retificação persistem; duplo comando não duplica |
| Cancelar uma interação | DialogosDaSessao; FolhaExecucaoViewModel | VerificarInfusao ampliado: desistir da justificativa não grava não realização; desistir do motivo não cancela prescrição |
| Encerrar execução | EncerrarCommand; ChecagemPrescricaoService | VerificarInfusao ampliado: pendência impede; após checagens encerra e sai da fila padrão |
| Cancelar prescrição | CancelarInfusaoCommand | VerificarInfusao ampliado: motivo obrigatório, registro permanece cancelado no histórico |
| Suspender, não executável e retificar | ChecagemPrescricaoService; PrescricaoInternaService | PrescricaoInternaTests, DocumentoUnicoInfusaoTests: motivos, imutabilidade do registro anterior e recusa de execução de item suspenso |
| PDF e impressão | DocumentoInfusaoAsync; PrescricaoInternaPdfService | VerificarInfusao gera PDFs da prescrição e execução; DocumentoUnicoInfusaoTests confirma documento único, assinatura e arquivo original |
| Execução com assinatura eletrônica | AssinaturaDePrescricaoService | SegundaAssinaturaExecucaoTests, DuasAssinaturasNoMesmoPdfTests: segunda assinatura sem perder a primeira |
| Infusão externa, validação e devolução | InfusaoExternaViewModel; serviços do portal | InfusaoExternaAssinaturasTests, GruposInfusaoPortalTests, ContinuidadePortalTests: autoria, grupos, validação, devolução e retificação |

## Execução desta revisão

O verificador ampliado em `tools/VerificarInfusao/Program.cs` foi executado centralmente e terminou com **32 verificações aprovadas**, código de saída 0. Evidência: `artifacts/validacao-infusao-pr245.log`; PDFs e capturas em `artifacts/validacao-infusao-pr245/`. A execução foi coordenada para não concorrer com outros builds nos diretórios bin/obj.

A tabela também identifica a cobertura de serviços existente. O resultado da suíte de domínio desta revisão será consolidado no relato principal; não está implícito nas 32 verificações do harness.

Comando: `dotnet run --project tools/VerificarInfusao -c Release -- artifacts/validacao-infusao-pr245`.

## Texto da ação de liberação

O botão do diálogo web passou de “Assinar e liberar” para “Liberar para enfermagem”. O contrato atual não oferece rótulo dinâmico por modo; o texto descreve o destino comum aos dois fluxos. O comando permanece `AssinarCommand`, com as mesmas permissões, confirmações de alergia e exigência de certificado quando a assinatura digital está habilitada. O novo rótulo não afirma que houve assinatura automática nem dispensa a assinatura exigida pelo modo.

## Limites

- O verificador de infusão usa SQLite em memória, dados fictícios, ViewModels reais e comandos usados pela ponte web. As capturas dele são WPF; não comprovam todos os cliques do diálogo React.
- Testes de serviços verificam assinaturas com certificados de teste. Não comprovam o certificado físico, SafeID, impressora ou configuração de produção da clínica.
- Geração de PDF comprova disponibilidade do documento; não dispara impressão física.
- O inventário distingue cobertura existente de resultados efetivamente executados nesta revisão. Não equivale a validação clínica da prescrição nem a homologação de produção.
- Nenhum dado real, migração, release, merge ou publicação é necessário para estes cenários.
