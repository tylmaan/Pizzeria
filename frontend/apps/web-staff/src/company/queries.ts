import {
  unwrap,
  type CreateLocationRequest,
  type CreateUserRequest,
  type UpdateLocationRequest,
} from '@pizzeria/api-client';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useServices } from '../app/services';

export const queryKeys = {
  locations: ['locations'] as const,
  location: (id: string) => ['locations', id] as const,
  staff: ['staff'] as const,
  assignableRoles: ['assignable-roles'] as const,
};

export function useLocations() {
  const { api } = useServices();
  return useQuery({ queryKey: queryKeys.locations, queryFn: () => unwrap(api.GET('/api/locations')) });
}

export function useLocation(id: string) {
  const { api } = useServices();
  return useQuery({
    queryKey: queryKeys.location(id),
    queryFn: () => unwrap(api.GET('/api/locations/{id}', { params: { path: { id } } })),
  });
}

export function useCreateLocation() {
  const { api } = useServices();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateLocationRequest) => unwrap(api.POST('/api/locations', { body })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.locations }),
  });
}

export function useUpdateLocation(id: string) {
  const { api } = useServices();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdateLocationRequest) =>
      unwrap(api.PUT('/api/locations/{id}', { params: { path: { id } }, body })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.locations }),
  });
}

export function useStaff() {
  const { api } = useServices();
  return useQuery({ queryKey: queryKeys.staff, queryFn: () => unwrap(api.GET('/api/users')) });
}

export function useAssignableRoles() {
  const { api } = useServices();
  return useQuery({
    queryKey: queryKeys.assignableRoles,
    queryFn: () => unwrap(api.GET('/api/users/assignable-roles')),
    staleTime: Infinity,
  });
}

export function useCreateStaff() {
  const { api } = useServices();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateUserRequest) => unwrap(api.POST('/api/users', { body })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.staff }),
  });
}
