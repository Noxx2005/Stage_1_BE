"""
HNG Stage 1 Backend - Profile API
FastAPI + SQLite implementation
"""

import time
import uuid
from datetime import datetime, timezone
from typing import List, Optional, Union

import httpx
from fastapi import FastAPI, HTTPException, Query, Request
from fastapi.exceptions import RequestValidationError
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from pydantic import BaseModel, Field, field_validator
from sqlalchemy import Column, String, Float, Integer, DateTime, create_engine, event
from sqlalchemy.orm import declarative_base, sessionmaker, Session

# Database setup
DATABASE_URL = "sqlite:///./profiles.db"
engine = create_engine(DATABASE_URL, connect_args={"check_same_thread": False})
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)
Base = declarative_base()


# Database Model
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


# Create tables
Base.metadata.create_all(bind=engine)


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
    data: List[ProfileListItem]


class ErrorResponse(BaseModel):
    status: str = "error"
    message: str


# UUID v7 implementation
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


# Database dependency
def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()


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


# Country name to country ID mapping (from common countries in seed data)
COUNTRY_NAME_TO_ID = {
    'nigeria': 'NG',
    'united states': 'US',
    'tanzania': 'TZ',
    'uganda': 'UG',
    'sudan': 'SD',
    'kenya': 'KE',
    'ghana': 'GH',
    'ethiopia': 'ET',
    'south africa': 'ZA',
    'cameroon': 'CM',
    'angola': 'AO',
    'ivory coast': 'CI',
    'mozambique': 'MZ',
    'zimbabwe': 'ZW',
    'zambia': 'ZM',
    'botswana': 'BW',
    'malawi': 'MW',
    'rwanda': 'RW',
    'burundi': 'BI',
    'senegal': 'SN',
    'mali': 'ML',
    'burkina faso': 'BF',
    'niger': 'NE',
    'chad': 'TD',
    'central african republic': 'CF',
    'congo': 'CG',
    'democratic republic of the congo': 'CD',
    'benin': 'BJ',
    'togo': 'TG',
    'gambia': 'GM',
    'guinea': 'GN',
    'guinea-bissau': 'GW',
    'sierra leone': 'SL',
    'liberia': 'LR',
    'libya': 'LY',
    'egypt': 'EG',
    'morocco': 'MA',
    'algeria': 'DZ',
    'tunisia': 'TN',
    'mauritania': 'MR',
    'western sahara': 'EH',
    'somalia': 'SO',
    'djibouti': 'DJ',
    'eritrea': 'ER',
    'south sudan': 'SS',
    'gabon': 'GA',
    'equatorial guinea': 'GQ',
    'sao tome and principe': 'ST',
    'cape verde': 'CV',
    'comoros': 'KM',
    'madagascar': 'MG',
    'mauritius': 'MU',
    'seychelles': 'SC',
    'lesotho': 'LS',
    'eswatini': 'SZ',
    'namibia': 'NA',
}

def parse_natural_language_query(query: str) -> dict:
    """
    Parse natural language query and convert to filters.
    Rule-based parsing only (no AI/LLM).
    
    Supported patterns:
    - "young" → ages 16-24
    - "male/female" → gender filter
    - "from {country}" → country_id filter
    - "adult/teenager/senior/child" → age_group filter
    - "above {age}" → min_age filter
    - "below {age}" → max_age filter
    - "and" → combines multiple conditions
    
    Returns dict with filter keys or None if query can't be interpreted.
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
    import re
    above_match = re.search(r'above\s+(\d+)', query_lower)
    if above_match:
        filters['min_age'] = int(above_match.group(1))
    
    # Extract "below {age}" pattern
    below_match = re.search(r'below\s+(\d+)', query_lower)
    if below_match:
        filters['max_age'] = int(below_match.group(1))
    
    # Extract "over {age}" pattern (alternative to above)
    over_match = re.search(r'over\s+(\d+)', query_lower)
    if over_match:
        filters['min_age'] = int(over_match.group(1))
    
    # Extract "under {age}" pattern (alternative to below)
    under_match = re.search(r'under\s+(\d+)', query_lower)
    if under_match:
        filters['max_age'] = int(under_match.group(1))
    
    # Extract "from {country}" pattern
    from_match = re.search(r'from\s+(\w+(?:\s+\w+)?)', query_lower)
    if from_match:
        country_name = from_match.group(1)
        # Try to match country name to ID
        country_id = COUNTRY_NAME_TO_ID.get(country_name)
        if country_id:
            filters['country_id'] = country_id
        else:
            # Try partial match
            for name, cid in COUNTRY_NAME_TO_ID.items():
                if country_name in name or name in country_name:
                    filters['country_id'] = cid
                    break
    
    # If no filters were extracted, return None
    if not filters:
        return None
    
    return filters


# FastAPI app
app = FastAPI(
    title="HNG Stage 1 - Profile API",
    description="API for creating and managing profiles with external API integration",
    version="1.0.0"
)

# CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.exception_handler(ValueError)
async def value_error_handler(request: Request, exc: ValueError):
    return JSONResponse(
        status_code=422,
        content={"status": "error", "message": "Invalid type"}
    )


@app.exception_handler(HTTPException)
async def http_exception_handler(request: Request, exc: HTTPException):
    """Custom handler to ensure error format matches requirements"""
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
    """Handle Pydantic validation errors"""
    return JSONResponse(
        status_code=422,
        content={"status": "error", "message": "Invalid type"}
    )


# POST /api/profiles - Create profile
@app.post("/api/profiles", response_model=None, status_code=201)
async def create_profile(request: CreateProfileRequest):
    # Validate name
    if not request.name or not isinstance(request.name, str):
        raise HTTPException(
            status_code=400,
            detail={"status": "error", "message": "Name is required and must be a non-empty string"}
        )
    
    # Check if profile with this name already exists (case-insensitive)
    db = next(get_db())
    existing_profile = db.query(ProfileDB).filter(ProfileDB.name == request.name.lower().strip()).first()
    
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
    
    # Validate Genderize response
    if genderize_data.get('gender') is None or genderize_data.get('count', 0) == 0:
        raise HTTPException(
            status_code=502,
            detail={"status": "error", "message": "Genderize returned an invalid response"}
        )
    
    # Validate Agify response
    if agify_data.get('age') is None:
        raise HTTPException(
            status_code=502,
            detail={"status": "error", "message": "Agify returned an invalid response"}
        )
    
    # Validate Nationalize response
    countries = nationalize_data.get('country', [])
    if not countries:
        raise HTTPException(
            status_code=502,
            detail={"status": "error", "message": "Nationalize returned an invalid response"}
        )
    
    # Extract and classify data
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
        country_name="",  # Will be filled by external API mapping
        country_probability=country_probability,
        created_at=datetime.now(timezone.utc)
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


# GET /api/profiles/search - Natural language search (must come before /{id} route)
@app.get("/api/profiles/search", response_model=SuccessResponseList)
async def search_profiles(
    q: str = Query(..., description="Natural language query string"),
    page: int = Query(1, ge=1, description="Page number (default: 1)"),
    limit: int = Query(10, ge=1, le=50, description="Results per page (default: 10, max: 50)")
):
    # Parse natural language query
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
    
    # Get total count before pagination
    total = query.count()
    
    # Apply pagination
    offset = (page - 1) * limit
    profiles = query.offset(offset).limit(limit).all()
    
    return SuccessResponseList(
        status="success",
        page=page,
        limit=limit,
        total=total,
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


# GET /api/profiles/{id} - Get single profile
@app.get("/api/profiles/{profile_id}", response_model=SuccessResponseSingle)
async def get_profile(profile_id: str):
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


# GET /api/profiles - Get all profiles with optional filters, sorting, and pagination
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
    limit: int = Query(10, ge=1, le=50, description="Results per page (default: 10, max: 50)")
):
    db = next(get_db())
    query = db.query(ProfileDB)
    
    # Apply filters (case-insensitive)
    if gender:
        query = query.filter(ProfileDB.gender.ilike(gender))
    if country_id:
        query = query.filter(ProfileDB.country_id.ilike(country_id))
    if age_group:
        query = query.filter(ProfileDB.age_group.ilike(age_group))
    
    # Apply numeric filters
    if min_age is not None:
        query = query.filter(ProfileDB.age >= min_age)
    if max_age is not None:
        query = query.filter(ProfileDB.age <= max_age)
    if min_gender_probability is not None:
        query = query.filter(ProfileDB.gender_probability >= min_gender_probability)
    if min_country_probability is not None:
        query = query.filter(ProfileDB.country_probability >= min_country_probability)
    
    # Get total count before pagination
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
    offset = (page - 1) * limit
    profiles = query.offset(offset).limit(limit).all()
    
    return SuccessResponseList(
        status="success",
        page=page,
        limit=limit,
        total=total,
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


# DELETE /api/profiles/{id} - Delete profile
@app.delete("/api/profiles/{profile_id}", status_code=204)
async def delete_profile(profile_id: str):
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


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
