from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.orm import Session

from app.database import get_db
from app.schemas.groups import GroupCreate, GroupResponse
from app.crud import groups as groups_crud

router = APIRouter(prefix="/groups", tags=["groups"])


@router.post("/create", response_model=GroupResponse, status_code=status.HTTP_201_CREATED)
def create_group(payload: GroupCreate, db: Session = Depends(get_db)):
    group = groups_crud.create_group(db, payload.name, payload.description)
    return group


@router.get("/list", response_model=list[GroupResponse])
def list_groups(db: Session = Depends(get_db)):
    return groups_crud.list_groups(db)
