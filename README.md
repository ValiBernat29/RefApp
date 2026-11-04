# 🏆 RefApp - Intelligent Referee Assignment System

<div align="center">

<img width="1408" height="768" alt="image" src="https://github.com/user-attachments/assets/a246aa70-e76d-4770-b22b-a81fb5e408be" />

<br/>

**RefApp** is a comprehensive, intelligent web application designed to streamline the management and assignment of football referees. Built with ASP.NET Core MVC and styled with Tailwind CSS, it features a smart scoring algorithm that helps board members assign the right referees to the right matches.

</div>

## ✨ Key Features

### 🧠 Intelligent Assignment Algorithm
- **Suitability Scoring**: Automatically calculates a score for each referee based on distance to the match, past assignments, and role preferences.
- **Geocoding Integration**: Uses location data to calculate distances between referees and match venues.
- **Role Preferences**: Referees can specify their preferred role (Main Referee vs. Assistant Referee), giving them a score boost when assigned to their preferred position.

### 🤖 Auto-Allocation
- A constraint-based solver (`SmartAllocationEngine`) fills a date range of matches at once, respecting rank per league, unavailability, home-city conflicts, recent history and car availability.

### 🗺️ Route Map
- An interactive Leaflet map (Vue) showing referees, the match venue, the driving route and a configurable buffer zone. Referees "on the way" are highlighted and can be assigned straight from the map.

### 🛡️ Smart Conflict Validation
- **Hard Conflicts**: Prevents assigning referees who have overlapping matches at the exact same date and time.
- **Soft Warnings**: Alerts board members if a referee has another match on the same day but at a different time, allowing for travel considerations.

### 📊 Comprehensive Dashboards
- **Board Member View**: Overview of upcoming fixtures, unassigned matches, and quick actions for match management.
- **Referee View**: Personalized dashboard showing upcoming assignments, past match history, and profile management.

### ⚙️ Automated Data Ingestion
- Seamlessly imports league fixtures (e.g., Liga 4, Liga 5) directly from CSV files to populate the database via robust seeding mechanisms.

---

## 📸 Screenshots

<details>
<summary><b>1. Admin / Board Dashboard</b></summary>
<br/>

<img width="1842" height="872" alt="image" src="https://github.com/user-attachments/assets/b365e3a2-01ff-4bf0-8aba-19468a4f2f0e" />


*Description: Overview of weekly matches, quick stats, and pending assignments.*
</details>

<details>
<summary><b>2. Intelligent Referee Assignment View</b></summary>
<br/>

<img width="1838" height="883" alt="image" src="https://github.com/user-attachments/assets/0d1be734-b13b-4ecc-9ce4-c6e93e8e76a6" />


*Description: The assignment interface displaying suitability scores, distance calculations, and role preference indicators.*
</details>

<details>
<summary><b>3. Conflict Validation in Action</b></summary>
<br/>

<img width="1833" height="879" alt="image" src="https://github.com/user-attachments/assets/fb19ef70-f455-45ff-afca-aaffaf167f62" />


*Description: System preventing a double-booking (hard conflict) and warning about same-day travel (soft conflict).*
</details>

<details>
<summary><b>4. Referee Profile & Preferences</b></summary>
<br/>

<img width="1837" height="873" alt="image" src="https://github.com/user-attachments/assets/c65bb648-79ff-4349-9eb3-880dbcc02a3c" />


*Description: Interface for referees to set their home location.*
</details>

---

## 🛠️ Technology Stack

- **Backend**: C# / ASP.NET Core 8 MVC
- **Database**: Entity Framework Core. SQLite in Development, SQL Server (Azure) in production
- **Authentication**: ASP.NET Core Identity, with `Board` and `Referee` roles
- **Frontend**: Razor views, plus **Vue 3** (Vite) for the interactive pages (route map and referee assignment), styled with **Tailwind CSS v3**
- **Architecture**: Service-oriented (`AssignmentService`, `RefereeScoringService`, `SmartAllocationEngine`, `GeocodingService`). Vue pages talk to JSON endpoints on the same origin and reuse the Identity cookie and antiforgery token.

## 🚀 Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) 20 or newer (builds the Vue frontend)

### Installation

1. **Clone the repository:**
   ```bash
   git clone https://github.com/yourusername/RefApp.git
   cd RefApp
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Build the frontend:**
   The Vue pages are not part of `dotnet build`. Build them once, and again after any change in `ClientApp/`. The output goes to `wwwroot/dist` (git-ignored).
   ```bash
   cd ClientApp
   npm ci
   npm run build
   ```

4. **Run the Application:**
   ```bash
   dotnet run
   ```
   On startup the app applies pending migrations, creates the roles, and seeds the league fixtures. In Development it also creates two demo accounts (`board@example.com` and `referee@example.com`, see `DbInitializer.cs` for the passwords).

5. **Access the App:**
   Open your browser and navigate to `https://localhost:7082` or `http://localhost:5031` (check your `Properties/launchSettings.json`).

---

## ⚙️ Configuration

| Setting | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQLite connection (Development). Defaults to `Data Source=app.db`. |
| `ConnectionStrings:ProductionConnection` | SQL Server connection (any non-Development environment). |
| `Seed:BoardEmail`, `Seed:BoardPassword` | Creates the initial Board account. **Required in production**, because no demo accounts are created outside Development. |
| `Seed:RefereeEmail`, `Seed:RefereePassword` | Optional. Creates a referee account the same way. |

In Azure App Service, set these as application settings. Use double underscores for nesting, for example `Seed__BoardPassword`. Never commit real passwords.

## 🚢 Deployment

Pushing to `main` runs `.github/workflows/main_refapp.yml`, which builds the Vue frontend (`npm ci && npm run build`), runs `dotnet publish`, and deploys to the Azure Web App `refapp`.

---

## 📁 Project Structure

- `Controllers/` - MVC controllers. Board-only controllers inherit `BoardControllerBase` (which requires the `Board` role):
  - `BoardController` (`/Board`, dashboard), `MatchesController`, `TeamsController`, `UsersController`, `UnavailabilityController`, `AllocationController`, `RouteMapController`
  - `MatchAssignmentApiController` - JSON API behind the Vue assign page (`api/board/matches/{id}/assignment`)
  - `RefereeController` - the referee's own area
- `Models/` - Entity classes and database schema definitions.
- `ViewModels/` - Data transfer objects optimized for specific Views.
- `Views/` - Razor views, one folder per controller.
- `Services/` - Core business logic:
  - `AssignmentService.cs` - Rules for who can be offered for a match, validation (double-booking, city conflicts) and saving.
  - `SmartAllocationEngine.cs` - Bulk auto-allocation solver.
  - `RefereeScoringService.cs` - Suitability scoring for the manual assign screen.
  - `GeocodingService.cs` - Nominatim geocoding and distance calculations.
- `Data/` - EF Core `DbContext` and seeding logic (`DbInitializer`).
- `ClientApp/` - Vue 3 + Vite + Tailwind source. `npm run build` writes to `wwwroot/dist`.
- `wwwroot/` - Static files. `wwwroot/dist` is generated and git-ignored.

---
