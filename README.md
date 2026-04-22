# HNG Stage 1 & 2 - Profile API

A FastAPI-based REST API that integrates with external APIs (Genderize, Agify, Nationalize) to create and manage profiles with persistence.

## Features

### Stage 1 Features
- **External API Integration**: Fetches gender, age, and nationality data from free APIs
- **Data Persistence**: SQLite database for storing profiles
- **Idempotency**: Returns existing profile if name already exists
- **Error Handling**: Proper HTTP status codes and error messages
- **CORS Support**: Allows cross-origin requests
- **UUID v7**: Time-ordered unique identifiers
- **UTC Timestamps**: ISO 8601 format

### Stage 2 Features
- **Advanced Filtering**: Filter by gender, age_group, country_id, min_age, max_age, min_gender_probability, min_country_probability
- **Sorting**: Sort results by age, created_at, or gender_probability in ascending or descending order
- **Pagination**: Paginate results with configurable page size (max 50 per page)
- **Natural Language Search**: Query profiles using plain English (e.g., "young males from nigeria")
- **Database Seeding**: Pre-seeded with 2026 profiles from provided JSON data

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
GET /api/profiles?min_age=25&max_age=50&sort_by=age&order=desc&page=1&limit=10
```

**Success Response (200):**
```json
{
  "status": "success",
  "page": 1,
  "limit": 10,
  "total": 2026,
  "data": [
    {
      "id": "id-1",
      "name": "emmanuel",
      "gender": "male",
      "gender_probability": 0.99,
      "age": 25,
      "age_group": "adult",
      "country_id": "NG",
      "country_name": "Nigeria",
      "country_probability": 0.85,
      "created_at": "2026-04-01T12:00:00Z"
    }
  ]
}
```

**Supported Query Parameters:**
- `gender`: Filter by gender (male/female)
- `age_group`: Filter by age group (child/teenager/adult/senior)
- `country_id`: Filter by country ISO code
- `min_age`: Filter by minimum age
- `max_age`: Filter by maximum age
- `min_gender_probability`: Filter by minimum gender probability
- `min_country_probability`: Filter by minimum country probability
- `sort_by`: Sort by field (age, created_at, gender_probability)
- `order`: Sort order (asc/desc)
- `page`: Page number (default: 1)
- `limit`: Results per page (default: 10, max: 50)

### 4. Natural Language Search
```http
GET /api/profiles/search?q=young males from nigeria
GET /api/profiles/search?q=females above 30
GET /api/profiles/search?q=adult males from kenya&page=1&limit=10
```

**Success Response (200):**
```json
{
  "status": "success",
  "page": 1,
  "limit": 10,
  "total": 150,
  "data": [...]
}
```

**Error Response (400):**
```json
{
  "status": "error",
  "message": "Unable to interpret query"
}
```

### 5. Delete Profile
```http
DELETE /api/profiles/{id}
```

**Success Response:** 204 No Content

## Natural Language Search Documentation

### Parsing Approach

The natural language search uses **rule-based pattern matching** (no AI or LLM). The parser converts plain English queries into structured database filters by detecting specific keywords and patterns.

### Supported Keywords and Their Mappings

| Keyword/Pattern | Mapped Filter | Example Query | Resulting Filters |
|-----------------|---------------|---------------|-------------------|
| `male` | `gender=male` | "young males" | `gender=male, min_age=16, max_age=24` |
| `female` | `gender=female` | "females above 30" | `gender=female, min_age=30` |
| `young` | `min_age=16, max_age=24` | "young people" | `min_age=16, max_age=24` |
| `adult` | `age_group=adult` | "adult males" | `gender=male, age_group=adult` |
| `teenager` | `age_group=teenager` | "teenagers above 17" | `age_group=teenager, min_age=17` |
| `senior` | `age_group=senior` | "senior women" | `gender=female, age_group=senior` |
| `child` | `age_group=child` | "young children" | `age_group=child, min_age=16, max_age=24` |
| `above {age}` | `min_age={age}` | "males above 30" | `gender=male, min_age=30` |
| `below {age}` | `max_age={age}` | "people below 25" | `max_age=25` |
| `over {age}` | `min_age={age}` | "people over 40" | `min_age=40` |
| `under {age}` | `max_age={age}` | "people under 18" | `max_age=18` |
| `from {country}` | `country_id={ISO}` | "from nigeria" | `country_id=NG` |

### How the Logic Works

1. **Query Normalization**: Convert the query to lowercase and strip whitespace
2. **Pattern Matching**: Apply regex patterns to extract keywords and values
3. **Filter Construction**: Build a dictionary of filters based on detected patterns
4. **Validation**: If no filters are extracted, return an error
5. **Database Query**: Apply filters to the database query with pagination

### Example Query Parsing

**Input**: "young males from nigeria"

**Parsing Steps**:
1. Detect "young" → Set `min_age=16, max_age=24`
2. Detect "male" → Set `gender=male`
3. Detect "from nigeria" → Look up country name "nigeria" → Set `country_id=NG`

**Resulting Filters**:
```python
{
  'gender': 'male',
  'min_age': 16,
  'max_age': 24,
  'country_id': 'NG'
}
```

### Country Name Mapping

The parser includes a mapping of 50+ African and other country names to their ISO codes. It supports:
- Exact matches: "nigeria" → "NG"
- Multi-word countries: "united states" → "US", "south africa" → "ZA"
- Partial matching for common variations

### Limitations and Edge Cases

**What the Parser Does NOT Handle:**

1. **Complex Boolean Logic**: Cannot handle "AND", "OR", "NOT" operators beyond simple keyword combination
2. **Age Ranges**: Cannot parse "between 20 and 30" - must use "above 20" and "below 30" separately
3. **Negative Filters**: Cannot handle "not from nigeria" or "excluding males"
4. **Multiple Countries**: Only extracts the first "from {country}" pattern
5. **Gender Combinations**: "male and female" will only match the last detected gender
6. **Synonyms**: Does not understand "kids" (use "children"), "guys" (use "males"), "ladies" (use "females")
7. **Misspellings**: Requires exact keyword matching - "nigera" will not match "nigeria"
8. **Contextual Understanding**: "young seniors" is semantically invalid but will still apply both filters
9. **Probability Filters**: Cannot parse "high confidence" or "low probability" - use numeric filters instead
10. **Date/Time Queries**: Cannot parse "profiles created this week" or "recent profiles"

**Edge Cases:**

- **Conflicting Age Filters**: "young adults above 30" will apply both `min_age=16, max_age=24` AND `min_age=30`, resulting in no matches
- **Unknown Countries**: "from mars" will not be recognized and no country filter will be applied
- **Empty Queries**: Returns 400 error with "Unable to interpret query"
- **Case Sensitivity**: Parser is case-insensitive, but country names must match the mapping
- **Partial Words**: "m" will not match "male" - full keywords are required

**Supported Query Combinations:**

✅ **Works**:
- "young males from nigeria"
- "females above 30"
- "adult males from kenya"
- "teenagers above 17"
- "people from angola"

❌ **Does Not Work**:
- "people between 20 and 30" (use min_age/max_age)
- "not from nigeria" (negative filters not supported)
- "males or females" (boolean OR not supported)
- "high confidence profiles" (use min_gender_probability)

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
