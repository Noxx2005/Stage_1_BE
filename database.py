"""
Database configuration and models for Insighta Labs+
"""

import time
import uuid
from datetime import datetime, timezone

from sqlalchemy import (
    Column, String, Float, Integer, DateTime, Boolean, ForeignKey, 
    create_engine, event, UniqueConstraint
)
from sqlalchemy.orm import declarative_base, sessionmaker, relationship

# Database setup
DATABASE_URL = "sqlite:///./profiles.db"
engine = create_engine(DATABASE_URL, connect_args={"check_same_thread": False})
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)
Base = declarative_base()


def generate_uuid_v7() -> str:
    """Generate UUID v7 (time-ordered)"""
    timestamp_ms = int(time.time() * 1000)
    
    # UUID v7 format: unix_ts_ms (48 bits) | ver (4 bits) | rand_a (12 bits) | var (2 bits) | rand_b (62 bits)
    timestamp_bytes = timestamp_ms.to_bytes(6, 'big')
    random_bytes = uuid.uuid4().bytes
    
    # Combine: first 6 bytes are timestamp, remaining 10 bytes include version and random
    uuid_bytes = timestamp_bytes + random_bytes[6:]
    
    # Set version (7) in bits 48-51
    uuid_bytes = uuid_bytes[:6] + bytes([0x70 | (uuid_bytes[6] & 0x0f)]) + uuid_bytes[7:]
    
    # Set variant (10) in bits 64-65
    uuid_bytes = uuid_bytes[:8] + bytes([0x80 | (uuid_bytes[8] & 0x3f)]) + uuid_bytes[9:]
    
    return str(uuid.UUID(bytes=uuid_bytes))


# Existing Profile Model
class ProfileDB(Base):
    __tablename__ = "profiles"
    
    id = Column(String, primary_key=True, index=True)
    name = Column(String, unique=True, index=True, nullable=False)
    gender = Column(String, nullable=False)
    gender_probability = Column(Float, nullable=False)
    sample_size = Column(Integer, nullable=False)
    age = Column(Integer, nullable=False)
    age_group = Column(String, nullable=False)
    country_id = Column(String, nullable=False)
    country_name = Column(String, nullable=False)
    country_probability = Column(Float, nullable=False)
    created_at = Column(DateTime, nullable=False)
    
    # Foreign key to user who created the profile
    created_by = Column(String, ForeignKey("users.id"), nullable=True)


# User Model
class UserDB(Base):
    __tablename__ = "users"
    
    id = Column(String, primary_key=True, index=True)
    github_id = Column(String, unique=True, index=True, nullable=False)
    username = Column(String, index=True, nullable=False)
    email = Column(String, nullable=True)
    avatar_url = Column(String, nullable=True)
    role = Column(String, default="analyst", nullable=False)  # admin or analyst
    is_active = Column(Boolean, default=True, nullable=False)
    last_login_at = Column(DateTime, nullable=True)
    created_at = Column(DateTime, default=lambda: datetime.now(timezone.utc), nullable=False)
    
    # Relationships
    profiles = relationship("ProfileDB", backref="creator")
    refresh_tokens = relationship("RefreshTokenDB", backref="user", cascade="all, delete-orphan")


# Refresh Token Model for token rotation
class RefreshTokenDB(Base):
    __tablename__ = "refresh_tokens"
    
    id = Column(String, primary_key=True, index=True)
    token_hash = Column(String, unique=True, index=True, nullable=False)
    user_id = Column(String, ForeignKey("users.id"), nullable=False)
    expires_at = Column(DateTime, nullable=False)
    created_at = Column(DateTime, default=lambda: datetime.now(timezone.utc), nullable=False)
    revoked_at = Column(DateTime, nullable=True)
    device_info = Column(String, nullable=True)  # Optional: store device/client info


def init_db():
    """Initialize database with all tables"""
    Base.metadata.create_all(bind=engine)


def get_db():
    """Database dependency"""
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()
