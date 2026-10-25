import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactElement } from 'react'
import { MemoryRouter, Route, Routes } from 'react-router'

export function renderWithProviders(ui: ReactElement, { route = '/', path = '*' } = {}) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[route]}>
        <Routes>
          <Route path={path} element={ui} />
          {path !== '/login' && (
            <Route path="/login" element={<p>Pantalla de inicio de sesión</p>} />
          )}
          {path !== '/' && <Route path="/" element={<p>Pantalla de inicio</p>} />}
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}
