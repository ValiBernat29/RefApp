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

_Description: Overview of weekly matches, quick stats, and pending assignments._

</details>

<details>
<summary><b>2. Intelligent Referee Assignment View</b></summary>
<br/>

<img width="1838" height="883" alt="image" src="https://github.com/user-attachments/assets/0d1be734-b13b-4ecc-9ce4-c6e93e8e76a6" />

_Description: The assignment interface displaying suitability scores, distance calculations, and role preference indicators._

</details>

<details>
<summary><b>3. Conflict Validation in Action</b></summary>
<br/>

<img width="1833" height="879" alt="image" src="https://github.com/user-attachments/assets/fb19ef70-f455-45ff-afca-aaffaf167f62" />

_Description: System preventing a double-booking (hard conflict) and warning about same-day travel (soft conflict)._

</details>

<details>
<summary><b>4. Referee Profile & Preferences</b></summary>
<br/>

<img width="1837" height="873" alt="image" src="https://github.com/user-attachments/assets/c65bb648-79ff-4349-9eb3-880dbcc02a3c" />

_Description: Interface for referees to set their home location._

</details>

---

## 🛠️ Technology Stack

- **Backend**: C# / ASP.NET Core 8 MVC
- **Database**: Entity Framework Core. SQLite in Development, SQL Server (Azure) in production
- **Authentication**: ASP.NET Core Identity, with `Board` and `Referee` roles
- **Frontend**: a **Vue 3** single-page app (Vite, Vue Router, Tailwind CSS v3), served by the same ASP.NET project under `/app`. Every screen is a Vue component. The only server-rendered pages are the built-in **ASP.NET Identity** pages (login, register, account management), which stay as Razor on purpose.
- **Architecture**: Service-oriented (`AssignmentService`, `RefereeScoringService`, `SmartAllocationEngine`, `GeocodingService`). The Vue app talks to JSON endpoints under `/api` on the same origin and reuses the Identity cookie and antiforgery token. `/api` returns 401/403 instead of redirecting to the login page.

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
   The Vue app is not part of `dotnet build`. Build it once, and again after any change in `ClientApp/`. The output goes to `wwwroot/dist` (git-ignored). `dotnet run` fails with a clear message if it has not been built.

   ```bash
   cd ClientApp
   npm ci
   npm run build
   ```

4. **Run the Application:**

   ```bash
   dotnet run
   ```

   On startup the app applies pending migrations and creates the roles. It seeds demo teams and fixtures once per league, and only for a league that has no teams yet, so existing data is never recreated or overwritten. Team coordinates are looked up in the background after startup (OpenStreetMap Nominatim, one request per second). In Development it also creates two demo accounts (`board@example.com` and `referee@example.com`, see `DbInitializer.cs` for the passwords).

5. **Access the App:**
   Open your browser and navigate to `https://localhost:7082` or `http://localhost:5031` (check your `Properties/launchSettings.json`).

---

## ⚙️ Configuration

| Setting                                     | Purpose                                                                                                                  |
| ------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| `ConnectionStrings:DefaultConnection`       | SQLite connection (Development). Defaults to `Data Source=app.db`.                                                       |
| `ConnectionStrings:ProductionConnection`    | SQL Server connection (any non-Development environment).                                                                 |
| `Seed:BoardEmail`, `Seed:BoardPassword`     | Creates the initial Board account. **Required in production**, because no demo accounts are created outside Development. |
| `Seed:RefereeEmail`, `Seed:RefereePassword` | Optional. Creates a referee account the same way.                                                                        |

In Azure App Service, set these as application settings. Use double underscores for nesting, for example `Seed__BoardPassword`. Never commit real passwords.

## 🚢 Deployment

Pushing to `main` runs `.github/workflows/main_refapp.yml`, which builds the Vue frontend (`npm ci && npm run build`), runs `dotnet publish`, and deploys to the Azure Web App `refapp`.

---

## 📁 Project Structure

- `Controllers/` - There are no page controllers any more; the screens are Vue components.
  - `Api/` - the JSON API the Vue app calls. Board endpoints derive from `BoardApiControllerBase` (Board role + antiforgery header), referee endpoints from `RefereeApiControllerBase`. `/api` answers 401/403 instead of redirecting to the login page.
  - `SpaController` - serves the Vue app shell for every URL under `/app`
  - `LegacyRedirectController` - sends the old Razor URLs (such as `/Teams`) to their Vue equivalents
  - `HomeController` - sends signed-in users to `/app`, everyone else to the login page
- `Models/` - Entity classes and database schema definitions.
- `ViewModels/` - `RefereeOption`, the scored candidate used by the assignment logic.
- `Views/` - only the Vue app shell (`Spa/Index.cshtml`), the shared layout used by the Identity pages, and the error page.
- `Services/` - Core business logic:
  - `AssignmentService.cs` - Rules for who can be offered for a match, validation (double-booking, city conflicts) and saving.
  - `SmartAllocationEngine.cs` - Bulk auto-allocation solver.
  - `RefereeScoringService.cs` - Suitability scoring for the manual assign screen.
  - `GeocodingService.cs` - Nominatim geocoding and distance calculations.
- `Data/` - EF Core `DbContext` and seeding logic (`DbInitializer`).
- `ClientApp/` - Vue 3 + Vite + Tailwind source. `npm run build` writes to `wwwroot/dist`.
  - `src/api/` - the only place that calls the server; `src/pages/` - one component per screen; `src/router.js` - routes and role guards; `src/components/ui/` - shared building blocks (page shell, forms, dialogs).
- `wwwroot/` - Static files. `wwwroot/dist` is generated (content-hashed file names plus a `manifest.json` that `ViteAssets` reads) and git-ignored.

---
