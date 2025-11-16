# StickyCutie API — Banco de Dados (Postgres)

## Tabelas Criadas
- **users**
- **groups**
- **group_members**
- **notes**
- **sync_events**
- **alembic_version**

## Resumo das Tabelas

### users
Controle de usuários/autenticação.

### groups
Grupos de trabalho.

### group_members
Relação entre usuários e grupos.

### notes
Notas sincronizáveis entre dispositivos.

### sync_events
Fila incremental com tudo que mudou desde o último sync.

## Regras
- `updated_at` controla conflitos.
- `deleted` é soft-delete.
- Alembic gerencia toda a migração.

