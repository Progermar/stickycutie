from datetime import datetime, timezone
from typing import Iterable, List, Optional

from sqlalchemy.orm import Session

from app import models
from app.schemas.sync import SyncSendRequest


def _to_int(value: Optional[str]) -> Optional[int]:
    if value is None:
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def _to_datetime(timestamp: int) -> datetime:
    return datetime.fromtimestamp(timestamp, tz=timezone.utc)


def upsert_note(db: Session, payload: SyncSendRequest) -> models.Note:
    note = (
        db.query(models.Note)
        .filter(models.Note.geometry == payload.id)
        .one_or_none()
    )
    if note is None:
        note = models.Note(
            geometry=payload.id,
        )
        db.add(note)

    note.title = payload.title
    note.content = payload.content
    note.deleted = payload.deleted
    note.group_id = _to_int(payload.group_id)
    note.created_by_user_id = _to_int(payload.created_by_user_id)
    note.source_user_id = _to_int(payload.target_user_id)
    note.updated_at = _to_datetime(payload.updated_at)
    db.flush()
    return note


def create_sync_event(db: Session, note: models.Note, payload: SyncSendRequest) -> models.SyncEvent:
    event = models.SyncEvent(
        note_id=note.id,
        user_id=_to_int(payload.created_by_user_id),
        event_type="note",
        updated_at=_to_datetime(payload.updated_at),
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


def delete_events(db: Session, event_ids: Iterable[str]) -> int:
    ids: List[int] = []
    for eid in event_ids:
        try:
            ids.append(int(eid))
        except (TypeError, ValueError):
            continue
    if not ids:
        return 0

    deleted = (
        db.query(models.SyncEvent)
        .filter(models.SyncEvent.id.in_(ids))
        .delete(synchronize_session=False)
    )
    db.commit()
    return deleted
