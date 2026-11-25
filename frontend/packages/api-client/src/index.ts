import type { components } from './schema';

// Request/response types are generated from the backend OpenAPI document: `pnpm generate:api`
// (needs the API running on :5080). Never write DTOs by hand — alias the generated ones here.
export type { components, paths } from './schema';

type Schemas = components['schemas'];
export type AddressDto = Schemas['AddressDto'];
export type AssignableRoleDto = Schemas['AssignableRoleDto'];
export type CategoryDto = Schemas['CategoryDto'];
export type HalfAndHalfPricing = Schemas['HalfAndHalfPricing'];
export type MenuDto = Schemas['MenuDto'];
export type ModifierDto = Schemas['ModifierDto'];
export type ModifierGroupDto = Schemas['ModifierGroupDto'];
export type PriceDto = Schemas['PriceDto'];
export type ProductDto = Schemas['ProductDto'];
export type SaveCategoryRequest = Schemas['SaveCategoryRequest'];
export type SaveModifierGroupRequest = Schemas['SaveModifierGroupRequest'];
export type SaveProductRequest = Schemas['SaveProductRequest'];
export type CreateLocationRequest = Schemas['CreateLocationRequest'];
export type CreateUserRequest = Schemas['CreateUserRequest'];
export type DeliveryCheckRequest = Schemas['DeliveryCheckRequest'];
export type DeliveryQuoteDto = Schemas['DeliveryQuoteDto'];
export type DeliveryZoneDto = Schemas['DeliveryZoneDto'];
export type GeoPointDto = Schemas['GeoPointDto'];
export type SaveDeliveryZoneRequest = Schemas['SaveDeliveryZoneRequest'];
export type LocationDto = Schemas['LocationDto'];
export type MeResponse = Schemas['MeResponse'];
export type UpdateLocationRequest = Schemas['UpdateLocationRequest'];
export type UserDto = Schemas['UserDto'];

export { AuthSession, localStorageTokenStore, type AuthSessionOptions, type RefreshTokenStore } from './auth';
export { createApiClient, unwrap, type ApiClient, type ApiClientOptions } from './client';
export { ApiError, errorCodeOf, NETWORK_ERROR_CODE, UNEXPECTED_ERROR_CODE, type ProblemDetails } from './errors';
export { Roles, type Role } from './roles';
