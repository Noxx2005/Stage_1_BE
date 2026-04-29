"""
Authentication module for Insighta Labs+
Handles GitHub OAuth with PKCE flow
"""

import os
import time
import hashlib
import base64
import secrets
from datetime import datetime, timezone, timedelta
from typing import Optional

import httpx
from fastapi import HTTPException, Request
from sqlalchemy.orm import Session
from sqlalchemy import Column, String, DateTime, Boolean, create_engine, event

from database import Base, SessionLocal, generate_uuid_v7

# PKCE Storage (temporary in-memory for CLI flow)
# Maps state -> {code_verifier, created_at}
# WARNING: In-memory storage is not suitable for production with multiple server instances.
# For production, use Redis or database-backed storage.
# TODO: Implement Redis-backed PKCE storage for horizontal scaling
pkce_storage = {}

# GitHub OAuth Configuration
GITHUB_CLIENT_ID = os.getenv("GITHUB_CLIENT_ID", "")
GITHUB_CLIENT_SECRET = os.getenv("GITHUB_CLIENT_SECRET", "")
GITHUB_REDIRECT_URI = os.getenv("GITHUB_REDIRECT_URI", "http://localhost:8000/auth/github/callback")

# Frontend URL for web flow redirect
FRONTEND_URL = os.getenv("FRONTEND_URL", "http://localhost:3000")


def generate_pkce_challenge():
    """Generate PKCE code_verifier and code_challenge"""
    # Generate a random code_verifier (43-128 chars)
    code_verifier = base64.urlsafe_b64encode(
        secrets.token_bytes(32)
    ).decode('utf-8').rstrip('=')
    
    # Create code_challenge using SHA256
    code_challenge = base64.urlsafe_b64encode(
        hashlib.sha256(code_verifier.encode('utf-8')).digest()
    ).decode('utf-8').rstrip('=')
    
    return code_verifier, code_challenge


def generate_state():
    """Generate a random state parameter"""
    return base64.urlsafe_b64encode(secrets.token_bytes(32)).decode('utf-8').rstrip('=')


def store_pkce_data(state: str, code_verifier: str, flow_type: str = "web"):
    """Store PKCE data temporarily"""
    pkce_storage[state] = {
        "code_verifier": code_verifier,
        "created_at": datetime.now(timezone.utc),
        "flow_type": flow_type
    }
    # Clean up old entries (older than 10 minutes)
    cleanup_old_pkce_data()


def cleanup_old_pkce_data():
    """Remove PKCE data older than 10 minutes"""
    now = datetime.now(timezone.utc)
    expired_states = [
        state for state, data in pkce_storage.items()
        if (now - data["created_at"]) > timedelta(minutes=10)
    ]
    for state in expired_states:
        del pkce_storage[state]


def get_pkce_data(state: str):
    """Retrieve and remove PKCE data"""
    return pkce_storage.pop(state, None)


def get_github_oauth_url(state: str, code_challenge: str, flow_type: str = "web"):
    """Build GitHub OAuth URL with PKCE"""
    scope = "read:user user:email"
    redirect_uri = GITHUB_REDIRECT_URI
    
    # For CLI flow, we might want a different redirect
    if flow_type == "cli":
        # CLI uses localhost callback server
        redirect_uri = "http://localhost:8000/auth/github/callback"
    
    return (
        f"https://github.com/login/oauth/authorize?"
        f"client_id={GITHUB_CLIENT_ID}&"
        f"redirect_uri={redirect_uri}&"
        f"scope={scope}&"
        f"state={state}&"
        f"code_challenge={code_challenge}&"
        f"code_challenge_method=S256"
    )


async def exchange_code_for_token(code: str, code_verifier: str):
    """Exchange authorization code for access token"""
    async with httpx.AsyncClient() as client:
        response = await client.post(
            "https://github.com/login/oauth/access_token",
            headers={"Accept": "application/json"},
            data={
                "client_id": GITHUB_CLIENT_ID,
                "client_secret": GITHUB_CLIENT_SECRET,
                "code": code,
                "redirect_uri": GITHUB_REDIRECT_URI,
                "code_verifier": code_verifier
            }
        )
        
        if response.status_code != 200:
            raise HTTPException(status_code=502, detail="Failed to exchange code with GitHub")
        
        data = response.json()
        if "error" in data:
            raise HTTPException(status_code=400, detail=f"GitHub error: {data.get('error_description', data['error'])}")
        
        return data.get("access_token")


async def get_github_user(access_token: str):
    """Fetch user info from GitHub API"""
    async with httpx.AsyncClient() as client:
        # Get user info
        user_response = await client.get(
            "https://api.github.com/user",
            headers={
                "Authorization": f"token {access_token}",
                "Accept": "application/json"
            }
        )
        
        if user_response.status_code != 200:
            raise HTTPException(status_code=502, detail="Failed to fetch user from GitHub")
        
        user_data = user_response.json()
        
        # Get primary email
        email_response = await client.get(
            "https://api.github.com/user/emails",
            headers={
                "Authorization": f"token {access_token}",
                "Accept": "application/json"
            }
        )
        
        email = None
        if email_response.status_code == 200:
            emails = email_response.json()
            # Get primary and verified email
            for e in emails:
                if e.get("primary") and e.get("verified"):
                    email = e.get("email")
                    break
            # Fallback to any email
            if not email and emails:
                email = emails[0].get("email")
        
        return {
            "github_id": str(user_data.get("id")),
            "username": user_data.get("login"),
            "email": email,
            "avatar_url": user_data.get("avatar_url")
        }
