# Túnel: /gestao no MESMO endereço do portal

A regra entra ANTES da regra do portal no `config.yml` do cloudflared (a primeira que casa vence):

```yaml
ingress:
  - hostname: portal.clinicasemdormacae.com.br
    path: ^/gestao(/.*)?$
    service: unix:/run/clinica-gestao/gestao.sock
  - hostname: portal.clinicasemdormacae.com.br
    service: unix:/run/clinica-tablet/portal.sock   # a regra que já existe
```

E o drop-in do serviço do cloudflared ganha o grupo do socket da gestão
(`/etc/systemd/system/cloudflared-site.service.d/gestao.conf`):

```ini
[Service]
SupplementaryGroups=clinica-gestao-proxy
```

Mesmo endereço = mesma origem: os cookies `__Host-` da sessão e do aparelho que a API grava
chegam à gestão sem CORS nenhum, e a gestão aceita o WebSocket só desta origem (`Gestao__Origem`).
No Cloudflare, mantenha o WAF e o limite de requisições do portal valendo também para `/gestao/*`.
