import type { Download } from '@playwright/test'
import { expect, test } from './fixtures'

async function content(download: Download) {
  const chunks = await (await download.createReadStream()).toArray()
  return Buffer.concat(chunks)
}

// Feature F8 Image export: R-IO-8.
test('export the whiteboard as SVG and PNG images', async ({ app, api, page }) => {
  // Without the File System Access API the images are downloaded, which Playwright can capture.
  await page.addInitScript(() => delete (window as unknown as Record<string, unknown>).showSaveFilePicker)
  const diagram = await api.create('Shop')
  const a = await api.addShape(diagram.id, { label: 'Order', x: 100, y: 100 })
  const b = await api.addShape(diagram.id, { label: 'Customer', x: 100, y: 300 })
  await api.addEdge(diagram.id, a.id, b.id, { label: 'placed by' })
  await app.open(diagram)

  const svgDownload = page.waitForEvent('download')
  await app.menu('File', 'Export as SVG…')
  const svgFile = await svgDownload
  expect(svgFile.suggestedFilename()).toBe('Shop.svg')
  const svg = (await content(svgFile)).toString()
  expect(svg).toMatch(/^(<\?xml[^>]*>\s*)?<svg[^>]+xmlns="http:\/\/www.w3.org\/2000\/svg"/)
  for (const label of ['Order', 'Customer', 'placed by']) {
    expect(svg).toMatch(new RegExp(`>${label}</(text|tspan)>`))
  }

  const pngDownload = page.waitForEvent('download')
  await app.menu('File', 'Export as PNG…')
  const pngFile = await pngDownload
  expect(pngFile.suggestedFilename()).toBe('Shop.png')
  const png = await content(pngFile)
  expect(png.subarray(1, 4).toString()).toBe('PNG')
  // The PNG has twice the resolution of the SVG.
  const svgWidth = Number(svg.match(/<svg[^>]* width="([\d.]+)"/)![1])
  expect(png.readUInt32BE(16)).toBe(Math.round(svgWidth * 2))

  await page.getByRole('button', { name: 'New whiteboard' }).click()
  await expect(app.tab('Untitled')).toHaveAttribute('aria-selected', 'true')
  await app.menu('File', 'Export as SVG…')
  await expect(app.status).toHaveText('The whiteboard is empty; there is nothing to export.')
})
