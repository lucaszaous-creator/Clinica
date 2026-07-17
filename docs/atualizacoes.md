# Atualização automática do sistema

O app se atualiza sozinho via **Velopack + GitHub Releases**. Instalado uma única vez pelo instalador, ninguém mais precisa baixar exe manualmente.

## Como funciona

1. Ao abrir — e a cada 4 horas com o app aberto — o sistema consulta a release mais recente do repositório (`UpdateService`).
2. Havendo versão nova, ela é **baixada em segundo plano** (delta, só o que mudou) sem interromper o uso.
3. Um aviso aparece no snackbar: *"Atualização X baixada. Feche e reabra o sistema para aplicar."*
4. Ao fechar o app, o Velopack aplica a atualização; na próxima abertura já está na versão nova.
5. A versão em uso aparece no rodapé da sidebar (ex.: `v1.0.9`).

Falhas de rede/GitHub são silenciosas: o sistema continua funcionando na versão atual e tenta de novo no próximo ciclo.

## Instalação correta (uma vez por máquina)

1. Abra a página de releases: <https://github.com/lucaszaous-creator/Clinica/releases/latest>
2. Baixe e execute o **`Clinica.Faturamento-win-Setup.exe`**.
3. Pronto — a partir daí as versões novas chegam sozinhas.

⚠️ **Não distribua o exe portátil** (`publish-exe.bat` ou artefato "Build EXE" do Actions) para uso na clínica: nesse formato o Velopack se considera não-instalado e o auto-update fica desativado — é exatamente o cenário de "baixar exe novo toda hora". O rodapé da sidebar denuncia esse caso: `vX.Y.Z (portátil — sem auto-update)`. O exe portátil serve só para desenvolvimento/teste rápido.

## Como publicar uma versão nova

Pelo terminal:

```bash
git tag v1.0.10
git push origin v1.0.10
```

Ou pela aba **Actions → "Release (instalador + auto-update)" → Run workflow**, informando a versão.

O workflow (`.github/workflows/release.yml`) então: roda o publish com a versão carimbada no assembly (`-p:Version`), empacota com `vpk pack` e publica a release com Setup.exe + pacotes de update. Minutos depois, todos os apps instalados baixam a nova versão no próximo ciclo de verificação.

## Arquivos envolvidos

| Papel | Arquivo |
|---|---|
| Cliente de update (checar/baixar/aplicar) | `src/Clinica.Desktop/UpdateService.cs` |
| Agendamento (abertura + a cada 4h) e aviso ao usuário | `src/Clinica.Desktop/App.xaml.cs` |
| Versão no rodapé da sidebar | `ViewModels/MainViewModel.cs` (`VersaoApp`) + `MainWindow.xaml` |
| Pipeline de release (instalador + publicação) | `.github/workflows/release.yml` |
| Build portátil (dev apenas) | `publish-exe.bat`, `.github/workflows/build-exe.yml` |
