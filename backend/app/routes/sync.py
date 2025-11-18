from datetime import datetime, timezone

from fastapi import APIRouter, Depends, Query, status
from sqlalchemy.orm import Session

from app.database import get_db
from app.crud import sync as sync_crud
from app.schemas.sync import (
    AckRequest,
    RemoteNote,
    SyncEventResponse,
    SyncSendRequest,
)
from app import models

router = APIRouter(prefix="/sync", tags=["sync"])


@router.post("/send", status_code=status.HTTP_201_CREATED)
def send_note(payload: SyncSendRequest, db: Session = Depends(get_db)):
    note = sync_crud.upsert_note(db=db, payload=payload)
    event = sync_crud.create_sync_event(db=db, note=note, payload=payload)
    return {"event_id": event.id}


@router.get("/updates", response_model=list[SyncEventResponse])
def get_updates(
    since: float = Query(..., description="Timestamp UNIX para filtrar eventos"),
    db: Session = Depends(get_db),
):
    since_dt = datetime.fromtimestamp(since, tz=timezone.utc)
    events = sync_crud.get_events_since(db, since_dt)
    responses: list[SyncEventResponse] = []
    for event in events:
        note = db.query(models.Note).filter(models.Note.id == event.note_id).first()
        if not note:
            continue
        responses.append(
            SyncEventResponse(
                event_id=str(event.id),
                note=RemoteNote(
                    id=note.geometry or str(note.id),
                    title=note.title,
                    content=note.content or "",
                    updated_at=int(event.updated_at.timestamp()),
                    deleted=note.deleted,
                    created_by_user_id=str(note.created_by_user_id or ""),
                    target_user_id=str(note.source_user_id or ""),
                    group_id=str(note.group_id or ""),
                ),
            )
        )
    return responses


@router.post("/ack")
def acknowledge(payload: AckRequest, db: Session = Depends(get_db)):
    removed = sync_crud.delete_events(db, payload.event_ids)
    return {"deleted": removed}
