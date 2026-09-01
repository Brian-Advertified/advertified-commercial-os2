import type { AnchorHTMLAttributes } from 'react'
import { Link as RouterLink } from 'react-router-dom'

type LinkProps = Omit<AnchorHTMLAttributes<HTMLAnchorElement>, 'href'> & {
  href: string
}

export function Link({ href, ...props }: LinkProps) {
  return <RouterLink to={href} {...props} />
}
