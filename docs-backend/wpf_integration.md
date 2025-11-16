# StickyCutie — Integração WPF ↔ Backend API  
## Protocolo Oficial de Comunicação com o Codex  
*(versão 1.0 — segura, à prova de bagunça)*

---

# 🧱 1. Objetivo deste Documento
Este arquivo define **como o Codex deve trabalhar** ao integrar o cliente **WPF** com o backend **FastAPI StickyCutie**.

Ele estabelece:

- Como enviar tarefas ao Codex  
- Como o Codex deve responder  
- O que ele pode e não pode modificar  
- Fluxos necessários para sincronização real  
- Estrutura mínima dos endpoints usados pelo WPF  

Este documento protege seu projeto contra *alterações indevidas*, *arquivos sobrescritos*, *migrations quebradas*, *lógica destruída*, etc.

O Codex deve SEGUIR este documento sempre que receber uma tarefa sobre o WPF.

---

# 🧠 2. REGRAS GERAIS PARA O CODEX (IMPORTANTÍSSIMO)

O Codex **deve obedecer todas as regras abaixo**:

### ✔️ O QUE O CODEX PODE FAZER
- Criar arquivos **novos** somente se autorizado  
- Alterar apenas os arquivos listados na tarefa  
- Adicionar classes, métodos ou rotas seguindo este documento  
- Melhorar organização quando permitido  
- Garantir que tudo compila antes de responder  

### ❌ O QUE O CODEX NÃO PODE FAZER
- Reescrever arquivos inteiros sem permissão explícita  
- Remover lógica existente  
- Criar migrations automaticamente  
- Alterar Alembic, banco ou `.env`  
- Alterar nomes de pastas ou mudar estrutura do projeto  
- Substituir código funcionando por novos blocos completos sem motivo  
- Criar tabelas novas sem tarefa autorizando  

### ⚠️ QUALQUER tarefa que viole as regras acima deve ser recusada pelo Codex.

---

# 🧩 3. FORMATO OBRIGATÓRIO DA RESPOSTA DO CODEX

Toda resposta do Codex deve seguir ESTE MODELO EXATO:

```
[✓] Tarefa concluída com sucesso

Arquivos alterados:
- caminho/arquivo1.cs (linhas 10–32)
- caminho/arquivo2.cs (criado)

Descrição técnica:
- Explique em 3 a 6 pontos objetivos o que foi implementado.

Validação:
- Projeto compila com sucesso.
- Nenhum arquivo fora da lista permitida foi modificado.
```

Se o Codex **não conseguir** executar:

```
[✗] Não consegui concluir a tarefa

Motivo:
(descrever exatamente o que impediu)
```

---

# 📬 4. COMO VOCÊ (ROGÉRIO) ENVIA UMA TAREFA AO CODEX

Use este modelo SEMPRE:

```
TAREFA CODEX #001 — Implementar Login WPF

OBJETIVO:
Implementar o fluxo de login no WPF usando a rota /auth/login do backend.

ARQUIVOS PERMITIDOS PARA ALTERAÇÃO:
- clients/wpf/StickyCutie.Wpf/Auth/LoginWindow.xaml
- clients/wpf/StickyCutie.Wpf/Auth/LoginWindow.xaml.cs
- clients/wpf/StickyCutie.Wpf/Services/ApiService.cs

ARQUIVOS PROIBIDOS:
- qualquer arquivo fora desses

INSTRUÇÕES:
1. Criar tela simples de login (email + senha + botão entrar).
2. Botão deve chamar ApiService.LoginAsync().
3. ApiService.LoginAsync envia POST para /auth/login.
4. Salvar access_token em memória (não precisa guardar refresh ainda).
5. Se sucesso → abrir MainControlWindow; se falha → mostrar mensagem de erro.

CRITÉRIOS DE ACEITE:
- projeto compila
- somente arquivos permitidos foram alterados
- resposta no formato obrigatório
```

---

# 🔌 5. INTEGRAÇÃO WPF ↔ API (O QUE O CODEX PRECISA SABER)

## 5.1 Endpoints relevantes

### **POST /auth/login**
Entrada:
```json
{
  "email": "string",
  "password": "string"
}
```

Saída:
```json
{
  "access_token": "jwt-here",
  "token_type": "bearer"
}
```

### **POST /sync/send**
Envia notas atualizadas.

### **GET /sync/updates?since=<timestamp>**
Recebe notas novas/alteradas.

### **POST /sync/ack**
Confirma recebimento.

## 5.2 Regras de sincronização (explicado como para uma criança 😁)
- Cada nota é como uma “cartinha”.
- O backend é o “correio”.
- O WPF pergunta: “Chegou algo novo?” → `/sync/updates`.
- Se chegou, ele abre a nota e responde: “Recebi!” → `/sync/ack`.
- Conflitos são resolvidos por `updated_at`.

---

# 🔨 6. Tarefas prontas (templates) para você enviar ao Codex

## **TAREFA CODEX #001 — Login WPF**
Implementar tela + envio para /auth/login.

## **TAREFA CODEX #002 — Enviar notas**
Chamar /sync/send sempre que:
- criar nota
- atualizar nota
- deletar nota

## **TAREFA CODEX #003 — Buscar notas novas**
Criar timer para chamar /sync/updates a cada 10 segundos.

## **TAREFA CODEX #004 — Confirmar notas recebidas**
Chamar /sync/ack após aplicar notas no SQLite.

---

# 🚨 7. REGRA-MÃE (SUPER IMPORTANTE)
**O Codex só pode mexer nos arquivos explicitamente autorizados em cada tarefa.  
Se alterar qualquer outro arquivo → rejeitar a tarefa.**

---

# 🎉 8. Considerações Finais
Este documento serve como:
- protocolo de segurança  
- manual de integração  
- guia de tarefas  
- linguagem de comunicação entre Rogério ↔ Codex  

Sempre envie tarefas seguindo o formato deste arquivo — o Codex vai seguir tudo à risca e seu projeto ficará seguro.

---

**Fim do arquivo — StickyCutie WPF Integration Docs**
