# Interface web do Financeiro

Primeira etapa da nova camada visual: resumo financeiro, séries do mês, filtros e lista de lançamentos em HTML/CSS/TypeScript, dentro do aplicativo Windows com WebView2. O C# continua responsável pelos valores, permissões, comandos, confirmações e persistência.

O resumo usa **resultado líquido do mês**, não saldo bancário. As curvas mostram somente realizados, na mesma escala, e não são desenhadas quando a carga é parcial. Os valores vêm de `CaixaViewModel`.

## Executar

```powershell
cd src/Clinica.Modulo.Financeiro/Web/frontend
npm ci
npm run build
```

Depois, na raiz do repositório:

```powershell
dotnet run --project src/Clinica.Financeiro
```

Esse caminho passa pela configuração e autenticação normais do aplicativo. A interface web é o padrão para usuários com acesso financeiro. O argumento `--financeiro-nativo` permite abrir o shell nativo para suporte; ausência ou falha do WebView2 também oferece acesso nativo. O instalador do Financeiro declara o WebView2 como pré-requisito. As outras telas e os formulários são abertos pelo shell existente; ainda não foram migrados para HTML. A compilação web fica em `Web/wwwroot` e acompanha a distribuição local, sem depender de servidor web ou CDN.

Para visualizar dados fictícios no navegador, executar `npm run dev` e abrir o endereço local com `?demo=1`. Esse modo não representa integração com banco e não deve ser confundido com as capturas do teste nativo abaixo.

## Verificar a integração sem produção

```powershell
dotnet run --project tools/validar-design-financeiro -- --web
```

O teste usa SQLite em memória, serviços C# reais, navegador WebView2 e o conteúdo web empacotado. Gera imagens em três dimensões e verifica troca de mês, filtros, retorno dos dados e rotas permitidas. Requer WebView2 Runtime instalado.

## Fronteira entre as camadas

O frontend envia ações de uma lista explícita. O host verifica a origem local, a sessão, as permissões e os comandos disponíveis. IDs de lançamentos são resolvidos na coleção corrente do ViewModel. Não há objeto .NET exposto ao JavaScript, conexão de banco no frontend ou execução arbitrária de métodos recebidos pela página.

Ícones vetoriais, fontes e logo são empacotados com os arquivos locais. A navegação externa, novas janelas e permissões do navegador são bloqueadas pelo host.
