# 🏆 RefApp - Intelligent Referee Assignment System

<div align="center">

<img src="./docs/images/RefappLogo.png" alt="RefApp Logo" width="400" />

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

<!-- TODO: Add screenshot of the board dashboard here -->
`![Board Dashboard](./docs/images/board-dashboard.png)`

*Description: Overview of weekly matches, quick stats, and pending assignments.*
</details>

<details>
<summary><b>2. Intelligent Referee Assignment View</b></summary>
<br/>

<!-- TODO: Add screenshot of the assignment view showing scores here -->
`![Assignment View](./docs/images/assignment-view.png)`

*Description: The assignment interface displaying suitability scores, distance calculations, and role preference indicators.*
</details>

<details>
<summary><b>3. Conflict Validation in Action</b></summary>
<br/>

<!-- TODO: Add screenshot of a conflict warning here -->
`![Conflict Validation](./docs/images/conflict-validation.png)`

*Description: System preventing a double-booking (hard conflict) and warning about same-day travel (soft conflict).*
</details>

<details>
<summary><b>4. Referee Profile & Preferences</b></summary>
<br/>

<!-- TODO: Add screenshot of the referee profile settings here -->
`![Referee Profile](./docs/images/referee-profile.png)`

*Description: Interface for referees to set their home location and preferred roles (Main/Assistant).*
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

## 🤝 Contributing

Contributions are welcome! If you'd like to improve the scoring algorithm or add new league support, please open an issue or submit a pull request.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
