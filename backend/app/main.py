from fastapi import FastAPI

from app.routes import auth, groups, users, admin

app = FastAPI()

app.include_router(auth.router, prefix="/auth")
app.include_router(groups.router)
app.include_router(users.router)
app.include_router(admin.router)


@app.get("/")
def root():
    return {"status": "stickycutie api ok"}
