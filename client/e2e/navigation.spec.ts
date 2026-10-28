import { expect, test } from './fixtures'

// Feature F5 Navigation: R-UI-15, R-UI-19.
test('zoom with the mouse wheel and pan with the pressed wheel', async ({ app, api, page }) => {
  const diagram = await api.create('Navigate')
  await api.addShape(diagram.id, { label: 'Order', x: 100, y: 100 })
  await app.open(diagram)
  expect(await app.view()).toEqual({ x: 0, y: 0, zoom: 1 })

  const pointer = await app.at({ x: 400, y: 300 })
  await page.mouse.move(pointer.x, pointer.y)
  await page.mouse.wheel(0, -200)
  await expect.poll(async () => (await app.view()).zoom).toBeGreaterThan(1)

  // Zooming keeps the point under the pointer in place.
  const zoomed = await app.view()
  expect((400 - zoomed.x) / zoomed.zoom).toBeCloseTo(400, 0)
  expect((300 - zoomed.y) / zoomed.zoom).toBeCloseTo(300, 0)

  await page.mouse.wheel(0, 400)
  await expect.poll(async () => (await app.view()).zoom).toBeLessThan(zoomed.zoom)

  const before = await app.view()
  await app.drag({ x: 400, y: 300 }, { x: 500, y: 250 }, { button: 'middle' })
  const panned = await app.view()
  expect(panned.x).toBeCloseTo(before.x + 100)
  expect(panned.y).toBeCloseTo(before.y - 50)
  expect(panned.zoom).toBe(before.zoom)

  // Panning and zooming change only the view, not the diagram.
  expect((await app.diagram()).shapes[0]).toMatchObject({ x: 100, y: 100 })
})

test('pinch to zoom and move two fingers to pan on a trackpad', async ({ app, page }) => {
  await app.start()
  const pointer = await app.at({ x: 400, y: 300 })
  await page.mouse.move(pointer.x, pointer.y)

  // Browsers report a trackpad pinch as a wheel event with Ctrl held.
  await page.keyboard.down('Control')
  await page.mouse.wheel(0, -20)
  await page.keyboard.up('Control')
  await expect.poll(async () => (await app.view()).zoom).toBeGreaterThan(1)

  const before = await app.view()
  // A two-finger move sends smooth pixel deltas, horizontal ones too.
  await page.mouse.wheel(30, 40)
  await expect.poll(async () => (await app.view()).x).toBeCloseTo(before.x + 30)
  const panned = await app.view()
  expect(panned.y).toBeCloseTo(before.y + 40)
  expect(panned.zoom).toBe(before.zoom)
})
