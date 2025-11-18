import secrets
from datetime import datetime, timedelta
from typing import List

from sqlalchemy.orm import Session

from app import models


def _generate_token() -> str:
    raw = secrets.token_hex(3).upper()
    return f"{raw[:3]}-{raw[3:]}"


def create_invitation(
    db: Session,
    group_id: int,
    created_by_user_id: int | None,
    email: str | None,
    expires_in_days: int,
) -> models.GroupInvitation:
    expires_at = datetime.utcnow() + timedelta(days=max(1, expires_in_days))
    invitation = models.GroupInvitation(
        group_id=group_id,
        email=email.lower() if email else None,
        token=_generate_token(),
        status="pending",
        expires_at=expires_at,
        created_by_user_id=created_by_user_id,
    )
    db.add(invitation)
    db.commit()
    db.refresh(invitation)
    return invitation


def list_invitations(db: Session, group_id: int) -> List[models.GroupInvitation]:
    return (
        db.query(models.GroupInvitation)
        .filter(models.GroupInvitation.group_id == group_id)
        .order_by(models.GroupInvitation.created_at.desc())
        .all()
    )


def get_by_token(db: Session, token: str) -> models.GroupInvitation | None:
    return (
        db.query(models.GroupInvitation)
        .filter(models.GroupInvitation.token == token.upper())
        .first()
    )


def revoke_invitation(db: Session, invitation: models.GroupInvitation) -> None:
    invitation.status = "revoked"
    invitation.updated_at = datetime.utcnow()
    db.commit()


def mark_accepted(db: Session, invitation: models.GroupInvitation) -> None:
    invitation.status = "accepted"
    invitation.updated_at = datetime.utcnow()
    db.commit()
