# StickyCutie

Aplicativo WPF para notas adesivas que replica o visual dos Post-it e prepara o terreno para o modo local-first com sincronização FastAPI.

## Estrutura do repositório
- `clients/wpf/StickyCutie.Wpf`: projeto principal (WPF .NET 8).
- `clients/wpf/StickyCutie.Wpf/Data`: DTOs e `DatabaseService` (SQLite via `Microsoft.Data.Sqlite`).
- `clients/wpf/StickyCutie.Wpf/Alarms`: janelas + `AlarmManager` (edição, alerta e snooze).
- `clients/wpf/StickyCutie.Wpf/CreateNoteChoiceWindow*.xaml`: mini-modal exibido ao clicar no `+`.
- `clients/wpf/StickyCutie.Wpf/CreateNoteAdvancedWindow*.xaml`: modal completo para notas destinadas a outras pessoas do grupo.
- `clients/wpf/StickyCutie.Wpf/NoteDocumentBuilder.cs`: helper que gera o `FlowDocument` padrão com texto inicial.
- `clients/wpf/StickyCutie.Package`: projeto de empacotamento MSIX (Desktop Bridge).
- `docs/`: documentação funcional e visual.
- `run-stickycutie.bat`: encerra instâncias antigas e executa `dotnet run --project clients/wpf/StickyCutie.Wpf/StickyCutie.Wpf.csproj`.

## Fluxo de primeiro uso
1. `App` inicializa `DatabaseService` e faz `EnsureContextAsync`.
2. Se não existir `current_author_id`, abre o `SetupWindow` (cria o primeiro admin, calcula a senha SHA-256 e salva `current_author_id` + `current_user_id`).
3. Assim que o setup termina o hub (`MainControlWindow`) é exibido e a engrenagem vira o ponto central do aplicativo.
4. A engrenagem abre `SettingsWindow`. Apenas administradores autenticados veem todas as abas. Caso não exista grupo ativo, o app abre a janela automaticamente (sem pedir senha) para você cadastrar/ativar o primeiro grupo.
5. Quando um grupo vira ativo, `RestoreNotesAsync` carrega apenas as notas dele; se não existir nenhuma, `CreateAndShowNoteAsync` cria a nota inicial.
6. O `SyncService` inicia em segundo plano para buscar atualizações remotas a cada 10 segundos.

## Conceitos principais
- **Autor atual (`current_author_id`)**: quem usa o PC (define `created_by_user_id` nas notas).
- **Destinatário atual (`current_user_id`)**: pessoa padrão para quem notas serão criadas (`source_user_id`).
- **Grupos**: notas pertencem a um `group_id`. Trocar o grupo fecha as janelas abertas e recria apenas as notas do novo grupo.
- **Soft delete**: excluir nota marca `deleted = 1` e fecha a janela.

## Conteúdo das notas
- O `RichTextBox` usa `FlowDocument`. Salvamos em XAML via `XamlWriter.Save` e restauramos via `XamlReader.Load`.
- Imagens coladas são copiadas para `%LOCALAPPDATA%/StickyCutie/images/<uuid>.png`, registradas na tabela `note_images` e referenciadas com `file:///...` dentro do XAML.
- O menu contextual identifica cliques sobre imagens para exibir “Excluir imagem” apenas quando necessário.

## Criação de notas
- O botão `+` abre `CreateNoteChoiceWindow`. Opções:
  - **Pessoal (para mim)** → chama `App.CreatePersonalNoteAsync()`.
  - **Outro usuário do grupo…** → abre `CreateNoteAdvancedWindow` com título, destinatário, texto inicial e alarme opcional.
- `App.CreateNoteForRecipientAsync()` persiste a nota, registra imagens/alarme e abre a janela imediatamente. Se o destinatário for outro usuário do grupo, o app também tenta enviar a nota para o backend.

## Alarmes
- Cada nota tem o botão 🔔 na toolbar. Ali é possível definir a data/hora do alarme ou remover o agendamento.
- Quando um alarme dispara, a nota treme e exibe o mini modal **Parar / Adiar**; se ela estiver fechada, o popup global aparece.
- Arquivos de áudio personalizados ficam em `%LOCALAPPDATA%/StickyCutie/alarms`. A aba “Alarmes” da janela de configurações permite escolher/copiar os arquivos.

## Configurações globais
- `SettingsWindow` possui abas de Grupos, Usuários, Alarmes e Notas. Apenas administradores conseguem abrir a janela (é exigida a senha do setup inicial).
- A aba **Notas** lista todas do grupo ativo com Autor/Destinatário/Data e permite atualizar ou excluir (soft delete).
- O botão **Resetar sistema** (somente admins) limpa o SQLite local, a pasta `images/`, `alarms/` e dispara `POST /admin/reset` no backend. O app fecha e reabre direto no setup.
- Se o administrador esquecer a senha, o modal de autenticação também possui o botão “Resetar sistema”.

## Sincronização FastAPI
O WPF envia/recebe notas através das rotas `/sync` do backend:

| Rota | Descrição |
|------|-----------|
| `POST /sync/send` | Recebe `{ id, title, content, updated_at (unix), created_by_user_id, target_user_id, group_id, deleted }`. O backend upserta a nota e cria um registro em `sync_events`. |
| `GET /sync/updates?since=<unix>` | Retorna a lista de eventos acima do timestamp informado. O formato de retorno é o mesmo usado pelo WPF (`RemoteNote`). |
| `POST /sync/ack` | Recebe `{"event_ids":["1","2",...]}` para remover os eventos confirmados. |

`ApiService.SendNoteAsync` é chamado sempre que o usuário cria notas destinadas a outra pessoa do grupo. O `SyncService` busca os eventos e abre as notas recebidas automaticamente. Caso o envio falhe, a nota continua localmente e o usuário é avisado.

## Distribuição (MSIX)
- Projeto de empacotamento: `clients/wpf/StickyCutie.Package/StickyCutie.Package.wapproj`.
- Assets/Manifesto: `Package.appxmanifest` + pastas `Assets/*`.
- Certificado temporário: `StickyCutie_TemporaryKey.pfx` (senha `stickycutie`).
- Build do bundle (requer Windows Application Packaging Tools instaladas):
  ```
  dotnet msbuild clients\wpf\StickyCutie.Package\StickyCutie.Package.wapproj /p:Configuration=Release /p:Platform=x64
  ```
  Saída em `clients/wpf/StickyCutie.Package/AppxPackages/`.

## Testar em múltiplos PCs (versão atual)
1. Instale o MSIX em cada máquina.
2. Primeira instalação: faça o setup, crie o grupo e cadastre os usuários na aba **Usuários**.
3. Segunda instalação: após o setup local, abra Configurações → aba **Grupos**, clique em **Atualizar**, selecione o grupo existente e use **Definir ativo**. Depois sincronize os usuários e defina o destinatário padrão.
4. Crie notas para outros usuários via `+ → Outro usuário do grupo…`. Elas aparecerão automaticamente nas demais máquinas que tiverem o mesmo `group_id` ativo.

## Como executar no modo desenvolvimento
```
run-stickycutie.bat
```
O script encerra instâncias antigas (`taskkill /F /IM StickyCutie.Wpf.exe`) e executa o projeto WPF em modo Debug.

## Próximos passos planejados
1. **Convites estilo Life360**: admin gera token/convite e novos usuários entram em um grupo existente (ver `docs/convites-life360.md`).  
2. **Autenticação aprimorada**: sessão lembrada por execução, troca de senha, fluxo “esqueci minha senha” sem precisar resetar tudo.  
3. **Experiência colaborativa**: mais atalhos no post-it para selecionar destinatário, notificações push e dashboard web.
