# Agenda SemDor — fotos reais e relatório

**Material de demonstração da PR #205.** Capturas dos componentes WPF reais do commit `798dd26`, executados localmente com banco isolado. Todos os pacientes, profissionais e horários apresentados são fictícios. As imagens mostram a versão atual; as melhorias descritas são propostas.

## Acesso e download

- [Relatório completo: 12 recomendações](DIAGNOSTICO-AGENDA.md)
- [Baixar ZIP com galeria, relatório e oito fotos](https://github.com/lucaszaous-creator/Clinica/raw/refs/heads/codex/navegacao-unificada/docs/revisoes/agenda-pr205/agenda-fotos-e-relatorio.zip)

O ZIP funciona sem servidor: extraia a pasta e abra `index.html` no navegador. As imagens abaixo também podem ser vistas diretamente nesta página do GitHub, em qualquer dispositivo.

## Capturas reais

### 1. Grade por profissional

A grade já mostra os profissionais, mas falta um seletor persistente para consultar a agenda completa de um médico.

![Grade por profissional — tela real com dados fictícios](fotos/02-grade-profissionais.png)

### 2. Semana da grade

A recepção vê profissionais reunidos por dia. Recomendamos manter a escolha do médico entre Dia e Semana.

![Semana da grade — tela real com dados fictícios](fotos/03-grade-semana.png)

### 3. Próximas vagas

Função que já existe: considera profissional, duração, jornada e bloqueios. Recomendamos integrá-la à agenda e cruzar sala/capacidade.

![Próximas vagas — tela real com dados fictícios](fotos/07-proximas-vagas.png)

### 4. Grade por sala

O Consultório 1 está ocupado às 11h no exemplo. Disponibilidade do médico não significa disponibilidade dessa sala.

![Grade por sala — tela real com dados fictícios](fotos/04-grade-salas.png)

### 5. Lista do dia

O filtro atual lista os profissionais com horários. A proposta inclui também os profissionais sem agendamentos.

![Lista do dia — tela real com dados fictícios](fotos/01-dia.png)

### 6. Horários e travas

A configuração atual utiliza uma faixa Das/Até. Recomendamos intervalos por dia e exceções por data.

![Horários e travas — tela real com dados fictícios](fotos/05-horarios-travas.png)

### 7. Marcar atendimento

O formulário real contém Próximas vagas e avisos de conflito. Esse fluxo completo deve ser preservado.

![Marcar atendimento — tela real com dados fictícios](fotos/08-marcar-preenchido.png)

### 8. Entrada da marcação

A marcação começa pela seleção do paciente.

![Entrada da marcação — tela real com dados fictícios](fotos/06-marcar-atendimento.png)
