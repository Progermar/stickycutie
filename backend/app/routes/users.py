from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.orm import Session

from app.database import get_db
from app.schemas.users import (
    UserRegister,
    UserRegisterResponse,
    UserResponse,
    UserUpdate,
)
from app.core.security import hash_password, create_access_token
from app.crud import users as users_crud, groups as groups_crud, auth as auth_crud
from app import models

router = APIRouter(prefix="/users", tags=["users"])


@router.post("/register", response_model=UserRegisterResponse, status_code=status.HTTP_201_CREATED)
def register_user(payload: UserRegister, db: Session = Depends(get_db)):
    group = groups_crud.get_group(db, payload.group_id)
    if not group:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Group not found")

    if auth_crud.get_user_by_email(db, payload.email.lower()):
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Email already registered")

    user = models.User(
        name=payload.name,
        email=payload.email.lower(),
        phone=payload.phone,
        password_hash=hash_password(payload.password),
        is_admin=payload.is_admin,
        group_id=payload.group_id,
    )
    users_crud.save_user(db, user)

    token = create_access_token({"sub": str(user.id), "email": user.email})
    return UserRegisterResponse(
        id=user.id,
        group_id=user.group_id,
        email=user.email,
        access_token=token,
    )


@router.get("/by-group/{group_id}", response_model=list[UserResponse])
def users_by_group(group_id: int, db: Session = Depends(get_db)):
    return users_crud.list_users_by_group(db, group_id)


@router.put("/{user_id}", response_model=UserResponse)
def update_user(user_id: int, payload: UserUpdate, db: Session = Depends(get_db)):
    user = users_crud.get_user(db, user_id)
    if not user:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="User not found")

    if payload.name is not None:
        user.name = payload.name
    if payload.email is not None:
        existing = auth_crud.get_user_by_email(db, payload.email.lower())
        if existing and existing.id != user_id:
            raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Email already in use")
        user.email = payload.email.lower()
    if payload.phone is not None:
        user.phone = payload.phone
    if payload.is_admin is not None:
        user.is_admin = payload.is_admin
    if payload.password:
        user.password_hash = hash_password(payload.password)

    users_crud.save_user(db, user)
    return user


@router.delete("/{user_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_user(user_id: int, db: Session = Depends(get_db)):
    user = users_crud.get_user(db, user_id)
    if not user:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="User not found")
    users_crud.delete_user(db, user)
    return {"status": "deleted"}
