# Cortex Back-End Api

Cortex puts an AI shopping assistant on a business's existing website. The business uploads what it knows (product catalog, FAQs, policies), and the assistant answers customers from that material. It's multi-tenant and sold to businesses, so the short name for it is B2B RAG as a Service.

Most online stores still make customers search by keyword and dig through an FAQ page. With Cortex, a customer describes what they want in plain language, asks about a product, or has the assistant compare a few options and suggest one.

On the business side, the owner subscribes, creates an organization and invites their team. They upload their documents, set how the assistant looks and talks so it fits their brand, and paste a JavaScript snippet into their site.

We also want the assistant to do more than answer questions. Through integrations with the business's own systems, it should be able to pull live product information and act for the customer, like adding an item to their cart.

This is our Senior Project 1. This repo is the backend API only. The Next.js frontend is in a separate repository, and [FRONTEND_INTEGRATION.md](FRONTEND_INTEGRATION.md) explains how the two talk to each other.

## What we're building

- **Product discovery.** Customers describe what they want in their own words and get matching products back.
- **Answers from the business's own data (RAG).** The assistant retrieves from the documents each business uploaded and answers from those.
- **Contextual chunking.** A chunk cut out of a document often loses the context that made it meaningful, and that hurts retrieval. So before we embed a chunk, an LLM writes a short note saying where it sits in the full document, and we embed the two together. This is Anthropic's Contextual Retrieval technique.
- **Embeddable widget.** One JavaScript snippet adds the assistant to an existing site.
- **Knowledge management.** Businesses upload and manage catalogs, FAQs, policies and other documents.
- **Shopping in the chat.** Customers can compare products and use store functions like the cart without leaving the conversation.
- **Organizations and roles.** Each business gets a workspace with team invitations and role-based access control.
- **Subscriptions.** Plans and billing are handled per organization.
- **Customization.** The business controls the assistant's appearance, tone and behavior.

## Current status

We're still early. Accounts and organizations work today. But the RAG pipeline, the widget and billing are not built yet.

| Feature                                                 | Status  |
| ------------------------------------------------------- | ------- |
| Registration, email verification, login, Google sign-in | Done    |
| JWT access tokens with rotating refresh tokens          | Done    |
| Organizations, members and role-based permissions       | Done    |
| Knowledge upload and contextual chunking                | Planned |
| RAG chat and embeddable widget                          | Planned |
| Assistant customization                                 | Planned |
| Commerce integrations (product lookup, add to cart)     | Planned |
| Subscriptions and billing                               | Planned |

## Technology stack

- **Backend:** C#, ASP.NET Core (.NET 10)
- **Frontend:** Next.js, NextAuth, TypeScript, Tailwind CSS
- **Database:** PostgreSQL, pgvector
- **ORM:** Entity Framework Core
- **Architecture:** Clean Architecture
- **Infrastructure:** Docker, AWS S3, GitHub Actions
- **Caching:** Redis
- **AI:** LLMs, embedding models, RAG with contextual chunking
- **Testing:** xUnit, Moq

pgvector, Redis, S3 and the AI pieces are not in the code yet. They come in with the planned features.

## Project structure

The backend follows Clean Architecture, so dependencies only point inward, toward the Domain.

```
src/
  Cortex.Domain/          Entities and business rules
  Cortex.Application/     Use-case services, DTOs, validators, interfaces
  Cortex.Infrastructure/  Database, migrations, JWT, password hashing, email
  Cortex.Api/             Controllers and app startup
test/
  Cortex.Application.Tests/
  Cortex.Infrastructure.Tests/
```

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL running locally
- EF Core tools: `dotnet tool install --global dotnet-ef --version 10.0.14`

### 1. Configure settings

Settings live in user secrets, which keeps passwords and keys out of git. Run these from the repo root with your own values:

```sh
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=cortex;Username=postgres;Password=your_password" --project src/Cortex.Api
dotnet user-secrets set "Jwt:Secret" "a-random-secret-at-least-32-characters-long" --project src/Cortex.Api
dotnet user-secrets set "Jwt:Issuer" "cortex-api" --project src/Cortex.Api
dotnet user-secrets set "Jwt:Audience" "cortex-frontend" --project src/Cortex.Api
dotnet user-secrets set "GoogleAuth:ClientId" "your-google-client-id" --project src/Cortex.Api
dotnet user-secrets set "Brevo:ApiKey" "your-brevo-api-key" --project src/Cortex.Api
dotnet user-secrets set "Brevo:SenderEmail" "verified-sender@example.com" --project src/Cortex.Api
dotnet user-secrets set "Brevo:SenderName" "Cortex" --project src/Cortex.Api
dotnet user-secrets set "Frontend:BaseUrl" "http://localhost:3000" --project src/Cortex.Api
```

### 2. Create the database

```sh
dotnet ef database update --project src/Cortex.Infrastructure/Cortex.Infrastructure.csproj --startup-project src/Cortex.Infrastructure/Cortex.Infrastructure.csproj
```

### 3. Run the API

```sh
dotnet run --project src/Cortex.Api
```

The API runs at `http://localhost:5203`. Open `http://localhost:5203/scalar` to try the endpoints in the browser.

### Running with Docker

Copy `.env.example` to `.env`, fill in the values, then:

```sh
docker compose up --build
```

Docker Compose only starts the API. You still need PostgreSQL running on your machine.

### Running tests

```sh
dotnet test Cortex.slnx
```

## Why we're building it

A business that wants an assistant like this today has to build and maintain its own AI infrastructure. We don't think it should have to. With Cortex, it uploads its documents and pastes a snippet, and its customers can shop by talking to the site.
