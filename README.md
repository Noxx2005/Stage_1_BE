# HNG Stage 1 - Profile API

A FastAPI-based REST API that integrates with external APIs (Genderize, Agify, Nationalize) to create and manage profiles with persistence.

## Features

- **External API Integration**: Fetches gender, age, and nationality data from free APIs
- **Data Persistence**: SQLite database for storing profiles
- **Idempotency**: Returns existing profile if name already exists
- **Filtering**: Query profiles by gender, country_id, or age_group
- **Error Handling**: Proper HTTP status codes and error messages
- **CORS Support**: Allows cross-origin requests
- **UUID v7**: Time-ordered unique identifiers
- **UTC Timestamps**: ISO 8601 format

## API Endpoints

### 1. Create Profile
```http
POST /api/profiles
Content-Type: application/json

{
  "name": "ella"
}
```

**Success Response (201):**
```json
{
  "status": "success",
  "data": {
    "id": "b3f9c1e2-7d4a-4c91-9c2a-1f0a8e5b6d12",
    "name": "ella",
    "gender": "female",
    "gender_probability": 0.99,
    "sample_size": 1234,
    "age": 46,
    "age_group": "adult",
    "country_id": "DRC",
    "country_probability": 0.85,
    "created_at": "2026-04-01T12:00:00Z"
  }
}
```

**Existing Profile Response:**
```json
{
  "status": "success",
  "message": "Profile already exists",
  "data": { ...existing profile... }
}
```

### 2. Get Single Profile
```http
GET /api/profiles/{id}
```

**Success Response (200):**
```json
{
  "status": "success",
  "data": {
    "id": "...",
    "name": "emmanuel",
    "gender": "male",
    "gender_probability": 0.99,
    "sample_size": 1234,
    "age": 25,
    "age_group": "adult",
    "country_id": "NG",
    "country_probability": 0.85,
    "created_at": "2026-04-01T12:00:00Z"
  }
}
```

### 3. Get All Profiles (with optional filters)
```http
GET /api/profiles
GET /api/profiles?gender=male
GET /api/profiles?country_id=NG
GET /api/profiles?age_group=adult
GET /api/profiles?gender=male&country_id=NG&age_group=adult
```

**Success Response (200):**
```json
{
  "status": "success",
  "count": 2,
  "data": [
    {
      "id": "id-1",
      "name": "emmanuel",
      "gender": "male",
      "age": 25,
      "age_group": "adult",
      "country_id": "NG"
    },
    {
      "id": "id-2",
      "name": "sarah",
      "gender": "female",
      "age": 28,
      "age_group": "adult",
      "country_id": "US"
    }
  ]
}
```

### 4. Delete Profile
```http
DELETE /api/profiles/{id}
```

**Success Response:** 204 No Content

## Error Responses

All errors follow this structure:
```json
{
  "status": "error",
  "message": "<error message>"
}
```

| Status Code | Scenario |
|------------|----------|
| 400 | Missing or empty name |
| 422 | Invalid type (e.g., name is a number) |
| 404 | Profile not found |
| 502 | External API returned invalid response |

## Classification Rules

- **Age Groups:**
  - 0-12: `child`
  - 13-19: `teenager`
  - 20-59: `adult`
  - 60+: `senior`

- **Nationality:** Country with highest probability from Nationalize API

## Installation

1. **Clone the repository:**
```bash
git clone <your-repo-url>
cd Stage_1_BE
```

2. **Create virtual environment (recommended):**
```bash
python -m venv venv

# On Windows:
venv\Scripts\activate

# On macOS/Linux:
source venv/bin/activate
```

3. **Install dependencies:**
```bash
pip install -r requirements.txt
```

## Running the Application

### Development Mode
```bash
uvicorn main:app --reload
```

### Production Mode
```bash
uvicorn main:app --host 0.0.0.0 --port 8000
```

The API will be available at `http://localhost:8000`

## Testing the API

You can test using curl, Postman, or the interactive docs:

**Interactive API Documentation:**
- Swagger UI: `http://localhost:8000/docs`
- ReDoc: `http://localhost:8000/redoc`

**Example curl commands:**

```bash
# Create a profile
curl -X POST "http://localhost:8000/api/profiles" \
  -H "Content-Type: application/json" \
  -d '{"name": "ella"}'

# Get all profiles
curl "http://localhost:8000/api/profiles"

# Get filtered profiles
curl "http://localhost:8000/api/profiles?gender=female"

# Get single profile
curl "http://localhost:8000/api/profiles/{id}"

# Delete profile
curl -X DELETE "http://localhost:8000/api/profiles/{id}"
```

## Deployment

This application can be deployed to:
- **Vercel** (with `vercel.json`)
- **Railway**
- **Heroku**
- **AWS**
- **PXXL App**
- **Any platform supporting Python**

**Note:** Render is not accepted per submission guidelines.

### Environment Variables
None required for basic deployment.

## Technology Stack

- **Framework:** FastAPI
- **Database:** SQLite (file-based)
- **ORM:** SQLAlchemy
- **HTTP Client:** httpx
- **Pydantic:** Data validation

## External APIs Used

- **Genderize:** `https://api.genderize.io?name={name}`
- **Agify:** `https://api.agify.io?name={name}`
- **Nationalize:** `https://api.nationalize.io?name={name}`

All external APIs are free and require no API keys.

## Project Structure

```
Stage_1_BE/
├── main.py           # Main application file
├── requirements.txt  # Python dependencies
├── README.md         # This file
└── profiles.db       # SQLite database (created on first run)
```

## License

MIT

## Author

Your Name
