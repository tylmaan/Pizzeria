import {
  unwrap,
  type HalfAndHalfPricing,
  type SaveCategoryRequest,
  type SaveModifierGroupRequest,
  type SaveProductRequest,
} from '@pizzeria/api-client';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useServices } from '../app/services';

const menuKey = ['menu'] as const;

/** The whole company menu; edit forms read their entity from it, so there is a single cache entry to refresh. */
export function useMenu() {
  const { api } = useServices();
  return useQuery({ queryKey: menuKey, queryFn: () => unwrap(api.GET('/api/menu')) });
}

function useMenuMutation<TVariables, TResult>(mutationFn: (variables: TVariables) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: menuKey }),
  });
}

export function useSaveMenuSettings() {
  const { api } = useServices();
  return useMenuMutation((halfAndHalfPricing: HalfAndHalfPricing) =>
    unwrap(api.PUT('/api/menu/settings', { body: { halfAndHalfPricing } })),
  );
}

export function useSaveCategory(id: string | undefined) {
  const { api } = useServices();
  return useMenuMutation((body: SaveCategoryRequest) =>
    id
      ? unwrap(api.PUT('/api/menu/categories/{id}', { params: { path: { id } }, body }))
      : unwrap(api.POST('/api/menu/categories', { body })),
  );
}

export function useSaveProduct(id: string | undefined) {
  const { api } = useServices();
  return useMenuMutation((body: SaveProductRequest) =>
    id
      ? unwrap(api.PUT('/api/menu/products/{id}', { params: { path: { id } }, body }))
      : unwrap(api.POST('/api/menu/products', { body })),
  );
}

export function useSaveModifierGroup(id: string | undefined) {
  const { api } = useServices();
  return useMenuMutation((body: SaveModifierGroupRequest) =>
    id
      ? unwrap(api.PUT('/api/menu/modifier-groups/{id}', { params: { path: { id } }, body }))
      : unwrap(api.POST('/api/menu/modifier-groups', { body })),
  );
}
