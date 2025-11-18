from typing import List, Optional

from pydantic import BaseModel


class SyncSendRequest(BaseModel):
    id: str
    title: Optional[str] = None
    content: str
    updated_at: int
    target_user_id: str
    created_by_user_id: str
    group_id: str
    deleted: bool = False


class RemoteNote(BaseModel):
    id: str
    title: Optional[str] = None
    content: str
    updated_at: int
    created_by_user_id: str
    target_user_id: str
    group_id: str
    deleted: bool = False


class SyncEventResponse(BaseModel):
    event_id: str
    note: RemoteNote


class AckRequest(BaseModel):
    event_ids: List[str]
