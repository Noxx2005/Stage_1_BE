"""
Test script for Stage 3 features
Tests authentication, RBAC, and protected endpoints
"""
import httpx
import json
import os
from typing import Optional

BASE_URL = os.getenv("TEST_BASE_URL", "http://localhost:8000")

# Test configuration - set these environment variables or modify here
# For actual testing, you need valid tokens from GitHub OAuth flow
TEST_ACCESS_TOKEN = os.getenv("TEST_ACCESS_TOKEN", "")
TEST_REFRESH_TOKEN = os.getenv("TEST_REFRESH_TOKEN", "")


def get_headers(token: Optional[str] = None, include_version: bool = True) -> dict:
    """Build request headers with optional auth and API version"""
    headers = {}
    
    if include_version:
        headers["X-API-Version"] = "1"
    
    if token:
        headers["Authorization"] = f"Bearer {token}"
    
    return headers


def test_unauthenticated_access():
    """Test that protected endpoints require authentication"""
    print("=" * 60)
    print("Test 1: Unauthenticated Access")
    print("=" * 60)
    
    # Test profiles endpoint without auth
    response = httpx.get(
        f"{BASE_URL}/api/profiles",
        headers=get_headers(include_version=True),
        timeout=10
    )
    print(f"GET /api/profiles without auth - Status: {response.status_code}")
    print(f"Expected: 401, Got: {response.status_code}")
    assert response.status_code == 401, f"Expected 401, got {response.status_code}"
    print("PASSED\n")


def test_missing_api_version():
    """Test that API version header is required"""
    print("=" * 60)
    print("Test 2: Missing API Version Header")
    print("=" * 60)
    
    # Test without version header (even with auth would fail)
    response = httpx.get(
        f"{BASE_URL}/api/profiles",
        headers={"Authorization": f"Bearer {TEST_ACCESS_TOKEN}"} if TEST_ACCESS_TOKEN else {},
        timeout=10
    )
    print(f"GET /api/profiles without X-API-Version - Status: {response.status_code}")
    # Should be 400 for missing version or 401 for missing auth
    assert response.status_code in [400, 401], f"Expected 400 or 401, got {response.status_code}"
    print("PASSED\n")


def test_csrf_endpoint():
    """Test CSRF token generation endpoint"""
    print("=" * 60)
    print("Test 3: CSRF Token Endpoint")
    print("=" * 60)
    
    response = httpx.get(f"{BASE_URL}/auth/csrf", timeout=10)
    print(f"GET /auth/csrf - Status: {response.status_code}")
    
    if response.status_code == 200:
        data = response.json()
        print(f"Response: {json.dumps(data, indent=2)}")
        assert "csrf_token" in data, "Response should contain csrf_token"
        assert "csrf_token" in response.cookies, "Response should set csrf_token cookie"
        print("PASSED\n")
    else:
        print(f"Error: {response.json()}")
        print("FAILED\n")


def test_rate_limiting():
    """Test rate limiting on auth endpoints (10/min)"""
    print("=" * 60)
    print("Test 4: Rate Limiting")
    print("=" * 60)
    
    print("Sending 12 requests to /auth/csrf to test rate limiting...")
    
    success_count = 0
    rate_limited = False
    
    for i in range(12):
        response = httpx.get(f"{BASE_URL}/auth/csrf", timeout=10)
        if response.status_code == 200:
            success_count += 1
        elif response.status_code == 429:
            rate_limited = True
            print(f"Request {i+1}: Rate limited (429)")
            break
        else:
            print(f"Request {i+1}: Unexpected status {response.status_code}")
    
    print(f"Successful requests before rate limit: {success_count}")
    print(f"Rate limited: {rate_limited}")
    
    if rate_limited and success_count <= 10:
        print("PASSED - Rate limiting working correctly\n")
    else:
        print("WARNING - Rate limiting may not be configured correctly\n")


def test_authenticated_endpoints():
    """Test authenticated access to profile endpoints"""
    print("=" * 60)
    print("Test 5: Authenticated Endpoints")
    print("=" * 60)
    
    if not TEST_ACCESS_TOKEN:
        print("SKIPPED - No TEST_ACCESS_TOKEN provided")
        print("To test authenticated endpoints, set TEST_ACCESS_TOKEN env var\n")
        return
    
    headers = get_headers(token=TEST_ACCESS_TOKEN, include_version=True)
    
    # Test GET /api/profiles
    response = httpx.get(
        f"{BASE_URL}/api/profiles",
        headers=headers,
        timeout=10
    )
    print(f"GET /api/profiles - Status: {response.status_code}")
    
    if response.status_code == 200:
        data = response.json()
        print(f"Total profiles: {data.get('total', 0)}")
        print(f"Page: {data.get('page', 1)}/{data.get('total_pages', 1)}")
        print("PASSED\n")
    else:
        print(f"Error: {response.json()}")
        print("FAILED\n")


def test_profile_search():
    """Test natural language search endpoint"""
    print("=" * 60)
    print("Test 6: Natural Language Search")
    print("=" * 60)
    
    if not TEST_ACCESS_TOKEN:
        print("SKIPPED - No TEST_ACCESS_TOKEN provided\n")
        return
    
    headers = get_headers(token=TEST_ACCESS_TOKEN, include_version=True)
    
    test_queries = [
        "young males from nigeria",
        "females above 30",
        "adults from kenya"
    ]
    
    for query in test_queries:
        response = httpx.get(
            f"{BASE_URL}/api/profiles/search",
            params={"q": query, "limit": 5},
            headers=headers,
            timeout=10
        )
        print(f"Query: '{query}' - Status: {response.status_code}")
        
        if response.status_code == 200:
            data = response.json()
            print(f"  Total matches: {data.get('total', 0)}")
        elif response.status_code == 400:
            print(f"  Unable to interpret query")
        else:
            print(f"  Error: {response.json().get('message', 'Unknown error')}")
    
    print()


def test_profile_export():
    """Test profile export endpoint"""
    print("=" * 60)
    print("Test 7: Profile Export")
    print("=" * 60)
    
    if not TEST_ACCESS_TOKEN:
        print("SKIPPED - No TEST_ACCESS_TOKEN provided\n")
        return
    
    headers = get_headers(token=TEST_ACCESS_TOKEN, include_version=True)
    
    response = httpx.get(
        f"{BASE_URL}/api/profiles/export",
        params={"format": "csv"},
        headers=headers,
        timeout=10
    )
    print(f"GET /api/profiles/export - Status: {response.status_code}")
    
    if response.status_code == 200:
        content_type = response.headers.get("content-type", "")
        content_disposition = response.headers.get("content-disposition", "")
        
        print(f"Content-Type: {content_type}")
        print(f"Content-Disposition: {content_disposition}")
        
        # Check if it's a valid CSV
        lines = response.text.split("\n")
        print(f"CSV lines: {len(lines)}")
        print(f"Header: {lines[0] if lines else 'N/A'}")
        print("PASSED\n")
    else:
        print(f"Error: {response.json()}")
        print("FAILED\n")


def test_token_refresh():
    """Test token refresh endpoint"""
    print("=" * 60)
    print("Test 8: Token Refresh")
    print("=" * 60)
    
    if not TEST_REFRESH_TOKEN:
        print("SKIPPED - No TEST_REFRESH_TOKEN provided\n")
        return
    
    response = httpx.post(
        f"{BASE_URL}/auth/refresh",
        json={"refresh_token": TEST_REFRESH_TOKEN},
        timeout=10
    )
    print(f"POST /auth/refresh - Status: {response.status_code}")
    
    if response.status_code == 200:
        data = response.json()
        print(f"New access token received: {'access_token' in data}")
        print(f"New refresh token received: {'refresh_token' in data}")
        print("PASSED\n")
    else:
        print(f"Error: {response.json()}")
        print("FAILED (may be expected if token is expired)\n")


def test_logout():
    """Test logout endpoint"""
    print("=" * 60)
    print("Test 9: Logout")
    print("=" * 60)
    
    # Test logout with invalid token (should still succeed)
    response = httpx.post(
        f"{BASE_URL}/auth/logout",
        json={"refresh_token": "invalid_token"},
        timeout=10
    )
    print(f"POST /auth/logout with invalid token - Status: {response.status_code}")
    
    if response.status_code == 200:
        data = response.json()
        print(f"Response: {data}")
        print("PASSED\n")
    else:
        print(f"Error: {response.json()}")
        print("FAILED\n")


def test_oauth_flow_initiation():
    """Test OAuth flow initiation"""
    print("=" * 60)
    print("Test 10: OAuth Flow Initiation")
    print("=" * 60)
    
    response = httpx.get(
        f"{BASE_URL}/auth/github",
        follow_redirects=False,
        timeout=10
    )
    print(f"GET /auth/github - Status: {response.status_code}")
    
    if response.status_code in [302, 307]:
        location = response.headers.get("location", "")
        print(f"Redirects to GitHub: {'github.com' in location}")
        print(f"Contains PKCE: {'code_challenge' in location}")
        print("PASSED\n")
    else:
        print(f"Unexpected status code")
        print("FAILED\n")


if __name__ == "__main__":
    print("\n" + "=" * 60)
    print("INSIGHTA LABS+ STAGE 3 TEST SUITE")
    print("=" * 60 + "\n")
    
    print(f"Base URL: {BASE_URL}")
    print(f"Access Token: {'Set' if TEST_ACCESS_TOKEN else 'Not set'}")
    print(f"Refresh Token: {'Set' if TEST_REFRESH_TOKEN else 'Not set'}")
    print()
    
    try:
        # Tests that don't require authentication
        test_unauthenticated_access()
        test_missing_api_version()
        test_csrf_endpoint()
        test_oauth_flow_initiation()
        
        # Rate limiting test (be careful, this sends many requests)
        # test_rate_limiting()
        
        # Tests that require authentication
        test_authenticated_endpoints()
        test_profile_search()
        test_profile_export()
        
        # Token management tests
        test_token_refresh()
        test_logout()
        
        print("=" * 60)
        print("TEST SUITE COMPLETED")
        print("=" * 60)
        
    except httpx.ConnectError:
        print(f"\nERROR: Could not connect to {BASE_URL}")
        print("Make sure the server is running:")
        print("  uvicorn main:app --host 0.0.0.0 --port 8000")
    except Exception as e:
        print(f"\nERROR: {e}")
        raise
