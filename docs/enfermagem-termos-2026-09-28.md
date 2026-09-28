# Evolução, termos e infusão

O portal da enfermagem apresenta Agenda e Execução de enfermagem. A evolução abre a coleta dos termos do mesmo paciente em uma área interna, preservando o formulário preenchido e o acesso da profissional. O paciente continua sendo o signatário.

A preparação na evolução exige sessão BSV ou BSV + acupuntura do dia. O token temporário da coleta fica somente na memória da página interna; não substitui o cookie da equipe. Toda operação com esse token exige também o acesso atual da mesma profissional. Termos recebidos continuam sendo arquivados ao voltar à evolução; apenas os pendentes são encerrados. TCLE já assinado não é reemitido. A alergia confirmada acompanha o outro termo e é registrada na ficha, preservando documentos assinados e o histórico de alterações.

Prescrições novas podem registrar diluente e volume total no cabeçalho, com Observações. Documentos anteriores mantêm seu conteúdo. A pressão arterial possui campos separados. Na execução, as opções aparecem por medicamento, com justificativa obrigatória para não realização ou impossibilidade. O horário do primeiro item é aplicado aos demais; o portal também valida essa igualdade no servidor.

As migrations adicionam três campos de diluição e um indicador de impossibilidade. O estado persistido continua `NaoRealizado`, permitindo a leitura por versões anteriores do Windows. Atualizar os aplicativos para visualizar a nova identificação e a diluição no cabeçalho.

Validação: testes de serviços, testes HTTP com sessão e proteção de acesso, testes de navegador com dados fictícios e compilação do aplicativo Windows. A implantação usa o atualizador com backup, conferência de arquivos e retorno à versão anterior em caso de falha.
