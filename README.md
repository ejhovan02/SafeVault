# SafeVault — Authentication & User Management (.NET + Blazor WebAssembly)

A multi-project .NET solution featuring an **ASP.NET Core Web API** (`VaultApi`) backed by **MySQL** and **Entity Framework Core**, a **Blazor WebAssembly** frontend (`VaultClient`), a shared model library (`VaultShared`), and an **NUnit** test suite (`VaultTests`) covering authentication and input-handling scenarios.

## Solution Architecture

- **`VaultShared`**: Defines the shared `User` entity (`UserID`, `UserName`, `Email`, `PasswordHash`, and `Role`, defaulting to `"User"`).
- **`VaultApi`**: ASP.NET Core Web API using `ApplicationDbContext` (Pomelo MySQL provider), **BCrypt.Net** for password hashing/verification, **JWT Bearer** authentication (`Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`), role-based authorization (`[Authorize(Roles = "Admin")]`), and CORS configured for `http://localhost:5089`.
- **`VaultClient`**: Blazor WebAssembly client (`http://localhost:5152` API base URL) with pages for registration (`/register`), login (`/login`), root redirect (`/`), and an admin dashboard (`/admin`) that clears `localStorage` (`authToken`) on logout.
- **`VaultTests`**: NUnit test suite (`TestInputValidation`) using EF Core's In-Memory provider to test SQL injection-like strings, XSS payloads, invalid login attempts, and role checks.

## Project Structure

```text
SafeVault/
├── VaultShared/
│   └── User.cs                  # Shared User model
├── VaultApi/
│   ├── Controllers/
│   │   └── UsersController.cs   # Register, Login, JWT generation, and Admin route
│   ├── ApplicationDbContext.cs  # EF Core DbContext with DbSet<User>
│   └── Program.cs               # MySQL, JWT auth, CORS, and controller setup
├── VaultClient/
│   ├── Pages/
│   │   ├── Register.razor       # /register — user registration form
│   │   ├── Login.razor          # /login — authenticates and stores JWT in localStorage
│   │   ├── Home / Redirect.razor# / — redirects to /login
│   │   └── Admin.razor          # /admin — Admin-authorized view with logout
│   └── Program.cs               # Blazor WASM host and HttpClient base address
└── VaultTests/
    └── TestInputValidation.cs   # NUnit tests for auth and input handling
