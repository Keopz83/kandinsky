# Kandinsky — Requirements

Web app for creating code UML diagrams. Server-side processing, thin React client, REST API for automated clients (e.g. an MCP server).

Requirements are grouped by main feature (F1 … F9) after the cross-cutting ones. Requirement ids are stable and do not encode the feature.

## Main features

| Feature | Scope | Requirements | E2E test (`client/e2e/`) |
|---|---|---|---|
| [F1 Whiteboards](#f1-whiteboards) | Create, switch, rename and delete diagrams shown as tabs. | R-MOD-1, R-UI-12, R-UI-13, R-UI-17 | [whiteboards.spec.ts](client/e2e/whiteboards.spec.ts) |
| [F2 Shapes](#f2-shapes) | Place shapes and flowchart symbols from the palette, edit them and nest them in groups. | R-MOD-2, R-MOD-2a, R-MOD-4, R-UI-2, R-UI-3, R-UI-3a, R-UI-3b, R-UI-3c, R-UI-5, R-UI-7 | [shapes.spec.ts](client/e2e/shapes.spec.ts) |
| [F3 Edges](#f3-edges) | Connect shapes with directed edges, route and label them. | R-MOD-3, R-UI-4, R-UI-4a, R-UI-23 | [edges.spec.ts](client/e2e/edges.spec.ts) |
| [F4 Selection and arrangement](#f4-selection-and-arrangement) | Select several objects, move them together, align and auto-layout. | R-UI-8, R-UI-14, R-UI-9, R-UI-10, R-UI-11 | [arrange.spec.ts](client/e2e/arrange.spec.ts) |
| [F5 Navigation](#f5-navigation) | Zoom and pan the whiteboard. | R-UI-15, R-UI-19 | [navigation.spec.ts](client/e2e/navigation.spec.ts) |
| [F6 Files (PlantUML / Mermaid)](#f6-files-plantuml--mermaid) | Open and save diagrams as PlantUML or Mermaid files that keep the layout. | R-UI-1, R-UI-16, R-UI-18, R-IO-1, R-IO-2, R-IO-3, R-IO-4, R-IO-5, R-IO-6, R-IO-6a, R-IO-6b, R-IO-6d, R-IO-6e, R-IO-6c, R-IO-7 | [files.spec.ts](client/e2e/files.spec.ts) |
| [F7 Paste](#f7-paste) | Paste PlantUML or Mermaid text from the clipboard into the open whiteboard. | R-UI-22 | [paste.spec.ts](client/e2e/paste.spec.ts) |
| [F8 Image export](#f8-image-export) | Export the whiteboard as an SVG or PNG image. | R-IO-8 | [image-export.spec.ts](client/e2e/image-export.spec.ts) |
| [F9 REST API and live updates](#f9-rest-api-and-live-updates) | The REST API used by the UI and external clients; changes show up in the open whiteboard. | R-API-1, R-API-2, R-API-3, R-API-4, R-API-5, R-API-5a, R-API-6, R-API-7, R-UI-6 | [api.spec.ts](client/e2e/api.spec.ts) |

## Cross-cutting

### Architecture

- **R-ARCH-1** The app consists of a React frontend and a backend service.
- **R-ARCH-2** All diagram processing (model changes, validation, import, export) runs on the server. The server owns the diagram state.
- **R-ARCH-3** The client is thin: it renders the diagram state from the server and sends user actions to the server. It holds no diagram business logic.
- **R-ARCH-4** The UI and external clients (e.g. an MCP server) use the same REST API.

### Look and feel

- **R-UI-20** The UI uses Bootstrap CSS styling (menu bar, drop-down menus, palette buttons, tabs, form fields, status bar).
- **R-UI-21** Buttons use solid backgrounds and no borders.

### Testing

- **R-TEST-1** Every main feature (F1 … F9) has at least one Playwright end-to-end test that covers its happy path through the UI, against the real server. The test is listed in the feature table above.

## F1 Whiteboards

Create, switch, rename and delete diagrams shown as tabs. E2E test: [whiteboards.spec.ts](client/e2e/whiteboards.spec.ts).

- **R-MOD-1** A diagram has an id, a name and a collection of shapes and edges.
- **R-UI-12** The application opens with a new blank diagram.
- **R-UI-13** Multiple diagrams (whiteboards) are shown as tabs at the top of the canvas, one tab per diagram on the server. Clicking a tab opens that diagram, a **+** tab creates a new one and a tab's **×** deletes the diagram; the deletion is confirmed only if the diagram has unsaved changes (a diagram is saved if it is unchanged since it was last saved to or opened from a file). The left menu has no diagram list.
- **R-UI-17** The user renames a diagram directly on its tab: double-clicking the tab turns the name into a text field; Enter or leaving the field saves the name, Esc cancels. The name is shown only on the tab, not repeated as a title below the tabs.

## F2 Shapes

Place shapes and flowchart symbols from the palette, edit them and nest them in groups. E2E test: [shapes.spec.ts](client/e2e/shapes.spec.ts).

- **R-MOD-2** A shape has an id, a kind, a label/text, a position (x, y) and a size (width, height). The kind is **rectangle** (default), **database** (drawn as a cylinder within its bounds), **decision** (drawn as a diamond within its bounds), **group** (a container drawn as a frame with its label in a title bar), or one of the other flowchart symbols: **start** (small filled circle), **end** (framed circle), **terminal** (stadium), **rounded** (rounded rectangle, an event), **io** (input / output parallelogram), **subroutine** (predefined process, double vertical sides), **document** (wavy bottom), **connector** (labeled circle), **preparation** (hexagon) and **manual** (manual operation, trapezoid wider at the top). Start and end points show their label below them.
- **R-MOD-2a** Group membership follows the geometry: a shape belongs to the smallest group whose bounds fully contain it (groups can be nested). The server derives each shape's parent group (`parentId`) from the positions after every change; it cannot be set directly. Moving a shape into or out of a group, or resizing a group around shapes, changes the membership.
- **R-MOD-4** Deleting a shape also deletes the edges connected to it. Deleting a group also deletes every shape inside it (at any depth) and their edges.
- **R-UI-2** A palette of shapes in the left menu containing:
  - a basic rectangle,
  - a database (cylinder),
  - shapes: rectangle, rounded rectangle, database (cylinder), group (container),
  - flowchart symbols: start, end, terminal, decision (diamond), input / output, subroutine, document, preparation, manual operation, connector,
  - edges: a directed edge (straight) and an angled edge to connect two shapes.
- **R-UI-3** The user adds a rectangle to the canvas from the palette. Clicking the palette's rectangle button selects the rectangle tool (shown as active, like the directed edge tool) and does not add a rectangle yet; the next click on the canvas places a rectangle centered there, selects it and returns to selection. Clicking the button again or Esc cancels. Dragging the button onto the canvas places a rectangle at the drop position.
- **R-UI-3a** The database tool works like the rectangle tool (R-UI-3): click to select it and click on the canvas, or drag it onto the canvas. Databases can be connected, moved, resized, relabeled, copied, aligned and deleted like rectangles.
- **R-UI-3b** The decision and group tools work like the rectangle tool (R-UI-3). A group is drawn behind edges and other shapes. Dragging a group moves (or with Shift copies) everything inside it along; resizing it leaves its content in place. A selection box selects a group only when it encloses the whole group. Shapes placed or dropped fully inside a group become part of it (R-MOD-2a).
- **R-UI-3c** All flowchart symbol tools work like the rectangle tool (R-UI-3).
- **R-UI-5** The user can move, resize, relabel and delete rectangles and edges on the canvas.
- **R-UI-7** Dragging a rectangle with Shift held creates a copy (same label and size) at the drop position; the original stays in place. Edges are not copied.

## F3 Edges

Connect shapes with directed edges, route and label them. E2E test: [edges.spec.ts](client/e2e/edges.spec.ts).

- **R-MOD-3** An edge connects a source shape to a target shape and is directed (source → target). It has an id, an optional label and a routing: **straight** (default, one line between the outlines) or **angled** (horizontal and vertical segments leaving and entering through the middle of a side; a Z shape for shapes above each other or side by side, an L shape for diagonal ones). Edges attach to a diamond at its corners: by default the corner facing the other end, or a corner fixed per edge end (`sourceCorner` / `targetCorner`: `auto`, `top`, `right`, `bottom`, `left`; R-UI-23). Any shape can be an endpoint, including decisions and groups.
- **R-UI-4** The user connects two rectangles with a directed edge from the palette.
- **R-UI-4a** The directed edge and angled edge tools are independent palette tools working the same way (R-UI-4); the edge gets the tool's routing. Right-clicking an edge selects it and opens a menu to switch it between straight and angled (the current routing is checked); the menu closes after a choice, on Esc, on a click elsewhere or when zooming or panning.
- **R-UI-23** The user drags to change the diamond corner an edge is attached to: a selected edge shows a handle on each end attached to a decision diamond; dragging the handle shows the diamond's four corners, highlights the one nearest the pointer and redraws the edge there, releasing fixes the edge end to that corner (straight or angled; an angled edge reroutes with horizontal and vertical segments, picking the side of the other shape with the fewest turns). Leaving the whiteboard cancels. The edge's right-click menu offers **Automatic diamond corners** to return to the corners facing the other end.

## F4 Selection and arrangement

Select several objects, move them together, align and auto-layout. E2E test: [arrange.spec.ts](client/e2e/arrange.spec.ts).

- **R-UI-8** The user selects multiple rectangles with Ctrl/Cmd+click (toggles a rectangle) or by dragging a selection box on the empty canvas (selects every rectangle the box touches, even partially; with Ctrl/Cmd held the box adds to the selection).
- **R-UI-14** Dragging one of several selected rectangles moves all selected rectangles together, keeping their relative positions; with Shift held all of them are copied (R-UI-7). Clicking a selected rectangle without dragging selects only that rectangle.
- **R-UI-9** An align tool lines up the selected rectangles (at least two; an aligned group moves its content along):
  - **horizontally** — in a row, all vertical centers on the first selected rectangle's center;
  - **vertically** — in a column, all horizontal centers on the first selected rectangle's center.
- **R-UI-10** An auto-layout button arranges all rectangles of the diagram: connected rectangles in layers following the edge direction (top to bottom, with as few crossings as practical), unconnected rectangles in a grid below. The content of each group is arranged the same way inside the group, which is resized to fit it; edges between shapes in different groups order the groups.
- **R-UI-11** Align and auto-layout run on the server and are available in the REST API.

## F5 Navigation

Zoom and pan the whiteboard. E2E test: [navigation.spec.ts](client/e2e/navigation.spec.ts).

- **R-UI-15** Mouse wheel navigation on the whiteboard: pressing the wheel and dragging pans the whiteboard; rotating the wheel forward zooms in, backward zooms out, centered on the mouse pointer.
- **R-UI-19** Trackpad gestures on the whiteboard (as on macOS): moving two fingers drags (pans) the whiteboard in any direction; pinching zooms, centered on the pointer. A mouse wheel keeps zooming (R-UI-15). On touch screens, moving two fingers also pans.

## F6 Files (PlantUML / Mermaid)

Open and save diagrams as PlantUML or Mermaid files that keep the layout. E2E test: [files.spec.ts](client/e2e/files.spec.ts).

- **R-UI-1** A header menu bar at the top of the application with a **File** drop-down menu containing all file operations:
  - **New** — create an empty diagram.
  - **Open** — open a PlantUML or Mermaid file as a diagram (these standard formats are the persistence format, so there is no separate import/export).
  - **Save** — save the current diagram to a file. The first Save of a diagram opens a dialog to choose the location (and format); further Saves overwrite that file without asking. A diagram opened from a file is saved back to that file (in its format) without asking, even on the first Save. After the diagram is renamed, Save opens the dialog again with the new name as the suggested file name, to save a new file instead of overwriting the file with the old name.
  - **Save As** — always opens the dialog to save the current diagram to a new location.
- **R-UI-16** A **Settings** drop-down menu in the header menu bar sets the default save format (PlantUML or Mermaid) used by Save / Save As. The default is **Mermaid**, the most widely used diagrams-as-code format (native rendering in GitHub, GitLab, Azure DevOps, Notion, Confluence). The setting is kept across sessions.
- **R-UI-18** Keyboard shortcuts: Ctrl+S (Cmd+S on macOS) runs **Save**, Ctrl+Shift+S (Cmd+Shift+S) runs **Save As**. The browser's own save-page action is suppressed. The File menu shows the shortcuts.
- **R-IO-1** Export a diagram to PlantUML.
- **R-IO-2** Export a diagram to Mermaid.
- **R-IO-3** Import a diagram from PlantUML.
- **R-IO-4** Import a diagram from Mermaid.
- **R-IO-5** Import reports unsupported or invalid syntax as an error instead of failing silently.
- **R-IO-6** Export keeps the layout: PlantUML and Mermaid cannot express positions, so the position and size of every shape are written as format comments (`' kandinsky:layout <id> x=<x> y=<y> width=<width> height=<height>` in PlantUML, `%% kandinsky:layout …` in Mermaid). Other tools ignore these comments and render the diagram as usual.
- **R-IO-6a** Databases are PlantUML `database` elements (`database "Oracle" as DB`; export adds `allowmixing` so PlantUML accepts them in a class diagram) and Mermaid flowchart database nodes (`DB[(Oracle)]`). Opening a file and pasting (R-UI-22) keep them as databases. Mermaid class diagrams cannot express databases, so a diagram with databases is exported as a Mermaid flowchart; other Mermaid node shapes are imported as rectangles.
- **R-IO-6b** Decisions are Mermaid flowchart decision nodes (`D{Valid?}`) and PlantUML `hexagon` elements (`hexagon "Valid?" as D`; PlantUML has no labeled diamond outside activity diagrams). PlantUML `diamond` and `<>` elements are imported as decisions too.
- **R-IO-6d** Flowchart symbols are Mermaid flowchart nodes: classic syntax where it exists (`(text)` rounded, `([text])` terminal, `[[text]]` subroutine, `((text))` connector, `{{text}}` preparation, `[/text/]` io, `[\text/]` manual) and the Mermaid v11 shape syntax otherwise (`A@{ shape: sm-circ, label: "Start" }` start, `fr-circ` end, `doc` document). Import accepts both syntaxes with the v11 aliases (e.g. `stop`, `pill`, `subprocess`, `lean-l`, `trap-t`, `dbl-circ`, `(((text)))` as end); other shapes become rectangles. PlantUML has no such elements in class and deployment diagrams, so export writes the closest element (`circle`, `usecase`, `storage`, `file`, `hexagon`, `rectangle`) and adds `kind=<kind>` to the layout comment when that element would be imported as another kind; import maps `circle` to start, `usecase` to terminal, `storage` to rounded and `file` to document unless the layout comment says otherwise. A Mermaid diagram with flowchart symbols is exported as a flowchart.
- **R-IO-6e** Edge routing: when all edges are angled, export sets the format's own orthogonal line style (PlantUML `skinparam linetype ortho`, Mermaid `%%{init: {"flowchart": {"curve": "step"}}}%%`); otherwise each angled edge is listed in a comment (`kandinsky:edge <source> <target> routing=angled`), since neither format can route single edges. Edges with a fixed diamond corner (R-MOD-3) are listed too, with the corners added (`kandinsky:edge D B routing=straight from=bottom to=left`; `from` / `to` are optional). Import reads both: the line style makes all edges angled, edge comments set single edges (in order, for repeated source/target pairs). Malformed edge comments and comments for unknown edges are reported as errors (R-IO-5). Mermaid diagrams with angled edges are exported as flowcharts.
- **R-IO-6c** Groups are Mermaid flowchart subgraphs (`subgraph G ["Back end"]` … `end`, nested subgraphs and edges to subgraphs included; a node belongs to the first subgraph it appears in) and PlantUML `package` blocks (`package "Back end" as G {` … `}`). Import also accepts Mermaid class diagram `namespace X {` … `}` blocks and PlantUML `namespace`, `rectangle`, `frame`, `folder`, `node`, `cloud` and `component` blocks. Export writes the shapes inside their group's block. A Mermaid diagram with decisions or groups is exported as a flowchart, like one with databases (R-IO-6a).
- **R-IO-7** Import restores the layout from these comments. Files without layout comments are arranged with the auto-layout (R-UI-10); in files with layout for only some shapes, the shapes without layout are placed in free grid slots. Malformed layout comments and layout for unknown shapes are reported as errors (R-IO-5).

## F7 Paste

Paste PlantUML or Mermaid text from the clipboard into the open whiteboard. E2E test: [paste.spec.ts](client/e2e/paste.spec.ts).

- **R-UI-22** Ctrl+V (Cmd+V on macOS) on the whiteboard pastes a PlantUML or Mermaid diagram from the clipboard into the open whiteboard. The pasted classes and associations are added as new rectangles and edges, keep their relative layout (layout comments, else auto-layout, R-IO-7), are centered on the mouse pointer (or the visible area when the pointer is outside the whiteboard) and become the selection. Shape ids already used in the whiteboard are replaced by new ones. Text that is not a supported diagram is reported as an error (R-IO-5) and changes nothing. Pasting into a text field (e.g. a label) is a normal text paste.

## F8 Image export

Export the whiteboard as an SVG or PNG image. E2E test: [image-export.spec.ts](client/e2e/image-export.spec.ts).

- **R-IO-8** Export the open whiteboard as an image: **File → Export as SVG…** and **Export as PNG…**. The image shows the shapes, labels and edges as on the whiteboard (without selection, handles or grid), cropped to the content with a margin, on a white background, and is named after the whiteboard. The SVG is standalone (labels are SVG text, so any viewer shows them); the PNG has twice the resolution, so it stays sharp. An empty whiteboard is reported as an error.

## F9 REST API and live updates

The REST API used by the UI and external clients; changes show up in the open whiteboard. E2E test: [api.spec.ts](client/e2e/api.spec.ts).

- **R-API-1** Diagrams: create, list, get, rename, delete.
- **R-API-2** Shapes: add, get, update (label, position, size), delete within a diagram.
- **R-API-3** Edges: add (source, target), get, update (label, endpoints, routing, diamond corners), delete within a diagram. Changing an endpoint without giving its corner resets that corner to `auto`.
- **R-API-4** Import a diagram from PlantUML or Mermaid text.
- **R-API-5** Export a diagram as PlantUML or Mermaid text.
- **R-API-5a** Paste PlantUML or Mermaid text into an existing diagram (R-UI-22); the response contains the ids of the added shapes and edges.
- **R-API-6** The API uses JSON, correct HTTP status codes and validates input (e.g. edge endpoints must reference existing shapes).
- **R-API-7** The API is described by an OpenAPI specification, so tools like an MCP server can be generated or written against it.
- **R-UI-6** Every change in the UI is sent to the server; the canvas shows the state returned by the server.

## Decisions (first implementation)

- Backend: ASP.NET Core minimal API (.NET 10), OpenAPI at `/openapi/v1.json`. Frontend: React + Vite + TypeScript, SVG canvas, Bootstrap 5 CSS (npm package `bootstrap`, CSS only — drop-downs are React state, not Bootstrap JavaScript).
- Persistence: diagrams live in server memory. The API endpoint `POST /api/diagrams/{id}/save` writes them as JSON files (`Storage:Path`, default `data/`), loaded on startup; the UI does not use it.
- Save / Save As write PlantUML or Mermaid with layout comments (R-IO-6), so saved files are reopened with Open. The format follows the extension chosen in the dialog; the default save format is offered first. The dialog uses the browser's File System Access API (Chrome, Edge); other browsers download the file in the default format. Open uses the same API's open dialog so that Save can write back to the opened file; the format of an opened file follows its extension, or its content for other extensions (e.g. `.txt`). Browsers without it open files through a regular file input, and the first Save asks for a location. If the browser refuses write access to the chosen file, it is downloaded instead and the next Save asks again. The default save format is stored in the browser (`localStorage`).
- Saved files contain no diagram name (no `title`); the name corresponds exactly to the file name (without extension). When a file name is chosen in the save dialog, the diagram is renamed to it; later Saves to the same file keep the current name. Opening a file names the diagram after the file, ignoring a title in it. A diagram renamed since it was saved counts as unsaved.
- Import/export: class diagram subset — classes (PlantUML also `interface`, `enum`, `abstract`, `rectangle`) and directed associations (`-->`, `..>`, reversed `<--`, `<..`) with optional labels. Class members and other relation types are rejected with a line-numbered error.
- Mermaid flowcharts (`flowchart` / `graph`) are imported too: nodes keep their flowchart symbol (R-IO-6d), other node shapes become rectangles, all labeled with their text (`<br/>` becomes a space), subgraphs become groups, directed links (`-->`, `-.->`, `==>`, with `|label|` or `-- label -->`, chains and `&`) become edges, styling lines (`style`, `classDef`, `class`, `linkStyle`, `click`, `direction`) are ignored. Undirected links are rejected. Export writes a class diagram, or a flowchart when the diagram has shapes other than rectangles or angled edges.
- Groups declared with a title only (Mermaid `subgraph Some title`, PlantUML `package com.example {`) get a generated id (`group1`, …).
- Text wrapped in a Markdown code fence (```` ```mermaid ```` … ```` ``` ````) is accepted on import and paste.
- Imported diagrams keep the layout from their layout comments, otherwise they are auto-laid out (R-IO-7); shapes added without a position are placed in the first free grid slot.
- Live updates: the UI polls the open diagram and the diagram list (tabs) every 2 seconds.
- Tabs keep their order; diagrams created elsewhere are appended at the end. If the open diagram is deleted, the neighboring tab opens.
- Self-referencing edges are rejected.
- On start the UI creates a blank "Untitled" diagram on the server. The open diagram's id is kept in the URL hash, so a reload or link reopens that diagram instead (a new one is created if it no longer exists).
- Image export (R-IO-8) is rendered in the browser from the same geometry as the whiteboard, saved like Save (File System Access API, else a download); the PNG is the SVG drawn on a canvas. The REST API has no image export.
- End-to-end tests (R-TEST-1): Playwright with Chromium in `client/e2e/`, one spec file per main feature, run with `npm run test:e2e`. Playwright starts its own backend (port 5091, empty temporary storage) and Vite (port 5191), so a running dev setup is not touched. The tests run one after another because every tab shows all diagrams on the server; each test starts on an empty server. Native file dialogs cannot be automated, so the File System Access API is replaced by an in-memory fake; the download fallback is tested as well.

## Open points

- Which UML element types beyond rectangles, flowchart symbols, groups and directed (straight or angled) edges (classes with attributes/methods, interfaces, association types such as inheritance/composition).
- Exporting layout hints (e.g. PlantUML `-right->`, Mermaid `direction`) so external renderers approximate the Kandinsky layout.
- Push-based live updates (SSE/WebSocket) instead of polling.
- Authentication / multi-user support.
- An MCP server on top of the REST API.
