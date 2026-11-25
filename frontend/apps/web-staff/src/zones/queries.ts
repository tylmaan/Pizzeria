import { unwrap, type DeliveryCheckRequest, type SaveDeliveryZoneRequest } from '@pizzeria/api-client';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useServices } from '../app/services';

const zonesKey = (locationId: string) => ['delivery-zones', locationId] as const;

export function useZones(locationId: string) {
  const { api } = useServices();
  return useQuery({
    queryKey: zonesKey(locationId),
    queryFn: () =>
      unwrap(api.GET('/api/locations/{locationId}/delivery-zones', { params: { path: { locationId } } })),
  });
}

export function useSaveZone(locationId: string, id: string | undefined) {
  const { api } = useServices();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: SaveDeliveryZoneRequest) =>
      id
        ? unwrap(
            api.PUT('/api/locations/{locationId}/delivery-zones/{id}', { params: { path: { locationId, id } }, body }),
          )
        : unwrap(api.POST('/api/locations/{locationId}/delivery-zones', { params: { path: { locationId } }, body })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: zonesKey(locationId) }),
  });
}

export function useDeliveryCheck() {
  const { api } = useServices();
  return useMutation({
    mutationFn: (body: DeliveryCheckRequest) => unwrap(api.POST('/api/delivery/check', { body })),
  });
}
