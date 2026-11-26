import { unwrap, type DriverShiftStatus } from '@pizzeria/api-client';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useServices } from '../app/services';

const currentKey = ['shift', 'current'] as const;

export function useCurrentShift() {
  const { api } = useServices();
  return useQuery({ queryKey: currentKey, queryFn: () => unwrap(api.GET('/api/driver/shifts/current')) });
}

export function useStartShift() {
  const { api } = useServices();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { locationId: string; startingCash: number }) => unwrap(api.POST('/api/driver/shifts', { body })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: currentKey }),
  });
}

export function useCloseShift() {
  const { api } = useServices();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (tips: number) => unwrap(api.POST('/api/driver/shifts/current/close', { body: { tips } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: currentKey }),
  });
}

const shiftsKey = (locationId: string) => ['shifts', locationId] as const;

export function useLocationShifts(locationId: string, status: DriverShiftStatus) {
  const { api } = useServices();
  return useQuery({
    queryKey: [...shiftsKey(locationId), status],
    queryFn: () => unwrap(api.GET('/api/locations/{locationId}/shifts', { params: { path: { locationId }, query: { status } } })),
    enabled: locationId !== '',
  });
}

export function useSettleShift(locationId: string) {
  const { api } = useServices();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, countedCash, comment }: { id: string; countedCash: number; comment: string | null }) =>
      unwrap(api.POST('/api/shifts/{id}/settle', { params: { path: { id } }, body: { countedCash, comment } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: shiftsKey(locationId) }),
  });
}
