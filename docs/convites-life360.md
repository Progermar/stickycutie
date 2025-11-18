# Planejamento – Convites e Grupos compartilhados (Life360-like)

Objetivo: permitir que múltiplas instalações do StickyCutie participem do mesmo grupo através de convites/token, de forma semelhante ao Life360. Hoje cada máquina cria um grupo diferente; precisamos de um fluxo que permita "entrar" em grupos existentes.

## 1. Backend (FastAPI)

### 1.1 Nova tabela `group_invitations`

| Campo                   | Tipo          | Descrição                                           |
| ----------------------- | ------------- | --------------------------------------------------- |
| id                      | integer (PK)  | chave técnica                                       |
| group_id                | integer       | grupo que gerou o convite                           |
| email                   | text          | endereço convidado (opcional, serve para histórico) |
| token                   | text (unique) | UUID/base64 enviado para o convidado                |
| status                  | text          | `pending`, `accepted`, `revoked`, `expired`         |
| expires_at              | datetime      | data limite (ex: +7 dias)                           |
| created_at / updated_at | datetime      | controle de auditoria                               |

### 1.2 Rotas

1. `POST /groups/{group_id}/invite`

   - Requer admin do grupo.
   - Payload: `{ "email": "ana@empresa.com" }`.
   - Gera token (UUID), grava o convite e retorna `{token, url}`.

2. `GET /groups/invitations/{token}`

   - Permite validar o convite antes de aceitar (mostra nome do grupo, quem convidou, status, expiração).

3. `POST /groups/invitations/{token}/accept`

   - Criar/associar usuário ao grupo.
   - Fluxo:
     1. Se o dispositivo ainda não tem usuário local, a chamada retorna os dados para criar `users_local`.
     2. Marca o convite como `accepted`.

4. (Opcional) `DELETE /groups/invitations/{token}` ou `PATCH /groups/invitations/{token}` para revogar um convite.

Todas as rotas devem validar se o usuário autenticado pertence ao grupo e é administrador (para criar/revogar convites).

## 2. WPF – Admin

### 2.1 Aba “Usuários”

- Botão **Convidar por e-mail**:

  1. Admin informa nome/e-mail.
  2. App chama `POST /groups/{id}/invite`.
  3. Exibe token/URL para copiar manualmente (enquanto o disparo de e-mail automático não for implementado).

- Lista de convites pendentes (grid simples):
  - Mostrar e-mail, token, status, expiração.
  - Botão para revogar convite.

## 3. WPF – Nova instalação

### 3.1 Setup inicial

- `SetupWindow` continua coletando o admin local (nome/e-mail/senha) para desbloquear o app.
- Após o setup, exibir nova tela “Participar de um grupo existente?” com duas opções:
  1. **Criar novo grupo** → fluxo atual.
  2. **Entrar com código** → abrir modal solicitando o token recebido por e-mail.

### 3.2 Modal “Entrar com código”

1. Usuário informa o token.
2. App chama `GET /groups/invitations/{token}` para validar (exibir nome do grupo).
3. Se estiver OK, chamar `POST /groups/invitations/{token}/accept` passando os dados do usuário local (nome, e-mail). O backend retorna `user_id`, `group_id` e, opcionalmente, `access_token`.
4. O app salva esses dados (`users_local`, `groups_local`, settings) e define o grupo ativo automaticamente.

## 4. Sincronização

- Após aceitar o convite:
  - Rodar `SyncGroupsFromBackendAsync` e `SyncUsersFromBackendAsync` para baixar a lista de usuários do grupo.
  - Selecionar o destinatário padrão.
  - O `SyncService` já estará pronto para receber notas destinadas ao usuário convidado.

## 5. Segurança

- Tokens devem ser únicos e difíceis de deduzir (UUID v4 ou base62).
- Expiração padrão (7 ou 14 dias). Rotina para revogar tokens expirados.
- Regras de permissão:
  - Somente admins podem gerar ou revogar convites.
  - Aceitar convite deve exigir que o token esteja em `pending` e não tenha excedido `expires_at`.

## 6. Roadmap sugerido

1. Criar tabela + CRUD + rotas no backend.
2. UI de convites no `SettingsWindow`.
3. Fluxo “Entrar com código” pós-setup.
4. Ajustes de UX (envio automático de e-mail, notificações push etc.).

Com esse recurso, qualquer instalação poderá entrar no mesmo grupo usando o token enviado pelo administrador, replicando a experiência de círculos do Life360.

📎 ADENDO – Fluxo de Convite com Link de Download (Life360-like)

Este adendo complementa o documento de convites, detalhando a experiência do usuário semelhante ao Life360.
Nada substitui o documento original — isto só adiciona fluxo e UX.

🔥 1. Geração de Convite Instantânea (modelo Life360)

No Life360, quando o administrador seleciona “Adicionar pessoas ao Círculo”, o app imediatamente:

gera um código de convite

exibe o código na tela

oferece a opção de enviar pelo WhatsApp

fornece um link para baixar o app

👉 Esse é o comportamento que será adotado no StickyCutie.

Fluxo no StickyCutie (Admin):

Admin abre:
Configurações → Gestão do Grupo → Adicionar pessoas ao grupo

O app chama:

POST /groups/{group_id}/invite

O backend retorna:

{
"token": "ABC-123",
"expires_at": "2025-11-20T12:00:00Z",
"group_name": "Art Closet"
}

WPF exibe modal com:

Código grande (ABC-123)

Validade (“Válido por 2 dias”)

Nome do grupo

Botões: Copiar código e Enviar convite

Você poderá incluir aqui as imagens que me enviou, como referência visual.

🔗 2. Mensagem Automática gerada pelo WPF (pronta para WhatsApp)

Quando o admin clica em Enviar convite, o StickyCutie gera automaticamente um texto padrão, inspirado no Life360:

Olá! Quero te adicionar ao grupo "Art Closet" no StickyCutie.

Use este código para entrar no grupo:
👉 ABC-123

Se ainda não tiver o aplicativo, baixe aqui:
👉 https://stickycutie.dexaweb.com.br/download

No Windows, o botão pode:

abrir https://web.whatsapp.com

copiar automaticamente o texto para a área de transferência

o usuário apenas cola e envia

🟣 3. Fluxo para quem recebe o convite (modelo Life360)

A pessoa recebe o código + link.
Após instalar:

Abre o StickyCutie pela primeira vez.

Após o Setup inicial local, o app mostra:

Você já tem um código de convite?
[ Entrar em grupo existente ]
[ Criar novo grupo ]

O usuário clica em Entrar em grupo existente.

Digita o código recebido.

WPF chama:

GET /groups/invitations/{token}

O app mostra:

Nome do grupo

Quem enviou

Tempo restante

Se o usuário clicar em Aceitar convite, o WPF chama:

POST /groups/invitations/{token}/accept

Backend retorna:

{
"user_id": "...",
"group_id": 3,
"access_token": "jwt..."
}

WPF salva:

user_id

group_id

access_token (em memória)

SyncService começa imediatamente a sincronizar:

notas

membros

eventos do grupo

🔐 4. Benefícios desse fluxo

Admin adiciona membros sem precisar cadastrar manualmente

A entrada no grupo fica simples e rápida

Funciona igual Life360, Slack, Discord

Ideal para múltiplos PCs e equipes (Art Closet, escritórios, famílias etc.)

Não duplica grupos nem usuários

Resolve tudo que o StickyCutie precisa para sync real
