import secrets
from datetime import datetime

from fastapi import APIRouter, Depends, HTTPException, Path, status
from sqlalchemy.orm import Session

from app import models
from app.core.security import create_access_token, hash_password
from app.crud import invitations as invitations_crud
from app.database import get_db
from app.schemas.groups import GroupResponse
from app.schemas.invitations import (
    GroupInviteResponse,
    InviteAcceptRequest,
    InviteAcceptResponse,
    InviteCreateRequest,
    InvitePreviewResponse,
)
from app.schemas.users import UserResponse


router = APIRouter(prefix="/groups", tags=["invitations"])


def _ensure_group(db: Session, group_id: int) -> models.Group:
    group = db.query(models.Group).filter(models.Group.id == group_id).first()
    if not group:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Group not found")
    return group


@router.post("/{group_id}/invite", response_model=GroupInviteResponse)
def create_invite(
    group_id: int,
    payload: InviteCreateRequest,
    db: Session = Depends(get_db),
):
    _ensure_group(db, group_id)
    invitation = invitations_crud.create_invitation(
        db=db,
        group_id=group_id,
        created_by_user_id=payload.created_by_user_id,
        email=payload.email,
        expires_in_days=payload.expires_in_days,
    )
    return GroupInviteResponse(
        token=invitation.token,
        email=invitation.email,
        status=invitation.status,
        expires_at=invitation.expires_at,
        created_by_user_id=invitation.created_by_user_id,
        group_id=invitation.group_id,
    )


@router.get("/{group_id}/invitations", response_model=list[GroupInviteResponse])
def list_invites(group_id: int, db: Session = Depends(get_db)):
    _ensure_group(db, group_id)
    invitations = invitations_crud.list_invitations(db, group_id)
    return [
        GroupInviteResponse(
            token=inv.token,
            email=inv.email,
            status=inv.status,
            expires_at=inv.expires_at,
            created_by_user_id=inv.created_by_user_id,
            group_id=inv.group_id,
        )
        for inv in invitations
    ]


@router.delete("/invitations/{token}", status_code=status.HTTP_204_NO_CONTENT)
def revoke_invite(token: str = Path(..., description="Token do convite"), db: Session = Depends(get_db)):
    invitation = invitations_crud.get_by_token(db, token)
    if not invitation:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Invitation not found")
    invitations_crud.revoke_invitation(db, invitation)
    return {"status": "revoked"}


@router.get("/invitations/{token}", response_model=InvitePreviewResponse)
def preview_invite(token: str, db: Session = Depends(get_db)):
    invitation = invitations_crud.get_by_token(db, token)
    if not invitation:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Invitation not found")
    group = _ensure_group(db, invitation.group_id)
    return InvitePreviewResponse(
        group_id=group.id,
        group_name=group.name,
        status=invitation.status,
        expires_at=invitation.expires_at,
    )


@router.post("/invitations/{token}/accept", response_model=InviteAcceptResponse)
def accept_invite(token: str, payload: InviteAcceptRequest, db: Session = Depends(get_db)):
    invitation = invitations_crud.get_by_token(db, token)
    if not invitation:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Invitation not found")
    if invitation.status != "pending":
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Invitation is not pending")
    if invitation.expires_at and invitation.expires_at < datetime.utcnow():
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Invitation expired")

    existing = db.query(models.User).filter(models.User.email == payload.email.lower()).first()
    if existing:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Email already registered")

    group = _ensure_group(db, invitation.group_id)
    password_hash = hash_password(secrets.token_hex(8))
    user = models.User(
        name=payload.name,
        email=payload.email.lower(),
        phone=payload.phone,
        password_hash=password_hash,
        is_admin=False,
        group_id=group.id,
    )
    db.add(user)
    db.commit()
    db.refresh(user)

    invitations_crud.mark_accepted(db, invitation)

    access_token = create_access_token({"sub": str(user.id), "email": user.email})
    return InviteAcceptResponse(
        group=GroupResponse.from_orm(group),
        user=UserResponse.from_orm(user),
        access_token=access_token,
    )
