import { useQuery } from '@tanstack/react-query'
import { Link, Outlet, useNavigate } from 'react-router'
import { useLogout, useSession } from '@/features/auth/useSession'
import { fetchSystemStatus, systemStatusQueryKey } from '@/features/courses/api'
import { useOnlineStatus } from '@/shared/hooks/useOnlineStatus'
import { Button } from '@/shared/ui/button'

export function AppLayout() {
  const navigate = useNavigate()
  const session = useSession()
  const logout = useLogout()
  const online = useOnlineStatus()
  const status = useQuery({
    queryKey: systemStatusQueryKey,
    queryFn: ({ signal }) => fetchSystemStatus(signal),
    staleTime: Infinity,
  })

  return (
    <div className="min-h-screen bg-background">
      <header className="sticky top-0 z-10 border-b bg-background">
        <div className="mx-auto flex h-14 max-w-6xl items-center justify-between gap-4 px-4">
          <Link to="/" className="font-semibold">
            Tutor virtual
          </Link>
          <div className="flex items-center gap-3 text-sm">
            <span className="hidden text-muted-foreground sm:inline">
              {session.data?.displayName}
            </span>
            <Button
              variant="outline"
              size="sm"
              disabled={logout.isPending}
              onClick={() =>
                logout.mutate(undefined, { onSettled: () => navigate('/login', { replace: true }) })
              }
            >
              Cerrar sesión
            </Button>
          </div>
        </div>
        {!online && (
          <p
            role="status"
            className="bg-amber-100 px-4 py-2 text-center text-sm text-amber-900 dark:bg-amber-900/40 dark:text-amber-100"
          >
            Sin conexión. Lo que escribas se conserva y podrás enviarlo cuando vuelva la red.
          </p>
        )}
        {status.data?.demoMode && (
          <p className="bg-sky-50 px-4 py-1.5 text-center text-xs text-sky-900 dark:bg-sky-900/30 dark:text-sky-100">
            Modo demo: el tutor y la transcripción usan respuestas simuladas porque no hay una llave
            de proveedor configurada.
          </p>
        )}
      </header>
      <main className="mx-auto max-w-6xl px-4 py-6">
        <Outlet />
      </main>
    </div>
  )
}
