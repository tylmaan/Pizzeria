import type { components } from './schema';

// Request/response types are generated from the backend OpenAPI document: `pnpm generate:api`
// (needs the API running on :5080). Never write DTOs by hand — alias the generated ones here.
export type { components, paths } from './schema';

type Schemas = components['schemas'];
export type AddressDto = Schemas['AddressDto'];
export type AssignableRoleDto = Schemas['AssignableRoleDto'];
export type CreateLocationRequest = Schemas['CreateLocationRequest'];
export type CreateUserRequest = Schemas['CreateUserRequest'];
export type LocationDto = Schemas['LocationDto'];
export type MeResponse = Schemas['MeResponse'];
export type UpdateLocationRequest = Schemas['UpdateLocationRequest'];
export type UserDto = Schemas['UserDto'];

export { AuthSession, localStorageTokenStore, type AuthSessionOptions, type RefreshTokenStore } from './auth';
export { createApiClient, unwrap, type ApiClient, type ApiClientOptions } from './client';
export { ApiError, errorCodeOf, NETWORK_ERROR_CODE, UNEXPECTED_ERROR_CODE, type ProblemDetails } from './errors';
export { Roles, type Role } from './roles';
