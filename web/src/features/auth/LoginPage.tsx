import { type FormEvent, useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { ApiError } from '@/shared/api/client'
import { Alert, AlertDescription } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card'
import { Input } from '@/shared/ui/input'
import { Label } from '@/shared/ui/label'
import { useLogin, useSession } from './useSession'

const DEMO_ACCOUNTS = [
  { label: 'Ana (con acceso al curso)', email: 'ana@demo.local', password: 'Ana-Demo-2026!' },
  { label: 'Bruno (sin acceso)', email: 'bruno@demo.local', password: 'Bruno-Demo-2026!' },
]

/** Only same-app paths are accepted as a post-login destination. */
function safeRedirect(from: unknown): string {
  return typeof from === 'string' && from.startsWith('/') && !from.startsWith('//') ? from : '/'
}

export function LoginPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const session = useSession()
  const loginMutation = useLogin()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  const destination = safeRedirect((location.state as { from?: unknown } | null)?.from)

  if (session.data) return <Navigate to={destination} replace />

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault()
    loginMutation.mutate(
      { email, password },
      { onSuccess: () => navigate(destination, { replace: true }) },
    )
  }

  const errorMessage = loginErrorMessage(loginMutation.error)

  return (
    <main className="flex min-h-screen items-center justify-center bg-muted/40 px-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>
            <h1 className="text-xl font-semibold">Tutor virtual</h1>
          </CardTitle>
          <CardDescription>Inicia sesión para ver tus lecciones.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-5">
          <form className="space-y-4" onSubmit={handleSubmit} noValidate>
            <div className="space-y-2">
              <Label htmlFor="email">Correo</Label>
              <Input
                id="email"
                type="email"
                autoComplete="username"
                required
                value={email}
                onChange={(event) => setEmail(event.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="password">Contraseña</Label>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                required
                value={password}
                onChange={(event) => setPassword(event.target.value)}
              />
            </div>
            {errorMessage && (
              <Alert variant="destructive">
                <AlertDescription>{errorMessage}</AlertDescription>
              </Alert>
            )}
            <Button type="submit" className="w-full" disabled={loginMutation.isPending}>
              {loginMutation.isPending ? 'Entrando…' : 'Entrar'}
            </Button>
          </form>

          <section aria-labelledby="demo-accounts" className="space-y-2 border-t pt-4">
            <h2 id="demo-accounts" className="text-sm font-medium text-muted-foreground">
              Cuentas de prueba
            </h2>
            <div className="flex flex-col gap-2">
              {DEMO_ACCOUNTS.map((account) => (
                <Button
                  key={account.email}
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    setEmail(account.email)
                    setPassword(account.password)
                  }}
                >
                  Usar {account.label}
                </Button>
              ))}
            </div>
          </section>
        </CardContent>
      </Card>
    </main>
  )
}

function loginErrorMessage(error: Error | null): string | null {
  if (!error) return null
  if (error instanceof ApiError) {
    if (error.kind === 'unauthorized' || error.kind === 'validation')
      return 'Correo o contraseña incorrectos.'
    if (error.kind === 'rate_limited')
      return 'Demasiados intentos. Espera un minuto e inténtalo de nuevo.'
    if (error.kind === 'network' || error.kind === 'timeout')
      return 'No hay conexión con el servidor.'
  }
  return 'No se pudo iniciar sesión. Inténtalo de nuevo.'
}
