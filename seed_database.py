"""
Seed database with profiles from seed_profiles.json
"""

import json
import time
from datetime import datetime, timezone
from sqlalchemy import create_engine, Column, String, Float, Integer, DateTime
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


# UUID v7 implementation
def generate_uuid_v7() -> str:
    """Generate UUID v7 (time-ordered)"""
    timestamp_ms = int(time.time() * 1000)
    
    timestamp_bytes = timestamp_ms.to_bytes(6, 'big')
    random_bytes = __import__('uuid').uuid4().bytes
    
    uuid_bytes = timestamp_bytes + random_bytes[6:]
    uuid_bytes = uuid_bytes[:6] + bytes([0x70 | (uuid_bytes[6] & 0x0f)]) + uuid_bytes[7:]
    uuid_bytes = uuid_bytes[:8] + bytes([0x80 | (uuid_bytes[8] & 0x3f)]) + uuid_bytes[9:]
    
    return str(__import__('uuid').UUID(bytes=uuid_bytes))


def seed_database():
    """Seed database with profiles from seed_profiles.json"""
    print("Loading seed data from seed_profiles.json...")
    
    try:
        with open('seed_profiles.json', 'r') as f:
            data = json.load(f)
            profiles_data = data.get('profiles', [])
        
        print(f"Found {len(profiles_data)} profiles in seed data")
        
        db = SessionLocal()
        
        # Track statistics
        added = 0
        skipped = 0
        errors = 0
        
        for profile_data in profiles_data:
            try:
                # Check if profile with this name already exists
                existing = db.query(ProfileDB).filter(
                    ProfileDB.name == profile_data['name'].lower().strip()
                ).first()
                
                if existing:
                    skipped += 1
                    print(f"Skipping duplicate: {profile_data['name']}")
                    continue
                
                # Create new profile
                new_profile = ProfileDB(
                    id=generate_uuid_v7(),
                    name=profile_data['name'].lower().strip(),
                    gender=profile_data['gender'],
                    gender_probability=profile_data['gender_probability'],
                    sample_size=0,  # Not provided in seed data
                    age=profile_data['age'],
                    age_group=profile_data['age_group'],
                    country_id=profile_data['country_id'],
                    country_name=profile_data['country_name'],
                    country_probability=profile_data['country_probability'],
                    created_at=datetime.now(timezone.utc)
                )
                
                db.add(new_profile)
                added += 1
                
                if added % 100 == 0:
                    print(f"Added {added} profiles so far...")
                    
            except Exception as e:
                errors += 1
                print(f"Error adding profile {profile_data.get('name', 'unknown')}: {e}")
                continue
        
        db.commit()
        
        print("\n" + "="*50)
        print("Seeding complete!")
        print(f"Added: {added} profiles")
        print(f"Skipped (duplicates): {skipped} profiles")
        print(f"Errors: {errors} profiles")
        print(f"Total profiles in database: {db.query(ProfileDB).count()}")
        print("="*50)
        
        db.close()
        
    except FileNotFoundError:
        print("Error: seed_profiles.json not found")
    except json.JSONDecodeError:
        print("Error: seed_profiles.json is not valid JSON")
    except Exception as e:
        print(f"Error: {e}")


if __name__ == "__main__":
    seed_database()
