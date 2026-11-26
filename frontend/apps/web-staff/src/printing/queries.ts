import { unwrap, type PrintSettingsDto } from '@pizzeria/api-client';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useServices } from '../app/services';

const agentsKey = (locationId: string) => ['print-agents', locationId] as const;
const jobsKey = (locationId: string) => ['print-jobs', locationId] as const;
const settingsKey = (locationId: string) => ['print-settings', locationId] as const;

// Agents report every 30 s; refreshing a bit more often keeps "online" honest without a live feed.
const STATUS_REFRESH_MS = 15_000;

export function usePrintAgents(locationId: string) {
  const { api } = useServices();
  return useQuery({
    queryKey: agentsKey(locationId),
    queryFn: () => unwrap(api.GET('/api/locations/{locationId}/print-agents', { params: { path: { locationId } } })),
    enabled: locationId !== '',
    refetchInterval: STATUS_REFRESH_MS,
  });
}

export function usePrintJobs(locationId: string) {
  const { api } = useServices();
  return useQuery({
    queryKey: jobsKey(locationId),
    queryFn: () => unwrap(api.GET('/api/locations/{locationId}/print-jobs', { params: { path: { locationId } } })),
    enabled: locationId !== '',
    refetchInterval: STATUS_REFRESH_MS,
  });
}

export function usePrintSettings(locationId: string) {
  const { api } = useServices();
  return useQuery({
    queryKey: settingsKey(locationId),
    queryFn: () => unwrap(api.GET('/api/locations/{locationId}/print-settings', { params: { path: { locationId } } })),
    enabled: locationId !== '',
  });
}

export function useRegisterAgent(locationId: string) {
  const { api } = useServices();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (name: string) =>
      unwrap(api.POST('/api/locations/{locationId}/print-agents', { params: { path: { locationId } }, body: { name } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: agentsKey(locationId) }),
  });
}

export function useRevokeAgent(locationId: string) {
  const { api } = useServices();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => unwrap(api.POST('/api/print-agents/{id}/revoke', { params: { path: { id } } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: agentsKey(locationId) }),
  });
}

export function useSavePrintSettings(locationId: string) {
  const { api } = useServices();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: PrintSettingsDto) =>
      unwrap(api.PUT('/api/locations/{locationId}/print-settings', { params: { path: { locationId } }, body })),
    onSuccess: (saved) => queryClient.setQueryData(settingsKey(locationId), saved),
  });
}

export function useRetryPrintJob(locationId: string) {
  const { api } = useServices();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => unwrap(api.POST('/api/print-jobs/{id}/retry', { params: { path: { id } } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: jobsKey(locationId) }),
  });
}

export function useReprintTicket() {
  const { api } = useServices();
  return useMutation({
    mutationFn: (orderId: string) => unwrap(api.POST('/api/orders/{orderId}/reprint', { params: { path: { orderId } } })),
  });
}
