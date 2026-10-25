import { expect, type Page, test } from '@playwright/test'

const ana = { email: 'ana@demo.local', password: 'Ana-Demo-2026!' }
const bruno = { email: 'bruno@demo.local', password: 'Bruno-Demo-2026!' }

async function login(page: Page, account: { email: string; password: string }) {
  await page.goto('/login')
  await page.getByLabel('Correo').fill(account.email)
  await page.getByLabel('Contraseña').fill(account.password)
  await page.getByRole('button', { name: 'Entrar' }).click()
  await expect(page.getByRole('heading', { name: 'Mis cursos' })).toBeVisible()
}

const conversation = (page: Page) => page.getByRole('list', { name: 'Conversación con el tutor' })

async function openFirstLesson(page: Page) {
  await page.getByRole('link', { name: /Métodos HTTP/ }).click()
  await expect(page.getByRole('heading', { level: 1, name: 'Métodos HTTP' })).toBeVisible()
}

test.beforeEach(({ page }) => {
  // The production build runs under the real Content-Security-Policy; any violation fails the test.
  page.on('console', (message) => {
    if (message.type() === 'error' && message.text().includes('Content Security Policy')) {
      throw new Error(`CSP violation: ${message.text()}`)
    }
  })
})

test('an enrolled student asks by text and finds the conversation after reloading', async ({
  page,
}) => {
  await login(page, ana)
  await openFirstLesson(page)

  await page.getByRole('textbox', { name: 'Tu pregunta' }).fill('¿Para qué sirve POST?')
  await page.getByRole('button', { name: 'Enviar' }).click()

  await expect(
    conversation(page).getByText(/POST sirve para enviar datos al servidor/),
  ).toBeVisible()
  await expect(
    page.getByRole('button', { name: /Ver la sección «POST: crear recursos»/ }),
  ).toBeVisible()

  await page.reload()
  await expect(
    conversation(page).getByText(/POST sirve para enviar datos al servidor/),
  ).toBeVisible()
})

test('an enrolled student asks by voice and reviews the transcript before sending', async ({
  page,
}) => {
  await login(page, ana)
  await openFirstLesson(page)

  await page.getByRole('button', { name: 'Grabar pregunta' }).click()
  await expect(page.getByText('Grabando tu pregunta…')).toBeVisible()
  await page.waitForTimeout(1_500)
  await page.getByRole('button', { name: 'Detener grabación' }).click()

  const question = page.getByRole('textbox', { name: 'Tu pregunta' })
  await expect(question).toHaveValue('¿Para qué sirve POST?')
  await expect(page.getByText('Revisa la transcripción')).toBeVisible()

  await page.getByRole('button', { name: 'Enviar' }).click()
  await expect(page.getByText('Pregunta por voz:').last()).toBeAttached()
  await expect(page.getByText(/Según la sección «POST: crear recursos»/).last()).toBeVisible()
})

test('a question outside the lesson is reported as not covered', async ({ page }) => {
  await login(page, ana)
  await openFirstLesson(page)

  await page
    .getByRole('textbox', { name: 'Tu pregunta' })
    .fill('¿Cómo despliego mi aplicación en Kubernetes?')
  await page.keyboard.press('Enter')

  await expect(page.getByText('La lección no cubre esta pregunta').last()).toBeVisible()
})

test('a student without access cannot open the lesson nor read its messages', async ({
  page,
  browser,
}) => {
  await login(page, ana)
  await openFirstLesson(page)
  const lessonPath = new URL(page.url()).pathname

  const otherStudent = await browser.newContext()
  const brunoPage = await otherStudent.newPage()
  await login(brunoPage, bruno)
  await expect(brunoPage.getByText('Aún no tienes cursos asignados.')).toBeVisible()

  await brunoPage.goto(lessonPath)
  await expect(brunoPage.getByText('No tienes acceso a esta lección')).toBeVisible()

  const messages = await brunoPage.request.get(`/api${lessonPath}/messages`)
  expect(messages.status()).toBe(403)
  await otherStudent.close()
})

test('the draft survives losing the connection', async ({ page, context }) => {
  await login(page, ana)
  await openFirstLesson(page)

  await context.setOffline(true)
  await expect(page.getByText(/Sin conexión/)).toBeVisible()
  await page.getByRole('textbox', { name: 'Tu pregunta' }).fill('¿Qué hace DELETE?')
  await expect(page.getByRole('button', { name: 'Enviar' })).toBeDisabled()

  await context.setOffline(false)
  await page.getByRole('button', { name: 'Enviar' }).click()
  await expect(page.getByText(/Según la sección «DELETE: eliminar recursos»/)).toBeVisible()
})
