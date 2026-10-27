# Kandinsky

Web app for creating code UML diagrams.

- **Frontend** (`client/`): thin React client with whiteboard tabs at the top, a left menu (New, Save, Import, Export) and a shape palette (rectangle, directed edge).
- **Backend** (`server/`): ASP.NET Core minimal API that owns and processes the diagrams. The UI and external clients (e.g. an MCP server) use the same REST API.
- **Formats:** import and export as PlantUML or Mermaid class diagrams.

See [REQUIREMENTS.md](REQUIREMENTS.md) for requirements, decisions and open points.

## Prerequisites

- .NET SDK 10
- Node.js 22

## Run

```sh
# backend on http://localhost:5080
cd server/Kandinsky.Server && dotnet run

# frontend on http://localhost:5180 (proxies /api to the backend; override with KANDINSKY_API_URL)
cd client && npm install && npm run dev
```

Open http://localhost:5180.

## Use

- The app opens with a new blank "Untitled" diagram (a reload keeps the open one, its id is in the URL hash).
- Every diagram on the server is a tab at the top; click a tab to switch, **+** (or **New**) creates an "Untitled" diagram, **×** deletes a diagram after confirmation. Click the name in the toolbar to rename it.
- **Rectangle**: click to add one in a free spot, or drag it onto the canvas.
- **Directed edge**: click it, then the source rectangle, then the target. Esc cancels.
- Drag rectangles to move them, Shift+drag to copy them, drag the corner handle to resize, double-click a rectangle or edge to edit its label, press Delete to remove the selection.
- **Save** writes the diagram to `server/Kandinsky.Server/data/`. Unsaved diagrams are lost on server restart.
- Select several rectangles with Ctrl/Cmd+click or by dragging a box on the empty canvas — any rectangle the box touches is selected (Ctrl/Cmd adds to the selection).
- **Align horizontally / vertically** lines up the selected rectangles on the first selected one (drawn with a thicker border).
- **Auto-layout** arranges all rectangles in layers following the edge direction; unconnected ones go in a grid below.
- Changes made through the API show up in the open diagram within 2 seconds.

## REST API

OpenAPI document: http://localhost:5080/openapi/v1.json. Example requests: [Kandinsky.Server.http](server/Kandinsky.Server/Kandinsky.Server.http).

| Method | Path | Purpose |
|---|---|---|
| GET / POST | `/api/diagrams` | List / create diagrams |
| GET / PATCH / DELETE | `/api/diagrams/{id}` | Get / rename / delete a diagram |
| POST | `/api/diagrams/{id}/save` | Persist a diagram |
| GET | `/api/diagrams/{id}/export?format=plantuml\|mermaid` | Export as text |
| POST | `/api/diagrams/import` | Create a diagram from text (`{ text, format?, name? }`) |
| POST | `/api/diagrams/{id}/align` | Align shapes (`{ shapeIds, direction: "horizontal" \| "vertical" }`) |
| POST | `/api/diagrams/{id}/layout` | Auto-layout all shapes |
| POST | `/api/diagrams/{id}/shapes` | Add a rectangle |
| GET / PATCH / DELETE | `/api/diagrams/{id}/shapes/{shapeId}` | Get / update / delete a rectangle |
| POST | `/api/diagrams/{id}/edges` | Add a directed edge |
| GET / PATCH / DELETE | `/api/diagrams/{id}/edges/{edgeId}` | Get / update / delete an edge |

Errors are returned as problem details (`application/problem+json`).

## Test

```sh
dotnet test Kandinsky.slnx
cd client && npm run build
```
