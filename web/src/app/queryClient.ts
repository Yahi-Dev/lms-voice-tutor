import { QueryCache, QueryClient } from '@tanstack/react-query'
import { sessionQueryKey } from '@/features/auth/api'
import { ApiError } from '@/shared/api/client'

export function createQueryClient(): QueryClient {
  const queryClient: QueryClient = new QueryClient({
    queryCache: new QueryCache({
      onError: (error) => {
        // An expired session anywhere sends the student back to the login screen.
        if (error instanceof ApiError && error.kind === 'unauthorized') {
          queryClient.setQueryData(sessionQueryKey, null)
        }
      },
    }),
    defaultOptions: {
      queries: {
        // 4xx answers (no access, not found) are final: retrying would only delay the message.
        retry: (failureCount, error) =>
          error instanceof ApiError && error.retryable && failureCount < 2,
        refetchOnWindowFocus: false,
      },
    },
  })

  return queryClient
}
