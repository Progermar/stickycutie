from datetime import datetime
from typing import Optional

from pydantic import BaseModel, EmailStr

from app.schemas.groups import GroupResponse
from app.schemas.users import UserResponse


class InviteCreateRequest(BaseModel):
    email: Optional[EmailStr] = None
    created_by_user_id: Optional[int] = None
    expires_in_days: int = 2


class GroupInviteResponse(BaseModel):
    token: str
    email: Optional[EmailStr]
    status: str
    expires_at: datetime
    created_by_user_id: Optional[int]
    group_id: int


class InvitePreviewResponse(BaseModel):
    group_id: int
    group_name: str
    status: str
    expires_at: datetime


class InviteAcceptRequest(BaseModel):
    name: str
    email: EmailStr
    phone: Optional[str] = None


class InviteAcceptResponse(BaseModel):
    group: GroupResponse
    user: UserResponse
    access_token: str
