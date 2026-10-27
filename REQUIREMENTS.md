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
- **R-IO-6** Export keeps the layout: PlantUML and Mermaid cannot express positions, so the position and size of every shape are written as format comments (`' kandinsky:layout <id> x=<x> y=<y> width=<width> height=<height>` in PlantUML, `%% kandinsky:layout …` in Mermaid). Other tools ignore these comments and render the diagram as usual.
- **R-IO-7** Import restores the layout from these comments. Files without layout comments are arranged with the auto-layout (R-UI-10); in files with layout for only some shapes, the shapes without layout are placed in free grid slots. Malformed layout comments and layout for unknown shapes are reported as errors (R-IO-5).

## User interface

- **R-UI-1** A header menu bar at the top of the application with a **File** drop-down menu containing all file operations:
  - **New** — create an empty diagram.
  - **Save** — save the current diagram to a file. The first Save of a diagram opens a dialog to choose the location (and format); further Saves overwrite that file without asking.
  - **Save As** — always opens the dialog to save the current diagram to a new location.
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
- **R-UI-8** The user selects multiple rectangles with Ctrl/Cmd+click (toggles a rectangle) or by dragging a selection box on the empty canvas (selects every rectangle the box touches, even partially; with Ctrl/Cmd held the box adds to the selection).
- **R-UI-9** An align tool lines up the selected rectangles (at least two):
  - **horizontally** — in a row, all vertical centers on the first selected rectangle's center;
  - **vertically** — in a column, all horizontal centers on the first selected rectangle's center.
- **R-UI-10** An auto-layout button arranges all rectangles of the diagram: connected rectangles in layers following the edge direction (top to bottom, with as few crossings as practical), unconnected rectangles in a grid below.
- **R-UI-11** Align and auto-layout run on the server and are available in the REST API.
- **R-UI-12** The application opens with a new blank diagram.
- **R-UI-13** Multiple diagrams (whiteboards) are shown as tabs at the top of the canvas, one tab per diagram on the server. Clicking a tab opens that diagram, a **+** tab creates a new one and a tab's **×** deletes the diagram after a confirmation. The left menu has no diagram list.
- **R-UI-14** Dragging one of several selected rectangles moves all selected rectangles together, keeping their relative positions; with Shift held all of them are copied (R-UI-7). Clicking a selected rectangle without dragging selects only that rectangle.
- **R-UI-15** Mouse wheel navigation on the whiteboard: pressing the wheel and dragging pans the whiteboard; rotating the wheel forward zooms in, backward zooms out, centered on the mouse pointer.
- **R-UI-16** A **Settings** drop-down menu in the header menu bar sets the default save format (PlantUML or Mermaid) used by Save / Save As. The default is **Mermaid**, the most widely used diagrams-as-code format (native rendering in GitHub, GitLab, Azure DevOps, Notion, Confluence). The setting is kept across sessions.

## Decisions (first implementation)

- Backend: ASP.NET Core minimal API (.NET 10), OpenAPI at `/openapi/v1.json`. Frontend: React + Vite + TypeScript, SVG canvas.
- Persistence: diagrams live in server memory. The API endpoint `POST /api/diagrams/{id}/save` writes them as JSON files (`Storage:Path`, default `data/`), loaded on startup; the UI does not use it.
- Save / Save As write PlantUML or Mermaid with layout comments (R-IO-6), so saved files are reopened with Import. The format follows the extension chosen in the dialog; the default save format is offered first. The dialog uses the browser's File System Access API (Chrome, Edge); other browsers download the file in the default format. The default save format is stored in the browser (`localStorage`).
- Import/export: class diagram subset — classes (PlantUML also `interface`, `enum`, `abstract`, `rectangle`) and directed associations (`-->`, `..>`, reversed `<--`, `<..`) with optional labels. Class members and other relation types are rejected with a line-numbered error.
- Imported diagrams keep the layout from their layout comments, otherwise they are auto-laid out (R-IO-7); shapes added without a position are placed in the first free grid slot.
- Live updates: the UI polls the open diagram and the diagram list (tabs) every 2 seconds.
- Tabs keep their order; diagrams created elsewhere are appended at the end. If the open diagram is deleted, the neighboring tab opens.
- Self-referencing edges are rejected.
- On start the UI creates a blank "Untitled" diagram on the server. The open diagram's id is kept in the URL hash, so a reload or link reopens that diagram instead (a new one is created if it no longer exists).

## Open points

- Which UML element types beyond rectangles and directed edges (classes with attributes/methods, interfaces, association types such as inheritance/composition).
- Exporting layout hints (e.g. PlantUML `-right->`, Mermaid `direction`) so external renderers approximate the Kandinsky layout.
- Push-based live updates (SSE/WebSocket) instead of polling.
- Authentication / multi-user support.
- An MCP server on top of the REST API.
