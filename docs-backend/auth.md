# StickyCutie API — Autenticação (JWT)

## Endpoints

### POST /auth/register
Cria usuário com:
- email
- nome
- senha (bcrypt)

### POST /auth/login
Retorna:
- access_token (15 min)
- refresh_token (7 dias)

## Middleware
Rotas protegidas usam:
```
Authorization: Bearer <token>
```

## Fluxo
1. Usuário envia email/senha
2. Validamos hash
3. Geramos JWT
4. Salvamos refresh opcional

