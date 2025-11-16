# Instruções para Codex — StickyCutie API

## O que o Codex PODE fazer
- Criar endpoints novos conforme documentação
- Manipular models e schemas
- Criar migrations automaticamente
- Adicionar lógica em routes ou services

## O que o Codex NÃO PODE FAZER
- Alterar estrutura já definida sem autorização
- Remover tabelas, índices ou colunas existentes
- Modificar alembic.ini
- Alterar o DATABASE_URL no .env
- Criar migrations destrutivas
- Mudar nomes de pastas

## Orientação Geral
1. Sempre seguir a documentação da pasta docs-backend.
2. Toda mudança deve gerar migration via Alembic.
3. Código deve ser modular (auth/, routes/, schemas/, crud/).
4. Jamais sobrescrever arquivos inteiros sem necessidade.
