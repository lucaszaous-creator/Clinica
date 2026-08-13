# A NOITE DA VIRADA — checklist de uma página

Migração definitiva Neon → VPS. Imprima esta folha. A regra de ouro: **cada passo
tem um critério de "pode seguir" — sem o critério, não se avança.** Roteiro
completo e tabela sintoma → conserto: `docs/banco-na-vps.md`.

## Dias ANTES (não deixe para a noite)

- [ ] **Backup automático rodando**: `ls -lh /var/backups/clinica/` mostra dumps
      dos últimos dias e o `backup.log` tem uma linha "ok" por dia.
- [ ] **Kit no pendrive**: pasta `C:\kit` com `ca.crt`, os `.pfx` e o
      `instalar-maquina.bat` (senhas já embutidas — nada a editar).
- [ ] **Ensaio feito**: o app já abriu contra a VPS mostrando os dados reais.
- [ ] **Backup/snapshot do painel do provedor** ativado, se o plano tiver.
- [ ] **Equipe avisada**: a partir do horário combinado, ninguém usa o sistema
      — inclusive quem trabalha de casa.

## A noite, na ordem

1. **Todos fora do sistema.** Critério: nenhum app aberto, presencial ou remoto.
2. **Foto final** — no SSH da VPS: `~/certs/migrar-da-neon.sh` (cola a string da
   Neon quando pedir). Critério: termina na tabela de contagens, sem erro.
3. **Prova** — `~/certs/conferir-migracao.sh`. Critério: a frase
   **"TODAS as tabelas batem"**.
   ⛔ Sem essa frase, **PARE AQUI**: nada foi mudado para a clínica — amanhã ela
   trabalha na Neon normalmente. Investigue com calma outro dia.
4. **Pendrive nas máquinas** — em cada uma: botão direito no
   `instalar-maquina.bat` → **Executar como administrador** → fechar e reabrir o
   app → conferir que abriu **com os dados**. Notebooks remotos: transferir a
   pasta do kit por acesso remoto, rodar, e **devolver o `registro.txt` e a
   pasta `usados\` atualizados ao pendrive** (é o kit que controla a pilha).
5. **Contagem** — abrir `registro.txt`: **uma linha por máquina virada**. Linha
   faltando = máquina esquecida ainda gravando na Neon — volte nela agora.
6. **Teste de fumaça** — numa máquina de cada tipo (recepção, faturamento,
   consultório): abrir um paciente, a agenda do dia, as pendências, emitir um
   PDF qualquer.
7. **Backup do dia 1** — na VPS: `sudo /usr/local/bin/backup-clinica.sh` e
   conferir o dump novo em `/var/backups/clinica/`.
8. **A Neon fica INTOCADA.** Não cancele, não apague — é o plano B por 30 dias.

## Se uma máquina precisar VOLTAR para a Neon

Apagar a variável que o `.bat` gravou devolve a máquina à configuração antiga
(que continua salva nela, apontando para a Neon). Como administrador:

```
REG delete "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /F /V ConnectionStrings__Clinica
```

Fechar e reabrir o app. (Atenção: o que foi digitado na VPS depois da virada
não aparece na Neon — voltar é decisão de emergência, não de conveniência.)

## A semana seguinte

- [ ] Conferir o `backup.log` por alguns dias (uma linha "ok" por dia).
- [ ] Guardar o pendrive do kit e o `registro.txt` — é a planilha de revogação.
- [ ] **Depois de 30 dias estáveis**: encerrar a Neon e atualizar
      `docs/conformidade-lgpd.md` (item 10: suboperador vira o provedor da VPS;
      a pendência de transferência internacional do art. 33 **sai da lista**).

## Datas no calendário da clínica (a única manutenção deste desenho)

- **2031** — renovar certificados (servidor e máquinas): `docs/banco-na-vps.md`, passo 4.
- **2036** — renovar a CA.
