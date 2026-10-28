import { centerOf, expect, test } from './fixtures'

// Feature F3 Edges: R-MOD-3, R-UI-4, R-UI-4a, R-UI-5.
test('connect two shapes, switch the routing, label and delete the edge', async ({ app, api, page }) => {
  const diagram = await api.create('Edges')
  const a = await api.addShape(diagram.id, { label: 'Order', x: 100, y: 100 })
  const b = await api.addShape(diagram.id, { label: 'Customer', x: 100, y: 300 })
  await app.open(diagram)

  await app.tool('Directed edge').click()
  await app.drag(centerOf(a), centerOf(b))
  await expect(app.edges()).toHaveCount(1)
  await expect.poll(async () => (await app.diagram()).edges).toEqual([
    expect.objectContaining({ sourceId: a.id, targetId: b.id, routing: 'straight' }),
  ])
  // The edge tool stays active for further edges until Esc.
  await expect(app.tool('Directed edge')).toHaveClass(/active/)
  await page.keyboard.press('Escape')
  await expect(app.tool('Directed edge')).not.toHaveClass(/active/)

  // The edge runs from the bottom of Order (y 160) to the top of Customer (y 300).
  const middle = { x: 170, y: 230 }
  await app.click(middle, { button: 'right' })
  await expect(page.getByRole('menuitemradio', { name: 'Straight edge' })).toHaveAttribute('aria-checked', 'true')
  await page.getByRole('menuitemradio', { name: 'Angled edge' }).click()
  await expect(page.getByRole('menu')).toBeHidden()
  await expect.poll(async () => (await app.diagram()).edges[0].routing).toBe('angled')

  await app.dblclick(middle)
  await page.getByRole('textbox', { name: 'Edge label' }).fill('places')
  await page.keyboard.press('Enter')
  await expect(app.edges().locator('text')).toHaveText('places')
  await expect.poll(async () => (await app.diagram()).edges[0].label).toBe('places')

  await app.click(middle)
  await expect(app.edges()).toHaveClass(/selected/)
  await page.keyboard.press('Delete')
  await expect(app.edges()).toHaveCount(0)
  await expect.poll(async () => (await app.diagram()).edges).toHaveLength(0)
  expect((await app.diagram()).shapes).toHaveLength(2)
})

test('the angled edge tool draws angled edges', async ({ app, api }) => {
  const diagram = await api.create('Edges')
  const a = await api.addShape(diagram.id, { label: 'Order', x: 100, y: 100 })
  const b = await api.addShape(diagram.id, { label: 'Customer', x: 400, y: 300 })
  await app.open(diagram)

  await app.tool('Angled edge').click()
  await app.drag(centerOf(a), centerOf(b))

  await expect.poll(async () => (await app.diagram()).edges).toEqual([
    expect.objectContaining({ sourceId: a.id, targetId: b.id, routing: 'angled' }),
  ])
})
