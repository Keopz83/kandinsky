import { test as base, expect, type APIRequestContext, type APIResponse, type Locator, type Page } from '@playwright/test'

export { expect }

export interface Shape {
  id: string
  kind: string
  label: string
  x: number
  y: number
  width: number
  height: number
  parentId?: string | null
}

export interface Edge {
  id: string
  sourceId: string
  targetId: string
  label?: string | null
  routing: 'straight' | 'angled'
}

export interface Diagram {
  id: string
  name: string
  shapes: Shape[]
  edges: Edge[]
}

/** REST client for test setup and for checking the server state, which owns the diagrams (R-ARCH-2). */
export class Api {
  constructor(private readonly request: APIRequestContext) {}

  private async json<T>(response: Promise<APIResponse>): Promise<T> {
    const r = await response
    expect(r.ok(), `${r.url()} → ${r.status()} ${await r.text()}`).toBeTruthy()
    return (r.status() === 204 ? undefined : await r.json()) as T
  }

  list = () => this.json<{ id: string; name: string }[]>(this.request.get('/api/diagrams'))
  get = (id: string) => this.json<Diagram>(this.request.get(`/api/diagrams/${id}`))
  create = (name: string) => this.json<Diagram>(this.request.post('/api/diagrams', { data: { name } }))
  addShape = (id: string, shape: Partial<Omit<Shape, 'id'>>) => this.json<Shape>(this.request.post(`/api/diagrams/${id}/shapes`, { data: shape }))
  addEdge = (id: string, sourceId: string, targetId: string, extra: Partial<Edge> = {}) =>
    this.json<Edge>(this.request.post(`/api/diagrams/${id}/edges`, { data: { sourceId, targetId, ...extra } }))

  async deleteAll() {
    for (const d of await this.list()) {
      await this.json(this.request.delete(`/api/diagrams/${d.id}`))
    }
  }
}

type Point = { x: number; y: number }

/** The Kandinsky UI. Diagram coordinates equal canvas coordinates as long as the whiteboard is not zoomed or panned. */
export class App {
  readonly canvas: Locator
  readonly status: Locator

  constructor(readonly page: Page, readonly api: Api) {
    this.canvas = page.locator('.canvas > svg')
    this.status = page.locator('footer.status')
  }

  /** Opens the app, which starts on a new blank whiteboard (R-UI-12). */
  async start() {
    await this.page.goto('/')
    await expect(this.tab('Untitled')).toHaveAttribute('aria-selected', 'true')
    await expect(this.page).toHaveURL(/#.+/)
  }

  /** Opens an existing diagram through its link (the id in the URL hash). */
  async open(diagram: Diagram) {
    await this.page.goto(`/#${diagram.id}`)
    await expect(this.tab(diagram.name)).toHaveAttribute('aria-selected', 'true')
  }

  diagramId() {
    return decodeURIComponent(new URL(this.page.url()).hash.slice(1))
  }

  diagram() {
    return this.api.get(this.diagramId())
  }

  tab(name: string) {
    return this.page.getByRole('tab', { name, exact: true })
  }

  tool(name: string) {
    return this.page.locator('aside.sidebar').getByRole('button', { name, exact: true })
  }

  shape(label: string) {
    return this.page.locator('g.shape:not(.ghost)').filter({ has: this.page.locator('.label', { hasText: new RegExp(`^${label}$`) }) })
  }

  shapes() {
    return this.page.locator('g.shape:not(.ghost)')
  }

  edges() {
    return this.page.locator('g.edge:not(.ghost)')
  }

  async menu(menu: 'File' | 'Settings', item: string) {
    await this.page.locator('header.menubar').getByRole('button', { name: menu, exact: true }).click()
    // Menu items may end with their shortcut ("Save ⌘S"), so "Save" must not match "Save As…".
    const name = new RegExp(`^${item.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(\\s+(Ctrl|⌘|⇧).*)?$`)
    await this.page.getByRole(menu === 'File' ? 'menuitem' : 'menuitemradio', { name }).click()
  }

  /** Page coordinates of a point on the whiteboard. */
  async at(p: Point) {
    const box = (await this.canvas.boundingBox())!
    return { x: box.x + p.x, y: box.y + p.y }
  }

  async click(p: Point, options: { modifiers?: ('Shift' | 'ControlOrMeta')[]; button?: 'left' | 'right' } = {}) {
    await this.canvas.click({ position: p, force: true, ...options })
  }

  async dblclick(p: Point) {
    await this.canvas.dblclick({ position: p, force: true })
  }

  /** Drags on the whiteboard from one point to another, optionally holding Shift. */
  async drag(from: Point, to: Point, options: { shift?: boolean; button?: 'left' | 'middle' } = {}) {
    const mouse = this.page.mouse
    const a = await this.at(from)
    const b = await this.at(to)
    if (options.shift) {
      await this.page.keyboard.down('Shift')
    }
    await mouse.move(a.x, a.y)
    await mouse.down({ button: options.button })
    await mouse.move(b.x, b.y, { steps: 8 })
    await mouse.up({ button: options.button })
    if (options.shift) {
      await this.page.keyboard.up('Shift')
    }
  }

  /** The view of the whiteboard: pan offset and zoom factor. */
  async view() {
    const transform = (await this.canvas.locator(':scope > g').getAttribute('transform'))!
    const [, x, y, zoom] = transform.match(/translate\(([-\d.e]+) ([-\d.e]+)\) scale\(([-\d.e]+)\)/)!.map(Number)
    return { x, y, zoom }
  }
}

export function byLabel(diagram: Diagram, label: string) {
  const shape = diagram.shapes.find((s) => s.label === label)
  expect(shape, `shape "${label}"`).toBeDefined()
  return shape!
}

export const centerOf = (s: Pick<Shape, 'x' | 'y' | 'width' | 'height'>): Point => ({ x: s.x + s.width / 2, y: s.y + s.height / 2 })

export const test = base.extend<{ api: Api; app: App }>({
  api: async ({ request }, use) => {
    await use(new Api(request))
  },
  app: async ({ page, api }, use) => {
    // Every test starts on an empty server.
    await api.deleteAll()
    await use(new App(page, api))
  },
})
