import { useCallback } from 'react'
import { useInfiniteQuery, useQueryClient, type InfiniteData } from '@tanstack/react-query'
import { listCaptures } from '../api/captures'
import type { CaptureFilters, CapturedRequestSummary, TrafficCaptureEvent } from '../types/api'
import type { CaptureListResponse } from '../types/api'

const PAGE_SIZE = 200

export function useCaptures(filters: CaptureFilters) {
  const queryClient = useQueryClient()
  const filtersKey = JSON.stringify(filters)
  const queryKey = ['captures', filtersKey]

  const query = useInfiniteQuery({
    queryKey,
    queryFn: ({ pageParam }) =>
      listCaptures({ ...filters, page: pageParam as number, pageSize: PAGE_SIZE }),
    initialPageParam: 1,
    getNextPageParam: (lastPage: CaptureListResponse) =>
      lastPage.page < lastPage.pages ? lastPage.page + 1 : undefined,
    staleTime: 0,
  })

  const captures = query.data?.pages.flatMap((p) => p.items) ?? []
  const firstPage = query.data?.pages[0]
  const lastPage = query.data?.pages[query.data.pages.length - 1]
  const total = firstPage?.total ?? 0

  const loadMore = useCallback(() => {
    if (!query.isFetchingNextPage && query.hasNextPage) {
      void query.fetchNextPage()
    }
  }, [query])

  const prependCapture = useCallback(
    (event: TrafficCaptureEvent) => {
      const partial: CapturedRequestSummary = {
        ...event,
        requestHeaders: '',
        responseHeaders: '',
        tlsCipherSuite: '',
        isModified: false,
        notes: '',
        device: undefined,
      }
      queryClient.setQueryData(
        queryKey,
        (old: InfiniteData<CaptureListResponse> | undefined) => {
          if (!old || old.pages.length === 0) return old
          return {
            ...old,
            pages: [
              {
                ...old.pages[0],
                items: [partial, ...old.pages[0].items],
                total: old.pages[0].total + 1,
              },
              ...old.pages.slice(1),
            ],
          }
        },
      )
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [queryClient, filtersKey],
  )

  return {
    captures,
    total,
    page: lastPage?.page ?? 1,
    pages: lastPage?.pages ?? 1,
    loading: query.isLoading,
    loadingMore: query.isFetchingNextPage,
    error: query.error instanceof Error ? query.error.message : null,
    loadMore,
    prependCapture,
    hasMore: query.hasNextPage ?? false,
  }
}
