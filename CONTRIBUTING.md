# Contributing to HookBridge

First off, thank you for considering contributing to HookBridge! We welcome contributions from everyone.

## Prerequisites

Before you begin, ensure you have the following installed:

* **Docker Engine** (24+)
* **Docker Compose** (v2+)
* **.NET 10 SDK**
* **Node.js** (22+)
* **pnpm**

## Getting Started

Follow these steps to set up the development environment:

1. **Clone the repo**
   ```bash
   git clone <repository-url>
   cd hookbridge
   ```

2. **Configure environment variables**
   ```bash
   cp .env.example .env
   ```

3. **Start the infrastructure** (PostgreSQL, RabbitMQ, Redis, Jaeger)
   ```bash
   docker compose up -d
   ```

4. **Run the backend (API)**
   ```bash
   dotnet run --project src/HookBridge.Api
   ```

5. **Run the frontend (Web)**
   ```bash
   cd src/HookBridge.Web
   pnpm install
   pnpm start
   ```

## Running Tests

To run the test suite, execute the following from the root directory:

```bash
dotnet test
```

## Code Style

This project follows the conventions defined in our `.editorconfig` file. We use Roslyn analyzers to enforce coding standards, and warnings are treated as errors (`TreatWarningsAsErrors`). Please ensure your code complies with these rules before submitting a pull request.

## Commit Conventions

We strictly follow the [Conventional Commits](https://www.conventionalcommits.org/) format. Ensure your commit messages use the following prefixes:

* `feat:` (New feature)
* `fix:` (Bug fix)
* `docs:` (Documentation changes)
* `test:` (Adding missing tests or correcting existing tests)
* `refactor:` (Code change that neither fixes a bug nor adds a feature)
* `chore:` (Updates to build process or auxiliary tools/libraries)

## Pull Request Process

1. Create a feature branch from `main` (e.g., `git checkout -b feat/my-new-feature`).
2. Ensure all tests pass.
3. Commit your changes following the commit conventions.
4. Push your branch and open a Pull Request.
5. Provide a clear description of the changes in the PR.

## Code of Conduct

By participating in this project, you agree to abide by the [Contributor Covenant v2.1](https://www.contributor-covenant.org/version/2/1/code_of_conduct/).

## License

HookBridge is licensed under the MIT License.
