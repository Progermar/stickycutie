# StickyCutie API — Sincronização

## Endpoints

### POST /sync/send
O cliente envia notas atualizadas.

Campos:
- id
- title
- content
- updated_at
- deleted
- group_id
- user_id

### GET /sync/updates?since=timestamp
Servidor devolve alterações novas.

### POST /sync/ack
Cliente confirma que recebeu os updates.

## Regras de Sincronização
- Conflito resolvido por `updated_at`
- Servidor vence empate
- Soft-delete sempre respeitado

