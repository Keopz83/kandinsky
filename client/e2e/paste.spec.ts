import { expect, test } from './fixtures'

const PASTED = `flowchart TD
  A[Order] --> B[(Orders DB)]
`

// Feature F7 Paste: R-UI-22, R-API-5a.
test('paste a Mermaid diagram from the clipboard into the whiteboard', async ({ app, api, page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write'])
  const diagram = await api.create('Paste')
  await api.addShape(diagram.id, { label: 'Existing', x: 40, y: 40 })
  await app.open(diagram)

  await page.evaluate((text) => navigator.clipboard.writeText(text), PASTED)
  const pointer = await app.at({ x: 500, y: 400 })
  await page.mouse.move(pointer.x, pointer.y)
  await page.keyboard.press('ControlOrMeta+v')

  await expect(app.shape('Order')).toHaveClass(/selected/)
  await expect(app.shape('Orders DB')).toHaveClass(/selected/)
  await expect(app.shape('Existing')).not.toHaveClass(/selected/)
  await expect(app.edges()).toHaveCount(1)

  const shapes = (await app.diagram()).shapes
  expect(shapes.map((s) => [s.kind, s.label])).toEqual([
    ['rectangle', 'Existing'],
    ['rectangle', 'Order'],
    ['database', 'Orders DB'],
  ])
  // The pasted shapes are centered on the mouse pointer.
  const pasted = shapes.slice(1)
  const left = Math.min(...pasted.map((s) => s.x))
  const right = Math.max(...pasted.map((s) => s.x + s.width))
  const top = Math.min(...pasted.map((s) => s.y))
  const bottom = Math.max(...pasted.map((s) => s.y + s.height))
  expect(Math.abs((left + right) / 2 - 500)).toBeLessThanOrEqual(1)
  expect(Math.abs((top + bottom) / 2 - 400)).toBeLessThanOrEqual(1)

  // Text that is not a diagram is reported and changes nothing.
  await page.evaluate(() => navigator.clipboard.writeText('just some text'))
  await page.keyboard.press('ControlOrMeta+v')
  await expect(app.status).toHaveClass(/text-danger/)
  expect((await app.diagram()).shapes).toHaveLength(3)
})
