# Kandinsky — Requirements

Web app for creating code UML diagrams. Server-side processing, thin React client, REST API for automated clients (e.g. an MCP server).

## Architecture

- **R-ARCH-1** The app consists of a React frontend and a backend service.
- **R-ARCH-2** All diagram processing (model changes, validation, import, export) runs on the server. The server owns the diagram state.
- **R-ARCH-3** The client is thin: it renders the diagram state from the server and sends user actions to the server. It holds no diagram business logic.
- **R-ARCH-4** The UI and external clients (e.g. an MCP server) use the same REST API.

## Diagram model

- **R-MOD-1** A diagram has an id, a name and a collection of shapes and edges.
- **R-MOD-2** A shape is a rectangle with an id, a label/text, a position (x, y) and a size (width, height).
- **R-MOD-3** An edge connects a source shape to a target shape and is directed (source → target). It has an id and an optional label.
- **R-MOD-4** Deleting a shape also deletes the edges connected to it.

## REST API

- **R-API-1** Diagrams: create, list, get, rename, delete.
- **R-API-2** Shapes: add, get, update (label, position, size), delete within a diagram.
- **R-API-3** Edges: add (source, target), get, update (label, endpoints), delete within a diagram.
- **R-API-4** Import a diagram from PlantUML or Mermaid text.
- **R-API-5** Export a diagram as PlantUML or Mermaid text.
- **R-API-6** The API uses JSON, correct HTTP status codes and validates input (e.g. edge endpoints must reference existing shapes).
- **R-API-7** The API is described by an OpenAPI specification, so tools like an MCP server can be generated or written against it.

## Import / Export

- **R-IO-1** Export a diagram to PlantUML.
- **R-IO-2** Export a diagram to Mermaid.
- **R-IO-3** Import a diagram from PlantUML.
- **R-IO-4** Import a diagram from Mermaid.
- **R-IO-5** Import reports unsupported or invalid syntax as an error instead of failing silently.

## User interface

- **R-UI-1** A menu on the left side with the basic operations:
  - **New** — create an empty diagram.
  - **Save** — persist the current diagram on the server.
  - **Export** — download the diagram as PlantUML or Mermaid.
  - **Import** — load a PlantUML or Mermaid file/text as a diagram.
- **R-UI-2** A palette of shapes in the left menu containing:
  - a basic rectangle,
  - a directed edge to connect two rectangles.
- **R-UI-3** The user adds a rectangle to the canvas from the palette.
- **R-UI-4** The user connects two rectangles with a directed edge from the palette.
- **R-UI-5** The user can move, resize, relabel and delete rectangles and edges on the canvas.
- **R-UI-6** Every change in the UI is sent to the server; the canvas shows the state returned by the server.
- **R-UI-7** Dragging a rectangle with Shift held creates a copy (same label and size) at the drop position; the original stays in place. Edges are not copied.
- **R-UI-8** The user selects multiple rectangles with Ctrl/Cmd+click (toggles a rectangle) or by dragging a selection box on the empty canvas (selects the rectangles fully inside the box; with Ctrl/Cmd held the box adds to the selection).
- **R-UI-9** An align tool lines up the selected rectangles (at least two):
  - **horizontally** — in a row, all vertical centers on the first selected rectangle's center;
  - **vertically** — in a column, all horizontal centers on the first selected rectangle's center.
- **R-UI-10** An auto-layout button arranges all rectangles of the diagram: connected rectangles in layers following the edge direction (top to bottom, with as few crossings as practical), unconnected rectangles in a grid below.
- **R-UI-11** Align and auto-layout run on the server and are available in the REST API.
- **R-UI-12** The application opens with a new blank diagram.

## Decisions (first implementation)

- Backend: ASP.NET Core minimal API (.NET 10), OpenAPI at `/openapi/v1.json`. Frontend: React + Vite + TypeScript, SVG canvas.
- Persistence: diagrams live in server memory; **Save** writes them as JSON files (`Storage:Path`, default `data/`), loaded on startup.
- Import/export: class diagram subset — classes (PlantUML also `interface`, `enum`, `abstract`, `rectangle`) and directed associations (`-->`, `..>`, reversed `<--`, `<..`) with optional labels. Class members and other relation types are rejected with a line-numbered error.
- Imported diagrams are laid out in a grid; shapes added without a position are placed in the first free grid slot.
- Live updates: the UI polls the open diagram every 2 seconds.
- Self-referencing edges are rejected.
- On start the UI creates a blank "Untitled" diagram on the server. The open diagram's id is kept in the URL hash, so a reload or link reopens that diagram instead (a new one is created if it no longer exists).

## Open points

- Which UML element types beyond rectangles and directed edges (classes with attributes/methods, interfaces, association types such as inheritance/composition).
- Better layout of imported diagrams; keeping positions across export/import.
- Push-based live updates (SSE/WebSocket) instead of polling.
- Authentication / multi-user support.
- An MCP server on top of the REST API.
