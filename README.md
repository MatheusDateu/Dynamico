# Dynamic .NET Database Manager (Proof of Concept)

A .NET 8, Dapper, and PostgreSQL proof-of-concept for dynamically managing database schemas and data at runtime. This CLI tool explores a security-first approach to building multi-tenant or low-code-style applications where the database structure is not fixed.

## Description

This project demonstrates how to build a .NET application that can safely execute dynamic DDL (`CREATE TABLE`) and DML (`INSERT`, `SELECT`) commands based on user input. It is built as an extensible CLI tool but is designed to be refactored into a core class library.

The primary goals of this architecture are:
1.  **Security:** To prove that dynamic SQL can be executed safely, preventing SQL Injection at both the schema (DDL) and data (DML) levels.
2.  **Extensibility:** To create a clean architecture (using a service layer and dictionary-based command pattern) that is easy to maintain and expand.
3.  **Multi-Tenancy:** To simulate a permissions/ownership layer using metadata tables, allowing different "users" to own and access only their specific tables.

## Key Features

* **Dynamic Table Creation:** Create new tables with user-defined column names and data types.
* **Dynamic Data Manipulation:** Insert data into and query data from any managed table using a dynamic interface.
* **Security-First Design:** Includes a dedicated `SqlSafeBuilder` utility to sanitize schema identifiers and leverages Dapper's parameterization for data.
* **Metadata & Ownership Layer:** A `MetadataService` tracks user-created tables in a separate `app_managed_tables` table, enforcing a basic permissions model.
* **Extensible Menu-Driven UI:** The main `Program.cs` uses a `Dictionary<string, Func<...>>` instead of a `switch-case`, adhering to the Open/Closed Principle.

## Technology Stack

* **.NET 8:** The core runtime and SDK.
* **PostgreSQL:** The target relational database.
* **Dapper:** A high-performance micro-ORM for executing queries.
* **Npgsql:** The .NET data provider for PostgreSQL.

## Core Architecture

The project is intentionally kept simple, relying on three key classes to separate concerns:

1.  **`SqlSafeBuilder.cs` (Security Layer):** A static utility class responsible for validating and sanitizing all schema identifiers (table and column names). It uses a strict allow-list regex (`^[a-zA-Z_][a-zA-Z0-9_]{0,50}$`) and a reserved keyword block-list to prevent DDL-based SQL injection.
2.  **`MetadataService.cs` (Permission Layer):** Manages all interactions with the `app_managed_tables`. It is responsible for checking if a user has permission to access or modify a given table before any operation is attempted.
3.  **`DynamicDbService.cs` (Service Layer):** The main "workhorse" class. It orchestrates all operations, first by checking permissions via `MetadataService` and then by safely building and executing SQL using `SqlSafeBuilder` (for schema) and Dapper (for data).
4.  **`Program.cs` (UI Layer):** Acts as the composition root and user interface. It initializes the services and uses a command-pattern dictionary to map user-facing menu options to their corresponding service methods.

## Getting Started

### Prerequisites

* [.NET 8 SDK](https://dotnet.microsoft.com/download)
* A running [PostgreSQL](https://www.postgresql.org/download/) server (local or in Docker)

### 1. Configuration

1.  Clone this repository.
2.  Restore the NuGet packages:
    ```bash
    dotnet restore
    ```
3.  Open `Program.cs` and update the `connectionString` constant with your PostgreSQL server details.
    ```csharp
    // IMPORTANT: Update this
    const string connectionString = "Host=localhost;Username=postgres;Password=your_password;Database=your_test_db";
    ```
4.  Ensure the database user specified in the connection string has permissions to `CREATE` tables in the target database.

### 2. Running the Application

From the project's root directory, simply run:

```bash
dotnet run
````

The application will start, automatically create the `app_managed_tables` metadata table (if it doesn't exist), and display the main menu.

### 3\. Simulating Users

The `currentUserId` constant in `Program.cs` is used to simulate user permissions:

  * `const int currentUserId = 1;` (Super Admin): Can access all tables.
  * `const int currentUserId = 2;` (Client User): Can only access tables they have created.

Change this value to test the metadata-based permission logic.

## Security Model

Preventing SQL Injection is the top priority of this design.

  * **DML (Data Injection):** This is handled by **Dapper**. When inserting or querying data, all *values* (e.g., `Alice`, `alice@example.com`) are passed as Dapper parameters. Dapper correctly parameterizes these values, preventing traditional SQL injection (`' OR 1=1`).
  * **DDL (Schema Injection):** This is the more complex threat, as you cannot parameterize table or column names. We solve this using the `SqlSafeBuilder`, which **validates and sanitizes** all identifiers *before* they are concatenated into a SQL string. Any invalid input (like `my_table; (DROP...)`) is rejected by the regex and an exception is thrown.

## Future Roadmap

This proof-of-concept can be extended into a full-fledged library.

  * Refactor core logic (`DynamicDbService`, `MetadataService`, `SqlSafeBuilder`) into a `.NET Standard` Class Library.
  * Implement a robust error-handling and retry-logic system.
  * Introduce global "exit" commands to improve the CLI flow.
  * Build a Web API wrapper around the core library to serve as a backend for a low-code UI.

<!-- end list -->

## 👨‍💻 Author & Contact

This project was developed and is maintained by **Matheus Delmondes**.

Connect with me:

* **GitHub:** [github.com/MatheeusDateu](https://github.com/MatheusDateu)
* **Portfolio:** [Portfolio - MatheusDateu - Blazor WASM](https://matheusdateu.github.io/blazor-pwa-portfolio-matheusdateu/)
* **LinkedIn:** [Matheus Delmondes](https://www.linkedin.com/in/matheus-delmondes-7260b6221/)
* **Email:** mdelmondes5@outlook.com
* **Instagram:** [@dev_MatheusDelmondes](https://www.instagram.com/dev_matheusdelmondes/)
