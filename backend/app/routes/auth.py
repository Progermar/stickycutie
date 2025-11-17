from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.orm import Session

from app.database import get_db
from app.schemas.auth import LoginRequest, LoginResponse, UserInfo
from app.core.security import verify_password, create_access_token
from app.crud import auth as auth_crud

router = APIRouter(tags=["auth"])


@router.post("/login", response_model=LoginResponse)
def login(payload: LoginRequest, db: Session = Depends(get_db)):
    user = auth_crud.get_user_by_email(db, payload.email.lower())
    if not user or not verify_password(payload.password, user.password_hash):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid credentials",
        )

    token = create_access_token({"sub": str(user.id), "email": user.email})

    return LoginResponse(
        access_token=token,
        user=UserInfo(
            id=user.id,
            name=user.name,
            email=user.email,
            group_id=user.group_id,
        ),
    )
