export type UserRole = 'Driver' | 'CargoOwner' | 'Logist' | 'Admin';
export type PostType = 'Cargo' | 'Transport' | 'Dogruz';

export interface User {
  id: number;
  telegramId: number;
  username?: string;
  firstName: string;
  lastName?: string;
  phoneNumber?: string;
  balance: number;
  role: UserRole;
  isVip: boolean;
  isPremium: boolean;
  isBlocked: boolean;
  isOnboarded: boolean;
  totalPosts: number;
  activeSubscription?: Subscription;
}

export interface Subscription {
  id: number;
  tariffId: number;
  tariffName: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
  daysLeft: number;
}

export interface Tariff {
  id: number;
  name: string;
  description?: string;
  price: number;
  durationDays: number;
  isActive: boolean;
  isRecommended: boolean;
  maxGroups: number;
  postsPerDay: number;
  maxAccounts: number;
  features: string[];
}

export interface Post {
  id: number;
  userId: number;
  userFullName: string;
  userRole: UserRole;
  postType: PostType;
  fromCity: string;
  toCity: string;
  fromLat?: number;
  fromLng?: number;
  toLat?: number;
  toLng?: number;
  cargoType?: string;
  weight?: string;
  vehicleType?: string;
  price?: string;
  contactPhone: string;
  description?: string;
  isVerified: boolean;
  viewCount: number;
  createdAt: string;
  expiresAt: string;
}

export interface Card {
  id: number;
  cardNumber: string;
  cardHolder: string;
  bankName: string;
  isActive: boolean;
}

export interface PaginatedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  hasMore: boolean;
}
