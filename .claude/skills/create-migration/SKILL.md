---
name: create-migration
description: Scaffolds a new EF Core migration for oed-authz, reviews it for safety, and optionally applies it to the local dev database.
disable-model-invocation: true
---

Follow these steps to create a new EF Core migration:

1. **Get migration name**: If the user provided a name as an argument, use it. Otherwise ask: "What should the migration be named? (PascalCase, e.g. Add_column_foo)"

2. **Scaffold the migration**:
   ```bash
   dotnet ef migrations add {name} --project src/oed-authz
   ```
   If this fails (e.g. model snapshot conflict), show the error and stop.

3. **Locate the generated files**: The new migration will appear in `src/oed-authz/Infrastructure/Database/Migrations/` as `{timestamp}_{name}.cs` and `{timestamp}_{name}.Designer.cs`, plus an updated `OedAuthzDbContextModelSnapshot.cs`.

4. **Review with migration-reviewer**: Read the generated `{timestamp}_{name}.cs` file and pass it to the `migration-reviewer` subagent. Show the verdict to the user.

5. **If UNSAFE**: Tell the user what to fix and stop. Do not apply the migration.

6. **If SAFE or REVIEW-NEEDED**: Ask the user: "Apply migration to local dev database? (requires local PostgreSQL running with admin credentials)"
   - If yes: `dotnet ef database update --project src/oed-authz`
   - Show success or error output.

7. **Remind the user**: The `OedAuthzDbContextModelSnapshot.cs` must be committed alongside the migration files.
