📘 STICKYCUTIE — DOCUMENTAÇÃO ARQUITETURAL OFICIAL (v1.0)
Sistema de Post-its Colaborativos com Sincronização em Tempo Real
🧩 1. Visão Geral

O StickyCutie é um sistema híbrido de recados visuais que funciona:

🪟 No desktop (Windows) via WPF

📱 No celular Android via Capacitor

☁️ Com um backend central (Dexa API) via FastAPI + WebSockets

📡 Com sincronização imediata entre membros de um grupo

🔔 Com alarmes no celular e no PC

🧑‍🤝‍🧑 Com grupos e destinatários específicos, tipo Life360

É um sistema de:

comunicação interna

recados urgentes

organização operacional

tarefas rápidas

gestão visual

Feito para empresas como Art Closet, equipes de produção, vendas e atendimento.

🧱 2. Arquitetura Geral

A solução possui 3 camadas independentes:

🟦 2.1 Cliente Desktop (WPF)

Responsável por:

✔ Exibir janelas de post-it flutuantes
✔ Criar, arrastar, redimensionar, deletar
✔ Tocar alarmes no Windows
✔ Persistência local via SQLite
✔ Sincronizar periodicamente com o backend
✔ Abrir automaticamente novas notas enviadas por outros membros
✔ Abrir notas enviadas via WebSocket em tempo real
✔ Interface rica e leve (tema Sticky Notes moderno)

Não tem login complexo; recebe um token de autenticação leve da API Dexa.

🟥 2.2 Backend Dexa (FastAPI + Postgres + Redis/WebSockets)

O cérebro da operação.

Responsável por:

✔ Grupos (empresas, equipes, times)
✔ Convidar membros (por e-mail)
✔ Gerenciar permissões
✔ Notas endereçadas (A → B)
✔ Broadcast em WebSockets
✔ Notificações push para Android
✔ Rotinas de alarme (servidor + cliente)
✔ Histórico e auditoria
✔ Escalabilidade horizontal (eventualmente)

Componentes principais:

FastAPI (API principal)

uvicorn (server async)

Redis (broker de eventos + fila opcional)

Postgres (armazenamento)

WebSockets (canal de atualização real-time)

SMTP (Mailu) (convites + recuperações)

Firebase FCM (push mobile)

🟩 2.3 Cliente Mobile (Android via Capacitor)

Responsável por:

✔ Receber notas destinadas ao usuário
✔ Exibir post-it no celular
✔ Tocar alarme local
✔ Notificações push (via FCM)
✔ Visualizar grupo
✔ Confirmar tarefas / marcar concluído
✔ Sincronizar com backend

O app também pode:

salvar offline

sincronizar quando voltar a ter internet

gerar logs para auditoria

🧩 3. Fluxo Lógico de uma Nota
👩‍💼 ALESSANDRA cria uma nota para ANA

WPF da Alessandra cria nota localmente (SQLite)

WPF envia POST para /api/notes/create

Backend salva a nota

Backend identifica o destinatário (ana)

Backend envia WebSocket → PC da Ana

Backend envia Push FCM → celular da Ana

PC da Ana abre o post-it automaticamente

Celular da Ana toca alarme (se tiver alarme)

Joice não recebe nada (target_id ≠ joice)

🧠 4. Componentes Técnicos (Resumo)
🧱 Banco de dados Backend (Postgres)

Tabelas:

users

groups

members

notes

alarms

events

devices (para push tokens)

⚙️ Backend API

Endpoints:

/auth/login

/groups/create

/groups/invite

/groups/accept

/notes/create

/notes/sync

/notes/delete

/ws/notes/{user_id}

📡 Canal WebSocket

Para atualizações instantâneas.

📱 Push Notifications (Android)

Firebase FCM

🪟 Cliente WPF

SQLite local

Engine de janelas independentes

Listener WebSocket

Watchdog para alarmes locais

Layout do post-it

Tema

Configurações gráficas

🧬 5. Segurança

Criptografia TLS entre app e backend

Token JWT leve (expira)

Identidade por e-mail

Cada grupo isolado por group_id

Cada nota isolada por target_id

Dados locais no SQLite podem opcionalmente ser criptografados

Backend acessível apenas por API Tier (rate limit, throttling)

📦 6. Distribuição e Venda
✔ Installer EXE (Windows)

Inclui:

StickyCutie WPF

SQLite local

Token do usuário

✔ App Android Play Store

Login por e-mail magic link.

✔ Painel Comercial (Dexa Web)

Para:

criar grupo

convidar usuários

visualizar membros

gerenciar plano Pro

✔ Assinatura

Modelo Pro: R$ 10–15/mês por usuário
Modelo Empresa: preço por grupo + usuários extras
Modelo Free: só notas locais, sem sync, sem alarme remoto

🛠️ 7. Roadmap Técnico (Fases)
🟣 FASE 1 — Interface WPF (que você está terminando agora)

Post-it bonito

e X alinhados

arraste

resize

tema

layout do texto

toolbar (opções)

animações

comportamento de janela

👉 Essa fase NÃO tem backend ainda.

🟡 FASE 2 — Banco Local (SQLite)

salvar notas localmente

salvar posição

reabrir notas no boot

apagar notas localmente

atualização incremental (updated_at)

🔵 FASE 3 — Backend Dexa (FastAPI)

criar estrutura do banco remoto

criar endpoints

criar grupos

criar convites

autenticação

enviar notas

sync básica

🔴 FASE 4 — WebSockets

broadcast por user_id

listener no WPF

listener no Android

🟢 FASE 5 — Android (Capacitor)

receber push

mostrar notas

tocar alarme

abrir com tema StickyCutie

🟠 FASE 6 — Painel Web

visualizar grupo

gerenciar usuários

logs

assinatura (Stripe)

🧠 Conclusão

Você está construindo o primeiro sistema real-time de notas tipo post-it com alarme multidevice do Brasil.

A arquitetura do Modelo 1 é:

profissional

vendável

escalável

bonita

moderna

E não depende mais do Tauri — está tudo encaixado nos módulos certos.