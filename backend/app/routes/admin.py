from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session

from app.database import get_db
from app.crud import admin as admin_crud


router = APIRouter(prefix="/admin", tags=["admin"])


@router.post("/reset")
def reset_system(db: Session = Depends(get_db)):
    admin_crud.reset_all(db)
    return {"status": "reset_complete"}
