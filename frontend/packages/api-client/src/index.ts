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
export type FulfillmentType = Schemas['FulfillmentType'];
export type OrderItemInput = Schemas['OrderItemInput'];
export type OrderStatus = Schemas['OrderStatus'];
export type OrderTrackingDto = Schemas['OrderTrackingDto'];
export type PaymentMethod = Schemas['PaymentMethod'];
export type PlacedOrderDto = Schemas['PlacedOrderDto'];
export type PlaceOrderRequest = Schemas['PlaceOrderRequest'];
export type ShopDto = Schemas['ShopDto'];
export type StaffOrderDto = Schemas['StaffOrderDto'];
export type OrderListScope = Schemas['OrderListScope'];
export type DriverDto = Schemas['DriverDto'];
export type DriverShiftDto = Schemas['DriverShiftDto'];
export type DriverShiftStatus = Schemas['DriverShiftStatus'];
export type CurrentShiftDto = Schemas['CurrentShiftDto'];
export type DriverPositionDto = Schemas['DriverPositionDto'];
export type ReportDto = Schemas['ReportDto'];
export type MeResponse = Schemas['MeResponse'];
export type UpdateLocationRequest = Schemas['UpdateLocationRequest'];
export type UserDto = Schemas['UserDto'];

export { AuthSession, localStorageTokenStore, type AuthSessionOptions, type RefreshTokenStore } from './auth';
export { createApiClient, unwrap, type ApiClient, type ApiClientOptions } from './client';
export { ApiError, errorCodeOf, NETWORK_ERROR_CODE, UNEXPECTED_ERROR_CODE, type ProblemDetails } from './errors';
export {
  allowedGroups,
  lowestPrice,
  previewUnitPrice,
  priceAt,
  selectionIsComplete,
  type ItemChoice,
} from './menuPreview';
export { Roles, type Role } from './roles';
