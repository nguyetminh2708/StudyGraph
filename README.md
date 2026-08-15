# StudyGraph

A learning management system (LMS) demonstrating the multi-model database **ArangoDB**: learning content is stored as documents, while learning relationships (enrollments, completions, prerequisites, ratings) are stored as a graph. The core feature is **course recommendation via graph traversal**. A SQL Server counterpart is included for experimental comparison.

- Backend: ASP.NET Core Web API (.NET 8) with the ArangoDBNetStandard driver
- Frontend: React 19 + Vite, talking to the API through the `X-User-Key` header
- Primary database: ArangoDB 3.11 Community (Windows installer, no Docker required)
- Reference database: SQL Server LocalDB (ships with Visual Studio)

## Project structure

| Folder | Purpose |
|---|---|
| `StudyGraph.Api` | Web API: controllers, repositories (where the AQL lives), services |
| `StudyGraphUI` | React frontend (login, courses, lessons, quizzes, progress, admin) |
| `StudyGraph.Seeder` | Generates sample data and loads it into ArangoDB |
| `StudyGraph.SqlImporter` | Reads `seed-output/*.json` and loads it into SQL Server (same dataset) |
| `StudyGraph.Benchmark` | Measures Q1, Q2, Q3 on both engines, writes `bench-output/*.csv` |
| `schema/` | arangosh script that creates collections, indexes and the named graph |
| `sql/` | SQL Server schema script and the three reference queries |
| `seed-output/` | Intermediate JSON files so both engines share one identical dataset |

## 1. Prerequisites

| Software | Version | Notes |
|---|---|---|
| .NET SDK | 8.0 or later | `dotnet --version` |
| Node.js | 20.19 or later | `node -v` |
| Yarn | 1.x | available via `corepack enable` |
| ArangoDB Community | 3.11.x for Windows | download at `download.arangodb.com/arangodb311/Community/Windows/` |
| SQL Server LocalDB | ships with Visual Studio | only needed for the benchmark part |

When installing ArangoDB, set the root password to `Study2026` (this matches `appsettings.Development.json` and the Seeder). ArangoDB runs as a Windows service; open `http://localhost:8529` and you should see the Web UI.

## 2. First-time setup

### 2.1. Create the ArangoDB database and schema

Open arangosh (available in the Start Menu after installing ArangoDB):

```
& "C:\Program Files\ArangoDB3 3.11.14\usr\bin\arangosh.exe" --server.password Study2026
```

Inside arangosh, create the database and run the schema script:

```js
db._createDatabase("studygraph");
db._useDatabase("studygraph");
// paste the contents of schema/01_create_schema.js here
```

Verify with `db._collections()`: you should see 8 collections (users, courses, lessons, quizzes, enrolled_in, completed, prerequisite_of, rated).

### 2.2. Load sample data (seed)

```
cd StudyGraph.Seeder
dotnet run
```

Expected output: `Seed xong: 12 courses, 60 lessons, 51 users, ...`

Options:

```
dotnet run -- --sf vua      # 5,000 users / 200 courses (for benchmarking)
dotnet run -- --sf lon      # 50,000 users / 1,000 courses
dotnet run -- --json        # also export seed-output/*.json for SqlImporter
```

The Seeder wipes old data before loading, so it is safe to run repeatedly. Random is fixed at seed 42, so every run produces exactly the same dataset.

### 2.3. Install UI dependencies

```
cd StudyGraphUI
yarn
```

## 3. Running the project

Open 2 terminals:

```
# Terminal 1: API (port 5133, Swagger at /swagger)
dotnet run --project StudyGraph.Api

# Terminal 2: UI (port 5173, proxies /api to 5133)
cd StudyGraphUI
yarn start
```

Open `http://localhost:5173` and log in with an email (no password required):

| Account | Role |
|---|---|
| `admin@studygraph.dev` | Admin: create, edit, delete courses |
| `user001@studygraph.dev` to `user050@studygraph.dev` | Student |

Suggested demo accounts: the "fresh" users (u001, u010, u011, u020, u021, u030...) have only started their first course, so logging in as them shows the recommendation block most clearly.

Learning rules in the app:

- Sequential learning: a lesson unlocks only after the previous one is completed
- Lessons with a quiz: click "Complete lesson" first to reveal the quiz, then score at least 80% on the quiz to actually complete the lesson
- Course progress is the percentage of completed lessons

## 4. SQL Server counterpart (experimental comparison)

```
# 0. If LocalDB is not running yet
sqllocaldb start MSSQLLocalDB

# 1. Create the StudyGraphSql database (10 tables)
sqlcmd -S "(localdb)\MSSQLLocalDB" -i sql\01_schema.sql

# 2. Export JSON from the Seeder, then load it into SQL Server
dotnet run --project StudyGraph.Seeder -- --json
cd StudyGraph.SqlImporter
dotnet run

# 3. Try the three reference queries
cd ..
sqlcmd -S "(localdb)\MSSQLLocalDB" -d StudyGraphSql -i sql\02_queries_Q1_Q2_Q3.sql
```

Both engines read from the same `seed-output/*.json` files, so the datasets are strictly identical.

## 5. Benchmark

```
dotnet run -c Release --project StudyGraph.Benchmark -- --sf nho
dotnet run -c Release --project StudyGraph.Benchmark -- --sf vua --iter 100
```

Preconditions: ArangoDB has been seeded and SqlImporter has been run at the same scale factor. Results are printed to the console and written to `StudyGraph.Benchmark/bench-output/bench_<sf>.csv`. Must be run in Release configuration.

## 6. Troubleshooting

| Symptom | Fix |
|---|---|
| UI shows network errors, Swagger does not open | The API is not running, or it uses a port other than 5133 (check `Properties/launchSettings.json` and adjust the proxy in `StudyGraphUI/vite.config.js` to match) |
| Cannot reach `localhost:8529` | Open Windows Services, find the ArangoDB service and press Start |
| sqlcmd reports "delay in opening server connection" | Run `sqllocaldb start MSSQLLocalDB` |
| Login says the email does not exist | Data has not been seeded, or the `studygraph` database has no schema (redo sections 2.1 and 2.2) |
| .NET build fails because the .exe file is locked | Stop the running API (Ctrl+C) and build again |
