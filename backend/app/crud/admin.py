from sqlalchemy.orm import Session
from sqlalchemy import text


def reset_all(db: Session) -> None:
    """
    Remove all records from sync_events, notes, users and groups.
    Order matters due to foreign keys.
    """
    statements = [
        "DELETE FROM sync_events;",
        "DELETE FROM notes;",
        "DELETE FROM users;",
        "DELETE FROM groups;",
    ]

    for stmt in statements:
        db.execute(text(stmt))

    db.commit()
