import { byLabel, expect, test } from './fixtures'

// Feature F2 Shapes: R-MOD-2, R-UI-2, R-UI-3, R-UI-3a, R-UI-3c, R-UI-5, R-UI-7.
test('place, relabel, move, resize, copy and delete a shape', async ({ app, page }) => {
  await app.start()

  // Clicking the tool selects it; the next click on the canvas places the shape there.
  await app.tool('Rectangle').click()
  await expect(app.tool('Rectangle')).toHaveClass(/active/)
  await app.click({ x: 200, y: 150 })
  await expect(app.tool('Rectangle')).not.toHaveClass(/active/)
  await expect(app.shape('Class')).toHaveClass(/selected/)
  await expect.poll(async () => (await app.diagram()).shapes).toEqual([
    expect.objectContaining({ kind: 'rectangle', label: 'Class', x: 130, y: 120, width: 140, height: 60 }),
  ])

  await app.dblclick({ x: 200, y: 150 })
  await page.getByRole('textbox', { name: 'Shape label' }).fill('Order')
  await page.keyboard.press('Enter')
  await expect(app.shape('Order')).toBeVisible()

  await app.drag({ x: 200, y: 150 }, { x: 300, y: 200 })
  await expect.poll(async () => (await app.diagram()).shapes[0]).toMatchObject({ label: 'Order', x: 230, y: 170 })
  // Wait until the canvas shows the server state before the next drag.
  const outline = app.shape('Order').locator('rect.outline')
  await expect(outline).toHaveAttribute('x', '230')

  // The resize handle sits on the bottom right corner of the selected shape.
  await expect(app.shape('Order').locator('rect.handle')).toHaveAttribute('x', '365')
  await app.drag({ x: 370, y: 230 }, { x: 410, y: 250 })
  await expect.poll(async () => (await app.diagram()).shapes[0]).toMatchObject({ width: 180, height: 80 })
  await expect(outline).toHaveAttribute('width', '180')

  await app.drag({ x: 300, y: 200 }, { x: 300, y: 400 }, { shift: true })
  await expect(app.shape('Order')).toHaveCount(2)
  await expect.poll(async () => (await app.diagram()).shapes.map((s) => s.y)).toEqual([170, 370])

  await app.click({ x: 300, y: 400 })
  await page.keyboard.press('Delete')
  await expect(app.shape('Order')).toHaveCount(1)
  await expect.poll(async () => (await app.diagram()).shapes.map((s) => s.y)).toEqual([170])
})

test('drag flowchart symbols from the palette onto the canvas', async ({ app }) => {
  await app.start()

  await app.tool('Database').dragTo(app.canvas, { targetPosition: { x: 200, y: 150 } })
  await app.tool('Decision').dragTo(app.canvas, { targetPosition: { x: 450, y: 150 } })

  await expect(app.shapes()).toHaveCount(2)
  await expect.poll(async () => (await app.diagram()).shapes.map((s) => [s.kind, s.label])).toEqual([
    ['database', 'Database'],
    ['decision', 'Decision'],
  ])
})

// R-MOD-2a, R-MOD-4, R-UI-3b.
test('a group contains the shapes placed in it and moves them along', async ({ app, page }) => {
  await app.start()

  await app.tool('Group').click()
  await app.click({ x: 400, y: 300 })
  await app.tool('Rectangle').click()
  await app.click({ x: 400, y: 320 })
  await expect(app.shape('Class')).toBeVisible()
  await expect.poll(async () => {
    const d = await app.diagram()
    return byLabel(d, 'Class').parentId === byLabel(d, 'Group').id
  }).toBe(true)

  // Dragging the group by its title bar moves its content too.
  await app.drag({ x: 300, y: 210 }, { x: 400, y: 260 })
  await expect.poll(async () => byLabel(await app.diagram(), 'Class')).toMatchObject({ x: 430, y: 340 })

  await app.click({ x: 400, y: 260 })
  await page.keyboard.press('Delete')
  await expect(app.shapes()).toHaveCount(0)
  await expect.poll(async () => (await app.diagram()).shapes).toHaveLength(0)
})
