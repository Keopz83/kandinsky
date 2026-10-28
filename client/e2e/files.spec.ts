import type { Page } from '@playwright/test'
import { expect, test } from './fixtures'

const SHOP = `classDiagram
  class Order
  class Customer
  Order --> Customer : placed by
`

declare global {
  interface Window {
    /** Files of the fake file system: name → content. */
    files: Record<string, string>
    /** File names the fake dialogs return, and the options they were opened with. */
    nextOpen: string
    nextSave: string
    pickerCalls: { kind: 'open' | 'save'; suggestedName?: string }[]
  }
}

/**
 * Replaces the File System Access API dialogs (native dialogs can't be automated) with an in-memory file system,
 * so the Chrome path of Open / Save / Save As (R-UI-1) runs as for a user.
 */
async function fakeFileSystem(page: Page, files: Record<string, string>) {
  await page.addInitScript((initial) => {
    window.files = initial
    window.pickerCalls = []
    const handle = (name: string) => ({
      kind: 'file',
      name,
      getFile: async () => new File([window.files[name]], name),
      createWritable: async () => {
        let content = ''
        return {
          write: async (data: string | Blob) => void (content += typeof data === 'string' ? data : await data.text()),
          close: async () => void (window.files[name] = content),
          abort: async () => undefined,
        }
      },
    })
    Object.assign(window, {
      showOpenFilePicker: async () => {
        window.pickerCalls.push({ kind: 'open' })
        return [handle(window.nextOpen)]
      },
      showSaveFilePicker: async (options: { suggestedName?: string }) => {
        window.pickerCalls.push({ kind: 'save', suggestedName: options.suggestedName })
        return handle(window.nextSave)
      },
    })
  }, files)
}

// Feature F6 Files: R-UI-1, R-UI-16, R-UI-18, R-IO-1 … R-IO-7.
test('open a file, save it back, change the save format and save as a new file', async ({ app, page }) => {
  await fakeFileSystem(page, { 'shop.mmd': SHOP })
  await app.start()

  await page.evaluate(() => (window.nextOpen = 'shop.mmd'))
  await app.menu('File', 'Open…')
  await expect(app.tab('shop')).toHaveAttribute('aria-selected', 'true')
  await expect(app.shape('Order')).toBeVisible()
  await expect(app.shape('Customer')).toBeVisible()
  await expect(app.edges()).toHaveCount(1)
  await expect(app.status).toHaveText('Opened shop.mmd')

  await app.tool('Rectangle').click()
  await app.click({ x: 600, y: 500 })
  await expect(app.shape('Class')).toBeVisible()

  // Save writes back to the opened file without a dialog, in its format and with the layout.
  await page.keyboard.press('ControlOrMeta+s')
  await expect(app.status).toHaveText('Saved shop.mmd (Mermaid)')
  const saved = await page.evaluate(() => window.files['shop.mmd'])
  expect(saved).toMatch(/class (\w+)\["Class"\][\s\S]*%% kandinsky:layout \1 x=530 y=470 width=140 height=60/)
  expect(await page.evaluate(() => window.pickerCalls)).toEqual([{ kind: 'open' }])

  await app.menu('Settings', 'PlantUML')
  await expect(app.status).toHaveText('Default save format: PlantUML')

  // Save As always asks; the chosen file name becomes the whiteboard's name.
  await page.evaluate(() => (window.nextSave = 'orders.puml'))
  await app.menu('File', 'Save As…')
  await expect(app.status).toHaveText('Saved orders.puml (PlantUML)')
  await expect(app.tab('orders')).toHaveAttribute('aria-selected', 'true')
  expect(await page.evaluate(() => window.pickerCalls.at(-1))).toEqual({ kind: 'save', suggestedName: 'shop.puml' })
  const plantUml = await page.evaluate(() => window.files['orders.puml'])
  expect(plantUml).toMatch(/^@startuml/)
  expect(plantUml).toContain('Order --> Customer : placed by')

  // The default save format is kept across sessions.
  await page.reload()
  await page.locator('header.menubar').getByRole('button', { name: 'Settings' }).click()
  await expect(page.getByRole('menuitemradio', { name: 'PlantUML' })).toHaveAttribute('aria-checked', 'true')
})

test('without the File System Access API, files are opened with a file input and saved as downloads', async ({ app, page }) => {
  await page.addInitScript(() => {
    const w = window as unknown as Record<string, unknown>
    delete w.showOpenFilePicker
    delete w.showSaveFilePicker
  })
  await app.start()

  const chooser = page.waitForEvent('filechooser')
  await app.menu('File', 'Open…')
  await (await chooser).setFiles({ name: 'shop.mmd', mimeType: 'text/plain', buffer: Buffer.from(SHOP) })
  await expect(app.tab('shop')).toHaveAttribute('aria-selected', 'true')
  await expect(app.shape('Order')).toBeVisible()

  const download = page.waitForEvent('download')
  await app.menu('File', 'Save')
  const file = await download
  expect(file.suggestedFilename()).toBe('shop.mmd')
  const text = Buffer.concat(await (await file.createReadStream()).toArray()).toString()
  expect(text).toContain('Order --> Customer : placed by')
  expect(text).toContain('%% kandinsky:layout')
})
