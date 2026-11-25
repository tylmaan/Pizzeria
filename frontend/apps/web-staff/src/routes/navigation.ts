import { Roles, type Role } from '@pizzeria/api-client';

export type NavItem = {
  to: string;
  labelKey: 'nav.orders' | 'nav.kitchen' | 'nav.driver' | 'nav.locations' | 'nav.menu' | 'nav.staff';
  roles: readonly Role[];
};

/** Sections of the staff PWA and who can open them. The first allowed item is the user's start page. */
export const navItems: readonly NavItem[] = [
  { to: '/orders', labelKey: 'nav.orders', roles: [Roles.LocationManager, Roles.Staff] },
  { to: '/kitchen', labelKey: 'nav.kitchen', roles: [Roles.LocationManager, Roles.Staff, Roles.Kitchen] },
  { to: '/driver', labelKey: 'nav.driver', roles: [Roles.Driver] },
  { to: '/company/locations', labelKey: 'nav.locations', roles: [Roles.CompanyAdmin] },
  { to: '/company/menu', labelKey: 'nav.menu', roles: [Roles.CompanyAdmin] },
  { to: '/company/staff', labelKey: 'nav.staff', roles: [Roles.CompanyAdmin, Roles.LocationManager] },
];

export function navItemsFor(userRoles: readonly string[]): NavItem[] {
  return navItems.filter((item) => item.roles.some((role) => userRoles.includes(role)));
}
