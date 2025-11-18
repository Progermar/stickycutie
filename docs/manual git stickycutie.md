Atualizar o Backend StickyCutie da VPS
✅ 1. Atualizar no PC

Sempre que fizer alterações no backend:

git add .
git commit -m "update"
git push


Isso envia o código novo para o GitHub.

✅ 2. Atualizar na VPS

Entre na pasta do backend:

cd /opt/stickycutie_api/backend


Baixe as atualizações:

git pull


Reinicie o serviço para carregar o código novo:

systemctl restart stickycutie_api

✅ 3. Testar se está rodando

Testar localmente na VPS:

curl http://127.0.0.1:8000/docs


Testar um endpoint real:

curl https://stickycutie.dexaweb.com.br/users/by-group/1


Se responder → atualização aplicada com sucesso.

🧩 Fluxo completo (resumo de bolso)
# NO PC
git add .
git commit -m "update"
git push

# NA VPS
cd /opt/stickycutie_api/backend
git pull
systemctl restart stickycutie_api

🔥 Fim.

Um manual rápido, limpo e eficiente — igual você gosta.

Se quiser, faço uma versão em PDF, Markdown ou estilo README para colocar no GitHub.