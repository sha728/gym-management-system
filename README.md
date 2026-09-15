# Apex Gym & Fitness Management System

A full-stack web application for managing a gym. Members can browse fitness programs, view trainers and sessions, book classes, and manage their membership. Admins handle the day-to-day operations: creating programs and sessions, managing trainer availability, reviewing membership requests and monitoring bookings.

Built as a two-week internship assignment using React, .NET 10 Web API and PostgreSQL.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | React 19, Vite, Bootstrap 5 |
| Backend | C# .NET 10 Web API |
| Database | PostgreSQL 17 |
| Auth | JWT (HS256, role-based) |
| Containers | Docker, Docker Compose |

---

## Running with Docker

This is the easiest way to get everything up. You just need Docker Desktop installed.

**1. Copy the env file and fill in your values**

```
cp .env.example .env
```

The defaults in `.env.example` work fine for local testing. The only thing you need to decide is the admin password.

**2. Start everything**

```
docker compose up --build
```

This starts PostgreSQL, the .NET API and the React frontend together. The backend waits for Postgres to be healthy before it starts, and applies any pending migrations automatically on first boot.

**3. Open the app**

- Frontend: `http://localhost`
- API / Swagger: `http://localhost:8080/swagger`

**Stopping**

```
docker compose down
```

If you also want to wipe the database volume:

```
docker compose down -v
```

---

## Running locally

If you prefer to run things without Docker, you'll need:

- .NET 10 SDK
- Node.js 22
- PostgreSQL 17 running locally on port 5432

**Backend**

```
cd backend/GymApi
dotnet run --launch-profile http
```

The API will be at `http://localhost:5256` and Swagger at `http://localhost:5256/swagger`.

Before the first run, apply the database migrations:

```
dotnet ef database update
```

**Frontend**

```
cd frontend
npm install
npm run dev
```

The app will be at `http://localhost:5173`. The Vite dev server automatically proxies `/api` requests to the backend, so no CORS configuration is needed.

---

## Default accounts

The admin account is created automatically the first time the backend starts, using the `ADMIN_PASSWORD` value from your `.env` file.

| Role | Email | Password |
|---|---|---|
| Admin | admin@gym.com | whatever you set as `ADMIN_PASSWORD` |

Members register themselves through the app at `/register`. There is no admin approval step for registration.

---

## Running tests

**Backend**

```
cd GymApi.Tests
dotnet test
```

**Frontend**

```
cd frontend
npm test
```

---

## Environment variables

All configuration is done through the `.env` file in the project root. Copy `.env.example` to get started.

| Variable | What it's for |
|---|---|
| `POSTGRES_DB` | Database name |
| `POSTGRES_USER` | Database user |
| `POSTGRES_PASSWORD` | Database password |
| `JWT_KEY` | JWT signing key, needs to be at least 32 characters |
| `ADMIN_PASSWORD` | Password for the auto-seeded admin account |

---

## Project structure

```
AssignmentGym/
  backend/
    GymApi/
      Controllers/      One controller per resource
      Models/           EF Core entity models
      DTOs/             Request and response shapes
      Data/             DbContext, relationships and seeder
      Migrations/       EF Core migration history
  frontend/
    src/
      api/              Authed fetch wrapper for every API endpoint
      components/       Navbar, Layout, ProtectedRoute
      context/          AuthContext (JWT state, login, logout)
      pages/            Member and Admin pages
  GymApi.Tests/         Backend unit and integration tests
  docker-compose.yml
  .env.example
```
