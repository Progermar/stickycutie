# StickyCutie — Integração do Sistema de Ideias (Android → API → WPF)
## Versão 1.0 — Documento Oficial

---

# 🧱 1. OBJETIVO DO SISTEMA DE IDEIAS
Este documento descreve o funcionamento completo do **Sistema de Ideias** do StickyCutie.

Este sistema permite:

- Capturar ideias rapidamente **via app Android** (botão flutuante).
- Armazenar todas as ideias **no backend (FastAPI + Postgres)**.
- Exibir todas as ideias **em um post-it especial (Inbox)** no WPF.
- Transformar cada ideia em:
  - ✔ um post-it temporário  
  - ✔ uma tarefa (Todoist)  
  - ✔ um projeto (Notion / Obsidian)  
- Remover ou marcar como concluída.

O objetivo é criar um **inbox universal** integrado entre Android, Windows e a API.

---

# 📦 2. TABELA FUTURA — `idea_inbox`

A tabela **ainda NÃO existe**, mas será criada na próxima fase.

Sugestão:

```
idea_inbox
-----------
id TEXT PRIMARY KEY (UUID)
user_id TEXT (autor da coleta)
group_id TEXT (grupo atual)
content TEXT (texto da ideia)
created_at INTEGER (timestamp)
processed INTEGER (0 = pendente, 1 = já convertido)
processed_type TEXT ("postit" | "todoist" | "notion" | "obsidian")
processed_ref TEXT (id retornado pelo destino)
```

---

# 🔌 3. ENDPOINTS FUTUROS (API)

## 📱 3.1. POST /ideas/create  
Chamado pelo app Android.

Entrada:
```json
{
  "user_id": "uuid",
  "group_id": "uuid",
  "content": "Ideia capturada pelo botão flutuante"
}
```

Saída:
```json
{ "status": "ok", "id": "<uuid>" }
```

---

## 🧲 3.2. GET /ideas/pending?user_id=X&group_id=Y  
Chamado pelo WPF (post-it Inbox).

Retorna:

```json
[
  {
    "id": "uuid",
    "content": "Texto da ideia",
    "created_at": 1731604000
  }
]
```

---

## 🔁 3.3. POST /ideas/convert/postit  
Cria um **Post-it temporário**.

Entrada:
```json
{
  "id": "uuid"   // ID do item do inbox
}
```

Backend:
- cria nota temporária  
- salva em `notes` / `sync_events`  
- marca a ideia como processada  

Saída:
```json
{ "status": "converted", "postit_id": "uuid" }
```

---

## 📝 3.4. POST /ideas/convert/todoist  
Converte ideia em tarefa Todoist.

Entrada:
```json
{
  "id": "uuid"
}
```

Saída:
```json
{ "status": "converted", "todoist_id": "12345" }
```

---

## 📘 3.5. POST /ideas/convert/notion  
Cria página no Notion.

Saída:
```json
{ "status": "converted", "notion_page_id": "<id>" }
```

---

## 📦 3.6. POST /ideas/mark/done  
Marca como executada.

---

# 🪟 4. WPF — POST-IT “INBOX DE IDEIAS”

O WPF terá um post-it especial:

### **StickyCutie — Inbox de Ideias**

Mostra todos os itens pendentes:

```
[ ]  Ideia A
[ ]  Ideia B
[ ]  Ideia C
```

Cada item tem botões:

1. **Criar post-it temporário**
2. **Enviar para Todoist**
3. **Enviar para Notion**
4. **Enviar para Obsidian**
5. **Marcar como concluída**

---

# 🔗 5. Android → API → WPF (Fluxo completo)

1. Usuário clica no botão flutuante do Android.  
2. Digita uma ideia → envia ao backend.  
3. Backend salva em `idea_inbox`.  
4. WPF chama `/ideas/pending`.  
5. Usuário destina a ideia para:
   - post-it
   - tarefa Todoist
   - projeto Notion/Obsidian  
6. Backend processa e devolve.  
7. WPF atualiza a lista.  

---

# 🧠 6. Regras importantes

- Cada ideia é **única** (UUID).  
- Uma ideia só pode ser processada **uma vez**.  
- O WPF deve atualizar o post-it Inbox automaticamente.  
- O backend é responsável por:
  - registrar conversões  
  - evitar duplicações  
  - manter histórico  

---

# 🧩 7. NOTAS AO CODEX
**NÃO IMPLEMENTAR NADA DESSE ARQUIVO AINDA.**

Apenas usar como referência futura no sprint após o post-it remoto.

---

# ✔ Fim do documento — future-ideas-integration.md
