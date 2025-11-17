from datetime import datetime
from pydantic import BaseModel, EmailStr


class UserRegister(BaseModel):
    group_id: int
    name: str
    email: EmailStr
    phone: str | None = None
    password: str
    is_admin: bool = False


class UserResponse(BaseModel):
    id: int
    group_id: int | None
    name: str
    email: EmailStr
    phone: str | None
    is_admin: bool
    created_at: datetime
    updated_at: datetime

    class Config:
        orm_mode = True


class UserRegisterResponse(BaseModel):
    id: int
    group_id: int
    email: EmailStr
    access_token: str
    token_type: str = "bearer"


class UserUpdate(BaseModel):
    name: str | None = None
    email: EmailStr | None = None
    phone: str | None = None
    is_admin: bool | None = None
    password: str | None = None
