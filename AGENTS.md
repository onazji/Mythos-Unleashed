# Mythos Unleashed Repository Guidance

## Design authority

- Treat the Living Codex and the approved, focused Concept Sheet for the current milestone as the design authority.
- Before implementing or proposing a new system, consult `Documentation/ASSET_ARSENAL.md`. Verify what is already in the project, inspect TopDown Engine's native capabilities, and evaluate owned technology before writing a custom replacement.
- Package ownership is not approval to import, integrate, upgrade, or use a package. Confirm repository evidence and creator approval independently.

## Stewart Boilerplate

- Build the functional world beneath the artwork: favor dependable systems, architectural consistency, and the smallest useful scoped change.
- Give every gameplay domain one clearly identified authority. Avoid duplicate managers, competing state, and parallel persistence paths.
- Keep persistent/global ownership in the established bootstrap architecture and scene-local ownership in the appropriate gameplay scene. Preserve existing lifecycle and registration contracts.
- Presentation consumes authoritative gameplay data; it must not create a competing copy.

## TopDown Engine Doctrine

- Use the engine before extending the engine. Extend the engine before replacing the engine.
- Prefer project-owned configuration, composition, subclasses, or adapters over vendor modification.
- Preserve TopDown Engine and Inventory Engine manager, character, inventory, event, and persistence lifecycles unless an approved task explicitly requires otherwise.
- Do not modify vendor code without explicit creator authorization.

## Creator-owned work

- Preserve creator-authored code, scenes, prefabs, assets, settings, documentation, and uncommitted work. Inspect first and make only task-authorized changes.
- Do not import, install, download, upgrade, remove, or replace packages unless explicitly directed.
- Do not broaden milestones, commit, push, or choose an artistic direction without explicit approval.
- When evidence is incomplete, report the uncertainty; do not guess versions, import state, integration state, or validation state.
