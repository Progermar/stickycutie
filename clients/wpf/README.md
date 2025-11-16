# StickyCutie WPF Client

Cliente WPF para notas estilo widget. Integra com a API local do Tauri/Axum quando disponível, mas roda offline com fallback seguro.

## Tecnologias
- WPF (.NET 8, C#, XAML) — janela transparente, sem moldura e oculta do Alt‑Tab.
- Code‑behind (sem MVVM) — menu criado dinamicamente via `BuildNoteMenu()` para evitar falhas de recursos XAML.
- Integração opcional com backend Tauri (Rust + Axum HTTP) em `http://127.0.0.1:8081`.
- Persistência online via SQLite no backend Tauri (quando disponível); offline mantém nota padrão em memória.
- API HTTP/JSON (`/api/notes`) para criação, leitura e exclusão de notas.

## Requisitos
- `.NET SDK 8` ou superior instalado.
- (Opcional) Backend Tauri/Axum em `http://127.0.0.1:8081`. Se não estiver rodando, o cliente funciona offline e abre uma nota padrão.

## Estrutura
- `StickyCutie.Wpf.csproj`: projeto WPF.
- `App.xaml/.cs`: inicialização com fallback offline seguro.
- `StickyNoteWindow.xaml/.cs`: janela transparente, sem moldura, escondida do Alt‑Tab; menu criado dinamicamente.
- `Services/NotesService.cs`: cliente HTTP para a API (`/api/notes`).
- `Models`: tipos de dados alinhados ao backend.

## Executar
1. Abrir um terminal na pasta `clients/wpf/StickyCutie.Wpf`.
2. Compilar: `dotnet build`.
3. Executar: `dotnet run`.

Comportamento esperado:
- Sempre abre pelo menos uma nota. Se o backend não estiver disponível, abre uma nota offline padrão.
- Janela da nota é transparente, sem moldura e não aparece no Alt‑Tab.

Controles:
- Clique direito em qualquer parte da nota → abre o menu.
- Botão "⋯" no cabeçalho → abre o menu ancorado ao botão.
- Barras de rolagem finas (~8px), alargam levemente no hover/drag.

Backend (opcional):
- Se desejar integração online, inicie o Tauri com o servidor HTTP em `http://127.0.0.1:8081` (config padrão).

## Solução de Problemas
- Alerta de recurso XAML (ex.: `StaticResourceHolder`, `NoteMenu`, `ScrollThumbStyle`):
  - Rode `dotnet clean` e depois `dotnet build`/`dotnet run` para limpar artefatos de cache.
  - O menu da nota é criado dinamicamente no code‑behind (`BuildNoteMenu()`), não há `ContextMenu` estático no XAML.
  - O estilo `ScrollThumbStyle` está definido em `Window.Resources` de `StickyNoteWindow.xaml`.
- Se nada abrir: verifique se não há outra instância do app em execução e tente novamente.

## Changelog (WPF)
- 2025‑11‑12
  - Handler global de exceções ajustado: mostra erro detalhado e só abre fallback se nenhuma janela existir.
  - Removido `ContextMenu` estático (`NoteMenu`) do XAML; menu agora é criado sob demanda no code‑behind.
  - Adicionado estilo `ScrollThumbStyle` às barras de rolagem.
  - README atualizado com execução offline, controles e troubleshooting.