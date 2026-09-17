# Enfermagem, conclusão médica e infusão com orientação externa

## Comportamento

- Recepção oferece **Marcar**; a entrada **Lançar** foi removida desse módulo.
- A enfermagem atende pacientes dos médicos sem trocar o responsável médico.
- **Salvar sessão**, na enfermagem, grava a evolução e o vínculo. Não conclui
  o atendimento e não cria guias.
- **Salvar e finalizar**, no médico, pergunta se houve enfermagem. A resposta
  não vem preenchida. “Sim” exige evolução vigente da sessão exata; registros
  cancelados, substituídos ou de outra sessão não liberam o fechamento.
- A conclusão registra resposta, autor e horário e conserva o fluxo idempotente
  de atendimento e guias. Só o profissional responsável pode concluir.
- Uma evolução avulsa pode ser vinculada depois à sessão original, inclusive já
  concluída. Exige autor ou direção, permissão, mesmo paciente e justificativa.
  O vínculo não reabre a sessão, não muda datas anteriores e não refatura.

## Infusão já realizada com orientação fora do sistema

A enfermagem informa a execução real, orientação recebida e médico responsável
com acesso ativo de prescrição. A execução nasce encerrada, com uma pendência
médica independente; o estado persistido continua compreensível para versões
anteriores dos módulos. Nenhuma prescrição é apresentada como assinada pelo
médico antes da assinatura dele.

A executante assina primeiro, à direita. Depois o médico responsável valida e
assina à esquerda, incrementalmente no PDF arquivado. As duas assinaturas e suas
datas reais são preservadas; a via final fica no prontuário. A operação não
conclui a sessão médica nem gera guias.

## Implantação

Migration aditiva: `20260917185911_EnfermagemVinculadaEValidacaoInfusao` (seis
colunas, sem alteração dos registros antigos). Publicar API e interface do
portal juntas. O instalador aceita manifesto contrato 3, exige backup e aceite
do mesmo pacote na homologação antes da produção. O papel do portal recebe
somente UPDATE de `EvolucoesEnfermagem.AgendamentoId` se ainda não possuir essa
permissão; não recebe administração do banco. Reversão troca o pacote anterior,
preserva as colunas e registros e retira apenas concessões novas.

Validação local: suíte de domínio/aplicação/infraestrutura, fronteira HTTP,
assinaturas criptográficas com certificados fictícios, tradução Npgsql, build
Windows, compilação-sombra, recursos XAML e telas WPF sem erros de binding.
Interface do portal: fluxos simulados, reenvio, vínculo tardio, confirmação sem
resposta padrão, acessibilidade e larguras de 320 a 1180 px com fonte ampliada.

O teste com certificados fictícios não substitui o aceite dos titulares SafeID.
A dupla assinatura real continua dependendo da disponibilidade de médico e
enfermagem. Nenhum paciente real deve ser usado para o teste automatizado.
