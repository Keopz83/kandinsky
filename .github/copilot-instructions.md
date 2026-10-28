# Kandinsky – coding guidelines

React + TypeScript client in `client/`, ASP.NET Core minimal API in `server/`. Follow these rules in all new and changed code:

1. **Keep functions and files small and single-purpose.** Aim for functions under ~40 lines that do one thing. When a component or service grows (e.g. `Canvas.tsx`, `DiagramService.cs`), extract the new logic into a well-named helper, hook or class instead of adding to it.
2. **Use intention-revealing names, no magic values.** Name variables, functions and types after what they mean in the domain (`selectedShapeIds`, not `ids` or `arr`); avoid abbreviations except common ones (`id`, `x`, `y`). Move unexplained numbers and strings into named constants (`const GRID_SPACING = 40`).
3. **Model domain concepts explicitly, one cohesive unit per file.** Give each concept (shape, edge, group, layout, format parser/writer…) its own type with the data and behavior that belong to it, instead of loose dictionaries, parallel arrays or catch-all `utils` / service files. One file holds one clearly purposed class, component or module, named after the concept it represents (`EdgeRouter.cs`, `useSelection.ts`). Keep everything in a file at the same level of abstraction and high cohesion; split it when it starts serving several responsibilities.
4. **Prefer flat, readable control flow; comment the *why*, not the *what*.** Use early returns / guard clauses instead of deep nesting (max ~2 levels). Only comment non-obvious decisions, workarounds or format quirks — never restate what the code already says.
