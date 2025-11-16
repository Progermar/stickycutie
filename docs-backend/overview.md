# StickyCutie API — Overview

## Objetivo
Backend oficial do StickyCutie para:
- Login e autenticação (JWT)
- Sincronização de notas entre múltiplos dispositivos
- Grupos, usuários e permissões
- Envio/recebimento de notas e eventos de sync
- Preparação para integração mobile (Android)

## Stack
- FastAPI
- SQLAlchemy
- Alembic
- PostgreSQL
- JWT (PyJWT)
- Bcrypt (hash de senha)

## Estrutura Geral
```
app/
 ├── main.py
 ├── database.py
 ├── models.py
 ├── auth/
 ├── routes/
 ├── schemas/
 └── crud/
alembic/
docs-backend/
```

## Fluxos Principais
- **Auth:** register/login, tokens, refresh
- **Sync:** send → updates → ack
- **Notas:** create/update/delete (soft-delete)
- **Eventos:** sync_events guarda histórico incremental

