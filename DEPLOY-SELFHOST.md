# Deploy self-host da API do CineMatch (Docker + Caddy)

Deploy da **API** num servidor Linux com Docker Compose e TLS automático via Caddy.
O **frontend fica na Vercel** e aponta para a API pública por HTTPS.

```
[Vercel]  cinematch-inky.vercel.app  ──HTTPS──►  api.SEU-DOMINIO.com
                                                       │
[Servidor Linux]                                       ▼
   docker compose:
     caddy      :80/:443  ──reverse_proxy──►  api:8080
     api        (ASP.NET, migrations automáticas no start)
     postgres   (volume cinematch-pgdata)
```

## Pré-requisitos no servidor
- Docker + Docker Compose plugin.
- Um subdomínio para a API (ex.: `api.cinematch.seu-dominio.com`) com **registro DNS A/AAAA apontando para o IP do servidor**.
- **Portas 80 e 443 abertas** para a internet (o Caddy usa para emitir o certificado Let's Encrypt).
- `HF_TOKEN` (Hugging Face, permissão de Inference Providers) e `TMDB_TOKEN` (TMDB v4 Read Access Token — o mesmo do frontend).

## 1. Clonar o repositório
```bash
sudo mkdir -p /opt/cinematch && sudo chown -R $USER:$USER /opt/cinematch
git clone https://github.com/arthurhzm/movies-api.git /opt/cinematch
cd /opt/cinematch
```

## 2. Criar o `.env`
```bash
cp .env.example .env
nano .env
```
Preencha:
```env
POSTGRES_PASSWORD=<openssl rand -hex 32>
JWT_SECRET_KEY=<openssl rand -hex 64>
HF_TOKEN=hf_...
TMDB_TOKEN=eyJ...            # TMDB v4 Read Access Token (Bearer), igual ao do front
API_DOMAIN=api.cinematch.seu-dominio.com
```
> Use `hex` (não `base64`) nas senhas — evita `+ / =` que atrapalham o `.env` do Compose.

## 3. Subir tudo
```bash
docker compose up -d --build
docker compose ps      # postgres (healthy), api, caddy — todos "running"
```
As migrations criam o schema no primeiro start (banco começa **limpo**).

## 4. Verificar a API
```bash
# Certificado + resposta pública (pode levar ~30s para o Caddy emitir o cert)
curl -sI https://api.SEU-DOMINIO.com/swagger/index.html | grep HTTP   # HTTP/2 200

# Login (mesmo com credencial inválida, deve retornar JSON, não erro de rede)
curl -s https://api.SEU-DOMINIO.com/login -X POST \
  -H "Content-Type: application/json" -d '{"email":"x@x.com","password":"x"}'
```
Logs se algo falhar: `docker compose logs api --tail=80` / `docker compose logs caddy --tail=40`.

## 5. Apontar o frontend (Vercel) para a nova API
1. No projeto da Vercel → **Settings → Environment Variables**, defina:
   ```
   VITE_FETCH_URL = https://api.SEU-DOMINIO.com
   ```
   (mantenha `VITE_TMDB_API_KEY` / `VITE_TMDB_READ_ACCESS_TOKEN`).
2. **Redeploy** do frontend na Vercel (Vite injeta a env no build).
3. **CORS**: a origem da Vercel precisa estar em `allowedOrigins` no `MoviesAPI/Program.cs`.
   Já inclui `https://cinematch-inky.vercel.app`. Se o seu domínio for outro, adicione-o lá,
   faça commit e `git pull && docker compose up -d --build api` no servidor.

## 6. Teste ponta a ponta
Abra o site na Vercel → login → recomendações/busca/roleta/chat, import Letterboxd e Match.
A sessão se renova sozinha (cookie de refresh `SameSite=None; Secure` em produção).

## Atualizações futuras
```bash
cd /opt/cinematch
git pull
docker compose up -d --build api      # rebuild só da API
```

## Backup do banco
```bash
# cron diário (crontab -e)
0 3 * * * docker exec cinematch-postgres pg_dump -U cinematch cinematch | gzip > /opt/backups/cinematch_$(date +\%Y\%m\%d).sql.gz
```

## Notas
- **Sem porta publicada** na API: só o Caddy expõe 80/443. Se já houver algo nessas portas
  no servidor, ou rode o Caddy no host (fora do compose) e remova o serviço `caddy` daqui,
  ou ajuste o proxy existente para `reverse_proxy` → `cinematch-api:8080`.
- **`HttpsRedirection`**: atrás do Caddy a API recebe HTTP interno; o redirect é no-op (ok).
- **Trocar de banco depois**: para usar um Postgres externo, remova o serviço `postgres` e
  aponte `DATABASE_URL` para ele (formato Npgsql `Host=...;Port=...;Database=...;Username=...;Password=...`
  ou URL `postgresql://user:pass@host:5432/db`).
