from logging.config import fileConfig
import os
from sqlalchemy import engine_from_config
from sqlalchemy import pool
from alembic import context

# Carregar .env
from dotenv import load_dotenv
load_dotenv()

# Database URL do .env
DATABASE_URL = os.getenv("DATABASE_URL")

# Config Alembic
config = context.config
fileConfig(config.config_file_name)

# Importa models
from app.models import Base

# Define metadata
target_metadata = Base.metadata

# Seta URL do banco dinamicamente
config.set_main_option("sqlalchemy.url", DATABASE_URL)


def run_migrations_offline():
    """Resgata URL e gera SQL sem conexão real."""
    url = config.get_main_option("sqlalchemy.url")
    context.configure(
        url=url,
        target_metadata=target_metadata,
        literal_binds=True
    )

    with context.begin_transaction():
        context.run_migrations()


def run_migrations_online():
    """Conecta no banco e aplica migrations."""
    connectable = engine_from_config(
        config.get_section(config.config_ini_section),
        prefix="sqlalchemy."
    )

    with connectable.connect() as connection:
        context.configure(
            connection=connection,
            target_metadata=target_metadata
        )

        with context.begin_transaction():
            context.run_migrations()


# Decide qual função usar
if context.is_offline_mode():
    run_migrations_offline()
else:
    run_migrations_online()

