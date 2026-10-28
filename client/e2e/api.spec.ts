import { expect, test } from './fixtures'

// Feature F9 REST API and live updates: R-ARCH-4, R-API-1 … R-API-7, R-UI-6.
test('changes made through the REST API show up in the open whiteboard', async ({ app, api, page, request }) => {
  await app.start()
  const id = app.diagramId()

  const order = await api.addShape(id, { label: 'Order', x: 100, y: 100 })
  const customer = await api.addShape(id, { label: 'Customer', x: 100, y: 300 })
  await api.addEdge(id, order.id, customer.id)
  await expect(app.shape('Order')).toBeVisible()
  await expect(app.shape('Customer')).toBeVisible()
  await expect(app.edges()).toHaveCount(1)

  const created = await api.create('From API')
  await expect(app.tab('From API')).toBeVisible()
  await expect(app.tab('Untitled')).toHaveAttribute('aria-selected', 'true')

  const imported = await request.post('/api/diagrams/import', { data: { text: 'classDiagram\n  class Invoice\n', name: 'Imported' } })
  expect(imported.status()).toBe(201)
  await expect(app.tab('Imported')).toBeVisible()

  // Deleting the open whiteboard elsewhere switches to another one.
  await request.delete(`/api/diagrams/${id}`)
  await expect(app.tab('Untitled')).toHaveCount(0)
  await expect(page.getByRole('tab', { selected: true })).toBeVisible()
  expect([created.id, (await imported.json()).id]).toContain(app.diagramId())

  // Invalid input is rejected with a problem details response.
  const invalid = await request.post(`/api/diagrams/${created.id}/edges`, { data: { sourceId: 'missing', targetId: 'missing' } })
  expect(invalid.status()).toBe(400)
  expect(invalid.headers()['content-type']).toContain('application/problem+json')

  const openApi = await request.get('/openapi/v1.json')
  expect(openApi.ok()).toBeTruthy()
  expect((await openApi.json()).paths).toHaveProperty(['/api/diagrams'])
})
