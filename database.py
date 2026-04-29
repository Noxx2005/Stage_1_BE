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
    """
    Generate UUID v7 (time-ordered) per RFC 9562.
    Format: unix_ts_ms (48 bits) | ver (4 bits) | rand_a (12 bits) | var (2 bits) | rand_b (62 bits)
    """
    timestamp_ms = int(time.time() * 1000)
    
    # Get 48-bit timestamp
    timestamp_bytes = timestamp_ms.to_bytes(6, 'big')
    
    # Generate random bytes for the rest
    random_bytes = uuid.uuid4().bytes
    
    # Build the UUID bytes
    uuid_bytes = bytearray(16)
    
    # First 6 bytes: timestamp
    uuid_bytes[0:6] = timestamp_bytes
    
    # Byte 6: version (7) in high nibble + 4 bits of random
    uuid_bytes[6] = 0x70 | (random_bytes[6] & 0x0f)
    
    # Byte 7: random
    uuid_bytes[7] = random_bytes[7]
    
    # Byte 8: variant (10) in high 2 bits + 6 bits of random
    uuid_bytes[8] = 0x80 | (random_bytes[8] & 0x3f)
    
    # Bytes 9-15: random
    uuid_bytes[9:16] = random_bytes[9:16]
    
    return str(uuid.UUID(bytes=bytes(uuid_bytes)))


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
