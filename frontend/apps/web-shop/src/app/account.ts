import { unwrap, type RegisterCustomerRequest, type SaveAddressRequest } from '@pizzeria/api-client';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useSyncExternalStore } from 'react';
import { useServices } from './services';

const accountKey = ['account'] as const;

/** Whether a customer is signed in (re-renders on sign-in and sign-out). */
export function useSignedIn(): boolean {
  const { session } = useServices();
  return useSyncExternalStore(session.subscribe, () => session.isSignedIn);
}

export function useLogin() {
  const { session, queryClient } = useServices();
  return useMutation({
    mutationFn: (body: { email: string; password: string }) => session.signIn('/api/shop/account/login', body),
    onSuccess: () => queryClient.removeQueries({ queryKey: accountKey }),
  });
}

export function useRegister() {
  const { session, queryClient } = useServices();
  return useMutation({
    mutationFn: (body: RegisterCustomerRequest) => session.signIn('/api/shop/account/register', body),
    onSuccess: () => queryClient.removeQueries({ queryKey: accountKey }),
  });
}

export function useLogout() {
  const { session, queryClient } = useServices();
  return () => {
    session.logout();
    queryClient.removeQueries({ queryKey: accountKey });
  };
}

export function useProfile() {
  const { api } = useServices();
  return useQuery({
    queryKey: [...accountKey, 'profile'],
    queryFn: () => unwrap(api.GET('/api/shop/account')),
    enabled: useSignedIn(),
  });
}

export function useUpdateProfile() {
  const { api, queryClient } = useServices();
  return useMutation({
    mutationFn: (body: { name: string; phone: string | null }) => unwrap(api.PUT('/api/shop/account', { body })),
    onSuccess: (profile) => queryClient.setQueryData([...accountKey, 'profile'], profile),
  });
}

export function useAddresses() {
  const { api } = useServices();
  return useQuery({
    queryKey: [...accountKey, 'addresses'],
    queryFn: () => unwrap(api.GET('/api/shop/account/addresses')),
    enabled: useSignedIn(),
  });
}

export function useAddAddress() {
  const { api, queryClient } = useServices();
  return useMutation({
    mutationFn: (body: SaveAddressRequest) => unwrap(api.POST('/api/shop/account/addresses', { body })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [...accountKey, 'addresses'] }),
  });
}

export function useDeleteAddress() {
  const { api, queryClient } = useServices();
  return useMutation({
    mutationFn: (id: string) => unwrap(api.DELETE('/api/shop/account/addresses/{id}', { params: { path: { id } } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [...accountKey, 'addresses'] }),
  });
}

export function useMyOrders() {
  const { api } = useServices();
  return useQuery({
    queryKey: [...accountKey, 'orders'],
    queryFn: () => unwrap(api.GET('/api/shop/account/orders')),
    enabled: useSignedIn(),
  });
}

/** Deletes the account (orders stay with the restaurant) and signs out. */
export function useDeleteAccount() {
  const { api } = useServices();
  const logout = useLogout();
  return useMutation({
    mutationFn: () => unwrap(api.DELETE('/api/shop/account')),
    onSuccess: logout,
  });
}
