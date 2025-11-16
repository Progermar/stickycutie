# StickyCutie — API Pública / SDK / Integração com Terceiros
## Versão 1.0 — Documento Oficial

---

# 🧱 1. OBJETIVO
Este documento define a futura **API pública do StickyCutie**, permitindo que:

- ERPs
- Automação industrial
- Apps externos
- Sistemas de tarefas
- Aplicativos móveis
- Ferramentas de produtividade

se conectem ao StickyCutie de forma segura, completa e padronizada.

---

# 🔑 2. AUTENTICAÇÃO (JWT)
Toda integração será autenticada via:

- Token de acesso (JWT)
- Token de integração (API Key opcional)
- Permissões por grupo

---

# 🧩 3. RECURSOS PÚBLICOS DA API

## 3.1. Notas
- Criar uma nota
- Atualizar nota
- Deletar nota
- Enviar nota para grupo ou usuário
- Listar notas de um grupo
- Buscar notas por ID

---

## 3.2. Ideias (Inbox)
- Criar ideias (Android ou qualquer app)
- Listar ideias pendentes
- Converter ideias:
  - Post-it temporário
  - Tarefa Todoist
  - Projeto Notion/Obsidian
- Marcar como concluídas

---

## 3.3. Sincronização
- `/sync/send`
- `/sync/updates`
- `/sync/ack`

Terceiros poderão:
- enviar notas
- receber notas novas
- integrar seu próprio sistema de sincronização

---

# 🔌 4. WEBHOOKS (Futuro)
Possibilitar eventos automáticos:

- Nota criada
- Nota modificada
- Nota deletada
- Ideia criada
- Ideia convertida
- Sincronização concluída

---

# 📦 5. SDKs OFICIAIS
Serão disponibilizados:

- ✔ JavaScript SDK
- ✔ Python SDK
- ✔ C# SDK

Cada SDK incluirá:

- Autenticação
- Envio de notas
- Recebimento de updates
- Conexão com grupos/usuários
- Funções utilitárias

---

# 📘 6. DOCUMENTAÇÃO PÚBLICA (MODELO)
Cada endpoint terá:

```
POST /notes/create
Descrição: Cria uma nova nota.

Body:
{
  "title": "string",
  "content": "XAML string",
  "group_id": "uuid",
  "target_user_id": "uuid"
}

Responses:
200 → { "id": "uuid", "status": "created" }
400 → { "error": "invalid_data" }
401 → { "error": "unauthorized" }
```

---

# 🔒 7. SEGURANÇA
- Rate limit (limitar abusos)
- Throttling
- Chaves de integração por aplicação
- Permissões por grupo
- Logs de auditoria
- Tokens com expiração

---

# 🧠 8. MODELO DE CASOS DE USO

## ✔ Integração com ERP
ERP envia nota → StickyCutie abre para equipe.

## ✔ Integração com automação industrial
Sistema envia alerta → StickyCutie abre pop-up no PC da produção.

## ✔ Integração com CRM
Quando um lead muda de status → gera uma nota.

---

# 🛠 9. COMO TERCEIROS VÃO USAR
1. Criam credenciais no painel DexaWeb  
2. Pegam o token JWT / API Key  
3. Instalam SDK  
4. Chamam endpoints conforme necessidade  
5. Recebem eventos via webhooks  

---

# 📚 10. NOTAS AO CODEX
Este arquivo é apenas visão de documentação pública futura.

**NÃO IMPLEMENTAR NADA A PARTIR DESTE ARQUIVO.**

Apenas seguir como guia para arquitetura.

---

# ✔ Fim do documento — external_api_manual.md
