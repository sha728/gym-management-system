# Apex Gym Management System

This is my gym management web app that I built during my internship. It's a pretty comprehensive system where gym members can browse programs, book sessions, and manage their memberships, while admins handle all the day-to-day operations.

The whole thing runs on React for the frontend, .NET for the API, and PostgreSQL for the database. I spent about two weeks building this from scratch.

---

## What it does

**For Members:**
- Browse fitness programs and trainers
- View available sessions and book classes
- Track your bookings and manage your membership
- Clean, responsive interface that works on mobile

**For Admins:**
- Create and manage fitness programs
- Set up trainer schedules and availability
- Review membership requests (approve/reject)
- Monitor all bookings and member activity
- Comprehensive dashboard with key metrics

---

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | React 19, Vite, Bootstrap 5 |
| Backend | C# .NET 10 Web API |
| Database | PostgreSQL 17 |
| Auth | JWT with role-based access |
| Containers | Docker, Docker Compose |

---

## Getting Started

### Option 1: Docker (Recommended)

This is the easiest way - just need Docker Desktop installed.

**1. Set up your environment**

```bash
cp .env.example .env
```

The defaults work fine for local development. Just set your preferred admin password.

**2. Start everything**

```bash
docker compose up --build
```

This spins up PostgreSQL, the .NET API, and the React frontend all together. The backend automatically waits for the database and creates its schema on first run.

**3. Open the app**

- Frontend: http://localhost
- API docs: http://localhost:8080/swagger

### Option 2: Local Development

If you want to run everything locally:

**Prerequisites:**
- .NET 10 SDK
- Node.js 22+
- PostgreSQL 17

**Start the backend:**
```bash
cd backend/GymApi
dotnet run --launch-profile http
```
The database schema is created automatically on first run — no separate migration step needed.

**Start the frontend:**
```bash
cd frontend
npm install
npm run dev
```

---

## Default Login

The system creates an admin account automatically:

| Role | Email | Password |
|---|---|---|
| Admin | admin@gym.com | (whatever you set in .env) |

New members can register themselves - no approval needed.

---

## Testing

I've included tests for both frontend and backend:

```bash
# Backend tests
cd GymApi.Tests
dotnet test

# Frontend tests  
cd frontend
npm test
```

---

## Project Structure

```
AssignmentGym/
├── backend/GymApi/
│   ├── Controllers/      # API endpoints
│   ├── Models/          # Database entities
│   ├── DTOs/            # API request/response models
│   └── Data/            # Database context and seeding
├── frontend/src/
│   ├── api/             # API client wrapper
│   ├── components/      # Reusable UI components
│   ├── context/         # React context (auth state)
│   ├── pages/           # All the app pages
│   └── __tests__/       # Frontend tests
├── GymApi.Tests/        # Backend unit/integration tests
└── docker-compose.yml   # Container orchestration
```

---

## Environment Variables

Copy `.env.example` to `.env` and customize:

| Variable | Description |
|---|---|
| `POSTGRES_DB` | Database name |
| `POSTGRES_USER` | Database username |
| `POSTGRES_PASSWORD` | Database password |
| `JWT_KEY` | JWT signing secret (32+ chars) |
| `ADMIN_PASSWORD` | Admin account password |

---

## Features I'm Proud Of

- **Clean, modern UI** - Spent time making it look professional
- **Role-based authentication** - Members and admins see different features
- **Comprehensive admin tools** - Full CRUD operations for all resources
- **Mobile-responsive** - Works great on phones and tablets
- **Docker-ready** - One command to get everything running
- **Well-tested** - Both frontend and backend test coverage

This was a great learning project that really helped me understand full-stack development. Hope you find it useful!
