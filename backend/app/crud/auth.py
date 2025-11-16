from sqlalchemy.orm import Session

from app import models


def get_user_by_email(db: Session, email: str) -> models.User | None:
    return db.query(models.User).filter(models.User.email == email).first()


def get_first_group_id(db: Session, user_id: int) -> int | None:
    membership = (
        db.query(models.GroupMember)
        .filter(models.GroupMember.user_id == user_id)
        .order_by(models.GroupMember.id.asc())
        .first()
    )
    if membership:
        return membership.group_id
    return None
