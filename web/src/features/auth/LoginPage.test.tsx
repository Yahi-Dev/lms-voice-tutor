import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { renderWithProviders } from '@/test/render'
import { apiUrl, problem, server } from '@/test/server'
import { LoginPage } from './LoginPage'

describe('LoginPage', () => {
  beforeEach(() => {
    server.use(http.get(apiUrl('/auth/me'), () => problem(401, 'unauthorized')))
  })

  it('shows a generic message for wrong credentials', async () => {
    server.use(http.post(apiUrl('/auth/login'), () => problem(401, 'invalid_credentials')))
    const user = userEvent.setup()
    renderWithProviders(<LoginPage />, { route: '/login', path: '/login' })

    await user.type(await screen.findByLabelText('Correo'), 'ana@demo.local')
    await user.type(screen.getByLabelText('Contraseña'), 'incorrecta')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByText('Correo o contraseña incorrectos.')).toBeInTheDocument()
  })

  it('fills in a demo account', async () => {
    const user = userEvent.setup()
    renderWithProviders(<LoginPage />, { route: '/login', path: '/login' })

    await user.click(await screen.findByRole('button', { name: /Ana \(con acceso al curso\)/ }))

    expect(screen.getByLabelText('Correo')).toHaveValue('ana@demo.local')
  })

  it('signs in and leaves the login screen', async () => {
    server.use(
      http.post(apiUrl('/auth/login'), () =>
        HttpResponse.json({ id: 'u1', email: 'ana@demo.local', displayName: 'Ana Gómez' }),
      ),
    )
    const user = userEvent.setup()
    renderWithProviders(<LoginPage />, { route: '/login', path: '/login' })

    await user.type(await screen.findByLabelText('Correo'), 'ana@demo.local')
    await user.type(screen.getByLabelText('Contraseña'), 'Ana-Demo-2026!')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByText('Pantalla de inicio')).toBeInTheDocument()
  })
})
