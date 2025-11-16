import os
from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker, declarative_base
from dotenv import load_dotenv

# Carrega o .env da raiz
load_dotenv()

DATABASE_URL = os.getenv("DATABASE_URL")

# Cria o engine
engine = create_engine(
    DATABASE_URL,
    pool_pre_ping=True,
)

# Sessão do banco
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)

# Base para os modelos
Base = declarative_base()

# Dependency para usar nas rotas
def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()

