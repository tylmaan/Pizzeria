/** Role names as issued by the API (mirror of `Pizzeria.Domain.Identity.Roles`). */
export const Roles = {
  PlatformAdmin: 'PlatformAdmin',
  CompanyAdmin: 'CompanyAdmin',
  LocationManager: 'LocationManager',
  Staff: 'Staff',
  Kitchen: 'Kitchen',
  Driver: 'Driver',
  Customer: 'Customer',
} as const;

export type Role = (typeof Roles)[keyof typeof Roles];
