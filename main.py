"""
Insighta Labs+ - Stage 3 Backend
FastAPI + SQLite with Authentication, RBAC, and Multi-Interface Support
"""

import os
import csv
import io
import re
from datetime import datetime, timezone
from typing import List, Optional, Union

import httpx
from fastapi import FastAPI, HTTPException, Query, Request, Depends, status
from fastapi.exceptions import RequestValidationError
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse, StreamingResponse, RedirectResponse
from pydantic import BaseModel, Field, field_validator
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials

# Load environment variables
from dotenv import load_dotenv
load_dotenv()

# Import our modules
from database import (
    Base, SessionLocal, get_db, generate_uuid_v7, init_db,
    ProfileDB, UserDB, RefreshTokenDB
)
from jwt_handler import (
    create_access_token, create_refresh_token, verify_refresh_token,
    revoke_refresh_token, rotate_refresh_token, get_current_user,
    require_role, require_admin, security
)
from auth import (
    generate_pkce_challenge, generate_state, store_pkce_data, get_pkce_data,
    get_github_oauth_url, exchange_code_for_token, get_github_user,
    GITHUB_CLIENT_ID, FRONTEND_URL
)
from middleware import (
    RateLimitMiddleware, LoggingMiddleware, APIVersionMiddleware,
    AuthMiddleware, CSRFMiddleware
)

# Pydantic Models
class CreateProfileRequest(BaseModel):
    name: str = Field(..., description="Name to create profile for")
    
    @field_validator('name')
    @classmethod
    def validate_name(cls, v):
        if not isinstance(v, str):
            raise ValueError('Name must be a string')
        return v


class ProfileResponse(BaseModel):
    id: str
    name: str
    gender: str
    gender_probability: float
    sample_size: int
    age: int
    age_group: str
    country_id: str
    country_name: str
    country_probability: float
    created_at: str


class ProfileListItem(BaseModel):
    id: str
    name: str
    gender: str
    gender_probability: float
    age: int
    age_group: str
    country_id: str
    country_name: str
    country_probability: float
    created_at: str


class SuccessResponseSingle(BaseModel):
    status: str = "success"
    data: ProfileResponse


class SuccessResponseSingleWithMessage(BaseModel):
    status: str = "success"
    message: str
    data: ProfileResponse


class SuccessResponseList(BaseModel):
    status: str = "success"
    page: int
    limit: int
    total: int
    total_pages: int
    links: dict
    data: List[ProfileListItem]


class ErrorResponse(BaseModel):
    status: str = "error"
    message: str


class TokenResponse(BaseModel):
    status: str = "success"
    access_token: str
    refresh_token: str
    token_type: str = "bearer"
    user: dict


class RefreshRequest(BaseModel):
    refresh_token: str


class RefreshResponse(BaseModel):
    status: str = "success"
    access_token: str
    refresh_token: str
    token_type: str = "bearer"


class LogoutResponse(BaseModel):
    status: str = "success"
    message: str


class UserResponse(BaseModel):
    id: str
    username: str
    email: Optional[str]
    avatar_url: Optional[str]
    role: str
    is_active: bool
    created_at: str


# External API calls
async def call_genderize_api(name: str):
    async with httpx.AsyncClient() as client:
        response = await client.get(f"https://api.genderize.io?name={name}")
        return response.json()


async def call_agify_api(name: str):
    async with httpx.AsyncClient() as client:
        response = await client.get(f"https://api.agify.io?name={name}")
        return response.json()


async def call_nationalize_api(name: str):
    async with httpx.AsyncClient() as client:
        response = await client.get(f"https://api.nationalize.io?name={name}")
        return response.json()


# Classification logic
def get_age_group(age: int) -> str:
    if age <= 12:
        return "child"
    elif age <= 19:
        return "teenager"
    elif age <= 59:
        return "adult"
    else:
        return "senior"


def get_country_with_highest_prob(countries: list) -> tuple:
    if not countries:
        return None, None
    
    highest = max(countries, key=lambda x: x.get('probability', 0))
    return highest.get('country_id'), highest.get('probability')


# Country name to country ID mapping
COUNTRY_NAME_TO_ID = {
    'nigeria': 'NG', 'united states': 'US', 'tanzania': 'TZ', 'uganda': 'UG',
    'sudan': 'SD', 'kenya': 'KE', 'ghana': 'GH', 'ethiopia': 'ET',
    'south africa': 'ZA', 'cameroon': 'CM', 'angola': 'AO', 'ivory coast': 'CI',
    'mozambique': 'MZ', 'zimbabwe': 'ZW', 'zambia': 'ZM', 'botswana': 'BW',
    'malawi': 'MW', 'rwanda': 'RW', 'burundi': 'BI', 'senegal': 'SN',
    'mali': 'ML', 'burkina faso': 'BF', 'niger': 'NE', 'chad': 'TD',
    'central african republic': 'CF', 'congo': 'CG', 'democratic republic of the congo': 'CD',
    'benin': 'BJ', 'togo': 'TG', 'gambia': 'GM', 'guinea': 'GN',
    'guinea-bissau': 'GW', 'sierra leone': 'SL', 'liberia': 'LR', 'libya': 'LY',
    'egypt': 'EG', 'morocco': 'MA', 'algeria': 'DZ', 'tunisia': 'TN',
    'mauritania': 'MR', 'western sahara': 'EH', 'somalia': 'SO', 'djibouti': 'DJ',
    'eritrea': 'ER', 'south sudan': 'SS', 'gabon': 'GA', 'equatorial guinea': 'GQ',
    'sao tome and principe': 'ST', 'cape verde': 'CV', 'comoros': 'KM',
    'madagascar': 'MG', 'mauritius': 'MU', 'seychelles': 'SC', 'lesotho': 'LS',
    'eswatini': 'SZ', 'namibia': 'NA',
}


def parse_natural_language_query(query: str) -> dict:
    """
    Parse natural language query and convert to filters.
    Rule-based parsing only (no AI/LLM).
    """
    if not query or not query.strip():
        return None
    
    query_lower = query.lower().strip()
    filters = {}
    
    # Extract gender
    if 'male' in query_lower:
        filters['gender'] = 'male'
    if 'female' in query_lower:
        filters['gender'] = 'female'
    
    # Extract age group keywords
    if 'adult' in query_lower and 'teenager' not in query_lower and 'child' not in query_lower:
        filters['age_group'] = 'adult'
    elif 'teenager' in query_lower:
        filters['age_group'] = 'teenager'
    elif 'senior' in query_lower:
        filters['age_group'] = 'senior'
    elif 'child' in query_lower:
        filters['age_group'] = 'child'
    
    # Extract "young" keyword (maps to 16-24)
    if 'young' in query_lower:
        filters['min_age'] = 16
        filters['max_age'] = 24
    
    # Extract "above {age}" pattern
    above_match = re.search(r'above\s+(\d+)', query_lower)
    if above_match:
        filters['min_age'] = int(above_match.group(1))
    
    # Extract "below {age}" pattern
    below_match = re.search(r'below\s+(\d+)', query_lower)
    if below_match:
        filters['max_age'] = int(below_match.group(1))
    
    # Extract "over {age}" pattern
    over_match = re.search(r'over\s+(\d+)', query_lower)
    if over_match:
        filters['min_age'] = int(over_match.group(1))
    
    # Extract "under {age}" pattern
    under_match = re.search(r'under\s+(\d+)', query_lower)
    if under_match:
        filters['max_age'] = int(under_match.group(1))
    
    # Extract "from {country}" pattern
    from_match = re.search(r'from\s+(\w+(?:\s+\w+)?)', query_lower)
    if from_match:
        country_name = from_match.group(1)
        country_id = COUNTRY_NAME_TO_ID.get(country_name)
        if country_id:
            filters['country_id'] = country_id
        else:
            for name, cid in COUNTRY_NAME_TO_ID.items():
                if country_name in name or name in country_name:
                    filters['country_id'] = cid
                    break
    
    if not filters:
        return None
    
    return filters


# Pagination helper
def build_pagination_links(base_path: str, page: int, limit: int, total: int, filters: dict = None) -> dict:
    """Build HATEOAS links for pagination"""
    total_pages = (total + limit - 1) // limit if total > 0 else 1
    
    # Build query string from filters
    query_parts = [f"page={page}", f"limit={limit}"]
    if filters:
        for key, value in filters.items():
            if value is not None:
                query_parts.append(f"{key}={value}")
    
    base_query = "&".join(query_parts)
    
    links = {
        "self": f"{base_path}?{base_query}"
    }
    
    # Next link
    if page < total_pages:
        next_query_parts = [f"page={page + 1}", f"limit={limit}"]
        if filters:
            for key, value in filters.items():
                if value is not None:
                    next_query_parts.append(f"{key}={value}")
        links["next"] = f"{base_path}?{'&'.join(next_query_parts)}"
    else:
        links["next"] = None
    
    # Previous link
    if page > 1:
        prev_query_parts = [f"page={page - 1}", f"limit={limit}"]
        if filters:
            for key, value in filters.items():
                if value is not None:
                    prev_query_parts.append(f"{key}={value}")
        links["prev"] = f"{base_path}?{'&'.join(prev_query_parts)}"
    else:
        links["prev"] = None
    
    return links


# FastAPI app
app = FastAPI(
    title="Insighta Labs+ - Stage 3",
    description="Secure Profile Intelligence System with Authentication and Multi-Interface Support",
    version="2.0.0"
)

# Parse CORS origins (comma-separated)
cors_origins_env = os.getenv("CORS_ORIGINS", "*")
if cors_origins_env and cors_origins_env != "*":
    allow_origins = [origin.strip() for origin in cors_origins_env.split(",")]
else:
    allow_origins = ["*"]

# CORS middleware - must be FIRST to handle preflight requests
app.add_middleware(
    CORSMiddleware,
    allow_origins=allow_origins,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
    expose_headers=["*"],
    max_age=600,
)

# Add other middleware (order matters - last added runs first)
app.add_middleware(RateLimitMiddleware)
app.add_middleware(LoggingMiddleware)
app.add_middleware(APIVersionMiddleware)
app.add_middleware(AuthMiddleware)
app.add_middleware(CSRFMiddleware)


@app.on_event("startup")
async def startup_event():
    """Initialize database on startup"""
    init_db()
    print("Database initialized")


# Error handlers
@app.exception_handler(ValueError)
async def value_error_handler(request: Request, exc: ValueError):
    return JSONResponse(
        status_code=422,
        content={"status": "error", "message": "Invalid type"}
    )


@app.exception_handler(HTTPException)
async def http_exception_handler(request: Request, exc: HTTPException):
    if isinstance(exc.detail, dict):
        content = exc.detail
    else:
        content = {"status": "error", "message": str(exc.detail)}
    return JSONResponse(
        status_code=exc.status_code,
        content=content
    )


@app.exception_handler(RequestValidationError)
async def validation_exception_handler(request: Request, exc: RequestValidationError):
    return JSONResponse(
        status_code=422,
        content={"status": "error", "message": "Invalid type"}
    )


# Auth Endpoints
@app.get("/auth/github")
async def github_auth(request: Request, flow: str = "web"):
    """
    Initiate GitHub OAuth flow.
    Redirects to GitHub OAuth page with PKCE parameters.
    """
    code_verifier, code_challenge = generate_pkce_challenge()
    state = generate_state()
    
    # Store PKCE data
    store_pkce_data(state, code_verifier, flow)
    
    # Build OAuth URL and redirect
    oauth_url = get_github_oauth_url(state, code_challenge, flow)
    
    return RedirectResponse(url=oauth_url)


@app.get("/auth/github/callback")
async def github_callback_get(code: str, state: str):
    """Handle GitHub OAuth callback (GET - for browser/web flow)"""
    return await handle_github_callback(code, state, None)


@app.post("/auth/github/callback")
async def github_callback_post(request: Request):
    """Handle GitHub OAuth callback (POST - for CLI flow)"""
    body = await request.json()
    return await handle_github_callback(
        body.get("code"),
        body.get("state"),
        body.get("code_verifier")
    )


async def handle_github_callback(code: str = None, state: str = None, provided_code_verifier: str = None):
    """Handle GitHub OAuth callback for both web and CLI flows"""
    # Validate required parameters
    if not code:
        raise HTTPException(
            status_code=400,
            detail={"status": "error", "message": "Authorization code is required"}
        )
    
    if not state:
        raise HTTPException(
            status_code=400,
            detail={"status": "error", "message": "State parameter is required"}
        )
    
    code_verifier = None
    flow_type = "web"
    
    # If code_verifier is provided (CLI flow), use it directly
    if provided_code_verifier:
        code_verifier = provided_code_verifier
        flow_type = "cli"
    else:
        # Retrieve from storage (web flow)
        pkce_data = get_pkce_data(state)
        if not pkce_data:
            raise HTTPException(
                status_code=400,
                detail={"status": "error", "message": "Invalid or expired state parameter"}
            )
        code_verifier = pkce_data["code_verifier"]
        flow_type = pkce_data.get("flow_type", "web")
    
    try:
        # Exchange code for GitHub access token
        github_access_token = await exchange_code_for_token(code, code_verifier)
        
        # Get user info from GitHub
        github_user = await get_github_user(github_access_token)
        
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(
            status_code=502,
            detail={"status": "error", "message": f"Failed to authenticate with GitHub: {str(e)}"}
        )
    
    # Create or update user in database
    db = SessionLocal()
    try:
        # Check if user exists
        user = db.query(UserDB).filter(
            UserDB.github_id == github_user["github_id"]
        ).first()
        
        if user:
            # Update existing user
            user.username = github_user["username"]
            user.email = github_user["email"]
            user.avatar_url = github_user["avatar_url"]
            user.last_login_at = datetime.now(timezone.utc)
        else:
            # Create new user with default analyst role
            user = UserDB(
                id=generate_uuid_v7(),
                github_id=github_user["github_id"],
                username=github_user["username"],
                email=github_user["email"],
                avatar_url=github_user["avatar_url"],
                role="analyst",  # Default role
                is_active=True,
                last_login_at=datetime.now(timezone.utc),
                created_at=datetime.now(timezone.utc)
            )
            db.add(user)
        
        db.commit()
        db.refresh(user)
        
        # Create tokens
        access_token = create_access_token(user.id, user.role)
        refresh_token = create_refresh_token(db, user.id)
        
        db.commit()
        
        # Prepare response
        token_response = {
            "status": "success",
            "access_token": access_token,
            "refresh_token": refresh_token,
            "token_type": "bearer",
            "user": {
                "id": user.id,
                "username": user.username,
                "email": user.email,
                "avatar_url": user.avatar_url,
                "role": user.role
            }
        }
        
        # For web flow, set cookies and redirect to frontend
        if flow_type == "web":
            response = RedirectResponse(url=FRONTEND_URL)
            
            # Set HTTP-only cookies
            response.set_cookie(
                key="access_token",
                value=access_token,
                httponly=True,
                secure=True,
                samesite="lax",
                max_age=180  # 3 minutes
            )
            response.set_cookie(
                key="refresh_token",
                value=refresh_token,
                httponly=True,
                secure=True,
                samesite="lax",
                max_age=300  # 5 minutes
            )
            
            return response
        
        # For CLI flow, return tokens directly
        return JSONResponse(status_code=200, content=token_response)
        
    finally:
        db.close()


@app.post("/auth/refresh", response_model=RefreshResponse)
async def refresh_token(request: RefreshRequest):
    """Refresh access token using refresh token"""
    db = SessionLocal()
    try:
        # Rotate tokens (invalidates old refresh token, creates new pair)
        access_token, refresh_token = rotate_refresh_token(db, request.refresh_token)
        
        return RefreshResponse(
            status="success",
            access_token=access_token,
            refresh_token=refresh_token,
            token_type="bearer"
        )
    except HTTPException:
        raise
    finally:
        db.close()


@app.post("/auth/logout", response_model=LogoutResponse)
async def logout(request: RefreshRequest):
    """Logout - invalidate refresh token"""
    db = SessionLocal()
    try:
        revoke_refresh_token(db, request.refresh_token)
        
        return LogoutResponse(
            status="success",
            message="Successfully logged out"
        )
    finally:
        db.close()


@app.get("/auth/me")
async def get_me(current_user: UserDB = Depends(get_current_user)):
    """Get current user info"""
    return {
        "status": "success",
        "data": {
            "id": current_user.id,
            "username": current_user.username,
            "email": current_user.email,
            "avatar_url": current_user.avatar_url,
            "role": current_user.role,
            "is_active": current_user.is_active,
            "created_at": current_user.created_at.strftime("%Y-%m-%dT%H:%M:%SZ")
        }
    }


# Profile Endpoints (All protected by auth middleware)

@app.post("/api/profiles", response_model=SuccessResponseSingle, status_code=201)
async def create_profile(
    request: CreateProfileRequest,
    current_user: UserDB = Depends(require_role("admin"))
):
    """
    Create a new profile (Admin only).
    Calls external APIs and stores the result.
    """
    # Validate name
    if not request.name or not isinstance(request.name, str):
        raise HTTPException(
            status_code=400,
            detail={"status": "error", "message": "Name is required and must be a non-empty string"}
        )
    
    # Check if profile already exists
    db = next(get_db())
    existing_profile = db.query(ProfileDB).filter(
        ProfileDB.name == request.name.lower().strip()
    ).first()
    
    if existing_profile:
        return SuccessResponseSingleWithMessage(
            status="success",
            message="Profile already exists",
            data=ProfileResponse(
                id=existing_profile.id,
                name=existing_profile.name,
                gender=existing_profile.gender,
                gender_probability=existing_profile.gender_probability,
                sample_size=existing_profile.sample_size,
                age=existing_profile.age,
                age_group=existing_profile.age_group,
                country_id=existing_profile.country_id,
                country_name=existing_profile.country_name,
                country_probability=existing_profile.country_probability,
                created_at=existing_profile.created_at.strftime("%Y-%m-%dT%H:%M:%SZ")
            )
        )
    
    # Call external APIs
    try:
        genderize_data = await call_genderize_api(request.name)
        agify_data = await call_agify_api(request.name)
        nationalize_data = await call_nationalize_api(request.name)
    except Exception as e:
        raise HTTPException(
            status_code=502,
            detail={"status": "error", "message": "Failed to call external APIs"}
        )
    
    # Validate responses
    if genderize_data.get('gender') is None or genderize_data.get('count', 0) == 0:
        raise HTTPException(
            status_code=502,
            detail={"status": "error", "message": "Genderize returned an invalid response"}
        )
    
    if agify_data.get('age') is None:
        raise HTTPException(
            status_code=502,
            detail={"status": "error", "message": "Agify returned an invalid response"}
        )
    
    countries = nationalize_data.get('country', [])
    if not countries:
        raise HTTPException(
            status_code=502,
            detail={"status": "error", "message": "Nationalize returned an invalid response"}
        )
    
    # Extract data
    gender = genderize_data['gender']
    gender_probability = genderize_data['probability']
    sample_size = genderize_data['count']
    age = agify_data['age']
    age_group = get_age_group(age)
    country_id, country_probability = get_country_with_highest_prob(countries)
    
    if country_id is None:
        raise HTTPException(
            status_code=502,
            detail={"status": "error", "message": "Nationalize returned an invalid response"}
        )
    
    # Create profile
    new_profile = ProfileDB(
        id=generate_uuid_v7(),
        name=request.name.lower().strip(),
        gender=gender,
        gender_probability=gender_probability,
        sample_size=sample_size,
        age=age,
        age_group=age_group,
        country_id=country_id,
        country_name=COUNTRY_NAME_TO_ID.get(country_id.lower(), ""),
        country_probability=country_probability,
        created_at=datetime.now(timezone.utc),
        created_by=current_user.id
    )
    
    db.add(new_profile)
    db.commit()
    db.refresh(new_profile)
    
    return SuccessResponseSingle(
        status="success",
        data=ProfileResponse(
            id=new_profile.id,
            name=new_profile.name,
            gender=new_profile.gender,
            gender_probability=new_profile.gender_probability,
            sample_size=new_profile.sample_size,
            age=new_profile.age,
            age_group=new_profile.age_group,
            country_id=new_profile.country_id,
            country_name=new_profile.country_name,
            country_probability=new_profile.country_probability,
            created_at=new_profile.created_at.strftime("%Y-%m-%dT%H:%M:%SZ")
        )
    )


@app.get("/api/profiles", response_model=SuccessResponseList)
async def get_all_profiles(
    gender: Optional[str] = Query(None, description="Filter by gender (case-insensitive)"),
    country_id: Optional[str] = Query(None, description="Filter by country ID (case-insensitive)"),
    age_group: Optional[str] = Query(None, description="Filter by age group (case-insensitive)"),
    min_age: Optional[int] = Query(None, description="Filter by minimum age"),
    max_age: Optional[int] = Query(None, description="Filter by maximum age"),
    min_gender_probability: Optional[float] = Query(None, description="Filter by minimum gender probability"),
    min_country_probability: Optional[float] = Query(None, description="Filter by minimum country probability"),
    sort_by: Optional[str] = Query(None, description="Sort by: age, created_at, gender_probability"),
    order: Optional[str] = Query("asc", description="Sort order: asc or desc"),
    page: int = Query(1, ge=1, description="Page number (default: 1)"),
    limit: int = Query(10, ge=1, le=50, description="Results per page (default: 10, max: 50)"),
    current_user: UserDB = Depends(get_current_user)
):
    """Get all profiles with filtering, sorting, and pagination"""
    db = next(get_db())
    query = db.query(ProfileDB)
    
    # Apply filters
    if gender:
        query = query.filter(ProfileDB.gender.ilike(gender))
    if country_id:
        query = query.filter(ProfileDB.country_id.ilike(country_id))
    if age_group:
        query = query.filter(ProfileDB.age_group.ilike(age_group))
    if min_age is not None:
        query = query.filter(ProfileDB.age >= min_age)
    if max_age is not None:
        query = query.filter(ProfileDB.age <= max_age)
    if min_gender_probability is not None:
        query = query.filter(ProfileDB.gender_probability >= min_gender_probability)
    if min_country_probability is not None:
        query = query.filter(ProfileDB.country_probability >= min_country_probability)
    
    # Get total count
    total = query.count()
    
    # Apply sorting
    if sort_by:
        valid_sort_fields = {
            'age': ProfileDB.age,
            'created_at': ProfileDB.created_at,
            'gender_probability': ProfileDB.gender_probability
        }
        if sort_by in valid_sort_fields:
            sort_field = valid_sort_fields[sort_by]
            if order.lower() == 'desc':
                query = query.order_by(sort_field.desc())
            else:
                query = query.order_by(sort_field.asc())
    
    # Apply pagination
    total_pages = (total + limit - 1) // limit if total > 0 else 1
    if page > total_pages:
        page = total_pages
    
    offset = (page - 1) * limit
    profiles = query.offset(offset).limit(limit).all()
    
    # Build filter dict for pagination links
    filters = {
        "gender": gender,
        "country_id": country_id,
        "age_group": age_group,
        "min_age": min_age,
        "max_age": max_age,
        "min_gender_probability": min_gender_probability,
        "min_country_probability": min_country_probability,
        "sort_by": sort_by,
        "order": order
    }
    
    # Build pagination links
    links = build_pagination_links("/api/profiles", page, limit, total, filters)
    
    return SuccessResponseList(
        status="success",
        page=page,
        limit=limit,
        total=total,
        total_pages=total_pages,
        links=links,
        data=[
            ProfileListItem(
                id=p.id,
                name=p.name,
                gender=p.gender,
                gender_probability=p.gender_probability,
                age=p.age,
                age_group=p.age_group,
                country_id=p.country_id,
                country_name=p.country_name,
                country_probability=p.country_probability,
                created_at=p.created_at.strftime("%Y-%m-%dT%H:%M:%SZ")
            )
            for p in profiles
        ]
    )


@app.get("/api/profiles/search", response_model=SuccessResponseList)
async def search_profiles(
    q: str = Query(..., description="Natural language query string"),
    page: int = Query(1, ge=1, description="Page number (default: 1)"),
    limit: int = Query(10, ge=1, le=50, description="Results per page (default: 10, max: 50)"),
    current_user: UserDB = Depends(get_current_user)
):
    """Search profiles using natural language queries"""
    filters = parse_natural_language_query(q)
    
    if not filters:
        raise HTTPException(
            status_code=400,
            detail={"status": "error", "message": "Unable to interpret query"}
        )
    
    db = next(get_db())
    query = db.query(ProfileDB)
    
    # Apply parsed filters
    if 'gender' in filters:
        query = query.filter(ProfileDB.gender.ilike(filters['gender']))
    if 'country_id' in filters:
        query = query.filter(ProfileDB.country_id.ilike(filters['country_id']))
    if 'age_group' in filters:
        query = query.filter(ProfileDB.age_group.ilike(filters['age_group']))
    if 'min_age' in filters:
        query = query.filter(ProfileDB.age >= filters['min_age'])
    if 'max_age' in filters:
        query = query.filter(ProfileDB.age <= filters['max_age'])
    
    total = query.count()
    
    # Apply pagination
    total_pages = (total + limit - 1) // limit if total > 0 else 1
    offset = (page - 1) * limit
    profiles = query.offset(offset).limit(limit).all()
    
    # Build pagination links
    links = build_pagination_links("/api/profiles/search", page, limit, total, {"q": q})
    
    return SuccessResponseList(
        status="success",
        page=page,
        limit=limit,
        total=total,
        total_pages=total_pages,
        links=links,
        data=[
            ProfileListItem(
                id=p.id,
                name=p.name,
                gender=p.gender,
                gender_probability=p.gender_probability,
                age=p.age,
                age_group=p.age_group,
                country_id=p.country_id,
                country_name=p.country_name,
                country_probability=p.country_probability,
                created_at=p.created_at.strftime("%Y-%m-%dT%H:%M:%SZ")
            )
            for p in profiles
        ]
    )


@app.get("/api/profiles/{profile_id}", response_model=SuccessResponseSingle)
async def get_profile(
    profile_id: str,
    current_user: UserDB = Depends(get_current_user)
):
    """Get a single profile by ID"""
    db = next(get_db())
    profile = db.query(ProfileDB).filter(ProfileDB.id == profile_id).first()
    
    if not profile:
        raise HTTPException(
            status_code=404,
            detail={"status": "error", "message": "Profile not found"}
        )
    
    return SuccessResponseSingle(
        status="success",
        data=ProfileResponse(
            id=profile.id,
            name=profile.name,
            gender=profile.gender,
            gender_probability=profile.gender_probability,
            sample_size=profile.sample_size,
            age=profile.age,
            age_group=profile.age_group,
            country_id=profile.country_id,
            country_name=profile.country_name,
            country_probability=profile.country_probability,
            created_at=profile.created_at.strftime("%Y-%m-%dT%H:%M:%SZ")
        )
    )


@app.delete("/api/profiles/{profile_id}", status_code=204)
async def delete_profile(
    profile_id: str,
    current_user: UserDB = Depends(require_role("admin"))
):
    """Delete a profile (Admin only)"""
    db = next(get_db())
    profile = db.query(ProfileDB).filter(ProfileDB.id == profile_id).first()
    
    if not profile:
        raise HTTPException(
            status_code=404,
            detail={"status": "error", "message": "Profile not found"}
        )
    
    db.delete(profile)
    db.commit()
    
    return None


@app.get("/api/profiles/export")
async def export_profiles(
    format: str = Query("csv", description="Export format (csv)"),
    gender: Optional[str] = Query(None, description="Filter by gender"),
    country_id: Optional[str] = Query(None, description="Filter by country ID"),
    age_group: Optional[str] = Query(None, description="Filter by age group"),
    min_age: Optional[int] = Query(None, description="Filter by minimum age"),
    max_age: Optional[int] = Query(None, description="Filter by maximum age"),
    sort_by: Optional[str] = Query(None, description="Sort by field"),
    order: Optional[str] = Query("asc", description="Sort order"),
    current_user: UserDB = Depends(get_current_user)
):
    """Export profiles to CSV (Analyst and Admin)"""
    if format != "csv":
        raise HTTPException(
            status_code=400,
            detail={"status": "error", "message": "Only CSV format is supported"}
        )
    
    db = next(get_db())
    query = db.query(ProfileDB)
    
    # Apply same filters as list endpoint
    if gender:
        query = query.filter(ProfileDB.gender.ilike(gender))
    if country_id:
        query = query.filter(ProfileDB.country_id.ilike(country_id))
    if age_group:
        query = query.filter(ProfileDB.age_group.ilike(age_group))
    if min_age is not None:
        query = query.filter(ProfileDB.age >= min_age)
    if max_age is not None:
        query = query.filter(ProfileDB.age <= max_age)
    
    # Apply sorting
    if sort_by:
        valid_sort_fields = {
            'age': ProfileDB.age,
            'created_at': ProfileDB.created_at,
            'gender_probability': ProfileDB.gender_probability
        }
        if sort_by in valid_sort_fields:
            sort_field = valid_sort_fields[sort_by]
            if order.lower() == 'desc':
                query = query.order_by(sort_field.desc())
            else:
                query = query.order_by(sort_field.asc())
    
    profiles = query.all()
    
    # Generate CSV
    output = io.StringIO()
    writer = csv.writer(output)
    
    # Write header
    writer.writerow([
        'id', 'name', 'gender', 'gender_probability', 'age', 'age_group',
        'country_id', 'country_name', 'country_probability', 'created_at'
    ])
    
    # Write data
    for p in profiles:
        writer.writerow([
            p.id, p.name, p.gender, p.gender_probability, p.age, p.age_group,
            p.country_id, p.country_name, p.country_probability,
            p.created_at.strftime("%Y-%m-%dT%H:%M:%SZ")
        ])
    
    output.seek(0)
    
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    filename = f"profiles_{timestamp}.csv"
    
    return StreamingResponse(
        iter([output.getvalue()]),
        media_type="text/csv",
        headers={
            "Content-Disposition": f'attachment; filename="{filename}"'
        }
    )


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
