from sqlalchemy.orm import Session
from app import models


def list_users_by_group(db: Session, group_id: int):
    return (
        db.query(models.User)
        .filter(models.User.group_id == group_id)
        .order_by(models.User.created_at.asc())
        .all()
    )


def get_user(db: Session, user_id: int) -> models.User | None:
    return db.query(models.User).filter(models.User.id == user_id).first()


def delete_user(db: Session, user: models.User):
    db.delete(user)
    db.commit()


def save_user(db: Session, user: models.User):
    db.add(user)
    db.commit()
    db.refresh(user)
    return user
