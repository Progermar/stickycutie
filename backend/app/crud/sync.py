from datetime import datetime
from typing import Iterable, List

from sqlalchemy.orm import Session

from app import models


def upsert_note(db: Session, note_id: int, user_id: int, group_id: int, content: str, created_at: datetime) -> models.Note:
    note = db.query(models.Note).filter(models.Note.id == note_id).first()
    if note is None:
        note = models.Note(
            id=note_id,
            group_id=group_id,
            created_by_user_id=user_id,
            source_user_id=user_id,
            title=None,
            content=content,
            geometry=None,
            alarm_at=None,
            snooze_until=None,
            deleted=False,
            updated_at=created_at,
        )
        db.add(note)
    else:
        note.group_id = group_id
        note.created_by_user_id = user_id
        note.source_user_id = user_id
        note.content = content
        note.updated_at = created_at
    db.flush()
    return note


def create_sync_event(db: Session, note_id: int, user_id: int, created_at: datetime) -> models.SyncEvent:
    event = models.SyncEvent(
        note_id=note_id,
        user_id=user_id,
        event_type="note",
        updated_at=created_at,
    )
    db.add(event)
    db.commit()
    db.refresh(event)
    return event


def get_events_since(db: Session, since: datetime) -> List[models.SyncEvent]:
    return (
        db.query(models.SyncEvent)
        .filter(models.SyncEvent.updated_at > since)
        .order_by(models.SyncEvent.updated_at.asc())
        .all()
    )


def delete_events(db: Session, event_ids: Iterable[int]) -> int:
    if not event_ids:
        return 0
    deleted = (
        db.query(models.SyncEvent)
        .filter(models.SyncEvent.id.in_(list(event_ids)))
        .delete(synchronize_session=False)
    )
    db.commit()
    return deleted
