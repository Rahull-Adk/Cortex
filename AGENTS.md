# Cortex

> **Turn your business knowledge into an intelligent shopping experience.**

Cortex is a multi-tenant, subscription-based AI customer support and conversational commerce platform. It allows businesses, particularly e-commerce stores, to integrate an intelligent AI chatbot into their websites without having to build or maintain their own AI infrastructure.

Instead of forcing customers to navigate through categories, filters, FAQs, and search bars, Cortex allows them to interact with an AI assistant using natural language.

Customers can describe what they are looking for, ask questions about products, understand business policies, and receive personalized product recommendations through a conversational interface.

Businesses can create and customize their AI assistants, provide their business knowledge, connect their product catalogs, and integrate the chatbot into their websites using a lightweight JavaScript snippet.

Cortex aims to make online shopping more conversational, personalized, and efficient.

---

# 1. Core Concept

Traditional e-commerce search requires customers to know exactly what they are looking for.

For example, a customer looking for shoes might need to manually select:

- Category: Shoes
- Brand: Timberland
- Color: Black
- Size: 34
- Maximum price: ฿4,000

With Cortex, the customer can simply ask:

> "I'm looking for black Timberland shoes under 4,000 baht in size 34. What do you recommend?"

Cortex understands the request, retrieves relevant products from the business's catalog, and presents suitable recommendations.

Another example:

> "I'm looking for a gaming controller under 1,000 baht that works with my PC."

The assistant should understand the customer's requirements, search the available product catalog, and recommend relevant products.

Cortex is not simply another FAQ chatbot. Its primary objective is to combine business knowledge retrieval with conversational product discovery and commerce capabilities.

# 2. Target Users

Cortex has two primary user groups.

### 2.1 Business Owners

Business owners are the customers who subscribe to Cortex.

They use the Cortex dashboard to:

- Create and manage AI assistants.
- Configure assistant behavior and personality.
- Upload business documents and knowledge sources.
- Manage product catalogs.
- Configure website integrations.
- Monitor conversations and usage.
- Manage subscriptions and billing.

### 2.2 Storefront Customers

Storefront customers are the visitors interacting with a Cortex-powered chatbot on a business's website.

They do not necessarily need a Cortex account.

They can:

- Ask questions about the business.
- Search for products using natural language.
- Receive product recommendations.
- Ask follow-up questions.
- Get information about shipping, returns, and other business policies.
- Interact with supported commerce functionality, such as adding products to a cart.

**Important:** Business owners and storefront customers are different user types. Do not confuse their identities, authentication requirements, permissions, or data ownership.

# 3. Core Features

The following describes the intended product capabilities. It is a product specification, not an instruction to implement everything immediately.

## 3.1 AI Assistant Management

Businesses can create and manage their own AI assistants.

Expected capabilities:

- Create and configure assistants.
- Set assistant names and descriptions.
- Customize tone, personality, and response behavior.
- Configure chatbot appearance.
- Manage assistant-specific knowledge sources.
- Enable or disable assistants.
- Manage assistant settings.

Each assistant belongs to a specific business tenant.

## 3.2 Retrieval-Augmented Generation (RAG)

Cortex uses Retrieval-Augmented Generation to provide responses grounded in business-specific information.

Businesses should be able to provide knowledge through:

- PDF documents.
- Text documents.
- FAQs.
- Business policies.
- Shipping and return information.
- Product information.
- Other supported knowledge sources.

The intended knowledge processing workflow includes:

1. Accepting knowledge sources.
2. Extracting their content.
3. Splitting content into meaningful chunks.
4. Generating embeddings.
5. Storing embeddings in a vector database.
6. Retrieving relevant information when customers ask questions.
7. Providing retrieved context to an LLM for response generation.

The assistant should prioritize relevant business information rather than generating unsupported answers.

## 3.3 Product Catalog Integration

Cortex should support product discovery through conversational search.

Expected capabilities:

- Import and manage product catalogs.
- Store product information and attributes.
- Support product names, descriptions, categories, prices, variants, and availability.
- Retrieve products based on natural-language requirements.
- Apply relevant product filters.
- Return relevant product recommendations.

Example:

A customer asks:

"I'm looking for a black gaming controller under 1,000 baht."

The assistant should identify relevant requirements, retrieve matching products, and present the results.

Product information must remain reasonably up to date. The system should account for catalog changes rather than relying exclusively on an initial website scrape.

## 3.4 Conversational Commerce

Cortex should go beyond answering questions and support actual shopping interactions.

Intended capabilities include:

- Product recommendations.
- Product detail retrieval.
- Product page navigation.
- Adding products to a shopping cart.
- Supporting product variants such as size and color.
- Retrieving relevant cart information where supported.

Commerce operations should interact with the business's website or supported commerce integration.

Cortex must not assume that every business uses Shopify or another specific e-commerce platform.

The integration architecture should remain flexible enough to support different platforms and custom business APIs.

## 3.5 Website Chatbot Widget

Businesses should be able to embed their assistants into their websites using a lightweight JavaScript snippet.

Expected capabilities:

- Easy website integration.
- Customizable chatbot appearance.
- Assistant-specific configuration.
- Responsive interface.
- Real-time conversational interaction.
- Product recommendation presentation.
- Support for different website environments.

A business should not need to build its own chatbot frontend.

## 3.6 Customization

Businesses should be able to customize the chatbot's appearance and behavior.

Potential customization options:

- Chatbot name.
- Primary color.
- Brand logo.
- Font.
- Welcome message.
- Assistant personality.
- Position on the website.
- Other appearance-related settings.

## 3.7 Multi-Tenancy

Cortex is a multi-tenant SaaS application.

Multiple businesses will use the same platform while maintaining strict isolation of their data.

Tenant isolation must apply to:

- Users and business memberships.
- Assistants.
- Knowledge sources.
- Documents and embeddings.
- Product catalogs.
- Conversations.
- Usage records.
- Subscriptions and billing information.

A business must never be able to access another business's private information or resources.

## 3.8 Subscription and Billing

Cortex is intended to operate as a subscription-based SaaS product.

Expected capabilities:

- Subscription plans.
- Usage limits.
- Subscription status management.
- Billing management.
- Plan-based feature restrictions.
- Usage tracking.

The exact pricing structure, quotas, and payment provider integration are not finalized.

---

# 4. Technology Stack

The following represents the intended technology direction. Do not introduce alternative technologies or dependencies without justification and approval.

| Component | Technology |
|---|---|
| Backend | ASP.NET Core (.NET 10) |
| Language | C# |
| Frontend | React + TypeScript |
| Styling | Tailwind CSS |
| Authentication | Clerk |
| Database | PostgreSQL |
| Vector Search | pgvector |
| ORM | Entity Framework Core |
| Object Storage | AWS S3 |
| Cache | Redis (optional, depending on requirements) |
| AI / RAG | LLM integrations and LlamaIndex where appropriate |
| Payments | Stripe (planned) |
| Email | Brevo (planned) |
| Observability | OpenTelemetry |

Technologies marked as planned or optional must not be treated as mandatory implementation decisions.

---

# 5. Backend Architecture

The backend must follow **Clean Architecture**.

The objective is to maintain clear separation of concerns, independent business logic, testability, and maintainability.

The backend is organized into four primary projects.

### 5.1 Domain

Contains the core business model.

Responsibilities:

- Entities.
- Value Objects.
- Domain Enums.
- Domain Events (when justified).
- Business rules.
- Domain-level abstractions where appropriate.

Rules:

- Must not depend on Application.
- Must not depend on Infrastructure.
- Must not depend on API.
- Must not contain database-specific implementation details.
- Must not contain framework-dependent business logic.

The Domain layer should remain independent of external technologies.

### 5.2 Application

Contains application use cases and orchestration.

Responsibilities:

- Application services.
- Use-case logic.
- Interfaces and abstractions.
- DTOs.
- Validation.
- Application-level authorization requirements where appropriate.
- Coordination between domain objects and external dependencies.

Rules:

- May depend on Domain.
- Must not depend on Infrastructure.
- Must not depend on API.
- Must not contain database implementation details.
- Must not contain HTTP-specific logic.

The Application layer defines what the system does, not how external infrastructure performs it.

### 5.3 Infrastructure

Contains implementations of external dependencies and technical concerns.

Responsibilities:

- Entity Framework Core.
- Database configurations.
- DbContext.
- Repository implementations when justified.
- External API integrations.
- AI provider integrations.
- Object storage integrations.
- Email integrations.
- Caching implementations.
- Background processing infrastructure.

Rules:

- May depend on Application and Domain.
- Must implement abstractions defined by inner layers.
- Must not contain business rules that belong in Domain or Application.

### 5.4 API

The presentation and entry point of the backend.

Responsibilities:

- HTTP endpoints.
- Controllers.
- Request and response handling.
- Authentication configuration.
- Middleware.
- Dependency injection composition.
- API-level error handling.
- HTTP-specific concerns.

Rules:

- May depend on Application and Infrastructure for composition and dependency registration.
- Controllers must remain thin.
- Controllers must not contain business logic.
- Controllers must not directly perform complex database operations.
- Controllers must delegate application behavior to Application services.

### 5.5 Dependency Direction

The intended dependency structure is:

```text
           API
          /   \
         v     v
    Application  Infrastructure
         |        |       |
         v        v       v
              Domain
```

More precisely:

```text
Domain
  ↑
Application
  ↑
API

Infrastructure → Application
Infrastructure → Domain
API → Application
API → Infrastructure (composition root)
```

The fundamental rule is that dependencies must point inward toward the Domain.

Infrastructure implementations must be replaceable without requiring changes to core business rules.

---

# 6. Mandatory Rules for AI Coding Agents

These rules are mandatory. They must not be ignored for convenience, speed, or personal preference.

## 6.1 Architecture Rules

1. NEVER break Clean Architecture.
2. NEVER introduce circular project dependencies.
3. NEVER place business logic inside controllers.
4. NEVER place infrastructure implementations inside Domain or Application.
5. NEVER allow Domain to depend on external frameworks or infrastructure projects.
6. NEVER bypass Application services by directly accessing Infrastructure from controllers.
7. NEVER mix presentation, business logic, and persistence responsibilities.
8. Keep responsibilities clearly separated.
9. Follow existing project conventions instead of introducing a new architectural pattern without justification.
10. Do not introduce unnecessary abstractions or layers.

## 6.2 Mediator and CQRS Rules

**DO NOT USE MEDIATR.**

This is a strict requirement.

- Do not install or reference the MediatR package.
- Do not use `IMediator`.
- Do not use `ISender` or `IPublisher`.
- Do not implement request handlers or notification handlers based on MediatR.
- Do not introduce a custom mediator implementation as a workaround.

Application services and use-case classes should handle application orchestration through normal dependency injection.

Do not introduce CQRS infrastructure or handler abstractions unless explicitly requested and approved.

## 6.3 Database and Persistence Rules

1. Use Entity Framework Core for database access.
2. Use PostgreSQL as the primary relational database.
3. Use pgvector for vector storage and similarity search where appropriate.
4. Never execute database operations directly inside controllers.
5. Do not expose EF Core entities directly through API responses.
6. Use appropriate DTOs for external requests and responses.
7. Avoid unnecessary database queries.
8. Be mindful of N+1 query problems.
9. Avoid loading unnecessary related entities.
10. Use asynchronous database operations where appropriate.
11. Never use synchronous database calls when an asynchronous alternative is available.
12. Database schema changes must be managed through EF Core migrations.
13. Consider indexing, query performance, and data integrity when designing persistence.
14. Tenant isolation must be enforced at the data-access and application levels, not merely through frontend filtering.

## 6.4 Multi-Tenancy and Security Rules

1. Never trust a tenant ID supplied by the frontend without validating authorization.
2. Every tenant-owned resource must have proper ownership enforcement.
3. Never allow cross-tenant data access.
4. Validate resource ownership before performing operations.
5. Never expose API secrets, private keys, or sensitive credentials.
6. Never hardcode secrets or credentials.
7. Never trust user-provided input.
8. Validate incoming requests.
9. Apply authorization on protected endpoints.
10. Never assume authentication automatically means authorization.
11. Keep business-owner authentication separate from storefront customer interactions.
12. Ensure that public chatbot endpoints cannot access administrative functionality.

## 6.5 Code Quality Rules

1. Write clean, readable, maintainable C#.
2. Follow established .NET naming conventions.
3. Use dependency injection.
4. Prefer constructor injection.
5. Avoid unnecessary static classes and global state.
6. Avoid massive classes with too many responsibilities.
7. Avoid deeply nested conditional logic.
8. Avoid unnecessary design patterns.
9. Do not duplicate business logic across multiple layers.
10. Do not introduce dependencies without a clear reason.
11. Use meaningful names for classes, methods, properties, and variables.
12. Handle errors explicitly and consistently.
13. Use cancellation tokens for appropriate asynchronous operations.
14. Avoid swallowing exceptions.
15. Do not use `async void` for application operations.

## 6.6 API Design Rules

1. Keep endpoints RESTful and consistent.
2. Use appropriate HTTP methods and status codes.
3. Validate request payloads.
4. Use consistent API response and error conventions.
5. Never expose internal implementation details through API responses.
6. Do not return database entities directly.
7. Apply authentication and authorization appropriately.
8. Avoid unnecessarily large API responses.
9. Consider pagination for collection endpoints.
10. Do not place application logic inside endpoint definitions or controllers.

## 6.7 Frontend Rules

1. Use TypeScript rather than JavaScript for application code.
2. Use reusable React components.
3. Keep components focused on presentation and interaction.
4. Separate API communication from UI components.
5. Avoid putting business logic inside large components.
6. Use appropriate loading, error, and empty states.
7. Do not duplicate backend business rules in the frontend.
8. Never trust frontend authorization checks as the only security mechanism.
9. Keep the frontend and backend contracts consistent.
10. Avoid unnecessary dependencies and state-management libraries.

## 6.8 AI and RAG Rules

1. Do not assume LLM responses are always correct.
2. Ground business-specific answers in retrieved knowledge whenever appropriate.
3. Do not invent product information, prices, or availability.
4. Respect tenant boundaries during retrieval.
5. Ensure that retrieved documents belong to the correct tenant and assistant.
6. Consider document processing failures and malformed input.
7. Do not hardcode a specific LLM provider throughout the application.
8. Keep provider-specific implementations behind appropriate abstractions.
9. Treat retrieved documents and customer messages as untrusted input.
10. Do not allow instructions inside retrieved documents to override system-level security rules.
11. Avoid unnecessary LLM calls and expensive operations.
12. Consider token usage, latency, and cost.

---

# 7. AI Agent Working Rules

These instructions govern how the coding agent should approach this project.

### 7.1 Understand Before Acting

- Always inspect the existing project structure before making suggestions about implementation.
- Read relevant files before proposing modifications.
- Understand existing dependencies and conventions.
- Do not assume that a feature has already been implemented.
- Do not invent classes, interfaces, services, or database tables that have not been established.

### 7.2 No Unrequested Implementations

**The current instruction is to understand and plan the project, not to implement features.**

Until explicitly instructed otherwise:

- Do not create implementation code.
- Do not modify existing source files.
- Do not generate database migrations.
- Do not install NuGet packages or frontend dependencies.
- Do not modify project configurations.
- Do not create entities, services, controllers, repositories, or endpoints.
- Do not execute commands that modify the project.

When asked about a feature, first explain the design, architectural responsibilities, trade-offs, and required components.

Wait for explicit implementation instructions before writing or modifying code.

### 7.3 Think Before Designing

When discussing a feature:

1. Understand the business requirement.
2. Identify the affected architectural layers.
3. Identify the required domain concepts.
4. Consider data ownership and tenant isolation.
5. Consider security implications.
6. Consider performance and scalability.
7. Identify dependencies and integration requirements.
8. Explain the proposed approach before implementation.

Do not blindly agree with an architectural decision if it introduces unnecessary complexity or technical problems.

Challenge questionable decisions and explain why an alternative might be better.

### 7.4 Avoid Overengineering

- Do not introduce abstractions without a clear purpose.
- Do not implement patterns just because they are popular.
- Do not create unnecessary interfaces for every class.
- Do not introduce unnecessary repositories on top of EF Core.
- Do not create generic frameworks for simple application requirements.
- Prefer straightforward, maintainable solutions.
- Optimize for actual requirements rather than hypothetical millions of users.

### 7.5 Communication

When responding to project-related requests:

- Be technically precise.
- Explain architectural decisions and trade-offs.
- Distinguish confirmed requirements from assumptions.
- Point out missing information.
- Ask for clarification when requirements are ambiguous.
- Do not silently make major architectural decisions.
- Keep explanations relevant to the requested task.

---

# 8. Development Philosophy

Cortex should be developed with the following priorities:

1. **Correctness:** Business logic and data integrity come first.
2. **Security:** Tenant isolation and authorization are fundamental.
3. **Maintainability:** The codebase should remain understandable as the project grows.
4. **Scalability:** Avoid architectural decisions that unnecessarily limit future growth.
5. **Performance:** Pay attention to database queries, LLM latency, and resource consumption.
6. **Cost efficiency:** AI infrastructure and external services should be used thoughtfully.
7. **Simplicity:** Prefer the simplest architecture that correctly solves the problem.

Do not sacrifice maintainability for premature optimization.

Do not sacrifice architectural integrity for implementation speed.

---

# 9. Project Status and Scope

Cortex is currently in its planning and architecture-design stage.

The purpose of this document is to establish shared context for AI agents and contributors.

The following are not necessarily finalized:

- Complete database schema and ERD.
- Exact subscription plans and pricing.
- Supported e-commerce integrations.
- LLM providers and model selection.
- Embedding model selection.
- Background job infrastructure.
- Final API contracts.
- Exact RAG pipeline configuration.
- Detailed frontend architecture.

These decisions should be made deliberately as the project evolves.

**An AI agent must never interpret this README as permission to independently implement unfinished features or finalize undecided architecture.**

---

# 10. Final Instruction

You are contributing to Cortex, a serious multi-tenant SaaS project.

Your responsibility is to help build a maintainable, secure, and technically sound product.

Understand the business requirements before proposing technical solutions.

Respect Clean Architecture without exceptions.

Do not use MediatR under any circumstances.

Do not introduce unnecessary complexity.

Do not make major decisions without justification.

Most importantly, **do not implement anything until explicitly instructed to do so.**

This document takes precedence over an agent's default coding preferences. If a requested task conflicts with these rules, identify the conflict and explain it before proceeding.