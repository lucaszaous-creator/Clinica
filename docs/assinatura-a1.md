# Certificado A1 no portal e no desktop

Implementação de 01/10/2026, nas branches `codex/assinatura-a1` (API e desktop) e
`codex/portal-a1` (interface). Não foi publicada em produção. Cada profissional
precisa de um e-CPF A1 próprio, vigente, com arquivo `.pfx` ou `.p12` protegido por
senha e CPF igual ao seu cadastro. Um A3 custodiado em nuvem não pode ser enviado
como esse arquivo: este caminho requer a emissão ou posse de um A1 separado.

## Como usar

**Portal:** em **Meu certificado**, enviar o arquivo (até 128 KB), informar a senha
e confirmar a guarda protegida. Na assinatura do documento, revisar o PDF e os
dados clínicos, confirmar a revisão, informar novamente a senha e
clicar em **Assinar e arquivar**. O menu permite substituir ou remover a credencial.
Médicos e enfermagem usam suas contas, dentro das permissões existentes.

**Desktop:** na janela de assinatura, em **Usar arquivo A1 (.pfx ou .p12)**,
escolher o arquivo, informar a senha e clicar em **Carregar A1**. Conferir o titular
e confirmar **Assinar**. O arquivo é aberto em memória para essa operação, sem
instalação no Windows. A senha e o caminho não são gravados em configuração.
A importação do arquivo A1 é a única origem de certificado oferecida pelo aplicativo.

As assinaturas A1 usam os serviços de assinatura e arquivamento já existentes para
documentos clínicos, prescrições e execução de infusões. Não existe chamada a
provedor de assinatura remota. Emissão/renovação do A1, infraestrutura e suporte
continuam tendo seus próprios custos. PDFs já arquivados permanecem acessíveis
pelas mesmas permissões, sem conversão, nova assinatura ou alteração dos bytes.

## Guarda e autorização

- O portal armazena o PFX ainda protegido por sua senha, envelopado com ASP.NET
  Data Protection. A finalidade da proteção inclui usuário, profissional e CPF.
  A tabela `CertificadosA1` guarda apenas arquivo cifrado e metadados; não guarda a senha.
- O navegador envia arquivo e senha exclusivamente à API do portal. Não os guarda
  em storage do navegador. Os campos são limpos no envio e ao fechar a janela.
  Senhas existem temporariamente na memória do processo durante a operação.
- A autorização dura cinco minutos, pertence à sessão e é consumida uma única vez.
  Alterar o documento, os dados usados na conferência ou substituir o certificado
  exige nova revisão. Uma senha errada também exige iniciar outra autorização.
- A assinatura revalida sessão, autoria, permissões, CPF, validade, cadeia e
  revogação. Usa transação e os bloqueios de paciente/agenda do fluxo existente.
  Se a resposta se perder, consultar o documento antes de tentar novamente.
- Cadastro/substituição e remoção registram auditoria na mesma gravação. Remover
  o certificado não apaga PDFs assinados, não revoga o certificado na autoridade
  emissora e não elimina imediatamente cópias presentes em backups anteriores.
- O portal limita tentativas de cadastro/assinatura a 30 por minuto por dispositivo,
  além do limite geral. Arquivos maiores que 128 KB são recusados.

## Ativação em homologação e produção

1. Aplicar a migration aditiva `20261001140452_CertificadosA1Profissionais` pelo
   procedimento normal de implantação. Ela cria somente a tabela e o índice novos.
   Não executar o `Down` em produção para desativar a funcionalidade: usar a configuração.
2. Manter `Portal:DiretorioChaves` persistente, fora do diretório servido ao navegador,
   com acesso restrito ao processo e à administração autorizada. A biblioteca não
   cifra automaticamente esse diretório quando configurada com persistência em arquivo:
   protegê-lo com os controles de disco/segredos da infraestrutura e manter backup
   separado e protegido. Acesso conjunto ao banco, ao chaveiro e à senha do PFX permite
   abrir a chave privada. A perda do chaveiro exige recadastrar os certificados.
3. Instalar a cadeia oficial ICP-Brasil apropriada no servidor e nas estações Windows.
   O novo importador A1 exige cadeia confiável pelo sistema, consulta online de
   revogação e raiz v5 com SHA-256
   `CAA53FC6091C6951887C976E378F6EF89AA6377C55D97B6475422B71ED7E9B17`.
   Outras raízes precisam de homologação e atualização explícita da lista aceita.
   Garantir acesso aos endereços oficiais de cadeia e revogação; falha de validação
   bloqueia a assinatura. Não desativar a conferência para contornar erro de rede.
4. Publicar primeiro a API, depois o pacote privado do portal. Definir
   `Portal__A1__Habilitado=true` no serviço (padrão: desabilitado). O desktop requer
   a nova versão do aplicativo; seu importador local não depende dessa configuração.
5. Manter HTTPS, cadastro de dispositivos e proteção de sessão do portal. Não
   habilitar gravação de corpos de requisição em proxy, APM ou logs para essas rotas.
   Os backups do banco passam a conter credenciais cifradas e devem seguir controles
   de acesso e retenção apropriados, também após a remoção da credencial ativa.
6. Homologar com um A1 real autorizado: senha incorreta, CPF divergente, certificado
   vencido, cadeia válida, assinatura de documento médico e execução da enfermagem,
   abertura/conferência do PDF final, substituição e remoção. Não enviar certificados
   ou senhas em chats, tickets ou repositórios.

Desabilitar a configuração bloqueia novos cadastros/assinaturas e oculta o menu.
A rota autenticada de remoção continua disponível; a clínica pode reabilitar o
menu para o próprio titular remover o arquivo. Documentos existentes são preservados.
Autorizações pendentes ficam em memória: reinício do processo exige nova revisão;
em múltiplas instâncias, usar afinidade de sessão ou implementar armazenamento
compartilhado dessas autorizações antes de habilitar.

## Verificações

- `dotnet test tests/Clinica.Tests`: regras e assinatura/arquivo de documentos.
- `dotnet test tests/Clinica.Assinaturas.Tests`: contrato HTTP; os testes A1 usam
  certificados fictícios e substituem a confiança somente no host de testes.
  Verificam cifragem, isolamento, CSRF, consentimento, mudança de conteúdo/certificado,
  senha errada, uso único e integridade criptográfica do PDF após remover a credencial.
  A conferência abrange horários, períodos, formatos, desenhos, diluição e registros da
  enfermagem; mudanças depois da autorização exigem revisão no A1.
- `python tools/verificar-suite.py` e `python tools/compilar-sombra.py`.
- `dotnet build src/Clinica.Clinico` e
  `dotnet run --project tools/ValidarLayoutWindows -- --a1` no Windows.
- No repositório da interface: `node ferramentas/testar-portal-a1.mjs`, além dos
  testes de consultório, posto e endereço da receita. O teste A1 cobre o formulário,
  revisão do documento, limpeza da senha, prevenção de interpretação de HTML do titular,
  telas de 320 a 1280 pixels e acessibilidade WCAG automatizada.
- `testar-portal-a1-concorrencia.mjs` e `testar-portal-a1-dialogos.mjs`: respostas
  atrasadas não substituem outra revisão nem fecham outra janela. A confirmação
  identifica o documento. Erro ao atualizar a tela depois da assinatura preserva
  a informação de que o PDF foi assinado e arquivado.
- No desktop, erro de importação A1 limpa a seleção anterior e exige nova escolha
  explícita. O cenário `--a1` verifica essa guarda e a limpeza de senha ao fechar.

Testes sintéticos e compilação não confirmam interoperabilidade com o A1 real da
cliente, a configuração da infraestrutura ou certificação regulatória do sistema.

## Compatibilidade com o ambiente de 01/10

A versão A1 incorpora a correção clínica já instalada no servidor: novas prescrições
reservam campos de execução antes da primeira assinatura e recebem a segunda assinatura
por revisão incremental. PDFs anteriores continuam com seus bytes originais e, quando
existente, com a folha de execução separada acessível.

O atualizador reconhece explicitamente o identificador histórico
`20261001015315_ConclusaoAutomaticaSafeId` como antecessor permitido. Essa referência
é somente compatibilidade com a migration já aplicada: não reativa integração, serviço
ou credencial do antigo provedor. A tabela histórica correspondente é preservada.

Na infraestrutura atual, o serviço de homologação só tem acesso de rede ao localhost.
A consulta online de revogação do A1 exige liberação dos endereços oficiais indicados
no certificado do titular. Enquanto isso não for conferido, a aceitação real permanece
pendente e a validação deve continuar bloqueando qualquer cadeia não verificável.
