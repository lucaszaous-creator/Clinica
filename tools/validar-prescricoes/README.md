# Prescrições reais com dados fictícios

No Windows com .NET 8 e WebView2 instalado:

```powershell
dotnet run --project tools/validar-prescricoes -c Release
```

Usa as duas Views, seus ViewModels, adapter e React de produção. Banco SQLite em memória com duas pessoas fictícias, documentos e infusões. A configuração de serviços usa endereço inválido como proteção adicional; o DbContext é substituído pelo SQLite. Não lê a conexão salva da clínica.

Confere busca e seleção, cancelar/trocar paciente, filtros e menus, quatro larguras, mensagens obsoletas, erro tardio de consulta, falha real de leitura/recuperação e permissões. Abre a janela real do editor de infusão pelo botão React e fecha sem salvar. A alteração temporária de nome de tabela existe apenas nesse banco em memória para exercitar falha de serviço.

Capturas ficam em `artifacts/prescricoes`. O teste de sobreposição da notificação é separado: `tools/validar-notificacao-web`.
