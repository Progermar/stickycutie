from datetime import datetime
from typing import List

from pydantic import BaseModel


class SyncSendRequest(BaseModel):
    note_id: int
    user_id: int
    group_id: int
    content: str
    created_at: datetime


class RemoteNote(BaseModel):
    note_id: int
    user_id: int
    group_id: int
    content: str
    created_at: datetime


class SyncEventResponse(BaseModel):
    event_id: int
    note: RemoteNote


class AckRequest(BaseModel):
    event_ids: List[int]
