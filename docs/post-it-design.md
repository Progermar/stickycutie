# Post-it – Design & UX

Documenta o layout atual do `MainWindow.xaml` e como cada parte foi implementada. Todas as referências estão no projeto `clients/wpf/StickyCutie.Wpf`.

## Estrutura geral

| Região | Descrição | Referência |
| --- | --- | --- |
| Janela sem borda | `WindowStyle=None`, `AllowsTransparency=True`, `RootBorder` com `Margin=-1` para eliminar o "filete" branco. | `MainWindow.xaml` linhas 1–42 |
| Área de texto | `RichTextBox` com margem superior de 24 px (acomoda os botões). Eventos controlam arraste e abertura da toolbar. | `MainWindow.xaml` linhas 43–99 e `MainWindow.xaml.cs` linhas 90–160 |
| Barra superior | Botões `+` e `x` desenhados em XAML simples; qualquer clique na grade superior permite arrastar. | `MainWindow.xaml` linhas 64–99 |
| Toolbar inferior | `Grid` + `StackPanel` contendo todas as ferramentas; a barra aparece quando há seleção no texto ou quando o mouse passa sobre a região inferior. | `MainWindow.xaml` linhas 100–235 |
| Menu lateral (SC) | Botão final abre popup com as cores oficiais do Post-it, opção futura de lista e exclusão de nota. | `MainWindow.xaml` linhas 235–318 + `MainWindow.xaml.cs` linhas 170–219 |
| Modal/Overlay de bloqueio | `LockOverlay` + `PasswordModal` para definir/desbloquear senhas. | `MainWindow.xaml` linhas 318–378 e `MainWindow.xaml.cs` linhas 220–340 |

## Toolbar & ícones

Os ícones são SVGs em `Assets/icons`. O controle `controls:SvgIcon` faz o parse do SVG e gera `Path/Line/Ellipse`. A toolbar usa margens pequenas (`Margin="2,0"`) para replicar a densidade do Sticky Notes.

| Botão | Ação |
| --- | --- |
| `B` | `EditingCommands.ToggleBold`.
| Balão (paleta) | Mostra o popup de marca-texto; aplica cor apenas no clique.
| Lista | Executa `ListButton_Click` que garante parágrafo e chama `ToggleBullets`.
| Imagem | Abre `OpenFileDialog`, insere `InlineUIContainer` com largura limitada e menu próprio.
| Alarme | Placeholder (`MessageBox`).
| Copiar/Colar/Desfazer/Refazer | `ApplicationCommands.*` ligados ao `RichTextBox`.
| Dup/Clipboard | Reservado para futuras ações (mesma UI do copiar).
| Pino | Alterna `_pinOn`, ajusta opacidade/borda e evidencia o botão com fundo dourado.
| Cadeado | Abre o fluxo de senha (detecta automaticamente se não há senha).
| Menu (SC) | Abre popup lateral com paleta tradicional (amarelo/verde/rosa/lilás/azul/cinza) + “Excluir anotação”.

## Paleta de marca-texto

- `TextColorPopup` abre ao passar o mouse sobre o ícone da paleta.
- O usuário precisa clicar para aplicar a cor (hover não altera texto).
- Os swatches usam `HighlightSwatchStyle`, sem estados visuais extras para não confundir.

## Context menu inteligente

`NoteRichText.ContextMenu` é reconstruído em tempo real (`BuildContextMenu`). O comportamento:

1. **Imagem sob o cursor:** o menu oferece apenas *Excluir imagem* (remove o `InlineUIContainer`).
2. **Seleção de texto:** mostra Copiar/Recortar, Colar (habilita apenas se o clipboard tem conteúdo), Desfazer/Refazer + Nova nota/Fechar.
3. **Sem seleção:** apenas colar/desfazer/refazer + opções de nota.

## Modal de senha

- `LockButton_Click` decide entre `ShowCreatePasswordModal` (não há senha), `ShowUnlockModal` (senha existente) ou reabertura do modal quando a nota está bloqueada.
- `PasswordConfirm_Click` trabalha nos modos `set` e `unlock` usando hash SHA-256 (`Hash`).
- `PasswordRemove_Click` exige a senha atual antes de remover.

## Inserção e remoção de imagens

- `ImageButton_Click` abre seletor, limita largura em 220 px e injeta a imagem onde o cursor estiver.
- Cada imagem recebe um menu próprio (botão direito) com *Excluir imagem*.
- O menu da nota também habilita *Excluir imagem* quando o clique direito ocorre sobre uma imagem.

## Scripts auxiliares

- `run-stickycutie.bat`: mata `StickyCutie.Wpf.exe` e roda o projeto via `dotnet run`. Evita erro de arquivo em uso durante o desenvolvimento.

## Próximos passos

1. Persistência (SQLite) das notas/senhas.
2. Alarmes/lista de anotações reais integrados ao menu lateral.
3. Testes automatizados de UI e captura visual para validar toolbar e menu contextual.
