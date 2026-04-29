"""
Middleware for Insighta Labs+
Rate limiting, logging, API versioning, and auth middleware
"""

import time
import os
from datetime import datetime, timezone
from typing import Optional
from collections import defaultdict
from functools import wraps

from fastapi import Request, HTTPException, Response
from fastapi.responses import JSONResponse
from starlette.middleware.base import BaseHTTPMiddleware

from jwt_handler import verify_access_token, get_current_user_from_token
from database import SessionLocal, UserDB

# Rate limiting storage
# Structure: {client_id: [(timestamp, endpoint), ...]}
request_history = defaultdict(list)

# Rate limit configuration (per requirements: 10/min for auth, 60/min for API)
AUTH_RATE_LIMIT = int(os.getenv("AUTH_RATE_LIMIT", "10"))   # 10 requests per minute for auth endpoints
API_RATE_LIMIT = int(os.getenv("API_RATE_LIMIT", "60"))     # 60 requests per minute for other endpoints
RATE_LIMIT_WINDOW = 60  # 60 seconds
RATE_LIMIT_CLEANUP_THRESHOLD = 1000  # Clean up when storage exceeds this many entries


class RateLimitMiddleware(BaseHTTPMiddleware):
    """Rate limiting middleware"""
    
    async def dispatch(self, request: Request, call_next):
        # Get client identifier (user_id from token or IP address)
        client_id = await self.get_client_id(request)
        endpoint = request.url.path
        
        # Determine rate limit based on endpoint
        if endpoint.startswith("/auth/"):
            limit = AUTH_RATE_LIMIT
        else:
            limit = API_RATE_LIMIT
        
        # Check rate limit
        if not check_rate_limit(client_id, endpoint, limit):
            return JSONResponse(
                status_code=429,
                content={
                    "status": "error",
                    "message": "Rate limit exceeded. Please try again later."
                }
            )
        
        response = await call_next(request)
        return response
    
    async def get_client_id(self, request: Request) -> str:
        """Get client identifier from token or IP"""
        # Try to get user_id from token
        auth_header = request.headers.get("authorization", "")
        if auth_header.startswith("Bearer "):
            token = auth_header.replace("Bearer ", "")
            payload = verify_access_token(token)
            if payload:
                user_id = payload.get("sub")
                if user_id:
                    return f"user:{user_id}"
        
        # Fallback to IP address
        forwarded = request.headers.get("x-forwarded-for")
        if forwarded:
            return f"ip:{forwarded.split(',')[0].strip()}"
        
        client = request.client
        if client:
            return f"ip:{client.host}"
        
        return "ip:unknown"


def check_rate_limit(client_id: str, endpoint: str, limit: int) -> bool:
    """Check if request is within rate limit"""
    now = time.time()
    window_start = now - RATE_LIMIT_WINDOW
    
    # Periodic cleanup of old entries to prevent memory leak
    if len(request_history) > RATE_LIMIT_CLEANUP_THRESHOLD:
        cleanup_rate_limit_storage(window_start)
    
    # Get existing requests for this client
    requests = request_history[client_id]
    
    # Filter to only include requests within the window
    recent_requests = [req for req in requests if req[0] > window_start]
    
    # Update storage
    request_history[client_id] = recent_requests
    
    # Check if under limit
    if len(recent_requests) >= limit:
        return False
    
    # Record this request
    recent_requests.append((now, endpoint))
    return True


def cleanup_rate_limit_storage(window_start: float):
    """Clean up expired entries from rate limit storage to prevent memory leak"""
    expired_clients = []
    for client_id, requests in request_history.items():
        # Filter to only recent requests
        recent = [req for req in requests if req[0] > window_start]
        if not recent:
            expired_clients.append(client_id)
        else:
            request_history[client_id] = recent
    
    # Remove clients with no recent requests
    for client_id in expired_clients:
        del request_history[client_id]


class LoggingMiddleware(BaseHTTPMiddleware):
    """Request logging middleware"""
    
    async def dispatch(self, request: Request, call_next):
        start_time = time.time()
        
        # Process request
        response = await call_next(request)
        
        # Calculate response time
        response_time = (time.time() - start_time) * 1000  # in milliseconds
        
        # Log request details
        timestamp = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S")
        method = request.method
        endpoint = request.url.path
        status_code = response.status_code
        
        # Get user info if available
        user_info = "anonymous"
        auth_header = request.headers.get("authorization", "")
        if auth_header.startswith("Bearer "):
            token = auth_header.replace("Bearer ", "")
            payload = verify_access_token(token)
            if payload:
                user_id = payload.get("sub", "unknown")
                role = payload.get("role", "unknown")
                user_info = f"{user_id}({role})"
        
        # Print log (in production, use proper logging)
        log_entry = f"[{timestamp}] {method} {endpoint} {status_code} - {response_time:.2f}ms - {user_info}"
        print(log_entry)
        
        return response


class APIVersionMiddleware(BaseHTTPMiddleware):
    """API Version header validation middleware"""
    
    # Endpoints that require version header
    PROTECTED_PREFIXES = ["/api/profiles"]
    
    async def dispatch(self, request: Request, call_next):
        path = request.url.path
        
        # Check if this endpoint requires version header
        requires_version = any(path.startswith(prefix) for prefix in self.PROTECTED_PREFIXES)
        
        if requires_version and request.method in ["GET", "POST", "PUT", "DELETE", "PATCH"]:
            version_header = request.headers.get("x-api-version")
            
            if not version_header:
                return JSONResponse(
                    status_code=400,
                    content={
                        "status": "error",
                        "message": "API version header required"
                    }
                )
            
            if version_header != "1":
                return JSONResponse(
                    status_code=400,
                    content={
                        "status": "error",
                        "message": "Unsupported API version. Use version 1."
                    }
                )
        
        response = await call_next(request)
        return response


class AuthMiddleware(BaseHTTPMiddleware):
    """Authentication middleware for protected endpoints"""
    
    # Public endpoints that don't require auth
    PUBLIC_ENDPOINTS = [
        "/auth/github",
        "/auth/github/callback",
        "/auth/refresh",
        "/docs",
        "/redoc",
        "/openapi.json",
        "/"
    ]
    
    # Public methods for specific endpoints
    PUBLIC_METHODS = {
        "/auth/github/callback": ["POST"]  # POST callback is public (CLI flow)
    }
    
    async def dispatch(self, request: Request, call_next):
        path = request.url.path
        method = request.method
        
        # Check if endpoint is public
        if any(path.startswith(public) for public in self.PUBLIC_ENDPOINTS):
            return await call_next(request)
        
        # Check if specific method for endpoint is public
        for public_path, methods in self.PUBLIC_METHODS.items():
            if path == public_path and method in methods:
                return await call_next(request)
        
        # Check if endpoint is under /api/
        if not path.startswith("/api/"):
            return await call_next(request)
        
        # Require authentication
        auth_header = request.headers.get("authorization", "")
        if not auth_header.startswith("Bearer "):
            return JSONResponse(
                status_code=401,
                content={
                    "status": "error",
                    "message": "Authentication required"
                }
            )
        
        token = auth_header.replace("Bearer ", "")
        payload = verify_access_token(token)
        
        if not payload:
            return JSONResponse(
                status_code=401,
                content={
                    "status": "error",
                    "message": "Invalid or expired access token"
                }
            )
        
        # Check if user is active
        user_id = payload.get("sub")
        if user_id:
            db = SessionLocal()
            try:
                user = db.query(UserDB).filter(UserDB.id == user_id).first()
                if user and not user.is_active:
                    return JSONResponse(
                        status_code=403,
                        content={
                            "status": "error",
                            "message": "User account is deactivated"
                        }
                    )
            finally:
                db.close()
        
        # Store user info in request state for later use
        request.state.user = payload
        
        response = await call_next(request)
        return response


class CSRFMiddleware(BaseHTTPMiddleware):
    """CSRF protection for web portal (cookie-based auth)"""
    
    # Endpoints that require CSRF protection (state-changing operations)
    PROTECTED_METHODS = ["POST", "PUT", "DELETE", "PATCH"]
    
    async def dispatch(self, request: Request, call_next):
        # Only check state-changing methods
        if request.method in self.PROTECTED_METHODS:
            # Skip for API token auth (bearer token)
            auth_header = request.headers.get("authorization", "")
            if auth_header.startswith("Bearer "):
                return await call_next(request)
            
            # Check for CSRF token in cookie-based requests
            csrf_token = request.headers.get("x-csrf-token") or request.headers.get("x-xsrf-token")
            cookie_csrf = request.cookies.get("csrf_token")
            
            if not csrf_token or not cookie_csrf or csrf_token != cookie_csrf:
                return JSONResponse(
                    status_code=403,
                    content={
                        "status": "error",
                        "message": "CSRF token missing or invalid"
                    }
                )
        
        response = await call_next(request)
        return response
