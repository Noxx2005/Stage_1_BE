"""
JWT Token Handler for Insighta Labs+
Manages access tokens and refresh token rotation
"""

import os
import hashlib
import secrets
from datetime import datetime, timezone, timedelta
from typing import Optional, Tuple

from jose import JWTError, jwt
from fastapi import HTTPException, Request
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials
from sqlalchemy.orm import Session

from database import UserDB, RefreshTokenDB, generate_uuid_v7, SessionLocal

# JWT Configuration
SECRET_KEY = os.getenv("JWT_SECRET_KEY", secrets.token_urlsafe(32))
ALGORITHM = "HS256"

# Token expiry times
ACCESS_TOKEN_EXPIRE_MINUTES = 3  # 3 minutes
REFRESH_TOKEN_EXPIRE_MINUTES = 5  # 5 minutes


security = HTTPBearer()


def hash_token(token: str) -> str:
    """Hash a token for secure storage"""
    return hashlib.sha256(token.encode()).hexdigest()


def create_access_token(user_id: str, role: str) -> str:
    """Create a short-lived access token"""
    expire = datetime.now(timezone.utc) + timedelta(minutes=ACCESS_TOKEN_EXPIRE_MINUTES)
    
    to_encode = {
        "sub": user_id,
        "role": role,
        "type": "access",
        "exp": expire,
        "iat": datetime.now(timezone.utc)
    }
    
    encoded_jwt = jwt.encode(to_encode, SECRET_KEY, algorithm=ALGORITHM)
    return encoded_jwt


def create_refresh_token(db: Session, user_id: str, device_info: Optional[str] = None) -> str:
    """Create a refresh token and store its hash in the database"""
    # Generate a random token
    token = secrets.token_urlsafe(32)
    token_hash = hash_token(token)
    
    # Calculate expiry
    expires_at = datetime.now(timezone.utc) + timedelta(minutes=REFRESH_TOKEN_EXPIRE_MINUTES)
    
    # Store in database
    refresh_token = RefreshTokenDB(
        id=generate_uuid_v7(),
        token_hash=token_hash,
        user_id=user_id,
        expires_at=expires_at,
        device_info=device_info
    )
    db.add(refresh_token)
    db.commit()
    
    return token


def verify_access_token(token: str) -> Optional[dict]:
    """Verify an access token and return payload"""
    try:
        payload = jwt.decode(token, SECRET_KEY, algorithms=[ALGORITHM])
        
        # Check token type
        if payload.get("type") != "access":
            return None
        
        return payload
    except JWTError:
        return None


def verify_refresh_token(db: Session, token: str) -> Optional[RefreshTokenDB]:
    """Verify a refresh token against the database"""
    token_hash = hash_token(token)
    
    # Look up the token in the database
    refresh_token = db.query(RefreshTokenDB).filter(
        RefreshTokenDB.token_hash == token_hash,
        RefreshTokenDB.revoked_at == None,
        RefreshTokenDB.expires_at > datetime.now(timezone.utc)
    ).first()
    
    return refresh_token


def revoke_refresh_token(db: Session, token: str):
    """Revoke a refresh token (on logout or after use)"""
    token_hash = hash_token(token)
    
    refresh_token = db.query(RefreshTokenDB).filter(
        RefreshTokenDB.token_hash == token_hash
    ).first()
    
    if refresh_token:
        refresh_token.revoked_at = datetime.now(timezone.utc)
        db.commit()


def revoke_all_user_tokens(db: Session, user_id: str):
    """Revoke all refresh tokens for a user (on password change or security event)"""
    tokens = db.query(RefreshTokenDB).filter(
        RefreshTokenDB.user_id == user_id,
        RefreshTokenDB.revoked_at == None
    ).all()
    
    now = datetime.now(timezone.utc)
    for token in tokens:
        token.revoked_at = now
    
    db.commit()


def rotate_refresh_token(db: Session, old_token: str, device_info: Optional[str] = None) -> Tuple[str, str]:
    """
    Rotate refresh token - create new one and revoke old one
    Returns (new_access_token, new_refresh_token)
    """
    # Verify the old refresh token
    refresh_token_db = verify_refresh_token(db, old_token)
    
    if not refresh_token_db:
        raise HTTPException(status_code=401, detail="Invalid or expired refresh token")
    
    # Get user
    user = db.query(UserDB).filter(UserDB.id == refresh_token_db.user_id).first()
    if not user:
        raise HTTPException(status_code=401, detail="User not found")
    
    if not user.is_active:
        raise HTTPException(status_code=403, detail="User account is deactivated")
    
    # Revoke the old token
    revoke_refresh_token(db, old_token)
    
    # Create new tokens
    new_access_token = create_access_token(user.id, user.role)
    new_refresh_token = create_refresh_token(db, user.id, device_info)
    
    return new_access_token, new_refresh_token


def get_current_user_from_token(token: str) -> Optional[UserDB]:
    """Get user from access token"""
    payload = verify_access_token(token)
    if not payload:
        return None
    
    user_id = payload.get("sub")
    if not user_id:
        return None
    
    db = SessionLocal()
    try:
        user = db.query(UserDB).filter(UserDB.id == user_id).first()
        return user
    finally:
        db.close()


async def get_current_user(credentials: HTTPAuthorizationCredentials = security) -> UserDB:
    """FastAPI dependency to get current user from bearer token"""
    token = credentials.credentials
    
    payload = verify_access_token(token)
    if not payload:
        raise HTTPException(status_code=401, detail="Invalid or expired access token")
    
    user_id = payload.get("sub")
    if not user_id:
        raise HTTPException(status_code=401, detail="Invalid token payload")
    
    db = SessionLocal()
    try:
        user = db.query(UserDB).filter(UserDB.id == user_id).first()
        if not user:
            raise HTTPException(status_code=401, detail="User not found")
        
        if not user.is_active:
            raise HTTPException(status_code=403, detail="User account is deactivated")
        
        return user
    finally:
        db.close()


def require_role(required_role: str):
    """Decorator to require specific role"""
    async def role_checker(credentials: HTTPAuthorizationCredentials = security):
        user = await get_current_user(credentials)
        
        if user.role != required_role and user.role != "admin":
            raise HTTPException(
                status_code=403, 
                detail=f"Insufficient permissions. Required role: {required_role}"
            )
        
        return user
    
    return role_checker


def require_admin(credentials: HTTPAuthorizationCredentials = security) -> UserDB:
    """Require admin role"""
    return require_role("admin")(credentials)
