from datetime import datetime

from fastapi import APIRouter, Depends, Query, status
from sqlalchemy.orm import Session

from app.database import get_db
from app.crud import sync as sync_crud
from app import models
from app.schemas.sync import (
    AckRequest,
    RemoteNote,
    SyncEventResponse,
    SyncSendRequest,
)

router = APIRouter(prefix="/sync", tags=["sync"])


@router.post("/send", status_code=status.HTTP_201_CREATED)
def send_note(payload: SyncSendRequest, db: Session = Depends(get_db)):
    note = sync_crud.upsert_note(
        db=db,
        note_id=payload.note_id,
        user_id=payload.user_id,
        group_id=payload.group_id,
        content=payload.content,
        created_at=payload.created_at,
    )
    event = sync_crud.create_sync_event(
        db=db,
        note_id=note.id,
        user_id=payload.user_id,
        created_at=payload.created_at,
    )
    return {"event_id": event.id}


@router.get("/updates", response_model=list[SyncEventResponse])
def get_updates(
    since: datetime = Query(..., description="Retornar eventos após essa data/hora"),
    db: Session = Depends(get_db),
):
    events = sync_crud.get_events_since(db, since)
    responses: list[SyncEventResponse] = []
    for event in events:
        note = db.query(models.Note).filter(models.Note.id == event.note_id).first()
        if not note:
            continue
        responses.append(
            SyncEventResponse(
                event_id=event.id,
                note=RemoteNote(
                    note_id=note.id,
                    user_id=event.user_id,
                    group_id=note.group_id or 0,
                    content=note.content or "",
                    created_at=event.updated_at,
                ),
            )
        )
    return responses


@router.post("/ack")
def acknowledge(payload: AckRequest, db: Session = Depends(get_db)):
    removed = sync_crud.delete_events(db, payload.event_ids)
    return {"deleted": removed}
