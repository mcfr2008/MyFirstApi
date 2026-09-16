# MyFirstApi

## Overview
**MyFirstApi** is a RESTful Web API built with ASP.NET Core[cite: 2]. The project is structured to ensure maintainability and clean architecture, providing core endpoints for product management and secure authentication[cite: 2].

## Features
* **Authentication & Authorization**: Secure API access managed by `AuthController.cs`[cite: 2]. The project implements JSON Web Tokens (JWT) using the `Microsoft.AspNetCore.Authentication.JwtBearer` library[cite: 2].
* **Product Management**: Provides comprehensive operations (CRUD) for products handled through `ProductsController.cs`[cite: 2].
* **Service Repository Pattern**: Adheres to the Dependency Injection design pattern by utilizing `IProductService.cs`[cite: 2] and its concrete implementation in `ProductService.cs`[cite: 2].
* **Data Access**: Utilizes Entity Framework Core via `AppDbContext.cs`[cite: 2] to interact with the database, mapping to models like `Product.cs`[cite: 2].

## Tech Stack
* **Framework**: .NET 10.0[cite: 2]
* **Language**: C#[cite: 2]
* **Security**: JWT Bearer Authentication[cite: 2]

## Project Structure
The repository follows a standard ASP.NET Core directory layout[cite: 2]:

* `Controllers/`: Contains the API endpoints routing logic (`AuthController.cs`, `ProductsController.cs`)[cite: 2].
* `Services/`: Houses the business logic implementation (`ProductService.cs`)[cite: 2].
* `Interfaces/`: Defines contracts for the services (`IProductService.cs`)[cite: 2].
* `Data/`: Contains the database context (`AppDbContext.cs`)[cite: 2].
* `Models/`: Defines the data schema and entities (`Product.cs`)[cite: 2].
* `Program.cs`: The main entry point and middleware configuration[cite: 2].
* `MyFirstApi.http`: An HTTP file for testing API endpoints directly within the IDE[cite: 2].

## Getting Started

### Prerequisites
* .NET 10.0 SDK[cite: 2]

### Configuration
Update your database connection strings and JWT configurations in the application settings[cite: 2]:
* `appsettings.json`[cite: 2]
* `appsettings.Development.json`[cite: 2]

### Running the Application
The project includes predefined launch configurations. You can run the application using the settings defined in `Properties/launchSettings.json`[cite: 2].