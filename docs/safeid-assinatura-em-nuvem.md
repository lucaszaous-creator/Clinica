# SafeID: assinar em nuvem e entrar no sistema com o certificado

Projeto da integração do **SafeID** (Safeweb), o certificado A3 em nuvem que a médica já tem
contratado. Duas metades: **assinar** os documentos com ele e **entrar** no sistema por ele.

A documentação técnica está em `docs/integracao/safeid-postman-collection.json` — a coleção
Postman oficial da Safeweb, guardada no repositório porque o portal dela é inacessível deste
ambiente e porque coleção exportada é a forma que menos perde informação (traz URL, método,
corpo, exemplos de resposta e a tabela de cada campo).

## A costura já existe no PDFsharp

`docs/apis-integracao-prescricao.md` estimou esta refatoração como "não grande, mas
estrutural", supondo cirurgia no miolo do `AssinaturaDigitalService` — calcular hash à mão,
remendar o `/Contents`, recalcular o `/ByteRange`. **Não é nada disso.** O PDFsharp 6.2.4 já
publica a interface, e ela tem exatamente o formato de uma assinatura remota:

```csharp
namespace PdfSharp.Pdf.Signatures;   // PdfSharp.System.dll

public interface IDigitalSigner
{
    string        CertificateName      { get; }
    Task<int>     GetSignatureSizeAsync();
    Task<byte[]>  GetSignatureAsync(Stream conteudoCoberto);
}
```

(Verificado por reflexão sobre o assembly. `PdfSharpDefaultSigner`, que usamos hoje, é só a
implementação local dela.) Já é assíncrona, já recebe o `Stream` do conteúdo coberto pelo
`/ByteRange` e já devolve os bytes do PKCS#7 que vão para o `/Contents`. **A integração é uma
classe nova que implementa essa interface.**

## Os três modos de assinar — e por que escolhemos o primeiro

A API oferece três caminhos, e a escolha entre eles **não é técnica, é de proteção de dados**.

| Modo | Endpoint | O que sai da clínica |
|---|---|---|
| **A. Hash** | `POST /oauth/signature`, `signature_format: "CMS"` | **32 bytes de hash SHA-256** |
| B. Documento ICP | `POST /oauth/signature-icp` | **o PDF inteiro**, em base64 |
| C. PAdES com carimbo | `POST /oauth/pades-signature/{start,apply,finish}` | **o PDF inteiro**, em base64 |

**Vai o modo A**, e o argumento decisivo é o que sobe na coluna da direita. Os modos B e C
mandam para o servidor da Safeweb o **documento inteiro** — nome do paciente, CPF, CID,
medicação, posologia. O modo A manda um hash: 32 bytes dos quais não se extrai nada. Numa
clínica que mantém `TitularDadosService` para atender pedido de eliminação do titular e que
imprime o CID **só com autorização expressa do paciente**, subir a receita inteira para um
terceiro seria contradizer, numa chamada HTTP, a regra que o sistema defende em toda tela.

O modo A ainda tem a vantagem de preservar tudo o que já foi construído: o nosso
`CarimboDeAssinatura` (que de propósito não imita carimbo de tinta), o posicionamento por
`AreaAssinatura`, o `Conferir` e a regra de a reimpressão devolver os **bytes guardados**. Nos
modos B e C quem desenha e assina é a Safeweb, e esse código todo vira letra morta.

**O que se perde, e é honesto dizer:** o modo A assina *"sem políticas […] e o carimbo de
tempo"* (texto da própria doc). Ou seja, não sai com política ICP-Brasil AD-RT. Isso **não é
regressão** — a nossa assinatura de hoje já é PAdES-B, e o `docs/prescricao-eletronica-
conformidade.md` recusa anunciar LTV/PAdES-LT justamente por não implementá-lo. Se um dia a
clínica quiser AD-RT, o caminho continua sendo a ACT RFC 3161 que já está em Configurações →
Operação, aplicada por nós.

O CMS que o modo A devolve é **PKCS#7 destacado** com `contentType`, `signingTime` (hora do
PSC), `messageDigest` e `signingCertificateV2` — exatamente o que o nosso `Conferir` já
decodifica com `new SignedCms(new ContentInfo(cobertos), detached: true)`. Encaixa sem
adaptador.

## Os endpoints que vamos usar

Base: `https://pscsafeweb.safewebpss.com.br/Service/Microservice/OAuth/api/v0/oauth/`

| Método | Caminho | Para quê |
|---|---|---|
| POST | `client_token` | Token **da aplicação** (`grant_type=client_credentials`) |
| GET | `authorize` | Abre a página com QR; devolve `code` na `redirect_uri` |
| POST | `token` | Troca o `code` por `access_token` (PKCE: `code_verifier`) |
| POST | `authorize-ca` | Empurra autorização para o celular; devolve `identifierCA` |
| POST | `pwd_authorize` | `grant_type=password`, senha = `identifierCA` + PIN do SafeID |
| GET | `certificate-discovery` | **O certificado público em PEM** — é dele que sai o CPF |
| POST | `signature` | **Assina o hash** (`hash_algorithm` = OID `2.16.840.1.101.3.4.2.1`) |
| POST | `user-discovery` | Confere se um CPF tem certificado no PSC |
| POST | `signature_verify` | Validação de assinatura pelo PSC (não substitui o nosso `Conferir`) |

Escopos: `single_signature` (um hash, token morre no uso), `multi_signature` (vários hashes
numa requisição) e `signature_session` (várias chamadas dentro da validade).

**`signature_session` é o que serve à clínica.** Numa consulta que emite receita, atestado e
folha de infusão, `single_signature` faria a médica confirmar quatro vezes no celular; com
sessão ela autoriza uma vez. O `lifetime` máximo para pessoa física é **7 dias**.

## O que NÃO muda — e é a metade que importa

Todo o miolo de segurança opera sobre o certificado **público**, e o
`certificate-discovery` entrega exatamente isso (`-----BEGIN CERTIFICATE-----` em base64).
Continuam valendo sem uma linha alterada:

- **`CertificadoIcpBrasil.CpfDoTitular`** — o ASN.1 lido à mão para tirar o CPF do OID
  `2.16.76.1.3.1`. O PEM do PSC é um X.509 comum; `CertificadoIcpBrasil.Ler` o consome direto.
- **`TitularDoCertificado.Exigir`** — a regra que impede o e-CPF de outra pessoa assinar pela
  médica. Em nuvem ela fica **mais** necessária, não menos: some o gesto físico de encaixar o
  token, e ela vira a única barreira que sobra.
- **`Conferir`** e **`ConferirCadeia`** — já são sobre os bytes e sobre a cadeia.
- **`ArquivoAssinado`** e a reimpressão pelos bytes guardados.

Note que a resposta do `token` traz `authorized_identification` (o CPF do titular). **Isso não
dispensa a leitura do OID**: o CPF que vale é o que está DENTRO do certificado que assinou, não
o que o servidor afirma ao lado. São coisas diferentes no dia em que divergirem.

## O problema que a doc revelou: a URI de retorno

Os dois fluxos de autorização entregam o resultado numa `redirect_uri` que **precisa estar
pré-cadastrada** na Safeweb. Nosso sistema é desktop, na máquina da clínica: **não existe
endereço público** para a Safeweb chamar de volta. É o mesmo problema que
`docs/apis-integracao-prescricao.md` levantou sobre o endpoint de validação da farmácia.

Há saída, e ela é o padrão para aplicativo nativo (RFC 8252): o fluxo do **QR Code** redireciona
o **navegador do usuário**, não um servidor — então `http://127.0.0.1:<porta livre>` funciona,
com o app abrindo um `HttpListener` para pegar o `code`. **Depende de a Safeweb aceitar cadastrar
uma URI de loopback**, e essa é a pergunta a fazer a eles.

O fluxo **CA** é diferente e mais problemático: ali a doc diz que o PSC "retorna para aplicação
cliente através de sua `redirect_uri`" com o `identifierCA` — isso é *webhook*, servidor a
servidor. Mas ele só é necessário **uma vez**: obtido o `identifierCA` (que vale até o fim da
validade do certificado), o dia a dia passa a ser `pwd_authorize`, que **não usa callback
nenhum** — manda CPF e `identifierCA` + PIN, e recebe o token.

**Plano:** tentar loopback no fluxo QR; se a Safeweb não cadastrar loopback, usar o fluxo CA com
um retorno obtido uma única vez na implantação, e daí em diante `pwd_authorize`.

## A segunda metade: entrar no sistema pelo SafeID

`UsuarioSistema` já aponta para `Profissional`, e `Profissional.Cpf` já é obrigatório para
assinar. O CPF autenticado pelo SafeID (`authorized_identification`) é **a mesma chave de
junção** — o login por certificado não inventa identidade nova.

Três decisões que já ficam fixadas:

- **Entra AO LADO do `AcessoService.AutenticarAsync`, nunca no lugar dele.** No balcão duas
  pessoas dividem a mesma máquina e não vão ter e-CPF cada uma; login só por certificado
  trancaria a recepção do lado de fora.
- **CPF que não casa com `UsuarioSistema` ativo NÃO cria usuário.** Autenticar prova quem a
  pessoa é, não que ela tem acesso a este sistema — quem concede acesso é a direção.
- **Grava `EventoAuditoria`**, como toda ação administrativa de acesso.

## Conferido contra a implementação de referência da Safeweb

O projeto de demonstração oficial (`PSC.DemonstracaoIntegracao`, backend .NET + front Vue) foi
lido linha a linha e **bate com o que está implementado aqui**:

- `/token` vai como `application/x-www-form-urlencoded` com `grant_type=authorization_code`,
  `client_id`, `client_secret`, `code_verifier`, `redirect_uri` e `code` — idêntico.
- `/pwd_authorize` vai como JSON com `grant_type=password` e `scope=signature_session` —
  idêntico, inclusive no escopo escolhido.
- `/signature` recebe `{ hashes: [{ id, alias, hash, signature_format: "CMS",
  hash_algorithm: "2.16.840.1.101.3.4.2.1" }] }` e a resposta é lida em
  `signatures[0].raw_signature` — idêntico.
- O `hash` é o **base64 dos 32 bytes crus** do SHA-256 (`crypto.SHA256(…).toString(Base64)`),
  que é o que `Convert.ToBase64String(hash)` produz.
- O demo **não envia `signature_policy` no `/signature`**, o que confirma a leitura da doc de
  que esse caminho assina sem política.

Duas coisas que a coleção não tinha e o demo tem:

1. **Existe ambiente de homologação** —
   `https://pscsafeweb-homologacao.safewebpss.com.br/…` (agora em
   `OpcoesSafeID.BaseHomologacao`). É onde se mede o tamanho do PKCS#7 sem gastar assinatura
   do plano de produção.
2. **O callback do fluxo CA é mesmo *webhook*.** O demo expõe um `POST /api/CA/CallbackCA`
   que o PSC chama com `{IdentifierCA, State, ExpirationDate, SerialNumber}`, e a aplicação
   depois consulta esse registro. Não é redirecionamento de navegador — confirma que o fluxo
   CA precisa de endereço público, e que o do QR Code é o caminho do desktop.

## O que ainda falta

1. **`client_id` / `client_secret`** da clínica, emitidos no autoatendimento do
   [admin-safeid.safeweb.com.br](https://admin-safeid.safeweb.com.br/) (plano gratuito de 50
   assinaturas serve para homologar).
2. **A Safeweb cadastra `redirect_uri` de loopback?** (ver acima — decide o fluxo). O demo é
   uma aplicação web e usa URL pública, então ele não responde isto.
3. **Tamanho máximo do PKCS#7 devolvido**, para dimensionar `GetSignatureSizeAsync`. O PDFsharp
   reserva o espaço no `/Contents` **antes** de assinar; reservar de menos quebra a assinatura.
   Não está documentado — mede-se em homologação; até lá, 32 KB com recusa explícita se estourar.
4. **`/signature` aceita `signature_policy`?** A interface TypeScript do demo declara o campo
   como opcional no objeto `Hash`, mas nenhuma tela o envia, e a doc diz que este caminho
   assina sem política. Se aceitar, ganharíamos AD-RT (com carimbo) **sem subir o documento** —
   vale perguntar, porque seria o melhor dos dois mundos.

Deliberadamente **não** foi criada uma classe que lance `NotImplementedException`: provedor que
aparece no seletor e falha no clique é o "botão aceso que não faz nada" da parcela 41, na tela
que dá valor jurídico ao documento.

## O `raw_signature` não é sempre o base64 padrão

O primeiro documento assinado em nuvem na clínica levou **"O SafeID devolveu uma assinatura que
não pôde ser decodificada"** (ago/2026). Não era a assinatura que faltava — era a leitura dela
que só aceitava uma forma.

`Convert.FromBase64String` é estrito exatamente nos três pontos em que um serviço REST costuma
ser frouxo, e **as três falhas produzem a mesma exceção**, sem dizer qual foi:

| forma que chega | por que chega assim | o que o .NET faz |
|---|---|---|
| **base64url** (`-` e `_` no lugar de `+` e `/`) | é a forma que o resto desta integração usa — o PKCE do próprio `ClienteSafeID` a produz — e a que sai de qualquer camada pensada para URL | `FormatException` |
| **sem o enchimento** (`=` do fim) | o comprimento deixa de fechar em múltiplo de 4 | `FormatException` |
| **com armadura PEM** (`-----BEGIN PKCS7-----`) | o mesmo conteúdo, embrulhado | `FormatException` |

`ClienteSafeID.DecodificarAssinatura` aceita as três, e mais o **hexadecimal** — que alguns PSCs
devolvem para o resultado cru.

⚠️ **O hexa é testado ANTES do base64, e a ordem não é preferência.** O alfabeto do hexa cabe
inteiro dentro do do base64: `FromBase64String` **aceita** qualquer hexa e devolve lixo. Deixá-lo
por último não o tornaria só inalcançável — manteria a resposta em hexa entrando **calada** no
`/Contents` do PDF, produzindo um PDF com assinatura inválida em vez de uma recusa. Que é a
garantia aparente que o projeto recusa desde a parcela 3. Por isso ele só ganha quando o
resultado começa em `0x30`, o `SEQUENCE` que abre todo DER e portanto todo PKCS#7 — um base64 de
verdade lido como hexa não cai aí (um DER em base64 começa em `MII`, não em `30`).

**Quando nada serve, recusar continua sendo o certo — mas a mensagem carrega a evidência**: o
tamanho, o começo do que veio e **quais caracteres** estão fora do base64. A frase do .NET
("contains a non-base 64 character") não diz qual é, e foi por isso que a primeira ocorrência não
deu para diagnosticar pelo log. Mostrar o começo do valor é seguro e é decisão: a assinatura
destacada cobre um **hash**, não o documento — não há nome de paciente, CID nem medicação dentro
dela; e o que veio no lugar já não é assinatura de nada.

De quebra, `status` e `message` da resposta entram na frase de "não devolveu a assinatura": o PSC
responde 200 com o motivo no corpo em alguns casos, e a frase sozinha mandava a clínica procurar
defeito no celular da médica.
