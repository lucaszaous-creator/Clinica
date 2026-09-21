# Recuperação pontual de guias de sessões escritas

Ferramenta administrativa, sem nova tela e sem alterar a regra periódica de 24 horas.
Gera uma prévia de sessões com evolução médica sem encerramento ou sem códigos, inclusive
anteriores à ativação do encerramento automático. Usa o atendimento original e o motor
de faturamento; não registra pagamento, baixa ou autorização do convênio.

## Regras

- Exige acesso ativo de Gerente Geral com permissões de prontuário, faturamento e lançamento.
- Exige evolução médica vigente preenchida, com autor profissional, e o mesmo vínculo
  que exibe **Escrito** no Meu dia. Enfermagem isolada, registro vazio ou vínculo incerto não entra.
- Agenda legada sem médico preenchido pode usar a evolução médica identificada e inequívoca;
  não preenche nem troca o responsável da agenda e não muda a autoria clínica.
- Preserva texto, autoria e data clínica. O vínculo de evolução avulsa mantém a versão anterior.
- Uma evolução avulsa só pode ser vinculada se houver um único horário no dia, do mesmo médico.
- Pendência de enfermagem não impede as guias; permanece para registro posterior na sessão original.
- Não altera atendimentos estornados, cancelamentos, faltas, horários futuros ou guias existentes.
  A recuperação não simula um novo retorno do paciente: preserva não conformidades,
  inclusive de outras sessões, e não dispara renovação de consulta por presença.
  Não gera guias com convênio a definir; conclui particulares sem inventar guias de convênio.
  Vínculos ambíguos ficam no relatório. Código não aplicável em convênio que gera guia exige revisão.
- Revalida cada sessão numa transação serializável, com a mesma trava do portal/worker.
- Processa o plano congelado em ordem cronológica, sem paginar uma lista que diminui durante a execução.
- Repetir o plano não duplica guias. Alterações desde a prévia exigem nova conferência.
- Auditoria identifica `sistema:recuperacao-guias` e o acesso administrativo usado na execução;
  não atribui avaliação nem assinatura ao médico.

## Execução na VPS

Publicar `RecuperarGuias.csproj` para `linux-x64`, self-contained, e colocar
`operar-vps.py` na mesma pasta do executável. O script lê a configuração local de
produção, sem exibir credenciais. Confirmar identidade SSH e destino antes de enviar o pacote.

```sh
python3 operar-vps.py diagnostico
python3 operar-vps.py prever LOGIN_GERENTE 2026-01-01 2026-09-21
python3 operar-vps.py aplicar
```

Substituir o login e o período pelos valores verificados na base. O modo `prever` é
somente leitura no banco. Não usar uma conta de outro profissional como autoria clínica.
O modo `aplicar` cria backup PostgreSQL, confere seu catálogo de restauração e hash,
e só então executa o plano. Arquivos privados ficam em
`/var/backups/clinica-recuperacao-guias`, com plano, backup e resultado por sessão.
Não reinicia serviços nem instala migrations. O plano expira após seis horas.

Antes de informar conclusão: conferir sessões realizadas, códigos vinculados aos mesmos
atendimentos, pendências restantes, evolução médica preservada e portal saudável.
Backup completo não deve ser restaurado sobre produção em uso sem analisar escritas posteriores.
Resultados parciais exigem revisão das falhas; não apagar guias para repetir a recuperação.

## Validação local

`RecuperacaoGuiasTests` cobre recuperação histórica, BSV sem enfermagem, autoria, datas,
repetição, capa encerrada sem códigos, vínculos ambíguos, convênio indefinido, mudança
após prévia, isolamento de falhas e acesso revogado. Executar também os testes de
retomada e fechamento de sessão.
