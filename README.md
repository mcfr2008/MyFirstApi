# MyFirstApi

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-13-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![OpenAPI](https://img.shields.io/badge/OpenAPI-Swagger-85EA2D?logo=swagger&logoColor=black)](https://swagger.io/specification/)
[![License](https://img.shields.io/badge/license-TBD-lightgrey)](#license)

> A clean and extensible RESTful Web API built with ASP.NET Core, featuring JWT authentication, product CRUD operations, Entity Framework Core, and a service-based architecture.

## Overview

**MyFirstApi** is a RESTful API built with **ASP.NET Core 10** and **C#**. The project demonstrates a practical API architecture with controller-based routing, dependency injection, a service layer, Entity Framework Core, MySQL, OpenAPI/Swagger, and JWT bearer authentication.

The current implementation provides:

- Authentication through a JWT-based login endpoint
- Product CRUD operations
- Service and interface abstractions for business logic
- Entity Framework Core with MySQL
- OpenAPI/Swagger documentation for development
- HTTPS redirection and authentication/authorization middleware

## Architecture

The application follows a lightweight layered architecture:

```text
Client
  |
  v
Controllers
  |
  v
Services / Interfaces
  |
  v
Entity Framework Core
  |
  v
MySQL Database
```

### Main responsibilities

| Layer | Responsibility |
|---|---|
| `Controllers/` | HTTP endpoints, request handling, and HTTP responses |
| `Services/` | Business logic and application operations |
| `Interfaces/` | Service contracts and dependency inversion |
| `Data/` | Entity Framework Core database context |
| `Models/` | Domain/data models |
| `Program.cs` | Dependency injection, middleware, authentication, OpenAPI, and application startup |

## Features

### Authentication

The API exposes `POST /api/Auth/login` and returns a JWT when the supplied credentials pass the current authentication logic.

JWT validation is configured with issuer, audience, lifetime, and signing-key validation.

> **Security note:** The current login implementation is a demonstration and contains hardcoded credentials in `AuthController.cs`. Replace this with a real user store and password hashing before using the API in a production environment.

### Product Management

`ProductsController` provides the following CRUD endpoints:

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/Products` | Retrieve all products |
| `GET` | `/api/Products/{id}` | Retrieve a product by ID |
| `POST` | `/api/Products` | Create a product |
| `PUT` | `/api/Products/{id}` | Update a product |
| `DELETE` | `/api/Products/{id}` | Delete a product |

The `Product` model currently contains:

```json
{
  "id": 1,
  "name": "Example Product",
  "price": 99.99
}
```

## Technology Stack

- **Framework:** ASP.NET Core 10
- **Language:** C#
- **Runtime Target:** .NET 10
- **ORM:** Entity Framework Core
- **Database:** MySQL
- **MySQL Provider:** Pomelo.EntityFrameworkCore.MySql
- **Authentication:** JWT Bearer
- **API Documentation:** OpenAPI / Swagger
- **Dependency Injection:** Built-in ASP.NET Core DI container

> The project currently targets .NET 10 while several Entity Framework Core and Pomelo packages are on the 9.x major version. Keep package versions aligned with your intended .NET/EF Core release before production deployment.

## Prerequisites

Install the following before running the project:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- MySQL Server 8.x or another compatible MySQL installation
- An IDE/editor such as Visual Studio, JetBrains Rider, or Visual Studio Code
- Git

Verify the .NET SDK:

```bash
dotnet --version
```

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/mcfr2008/MyFirstApi.git
cd MyFirstApi
```

### 2. Restore dependencies

```bash
dotnet restore
```

### 3. Configure the database

Create a MySQL database for the application, then configure the connection string through a secure configuration mechanism.

Recommended local-development approaches include:

- `dotnet user-secrets`
- Environment variables
- A local, untracked configuration file

Example configuration shape:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "server=localhost;port=3306;database=myfirstapi_db;user=<username>;password=<password>"
  },
  "Jwt": {
    "Key": "<strong-random-secret>",
    "Issuer": "MyFirstApi",
    "Audience": "MyFirstApiClient"
  }
}
```

**Never commit real database passwords, JWT signing keys, API keys, or other secrets to a public repository.**

### 4. Run the API

```bash
dotnet run
```

The application uses the launch profile in `Properties/launchSettings.json` when started from supported development environments.

## API Usage

### Authentication

Request a token:

```http
POST /api/Auth/login
Content-Type: application/json

{
  "username": "<username>",
  "password": "<password>"
}
```

Successful response:

```json
{
  "token": "<jwt-token>"
}
```

For endpoints protected with authorization, send the token using the standard bearer scheme:

```http
Authorization: Bearer <jwt-token>
```

### List products

```bash
curl -X GET "https://localhost:<port>/api/Products"
```

### Get a product

```bash
curl -X GET "https://localhost:<port>/api/Products/1"
```

### Create a product

```bash
curl -X POST "https://localhost:<port>/api/Products" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Example Product",
    "price": 99.99
  }'
```

### Update a product

```bash
curl -X PUT "https://localhost:<port>/api/Products/1" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Updated Product",
    "price": 129.99
  }'
```

### Delete a product

```bash
curl -X DELETE "https://localhost:<port>/api/Products/1"
```

## OpenAPI / Swagger

In the development environment, the application enables OpenAPI and Swagger UI.

Use Swagger UI to explore and test endpoints interactively after starting the application.

Typical local URL:

```text
https://localhost:<port>/swagger
```

The exact port is defined by the active launch profile.

## Project Structure

```text
MyFirstApi/
├── Controllers/
│   ├── AuthController.cs
│   └── ProductsController.cs
├── Data/
│   └── AppDbContext.cs
├── Interfaces/
│   └── IProductService.cs
├── Models/
│   └── Product.cs
├── Services/
│   └── ProductService.cs
├── Properties/
│   └── launchSettings.json
├── Program.cs
├── MyFirstApi.csproj
├── MyFirstApi.http
├── appsettings.json
└── appsettings.Development.json
```

## Development Guidelines

For future development, consider the following practices:

- Keep secrets outside source control.
- Add database migrations and a documented migration workflow.
- Use request/response DTOs instead of exposing persistence models directly.
- Add model validation with clear `400 Bad Request` responses.
- Move user authentication to a persistent database or external identity provider.
- Store passwords using a modern password-hashing algorithm; never store plaintext passwords.
- Protect sensitive endpoints with `[Authorize]` where appropriate.
- Add automated unit and integration tests.
- Add CI checks for build, test, formatting, and security scanning.
- Pin compatible package versions and keep the .NET/EF Core dependency chain aligned.

## Error Handling

The current controllers use standard HTTP responses such as:

- `200 OK` for successful reads
- `201 Created` when a product is created
- `204 No Content` for successful updates/deletes
- `401 Unauthorized` for invalid authentication
- `404 Not Found` when a requested product does not exist

A production-ready API should also standardize validation errors and unexpected exceptions using a consistent error response format such as `ProblemDetails`.

## Testing

The repository includes `MyFirstApi.http`, which can be used to manually exercise API endpoints from supported IDEs.

For a production-grade development workflow, add automated tests for:

- Authentication behavior
- Product CRUD operations
- Validation rules
- Service-layer behavior
- Database integration
- Authorization behavior

## Security Considerations

Before exposing this API to the internet or deploying it to production:

1. Remove all hardcoded credentials and secrets from source code and configuration files.
2. Rotate any credentials or JWT keys that may already have been committed to a public repository.
3. Use HTTPS in every non-local environment.
4. Use strong, randomly generated JWT signing keys and protect them through a secrets manager.
5. Replace the demo login flow with a real identity and password-verification system.
6. Apply authorization policies to protected resources.
7. Add rate limiting, structured logging, monitoring, and centralized exception handling.
8. Keep all framework and authentication dependencies patched and supported.

## Contributing

Contributions are welcome.

A typical workflow is:

```bash
git checkout -b feature/<short-description>
# make changes
git add .
git commit -m "feat: describe the change"
git push origin feature/<short-description>
```

Then open a pull request with:

- A clear summary of the change
- Testing performed
- Any configuration or migration steps
- Security considerations when relevant

## Commit Convention

A Conventional Commits-style format is recommended:

```text
feat: add product filtering
fix: handle missing product correctly
docs: improve API documentation
refactor: simplify product service
chore: update dependencies
test: add product service tests
```

## Roadmap

Potential improvements include:

- [ ] Real database-backed authentication
- [ ] Password hashing and account management
- [ ] Authorization policies and roles
- [ ] DTOs and input validation
- [ ] EF Core migrations
- [ ] Global exception handling with `ProblemDetails`
- [ ] Automated unit and integration tests
- [ ] CI/CD with GitHub Actions
- [ ] Containerization with Docker
- [ ] Production observability and health checks
- [ ] API versioning

## License

No license is currently specified for this repository.

If this project is intended for public reuse, add an explicit open-source license (for example MIT, Apache-2.0, or another license that matches your requirements).

## Author

**mcfr2008**

GitHub: https://github.com/mcfr2008

---

Built with ASP.NET Core and C#.
