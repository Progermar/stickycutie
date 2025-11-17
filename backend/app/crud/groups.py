from sqlalchemy.orm import Session
from app import models


def create_group(db: Session, name: str, description: str | None = None) -> models.Group:
    group = models.Group(name=name, description=description)
    db.add(group)
    db.commit()
    db.refresh(group)
    return group


def list_groups(db: Session) -> list[models.Group]:
    return db.query(models.Group).order_by(models.Group.created_at.asc()).all()


def get_group(db: Session, group_id: int) -> models.Group | None:
    return db.query(models.Group).filter(models.Group.id == group_id).first()
