# Kandinsky

Web app for creating code UML diagrams.

- **Frontend** (`client/`): thin React client styled with Bootstrap CSS, with a header menu bar with a File drop-down (New, Open, Save, Save As) and a Settings drop-down (default save format), whiteboard tabs at the top of the canvas and a left shape palette (rectangle, directed edge).
- **Backend** (`server/`): ASP.NET Core minimal API that owns and processes the diagrams. The UI and external clients (e.g. an MCP server) use the same REST API.
- **Formats:** import and export as PlantUML or Mermaid class diagrams. Shape positions are kept in `kandinsky:layout` comments; files without them are auto-laid out on import.

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
- Every diagram on the server is a tab at the top; click a tab to switch, **+** (or **New**) creates an "Untitled" diagram, **×** deletes a diagram (asks for confirmation only if it has unsaved changes, i.e. changed since it was last saved to or opened from a file). Double-click a tab to rename the diagram in place (Enter or clicking elsewhere confirms, Esc cancels). The name is shown only on the tab. Saved files don't store the name, it is the file name: choosing a file name in the Save / Save As dialog renames the diagram to it, and opening a file names the diagram after the file (a title in the file is ignored).
- **Rectangle**: click it, then click on the canvas to place one there (Esc or clicking the tool again cancels), or drag it onto the canvas.
- **Directed edge**: click it, then the source rectangle, then the target. The tool stays active for further edges until Esc is pressed or the tool is clicked again.
- Drag rectangles or edges to move them (dragging one of several selected objects moves them all; a selected edge carries its two rectangles along), Shift+drag to copy them (copied edges connect the copied rectangles), drag the corner handle to resize, double-click a rectangle or edge to edit its label, press Delete to remove the selection.
- **Save** opens a save dialog to choose the file location and format (PlantUML or Mermaid, with layout comments, so the file can be reopened with **Open…**); later Saves of the same whiteboard overwrite that file without asking. A whiteboard opened from a file is saved back to that file (in its format) without asking, even on the first Save. After a whiteboard is renamed, Save opens the dialog again with the new name as file name, so the old file is kept. **Save As…** always asks for a new location. Shortcuts: Ctrl+S / Cmd+S saves, Ctrl+Shift+S / Cmd+Shift+S saves as. **Settings → Default save format** selects the format offered first (default: Mermaid; stored in the browser). The dialog needs the File System Access API (Chrome, Edge); other browsers download the file in the default format instead. If the browser refuses write access to the chosen file (e.g. in embedded browsers), the file is downloaded instead and the next Save asks for a location again.
- Diagrams on the server live in memory; the UI no longer writes them to `server/Kandinsky.Server/data/` (the `/save` endpoint remains for API clients). Diagrams not saved to a file are lost on server restart.
- Select several rectangles and edges with Ctrl/Cmd+click or by dragging a box on the empty canvas — any rectangle or edge the box touches is selected (Ctrl/Cmd adds to the selection).
- **Align horizontally / vertically** lines up the selected rectangles on the first selected one (drawn with a thicker border); selected edges are ignored.
- **Auto-layout** arranges all rectangles in layers following the edge direction; unconnected ones go in a grid below.
- Navigate: rotate the mouse wheel or pinch on the trackpad to zoom (centered on the pointer); drag with the wheel pressed, or move two fingers on the trackpad (or touch screen), to pan.
- Changes made through the API show up in the open diagram within 2 seconds.

## REST API

OpenAPI document: http://localhost:5080/openapi/v1.json. Example requests: [Kandinsky.Server.http](server/Kandinsky.Server/Kandinsky.Server.http).

| Method | Path | Purpose |
|---|---|---|
| GET / POST | `/api/diagrams` | List / create diagrams |
| GET / PATCH / DELETE | `/api/diagrams/{id}` | Get / rename / delete a diagram |
| POST | `/api/diagrams/{id}/save` | Persist a diagram on the server (`data/`) |
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
