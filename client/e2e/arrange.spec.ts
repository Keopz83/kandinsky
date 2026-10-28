import { byLabel, centerOf, expect, test } from './fixtures'

// Feature F4 Selection and arrangement: R-UI-8, R-UI-9, R-UI-10, R-UI-14.
test('select several shapes, align them and auto-layout the diagram', async ({ app, api, page }) => {
  const diagram = await api.create('Arrange')
  const a = await api.addShape(diagram.id, { label: 'A', x: 100, y: 100 })
  const b = await api.addShape(diagram.id, { label: 'B', x: 300, y: 160, height: 100 })
  const c = await api.addShape(diagram.id, { label: 'C', x: 500, y: 60 })
  await api.addEdge(diagram.id, c.id, a.id)
  await app.open(diagram)

  // A selection box selects every shape it touches.
  await app.drag({ x: 50, y: 40 }, { x: 320, y: 300 })
  await expect(app.shape('A')).toHaveClass(/selected/)
  await expect(app.shape('B')).toHaveClass(/selected/)
  await expect(app.shape('C')).not.toHaveClass(/selected/)

  // Ctrl/Cmd+click adds a shape.
  await app.click(centerOf(c), { modifiers: ['ControlOrMeta'] })
  await expect(app.shape('C')).toHaveClass(/selected/)

  await app.tool('Align horizontally').click()
  await expect.poll(async () => (await app.diagram()).shapes.map((s) => s.y + s.height / 2)).toEqual([130, 130, 130])
  await expect(app.shape('B').locator('rect.outline')).toHaveAttribute('y', '80')

  // Dragging one of the selected shapes moves all of them.
  await app.drag({ x: 170, y: 130 }, { x: 170, y: 230 })
  await expect.poll(async () => (await app.diagram()).shapes.map((s) => s.y + s.height / 2)).toEqual([230, 230, 230])

  await app.click({ x: 900, y: 600 })
  await expect(page.locator('g.shape.selected')).toHaveCount(0)

  // Auto-layout puts the source of an edge above its target.
  await app.tool('Auto-layout').click()
  await expect.poll(async () => {
    const d = await app.diagram()
    return byLabel(d, 'C').y < byLabel(d, 'A').y
  }).toBe(true)
})
