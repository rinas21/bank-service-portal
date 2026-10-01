import { useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import {
  BarChart3,
  Building2,
  ClipboardList,
  FileUp,
  LayoutDashboard,
  LogOut,
  Menu,
  Plus,
  ScrollText,
  Users,
  X,
} from 'lucide-react'
import { useAuth } from '@/context/authContext'

interface NavItem {
  to: string
  label: string
  icon: typeof LayoutDashboard
  roles?: string[]
}

const NAV_ITEMS: NavItem[] = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/requests', label: 'Requests', icon: ClipboardList },
  { to: '/requests/new', label: 'New request', icon: Plus },
  { to: '/users', label: 'Users', icon: Users, roles: ['Admin'] },
  { to: '/branches', label: 'Branches', icon: Building2, roles: ['Admin', 'Manager'] },
  { to: '/audit-logs', label: 'Audit logs', icon: ScrollText, roles: ['Admin', 'Manager'] },
  { to: '/migration', label: 'CSV migration', icon: FileUp, roles: ['Admin'] },
]

function isVisible(item: NavItem, roles: string[]) {
  return !item.roles || item.roles.some((role) => roles.includes(role))
}

export default function AppLayout() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [isMenuOpen, setIsMenuOpen] = useState(false)

  const roles = user?.roles ?? []
  const visibleItems = NAV_ITEMS.filter((item) => isVisible(item, roles))

  function closeMenu() {
    setIsMenuOpen(false)
  }

  function handleLogout() {
    logout()
    navigate('/login', { replace: true })
  }

  const sidebar = (
    <div className="flex h-full flex-col gap-6 p-4">
      <div className="flex items-center gap-3 px-2 pt-2">
        <span className="flex size-9 items-center justify-center rounded-lg bg-brand-600 text-white">
          <BarChart3 className="size-5" aria-hidden="true" />
        </span>
        <div className="leading-tight">
          <p className="text-sm font-bold text-white">Bank Service</p>
          <p className="text-xs text-brand-200">Service Portal</p>
        </div>
      </div>

      <nav className="flex-1 space-y-1" aria-label="Main navigation">
        {visibleItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.to === '/'}
            onClick={closeMenu}
            className={({ isActive }) =>
              `flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors ${
                isActive
                  ? 'bg-brand-600 text-white'
                  : 'text-brand-100 hover:bg-brand-800 hover:text-white'
              }`
            }
          >
            <item.icon className="size-4.5 shrink-0" aria-hidden="true" />
            {item.label}
          </NavLink>
        ))}
      </nav>

      <div className="rounded-lg bg-brand-950/60 p-3">
        <p className="truncate text-sm font-semibold text-white">{user?.fullName}</p>
        <p className="truncate text-xs text-brand-200">{user?.email}</p>
        <div className="mt-2 flex flex-wrap gap-1">
          {roles.map((role) => (
            <span key={role} className="rounded bg-brand-700 px-1.5 py-0.5 text-[10px] font-semibold text-brand-100">
              {role}
            </span>
          ))}
        </div>
        <button
          type="button"
          onClick={handleLogout}
          className="mt-3 flex w-full cursor-pointer items-center justify-center gap-2 rounded-lg border border-brand-700 px-3 py-2 text-sm font-semibold text-brand-100 hover:bg-brand-800"
        >
          <LogOut className="size-4" aria-hidden="true" />
          Sign out
        </button>
      </div>
    </div>
  )

  return (
    <div className="min-h-screen lg:flex">
      <a
        href="#main-content"
        className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50 focus:rounded focus:bg-white focus:px-4 focus:py-2 focus:shadow"
      >
        Skip to main content
      </a>

      {/* Desktop sidebar */}
      <aside className="hidden w-64 shrink-0 bg-ink-900 lg:block">
        <div className="sticky top-0 h-screen">{sidebar}</div>
      </aside>

      {/* Mobile header + drawer */}
      <header className="sticky top-0 z-30 flex items-center justify-between gap-3 border-b border-ink-200 bg-white px-4 py-3 lg:hidden">
        <div className="flex items-center gap-2">
          <span className="flex size-8 items-center justify-center rounded-lg bg-brand-600 text-white">
            <BarChart3 className="size-4" aria-hidden="true" />
          </span>
          <span className="text-sm font-bold text-ink-900">Bank Service Portal</span>
        </div>
        <button
          type="button"
          className="btn-secondary p-2"
          onClick={() => setIsMenuOpen((open) => !open)}
          aria-expanded={isMenuOpen}
          aria-label={isMenuOpen ? 'Close navigation menu' : 'Open navigation menu'}
        >
          {isMenuOpen ? <X className="size-5" /> : <Menu className="size-5" />}
        </button>
      </header>

      {isMenuOpen && (
        <div className="fixed inset-0 top-[57px] z-20 lg:hidden">
          <button
            type="button"
            className="absolute inset-0 bg-ink-950/40"
            aria-label="Close navigation menu"
            onClick={() => setIsMenuOpen(false)}
          />
          <nav className="relative h-full w-72 max-w-[85%] overflow-y-auto bg-ink-900" aria-label="Main navigation">
            {sidebar}
          </nav>
        </div>
      )}

      <main id="main-content" className="min-w-0 flex-1">
        <div className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
          <Outlet />
        </div>
      </main>
    </div>
  )
}
