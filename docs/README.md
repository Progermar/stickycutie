# StickyCutie

Aplicativo WPF para notas adesivas que replica o visual dos Post-it e prepara o terreno para o modo local-first + sincronizacao FastAPI.

## Estrutura
- `clients/wpf/StickyCutie.Wpf`: projeto principal (WPF .NET 8).
- `clients/wpf/StickyCutie.Wpf/Data`: DTOs e `DatabaseService` (SQLite via `Microsoft.Data.Sqlite`).
- `clients/wpf/StickyCutie.Wpf/Alarms`: janelas + `AlarmManager` (edicao, alerta e snooze).
- `clients/wpf/StickyCutie.Wpf/CreateNoteChoiceWindow*.xaml`: mini-modal exibido ao clicar no `+`.
- `clients/wpf/StickyCutie.Wpf/CreateNoteAdvancedWindow*.xaml`: modal completo para notas destinadas a outras pessoas do grupo.
- `clients/wpf/StickyCutie.Wpf/NoteDocumentBuilder.cs`: helper que gera o `FlowDocument` padrão com texto inicial.
- `docs`: documentacao funcional e visual.
- `run-stickycutie.bat`: encerra instancias antigas e executa `dotnet run --project clients/wpf/StickyCutie.Wpf/StickyCutie.Wpf.csproj`.

## Fluxo de primeiro uso
1. `App` inicializa `DatabaseService` e faz `EnsureContextAsync`.
2. Se nao existir `current_author_id`, abre `SetupWindow` (apenas cria o primeiro admin com senha SHA-256, salva `current_author_id` + `current_user_id`).
3. O hub (`MainControlWindow`) e exibido assim que o setup termina e permanece como janela raiz com o botao da engrenagem.
4. A engrenagem abre `SettingsWindow` (apenas admins autenticados podem ver). Ali criamos grupos/usuarios e escolhemos o grupo ativo.
5. Quando um grupo vira ativo, `RestoreNotesAsync` carrega somente as notas dele; se nao existir nenhuma, `CreateAndShowNoteAsync` gera a nota inicial.

## Conceitos principais
- **Autor atual (`current_author_id`)**: quem usa o PC. Define `created_by_user_id` em novas notas.
- **Destinatario atual (`current_user_id`)**: pessoa padrao para quem notas serao criadas (`source_user_id`). O botao "Definir ativo" so atualiza essa preferencia.
- **Grupos**: as notas pertencem a `group_id`. Trocar o grupo fecha todas as janelas e recria apenas as notas do novo grupo.
- **Soft delete**: excluir nota marca `deleted = 1` e fecha a janela.

## Conteudo das notas
- O `RichTextBox` trabalha com `FlowDocument`. Salvamos tudo como XAML via `XamlWriter.Save` e restauramos via `XamlReader.Load`.
- Imagens coladas sao copiadas para `%LOCALAPPDATA%/StickyCutie/images/<uuid>.png`, registradas na tabela `note_images` e apontadas com `file:///...` dentro do XAML.
- O menu contextual da nota identifica se o clique ocorreu sobre uma imagem e exibe "Excluir imagem" dinamicamente.

## Criação de notas
- O botão `+` (`MainWindow.xaml`) abre `CreateNoteChoiceWindow.xaml`. Opções:
  - **Pessoal (para mim)** → chama `App.CreatePersonalNoteAsync()`; antes de criar a nota o app abre `CreateNoteTitleWindow.xaml` para você definir o título e depois a nota nasce imediatamente com autor e destinatário iguais ao usuário atual.
  - **Outro usuário do grupo** → abre `CreateNoteAdvancedWindow.xaml`, preenchido com os usuários retornados por `DatabaseService.GetUsersAsync()`; ali o usuário escolhe o destinatário, título, texto inicial e (opcionalmente) data/hora do alarme.
- `App.CreateNoteForRecipientAsync()` grava o título, gera o `FlowDocument` inicial (via `NoteDocumentBuilder`), persiste a nota em `notes_local` e, caso exista alarme, insere em `alarms_local`.
- Toda nota criada localmente continua abrindo no WPF para edição imediata; em fases futuras o backend FastAPI replicará notas destinadas a outras pessoas.

## Alarmes
- Cada nota possui um botao de alarme na toolbar. Ele abre `AlarmEditorWindow` para escolher data e hora.
- Quando o alarme dispara, a propria nota treme e exibe um mini modal com **Adiar**/**Parar**; se ela nao estiver aberta, o popup global continua funcionando.
- Os dados ficam em `alarms_local` (`alarm_at`, `snooze_until`, `is_enabled`, `created_at`, `updated_at`).
- `AlarmManager` roda com um `DispatcherTimer`, varre alarmes vencidos e controla o popup inline (ou global). O adiar reabre `AlarmSnoozeWindow` direto do post-it.
- Som: por padrao usamos o alerta do Windows; na aba “Alarmes” das Configuracoes voce escolhe o arquivo (a pasta `%LOCALAPPDATA%/StickyCutie/alarms` abre automaticamente e o arquivo selecionado fica salvo ali).

## Configuracoes globais
- `SettingsWindow` possui abas de Grupos e Usuarios. Apenas admins veem a aba de grupos; a aba de usuarios permite criar/editar/demitir admins e definir o destinatario padrao.
- O botao "Fechar" encerra o modal. Fechar a janela sempre invalida a sessao admin.
- A aba **Alarmes** permite abrir a pasta `%LOCALAPPDATA%/StickyCutie/alarms` e selecionar o arquivo de áudio usado nos alertas.
- A aba **Notas** lista todas as notas do grupo ativo (autor, destinatário, data). É possível atualizar e excluir diretamente dali (a exclusão usa o SoftDelete e fecha a janela correspondente se estiver aberta).
- O menu contextual do RichTextBox tem a opção “Excluir nota”, que dispara o mesmo fluxo do botão no menu lateral.

## Como executar
```
run-stickycutie.bat
```
O script mata `StickyCutie.Wpf.exe` se estiver aberto e roda o projeto WPF em modo Debug.

## Proximos passos sugeridos
- Empacotar (MSIX/WinGet) para distribuicao.
- Backend FastAPI com login real, sync e notificacoes push.
- UI para escolher o destinatario direto na toolbar da nota.


