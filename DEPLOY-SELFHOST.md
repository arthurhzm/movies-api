# Deploy do CineMatch no Notebook (Self-host)

> **Assumindo que o barberOS já está rodando** — Docker, Nginx e cloudflared já estão instalados. Você só precisa adicionar o CineMatch ao lado.

---

## Visão geral

```
[Notebook — Ubuntu]
    ├── cinematch-postgres  (container, porta 5433)
    ├── cinematch-api       (container, porta 8081)
    └── Nginx
         ├── :3002  → /var/www/cinematch  (frontend estático)
         └── /api/tmdb/* → proxy https://api.themoviedb.org/3

[Cloudflare Tunnel]
    ├── cinematch.seu-dominio.com  → localhost:3002
    └── api.cinematch.seu-dominio.com → localhost:8081
```

Dois subdomínios: um para o frontend, outro para a API. O `VITE_FETCH_URL` aponta para o subdomínio da API — como o axios roda no navegador, a URL precisa ser pública.

---

## Parte 1 — Preparar os arquivos no repositório

Faça isso **no seu Windows**, antes de copiar para o notebook.

### 1.1 Criar o `docker-compose.yml`

Crie o arquivo em `movies-api/docker-compose.yml`:

```yaml
services:
  postgres:
    image: postgres:16-alpine
    container_name: cinematch-postgres
    restart: always
    environment:
      POSTGRES_DB: cinematch
      POSTGRES_USER: cinematch
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - cinematch-pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U cinematch"]
      interval: 10s
      timeout: 5s
      retries: 5

  api:
    build:
      context: .
      dockerfile: DockerFile
    container_name: cinematch-api
    restart: always
    ports:
      - "8081:8080"
    environment:
      DATABASE_URL: "Host=postgres;Port=5432;Database=cinematch;Username=cinematch;Password=${POSTGRES_PASSWORD}"
      JWT_SECRET_KEY: ${JWT_SECRET_KEY}
      HF_TOKEN: ${HF_TOKEN}
      HuggingFace__RecommendationModel: Qwen/Qwen3-32B
      HuggingFace__ConversationModel: openai/gpt-oss-120b
      LETTERBOXD_POLL_HOURS: 6
      ASPNETCORE_ENVIRONMENT: Production
    depends_on:
      postgres:
        condition: service_healthy

volumes:
  cinematch-pgdata:
```

> **Porta 8081** (não 8080) para não conflitar com o barberOS.

### 1.2 Criar o `.env.example`

Crie em `movies-api/.env.example`:

```env
POSTGRES_PASSWORD=
JWT_SECRET_KEY=
HF_TOKEN=
```

### 1.3 Atualizar CORS da API

A API precisa aceitar requisições do novo domínio. Edite `MoviesAPI/Program.cs` e adicione o novo domínio na lista de `allowedOrigins`:

```csharp
var allowedOrigins = new List<string>
{
    "https://cinematch-inky.vercel.app",
    "https://cfcc352d8998.ngrok-free.app",
    "https://cinematch.SEU-DOMINIO.com"   // ← adicionar
};
```

> Substitua `SEU-DOMINIO.com` pelo seu domínio real (pode ser o mesmo do barberOS).

### 1.4 Criar o `.env.production` do frontend

Crie em `cinematch/frontend/.env.production`:

```env
VITE_FETCH_URL=https://api.cinematch.SEU-DOMINIO.com
VITE_TMDB_API_KEY=          # sua chave TMDB atual (copie do .env)
VITE_TMDB_READ_ACCESS_TOKEN= # mesmo valor
```

> **Não inclua token de IA no frontend** — todas as gerações usam o backend.

### 1.5 Build do frontend

```bash
cd cinematch/frontend
npm run build
```

O build vai usar automaticamente o `.env.production`. Verifique que `dist/` foi criado.

---

## Parte 2 — Enviar arquivos para o notebook

No seu Windows (PowerShell ou Git Bash). Substitua `SEU-IP` pelo IP do notebook na rede (ex: `10.0.0.160`).

```bash
# Criar diretório do frontend no servidor
ssh barberos@SEU-IP "sudo mkdir -p /var/www/cinematch && sudo chown -R barberos:barberos /var/www/cinematch"

# Copiar o build do frontend
scp -r cinematch/frontend/dist/* barberos@SEU-IP:/var/www/cinematch/

# Copiar o projeto da API
scp -r movies-api barberos@SEU-IP:/opt/cinematch
```

---

## Parte 3 — Configurar e subir a API

Agora no **notebook** (via SSH ou direto no terminal):

```bash
ssh barberos@SEU-IP
cd /opt/cinematch
```

### 3.1 Criar o arquivo `.env`

```bash
cp .env.example .env
nano .env
```

Gere as senhas e preencha:

```bash
# Gerar JWT_SECRET_KEY
openssl rand -hex 64

# Gerar POSTGRES_PASSWORD
openssl rand -hex 32
```

Exemplo preenchido:
```env
POSTGRES_PASSWORD=a3f8b2c1...
JWT_SECRET_KEY=7e4d9f2a1b3c...
HF_TOKEN=hf_...
```

> Use `hex` em vez de `base64` nas senhas. Senhas base64 contêm `+`, `/` e `=` que o Docker Compose não aceita como valor sem aspas no `.env`.

O `HF_TOKEN` precisa ter permissão de Inference Providers. O backend usa `Qwen/Qwen3-32B` para respostas JSON estruturadas (recomendações, roleta e busca) e `openai/gpt-oss-120b` para o chat; os modelos podem ser alterados pelas variáveis `HuggingFace__RecommendationModel` e `HuggingFace__ConversationModel` do compose.

### 3.2 Build e subir os containers

```bash
cd /opt/cinematch
docker build -t cinematch-api:latest .
docker compose up -d
```

### 3.3 Verificar

```bash
docker compose ps
# cinematch-postgres   running (healthy)
# cinematch-api        running (healthy)

# Testar a API localmente
curl http://localhost:8081/login -X POST \
  -H "Content-Type: application/json" \
  -d '{"email":"x@x.com","password":"x"}'
# Deve retornar JSON (mesmo que seja erro de autenticação)
```

Se a API falhar, veja os logs:
```bash
docker compose logs api --tail=50
```

---

## Parte 4 — Configurar Nginx

```bash
sudo nano /etc/nginx/sites-available/cinematch
```

Cole este conteúdo:

```nginx
# Frontend CineMatch
server {
    listen 3002;
    server_name localhost;
    root /var/www/cinematch;
    index index.html;

    # SPA routing — todas as rotas vão para o index.html
    location / {
        try_files $uri $uri/ /index.html;
    }

    # Proxy TMDB — o frontend em produção chama /api/tmdb/*
    location /api/tmdb/ {
        proxy_pass https://api.themoviedb.org/3/;
        proxy_ssl_server_name on;
        proxy_set_header Host api.themoviedb.org;
        proxy_set_header Accept-Encoding "";
        add_header Access-Control-Allow-Origin *;
    }

    gzip on;
    gzip_types text/plain text/css application/json application/javascript text/xml application/xml image/svg+xml;
    gzip_min_length 1000;

    # Cache para assets com hash no nome (JS/CSS gerados pelo Vite)
    location ~* \.(js|css|png|jpg|jpeg|gif|svg|ico|woff2)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }
}
```

Ativar:

```bash
sudo ln -s /etc/nginx/sites-available/cinematch /etc/nginx/sites-enabled/cinematch
sudo nginx -t && sudo systemctl reload nginx
```

Testar o frontend localmente:
```bash
curl -s http://localhost:3002 | head -5
# deve retornar o HTML do index.html
```

---

## Parte 5 — Configurar Cloudflare Tunnel

O cloudflared já está instalado e rodando. Só precisa atualizar a configuração.

### 5.1 Criar os registros DNS

```bash
cloudflared tunnel route dns barberos cinematch.SEU-DOMINIO.com
cloudflared tunnel route dns barberos api.cinematch.SEU-DOMINIO.com
```

### 5.2 Atualizar o `config.yml`

```bash
nano ~/.cloudflared/config.yml
```

Adicione as entradas do CineMatch **antes** do catch-all `http_status:404`:

```yaml
tunnel: barberos
credentials-file: /home/barberos/.cloudflared/<ID-DO-TUNEL>.json

ingress:
  # ── barberOS (já existente) ──────────────────────────
  - hostname: app.barberos.app.br
    path: /api/.*
    service: http://localhost:8080

  - hostname: app.barberos.app.br
    service: http://localhost:3000

  - hostname: booking.barberos.app.br
    path: /api/.*
    service: http://localhost:8080

  - hostname: booking.barberos.app.br
    service: http://localhost:3001

  # ── CineMatch (novo) ────────────────────────────────
  - hostname: api.cinematch.SEU-DOMINIO.com
    service: http://localhost:8081

  - hostname: cinematch.SEU-DOMINIO.com
    service: http://localhost:3002

  - service: http_status:404
```

### 5.3 Reiniciar o cloudflared

```bash
sudo systemctl restart cloudflared
sudo systemctl status cloudflared
# Active: active (running)
```

---

## Parte 6 — Verificar tudo

```bash
# API respondendo publicamente
curl https://api.cinematch.SEU-DOMINIO.com/login \
  -X POST -H "Content-Type: application/json" \
  -d '{"email":"x@x.com","password":"x"}'

# Frontend carregando
curl -sI https://cinematch.SEU-DOMINIO.com | grep "HTTP\|content-type"
# HTTP/2 200
# content-type: text/html
```

Abra no navegador: `https://cinematch.SEU-DOMINIO.com` — a tela de login deve aparecer.

---

## Atualizações futuras

### Atualizar a API

No notebook:
```bash
cd /opt/cinematch
git pull   # se tiver git, senão copie os arquivos pelo scp
docker build -t cinematch-api:latest .
docker compose up -d --no-deps api
```

### Atualizar o frontend

No Windows:
```bash
cd cinematch/frontend
npm run build
scp -r dist/* barberos@SEU-IP:/var/www/cinematch/
```

---

## Backup do banco

```bash
crontab -e
```

Adicione junto com o backup do barberOS:
```cron
0 3 * * * docker exec cinematch-postgres pg_dump -U cinematch cinematch | gzip > /opt/backups/cinematch_$(date +\%Y\%m\%d).sql.gz
```

---

## Resumo das portas

| Serviço | Porta local |
|---|---|
| barberOS API | 8080 |
| barberOS ERP (Nginx) | 3000 |
| barberOS Booking (Nginx) | 3001 |
| **CineMatch API** | **8081** |
| **CineMatch Frontend (Nginx)** | **3002** |
| cinematch-postgres | 5433 (interno ao Docker) |
