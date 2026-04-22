"""
Test script for Stage 2 features
"""
import httpx
import json

BASE_URL = "http://localhost:8002"

def test_enhanced_get_profiles():
    """Test enhanced GET /api/profiles with filters, sorting, pagination"""
    print("="*60)
    print("Test 1: Enhanced GET /api/profiles")
    print("="*60)
    
    # Test basic pagination
    response = httpx.get(f"{BASE_URL}/api/profiles?page=1&limit=5", timeout=10)
    print(f"Basic pagination - Status: {response.status_code}")
    data = response.json()
    print(f"Page: {data.get('page')}, Limit: {data.get('limit')}, Total: {data.get('total')}")
    print(f"Results: {len(data.get('data', []))} profiles\n")
    
    # Test with filters
    response = httpx.get(f"{BASE_URL}/api/profiles?gender=male&country_id=NG&min_age=25", timeout=10)
    print(f"Filters (male, NG, min_age=25) - Status: {response.status_code}")
    data = response.json()
    print(f"Total matching: {data.get('total')}\n")
    
    # Test sorting
    response = httpx.get(f"{BASE_URL}/api/profiles?sort_by=age&order=desc&limit=3", timeout=10)
    print(f"Sort by age desc - Status: {response.status_code}")
    data = response.json()
    if data.get('data'):
        print(f"First profile age: {data['data'][0]['age']}")
        print(f"Last profile age: {data['data'][-1]['age']}\n")
    
    # Test numeric probability filters
    response = httpx.get(f"{BASE_URL}/api/profiles?min_gender_probability=0.95", timeout=10)
    print(f"Min gender probability 0.95 - Status: {response.status_code}")
    data = response.json()
    print(f"Total with high confidence: {data.get('total')}\n")

def test_natural_language_search():
    """Test natural language search endpoint"""
    print("="*60)
    print("Test 2: Natural Language Search")
    print("="*60)
    
    test_queries = [
        "young males from nigeria",
        "females above 30",
        "people from kenya",
        "adult males",
        "teenagers above 17"
    ]
    
    for query in test_queries:
        response = httpx.get(f"{BASE_URL}/api/profiles/search?q={query}&limit=5", timeout=10)
        print(f"Query: '{query}' - Status: {response.status_code}")
        if response.status_code == 200:
            data = response.json()
            print(f"  Total: {data.get('total')}, Page: {data.get('page')}")
        else:
            print(f"  Error: {response.json().get('message')}")
        print()
    
    # Test uninterpretable query
    response = httpx.get(f"{BASE_URL}/api/profiles/search?q=random xyz words", timeout=10)
    print(f"Uninterpretable query - Status: {response.status_code}")
    print(f"Message: {response.json().get('message')}\n")

def test_error_responses():
    """Test error responses"""
    print("="*60)
    print("Test 3: Error Responses")
    print("="*60)
    
    # Test invalid sort_by
    response = httpx.get(f"{BASE_URL}/api/profiles?sort_by=invalid_field", timeout=10)
    print(f"Invalid sort_by - Status: {response.status_code}")
    print(f"Data returned: {len(response.json().get('data', []))} profiles (should still work)\n")
    
    # Test invalid page
    response = httpx.get(f"{BASE_URL}/api/profiles?page=0", timeout=10)
    print(f"Invalid page (0) - Status: {response.status_code}")
    print(f"Error: {response.json().get('message')}\n")
    
    # Test invalid limit
    response = httpx.get(f"{BASE_URL}/api/profiles?limit=100", timeout=10)
    print(f"Invalid limit (100 > 50) - Status: {response.status_code}")
    print(f"Error: {response.json().get('message')}\n")

if __name__ == "__main__":
    try:
        test_enhanced_get_profiles()
        test_natural_language_search()
        test_error_responses()
        print("="*60)
        print("All tests completed!")
        print("="*60)
    except Exception as e:
        print(f"Error: {e}")
        print("\nMake sure the server is running on port 8002")
        print("Run: uvicorn main:app --host 0.0.0.0 --port 8002")
