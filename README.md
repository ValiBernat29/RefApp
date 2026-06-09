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

- **Backend**: C# / ASP.NET Core MVC
- **Database**: SQLite with Entity Framework Core
- **Authentication**: ASP.NET Core Identity
- **Frontend**: Razor Pages / HTML5, powered by **Tailwind CSS v3**
- **Architecture**: Service-Oriented Architecture (`GeocodingService`, `RefereeScoringService`)

## 🚀 Getting Started

### Prerequisites
- [.NET SDK](https://dotnet.microsoft.com/download)
- Node.js (for Tailwind CSS processing, if applicable)

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

3. **Database Setup:**
   Apply the migrations to create the SQLite database (`app.db`).
   ```bash
   dotnet ef database update
   ```
   *Note: The application includes a `DbInitializer` that automatically seeds administrative accounts and initial CSV fixtures upon first run.*

4. **Run the Application:**
   ```bash
   dotnet run
   ```

5. **Access the App:**
   Open your browser and navigate to `https://localhost:7082` or `http://localhost:5031` (check your `Properties/launchSettings.json`).

---

## 📁 Project Structure

- `Controllers/` - MVC Controllers managing web requests and routing (e.g., `BoardController`).
- `Models/` - Entity classes and database schema definitions.
- `ViewModels/` - Data transfer objects optimized for specific Views.
- `Views/` - Razor views for rendering the UI, styled with Tailwind CSS.
- `Services/` - Core business logic:
  - `GeocodingService.cs` - Handles distance calculations.
  - `RefereeScoringService.cs` - Manages the assignment suitability algorithm.
- `Data/` - EF Core `DbContext` and seeding logic (`DbInitializer`).
- `wwwroot/` - Static files, compiled CSS, and JS assets.

---
