# StickyCutie - Banco de Dados Local (SQLite)

Este arquivo descreve o schema de persistencia local usado pelo aplicativo WPF. O banco fica em `%LOCALAPPDATA%/StickyCutie/stickycutie.db` e e gerenciado pela classe `DatabaseService`.

## 1. Objetivo
Garantir que todas as notas, imagens, alarmes e configuracoes de contexto (autor, destinatario e grupo) funcionem no modo local-first e estejam prontas para sincronizacao futura via FastAPI.

## 2. Entidades
| Tabela            | Descricao                                                                          |
|-------------------|-------------------------------------------------------------------------------------|
| `users`           | Pessoas cadastradas localmente (admins e destinatarios das notas).                 |
| `groups_local`    | Grupos/equipamentos onde as notas serao exibidas.                                  |
| `notes_local`     | Notas/post-its exibidos no desktop.                                                |
| `note_images`     | Metadados das imagens inseridas inside do RichTextBox.                             |
| `alarms_local`    | Alarmes individuais de cada nota.                                                   |
| `settings_local`  | Preferencias globais (`current_author_id`, `current_user_id`, `current_group_id`). |

*Regra geral:* todos os `id` usam `TEXT` (UUID), timestamps usam `INTEGER` (Unix epoch) e booleanos usam `INTEGER` 0/1.

## 3. Campos por tabela
### users
```
id TEXT PRIMARY KEY,
name TEXT,
email TEXT,
phone TEXT,
password_hash TEXT,
created_at INTEGER,
updated_at INTEGER,
is_admin INTEGER
```

### groups_local
```
id TEXT PRIMARY KEY,
name TEXT,
description TEXT,
joined_at INTEGER,
updated_at INTEGER
```

### notes_local
```
id TEXT PRIMARY KEY,
local_id TEXT,
server_id TEXT,
title TEXT,
content TEXT,
color TEXT,
theme TEXT,
x INTEGER,
y INTEGER,
width INTEGER,
height INTEGER,
locked INTEGER,
lock_password TEXT,
alarm_enabled INTEGER,
alarm_time INTEGER,
alarm_repeat TEXT,
photo_mode INTEGER,
updated_at INTEGER,
created_at INTEGER,
deleted INTEGER,
target_user_id TEXT,
source_user_id TEXT,
created_by_user_id TEXT,
group_id TEXT
```

`source_user_id` representa o destinatario da nota (quem deve recebe-la). `created_by_user_id` guarda quem criou a nota (autor real no dispositivo) e e usado para auditoria/sincronizacao.

### note_images
```
id TEXT PRIMARY KEY,
note_id TEXT NOT NULL REFERENCES notes_local(id),
path TEXT,
order_index INTEGER,
duration INTEGER,
created_at INTEGER
```

### alarms_local
```
id TEXT PRIMARY KEY,
note_id TEXT NOT NULL REFERENCES notes_local(id),
alarm_at INTEGER,
snooze_until INTEGER,
is_enabled INTEGER,
created_at INTEGER,
updated_at INTEGER
```

### settings_local
```
key TEXT PRIMARY KEY,
value TEXT
```

## 4. Fluxos importantes
- O `SetupWindow` cria o primeiro usuario admin, salva `current_author_id` e `current_user_id` em `settings_local` e habilita o hub principal.
- O hub (MainControlWindow) abre o `SettingsWindow`, onde o admin gerencia grupos e usuarios. Apenas admins autenticados (hash SHA-256) acessam essa tela.
- Notas sempre pertencem ao `current_group_id` ativo. `source_user_id` e preenchido com o destinatario padrao definido na aba Usuarios.
- O conteudo do RichTextBox e salvo como XAML (`XamlWriter.Save`) e restaurado com `XamlReader.Load`, preservando formatacao, marcadores e imagens.
- Imagens sao copiadas para `%LOCALAPPDATA%/StickyCutie/images/<uuid>.png`, registradas em `note_images` e referenciadas via `<Image Source="file:///..." />` dentro do FlowDocument.
- Alarmes sao configurados por nota, persistidos em `alarms_local` e monitorados pelo `AlarmManager` com temporizador em background. Snooze grava `snooze_until` e parar desabilita `is_enabled`.
- Exclusao de nota e sempre soft delete (`deleted = 1`).

Com este schema, o aplicativo atende ao modo local-first e esta pronto para sincronizar usuarios, grupos e notas contra o backend FastAPI no futuro.
