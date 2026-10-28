import { expect, test } from './fixtures'

// Feature F1 Whiteboards: R-UI-12, R-UI-13, R-UI-17.
test('create, rename, switch, reopen and delete whiteboards', async ({ app, api, page }) => {
  await app.start()
  const first = app.diagramId()
  expect((await app.diagram()).shapes).toHaveLength(0)

  await page.getByRole('button', { name: 'New whiteboard' }).click()
  await expect(page.getByRole('tab')).toHaveCount(2)
  await expect(page.getByRole('tab', { selected: true })).toHaveText('Untitled')
  expect(app.diagramId()).not.toBe(first)

  await page.getByRole('tab', { selected: true }).dblclick()
  await page.getByRole('textbox', { name: 'Diagram name' }).fill('Orders')
  await page.keyboard.press('Enter')
  await expect(app.tab('Orders')).toHaveAttribute('aria-selected', 'true')
  await expect.poll(async () => (await app.diagram()).name).toBe('Orders')

  await app.tab('Untitled').click()
  await expect(app.tab('Untitled')).toHaveAttribute('aria-selected', 'true')
  expect(app.diagramId()).toBe(first)

  // A reload reopens the whiteboard from the URL instead of creating a new one.
  await page.reload()
  await expect(app.tab('Untitled')).toHaveAttribute('aria-selected', 'true')
  await expect(page.getByRole('tab')).toHaveCount(2)

  // The unsaved whiteboard is deleted only after a confirmation.
  page.once('dialog', (dialog) => dialog.accept())
  await page.getByRole('button', { name: 'Delete Orders' }).click()
  await expect(page.getByRole('tab')).toHaveCount(1)
  await expect.poll(async () => (await api.list()).map((d) => d.name)).toEqual(['Untitled'])
})
